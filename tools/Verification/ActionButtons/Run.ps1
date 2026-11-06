param([string]$Unity='D:/Unity/Unity/Hub/Editor/6000.0.40f1/Editor/Unity.exe')
$ErrorActionPreference='Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$isolated = Join-Path $project 'Temp/ActionButtonsCheckProject'
New-Item -ItemType Directory -Force -Path "$isolated/Assets/Runtime","$isolated/Assets/Editor","$isolated/Packages","$isolated/ProjectSettings" | Out-Null
$runtime=@('Assets/_Project/UI/GameplayActionButtonArt.cs','Assets/_Project/UI/GameplayActionButtonSkin.cs','Assets/_Project/UI/CustomerCommentBubble.cs','Assets/_Project/UI/UiPrefab/TableNumberUI.cs','Assets/_Project/UI/UIFollowWorldPoint.cs','Assets/_Project/Gameplay/PlayerTaskBubbleFocus.cs')
foreach($file in $runtime) { Copy-Item -LiteralPath (Join-Path $project $file) -Destination "$isolated/Assets/Runtime" -Force; Copy-Item -LiteralPath (Join-Path $project ($file+'.meta')) -Destination "$isolated/Assets/Runtime" -Force }
Copy-Item -LiteralPath "$PSScriptRoot/BoundaryDoubles.cs.txt" -Destination "$isolated/Assets/Runtime/BoundaryDoubles.cs" -Force
Copy-Item -LiteralPath "$PSScriptRoot/UnityCheck.cs.txt" -Destination "$isolated/Assets/Editor/UnityCheck.cs" -Force
foreach($pack in @('ActionButtonSpritePackV1','RestaurantControlsSpritePackV1','CustomerCommentSpritePackV1')) {
 $dest="$isolated/Assets/Art/$pack"; New-Item -ItemType Directory -Force -Path $dest | Out-Null
 Copy-Item -LiteralPath "$project/Assets/_Project/UI/Assets/$pack/Components" -Destination $dest -Recurse -Force
}
Copy-Item -LiteralPath "$project/Assets/_Project/UI/Assets/CustomerCommentSpritePackV1/CustomerCommentsV1.png","$project/Assets/_Project/UI/Assets/CustomerCommentSpritePackV1/CustomerCommentsV1.png.meta" -Destination "$isolated/Assets/Art/CustomerCommentSpritePackV1" -Force
foreach($mood in @('Happy','Neutral','Angry')) {Copy-Item -LiteralPath "$project/Assets/_Project/UI/Assets/DayPanelSpritePackV2/Components/Icon$mood.png","$project/Assets/_Project/UI/Assets/DayPanelSpritePackV2/Components/Icon$mood.png.meta" -Destination "$isolated/Assets/Art" -Force}
foreach($file in @('Assets/_Project/UI/Assets/DayPanelSpritePackV2/Components/ProgressTrack.png','Assets/_Project/UI/Assets/Legacy/Bubbles/greet.png','Assets/_Project/UI/Assets/Legacy/Bubbles/table.png')) {Copy-Item -LiteralPath (Join-Path $project $file),(Join-Path $project ($file+'.meta')) -Destination "$isolated/Assets/Art" -Force}
$res="$isolated/Assets/Resources/UI";New-Item -ItemType Directory -Force -Path $res | Out-Null
foreach($name in @('ActionButtonHeadingOutline','ActionButtonBodyOutline')) {Copy-Item -LiteralPath "$project/Assets/_Project/UI/Assets/ActionButtonSpritePackV1/$name.mat","$project/Assets/_Project/UI/Assets/ActionButtonSpritePackV1/$name.mat.meta" -Destination $res -Force}
Copy-Item -LiteralPath "$project/Assets/_Project/Resources/UI/GameplayActionButtons.asset","$project/Assets/_Project/Resources/UI/GameplayActionButtons.asset.meta" -Destination $res -Force
$fonts="$isolated/Assets/Fonts";New-Item -ItemType Directory -Force -Path $fonts | Out-Null
foreach($font in @('DayPanelHeading','DayPanelBodyBold')) { Copy-Item -LiteralPath "$project/Assets/_Project/UI/Assets/DayPanelSpritePackV2/Fonts/$font.asset","$project/Assets/_Project/UI/Assets/DayPanelSpritePackV2/Fonts/$font.asset.meta" -Destination $fonts -Force }
foreach($font in @('Assets/_Project/UI/Assets/Fonts/Fonts/LilitaOne-Regular.ttf','Assets/_Project/UI/Assets/DayPanelSpritePackV2/Fonts/Sources/NunitoSans-ExtraBold.ttf')) {Copy-Item -LiteralPath (Join-Path $project $font),(Join-Path $project ($font+'.meta')) -Destination $fonts -Force}
New-Item -ItemType Directory -Force -Path "$isolated/Assets/TextMesh Pro/Resources","$isolated/Assets/Prefabs" | Out-Null
Copy-Item -LiteralPath "$project/Assets/_Project/UI/Assets/Legacy/Bubbles/CustomerGreetBubble.prefab" -Destination "$isolated/Assets/Prefabs" -Force
Copy-Item -LiteralPath "$project/Assets/TextMesh Pro/Shaders" -Destination "$isolated/Assets/TextMesh Pro" -Recurse -Force
Copy-Item -LiteralPath "$project/Assets/TextMesh Pro/Resources" -Destination "$isolated/Assets/TextMesh Pro" -Recurse -Force
foreach($prefab in @('Assets/_Project/Restaurant/Assets/Customers/Icons/OrderAlertUI.prefab','Assets/_Project/Restaurant/Assets/Customers/Icons/TableNumber.prefab','Assets/_Project/Restaurant/Assets/Customers/Icons/BillAlertUI.prefab','Assets/_Project/Restaurant/Assets/Level1/UI/TrayUi.prefab','Assets/_Project/Restaurant/Assets/Customers/Icons/CustomerComments.prefab')) {Copy-Item -LiteralPath (Join-Path $project $prefab) -Destination "$isolated/Assets/Prefabs" -Force}
Copy-Item -LiteralPath "$project/ProjectSettings/ProjectVersion.txt" -Destination "$isolated/ProjectSettings" -Force
$ugui=Get-ChildItem -LiteralPath "$project/Library/PackageCache" -Directory -Filter 'com.unity.ugui@*' | Select-Object -First 1
$manifest=@{dependencies=@{'com.unity.ugui'=('file:'+$ugui.FullName.Replace('\','/'));'com.unity.modules.ai'='1.0.0';'com.unity.modules.ui'='1.0.0';'com.unity.modules.imageconversion'='1.0.0'}}|ConvertTo-Json -Depth 4
[IO.File]::WriteAllText("$isolated/Packages/manifest.json",$manifest)
$process=Start-Process -FilePath $Unity -ArgumentList @('-batchmode','-projectPath',('"'+$isolated+'"'),'-executeMethod','ActionButtonsUnityCheck.Run','-logFile',('"'+$project+'/Temp/ActionButtonsCheck.log"')) -WindowStyle Hidden -Wait -PassThru
Write-Output "Unity verification exit: $($process.ExitCode)"
if(Test-Path -LiteralPath "$isolated/CheckOutput/report.txt") {Get-Content -LiteralPath "$isolated/CheckOutput/report.txt"}
exit $process.ExitCode
