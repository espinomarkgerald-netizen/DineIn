param([string]$Unity = 'D:\Unity\Unity\Hub\Editor\6000.0.40f1\Editor')
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$output = Join-Path $project 'Temp/CookingVerification'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$runtime = Join-Path $Unity 'Data/NetCoreRuntime'
$framework = Get-ChildItem "$runtime/shared/Microsoft.NETCore.App" -Directory | Sort-Object { [version]$_.Name } | Select-Object -Last 1
$refs = Get-ChildItem $framework.FullName -Filter '*.dll' | Where-Object { $_.Name -match '^(System\.|Microsoft.CSharp|netstandard|mscorlib)' -and $_.Name -notmatch '\.Native\.' } | ForEach-Object { '-r:"' + $_.FullName + '"' }
$assembly = Join-Path $output 'CookingHarness.dll'
$rsp = @('-nologo','-target:exe','-langversion:latest',('-out:"' + $assembly + '"')) + $refs + @(('"' + $project + '/Assets/_Project/Restaurant/FastFoodCookingState.cs"'),('"' + $PSScriptRoot + '/fixtures/fastfood_cooking_harness.cs"'))
$rsp += '"' + $project + '/Assets/_Project/Gameplay/PlayerTaskGuidance.cs"'
[IO.File]::WriteAllLines("$output/harness.rsp", $rsp)
$config = @{runtimeOptions=@{tfm='net'+$framework.Name.Substring(0,3);framework=@{name='Microsoft.NETCore.App';version=$framework.Name}}} | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText("$output/CookingHarness.runtimeconfig.json",$config)
& "$runtime/dotnet.exe" "$Unity/Data/DotNetSdkRoslyn/csc.dll" "@$output/harness.rsp"
if ($LASTEXITCODE -ne 0) { throw 'Cooking harness compilation failed' }
& "$runtime/dotnet.exe" $assembly
if ($LASTEXITCODE -ne 0) { throw 'Cooking ledger regression failed' }
