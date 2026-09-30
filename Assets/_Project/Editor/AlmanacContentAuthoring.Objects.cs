#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>Audited functional objects and catalog-backed food. No runtime catalog or gameplay state is changed.</summary>
public static partial class AlmanacContentAuthoring
{
    public static void PopulateObjects()
    {
        var catalogs = new[]
        {
            AssetDatabase.LoadAssetAtPath<MenuCatalog>("Assets/_Project/Resources/MenuCatalog.asset"),
            AssetDatabase.LoadAssetAtPath<MenuCatalog>("Assets/_Project/Resources/CasualDiningMenuCatalog.asset")
        };
        foreach (var catalog in catalogs.Where(c => c != null))
        {
            foreach (var recipe in catalog.Products.Where(r => r != null && r.availableOnMenu))
                PopulateObjectRecipe(catalog, recipe);
            foreach (var ingredient in catalog.Ingredients.Where(i => i != null))
                PopulateObjectIngredient(catalog, ingredient);
        }
        PopulateKitchenObjects();
        PopulateServiceObjects();
        PopulateStorageObjects();
        PopulateManagementObjects();
    }

    private static string ObjectSlug(string value) => Regex.Replace((value ?? string.Empty).ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
    private static string ObjectFoodId(Recipe recipe) => "food-" + ObjectSlug(recipe.recipeName);
    private static string ObjectIngredientId(ItemData item) => "ingredient-" + ObjectSlug(item.StableItemId);
    private static string ObjectRestaurantId(RestaurantType type) => type == RestaurantType.FastFood ? "restaurant-fast-food" : "restaurant-casual-dining";
    private static string ObjectRestaurantName(RestaurantType type) => type == RestaurantType.FastFood ? "Fast Food" : "Casual Dining";
    private static string ObjectIngredientName(ItemData item)
    {
        // Bulk quantities belong in the notes; the inventory catalog remains untouched.
        return Regex.Replace(item.displayName ?? item.name, @"\s*(?:[x\u00d7]\s*\d+|[\u2013-]\s*\d+(?:\s+Servings)?)\s*$", "", RegexOptions.IgnoreCase).Trim();
    }

    private static void PopulateObjectRecipe(MenuCatalog catalog, Recipe recipe)
    {
        string restaurant = ObjectRestaurantName(catalog.RestaurantType);
        var ingredients = (recipe.ingredients ?? new List<RecipeIngredient>())
            .Where(i => i != null && i.item != null && i.amount > 0).ToArray();
        bool drink = recipe.category == MenuProductCategory.Drink;
        string introduction = string.IsNullOrWhiteSpace(recipe.descriptionText)
            ? recipe.DisplayName + " is a " + (drink ? "drink" : "dish") + " on the " + restaurant + " menu."
            : recipe.descriptionText;
        var notes = new List<string>();
        if (ingredients.Length > 0)
            notes.Add("Ingredients per serving\n" + string.Join("\n", ingredients
                .Select(i => "\u2022 " + ObjectIngredientName(i.item) + " \u00d7" + i.amount)));
        var links = new List<string> { ObjectRestaurantId(catalog.RestaurantType), "management-menu" };
        if (catalog.RestaurantType == RestaurantType.FastFood)
        {
            var station = FastFoodCookingState.Station(recipe);
            if (station == FastFoodStationMode.Assembler)
            {
                notes.Add("Preparation\nAdd the drink at the assembly counter when completing its order.");
                links.Add("kitchen-assembler");
            }
            else
            {
                bool fry = station == FastFoodStationMode.Fry;
                string preparation = fry
                    ? "Cook at the fryer, then collect the raised basket onto the holding rack."
                    : "Cook at the grill. Collect it before it burns.";
                if (FastFoodCookingState.AssemblySteps(recipe).Count > 0 &&
                    FastFoodCookingState.PreparationStation(recipe) == FastFoodStationMode.Grill)
                    preparation += " Then assemble it at the grill preparation board in the shown order.";
                notes.Add("Preparation\n" + preparation);
                links.Add(fry ? "kitchen-fryer" : "kitchen-grill");
            }
        }
        else notes.Add("Keep these ingredients stocked and enable this " + (drink ? "drink" : "dish") + " for customer orders.");
        notes.Add("Available from Day " + Mathf.Max(1, recipe.dayToUnlock) + ".");
        links.AddRange(ingredients.Select(i => ObjectIngredientId(i.item)));
        var entry = Entry(ObjectFoodId(recipe), AlmanacCategory.Food, recipe.DisplayName,
            restaurant + " \u00b7 DISH", introduction, string.Join("\n\n", notes),
            AssetDatabase.GetAssetPath(recipe) + "\n" + AssetDatabase.GetAssetPath(catalog) +
            "\nAssets/_Project/Office/Inventory/Recipe.cs\nAssets/_Project/Restaurant/FastFoodCookingState.cs",
            iconPath: AssetDatabase.GetAssetPath(recipe.sprite), related: links.Distinct().ToArray());
        entry.listSection = "Dishes";
        entry.sectionOrder = 0;
        entry.searchAliases = restaurant + " " + recipe.name + (drink ? " drink beverage" : " dish food");
    }

    private static void PopulateObjectIngredient(MenuCatalog catalog, ItemData item)
    {
        var recipes = catalog.Products.Where(r => r != null && r.availableOnMenu && r.ingredients != null &&
            r.ingredients.Any(i => i != null && i.item == item && i.amount > 0)).ToArray();
        bool frozen = item.requiredStorage == RestockStorageType.Frozen;
        string restaurant = ObjectRestaurantName(catalog.RestaurantType);
        string description = ObjectIngredientName(item) + " is a stock ingredient for " + restaurant + ".";
        var notes = new List<string>
        {
            "Stored in: " + (frozen ? "Frozen storage" : "Dry storage"),
            "Used for: " + string.Join(", ", recipes.Select(r => r.DisplayName))
        };
        if (catalog.RestaurantType == RestaurantType.FastFood)
        {
            var stations = new List<string>();
            foreach (var recipe in recipes)
            {
                if (recipe.category == MenuProductCategory.Drink) stations.Add("Assembly counter");
                else
                {
                    // Only the first cooking ingredient goes on the hot appliance.
                    // Bun/cheese and cooked proteins instead participate in the prep sequence.
                    if (FastFoodCookingState.Steps(recipe).FirstOrDefault()?.item == item)
                        stations.Add(FastFoodCookingState.Station(recipe) == FastFoodStationMode.Fry ? "Fryer" : "Grill");
                    if (FastFoodCookingState.AssemblySteps(recipe).Any(step => step != null && step.item == item) &&
                        FastFoodCookingState.PreparationStation(recipe) == FastFoodStationMode.Grill)
                        stations.Add("Grill preparation board");
                }
            }
            if (stations.Count > 0) notes.Add("Prepared at: " + string.Join(", ", stations.Distinct()));
        }
        notes.Add("Box quantity: " + item.unitsPerBox + " units");
        var links = recipes.Select(ObjectFoodId).Concat(new[] { frozen ? "storage-frozen" : "storage-dry", "storage-delivery" }).ToArray();
        var entry = Entry(ObjectIngredientId(item), AlmanacCategory.Food, ObjectIngredientName(item),
            restaurant + " \u00b7 INGREDIENT", description, string.Join("\n", notes),
            AssetDatabase.GetAssetPath(item) + "\n" + AssetDatabase.GetAssetPath(catalog) +
            "\nAssets/_Project/Office/Inventory/ItemData.cs\nAssets/_Project/Restaurant/FastFoodCookingState.cs" +
            "\nAssets/_Project/Restaurant/RestockRoom/RestockOrderManager.cs",
            iconPath: AssetDatabase.GetAssetPath(item.sprite), related: links);
        entry.listSection = "Ingredients";
        entry.sectionOrder = 1;
        entry.searchAliases = restaurant + " " + item.name + " " + item.displayName + " ingredient stock";
    }

    private static void PopulateKitchenObjects()
    {
        Entry("kitchen-grill", AlmanacCategory.Kitchen, "Grill & Preparation Board", "Fast Food / Cooking station",
            "A grill cooks food on a hot surface. Its preparation board is where sandwiches come together.",
            "Cook burger patties at the grill.\nAssemble burgers with bun and cheese in the shown order.\nChicken and fish sandwich proteins come from the fryer, then finish at this preparation board.\nCollect cooked portions before they burn.",
            "Assets/_Project/Restaurant/FastFoodCookingState.cs: Station, PreparationStation, AssemblySteps\nAssets/_Project/Office/Manager/Recipe/Cheese Burger.asset\nAssets/_Project/Resources/FastFood/Recipes",
            prefabPath: "Assets/_Project/Scenes/RoleBased/Kitchen/Prefabs/Grill.prefab", kind: AlmanacPreviewKind.Model,
            related: new[] { "food-burger", "food-chicken-sandwich", "food-fish-fillet-sandwich", "kitchen-fryer" });
        Entry("kitchen-fryer", AlmanacCategory.Kitchen, "Fryer", "Fast Food / Cooking station",
            "A fryer cooks food in hot oil. Keep an eye on each basket as the food cooks.",
            "Prepare fries, fried chicken, nuggets, and the proteins for chicken and fish sandwiches.\nCollect ready baskets and use the holding rack.\nChicken and fish sandwiches finish at the grill preparation board.\nReady fryer food stops heating. Collect raised baskets when the holding rack has space.",
            "Assets/_Project/Restaurant/FastFoodCookingState.cs: Station, BasketRaised, CollectBasket\nAssets/_Project/Office/Manager/Recipe\nAssets/_Project/Resources/FastFood/Recipes",
            prefabPath: "Assets/_Project/Scenes/RoleBased/Kitchen/Prefabs/Fryer.prefab", kind: AlmanacPreviewKind.Model,
            related: new[] { "food-fries", "food-fried-chicken", "food-chicken-nuggets", "kitchen-grill" });
        Entry("kitchen-assembler", AlmanacCategory.Kitchen, "Assembly Counter", "Fast Food / Order assembly",
            "The assembly counter brings finished food and drinks together into a customer's order.",
            "Use the order ticket to choose the required portions.\nPlace each finished item on the order's tray.\nDrinks are added here alongside cooked food.\nEvery required portion must be placed before the ticket is ready.",
            "Assets/_Project/Restaurant/FastFoodCookingState.cs: Ticket.Ready, Place\nAssets/_Project/Restaurant/FastFoodCookingView.Assembler.cs",
            prefabPath: "Assets/_Project/Scenes/RoleBased/Kitchen/Prefabs/Counter.prefab", kind: AlmanacPreviewKind.Model,
            related: new[] { "service-tray", "service-order-pad", "kitchen-grill", "kitchen-fryer" });
    }

    private static void PopulateServiceObjects()
    {
        Entry("service-tray", AlmanacCategory.Service, "Food Tray", "Order delivery",
            "A tray keeps a customer's food and drink together for service.",
            "Prepared trays carry an order number and their food contents.\nMatch the tray to the correct order before delivery.\nUsed trays must be cleared so the dining area is ready for new guests.",
            "Assets/_Project/Restaurant/Items/FoodTray.cs\nAssets/_Project/Restaurant/Items/FoodTrayInteractable.cs\nAssets/_Project/Restaurant/Items/SinkInteractable.cs",
            prefabPath: "Assets/_Project/Restaurant/Assets/Level1/GameObjects/RestaurantObjects/Customers/Food Tray.prefab", kind: AlmanacPreviewKind.Model,
            related: new[] { "equipment-waiter-trolley", "equipment-busser-trolley", "storage-sink" });
        Entry("service-takeout-bag", AlmanacCategory.Service, "Takeout Bag", "Takeaway orders",
            "A takeaway bag packages a prepared order for a customer leaving the restaurant.",
            "Each bag belongs to a particular customer order.\nFast Food customers use the pickup flow after their order is ready.\nTakeaway orders skip the table meal and used-tray cleanup flow.",
            "Assets/_Project/Restaurant/Takeout/TakeoutBagInteractable.cs\nAssets/_Project/Customers/Customer/CustomerGroup.FastFood.cs\nAssets/_Project/Restaurant/Items/KitchenManager.cs",
            prefabPath: "Assets/_Project/Art/Models/3D Models/PaperBag.prefab", kind: AlmanacPreviewKind.Model,
            related: new[] { "restaurant-fast-food", "service-cashier", "service-kiosk" });
        Entry("service-cashier", AlmanacCategory.Service, "Cashier Counter", "Orders & payment",
            "A cashier counter is the service point for taking orders and settling payments.",
            "Fast Food counters have their own ordering queues.\nCashiers handle the order and payment stages.\nA second counter can be bought through Equipment; staffing is managed separately.\nThe Card Payment upgrade adds card payments at Fast Food counters.",
            "Assets/_Project/Restaurant/FastFoodServiceStation.cs\nAssets/_Project/Lobby/Roles/CashierRegisterUI.cs\nAssets/_Project/Resources/FastFood/Equipment/ff_second_cashier.asset",
            prefabPath: "Assets/_Project/Restaurant/Prefabs/FastFood/Fast Food Cashier.prefab", kind: AlmanacPreviewKind.Model,
            related: new[] { "equipment-card-payment", "service-kiosk", "management-computer" });
        Entry("service-kiosk", AlmanacCategory.Service, "Self-Service Kiosk", "Fast Food / Ordering",
            "A self-service kiosk lets customers place and pay for their own order.",
            "Customers order and pay automatically at an unlocked kiosk.\nEach kiosk opens an independent queue.\nBuy the restaurant's kiosk upgrades in Computer > Equipment when they become available.",
            "Assets/_Project/Restaurant/FastFoodServiceStation.cs\nAssets/_Project/Resources/FastFood/Equipment/ff_kiosk_1.asset\nAssets/_Project/Resources/FastFood/Equipment/ff_kiosk_2.asset",
            prefabPath: "Assets/_Project/Restaurant/Prefabs/FastFood/Fast Food Kiosk.prefab", kind: AlmanacPreviewKind.Model,
            related: new[] { "restaurant-fast-food", "service-cashier", "management-progression" });
        const string orderCapture = "Assets/_Project/UI/Almanac/Thumbnails/service-order-ui.png";
        var orderEntry = Entry("service-order-pad", AlmanacCategory.Service, "Order Check", "Customer orders / Food, drinks & quantities",
            "Use the customer's requested items and quantities to collect and check their order.",
            "Choose the requested items and quantities in Food and Drinks.\nSelect CHECK ORDER to compare your selection with the customer's request.\nIf anything is missing, extra or incorrect, use FIX ORDER and correct it.\nWhen ORDER MATCHES appears, select CONFIRM ORDER. The accepted order then continues to kitchen and payment service.",
            "Assets/_Project/Restaurant/Items/OrderChecklistUI.cs: Open, BindStaticButtons, CheckOrder, ShowReviewPanel, Confirm\n" +
            "Assets/_Project/Restaurant/Items/ReviewedOrderSubmission.cs: TryBuild, TrySubmit\n" +
            "Assets/_Project/Restaurant/FastFoodCounter.cs: Interact\n" +
            "Assets/_Project/Scenes/RoleBased/Lobby1.unity: ORDER CHECK\nAssets/_Project/Scenes/RoleBased/Lobby2.unity: ORDER CHECK\n" +
            "Illustrative UI-only example: Tomato Soup x1 and Iced Tea Pitcher x1, with sample displayed stock of 12. Captured from the actual Lobby1 OrderChecklistUI and Notepad Menu Item prefab; no customer, campaign, order, inventory or save state was changed.",
            iconPath: AssetDatabase.LoadAssetAtPath<Sprite>(orderCapture) != null ? orderCapture : null,
            related: new[] { "management-menu", "service-tray", "kitchen-assembler" });
        orderEntry.searchAliases = "order notepad order review customer order order panel order collection";
        orderEntry.widePreview = true;
        Entry("equipment-seating", AlmanacCategory.Equipment, "Tables & Booths", "Dining capacity",
            "Tables and booths give dine-in customers somewhere to sit and enjoy their meal.",
            "Each table has a fixed set of seats and service points.\nSeating upgrades make additional seats available.\nFast Food includes booths, round tables, window tables and back-stool tables.\nClear used trays and clean dirty tables for the next guests.",
            "Assets/_Project/Restaurant/FastFoodTable.cs\nAssets/_Project/Customers/Booth/Booth.cs\nAssets/_Project/Office/Manager/Equipment\nAssets/_Project/Resources/FastFood/Equipment",
            iconPath: "Assets/_Project/Office/Manager/Equipment/Table Icon.png",
            prefabPath: "Assets/_Project/Restaurant/Prefabs/FastFood/Fast Food Round Table.prefab", kind: AlmanacPreviewKind.Model,
            related: new[] { "equipment-busser-trolley", "storage-mop", "management-progression" });
        Entry("equipment-busser-trolley", AlmanacCategory.Equipment, "Busser / Lobby Trolley", "Cleanup upgrade",
            "A trolley carries several used trays in one trip, reducing journeys back to the sink.",
            "The Busser Trolley in Casual Dining and Lobby Trolley in Fast Food each hold up to four dirty trays.\nPurchase the upgrade from Computer > Equipment.\nUpgrade availability and cost depend on the restaurant.",
            "Assets/_Project/Office/Manager/Equipment/Upgrades/Busser Trolley.asset\nAssets/_Project/Resources/FastFood/Equipment/Busser Trolley.asset\nAssets/_Project/Gameplay/AutonomousService/Lobby/BotTrolleyCarrier.cs",
            iconPath: "Assets/_Project/Art/Icons/GameIcons/Upgrades/BusserTrolleyIcon.png",
            prefabPath: "Assets/_Project/Resources/Upgrades/BusserTrolley.prefab", kind: AlmanacPreviewKind.Model,
            related: new[] { "service-tray", "storage-sink", "equipment-seating" });
        Entry("equipment-waiter-trolley", AlmanacCategory.Equipment, "Waiter / Delivery Trolley", "Service upgrade",
            "A delivery trolley carries several prepared orders on one trip to the dining area.",
            "The Waiter Trolley carries up to four prepared orders.\nFast Food calls it the Delivery Trolley, used by Lobby Person 2 for table delivery.\nPurchase the upgrade and arrange the appropriate staff through the computer.",
            "Assets/_Project/Office/Manager/Equipment/Upgrades/Waiter Trolley.asset\nAssets/_Project/Resources/FastFood/Equipment/Waiter Trolley.asset\nAssets/_Project/Gameplay/AutonomousService/Lobby/BotTrolleyCarrier.cs",
            iconPath: "Assets/_Project/Art/Icons/GameIcons/Upgrades/WaiterTrolleyIcon.png",
            prefabPath: "Assets/_Project/Resources/Upgrades/WaiterTrolley.prefab", kind: AlmanacPreviewKind.Model,
            related: new[] { "service-tray", "equipment-busser-trolley", "management-computer" });
        Entry("equipment-card-payment", AlmanacCategory.Equipment, "Card Payment", "Payment upgrade",
            "Card payment offers an alternative to cash when customers settle their bill.",
            "After purchase, some customers choose card payment.\nCasual Dining uses card payments at the table.\nFast Food accepts card payments at cashier counters.\nThe upgrade is purchased through Computer > Equipment.",
            "Assets/_Project/Restaurant/Items/CardPaymentService.cs\nAssets/_Project/Office/Manager/Equipment/Upgrades/Card Payment.asset\nAssets/_Project/Resources/FastFood/Equipment/Card Payment.asset",
            iconPath: "Assets/_Project/Art/Icons/GameIcons/Upgrades/CardPaymentIcon.png",
            related: new[] { "service-cashier", "management-computer" });
    }

    private static void PopulateStorageObjects()
    {
        Entry("storage-sink", AlmanacCategory.CleaningStorage, "Sink", "Tray cleanup",
            "A sink provides a washing point for used service ware.",
            "Bring carried dirty trays to the sink to finish the cleanup trip.\nKeeping trays moving back to the sink helps staff prepare the dining area for new guests.",
            "Assets/_Project/Restaurant/Items/SinkInteractable.cs\nAssets/_Project/Gameplay/AutonomousService/Lobby/LobbyAutonomousService.cs",
            prefabPath: "Assets/_Project/Restaurant/Prefabs/FastFood/Fast Food Sink.prefab", kind: AlmanacPreviewKind.Model,
            related: new[] { "service-tray", "equipment-busser-trolley" });
        Entry("storage-mop", AlmanacCategory.CleaningStorage, "Mop & Cleaning", "Restaurant hygiene",
            "A mop helps staff clean the dining floor and keep the restaurant welcoming.",
            "Walking and restaurant service can create dirt and spills.\nRequest a floor-cleaning pass from available cleaning staff.\nDirty tables need attention between groups of guests.\nKitchen cleaning has its own work and cooking tradeoffs.",
            "Assets/_Project/Gameplay/Hygiene/HygieneManager.cs\nAssets/_Project/Gameplay/Hygiene/HygieneManager.Actions.cs\nAssets/_Project/Gameplay/Hygiene/HygieneSettings.cs\nAssets/_Project/Resources/Hygiene/CleaningPresentation.asset",
            prefabPath: "Assets/_Project/Art/Models/3D Models/MOP MODEL.fbx", kind: AlmanacPreviewKind.Model,
            related: new[] { "storage-sink", "equipment-seating", "management-computer" });
        Entry("storage-dry", AlmanacCategory.CleaningStorage, "Dry Storage", "Ingredient shelves",
            "Dry storage keeps ingredients that the restaurant catalog assigns to a dry environment.",
            "Place delivered boxes on a dry shelf to add their contents to usable stock.\nEach container occupies shelf space.\nUse the ingredient's required storage type: incorrect storage shortens its useful life.",
            "Assets/_Project/Office/Inventory/ItemData.cs\nAssets/_Project/Restaurant/RestockRoom/RestockOrderManager.cs: TryStoreOneContainer\nAssets/_Project/Restaurant/RestockRoom/Prefabs/ShelvesScript/ShelfGrid.cs",
            prefabPath: "Assets/_Project/Restaurant/RestockRoom/Prefabs/DryRoomShelf.prefab", kind: AlmanacPreviewKind.Model,
            related: new[] { "storage-frozen", "storage-delivery", "storage-containers" });
        Entry("storage-frozen", AlmanacCategory.CleaningStorage, "Frozen Storage", "Cold ingredient shelves",
            "Frozen storage is the stock-room area for ingredients marked for frozen storage in Dine In.",
            "Use the ingredient's storage label when placing delivered boxes.\nFrozen and dry storage have separate capacity limits.\nKeeping a frozen ingredient in the wrong environment makes it spoil faster.",
            "Assets/_Project/Office/Inventory/ItemData.cs\nAssets/_Project/Restaurant/RestockRoom/RestockOrderManager.cs: TryStoreOneContainer\nAssets/_Project/Resources/CasualDiningStorageConfig.asset",
            prefabPath: "Assets/_Project/Restaurant/RestockRoom/Prefabs/WalkingFreezerShelf.prefab", kind: AlmanacPreviewKind.Model,
            related: new[] { "storage-dry", "storage-delivery", "storage-containers" });
        Entry("storage-delivery", AlmanacCategory.CleaningStorage, "Delivery Truck", "Restocking",
            "The delivery truck brings the ingredients purchased from the restaurant computer.",
            "Order boxes from Computer > Restock and collect them when the truck arrives.\nCollected deliveries wait in the delivery hotbar.\nPlace a box on a stock-room shelf before its contents become usable kitchen stock.\nCheck available storage space before ordering.",
            "Assets/_Project/Restaurant/RestockRoom/RestockOrderManager.cs: CollectDeliveredOrders, TryStoreOneContainer\nAssets/_Project/Restaurant/RestockRoom/RestockTruckInteractable.cs",
            prefabPath: "Assets/_Project/Resources/RestockFlow/RestockDeliveryTruck.prefab", kind: AlmanacPreviewKind.Model,
            related: new[] { "management-computer", "storage-containers", "storage-dry", "storage-frozen" });
        Entry("storage-containers", AlmanacCategory.CleaningStorage, "Ingredient Boxes", "Stock & freshness",
            "Labeled ingredient boxes connect deliveries to the restaurant's usable inventory.",
            "A box's label shows its ingredient and remaining quantity.\nInspect stock condition and expiry before keeping or discarding it.\nThe wrong storage environment accelerates spoilage.\nAn emptied box can be removed as its stock is used.",
            "Assets/_Project/Restaurant/RestockRoom/RestockStorageContainer.cs\nAssets/_Project/Restaurant/RestockRoom/ThrowKeepPanel.cs\nAssets/_Project/Office/Inventory/ItemData.cs",
            prefabPath: "Assets/_Project/Restaurant/RestockRoom/Prefabs/CardboardBox.prefab", kind: AlmanacPreviewKind.Model,
            related: new[] { "storage-delivery", "storage-dry", "storage-frozen" });
    }

    private static void PopulateManagementObjects()
    {
        Entry("management-computer", AlmanacCategory.Management, "Management Computer", "Restaurant planning",
            "The restaurant computer brings everyday management into one place.",
            "Review the dashboard, staff, menu, restock, equipment, finances and objectives.\nUse Staff to manage the restaurant team.\nOrder ingredients and plan equipment purchases before service.\nCheck the opening checklist when preparing the next day.",
            "Assets/_Project/ManagementComputer/ManagementComputerController.cs\nAssets/_Project/ManagementComputer/ManagementComputerStation.cs",
            iconPath: "Assets/_Project/Art/Icons/ComputerIcons/DashboardIcon.png",
            prefabPath: "Assets/_Project/Restaurant/Management/Computer Final 1.prefab", kind: AlmanacPreviewKind.Model,
            related: new[] { "management-menu", "storage-delivery", "management-progression" });
        Entry("management-menu", AlmanacCategory.Management, "Menu Planning", "Recipes & prices",
            "The menu controls which dishes and drinks the restaurant offers its customers.",
            "Choose from the current restaurant's unlocked recipes.\nEnable or disable products and adjust their selling prices through the menu app.\nStock the ingredients needed by enabled recipes.\nFast Food also offers configured food bundles.",
            "Assets/_Project/Office/Inventory/MenuCatalog.cs\nAssets/_Project/Office/Inventory/MenuAvailabilityManager.cs\nAssets/_Project/ManagementComputer/ManagementComputerController.cs",
            iconPath: "Assets/_Project/Art/Icons/ComputerIcons/MenuIcon.png",
            related: new[] { "restaurant-casual-dining", "restaurant-fast-food", "storage-delivery", "management-computer" });
        Entry("management-progression", AlmanacCategory.Management, "Equipment & Unlocks", "Growing the restaurant",
            "As service days progress, new menu items and equipment become available.",
            "Recipe and equipment availability is tied to the restaurant's progression day.\nBuy eligible equipment from Computer > Equipment between service sessions.\nSeating upgrades open capacity; service upgrades improve specific workflows.\nEquipment purchases and staff hiring are separate decisions.",
            "Assets/_Project/Office/Inventory/Recipe.cs: IsUnlocked\nAssets/_Project/Office/Manager/EquipmentManager.cs: UnlockByDay, Purchase\nAssets/_Project/Resources/FastFood/Progression.asset",
            iconPath: "Assets/_Project/Art/Icons/ComputerIcons/EquipmentIcon.png",
            related: new[] { "equipment-seating", "service-kiosk", "equipment-waiter-trolley", "management-computer" });
    }
}
#endif
