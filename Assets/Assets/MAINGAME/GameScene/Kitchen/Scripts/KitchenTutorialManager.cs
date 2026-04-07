using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the KitchenTutorial scene through a 4-day tutorial flow.
///
/// Day 1 — Prep Cook  : guided fetch + breading + burger, then 4-order free play.
/// Day 2 — Line Cook  : guided grill/fry cycle, then 4-order free play (prep auto-places).
/// Day 3 — Assembler  : guided assembly + drink + delivery, then 4-order free play.
/// Day 4 — All Together: unguided full free-play with all three roles.
/// </summary>
public class KitchenTutorialManager : MonoBehaviour
{
    public static KitchenTutorialManager Instance { get; private set; }

    // ─── Phase enum ───────────────────────────────────────────────────────────

    public enum KitchenTutorialPhase
    {
        None,

        // ── Global intro ─────────────────────────────────
        Intro_Welcome,
        Intro_Roles,
        Intro_RoleSwitcher,
        Intro_Tickets,
        Tour_InventoryRoom,
        Tour_CookingArea,

        // ── Day 1 : Prep Cook ─────────────────────────────
        Day1_Intro,
        Day1_OpenRestaurant,
        Day1_FirstOrder,
        Day1_GrabFries,
        Day1_PlaceFriesOnIsland,
        Day1_LineCookTakesFries,
        Day1_ChickenOrder,
        Day1_GrabChicken,
        Day1_BreadChicken,
        Day1_PlaceBreaderResult,
        Day1_LineCookTakesChicken,
        Day1_BurgerOrder,
        Day1_GrabBuns,
        Day1_GrabMeat,
        Day1_GrabCheese,
        Day1_LineCookTakesMeat,
        Day1_FreePlay,

        // ── Day 2 : Line Cook ─────────────────────────────
        Day2_Intro,
        Day2_GrillMeat,
        Day2_FryFries,
        Day2_FryChicken,
        Day2_GrillBurger,
        Day2_PlaceOnIsland,
        Day2_FreePlay,

        // ── Day 3 : Assembler ─────────────────────────────
        Day3_Intro,
        // Round 1 — Fries & Coke
        Day3_AssembleFood,
        Day3_DeliverFood,
        Day3_GrabCup,
        Day3_UseDrinkDispenser,
        Day3_DeliverDrink,
        // Round 2 — Chicken & Iced Tea
        Day3_Chicken_Intro,
        Day3_AssembleFoodChicken,
        Day3_DeliverFoodChicken,
        Day3_GrabCupChicken,
        Day3_UseDispenserChicken,
        Day3_DeliverDrinkChicken,
        // Round 3 — Burger & Pineapple
        Day3_Burger_Intro,
        Day3_AssembleFoodBurger,
        Day3_DeliverFoodBurger,
        Day3_GrabCupBurger,
        Day3_UseDispenserBurger,
        Day3_DeliverDrinkBurger,
        Day3_FreePlay,

        // ── Day 4 : All Together ──────────────────────────
        Day4_AllTogether,

        Complete
    }

    // ─── Day tracking ─────────────────────────────────────────────────────────

    public enum KitchenTutorialDay { Day1 = 1, Day2 = 2, Day3 = 3, Day4 = 4 }

    private const string SavedDayKey   = "DineIn_KitchenTutorial_Day";
    private const string TransitionKey  = "DineIn_KitchenTutorial_Transition";

    // ─── Inspector references ─────────────────────────────────────────────────

    [Header("Tutorial Helpers")]
    [SerializeField] private TutorialDialogueUI dialogueUI;
    [SerializeField] private KitchenTutorialArrowDriver arrowDriver;

    [Header("Role Switcher UI (left panel button)")]
    [SerializeField] private RectTransform roleSwitcherRect;
    [SerializeField] private RectTransform ticketContainerRect;

    [Header("Kitchen Zones (for tour arrows)")]
    [SerializeField] private Transform inventoryRoomAnchor;
    [SerializeField] private Transform cookingAreaAnchor;
    [Header("Shelves")]
    [SerializeField] private Shelf friesShelf;
    [SerializeField] private Shelf chickenShelf;
    [SerializeField] private Shelf bunsShelf;
    [SerializeField] private Shelf meatShelf;
    [SerializeField] private Shelf cheeseShelf;

    [Header("Stations")]
    [SerializeField] private Grill breaderStation;
    [SerializeField] private Grill grillStation;
    [SerializeField] private Grill fryerStation;
    [SerializeField] private PlateSpawner assemblyStation;
    [SerializeField] private DeliveryCounter deliveryCounter;
    [SerializeField] private CupSpawner cupSpawner;
    [SerializeField] private DrinkDispenser drinkDispenser;

    [Header("Island Counter (all slots)")]
    [SerializeField] private Counter[] islandCounters;

    // Convenience: first slot used for auto-place NPC simulation
    private Counter islandCounter => (islandCounters != null && islandCounters.Length > 0) ? islandCounters[0] : null;

    [Header("Roles")]
    [SerializeField] private KitchenPlayerMovement prepCook;
    [SerializeField] private KitchenPlayerMovement lineCook;
    [SerializeField] private KitchenPlayerMovement assembler;
    [SerializeField] private KitchenRoleManager kitchenRoleManager;

    [Header("Timer")]
    [SerializeField] private GameObject shiftTimerObject;

    [Header("Completion")]
    [SerializeField] private GameObject completionPanel;
    [SerializeField] private Button nextDayButton;
    [SerializeField] private Button finishButton;
    [SerializeField] private string nextSceneName = "KitchenScene";

    [Header("Dialogue Speaker Name")]
    [SerializeField] private string speakerName = "Manager";

    // ── Dialogue messages ─────────────────────────────────────────────────────

    [Header("Intro Messages")]
    [SerializeField][TextArea(2, 5)]
    private string msg_Welcome = "Welcome! This is the Kitchen — where every order gets prepared before it heads out to the lobby.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Roles = "In the kitchen there are three main roles: the Prep Cook, the Line Cook, and the Assembler. Each one has a specific job that keeps the kitchen running.";

    [SerializeField][TextArea(2, 5)]
    private string msg_RoleSwitcher = "You can switch between roles using this panel on the left side of the screen. Only one role is active at a time.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Tickets = "Orders come in through the ticket board at the top of the screen. Your goal is to complete every order with as few mistakes as possible.";

    [SerializeField][TextArea(2, 5)]
    private string msg_TourInventory = "Here we have the Inventory Room — this is where our ingredients are stored. The Prep Cook comes here to collect everything the kitchen needs.";

    [SerializeField][TextArea(2, 5)]
    private string msg_TourCooking = "And this is the Cooking and Preparation Area — where all the orders get prepared: grilling, frying, breading, and assembling.";

    [Header("Day 1 — Prep Cook")]
    [SerializeField][TextArea(2, 5)]
    private string msg_Day1_Intro = "Let's begin with the Prep Cook. The Prep Cook prepares the ingredients for the Line Cook.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day1_OpenRestaurant = "OK, let's open the restaurant!";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day1_FirstOrder = "Look — our first order came up! We need to make Fries.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day1_GrabFries = "Head to the Inventory Room and grab a bag of fries from the shelf.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day1_PlaceFriesOnIsland = "Great! Now place them on the Island Counter so the Line Cook can take them.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day1_LineCookTakesFries = "The Line Cook picks them up and gets to frying. Nice work!";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day1_ChickenOrder = "Oh look — another order came in! A customer wants Chicken.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day1_GrabChicken = "Go ahead and grab the raw chicken from the Inventory Room.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day1_BreadChicken = "The Prep Cook also helps with preparation! Bring the chicken to the Breader Station to bread it before it gets fried.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day1_PlaceBreaderResult = "Perfect breading! Now place it on the Island Counter for the Line Cook.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day1_LineCookTakesChicken = "The Line Cook grabs it and heads to the fryer. Great teamwork!";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day1_BurgerOrder = "Oh look — another order! A Burger needs three ingredients: Buns, Raw Meat, and Cheese.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day1_GrabBuns = "Take the Buns from the shelf and place them on the Island Counter.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day1_GrabMeat = "Now get the Raw Meat and place it on the Island Counter.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day1_GrabCheese = "And finally the Cheese — place it on the Island Counter too.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day1_LineCookTakesMeat = "The Line Cook takes the meat to the grill. Buns and cheese wait for assembly.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day1_FreePlay = "Good job — you now understand the Prep Cook! Now try it yourself. Prepare 4 orders to complete Day 1. No more pointers — you've got this!";

    [Header("Day 2 — Line Cook")]
    [SerializeField][TextArea(2, 5)]
    private string msg_Day2_Intro = "Day 2 — the Line Cook! The Line Cook takes ingredients from the island counter and cooks them at the stations.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day2_Grill = "This is the Grill — it's for raw meat patties. Place the patty on the grill and pick it up once cooked. Watch out for burning!";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day2_Fry = "This is the Fryer — for fries and breaded chicken. Place the ingredient in and pick it up when done.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day2_GrabFromIsland = "The Prep Cook placed an ingredient on the Island Counter. Go pick it up!";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day2_CookIt = "Now cook it at the station!";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day2_PlaceBackOnIsland = "Done! Place the cooked item back on the Island Counter.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day2_PlaceCooked = "Once cooked, place the item back on the Island Counter so the Assembler can grab it for plating.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day2_ChickenIntro = "Good work! Next up — breaded chicken goes in the Fryer too. The Prep Cook has placed some on the island.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day2_GrabChicken = "Pick up the breaded chicken from the Island Counter.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day2_FryChicken = "Now fry it at the Fryer station!";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day2_PlaceChicken = "Fried! Place it back on the Island Counter.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day2_MeatIntro = "Last one — raw meat goes on the Grill. The Prep Cook has placed the patty on the island.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day2_GrabMeat = "Pick up the raw meat from the Island Counter.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day2_GrillMeat = "Now grill it at the Grill station!";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day2_PlaceMeat = "Grilled! Place the patty back on the Island Counter.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day2_FreePlay = "Now cook 4 orders on your own. The Prep Cook will automatically deliver ingredients to the counter for you. Go!";

    [Header("Day 3 — Assembler")]
    [Tooltip("Cooked Fries prefab placed on island for the Round 1 demo.")]
    [SerializeField] private GameObject day3CookedFriesPrefab;
    [Tooltip("Fried Chicken prefab placed on island for the Round 2 demo.")]
    [SerializeField] private GameObject day3CookedChickenPrefab;
    [Tooltip("Cooked Meat prefab placed on island for the Round 3 burger demo.")]
    [SerializeField] private GameObject day3CookedMeatPrefab;

    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_Intro = "Day 3 — the Assembler! The Assembler completes every order — food and drinks — and sends them out to the lobby.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_Assemble = "Pick up the cooked food from the Island Counter and bring it to the Assembly Station to plate it.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_Deliver = "Plated! Bring it to the Delivery Window to send it out.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_GrabCup = "Now for the drink. Head to the Cup Spawner and grab an empty cup.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_Dispenser = "Bring the cup to the Drink Dispenser and tap it to fill the drink from the order.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_DeliverDrink = "Filled! Bring the drink to the Delivery Window to complete the order.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_FreePlay = "You're a natural! Complete 4 full orders on your own. Prep and Line Cook will handle their parts automatically.";

    [Header("Day 3 — Round 2 (Chicken & Iced Tea)")]
    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_Chicken_Intro = "Great job! Now a Chicken & Iced Tea order came in. Same process — the cooked chicken is on the Island Counter.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_AssembleChicken = "Pick up the fried chicken from the Island Counter and bring it to the Assembly Station.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_DeliverChicken = "Plated! Bring it to the Delivery Window.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_GrabCupChicken = "Now grab a cup for the Iced Tea.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_DispenserChicken = "Fill it at the Drink Dispenser.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_DeliverDrinkChicken = "Deliver the drink to the Delivery Window to complete the order.";

    [Header("Day 3 — Round 3 (Burger & Pineapple)")]
    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_Burger_Intro = "Almost there! One more — a Burger & Pineapple Juice order. The cooked meat is waiting on the Island Counter.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_AssembleBurger = "Pick up the cooked meat from the Island Counter and bring it to the Assembly Station.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_DeliverBurger = "Plated! Bring the burger to the Delivery Window.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_GrabCupBurger = "Grab a cup for the Pineapple Juice.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_DispenserBurger = "Fill it at the Drink Dispenser.";

    [SerializeField][TextArea(2, 5)]
    private string msg_Day3_DeliverDrinkBurger = "Deliver the drink to complete the order. You've done all three!";


    [Header("Day 4 — All Together")]
    [SerializeField][TextArea(2, 5)]
    private string msg_Day4_Intro = "Day 4 — All Together! You've learned every role. Now run the whole kitchen yourself. Switch between roles and keep those orders moving. Good luck!";

    // ─── Runtime state ────────────────────────────────────────────────────────

    [Header("Runtime (read-only)")]
    [SerializeField] private KitchenTutorialDay currentDay = KitchenTutorialDay.Day1;
    [SerializeField] private KitchenTutorialPhase currentPhase = KitchenTutorialPhase.None;

    private int freePlayOrdersCompleted;
    private const int FreePlayTarget = 4;

    // ─── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (completionPanel != null) completionPanel.SetActive(false);

        if (nextDayButton != null)
            nextDayButton.onClick.AddListener(OnNextDayPressed);

        if (finishButton != null)
            finishButton.onClick.AddListener(OnFinishPressed);

        bool isDay4 = (currentDay == KitchenTutorialDay.Day4);
        if (shiftTimerObject != null)
            shiftTimerObject.SetActive(isDay4);

        if (OrderManagerKitchen.Instance != null)
        {
            OrderManagerKitchen.Instance.isShiftActive = isDay4;
            if (isDay4) OrderManagerKitchen.Instance.currentShiftTime = OrderManagerKitchen.Instance.shiftDuration;
        }

        LoadSavedDay();
        BeginCurrentDay();
    }

    // ─── Day entry point ──────────────────────────────────────────────────────

    private void BeginCurrentDay()
    {
        switch (currentDay)
        {
            case KitchenTutorialDay.Day1: StartCoroutine(RunIntroSequence());  break;
            case KitchenTutorialDay.Day2: StartCoroutine(RunDay2Sequence());   break;
            case KitchenTutorialDay.Day3: StartCoroutine(RunDay3Sequence());   break;
            case KitchenTutorialDay.Day4: StartCoroutine(RunDay4Sequence());   break;
        }
    }

    // ─── Global Intro + Tour ──────────────────────────────────────────────────

    private IEnumerator RunIntroSequence()
    {
        yield return Narrate(KitchenTutorialPhase.Intro_Welcome, msg_Welcome);

        arrowDriver?.PointToWorld(prepCook?.transform);
        yield return Narrate(KitchenTutorialPhase.Intro_Roles, msg_Roles);

        arrowDriver?.PointToUI(roleSwitcherRect);
        yield return Narrate(KitchenTutorialPhase.Intro_RoleSwitcher, msg_RoleSwitcher);

        arrowDriver?.PointToUI(ticketContainerRect);
        yield return Narrate(KitchenTutorialPhase.Intro_Tickets, msg_Tickets);
        arrowDriver?.Hide();

        // Tour inventory room
        arrowDriver?.PointToWorld(inventoryRoomAnchor);
        yield return Narrate(KitchenTutorialPhase.Tour_InventoryRoom, msg_TourInventory);
        arrowDriver?.Hide();

        yield return new WaitForSeconds(0.3f);

        // Tour cooking area
        arrowDriver?.PointToWorld(cookingAreaAnchor);
        yield return Narrate(KitchenTutorialPhase.Tour_CookingArea, msg_TourCooking);
        arrowDriver?.Hide();

        yield return new WaitForSeconds(0.3f);

        yield return RunDay1Sequence();
    }

    // ─── Day 1 : Prep Cook ────────────────────────────────────────────────────

    private IEnumerator RunDay1Sequence()
    {
        kitchenRoleManager?.ForceRole(KitchenRole.PrepCook);

        yield return Narrate(KitchenTutorialPhase.Day1_Intro, msg_Day1_Intro);
        yield return Narrate(KitchenTutorialPhase.Day1_OpenRestaurant, msg_Day1_OpenRestaurant);

        // ── Fries ────────────────────────────────────────
        OrderManagerKitchen.Instance?.InjectOrder(ItemTypeKitchen.Fries, ItemTypeKitchen.Coke);
        yield return Auto(KitchenTutorialPhase.Day1_FirstOrder, msg_Day1_FirstOrder, 3f);

        arrowDriver?.PointToWorld(friesShelf?.transform);
        yield return Instruct(KitchenTutorialPhase.Day1_GrabFries, msg_Day1_GrabFries,
            WaitForPrepCookToHold("fries"));

        arrowDriver?.PointToWorld(islandCounter?.transform);
        yield return Instruct(KitchenTutorialPhase.Day1_PlaceFriesOnIsland, msg_Day1_PlaceFriesOnIsland,
            WaitForAnyIslandCounterToHave("fries"));

        arrowDriver?.Hide();
        yield return Auto(KitchenTutorialPhase.Day1_LineCookTakesFries, msg_Day1_LineCookTakesFries, 3f);
        yield return AutoGrabAndCook(fryerStation, "fries", 0.2f);
        OrderManagerKitchen.Instance?.ForceCompleteTutorialOrder();

        // ── Chicken ──────────────────────────────────────
        OrderManagerKitchen.Instance?.InjectOrder(ItemTypeKitchen.Chicken, ItemTypeKitchen.IcedTea);
        yield return Auto(KitchenTutorialPhase.Day1_ChickenOrder, msg_Day1_ChickenOrder, 3f);

        arrowDriver?.PointToWorld(chickenShelf?.transform);
        yield return Instruct(KitchenTutorialPhase.Day1_GrabChicken, msg_Day1_GrabChicken,
            WaitForPrepCookToHold("chicken"));

        arrowDriver?.PointToWorld(breaderStation?.transform);
        yield return Instruct(KitchenTutorialPhase.Day1_BreadChicken, msg_Day1_BreadChicken,
            WaitForPrepCookToHold("breaded"));

        arrowDriver?.PointToWorld(islandCounter?.transform);
        yield return Instruct(KitchenTutorialPhase.Day1_PlaceBreaderResult, msg_Day1_PlaceBreaderResult,
            WaitForAnyIslandCounterToHave("breaded"));

        arrowDriver?.Hide();
        yield return Auto(KitchenTutorialPhase.Day1_LineCookTakesChicken, msg_Day1_LineCookTakesChicken, 3f);
        yield return AutoGrabAndCook(fryerStation, "breaded", 0.2f);
        OrderManagerKitchen.Instance?.ForceCompleteTutorialOrder();

        // ── Burger ───────────────────────────────────────
        OrderManagerKitchen.Instance?.InjectOrder(ItemTypeKitchen.Burger, ItemTypeKitchen.Coke);
        yield return Auto(KitchenTutorialPhase.Day1_BurgerOrder, msg_Day1_BurgerOrder, 4f);

        arrowDriver?.PointToWorld(bunsShelf?.transform);
        yield return Instruct(KitchenTutorialPhase.Day1_GrabBuns, msg_Day1_GrabBuns,
            WaitForAnyIslandCounterToHave("bun"));

        arrowDriver?.PointToWorld(meatShelf?.transform);
        yield return Instruct(KitchenTutorialPhase.Day1_GrabMeat, msg_Day1_GrabMeat,
            WaitForAnyIslandCounterToHave("meat"));

        arrowDriver?.PointToWorld(cheeseShelf?.transform);
        yield return Instruct(KitchenTutorialPhase.Day1_GrabCheese, msg_Day1_GrabCheese,
            WaitForAnyIslandCounterToHave("cheese"));

        arrowDriver?.Hide();
        yield return Auto(KitchenTutorialPhase.Day1_LineCookTakesMeat, msg_Day1_LineCookTakesMeat, 3f);
        yield return AutoGrabAndCook(grillStation, "meat", 0.2f);
        ClearIslandCounters(); // remove leftover buns and cheese once meat is cooked
        OrderManagerKitchen.Instance?.ForceCompleteTutorialOrder();

        // ── Free play ─────────────────────────────────────
        StartFreePlay(KitchenTutorialPhase.Day1_FreePlay, msg_Day1_FreePlay);
    }

    // ─── Day 2 : Line Cook ────────────────────────────────────────────────────

    private IEnumerator RunDay2Sequence()
    {
        kitchenRoleManager?.ForceRole(KitchenRole.LineCook);

        yield return Narrate(KitchenTutorialPhase.Day2_Intro, msg_Day2_Intro);

        arrowDriver?.PointToWorld(fryerStation?.transform);
        yield return Narrate(KitchenTutorialPhase.Day2_FryFries, msg_Day2_Fry);

        arrowDriver?.PointToWorld(grillStation?.transform);
        yield return Narrate(KitchenTutorialPhase.Day2_GrillMeat, msg_Day2_Grill);

        // ── Demo 1 : Fries ────────────────────────────────
        OrderManagerKitchen.Instance?.InjectOrder(ItemTypeKitchen.Fries, ItemTypeKitchen.Coke);
        yield return new WaitForSeconds(0.5f);
        yield return AutoPlaceOnIsland(friesShelf, islandCounter, 0.5f);

        arrowDriver?.PointToWorld(islandCounter?.transform);
        yield return Instruct(KitchenTutorialPhase.Day2_GrillMeat, msg_Day2_GrabFromIsland,
            WaitForLineCookToHold("fries"));

        arrowDriver?.PointToWorld(fryerStation?.transform);
        yield return Instruct(KitchenTutorialPhase.Day2_FryFries, msg_Day2_CookIt,
            WaitForLineCookToHold("cooked fries"));

        arrowDriver?.PointToWorld(islandCounter?.transform);
        yield return Instruct(KitchenTutorialPhase.Day2_PlaceOnIsland, msg_Day2_PlaceBackOnIsland,
            WaitForAnyIslandCounterToHave("cooked fries"));

        arrowDriver?.Hide();
        yield return new WaitForSeconds(2f);  // assembler "picks it up"
        ClearIslandCounters();
        OrderManagerKitchen.Instance?.ForceCompleteTutorialOrder();

        // ── Demo 2 : Breaded Chicken ──────────────────────
        OrderManagerKitchen.Instance?.InjectOrder(ItemTypeKitchen.Chicken, ItemTypeKitchen.IcedTea);
        yield return Auto(KitchenTutorialPhase.Day2_FryChicken, msg_Day2_ChickenIntro, 3f);
        yield return AutoPlaceOnIsland(chickenShelf, islandCounter, 0.5f);

        // Auto-bread the raw chicken so it becomes "breaded" on the island
        yield return AutoGrabAndBread(islandCounter, breaderStation, 0.3f);

        arrowDriver?.PointToWorld(islandCounter?.transform);
        yield return Instruct(KitchenTutorialPhase.Day2_FryChicken, msg_Day2_GrabChicken,
            WaitForLineCookToHold("breaded"));

        arrowDriver?.PointToWorld(fryerStation?.transform);
        yield return Instruct(KitchenTutorialPhase.Day2_FryChicken, msg_Day2_FryChicken,
            WaitForLineCookToHold("fried chicken"));

        arrowDriver?.PointToWorld(islandCounter?.transform);
        yield return Instruct(KitchenTutorialPhase.Day2_PlaceOnIsland, msg_Day2_PlaceChicken,
            WaitForAnyIslandCounterToHave("fried chicken"));

        arrowDriver?.Hide();
        yield return new WaitForSeconds(2f);  // assembler "picks it up"
        ClearIslandCounters();
        OrderManagerKitchen.Instance?.ForceCompleteTutorialOrder();

        // ── Demo 3 : Burger Meat ──────────────────────────
        OrderManagerKitchen.Instance?.InjectOrder(ItemTypeKitchen.Burger, ItemTypeKitchen.Coke);
        yield return Auto(KitchenTutorialPhase.Day2_GrillMeat, msg_Day2_MeatIntro, 3f);
        yield return AutoPlaceOnIsland(meatShelf, islandCounter, 0.5f);

        arrowDriver?.PointToWorld(islandCounter?.transform);
        yield return Instruct(KitchenTutorialPhase.Day2_GrillMeat, msg_Day2_GrabMeat,
            WaitForLineCookToHold("meat"));

        arrowDriver?.PointToWorld(grillStation?.transform);
        yield return Instruct(KitchenTutorialPhase.Day2_GrillMeat, msg_Day2_GrillMeat,
            WaitForLineCookToHold("cooked meat"));

        arrowDriver?.PointToWorld(islandCounter?.transform);
        yield return Instruct(KitchenTutorialPhase.Day2_PlaceOnIsland, msg_Day2_PlaceMeat,
            WaitForAnyIslandCounterToHave("cooked meat"));

        arrowDriver?.Hide();
        yield return new WaitForSeconds(2f);  // assembler "picks it up"
        ClearIslandCounters();
        OrderManagerKitchen.Instance?.ForceCompleteTutorialOrder();

        // ── Free play ─────────────────────────────────────
        StartFreePlay(KitchenTutorialPhase.Day2_FreePlay, msg_Day2_FreePlay);
    }

    // ─── Day 3 : Assembler ────────────────────────────────────────────────────

    private IEnumerator RunDay3Sequence()
    {
        kitchenRoleManager?.ForceRole(KitchenRole.Assembler);

        yield return Narrate(KitchenTutorialPhase.Day3_Intro, msg_Day3_Intro);

        // ── Round 1 : Fries & Coke ────────────────────────────────────────────
        OrderManagerKitchen.Instance?.InjectOrder(ItemTypeKitchen.Fries, ItemTypeKitchen.Coke);
        yield return new WaitForSeconds(1f);
        yield return AutoPlaceOnIsland(day3CookedFriesPrefab, islandCounter, 0.5f);

        // Wait silently for the player to pick up the fries and plate them at the assembly station.
        // The arrow points to the island counter so the player knows where to look.
        arrowDriver?.PointToWorld(islandCounter?.transform);
        SetPhase(KitchenTutorialPhase.Day3_AssembleFood);
        yield return WaitForCounterToHave(assemblyStation as Counter, "");

        // Food is now plated — guide to serving counter.
        arrowDriver?.PointToWorld(deliveryCounter?.transform);
        yield return Instruct(KitchenTutorialPhase.Day3_DeliverFood, msg_Day3_Deliver,
            WaitForDeliveryCounterOrOrderComplete(ItemTypeKitchen.Fries));

        // Food delivered — guide to cup spawner.
        arrowDriver?.PointToWorld(cupSpawner?.transform);
        yield return Instruct(KitchenTutorialPhase.Day3_GrabCup, msg_Day3_GrabCup,
            WaitForAssemblerToHold("cup"));

        // Cup in hand — guide to drink dispenser.
        arrowDriver?.PointToWorld(drinkDispenser?.transform);
        yield return Instruct(KitchenTutorialPhase.Day3_UseDrinkDispenser, msg_Day3_Dispenser,
            WaitForAssemblerToHold("coke", "pineapple", "iced", "juice"));

        // Drink filled — guide to delivery window.
        arrowDriver?.PointToWorld(deliveryCounter?.transform);
        yield return Instruct(KitchenTutorialPhase.Day3_DeliverDrink, msg_Day3_DeliverDrink,
            WaitForAssemblerToDeliver("coke", "pineapple", "iced", "juice"));

        OrderManagerKitchen.Instance?.ForceCompleteTutorialOrder();
        yield return new WaitForSeconds(0.5f);

        // ── Round 2 : Chicken & Iced Tea ──────────────────────────────────────
        OrderManagerKitchen.Instance?.InjectOrder(ItemTypeKitchen.Chicken, ItemTypeKitchen.IcedTea);
        yield return new WaitForSeconds(1f);
        yield return AutoPlaceOnIsland(day3CookedChickenPrefab, islandCounter, 0.5f);

        yield return Narrate(KitchenTutorialPhase.Day3_Chicken_Intro, msg_Day3_Chicken_Intro);

        arrowDriver?.PointToWorld(islandCounter?.transform);
        SetPhase(KitchenTutorialPhase.Day3_AssembleFoodChicken);
        yield return WaitForCounterToHave(assemblyStation as Counter, "");

        arrowDriver?.PointToWorld(deliveryCounter?.transform);
        yield return Instruct(KitchenTutorialPhase.Day3_DeliverFoodChicken, msg_Day3_DeliverChicken,
            WaitForDeliveryCounterOrOrderComplete(ItemTypeKitchen.Chicken));

        arrowDriver?.PointToWorld(cupSpawner?.transform);
        yield return Instruct(KitchenTutorialPhase.Day3_GrabCupChicken, msg_Day3_GrabCupChicken,
            WaitForAssemblerToHold("cup"));

        arrowDriver?.PointToWorld(drinkDispenser?.transform);
        yield return Instruct(KitchenTutorialPhase.Day3_UseDispenserChicken, msg_Day3_DispenserChicken,
            WaitForAssemblerToHold("coke", "pineapple", "iced", "juice"));

        arrowDriver?.PointToWorld(deliveryCounter?.transform);
        yield return Instruct(KitchenTutorialPhase.Day3_DeliverDrinkChicken, msg_Day3_DeliverDrinkChicken,
            WaitForAssemblerToDeliver("coke", "pineapple", "iced", "juice"));

        OrderManagerKitchen.Instance?.ForceCompleteTutorialOrder();
        yield return new WaitForSeconds(0.5f);

        // ── Round 3 : Burger & Pineapple ──────────────────────────────────────
        OrderManagerKitchen.Instance?.InjectOrder(ItemTypeKitchen.Burger, ItemTypeKitchen.Pineapple);
        yield return new WaitForSeconds(1f);
        yield return AutoPlaceOnIsland(day3CookedMeatPrefab, islandCounter, 0.5f);

        yield return Narrate(KitchenTutorialPhase.Day3_Burger_Intro, msg_Day3_Burger_Intro);

        arrowDriver?.PointToWorld(islandCounter?.transform);
        SetPhase(KitchenTutorialPhase.Day3_AssembleFoodBurger);
        yield return WaitForCounterToHave(assemblyStation as Counter, "");

        arrowDriver?.PointToWorld(deliveryCounter?.transform);
        yield return Instruct(KitchenTutorialPhase.Day3_DeliverFoodBurger, msg_Day3_DeliverBurger,
            WaitForDeliveryCounterOrOrderComplete(ItemTypeKitchen.Burger));

        arrowDriver?.PointToWorld(cupSpawner?.transform);
        yield return Instruct(KitchenTutorialPhase.Day3_GrabCupBurger, msg_Day3_GrabCupBurger,
            WaitForAssemblerToHold("cup"));

        arrowDriver?.PointToWorld(drinkDispenser?.transform);
        yield return Instruct(KitchenTutorialPhase.Day3_UseDispenserBurger, msg_Day3_DispenserBurger,
            WaitForAssemblerToHold("coke", "pineapple", "iced", "juice"));

        arrowDriver?.PointToWorld(deliveryCounter?.transform);
        yield return Instruct(KitchenTutorialPhase.Day3_DeliverDrinkBurger, msg_Day3_DeliverDrinkBurger,
            WaitForAssemblerToDeliver("coke", "pineapple", "iced", "juice"));

        OrderManagerKitchen.Instance?.ForceCompleteTutorialOrder();
        yield return new WaitForSeconds(0.5f);

        arrowDriver?.Hide();
        StartFreePlay(KitchenTutorialPhase.Day3_FreePlay, msg_Day3_FreePlay);
    }

    // ─── Day 4 : All Together ─────────────────────────────────────────────────

    private IEnumerator RunDay4Sequence()
    {
        SetPhase(KitchenTutorialPhase.Day4_AllTogether);
        yield return Narrate(KitchenTutorialPhase.Day4_AllTogether, msg_Day4_Intro);
        arrowDriver?.Hide();
        kitchenRoleManager?.UnlockAllRoles();

        if (OrderManagerKitchen.Instance != null)
        {
            OrderManagerKitchen.Instance.isShiftActive = true;
            OrderManagerKitchen.Instance.currentShiftTime = OrderManagerKitchen.Instance.shiftDuration;
        }
    }

    // ─── Free play ────────────────────────────────────────────────────────────

    /// <summary>
    /// Maps each food type to the Shelf the prep cook would grab from
    /// and whether the ingredient needs breading before the line cook can cook it.
    /// </summary>
    private struct LineCookIngredient
    {
        public Shelf  shelf;
        public bool   needsBreading;
        public string cookedKeyword;   // keyword to detect that the item is cooked in hand
        public Grill  station;         // which cooking station to point to

        public LineCookIngredient(Shelf shelf, bool needsBreading, string cookedKeyword, Grill station)
        {
            this.shelf         = shelf;
            this.needsBreading = needsBreading;
            this.cookedKeyword = cookedKeyword;
            this.station       = station;
        }
    }

    private void StartFreePlay(KitchenTutorialPhase phase, string message)
    {
        SetPhase(phase);
        freePlayOrdersCompleted = 0;
        arrowDriver?.Hide();
        kitchenRoleManager?.UnlockAllRoles();

        dialogueUI?.ShowAuto(speakerName, message, 6f);

        if (currentDay == KitchenTutorialDay.Day2)
        {
            StartCoroutine(WatchLineCookFreePlay());
        }
        else if (currentDay == KitchenTutorialDay.Day3)
        {
            // Day 3 assembler free play: inject all orders upfront then watch for
            // the full plate→deliver food → fill cup → deliver drink cycle.
            for (int i = 0; i < FreePlayTarget; i++)
                OrderManagerKitchen.Instance?.InjectRandomOrder();
            StartCoroutine(WatchAssemblerFreePlay());
        }
        else
        {
            // Day 1 — show all 4 tickets upfront and watch the island.
            for (int i = 0; i < FreePlayTarget; i++)
                OrderManagerKitchen.Instance?.InjectRandomOrder();
            StartCoroutine(WatchIslandForFreePlay());
        }
    }

    /// <summary>
    /// Maps each food ItemTypeKitchen to the set of ingredient name keywords that the
    /// prep cook must place on the island counter before the order is considered ready.
    /// All keywords must be present simultaneously (case-insensitive contains check).
    /// </summary>
    private static readonly Dictionary<ItemTypeKitchen, string[]> PrepIngredientKeywords =
        new Dictionary<ItemTypeKitchen, string[]>
        {
            { ItemTypeKitchen.Burger,  new[] { "bun", "meat", "cheese" } },
            { ItemTypeKitchen.Chicken, new[] { "breaded" } },
            { ItemTypeKitchen.Fries,   new[] { "fries" } },
        };

    /// <summary>
    /// Day 2 free-play loop. All 4 tickets are shown upfront. Then, one at a time:
    ///   1. Auto-place the raw ingredient for the oldest active ticket on the island.
    ///   2. (Chicken only) Auto-bread it so the line cook picks up breaded chicken.
    ///   3. Wait for ANY cooked item to appear on the island (player cooks and places it back).
    ///   4. Wait 2 s, clear island, force-complete ticket. Move to next order.
    /// </summary>
    private IEnumerator WatchLineCookFreePlay()
    {
        // Inject all 4 orders upfront so the board is full from the start.
        // Record the food type of each ticket in injection order.
        var foodQueue = new List<ItemTypeKitchen>();
        for (int i = 0; i < FreePlayTarget; i++)
        {
            OrderManagerKitchen.Instance?.InjectRandomOrder();
            yield return null; // one frame so activeOrders is updated before reading
            foodQueue.Add(GetNewestTutorialOrderFood());
        }

        // Process each order sequentially.
        foreach (ItemTypeKitchen food in foodQueue)
        {
            LineCookIngredient info = GetLineCookIngredient(food);

            // Auto-place the raw ingredient so the player knows what to cook.
            yield return AutoPlaceOnIsland(info.shelf, islandCounter, 0.4f);

            // Chicken needs breading first.
            if (info.needsBreading)
                yield return AutoGrabAndBread(islandCounter, breaderStation, 0.3f);

            // Wait for player to cook it and place the result back on any island slot.
            string cooked = info.cookedKeyword;
            yield return new WaitUntil(() =>
            {
                if (islandCounters == null) return false;
                foreach (Counter c in islandCounters)
                {
                    if (c?.currentItem == null) continue;
                    if (c.currentItem.name.ToLower().Contains(cooked)) return true;
                }
                return false;
            });

            // 2 s pause — simulates the assembler collecting the cooked item.
            yield return new WaitForSeconds(2f);
            ClearIslandCounters();
            OrderManagerKitchen.Instance?.ForceCompleteTutorialOrder();
            freePlayOrdersCompleted++;
        }

        OnFreePlayComplete();
    }

    /// <summary>Returns the food type on the most-recently-injected tutorial (noTimer) ticket.</summary>
    private ItemTypeKitchen GetNewestTutorialOrderFood()
    {
        if (OrderManagerKitchen.Instance == null) return ItemTypeKitchen.Fries;
        var orders = OrderManagerKitchen.Instance.activeOrders;
        for (int i = orders.Count - 1; i >= 0; i--)
        {
            if (!orders[i].noTimer) continue;
            foreach (var item in orders[i].missingItems)
            {
                if (item == ItemTypeKitchen.Burger  ||
                    item == ItemTypeKitchen.Chicken ||
                    item == ItemTypeKitchen.Fries)
                    return item;
            }
        }
        return ItemTypeKitchen.Fries;
    }

    /// <summary>
    /// Returns the shelf, breading flag, cooked-result keyword, and cooking station
    /// for a given food type, so WatchLineCookFreePlay can set up each order correctly.
    /// Keywords match the exact cooked prefab names: "Cooked Meat", "Fried Chicken", "Cooked Fries".
    /// </summary>
    private LineCookIngredient GetLineCookIngredient(ItemTypeKitchen food)
    {
        switch (food)
        {
            case ItemTypeKitchen.Burger:
                return new LineCookIngredient(meatShelf,    false, "cooked meat",   grillStation);
            case ItemTypeKitchen.Chicken:
                return new LineCookIngredient(chickenShelf, true,  "fried chicken", fryerStation);
            case ItemTypeKitchen.Fries:
            default:
                return new LineCookIngredient(friesShelf,   false, "cooked fries",  fryerStation);
        }
    }

    /// <summary>
    /// Returns true when the island counters collectively contain at least one item
    /// whose name contains every keyword in <paramref name="keywords"/>.
    /// </summary>
    private bool IslandHasIngredients(string[] keywords)
    {
        if (islandCounters == null || keywords == null) return false;

        foreach (string keyword in keywords)
        {
            bool found = false;
            foreach (Counter c in islandCounters)
            {
                if (c != null && c.currentItem != null &&
                    c.currentItem.name.ToLower().Contains(keyword.ToLower()))
                {
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }
        return true;
    }

    /// <summary>
    /// Resolves the food type of the current oldest tutorial order so the watcher knows
    /// which prep ingredients to wait for on the island counter.
    /// Returns null when no tutorial orders are active.
    /// </summary>
    private string[] GetRequiredPrepKeywordsForCurrentOrder()
    {
        if (OrderManagerKitchen.Instance == null) return null;

        foreach (var ticket in OrderManagerKitchen.Instance.activeOrders)
        {
            if (!ticket.noTimer) continue;

            foreach (var item in ticket.missingItems)
            {
                if (PrepIngredientKeywords.TryGetValue(item, out string[] keywords))
                    return keywords;
            }

            // Drink-only ticket — any placement completes it.
            return new[] { "" };
        }
        return null;
    }


    /// <summary>
    /// Waits until the assembler no longer holds the given food item, meaning they
    /// placed it at the delivery counter. More reliable than checking counter.currentItem
    /// because TryDeliver processes and clears the counter in the same frame the item arrives.
    /// Waits first for the assembler TO hold the item, then for them to release it.
    /// </summary>
    private IEnumerator WaitForAssemblerToDeliver(params string[] itemNameTerms)
    {
        // Phase 1 — wait for the assembler to pick up the food.
        yield return new WaitUntil(() =>
        {
            if (assembler == null) return false;
            PlayerHolding hands = assembler.GetComponent<PlayerHolding>();
            if (hands == null || hands.heldObject == null) return false;
            string n = hands.heldObject.name.ToLower();
            foreach (string term in itemNameTerms)
                if (n.Contains(term.ToLower())) return true;
            return false;
        });

        // Phase 2 — wait for the assembler to let go of it.
        yield return new WaitUntil(() =>
        {
            if (assembler == null) return true;
            PlayerHolding hands = assembler.GetComponent<PlayerHolding>();
            if (hands == null || hands.heldObject == null) return true;
            string n = hands.heldObject.name.ToLower();
            foreach (string term in itemNameTerms)
                if (n.Contains(term.ToLower())) return false;
            return true;
        });
    }

    /// <summary>
    /// Waits until the active order count drops below <paramref name="targetCount"/>.
    /// Subscribes to <see cref="OrderManagerKitchen.OnOrderCompleted"/> as a reliable
    /// fallback covering the case where TryDeliver removes the ticket in the same frame
    /// the drink lands, before the WaitUntil predicate evaluates.
    /// </summary>
    private IEnumerator WaitForOrderCountToDrop(int targetCount)
    {
        bool orderCompleted = false;
        System.Action onComplete = () => orderCompleted = true;
        OrderManagerKitchen.OnOrderCompleted += onComplete;

        yield return new WaitUntil(() =>
            orderCompleted ||
            OrderManagerKitchen.Instance == null ||
            OrderManagerKitchen.Instance.activeOrders.Count < targetCount);

        OrderManagerKitchen.OnOrderCompleted -= onComplete;
    }

    /// <summary>
    /// Waits until the food item is registered as delivered on the active ticket,
    /// OR until the assembler places the food item down (handles same-frame removal).
    /// </summary>
    private IEnumerator WaitForDeliveryCounterOrOrderComplete(ItemTypeKitchen foodType)
    {
        // Map food type to name terms the assembler's held item would contain.
        string[] terms = FoodTypeToNameTerms(foodType);

        bool delivered = false;
        System.Action onComplete = () => delivered = true;
        OrderManagerKitchen.OnOrderCompleted += onComplete;

        yield return new WaitUntil(() =>
        {
            if (delivered) return true;

            // Check completedItems on any live tutorial ticket.
            if (OrderManagerKitchen.Instance != null)
            {
                foreach (var ticket in OrderManagerKitchen.Instance.activeOrders)
                {
                    if (ticket.noTimer && ticket.completedItems.Contains(foodType))
                        return true;
                }
            }

            // Assembler released the food item — they placed it somewhere.
            if (assembler != null)
            {
                PlayerHolding hands = assembler.GetComponent<PlayerHolding>();
                if (hands == null || hands.heldObject == null) return true; // let go of something
                string n = hands.heldObject.name.ToLower();
                foreach (string term in terms)
                    if (n.Contains(term)) return false; // still holding food → not yet
                return true; // holding something else → released the food
            }

            return false;
        });

        OrderManagerKitchen.OnOrderCompleted -= onComplete;
    }

    private static string[] FoodTypeToNameTerms(ItemTypeKitchen food)
    {
        switch (food)
        {
            case ItemTypeKitchen.Fries:   return new[] { "fries" };
            case ItemTypeKitchen.Chicken: return new[] { "chicken", "fried" };
            case ItemTypeKitchen.Burger:  return new[] { "burger", "meat" };
            default:                      return new[] { food.ToString().ToLower() };
        }
    }



    /// <summary>
    /// Day 3 assembler free-play watcher. For each of the 4 injected orders it:
    ///   1. Resolves the food type from the oldest active ticket.
    ///   2. Auto-places the matching cooked prefab on the island counter.
    ///   3. Waits for the food item to be delivered (plated + sent to delivery window).
    ///   4. Waits for the drink to be delivered.
    ///   5. Force-completes the ticket and moves to the next order.
    /// </summary>
    private IEnumerator WatchAssemblerFreePlay()
    {
        // Gather the food type for each injected order in injection order.
        var foodQueue = new List<ItemTypeKitchen>();
        yield return null; // one frame so activeOrders is populated
        foreach (var ticket in OrderManagerKitchen.Instance.activeOrders)
        {
            foreach (var item in ticket.missingItems)
            {
                if (item == ItemTypeKitchen.Burger ||
                    item == ItemTypeKitchen.Chicken ||
                    item == ItemTypeKitchen.Fries)
                {
                    foodQueue.Add(item);
                    break;
                }
            }
        }

        foreach (ItemTypeKitchen food in foodQueue)
        {
            // Resolve the correct cooked prefab for this food type.
            GameObject cookedPrefab = GetCookedPrefabForFood(food);

            // Auto-place cooked food on the first free island counter slot.
            yield return AutoPlaceOnIsland(cookedPrefab, GetFreeIslandCounter(), 0.5f);

            // Wait for the food component to be delivered.
            yield return new WaitUntil(() =>
            {
                if (OrderManagerKitchen.Instance == null) return true;
                foreach (var ticket in OrderManagerKitchen.Instance.activeOrders)
                {
                    if (ticket.noTimer && ticket.completedItems.Contains(food))
                        return true;
                }
                return false;
            });

            // Wait for the drink to be delivered (order disappears from activeOrders).
            int countBefore = OrderManagerKitchen.Instance?.activeOrders.Count ?? 0;
            yield return new WaitUntil(() =>
                OrderManagerKitchen.Instance == null ||
                OrderManagerKitchen.Instance.activeOrders.Count < countBefore);

            freePlayOrdersCompleted++;
        }

        OnFreePlayComplete();
    }

    /// <summary>Returns the cooked prefab that corresponds to the given food type.</summary>
    private GameObject GetCookedPrefabForFood(ItemTypeKitchen food)
    {
        switch (food)
        {
            case ItemTypeKitchen.Chicken: return day3CookedChickenPrefab;
            case ItemTypeKitchen.Burger:  return day3CookedMeatPrefab;
            default:                      return day3CookedFriesPrefab;
        }
    }

    /// <summary>Returns the first island counter slot that has no current item, or the first slot as fallback.</summary>
    private Counter GetFreeIslandCounter()
    {
        if (islandCounters == null || islandCounters.Length == 0) return null;
        foreach (Counter c in islandCounters)
        {
            if (c != null && c.currentItem == null) return c;
        }
        return islandCounters[0];
    }

    /// <summary>
    /// Drives the 4-order free-play trial for Day 1 / Day 3. Waits for the prep cook to place ALL
    /// required ingredients for the current order on the island counter, then holds 2 seconds to
    /// simulate the rest of the team finishing, clears the island, and force-completes the ticket.
    /// All 4 tickets are shown upfront; no new tickets are injected during play.
    /// </summary>
    private IEnumerator WatchIslandForFreePlay()
    {
        while (freePlayOrdersCompleted < FreePlayTarget)
        {
            string[] required = GetRequiredPrepKeywordsForCurrentOrder();
            if (required == null) { yield return null; continue; }

            // Wait until the island has every required ingredient for this order.
            yield return new WaitUntil(() => IslandHasIngredients(required));

            // Simulate the line cook "picking up" — wait 2 seconds before completing.
            yield return new WaitForSeconds(2f);

            ClearIslandCounters();
            OrderManagerKitchen.Instance?.ForceCompleteTutorialOrder();

            freePlayOrdersCompleted++;
        }

        OnFreePlayComplete();
    }

    private void OnFreePlayComplete()
    {
        dialogueUI?.Hide();
        arrowDriver?.Hide();

        if (currentDay < KitchenTutorialDay.Day4)
            ShowDayComplete();
        else
            ShowTutorialComplete();
    }

    // ─── Day / Tutorial completion ────────────────────────────────────────────

    private void ShowDayComplete()
    {
        SetPhase(KitchenTutorialPhase.Complete);
        if (completionPanel != null) completionPanel.SetActive(true);
        if (nextDayButton   != null) nextDayButton.gameObject.SetActive(true);
        if (finishButton    != null) finishButton.gameObject.SetActive(false);
    }

    private void ShowTutorialComplete()
    {
        SetPhase(KitchenTutorialPhase.Complete);
        if (completionPanel != null) completionPanel.SetActive(true);
        if (nextDayButton   != null) nextDayButton.gameObject.SetActive(false);
        if (finishButton    != null) finishButton.gameObject.SetActive(true);

        PlayerPrefs.SetInt(SavedDayKey, 0);
        PlayerPrefs.Save();
    }

    private void OnNextDayPressed()
    {
        int next = (int)currentDay + 1;
        PlayerPrefs.SetInt(SavedDayKey, next);
        PlayerPrefs.SetInt(TransitionKey, 1);   // signal that the scene reload is intentional
        PlayerPrefs.Save();
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    private void OnFinishPressed()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
    }

    // ─── Dialogue helpers ─────────────────────────────────────────────────────

    private void SetPhase(KitchenTutorialPhase phase) => currentPhase = phase;

    /// <summary>
    /// Shows a tap-to-advance dialogue and waits for the player to press Next.
    /// Use for pure narration — no player action required.
    /// </summary>
    private IEnumerator Narrate(KitchenTutorialPhase phase, string message)
    {
        SetPhase(phase);
        bool advanced = false;
        dialogueUI?.ShowManual(speakerName, message, () => advanced = true);
        yield return new WaitUntil(() => advanced);
    }

    /// <summary>
    /// Shows a persistent instruction dialogue (no Next button) and waits for
    /// the given action coroutine to finish. Dialogue hides automatically on completion.
    /// </summary>
    private IEnumerator Instruct(KitchenTutorialPhase phase, string message, IEnumerator waitCondition)
    {
        SetPhase(phase);
        dialogueUI?.ShowAuto(speakerName, message, 9999f);
        yield return waitCondition;
        dialogueUI?.Hide();
        yield return new WaitForSeconds(0.4f);
    }

    /// <summary>Shows an auto-dismiss dialogue and waits for it to finish.</summary>
    private IEnumerator Auto(KitchenTutorialPhase phase, string message, float duration)
    {
        SetPhase(phase);
        dialogueUI?.ShowAuto(speakerName, message, duration);
        yield return new WaitForSeconds(duration + 0.2f);
    }

    // ─── Wait conditions ──────────────────────────────────────────────────────

    /// <summary>Waits until the prep cook holds an item whose name contains any of the given terms.</summary>
    private IEnumerator WaitForPrepCookToHold(params string[] terms)
    {
        yield return new WaitUntil(() =>
        {
            if (prepCook == null) return false;
            PlayerHolding hands = prepCook.GetComponent<PlayerHolding>();
            if (hands == null || hands.heldObject == null) return false;
            string n = hands.heldObject.name.ToLower();
            foreach (string t in terms)
                if (n.Contains(t.ToLower())) return true;
            return false;
        });
    }

    /// <summary>Waits until the line cook holds an item whose name contains any of the given terms.</summary>
    private IEnumerator WaitForLineCookToHold(params string[] terms)
    {
        yield return new WaitUntil(() =>
        {
            if (lineCook == null) return false;
            PlayerHolding hands = lineCook.GetComponent<PlayerHolding>();
            if (hands == null || hands.heldObject == null) return false;
            string n = hands.heldObject.name.ToLower();
            if (terms.Length == 0) return true;
            foreach (string t in terms)
                if (n.Contains(t.ToLower())) return true;
            return false;
        });
    }

    /// <summary>Waits until the assembler holds an item whose name contains any of the given terms.</summary>
    private IEnumerator WaitForAssemblerToHold(params string[] terms)
    {
        yield return new WaitUntil(() =>
        {
            if (assembler == null) return false;
            PlayerHolding hands = assembler.GetComponent<PlayerHolding>();
            if (hands == null || hands.heldObject == null) return false;
            string n = hands.heldObject.name.ToLower();
            if (terms.Length == 0) return true;
            foreach (string t in terms)
                if (n.Contains(t.ToLower())) return true;
            return false;
        });
    }

    /// <summary>Waits until a specific counter has an item whose name contains term (empty = any item).</summary>
    private IEnumerator WaitForCounterToHave(Counter counter, string term)
    {
        yield return new WaitUntil(() =>
        {
            if (counter == null || counter.currentItem == null) return false;
            if (string.IsNullOrEmpty(term)) return true;
            return counter.currentItem.name.ToLower().Contains(term.ToLower());
        });
    }

    /// <summary>
    /// Waits until ANY of the island counter slots has an item whose name contains term.
    /// Required because the island has 7 separate Counter objects — the player can use any slot.
    /// </summary>
    private IEnumerator WaitForAnyIslandCounterToHave(string term)
    {
        yield return new WaitUntil(() =>
        {
            if (islandCounters == null) return false;
            foreach (Counter c in islandCounters)
            {
                if (c == null || c.currentItem == null) continue;
                if (string.IsNullOrEmpty(term)) return true;
                if (c.currentItem.name.ToLower().Contains(term.ToLower())) return true;
            }
            return false;
        });
    }

    /// <summary>Destroys all items currently sitting on island counter slots.</summary>
    private void ClearIslandCounters()
    {
        if (islandCounters == null) return;
        foreach (Counter c in islandCounters)
        {
            if (c != null && c.currentItem != null)
            {
                Destroy(c.currentItem);
                c.currentItem = null;
            }
        }
    }

    // ─── NPC automation ───────────────────────────────────────────────────────

    /// <summary>
    /// Simulates the Prep Cook auto-breading raw chicken sitting on the island:
    /// moves it to the breader station, waits for it to finish, then places
    /// the breaded result back on the island counter for the Line Cook to pick up.
    /// </summary>
    private IEnumerator AutoGrabAndBread(Counter source, Grill breader, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (breader == null || source == null || source.currentItem == null) yield break;
        if (breader.currentItem != null) yield break;

        GameObject rawItem = source.currentItem;
        source.currentItem = null;

        rawItem.transform.SetParent(breader.itemPlacementPoint);
        rawItem.transform.localPosition = Vector3.zero;
        rawItem.transform.localRotation = Quaternion.identity;
        breader.currentItem = rawItem;

        // Wait for breader to replace raw item with breaded result
        yield return new WaitUntil(() =>
            breader == null ||
            breader.currentItem == null ||
            breader.currentItem != rawItem
        );
        yield return null;

        // Move breaded result back onto the island counter
        if (breader != null && breader.currentItem != null && breader.currentItem != rawItem)
        {
            GameObject breaded = breader.currentItem;
            breader.currentItem = null;

            source.currentItem = breaded;
            breaded.transform.SetParent(source.itemPlacementPoint);
            breaded.transform.localPosition = Vector3.zero;
            breaded.transform.localRotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// Fakes the line cook taking the first island counter slot containing an item matching
    /// itemTerm and moving it to a cooking station. Scans all island slots.
    /// Then auto-clears the station once cooking finishes (item reference is replaced by Grill).
    /// </summary>
    private IEnumerator AutoGrabAndCook(Grill toStation, string itemTerm, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (toStation == null || islandCounters == null) yield break;
        if (toStation.currentItem != null) yield break;

        Counter source = null;
        foreach (Counter c in islandCounters)
        {
            if (c == null || c.currentItem == null) continue;
            if (string.IsNullOrEmpty(itemTerm) ||
                c.currentItem.name.ToLower().Contains(itemTerm.ToLower()))
            {
                source = c;
                break;
            }
        }

        if (source == null) yield break;

        GameObject rawItem = source.currentItem;
        source.currentItem = null;

        rawItem.transform.SetParent(toStation.itemPlacementPoint);
        rawItem.transform.localPosition = Vector3.zero;
        rawItem.transform.localRotation = Quaternion.identity;

        toStation.currentItem = rawItem;

        // Wait for cooking to finish before returning so the caller can
        // immediately call ForceCompleteTutorialOrder afterwards.
        yield return AutoClearStationWhenCooked(toStation, rawItem);
    }

    /// <summary>
    /// Waits until the station's currentItem is no longer the original raw item
    /// (Grill.CookItem destroys it and sets a new cooked GameObject), then
    /// destroys the cooked item immediately — simulating the NPC taking it off.
    /// </summary>
    private IEnumerator AutoClearStationWhenCooked(Grill station, GameObject placedItem)
    {
        // Wait until Grill replaces the raw item with the cooked one.
        // Grill.CookItem() calls Destroy(rawItem) so placedItem becomes null,
        // and station.currentItem becomes a brand-new cooked GameObject.
        yield return new WaitUntil(() =>
            station == null ||
            station.currentItem == null ||
            station.currentItem != placedItem   // reference changed → cooking done
        );

        yield return null; // one frame for Grill to finish its parent/position calls

        if (station != null && station.currentItem != null && station.currentItem != placedItem)
        {
            Destroy(station.currentItem);
            station.currentItem = null;
        }
    }

    /// <summary>
    /// Simulates the prep cook spawning an ingredient from a shelf and placing it on the island counter.
    /// Used in Days 2 &amp; 3 to set up guided steps without requiring player action on another role.
    /// </summary>
    private IEnumerator AutoPlaceOnIsland(Shelf shelf, Counter counter, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (shelf == null || counter == null || shelf.ingredientToSpawn == null) yield break;
        if (counter.currentItem != null) yield break;

        GameObject spawned = Instantiate(shelf.ingredientToSpawn);
        spawned.name = shelf.ingredientToSpawn.name;

        spawned.transform.SetParent(counter.itemPlacementPoint);
        spawned.transform.localPosition = Vector3.zero;
        spawned.transform.localRotation = Quaternion.identity;

        counter.currentItem = spawned;
    }

    /// <summary>
    /// Spawns a specific prefab directly onto the island counter.
    /// Used in Day 3 to place a cooked ingredient without going through a raw shelf.
    /// </summary>
    private IEnumerator AutoPlaceOnIsland(GameObject prefab, Counter counter, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (prefab == null || counter == null) yield break;
        if (counter.currentItem != null) yield break;

        GameObject spawned = Instantiate(prefab);
        spawned.name = prefab.name;

        spawned.transform.SetParent(counter.itemPlacementPoint);
        spawned.transform.localPosition = Vector3.zero;
        spawned.transform.localRotation = Quaternion.identity;

        counter.currentItem = spawned;
    }

    // ─── Debug helpers ────────────────────────────────────────────────────────

#if UNITY_EDITOR
    /// <summary>
    /// Immediately stops all running tutorial coroutines, clears active orders,
    /// and restarts the sequence from Day 3. Wired to the debug skip button.
    /// Editor-only — the button is hidden in production builds.
    /// </summary>
    public void Debug_SkipToDay3()
    {
        StopAllCoroutines();
        ClearIslandCounters();

        if (OrderManagerKitchen.Instance != null)
        {
            OrderManagerKitchen.Instance.activeOrders.Clear();
            OrderManagerKitchen.Instance.isShiftActive = false;
        }

        currentDay = KitchenTutorialDay.Day3;
        StartCoroutine(RunDay3Sequence());
    }
#endif

    // ─── Save / Load ──────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the saved day only when the scene was reloaded via the "Next Day" button
    /// (signalled by the TransitionKey flag). Clears both keys immediately so a fresh
    /// play-mode run always starts at Day 1.
    /// </summary>
    private void LoadSavedDay()
    {
        bool isTransition = PlayerPrefs.GetInt(TransitionKey, 0) == 1;
        int saved = isTransition ? PlayerPrefs.GetInt(SavedDayKey, 1) : 1;

        // Always clear both keys so the next play-mode session starts fresh.
        PlayerPrefs.DeleteKey(TransitionKey);
        PlayerPrefs.DeleteKey(SavedDayKey);
        PlayerPrefs.Save();

        saved = Mathf.Clamp(saved, 1, 4);
        currentDay = (KitchenTutorialDay)saved;
    }
}

