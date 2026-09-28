# Additional customer aliens

Old, Orange, Purple and Yellow Alien are regular-customer **visual variants**, not new gameplay types. Their original FBXs in `G:/Downloads` are unchanged.

## Assets and configuration

- `Assets/_Project/Art/Models/Customer/AdditionalAliens/<Name>/`: rigged FBX, original albedo texture, URP material and customer prefab.
- `ArtSource/Customers/AdditionalAliens/`: editable Blender sources with packed textures and 21-bone Humanoid armatures.
- Normal Lobby1 and Lobby2 GroupSpawner: **Regular Customer Visual Variants** contains the four new prefabs. Each regular group chooses one appearance from the original Green customer plus these four, equally. Pink/Blue selection, unlock rules, patience, tipping and order behavior are unchanged.
- Empty variant arrays preserve the previous behavior. Tutorial and multiplayer scenes were not opted in.
- Prefabs retain the regular customer's collider, navigation settings, tray anchors and existing AlienController (Idle, Walking, sitting). Only the visual, material and explicit Animator reference differ. Visual height and floor offset match the existing Green model's bind-pose bounds.

## Authoring

Use Blender 4.5+ in background mode with `rig_aliens.py`. Place copies of the four originals in the sibling `../AlienRiggingWork/input/` folder. Joint positions are editable in `CONFIG`; weights and bones are editable in the `.blend` sources. Auto heat weights are used where possible; Orange uses anatomical envelope weights because heat binding fails on its source mesh. Weights are normalized with no unbound vertices and at most four influences per vertex. This is a body rig, not facial/lip-sync animation.

The C# files are Unity CLI `eval_file` snippets (not scripts to place in Assets):

1. `import_models.cs`: import exported copies and validate Humanoid avatars.
2. `create_customers.cs`: regenerate these four customer prefabs from GreenCustomer. **This replaces edits to the generated prefabs**; do not rerun after hand-tuning without preserving those edits.
3. `configure_scenes.cs`: assign the four variants in normal Lobby1/Lobby2.
4. `verify_customers.cs`: isolated edit-mode checks and animation sampling; no Play Mode.
5. Blender `render_unity_poses.py`: render the meshes sampled by Unity for visual review. Columns: Old, Orange, Purple, Yellow. Rows: Idle, Walking, sitting. Rendering uses Blender lighting, not the game's URP lighting.

## Verification and remaining checks

Verified four valid Humanoid avatars, complete normalized weights, one Animator per prefab, head/hand/foot mappings, customer/tray references, and finite mesh deformation for all 12 model/clip combinations. Retargeted pose renders were inspected. Unity script compilation and focused diff checks were run.

Not run: a Play Mode customer session, full walk-cycle review, procedural eating/hand IK, tray pickup/carry, chair alignment, mobile/device tests or builds. Before shipping, check each appearance walking, sitting/eating and carrying a takeout tray in both lobbies. The very short limbs and low-poly bodies can need manual weight/pose tuning for extreme bends; these are editable body rigs, not a guarantee of every procedural pose.
