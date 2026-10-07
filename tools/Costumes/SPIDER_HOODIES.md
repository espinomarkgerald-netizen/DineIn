# Spider hoodie costumes

- `red-spider-hoodie`: male and female bodies, 200 GC.
- `pink-spider-hoodie`: female and female bodies, 200 GC.
- Registered in `Assets/Resources/AppearanceCatalog.asset`, the existing NewGameMenu shop list, and `Backend/PlayFab/CostumesV1.catalog.json`.
- Publish the two new items to the existing **CostumesV1** PlayFab catalog before testing purchases. This change does not deploy backend configuration or grant ownership.

## Authoring

Body textures, materials, shop thumbnails, the shared `SpiderHood.asset`, and the two `*Hood.prefab` assets live in `Assets/_Project/Player/Assets/Appearance/Costumes`.
Original supplied images are preserved under `ArtSource/Costumes`. `Tools/Costumes/prepare_spider_hoodies.py` reprojects them using the male/female UV exports produced by the existing `Tools/Costumes/export_uv.py` offline Blender tool. No body mesh, rig, or animation was changed.

The hood prefab's **HoodHairOcclusion / Covered Hair** bounds define the head-local regions covered by the hood. Select the component to see editable wire bounds. The mask retains front hair and strands below the hood. It clips instance-owned copies of rigid hairstyle meshes only when equipping, preserving uncovered positions, UVs, normals, colors, submeshes and the original hair transform. Removing/replacing the hood restores the source meshes and releases the copies. It does not disable the hairstyle. The 13 existing hair FBX imports need Read/Write enabled for this one-time operation; their geometry is unchanged. New rigid hairstyle assets also need Read/Write enabled.

## Checks and remaining verification

Standalone C# compilation passed with existing warnings. Body compatibility, live shop references, 200 GC backend entries, mesh buffer lengths, and the scope of the hair import changes were checked. Offline Blender previews were inspected for male hair 01/05 and female hair 01/03. These are source-asset previews, not Unity gameplay verification.

In Unity, check all hairstyle options from front/side/back, hair colors, both body selections, equip/unequip/repeated switching, save/reload and multiplayer appearance. Check the new item labels/thumbnails and owned states in the shop. Test purchases only after publishing the catalog entries. No Unity launch, Play Mode, game build, live purchase or runtime verification was performed by this task.
