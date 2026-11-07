# Generation brief and provenance

Built-in image generation, using the approved action-icon sheet (`exec-9a8d713f-e8d1-430a-a10a-4561b93c2c7c.png`) as the style reference. The original output is `IconsGenerated.png`.

Final brief: production restaurant-game UI icons only; square transparent canvas; regular four-column/four-row grid; consistent navy outlines, rounded geometric silhouettes, restrained two/three-tone fills and cream/cyan/green/gold/coral palette. No words, button frames, scenery or decorative rays. Every icon remains inside its cell with generous padding. Reading order: wine refill, water refill, drag-to-pour, fill target; grill, fryer, assembly, takeout; restock, cooking, ready, burnt; reservations, accept reservation, decline request, back. Wine and water use distinct vessel shapes as well as different liquid colors. Ready and reservation acceptance use different silhouettes. These are small control pictograms, not full minigame artwork.

Button-state PNGs, blank TableBadge and HoldProgressRing are reused byte-for-byte from ActionButtonSpritePackV1. IconWaiting reuses DayPanelSpritePackV2's hourglass; IconCleaning reuses ActionButtonSpritePackV1's sponge. The full standalone copies are included in Components.

`BuildRestaurantControls.cs.txt` preserves the earlier pack's extraction and nine-slice preview helpers, removes isolated alpha fragments, centers generated icons on 256-pixel canvases and packs all pieces into the atlas. The preview's text is illustrative and does not appear in the production sprites.

Reception additions use the approved built-in imagegen outputs, retained as `GreetCustomerApproved.png` and `SeatCustomerApproved.png`. The hand was generated against the existing pitcher and serving-hand sprites, then recolored with a coral cuff, gold motion marks and warm dark outlines. The table was refined against the same production style references: two matte olive chairs, cream tablecloth, brown wooden legs, bold outlines and simplified two-tone shading. Neither includes a person or baked text. The approved hand is generation exec-ef63acee-c393-4fda-a464-0b35650d4b1d; the approved seating icon is exec-c63e36ee-382b-4be6-a5b9-e4b848d16710.

`tools/Art/BuildReceptionActionIcons.cs` preserves the approved shapes/colors and alpha, normalizes the visible artwork within a 208-pixel span on a 256-pixel canvas, and updates the free atlas cells. Its PowerShell wrapper preserves component GUIDs, atlas names/IDs and manifest entries. Labels appear only in `ReceptionActionsV1.preview.png`.
