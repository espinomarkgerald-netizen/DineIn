# Dine In - Bugs and Fixes Checklist

**Last updated:** 1 August 2026  
**Use this during testing:** Start the game from `Bootstrap`, reproduce an issue, then record the scene, actions, Console message, and result beside the relevant item.

## Fix first (P0)

### 1. Full scene flow

Test this complete route:

`Bootstrap -> Main Menu -> Office -> Lobby -> Office -> Kitchen -> end of day -> next day`

The game uses persistent managers and scene-specific UI references. Starting directly from Office, Lobby, or Kitchen can hide startup and transition problems.

- [ ] Every scene loads correctly.
- [ ] Loading UI behaves correctly.
- [ ] Day text, money, approval, inventory, and objectives remain correct after each transition.
- [ ] Returning to the menu works.

### 2. Current uncommitted UI edits

The main project currently has uncommitted scene/UI changes.

- [ ] Office Settings button appears correctly.
- [ ] Settings button opens and closes the intended panel.
- [ ] Settings button does not overlap other Office UI.
- [ ] Main Menu text/layout changes look correct at the target resolution.
- [ ] Skybox and fallback font changes do not create visual problems.

### 3. Bootstrap loading-screen reference

`Bootstrap.unity` contains a new `bootstrapLoadingScreen` field with no object assigned.

- [ ] Start from Bootstrap and look for a missing-reference warning or missing loading screen.
- [ ] Assign the correct loading-screen object if it is required by the scene-loading script.
- [ ] Test a slow scene transition, not only an instant editor load.

### 4. Tutorials

Run the intended menu route for every tutorial.

- [ ] Office tutorial completes.
- [ ] Lobby tutorial completes.
- [ ] Kitchen tutorial completes.
- [ ] Tutorial skip buttons work.
- [ ] Tutorial reset works after restarting the game.
- [ ] No missing target, Canvas, GroupSpawner, cashier UI, or role-controller errors appear.

### 5. Save, load, and reset

The project contains the newer `GameSaveManager` and older local-save scripts. Confirm they do not conflict.

- [ ] Start a clean new run.
- [ ] Quit and relaunch; the correct save loads.
- [ ] Test a save made in Office, Lobby, and Kitchen.
- [ ] Reach game over, select Try Again, and confirm time, money, approval, day, inventory, and unlocks reset correctly.
- [ ] Confirm tutorial reset does not damage a normal save.

## Fix next (P1)

### 6. Garbled text / encoding

Some source strings contain garbled symbols, most noticeably in `Assets/Scripts/Gameplay/GameOverScreen.cs`.

- [ ] Check the game-over screen for bad em dashes, peso symbols, check marks, and divider lines.
- [ ] Replace bad characters with correct UTF-8 text or clear plain-text alternatives.
- [ ] Verify the result in a standalone build, not only in the editor.

### 7. Missing Inspector references

These systems depend on scene/prefab assignments and should be checked when their route is tested:

- [ ] GroupSpawner and customer prefabs/spawn points.
- [ ] KitchenManager food-tray and takeout-bag prefabs plus spawn points.
- [ ] Employee generator and salary configuration.
- [ ] Inventory, recipe, equipment, and shop UI prefabs.
- [ ] Gameplay and world-space UI canvases.
- [ ] Waiter/busser tray, bill, and money hand points.
- [ ] GameOverScreen reference and root-level persistence.

**Rule:** if a tested route produces a missing-reference Console warning/error, treat it as a bug and write the exact message below.

### 8. Duplicate / old scripts

There are older and newer implementations for some systems, especially saving, movement, and bridge/manager scripts.

- [ ] Find which scripts are attached in each enabled build scene.
- [ ] Confirm only one system owns saving.
- [ ] Confirm only one player-movement setup is active per scene.
- [ ] Archive or remove scripts only after confirming they are unused.

### 9. Multiplayer scope

Photon and PlayFab are included, but online play has a much larger test surface.

- [ ] Decide whether multiplayer is required for the thesis defense.
- [ ] If not required, make the single-player route the stable supported demo.
- [ ] If required, separately test login, create room, join room, player spawning, movement sync, customization, disconnect, and return to menu.

## Polish and delivery (P2)

- [ ] Test Main Menu, Office, Lobby, and Kitchen at the final target resolution.
- [ ] Confirm settings persistence, music volume, SFX volume, and quality controls work as expected.
- [ ] Make a clean standalone build and test it without the Unity editor.
- [ ] Delete/reset test save data before every thesis demo.
- [ ] Commit verified scene/UI changes with a clear commit message.

## Bug report template

Copy this under the relevant item whenever you find a problem:

```text
Date:
Priority: P0 / P1 / P2
Scene:
Role / player count:
Steps to reproduce:
Expected result:
Actual result:
First Console error or warning:
Status: Open / Fixed / Retest
```
