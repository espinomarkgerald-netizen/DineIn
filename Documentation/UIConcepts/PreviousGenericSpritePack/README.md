# Dine In day panel sprite pack

`DayPanelSpriteSheet.png` is the transparent 2048 x 1120 master sheet, imported as Sprite (2D and UI) in Multiple mode. The atlas rectangle and per-sprite border map is `DayPanelSpriteSheet.json` (coordinates are provided in top-left and Unity bottom-left formats). Unity can slice the master image by transparency; for immediate use, drag the already-split sprites from `Components`.

The `Components` sprites are each imported as a single Sprite with alpha transparency and per-sprite borders in their `.meta` files. For scalable pieces, set the UGUI Image Type to **Sliced**. Use **Simple** for `IconBadge`, `StarEmpty`, and `StarFilled`.

## Compose the panels

- Start Day: `PanelFrame` + `HeaderPlate` + three `ObjectiveRow` sprites + `PrimaryButton`. Overlay the day heading, optional previous-day recap, objective text/status and button label in TextMeshPro.
- End results: `PanelFrame` + `HeaderPlate` + three `ResultCard` sprites + three dynamic `StarEmpty`/`StarFilled` sprites + `PrimaryButton`. Overlay the result title, revenue/orders, customer/coaching, approval/cash/tips and action label.
- Use `IconBadge` for dynamic objective or report icons and `Divider` between groups when needed.

The sprites contain no fixed words or result values. No tablet or device frame is included.