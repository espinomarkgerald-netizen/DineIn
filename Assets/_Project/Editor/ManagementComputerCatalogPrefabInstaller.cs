#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates only missing catalog assets. It deliberately never rebuilds an
/// existing prefab, so designer changes made in the Inspector remain authored.
/// </summary>
public static class ManagementComputerCatalogPrefabInstaller
{
    private const string PrefabFolder = "Assets/_Project/ManagementComputer/Prefabs";
    private const string CardPath = PrefabFolder + "/ManagementComputerCatalogCard.prefab";
    private const string LinePath = PrefabFolder + "/ManagementComputerCheckoutLine.prefab";
    private const string PanelPath = PrefabFolder + "/ManagementComputerCatalogPanel.prefab";
    private const string StoragePath = "Assets/_Project/Resources/CasualDiningStorageConfig.asset";
    private const string ConfigPath = "Assets/_Project/Resources/ManagementComputerCatalogUIConfig.asset";

    private static readonly Color Navy = new Color(0.035f, 0.18f, 0.34f, 1f);
    private static readonly Color Blue = new Color(0.04f, 0.61f, 0.86f, 1f);
    private static readonly Color PaleBlue = new Color(0.88f, 0.96f, 1f, 1f);
    private static readonly Color Panel = new Color(0.96f, 0.965f, 0.985f, 1f);
    private static readonly Color Ink = new Color(0.12f, 0.22f, 0.31f, 1f);
    private static readonly Color Green = new Color(0.22f, 0.72f, 0.28f, 1f);
    private static readonly Color Red = new Color(0.88f, 0.22f, 0.20f, 1f);

    [InitializeOnLoadMethod]
    private static void ScheduleMissingAssetCreation()
    {
        EditorApplication.delayCall += EnsureMissingAssets;
    }

    [MenuItem("Tools/Dine In/Create Missing Management Catalog Prefabs")]
    public static void EnsureMissingAssets()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Directory.CreateDirectory(PrefabFolder);
        Directory.CreateDirectory("Assets/_Project/Resources");

        ManagementComputerCatalogCardUI card = EnsureCardPrefab();
        ManagementComputerCheckoutLineUI line = EnsureCheckoutLinePrefab();
        ManagementComputerCatalogPanelUI panel = EnsurePanelPrefab(card, line);
        UpgradeCheckoutLineLayout();
        UpgradePanelPriceLayout();
        RestaurantStorageConfig storage = EnsureStorageConfig();
        EnsureUIConfig(panel, storage);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static ManagementComputerCatalogCardUI EnsureCardPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(CardPath);
        if (existing != null)
            return existing.GetComponent<ManagementComputerCatalogCardUI>();

        GameObject root = CreateUIObject("ManagementComputerCatalogCard", null);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(250f, 300f);
        Image background = AddImage(root, new Color(0.96f, 0.97f, 1f, 1f));
        Button cardButton = root.AddComponent<Button>();
        cardButton.targetGraphic = background;
        cardButton.navigation = new Navigation { mode = Navigation.Mode.None };
        LayoutElement cardLayout = root.AddComponent<LayoutElement>();
        cardLayout.minWidth = 204f;
        cardLayout.preferredWidth = 250f;
        cardLayout.minHeight = 248f;
        cardLayout.preferredHeight = 300f;

        Image icon = CreateImage("Icon", root.transform, null, Color.white);
        SetTopFixed(icon.rectTransform, 0.5f, 12f, 76f, 76f);

        TMP_Text title = CreateText("Name", root.transform, "ITEM NAME", 18f, FontStyles.Bold, Ink);
        SetTopStretch(title.rectTransform, 10f, 10f, 88f, 31f);
        title.alignment = TextAlignmentOptions.Center;

        TMP_Text meta = CreateText("Meta", root.transform, "12 units • Dry", 13f, FontStyles.Normal, Ink);
        SetTopStretch(meta.rectTransform, 10f, 10f, 119f, 23f);
        meta.alignment = TextAlignmentOptions.Center;

        TMP_Text status = CreateText("Status", root.transform, "OUT OF STOCK\nExpires Day 8", 13f, FontStyles.Normal, Ink);
        SetTopStretch(status.rectTransform, 10f, 10f, 143f, 49f);
        status.alignment = TextAlignmentOptions.Center;
        status.textWrappingMode = TextWrappingModes.Normal;

        TMP_Text price = CreateText("Price", root.transform, "₱100 / box", 17f, FontStyles.Bold, Blue);
        SetTopStretch(price.rectTransform, 10f, 10f, 190f, 28f);
        price.alignment = TextAlignmentOptions.Center;

        GameObject quantityRoot = CreateUIObject("QuantityControls", root.transform);
        RectTransform quantityRect = quantityRoot.GetComponent<RectTransform>();
        SetBottomStretch(quantityRect, 8f, 8f, 6f, 48f);
        AddImage(quantityRoot, new Color(0.83f, 0.94f, 1f, 1f));

        Button minus = CreateButton("Minus", quantityRoot.transform, "−", Navy, Color.white, 20f);
        SetLeftFixed(minus.transform as RectTransform, 2f, 2f, 44f, 44f);
        Button plus = CreateButton("Plus", quantityRoot.transform, "+", Navy, Color.white, 20f);
        SetRightFixed(plus.transform as RectTransform, 2f, 2f, 44f, 44f);
        TMP_Text quantity = CreateText("Quantity", quantityRoot.transform, "0", 19f, FontStyles.Bold, Ink);
        Stretch(quantity.rectTransform, 50f, 50f, 1f, 1f);
        quantity.alignment = TextAlignmentOptions.Center;

        ManagementComputerCatalogCardUI component =
            root.AddComponent<ManagementComputerCatalogCardUI>();
        component.ConfigureReferences(
            background,
            cardButton,
            icon,
            title,
            meta,
            status,
            price,
            quantityRoot,
            minus,
            plus,
            quantity);

        PrefabUtility.SaveAsPrefabAsset(root, CardPath);
        Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(CardPath)
            .GetComponent<ManagementComputerCatalogCardUI>();
    }

    private static ManagementComputerCheckoutLineUI EnsureCheckoutLinePrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(LinePath);
        if (existing != null)
            return existing.GetComponent<ManagementComputerCheckoutLineUI>();

        GameObject root = CreateUIObject("ManagementComputerCheckoutLine", null);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300f, 92f);
        AddImage(root, Color.white);
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.minHeight = 88f;
        layout.preferredHeight = 92f;
        layout.flexibleWidth = 1f;

        Image icon = CreateImage("Icon", root.transform, null, Color.white);
        SetLeftFixed(icon.rectTransform, 8f, 10f, 58f, 58f);

        TMP_Text name = CreateText("Name", root.transform, "ITEM", 15f, FontStyles.Bold, Ink);
        SetTopStretch(name.rectTransform, 74f, 108f, 8f, 48f);
        name.alignment = TextAlignmentOptions.TopLeft;
        name.textWrappingMode = TextWrappingModes.Normal;

        TMP_Text total = CreateText("Total", root.transform, "₱0", 15f, FontStyles.Bold, Red);
        SetBottomStretch(total.rectTransform, 74f, 108f, 7f, 24f);
        total.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject controls = CreateUIObject("QuantityControls", root.transform);
        RectTransform controlsRect = controls.GetComponent<RectTransform>();
        SetRightFixed(controlsRect, 7f, 7f, 96f, 64f);
        Button minus = CreateButton("Minus", controls.transform, "−", Navy, Color.white, 17f);
        SetLeftFixed(minus.transform as RectTransform, 0f, 6f, 36f, 50f);
        Button plus = CreateButton("Plus", controls.transform, "+", Navy, Color.white, 17f);
        SetRightFixed(plus.transform as RectTransform, 0f, 6f, 36f, 50f);
        TMP_Text quantity = CreateText("Quantity", controls.transform, "1", 16f, FontStyles.Bold, Ink);
        Stretch(quantity.rectTransform, 37f, 37f, 4f, 4f);
        quantity.alignment = TextAlignmentOptions.Center;

        ManagementComputerCheckoutLineUI component =
            root.AddComponent<ManagementComputerCheckoutLineUI>();
        component.ConfigureReferences(icon, name, total, controls, minus, plus, quantity);

        PrefabUtility.SaveAsPrefabAsset(root, LinePath);
        Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(LinePath)
            .GetComponent<ManagementComputerCheckoutLineUI>();
    }

    private static ManagementComputerCatalogPanelUI EnsurePanelPrefab(
        ManagementComputerCatalogCardUI cardPrefab,
        ManagementComputerCheckoutLineUI linePrefab)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
        if (existing != null)
            return existing.GetComponent<ManagementComputerCatalogPanelUI>();

        GameObject root = CreateUIObject("ManagementComputerCatalogPanel", null);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(1000f, 620f);
        HorizontalLayoutGroup split = root.AddComponent<HorizontalLayoutGroup>();
        split.padding = new RectOffset(8, 8, 8, 8);
        split.spacing = 12f;
        split.childAlignment = TextAnchor.UpperLeft;
        split.childControlWidth = true;
        split.childControlHeight = true;
        split.childForceExpandWidth = false;
        split.childForceExpandHeight = true;

        GameObject catalogRoot = CreateUIObject("Catalog", root.transform);
        AddImage(catalogRoot, PaleBlue);
        LayoutElement catalogLayout = catalogRoot.AddComponent<LayoutElement>();
        catalogLayout.minWidth = 300f;
        catalogLayout.flexibleWidth = 1f;

        TMP_Text context = CreateText(
            "Context",
            catalogRoot.transform,
            "Expected visitors and catalog guidance",
            16f,
            FontStyles.Bold,
            Ink);
        SetTopStretch(context.rectTransform, 16f, 16f, 10f, 50f);
        context.alignment = TextAlignmentOptions.MidlineLeft;
        context.textWrappingMode = TextWrappingModes.Normal;

        ScrollRect catalogScroll = CreateScrollView(
            "CatalogScroll",
            catalogRoot.transform,
            out RectTransform cardContent,
            out _);
        Stretch(catalogScroll.transform as RectTransform, 10f, 10f, 10f, 66f);
        GridLayoutGroup grid = cardContent.gameObject.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.spacing = new Vector2(12f, 12f);
        grid.cellSize = new Vector2(250f, 300f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        ContentSizeFitter cardFitter = cardContent.gameObject.AddComponent<ContentSizeFitter>();
        cardFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        cardFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject rightRail = CreateUIObject("DetailsAndCheckout", root.transform);
        AddImage(rightRail, Panel);
        LayoutElement rightLayout = rightRail.AddComponent<LayoutElement>();
        rightLayout.minWidth = 250f;
        rightLayout.preferredWidth = 320f;
        rightLayout.flexibleWidth = 0f;

        TMP_Text rightHeader = CreateText(
            "Header",
            rightRail.transform,
            "Shopping Cart",
            24f,
            FontStyles.Bold,
            Ink);
        SetTopStretch(rightHeader.rectTransform, 12f, 12f, 8f, 42f);
        rightHeader.alignment = TextAlignmentOptions.Center;

        TMP_Text rightMessage = CreateText(
            "Message",
            rightRail.transform,
            string.Empty,
            13f,
            FontStyles.Normal,
            Ink);
        SetBottomStretch(rightMessage.rectTransform, 12f, 12f, 69f, 42f);
        rightMessage.alignment = TextAlignmentOptions.Center;
        rightMessage.textWrappingMode = TextWrappingModes.Normal;

        GameObject menuRoot = CreateUIObject("MenuDetails", rightRail.transform);
        Stretch(menuRoot.GetComponent<RectTransform>(), 0f, 0f, 0f, 50f);
        Image menuIcon = CreateImage("MenuIcon", menuRoot.transform, null, Color.white);
        SetTopFixed(menuIcon.rectTransform, 0.5f, 8f, 80f, 80f);

        TMP_Text menuName = CreateText("MenuName", menuRoot.transform, "Menu Item", 21f, FontStyles.Bold, Ink);
        SetTopStretch(menuName.rectTransform, 12f, 12f, 91f, 37f);
        menuName.alignment = TextAlignmentOptions.Center;

        TMP_Text menuDescription = CreateText(
            "MenuDescription",
            menuRoot.transform,
            "Description",
            14f,
            FontStyles.Normal,
            Ink);
        SetTopStretch(menuDescription.rectTransform, 14f, 14f, 130f, 64f);
        menuDescription.alignment = TextAlignmentOptions.TopLeft;
        menuDescription.textWrappingMode = TextWrappingModes.Normal;

        TMP_Text priceLabel = CreateText("PriceLabel", menuRoot.transform, "PRICE", 14f, FontStyles.Bold, Ink);
        SetTopStretch(priceLabel.rectTransform, 14f, 14f, 198f, 24f);
        priceLabel.alignment = TextAlignmentOptions.MidlineLeft;

        TMP_InputField priceInput = CreateInput("PriceInput", menuRoot.transform, "0");
        RectTransform inputRect = priceInput.transform as RectTransform;
        inputRect.anchorMin = new Vector2(0f, 1f);
        inputRect.anchorMax = new Vector2(1f, 1f);
        inputRect.pivot = new Vector2(0.5f, 1f);
        inputRect.anchoredPosition = new Vector2(-49f, -226f);
        inputRect.sizeDelta = new Vector2(-126f, 48f);

        Button savePrice = CreateButton("SavePrice", menuRoot.transform, "SAVE", Blue, Color.white, 15f);
        SetTopRight(savePrice.transform as RectTransform, 12f, 226f, 92f, 48f);

        TMP_Text ingredientsLabel = CreateText(
            "IngredientsLabel",
            menuRoot.transform,
            "INGREDIENTS",
            15f,
            FontStyles.Bold,
            Ink);
        SetTopStretch(ingredientsLabel.rectTransform, 14f, 14f, 282f, 27f);
        ingredientsLabel.alignment = TextAlignmentOptions.MidlineLeft;

        ScrollRect ingredientScroll = CreateScrollView(
            "IngredientScroll",
            menuRoot.transform,
            out RectTransform ingredientContent,
            out _);
        Stretch(ingredientScroll.transform as RectTransform, 12f, 12f, 178f, 310f);
        AddVerticalContentLayout(ingredientContent, 6f, 6);

        Button availability = CreateButton(
            "MenuAvailability",
            menuRoot.transform,
            "REMOVE FROM MENU",
            Blue,
            Color.white,
            15f);
        SetBottomStretch(availability.transform as RectTransform, 12f, 12f, 115f, 48f);
        TMP_Text availabilityLabel = availability.GetComponentInChildren<TMP_Text>();

        GameObject restockRoot = CreateUIObject("RestockCart", rightRail.transform);
        Stretch(restockRoot.GetComponent<RectTransform>(), 0f, 0f, 0f, 50f);
        ScrollRect cartScroll = CreateScrollView(
            "CartScroll",
            restockRoot.transform,
            out RectTransform cartContent,
            out _);
        Stretch(cartScroll.transform as RectTransform, 10f, 10f, 205f, 8f);
        AddVerticalContentLayout(cartContent, 7f, 6);

        TMP_Text summary = CreateText(
            "CartSummary",
            restockRoot.transform,
            "Boxes: 0\nDry after order: 0 / 24\nFrozen after order: 0 / 20\nTOTAL: ₱0",
            15f,
            FontStyles.Bold,
            Ink);
        SetBottomStretch(summary.rectTransform, 12f, 12f, 116f, 82f);
        summary.alignment = TextAlignmentOptions.MidlineLeft;
        summary.textWrappingMode = TextWrappingModes.Normal;

        Button primary = CreateButton("Primary", restockRoot.transform, "CHECKOUT", Green, Color.white, 18f);
        RectTransform primaryRect = primary.transform as RectTransform;
        primaryRect.anchorMin = new Vector2(0f, 0f);
        primaryRect.anchorMax = new Vector2(0.68f, 0f);
        primaryRect.pivot = new Vector2(0.5f, 0f);
        primaryRect.anchoredPosition = new Vector2(6f, 10f);
        primaryRect.sizeDelta = new Vector2(-20f, 54f);
        TMP_Text primaryLabel = primary.GetComponentInChildren<TMP_Text>();

        Button secondary = CreateButton("Secondary", restockRoot.transform, "CLEAR", Red, Color.white, 17f);
        RectTransform secondaryRect = secondary.transform as RectTransform;
        secondaryRect.anchorMin = new Vector2(0.68f, 0f);
        secondaryRect.anchorMax = new Vector2(1f, 0f);
        secondaryRect.pivot = new Vector2(0.5f, 0f);
        secondaryRect.anchoredPosition = new Vector2(-6f, 10f);
        secondaryRect.sizeDelta = new Vector2(-20f, 54f);
        TMP_Text secondaryLabel = secondary.GetComponentInChildren<TMP_Text>();

        menuRoot.SetActive(true);
        restockRoot.SetActive(false);

        ManagementComputerCatalogPanelUI component =
            root.AddComponent<ManagementComputerCatalogPanelUI>();
        component.ConfigureReferences(
            context,
            catalogScroll,
            cardContent,
            grid,
            cardPrefab,
            rightLayout,
            rightHeader,
            rightMessage,
            menuRoot,
            menuIcon,
            menuName,
            menuDescription,
            priceInput,
            savePrice,
            availability,
            availabilityLabel,
            ingredientContent,
            restockRoot,
            cartContent,
            summary,
            primary,
            primaryLabel,
            secondary,
            secondaryLabel,
            linePrefab);

        PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
        Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath)
            .GetComponent<ManagementComputerCatalogPanelUI>();
    }

    /// <summary>
    /// One-time migration for the original short cart row. The height check is
    /// the guard: once migrated, later designer edits are never overwritten.
    /// </summary>
    private static void UpgradeCheckoutLineLayout()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(LinePath);
        if (contents == null)
            return;

        try
        {
            RectTransform root = contents.transform as RectTransform;
            if (root == null || root.sizeDelta.y >= 80f)
                return;

            root.sizeDelta = new Vector2(root.sizeDelta.x, 92f);
            LayoutElement layout = contents.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.minHeight = 88f;
                layout.preferredHeight = 92f;
            }

            RectTransform icon = contents.transform.Find("Icon") as RectTransform;
            RectTransform name = contents.transform.Find("Name") as RectTransform;
            RectTransform total = contents.transform.Find("Total") as RectTransform;
            RectTransform controls = contents.transform.Find("QuantityControls") as RectTransform;
            if (icon != null) SetLeftFixed(icon, 8f, 10f, 58f, 58f);
            if (name != null)
            {
                SetTopStretch(name, 74f, 108f, 8f, 48f);
                TMP_Text text = name.GetComponent<TMP_Text>();
                if (text != null)
                {
                    text.alignment = TextAlignmentOptions.TopLeft;
                    text.textWrappingMode = TextWrappingModes.Normal;
                }
            }
            if (total != null)
            {
                SetBottomStretch(total, 74f, 108f, 7f, 24f);
                TMP_Text text = total.GetComponent<TMP_Text>();
                if (text != null)
                    text.alignment = TextAlignmentOptions.MidlineLeft;
            }
            if (controls != null)
            {
                SetRightFixed(controls, 7f, 7f, 96f, 64f);
                RectTransform minus = controls.Find("Minus") as RectTransform;
                RectTransform plus = controls.Find("Plus") as RectTransform;
                RectTransform quantity = controls.Find("Quantity") as RectTransform;
                if (minus != null) SetLeftFixed(minus, 0f, 6f, 36f, 50f);
                if (plus != null) SetRightFixed(plus, 0f, 6f, 36f, 50f);
                if (quantity != null) Stretch(quantity, 37f, 37f, 4f, 4f);
            }

            PrefabUtility.SaveAsPrefabAsset(contents, LinePath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    /// <summary>
    /// One-time migration for the clipped PRICE row. Its legacy 52px label is
    /// used as the guard so the Inspector remains authoritative afterwards.
    /// </summary>
    private static void UpgradePanelPriceLayout()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PanelPath);
        if (contents == null)
            return;

        try
        {
            Transform menuRoot = contents.transform.Find("DetailsAndCheckout/MenuDetails");
            RectTransform priceLabel = menuRoot?.Find("PriceLabel") as RectTransform;
            if (priceLabel == null || priceLabel.sizeDelta.x > 60f)
                return;

            RectTransform priceInput = menuRoot.Find("PriceInput") as RectTransform;
            RectTransform savePrice = menuRoot.Find("SavePrice") as RectTransform;
            RectTransform ingredientsLabel = menuRoot.Find("IngredientsLabel") as RectTransform;
            RectTransform ingredientScroll = menuRoot.Find("IngredientScroll") as RectTransform;

            SetTopStretch(priceLabel, 14f, 14f, 198f, 24f);
            if (priceInput != null) SetTopStretch(priceInput, 14f, 112f, 226f, 48f);
            if (savePrice != null) SetTopRight(savePrice, 12f, 226f, 92f, 48f);
            if (ingredientsLabel != null) SetTopStretch(ingredientsLabel, 14f, 14f, 282f, 27f);
            if (ingredientScroll != null) Stretch(ingredientScroll, 12f, 12f, 178f, 310f);

            PrefabUtility.SaveAsPrefabAsset(contents, PanelPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static RestaurantStorageConfig EnsureStorageConfig()
    {
        RestaurantStorageConfig storage =
            AssetDatabase.LoadAssetAtPath<RestaurantStorageConfig>(StoragePath);
        if (storage != null)
            return storage;

        storage = ScriptableObject.CreateInstance<RestaurantStorageConfig>();
        AssetDatabase.CreateAsset(storage, StoragePath);
        return storage;
    }

    private static void EnsureUIConfig(
        ManagementComputerCatalogPanelUI panel,
        RestaurantStorageConfig storage)
    {
        ManagementComputerCatalogUIConfig config =
            AssetDatabase.LoadAssetAtPath<ManagementComputerCatalogUIConfig>(ConfigPath);
        if (config != null)
            return;

        config = ScriptableObject.CreateInstance<ManagementComputerCatalogUIConfig>();
        config.EditorConfigure(panel, storage);
        AssetDatabase.CreateAsset(config, ConfigPath);
        EditorUtility.SetDirty(config);
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            go.transform.SetParent(parent, false);
        return go;
    }

    private static Image AddImage(GameObject go, Color color)
    {
        Image image = go.GetComponent<Image>();
        if (image == null)
            image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = CreateUIObject(name, parent);
        Image image = AddImage(go, color);
        image.sprite = sprite;
        image.preserveAspect = true;
        return image;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string value,
        float fontSize,
        FontStyles style,
        Color color)
    {
        GameObject go = CreateUIObject(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(10f, fontSize - 5f);
        text.fontSizeMax = fontSize;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        string label,
        Color background,
        Color foreground,
        float fontSize)
    {
        GameObject go = CreateUIObject(name, parent);
        Image image = AddImage(go, background);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.78f, 0.88f, 0.96f, 1f);
        colors.disabledColor = new Color(0.65f, 0.68f, 0.72f, 0.75f);
        button.colors = colors;

        TMP_Text text = CreateText("Label", go.transform, label, fontSize, FontStyles.Bold, foreground);
        Stretch(text.rectTransform, 6f, 6f, 4f, 4f);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static TMP_InputField CreateInput(string name, Transform parent, string value)
    {
        GameObject root = CreateUIObject(name, parent);
        AddImage(root, Color.white);
        TMP_InputField input = root.AddComponent<TMP_InputField>();
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.text = value;

        GameObject area = CreateUIObject("Text Area", root.transform);
        RectTransform areaRect = area.GetComponent<RectTransform>();
        Stretch(areaRect, 8f, 8f, 4f, 4f);
        area.AddComponent<RectMask2D>();
        TMP_Text placeholder = CreateText("Placeholder", area.transform, "Price", 16f, FontStyles.Normal, new Color(0.4f, 0.45f, 0.5f, 0.65f));
        Stretch(placeholder.rectTransform, 2f, 2f, 0f, 0f);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        TMP_Text text = CreateText("Text", area.transform, value, 18f, FontStyles.Bold, Ink);
        Stretch(text.rectTransform, 2f, 2f, 0f, 0f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = true;
        input.textViewport = areaRect;
        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    private static ScrollRect CreateScrollView(
        string name,
        Transform parent,
        out RectTransform content,
        out RectTransform viewport)
    {
        GameObject root = CreateUIObject(name, parent);
        AddImage(root, new Color(1f, 1f, 1f, 0.45f));
        ScrollRect scroll = root.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        GameObject viewportObject = CreateUIObject("Viewport", root.transform);
        viewport = viewportObject.GetComponent<RectTransform>();
        Stretch(viewport, 4f, 4f, 4f, 4f);
        AddImage(viewportObject, new Color(1f, 1f, 1f, 0.01f));
        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = CreateUIObject("Content", viewportObject.transform);
        content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        scroll.viewport = viewport;
        scroll.content = content;
        return scroll;
    }

    private static void AddVerticalContentLayout(RectTransform content, float spacing, int padding)
    {
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static void Stretch(RectTransform rect, float left, float right, float bottom, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetTopStretch(RectTransform rect, float left, float right, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2((left - right) * 0.5f, -top);
        rect.sizeDelta = new Vector2(-(left + right), height);
    }

    private static void SetBottomStretch(RectTransform rect, float left, float right, float bottom, float height)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2((left - right) * 0.5f, bottom);
        rect.sizeDelta = new Vector2(-(left + right), height);
    }

    private static void SetTopFixed(RectTransform rect, float anchorX, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(anchorX, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetTopLeft(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetTopRight(RectTransform rect, float right, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-right, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetLeftFixed(RectTransform rect, float left, float verticalMargin, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(left, 0f);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetRightFixed(RectTransform rect, float right, float verticalMargin, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-right, 0f);
        rect.sizeDelta = new Vector2(width, height);
    }
}
#endif
