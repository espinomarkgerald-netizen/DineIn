# Dine In — Thesis Project Recap

**Last reviewed:** 31 July 2026  
**Main Unity project:** `G:\Unity Projects\DineIn\DineIn`  
**Separate menu prototype:** `G:\Unity Projects\New Dinein` — do not treat this as the main game source.

## Read this first after a break

The main game is a Unity 6 restaurant-management / role-playing thesis game with an alien-survival premise. The player runs a diner whose alien customers determine whether Earth is spared. A game day is divided into **management**, a **lobby/front-of-house shift**, and a **kitchen shift**. The project already has the core systems, tutorials, progression, saving, and end conditions in code. It is **not yet safe to call fully finished**: the scene-to-scene flow, Inspector references, tutorial routes, and the latest menu/UI edits need a focused end-to-end test pass.

The practical restart point is: **preserve the current uncommitted work, run the complete player journey from Bootstrap, then fix only confirmed broken handoffs before adding features.**

---

## 1. What the game is

### Elevator pitch

*Dine In* is a cooperative/single-player restaurant operations game. Players manage a restaurant serving alien customers, balance money and customer approval, and survive a 30-day campaign to prevent an alien conquest of Earth.

### Player loop

```text
Bootstrap → Main Menu → Management Office
                         │
                         ├─ hire/assign staff, buy stock/equipment, review finances
                         │
                         ▼
                     Lobby shift (morning)
                         │ host / waiter / cashier / busser / takeout work
                         ▼
                  Management Office (afternoon)
                         │
                         ▼
                     Kitchen shift
                         │ cook and fulfill orders
                         ▼
          daily finance + objectives + approval evaluation
                         │
                  next day, or a campaign ending
```

### Win / loss conditions implemented in code

* The campaign ends at **Day 30**.
* Earth is saved when Day 30 is reached with **Alien Approval of at least 40**.
* The run ends early if money reaches zero after daily expenses (**bankruptcy**) or alien approval reaches zero (**approval collapsed**).
* Approval starts at 50 by default; happy, neutral, and angry customer results adjust it. Daily objectives also apply an approval bonus or penalty.

Primary flow owner: `Assets/GameFlowManager.cs`.

---

## 2. Project map

### Entry and build scenes

The build profile currently enables:

1. `Assets/Scenes/Bootstrap.unity`
2. `Assets/Scenes/MainMenu.unity`
3. `Assets/Assets/MAINGAME/GameScene/Office.unity`
4. `Assets/Assets/MAINGAME/GameScene/Lobby1.unity`
5. `Assets/Assets/MAINGAME/GameScene/Kitchen.unity`
6. `Assets/Assets/MAINGAME/GameScene/LobbyTutorial.unity`
7. `Assets/Assets/MAINGAME/GameScene/OfficeTutorial.unity`
8. `Assets/Assets/MAINGAME/GameScene/Multiplayer.unity`

`CoreGameplay.unity` and `WaiterLevel1.unity` exist but are not enabled for builds. Start testing from **Bootstrap**, not by opening a later scene directly; several singleton systems expect that path.

### Major implemented systems

| Area | Current implementation |
| --- | --- |
| Campaign flow | `GameFlowManager`: day state, office/lobby/kitchen transitions, 30-day evaluation, reset flow. |
| Alien approval | `AlienApprovalManager`, approval HUD, demands panel, approval-driven group spawn modifier. |
| Objectives and difficulty | `DailyObjectiveManager` chooses mandatory/secondary/bonus objectives; `ShiftScaler` increases groups and reduces patience by day. |
| Management | HR/staff assignment, payroll, inventory, purchasing, equipment and recipe unlocks, finance and daily reports. |
| Lobby gameplay | Customers/groups, booth assignment, host/waiter/cashier/busser roles, bills/payments, cleaning, queues, takeout. |
| Kitchen gameplay | Ingredients, grill/fryer/drink stations, plates, orders, delivery counter, kitchen tutorial and performance UI. |
| Tutorials | Dedicated lobby, office, and kitchen tutorial scenes plus role-specific guidance scripts. |
| Persistence | `GameSaveManager` saves day/phase, money, approval, unlocks, and inventory to `dinein_save.json` in Unity's persistent-data folder. |
| Multiplayer / identity | Photon PUN and PlayFab code, room create/join flow, player spawn/customization/network movement. |
| UI / quality of life | Loading screen, settings, FPS tools, outlines, camera controls, world-space feedback bubbles. |

### Important third-party / Unity dependencies

* Unity editor: **6000.0.40f1**
* Universal Render Pipeline, Input System, AI Navigation, Cinemachine
* Photon PUN / Realtime and PlayFab
* Unity multiplayer packages are present as well

---

## 3. Where work stopped

### Latest committed work

The latest main-project commit is **14 July 2026**: `41072ca — Ui fix`. The immediately preceding April commits were focused on final-defense readiness, tutorial completion, kitchen completion UI, tutorial skip buttons, and UI fixes.

### Current uncommitted changes — protect these

Do **not** discard or overwrite these without first checking them in Unity and committing or copying them:

* `Assets/Assets/MAINGAME/GameScene/Office.unity`
  * Adds a Settings button wired to `UIManager.ToggleSettings` and adjusts some office UI visibility/layout.
* `Assets/Scenes/Bootstrap.unity`
  * Adds the `bootstrapLoadingScreen` field to the bootstrap scene object, currently with no assigned object.
* `Assets/Scenes/MainMenu.unity`
  * Adjusts a text element's size and position.
* `Assets/Assets/World/Skybox/BlueSkyBox.mat`
  * Small material change.
* `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset`
  * Font-asset change; confirm it has not altered fallback rendering unexpectedly.

These are scene/asset edits, not scripts. They need visual verification in Unity before a commit.

---

## 4. Known risks and likely fixes

This is a risk list from code and project structure, **not a claim that every item is broken at runtime**. Verify each item in Play mode and record the result.

### P0 — test before changing gameplay

1. **Full startup and scene chain**
   * Test `Bootstrap → MainMenu → Office → Lobby1 → Office → Kitchen → end-of-day`.
   * The game uses persistent singleton managers and scene-local references. Starting in a later scene can hide startup problems.
   * Confirm loading UI, day text, game-over UI, and menu return behavior all still work after several scene changes.

2. **Current uncommitted Office settings button**
   * Confirm the new button appears correctly, opens/closes settings, and does not overlap other office UI at target resolution.

3. **Tutorial journey**
   * Run each tutorial once from the intended menu route: office, lobby, and kitchen.
   * Tutorials have many scene references and role gates; their code deliberately warns when a target, canvas, GroupSpawner, or cashier UI is missing.

4. **Save and reset behavior**
   * Test a new run, quit/relaunch, load, game over, Try Again, and tutorial reset.
   * There is an active `GameSaveManager` plus older `LocalGameSaveManager` / `LocalSaveManager` scripts in the project. Confirm only the intended save path is active and that an old save cannot produce a confusing state.

### P1 — architecture / content cleanup

5. **Inspector reference audit**
   * Many components are intentionally defensive, but important gameplay paths rely on Inspector wiring: GroupSpawner, KitchenManager spawn points/prefabs, employee generator/salary config, shop prefabs, UI canvases, role hand points, and GameOverScreen.
   * Use Unity's Console during the P0 test. Treat missing-reference messages on a tested route as bugs, not as noise.

6. **Text encoding cleanup**
   * Some code strings display mojibake (garbled em-dash, peso, and check-mark characters) in source. These are most visible in `GameOverScreen.cs` and some comments/logs.
   * Replace them with correct UTF-8 characters or plain ASCII, then confirm the end screen renders the peso symbol and separators correctly.

7. **Duplicate / legacy scripts**
   * The project has older and newer implementations side-by-side in several areas (notably saving, player movement, and some root-level bridge scripts). Do not delete them blindly—first identify which components are attached in each build scene. Then archive or remove truly unused copies to make future debugging safer.

8. **Multiplayer scope decision**
   * Photon/PlayFab systems are present, but they increase test surface substantially. Decide whether the thesis demo must prove online multiplayer. If not, make the single-player path the supported presentation path and test multiplayer separately.

### P2 — polish / delivery

9. Verify target-resolution layout for Main Menu, Office, lobby UI, and kitchen UI.
10. Check audio/settings persistence and whether music/SFX sliders are actually routed to an AudioMixer.
11. Make a clean build and test it outside the editor, including a first launch with no save file.
12. Capture a short final demo path and reset save data before each defense/demo run.

---

## 5. Separate project: New Dinein menu prototype

**Location:** `G:\Unity Projects\New Dinein`  
**Purpose:** new main-menu work only.

### What is in it

* A separate Unity 6.0.40f1 project with two enabled scenes: `MainMenu.unity` and `GameMenu.unity`.
* Main-menu visuals, menu animation, loading screen, settings, audio, camera rigs, a restaurant selector, and generic scene loading.
* `SceneLoader` is a persistent loading-screen controller; `SceneTrigger` lets buttons load a named scene or build index.

### Current integration status

This prototype is **not integrated** into the main thesis project. It has its own assets, scene names, package state, settings implementation, and no Git repository visible at its root. Treat it as a source of menu assets/behaviour, not as a replacement for the main project.

### Safe integration approach

1. Back up or commit the main project’s current scene changes first.
2. Duplicate the main project before importing anything from the prototype.
3. Import only the selected menu assets, prefabs, animations, and scripts into a dedicated `Assets/MainMenuV2/` folder in the main project.
4. Make the new menu load the main project’s existing routes—especially `Bootstrap`, tutorials, Office, and multiplayer—not the prototype's `GameMenu` placeholder flow.
5. Resolve duplicated settings, loading-screen, PlayFab, and scene-loader systems deliberately. Keep one owner for each global system.
6. Test the new menu from a clean build before replacing the existing `MainMenu.unity` in the build profile.

---

## 6. Recommended first week back

### Day 1: establish a safe baseline

- [ ] Open `G:\Unity Projects\DineIn\DineIn` in Unity 6000.0.40f1.
- [ ] Let Unity finish import/compile; capture all Console errors and the first occurrence of each warning.
- [ ] Inspect the five uncommitted files listed above; commit them with a clear message if correct, or revert only the confirmed unwanted edits.
- [ ] Make a separate test build from the current main project.

### Day 2: complete one critical playthrough

- [ ] Start from Bootstrap and test the menu, Office, Lobby, return to Office, Kitchen, daily evaluation, and next-day transition.
- [ ] Check money, approval, objective progress, inventory/unlocks, and day text at every transition.
- [ ] Test failure paths using the debug tools only if needed: bankruptcy, zero approval, Day 30 win/loss.

### Day 3: tutorials and saving

- [ ] Complete office, lobby, and kitchen tutorials.
- [ ] Verify skip/reset buttons, save/load, Try Again, and menu return.
- [ ] Create a short issue list with exact reproduction steps, expected result, actual result, scene, and Console message.

### Day 4 onward: fix in priority order

1. Broken scene flow, loading, save, or game-over/reset paths.
2. Missing references and tutorial blockers.
3. Essential presentation/UI defects.
4. Menu-prototype integration only after the core demo path is stable.
5. Nice-to-have multiplayer and polish work last.

---

## 7. Working rules for the team

* Start from `Bootstrap` when testing the whole game.
* Keep one task per commit and use meaningful commit messages; avoid `test` commits for milestones.
* Do not change a shared scene and a core manager in the same untested batch if it can be avoided.
* Before editing an old/duplicate-looking script, first search which scene or prefab references it.
* When a bug is found, record: scene, role, exact actions, expected behavior, actual behavior, and first Console error/warning.
* Preserve the main game and menu prototype as separate projects until a deliberate, tested migration is complete.

---

## 8. Useful locations

| Need | Location |
| --- | --- |
| Main project | `G:\Unity Projects\DineIn\DineIn` |
| Flow and end conditions | `Assets/GameFlowManager.cs`, `Assets/Scripts/Gameplay/GameOverScreen.cs`, `Assets/Scripts/Gameplay/GameOverReason.cs` |
| Approval / objectives / difficulty | `Assets/Scripts/Gameplay/AlienApprovalManager.cs`, `DailyObjectiveManager.cs`, `ShiftScaler.cs` |
| Saving | `Assets/GameSaveManager.cs`, `Assets/GameSaveData.cs` |
| Main build scene list | `ProjectSettings/EditorBuildSettings.asset` |
| Office / lobby / kitchen scenes | `Assets/Assets/MAINGAME/GameScene/` |
| Menu prototype | `G:\Unity Projects\New Dinein` |

## 9. Open questions to answer during the first review meeting

1. Is online multiplayer required for the thesis presentation, or is it an optional feature?
2. Which menu version is intended for the final build: existing main-project menu or the separate prototype?
3. What is the target platform and resolution for evaluation?
4. Which tutorial route is mandatory in the final demo?
5. What exact features are required for the thesis rubric, versus polish ideas that can be deferred?

Answering these will turn the restart checklist into a realistic final-sprint plan.
