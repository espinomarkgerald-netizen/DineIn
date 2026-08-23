using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum DailyIncidentType
{
    Unaccommodated,
    WaitedTooLong,
    WrongOrder,
    OrderFailed,
    PaymentError,
    DirtyTableDelay,
    StockoutRefusal,
    TakeoutFailure
}

/// <summary>
/// Additive owner for Casual Dining's daily newspaper, market, rating, and
/// review state. Existing gameplay managers remain authoritative for their
/// own data; this component only snapshots and presents those results.
/// </summary>
[DefaultExecutionOrder(-470)]
public sealed class CasualDiningPolishManager : MonoBehaviour
{
    private const string LobbySceneName = "Lobby1";
    private const int CurrentSchemaVersion = 3;
    private const int CurrentNewspaperPresentationVersion = 2;

    public static CasualDiningPolishManager Instance { get; private set; }

    [SerializeField] private CasualDiningPolishSettings settings;
    [SerializeField] private int preparedDay;
    [SerializeField] private int lastFinalizedDay;
    [SerializeField] private int dayStartApproval = 30;
    [SerializeField] private int dayStartMoney = 500;
    [SerializeField, Range(0, 100)] private int restaurantRatingScore = 60;
    [SerializeField] private int supplierMarketGeneratedDay;

    private readonly List<SupplierPriceSaveEntry> supplierPrices =
        new List<SupplierPriceSaveEntry>();
    private readonly List<RestaurantRatingHistorySaveEntry> ratingHistory =
        new List<RestaurantRatingHistorySaveEntry>();
    private readonly List<RestaurantReviewSaveEntry> reviews =
        new List<RestaurantReviewSaveEntry>();
    private readonly List<NewspaperTemplateUseSaveEntry> templateHistory =
        new List<NewspaperTemplateUseSaveEntry>();
    private readonly List<NewspaperIssueSaveEntry> newspaperIssues =
        new List<NewspaperIssueSaveEntry>();

    private DailyRestaurantSnapshotSaveData lastSnapshot;
    private DailyNewspaperPresenter presenter;
    private int unaccommodated;
    private int waitedTooLong;
    private int wrongOrders;
    private int orderFailures;
    private int paymentErrors;
    private int dirtyTableDelays;
    private int stockoutRefusals;
    private int takeoutFailures;
    private bool applyingSave;

    public CasualDiningPolishSettings Settings => settings;
    public int RestaurantRatingScore => restaurantRatingScore;
    public float RestaurantStars => Mathf.Round(restaurantRatingScore / 10f) * 0.5f;
    public DailyRestaurantSnapshotSaveData LastSnapshot => lastSnapshot;
    public IReadOnlyList<RestaurantReviewSaveEntry> Reviews => reviews;
    public IReadOnlyList<NewspaperIssueSaveEntry> NewspaperIssues => newspaperIssues;
    public event Action NewspaperStateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static CasualDiningPolishManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        CasualDiningPolishManager existing = FindFirstObjectByType<CasualDiningPolishManager>();
        if (existing != null)
            return existing;

        GameObject root = new GameObject("Casual Dining Polish Manager");
        return root.AddComponent<CasualDiningPolishManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResolveSettings();
        restaurantRatingScore = Mathf.Clamp(
            restaurantRatingScore <= 0 ? settings.startingRatingScore : restaurantRatingScore,
            0,
            100);

        presenter = GetComponent<DailyNewspaperPresenter>();
        if (presenter == null)
            presenter = gameObject.AddComponent<DailyNewspaperPresenter>();
        presenter.Bind(this, settings);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        PrepareCurrentDayWhenReady();
        presenter?.RefreshVisibility();
    }

    private void Update()
    {
        GameFlowManager flow = GameFlowManager.Instance;
        if (flow == null || !flow.UsesSingleRestaurantFlow)
            return;

        GameFlowManager.RestaurantSessionState state = flow.CurrentRestaurantSessionState;
        if (state == GameFlowManager.RestaurantSessionState.PreOpen ||
            state == GameFlowManager.RestaurantSessionState.Endless)
        {
            PrepareDay(flow.CurrentDay, flow.IsEndlessRestaurantMode);
        }
    }

    private void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        PrepareCurrentDayWhenReady();
        presenter?.RefreshVisibility();
    }

    private void ResolveSettings()
    {
        if (settings != null)
            return;

        settings = Resources.Load<CasualDiningPolishSettings>(
            "CasualDining/CasualDiningPolishSettings");
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<CasualDiningPolishSettings>();
            settings.name = "Runtime Casual Dining Polish Settings";
        }
    }

    private void PrepareCurrentDayWhenReady()
    {
        GameFlowManager flow = GameFlowManager.Instance;
        if (flow != null && flow.UsesSingleRestaurantFlow)
            PrepareDay(flow.CurrentDay, flow.IsEndlessRestaurantMode);
    }

    public void PrepareDay(int day, bool endless)
    {
        ResolveSettings();
        day = Mathf.Max(1, day);
        bool changed = false;

        if (preparedDay != day)
        {
            preparedDay = day;
            dayStartApproval = AlienApprovalManager.Instance != null
                ? AlienApprovalManager.Instance.Approval
                : dayStartApproval;
            dayStartMoney = MoneyManager.Instance != null
                ? MoneyManager.Instance.Money
                : dayStartMoney;
            ResetIncidentCounters();
            InventoryManager.Instance?.ResetDiscardedUnitsForNewDay();
            EmployeeManager.Instance?.RefreshApplicantsIfDue(
                day,
                Mathf.Max(1, settings.applicantRefreshDays));
            changed = true;
        }

        if (EnsureSupplierMarketForDay(day))
            changed = true;
        EmployeeManager.Instance?.RefreshApplicantsIfDue(
            day,
            Mathf.Max(1, settings.applicantRefreshDays));
        if (EnsureNewspaperIssue(day, endless))
            changed = true;

        presenter?.RefreshVisibility();
        if (changed && !applyingSave)
            GameSaveManager.Instance?.RequestSave();
    }

    public void FinalizeDay(int day)
    {
        if (day <= 0 || lastFinalizedDay == day)
            return;

        ResolveSettings();
        DailyRestaurantSnapshotSaveData snapshot = BuildSnapshot(day);
        int ratingBefore = restaurantRatingScore;
        int dailyQuality = CalculateDailyQualityScore(snapshot);
        int target = Mathf.RoundToInt(Mathf.Lerp(
            ratingBefore,
            dailyQuality,
            Mathf.Clamp01(settings.ratingSmoothing)));
        int maxChange = Mathf.Max(1, settings.maximumDailyRatingChange);
        restaurantRatingScore = Mathf.Clamp(
            target,
            ratingBefore - maxChange,
            ratingBefore + maxChange);
        restaurantRatingScore = Mathf.Clamp(restaurantRatingScore, 0, 100);

        snapshot.ratingBefore = ratingBefore;
        snapshot.ratingAfter = restaurantRatingScore;
        lastSnapshot = snapshot;
        lastFinalizedDay = day;

        ratingHistory.Add(new RestaurantRatingHistorySaveEntry
        {
            day = day,
            previousScore = ratingBefore,
            dailyQualityScore = dailyQuality,
            resultingScore = restaurantRatingScore
        });
        TrimToRecent(ratingHistory, 90);

        GenerateReviews(snapshot);
        EmployeeManager.Instance?.ApplyDailyProgression(snapshot, settings);
        ResolveTopEmployee(snapshot);
        GameSaveManager.Instance?.RequestSave();
    }

    public void ResetRun()
    {
        ResolveSettings();
        preparedDay = 0;
        lastFinalizedDay = 0;
        dayStartApproval = 30;
        dayStartMoney = 500;
        restaurantRatingScore = Mathf.Clamp(settings.startingRatingScore, 0, 100);
        supplierMarketGeneratedDay = 0;
        supplierPrices.Clear();
        ratingHistory.Clear();
        reviews.Clear();
        templateHistory.Clear();
        newspaperIssues.Clear();
        lastSnapshot = null;
        ResetIncidentCounters();
        presenter?.CloseImmediately();
        presenter?.RefreshVisibility();
    }

    public void RegisterIncident(DailyIncidentType type, int amount = 1)
    {
        amount = Mathf.Max(0, amount);
        if (amount == 0)
            return;

        switch (type)
        {
            case DailyIncidentType.Unaccommodated: unaccommodated += amount; break;
            case DailyIncidentType.WaitedTooLong: waitedTooLong += amount; break;
            case DailyIncidentType.WrongOrder: wrongOrders += amount; break;
            case DailyIncidentType.OrderFailed: orderFailures += amount; break;
            case DailyIncidentType.PaymentError: paymentErrors += amount; break;
            case DailyIncidentType.DirtyTableDelay: dirtyTableDelays += amount; break;
            case DailyIncidentType.StockoutRefusal: stockoutRefusals += amount; break;
            case DailyIncidentType.TakeoutFailure: takeoutFailures += amount; break;
        }
    }

    public bool TryAllowStartShift()
    {
        GameFlowManager flow = GameFlowManager.Instance;
        if (flow == null || !flow.UsesSingleRestaurantFlow)
            return true;

        PrepareDay(flow.CurrentDay, flow.IsEndlessRestaurantMode);
        NewspaperIssueSaveEntry issue = GetIssueForDay(flow.CurrentDay);
        if (issue == null)
        {
            Debug.LogWarning("[DailyNewspaper] Issue generation was unavailable; service will not be blocked.");
            return true;
        }

        if (issue.viewed)
            return true;

        presenter?.Open(issue);
        WarningSlideUI.Instance?.Show("Read today's Galactic Gazette before opening the restaurant.");
        return false;
    }

    public void OpenCurrentIssue()
    {
        GameFlowManager flow = GameFlowManager.Instance;
        if (flow == null)
            return;
        PrepareDay(flow.CurrentDay, flow.IsEndlessRestaurantMode);
        NewspaperIssueSaveEntry issue = GetIssueForDay(flow.CurrentDay);
        if (issue != null)
            presenter?.Open(issue);
    }

    public string GetLatestReviewText()
    {
        return reviews.Count > 0 && reviews[reviews.Count - 1] != null
            ? reviews[reviews.Count - 1].text
            : "No customer reviews have been recorded yet.";
    }

    public void MarkCurrentIssueViewed()
    {
        int day = GameFlowManager.Instance != null
            ? GameFlowManager.Instance.CurrentDay
            : preparedDay;
        NewspaperIssueSaveEntry issue = GetIssueForDay(day);
        if (issue == null || issue.viewed)
            return;

        issue.viewed = true;
        NewspaperStateChanged?.Invoke();
        presenter?.RefreshVisibility();
        GameSaveManager.Instance?.RequestSave();
    }

    public NewspaperIssueSaveEntry GetIssueForDay(int day)
    {
        for (int i = 0; i < newspaperIssues.Count; i++)
        {
            NewspaperIssueSaveEntry issue = newspaperIssues[i];
            if (issue != null && issue.day == day)
            {
                UpgradeIssuePresentation(issue);
                return issue;
            }
        }
        return null;
    }

    private void UpgradeIssuePresentation(NewspaperIssueSaveEntry issue)
    {
        if (issue == null ||
            issue.presentationVersion >= CurrentNewspaperPresentationVersion)
            return;

        bool rebuilt = false;
        bool endless = GameFlowManager.Instance != null &&
                       GameFlowManager.Instance.IsEndlessRestaurantMode;
        if (issue.templateIDs == null)
            issue.templateIDs = new List<string>();
        if (issue.day <= 1)
        {
            issue.templateIDs.Clear();
            BuildWelcomeIssue(issue, endless);
            rebuilt = true;
        }
        else if (lastSnapshot != null && lastSnapshot.day == issue.sourceDay)
        {
            issue.templateIDs.Clear();
            BuildResultIssue(issue, lastSnapshot, endless);
            rebuilt = true;
        }

        if (rebuilt)
            issue.presentationVersion = CurrentNewspaperPresentationVersion;
    }

    public int GetCurrentBoxCost(ItemData item)
    {
        if (item == null)
            return 0;

        SupplierPriceSaveEntry price = FindSupplierPrice(item);
        return price != null
            ? Mathf.Max(0, price.currentCost)
            : Mathf.Max(0, item.boxCost);
    }

    public int GetPreviousBoxCost(ItemData item)
    {
        SupplierPriceSaveEntry price = FindSupplierPrice(item);
        return price != null
            ? Mathf.Max(0, price.previousCost)
            : item != null ? Mathf.Max(0, item.boxCost) : 0;
    }

    public static int GetCurrentBoxCostOrBase(ItemData item)
    {
        return Instance != null
            ? Instance.GetCurrentBoxCost(item)
            : item != null ? Mathf.Max(0, item.boxCost) : 0;
    }

    public string GetMarketTrendLabel(ItemData item)
    {
        int current = GetCurrentBoxCost(item);
        int previous = GetPreviousBoxCost(item);
        if (current > previous) return "▲ ₱" + current;
        if (current < previous) return "▼ ₱" + current;
        return "— ₱" + current;
    }

    public void FillSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.saveSchemaVersion = CurrentSchemaVersion;
        data.polishPreparedDay = preparedDay;
        data.polishLastFinalizedDay = lastFinalizedDay;
        data.polishDayStartApproval = dayStartApproval;
        data.polishDayStartMoney = dayStartMoney;
        data.restaurantRatingScore = restaurantRatingScore;
        data.supplierMarketGeneratedDay = supplierMarketGeneratedDay;
        data.lastDailyRestaurantSnapshot = CloneSnapshot(lastSnapshot);

        data.supplierPrices.Clear();
        for (int i = 0; i < supplierPrices.Count; i++)
            data.supplierPrices.Add(ClonePrice(supplierPrices[i]));

        data.restaurantRatingHistory.Clear();
        for (int i = 0; i < ratingHistory.Count; i++)
            data.restaurantRatingHistory.Add(CloneRating(ratingHistory[i]));

        data.restaurantReviews.Clear();
        for (int i = 0; i < reviews.Count; i++)
            data.restaurantReviews.Add(CloneReview(reviews[i]));

        data.newspaperTemplateHistory.Clear();
        for (int i = 0; i < templateHistory.Count; i++)
            data.newspaperTemplateHistory.Add(CloneTemplateUse(templateHistory[i]));

        data.newspaperIssues.Clear();
        for (int i = 0; i < newspaperIssues.Count; i++)
            data.newspaperIssues.Add(CloneIssue(newspaperIssues[i]));
    }

    public void ApplySaveData(GameSaveData data)
    {
        if (data == null)
            return;

        applyingSave = true;
        try
        {
            ResolveSettings();
            preparedDay = Mathf.Max(0, data.polishPreparedDay);
            lastFinalizedDay = Mathf.Max(0, data.polishLastFinalizedDay);
            bool migratedSave = data.saveSchemaVersion < CurrentSchemaVersion;
            dayStartApproval = migratedSave
                ? Mathf.Clamp(data.approval, 0, 100)
                : Mathf.Clamp(data.polishDayStartApproval, 0, 100);
            dayStartMoney = migratedSave ? data.money : data.polishDayStartMoney;
            restaurantRatingScore = migratedSave
                ? Mathf.Clamp(settings.startingRatingScore, 0, 100)
                : Mathf.Clamp(data.restaurantRatingScore, 0, 100);
            supplierMarketGeneratedDay = Mathf.Max(0, data.supplierMarketGeneratedDay);
            lastSnapshot = CloneSnapshot(data.lastDailyRestaurantSnapshot);

            supplierPrices.Clear();
            if (data.supplierPrices != null)
                for (int i = 0; i < data.supplierPrices.Count; i++)
                    if (data.supplierPrices[i] != null)
                        supplierPrices.Add(ClonePrice(data.supplierPrices[i]));

            ratingHistory.Clear();
            if (data.restaurantRatingHistory != null)
                for (int i = 0; i < data.restaurantRatingHistory.Count; i++)
                    if (data.restaurantRatingHistory[i] != null)
                        ratingHistory.Add(CloneRating(data.restaurantRatingHistory[i]));

            reviews.Clear();
            if (data.restaurantReviews != null)
                for (int i = 0; i < data.restaurantReviews.Count; i++)
                    if (data.restaurantReviews[i] != null)
                        reviews.Add(CloneReview(data.restaurantReviews[i]));

            templateHistory.Clear();
            if (data.newspaperTemplateHistory != null)
                for (int i = 0; i < data.newspaperTemplateHistory.Count; i++)
                    if (data.newspaperTemplateHistory[i] != null)
                        templateHistory.Add(CloneTemplateUse(data.newspaperTemplateHistory[i]));

            newspaperIssues.Clear();
            if (data.newspaperIssues != null)
                for (int i = 0; i < data.newspaperIssues.Count; i++)
                    if (data.newspaperIssues[i] != null)
                        newspaperIssues.Add(CloneIssue(data.newspaperIssues[i]));

            ResetIncidentCounters();
        }
        finally
        {
            applyingSave = false;
        }

        presenter?.RefreshVisibility();
        NewspaperStateChanged?.Invoke();
    }

    private bool EnsureSupplierMarketForDay(int day)
    {
        IReadOnlyList<ItemData> items = InventoryManager.Instance != null
            ? InventoryManager.Instance.Items
            : null;
        if (items == null || items.Count == 0)
            return false;

        SyncSupplierCatalog(items);
        if (supplierMarketGeneratedDay == day)
            return false;

        for (int i = 0; i < supplierPrices.Count; i++)
            supplierPrices[i].previousCost = Mathf.Max(0, supplierPrices[i].currentCost);

        int minimum = Mathf.Clamp(settings.minimumDailyPriceChanges, 1, items.Count);
        int maximum = Mathf.Clamp(settings.maximumDailyPriceChanges, minimum, items.Count);
        System.Random random = new System.Random(StableSeed(day, "market"));
        int changeCount = random.Next(minimum, maximum + 1);
        List<int> available = new List<int>();
        for (int i = 0; i < supplierPrices.Count; i++)
            available.Add(i);

        for (int change = 0; change < changeCount && available.Count > 0; change++)
        {
            int pick = random.Next(0, available.Count);
            SupplierPriceSaveEntry entry = supplierPrices[available[pick]];
            available.RemoveAt(pick);

            int percent = random.Next(
                Mathf.Max(1, settings.minimumPriceChangePercent),
                Mathf.Max(settings.minimumPriceChangePercent, settings.maximumPriceChangePercent) + 1);
            if (random.NextDouble() < settings.rareMarketEventChance)
                percent = Mathf.Max(percent, settings.rarePriceChangePercent);

            int direction = random.Next(0, 2) == 0 ? -1 : 1;
            float multiplier = 1f + direction * percent / 100f;
            int lower = Mathf.RoundToInt(entry.baseCost * settings.minimumPriceMultiplier);
            int upper = Mathf.RoundToInt(entry.baseCost * settings.maximumPriceMultiplier);
            int next = Mathf.Clamp(
                Mathf.RoundToInt(entry.currentCost * multiplier),
                Mathf.Max(0, lower),
                Mathf.Max(lower, upper));
            if (next == entry.currentCost && entry.baseCost > 0)
                next = Mathf.Clamp(entry.currentCost + direction, Mathf.Max(0, lower), Mathf.Max(lower, upper));
            entry.currentCost = Mathf.Max(0, next);
            entry.lastChangedDay = day;
            entry.marketEvent = BuildMarketEvent(entry.currentCost > entry.previousCost, random);
        }

        supplierMarketGeneratedDay = day;
        return true;
    }

    private void SyncSupplierCatalog(IReadOnlyList<ItemData> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            if (item == null)
                continue;
            SupplierPriceSaveEntry existing = FindSupplierPrice(item);
            if (existing == null)
            {
                int cost = Mathf.Max(0, item.boxCost);
                supplierPrices.Add(new SupplierPriceSaveEntry
                {
                    itemID = item.StableItemId,
                    itemType = item.itemType,
                    baseCost = cost,
                    previousCost = cost,
                    currentCost = cost,
                    lastChangedDay = 0
                });
            }
            else
            {
                existing.itemID = item.StableItemId;
                existing.itemType = item.itemType;
                if (existing.baseCost <= 0 && item.boxCost > 0)
                    existing.baseCost = item.boxCost;
                if (existing.currentCost < 0)
                    existing.currentCost = Mathf.Max(0, item.boxCost);
            }
        }
    }

    private SupplierPriceSaveEntry FindSupplierPrice(ItemData item)
    {
        if (item == null)
            return null;
        for (int i = 0; i < supplierPrices.Count; i++)
        {
            SupplierPriceSaveEntry entry = supplierPrices[i];
            if (entry == null)
                continue;
            if (!string.IsNullOrWhiteSpace(entry.itemID) &&
                string.Equals(entry.itemID, item.StableItemId, StringComparison.OrdinalIgnoreCase))
                return entry;
            if (string.IsNullOrWhiteSpace(entry.itemID) && entry.itemType == item.itemType)
                return entry;
        }
        return null;
    }

    private bool EnsureNewspaperIssue(int day, bool endless)
    {
        if (GetIssueForDay(day) != null)
            return false;

        int seed = StableSeed(day, "newspaper");
        NewspaperIssueSaveEntry issue = new NewspaperIssueSaveEntry
        {
            issueID = "gazette-day-" + day,
            day = day,
            sourceDay = lastSnapshot != null ? lastSnapshot.day : 0,
            seed = seed,
            presentationVersion = CurrentNewspaperPresentationVersion,
            viewed = false,
            byline = settings.alienReporterName
        };

        if (day <= 1 || lastSnapshot == null)
            BuildWelcomeIssue(issue, endless);
        else
            BuildResultIssue(issue, lastSnapshot, endless);

        newspaperIssues.Add(issue);
        TrimToRecent(newspaperIssues, 90);
        return true;
    }

    private void BuildWelcomeIssue(NewspaperIssueSaveEntry issue, bool endless)
    {
        issue.headline = endless
            ? "EARTH DINER CONTINUES ITS ENDLESS GALACTIC LEGACY"
            : "HUMAN DINER OPENS UNDER GALACTIC WATCH";
        string market = BuildMarketWatch(issue.day);
        issue.renderedContent =
            "<size=26><b>◎ APPROVAL WATCH</b></size>\n" +
            "<color=#244F73><size=30><b>" + dayStartApproval + "%  UNDER WATCH</b></size></color>\n" +
            "New human diner. Galactic inspectors are observing today's service.\n\n" +
            "<size=26><b>★ RESTAURANT RATING</b></size>\n" +
            "<color=#9A6912><size=29><b>" + BuildStars(restaurantRatingScore) +
            "  " + FormatStars(restaurantRatingScore) + " / 5</b></size></color>\n" +
            "<b>NO REVIEWS YET</b>  Today's shift creates the first report.\n\n" +
            market + "\n\n" +
            "<color=#5B1715><size=26><b>! BOSS ORDER: PREP FIRST</b></size></color>\n" +
            "<color=#241D17><b>1  ASSIGN ROLES\n2  CHECK FRESH STOCK\n3  REVIEW PRICES\n4  OPEN WHEN READY</b></color>";
    }

    private void BuildResultIssue(
        NewspaperIssueSaveEntry issue,
        DailyRestaurantSnapshotSaveData snapshot,
        bool endless)
    {
        bool negative = snapshot.angryCustomers > 0 || DominantIncidentValue(snapshot) > 0;
        bool positive = !negative && snapshot.happyCustomers > snapshot.neutralCustomers;
        string headlineID;
        string storyID;
        string quoteID;
        string adviceID;
        string milestoneHeadline = GetMilestoneHeadline(issue.day);
        if (!string.IsNullOrWhiteSpace(milestoneHeadline))
        {
            issue.headline = milestoneHeadline;
            headlineID = "headline-milestone-" + issue.day;
            RecordTemplateUse("headline", headlineID, issue.day);
        }
        else
        {
            issue.headline = SelectTemplate(
                "headline",
                settings.approvalHeadlines,
                issue.day,
                issue.seed,
                out headlineID);
        }

        string storyTemplate = SelectTemplate(
            positive ? "story-positive" : negative ? "story-negative" : "story-neutral",
            positive ? settings.positiveStories : negative ? settings.negativeStories : settings.neutralStories,
            issue.day,
            issue.seed + 17,
            out storyID);
        string quote = SelectTemplate(
            positive ? "quote-positive" : negative ? "quote-negative" : "quote-neutral",
            positive ? settings.positiveCustomerQuotes : negative ? settings.negativeCustomerQuotes : settings.neutralCustomerQuotes,
            issue.day,
            issue.seed + 31,
            out quoteID);
        string advice = BuildAdvice(snapshot, issue.day, issue.seed + 47, out adviceID);

        issue.templateIDs.Add(headlineID);
        issue.templateIDs.Add(storyID);
        issue.templateIDs.Add(quoteID);
        issue.templateIDs.Add(adviceID);

        string story = ResolveTokens(storyTemplate, snapshot);
        int approvalDelta = snapshot.approvalAfter - snapshot.approvalBefore;
        string approvalColor = approvalDelta > 0
            ? "#176B36"
            : approvalDelta < 0 ? "#A31818" : "#545047";
        string approvalSignal = approvalDelta > 0
            ? "▲ +" + approvalDelta
            : approvalDelta < 0 ? "▼ " + approvalDelta : "■ NO CHANGE";
        int ratingDelta = snapshot.ratingAfter - snapshot.ratingBefore;
        string ratingSignal = ratingDelta > 0
            ? "▲ IMPROVING"
            : ratingDelta < 0 ? "▼ FALLING" : "■ STEADY";
        string ratingColor = ratingDelta > 0
            ? "#176B36"
            : ratingDelta < 0 ? "#A31818" : "#545047";

        StringBuilder content = new StringBuilder(1700);
        content.Append("<size=26><b>◎ APPROVAL WATCH</b></size>\n")
            .Append("<color=").Append(approvalColor).Append("><size=30><b>")
            .Append(snapshot.approvalAfter).Append("%   ")
            .Append(endless ? "ENDLESS" : approvalSignal)
            .Append("</b></size></color>\n")
            .Append(story).Append("\n\n")
            .Append("<size=26><b>★ RESTAURANT RATING</b></size>\n")
            .Append("<color=#9A6912><size=29><b>")
            .Append(BuildStars(snapshot.ratingAfter)).Append("  ")
            .Append(FormatStars(snapshot.ratingAfter)).Append(" / 5")
            .Append("</b></size></color>\n")
            .Append("<color=").Append(ratingColor).Append("><b>")
            .Append(ratingSignal).Append("</b></color>  DAY ")
            .Append(snapshot.day).Append(" RESULT\n\n")
            .Append("<size=26><b>● SHIFT SNAPSHOT</b></size>\n")
            .Append("<b>").Append(snapshot.customersServed).Append(" SERVED</b>    ")
            .Append("<color=#176B36><b>").Append(snapshot.happyCustomers).Append(" HAPPY</b></color>    ")
            .Append("<color=#A31818><b>").Append(snapshot.angryCustomers).Append(" ANGRY</b></color>\n")
            .Append("<b>").Append(snapshot.groupsSeated).Append(" GROUPS SEATED</b>    ")
            .Append(snapshot.ordersCompleted).Append(" ORDERS DONE\n")
            .Append(BuildIncidentAlert(snapshot)).Append("\n\n")
            .Append("<size=25><b>◆ VOICE FROM THE QUEUE</b></size>\n<i>\"")
            .Append(quote).Append("\"</i>\n\n")
            .Append(BuildMarketWatch(issue.day)).Append("\n\n")
            .Append("<size=26><b>! BOSS ORDER: ")
            .Append(BuildAdviceFocus(snapshot)).Append("</b></size>\n")
            .Append(advice);

        if (!string.IsNullOrWhiteSpace(snapshot.topEmployeeName))
        {
            content.Append("\n\n<size=25><b>★ STAFF SPOTLIGHT</b></size>\n")
                .Append("<b>").Append(snapshot.topEmployeeName.ToUpperInvariant()).Append("</b>    ")
                .Append(snapshot.topEmployeePerformance).Append("% PERFORMANCE");

            EmployeeData topEmployee = FindEmployee(snapshot.topEmployeeID);
            if (topEmployee != null && topEmployee.lastPromotionDay == snapshot.day)
            {
                content.Append("\n<color=#176B36><b>▲ PROMOTED TO ")
                    .Append(topEmployee.stars).Append(" STARS</b></color>");
                if (EmployeeManager.Instance != null && EmployeeManager.Instance.salaryConfig != null)
                {
                    content.Append("    NEW WAGE ₱")
                        .Append(topEmployee.GetSalary(EmployeeManager.Instance.salaryConfig));
                }
            }
        }

        issue.renderedContent = content.ToString();
        issue.presentationVersion = CurrentNewspaperPresentationVersion;
    }

    private static string GetMilestoneHeadline(int day)
    {
        return day switch
        {
            10 => "TEN DAYS OF HUMAN DINING ENTER THE GALACTIC RECORD",
            20 => "EARTH DINER REACHES TWENTY-DAY COSMIC MILESTONE",
            30 => "THIRTY DAYS COMPLETE AS THE FINAL INSPECTION NEARS",
            _ => string.Empty
        };
    }

    private static EmployeeData FindEmployee(string employeeID)
    {
        if (string.IsNullOrWhiteSpace(employeeID) || EmployeeManager.Instance == null ||
            EmployeeManager.Instance.allEmployees == null)
            return null;

        return EmployeeManager.Instance.allEmployees.Find(employee =>
            employee != null && string.Equals(
                employee.EmployeeID,
                employeeID,
                StringComparison.OrdinalIgnoreCase));
    }

    private string BuildMarketWatch(int day)
    {
        StringBuilder result = new StringBuilder("<size=26><b>₱ MARKET WATCH</b></size>\n");
        IReadOnlyList<ItemData> items = InventoryManager.Instance != null
            ? InventoryManager.Instance.Items
            : null;
        int listed = 0;
        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                ItemData item = items[i];
                SupplierPriceSaveEntry price = FindSupplierPrice(item);
                if (item == null || price == null || price.lastChangedDay != day ||
                    price.currentCost == price.previousCost)
                    continue;

                int difference = price.currentCost - price.previousCost;
                float percentage = price.previousCost > 0
                    ? Mathf.Abs(difference) * 100f / price.previousCost
                    : 0f;
                bool increased = difference > 0;
                result.Append(increased
                        ? "<color=#A31818><size=24><b>▲ PRICE INCREASE</b></size></color>\n"
                        : "<color=#176B36><size=24><b>▼ PRICE DECREASE</b></size></color>\n")
                    .Append("<b>").Append(item.displayName.ToUpperInvariant()).Append("    ₱")
                    .Append(price.currentCost).Append(" / BOX    ")
                    .Append(increased ? "+" : "-")
                    .Append(Mathf.RoundToInt(percentage)).Append("%</b>\n")
                    .Append(string.IsNullOrWhiteSpace(price.marketEvent)
                        ? "Supplier cost changed today."
                        : price.marketEvent)
                    .Append("\n\n");
                listed++;
            }
        }

        if (listed == 0)
            result.Append("<color=#545047><size=23><b>■ PRICES STABLE</b></size></color>\n")
                .Append("No supplier price changed today.");
        else
            result.Append("<b>LOCKED AT CHECKOUT</b>  Today's order keeps today's price.");
        return result.ToString().TrimEnd();
    }

    private string BuildAdvice(
        DailyRestaurantSnapshotSaveData snapshot,
        int day,
        int seed,
        out string templateID)
    {
        string advice;
        if (snapshot.unaccommodated > 0)
            advice = "Improve host coverage and table turnover. {unaccommodated} group(s) left without a table.";
        else if (snapshot.wrongOrders > 0 || snapshot.orderFailures > 0)
            advice = "Tighten waiter and kitchen checks. {wrong} wrong order(s); {failed} failed service(s).";
        else if (snapshot.waitedTooLong > 0)
            advice = "Shorten the queue and response time. {waited} group(s) exhausted their patience.";
        else if (snapshot.paymentErrors > 0)
            advice = "Assign a reliable cashier and verify change. {cash} payment error(s) recorded.";
        else if (snapshot.stockoutRefusals > 0 || snapshot.discardedUnits > 0)
            advice = "Balance purchasing and freshness. Stock failed guests; {discarded} unit(s) discarded.";
        else
            return SelectTemplate(
                "advice-positive",
                settings.positiveAdvice,
                day,
                seed,
                out templateID);

        templateID = "advice-dynamic-" + DominantIncidentName(snapshot);
        RecordTemplateUse("advice", templateID, day);
        return ResolveTokens(advice, snapshot);
    }

    private static string BuildAdviceFocus(DailyRestaurantSnapshotSaveData snapshot)
    {
        if (snapshot == null) return "STAY READY";
        if (snapshot.unaccommodated > 0) return "HOST COVERAGE";
        if (snapshot.wrongOrders > 0 || snapshot.orderFailures > 0) return "ORDER ACCURACY";
        if (snapshot.waitedTooLong > 0) return "SHORTER WAITS";
        if (snapshot.paymentErrors > 0) return "CASH CONTROL";
        if (snapshot.stockoutRefusals > 0 || snapshot.discardedUnits > 0) return "STOCK CONTROL";
        return "KEEP THE STANDARD";
    }

    private static string BuildIncidentAlert(DailyRestaurantSnapshotSaveData snapshot)
    {
        int value = DominantIncidentValue(snapshot);
        if (value <= 0)
        {
            return "<color=#176B36><size=24><b>✓ CLEAN SHIFT</b></size></color>\n" +
                   "No major service incident.";
        }

        string label;
        string detail;
        switch (DominantIncidentName(snapshot))
        {
            case "unaccommodated":
                label = "LEFT WITHOUT A TABLE";
                detail = "Improve host coverage and table turnover.";
                break;
            case "wrong-order":
                label = "WRONG ORDERS";
                detail = "Check tickets before food reaches a table.";
                break;
            case "wait":
                label = "PATIENCE LOST";
                detail = "The queue moved too slowly.";
                break;
            case "payment":
                label = "PAYMENT ERRORS";
                detail = "Verify every payment and change amount.";
                break;
            case "stockout":
                label = "STOCKOUT REFUSALS";
                detail = "Restock before opening service.";
                break;
            case "takeout":
                label = "TAKEOUT FAILURES";
                detail = "Keep the pickup queue moving.";
                break;
            default:
                label = "FAILED ORDERS";
                detail = "Review yesterday's service handoff.";
                break;
        }

        return "<color=#A31818><size=24><b>! " + value + " " + label +
               "</b></size></color>\n" + detail;
    }

    private string BuildIncidentSummary(DailyRestaurantSnapshotSaveData snapshot)
    {
        int value = DominantIncidentValue(snapshot);
        if (value <= 0)
            return "No major service incident dominated the report.";

        string name = DominantIncidentName(snapshot);
        return name switch
        {
            "unaccommodated" => value + " group" + Plural(value) + " left without being accommodated.",
            "wrong-order" => value + " wrong order" + Plural(value) + " reached alien tables.",
            "wait" => value + " group" + Plural(value) + " waited beyond their patience.",
            "payment" => value + " payment error" + Plural(value) + " reached the daily report.",
            "stockout" => value + " group" + Plural(value) + " encountered missing stock.",
            "takeout" => value + " takeout order" + Plural(value) + " failed.",
            _ => value + " order failure" + Plural(value) + " was recorded."
        };
    }

    private DailyRestaurantSnapshotSaveData BuildSnapshot(int day)
    {
        GameDayManager game = GameDayManager.Instance;
        DailyRevenueTracker revenue = DailyRevenueTracker.Instance;
        DailyFinanceBridge finance = DailyFinanceBridge.Instance;
        int earned = finance != null ? finance.EarnedToday : 0;
        int ingredients = finance != null ? finance.IngredientCostToday : revenue != null ? revenue.IngredientCost : 0;
        int employees = finance != null ? finance.EmployeeCostToday : 0;
        int other = finance != null ? finance.MarketingCostToday + finance.BillsCostToday : 0;

        return new DailyRestaurantSnapshotSaveData
        {
            day = day,
            approvalBefore = dayStartApproval,
            approvalAfter = AlienApprovalManager.Instance != null
                ? AlienApprovalManager.Instance.Approval
                : dayStartApproval,
            ratingBefore = restaurantRatingScore,
            ratingAfter = restaurantRatingScore,
            groupsArrived = game != null ? game.GroupsSpawnedThisShift : 0,
            groupsSeated = game != null ? game.GroupsSeated : 0,
            customersServed = game != null ? game.CustomersServed : 0,
            happyCustomers = game != null ? game.HappyCustomers : 0,
            neutralCustomers = game != null ? game.NeutralCustomers : 0,
            angryCustomers = game != null ? game.AngryCustomers : 0,
            unaccommodated = unaccommodated,
            waitedTooLong = waitedTooLong,
            wrongOrders = wrongOrders,
            orderFailures = Mathf.Max(orderFailures, revenue != null ? revenue.OrdersFailed : 0),
            paymentErrors = Mathf.Max(paymentErrors, game != null ? game.CashErrors : 0),
            dirtyTableDelays = dirtyTableDelays,
            stockoutRefusals = stockoutRefusals,
            takeoutFailures = takeoutFailures,
            ordersCompleted = revenue != null ? revenue.OrdersCompleted : 0,
            ordersFailed = revenue != null ? revenue.OrdersFailed : 0,
            revenue = earned,
            ingredientCost = ingredients,
            employeeCost = employees,
            otherCosts = other,
            profit = earned - ingredients - employees - other,
            discardedUnits = InventoryManager.Instance != null
                ? InventoryManager.Instance.DiscardedUnitsToday
                : 0,
            lowStockItems = CountLowStockItems(day)
        };
    }

    private int CalculateDailyQualityScore(DailyRestaurantSnapshotSaveData snapshot)
    {
        int served = Mathf.Max(1, snapshot.customersServed);
        int orders = Mathf.Max(1, snapshot.ordersCompleted + snapshot.ordersFailed);
        int arrivals = Mathf.Max(1, snapshot.groupsArrived);
        float satisfaction = (snapshot.happyCustomers * 100f + snapshot.neutralCustomers * 60f) / served;
        float completion = snapshot.ordersCompleted * 100f / orders;
        float accommodation = snapshot.groupsSeated * 100f / arrivals;
        float accuracy = Mathf.Clamp(
            100f - snapshot.paymentErrors * 15f - snapshot.wrongOrders * 18f,
            0f,
            100f);
        float stock = Mathf.Clamp(
            100f - snapshot.stockoutRefusals * 20f - snapshot.discardedUnits * 0.5f,
            0f,
            100f);
        float score = satisfaction * 0.35f + completion * 0.25f +
                      accommodation * 0.20f + accuracy * 0.10f + stock * 0.10f;
        return Mathf.Clamp(Mathf.RoundToInt(score), 0, 100);
    }

    private void GenerateReviews(DailyRestaurantSnapshotSaveData snapshot)
    {
        int count = Mathf.Clamp(snapshot.customersServed, 1, 3);
        bool positive = snapshot.happyCustomers >= snapshot.angryCustomers &&
                        DominantIncidentValue(snapshot) == 0;
        bool negative = snapshot.angryCustomers > snapshot.happyCustomers ||
                        DominantIncidentValue(snapshot) > 0;
        string[] pool = positive
            ? settings.positiveCustomerQuotes
            : negative ? settings.negativeCustomerQuotes : settings.neutralCustomerQuotes;
        string section = positive ? "review-positive" : negative ? "review-negative" : "review-neutral";

        for (int i = 0; i < count; i++)
        {
            string template = SelectTemplate(
                section,
                pool,
                snapshot.day,
                StableSeed(snapshot.day, "review-" + i),
                out string templateID);
            reviews.Add(new RestaurantReviewSaveEntry
            {
                reviewID = "review-" + snapshot.day + "-" + i,
                templateID = templateID,
                day = snapshot.day,
                positive = positive,
                text = ResolveTokens(template, snapshot)
            });
        }
        TrimToRecent(reviews, 180);
    }

    private void ResolveTopEmployee(DailyRestaurantSnapshotSaveData snapshot)
    {
        if (snapshot == null || EmployeeManager.Instance == null)
            return;
        EmployeeData top = null;
        IReadOnlyList<EmployeeData> employees = EmployeeManager.Instance.allEmployees;
        for (int i = 0; i < employees.Count; i++)
        {
            EmployeeData candidate = employees[i];
            if (candidate == null || !candidate.hired || candidate.daysWorked <= 0)
                continue;
            if (top == null || candidate.recentPerformance > top.recentPerformance)
                top = candidate;
        }
        if (top == null)
            return;
        snapshot.topEmployeeID = top.EmployeeID;
        snapshot.topEmployeeName = top.employeeName;
        snapshot.topEmployeePerformance = top.recentPerformance;
    }

    private int CountLowStockItems(int day)
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null || inventory.Items == null)
            return 0;
        int result = 0;
        for (int i = 0; i < inventory.Items.Count; i++)
        {
            ItemData item = inventory.Items[i];
            if (item != null && inventory.GetFreshStock(item.itemType, day) < Mathf.Max(1, item.unitsPerBox))
                result++;
        }
        return result;
    }

    private string SelectTemplate(
        string section,
        string[] pool,
        int day,
        int seed,
        out string templateID)
    {
        if (pool == null || pool.Length == 0)
        {
            templateID = section + "-fallback";
            return "No report was available for this section.";
        }

        int exclusionDay = day - Mathf.Max(1, settings.recentTemplateExclusionDays);
        List<int> candidates = new List<int>();
        for (int i = 0; i < pool.Length; i++)
        {
            string id = section + "-" + i;
            bool recent = false;
            for (int h = templateHistory.Count - 1; h >= 0; h--)
            {
                NewspaperTemplateUseSaveEntry use = templateHistory[h];
                if (use == null || use.day < exclusionDay)
                    break;
                if (use.section == section && use.templateID == id)
                {
                    recent = true;
                    break;
                }
            }
            if (!recent)
                candidates.Add(i);
        }

        if (candidates.Count == 0)
        {
            int oldestDay = int.MaxValue;
            int oldestIndex = 0;
            for (int i = 0; i < pool.Length; i++)
            {
                string id = section + "-" + i;
                int lastUse = int.MinValue;
                for (int h = templateHistory.Count - 1; h >= 0; h--)
                {
                    NewspaperTemplateUseSaveEntry use = templateHistory[h];
                    if (use != null && use.section == section && use.templateID == id)
                    {
                        lastUse = use.day;
                        break;
                    }
                }
                if (lastUse < oldestDay)
                {
                    oldestDay = lastUse;
                    oldestIndex = i;
                }
            }
            candidates.Add(oldestIndex);
        }

        System.Random random = new System.Random(seed);
        int selected = candidates[random.Next(0, candidates.Count)];
        templateID = section + "-" + selected;
        RecordTemplateUse(section, templateID, day);
        return pool[selected] ?? string.Empty;
    }

    private void RecordTemplateUse(string section, string templateID, int day)
    {
        for (int i = 0; i < templateHistory.Count; i++)
        {
            NewspaperTemplateUseSaveEntry existing = templateHistory[i];
            if (existing != null && existing.day == day && existing.section == section &&
                existing.templateID == templateID)
                return;
        }
        templateHistory.Add(new NewspaperTemplateUseSaveEntry
        {
            section = section,
            templateID = templateID,
            day = day
        });
        TrimToRecent(templateHistory, 360);
    }

    private static string ResolveTokens(string source, DailyRestaurantSnapshotSaveData snapshot)
    {
        if (string.IsNullOrEmpty(source) || snapshot == null)
            return source ?? string.Empty;
        return source
            .Replace("{happy}", snapshot.happyCustomers.ToString())
            .Replace("{neutral}", snapshot.neutralCustomers.ToString())
            .Replace("{angry}", snapshot.angryCustomers.ToString())
            .Replace("{approval}", snapshot.approvalAfter.ToString())
            .Replace("{unaccommodated}", snapshot.unaccommodated.ToString())
            .Replace("{waited}", snapshot.waitedTooLong.ToString())
            .Replace("{wrong}", snapshot.wrongOrders.ToString())
            .Replace("{failed}", snapshot.orderFailures.ToString())
            .Replace("{cash}", snapshot.paymentErrors.ToString())
            .Replace("{discarded}", snapshot.discardedUnits.ToString());
    }

    private static int DominantIncidentValue(DailyRestaurantSnapshotSaveData snapshot)
    {
        if (snapshot == null)
            return 0;
        return Mathf.Max(
            snapshot.unaccommodated,
            snapshot.wrongOrders,
            snapshot.waitedTooLong,
            snapshot.paymentErrors,
            snapshot.stockoutRefusals,
            snapshot.takeoutFailures,
            snapshot.orderFailures);
    }

    private static string DominantIncidentName(DailyRestaurantSnapshotSaveData snapshot)
    {
        int max = DominantIncidentValue(snapshot);
        if (max <= 0) return "none";
        if (snapshot.unaccommodated == max) return "unaccommodated";
        if (snapshot.wrongOrders == max) return "wrong-order";
        if (snapshot.waitedTooLong == max) return "wait";
        if (snapshot.paymentErrors == max) return "payment";
        if (snapshot.stockoutRefusals == max) return "stockout";
        if (snapshot.takeoutFailures == max) return "takeout";
        return "order";
    }

    private static string BuildStars(int score)
    {
        int halfStars = Mathf.Clamp(Mathf.RoundToInt(score / 10f), 0, 10);
        int full = halfStars / 2;
        bool half = halfStars % 2 != 0;
        return new string('★', full) + (half ? "½" : string.Empty) +
               new string('☆', Mathf.Max(0, 5 - full - (half ? 1 : 0)));
    }

    private static string FormatStars(int score)
    {
        return (Mathf.Round(score / 10f) * 0.5f).ToString("0.0");
    }

    private static string Plural(int count) => count == 1 ? string.Empty : "s";

    private static int StableSeed(int day, string salt)
    {
        unchecked
        {
            int hash = 17;
            string value = salt ?? string.Empty;
            for (int i = 0; i < value.Length; i++)
                hash = hash * 31 + value[i];
            return hash * 397 ^ day * 7919;
        }
    }

    private static string BuildMarketEvent(bool increased, System.Random random)
    {
        string[] increases =
        {
            "A cargo delay near the outer moons tightened supply.",
            "A busy orbital festival increased sector-wide demand.",
            "Supplier drones reported a smaller harvest than expected.",
            "A customs inspection slowed incoming freighters."
        };
        string[] decreases =
        {
            "A strong moon harvest expanded today's supply.",
            "New cargo lanes brought extra boxes into the sector.",
            "Supplier drones cleared a large warehouse reserve.",
            "A calm trade route lowered delivery costs."
        };
        string[] pool = increased ? increases : decreases;
        return pool[random.Next(0, pool.Length)];
    }

    private void ResetIncidentCounters()
    {
        unaccommodated = 0;
        waitedTooLong = 0;
        wrongOrders = 0;
        orderFailures = 0;
        paymentErrors = 0;
        dirtyTableDelays = 0;
        stockoutRefusals = 0;
        takeoutFailures = 0;
    }

    private static void TrimToRecent<T>(List<T> list, int maximum)
    {
        if (list != null && list.Count > maximum)
            list.RemoveRange(0, list.Count - maximum);
    }

    private static SupplierPriceSaveEntry ClonePrice(SupplierPriceSaveEntry source) =>
        source == null ? null : new SupplierPriceSaveEntry
        {
            itemID = source.itemID,
            itemType = source.itemType,
            baseCost = source.baseCost,
            previousCost = source.previousCost,
            currentCost = source.currentCost,
            lastChangedDay = source.lastChangedDay,
            marketEvent = source.marketEvent
        };

    private static RestaurantRatingHistorySaveEntry CloneRating(RestaurantRatingHistorySaveEntry source) =>
        source == null ? null : new RestaurantRatingHistorySaveEntry
        {
            day = source.day,
            previousScore = source.previousScore,
            dailyQualityScore = source.dailyQualityScore,
            resultingScore = source.resultingScore
        };

    private static RestaurantReviewSaveEntry CloneReview(RestaurantReviewSaveEntry source) =>
        source == null ? null : new RestaurantReviewSaveEntry
        {
            reviewID = source.reviewID,
            templateID = source.templateID,
            day = source.day,
            positive = source.positive,
            text = source.text
        };

    private static NewspaperTemplateUseSaveEntry CloneTemplateUse(NewspaperTemplateUseSaveEntry source) =>
        source == null ? null : new NewspaperTemplateUseSaveEntry
        {
            section = source.section,
            templateID = source.templateID,
            day = source.day
        };

    private static NewspaperIssueSaveEntry CloneIssue(NewspaperIssueSaveEntry source)
    {
        if (source == null)
            return null;
        NewspaperIssueSaveEntry clone = new NewspaperIssueSaveEntry
        {
            issueID = source.issueID,
            day = source.day,
            sourceDay = source.sourceDay,
            seed = source.seed,
            presentationVersion = source.presentationVersion,
            viewed = source.viewed,
            headline = source.headline,
            byline = source.byline,
            renderedContent = source.renderedContent
        };
        if (source.templateIDs != null)
            clone.templateIDs.AddRange(source.templateIDs);
        return clone;
    }

    private static DailyRestaurantSnapshotSaveData CloneSnapshot(
        DailyRestaurantSnapshotSaveData source)
    {
        if (source == null)
            return null;
        return new DailyRestaurantSnapshotSaveData
        {
            day = source.day,
            approvalBefore = source.approvalBefore,
            approvalAfter = source.approvalAfter,
            ratingBefore = source.ratingBefore,
            ratingAfter = source.ratingAfter,
            groupsArrived = source.groupsArrived,
            groupsSeated = source.groupsSeated,
            customersServed = source.customersServed,
            happyCustomers = source.happyCustomers,
            neutralCustomers = source.neutralCustomers,
            angryCustomers = source.angryCustomers,
            unaccommodated = source.unaccommodated,
            waitedTooLong = source.waitedTooLong,
            wrongOrders = source.wrongOrders,
            orderFailures = source.orderFailures,
            paymentErrors = source.paymentErrors,
            dirtyTableDelays = source.dirtyTableDelays,
            stockoutRefusals = source.stockoutRefusals,
            takeoutFailures = source.takeoutFailures,
            ordersCompleted = source.ordersCompleted,
            ordersFailed = source.ordersFailed,
            revenue = source.revenue,
            ingredientCost = source.ingredientCost,
            employeeCost = source.employeeCost,
            otherCosts = source.otherCosts,
            profit = source.profit,
            discardedUnits = source.discardedUnits,
            lowStockItems = source.lowStockItems,
            topEmployeeID = source.topEmployeeID,
            topEmployeeName = source.topEmployeeName,
            topEmployeePerformance = source.topEmployeePerformance
        };
    }
}
