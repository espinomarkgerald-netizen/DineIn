# Dine In Day Panel Sprite Pack

Transparent sprite assets for the Start Day and End Results panels. The look follows the requested bright cream, cyan, navy, coral, yellow and kitchen-checker style. No tablet frame or baked text is included.

## Unity import

- `DayPanelSpriteSheet.png` is the 3328 x 1280 Sprite (2D and UI) atlas. It imports in Multiple mode with named sprites and 9-slice borders. `DayPanelSpriteSheet.json` lists each top-left and Unity bottom-left rectangle.
- `Components/` contains the same assets as individual Sprite (2D and UI) textures with their borders already set.
- Set UGUI `Image Type` to **Sliced** for `PanelFrame`, `ObjectiveRow`, `ReportCardShell`, `RibbonCoral`, `RibbonGold`, `PrimaryButton` and `SecondaryButton`.
- Use **Simple** for `IconBadge`, `StarEmpty`, `StarFilled` and `StarDivider`.
- Keep the panel near its original aspect ratio when possible; the integrated raised title tab and corner decorations are protected by the frame borders.

## Suggested compositions

**Start Day:** `PanelFrame` + TMP heading over the navy tab + three `ObjectiveRow` sprites + `PrimaryButton`. Place objective icons over the round badges, labels over the coral ribbons, and reward text over the yellow pills.

**End Results:** `PanelFrame` + TMP heading + `StarDivider` (or individual dynamic stars) + three `ReportCardShell` sprites. Place `RibbonCoral` or `RibbonGold` and `IconBadge` over each card, then add the report text and `PrimaryButton`.

All words, numbers, objective icons, result values and button labels remain editable UI elements.