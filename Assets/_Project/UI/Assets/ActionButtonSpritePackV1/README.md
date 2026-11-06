# Dine In action button sprite pack V1

Artwork only, based on the approved original-style buttons with calmer icons. No gameplay scripts, scenes or action bindings are changed by this pack. The proposed matching-table Serve Food feature is not implemented.

## Contents

- `ActionButtonsV1.png`: transparent 2048 x 2048 atlas with 33 named Unity slices.
- `Components`: individual transparent PNGs with matching Unity metadata.
- `ActionButtonsV1.json`: atlas rectangles in top-left and Unity bottom-left coordinates, pivots, borders, sprite file IDs and suggested action/icon mappings.
- `ActionButtonsV1.preview.png`: assembled examples; lettering and sample table number appear only in this preview.
- `Sources`: original generated grids, generation brief and deterministic extraction/packing/vector-shape source.

## Button states

The first four atlas rows are Green, Cyan, Gold and Coral. Columns are Ready, Pressed, Disabled and Focused. Every exported background uses a 480 x 192 canvas and centered pivot. Focus is a complete background state; it is not an animated progress overlay.

Use `Image.Type = Sliced`. Borders in pixels are **left 128, bottom 56, right 128, top 56**. For a logical button around 240 x 96, use `pixelsPerUnitMultiplier = 2`; wider buttons stretch their center while preserving the ends. Keep text/icons as separate widgets, inset from the visible button edges. Do not crop away the matching transparent canvas padding or change pivots per state.

The illustrated ready/pressed states deliberately share their allocation; final apparent stroke bounds can vary by a few pixels. The native center strip retains a soft gradient/highlight; inspect extreme aspect ratios before shipping.

## Action mapping

| Action | Background family | Icon |
|---|---|---|
| Take order | Cyan | IconTakeOrder |
| Clean | Cyan | IconClean |
| Ask for bill | Gold | IconAskForBill |
| Collect bill | Gold | IconCollectBill |
| Collect payment | Green | IconCollectPayment |
| Pick up | Green | IconPickUp |
| Serve food | Green | IconServeFood |

The three bill/payment actions have separate icons: receipt/question mark, hand receiving a paper receipt, and hand receiving money. Pause, Camera, Tasks, Manage and Close icons are also included. Coral backgrounds support cancel/close actions.

Icons use 256 x 256 canvases with approximately 24-pixel padding and centered pivots. Use **Simple + Preserve Aspect**, without slice borders. Keep labels editable with the game's existing Lilita One/TMP heading font and Nunito Sans for numbers.

## Table and popover pieces

- `TableNumberBubble`: blank cream/cyan circular bubble, Simple + Preserve Aspect. Overlay the actual table number.
- `TableBadge`: blank navy label, Sliced borders 64/32/64/32 (L/B/R/T). Overlay `TABLE 04` or the actual table number.
- `PopoverPanel`: blank cream/cyan panel, Sliced borders 72 on all edges.
- `PopoverPointer`: separate downward pointer, Simple + Preserve Aspect. Overlap its open upper edge slightly with the panel's lower cyan rim.
- `HoldProgressRing`: transparent cyan ring. For a future progress UI, use a separate Filled/Radial360 Image with actual hold progress. It does not implement hold behavior.

The intended future serving composition is a green Serve food button with its matching table badge attached. Existing movement, pickup confirmation and delivery validation must remain the action owners when that feature is implemented later.

## Verification

See `VALIDATION.txt` for the actual asset/import checks. No live gameplay or integration testing is implied by an asset-only pack.

Runtime integration (2026-11-07): the shared gameplay skin loads these sprites through Assets/_Project/Resources/UI/GameplayActionButtons.asset. ActionButtonHeadingOutline.mat and ActionButtonBodyOutline.mat are dedicated black-outline presets for editable Lilita/Nunito text; they do not alter the day-panel font assets. See Documentation/Gameplay/ActionButtonsImplementationPlan.md and ImplementationChecks for scope and verification limitations.