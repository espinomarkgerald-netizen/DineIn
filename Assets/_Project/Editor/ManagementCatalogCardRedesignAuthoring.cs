#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Idempotent prefab migration for the shared Equipment/Menu/Restock card family.
/// It changes authored visuals only; runtime catalog and purchasing logic remain intact.
/// </summary>
[InitializeOnLoad]
public static class ManagementCatalogCardRedesignAuthoring
{
    private const string EquipmentCardPath =
        "Assets/_Project/Resources/ManagementComputer/ManagementEquipmentCard.prefab";
    private const string CatalogCardPath =
        "Assets/_Project/ManagementComputer/Prefabs/ManagementComputerCatalogCard.prefab";
    private const string CatalogPanelPath =
        "Assets/_Project/ManagementComputer/Prefabs/ManagementComputerCatalogPanel.prefab";
    private const string NeutralRoundButtonPath =
        "Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/Grey/Double/button_round_gradient.png";

    private static readonly Color CardBlue = new Color(0.89f, 0.95f, 0.99f, 1f);
    private static readonly Color IconWhite = new Color(1f, 1f, 1f, 0.98f);
    private static readonly Color Navy = new Color(0.025f, 0.08f, 0.14f, 1f);
    private static readonly Color MutedInk = new Color(0.12f, 0.21f, 0.28f, 1f);
    private static readonly Color Blue = new Color(0.02f, 0.32f, 0.62f, 1f);
    private static readonly Color StockGreen = new Color(0.02f, 0.38f, 0.18f, 1f);
    private static readonly Color WarningRed = new Color(0.68f, 0.06f, 0.08f, 1f);
    private static readonly Color IncomingBlue = new Color(0.03f, 0.30f, 0.60f, 1f);
    private static readonly Color WarningAmber = new Color(0.60f, 0.32f, 0.02f, 1f);
    private static readonly Color BodyInk = new Color(0.035f, 0.10f, 0.17f, 1f);
    private static readonly Color ReadyBackground = new Color(0.82f, 0.94f, 0.85f, 1f);
    private static readonly Color LowBackground = new Color(1f, 0.84f, 0.86f, 1f);
    private static readonly Color WarningBackground = new Color(1f, 0.91f, 0.76f, 1f);
    private static readonly Color NeutralStatusBackground = new Color(0.86f, 0.90f, 0.93f, 1f);
    private static readonly Color SelectedCardBlue = new Color(0.76f, 0.90f, 0.98f, 1f);
    private static readonly Color LockedCardBlue = new Color(0.84f, 0.88f, 0.91f, 1f);

    static ManagementCatalogCardRedesignAuthoring()
    {
        EditorApplication.update += TryAutomaticUpgrade;
    }

    [MenuItem("Tools/Dine In/UI/Apply Unified Catalog Card Redesign %#F10")]
    public static void Apply()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        UpgradeCatalogCard();
        UpgradeGridSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ManagementCards] Menu and Restock card cleanup applied; Equipment unchanged.");
    }

    private static void TryAutomaticUpgrade()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EditorApplication.update -= TryAutomaticUpgrade;
        if (NeedsCatalogUpgrade())
            Apply();
    }

    private static bool NeedsCatalogUpgrade()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CatalogCardPath);
        if (prefab == null)
            return false;
        RectTransform rect = prefab.transform as RectTransform;
        Transform quantity = Find(prefab.transform, "QuantityControls");
        TMP_Text title = FindText(prefab.transform, "Name");
        TMP_Text meta = FindText(prefab.transform, "Meta");
        TMP_Text status = FindText(prefab.transform, "Status");
        TMP_Text price = FindText(prefab.transform, "Price");
        TMP_Text quantityText = FindText(prefab.transform, "Quantity");
        return prefab.GetComponent<ManagementItemCardFeedback>() == null ||
               prefab.transform.Find("Tooltip") == null ||
               Find(prefab.transform, "StatusBand") == null ||
               Find(prefab.transform, "RestockStats") == null ||
               prefab.transform.Find("InfoBadge") != null ||
               prefab.transform.Find("MenuActionBar") != null ||
               (quantity != null && quantity.GetComponent<Image>() != null) ||
               title == null || title.fontSizeMin < 17f || title.color.r > 0.04f ||
               meta == null || meta.fontSizeMin < 13f || meta.color.r > 0.13f ||
               status == null || status.fontSizeMin < 13.5f ||
               price == null || price.fontSizeMin < 16f || price.color.b > 0.7f ||
               quantityText == null || quantityText.fontSizeMin < 18f ||
               rect == null ||
               !Mathf.Approximately(rect.sizeDelta.y, 316f);
    }

    private static void UpgradeCatalogCard()
    {
        GameObject equipmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EquipmentCardPath);
        Image equipmentBackground = equipmentPrefab != null
            ? equipmentPrefab.GetComponent<Image>()
            : null;
        TMP_FontAsset equipmentFont = equipmentPrefab != null
            ? FindText(equipmentPrefab.transform, "Title")?.font
            : null;
        Sprite equipmentFrame = equipmentBackground != null ? equipmentBackground.sprite : null;
        Sprite neutralRoundButton = AssetDatabase.LoadAssetAtPath<Sprite>(NeutralRoundButtonPath);

        EditPrefab(CatalogCardPath, root =>
        {
            RectTransform rootRect = root.transform as RectTransform;
            rootRect.sizeDelta = new Vector2(248f, 316f);
            Image background = root.GetComponent<Image>();
            if (background != null)
            {
                background.color = CardBlue;
                if (equipmentFrame != null)
                    background.sprite = equipmentFrame;
                background.type = Image.Type.Sliced;
            }
            ConfigureLayout(root.GetComponent<LayoutElement>(), 220f, 248f, 316f);

            Shadow shadow = EnsureShadow(root);
            EnsureTopAccent(root.transform);
            Sprite cardFrame = equipmentFrame != null
                ? equipmentFrame
                : background != null ? background.sprite : null;
            Image highlight = EnsureHighlight(root.transform, cardFrame);
            Transform icon = Find(root.transform, "Icon");
            Transform iconPanel = Find(root.transform, "IconPanel");
            if (iconPanel == null)
            {
                GameObject panelObject = CreateUI("IconPanel", root.transform);
                iconPanel = panelObject.transform;
                Image panel = panelObject.AddComponent<Image>();
                panel.sprite = cardFrame;
                panel.type = Image.Type.Sliced;
                panel.color = IconWhite;
                panel.raycastTarget = false;
                if (icon != null)
                    panelObject.transform.SetSiblingIndex(icon.GetSiblingIndex());
            }
            Image iconPanelImage = iconPanel != null ? iconPanel.GetComponent<Image>() : null;
            if (iconPanelImage != null)
            {
                iconPanelImage.sprite = cardFrame;
                iconPanelImage.type = Image.Type.Sliced;
                iconPanelImage.color = IconWhite;
            }
            SetAnchors(iconPanel as RectTransform, new Vector2(0.055f, 0.60f),
                new Vector2(0.945f, 0.95f));
            if (icon != null)
            {
                icon.SetParent(iconPanel, false);
                SetAnchors(icon as RectTransform, new Vector2(0.1f, 0.09f),
                    new Vector2(0.9f, 0.91f));
            }

            CatalogModeParts modeParts = EnsureCatalogModeParts(root.transform, cardFrame);
            ConfigureText(FindText(root.transform, "Name"),
                new Vector2(0.055f, 0.50f), new Vector2(0.945f, 0.59f),
                Navy, 23f, 17f, FontStyles.Bold, TextAlignmentOptions.Center, false);
            ConfigureText(FindText(root.transform, "Meta"),
                new Vector2(0.07f, 0.445f), new Vector2(0.93f, 0.50f),
                MutedInk, 16f, 13f, FontStyles.Normal, TextAlignmentOptions.Center, false);
            ConfigureText(FindText(root.transform, "Status"),
                new Vector2(0.075f, 0.345f), new Vector2(0.925f, 0.415f),
                BodyInk, 16f, 13.5f, FontStyles.Bold, TextAlignmentOptions.Center, false);
            ConfigureText(FindText(root.transform, "Price"),
                new Vector2(0.075f, 0.135f), new Vector2(0.925f, 0.195f),
                Blue, 21f, 16f, FontStyles.Bold, TextAlignmentOptions.Center, false);
            ConfigureRestockQuantityControl(root.transform, neutralRoundButton);
            ConfigureButtonLabels(root.transform);
            ApplyFont(root.transform, equipmentFont);

            ManagementComputerCatalogCardUI card = root.GetComponent<ManagementComputerCatalogCardUI>();
            if (card != null)
            {
                SerializedObject serialized = new SerializedObject(card);
                SetColor(serialized, "normalColor", CardBlue);
                SetColor(serialized, "selectedColor", SelectedCardBlue);
                SetColor(serialized, "lockedColor", LockedCardBlue);
                SetColor(serialized, "primaryTextColor", Navy);
                SetColor(serialized, "secondaryTextColor", MutedInk);
                SetColor(serialized, "priceTextColor", Blue);
                SetColor(serialized, "stockAccentColor", StockGreen);
                SetColor(serialized, "expiryWarningColor", WarningRed);
                SetColor(serialized, "incomingAccentColor", IncomingBlue);
                SetColor(serialized, "warningAccentColor", WarningAmber);
                SetColor(serialized, "bodyTextColor", BodyInk);
                SetColor(serialized, "readyBackgroundColor", ReadyBackground);
                SetColor(serialized, "lowBackgroundColor", LowBackground);
                SetColor(serialized, "warningBackgroundColor", WarningBackground);
                SetColor(serialized, "neutralStatusBackgroundColor", NeutralStatusBackground);
                SetObject(serialized, "statusBackground", modeParts.StatusBand);
                SetObject(serialized, "restockStatsRoot", modeParts.StatsRoot);
                SetObject(serialized, "inStockLabelText", modeParts.InStockLabel);
                SetObject(serialized, "inStockValueText", modeParts.InStockValue);
                SetObject(serialized, "neededTodayLabelText", modeParts.NeededTodayLabel);
                SetObject(serialized, "neededTodayValueText", modeParts.NeededTodayValue);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            RemoveChild(root.transform, "InfoBadge");
            RemoveChild(root.transform, "MenuActionBar");
            TooltipParts tooltip = EnsureTooltip(root.transform, cardFrame);
            ManagementItemCardFeedback feedback =
                root.GetComponent<ManagementItemCardFeedback>() ??
                root.AddComponent<ManagementItemCardFeedback>();
            feedback.ConfigureForEditor(highlight, shadow, tooltip.Root, tooltip.Group, tooltip.Text, false);
            EditorUtility.SetDirty(feedback);
        });
    }

    private static void UpgradeGridSettings()
    {
        EditPrefab(CatalogPanelPath, root =>
        {
            ManagementComputerCatalogPanelUI panel =
                root.GetComponent<ManagementComputerCatalogPanelUI>();
            if (panel == null)
                return;
            SerializedObject serialized = new SerializedObject(panel);
            SetVector2(serialized, "preferredCardSize", new Vector2(248f, 316f));
            SetFloat(serialized, "cardSpacing", 16f);
            SetVector2(serialized, "menuCardSize", new Vector2(248f, 228f));
            SetVector2(serialized, "restockCardSize", new Vector2(248f, 316f));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        });
    }

    private static void ConfigureLayout(
        LayoutElement layout,
        float minimumWidth,
        float preferredWidth,
        float height)
    {
        if (layout == null)
            return;
        layout.minWidth = minimumWidth;
        layout.preferredWidth = preferredWidth;
        layout.minHeight = height;
        layout.preferredHeight = height;
    }

    private static Shadow EnsureShadow(GameObject root)
    {
        Shadow shadow = root.GetComponent<Shadow>() ?? root.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.03f, 0.15f, 0.25f, 0.18f);
        shadow.effectDistance = new Vector2(0f, -5f);
        shadow.useGraphicAlpha = true;
        return shadow;
    }

    private static void EnsureTopAccent(Transform root)
    {
        Transform existing = Find(root, "TopAccent");
        Image accent;
        if (existing == null)
        {
            GameObject go = CreateUI("TopAccent", root);
            accent = go.AddComponent<Image>();
            accent.raycastTarget = false;
            go.transform.SetSiblingIndex(0);
        }
        else
        {
            accent = existing.GetComponent<Image>();
        }
        if (accent == null)
            return;
        accent.color = Blue;
        RectTransform rect = accent.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(8f, -8f);
        rect.offsetMax = new Vector2(-8f, -3f);
    }

    private static Image EnsureHighlight(Transform root, Sprite frame)
    {
        Transform existing = Find(root, "InteractionHighlight");
        Image image;
        if (existing == null)
        {
            GameObject go = CreateUI("InteractionHighlight", root);
            image = go.AddComponent<Image>();
        }
        else
        {
            image = existing.GetComponent<Image>();
        }
        if (image == null)
            return null;
        Stretch(image.rectTransform, 0f);
        image.sprite = frame;
        image.type = Image.Type.Sliced;
        image.color = new Color(0.05f, 0.62f, 0.92f, 0f);
        image.raycastTarget = false;
        return image;
    }

    private static CatalogModeParts EnsureCatalogModeParts(Transform root, Sprite frame)
    {
        Transform band = Find(root, "StatusBand") ?? Find(root, "StatusPill");
        if (band == null)
            band = CreateUI("StatusBand", root).transform;
        band.name = "StatusBand";
        band.gameObject.SetActive(true);
        Image bandImage = band.GetComponent<Image>() ?? band.gameObject.AddComponent<Image>();
        bandImage.sprite = frame;
        bandImage.type = frame != null ? Image.Type.Sliced : Image.Type.Simple;
        bandImage.color = ReadyBackground;
        bandImage.raycastTarget = false;
        SetAnchors(band as RectTransform, new Vector2(0.055f, 0.335f),
            new Vector2(0.945f, 0.425f));
        TMP_Text status = FindText(root, "Status");
        if (status != null)
            band.SetSiblingIndex(Mathf.Max(0, status.transform.GetSiblingIndex()));

        Transform stats = Find(root, "RestockStats");
        if (stats == null)
            stats = CreateUI("RestockStats", root).transform;
        stats.gameObject.SetActive(true);
        SetAnchors(stats as RectTransform, new Vector2(0.055f, 0.195f),
            new Vector2(0.945f, 0.325f));

        StatCellParts stock = EnsureStatCell(
            stats,
            "InStockCell",
            "InStockLabel",
            "InStockValue",
            "IN STOCK",
            frame,
            new Vector2(0f, 0f),
            new Vector2(0.48f, 1f));
        StatCellParts needed = EnsureStatCell(
            stats,
            "NeededTodayCell",
            "NeededTodayLabel",
            "NeededTodayValue",
            "NEEDED TODAY",
            frame,
            new Vector2(0.52f, 0f),
            new Vector2(1f, 1f));

        return new CatalogModeParts(
            bandImage,
            stats.gameObject,
            stock.Label,
            stock.Value,
            needed.Label,
            needed.Value);
    }

    private static StatCellParts EnsureStatCell(
        Transform parent,
        string cellName,
        string labelName,
        string valueName,
        string labelValue,
        Sprite frame,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        Transform cell = Find(parent, cellName);
        if (cell == null)
            cell = CreateUI(cellName, parent).transform;
        SetAnchors(cell as RectTransform, anchorMin, anchorMax);
        Image panel = cell.GetComponent<Image>() ?? cell.gameObject.AddComponent<Image>();
        panel.sprite = frame;
        panel.type = frame != null ? Image.Type.Sliced : Image.Type.Simple;
        panel.color = new Color(1f, 1f, 1f, 0.88f);
        panel.raycastTarget = false;

        TMP_Text label = EnsureCatalogText(
            cell,
            labelName,
            labelValue,
            new Vector2(0.04f, 0.48f),
            new Vector2(0.96f, 0.92f),
            MutedInk,
            12.5f,
            10.5f,
            FontStyles.Normal);
        TMP_Text value = EnsureCatalogText(
            cell,
            valueName,
            "0",
            new Vector2(0.04f, 0.05f),
            new Vector2(0.96f, 0.56f),
            Navy,
            17f,
            14f,
            FontStyles.Bold);
        return new StatCellParts(label, value);
    }

    private static TMP_Text EnsureCatalogText(
        Transform parent,
        string objectName,
        string value,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color color,
        float maximumSize,
        float minimumSize,
        FontStyles style)
    {
        Transform existing = Find(parent, objectName);
        TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;
        if (text == null)
        {
            GameObject textObject = CreateUI(objectName, parent);
            text = textObject.AddComponent<TextMeshProUGUI>();
        }
        ConfigureText(
            text,
            anchorMin,
            anchorMax,
            color,
            maximumSize,
            minimumSize,
            style,
            TextAlignmentOptions.Center,
            false);
        text.text = value;
        return text;
    }

    private static void ConfigureRestockQuantityControl(Transform root, Sprite neutralButtonSprite)
    {
        Transform controls = Find(root, "QuantityControls");
        if (controls == null)
            return;

        SetAnchors(controls as RectTransform, new Vector2(0.055f, 0.015f),
            new Vector2(0.945f, 0.13f));
        Image reusedStrip = controls.GetComponent<Image>();
        if (reusedStrip != null)
            Object.DestroyImmediate(reusedStrip);

        ConfigureQuantityButton(
            Find(root, "Minus")?.GetComponent<Button>(),
            neutralButtonSprite,
            new Color(0.46f, 0.52f, 0.58f, 1f),
            "-");
        ConfigureQuantityButton(
            Find(root, "Plus")?.GetComponent<Button>(),
            neutralButtonSprite,
            new Color(0.13f, 0.55f, 0.83f, 1f),
            "+");

        TMP_Text quantity = FindText(root, "Quantity");
        if (quantity != null)
        {
            quantity.color = Navy;
            quantity.fontStyle = FontStyles.Bold;
            quantity.fontSizeMin = 18f;
            quantity.fontSizeMax = 22f;
            quantity.alignment = TextAlignmentOptions.Center;
        }
    }

    private static void ConfigureQuantityButton(
        Button button,
        Sprite neutralButtonSprite,
        Color tint,
        string labelValue)
    {
        if (button == null)
            return;
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            if (neutralButtonSprite != null)
                image.sprite = neutralButtonSprite;
            image.type = Image.Type.Simple;
            image.color = tint;
        }

        RectTransform rect = button.transform as RectTransform;
        if (rect != null)
            rect.sizeDelta = new Vector2(48f, 48f);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = labelValue;
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            label.fontSizeMin = 16f;
            label.fontSizeMax = 22f;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
        colors.pressedColor = new Color(0.82f, 0.86f, 0.9f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.62f, 0.65f, 0.68f, 0.65f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private static TooltipParts EnsureTooltip(Transform root, Sprite frame)
    {
        Transform existing = Find(root, "Tooltip");
        GameObject tooltip = existing != null ? existing.gameObject : CreateUI("Tooltip", root);
        RectTransform tooltipRect = tooltip.transform as RectTransform;
        SetAnchors(tooltipRect, new Vector2(0.055f, 0.205f), new Vector2(0.945f, 0.57f));
        Image panel = tooltip.GetComponent<Image>() ?? tooltip.AddComponent<Image>();
        panel.sprite = frame;
        panel.type = Image.Type.Sliced;
        panel.color = new Color(0.035f, 0.17f, 0.29f, 0.97f);
        panel.raycastTarget = false;
        TMP_Text text = FindText(tooltip.transform, "Text");
        if (text == null)
        {
            GameObject textObject = CreateUI("Text", tooltip.transform);
            text = textObject.AddComponent<TextMeshProUGUI>();
        }
        Stretch(text.rectTransform, 12f);
        text.text = "<b>ITEM DETAILS</b>\nHover or hold to view extra information.";
        text.fontSize = 14f;
        text.fontSizeMin = 10.5f;
        text.fontSizeMax = 14f;
        text.enableAutoSizing = true;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        tooltip.transform.SetAsLastSibling();
        tooltip.SetActive(true);
        return new TooltipParts(tooltipRect, null, text);
    }

    private static void ConfigureButtonLabels(Transform root)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
                continue;
            UISubtlePressFeedback pressFeedback =
                buttons[i].GetComponent<UISubtlePressFeedback>();
            if (buttons[i].transform == root)
            {
                if (pressFeedback != null)
                    Object.DestroyImmediate(pressFeedback);
            }
            else if (pressFeedback == null)
            {
                buttons[i].gameObject.AddComponent<UISubtlePressFeedback>();
            }
            TMP_Text label = buttons[i].GetComponentInChildren<TMP_Text>(true);
            if (label == null)
                continue;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = Mathf.Max(18f, label.fontSize);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    private static void ApplyFont(Transform root, TMP_FontAsset font)
    {
        if (font == null)
            return;
        TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null)
                labels[i].font = font;
        }
    }

    private static void ConfigureText(
        TMP_Text text,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color color,
        float maximumSize,
        float minimumSize,
        FontStyles style,
        TextAlignmentOptions alignment,
        bool wrap)
    {
        if (text == null)
            return;
        SetAnchors(text.rectTransform, anchorMin, anchorMax);
        text.color = color;
        text.fontStyle = style;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = minimumSize;
        text.fontSizeMax = maximumSize;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.margin = new Vector4(3f, 1f, 3f, 1f);
        text.raycastTarget = false;
    }

    private static void SetFloat(SerializedObject serialized, string propertyName, float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetVector2(SerializedObject serialized, string propertyName, Vector2 value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.vector2Value = value;
    }

    private static void SetColor(SerializedObject serialized, string propertyName, Color value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.colorValue = value;
    }

    private static void SetObject(
        SerializedObject serialized,
        string propertyName,
        Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void RemoveChild(Transform root, string childName)
    {
        Transform child = Find(root, childName);
        if (child != null)
            Object.DestroyImmediate(child.gameObject);
    }

    private static void EditPrefab(string path, System.Action<GameObject> edit)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        if (contents == null)
            return;
        try
        {
            edit(contents);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static GameObject CreateUI(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
            result.layer = uiLayer;
        return result;
    }

    private static Transform Find(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;
        foreach (Transform child in root)
        {
            Transform found = Find(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static TMP_Text FindText(Transform root, string name)
    {
        Transform found = Find(root, name);
        return found != null ? found.GetComponent<TMP_Text>() : null;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        if (rect == null)
            return;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        if (rect == null)
            return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = Vector2.one * -inset;
        rect.localScale = Vector3.one;
    }

    private readonly struct TooltipParts
    {
        public readonly RectTransform Root;
        public readonly CanvasGroup Group;
        public readonly TMP_Text Text;

        public TooltipParts(RectTransform root, CanvasGroup group, TMP_Text text)
        {
            Root = root;
            Group = group;
            Text = text;
        }
    }

    private readonly struct StatCellParts
    {
        public readonly TMP_Text Label;
        public readonly TMP_Text Value;

        public StatCellParts(TMP_Text label, TMP_Text value)
        {
            Label = label;
            Value = value;
        }
    }

    private readonly struct CatalogModeParts
    {
        public readonly Image StatusBand;
        public readonly GameObject StatsRoot;
        public readonly TMP_Text InStockLabel;
        public readonly TMP_Text InStockValue;
        public readonly TMP_Text NeededTodayLabel;
        public readonly TMP_Text NeededTodayValue;

        public CatalogModeParts(
            Image statusBand,
            GameObject statsRoot,
            TMP_Text inStockLabel,
            TMP_Text inStockValue,
            TMP_Text neededTodayLabel,
            TMP_Text neededTodayValue)
        {
            StatusBand = statusBand;
            StatsRoot = statsRoot;
            InStockLabel = inStockLabel;
            InStockValue = inStockValue;
            NeededTodayLabel = neededTodayLabel;
            NeededTodayValue = neededTodayValue;
        }
    }
}
#endif
