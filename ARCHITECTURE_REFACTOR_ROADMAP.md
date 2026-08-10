# Dine In - Architecture Refactor Roadmap

Static analysis date: 10 August 2026  
Organization pass completed: 10 August 2026  
Scope: Project-owned Unity scripts and assets under `Assets`, excluding vendor/runtime packages such as Photon, PlayFab SDK, TextMesh Pro examples, and QuickOutline.

This document is an organization and refactoring roadmap only. It does not include rewritten code.

## 1. File Organization & Architecture

### Current Implemented Structure

All first-party scripts and assets now live under one root:

```text
Assets/_Project
```

The former `Assets/Assets` and `Assets/Assets/MAINGAME` trees have been removed. The existing role-based Kitchen, Lobby, Office, tutorial, and Multiplayer scenes are now grouped under `Assets/_Project/Scenes/RoleBased`. This name is intentional: those scenes represent the older multi-role design and are the candidates to be replaced by the future single management gameplay scene.

Vendor and package folders should stay isolated:

- `Assets/Photon`
- `Assets/PlayFabSDK`
- `Assets/PlayFabEditorExtensions`
- `Assets/TextMesh Pro`
- `Assets/QuickOutline`

### Project Root

The project-owned root is now:

```text
Assets/_Project
```

All thesis/game-authored scripts and assets live under this folder, grouped by responsibility. Feature folders may contain an `Assets` subfolder when the content is owned by that feature.

### Recommended Folder Map

```text
Assets/_Project/Core
Assets/_Project/Save
Assets/_Project/Player
Assets/_Project/Lobby
Assets/_Project/Customers
Assets/_Project/Restaurant/Booths
Assets/_Project/Restaurant/Orders
Assets/_Project/Restaurant/Items
Assets/_Project/Kitchen
Assets/_Project/Office
Assets/_Project/Tutorials
Assets/_Project/UI
Assets/_Project/Networking
Assets/_Project/Debug
Assets/_Project/Art
Assets/_Project/Audio
Assets/_Project/Scenes/RoleBased
```

### Core

Purpose: application-level flow, persistent managers, global settings, and loading.

Recommended scripts:

- `GameFlowManager.cs`
- `CoreManagersBridge.cs`
- `SettingsManager.cs`
- `SceneLoadWithScreen.cs`
- `LoadingScreenUI.cs`

Notes:

- `GameFlowManager` should remain the main day/scene flow coordinator.
- Avoid adding gameplay-specific logic here.

### Save

Purpose: save data models, save/load ownership, and migration-only save helpers.

Recommended scripts:

- `GameSaveManager.cs`
- `GameSaveData.cs`
- `LocalGameSaveManager.cs` only if still needed for migration
- `LocalSaveManager.cs` only if still used by active game flow

Notes:

- The project should have one primary save owner.
- Legacy save scripts should be clearly marked as migration/legacy or removed after verification.

### Player

Purpose: player movement, setup, camera binding, player animation, and player local effects.

Recommended scripts:

- `PlayerMovement.cs`
- `PlayerSetup.cs`
- `PlayerAnimationController.cs`
- `PlayerMovementParticles.cs`
- `FootDustTrail.cs`
- `CameraController.cs`
- `MainCameraController.cs`

Notes:

- `PlayerMovement` is a high-impact dependency because many interactables depend on it.
- Avoid having unrelated office/kitchen movement scripts with overlapping class names.

### Lobby

Purpose: role switching, lobby queue/line systems, role-specific hands, lobby task tracking, and assignment controls.

Recommended scripts:

- `RoleManager.cs`
- `RoleBasedAssignController.cs`
- `RoleCameraController.cs`
- `StaffRole.cs`
- `RoleIndicator.cs`
- `RoleSwitchWarningUI.cs`
- `HostAssignController.cs`
- `BusserHands.cs`
- `WaiterHands.cs`
- `LobbyLineManager.cs`
- `LobbyQueueManager.cs`
- `LobbyTaskTracker.cs`
- `LobbyTaskUI.cs`
- `LobbySceneController.cs`
- `LobbyStockBridge.cs`
- `ManagementToLobbyStarter.cs`

Notes:

- Role switching and active-player lookup should be centralized in `RoleManager`.
- Scripts should avoid directly searching for active player objects when `RoleManager` can provide them.

### Customers

Purpose: customer group lifecycle, customer agents, customer order state, and customer-facing UI bubbles.

Recommended scripts:

- `CustomerGroup.cs`
- `CustomerAgent.cs`
- `CustomerGroupClickable.cs`
- `CustomerGroupFinder.cs`
- `CustomerOrder.cs`
- `CustomerGreetBubbleSpawner.cs`
- `CustomerGreetBubbleUI.cs`
- `EatingBubbleUI.cs`
- `FadeOnCameraView.cs`
- `LinePatienceUI.cs`

Notes:

- `CustomerGroup` is currently too large and should be split into smaller controllers.

### Restaurant / Booths

Purpose: booth state, seats, booth assignment, table mess events, money/tray spawn points, and booth-specific interactables.

Recommended scripts:

- `Booth.cs`
- `SeatAnchor.cs`
- `GroupSpawner.cs`
- `BoothDeliverInteractable.cs`
- `BoothMoneySpawner.cs`
- `BoothPuddleSpawner.cs`
- `BoothTrayRegistry.cs`
- `TableMessEvent.cs`
- `BoothMessHoldToCleanUI.cs`

Notes:

- Booth scripts should own booth/table state, not full customer lifecycle logic.

### Restaurant / Orders

Purpose: order tickets, checklist UI, billing, payment flow, and order numbering.

Recommended scripts:

- `OrderFlowManager.cs`
- `OrderChecklistUI.cs`
- `OrderTicketUI.cs`
- `OrderNumberManager.cs`
- `BillManager.cs`
- `BillPaper.cs`
- `BillPaperPickupButton.cs`
- `PaymentPickupInteractable.cs`
- `PaymentPopupUI.cs`

Notes:

- `OrderChecklistUI` should not own pricing and validation long-term.

### Restaurant / Items

Purpose: food trays, money objects, pickup/drop behavior, cleaning behavior, and item popups.

Recommended scripts:

- `FoodTray.cs`
- `FoodTrayInteractable.cs`
- `TrayPickupQueue.cs`
- `TrayPickupUIButton.cs`
- `TrayCleanable.cs`
- `TrayHoldToClean.cs`
- `MoneyPickup.cs`
- `MoneyPopupSpawner.cs`
- `MoneyPopupUI.cs`
- `MoneyBubbleUI.cs`
- `IconBubbleUI.cs`
- `BagPickupUIButton.cs`
- `TakeoutBagInteractable.cs`
- `TakeoutBagMarker.cs`
- `TakeoutCounterInteractable.cs`
- `TakeoutCounterClickable.cs`
- `TakeoutCustomerInteractable.cs`
- `TakeoutFlowManager.cs`
- `TakeoutQueueCustomer.cs`
- `TakeoutQueueManager.cs`

Notes:

- Takeout may become its own subfolder if it continues growing.

### Kitchen

Purpose: kitchen role movement, cooking equipment, ingredient data, plate assembly, delivery counters, and kitchen-specific tutorial/order systems.

Recommended scripts:

- `KitchenManager.cs`
- `KitchenRoleManager.cs`
- `KitchenPlayerMovement` currently inside `RevisedPlayerMovement.cs`
- `OrderManagerKitchen.cs`
- `PerformanceManager.cs`
- `PlayerHolding.cs`
- `Counter.cs`
- `Cupboard.cs`
- `Shelf.cs`
- `ShelfButton.cs`
- `Fryer.cs`
- `Grill.cs`
- `DrinkDispenser.cs`
- `DeliveryCounter.cs`
- `DeliveryFeedback.cs`
- `CupSpawner.cs`
- `Plate.cs`
- `PlateSpawner.cs`
- `Ingredient.cs`
- `IngredientComponent.cs`
- `IngredientStack.cs`
- `ItemIdentity.cs`
- `UIManagerKitchen.cs`
- `KitchenTutorialManager.cs`
- `TutorialOrderManager.cs`
- `TutorialWarningPopup.cs`
- `TutorialCompletePopup.cs`

Notes:

- Rename files so file names match class names.
- Separate kitchen movement from kitchen item interaction logic where possible.

### Office

Purpose: management scene systems: HR, money, inventory, recipes, unlocks, equipment, and office UI.

Recommended subfolders:

```text
Assets/_Project/Office/HR
Assets/_Project/Office/Inventory
Assets/_Project/Office/Finance
Assets/_Project/Office/Equipment
Assets/_Project/Office/UI
```

Recommended HR scripts:

- `HRManager.cs`
- `EmployeeManager.cs`
- `EmployeeGenerator.cs`
- `EmployeeData.cs`
- `EmployeeCard.cs`
- `EmployeeRole.cs`
- `RoleSlot.cs`
- `RoleRowUI.cs`
- `RoleGroup.cs`
- `SlotButton.cs`

Recommended Inventory/Recipe scripts:

- `InventoryManager.cs`
- `InventoryEntry.cs`
- `ItemData.cs`
- `ItemType.cs`
- `Recipe.cs`
- `RecipeManager.cs`
- `RecipeItemUI.cs`
- `OrderManager.cs`
- `ShopManager.cs`
- `ShopItemUI.cs`
- `ShopCheckoutManager.cs`
- `ReceiptItem.cs`

Recommended Finance scripts:

- `MoneyManager.cs`
- `MoneyUI.cs`
- `FinanceManager.cs`
- `DailyFinanceBridge.cs`
- `DailyRevenueTracker.cs`
- `DailyReportUI.cs`

Recommended Equipment scripts:

- `EquipmentManager.cs`
- `EquipmentShopManager.cs`
- `EquipmentItemUI.cs`
- `Equipment.cs`
- `EquipmentLink.cs`
- `EquipmentLinkActivator.cs`

Recommended Office UI scripts:

- `UIManager.cs`
- `OfficeStartButtons.cs`
- `OfficeStartDayButton.cs`
- `StartBlockedPanel.cs`
- `SettingsMenuManager.cs`
- `KitchenSceneController.cs`
- `LobbyToManagement.cs`

### Tutorials

Purpose: tutorial phases, dialogue, arrows, role locks, tutorial-only scene flow, and guided interactions.

Recommended scripts:

- `TutorialManager.cs`
- `TutorialArrowManager.cs`
- `TutorialDialogueUI.cs`
- `TutorialHintTextUI.cs`
- `TutorialPhaseGuidanceDriver.cs`
- `TutorialPracticeArrowDriver.cs`
- `TutorialInteractionLocker.cs`
- `TutorialRoleSwitchIntro.cs`
- `TutorialRoleHighlight.cs`
- `TutorialSceneWatcher.cs`
- `TutorialGroupWatcher.cs`
- `TutorialPlaySessionManager.cs`
- `TutorialCashierArrivalGate.cs`
- `BoothAssignArrowManager.cs`
- `BoothAssignArrowUI.cs`
- `BusserSinkPointer.cs`
- all root `TutorialWaiter*` scripts
- all root `TutorialCashier*` scripts

Notes:

- Root-level tutorial scripts should either be moved here or archived once confirmed unused.
- Tutorial scripts should not silently duplicate normal gameplay logic.

### UI

Purpose: reusable UI helpers and common world-space UI behavior.

Recommended scripts:

- `UIRoot.cs`
- `UIFollowWorldPoint.cs`
- `UIBounceAnimator.cs`
- `UiShake.cs`
- `WallFadeController.cs`
- `TipPopupUI.cs`
- `AlmanacCardUI.cs`
- `AlmanacListPopulator.cs`
- `TableNumberUI.cs`
- `BillBubbleUI.cs`
- `AngryBubbleUI.cs`
- `ProcessingBillIndicatorUI.cs`
- `GameplayUIBlocker.cs`
- `WarningSlideUI.cs`

### Networking

Purpose: project-owned PlayFab/Photon wrappers only.

Recommended scripts:

- `PlayfabManager.cs`
- `PlayfabMenuUIBinder.cs`
- `PhotonStateWatcher.cs`
- `PhotonBootstrap.cs`
- `PhotonDisconnectTrap.cs`
- `PhotonStateProbe.cs`
- `NetworkPlayerSpawner.cs`
- `RoomManager.cs`
- `RoomCodeDisplay.cs`
- `CreateJoinUI.cs`
- `JoinRoomUI.cs`
- `NetworkPlayerMovementSync.cs`
- `PhotonCustomizationSync.cs`
- `PhotonPlayerCustomizationApplie.cs`

Notes:

- Keep vendor Photon SDK scripts in `Assets/Photon`.
- Keep project networking wrappers separate from SDK code.

### Debug

Purpose: diagnostics, test-only watchers, dev panels, and temporary probes.

Recommended scripts:

- `DevSettingsConsole.cs`
- `ApprovalDebugWatcher.cs`
- `KitchenDebugReader.cs`
- `TutorialSpawnDebugger.cs`
- `OfficeInputDiagnostic.cs`
- `RaycastDebugger.cs`
- `UIDeactivationWatcher.cs`

Notes:

- Confirm these are not included in final thesis/demo scenes unless intentionally needed.

## 2. Refactoring & Reusability Targets

### Critical Bloated Scripts

#### `TutorialManager.cs`

Approximate size: 3016 lines  
Current risk: very high

Likely responsibilities currently mixed:

- tutorial phase state
- tutorial day configuration
- dialogue sequencing
- arrows and target guidance
- role gating
- object spawning
- cashier behavior
- tutorial scene loading
- tutorial validation
- direct object searches

Recommended split:

- `TutorialPhaseController`
- `TutorialDayConfig`
- `TutorialDialogueController`
- `TutorialObjectiveTracker`
- `TutorialSceneRouter`
- `TutorialSpawnController`
- `TutorialRoleGate`
- `TutorialArrowDirector`

#### `CustomerGroup.cs`

Approximate size: 1831 lines  
Current risk: very high

Likely responsibilities currently mixed:

- customer group state
- customer movement/seat flow
- patience and anger
- order generation/state
- UI bubble spawning
- table assignment
- eating state
- bill/payment flow
- cleanup/despawn

Recommended split:

- `CustomerGroupState`
- `CustomerOrderController`
- `CustomerPatienceController`
- `CustomerBoothAssignment`
- `CustomerPaymentController`
- `CustomerUIController`
- `CustomerExitController`

#### `OrderChecklistUI.cs`

Approximate size: 854 lines  
Current risk: high

Likely responsibilities currently mixed:

- UI binding
- toggle state
- order validation
- pricing
- typewriter text
- tutorial hints
- inventory/availability display

Recommended split:

- `OrderChecklistView`
- `OrderSelectionState`
- `OrderPricingService`
- `OrderValidationService`
- `OrderAvailabilityPresenter`

#### `GameManager.cs`

Approximate size: 663 lines  
Current risk: high

Potential overlap:

- `GameFlowManager`
- `DailyObjectiveManager`
- `AlienApprovalManager`
- `GameplayUIBlocker`
- `WarningSlideUI`

Action:

- Decide whether `GameManager` owns in-game shift logic only.
- Move global day/scene flow to `GameFlowManager`.

#### `PlayerMovement.cs`

Approximate size: 562 lines  
Current risk: high

Likely responsibilities currently mixed:

- click/touch detection
- NavMesh movement
- interaction target selection
- task cancellation
- animation helper updates
- camera injection
- multiplayer ownership checks

Recommended split:

- `PlayerInputReader`
- `PlayerNavMover`
- `PlayerInteractionController`
- `PlayerTaskState`
- `PlayerCameraBinding`

#### `CashierRegisterUI.cs`

Approximate size: 537 lines  
Current risk: medium-high

Likely responsibilities currently mixed:

- register screen UI
- open/close state
- payment calculation
- scene hierarchy recovery
- external deactivation detection

Recommended split:

- `CashierRegisterView`
- `CashierPaymentCalculator`
- `CashierRegisterController`
- `CashierRegisterLifecycleGuard`

#### `PlayfabManager.cs`

Approximate size: 588 lines  
Current risk: depends on multiplayer scope

Likely responsibilities currently mixed:

- PlayFab login
- account/profile state
- UI feedback
- Photon handoff
- player customization persistence

Recommended split:

- `PlayFabAuthService`
- `PlayerProfileService`
- `NetworkSessionBootstrap`
- `PlayFabLoginViewBinder`

#### `FoodTrayInteractable.cs`

Approximate size: 367 lines  
Current risk: medium

Likely responsibilities currently mixed:

- tray pickup
- delivery validation
- UI pickup button spawning
- sink/cleaning coordination
- cancellation/return behavior

Recommended split:

- `FoodTrayPickupController`
- `FoodTrayDeliveryController`
- `FoodTrayCleanState`
- `FoodTrayWorldUI`

## 3. Dead Code & Unused Objects Detector

This section identifies suspicious candidates only. Unity scenes and prefabs can reference scripts through serialized component data, so every candidate should be verified in scenes/prefabs before deletion.

### Duplicate Save Systems

Candidates:

- `GameSaveManager.cs`
- `LocalGameSaveManager.cs`
- `LocalSaveManager.cs`

Observed:

- `GameSaveManager.Instance` is referenced by active systems such as `GameFlowManager`, `AlienApprovalManager`, `UnlockManager`, and `MoneyManager`.
- `LocalGameSaveManager` appears to be a migration bridge from PlayerPrefs to the JSON save.
- `LocalSaveManager` appears separate and may be older.

Action:

- Keep `GameSaveManager` as primary unless testing proves otherwise.
- Mark `LocalGameSaveManager` as migration-only.
- Confirm whether `LocalSaveManager` is still used in active scenes.

### Duplicate Movement Systems

Candidates:

- `Assets/Scripts/Player Scripts/Movement/PlayerMovement.cs`
- `Assets/Assets/MAINGAME/GameScene/Kitchen/Scripts/RevisedPlayerMovement.cs`
- `Assets/Assets/MAINGAME/GameScene/Office/Scripts/RevisedPlayerMovement.cs`
- `Assets/Assets/MAINGAME/GameScene/Office/Scripts/PlayerMovement.cs`
- `Assets/Assets/MAINGAME/GameScene/Office/Scripts/MoveToTarget.cs`
- `Assets/Assets/MAINGAME/Player/Scripts/Movement/Movement.cs`

Observed:

- Main gameplay interactables use `PlayerMovement`.
- Kitchen systems use `KitchenPlayerMovement`, but the file is named `RevisedPlayerMovement.cs`.
- Office systems include `SimplePlayerMovement`, `PlayerMove`, and `UIButtonMove`.
- `UIButtonMove` is referenced in Kitchen scene files, which suggests confusing cross-scene reuse or legacy wiring.

Action:

- Identify which movement script is attached in each build scene.
- Keep one movement controller per scene role.
- Rename files so file names match class names.

### Debug-Only Candidates

Candidates:

- `ApprovalDebugWatcher.cs`
- `KitchenDebugReader.cs`
- `TutorialSpawnDebugger.cs`
- `OfficeInputDiagnostic.cs`
- `RaycastDebugger.cs`
- `UIDeactivationWatcher.cs`
- `DevSettingsConsole.cs`

Action:

- Move to `Assets/_Project/Debug`.
- Remove from production scenes unless intentionally needed for thesis demo safety.

### Root-Level Script Clutter

Root `Assets` contains many project scripts mixed with asset folders.

Examples:

- `GameFlowManager.cs`
- `GameSaveManager.cs`
- `CoreManagersBridge.cs`
- `TutorialWaiter*.cs`
- `TutorialCashier*.cs`
- `Takeout*.cs`
- `Lobby*.cs`
- `OfficeStart*.cs`

Action:

- Move project-authored scripts under `Assets/_Project`.
- Leave non-project package folders untouched.

### Naming Mismatch Risks

These scripts have file/class naming mismatches or unclear names:

- `BoothMessHoldToCleanUI.cs` contains `BoothMessCleanUI`
- `RevisedPlayerMovement.cs` contains `KitchenPlayerMovement`
- Office `RevisedPlayerMovement.cs` contains `SimplePlayerMovement`
- Office `PlayerMovement.cs` contains `PlayerMove`
- `MoveToTarget.cs` contains `UIButtonMove`
- `PhotonPlayerCustomizationApplie.cs` appears misspelled
- `IngredientSloutUI.cs` appears misspelled

Action:

- Rename after confirming scene references and Unity meta GUIDs are preserved.
- Do not rename casually without checking serialized scene/prefab references.

### Vendor And Sample Clutter

Large third-party/sample areas:

- Photon demos
- TextMesh Pro examples
- PlayFab editor extensions
- QuickOutline samples

Action:

- Do not mix project-authored scripts into these folders.
- Remove demo/sample content only after confirming no active scene references them.

## 4. Next Steps Prioritization

### P0 - Refactor First

- Audit and split `TutorialManager.cs`.
- Decide the single save owner: `GameSaveManager` vs legacy local save scripts.
- Normalize movement ownership across lobby, office, and kitchen.
- Verify scene flow ownership between `GameFlowManager`, `GameManager`, `UIManager`, and `CoreManagersBridge`.
- Confirm Bootstrap-to-MainMenu-to-Office-to-Lobby-to-Kitchen route works from a clean launch.

### P1 - Refactor Next

- Split `CustomerGroup.cs` into state, order, patience, UI, booth assignment, and payment components.
- Split `OrderChecklistUI.cs` so pricing and validation are no longer locked inside UI.
- Refactor interactable flow around `PlayerMovement`, `IInteractable`, `FoodTrayInteractable`, `BillPaper`, and `MoneyPickup`.
- Move all root-level project scripts into domain folders.
- Group takeout scripts into their own feature folder if takeout remains part of the demo.

### P2 - Polish And Cleanup

- Move debug scripts into a dedicated Debug folder.
- Remove debug scripts from production/demo scenes unless intentionally used.
- Isolate or remove vendor demo/sample folders after confirming build scenes do not use them.
- Rename scripts with file/class mismatches.
- Clean up misspelled file/class names after preserving Unity references.

## Recommended Refactor Rule

Do not refactor by moving everything at once.

Recommended sequence:

1. Create `Assets/_Project`.
2. Move only low-risk UI/debug scripts first.
3. Verify Unity still compiles.
4. Move one domain at a time.
5. After each domain move, open affected scenes and check missing script warnings.
6. Only then split large scripts such as `TutorialManager` and `CustomerGroup`.

## Architecture Goal

The final project should make this easy to answer:

- Which scripts are core managers?
- Which scripts belong to lobby, kitchen, office, customers, or UI?
- Which scripts are debug-only?
- Which scripts are legacy/migration-only?
- Which script owns save/load?
- Which script owns scene flow?
- Which movement controller is active in each scene?

Once those questions are clear, bug localization and thesis demo stabilization become much faster.
