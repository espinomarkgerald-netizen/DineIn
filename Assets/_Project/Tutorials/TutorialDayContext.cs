using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Owns a disposable Tutorial Day runtime. The player's loaded career state is
/// captured, tutorial managers receive fresh in-memory values, and save writes
/// remain suspended until the original state is restored.
/// </summary>
[DefaultExecutionOrder(-9001)]
[DisallowMultipleComponent]
public sealed class TutorialDayContext : MonoBehaviour
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    private readonly List<Object> runtimeClones = new List<Object>();
    private MenuCatalog authoredCatalog;
    private MenuCatalog tutorialCatalog;
    private object authoredCatalogSceneName;
    private List<ItemData> authoredInventoryItems;
    private List<Equipment> authoredEquipment;
    private GameSaveData originalRuntimeState;
    private ObjectiveSnapshot originalObjectives;
    private GameSaveManager saveManager;
    private bool originalAutoLoad;
    private bool originalAutoSaveOnPause;
    private bool originalAutoSaveOnQuit;
    private bool originalApplyingSave;
    private bool originalHasAutoLoaded;
    private bool saveIsolationPrepared;
    private bool careerSaveExisted;
    private bool runtimeIsolated;
    private bool restored;
#if UNITY_EDITOR
    private bool editorExitingPlayMode;
#endif

    private sealed class ObjectiveSnapshot
    {
        public ObjectiveDefinition mandatory;
        public ObjectiveDefinition secondary;
        public ObjectiveDefinition bonus;
        public ObjectiveGrade grade;
        public bool mandatoryPassed;
        public bool secondaryPassed;
        public bool bonusPassed;
        public bool hasPrevious;
        public int resultDay;
        public int totalDaysPassed;
        public int angryDepartures;
        public int currentDay;
    }

    private void Awake()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        PrepareSaveIsolation();
        authoredCatalog = MenuCatalog.Default;
        authoredCatalogSceneName = GetStaticField(typeof(MenuCatalog), "cachedSceneName");
        if (authoredCatalog == null)
        {
            Debug.LogError("[Tutorial Day] Casual Dining catalog is unavailable.", this);
            return;
        }

        tutorialCatalog = BuildRuntimeCatalog(authoredCatalog);
        SetStaticField(typeof(MenuCatalog), "cachedDefault", tutorialCatalog);
        SetStaticField(typeof(MenuCatalog), "cachedSceneName", gameObject.scene.name);
    }

#if UNITY_EDITOR
    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            editorExitingPlayMode = true;
    }
#endif

    /// <summary>Builds one deterministic, fully stocked tutorial order in memory.</summary>
    public bool PrepareCustomerMenu()
    {
        if (!runtimeIsolated || tutorialCatalog == null ||
            MenuAvailabilityManager.Instance == null || InventoryManager.Instance == null)
            return false;

        Recipe food = null;
        Recipe drink = null;
        foreach (Recipe recipe in tutorialCatalog.Products)
        {
            if (recipe == null || !recipe.availableOnMenu) continue;
            MenuAvailabilityManager.Instance.SetProductAvailable(recipe, false);
            if (recipe.category == MenuProductCategory.Food && food == null) food = recipe;
            if (recipe.category == MenuProductCategory.Drink && drink == null) drink = recipe;
        }

        if (food == null) return false;
        MenuAvailabilityManager.Instance.SetProductAvailable(food, true);
        if (drink != null) MenuAvailabilityManager.Instance.SetProductAvailable(drink, true);
        StockRecipe(food);
        StockRecipe(drink);
        Debug.Log($"[Tutorial Day] Tutorial order prepared: {food.DisplayName}" +
                  (drink != null ? $" + {drink.DisplayName}." : "."), this);
        return true;
    }

    private static void StockRecipe(Recipe recipe)
    {
        if (recipe == null || recipe.ingredients == null) return;
        foreach (RecipeIngredient ingredient in recipe.ingredients)
            if (ingredient != null && ingredient.item != null)
                InventoryManager.Instance.AddStock(ingredient.item.itemType,
                    Mathf.Max(ingredient.item.unitsPerBox, Mathf.Max(12, ingredient.amount * 12)));
    }

    private void Start()
    {
        PrepareSaveIsolation();
        originalRuntimeState = CaptureRuntimeState();
        originalObjectives = CaptureObjectives();

        if (InventoryManager.Instance != null && tutorialCatalog != null)
        {
            authoredInventoryItems = InventoryManager.Instance.Items != null
                ? new List<ItemData>(InventoryManager.Instance.Items)
                : new List<ItemData>();
            InventoryManager.Instance.ConfigureItems(new List<ItemData>(tutorialCatalog.Ingredients));
        }

        EquipmentManager equipment = EquipmentManager.Instance;
        if (equipment != null && equipment.AllEquipment != null)
        {
            authoredEquipment = new List<Equipment>(equipment.AllEquipment);
            List<Equipment> tutorialEquipment = new List<Equipment>();
            foreach (Equipment source in authoredEquipment)
            {
                if (source == null) continue;
                Equipment clone = Instantiate(source);
                clone.name = source.name + " (Tutorial Day)";
                clone.hideFlags = HideFlags.DontSave;
                clone.dayToUnlock = 1;
                runtimeClones.Add(clone);
                tutorialEquipment.Add(clone);
            }
            equipment.Configure(tutorialEquipment);
        }

        ApplyRuntimeState(BuildFreshTutorialState());
        DailyObjectiveManager.Instance?.ResetForNewRun();
        runtimeIsolated = true;
        Debug.Log("[Tutorial Day] Fresh isolated session applied: Day 1, P5000, 50% approval, empty stock/orders, fresh staff and menu state.", this);
    }

    private static GameSaveData BuildFreshTutorialState()
    {
        GameSaveData data = new GameSaveData
        {
            currentDay = 1,
            currentPhase = 0,
            currentDayHalf = 0,
            lobbyCompleted = false,
            kitchenCompleted = false,
            campaignCompleted = false,
            money = 5000,
            approval = 50,
            polishPreparedDay = 0,
            polishLastFinalizedDay = 0,
            polishDayStartApproval = 50,
            polishDayStartMoney = 5000,
            restaurantRatingScore = 60,
            employeeApplicantNextRefreshDay = 3,
            employeeApplicantLastProcessedDay = 1,
            employeeApplicantsUnseen = true
        };

        AddTutorialApplicant(data, "tutorial-host", "Nova", EmployeeRole.Host);
        AddTutorialApplicant(data, "tutorial-waiter", "Milo", EmployeeRole.Waiter);
        AddTutorialApplicant(data, "tutorial-cashier", "Iris", EmployeeRole.Cashier);
        AddTutorialApplicant(data, "tutorial-busser", "Pax", EmployeeRole.Busser);
        AddTutorialApplicant(data, "tutorial-chef", "Sora", EmployeeRole.Chef);
        AddTutorialApplicant(data, "tutorial-barista", "Lumi", EmployeeRole.Barista);
        return data;
    }

    private static void AddTutorialApplicant(GameSaveData data, string id, string employeeName, EmployeeRole role)
    {
        data.employees.Add(new EmployeeSaveEntry
        {
            employeeID = id,
            employeeName = employeeName,
            stars = 2,
            role = role,
            assigned = false,
            hired = false,
            applicantAvailableUntilDay = 3,
            speed = 100,
            accuracy = 80,
            reliability = 80,
            performanceMultiplier = 1f,
            recentPerformance = 75,
            previousPerformance = 75
        });
    }

    private void PrepareSaveIsolation()
    {
        if (saveIsolationPrepared) return;
        saveManager = GameSaveManager.Instance ??
            FindFirstObjectByType<GameSaveManager>(FindObjectsInactive.Include);
        if (saveManager == null) return;

        originalAutoLoad = ReadBool(saveManager, "autoLoadOnStart");
        originalAutoSaveOnPause = ReadBool(saveManager, "autoSaveOnPause");
        originalAutoSaveOnQuit = ReadBool(saveManager, "autoSaveOnQuit");
        originalApplyingSave = ReadBool(saveManager, "<IsApplyingSave>k__BackingField");
        originalHasAutoLoaded = ReadBool(saveManager, "hasAutoLoaded");
        careerSaveExisted = saveManager.HasSave();
        SetInstanceField(saveManager, "autoLoadOnStart", false);
        SetInstanceField(saveManager, "autoSaveOnPause", false);
        SetInstanceField(saveManager, "autoSaveOnQuit", false);
        SetInstanceField(saveManager, "<IsApplyingSave>k__BackingField", true);
        saveIsolationPrepared = true;
        Debug.Log("[Tutorial Day] Career auto-load and save writes suspended for this tutorial session.", this);
    }

    public bool CareerSaveExisted => careerSaveExisted;

    /// <summary>Commits only the clean post-tutorial Day 2 start for a brand-new career.</summary>
    public void CommitFirstCareerDayTwo()
    {
        if (careerSaveExisted || restored || saveManager == null) return;
        RestoreAuthoredRuntimeAssets();
        GameSaveData dayTwo = BuildFreshTutorialState();
        dayTwo.currentDay = 2;
        dayTwo.currentPhase = 0;
        dayTwo.currentDayHalf = 0;
        dayTwo.lobbyCompleted = false;
        SetInstanceField(saveManager, "<IsApplyingSave>k__BackingField", false);
        SetInstanceField(saveManager, "hasAutoLoaded", true);
        ApplyRuntimeState(dayTwo);
        saveManager.SaveGame();
        runtimeIsolated = false;
        DestroyRuntimeClones();
        restored = true;
        Debug.Log("[Tutorial Day] Tutorial Day completed; clean career Day 2 saved.", this);
    }

    /// <summary>Restores an existing career before a revisit returns to Game Mode Select.</summary>
    public void RestoreExistingCareerNow()
    {
        if (!careerSaveExisted || restored || saveManager == null) return;
        RestoreAuthoredRuntimeAssets();
        SetInstanceField(saveManager, "<IsApplyingSave>k__BackingField", false);
        SetInstanceField(saveManager, "hasAutoLoaded", true);
        saveManager.LoadGame();
        runtimeIsolated = false;
        DestroyRuntimeClones();
        restored = true;
        Debug.Log("[Tutorial Day] Revisit ended; existing career reloaded unchanged.", this);
    }

    private void RestoreAuthoredRuntimeAssets()
    {
        if (authoredCatalog != null) SetStaticField(typeof(MenuCatalog), "cachedDefault", authoredCatalog);
        SetStaticField(typeof(MenuCatalog), "cachedSceneName", authoredCatalogSceneName);
        if (InventoryManager.Instance != null && authoredInventoryItems != null)
            InventoryManager.Instance.ConfigureItems(authoredInventoryItems);
        if (EquipmentManager.Instance != null && authoredEquipment != null)
            EquipmentManager.Instance.Configure(authoredEquipment);
        SetInstanceField(saveManager, "autoLoadOnStart", originalAutoLoad);
        SetInstanceField(saveManager, "autoSaveOnPause", originalAutoSaveOnPause);
        SetInstanceField(saveManager, "autoSaveOnQuit", originalAutoSaveOnQuit);
    }

    private static GameSaveData CaptureRuntimeState()
    {
        GameSaveData data = new GameSaveData();
        GameFlowManager.Instance?.FillSaveData(data);
        MoneyManager.Instance?.FillSaveData(data);
        AlienApprovalManager.Instance?.FillSaveData(data);
        UnlockManager.Instance?.FillSaveData(data);
        InventoryManager.Instance?.FillSaveData(data);
        MenuAvailabilityManager.Instance?.FillSaveData(data);
        EquipmentManager.Instance?.FillSaveData(data);
        UnlockCelebrationManager.EnsureInstance()?.FillSaveData(data);
        EmployeeManager.Instance?.FillSaveData(data);
        RestockOrderManager.Instance?.FillSaveData(data);
        CasualDiningPolishManager.EnsureInstance()?.FillSaveData(data);
        ManagerComplaintSystem.EnsureInstance()?.FillSaveData(data);
        return data;
    }

    private static void ApplyRuntimeState(GameSaveData data)
    {
        if (data == null) return;
        GameFlowManager.Instance?.ApplySaveData(data);
        UnlockManager.Instance?.ApplySaveData(data);
        InventoryManager.Instance?.ApplySaveData(data);
        MenuAvailabilityManager.Instance?.ApplySaveData(data);
        EquipmentManager.Instance?.ApplySaveData(data);
        UnlockCelebrationManager.EnsureInstance()?.ApplySaveData(data);
        EmployeeManager.Instance?.ApplySaveData(data);
        RestockOrderManager.EnsureInstance()?.ApplySaveData(data);
        CasualDiningPolishManager.EnsureInstance()?.ApplySaveData(data);
        ManagerComplaintSystem.EnsureInstance()?.ApplySaveData(data);
        MoneyManager.Instance?.ApplySaveData(data);
        AlienApprovalManager.Instance?.ApplySaveData(data);
    }

    private static ObjectiveSnapshot CaptureObjectives()
    {
        DailyObjectiveManager manager = DailyObjectiveManager.Instance;
        if (manager == null) return null;
        return new ObjectiveSnapshot
        {
            mandatory = manager.ActiveMandatory,
            secondary = manager.ActiveSecondary,
            bonus = manager.ActiveBonus,
            grade = manager.LastGrade,
            mandatoryPassed = manager.LastMandatoryPassed,
            secondaryPassed = manager.LastSecondaryPassed,
            bonusPassed = manager.LastBonusPassed,
            hasPrevious = manager.HasPreviousDayResult,
            resultDay = manager.LastResultDay,
            totalDaysPassed = manager.TotalDaysPassed,
            angryDepartures = ReadInt(manager, "angryDeparturesToday"),
            currentDay = ReadInt(manager, "currentDay")
        };
    }

    private static void RestoreObjectives(ObjectiveSnapshot state)
    {
        DailyObjectiveManager manager = DailyObjectiveManager.Instance;
        if (manager == null || state == null) return;
        SetInstanceField(manager, "<ActiveMandatory>k__BackingField", state.mandatory);
        SetInstanceField(manager, "<ActiveSecondary>k__BackingField", state.secondary);
        SetInstanceField(manager, "<ActiveBonus>k__BackingField", state.bonus);
        SetInstanceField(manager, "<LastGrade>k__BackingField", state.grade);
        SetInstanceField(manager, "<LastMandatoryPassed>k__BackingField", state.mandatoryPassed);
        SetInstanceField(manager, "<LastSecondaryPassed>k__BackingField", state.secondaryPassed);
        SetInstanceField(manager, "<LastBonusPassed>k__BackingField", state.bonusPassed);
        SetInstanceField(manager, "<HasPreviousDayResult>k__BackingField", state.hasPrevious);
        SetInstanceField(manager, "<LastResultDay>k__BackingField", state.resultDay);
        SetInstanceField(manager, "<TotalDaysPassed>k__BackingField", state.totalDaysPassed);
        SetInstanceField(manager, "angryDeparturesToday", state.angryDepartures);
        SetInstanceField(manager, "currentDay", state.currentDay);
    }

    private MenuCatalog BuildRuntimeCatalog(MenuCatalog source)
    {
        Dictionary<ItemData, ItemData> items = new Dictionary<ItemData, ItemData>();
        List<ItemData> clonedItems = new List<ItemData>();
        foreach (ItemData item in source.Ingredients)
        {
            if (item == null) continue;
            ItemData clone = Instantiate(item);
            clone.name = item.name + " (Tutorial Day)";
            clone.hideFlags = HideFlags.DontSave;
            clone.dayToUnlock = 1;
            runtimeClones.Add(clone);
            items[item] = clone;
            clonedItems.Add(clone);
        }

        Dictionary<Recipe, Recipe> recipes = new Dictionary<Recipe, Recipe>();
        List<Recipe> clonedRecipes = new List<Recipe>();
        foreach (Recipe recipe in source.Products)
        {
            if (recipe == null) continue;
            Recipe clone = Instantiate(recipe);
            clone.name = recipe.name + " (Tutorial Day)";
            clone.hideFlags = HideFlags.DontSave;
            clone.dayToUnlock = 1;
            clone.ingredients = new List<RecipeIngredient>();
            if (recipe.ingredients != null)
            {
                foreach (RecipeIngredient ingredient in recipe.ingredients)
                {
                    if (ingredient == null) continue;
                    clone.ingredients.Add(new RecipeIngredient
                    {
                        item = ingredient.item != null && items.TryGetValue(ingredient.item, out ItemData mapped)
                            ? mapped : ingredient.item,
                        amount = ingredient.amount
                    });
                }
            }
            runtimeClones.Add(clone);
            recipes[recipe] = clone;
            clonedRecipes.Add(clone);
        }

        List<MenuBundle> bundles = new List<MenuBundle>();
        foreach (MenuBundle sourceBundle in source.FoodBundles)
        {
            if (sourceBundle == null) continue;
            MenuBundle bundle = new MenuBundle
            {
                bundleId = sourceBundle.bundleId,
                displayName = sourceBundle.displayName,
                availableOnMenu = sourceBundle.availableOnMenu,
                menuSortOrder = sourceBundle.menuSortOrder,
                useCustomPrice = sourceBundle.useCustomPrice,
                customPrice = sourceBundle.customPrice,
                products = new List<Recipe>()
            };
            if (sourceBundle.products != null)
                foreach (Recipe product in sourceBundle.products)
                    if (product != null)
                        bundle.products.Add(recipes.TryGetValue(product, out Recipe mapped) ? mapped : product);
            bundles.Add(bundle);
        }

        MenuCatalog cloneCatalog = Instantiate(source);
        cloneCatalog.name = source.name + " (Tutorial Day)";
        cloneCatalog.hideFlags = HideFlags.DontSave;
        SetInstanceField(cloneCatalog, "products", clonedRecipes);
        SetInstanceField(cloneCatalog, "ingredients", clonedItems);
        SetInstanceField(cloneCatalog, "foodBundles", bundles);
        SetInstanceField(cloneCatalog, "byId", null);
        SetInstanceField(cloneCatalog, "byDisplayName", null);
        runtimeClones.Add(cloneCatalog);
        return cloneCatalog;
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
        if (restored) return;
        restored = true;

        if (authoredCatalog != null)
            SetStaticField(typeof(MenuCatalog), "cachedDefault", authoredCatalog);
        SetStaticField(typeof(MenuCatalog), "cachedSceneName", authoredCatalogSceneName);

        // Unity tears scene objects down in an undefined order when Play Mode is
        // stopping. Manager restoration at that point can recreate persistent
        // objects from OnDestroy, which Unity correctly reports as a cleanup
        // error. The process is ending, so only a live in-game scene transition
        // needs its previous runtime state restored.
        bool restoreLiveRuntime = Application.isPlaying;
#if UNITY_EDITOR
        // Application.isPlaying remains true during part of Editor teardown.
        // EditorApplication.isPlaying is already false once Play Mode is exiting,
        // while it stays true for an ordinary in-game scene transition.
        restoreLiveRuntime &= UnityEditor.EditorApplication.isPlaying
                              && UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode
                              && !editorExitingPlayMode;
#endif
        if (restoreLiveRuntime && InventoryManager.Instance != null && authoredInventoryItems != null)
            InventoryManager.Instance.ConfigureItems(authoredInventoryItems);
        if (restoreLiveRuntime && EquipmentManager.Instance != null && authoredEquipment != null)
            EquipmentManager.Instance.Configure(authoredEquipment);
        if (restoreLiveRuntime && runtimeIsolated)
        {
            ApplyRuntimeState(originalRuntimeState);
            RestoreObjectives(originalObjectives);
        }

        RestoreSaveManager(restoreLiveRuntime);
        DestroyRuntimeClones();
        Debug.Log(restoreLiveRuntime
            ? "[Tutorial Day] Original career runtime restored; tutorial clones discarded."
            : "[Tutorial Day] Play Mode ended; tutorial clones discarded without rebuilding scene managers.", this);
    }

    private void RestoreSaveManager(bool restoreLiveRuntime)
    {
        if (!saveIsolationPrepared || saveManager == null) return;
        SetInstanceField(saveManager, "autoLoadOnStart", originalAutoLoad);
        SetInstanceField(saveManager, "autoSaveOnPause", originalAutoSaveOnPause);
        SetInstanceField(saveManager, "autoSaveOnQuit", originalAutoSaveOnQuit);
        SetInstanceField(saveManager, "<IsApplyingSave>k__BackingField", originalApplyingSave);
        SetInstanceField(saveManager, "hasAutoLoaded", originalHasAutoLoaded);

        // When Lobby1Tutorial was launched directly, GameSaveManager.Start never
        // imported the career save. Load it only after the disposable context ends.
        if (restoreLiveRuntime && originalAutoLoad && !originalHasAutoLoaded && saveManager.HasSave())
        {
            saveManager.LoadGame();
            SetInstanceField(saveManager, "hasAutoLoaded", true);
        }
    }

    private void DestroyRuntimeClones()
    {
        foreach (Object clone in runtimeClones)
            if (clone != null) Destroy(clone);
        runtimeClones.Clear();
    }

    private static bool ReadBool(object owner, string name)
    {
        FieldInfo field = owner?.GetType().GetField(name, InstancePrivate);
        return field != null && field.GetValue(owner) is bool value && value;
    }

    private static int ReadInt(object owner, string name)
    {
        FieldInfo field = owner?.GetType().GetField(name, InstancePrivate);
        return field != null && field.GetValue(owner) is int value ? value : 0;
    }

    private static object GetStaticField(System.Type type, string name) =>
        type.GetField(name, StaticPrivate)?.GetValue(null);

    private static void SetInstanceField(object owner, string name, object value)
    {
        FieldInfo field = owner?.GetType().GetField(name, InstancePrivate);
        if (field == null)
            Debug.LogError("[Tutorial Day] Missing runtime field " + name + ".");
        else
            field.SetValue(owner, value);
    }

    private static void SetStaticField(System.Type type, string name, object value)
    {
        FieldInfo field = type.GetField(name, StaticPrivate);
        if (field == null)
            Debug.LogError("[Tutorial Day] Missing runtime field " + name + ".");
        else
            field.SetValue(null, value);
    }
}
