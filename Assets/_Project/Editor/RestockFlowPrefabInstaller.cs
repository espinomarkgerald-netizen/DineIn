#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Creates only missing, designer-editable restock flow prefabs.</summary>
public static class RestockFlowPrefabInstaller
{
    private const string Folder = "Assets/_Project/Resources/RestockFlow";
    private const string HudPath = Folder + "/RestockFlowHUD.prefab";
    private const string SlotPath = Folder + "/RestockHotbarSlot.prefab";
    private const string TruckPath = Folder + "/RestockDeliveryTruck.prefab";
    private const string EntrancePath = Folder + "/RestockStockRoomEntrance.prefab";
    private const string BlueMaterialPath = Folder + "/RestockFlowBlue.mat";
    private const string GreenMaterialPath = Folder + "/RestockFlowGreen.mat";
    private const string CardboardBoxPath =
        "Assets/_Project/Restaurant/RestockRoom/Prefabs/CardboardBox.prefab";
    private const string CratePath =
        "Assets/_Project/Restaurant/RestockRoom/Prefabs/crate.prefab";

    private static readonly Color Navy = new Color(0.03f, 0.16f, 0.31f, 1f);
    private static readonly Color Blue = new Color(0.03f, 0.61f, 0.86f, 1f);
    private static readonly Color Pale = new Color(0.92f, 0.97f, 1f, 1f);
    private static readonly Color Ink = new Color(0.10f, 0.20f, 0.29f, 1f);
    private static readonly Color Green = new Color(0.20f, 0.72f, 0.31f, 1f);

    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        EditorApplication.delayCall += EnsureMissingAssets;
    }

    [MenuItem("Tools/Dine In/Create Missing Restock Flow Prefabs")]
    public static void EnsureMissingAssets()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Directory.CreateDirectory(Folder);
        Material blue = EnsureMaterial(BlueMaterialPath, Blue);
        Material green = EnsureMaterial(GreenMaterialPath, Green);
        Button slot = EnsureSlotPrefab();
        EnsureHudPrefab(slot);
        UpgradeSlotPrefab();
        UpgradeHudPrefab();
        UpgradeHudCompactV2();
        EnsureTruckPrefab(blue);
        EnsureEntrancePrefab(green);
        UpgradeInteractablePrefab(TruckPath);
        UpgradeInteractablePrefab(EntrancePath);
        UpgradeWorldLabelRaycasts(TruckPath);
        UpgradeWorldLabelRaycasts(EntrancePath);
        UpgradeStorageContainerPrefab(CardboardBoxPath);
        UpgradeStorageContainerPrefab(CratePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void UpgradeSlotPrefab()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPath);
        if (asset == null || asset.GetComponent<RestockHotbarSlotUI>() != null)
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(SlotPath);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(56f, 56f);

        LayoutElement layout = root.GetComponent<LayoutElement>();
        if (layout == null)
            layout = root.AddComponent<LayoutElement>();
        layout.minWidth = 56f;
        layout.preferredWidth = 56f;
        layout.minHeight = 56f;
        layout.preferredHeight = 56f;

        Transform name = root.transform.Find("Name");
        if (name != null)
            name.gameObject.SetActive(false);

        Image icon = root.transform.Find("Icon")?.GetComponent<Image>();
        if (icon != null)
        {
            Stretch(icon.rectTransform, 7f, 7f, 7f, 7f);
            icon.raycastTarget = false;
        }

        TMP_Text count = root.transform.Find("Count")?.GetComponent<TMP_Text>();
        if (count != null)
        {
            RectTransform countRect = count.rectTransform;
            countRect.anchorMin = countRect.anchorMax = new Vector2(1f, 0f);
            countRect.pivot = new Vector2(1f, 0f);
            countRect.anchoredPosition = new Vector2(-3f, 2f);
            countRect.sizeDelta = new Vector2(34f, 20f);
            count.alignment = TextAlignmentOptions.BottomRight;
            count.fontSize = 16f;
            count.fontSizeMin = 11f;
            count.fontSizeMax = 16f;
            count.raycastTarget = false;
        }

        GameObject selected = UI("SelectedBorder", root.transform);
        selected.transform.SetAsFirstSibling();
        Image selectedImage = Image(selected, new Color(0.15f, 0.92f, 1f, 0.24f));
        selectedImage.raycastTarget = false;
        Stretch(selected.GetComponent<RectTransform>(), -3f, -3f, -3f, -3f);
        selected.SetActive(false);

        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = root.AddComponent<CanvasGroup>();

        RestockHotbarSlotUI slot = root.AddComponent<RestockHotbarSlotUI>();
        slot.ConfigureReferences(icon, count, selected, canvasGroup);

        PrefabUtility.SaveAsPrefabAsset(root, SlotPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void UpgradeHudPrefab()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
        if (asset == null)
            return;

        Transform existingHotbar = asset.transform.Find("RestockHotbar");
        if (existingHotbar != null && existingHotbar.Find("Remaining") != null)
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        RestockFlowHUD hud = root.GetComponent<RestockFlowHUD>();
        Transform hotbarTransform = root.transform.Find("RestockHotbar");
        if (hud == null || hotbarTransform == null)
        {
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        RectTransform hotbarRect = hotbarTransform as RectTransform;
        hotbarRect.anchoredPosition = new Vector2(0f, 12f);
        hotbarRect.sizeDelta = new Vector2(520f, 96f);

        TMP_Text roomMessage = hotbarTransform.Find("RoomMessage")?.GetComponent<TMP_Text>();
        if (roomMessage != null)
        {
            SetTopStretch(roomMessage.rectTransform, 12f, 12f, 5f, 20f);
            roomMessage.fontSize = 14f;
            roomMessage.fontSizeMin = 10f;
            roomMessage.fontSizeMax = 14f;
        }

        RectTransform viewport = hotbarTransform.Find("Viewport") as RectTransform;
        if (viewport != null)
            Stretch(viewport, 10f, 10f, 21f, 27f);

        TMP_Text remaining = Text(
            "Remaining", hotbarTransform,
            "DELIVERY • 0 REMAINING • DRY 0 • FROZEN 0",
            12f, FontStyles.Bold, Color.white);
        SetBottomStretch(remaining.rectTransform, 10f, 10f, 3f, 17f);
        remaining.alignment = TextAlignmentOptions.Center;

        GameObject tooltip = UI("Tooltip", hotbarTransform);
        Image(tooltip, new Color(0.03f, 0.16f, 0.31f, 0.98f));
        RectTransform tooltipRect = tooltip.GetComponent<RectTransform>();
        tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(0.5f, 1f);
        tooltipRect.pivot = new Vector2(0.5f, 0f);
        tooltipRect.anchoredPosition = new Vector2(0f, 8f);
        tooltipRect.sizeDelta = new Vector2(470f, 34f);
        TMP_Text tooltipText = Text(
            "Text", tooltip.transform,
            "ITEM • 1 BOX • 12 UNITS EACH • DRY ROOM",
            13f, FontStyles.Bold, Color.white);
        Stretch(tooltipText.rectTransform, 10f, 10f, 4f, 4f);
        tooltipText.alignment = TextAlignmentOptions.Center;
        tooltip.SetActive(false);

        GameObject reminder = UI("StartReminder", root.transform);
        Image(reminder, new Color(0f, 0f, 0f, 0.64f));
        Stretch(reminder.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        GameObject panel = UI("Panel", reminder.transform);
        Image(panel, Pale);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(540f, 230f);
        TMP_Text title = Text("Title", panel.transform, "DELIVERY STILL UNPACKED", 25f, FontStyles.Bold, Ink);
        SetTopStretch(title.rectTransform, 20f, 20f, 18f, 36f);
        title.alignment = TextAlignmentOptions.Center;
        TMP_Text message = Text(
            "Message", panel.transform,
            "Delivered boxes are still in your hotbar.",
            17f, FontStyles.Normal, Ink);
        SetTopStretch(message.rectTransform, 34f, 34f, 62f, 68f);
        message.alignment = TextAlignmentOptions.Center;
        message.textWrappingMode = TextWrappingModes.Normal;
        Button restockFirst = Button("RestockFirst", panel.transform, "RESTOCK FIRST", Blue, Color.white, 17f);
        RectTransform firstRect = restockFirst.transform as RectTransform;
        firstRect.anchorMin = firstRect.anchorMax = new Vector2(0.5f, 0f);
        firstRect.pivot = new Vector2(1f, 0f);
        firstRect.anchoredPosition = new Vector2(-7f, 22f);
        firstRect.sizeDelta = new Vector2(200f, 56f);
        Button startAnyway = Button("StartAnyway", panel.transform, "START ANYWAY", Navy, Color.white, 17f);
        RectTransform anywayRect = startAnyway.transform as RectTransform;
        anywayRect.anchorMin = anywayRect.anchorMax = new Vector2(0.5f, 0f);
        anywayRect.pivot = new Vector2(0f, 0f);
        anywayRect.anchoredPosition = new Vector2(7f, 22f);
        anywayRect.sizeDelta = new Vector2(200f, 56f);
        reminder.SetActive(false);

        hud.ConfigureExtendedReferences(
            remaining,
            tooltip,
            tooltipText,
            reminder,
            message,
            restockFirst,
            startAnyway);

        PrefabUtility.SaveAsPrefabAsset(root, HudPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void UpgradeHudCompactV2()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
        if (asset == null || asset.transform.Find("RestockHotbar/CompactLayoutV2") != null)
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        RectTransform hotbar = root.transform.Find("RestockHotbar") as RectTransform;
        if (hotbar == null)
        {
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        hotbar.sizeDelta = new Vector2(520f, 104f);
        RectTransform viewport = hotbar.Find("Viewport") as RectTransform;
        if (viewport != null)
            Stretch(viewport, 10f, 10f, 21f, 27f);

        GameObject marker = UI("CompactLayoutV2", hotbar);
        marker.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, HudPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static Material EnsureMaterial(string path, Color color)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
            return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        Material material = new Material(shader) { color = color };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Button EnsureSlotPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPath);
        if (existing != null)
            return existing.GetComponent<Button>();

        GameObject root = UI("RestockHotbarSlot", null);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(138f, 92f);
        Image background = Image(root, Pale);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.minWidth = 126f;
        layout.preferredWidth = 138f;
        layout.minHeight = 84f;
        layout.preferredHeight = 92f;

        Image icon = Image(UI("Icon", root.transform), Color.white);
        SetLeft(icon.rectTransform, 8f, 18f, 54f, 54f);
        icon.preserveAspect = true;

        TMP_Text name = Text("Name", root.transform, "ITEM", 15f, FontStyles.Bold, Ink);
        SetTopStretch(name.rectTransform, 68f, 8f, 9f, 43f);
        name.alignment = TextAlignmentOptions.TopLeft;
        name.textWrappingMode = TextWrappingModes.Normal;

        TMP_Text count = Text("Count", root.transform, "x1", 19f, FontStyles.Bold, Blue);
        SetBottomStretch(count.rectTransform, 68f, 8f, 9f, 28f);
        count.alignment = TextAlignmentOptions.MidlineLeft;

        PrefabUtility.SaveAsPrefabAsset(root, SlotPath);
        Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(SlotPath).GetComponent<Button>();
    }

    private static void EnsureHudPrefab(Button slotPrefab)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(HudPath) != null)
            return;

        GameObject root = UI("RestockFlowHUD", null);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 450f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root.AddComponent<GraphicRaycaster>();

        GameObject notification = UI("DeliveryNotification", root.transform);
        Image(notification, Navy);
        RectTransform notificationRect = notification.GetComponent<RectTransform>();
        notificationRect.anchorMin = notificationRect.anchorMax = new Vector2(0.5f, 1f);
        notificationRect.pivot = new Vector2(0.5f, 1f);
        notificationRect.anchoredPosition = new Vector2(0f, -22f);
        notificationRect.sizeDelta = new Vector2(650f, 82f);
        TMP_Text notificationText = Text(
            "Message", notification.transform,
            "Your order has arrived! Go to the delivery truck.",
            20f, FontStyles.Bold, Color.white);
        Stretch(notificationText.rectTransform, 22f, 22f, 12f, 12f);
        notificationText.alignment = TextAlignmentOptions.Center;
        notificationText.textWrappingMode = TextWrappingModes.Normal;

        GameObject holdRoot = UI("TruckHoldOverlay", root.transform);
        Image(holdRoot, new Color(0f, 0f, 0f, 0.58f));
        Stretch(holdRoot.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        GameObject holdPanel = UI("Panel", holdRoot.transform);
        Image(holdPanel, Pale);
        RectTransform holdPanelRect = holdPanel.GetComponent<RectTransform>();
        holdPanelRect.anchorMin = holdPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        holdPanelRect.sizeDelta = new Vector2(510f, 210f);
        TMP_Text holdTitle = Text(
            "Title", holdPanel.transform, "COLLECT DELIVERY", 28f,
            FontStyles.Bold, Ink);
        SetTopStretch(holdTitle.rectTransform, 20f, 20f, 16f, 40f);
        holdTitle.alignment = TextAlignmentOptions.Center;
        TMP_Text holdHint = Text(
            "Hint", holdPanel.transform,
            "Keep holding until every ordered box is loaded into your hotbar.",
            16f, FontStyles.Normal, Ink);
        SetTopStretch(holdHint.rectTransform, 30f, 30f, 58f, 44f);
        holdHint.alignment = TextAlignmentOptions.Center;
        holdHint.textWrappingMode = TextWrappingModes.Normal;

        GameObject holdArea = UI("HoldButton", holdPanel.transform);
        Image holdAreaImage = Image(holdArea, Blue);
        Button holdAreaButton = holdArea.AddComponent<Button>();
        holdAreaButton.targetGraphic = holdAreaImage;
        SetBottomStretch(holdArea.GetComponent<RectTransform>(), 55f, 55f, 24f, 70f);
        RestockHoldButton hold = holdArea.AddComponent<RestockHoldButton>();
        Image radial = Image(UI("CircularProgress", holdArea.transform), Blue);
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        radial.sprite = circle;
        radial.type = UnityEngine.UI.Image.Type.Filled;
        radial.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
        radial.fillAmount = 0f;
        radial.color = new Color(0.30f, 1f, 0.55f, 1f);
        SetLeft(radial.rectTransform, 10f, 7f, 56f, 56f);
        TMP_Text holdLabel = Text(
            "Label", holdArea.transform, "HOLD TO COLLECT", 20f,
            FontStyles.Bold, Color.white);
        Stretch(holdLabel.rectTransform, 74f, 10f, 5f, 5f);
        holdLabel.alignment = TextAlignmentOptions.Center;
        hold.Configure(radial, holdLabel);

        Button holdClose = Button("Close", holdPanel.transform, "×", Navy, Color.white, 25f);
        SetTopRight(holdClose.transform as RectTransform, 10f, 10f, 42f, 42f);

        GameObject hotbar = UI("RestockHotbar", root.transform);
        Image(hotbar, new Color(0.03f, 0.16f, 0.31f, 0.96f));
        RectTransform hotbarRect = hotbar.GetComponent<RectTransform>();
        hotbarRect.anchorMin = hotbarRect.anchorMax = new Vector2(0.5f, 0f);
        hotbarRect.pivot = new Vector2(0.5f, 0f);
        hotbarRect.anchoredPosition = new Vector2(0f, 14f);
        hotbarRect.sizeDelta = new Vector2(760f, 146f);
        TMP_Text roomMessage = Text(
            "RoomMessage", hotbar.transform,
            "Choose a delivered box, then tap an open shelf slot.",
            17f, FontStyles.Bold, Color.white);
        SetTopStretch(roomMessage.rectTransform, 16f, 16f, 8f, 32f);
        roomMessage.alignment = TextAlignmentOptions.Center;

        GameObject viewport = UI("Viewport", hotbar.transform);
        Stretch(viewport.GetComponent<RectTransform>(), 12f, 12f, 10f, 44f);
        RectMask2D mask = viewport.AddComponent<RectMask2D>();
        GameObject contentObject = UI("Content", viewport.transform);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        HorizontalLayoutGroup horizontal = contentObject.AddComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 8f;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = true;
        ContentSizeFitter contentFitter = contentObject.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        ScrollRect hotbarScroll = hotbar.AddComponent<ScrollRect>();
        hotbarScroll.viewport = viewport.GetComponent<RectTransform>();
        hotbarScroll.content = content;
        hotbarScroll.horizontal = true;
        hotbarScroll.vertical = false;
        hotbarScroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject irisObject = UI("IrisTransition", root.transform);
        Stretch(irisObject.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        RestockIrisGraphic iris = irisObject.AddComponent<RestockIrisGraphic>();
        iris.color = Navy;

        RestockFlowHUD hud = root.AddComponent<RestockFlowHUD>();
        hud.ConfigureReferences(
            notification,
            notificationText,
            holdRoot,
            hold,
            holdClose,
            hotbar,
            content,
            slotPrefab,
            roomMessage,
            iris);

        PrefabUtility.SaveAsPrefabAsset(root, HudPath);
        Object.DestroyImmediate(root);
    }

    private static void EnsureTruckPrefab(Material material)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TruckPath) != null)
            return;

        GameObject root = new GameObject("RestockDeliveryTruck");
        ConfigureInteractableRoot(root);
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.8f, 0f);
        collider.size = new Vector3(3.8f, 1.6f, 2.4f);
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Placeholder Truck Body";
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        body.transform.localScale = new Vector3(3.8f, 1.6f, 2.4f);
        body.GetComponent<Renderer>().sharedMaterial = material;

        Transform stand = new GameObject("StandPoint").transform;
        stand.SetParent(root.transform, false);
        stand.localPosition = new Vector3(0f, 0f, -2.1f);
        TMP_Text status = CreateWorldLabel(root.transform, "DELIVERY TRUCK\nNO ORDER READY");
        RestockTruckInteractable component = root.AddComponent<RestockTruckInteractable>();
        component.Configure(stand, status);

        PrefabUtility.SaveAsPrefabAsset(root, TruckPath);
        Object.DestroyImmediate(root);
    }

    private static void EnsureEntrancePrefab(Material material)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(EntrancePath) != null)
            return;

        GameObject root = new GameObject("RestockStockRoomEntrance");
        ConfigureInteractableRoot(root);
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 1.1f, 0f);
        collider.size = new Vector3(1.8f, 2.2f, 0.7f);
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "Stock Room Shelf Marker";
        Object.DestroyImmediate(marker.GetComponent<Collider>());
        marker.transform.SetParent(root.transform, false);
        marker.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        marker.transform.localScale = new Vector3(1.8f, 2.2f, 0.7f);
        marker.GetComponent<Renderer>().sharedMaterial = material;

        Transform stand = new GameObject("StandPoint").transform;
        stand.SetParent(root.transform, false);
        stand.localPosition = new Vector3(0f, 0f, -1.4f);
        TMP_Text status = CreateWorldLabel(root.transform, "STOCK ROOM\nCOLLECT DELIVERY FIRST");
        RestockStockRoomEntrance component = root.AddComponent<RestockStockRoomEntrance>();
        component.Configure(stand, status);

        PrefabUtility.SaveAsPrefabAsset(root, EntrancePath);
        Object.DestroyImmediate(root);
    }

    private static TMP_Text CreateWorldLabel(Transform parent, string value)
    {
        GameObject canvasObject = UI("WorldLabel", parent);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 25;
        canvasObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        rect.localPosition = new Vector3(0f, 2.45f, 0f);
        rect.localRotation = Quaternion.Euler(90f, 0f, 0f);
        rect.localScale = Vector3.one * 0.01f;
        rect.sizeDelta = new Vector2(380f, 100f);
        Image labelBackground = Image(canvasObject, Navy);
        labelBackground.raycastTarget = false;
        TMP_Text text = Text("Status", canvasObject.transform, value, 28f, FontStyles.Bold, Color.white);
        Stretch(text.rectTransform, 12f, 12f, 8f, 8f);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static void UpgradeInteractablePrefab(string prefabPath)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        int originalLayer = root.layer;
        Outline originalOutline = root.GetComponent<Outline>();
        ConfigureInteractableRoot(root);
        if (root.layer != originalLayer || originalOutline == null)
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void ConfigureInteractableRoot(GameObject root)
    {
        int interactionLayer = LayerMask.NameToLayer("Interactable ");
        if (interactionLayer >= 0)
            root.layer = interactionLayer;

        Outline outline = root.GetComponent<Outline>();
        if (outline == null)
            outline = root.AddComponent<Outline>();
        outline.OutlineMode = Outline.Mode.OutlineAll;
        outline.OutlineColor = Color.white;
        outline.OutlineWidth = 4f;
        outline.enabled = false;
    }

    private static void UpgradeWorldLabelRaycasts(string prefabPath)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (asset == null)
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        bool changed = false;
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name != "WorldLabel")
                continue;

            Graphic[] graphics = children[i].GetComponentsInChildren<Graphic>(true);
            for (int g = 0; g < graphics.Length; g++)
            {
                if (graphics[g] != null && graphics[g].raycastTarget)
                {
                    graphics[g].raycastTarget = false;
                    changed = true;
                }
            }

            GraphicRaycaster raycaster = children[i].GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                Object.DestroyImmediate(raycaster);
                changed = true;
            }
        }

        if (changed)
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void UpgradeStorageContainerPrefab(string prefabPath)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (asset == null)
            return;

        RestockStorageContainer existing = asset.GetComponent<RestockStorageContainer>();
        if (existing != null && existing.HasConfiguredLabels)
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        RestockStorageContainer identity = root.GetComponent<RestockStorageContainer>();
        if (identity == null)
            identity = root.AddComponent<RestockStorageContainer>();

        List<TMP_Text> labels = new List<TMP_Text>();
        List<Image> icons = new List<Image>();
        Transform labelRoot = root.transform.Find("UI");
        if (labelRoot != null)
        {
            for (int i = 0; i < labelRoot.childCount; i++)
            {
                Transform child = labelRoot.GetChild(i);
                if (child.name != "Canvas" && child.name != "Canvas 2" && child.name != "Canvas2")
                    continue;

                TMP_Text label = child.GetComponentInChildren<TMP_Text>(true);
                Image icon = child.GetComponentInChildren<Image>(true);
                if (label != null)
                    labels.Add(label);
                if (icon != null)
                    icons.Add(icon);
            }
        }

        identity.ConfigureLabels(labels.ToArray(), icons.ToArray());
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static GameObject UI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            go.transform.SetParent(parent, false);
        return go;
    }

    private static Image Image(GameObject go, Color color)
    {
        Image image = go.GetComponent<Image>();
        if (image == null)
            image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text Text(
        string name, Transform parent, string value, float size,
        FontStyles style, Color color)
    {
        TextMeshProUGUI text = UI(name, parent).AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(10f, size - 7f);
        text.fontSizeMax = size;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static Button Button(
        string name, Transform parent, string value,
        Color background, Color foreground, float size)
    {
        GameObject go = UI(name, parent);
        Image image = Image(go, background);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text label = Text("Label", go.transform, value, size, FontStyles.Bold, foreground);
        Stretch(label.rectTransform, 4f, 4f, 4f, 4f);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static void Stretch(RectTransform rect, float left, float right, float bottom, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
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

    private static void SetLeft(RectTransform rect, float left, float margin, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(left, 0f);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetTopRight(RectTransform rect, float right, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-right, -top);
        rect.sizeDelta = new Vector2(width, height);
    }
}
#endif
