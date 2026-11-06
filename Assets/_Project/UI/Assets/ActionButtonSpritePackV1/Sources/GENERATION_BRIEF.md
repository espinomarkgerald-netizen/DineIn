# Artwork provenance

Generated with the built-in image-generation tool using the approved `ORIGINAL STYLE — CALMER ICONS` reference (`exec-5801f6d2-153e-4078-856e-c0e4d51e3ce4.png`). This is the final generation brief for the two artwork inputs.

## ButtonsGenerated.png

Production game UI sprite sheet, blank button backgrounds only, transparent background, no text/icons/labels. Regular four-column/four-row grid on a 1536 x 1024 canvas. Friendly rounded glossy restaurant-game pills, navy edge, restrained cream highlight at upper left, shallow lower bevel, colorful gradient. Rows: lime green, cyan blue, gold yellow, coral salmon. Columns: Ready, Pressed (darker/reduced bevel), Disabled (muted blue-gray), Focused (cream/cyan keyline). Identical allocations and alignment, generous transparent spacing. Keep corner/highlight details in the end regions and the central stretch strip clear for nine-slice use.

## IconsGenerated.png

Production icon sheet, transparent background, regular four-column/three-row layout on a 1536 x 1024 canvas. Consistent navy strokes and two/three-tone fills, calmer simplified icons, no labels, button frames, scenery or attention rays. Reading order: Take order (receipt/pencil), Clean (sponge), Ask for bill (receipt/question mark), Collect bill (hand holding paper receipt), Collect payment (hand with banknote/coin), Pick up (hand holding plain plate), Serve food (cloche), Pause, Camera, Tasks, Manage, Close.

## Packaging

`BuildActionPack.cs.txt` extracts each grid cell, discards isolated alpha speckles while retaining major disconnected icon parts, centers artwork on uniform transparent canvases and packs the atlas. No text is baked into production sprites. Utility frames, badges, pointer and progress ring are deterministic vector primitives rendered by that source. The preview demonstrates composition only; it is not the sprite sheet and its lettering is not part of the assets.
