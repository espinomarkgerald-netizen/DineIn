#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates the editable equipment-catalog building blocks once. Existing
/// prefabs are never overwritten, so Prefab Mode edits remain authoritative.
/// </summary>
[InitializeOnLoad]
public static class ManagementEquipmentCatalogAuthoring
{
    private const string ResourceFolder = "Assets/_Project/Resources/ManagementComputer";
    private const string CardPath = ResourceFolder + "/ManagementEquipmentCard.prefab";
    private const string SectionPath = ResourceFolder + "/ManagementEquipmentSection.prefab";
    private const string SessionKey = "DineIn.ManagementEquipmentCatalog.Installed.v1";
    private const string FramePath = "Assets/_Project/MainMenu/Assets/Buttons/Frames/9Sliced.png";
    private const string BlueButtonPath = "Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/Blue/Double/button_rectangle_depth_flat.png";
    private const string GreenButtonPath = "Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/Green/Default/button_rectangle_depth_flat.png";
    private const string FontPath = "Assets/_Project/UI/Assets/Legacy/Fonts/Fredoka,Lilita_One/Fredoka/Fredoka-VariableFont_wdth,wght SDF.asset";

    static ManagementEquipmentCatalogAuthoring()
    {
        EditorApplication.delayCall += TryAutomaticInstall;
    }

    [MenuItem("Tools/Dine In/Install Equipment Catalog UI")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogWarning("[EquipmentCatalogUI] Stop Play Mode and wait for compilation, then run the installer again.");
            return;
        }

        EnsureFolder("Assets/_Project/Resources");
        EnsureFolder(ResourceFolder);

        GameObject cardAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CardPath);
        if (cardAsset == null)
            cardAsset = CreateCardPrefab();

        if (AssetDatabase.LoadAssetAtPath<GameObject>(SectionPath) == null)
            CreateSectionPrefab(cardAsset != null ? cardAsset.GetComponent<ManagementEquipmentCardUI>() : null);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SessionState.SetBool(SessionKey, true);
        Debug.Log("[EquipmentCatalogUI] Editable, responsive equipment catalog prefabs are ready.");
    }

    private static void TryAutomaticInstall()
    {
        if (SessionState.GetBool(SessionKey, false) ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling)
            return;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(CardPath) != null &&
            AssetDatabase.LoadAssetAtPath<GameObject>(SectionPath) != null)
        {
            SessionState.SetBool(SessionKey, true);
            return;
        }

        Install();
    }

    private static GameObject CreateCardPrefab()
    {
        Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
        Sprite blueButton = AssetDatabase.LoadAssetAtPath<Sprite>(BlueButtonPath);
        Sprite greenButton = AssetDatabase.LoadAssetAtPath<Sprite>(GreenButtonPath);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        GameObject root = CreateRect("ManagementEquipmentCard", null);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(340f, 330f);
        Image background = root.AddComponent<Image>();
        background.sprite = frame;
        background.type = Image.Type.Sliced;
        background.color = new Color(0.88f, 0.95f, 1f, 1f);
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.minWidth = 260f;
        layout.preferredWidth = 340f;
        layout.minHeight = 330f;
        layout.preferredHeight = 330f;

        GameObject iconPanel = CreateRect("IconPanel", root.transform);
        SetAnchors(iconPanel.GetComponent<RectTransform>(), new Vector2(0.055f, 0.53f), new Vector2(0.945f, 0.955f), Vector2.zero, Vector2.zero);
        Image iconPanelImage = iconPanel.AddComponent<Image>();
        iconPanelImage.sprite = frame;
        iconPanelImage.type = Image.Type.Sliced;
        iconPanelImage.color = new Color(1f, 1f, 1f, 0.96f);

        GameObject iconObject = CreateRect("ItemIcon", iconPanel.transform);
        SetAnchors(iconObject.GetComponent<RectTransform>(), new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero);
        Image icon = iconObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text title = CreateText("Title", root.transform, font, 25f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.03f, 0.19f, 0.31f, 1f));
        SetAnchors(title.rectTransform, new Vector2(0.055f, 0.41f), new Vector2(0.945f, 0.53f), Vector2.zero, Vector2.zero);
        title.enableAutoSizing = true;
        title.fontSizeMin = 17f;
        title.fontSizeMax = 25f;

        TMP_Text description = CreateText("Description", root.transform, font, 16f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.12f, 0.24f, 0.34f, 1f));
        SetAnchors(description.rectTransform, new Vector2(0.075f, 0.235f), new Vector2(0.925f, 0.41f), Vector2.zero, Vector2.zero);
        description.textWrappingMode = TextWrappingModes.Normal;
        description.overflowMode = TextOverflowModes.Ellipsis;

        TMP_Text availability = CreateText("Availability", root.transform, font, 14f, FontStyles.Bold, TextAlignmentOptions.Left, new Color(0.05f, 0.40f, 0.67f, 1f));
        SetAnchors(availability.rectTransform, new Vector2(0.075f, 0.165f), new Vector2(0.58f, 0.235f), Vector2.zero, Vector2.zero);
        availability.enableAutoSizing = true;
        availability.fontSizeMin = 10f;
        availability.fontSizeMax = 14f;

        TMP_Text price = CreateText("Price", root.transform, font, 18f, FontStyles.Bold, TextAlignmentOptions.Right, new Color(0.05f, 0.40f, 0.67f, 1f));
        SetAnchors(price.rectTransform, new Vector2(0.56f, 0.165f), new Vector2(0.925f, 0.235f), Vector2.zero, Vector2.zero);
        price.enableAutoSizing = true;
        price.fontSizeMin = 12f;
        price.fontSizeMax = 18f;

        GameObject buttonObject = CreateRect("BuyButton", root.transform);
        SetAnchors(buttonObject.GetComponent<RectTransform>(), new Vector2(0.075f, 0.035f), new Vector2(0.925f, 0.155f), Vector2.zero, Vector2.zero);
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.sprite = blueButton;
        buttonImage.type = Image.Type.Sliced;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        ColorBlock buttonColors = button.colors;
        buttonColors.normalColor = Color.white;
        buttonColors.highlightedColor = new Color(1f, 1f, 1f, 1f);
        buttonColors.pressedColor = new Color(0.84f, 0.92f, 1f, 1f);
        buttonColors.disabledColor = new Color(0.56f, 0.62f, 0.68f, 0.68f);
        button.colors = buttonColors;

        TMP_Text buttonLabel = CreateText("Label", buttonObject.transform, font, 21f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Stretch(buttonLabel.rectTransform, 12f, 8f);

        GameObject ownedObject = CreateRect("OwnedBadge", root.transform);
        SetAnchors(ownedObject.GetComponent<RectTransform>(), new Vector2(0.075f, 0.035f), new Vector2(0.925f, 0.155f), Vector2.zero, Vector2.zero);
        Image ownedBadge = ownedObject.AddComponent<Image>();
        ownedBadge.sprite = greenButton;
        ownedBadge.type = Image.Type.Sliced;
        TMP_Text ownedLabel = CreateText("Label", ownedObject.transform, font, 20f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        ownedLabel.text = "OWNED";
        Stretch(ownedLabel.rectTransform, 12f, 8f);
        ownedObject.SetActive(false);

        ManagementEquipmentCardUI card = root.AddComponent<ManagementEquipmentCardUI>();
        card.ConfigureReferences(icon, title, description, availability, price, button, buttonLabel, ownedBadge);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, CardPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static void CreateSectionPrefab(ManagementEquipmentCardUI cardPrefab)
    {
        if (cardPrefab == null)
        {
            Debug.LogError("[EquipmentCatalogUI] The equipment card prefab could not be created.");
            return;
        }

        Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        GameObject root = CreateRect("ManagementEquipmentSection", null);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.sizeDelta = new Vector2(0f, 520f);
        LayoutElement sectionLayout = root.AddComponent<LayoutElement>();
        sectionLayout.minHeight = 520f;
        sectionLayout.preferredHeight = 520f;

        GameObject header = CreateRect("SectionHeader", root.transform);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.offsetMin = new Vector2(10f, -82f);
        headerRect.offsetMax = new Vector2(-10f, -4f);
        Image headerImage = header.AddComponent<Image>();
        headerImage.sprite = frame;
        headerImage.type = Image.Type.Sliced;
        headerImage.color = new Color(0.035f, 0.24f, 0.40f, 0.98f);

        TMP_Text title = CreateText("Title", header.transform, font, 25f, FontStyles.Bold, TextAlignmentOptions.Left, Color.white);
        SetAnchors(title.rectTransform, new Vector2(0.035f, 0.42f), new Vector2(0.965f, 0.94f), Vector2.zero, Vector2.zero);
        title.enableAutoSizing = true;
        title.fontSizeMin = 18f;
        title.fontSizeMax = 25f;

        TMP_Text subtitle = CreateText("Subtitle", header.transform, font, 15f, FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.82f, 0.93f, 1f, 1f));
        SetAnchors(subtitle.rectTransform, new Vector2(0.035f, 0.08f), new Vector2(0.965f, 0.47f), Vector2.zero, Vector2.zero);
        subtitle.enableAutoSizing = true;
        subtitle.fontSizeMin = 11f;
        subtitle.fontSizeMax = 15f;

        GameObject dividerObject = CreateRect("Divider", root.transform);
        RectTransform dividerRect = dividerObject.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 1f);
        dividerRect.anchorMax = new Vector2(1f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.offsetMin = new Vector2(18f, -91f);
        dividerRect.offsetMax = new Vector2(-18f, -86f);
        Image divider = dividerObject.AddComponent<Image>();
        divider.color = new Color(0.12f, 0.64f, 0.88f, 1f);
        divider.raycastTarget = false;

        GameObject cardsObject = CreateRect("Cards", root.transform);
        RectTransform cards = cardsObject.GetComponent<RectTransform>();
        cards.anchorMin = new Vector2(0f, 1f);
        cards.anchorMax = new Vector2(1f, 1f);
        cards.pivot = new Vector2(0.5f, 1f);
        cards.offsetMin = new Vector2(0f, -500f);
        cards.offsetMax = new Vector2(0f, -92f);
        GridLayoutGroup grid = cardsObject.AddComponent<GridLayoutGroup>();
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.cellSize = new Vector2(340f, 330f);
        grid.spacing = new Vector2(18f, 18f);
        grid.padding = new RectOffset(14, 14, 0, 0);

        ManagementEquipmentSectionUI section = root.AddComponent<ManagementEquipmentSectionUI>();
        section.ConfigureReferences(title, subtitle, divider, cards, grid, sectionLayout, cardPrefab);
        section.Reflow(true);

        PrefabUtility.SaveAsPrefabAsset(root, SectionPath);
        Object.DestroyImmediate(root);
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        TMP_FontAsset font,
        float size,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject go = CreateRect(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.text = name;
        text.margin = new Vector4(2f, 1f, 2f, 1f);
        return text;
    }

    private static GameObject CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetAnchors(
        RectTransform rect,
        Vector2 min,
        Vector2 max,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void Stretch(RectTransform rect, float horizontal, float vertical)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontal, vertical);
        rect.offsetMax = new Vector2(-horizontal, -vertical);
        rect.localScale = Vector3.one;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = path.Substring(0, path.LastIndexOf('/'));
        string name = path.Substring(path.LastIndexOf('/') + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
