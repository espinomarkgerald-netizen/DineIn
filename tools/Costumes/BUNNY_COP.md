# Bunny Cop and universal costume compatibility

Bunny Cop: item ID `bunny-cop`, display name `Bunny Cop`, 200 GC in the existing `CostumesV1` catalog. Publish the new backend catalog entry before testing a purchase. No live catalog deployment or purchases were performed.

All five costumes now support both body IDs: Pink Beaver, Cowboy, Red Spider Hoodie, Pink Spider Hoodie, and Bunny Cop. Existing IDs, prices, ownership records, and equip/save data remain unchanged. The hoodies and Bunny Cop have separate body-compatible material entries in `Assets/Resources/AppearanceCatalog.asset`. Pink Beaver and Cowboy already supported both bodies.

Bunny Cop assets are in `Assets/_Project/Player/Assets/Appearance/Costumes`: body textures/materials, preview, three headband meshes/materials, and `BunnyCopHeadband.prefab`. The original supplied texture is preserved in `ArtSource/Costumes/BunnyCop-reference.png`. The headband geometry is retained in `ArtSource/Costumes/BunnyCopHeadband-geometry.json`. Existing body meshes, rigs, animations, skin and face materials are unchanged; the costume's existing hand-color override supplies the black gloves.

The headband uses the existing `HoodHairOcclusion` component. Its narrow `Covered Hair` bounds are editable on the prefab. Removing the costume restores the source hair mesh; exposed hair is not moved, resized, replaced or wholly disabled. Mask clipping now skips polygons whose bounds do not intersect a mask, avoiding unnecessary splitting with several narrow regions.

Checks: standalone C# compilation passed; both-body catalog/material compatibility, shop/backend IDs and 200 GC price, prefab references, mesh buffer structure and outward winding checked. Offline Bunny Cop previews inspected on both bodies, plus a ponytail; cross-body hoodie previews inspected. These are asset previews, not Unity runtime verification.

Still check in Unity: all hairstyles and viewing angles, headband/ear intersections, repeated equip/unequip, owned selection on both bodies, save/reload and multiplayer appearance. No Unity launch, Play Mode, game build or gameplay automation was used.
