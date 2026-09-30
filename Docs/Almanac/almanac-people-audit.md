# Dine In Almanac: people and restaurant audit

Read-only audit. Paths below are relative to `G:/Unity Projects/DineIn/DineIn`. No scene/prefab changes and no Editor automation were performed during this discovery audit. No additional AGENTS.md was found under the Unity project. See README.md for the subsequent implementation validation results.

## Playable restaurants (do not promise planned gameplay)

- Casual Dining: campaign `Lobby1`. Fast Food: campaign `Lobby2`.
- Fine Dining: selectable name/exterior and clothing assets exist, but no third campaign scene assignment. The only two campaign scenes in `Assets/_Project/MainMenu/GameMenu/Scripts/UI/GameModePopupController.cs:30` are Lobby1/Lobby2; unresolved index warns instead of loading. Same two assignments serialized in `Assets/_Project/Scenes/NewMenu/NewGameMenu.unity` at `campaignRestaurantScenes`. `ProjectSettings/EditorBuildSettings.asset` enables Lobby1/Lobby2 and has no Fine Dining scene.
- `Assets/_Project/Save/CampaignSaveStore.cs` explicitly accepts only Lobby1 and Lobby2. `IsFastFood` is Lobby2.
- Fine Dining can appear as an honestly labeled preview/planned restaurant or wardrobe subsection; do not give it invented recipes, customer schedules, employees, or live campaign claims.

## Customer profiles: seven configured actual types

Authoritative profiles: `Assets/_Project/Art/Models/Customer/New Alien Models/PlayerType/` + filenames below. `CustomerTypeProfile.cs` explains each multiplier; the multipliers describe drain/duration, not percentage patience.

| Profile | Friendly entry facts | Availability |
|---|---|---|
| GreenCustomer.asset | Regular (green): baseline patience, eating and walking; no fixed happy-result tip; not messy. | Casual Day 1; Fast Food Day 1. |
| PinkCustomer.asset | VIP (pink): order and line patience drain 3x; fixed +100 tip on happy payment; normal eating; not messy. Prompt service is the useful tip. | Casual Day 5; Fast Food Day 1. |
| BlueCustomer.asset | Messy (blue): baseline patience; meal takes 2x duration; flagged messy; no fixed tip. Clear tables after meal. | Casual Day 10; Fast Food Day 1. |
| PurpleCustomer.asset | Purple: normal patience/eating/walking, normal ordering; a new Fast Food visitor with no special penalty or bonus. | Normal Fast Food Day 2, weight .7. |
| OrangeCustomer.asset | Orange: chooses orders in .75x duration; eats in .7x duration; 70% takeout chance; normal patience/walking. | Normal Fast Food Day 5, weight .2. |
| ElderlyCustomer.asset | Elderly: walking speed .7x, ordering time 1.4x, line/order patience drain 3x. Assisted priority counter service and dine-in table delivery. | Normal Fast Food Day 10, weight .15, requires an available assisted-service worker. |
| YellowCustomer.asset | Yellow / Family: family groups of 3–4, 1–2 child members (visual scale .65), eating duration 1.5x; children may play in authored dining zones and leave footprints. | Normal Fast Food Day 15, weight .15; dine-in requires capacity >=3; mobile maximum group size must support 3. |

Evidence: `Assets/_Project/Customers/Booth/GroupSpawner.CampaignCustomers.cs` gates additional types to scene Lobby2 outside protected sessions, unlock days >=2, valid prefab; checks assisted service/table capacity. `GroupSpawner.cs:149` enables original three from Fast Food Day 1. Casual days are serialized 5/10 in Lobby1 and applied by `Gameplay/GameManager/GameManager.cs:953`. `Customer/CustomerGroup.CampaignVisit.cs` handles family children and assigned dining-only play points. Exclude claims of Purple personality that are not in the data.

### Customer preview candidates

The misleading folder `Old Alien Models` contains **actual current scene-referenced prefabs**, not unused content:

- Regular: `Assets/_Project/Art/Models/Customer/Old Alien Models/GreenCustomer.prefab` (guid 62cec5e897988a94ab61176ffdf07e3a)
- VIP: same folder `PinkCustomer.prefab` (guid dcecce53ddca08c4c9737b501aa9cc25)
- Messy: same folder `BlueCustomer.prefab` (guid 2ac0d442e43602440a5783c4290b730b)
- Purple: `Assets/_Project/Art/Models/Customer/AdditionalAliens/PurpleAlien/PurpleAlienCustomer.prefab`
- Orange: `Assets/_Project/Art/Models/Customer/AdditionalAliens/OrangeAlien/OrangeAlienCustomer.prefab`
- Elderly: `Assets/_Project/Art/Models/Customer/AdditionalAliens/OldAlien/OldAlienCustomer.prefab`
- Yellow: `Assets/_Project/Art/Models/Customer/AdditionalAliens/YellowAlien/YellowAlienCustomer.prefab`

All prefab instances carry gameplay behavior/physics (CustomerAgent, NavMeshAgent, colliders, Rigidbody, outlines), so derive inert preview prefabs in Editor by stripping these components. Do not instantiate an active gameplay prefab at runtime then disable it, because Awake/OnEnable may already fire. Preserve mesh, hierarchy, rig, material and Animator only; don't drive full gameplay customer scripts. The last four raw visual-only FBX assets are beside their prefabs as PurpleAlien.fbx, OrangeAlien.fbx, OldAlien.fbx, YellowAlien.fbx; prefabs also override materials, so preserving prefab visuals is safer.

Customer profiles directly contain valid `customerImage` sprites. Original three image paths are under `New Alien Models/2dCloseupImages/` (1.png, 2.png, 3.png); resolve profile reference directly rather than guessing which is which.

All inspected customer prefabs use `Assets/_Project/Art/Models/3D Models/Character/Alien/AlienController.controller` (guid e448c94d334b127409fb68e78c00b938). Its Idle state uses the same `Assets/_Project/Art/Animations/PlayerAnimation/Idle.anim` as menu staff (guid 68b0a9eb38471694cbba952db7d69009). Parameters Speed=0 and IsSitting=false give standing idle. Controller also has sitting.anim in the Alien folder. For inert preview use a clip playable against the retained humanoid Animator; actual runtime retargeting still needs validation. `Happy Idle.fbx` is available for staff reactions, but not needed for encyclopedia idle.

## Employee roles and variants

Authoritative role catalog: `Assets/_Project/Office/HR/EmployeeRoleCatalog.cs`; enum `EmployeeRole.cs` preserves old numeric values for saves. Actual hireable roles are:

- Host: greets queued guests and coordinates seating (Casual). `Lobby/Assets/Host/Host.prefab`.
- Waiter: takes orders, delivers food and bills, brings payments to cashier (Casual). `Lobby/Assets/Waiter/Waiter.prefab`.
- Cashier: processes payments; in Fast Food works at ordering/payment counter, second cashier unlock supported. `Lobby/Assets/Cashier/Cashier.prefab`.
- Busser: clears dirty tables/trays and supports cleaning. Fast Food display name is **Lobby Person**, including meal delivery/assisted service where assigned. `Lobby/Assets/Busser/Busser.prefab`.
- Chef: Casual kitchen role, prepares food; actual KitchenManager remains timing authority.
- Barista: Casual kitchen role, drink preparation; no chef hat in configured uniform.
- Grill Station: Fast Food kitchen. The initial authoring stationProducts list includes Burger / ChickenSandwich / FishFilletSandwich; the current cooking state distinguishes cooking from preparation: sandwich proteins cook at Fry and move to Grill preparation. See the objects audit and Restaurant/FastFoodCookingState.cs for that sequence.
- Fry Station: Fast Food kitchen. The initial authoring stationProducts list contains Chicken / Fries / ChickenNuggets; current FastFoodCookingState also assigns chicken-sandwich and fish-fillet-sandwich proteins to Fry before Grill preparation.
- Assembler (`FastFoodAssembler`, enum 11): Fast Food kitchen, accepts every order and handles assembly tickets.

Evidence: `Gameplay/AutonomousService/Lobby/LobbyAutonomousService.cs` particularly 636–650, 681–732 and staff bindings338–344; `Gameplay/AutonomousService/Kitchen/KitchenWorkerBot.cs`; `Editor/FastFoodStaffAuthoring.cs` station assignments; Lobby2 serialized workers employeeRole9/10/11.

Catalog still returns Host/Waiter in its LobbyRoleArray for Fast Food, but Fast Food runtime skips normal Host/Waiter dispatch and uses Cashier/Lobby Person. Do not claim table-order waiter workflow runs in Fast Food.

Legacy PrepCook, LineCook, Assembler enum4/5/6 are save-compatible old roles; EmployeeRoleCatalog.MigrateLegacyRole maps them to Chef, Chef, Barista. `Kitchen/KitchenRoleManager.cs` still implements them for the disabled standalone Kitchen scene. Existing `UI/Almanac/Data/Roles/Kitchen/` assets are therefore stale as a description of current campaign staffing.

Existing almanac Office entries: Head Chef, HR Officer, Inventory Manager, Equipment Procurement. These represent management responsibilities, not hireable EmployeeRole values and not unique uniformed 3D staff roles. Their existing descriptions have support from RecipeManager, HRManager/EmployeeManager, Inventory/Order managers, EquipmentShopManager. Keep them as management entries if retained, labeled accurately; do not fake dedicated staff model or salary.

## Employee preview appearance: reuse the catalog

- `Assets/Resources/AppearanceCatalog.asset` loads via `DineIn.Appearance.AppearanceCatalog.Load()`.
- Shared humanoid bodies: `Assets/_Project/Player/Assets/Appearance/Models/Male.fbx` and `Female.fbx`, both importer animationType3 Human, avatarSetup1.
- `Assets/Resources/CasualEmployeeUniforms.asset` and `Assets/Resources/FastFoodEmployeeUniforms.asset` contain male and female assignments per role.
- `DineIn.Appearance.CharacterAppearance` applies a recipe, compatible skins/faces/hairs/materials/hat, instantiates the common rig and copies body meshes. Prefer it over custom replacement clothing logic. It expects an Animator on its root or child, uses default animator.transform as visualParent, and resolves catalog automatically.
- Construct an isolated preview wrapper with Animator + CharacterAppearance, configure `visualParent` to that root/Animator, use the catalog's own body recipes (or `GenerateEmployee("almanac-<role>-<body>")`, which is deterministic and doesn't consume Unity.Random), force bodyId, then call `EmployeeUniforms.Apply(recipe, role, catalog)` and `CharacterAppearance.Apply`. This avoids employee save/randomization/network code. Do not call EmployeeAppearance.Ensure/Bind with live roster; they include gameplay/session hooks.
- Casual Host/Cashier -> `<body>-casual-receptionist`, no hat. Waiter/Busser/Barista -> `<body>-casual-waiter`, no hat. Chef -> `<body>-casual-chef`, chef-hat.
- Fast Food all roles -> `<body>-fastfood-uniform`; Grill/Fry/Assembler/Chef legacy cooking values require chef-hat; lobby roles and Barista have none.
- Catalog includes Fine Dining wardrobe `<body>-fine-chef`, `<body>-fine-maitre-d`, `<body>-fine-receptionist`, `<body>-fine-waiter`, but there is no FineEmployeeUniforms asset or live Fine Dining staff campaign. Label these as wardrobe previews, not actual Fine Dining hiring configurations.
- Each catalog outfit already includes a valid thumbnail and material; reuse those authored references for cards/fallbacks. Outfits point to materials under `Player/Assets/Appearance/Materials` and textures in `Player/Assets/Appearance/Textures/Male|Female/Casual Dining|Fine Dining`, plus FastFoodUniform materials.
- `Idle.anim` is already used by menu selector with the customized humanoid rig. Use looped idle in isolated preview; don't rely on a playable gameplay controller for the entire employee prefab.

## Validation boundaries

Confirmed data and references by scripts, serialized assets and scene references only. No instantiation/render/animation playback was performed, so animation compatibility and material appearance must be checked by the implementing agent in an isolated preview.

