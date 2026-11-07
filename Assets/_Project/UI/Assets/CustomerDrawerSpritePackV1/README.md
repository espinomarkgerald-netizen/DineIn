# Compact customer drawer sprite pack V1

Artwork for the approved compact customer list, customer-details drawer and task popup. No gameplay scripts, prefab bindings, live customer data or input behavior are added.

## Files

- `CustomerDrawerV1.png`: transparent 2048 x 2048 atlas, 40 named sprites.
- `Components/`: 40 individual transparent PNGs with Unity importer metadata.
- `CustomerDrawerV1.json`: sprite names, full canvas rectangles, Unity bottom-left coordinates, explicit borders, centered pivots, GUIDs, atlas local file IDs and source attribution.
- `CustomerDrawerV1.preview.png`: assembled examples. Preview text is not part of production sprites; group icons stand in for the runtime portraits.
- Build source: `tools/Art/BuildCustomerDrawerArt.cs` and `.ps1` at project root.

## Sliced artwork

Borders are native pixels in **left / bottom / right / top** order. Start at **pixelsPerUnitMultiplier 2** for the illustrated compact sizes; adjust RectTransforms to fit the real text. Use Full Rect, bilinear filtering, Clamp, no mipmaps and no texture compression, as provided by the metadata.

| Piece | Canvas | Border |
|---|---|---|
| PanelFrame | 512 x 512 | 48 / 48 / 48 / 48 |
| HeaderTab | 384 x 80 | 32 / 24 / 32 / 24 |
| TableBadge | 192 x 64 | 24 / 16 / 24 / 16 |
| PortraitTile | 128 x 128 | 24 / 24 / 24 / 24 |
| SelectedRow, HoverRow | 256 x 96 | 16 / 16 / 16 / 16 |
| StatusStrip | 384 x 72 | 24 / 20 / 24 / 20 |
| Divider | 256 x 8 | 4 / 3 / 4 / 3 |
| NavButton Ready/Pressed/Disabled/Focused | 256 x 64 | 24 / 16 / 24 / 16 |
| SecondaryButton Ready/Pressed/Disabled/Focused | 384 x 96 | 64 / 32 / 64 / 32 |
| ScrollTrack | 24 x 128 | 8 / 12 / 8 / 12 |
| ScrollThumb | 24 x 64 | 8 / 12 / 8 / 12 |
| PatienceTrack and four fills | 256 x 32 | 16 / 12 / 16 / 12 |

## Icons and assembly

- Standalone chevrons, left/right popup pointers, location pin and cashier pictogram have zero borders; use Simple and Preserve Aspect.
- Existing game assets are reused for mood faces, clock, party count, cloche, check, waiting hourglass, cash, receipt and the approved matte table icon. Their exact source paths are listed in the manifest. They were not redrawn.
- PortraitTile is blank. Bind the actual `CustomerTypeProfile.customerImage`; do not bake a sample alien portrait, name, group count, table number, service state or patience percentage into the artwork.
- Put the list and details views inside the same left drawer. Suggested artwork scale: approximately 300 x 300 for the list and 300 x 330 for details at the 1670 x 940 concept viewport. Keep the list middle region scrollable rather than enlarging the drawer as groups arrive. Sizes are design examples, not implemented settings.
- Place labels above the artwork as editable TMP text using the existing Lilita One / Nunito assets. Use a separate HeaderTab, portrait, status strip and stat rows; nothing is baked into PanelFrame.
- For patience, place the chosen colored fill inside the track with consistent inset. The white Tintable fill permits the existing real value/color owner to supply its tint. Resize a sliced fill within a clipped region; no timing or mood calculation is supplied by this art pack.
- Use the existing UI controller for button states, scrolling and selection. Decorative images should not capture pointer input. The task popup can reuse PanelFrame, HeaderTab and PointerRight.

See `VALIDATION.txt` for performed checks and boundaries. This pack does not implement the proposed customer-directory feature.
