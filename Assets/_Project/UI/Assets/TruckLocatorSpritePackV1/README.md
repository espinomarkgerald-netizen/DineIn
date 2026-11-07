# Truck locator sprite V1

`Components/TruckLocatorButton.png` is a transparent ready-state badge for the existing delivery truck locator. It depicts the blue-cab, white cargo-box truck from the project's TruckIcon reference in the approved illustrated UI style.

- Source PNG: 1254 x 1254 RGBA, transparent exterior.
- Unity import: Sprite (2D and UI), single sprite, centered pivot, 100 pixels per unit, maximum texture size 512.
- Use Image.Type.Simple and preserveAspect; scale uniformly. The truck is baked into this badge, so do not nine-slice it.
- Suggested initial display: 68 x 68 UI units, matching the existing marker allocation; check readability at the target viewport.
- No labels, numbers, arrows, or exclamation glyphs are baked in. Keep any direction cue separate.
- Generated from the actual project truck illustration and existing action-button/icon references. Original art is preserved.

Integrated into RestockTruckOffscreenIndicator.prefab. Its runtime owner binds a real button with existing click feedback, safe-area placement and click-to-center framing. It appears for ready, offscreen deliveries in supported gameplay contexts; tutorial lessons retain their HUD restrictions. See Documentation/Gameplay/GamePolishPlan.md for verification boundaries.

Validation performed: PNG dimensions, transparent corner pixels, Sprite importer mode/type, zero nine-slice borders. Live Unity appearance and small-screen readability were not tested.
