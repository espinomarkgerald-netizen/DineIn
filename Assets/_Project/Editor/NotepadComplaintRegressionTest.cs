#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class NotepadComplaintRegressionTest
{
    private const string ScenePath = "Assets/_Project/Scenes/RoleBased/Lobby1.unity";
    private const string RunningKey = "DineIn.NotepadComplaintRegressionTest.Running";
    private const string RequestFileName = "RunNotepadComplaintRegressionTest.request";
    private const string ResultFileName = "NotepadComplaintRegressionTest.result";

    private enum Phase
    {
        None,
        WaitingForLobby,
        WaitingForLayout,
        WaitingForReplacement
    }

    private static Phase phase;
    private static double phaseStartedAt;
    private static int passedChecks;
    private static OrderChecklistUI notepad;
    private static CustomerGroup complaintGroup;
    private static Booth complaintBooth;
    private static KitchenManager kitchen;
    private static Transform traySlot;
    private static FoodTray trayTemplate;
    private static OrderNumberManager previousOrderNumberManager;
    private static OrderNumberManager testOrderNumberManager;
    private static int replacementOrderNumber;
    private static int kitchenStartedCount;
    private static bool previousSaveWriteSuppression;

    static NotepadComplaintRegressionTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;

        string requestPath = Path.Combine(ProjectRoot, "Temp", RequestFileName);
        if (File.Exists(requestPath))
        {
            File.Delete(requestPath);
            EditorApplication.delayCall += Run;
        }
    }

    [MenuItem("Tools/Dine In/Run Notepad + Complaint Regression %#F9")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[NotepadComplaintRegressionTest] Stop Play Mode before running.");
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ResetState();
        SessionState.SetBool(RunningKey, true);
        WriteResult("RUNNING");
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            if (GameSaveManager.Instance != null)
            {
                previousSaveWriteSuppression = GameSaveManager.Instance.SuppressWritesForTests;
                GameSaveManager.Instance.SuppressWritesForTests = true;
            }

            SetPhase(Phase.WaitingForLobby);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(RunningKey, false);
            ResetState();
        }
    }

    private static void Tick()
    {
        try
        {
            switch (phase)
            {
                case Phase.WaitingForLobby:
                    if (Elapsed >= 1.5d)
                        PrepareNotepadLayout();
                    break;

                case Phase.WaitingForLayout:
                    if (Elapsed >= 2.5d)
                    {
                        ValidateNotepadLayout();
                        PrepareComplaintReplacement();
                    }
                    break;

                case Phase.WaitingForReplacement:
                    ValidateReplacementWhenReady();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void PrepareNotepadLayout()
    {
        notepad = FindFirst<OrderChecklistUI>();
        Assert(notepad != null, "Lobby1 has no OrderChecklistUI.");
        notepad.gameObject.SetActive(true);

        Invoke(notepad, "ResolveUIReferences");
        Invoke(notepad, "RefreshResponsiveLayout");
        Invoke(notepad, "RebuildMenu");

        RectTransform requestedRoot = GetField<RectTransform>(notepad, "requestedIconsRoot");
        List<CustomerGroup.OrderLine> requestedLines =
            GetField<List<CustomerGroup.OrderLine>>(notepad, "requestedOrderLines");
        requestedLines.Clear();

        MenuCatalog catalog = MenuCatalog.Default;
        Assert(catalog != null, "MenuCatalog is unavailable.");
        List<Recipe> products = catalog.GetProducts(MenuProductCategory.Food, false);
        products.AddRange(catalog.GetProducts(MenuProductCategory.Drink, false));
        int requestedCount = Mathf.Min(6, products.Count);
        for (int i = 0; i < requestedCount; i++)
        {
            CustomerGroup.OrderLine line = new CustomerGroup.OrderLine();
            line.SetProduct(products[i], i % 2 == 0 ? 2 : 1);
            requestedLines.Add(line);
        }

        Invoke(notepad, "RebuildRequestedIcons");
        LayoutRebuilder.ForceRebuildLayoutImmediate(requestedRoot);
        Canvas.ForceUpdateCanvases();
        SetPhase(Phase.WaitingForLayout);
    }

    private static void ValidateNotepadLayout()
    {
        TMP_Text message = GetField<TMP_Text>(notepad, "customerMessageText");
        RectTransform customerArea =
            GetField<RectTransform>(notepad, "customerInformationRoot");
        RectTransform requested = GetField<RectTransform>(notepad, "requestedIconsRoot");
        RectTransform availability = GetField<RectTransform>(notepad, "availableItemsRoot");
        ScrollRect foodScroll = GetField<ScrollRect>(notepad, "foodScrollRect");
        ScrollRect drinkScroll = GetField<ScrollRect>(notepad, "drinkScrollRect");
        Image customerImage = GetField<Image>(notepad, "customerImage");
        Button foodTab = GetField<Button>(notepad, "foodTabButton");
        Button drinkTab = GetField<Button>(notepad, "drinkTabButton");

        ValidateMenuGrid(foodScroll, "1. Food is not arranged as three columns by two rows.");
        Pass();
        ValidateMenuGrid(drinkScroll, "2. Drinks are not arranged as three columns by two rows.");
        Pass();

        bool foodSixFit = FirstSixCardsFit(foodScroll);
        bool drinkSixFit = FirstSixCardsFit(drinkScroll);
        Assert(foodSixFit && drinkSixFit,
            "3. All six Casual Dining items do not fit in their viewport at once. " +
            $"Food: {DescribeFirstSixCards(foodScroll)}; Drinks: {DescribeFirstSixCards(drinkScroll)}.");
        Pass();

        NotepadMenuEntryUI firstCard = GetFirstActiveCard(foodScroll.content);
        RectTransform firstCardRect = firstCard != null
            ? firstCard.transform as RectTransform
            : null;
        Assert(firstCardRect != null && firstCardRect.rect.height >= 280f,
            "4. Menu cards are still too short or cramped.");
        Pass();

        RectTransform cardIcons = GetPrivateField<RectTransform>(firstCard, "iconRoot");
        Image cardIcon = cardIcons != null ? cardIcons.GetComponentInChildren<Image>(true) : null;
        Assert(cardIcon != null && cardIcon.rectTransform.rect.width >= 90f,
            "5. Menu-card product images are not clearly visible.");
        Pass();

        Button minus = GetPrivateField<Button>(firstCard, "decreaseButton");
        Button plus = GetPrivateField<Button>(firstCard, "increaseButton");
        TMP_Text cardQuantity = GetPrivateField<TMP_Text>(firstCard, "quantityText");
        TMP_Text cardName = GetPrivateField<TMP_Text>(firstCard, "nameText");
        TMP_Text cardPrice = GetPrivateField<TMP_Text>(firstCard, "priceText");
        Assert(IsContained(minus.transform as RectTransform, firstCardRect) &&
               IsContained(cardQuantity.rectTransform, firstCardRect) &&
               IsContained(plus.transform as RectTransform, firstCardRect) &&
               !OverlapsInRoot(cardName.rectTransform, cardQuantity.rectTransform) &&
               !OverlapsInRoot(cardPrice.rectTransform, cardQuantity.rectTransform),
            "6. Quantity controls leave the card or collide with its name/price.");
        Pass();

        RectTransform tabRoot = foodTab.transform.parent as RectTransform;
        RectTransform menuViewport = foodScroll.transform as RectTransform;
        Bounds tabsBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            notepad.transform, tabRoot);
        Bounds menuBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            notepad.transform, menuViewport);
        float tabCenterDelta = Mathf.Abs(tabsBounds.center.x - menuBounds.center.x);
        float foodTabWidth = (foodTab.transform as RectTransform).rect.width;
        float drinkTabWidth = (drinkTab.transform as RectTransform).rect.width;
        float tabWidthDelta = Mathf.Abs(foodTabWidth - drinkTabWidth);
        Assert(tabRoot != null && tabRoot.GetComponent<HorizontalLayoutGroup>() != null &&
               tabCenterDelta <= 1f && tabWidthDelta <= 0.5f,
            $"7. Food/Drinks are not one centered, evenly sized tab group " +
            $"(center delta {tabCenterDelta:0.##}, widths {foodTabWidth:0.##}/{drinkTabWidth:0.##}).");
        Pass();

        Assert(customerArea != null && customerArea.anchoredPosition.x <= -360f &&
               customerImage.rectTransform.anchoredPosition.x <= -240f,
            "8. Customer information did not move into the left-side area.");
        Pass();

        message.text = "Don't keep us waiting and I'll give you a tip. Can we have 2 Pork Chop, 1 Roasted Chicken, and 2 Mango Juice Pitcher?";
        message.ForceMeshUpdate(true, true);
        Assert(!OverlapsInRoot(message.rectTransform, customerImage.rectTransform) &&
               !OverlapsInRoot(message.rectTransform, requested) &&
               !OverlapsInRoot(message.rectTransform, availability),
            "9. Customer message overlaps another customer-information element.");
        Pass();

        Assert(message.textWrappingMode == TextWrappingModes.Normal &&
               message.textInfo.lineCount >= 3 &&
               message.fontSize >= 14f &&
               !message.isTextOverflowing,
            "10. Long VIP messages do not wrap naturally at a readable size.");
        Pass();

        Image requestedIcon = requested.GetComponentInChildren<Image>(true);
        Assert(requestedIcon != null && requestedIcon.rectTransform.rect.width >= 60f,
            "11. Requested-order icons are not clearly paired with their quantity.");
        RectTransform requestedEntry = requestedIcon.rectTransform.parent as RectTransform;
        TMP_Text quantity = requestedEntry != null
            ? FindText(requestedEntry, "Quantity")
            : null;
        Assert(quantity != null && quantity.fontSize >= 21f &&
               quantity.text.StartsWith("x", StringComparison.Ordinal) &&
               IsContained(requestedIcon.rectTransform, requestedEntry) &&
               IsContained(quantity.rectTransform, requestedEntry),
            "11. Requested-order icons are not clearly paired with their quantity.");
        Pass();

        GridLayoutGroup availabilityGrid = availability.GetComponent<GridLayoutGroup>();
        TMP_Text availabilityText = availability.GetComponentInChildren<TMP_Text>(true);
        Assert(availabilityGrid != null && availabilityGrid.constraintCount == 2 &&
               availability.rect.width >= 680f &&
               availabilityText != null &&
               availabilityText.textWrappingMode == TextWrappingModes.Normal &&
               IsContained(availabilityText.rectTransform, availability) &&
               !OverlapsInRoot(availability, menuViewport),
            "12. Products Availability is not a readable, contained two-column list.");
        Pass();

        Bounds customerBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            notepad.transform, customerArea);
        Assert(Mathf.Abs(Mathf.Abs(customerBounds.center.x) -
                         Mathf.Abs(menuBounds.center.x)) <= 40f &&
               Mathf.Abs(customerBounds.size.x - menuBounds.size.x) <= 80f,
            "13. Customer and menu halves are not visually balanced.");
        Pass();
    }

    private static void PrepareComplaintReplacement()
    {
        MenuCatalog catalog = MenuCatalog.Default;
        List<Recipe> products = catalog.GetProducts(MenuProductCategory.Food);
        Assert(products.Count > 0, "No unlocked food is available for replacement validation.");
        Recipe product = products[0];

        previousOrderNumberManager = OrderNumberManager.Instance;
        GameObject numberObject = new GameObject("Regression Order Number Manager");
        testOrderNumberManager = numberObject.AddComponent<OrderNumberManager>();
        SetField(testOrderNumberManager, "nextOrderNumber", 1001);
        OrderNumberManager.Instance = testOrderNumberManager;

        GameObject boothObject = new GameObject("Regression Occupied Booth");
        complaintBooth = boothObject.AddComponent<Booth>();
        GameObject groupObject = new GameObject("Regression Complaint Group");
        complaintGroup = groupObject.AddComponent<CustomerGroup>();
        complaintBooth.SetCurrentGroup(complaintGroup);
        complaintGroup.assignedBooth = complaintBooth;
        complaintGroup.currentOrderNumber = 777;
        complaintGroup.currentOrder.SetProducts(
            new List<Recipe> { product }, product.DisplayName, product.EffectiveSellPrice);
        complaintGroup.submittedOrder.SetProducts(
            new List<Recipe> { product }, product.DisplayName, product.EffectiveSellPrice);
        complaintGroup.state = CustomerGroup.GroupState.Eating;
        SetField(complaintGroup, "managerComplaintPending", true);
        SetField(complaintGroup, "isOrderPaused", true);

        complaintGroup.ResolveManagerComplaint(
            ManagerComplaintResponseQuality.Professional,
            ManagerComplaintType.WrongOrder);
        Assert(complaintGroup.state == CustomerGroup.GroupState.ReadyToOrder &&
               complaintGroup.IsWaitingForRemake() &&
               complaintBooth.CurrentGroup == complaintGroup,
            "Complaint apology did not retain the occupied table and requested order.");

        bool retryFlag = GetPrivateField<bool>(complaintGroup, "managerComplaintRetryUsed");
        Assert(retryFlag, "The professional replacement was not marked as the final retry.");
        Assert(complaintGroup.BeginPlayerOrderReview(),
            "Replacement order could not enter normal player review.");
        Assert(complaintGroup.ConfirmPlayerReviewedOrder(
                   CustomerGroup.FoodType.Chicken,
                   CustomerGroup.DrinkType.Coke),
            "Replacement order could not be confirmed through the normal order path.");

        replacementOrderNumber = complaintGroup.currentOrderNumber;
        Assert(replacementOrderNumber != 777 && complaintGroup.HasConfirmedOrder &&
               complaintGroup.state == CustomerGroup.GroupState.OrderTaken,
            "14. Confirm Order did not preserve the corrected order flow.");
        Pass();

        GameObject kitchenObject = new GameObject("Regression Kitchen");
        kitchen = kitchenObject.AddComponent<KitchenManager>();
        SetField(kitchen, "preparationDelaySeconds", 0f);
        kitchen.cookSeconds = 0.05f;
        traySlot = new GameObject("Regression Tray Slot").transform;
        traySlot.SetParent(kitchenObject.transform, false);
        kitchen.traySpawnPoints = new[] { traySlot };
        GameObject trayObject = new GameObject("Regression Tray Template");
        trayTemplate = trayObject.AddComponent<FoodTray>();
        trayObject.SetActive(false);
        kitchen.foodTrayPrefab = trayTemplate;
        kitchen.OrderStarted += (_, _) => kitchenStartedCount++;

        HashSet<int> completed = GetPrivateField<HashSet<int>>(kitchen, "completedOrders");
        completed.Add(777);
        Assert(kitchen.ProcessOrder(complaintGroup),
            "The previously fixed replacement was rejected by the normal kitchen queue.");
        Assert(!kitchen.ProcessOrder(complaintGroup),
            "Replacement was accepted twice by the kitchen.");

        SetPhase(Phase.WaitingForReplacement);
    }

    private static void ValidateReplacementWhenReady()
    {
        FoodTray replacement = null;
        for (int i = 0; i < traySlot.childCount; i++)
        {
            FoodTray candidate = traySlot.GetChild(i).GetComponent<FoodTray>();
            if (candidate != null && candidate.TargetGroup == complaintGroup)
            {
                replacement = candidate;
                break;
            }
        }

        if (replacement == null)
        {
            if (Elapsed > 4d)
                throw new InvalidOperationException("Replacement tray never reached the kitchen pickup slot.");
            return;
        }

        Assert(kitchenStartedCount == 1 &&
               replacement.orderNumber == replacementOrderNumber &&
               replacement.TargetGroup == complaintGroup,
            "Replacement did not preserve its customer/order reference in the kitchen.");

        complaintGroup.ReceiveFoodFromWaiter(replacement.DeliveredContents, replacement);
        Assert(complaintGroup.state == CustomerGroup.GroupState.Eating,
            "Replacement could not be delivered to the complaining customer.");

        Assert(!complaintGroup.IsWaitingForRemake() && complaintGroup.HasConfirmedOrder,
            "Correct replacement delivery did not resolve the remake state.");

        Assert(complaintBooth.CurrentGroup == complaintGroup &&
               complaintGroup.assignedBooth == complaintBooth,
            "Customer/table reference was lost during replacement delivery.");

        ValidateNormalOrderUnaffected();
        ValidatePhoneLandscapeContainment();
        Pass();

        Assert(passedChecks == 15, $"Expected 15 checks, completed {passedChecks}.");
        Finish(true, "PASS — all 15 Notepad layout and confirmation checks passed.");
    }

    private static void ValidateNormalOrderUnaffected()
    {
        GameObject normalObject = new GameObject("Regression Normal Order Group");
        CustomerGroup normal = normalObject.AddComponent<CustomerGroup>();
        normal.currentOrderNumber = 55;
        normal.state = CustomerGroup.GroupState.ReadyToOrder;
        Assert(normal.BeginPlayerOrderReview(), "Normal order could not begin review.");
        Assert(normal.ConfirmPlayerReviewedOrder(
                   CustomerGroup.FoodType.Chicken,
                   CustomerGroup.DrinkType.Coke),
            "Normal order could not complete review.");
        Assert(normal.currentOrderNumber == 55 &&
               normal.state == CustomerGroup.GroupState.OrderTaken,
            "Normal order behavior changed while correcting the Notepad layout.");
        UnityEngine.Object.Destroy(normalObject);
    }

    private static void ValidateMenuGrid(ScrollRect scroll, string message)
    {
        Assert(scroll != null && scroll.content != null, message);
        RectTransform viewport = scroll.viewport != null
            ? scroll.viewport
            : scroll.transform as RectTransform;
        NotepadMenuEntryUI card = GetFirstActiveCard(scroll.content);
        Assert(viewport != null && card != null, message);
        GridLayoutGroup grid = scroll.content.GetComponent<GridLayoutGroup>();
        float cardHeight = (card.transform as RectTransform).rect.height;
        Assert(grid != null && grid.enabled &&
               grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount &&
               grid.constraintCount == 3 &&
               grid.startAxis == GridLayoutGroup.Axis.Horizontal &&
               viewport.rect.height + 1f >= cardHeight * 2f + 36f,
            $"{message} Viewport={viewport.rect.height:0.00}, card={cardHeight:0.00}.");
    }

    private static bool FirstSixCardsFit(ScrollRect scroll)
    {
        if (scroll == null || scroll.content == null)
            return false;

        RectTransform viewport = scroll.viewport != null
            ? scroll.viewport
            : scroll.transform as RectTransform;
        int count = 0;
        for (int i = 0; i < scroll.content.childCount && count < 6; i++)
        {
            RectTransform card = scroll.content.GetChild(i) as RectTransform;
            if (card == null || card.GetComponent<NotepadMenuEntryUI>() == null ||
                !card.gameObject.activeSelf)
                continue;
            if (!IsContained(card, viewport, 1f))
                return false;
            count++;
        }

        return count == 6;
    }

    private static string DescribeFirstSixCards(ScrollRect scroll)
    {
        if (scroll == null || scroll.content == null)
            return "missing scroll/content";

        RectTransform viewport = scroll.viewport != null
            ? scroll.viewport
            : scroll.transform as RectTransform;
        int activeCards = 0;
        int containedCards = 0;
        for (int i = 0; i < scroll.content.childCount; i++)
        {
            RectTransform card = scroll.content.GetChild(i) as RectTransform;
            if (card == null || card.GetComponent<NotepadMenuEntryUI>() == null ||
                !card.gameObject.activeSelf)
                continue;

            activeCards++;
            if (activeCards <= 6 && IsContained(card, viewport, 1f))
                containedCards++;
        }

        return $"active {activeCards}, first-six contained {containedCards}, " +
               $"viewport {viewport.rect.width:0.#}x{viewport.rect.height:0.#}, " +
               $"content {scroll.content.rect.width:0.#}x{scroll.content.rect.height:0.#}";
    }

    private static void ValidatePhoneLandscapeContainment()
    {
        RectTransform customerArea =
            GetField<RectTransform>(notepad, "customerInformationRoot");
        RectTransform requested = GetField<RectTransform>(notepad, "requestedIconsRoot");
        RectTransform availability = GetField<RectTransform>(notepad, "availableItemsRoot");
        TMP_Text message = GetField<TMP_Text>(notepad, "customerMessageText");
        Image portrait = GetField<Image>(notepad, "customerImage");
        ScrollRect foodScroll = GetField<ScrollRect>(notepad, "foodScrollRect");
        ScrollRect drinkScroll = GetField<ScrollRect>(notepad, "drinkScrollRect");

        Assert(IsContained(message.rectTransform, customerArea) &&
               IsContained(portrait.rectTransform, customerArea) &&
               IsContained(requested, customerArea) &&
               IsContained(availability, customerArea) &&
               FirstSixCardsFit(foodScroll) && FirstSixCardsFit(drinkScroll),
            "15. New clipping appears at PhoneLandscape/Android resolution.");
    }

    private static NotepadMenuEntryUI GetFirstActiveCard(RectTransform content)
    {
        if (content == null)
            return null;
        for (int i = 0; i < content.childCount; i++)
        {
            NotepadMenuEntryUI card = content.GetChild(i).GetComponent<NotepadMenuEntryUI>();
            if (card != null && card.gameObject.activeSelf)
                return card;
        }
        return null;
    }

    private static bool Overlaps(RectTransform left, RectTransform right)
    {
        if (left == null || right == null || left.parent == null || left.parent != right.parent)
            return false;
        Bounds a = RectTransformUtility.CalculateRelativeRectTransformBounds(left.parent, left);
        Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(right.parent, right);
        return a.Intersects(b);
    }

    private static bool OverlapsInRoot(RectTransform left, RectTransform right)
    {
        if (left == null || right == null || notepad == null)
            return false;
        Bounds a = RectTransformUtility.CalculateRelativeRectTransformBounds(
            notepad.transform, left);
        Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(
            notepad.transform, right);
        return a.Intersects(b);
    }

    private static bool IsContained(
        RectTransform child,
        RectTransform parent,
        float tolerance = 0.5f)
    {
        if (child == null || parent == null)
            return false;

        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, child);
        Rect rect = parent.rect;
        return bounds.min.x >= rect.xMin - tolerance &&
               bounds.max.x <= rect.xMax + tolerance &&
               bounds.min.y >= rect.yMin - tolerance &&
               bounds.max.y <= rect.yMax + tolerance;
    }

    private static TMP_Text FindText(RectTransform root, string objectName)
    {
        TMP_Text[] texts = root != null
            ? root.GetComponentsInChildren<TMP_Text>(true)
            : Array.Empty<TMP_Text>();
        for (int i = 0; i < texts.Length; i++)
            if (texts[i].gameObject.name == objectName)
                return texts[i];
        return null;
    }

    private static T FindFirst<T>() where T : UnityEngine.Object
    {
        T[] values = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        return values.Length > 0 ? values[0] : null;
    }

    private static void SetPhase(Phase next)
    {
        phase = next;
        phaseStartedAt = EditorApplication.timeSinceStartup;
    }

    private static double Elapsed => EditorApplication.timeSinceStartup - phaseStartedAt;

    private static void Pass() => passedChecks++;

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Invoke(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (method == null)
            throw new MissingMethodException(target.GetType().Name, methodName);
        method.Invoke(target, null);
    }

    private static T GetField<T>(object target, string fieldName)
    {
        return GetPrivateField<T>(target, fieldName);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        return (T)field.GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    private static void Fail(Exception exception)
    {
        string message = exception is TargetInvocationException invocation &&
                         invocation.InnerException != null
            ? invocation.InnerException.ToString()
            : exception.ToString();
        Finish(false, "FAIL — " + message);
    }

    private static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Tick;
        WriteResult(message);
        if (passed)
            Debug.Log("[NotepadComplaintRegressionTest] " + message);
        else
            Debug.LogError("[NotepadComplaintRegressionTest] " + message);

        CleanupRuntimeObjects();
        if (GameSaveManager.Instance != null)
            GameSaveManager.Instance.SuppressWritesForTests = previousSaveWriteSuppression;
        EditorApplication.ExitPlaymode();
    }

    private static void CleanupRuntimeObjects()
    {
        if (notepad != null)
            notepad.gameObject.SetActive(false);
        if (previousOrderNumberManager != null)
            OrderNumberManager.Instance = previousOrderNumberManager;

        DestroyObject(testOrderNumberManager != null ? testOrderNumberManager.gameObject : null);
        DestroyObject(kitchen != null ? kitchen.gameObject : null);
        DestroyObject(trayTemplate != null ? trayTemplate.gameObject : null);
        DestroyObject(complaintGroup != null ? complaintGroup.gameObject : null);
        DestroyObject(complaintBooth != null ? complaintBooth.gameObject : null);
    }

    private static void DestroyObject(GameObject value)
    {
        if (value != null)
            UnityEngine.Object.Destroy(value);
    }

    private static void ResetState()
    {
        phase = Phase.None;
        phaseStartedAt = 0d;
        passedChecks = 0;
        notepad = null;
        complaintGroup = null;
        complaintBooth = null;
        kitchen = null;
        traySlot = null;
        trayTemplate = null;
        previousOrderNumberManager = null;
        testOrderNumberManager = null;
        replacementOrderNumber = -1;
        kitchenStartedCount = 0;
        previousSaveWriteSuppression = false;
    }

    private static void WriteResult(string value)
    {
        Directory.CreateDirectory(Path.Combine(ProjectRoot, "Temp"));
        File.WriteAllText(Path.Combine(ProjectRoot, "Temp", ResultFileName), value);
    }

    private static string ProjectRoot =>
        Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
}
// Keep the request-file entry point domain-reload safe for unattended validation.
#endif
