# V2 production UI renders

These PNGs are rendered by Unity from the production sprites, fonts and DayPanelView hierarchy. They are isolated UI renders with representative data, not gameplay screenshots. The game binds actual settled values.

- `intro-landscape.png`: one-line hours/target/prep and configuration chips.
- `report-landscape.png`: full finances, service, reservations, moods, approval and grade.
- `report-failure.png`: separate consequences/recovery notice and both actions; metrics scroll above it.
- `report-portrait-large-text.png`: fixed header/actions with larger text and stacked cards.
- `report-portrait-bottom.png`: final stacked card reached through the report's ScrollRect.

`render-validation.txt` records all 14 rendered cases and partial-to-final reset checks. `assets.txt` records the bound production fonts and currency/punctuation coverage. `scene-validation.txt` records references after all ten saved scenes were reopened.

To reproduce, use **Dine In → UI → Day Panels → Render Acceptance Previews** and **Validate Saved Scene Assets** in Edit Mode. Output goes to `Temp/DayPanelV2`. Authoring preserves dirty loaded scenes and keeps the original actions and data owners.

Live Play Mode animation timing, audio, device safe areas, purchases and multiplayer/reconnect behavior still need gameplay verification. Check a full shift per restaurant, 0–3 stars, reservation outcomes, rapid actions, Reduced Motion, portrait dragging and rotation during entry. No player/account data or preferences were changed for these preview checks.

`SizingFix` contains the later regression check for the real scene's extra 100 × 100 modal wrapper. The renderer now recreates that nesting and checks the final canvas-sized fit. Its scene report covers all 20 surface ancestor chains; its renders remain isolated UI checks, with the open gameplay session unchanged.

`HeaderStars` contains the latest header/rating presentation: centered navy tab at its native aspect, geometrically centered title text, and 112-unit stars (about 47% larger). These are isolated production-asset renders, including portrait Large Text, with the same 14-case check report.

`Details` contains the later finishing pass using the existing sprite pack: row wells, reservations badge/well, column/section dividers, target-state pills, rating accents and footer doodles. The shared view adds missing decorative Images once to existing saved layouts; all ten scenes reference the same 40 pack sprites. Its reports distinguish static scene bindings and isolated renders from live gameplay.
