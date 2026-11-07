param([string]$Unity='D:/Unity/Unity/Hub/Editor/6000.0.40f1/Editor/Unity.exe')
$ErrorActionPreference='Stop'
$project=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$isolated=Join-Path $project 'Temp/CustomerDrawerCheckProject'
foreach($dir in @('Assets/Plugins','Assets/Editor','Assets/Art','Assets/Fonts','Assets/TextMesh Pro','Packages','ProjectSettings')){New-Item -ItemType Directory -Force -Path (Join-Path $isolated $dir)|Out-Null}
# Run the freshly compiled production assembly in a separate empty project. No gameplay doubles.
$rsp=[IO.File]::ReadAllText("$project/Temp/ActionButtonsCompile/runtime.rsp").Replace('-out:"Temp/ActionButtonsCompile/Assembly-CSharp.dll"','-out:"Temp/ActionButtonsCompile/ProductionGame.dll"')
[IO.File]::WriteAllText("$project/Temp/ActionButtonsCompile/drawer.rsp",$rsp)
Push-Location $project
try {& 'D:/Unity/Unity/Hub/Editor/6000.0.40f1/Editor/Data/NetCoreRuntime/dotnet.exe' 'D:/Unity/Unity/Hub/Editor/6000.0.40f1/Editor/Data/DotNetSdkRoslyn/csc.dll' '@Temp/ActionButtonsCompile/drawer.rsp' > Temp/CustomerDrawerIsolatedCompile.log;if($LASTEXITCODE -ne 0){throw 'Isolated production assembly compilation failed.'}}finally{Pop-Location}
# Include only the assembly dependency closure, avoiding unrelated package initializers.
$candidates=@{}
foreach($dll in Get-ChildItem "$project/Library/ScriptAssemblies" -Filter '*.dll'){$candidates[$dll.BaseName]=$dll.FullName}
foreach($line in Get-Content "$project/Temp/ActionButtonsCompile/runtime.rsp") {
 if($line -match '^-r:"((Assets/|Library/PackageCache/).*\.dll)"$'){$path=Join-Path $project $Matches[1];$candidates[[IO.Path]::GetFileNameWithoutExtension($path)]=$path}
}
$required=@{};$queue=[Collections.Generic.Queue[string]]::new();$queue.Enqueue("$project/Temp/ActionButtonsCompile/ProductionGame.dll")
while($queue.Count){$path=$queue.Dequeue();$name=[IO.Path]::GetFileNameWithoutExtension($path);if($required.ContainsKey($name)){continue};$required[$name]=$path
 foreach($reference in [Reflection.Assembly]::LoadFile($path).GetReferencedAssemblies()) {
  if($reference.Name -in @('Unity.TextMeshPro','UnityEngine.UI','UnityEditor.UI','Unity.Mathematics') -or $reference.Name.StartsWith('Unity.Burst')){continue}
  if($candidates.ContainsKey($reference.Name) -and !$required.ContainsKey($reference.Name)){$queue.Enqueue($candidates[$reference.Name])}
 }
}
foreach($dll in Get-ChildItem -LiteralPath "$isolated/Assets/Plugins" -Filter '*.dll') {
 if(!$required.ContainsKey($dll.BaseName)){Remove-Item -LiteralPath $dll.FullName;if(Test-Path -LiteralPath ($dll.FullName+'.meta')){Remove-Item -LiteralPath ($dll.FullName+'.meta')}}
}
foreach($path in $required.Values){Copy-Item -LiteralPath $path -Destination "$isolated/Assets/Plugins" -Force}
foreach($dll in Get-ChildItem "$isolated/Assets/Plugins" -Filter '*.dll') {
 $existing=if(Test-Path -LiteralPath ($dll.FullName+'.meta')){[regex]::Match([IO.File]::ReadAllText($dll.FullName+'.meta'),'(?m)^guid: ([a-f0-9]{32})').Groups[1].Value}else{''}
 $guid=if($existing){$existing}else{[guid]::NewGuid().ToString('N')}
 $meta=@"
fileFormatVersion: 2
guid: $guid
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 0
  platformData:
  - first:
      Any:
    second:
      enabled: 1
      settings: {}
"@
 [IO.File]::WriteAllText($dll.FullName+'.meta',$meta)
}
Copy-Item -LiteralPath "$project/Assets/_Project/UI/Assets/CustomerDrawerSpritePackV1/Components" -Destination "$isolated/Assets/Art" -Recurse -Force
New-Item -ItemType Directory -Force -Path "$isolated/Assets/Newspaper"|Out-Null
Copy-Item -LiteralPath "$project/Assets/_Project/UI/Assets/ReceptionNewspaperSpritePackV1/Components","$project/Assets/_Project/UI/Assets/ReceptionNewspaperSpritePackV1/Surfaces" -Destination "$isolated/Assets/Newspaper" -Recurse -Force
Copy-Item -LiteralPath "$project/Assets/_Project/UI/Assets/ReceptionNewspaperSpritePackV1/Fonts" -Destination "$isolated/Assets/Newspaper" -Recurse -Force
foreach($name in @('ActionButtonSpritePackV1','RestaurantControlsSpritePackV1')){New-Item -ItemType Directory -Force -Path "$isolated/Assets/Actions/$name"|Out-Null;Copy-Item -LiteralPath "$project/Assets/_Project/UI/Assets/$name/Components" -Destination "$isolated/Assets/Actions/$name" -Recurse -Force}
New-Item -ItemType Directory -Force -Path "$isolated/Assets/Portraits"|Out-Null
Copy-Item -LiteralPath "$project/Assets/_Project/Art/Models/Customer/New Alien Models/2dCloseupImages/1.png","$project/Assets/_Project/Art/Models/Customer/New Alien Models/2dCloseupImages/1.png.meta" -Destination "$isolated/Assets/Portraits" -Force
foreach($font in @('DayPanelHeading','DayPanelBodyBold','DayPanelBody')){Copy-Item -LiteralPath "$project/Assets/_Project/UI/Assets/DayPanelSpritePackV2/Fonts/$font.asset","$project/Assets/_Project/UI/Assets/DayPanelSpritePackV2/Fonts/$font.asset.meta" -Destination "$isolated/Assets/Fonts" -Force}
foreach($font in @('Assets/_Project/UI/Assets/Fonts/Fonts/LilitaOne-Regular.ttf','Assets/_Project/UI/Assets/DayPanelSpritePackV2/Fonts/Sources/NunitoSans-ExtraBold.ttf')){Copy-Item -LiteralPath (Join-Path $project $font),(Join-Path $project ($font+'.meta')) -Destination "$isolated/Assets/Fonts" -Force}
Copy-Item -LiteralPath "$project/Assets/TextMesh Pro/Shaders","$project/Assets/TextMesh Pro/Resources" -Destination "$isolated/Assets/TextMesh Pro" -Recurse -Force
Copy-Item -LiteralPath "$project/Assets/_Project/UI/Assets/DayPanelSpritePackV2/Fonts/Sources/NunitoSans-Bold.ttf","$project/Assets/_Project/UI/Assets/DayPanelSpritePackV2/Fonts/Sources/NunitoSans-Bold.ttf.meta" -Destination "$isolated/Assets/Fonts" -Force
Copy-Item -LiteralPath "$project/Assets/_Project/UI/Assets/Fonts/Atkinson_Hyperlegible" -Destination "$isolated/Assets/Fonts" -Recurse -Force
Copy-Item -LiteralPath "$PSScriptRoot/UnityCheck.cs.txt" -Destination "$isolated/Assets/Editor/CustomerDrawerUnityCheck.cs" -Force
Copy-Item -LiteralPath "$project/ProjectSettings/ProjectVersion.txt" -Destination "$isolated/ProjectSettings" -Force
$ugui=Get-ChildItem "$project/Library/PackageCache" -Directory -Filter 'com.unity.ugui@*'|Select-Object -First 1
$deps=@{'com.unity.ugui'=('file:'+$ugui.FullName.Replace('\','/'))}
foreach($name in @('com.unity.burst','com.unity.mathematics')){$package=Get-ChildItem "$project/Library/PackageCache" -Directory -Filter ($name+'@*')|Select-Object -First 1;$deps[$name]='file:'+$package.FullName.Replace('\','/')}
foreach($module in @('ai','ui','imageconversion','physics','animation','audio','jsonserialize')){$deps['com.unity.modules.'+$module]='1.0.0'}
[IO.File]::WriteAllText("$isolated/Packages/manifest.json",(@{dependencies=$deps}|ConvertTo-Json -Depth 4))
$startedAt=Get-Date
$process=Start-Process -FilePath $Unity -ArgumentList @('-batchmode','-projectPath',('"'+$isolated+'"'),'-executeMethod','CustomerDrawerUnityCheck.Run','-logFile',('"'+$project+'/Temp/CustomerDrawerCheck.log"')) -WindowStyle Hidden -Wait -PassThru
Write-Output "Unity verification exit: $($process.ExitCode)"
$report="$isolated/CheckOutput/report.txt"
if((Test-Path -LiteralPath $report) -and (Get-Item -LiteralPath $report).LastWriteTime -ge $startedAt){Get-Content -LiteralPath $report}else{Write-Output 'No fresh verification report generated.'}
exit $process.ExitCode
