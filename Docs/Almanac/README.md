# Dine In Almanac

The newspaper reader in `NewGameMenu` contains 92 audited entries covering customers, staff, restaurants, kitchen stations, equipment, food, service, cleaning/storage and management. Descriptions follow implemented gameplay and configured assets. Each entry's `sourceNotes` records its evidence and is not shown to players. Fine Dining is explicitly **coming soon**, because only Casual Dining (`Lobby1`) and Fast Food (`Lobby2`) have assigned campaigns.

## Implementation and resources

- `Assets/_Project/UI/Almanac/AlmanacMenu.cs`: category/index browsing, search, related-entry history, variants and animated open/page/close. It blocks the existing menu while open and restores its previous interaction state on close.
- `AlmanacCatalog.cs`: merges explicit catalog references with `Resources/Almanac` entries and resolves entries by stable ID.
- `AlmanacPreviewStage.cs` and `AlmanacPreviewDrag.cs`: one reusable preview camera/RenderTexture and one displayed visual at a time. Preview copies preserve meshes, materials, rigs and Animators without activating gameplay scripts, physics or audio. Closing releases the active preview and render resources. Static entries use their authored sprite.
- `Assets/_Project/UI/Almanac/AlmanacCatalog.asset`, `Previews/` and `Thumbnails/`: catalog, derived inert visual assets and static artwork. Staff previews reuse the appearance catalog and actual restaurant uniforms; baked materials retain skin/hair tints.

## Add or edit an entry

Create **DineIn > Almanac Entry** in the Project window, or edit an existing `AlmanacEntryData` asset. Put additions under `Assets/Resources/Almanac/` for automatic discovery, or reference them from `AlmanacCatalog.asset`.

Give every entry a unique, permanent `entryId`. Set its category, name, subtitle, description and gameplay notes; `bossNote` is optional. Set `icon` for the card/static image and select the appropriate `previewKind`. Character/model entries need a safe visual prefab; characters can specify idle and introduction clips. Optional variants provide labeled prefab alternatives, and `previewEuler` controls initial orientation. Set `relatedIds` to existing stable IDs and record concrete script/config/asset evidence in `sourceNotes`. Avoid unsupported gameplay claims and keep development evidence out of player-facing prose.

**Dine In > Almanac > Populate Audited Content** authors the baseline catalog, rewrites baseline entry fields and creates derived previews (existing baked previews may be reused). It is not needed for hand-authored additions; do not use it to save ordinary content edits or preserve custom catalog-only references. Run **Dine In > Almanac > Validate Reader and Previews** with `NewGameMenu` open in Edit Mode after changes.

## Validation and remaining checks

Focused Edit Mode validation passed for 92 entries and their links/images, 34 model entries, 46 visual variants and 29 animated character variants. It checked safe visual copies, reuse of one preview camera/texture, search/empty results, history, stale variants, interrupted page fades, close/reopen and restoration of menu state. Report: `../output/almanac/validation.txt` relative to the Unity project directory.

Offscreen UI renders were checked at 16:9 and 4:3 (`../output/almanac/customer.png`, `staff.png`, `food-tablet.png`). These are not Play Mode or device tests. **Play Mode, Android touch/performance and audio were not tested.** Manually verify opening/closing, rapid page changes, drag rotation, text scrolling, variants, menu restoration and sound on the intended device.

The accompanying [people audit](almanac-people-audit.md) and [objects audit](almanac-objects-audit.md) preserve the discovery evidence. They are audit snapshots; current entry `sourceNotes` and implementation/configuration remain authoritative.
