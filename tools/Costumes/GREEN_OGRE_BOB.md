# Green Ogre and Bob

Both costumes cost 200 GC in CostumesV1 and support male and female bodies.
Item IDs: green-ogre (Green Ogre), bob (Bob).

Assets: Assets/_Project/Player/Assets/Appearance/Costumes/GreenOgre* and Bob*.
The matching Headwear prefabs equip through AppearanceCatalog with the outfit.
Their HoodHairOcclusion coveredHair bounds are Inspector-editable and clip only
intersecting local hair geometry. Existing restore behavior returns original hair
when headwear changes. Skin, face, body mesh, rig and animation remain unchanged.
Bob goggles sit above the eyes to preserve face visibility.

Source artwork and headwear geometry/masks are in ArtSource/Costumes.
tools/Costumes/prepare_ogre_bob.py reprojects the supplied artwork onto the existing
male/female UVs, without modifying originals. The existing female mesh retains its
longer hem silhouette.

Publish the green-ogre and bob entries from Backend/PlayFab/CostumesV1.catalog.json
to the live CostumesV1 catalog before testing purchases. No backend deployment or
live purchase was performed.

Checks: seven unique universal catalog entries; material/texture/prefab/shop/scene
references; 200 GC prices; mesh buffer integrity; offline previews on both bodies.
Standalone C# compilation is recorded in Temp/OgreBob/compile.log.

Unity was not launched. Verify purchase/ownership refresh, equip/save/reload,
body switching, all hairstyles, and full hair restoration on unequip in-game.
Offline renders do not establish runtime correctness or clipping-free fit for
every procedural hairstyle.
