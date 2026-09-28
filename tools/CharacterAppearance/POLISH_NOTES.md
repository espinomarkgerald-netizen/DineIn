# Character appearance polish — 28 September 2026

## Corrections

- The manager inherited its larger legacy model/root scale, whereas employees inherited astronaut proportions. Gameplay appearance bindings now opt into one catalog-controlled adult body scale. Only the generated visual child is scaled; gameplay roots, agents, colliders, controllers, task references and network transforms are unchanged. The menu keeps separate preview framing.
- Twelve authored legacy head-socket hat renderers were missing from `originalRenderers`. They are now included in the existing hide/restore ownership path, preventing duplicate chef hats without deleting the original visuals.
- Chef, cowboy and witch headwear were fitted to the supplied heads. Blanket `hidesHair` settings are off. The tall `female-hair-03` ponytail is a single, inseparable mesh and uses an explicit per-hat fallback; removing a hat restores that hairstyle. All other supplied hairstyles remain visible. Hair/Hat IDs are never rewritten by fitting. Bare-head offsets prevent the fallback hat floating above the scalp. Future non-covering accessories retain hair by default.
- The existing `RestaurantSelector` owns opt-in, menu-only turning, run/idle blending and camera-facing arrival. New selections cancel previous movement/arrival work. Customize suspends travel, settles toward the camera, and yields rotation ownership to the dedicated preview drag area. Closing resumes unfinished travel to the selected restaurant. Gameplay Animator assets and movement code are not changed.
- The existing panel now has a dedicated header, two tab rows, bounded cards, compact palette swatches, an independently scrolling grid, helper text above fixed buttons, and restored-on-close menu visibility. Camera framing includes headwear. Cosmetic/category changes do not reset the dragged viewing angle.
- Apply/Cancel and all local/cloud/Photon/employee data paths remain the existing implementations. Apply does not wait for its short existing Happy Idle reaction.

## Animation assets still needed

The inspected project contains compatible idle, run, carrying and Happy Idle clips, but no wave, body inspection, head/hair inspection or face-showcase clip. No procedural arm motion or unrelated work animation was substituted.

`RestaurantSelector` exposes optional `waveClip`, `bodyReaction`, `headReaction`, and `faceReaction` fields on NewGameMenu. These remain empty, so those gestures are **not yet present**. Assign suitable Humanoid clips to finish this part. `applyReaction` uses the existing Happy Idle clip. All one-shots share the same replacement/cancellation path; they do not queue or delay saving.

## Changed files / assets in this pass

- `Assets/_Project/Player/Customization/AppearanceCatalog.cs`
- `Assets/_Project/Player/Customization/CharacterAppearance.cs`
- `Assets/_Project/Player/Customization/AppearanceCustomizationPanel.cs`
- `Assets/_Project/Player/Customization/AppearancePreviewDrag.cs`
- `Assets/_Project/MainMenu/GameMenu/Scripts/Character/RestaurantSelector.cs`
- `Assets/Resources/AppearanceCatalog.asset`
- Three existing derived hat prefabs and their three thumbnails under `Assets/_Project/Player/Assets/Appearance/`.
- Existing appearance bindings in Manager, ManagerMultiplayer and Player prefabs; Lobby1, Lobby2, Lobby1 Multiplayer, Lobby1Tutorial and Lobby2Tutorial. Tutorial content/order is unchanged.
- Existing `NewGameMenu` hierarchy, preview Animator settings (menu-only in-place animation), and presentation references.
- Narrow inspection, authoring, preview and verification scripts beside this document. Original character assets, employee uniform assignments, save/network scripts and unrelated existing changes were preserved.

## Verification actually run

- Unity script compilation succeeded.
- 267 focused Edit-mode assertions: consistent visual scale before/after pose sampling, equal posed body size across player/staff bindings, unchanged gameplay roots/controllers/root-motion settings, legacy renderer restoration, all 14 hair choices × 4 headwear choices, immutable selection IDs and hairstyle restoration.
- Existing 130 in-memory data/compatibility/migration/account-guard assertions passed. No disk save or cloud/network requests.
- Existing 110 Edit-mode animation samples passed for both bodies across eleven player/staff sources, preserving gameplay references and task attachments.
- Isolated static menu renders inspected at 1280×720 and 1024×768, including body, palette and outfit grids. Headwear-fit contact sheets inspected; only changed hat thumbnails regenerated.
- Source `git diff --check -- '*.cs'` passed (Git emits existing line-ending notices).

These are not runtime acceptance tests. No Play Mode, build, desktop automation, live Photon/PlayFab or device input testing was performed.

## Short manual checklist

1. Rapidly switch restaurants; verify turn → run → camera-facing idle, and only the final arrival gesture once a wave is supplied.
2. Open Customize mid-travel, drag immediately, then change body/hair/hat/outfit. Rotation must remain yours; Apply/Cancel must restore controls and resume the selected destination without a stale animation.
3. Check mouse wheel/touch scrolling, preview-only rotation, Escape/Android Back, narrow landscape layouts, and hats fully in frame.
4. Inspect manager versus employee size, a kitchen employee's single hat, and carried props while walking/working. Reload/reassign staff and confirm identity, uniform and hair restoration remain intact.
5. Smoke-test existing Apply persistence and two-player appearance synchronization; those integrations were not rewritten or exercised live here.
