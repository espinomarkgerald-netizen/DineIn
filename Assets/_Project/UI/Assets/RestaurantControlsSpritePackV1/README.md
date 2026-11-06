# Restaurant controls sprite pack V1

Buttons and small icons only, matching the approved ActionButtonSpritePackV1. No game scripts, scene bindings, pouring illustrations or interaction behavior are changed. The bottles/pitchers pictured inside icons are pictograms, not replacements for the draggable Pour UI artwork.

## Contents

- `RestaurantControlsV1.png`: transparent 2048 x 2048 atlas, 36 named sprites.
- `Components`: individual transparent PNGs and Unity metadata.
- `RestaurantControlsV1.json`: atlas rectangles, borders, pivots, local file IDs and intended UI uses.
- `RestaurantControlsV1.preview.png`: composition examples with preview-only labels.
- `Sources`: generated icon sheet, generation brief and extraction/packing source.

The pack contains 16 button backgrounds (Green/Cyan/Gold/Coral x Ready/Pressed/Disabled/Focused), 18 restaurant icons, a blank navy badge and a cyan progress ring. Button pixels are reused unchanged from the approved first pack; the new files have independent Unity GUIDs so both packs can coexist.

## Coverage

| Area | Icons |
|---|---|
| Wine/water pouring | WineRefill, WaterRefill, DragToPour, FillTarget |
| Fast Food | Grill, Fryer, Assembly, Takeout, Restock, Cleaning |
| Cooking status | Waiting, Cooking, Ready, Burnt; Assembly also fits Preparing, Ready also fits Complete |
| Fine Dining reservations | Reservations, AcceptReservation, DeclineRequest, Back |

Pouring remains a drag interaction. Wine/water refill, drag guidance and fill-target examples are visual indicators; this pack does not introduce a click-to-pour command. Station icons likewise decorate the existing navigation/controls rather than create new cooking actions. Preserve live labels and state conditions when integrating later.

## Unity use

- Button backgrounds: **Image Type Sliced**, native 480 x 192, centered pivot, borders **128/56/128/56** in left/bottom/right/top order. For a logical 240 x 96 button, start with pixelsPerUnitMultiplier 2 and stretch the middle horizontally.
- Icons: **Simple + Preserve Aspect**, native 256 x 256, centered pivots and transparent padding. Place icons and editable TMP labels separately; no production sprite contains a text label.
- `TableBadge`: blank 384 x 128 navy badge, Sliced borders **64/32/64/32**. Add the actual table number or contextual text separately.
- `HoldProgressRing`: Simple for a complete ring or Filled/Radial360 for an actual progress value; it supplies no timing/input logic.
- Focused backgrounds are complete state images, not animated progress overlays.

Waiting and Cleaning reuse existing matching icons from the day-panel and action packs. Other icons were generated from the approved calmer-icon style and normalized onto consistent transparent canvases. Source art and generation brief are included.

See `VALIDATION.txt` for actual import/asset checks. No gameplay, device or live pouring verification is implied.
