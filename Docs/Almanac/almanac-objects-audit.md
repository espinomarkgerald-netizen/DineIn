# Almanac objects audit — 2026-09-30

Read-only audit of `DineIn/Assets/_Project`; no Editor control or scene edits.

## Authoritative catalogs
- `Resources/MenuCatalog.asset`: Fast Food / Lobby2, nine products and eleven ingredients, three food bundles.
- `Resources/CasualDiningMenuCatalog.asset`: Casual Dining / Lobby1, twelve products and nineteen ingredients, no bundles.
- `Office/Inventory/Recipe.cs`: authorable description, sprite, serving model, ingredient costs, day unlock; menu manager can override current price and disable a product. Entries should describe base products without treating price as permanent.
- `Office/Inventory/ItemData.cs`: ingredient storage is explicitly Dry or Frozen; wrong storage accelerates spoilage. This is game behavior even for ingredients normally refrigerated in real life.
- `Office/Manager/Equipment/*` plus `Resources/FastFood/Equipment/*`: 35 upgrade/seat assets. Group repeated numbered tables into functional types. Fast Food upgrade assets override common IDs with restaurant-specific cost/unlock. `EquipmentManager.AllEquipment` selects the current restaurant list.
- Existing `UI/Almanac/Data` has fourteen legacy generic customer/role cards; they are not authoritative for modern gameplay (e.g. Equipment Procurement is an office task, not evidence of a current staffed role).

## Verified functional entries and source
- Grill / preparation board: `Restaurant/FastFoodCookingState.cs` (`Station`, `PreparationStation`, `AssemblySteps`) maps Burger to Grill; Chicken Sandwich and Fish Fillet Sandwich cook at Fry then transfer to Grill preparation. Bun halves consume one bun according to recipe cost, not two. Model: `Scenes/RoleBased/Kitchen/Prefabs/Grill.prefab`.
- Fryer: Fries, Fried Chicken, Chicken Nuggets, Chicken Sandwich protein and Fish Fillet Sandwich protein are assigned to Fry. Basket collection and holding-rack capacity are implemented. Source `Restaurant/FastFoodCookingState.cs`, `Restaurant/FastFoodCookingStation.cs`; model `Scenes/RoleBased/Kitchen/Prefabs/Fryer.prefab`.
- Assembly counter: combines completed portions and drink for an order. Source `Restaurant/FastFoodCookingView.Assembler.cs`, `Restaurant/FastFoodCookingState.cs`; representative authored counter `Scenes/RoleBased/Kitchen/Prefabs/Counter.prefab`.
- Food tray: order number and delivered-product identity, food/drink anchors; `Restaurant/Items/FoodTray.cs`. Model `Restaurant/Assets/Level1/GameObjects/RestaurantObjects/Customers/Food Tray.prefab`.
- Takeout bag: correct customer/order contents, takeaway service; `Restaurant/Takeout/TakeoutBagInteractable.cs` and `Customers/Customer/CustomerGroup.FastFood.cs`. Model `Art/Models/3D Models/PaperBag.prefab`. Do not claim manual pickup universally: Fast Food uses customer pickup logic, and current CanInteract intentionally suppresses player pickup for that flow.
- Cashier counter: independent counter queue, order/payment handling; `Restaurant/FastFoodServiceStation.cs`, `Lobby/Roles/CashierRegisterUI.cs`. Models `Restaurant/Prefabs/FastFood/Fast Food Cashier.prefab`, `Art/Models/3D Models/Cash Register Open.prefab`.
- Kiosk: automatic ordering/payment once bought; `Restaurant/FastFoodServiceStation.cs`. Model `Restaurant/Prefabs/FastFood/Fast Food Kiosk.prefab`.
- Table / booth seating: seats define capacity and upgrades make seats available; `Restaurant/FastFoodTable.cs`, `Customers/Booth/Booth.cs`. Table model `Restaurant/Prefabs/FastFood/Fast Food Round Table.prefab`; long/stool models adjacent. Existing table/booth icons in `Office/Manager/Equipment`.
- Busser/Lobby trolley: four dirty trays per trip by both upgrade variants. Model `Resources/Upgrades/BusserTrolley.prefab`; source both Busser Trolley assets and `Gameplay/AutonomousService/Lobby/BotTrolleyCarrier.cs`.
- Waiter/Delivery trolley: four prepared orders per trip; FF version used by Lobby Person 2. Model `Resources/Upgrades/WaiterTrolley.prefab`; source both Waiter Trolley assets and carry systems.
- Card payment: purchased upgrade allows probability-based card payments; Casual Dining asset says at table, Fast Food says cashier counter. Source `Restaurant/Items/CardPaymentService.cs`, both Card Payment assets. Icon `Art/Icons/GameIcons/Upgrades/CardPaymentIcon.png`.
- Sink: receives carried dirty trays and removes them; `Restaurant/Items/SinkInteractable.cs`. Model `Restaurant/Prefabs/FastFood/Fast Food Sink.prefab` (also `Art/Models/3D Models/Sink.prefab`). Do not substitute dishwasher behavior.
- Mop/bucket: authored cleaning appearance `Resources/Hygiene/CleaningPresentation.asset` uses `Art/Models/3D Models/MOP MODEL.fbx` and `Art/Models/3D Models/MOP BUCKET/MopBucket (1).fbx`. `Gameplay/Hygiene/HygieneManager.Actions.cs` implements lobby floor cleaning and kitchen cleaning choices; `HygieneSettings` tunings are editable.
- Dry / frozen shelf: `Restaurant/RestockRoom/Prefabs/DryRoomShelf.prefab`, `WalkingFreezerShelf.prefab`; storage environment from `ItemData.requiredStorage`, capacity enforced by `RestockOrderManager.TryStoreOneContainer`, per-cell shelf occupancy from `ShelfGrid`.
- Delivery truck: `Resources/RestockFlow/RestockDeliveryTruck.prefab`; `RestockOrderManager` purchase delivery is not recipe-usable until physical storage placement. Truck collecting moves items into temporary delivery hotbar. Wrong storage flags and spoils faster.
- Boxes/crates: all catalog ingredients currently point to `Restaurant/RestockRoom/Prefabs/CardboardBox.prefab` (do not invent unique packaging). `RestockStorageContainer.cs` displays item name, quantity and expired status.
- Management computer: source `ManagementComputer/ManagementComputerController.cs`, apps Dashboard/Staff/Menu/Restock/Equipment/Finances/Objectives. Model `Restaurant/Management/Computer Final 1.prefab`. Menu availability and prices editable; equipment purchases between service sessions; Finance reflects existing records.

## Exclusions / cautions
- No Dishwasher, Oven, Microwave, or Blender definitions found in project C# or asset configurations. Do not invent these entries based on examples in the request.
- Flowers, trees, fountains and exterior park scenery are decorative; no entries needed.
- Kitchen legacy/tutorial scenes also implement Plate, DrinkDispenser, TrashCan and DeliveryCounter. They are functional, but distinguish tutorial kitchen flow from modern Lobby2 station flow. Avoid claiming all restaurant kitchens use the old manual cup/plate system.
- Full resolved JSON companion `almanac-objects-catalogs.json` records exact source/sprite/prefab paths and raw config values; no assumptions added.

## Recipe inventory (actual names)
| Name | Restaurant | Unlock day | Config path | Image |
|---|---|---:|---|---|
| Burger | Fast Food | 1 | Assets/_Project/Office/Manager/Recipe/Cheese Burger.asset | Assets/_Project/UI/Assets/FoodIcons/Fast Food/Menu/Burger.png |
| Fried Chicken | Fast Food | 7 | Assets/_Project/Office/Manager/Recipe/Chicken Bucket.asset | Assets/_Project/UI/Assets/FoodIcons/Fast Food/Menu/Fried Chicken.png |
| Coke | Fast Food | 1 | Assets/_Project/Office/Manager/Recipe/Coke.asset | Assets/_Project/Restaurant/Assets/Customers/Icons/Drinks/Coke.png |
| Fries | Fast Food | 1 | Assets/_Project/Office/Manager/Recipe/French Fries.asset | Assets/_Project/UI/Assets/FoodIcons/Fast Food/Menu/Fries.png |
| Ice Tea | Fast Food | 1 | Assets/_Project/Office/Manager/Recipe/Iced Tea.asset | Assets/_Project/Restaurant/Assets/Customers/Icons/Drinks/ice-tea.png |
| Pineapple | Fast Food | 1 | Assets/_Project/Office/Manager/Recipe/Pineapple Juice.asset | Assets/_Project/Restaurant/Assets/Customers/Icons/Drinks/Pineapple-juice.png |
| Caesar Salad | Casual Dining | 3 | Assets/_Project/Office/Manager/Recipe/Casual Dining/Caesar Salad.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Menu/Caesar Salad.png |
| Cucumber Lemonade Pitcher | Casual Dining | 7 | Assets/_Project/Office/Manager/Recipe/Casual Dining/Cucumber Lemonade Pitcher.asset | Assets/_Project/UI/Assets/DrinksIcons/Casual Dining/Menu/Cucumber Lemonade Pitcher.png |
| Four Seasons Juice Pitcher | Casual Dining | 18 | Assets/_Project/Office/Manager/Recipe/Casual Dining/Four Seasons Juice Pitcher.asset | Assets/_Project/UI/Assets/DrinksIcons/Casual Dining/Menu/Four Seasons Juice.png |
| Fried Salmon | Casual Dining | 10 | Assets/_Project/Office/Manager/Recipe/Casual Dining/Fried Salmon.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Menu/Fried Salmon.png |
| Garlic Butter Shrimp | Casual Dining | 15 | Assets/_Project/Office/Manager/Recipe/Casual Dining/Garlic Butter Shrimp.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Menu/Garlic Butter Shrimp.png |
| Iced Tea Pitcher | Casual Dining | 1 | Assets/_Project/Office/Manager/Recipe/Casual Dining/Iced Tea Pitcher.asset | Assets/_Project/UI/Assets/DrinksIcons/Casual Dining/Menu/Iced Tea Pitcher.png |
| Mango Juice Pitcher | Casual Dining | 12 | Assets/_Project/Office/Manager/Recipe/Casual Dining/Mango Juice Pitcher.asset | Assets/_Project/UI/Assets/DrinksIcons/Casual Dining/Menu/Mango Juice Pitcher.png |
| Orange Juice Pitcher | Casual Dining | 2 | Assets/_Project/Office/Manager/Recipe/Casual Dining/Orange Juice Pitcher.asset | Assets/_Project/UI/Assets/DrinksIcons/Casual Dining/Menu/Orange Juice Pitcher.png |
| Pineapple Juice Pitcher | Casual Dining | 4 | Assets/_Project/Office/Manager/Recipe/Casual Dining/Pineapple Juice Pitcher.asset | Assets/_Project/UI/Assets/DrinksIcons/Casual Dining/Menu/Pineapple Juice Pitcher.png |
| Pork Chop | Casual Dining | 6 | Assets/_Project/Office/Manager/Recipe/Casual Dining/Pork Chop.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Menu/Pork Chop.png |
| Roasted Chicken | Casual Dining | 20 | Assets/_Project/Office/Manager/Recipe/Casual Dining/Roasted Chicken.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Menu/Roasted Chicken.png |
| Tomato Soup | Casual Dining | 1 | Assets/_Project/Office/Manager/Recipe/Casual Dining/Tomato Soup.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Menu/Tomato Soup.png |
| Chicken Nuggets | Fast Food | 3 | Assets/_Project/Resources/FastFood/Recipes/Chicken Nuggets.asset | Assets/_Project/UI/Assets/FoodIcons/Fast Food/Menu/Chicken Nuggets.png |
| Chicken Sandwich | Fast Food | 5 | Assets/_Project/Resources/FastFood/Recipes/Chicken Sandwich.asset | Assets/_Project/UI/Assets/FoodIcons/Fast Food/Menu/Chicken Sandwich.png |
| Fish Fillet Sandwich | Fast Food | 10 | Assets/_Project/Resources/FastFood/Recipes/Fish Fillet Sandwich.asset | Assets/_Project/UI/Assets/FoodIcons/Fast Food/Menu/Fish Fillet Sandwich.png |

## Ingredient inventory
| Source name | Storage | Config | Sprite |
|---|---|---|---|
| Burger Buns ×20 | Dry | Assets/_Project/Office/Inventory/Buns.asset | Assets/_Project/UI/Assets/FoodIcons/Fast Food/Ingredients/Burger Bun.png |
| Cheese Slices ×20 | Frozen | Assets/_Project/Office/Inventory/Cheese Slice.asset | Assets/_Project/UI/Assets/Legacy/Icons/Foods/Cheese Slice.png |
| Coke Mix – 20 Servings | Dry | Assets/_Project/Office/Inventory/Coke.asset | Assets/_Project/Restaurant/Assets/Customers/Icons/Drinks/Coke.png |
| Chicken Drumsticks ×12 | Frozen | Assets/_Project/Office/Inventory/Drumstick.asset | Assets/_Project/UI/Assets/FoodIcons/Fast Food/Ingredients/Chicken Pieces.png |
| French Fries ×10 | Frozen | Assets/_Project/Office/Inventory/French Fry Bag.asset | Assets/_Project/UI/Assets/FoodIcons/Fast Food/Ingredients/Frozen Fries.png |
| Iced Tea Mix – 20 Servings | Dry | Assets/_Project/Office/Inventory/Iced Tea.asset | Assets/_Project/Restaurant/Assets/Customers/Icons/Drinks/ice-tea.png |
| Beef Patties ×20 | Frozen | Assets/_Project/Office/Inventory/Patty.asset | Assets/_Project/UI/Assets/FoodIcons/Fast Food/Ingredients/Beef Patty.png |
| Pineapple Mix – 20 | Dry | Assets/_Project/Office/Inventory/Pineapple Juice.asset | Assets/_Project/Restaurant/Assets/Customers/Icons/Drinks/Pineapple-juice.png |
| Bouillon Cube | Dry | Assets/_Project/Office/Inventory/Casual Dining/Bouillon Cube.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Ingredients/Bouillon Cube.png |
| Butter | Frozen | Assets/_Project/Office/Inventory/Casual Dining/Butter.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Ingredients/Butter.png |
| Canned Tomato | Dry | Assets/_Project/Office/Inventory/Casual Dining/Canned Tomato.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Ingredients/Canned Tomato.png |
| Cucumber Lemonade Powder | Dry | Assets/_Project/Office/Inventory/Casual Dining/Cucumber Lemonade Powder.asset | Assets/_Project/UI/Assets/DrinksIcons/Casual Dining/Ingredient/Powdered Cucumber Lemonade.png |
| Dressing | Frozen | Assets/_Project/Office/Inventory/Casual Dining/Dressing.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Ingredients/Dressing.png |
| Four Seasons Juice Powder | Dry | Assets/_Project/Office/Inventory/Casual Dining/Four Seasons Juice Powder.asset | Assets/_Project/UI/Assets/DrinksIcons/Casual Dining/Ingredient/Powdered Four Seasons Juice.png |
| Garlic | Dry | Assets/_Project/Office/Inventory/Casual Dining/Garlic.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Ingredients/Garlic.png |
| Iced Tea Powder | Dry | Assets/_Project/Office/Inventory/Casual Dining/Iced Tea Powder.asset | Assets/_Project/UI/Assets/DrinksIcons/Casual Dining/Ingredient/Powdered Iced Tea.png |
| Lemon | Frozen | Assets/_Project/Office/Inventory/Casual Dining/Lemon.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Ingredients/Lemon.png |
| Lettuce | Frozen | Assets/_Project/Office/Inventory/Casual Dining/Lettuce.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Ingredients/Lettuce.png |
| Mango Juice Powder | Dry | Assets/_Project/Office/Inventory/Casual Dining/Mango Juice Powder.asset | Assets/_Project/UI/Assets/DrinksIcons/Casual Dining/Ingredient/Powdered Mango Juice.png |
| Oil | Dry | Assets/_Project/Office/Inventory/Casual Dining/Oil.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Ingredients/Oil.png |
| Orange Juice Powder | Dry | Assets/_Project/Office/Inventory/Casual Dining/Orange Juice Powder.asset | Assets/_Project/UI/Assets/DrinksIcons/Casual Dining/Ingredient/Powdered Orange Juice.png |
| Pineapple Juice Powder | Dry | Assets/_Project/Office/Inventory/Casual Dining/Pineapple Juice Powder.asset | Assets/_Project/UI/Assets/DrinksIcons/Casual Dining/Ingredient/Powdered Pineapple Juice.png |
| Pork Chop | Frozen | Assets/_Project/Office/Inventory/Casual Dining/Pork Chop.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Ingredients/PorkChop.png |
| Salmon Fillet | Frozen | Assets/_Project/Office/Inventory/Casual Dining/Salmon Fillet.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Ingredients/Salmon Fillet.png |
| Seasoning | Dry | Assets/_Project/Office/Inventory/Casual Dining/Seasoning.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Ingredients/Seasoning.png |
| Shrimp | Frozen | Assets/_Project/Office/Inventory/Casual Dining/Shrimp.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Ingredients/Shrimp.png |
| Whole Chicken | Frozen | Assets/_Project/Office/Inventory/Casual Dining/Whole Chicken.asset | Assets/_Project/UI/Assets/FoodIcons/Casual Dining/Ingredients/Whole Chicken.png |
| Chicken Patty x20 | Frozen | Assets/_Project/Resources/FastFood/Ingredients/Chicken Patty.asset | Assets/_Project/UI/Assets/FoodIcons/Fast Food/Ingredients/Chicken Patty.png |
| Frozen Fish Fillet x20 | Frozen | Assets/_Project/Resources/FastFood/Ingredients/Frozen Fish Fillet.asset | Assets/_Project/UI/Assets/FoodIcons/Fast Food/Ingredients/Frozen Fish Fillet.png |
| Frozen Nuggets x20 | Frozen | Assets/_Project/Resources/FastFood/Ingredients/Frozen Nuggets.asset | Assets/_Project/UI/Assets/FoodIcons/Fast Food/Ingredients/Frozen Nuggets.png |
