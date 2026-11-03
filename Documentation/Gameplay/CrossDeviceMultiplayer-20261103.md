# Cross-device multiplayer changes — 3 November 2026

## What changed

The private room-code flow still uses the existing Photon application, shared Asia-region fallback, application version and `shared-session-24` protocol. Both current build profiles resolve to application version `0.1.0`. No server migration, account/career changes, profile replacement, player builds or installations were performed.

The screenshots establish error 32758 in both joining directions. They do not establish the underlying live connection mismatch. The old release player omitted room tracing, and its Windows log contained no connection identity to compare with Android. The actual Windows/Android pairing therefore remains a manual acceptance item, not a proven fix from compilation alone.

- `PhotonBootstrap.cs` validates the actual connected application, effective PUN version and normalized region before a room request can use that connection. PUN adds its own version suffix; the guard checks that effective value without appending it twice. Expected and connected values are available in **Connection details**.
- `MultiplayerMenuController.cs` and its new `Presentation.cs` partial keep one room operation active, distinguish corrective account/settings reconnects from cancellation, cancel timed-out/closed requests, reject late joins, clear disconnected room displays, and retain exact-name joining. No missing room becomes a replacement room.
- Release tracing records connection/master/create/join/leave/disconnect/cancellation events, room code, compatibility information, availability and numeric failures. It excludes account identity, authentication values, credentials and raw server-message text. Application identifiers are shortened.
- `NewGameMenu.unity` now fits the existing 700×350 room frame inside the safe viewport. The close button is kept above content, while decorative graphics stretch across the screen and do not intercept its input. Join, Create and all three restaurants use the same menu.
- The close button, Android Back and Windows Escape share `RequestClose`. Closing a pending request cancels it. Closing an established room panel hides it; **Leave Room** explicitly leaves. Once a run is registering/loading, close is disabled until that transition resolves.
- Layout margins and diagnostic-panel dimensions remain editable on the existing controller. The original close artwork, fonts and button sprites are reused.

## Source and verification

Runtime paths under `Assets/_Project/Networking/Photon/`:

- `PhotonBootstrap.cs`
- `MultiplayerMenuController.cs`
- `MultiplayerMenuController.Presentation.cs`

Scene: `Assets/_Project/Scenes/NewMenu/NewGameMenu.unity`. A pre-authoring copy, including the user's pre-existing scene changes, is preserved at `Logs/NewGameMenu.before-multiplayer-fix.unity`. Comparison found changes confined to the multiplayer menu's existing objects plus its new bindings/diagnostic controls; no original scene objects were removed.

`Assets/_Project/Editor/MultiplayerMenuValidation.cs` provides explicit authoring and checks; it never runs automatically on import or invokes a player-build helper. `CheckAndRunRepair` runs the menu checks followed by the existing isolated multiplayer suite.

Related test maintenance preserves the intended assertions: an active claim owner for the generation fixture, the current protocol identifier, an explicit Unity 6 mesh overload, and the actual **Rejoin Session** recovery message. These changes are in `MultiplayerRepairRegressionTest.cs`, `MultiplayerReadinessRegressionCases.cs`, `MultiplayerTaskTrackingRegressionCases.cs` and `MultiplayerRoomRestockRegressionCases.cs`.

Results are recorded in `Logs/MultiplayerMenuValidation.result`, `Logs/MultiplayerRepairRegression-current.log` and `Artifacts/MultiplayerRepairRegression.txt`. Layout cases cover 16:9, 16:10, ultrawide, Android landscape phone/tablet shapes, simulated cutouts/navigation insets, 85%/100%/115% UI scale, and expanded/collapsed connection details. Checks also cover close bindings/hit size, profile versions, PUN suffix/region normalization, corrective reconnect, cancellation, queued submission and late failure callbacks.

No real accounts, room connections or payouts are used by these checks. No Windows/Android player was rebuilt. Existing build artifacts do not contain these changes until the user rebuilds them.

Final result: script compilation passed, all 30 menu-layout cases passed, and all 18 isolated multiplayer regression cases passed. Corrective reconnect, cancellation, late-callback and queued-submission checks passed as well. `Logs/CrossDeviceMultiplayer.result.json` records this outcome and the remaining live-device acceptance limit. Unity was left closed, with the Windows profile used for the checks.

## Manual acceptance after your builds

Open Unity after implementation/checks finish. Inspect `NewGameMenu`, then build Windows first and Android from the existing matching profiles. Install your new APK on each test phone and use separate signed-in accounts. Do not use an older installed APK for the new pairing.

For each restaurant, test these host/guest pairs with two participants:

| Restaurant | Windows → Android | Android → Windows | Windows → Windows | Android → Android |
| --- | --- | --- | --- | --- |
| Casual Dining | Not run | Not run | Not run | Not run |
| Fast Food | Not run | Not run | Not run | Not run |
| Fine Dining | Not run | Not run | Not run | Not run |

1. Create a room, keep the host in its visible lobby, and join with its displayed code. Confirm both rosters show both players. Compare **app**, **region**, **effective version** and **protocol** in Connection details while connected; these must agree. Platform and build GUID may differ normally. Repeat with a four-player mixed-device party and across separate internet connections.
2. Check full and wrong-code rooms, duplicate accounts, repeated clicks, cancellation during connect/create/join, reopening, and closing an established lobby without leaving. Verify **Leave Room** separately. No cancelled operation may later create a hidden room or load gameplay.
3. On each phone/tablet, test Join/Create tabs, expanded details, on-screen close, Android Back, notches/navigation bars, and supported UI scales. On Windows test Escape, window resizing, 16:10 and ultrawide. Controls must remain visible and tappable.
4. Ready/start each restaurant. Confirm all clients load the same scene, own the correct player, move correctly, and can perform service/payment and shared-stock interactions. Finish a normal successful day and check personal reward/results ordering and return to menu.
5. Disconnect/reconnect a guest, background/restore Android, switch accounts at the menu, and leave/close the host. A departed host's room must not remain advertised by stale UI; reconnect must return to the original eligible run or explain why it ended.
6. If 32758 remains with a matching connection identity and an active host room, preserve the host and guest logs from that same attempt plus both Connection details displays and the exact code/time. Windows log: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/DineIN game/Player.log`; Android: Unity-tagged device logcat. Investigate those paired callbacks before changing region, protocol or matchmaking policy again.

Live cross-device networking, phone visuals/input, real multiplayer simulation and PlayFab payouts remain **NOT RUN** until this matrix is completed.
