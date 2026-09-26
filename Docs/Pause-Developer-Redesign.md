# Pause and Developer Settings redesign

## Editable assets

- `Assets/_Project/Gameplay/UI/Resources/LobbyPauseMenu.prefab`
- `Assets/_Project/Resources/UI/LobbyHUD.prefab` (shared gameplay pause UI)
- `Assets/_Project/Resources/UI/DeveloperSettings.prefab`

The saved hierarchies contain the actual controls. Edit their sprites, layout, typography, references and switch visuals in Prefab Mode. Pause overlays are saved closed; temporarily enable Overlay to inspect the menu. Developer Settings is saved inactive; enable the root to preview it. Existing Blue/Green/Red/Grey DOUBLE sprites and the Management Computer's Anton/Atkinson fonts are reused. No imported or generated artwork.

`PauseSettingsAuthoring` is an explicit Editor migration, not a runtime UI builder. Running its menu item reapplies the design defaults, so do not run it over custom prefab styling unless intended.

## Settings

- Audio: master, music, SFX, mute (keeps channel values).
- Controls: pan speed, zoom speed, invert zoom, desktop edge pan, reset.
- Graphics: quality, resolution, windowed/borderless/exclusive fullscreen, VSync, FPS limit, global UI scale. Display changes have a timed Keep/Revert confirmation.
- Accessibility: large text, high contrast, reduced motion, dialogue speed, reset. Existing accessibility consumers are preserved; this is not a global world recoloring effect.

Preferences use the existing SettingsManager/PlayerPrefs architecture. Audio uses existing mixer parameters and AudioListener master gain. No ambience control is shown because the project has no separate ambience channel. No color-vision filter is shown.

UI Scale is limited to 85–115%. SettingsManager caches authored root CanvasScaler baselines, scales their reference resolution or constant-pixel factor, and discovers additional canvases without resizing individual controls. Nested canvases inherit the parent scale. Closed menus are included; new scenes trigger discovery. Existing safe-area logic remains in place. UIFollowWorldPoint applies the same multiplier to player-facing world prompts; environmental signage is unchanged.

## Existing developer command mapping

| Category | Existing backend commands | UI |
|---|---|---|
| Session | `day`, `approval` / `reputation`, `money`, `addMoney` | Dedicated numeric fields and Set/Add buttons |
| Session | `startDay`, `endDay`, `gameOver` | Named action buttons |
| Gameplay | `timeScale` | Numeric field and Set button |
| Gameplay | `complaint(1)` / `wrongOrder`, `complaint(2)` / `burntFood`, `cardPayment` | Wrong-order, burnt-food and next-card-payment buttons |
| Inventory | `fillStocks`, `zeroStocks` | Quantity field, Fill and Empty actions |
| Account | `setCoin`, `addCoin` | Separate amount fields and Set/Add buttons |
| Progression | `resetRun`, `recover` | Reset campaign and Recover buttons |
| Progression | `upgrade(1..3)`, `unlockPopup(1..3)` | Individual busser trolley, waiter trolley and card-payment unlock/preview buttons |
| Utility | `save`, `status`, `help` | Save, Refresh status and Command reference buttons |

Aliases share their existing backend; no functionality was replaced with invented cheats. Destructive actions show confirmation. Day changes retain the original fresh-day reset behavior. Account actions retain existing PlayFab authorization and asynchronous results. No staff commands existed, so no fake Staff category was added.

F10 retains the existing Windows development/authorized-account guard. Android has no developer window or entry button. Windows-only display controls and edge pan are hidden on Android; Android's FPS default is labelled Device default.

## Source files changed for this pass

- `Gameplay/LobbyPauseMenu.cs`, `PauseSettingsPanel.cs`, `SettingsToggleVisual.cs`
- `ManagementComputer/ManagementComputerController.cs`
- `MainMenu/NewDesign/Scripts/Settings/SettingsManager.cs`, `.Advanced.cs`, `.UIScale.cs`
- `Camera/MainCameraController.cs`, `Player/Camera/CameraController.cs`
- `UI/UIFollowWorldPoint.cs`
- `Debug/DevSettingsConsole.cs`, `DeveloperSettingsView.cs`
- `Editor/PauseSettingsAuthoring.cs`, `PauseSettingsRegression.cs`, `BuildReferenceIntegrityGuard.cs`, `BuildParityRegression.cs`

Paths above are relative to `Assets/_Project`. New scripts include Unity metadata. Existing unrelated work is retained; no commits or pushes were made.

## Manual acceptance checks

Automated Editor checks passed: compilation; saved prefab bindings; all setting callbacks and preference round trips; resets; non-compounding screen/pixel UI scaling; computer visibility; tutorial time-scale restoration; developer category switching; landscape layout bounds at 1280×720, 2340×1080 and 1920×1440, including 85–115% scale and a simulated notch margin. These are structural checks, not visual or physical-device approval.

1. Lobby1, Lobby2 and tutorial: pause/resume; open Computer and each app; close with X. Pause must hide only while the computer is open and remain usable during tutorials.
2. Change settings, restart, verify persistence. Listen to music and gameplay/UI sounds separately; toggle mute without losing sliders.
3. Test UI Scale at 85%, 100%, 115% across HUD, Computer, kitchen, dialogue and world prompts. Check Android landscape notches and slider touch targets.
4. Windows: resolution/window-mode Apply, Keep and timeout Revert; VSync and FPS cap. Editor Game View is not a standalone OS window.
5. Development Windows build: F10 twice; use a disposable save to test money, day reset, stocks, progression and complaints. Check account actions with a test account.
6. Multiplayer: each client can open/close pause locally without stopping the server; return to menu leaves correctly.

Standalone Windows/Android builds, native display switching, physical touch, audible mixing, live PlayFab mutations and multiplayer sessions require these manual checks. No claim of device-wide bug-free behavior is made.
