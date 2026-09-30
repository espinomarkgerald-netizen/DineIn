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


## Content and visual polish audit — 2026-09-30

### Staff taxonomy and names

Rechecked `EmployeeRoleCatalog`, `EmployeeManager.IsRoleUsedInCurrentRestaurant`, `ManagementComputerHRPanel`, `ManagementHRRoleSectionUI` and `ManagementEmployeeCardUI`. The current management UI uses the catalog's names directly: **Lobby Person**, **Grill Station**, **Fry Station**, **Assembler**. These are intentional player-facing labels. The latter three are separate EmployeeRole enum values and independent Fast Food hiring/scheduling roles, not optional assignments on one generic Chef employee. No Manager employee was invented.

Almanac sections now reflect actual campaign use:

- Shared roles (order 0): Cashier, with Casual Dining and Fast Food uniform previews.
- Casual Dining (order 1): Host, Waiter, Busser, Chef, Barista.
- Fast Food (order 2): Lobby Person, Grill Station, Fry Station, Assembler.

`EmployeeManager.IsRoleUsedInCurrentRestaurant` excludes Host/Waiter from normal Fast Food and excludes Chef/Barista when the Fast Food station roles are in use. Busser's Fast Food display name and responsibilities justify its separate Lobby Person presentation; the underlying role is still Busser. Uniform variant labels remain restaurant + Male/Female and are shown beside the preview by the reader. Search aliases include familiar terms such as server, receptionist, fryer, cook and lobby staff without renaming the actual UI roles.

Staff list thumbnails must be rendered from the same baked, uniformed preview used in the detail view. `PeopleStaff` now references `Assets/_Project/UI/Almanac/Thumbnails/<entryId>.png` for these ten IDs: `staff-host`, `staff-waiter`, `staff-cashier`, `staff-busser`, `staff-lobby-person`, `staff-chef`, `staff-barista`, `staff-grill`, `staff-fry`, `staff-fast-food-assembler`. Existing outfit-only thumbnails are no longer assigned as role portraits.

### Progression recheck

The claims still match normal campaign code/configuration. `GroupSpawner.NormalFastFood` is Lobby2 outside protected sessions; both `SetCustomerTypeAvailability` and `IsCustomerTypeEnabled` explicitly enable Green/Pink/Blue from Day 1 in that mode. `GameDayManager` uses the same override, even though legacy FastFoodProgressionSettings pink/blue day fields remain 5/10. Casual Lobby1's serialized unlocks remain Day 5/10. Additional profile unlocks remain Purple 2, Orange 5, Elderly 10, Yellow 15. Orange retains a 70% takeaway override; other additional guests defer to the restaurant probability instead of assuming the profile's unused default is active.

`StaffHiringProgression.asset` allows the second Lobby Person hire from Day 10 and the second Cashier hire from Day 15. A working second Fast Food cashier also requires the purchased `ff_second_cashier` station (`FastFoodProgressionSettings.HasSecondCashier`). The entry states that a second cashier station can be unlocked and does not imply day progression alone creates it.

Cooking descriptions now distinguish the current recipe-controlled stages: sandwich proteins are fried, then prepared at the grill board; burger patties are grilled. Runtime `FastFoodCookingState.Station` / `PreparationStation` and recipe settings take priority over the older authoring `stationProducts` lists.

### Actual restaurant presentation

The source is `Assets/_Project/Scenes/NewMenu/NewGameMenu.unity`, root `Restaurants`:

| Entry | Exact child | Source model | Actual material presentation |
|---|---|---|---|
| Casual Dining | `CasualDiningExterior (1)` | `Assets/_Project/Art/Models/3D Models/CasualDiningExterior/CasualDiningExterior (1).fbx` | Scene override on renderer source fileID -7511558181221131132: `Assets/_Project/Art/Models/3D Models/CasualDiningExterior/Materials/CasualDiningExterior (1).mat`, guid 663d8b75e83a03c4684d62c3b70a0676. |
| Fast Food | `FastFoodRestaurant_Exterior` | `Assets/_Project/Art/Models/3D Models/FastFoodRestaurant_Exterior.fbx` | Actual imported renderer material; this scene instance has no material override. Preserve its renderer references as authored. |
| Fine Dining | `diner (2)` | `Assets/_Project/MainMenu/NewDesign/Restaurant/Models/3D Models/Objects/Diner/diner.fbx` | Scene override on renderer source fileID -7511558181221131132: `Assets/_Project/MainMenu/NewDesign/Restaurant/Models/3D Models/Objects/Diner/Diner/Materials/Meshy_AI_Mid_Century_Diner_0713023116_texture.mat`, guid 03978b89d36447946bec29779124ad4e. |

The Restaurants children are explicitly ordered Casual / Fast Food / diner (2), matching the three selector names Casual Dining / Fast Food / Fine Dining and the three travel points. Fine Dining is therefore a configured **district map preview** only: `campaignRestaurantScenes` still contains just Lobby1/Lobby2. Its article now says coming soon and describes only the building currently assigned to that map location.

`PeopleRestaurantVisual` copies the actual authored scene instance through `AlmanacPreviewStage.CreateVisualCopy`, retaining its model orientation, scale and material assignments. It reads the already loaded source scene, or opens the saved scene as an Editor preview scene, and closes only scenes it owns. It does not mutate or save the source scene and does not fall back to an unconfigured FBX. The derived `Previews/restaurant-*.prefab` assets are refreshed when authoring is explicitly invoked; generated wide static thumbnails are handled by the shared authoring pass. Each entry's sourceNotes records actual renderer/material paths at generation time.

The customer authoring accidentally switched entries to Image whenever their existing profile icon was available. That switch was removed: profile images remain list thumbnails, while all seven customer detail previews remain live Character entries.

This polish audit and authoring-code update did not invoke Unity, regenerate assets or run Play Mode. The root implementation task owns compilation, asset generation and visual validation.
