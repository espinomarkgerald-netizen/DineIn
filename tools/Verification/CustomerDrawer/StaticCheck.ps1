$ErrorActionPreference='Stop'
$project=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$script:checks=0
function Check([bool]$condition,[string]$label){$script:checks++;if(!$condition){throw $label}}
function Read([string]$path){[IO.File]::ReadAllText((Join-Path $project $path))}
$pack='Assets/_Project/UI/Assets/CustomerDrawerSpritePackV1'
$manifest=(Read "$pack/CustomerDrawerV1.json")|ConvertFrom-Json
$binding=Read 'Assets/_Project/Resources/UI/CustomerDrawerArt.asset'
foreach($sprite in $manifest.sprites){
 Check ($binding.Contains('name: '+$sprite.name)) ('Binding name '+$sprite.name)
 Check ($binding.Contains('guid: '+$sprite.guid)) ('Binding GUID '+$sprite.name)
 Check ((Read "$pack/$($sprite.file).meta").Contains('guid: '+$sprite.guid)) ('Production sprite GUID '+$sprite.name)
 Check (Test-Path -LiteralPath (Join-Path $project "$pack/$($sprite.file)")) ('Production PNG '+$sprite.name)
}
foreach($file in @('Assets/_Project/UI/CustomerDrawerArt.cs','Assets/_Project/UI/CustomerDrawerView.cs','Assets/_Project/UI/CompactTaskPanelView.cs','Assets/_Project/Customers/Customer/CustomerGroup.Inspection.cs')){
 Check (Test-Path -LiteralPath (Join-Path $project ($file+'.meta'))) ('Script metadata '+$file)
 Check ((Read 'Temp/ActionButtonsCompile/runtime.rsp').Contains($file)) ('Runtime compilation includes '+$file)
}
$scriptGuid=[regex]::Match((Read 'Assets/_Project/UI/CustomerDrawerArt.cs.meta'),'guid: (\w+)').Groups[1].Value
Check ($binding.Contains('guid: '+$scriptGuid)) 'Resource script binding'
foreach($font in @('DayPanelHeading','DayPanelBodyBold')){
 $guid=[regex]::Match((Read "Assets/_Project/UI/Assets/DayPanelSpritePackV2/Fonts/$font.asset.meta"),'guid: (\w+)').Groups[1].Value
 Check ($binding.Contains('guid: '+$guid)) ('Font binding '+$font)
}
foreach($file in @('Assets/_Project/Player/Movement/PlayerMovement.cs','Assets/_Project/Lobby/Roles/RoleBasedAssignController.cs','Assets/_Project/Lobby/Roles/HostAssignController.cs','Assets/_Project/Player/Levels/Waiter/WaiterAssignController.cs')){
 Check ((Read $file).Contains('CustomerDrawerView.BlocksWorldPointer || CustomerDrawerView.TryCaptureWorldPress(screenPos)')) ('World action gate '+$file)
}
Check ((Read 'Assets/_Project/Restaurant/Items/CustomerGroupClickable.cs').Contains('CustomerDrawerView.BlocksCustomerClick')) 'Direct customer click gate'
Check ((Read 'Assets/_Project/Restaurant/Managers/TapOutlineSelector.cs').Contains('CustomerDrawerView.BlocksWorldPointer')) 'Outline selection gate'
Check ((Read 'Assets/_Project/UI/UiPrefab/TableNumberUI.cs').Contains('!serveAvailable && CustomerDrawerView.TryInspectGroupFromUI(group)')) 'Plain table inspection preserves explicit Serve'
$projection=Read 'Assets/_Project/Customers/Customer/CustomerGroup.Inspection.cs'
Check ($projection -notmatch 'Time\.deltaTime|state\s*=\s*GroupState|SetState\(|SetDestination\(|Request[A-Z]\w*\(') 'Inspection projection does not advance gameplay or submit commands'
$network=Read 'Assets/_Project/Networking/Multiplayer/MultiplayerCustomerSpawn.State.cs'
Check ($network.Contains('nextInspectionSample=Time.unscaledTime+.25f')) 'Inspection samples limited to four per second'
Check ($network.Contains('Mathf.Round(inspection.Patience*100f)/100f')) 'Inspection patience quantized to one percent'
Check ($network.IndexOf('state.run != session.RunId') -lt $network.IndexOf('Group.PresentInspectionPatience')) 'Existing run/day/revision acceptance precedes inspection projection'
$prefab=Read 'Assets/_Project/Resources/UI/LobbyHUD.prefab'
foreach($name in @('LivePanel','LiveToggle','TaskMessage')){Check ($prefab.Contains('m_Name: '+$name)) ('Shared HUD branch '+$name)}
Check ((Read 'Assets/_Project/Gameplay/LobbyHUDRoot.cs').Contains('CustomerDrawerArt.Shared==null && MobileUISettings.UseMobileLayout')) 'No second mobile task scale'
$scenes=Get-ChildItem -LiteralPath "$project/Assets/_Project/Scenes" -Recurse -Filter 'Lobby*.unity'
foreach($scene in $scenes){Check ($scene.Length -gt 0) ('Restaurant scene retained '+$scene.Name)}
Check ($scenes.Count -eq 9) 'All nine campaign/tutorial/multiplayer restaurant scenes present'
$message="PASS: $script:checks sprite/font/resource, source-guard and shared-HUD reference checks. Scene references are static coverage, not live mode testing."
Write-Output $message
New-Item -ItemType Directory -Force -Path "$project/Temp/CustomerDrawerCheckProject/CheckOutput"|Out-Null
[IO.File]::WriteAllText("$project/Temp/CustomerDrawerCheckProject/CheckOutput/static-report.txt",$message)
