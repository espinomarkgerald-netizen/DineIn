# Purchasable costumes

## Included

Pink Beaver and Cowboy cost 100 GC each. The shop has Money and Costumes rows using the existing card style and vertical scroll. Customize > Costume shows only owned items plus No costume. Buying unlocks without automatically equipping. Apply saves the recipe; Cancel keeps the prior selection. Choosing a normal outfit/hat removes the costume; No costume restores the stored base outfit/hat.

The same optional `costumeId` travels in the existing appearance JSON and Photon AppearanceV2 property. Rendering remote appearances does not depend on the local account owning that costume. Normal employee appearance generation is unchanged.

Both supplied UV images contained topology guides. The cleaned textures preserve the island layout. Cowboy vest artwork was originally on leg islands; the vest/belt and trousers were reprojected onto the actual mesh UVs. Male and female outfit meshes have identical UVs, so both use the same costume material. The existing female body shape is retained.

Pink Beaver uses a new mesh headband with round pink ears. Cowboy uses the existing CowboyHat prefab. Costume headwear never hides or reshapes hair. Accessory-only offsets/scales for larger styles and the bare head live in AppearanceCatalog > Costumes > Headwear > Headwear Fits.

## Editable assets

- Assets/Resources/AppearanceCatalog.asset: costume material, headwear, fit, thumbnail and hand tint.
- Assets/_Project/Player/Assets/Appearance/Costumes/: textures, materials, headband prefab/meshes and icons.
- Assets/_Project/MainMenu/NewDesign/ShopItems/Data/Costumes/: display names, price/currency and matching catalog IDs.
- Assets/_Project/Scenes/NewMenu/NewGameMenu.unity: serialized shop rows, eight customization category buttons and wallet catalog version.
- Assets/_Project/MainMenu/NewDesign/ShopItems/ShopItem.prefab: card bounds and costume icon size. The card now occupies its real rectangle rather than drawing 286 units outside its layout slot.

Keep the item's Unity price synchronized with the PlayFab catalog price. A mismatch fails the purchase rather than using a different price silently.

## Required PlayFab setup — not deployed

Import/merge the two entries in Backend/PlayFab/CostumesV1.catalog.json into the title's Classic Economy catalog version **CostumesV1**. Do not change the default catalog or replace other catalog entries. The file is an UpdateCatalogItems payload, not a CloudScript handler; if the admin workflow replaces a whole catalog, export and merge existing entries first.

IDs must be `pink-beaver` and `cowboy`, permanent/non-consumable items with GC price 100. No title secret belongs in Unity. This task did not connect to the live title, deploy the catalog, or spend currency.

PlayFab PurchaseItem handles GC debit and inventory grant together. Existing GetUserInventory refreshes restore entitlement; LocalSaveManager.ownedItemIds is the existing account-scoped offline cache. Login/account changes invalidate stale inventory callbacks. Known owned items and in-flight requests block another purchase in this client. An uncertain purchase is not retried automatically: inventory refresh recovers accepted grants. Concurrent purchases from separate clients are not covered by a new server idempotency handler.

## Offline authoring

Run from the project root. Source art is in ArtSource/Costumes; existing body sources remain in ArtSource/CharacterAppearance.

1. Blender `--factory-startup -b -t 2 --python Tools/Costumes/export_uv.py` exports current mesh UVs to Temp/Costumes/body-uvs.json.
2. Python with NumPy and Pillow: `Tools/Costumes/prepare_textures.py` cleans the supplied references and aligns the cowboy artwork.
3. Blender `--factory-startup -b -t 2 --python Tools/Costumes/render_previews.py` renders shop icons and male/female fit previews using the catalog settings. No Unity process is needed.

The headband's authored geometry is also preserved in ArtSource/Costumes/headband-geometry.json. Runtime visuals and prices are asset settings, not runtime-only overrides.

## Validation and manual checks

Completed: C# compilation against the installed Unity references, serialized scene/asset reference checks, headband mesh index/winding checks and offline male/female material/headwear renders. These are not Unity runtime or build verification.

After deploying the catalog, manually verify:

- Unowned costumes absent from customization; both shop cards show the right GC price.
- Successful purchase charges once, shows Owned, and reveals the costume; insufficient GC/cancelled or failed request does not grant locally.
- Apply/reopen/relogin/account switching restore the right owner's selection; an uncertain response recovers from inventory without an automatic retry.
- Male/female, bald and each hairstyle; Pink Beaver headband and Cowboy hat remain fitted while animated.
- Another multiplayer client sees the selected costume, hair and face.
- Both shop categories use the same scrollable grid and card labels/buttons remain reachable on target desktop/mobile resolutions.

Not run: Unity launch, Play Mode, player build, live PlayFab transactions, or multiplayer gameplay testing.


## Shop purchasing and layout correction (2026-10-06)

Confirmed deployment gap: the owner reported that CostumesV1 has not been published. Publish `Backend/PlayFab/CostumesV1.catalog.json` to the same PlayFab title used by this game, with catalog version `CostumesV1`, item IDs `pink-beaver` and `cowboy`, GC prices 100 each. Keep the existing default catalog. This change does not deploy the catalog or spend currency.

Buy now opens a shared confirmation for both categories. Costume quotes come from the live catalog, are rechecked on confirmation, and changed prices require a new confirmation. Current inventory is read before a costume purchase. Only PurchaseItem's returned entitlement updates ownership; existing campaign credit receipts remain the money-pack grant path. Errors appear in the shop, including missing catalog setup.

The existing shop scene now has a fixed header, Money/Costumes tabs, a single masked responsive grid and a light Blue/Double content frame, blue selected tabs and neutral inactive tabs. The shared card is 174 by 180 canvas units, with a 72-unit preview and a neutral Grey/Double frame. Buttons use actual Green/Double `button_rectangle_depth_flat` / `button_rectangle_flat`; cancel uses Grey/Double; close uses Red/Double `check_square_color_cross`; success uses Green/Double `check_square_color_checkmark`. Existing Atkinson Hyperlegible body text, approved heading font, AnimatedPanel, ButtonAnimator and Gentle Ready audio are reused. The existing wallet uses a gold G text marker rather than a separate GC coin sprite; this same marker appears in the shop header.

Uncertain costume purchases reconcile through inventory and block another attempt while unresolved in the wallet session. The client cannot enforce cross-device simultaneous one-time purchases by itself; PlayFab inventory/catalog behavior still needs a live check. For money packs, the existing `campaign_credits` receipt journal now records an unconfirmed debit before submission. Only a confirmed response converts it to a grantable receipt; rejected requests remove it. Ambiguous responses or interruption leave it blocked across scene changes/restarts. A wallet balance alone cannot prove that debit's outcome: support/backend reconciliation is required, and no automatic grant/retry is performed. This preserves the existing debit-plus-local-Campaign-credit architecture rather than claiming it is an atomic backend transaction.

Validation for this pass: runtime C# compiled with Unity references without launching Unity; serialized local references, exact colored sprite references, hierarchy ownership and first-row layout arithmetic checked. No purchase, Play Mode, build, desktop automation, or rendered in-game UI verification. Manually check both categories at 16:9 and 2960x1440, confirmation/cancel/rapid clicks, success balance animation, already-owned costume, insufficient GC, backend errors, reopening and account changes after catalog deployment.


Shop visual polish: the existing confirmation is now 420 by 252 canvas units with an item preview, name, GC price and short prompt. Balance breakdowns and setup diagnostics are omitted from player-facing text; full failure details remain in logs. Confirmed success retains the existing small pop, check, sound and balance update. Commerce, inventory and ownership code were not changed by this visual pass. Static bounds/reference checks and C# compilation passed; rendered UI and live transactions remain unverified.
