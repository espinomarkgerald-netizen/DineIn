# Customization presentation polish

## Changes

- `AppearanceCustomizationPanel.cs`: Inspector-authored category framing, eased camera transitions, stable framing for similar hair/color selections, and aspect-preserving cards. Manual rotation remains authoritative; Cancel restores the menu camera.
- `AppearanceCatalog.cs` / `AppearanceCatalog.asset`: seven skin colors, ten hair colors, readable outfit labels, optional per-hair hat fit offsets and hat preview headroom. Stable existing IDs remain unchanged.
- `CharacterAppearance.cs`: optional derived sleeve skin weights and reversible hat-specific hair fitting. Gameplay roots, controllers, sockets and persisted recipes remain unchanged.
- `RestaurantSelector.cs`: category reaction pools using the existing animation playback path. Only the existing suitable Happy Idle clip is assigned; no new gestures were invented.
- `NewGameMenu.unity`: matching Apply/Cancel construction, clearer cards and labels, category presentation settings and reaction assignments.
- 38 existing thumbnail PNGs regenerated from actual character assets with category-specific framing. Two derived sleeve-skin mesh assets added.

## Causes and asset limits

The skin mesh extended inside the sleeves but its covered vertices followed lower-arm/hand bones rather than the garment's shoulder/upper-arm weighting. Derived meshes correct 73 of 264 vertex weights per body. Positions, triangles, UVs and bind poses are retained; original FBX assets are untouched.

Rear-angle inspection identified witch-hat crown intersections for male-hair-05, female-hair-02 and female-hair-05. Small attachment-local fit overrides retain their visible hair. Removing the hat restores the authored hair transform. The existing tall-ponytail incompatibility fallback remains because that hairstyle has no separate compatible mesh variant.

All 13 supplied hairstyles were already catalogued. No additional compatible hairstyle assets or suitable varied reaction clips were found. Reaction pools and fit offsets remain editable for future assets.

The installed Sprite Editor API lacked the required frame-edit capability interface. Shared sprite metadata was not changed; card contents were fitted inside the existing artwork instead.

## Configuration and maintenance

Tune category framing, thumbnail angles/margins, camera transition duration and viewport center on the existing menu customization component. Tune hat headroom and hair-fit entries in the appearance catalog. Reaction arrays are on RestaurantSelector.

`premium_presentation.cs` authors the initial settings and will overwrite presentation tuning if rerun. `render_options.cs` regenerates thumbnails using the scene settings. `author_sleeve_skin.cs` regenerates the derived weight-only meshes. These are focused Editor maintenance scripts, not runtime systems.

## Checks actually performed

- Unity compilation: no errors reported.
- Premium checks: 924 assertions covering mesh invariants, normalized weights, camera transitions, rapid selections, manual rotation, stable cosmetic framing and Cancel restoration.
- Menu regression: 190 assertions across three aspect ratios and UI scales 0.85/1/1.15.
- Data checks: 130 assertions; application checks: 110 pose samples; appearance/hat checks: 267 assertions; carry attachment checks: 42 assertions.
- Edit-mode rendered UI review at 16:9 and 4:3, front/side/rear hat contact sheets, and representative rear sleeve pose comparisons.
- Focused source/tool diff whitespace checks.

No Play Mode, builds, runtime animation playback, device testing, save writes, Photon, PlayFab or network verification was performed. Edit-mode sampled poses do not establish correctness for every animation frame.

## Short manual checklist

1. Open customization, switch categories rapidly, rotate manually, and compare small/large hats. Confirm framing feels comfortable and does not fight rotation.
2. Check mouse/touch scrolling, card labels, swatches and selection indicators; verify Cancel restores the previous appearance and Apply persists normally.
3. Inspect player and employee elbows while walking, carrying and working, including rear views. Confirm hats and retained hair remain acceptable throughout animation.
4. Verify original menu restaurant navigation and gameplay carrying/working behavior remain unchanged.
