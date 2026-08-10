# Dine In - Name Cleanup And Validation Scan

Scan date: 10 August 2026  
Scope: `Assets/_Project` after script organization and Unity reimport.

This document is an audit only. No code was renamed or refactored during this scan.

## 1. Name Cleanup Audit

### High-Confidence Rename Candidates

These files have a main Unity component class whose name does not match the file name. Rename only after confirming Unity keeps the `.meta` file/GUID.

| Current File | Declared Class | Risk / Note |
| --- | --- | --- |
| `Assets/_Project/Gameplay/GameManager/GameManager.cs` | `GameDayManager` | Important gameplay manager. Rename later, not during broad cleanup. |
| `Assets/_Project/Gameplay/GameManager/UFORoam.cs` | `UFORoamer` | Low-medium risk. Simple typo/style cleanup. |
| `Assets/_Project/Kitchen/MainGameScene/RevisedPlayerMovement.cs` | `KitchenPlayerMovement` | High-value cleanup. Current file name hides the real class purpose. |
| `Assets/_Project/MainMenu/buttonAnimator.cs` | `ButtonBounceAnimator` | Low risk. Also casing issue in file name. |
| `Assets/_Project/MainMenu/TabsManager.cs` | `TabManager` | Low-medium risk. |
| `Assets/_Project/Networking/Multiplayer/PhotonPlayerCustomizationApplie.cs` | `PhotonPlayerCustomizationApplier` | Clear spelling issue. |
| `Assets/_Project/Networking/Photon/CreateJoinUI.cs` | `CreateRoomUI` | Medium risk. Confirm whether file is intentionally broader than class. |
| `Assets/_Project/Office/MainGameScene/Manager/IngredientSloutUI.cs` | `IngredientSlotUI` | Clear spelling issue. |
| `Assets/_Project/Office/MainGameScene/MoveToTarget.cs` | `UIButtonMove` | Medium risk. File name is generic; class is UI-specific. |
| `Assets/_Project/Office/MainGameScene/PlayerMovement.cs` | `PlayerMove` | Medium-high risk because there are multiple player movement scripts. |
| `Assets/_Project/Office/MainGameScene/RevisedPlayerMovement.cs` | `SimplePlayerMovement` | Medium-high risk. Current name is ambiguous/legacy. |
| `Assets/_Project/Player/MAINGAME/Movement/Movement.cs` | `PlayerAction` | Medium risk. Likely legacy or duplicate movement logic. |
| `Assets/_Project/Restaurant/Booths/BoothMessHoldToCleanUI.cs` | `BoothMessCleanUI` | Low-medium risk. File and class describe similar behavior but should be aligned. |
| `Assets/_Project/Restaurant/Managers/HoldToCleanUI.cs` | `BoothHoldToCleanUI` | Medium risk. Could conflict conceptually with `BoothMessCleanUI`. |
| `Assets/_Project/SceneManagement/SceneSwitcher.cs` | `SceneManagerUI` | Medium risk. Rename after checking button bindings. |
| `Assets/_Project/Tutorials/TutorialCashierRuntime.cs` | `TutorialCashierTotalsReader` | Low-medium risk. |
| `Assets/_Project/UI/UIBounceAnimator.cs` | `UIElementAnimator` | Low risk. |
| `Assets/_Project/UI/WallFadeController.cs` | `FakeWallFadeSwapController` | Low-medium risk. |

### False Positives / Data Containers

These files triggered the file/class mismatch scan because the first declared type is a nested data class or data model. Do not rename based only on this scan.

| File | First Declared Type | Note |
| --- | --- | --- |
| `Assets/_Project/Gameplay/DailyObjectiveManager.cs` | `ObjectiveDefinition` | Likely contains manager later in file. |
| `Assets/_Project/Kitchen/MainGameScene/KitchenTutorialManager.cs` | `TutorialStep` | Data type appears before manager class. |
| `Assets/_Project/Kitchen/MainGameScene/Plate.cs` | `PlatingRecipe` | Data type appears before `Plate`. |
| `Assets/_Project/Office/MainGameScene/Finance/FinanceManager.cs` | `Expense` | Data type appears before manager class. |
| `Assets/_Project/Office/MainGameScene/Inventory/Recipe.cs` | `RecipeIngredient` | Data type appears before `Recipe`. |
| `Assets/_Project/Save/GameSaveData.cs` | `InventorySaveEntry` | Data model file can contain multiple save models. |
| `Assets/_Project/Save/LocalSaveManager.cs` | `SaveData` | Static save helper plus data type. |

## 2. Compile-Safe Validation Scan

### Project-Owned C# Location

Result: clean.

- Project-owned scripts under `Assets/_Project`: 295
- Project-owned scripts outside `_Project`: none found
- Vendor/sample folders left outside `_Project`: Photon, PlayFab, TextMesh Pro, QuickOutline, TutorialInfo

### Role-Switching Dependency Count

The new game direction removes role switching, but old role systems are still heavily referenced.

| Pattern | Count |
| --- | ---: |
| `RoleManager` | 94 |
| `StaffRole` | 59 |
| `GetActivePlayerMovement` | 11 |
| `RoleBasedAssignController` | 4 |
| `RoleSwitchWarningUI` | 1 |

Impact:

- Role switching is still structurally embedded.
- Removing it will require a task-based replacement, not a simple script deletion.

Recommended next architecture target:

```text
Role-based access -> RestaurantTask access
Role-specific player -> Manager/helper player
Role-owned tasks -> Bot/player task ownership
```

### Input System Mixed Usage

| Pattern | Count |
| --- | ---: |
| `Input.Get` | 44 |
| `UnityEngine.InputSystem` | 27 |

Impact:

- The project still mixes old Unity input and the new Input System.
- This can cause click/touch failures depending on Unity's Active Input Handling setting.

Recommendation:

- Do not fix all at once.
- Standardize input when the movement/interaction system is converted to manager/task-based gameplay.

### Scene And Object Lookup Usage

| Pattern | Count |
| --- | ---: |
| `FindFirstObjectByType` | 52 |
| `GameObject.Find` | 5 |
| `FindObjectOfType` | 3 |

Impact:

- Many systems discover dependencies at runtime.
- This is acceptable short-term but fragile during the one-scene migration.

Recommendation:

- Centralize core references through scene bootstrap or a gameplay service locator.
- Reduce direct scene searches in large systems such as tutorial, customer, order, and kitchen flow.

### Persistent Manager Usage

| Pattern | Count |
| --- | ---: |
| `DontDestroyOnLoad` | 41 |

Impact:

- The old multi-scene architecture depends heavily on persistent objects.
- The new one-scene gameplay model can reduce this over time.

Recommendation:

- Keep current persistent managers until gameplay is stable.
- Later, reduce persistent state to only save/account/session systems.

### Scene Loading Usage

| Pattern | Count |
| --- | ---: |
| `SceneManager.LoadScene` | 19 |

Impact:

- Multiple systems still assume scene transitions.
- This conflicts with the new one-scene gameplay plan.

Recommendation:

- Audit scene loading calls before collapsing Office/Lobby/Kitchen into one gameplay scene.
- Replace phase scene loads with UI panels, area activation, or day-state transitions.

### Console Noise

| Pattern | Count |
| --- | ---: |
| `Debug.Log(` | 296 |
| `PhotonState` | 7 |

Impact:

- Runtime logs are noisy.
- `[PhotonState]` is the visible repeated log in the editor Console.

Recommendation:

- First silence `PhotonStateWatcher` or gate it behind a debug flag.
- Later, wrap development logs in a project debug setting.

## 3. Immediate Next Recommendations

### Safe Next Step

Rename only low-risk misspelled files first:

1. `PhotonPlayerCustomizationApplie.cs` -> `PhotonPlayerCustomizationApplier.cs`
2. `IngredientSloutUI.cs` -> `IngredientSlotUI.cs`
3. `buttonAnimator.cs` -> `ButtonBounceAnimator.cs`

### Do Not Rename Yet

Delay these until we start actual architecture refactor:

- `GameManager.cs` / `GameDayManager`
- all `PlayerMovement.cs`, `RevisedPlayerMovement.cs`, and `Movement.cs` variants
- `SceneSwitcher.cs` / `SceneManagerUI`
- `HoldToCleanUI.cs` / `BoothHoldToCleanUI`

Reason:

- These are more likely to have scene button bindings, duplicate-class confusion, or architectural replacement work tied to the new manager/bot plan.

## 4. Claude Handoff Format

**Target Objective:** Clean up file/class naming mismatches after project organization.

**Target Script/File:**  
Primary low-risk candidates:
- `Assets/_Project/Networking/Multiplayer/PhotonPlayerCustomizationApplie.cs`
- `Assets/_Project/Office/MainGameScene/Manager/IngredientSloutUI.cs`
- `Assets/_Project/MainMenu/buttonAnimator.cs`

**Dependencies & Context:**  
Unity scene/prefab references are preserved by `.meta` GUIDs if files are renamed together with their `.meta` files. Do not change class names unless references and compile impact are checked.

**Exact Problem / Root Cause:**  
Several file names do not match their main declared classes. This makes Unity script lookup, search, and maintenance confusing after moving all project scripts into `Assets/_Project`.

**Constraints:**  
Do not refactor behavior. Do not rename high-risk movement or scene manager scripts yet. Preserve `.meta` files and validate Unity reimport after each small rename batch.
