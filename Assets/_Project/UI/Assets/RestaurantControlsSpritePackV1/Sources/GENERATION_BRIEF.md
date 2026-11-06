# Generation brief and provenance

Built-in image generation, using the approved action-icon sheet (`exec-9a8d713f-e8d1-430a-a10a-4561b93c2c7c.png`) as the style reference. The original output is `IconsGenerated.png`.

Final brief: production restaurant-game UI icons only; square transparent canvas; regular four-column/four-row grid; consistent navy outlines, rounded geometric silhouettes, restrained two/three-tone fills and cream/cyan/green/gold/coral palette. No words, button frames, scenery or decorative rays. Every icon remains inside its cell with generous padding. Reading order: wine refill, water refill, drag-to-pour, fill target; grill, fryer, assembly, takeout; restock, cooking, ready, burnt; reservations, accept reservation, decline request, back. Wine and water use distinct vessel shapes as well as different liquid colors. Ready and reservation acceptance use different silhouettes. These are small control pictograms, not full minigame artwork.

Button-state PNGs, blank TableBadge and HoldProgressRing are reused byte-for-byte from ActionButtonSpritePackV1. IconWaiting reuses DayPanelSpritePackV2's hourglass; IconCleaning reuses ActionButtonSpritePackV1's sponge. The full standalone copies are included in Components.

`BuildRestaurantControls.cs.txt` preserves the earlier pack's extraction and nine-slice preview helpers, removes isolated alpha fragments, centers generated icons on 256-pixel canvases and packs all pieces into the atlas. The preview's text is illustrative and does not appear in the production sprites.
