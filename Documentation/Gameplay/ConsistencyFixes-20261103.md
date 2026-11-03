# Shared gameplay corrections — 3 November 2026

## Implemented behavior

- Campaign entry checks the account's saved restaurant career before onboarding flags. A valid career past Day 1, or with recorded regular-service completion, bypasses tutorial even when onboarding flags are absent. First regular service records a per-restaurant flag in the existing account profile; Tutorial replay becomes available thereafter. Older 6- and 9-entry tutorial arrays remain readable alongside the new 12-entry arrays.
- The shared Big Boss dialogue constrains the portrait to editable safe-area dimensions, preserves its aspect ratio, and keeps the artwork inside the screen. Tutorial bridges already reject non-tutorial scenes; those guards remain in use.
- SettingsManager owns quality and display choices. Fresh desktop defaults to High/native display, mobile to Mid. GameOptimizer no longer overrides saved quality, frame timing, or audio settings. The camera button toggles follow/stop through the existing controllers for every restaurant, and temporary kitchen/storage views preserve follow.
- Inventory expiry queries now agree with storage labels that an expiry of zero means no expiry. Supplier cards distinguish expired units, future expiry risk and wrong storage. Stat cells use fitting label/value sizes, zero margins and a matching font material, with no value ellipsis.
- Fixed cashier workers are parked with a kinematic body and a carved occupied footprint instead of a stopped, pushable NavMeshAgent. The shared stationary-post component re-enables navigation only when displaced and returns to the captured home. Moving Fast Food cashiers retain their existing bot tasks. Staff recovery is authority-owned in multiplayer.
- The build helper reads enabled scenes from the selected profile, rejects a mismatched target, and records source and artifact SHA-256 hashes. The existing Windows/Android profiles include all three restaurants and all tutorial/multiplayer variants.

## Rewards — revised scope requested by the user

No Azure service or Economy v2 migration is used. `DailyRewardGameBackend` implements the existing provider boundary with game code, PlayFab private UserData, the existing Classic GC wallet, and campaign save receipts.

An offer is identified by account, restaurant, mode, run and logical day. Successful non-tutorial days offer a spin before results. Debug completions, restored results, and tutorial sessions are excluded. Outcomes use the existing editable prize settings. The outcome is saved before payout and is not rerolled on reopening. Multiplayer publishes completion before personal rewards; the host waits for connected participants to finish the reward presentation before advancing. The room protocol is now `shared-session-24`; all participants need the new build.

Cash credits go into the player's personal career for that restaurant, including multiplayer prizes. Active campaign results are committed before applying cash, so a day-start checkpoint cannot replace current earnings. Cash and receipt are saved together through the existing recoverable pair journal. Credits do not unlock restaurants. Account snapshot uploads continue through the existing CampaignCloudSync flow.

GC uses AddUserVirtualCurrency through PlayFabWalletManager; its response updates the wallet. A pending marker is saved before dispatch. An ambiguous request is not blindly retried. A failed/unavailable offer leaves a visible Continue path to results rather than blocking the day. Claim receipts are stored under private PlayFab UserData keys beginning `DR_`, with a local recovery copy under the account's `daily_rewards` folder.

This is explicitly a client-owned reward implementation, as requested, not server-authoritative anti-cheat or an atomic multi-device economy. Classic GC does not supply an idempotency key: concurrent devices and modified clients are not secured by a client receipt. An ambiguous GC grant needs account review; do not reset its pending state and resend without checking the original grant. The title must permit the existing client currency-grant and UserData APIs. No API policies or live balances were changed by these checks.

## Verification

- `GameplayConsistencyRegression.Run`: isolated tutorial-career fixtures, fresh/legacy/partial ownership, old profile arrays, expiry boundaries, wrong storage, serialized cash receipt replay, High render scale/MSAA, actual restock prefab text/geometry at three card sizes, enabled scene coverage and authored navigation references (including tutorial-bound bakes).
- `dotnet run --project Backend/Tests/RewardClaims/RewardClaims.csproj`: production provider compiled with simulated external boundaries. Checks normal cash/GC, repeated/reopened claims, uncertain GC requests, failed writes without rerolls, skip, no-match and account switch. Does not contact PlayFab.
- `node Backend/Tests/DailyRewardSlot.tests.cjs`: food/config references, result ordering/guards, UI cleanup, reduced motion and compiled odds/motion checks.
- Build results and hashes: `Logs/MockDefenseWindows.result`, `Logs/MockDefenseAndroid.result`, and their `.sha256` manifests. Per-platform consistency reports are copied before each build.

Final build results on 3 November: Windows succeeded with 0 errors / 53 warnings; Android succeeded with 0 errors / 84 warnings. Both used the same source fingerprint and all 13 enabled profile scenes. The final Windows player assembly and APK metadata contain the new reward provider, cash-recovery, cashier-post and camera-toggle code. APK archive checks passed. `Logs/GameplayConsistency-FinalBuilds.json` records the source, profile and artifact hashes. Both final consistency runs passed; the reward-provider harness passed 12 checks, and the slot suite passed 39 source/asset checks plus 1,121 odds/motion assertions. Live PlayFab, real multiplayer and visual/device acceptance below remain unrun.

## Changed implementation files

Paths below are relative to `Assets/_Project/` unless otherwise stated. Existing unrelated asset edits were preserved.

- Tutorials: `Tutorials/TutorialGameModeEntry.cs`, `Tutorials/TutorialDialogueUI.cs`, `Save/CampaignSaveStore.Account.cs`.
- Display: `MainMenu/NewDesign/Scripts/Settings/SettingsManager.cs`, `SettingsManager.Advanced.cs`, `SettingsController.cs`, and `MainMenu/NewDesign/Scripts/GlobalScript/GameOptimizer.cs`.
- Camera: `Gameplay/LobbyHUDRedesign.cs`, `Camera/MainCameraController.cs`, `Player/Camera/CameraController.cs`.
- Stock: `Office/Inventory/InventoryManager.cs`, `ManagementComputer/RestockStockProjection.cs`, `ManagementComputer/ManagementComputerCatalogCardUI.cs`, `Restaurant/RestockRoom/RestockStorageContainer.cs`.
- Cashier: `Gameplay/AutonomousService/Core/StationaryStaffPost.cs`, `Gameplay/AutonomousService/Lobby/LobbyAutonomousService.cs`.
- Rewards: `Gameplay/Rewards/DailyRewardGameBackend.cs`, `IDailyRewardBackend.cs`, `DailyRewardSlotUI.cs`, `DailyRewardSlotSettings.cs`; `MainMenu/GameMenu/Scripts/WalletManager/PlayFabWalletManager.cs`; `Save/CampaignSaveStore.Rewards.cs`, `GameSaveData.cs`, `GameSaveManager.cs`; `Core/GameFlowManager.cs`, `GameFlowManager.Rewards.cs`, `GameFlowManager.Ranking.cs`; `Gameplay/GameManager/GameManager.cs`; `Networking/Multiplayer/MultiplayerDayBridge.cs`, `MultiplayerSessionManager.cs`.
- Build/checks: `Editor/MockDefenseBuild.cs`, `Editor/GameplayConsistencyRegression.cs`; project-root `Backend/Tests/DailyRewardSlot.tests.cjs` and `Backend/Tests/RewardClaims/`.

## Required manual acceptance

1. Windows first: sign in to an existing Fine Dining Day 5 career; Campaign must open regular preparation with no training dialogue. On a fresh account, only Casual Dining is available; verify first Campaign onboarding and Tutorial replay after regular service starts. Switch accounts on the same machine and restore legacy progress.
2. In every restaurant/mode, test follow/stop/manual pan and return from storage/kitchen; inspect Big Boss and both restock quantities at supported aspect ratios and UI scales. Confirm High/native output and saved lower-quality choices survive menu changes and restart. Listen to Fast Food kitchen cues and the sizzle loop; the prior audio-position correction is included in these builds but playback remains unobserved.
3. Walk staff/player traffic around cashier posts and force a displacement. Check recovery, payment access and moving Fast Food workers. Test actual multiplayer authority, disconnect and reconnect.
4. Finish a successful day normally. After the last departure, spin or skip before results. Verify cash/GC on the real account, relaunch and save restoration. Test unavailable networking and host advancement while another player is still viewing a reward.
5. Repeat on an Android device after Windows acceptance. Automated compilation/builds and prefab checks do not establish visual quality, physical traffic behavior, live payouts, or phone performance.
