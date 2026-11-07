# Reception and newspaper icons V1

27 named sprites in `ReceptionNewspaperIconsV1.png`, with transparent individual PNGs in `Components`, Unity import metadata and a rectangle/source manifest. Seven blank scalable surfaces are now in `Surfaces`, with their dimensions/borders in `NewspaperSurfacesV1.json`.

Integration uses the existing CasualDiningPolishSettings asset for icon/surface references, dimensions and text sizing. DailyNewspaperPresenter owns the opening, closing, archive and audio behavior; NewspaperLandscapeView arranges the persisted article into responsive columns and real icon/value rows. Fine Dining's reservation reception indicator now has explicit Greet/Seat bindings, with runtime repair for older authored variants. The compact non-clickable party tag reads actual booking/replicated reception data. Reservation management screens are unchanged.

22 existing approved icons/stars are copied byte-for-byte from the game. Added matching editorial glyphs: TrendUp, TrendDown, TrendSteady and Quote. StarHalf composites the left half of the original filled star over the matching empty star. Original assets and their GUIDs remain untouched; this standalone pack has its own GUIDs.

Use Simple images with Preserve Aspect. These are icons, so borders are explicitly zero; do not 9-slice them. Original dimensions are preserved (512px stars, 256px illustrated icons and 64px navigation chevrons). All pivots are centered. Atlas is 2048px, uncompressed with no mipmaps and 16px gutters. JSON rects use Unity bottom-left coordinates; topLeft rects are also provided.

Semantic mapping: IconGuests = groups seated; IconCloche = served; IconOrder = completed orders; IconRestock = supplier/market information; IconCash = costs; IconApproval = approval; IconMegaphone = advice. Reactions use the existing face sprites. Reception uses IconGreetCustomer (waving hand) and IconSeatCustomer (table). A five-star rating uses exactly five star slots; 4.5 is four filled and one half-filled star. Trend colors describe numeric direction, not whether a price change is good or bad; use text and tint appropriately in the later presenter.

The real customer portrait remains supplied by CustomerTypeProfile.customerImage; this sheet does not introduce a substitute alien illustration or attribute an untyped saved quote to a specific customer type. Text, values and newspaper content stay separate from these sprites.

The clean newspaper layout now uses independent fixed-heading column viewports (Last Shift / Next Shift / Market Watch), an aligned supplier table, and Fonts/NewspaperBody.asset. This dedicated regular TMP font reuses the project's Atkinson regular atlas with a currency/symbol fallback; the original font remains unchanged, and its OFL license is included. Existing polish settings expose paper dimensions, type sizes and column weights. Supplementary incident details remain available in Service Notes.

Rebuild: `tools/Art/BuildReceptionNewspaperIcons.ps1`. Named sprite IDs/GUIDs are preserved on rebuild. Preview: `Documentation/UIConcepts/Newspaper/ReceptionNewspaperIconsV1.preview.png`. Validation is static/pixel-level only; native Unity import and runtime behavior have not been tested for this new sheet.
