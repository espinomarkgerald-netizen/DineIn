# Player customization and employee appearance

Implemented 2026-09-28. This is a visual layer over the existing gameplay, saves, Photon roster, and PlayFab authentication flow.

## Where to use/edit it

- Open **NewGameMenu → Customize**. Seven categories: Body, Skin, Face, Hair, Hair Color, Outfit, Hats. Choices are a draft until Apply; Cancel/Back/Escape restores the committed selection. Only the dedicated preview region rotates the model.
- `Assets/Resources/AppearanceCatalog.asset`: stable IDs, compatibility, palettes, defaults, models, materials, head attachments, and thumbnails. Keep shipped IDs stable when rearranging entries.
- `Assets/Resources/CasualEmployeeUniforms.asset`: role/body uniform assignments.
- `Assets/Resources/FastFoodEmployeeUniforms.asset`: independent Fast Food assignments. These now use the supplied **FastFoodMale.png for male** and **FastFood.png for female**, not the temporary Casual Dining outfits. Existing chef headwear requirements remain editable here.
- Both new Fast Food outfits are also available to players. There are 16 outfit entries overall, filtered by body.
- `Assets/_Project/Player/Assets/Appearance/`: derived models, original texture copies, materials, headwear, and 38 rendered option thumbnails.
- `ArtSource/CharacterAppearance/`: preserved original ZIP and editable derived Blender rigs. The supplied files in Downloads were not modified.

## Ownership and integration

`CharacterAppearance` accepts an explicit recipe and changes only visual presentation. It retains the existing Animator component/controller and gameplay roots, colliders, agents, and task scripts. Both derived body meshes use the same verified bone order and bind poses. Imported hair transforms are retained when attaching to the head.

Existing skeleton-attached task anchor objects retain their identity and references. They stay under the gameplay root while customized, following calibrated new-rig bone poses before trolley presentation in LateUpdate. Calibration samples the same existing CarryIdle clip on both avatars; unrelated bind-pose axes must not be reused. Restoring a legacy appearance restores the exact original parent/local transform. Root-level task anchors stay untouched. Normal task completion and movement code are unchanged. See `REGRESSION_NOTES.md` for the follow-up fixes and verification limits.

`PlayerAppearanceBinding` applies local committed data or the owning remote player's compact `AppearanceV2` Photon property. Old profiles keep their original model until customization is applied. The existing legacy visual helper is called explicitly for V1 colors/headwear; remote appearance no longer writes into shared local customization data.

The existing `LocalSaveManager.SaveData.appearances` stores independent guest/account records, revisions, and pending-upload flags. There is no new save file and no player appearance in restaurant day-start checkpoints. Apply rolls back its in-memory commit if local saving fails.

`PlayFabAppearanceAdapter` is attached to the existing persistent `PlayFabAuthManager`. It uses the established `CustomizationV1` UserData key with a V2 payload. Requests capture authentication context; callbacks are gated by account/generation/revision. Uploads are serialized; offline changes remain pending. Missing/malformed cloud data and old V1 payloads cannot erase a modern local recipe. Draft preview does not respond to committed/cloud appearance refreshes.

Employees get deterministic personal traits from stable employee IDs without consuming Unity's gameplay RNG. Recipes travel in existing `EmployeeData`/`EmployeeSaveEntry` and management roster snapshots. Existing staff with missing/empty legacy recipes migrate in memory and persist through the normal save flow. Observers do not generate identities. Assignment changes overlay uniforms without changing personal traits. Tutorial staff continue to use the existing isolated tutorial roster/persistence handling.

## Files/scene scope

- New appearance scripts in `Assets/_Project/Player/Customization/` plus extensions of its legacy customization helpers.
- Existing `LocalSaveManager`, `GameSaveData`, employee generator/manager/data, `LobbyAutonomousService`, Photon publication/spawn entry points, and legacy PlayFab integration guards.
- Player visual bindings on `Manager`, `ManagerMultiplayer`, and `Player` prefabs.
- Menu panel in `NewGameMenu`; auth adapter in `NewMainMenu`.
- Staff visual bindings in Lobby1, Lobby2, Lobby1 Multiplayer, Lobby1Tutorial, and Lobby2Tutorial. No tutorial sequence or progression changes.
- Unity reserialized scene blocks/default fields when saving; the block-level diff check found no removed serialized objects. Unity's empty YAML fields produce trailing-space warnings in a whole-project `git diff --check`; they are not source compiler errors.

## Checks actually performed

- Unity script recompilation: passed, no compiler errors.
- Both derived Humanoid avatars: valid/isHuman; identical body bone order and bind poses.
- Edit-mode sampling of existing Idle, Walking, Running, WalkingCarry, and CarryIdle clips. Existing working behavior uses the same controller; no new work animation/controller was introduced.
- Applied both bodies to temporary instances of eight Lobby2 staff roots plus Manager, ManagerMultiplayer, and Player (110 pose samples): finite baked skin vertices; original gameplay components/controller/root settings preserved; existing task anchor references restored correctly.
- Offline pose-sheet inspection plus rendered outfit/hair/headwear thumbnail inspection. A discovered discarded-FBX-rotation issue was corrected before handoff.
- 130 focused in-memory assertions: catalog/defaults/compatibility, every uniform assignment, deterministic employee identity, legacy employee migration, JSON roundtrips, V1 readability, invalid IDs, draft-copy isolation, account/revision/pending-upload rejection guards, and preservation of other local-save fields. Test cache was isolated and restored; tests did not save player data or make network/cloud calls.
- Read-only scene checks: expected menu references, seven categories, EventSystem/GraphicRaycaster, one auth adapter, and staff visual bindings.
- Supplied Fast Food texture copies checked against originals; no generated substitute textures.

## Not run / manual acceptance checklist

No Play Mode sessions, builds, desktop automation, full tutorial walkthroughs, live Photon sessions, or PlayFab requests were run. Edit-mode numeric pose checks are not a substitute for seeing held props and deformation during gameplay.

1. NewGameMenu: open Customize, try both bodies and every category, rotate only over the preview, scroll options, test narrow/mobile layouts, Apply, restart, and verify persistence.
2. Cancel, Back/Escape/Android Back: restore committed appearance; restaurant navigation and Play controls return to their prior states. Open during a restaurant camera transition too.
3. Lobby1/Lobby2: walk/run, carry trays and bills, collect payment, work stations, and clean with both bodies. Inspect hand/prop alignment and the female skirt in motion. Required chef hats retain compatible hair; see `POLISH_NOTES.md` for the specific tall-ponytail fallback.
4. Staff: save/reload and reassign employees. Identity must stay the same while role/restaurant uniforms change; include the second cashier and both Fast Food lobby workers.
5. Tutorial sessions: staff visuals work, and returning to a career does not transfer tutorial staff data.
6. Two Photon players: distinct looks, property updates, late joining, and host-provided staff appearances without observer AI authority.
7. Offline Apply, restart/reconnect, logout/account switching, and delayed cloud responses: pending local changes survive; drafts are untouched; no guest/account leakage.

## Maintenance scripts

Scripts here are narrowly scoped development tools, not runtime components. `verify_data.cs`, `verify_application.cs`, `inspect_bindings.cs`, and `verify_scene_bindings.cs` run through the connected Unity Editor's `eval_file` in Edit Mode. `inspect_serialized_diff.py` reads Git/scene blocks without editing them.

Authoring scripts were used for initial setup. Inspect them before rerunning; do not indiscriminately rerun import/authoring steps over later artist/Inspector edits. `finalize_visual_authoring.cs` intentionally invalidates option thumbnails; `render_options.cs` regenerates missing thumbnails.
