# Scene Flow and Lobby Day Debugger

## Active Campaign Route

1. `Bootstrap` loads `NewMainMenu` through `SceneManagerUI`.
2. The New Main Menu Play button uses `SceneTrigger` to load `NewGameMenu`.
3. `RestaurantSelector` exposes the highlighted restaurant index.
4. `GameModePopupController.ChooseCampaign()` resolves that index through `campaignRestaurantScenes`.
5. Casual Dining is index `0` and loads `Lobby1` through the persistent `SceneLoader`.

The Multiplayer button still records the selected mode and closes the popup. Its scene transition is intentionally unchanged for now.

## Lobby Debugger

`Lobby1` contains the cloned Office debugger Canvas and `DevSettingsManager`. In Play Mode:

- Press `F10` to open or close the panel.
- Run `day(1)` through `day(30)` to change the campaign day.
- Run `approval(0)` through `approval(100)` to change approval.
- Run `money(0)` or a higher value to set money.

`DevSettingsConsole.RunCurrentCode()` validates the command and calls `GameFlowManager.TrySetCurrentDayDebug()` for day changes.

## Live Day Refresh

`GameFlowManager.TrySetCurrentDayDebug()` raises `OnDayChanged`. `GameDayManager.HandleDayChanged()` then reapplies:

- spawn difficulty curves and limits;
- customer type availability;
- autonomous dine-in restrictions;
- lobby booth unlocks;
- current HUD values.

New customer groups use the updated availability on their next spawn. Existing groups are not replaced or reset.

Current customer unlock checks:

- Day 1: Green customers.
- Day 5: Pink customers become available.
- Day 10: Blue customers become available.
- Day 20: takeout reaches its campaign unlock, but remains disabled while the current autonomous service is dine-in only.

## Build Profiles

`NewMainMenu`, `NewGameMenu`, and `Lobby1` are enabled in the global scene list and all current Windows/Android build profiles. Scene-name loading therefore works in both Editor Play Mode and builds.
