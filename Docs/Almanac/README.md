# Dine In Almanac

The newspaper reader in `NewGameMenu` contains 92 audited entries covering customers, staff, restaurants, kitchen stations, equipment, food, service, cleaning/storage and management. Descriptions follow implemented gameplay and configured assets. Each entry's `sourceNotes` records its evidence and is not shown to players. Fine Dining is explicitly **coming soon**: only Casual Dining (`Lobby1`) and Fast Food (`Lobby2`) have assigned campaigns.

## Reader and organization

`Assets/_Project/UI/Almanac/AlmanacMenu.cs` retains the newspaper layout, category tabs, index, detail pages, variants and animated open/page/close. The related-entry button strip is removed; `relatedIds` remains internal metadata. Uniform/restaurant variants are labeled beside the preview. Back returns to the previously viewed entry and is hidden before any history exists; X closes the reader and restores the menu's previous interaction state.

Food is grouped into **Dishes** and **Ingredients**, with restaurant context on each article. Staff is grouped into **Shared roles**, **Casual Dining** and **Fast Food**. `listSection` and `sectionOrder` define the simple grouped index. `AlmanacCatalog.cs` merges explicit catalog references with `Resources/Almanac` entries, deduplicates by stable ID and sorts by category, section order/name and entry name. Search matches names and `searchAliases`, ignoring case. Previous/Next follow the displayed filtered list and disable at its edges.

## Preview resources and artwork

- `AlmanacPreviewStage.cs` and `AlmanacPreviewDrag.cs` use one reusable camera/RenderTexture and one displayed visual at a time. The Inspector defaults are 1024 pixels square on desktop and 768 on mobile, clamped to hardware limits, with supported MSAA and bilinear filtering. Character proportions are preserved; framing modes distinguish characters, equipment and small objects. Hidden entries have no active preview animation.
- Visual copies preserve meshes, materials, rigs and Animators without activating gameplay scripts, physics or audio. Closing releases the active preview and render resources. The isolated lighting does not recolor source materials.
- `Assets/_Project/UI/Almanac/AlmanacCatalog.asset`, `Previews/` and `Thumbnails/` hold catalog references, derived inert visuals and static artwork. Staff variants use the appearance catalog and real uniforms; baked materials retain skin/hair tints. The ten staff list portraits are generated from those same uniformed previews, not outfit-only swatches.
- Restaurant previews are baked from the actual `NewGameMenu/Restaurants` instances, preserving their material overrides. `AlmanacContentAuthoring.GenerateRestaurantArtwork()` renders wide 1536×864 static artwork with the district camera angle. It does not substitute a recolored generic building. Fine Dining's image represents only its configured district-map preview.
- **Order Check** replaces the outdated Order Notepad presentation. `AlmanacContentAuthoring.RenderOrderPreview()` captures the current `OrderChecklistUI` layout from Lobby1 with an illustrative catalog order, without opening a live customer transaction. Its static image is `Thumbnails/service-order-ui.png`; the stable entry ID remains `service-order-pad`.

## Add or edit an entry

Create **DineIn > Almanac Entry** in the Project window, or edit an existing `AlmanacEntryData`. Put additions under `Assets/Resources/Almanac/` for automatic discovery, or reference them from `AlmanacCatalog.asset`.

Give each entry a unique, permanent `entryId`. Set category, `listSection`, `sectionOrder`, name, subtitle, description and gameplay notes; `bossNote` is optional. Put useful alternate terms in `searchAliases`. Set `icon` for its list/static image and choose `previewKind`; live previews require a safe visual prefab, with optional idle/introduction clips and labeled variants. `previewEuler` sets orientation, `previewFraming` selects Automatic/Character/Equipment/SmallObject framing, and `widePreview` requests a wider photograph for static restaurant or UI presentations. Record concrete script/config/asset evidence in `sourceNotes`. Optional `relatedIds` must still reference valid entries but produce no visible button strip.

Authoring commands under **Dine In > Almanac**:

- **Populate Audited Content** rewrites baseline entry fields/catalog and creates derived previews. It is not required for hand-authored additions; do not use it to save ordinary edits or preserve custom catalog-only references.
- **Apply Presentation Polish** (`PolishPresentation`) updates the existing `NewGameMenu` reader layout, variant caption and preview-quality settings; it does not rebuild the Almanac.
- **Render Current Order Preview** regenerates the illustrative Order Check image.
- **Validate Reader and Previews** runs focused Edit Mode checks with `NewGameMenu` open.

Restaurant artwork and other generated portraits can be refreshed through the shared editor authoring methods after their source visuals change. Good existing food/ingredient artwork remains in use.

## Validation and remaining checks

The current polish pass passed focused Edit Mode validation for 92 entries, 34 model entries, 46 visual variants and 29 animated character variants. Checks confirmed a 1024-pixel square preview with supported 4× MSAA; unchanged material copies; eight sampled character yaw framings (four each for Chef and Family); exact restaurant material references; and the current Order Check asset reference. Reader checks covered Food/Staff group headings, name/alias search and restored grouping, filtered paging edges, history query reset, labeled uniform variants, absence of the related strip, interrupted fades, close/reopen and resource cleanup. Report: `../output/almanac/validation.txt` relative to the Unity project directory.

Current offscreen renders were checked at 16:9 and 4:3: `../output/almanac/polished-staff.png`, `polished-food-tablet.png` and `polished-restaurant.png`. The final Order Check image and article were also visually checked after fitting the responsive columns and full visible UI bounds (`polished-order.png`).

**Play Mode, Android touch/performance and audio were not run.** Manually verify opening/closing, rapid navigation, grouped search, drag rotation, text scrolling, uniform variants, menu restoration and sound on the intended device. Two existing, unrelated mobile unlock-popup checks still fail; they are outside the Almanac validation above.
The [people audit](almanac-people-audit.md) and [objects audit](almanac-objects-audit.md) preserve discovery evidence. Current entry `sourceNotes` and implementation/configuration remain authoritative.

## Customer framing correction — 2026-09-30

- Reproduced all seven customers using the live stage before editing. Regular/VIP were small; Orange, Purple, Elderly and Family were effectively dots. Their active mesh/material/layer/animation references were present. The default skin bake combined with the renderer matrix counted imported scale twice (Orange has a 100x mesh transform), inflating both framing bounds and centering. `BakeMesh(mesh, true)` supplies the correct scale-aware sample for this hierarchy; before/after raster captures prove the correction.
- The existing preview-only centering root and yaw-safe camera framing are retained. Visible mesh bounds are combined after animation initialization and sampled preview poses, once per selection. Character fill defaults to 80%; no customer requires an override. Source prefab scales, gameplay components, materials, clips, descriptions and identities are unchanged. Family uses the actual Yellow representative adult; Elderly uses OldAlien with its real accessories.
- Disabled/inactive renderers, lower LOD duplicates, non-mesh effects, world-space UI and non-finite bounds do not determine framing. An optional exact renderer-path exclusion and optional fill override are available on entries for future unusual subjects; current customers use neither. Missing/empty previews retain their static thumbnail and issue one development warning per entry.
- The Almanac hint reads `DRAG TO ROTATE`, uses only existing typography, and is parented/anchored to the square, aspect-fitted live viewport with room below the feet. The existing dark `Preview Hint Backdrop` (owned by the menu customization presentation) is disabled in NewGameMenu; its controls/behavior were not changed.
- Verification: compilation passed; 92 entries, 46 variants and 29 animated variants passed the existing checks. New checks inspected actual rendered pixels for all seven customers plus Chef at four yaw angles (32 frames), including animation progression, camera stability, visible body/antennae, centered rotation and foot clearance. Material snapshots were unchanged. Fourteen immediate customer switches preserved selection/headline/model identity and one active subject; the rotation API and missing/empty asset fallback passed. Orange at 1280x720 and VIP at 960x720 were visually inspected with viewport-containment assertions for the hint.
- Evidence: `../output/almanac/framing/verified-customers.png`, `page-customer-orange.png`, `page-customer-vip.png`, and `../output/almanac/validation.txt`. Before captures are retained in the same framing directory.
- Not run: actual Play Mode pointer/touch input, deferred-destruction timing, long-running animation, Android rendering/performance. The scene was left closed with preview resources released. No saves/progression/gameplay assets were modified.
