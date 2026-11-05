# Dine In day panel sprite pack V2

Production artwork for the rounded day-panel redesign. `Components` contains 40 immediately usable transparent PNG sprites. `DayPanelV2SpriteSheet.png` is the 4096 × 2048 Multiple-mode sheet with named slices. Its rectangle/border/pivot manifest is `DayPanelV2SpriteSheet.json`.

All labels, numbers, grade letters and button text belong in TextMeshPro; none are painted into the artwork. The blank grade medallion is `IconGrade`.

## Unity use

- Use **Image Type: Sliced** for PanelBody, RowShell, ReportCard, RibbonCoral, RibbonGold, ValuePill, CoachingStrip, PrimaryButton, SecondaryButton, ProgressTrack and ProgressFill. Explicit borders are stored in the PNG `.meta` files and manifest.
- Use **Simple + Preserve Aspect** for TitleTab, Badge, all icons, the two stars and AccentRays. The header is deliberately separate from the panel body so it can keep its proportions while the body resizes.
- Icons use 256 × 256 canvases with 18-pixel minimum transparent padding. Both stars use 512 × 512 canvases with 32-pixel minimum padding, centered pivots and matched apparent bounds.
- PanelBody has a tight transparent perimeter; its cream content begins approximately 34 pixels from each edge. Keep text/widgets inside a 44-pixel native inset. Its 72-pixel slice borders protect the corners/checker details.
- All UI pieces import without texture compression or mipmaps for crisp linework. Atlas maximum size is 4096; individual textures remain at native resolution.

## Artwork sources

The illustrated icons and rounded stars were generated with the built-in image-generation tool from the approved V2 report/intro references. The original outputs are preserved as `Sources/IconsGenerated.png` and `Sources/StarsGenerated.png`; generation prompts are in `Sources/GENERATION_PROMPTS.md`.

The scalable panel/card/ribbon/button shapes are deterministic vector artwork. Their editable SVGs and the rendering source `Sources/BuildDayPanelV2Art.cs.txt` are included. These primitives share the reference's navy/cyan/cream/coral/gold palette. Generated icon extraction removes isolated alpha speckles and neighboring-cell fragments while preserving the icon illustration and detached accent rays.

## Fonts and production use

`Fonts/DayPanelHeading.asset` uses the existing Lilita One source with a dedicated navy outline material. `DayPanelBody.asset` uses `Fonts/Sources/NunitoSans-Bold.ttf` (weight 700); its bold weight points to `DayPanelBodyBold.asset`, using `NunitoSans-ExtraBold.ttf` (weight 800). These are static instances of the project's Nunito Sans variable source, at width 100, optical size 12 and lowercase-height axis 500. Their OFL license is bundled in `Fonts/Sources`. The inherited source style name can still appear as ExtraLight in the inspector; the actual exported weights are 700 and 800. Both body assets include the peso sign, en dash, bullet and ellipsis.

`DayPanelSkinAuthoring` applies these production assets to the ten restaurant scenes. Text and numbers remain editable TMP widgets. `DayPanelView` renders the snapshot supplied by the original `GameDayManager`; it never settles money, purchases or campaign state. Animation remains in `GameManager.cs`, including the existing restore/reconnect and failed-outcome restrictions. The explicit `Dine In/UI/Day Panels` commands prepare assets, apply the skin and render isolated acceptance previews while preserving unsaved scenes.

The old DayPanelSpritePack remains preserved. Actual data, animation timing, safe-area behavior and result actions remain owned by the game's day-panel presentation.

The finishing pass reuses the existing row shells, value pills, progress-fill strokes, accent rays and footer outline icons. Tan section/column dividers are solid-color Unity Images with raycasts disabled. DayPanelView adds only missing decorative Images once to an existing authored layout; explicit authoring includes them in the saved tree. No additional bitmap assets are required.
