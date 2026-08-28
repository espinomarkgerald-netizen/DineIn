#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class CardPaymentAndUpgradeAuthoring
{
    private const string LobbyScenePath = "Assets/_Project/Scenes/RoleBased/Lobby1.unity";
    private const string CardPrefabPath = "Assets/_Project/Resources/UI/CardPaymentUI.prefab";
    private const string UnlockPrefabPath = "Assets/_Project/Resources/UI/UnlockCelebrationUI.prefab";
    private const string UpgradeFolder = "Assets/_Project/Office/Manager/Equipment/Upgrades";
    private const string ResourceUpgradeFolder = "Assets/_Project/Resources/Upgrades";
    private const string SessionKey = "DineIn.CardPaymentAndUpgrades.Installed.v4";
    private const int CardPrefabAuthoringVersion = 4;
    private const int TrolleyPrefabAuthoringVersion = 3;

    static CardPaymentAndUpgradeAuthoring()
    {
        EditorApplication.delayCall += TryAutomaticInstall;
    }

    [MenuItem("Tools/Dine In/Install Card Payment + Upgrades")]
    public static void InstallAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogWarning("[CardPaymentAuthoring] Stop Play Mode and wait for compilation, then run the installer again.");
            return;
        }

        EnsureFolder("Assets/_Project/Resources");
        EnsureFolder("Assets/_Project/Resources/UI");
        EnsureFolder(ResourceUpgradeFolder);
        EnsureFolder(UpgradeFolder);

        EquipmentUpgrade busser = CreateOrUpdateUpgrade(
            UpgradeFolder + "/Busser Trolley.asset",
            EquipmentUpgradeService.BusserTrolleyID,
            "Busser Trolley",
            "Collects up to four dirty trays before returning to the sink.",
            15,
            5000,
            EquipmentUpgradeEffect.BusserTrolley,
            "Assets/_Project/Art/Icons/GameIcons/Upgrades/BusserTrolleyIcon.png");
        EquipmentUpgrade waiter = CreateOrUpdateUpgrade(
            UpgradeFolder + "/Waiter Trolley.asset",
            EquipmentUpgradeService.WaiterTrolleyID,
            "Waiter Trolley",
            "Carries up to four prepared orders for faster table delivery.",
            18,
            8000,
            EquipmentUpgradeEffect.WaiterTrolley,
            "Assets/_Project/Art/Icons/GameIcons/Upgrades/WaiterTrolleyIcon.png");
        EquipmentUpgrade card = CreateOrUpdateUpgrade(
            UpgradeFolder + "/Card Payment.asset",
            EquipmentUpgradeService.CardPaymentID,
            "Card Payment",
            "Gives customers a 50% chance to pay by card at the table.",
            20,
            10000,
            EquipmentUpgradeEffect.CardPayment,
            "Assets/_Project/Art/Icons/GameIcons/Upgrades/CardPaymentIcon.png");

        CreateOrUpgradeTrolleyPrefab(
            ResourceUpgradeFolder + "/WaiterTrolley.prefab",
            ResourceUpgradeFolder + "/WaiterTrolley.mat",
            new Color(0.08f, 0.48f, 0.82f, 1f),
            EquipmentUpgradeEffect.WaiterTrolley);
        CreateOrUpgradeTrolleyPrefab(
            ResourceUpgradeFolder + "/BusserTrolley.prefab",
            ResourceUpgradeFolder + "/BusserTrolley.mat",
            new Color(0.92f, 0.43f, 0.10f, 1f),
            EquipmentUpgradeEffect.BusserTrolley);
        CreateUnlockCelebrationPrefab();
        UpgradeMoneyBubblePrefab();
        InstallLobbyScene(busser, waiter, card);
        UpgradeCardPaymentPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SessionState.SetBool(SessionKey, true);
        Debug.Log("[CardPaymentAuthoring] Card payment, upgrades, trolleys and unlock UI are installed and editable.");
    }

    private static void TryAutomaticInstall()
    {
        if (SessionState.GetBool(SessionKey, false) ||
            EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject unlockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnlockPrefabPath);
        GameObject waiterTrolley = AssetDatabase.LoadAssetAtPath<GameObject>(
            ResourceUpgradeFolder + "/WaiterTrolley.prefab");
        GameObject busserTrolley = AssetDatabase.LoadAssetAtPath<GameObject>(
            ResourceUpgradeFolder + "/BusserTrolley.prefab");
        CardPaymentUI cardUI = cardPrefab != null ? cardPrefab.GetComponent<CardPaymentUI>() : null;
        BotTrolleyCarrier waiterCarrier = waiterTrolley != null
            ? waiterTrolley.GetComponent<BotTrolleyCarrier>()
            : null;
        BotTrolleyCarrier busserCarrier = busserTrolley != null
            ? busserTrolley.GetComponent<BotTrolleyCarrier>()
            : null;
        if (cardUI != null && cardUI.AuthoringVersion >= CardPrefabAuthoringVersion &&
            waiterCarrier != null && waiterCarrier.AuthoringVersion >= TrolleyPrefabAuthoringVersion &&
            busserCarrier != null && busserCarrier.AuthoringVersion >= TrolleyPrefabAuthoringVersion &&
            unlockPrefab != null && unlockPrefab.GetComponent<UnlockCelebrationUI>() != null &&
            AssetDatabase.LoadAssetAtPath<EquipmentUpgrade>(UpgradeFolder + "/Card Payment.asset") != null)
        {
            SessionState.SetBool(SessionKey, true);
            return;
        }

        InstallAll();
    }

    private static EquipmentUpgrade CreateOrUpdateUpgrade(
        string path,
        string id,
        string displayName,
        string description,
        int day,
        int price,
        EquipmentUpgradeEffect effect,
        string iconPath)
    {
        EquipmentUpgrade upgrade = AssetDatabase.LoadAssetAtPath<EquipmentUpgrade>(path);
        bool created = upgrade == null;
        if (upgrade == null)
        {
            upgrade = ScriptableObject.CreateInstance<EquipmentUpgrade>();
            AssetDatabase.CreateAsset(upgrade, path);
        }

        upgrade.itemID = id;
        upgrade.effect = effect;
        upgrade.catalogSection = EquipmentCatalogSection.Upgrades;
        if (created)
        {
            upgrade.displayName = displayName;
            upgrade.description = description;
            upgrade.dayToUnlock = day;
            upgrade.cost = price;
            upgrade.carryCapacity = 4;
            if (effect == EquipmentUpgradeEffect.CardPayment)
            {
                upgrade.cardPaymentChance = 0.5f;
                upgrade.playerPrioritySeconds = 5f;
                upgrade.successCloseDelay = 0.5f;
            }
        }

        if (upgrade.sprite == null)
            upgrade.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        EditorUtility.SetDirty(upgrade);
        return upgrade;
    }

    private static void InstallLobbyScene(params EquipmentUpgrade[] upgrades)
    {
        Scene scene = SceneManager.GetSceneByPath(LobbyScenePath);
        bool openedForInstall = !scene.IsValid() || !scene.isLoaded;
        if (openedForInstall)
            scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Additive);

        GameObject cardRoot = FindInScene(scene, "CardPaymentUI");
        if (cardRoot == null)
        {
            Debug.LogError("[CardPaymentAuthoring] CardPaymentUI was not found in Lobby1.");
        }
        else if (AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath) == null)
        {
            AuthorCardPaymentUI(cardRoot);
            cardRoot.SetActive(false);
            PrefabUtility.SaveAsPrefabAssetAndConnect(
                cardRoot,
                CardPrefabPath,
                InteractionMode.AutomatedAction);
        }

        EnsureTrolleyParkingPoints(scene);

        EquipmentManager[] managers = Object.FindObjectsByType<EquipmentManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            EquipmentManager manager = managers[i];
            if (manager == null || manager.gameObject.scene != scene)
                continue;
            SerializedObject serialized = new SerializedObject(manager);
            SerializedProperty list = serialized.FindProperty("allEquipment");
            for (int u = 0; u < upgrades.Length; u++)
                AddUniqueObjectReference(list, upgrades[u]);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (openedForInstall)
            EditorSceneManager.CloseScene(scene, true);
    }

    private static void UpgradeCardPaymentPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        CardPaymentUI current = prefab != null ? prefab.GetComponent<CardPaymentUI>() : null;
        if (current == null || current.AuthoringVersion >= CardPrefabAuthoringVersion)
            return;

        GameObject contents = PrefabUtility.LoadPrefabContents(CardPrefabPath);
        try
        {
            AuthorCardPaymentUI(contents);
            PrefabUtility.SaveAsPrefabAsset(contents, CardPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void EnsureTrolleyParkingPoints(Scene scene)
    {
        GameObject parkingRoot = FindInScene(scene, "TrolleyParkingPoints");
        if (parkingRoot == null)
        {
            parkingRoot = new GameObject("TrolleyParkingPoints");
            SceneManager.MoveGameObjectToScene(parkingRoot, scene);
            parkingRoot.transform.position = Vector3.zero;
            parkingRoot.transform.rotation = Quaternion.identity;
            parkingRoot.transform.localScale = Vector3.one;
        }

        KitchenManager kitchen = FindSceneComponent<KitchenManager>(scene);
        SinkInteractable sceneSink = FindSceneComponent<SinkInteractable>(scene);
        Transform waiterHome = FindInScene(scene, "WaiterHomePoint")?.transform;
        Transform busserHome = FindInScene(scene, "BusserHomePoint")?.transform;

        if (FindInScene(scene, "WaiterTrolleyParkingPoint") == null)
        {
            Vector3 foodCentre = waiterHome != null ? waiterHome.position : Vector3.zero;
            int count = 0;
            if (kitchen != null && kitchen.traySpawnPoints != null)
            {
                foodCentre = Vector3.zero;
                for (int i = 0; i < kitchen.traySpawnPoints.Length; i++)
                {
                    Transform spawn = kitchen.traySpawnPoints[i];
                    if (spawn == null) continue;
                    foodCentre += spawn.position;
                    count++;
                }
                if (count > 0)
                    foodCentre /= count;
            }

            Vector3 point = foodCentre;
            if (waiterHome != null)
            {
                Vector3 towardOpenFloor = waiterHome.position - foodCentre;
                towardOpenFloor.y = 0f;
                if (towardOpenFloor.sqrMagnitude > 0.001f)
                    point += towardOpenFloor.normalized * 1.35f;
                point.y = waiterHome.position.y;
            }
            EnsureParkingPoint(parkingRoot.transform, "WaiterTrolleyParkingPoint", point, foodCentre);
        }

        if (FindInScene(scene, "BusserTrolleyParkingPoint") == null)
        {
            Vector3 sinkPosition = sceneSink != null && sceneSink.StandPoint != null
                ? sceneSink.StandPoint.position
                : busserHome != null ? busserHome.position : Vector3.zero;
            Vector3 point = sinkPosition;
            if (busserHome != null)
            {
                Vector3 towardOpenFloor = busserHome.position - sinkPosition;
                towardOpenFloor.y = 0f;
                if (towardOpenFloor.sqrMagnitude > 0.001f)
                    point += towardOpenFloor.normalized * 0.9f;
                point.y = busserHome.position.y;
            }
            EnsureParkingPoint(parkingRoot.transform, "BusserTrolleyParkingPoint", point, sinkPosition);
        }

        EditorUtility.SetDirty(parkingRoot);
    }

    private static void EnsureParkingPoint(
        Transform parent,
        string pointName,
        Vector3 position,
        Vector3 lookAt)
    {
        GameObject point = new GameObject(pointName);
        point.transform.SetParent(parent, true);
        point.transform.position = position;
        Vector3 facing = lookAt - position;
        facing.y = 0f;
        point.transform.rotation = facing.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(facing.normalized, Vector3.up)
            : Quaternion.identity;
        point.transform.localScale = Vector3.one;
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        T[] components = Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component != null && component.gameObject.scene == scene)
                return component;
        }
        return null;
    }

    private static void AuthorCardPaymentUI(GameObject root)
    {
        root.layer = LayerMask.NameToLayer("UI");
        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);
        rootRect.localScale = Vector3.one;

        OrderChecklistUI accidentalOrderUI = root.GetComponent<OrderChecklistUI>();
        if (accidentalOrderUI != null)
            Object.DestroyImmediate(accidentalOrderUI, true);

        RawImage background = root.GetComponent<RawImage>();
        if (background != null)
        {
            background.raycastTarget = true;
            Stretch(background.rectTransform);
        }

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group == null) group = root.AddComponent<CanvasGroup>();

        RectTransform safe = GetOrCreateRect("SafeAreaContent", root.transform);
        Stretch(safe);

        Transform foregroundTransform = FindDeep(root.transform, "Foreground");
        if (foregroundTransform == null)
            foregroundTransform = GetOrCreateRect("Foreground", safe).transform;
        foregroundTransform.SetParent(safe, false);
        RectTransform foreground = (RectTransform)foregroundTransform;
        Stretch(foreground);
        foreground.offsetMin = new Vector2(28f, 24f);
        foreground.offsetMax = new Vector2(-28f, -24f);
        Image foregroundImage = foreground.GetComponent<Image>();
        if (foregroundImage != null)
            foregroundImage.color = Color.clear;

        RectTransform posRect = FindDeep(foreground, "HandHeldPOS") as RectTransform;
        RectTransform cardRect = FindDeep(foreground, "Card") as RectTransform;
        RectTransform exitRect = FindDeep(root.transform, "ExitButton") as RectTransform;
        if (posRect == null || cardRect == null || exitRect == null)
        {
            Debug.LogError("[CardPaymentAuthoring] POS, Card or ExitButton is missing.", root);
            return;
        }

        posRect.SetParent(foreground, false);
        posRect.anchorMin = posRect.anchorMax = new Vector2(0.65f, 0.5f);
        posRect.pivot = new Vector2(0.5f, 0.5f);
        posRect.anchoredPosition = Vector2.zero;
        posRect.sizeDelta = new Vector2(360f, 360f);
        posRect.localScale = Vector3.one;
        posRect.localRotation = Quaternion.identity;
        Image posImage = posRect.GetComponent<Image>();
        if (posImage != null) posImage.preserveAspect = true;

        cardRect.SetParent(foreground, false);
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.23f, 0.48f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(200f, 200f);
        cardRect.localScale = Vector3.one;
        cardRect.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Image cardImage = cardRect.GetComponent<Image>();
        if (cardImage != null)
        {
            cardImage.preserveAspect = true;
            cardImage.raycastTarget = true;
        }

        exitRect.SetParent(safe, false);
        exitRect.anchorMin = exitRect.anchorMax = new Vector2(1f, 1f);
        exitRect.pivot = new Vector2(1f, 1f);
        exitRect.anchoredPosition = new Vector2(-18f, -18f);
        exitRect.sizeDelta = new Vector2(82f, 82f);

        RemoveLegacyCardAuthoringObjects(foreground, posRect, exitRect);

        RectTransform slot = GetOrCreateRect("CardSlotTarget", posRect);
        slot.SetParent(posRect, false);
        slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0.1f);
        slot.pivot = new Vector2(0.5f, 0.5f);
        slot.anchoredPosition = Vector2.zero;
        slot.sizeDelta = new Vector2(170f, 130f);
        slot.localScale = Vector3.one;
        slot.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Image slotDebug = slot.GetComponent<Image>();
        if (slotDebug != null) Object.DestroyImmediate(slotDebug, true);

        DisableLegacyCardTexts(foreground, posRect, exitRect);

        TMP_Text screen = GetOrCreateText(
            "TerminalStatusText",
            posRect,
            "TOTAL  ₱0\nINSERT CARD",
            26f,
            TextAlignmentOptions.Center);
        screen.rectTransform.SetParent(posRect, false);
        screen.rectTransform.anchorMin = new Vector2(0.31f, 0.52f);
        screen.rectTransform.anchorMax = new Vector2(0.69f, 0.67f);
        screen.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        screen.rectTransform.offsetMin = Vector2.zero;
        screen.rectTransform.offsetMax = Vector2.zero;
        screen.rectTransform.localScale = Vector3.one;
        screen.rectTransform.localRotation = Quaternion.identity;
        screen.enableAutoSizing = true;
        screen.fontSizeMin = 15f;
        screen.fontSizeMax = 25f;
        screen.color = Color.white;
        screen.raycastTarget = false;

        Sprite idle = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Art/Icons/GameIcons/CardPayment/HandHeldPOS.png");
        Sprite inserted = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Art/Icons/GameIcons/CardPayment/CardInserted.png");
        if (posImage != null) posImage.sprite = idle;

        Button close = exitRect.GetComponent<Button>();
        CardPaymentUI controller = root.GetComponent<CardPaymentUI>();
        if (controller == null) controller = root.AddComponent<CardPaymentUI>();
        controller.ConfigureReferences(
            rootRect, safe, foreground, posImage, idle, inserted,
            cardRect, slot, screen, close, group);
        controller.ConfigureAuthoringVersion(CardPrefabAuthoringVersion);

        CardPaymentDraggableCard drag = cardRect.GetComponent<CardPaymentDraggableCard>();
        if (drag == null) drag = cardRect.gameObject.AddComponent<CardPaymentDraggableCard>();
        drag.Configure(controller, cardRect);
        SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
        EditorUtility.SetDirty(root);
    }

    private static void UpgradeMoneyBubblePrefab()
    {
        const string path = "Assets/_Project/Restaurant/Assets/Level1/UI/Money.prefab";
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            MoneyBubbleUI ui = contents.GetComponentInChildren<MoneyBubbleUI>(true);
            if (ui == null) return;
            RectTransform iconRect = FindDeep(contents.transform, "PaymentIcon") as RectTransform;
            if (iconRect == null)
            {
                GameObject iconObject = new GameObject("PaymentIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.SetParent(ui.transform, false);
            }
            Center(iconRect, new Vector2(104f, 104f), Vector2.zero);
            iconRect.SetAsLastSibling();
            Image icon = iconRect.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            Button paymentButton = ui.GetComponentInChildren<Button>(true);
            Image paymentFrame = paymentButton != null ? paymentButton.image : null;
            Sprite cashFrame = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/UI/Assets/Legacy/Buttons/InGameHUD/Interactables/Cash.png");
            Sprite cardFrame = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/UI/Assets/Legacy/Buttons/InGameHUD/Interactables/Popup Frame.png");
            Sprite cardIcon = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Icons/GameIcons/Upgrades/CardPaymentIcon.png");
            ui.ConfigurePaymentPresentation(
                paymentFrame, cashFrame, cardFrame,
                icon, null, cardIcon);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void RemoveLegacyCardAuthoringObjects(
        RectTransform foreground,
        RectTransform posRect,
        RectTransform exitButton)
    {
        if (foreground == null || posRect == null)
            return;

        RectTransform[] rects = foreground.GetComponentsInChildren<RectTransform>(true);
        for (int i = rects.Length - 1; i >= 0; i--)
        {
            RectTransform rect = rects[i];
            if (rect == null || rect == foreground || rect == posRect || rect == exitButton)
                continue;

            bool obsoleteSlot = rect.name == "CardSlotTarget" && rect.parent != posRect;
            bool obsoleteStatus = rect.name == "TerminalStatusText" && rect.parent != posRect;
            if (obsoleteSlot || obsoleteStatus)
                Object.DestroyImmediate(rect.gameObject, true);
        }
    }

    private static void DisableLegacyCardTexts(
        RectTransform foreground,
        RectTransform posRect,
        RectTransform exitButton)
    {
        if (foreground == null)
            return;

        TMP_Text[] labels = foreground.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null)
                continue;
            if (label.name == "TerminalStatusText" && label.transform.parent == posRect)
                continue;
            if (exitButton != null && label.transform.IsChildOf(exitButton))
                continue;
            label.gameObject.SetActive(false);
        }
    }

    private static void CreateOrUpgradeTrolleyPrefab(
        string prefabPath,
        string materialPath,
        Color color,
        EquipmentUpgradeEffect effect)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Art/Models/3D Models/Trolly/Trolly (1).fbx");
        if (model == null)
        {
            Debug.LogError("[CardPaymentAuthoring] Trolley FBX is missing.");
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(materialPath) };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.color = color;
        EditorUtility.SetDirty(material);

        bool existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
        GameObject root = existingPrefab
            ? PrefabUtility.LoadPrefabContents(prefabPath)
            : new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
        try
        {
            BotTrolleyCarrier existingCarrier = root.GetComponent<BotTrolleyCarrier>();
            if (existingCarrier != null &&
                existingCarrier.AuthoringVersion >= TrolleyPrefabAuthoringVersion)
            {
                return;
            }

            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Transform visualTransform = FindDeep(root.transform, "TrolleyModel");
            GameObject visual;
            if (visualTransform == null)
            {
                visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "TrolleyModel";
                visual.transform.SetParent(root.transform, false);
            }
            else
            {
                visual = visualTransform.gameObject;
            }

            // The imported FBX is already converted to Unity's Y-up space.
            // Keeping the visual at identity prevents the previous vertical cart.
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            // The source FBX is authored at a miniature scale. Keep the root
            // prefab at 1 so parking/push offsets stay intuitive, and scale
            // only the editable visual child to restaurant-prop size.
            visual.transform.localScale = Vector3.one * 2.5f;
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                for (int m = 0; m < materials.Length; m++) materials[m] = material;
                renderers[i].sharedMaterials = materials;
            }

            Vector3[] positions =
            {
                new Vector3(-0.28f, 0.76f, 0.05f),
                new Vector3(0.28f, 0.76f, 0.05f),
                new Vector3(-0.28f, 1.08f, 0.05f),
                new Vector3(0.28f, 1.08f, 0.05f)
            };
            List<Transform> slots = new List<Transform>(positions.Length);
            for (int i = 0; i < positions.Length; i++)
            {
                string slotName = "TraySlot" + (i + 1);
                Transform slot = FindDeep(root.transform, slotName);
                if (slot == null)
                {
                    GameObject slotObject = new GameObject(slotName);
                    slot = slotObject.transform;
                    slot.SetParent(root.transform, false);
                }
                slot.localPosition = positions[i];
                slot.localRotation = Quaternion.identity;
                slot.localScale = Vector3.one;
                slots.Add(slot);
            }

            BotTrolleyCarrier carrier = existingCarrier != null
                ? existingCarrier
                : root.AddComponent<BotTrolleyCarrier>();
            carrier.ConfigureAuthoring(effect, slots, TrolleyPrefabAuthoringVersion);
            EditorUtility.SetDirty(carrier);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            if (existingPrefab)
                PrefabUtility.UnloadPrefabContents(root);
            else
                Object.DestroyImmediate(root);
        }
    }

    private static void CreateUnlockCelebrationPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(UnlockPrefabPath) != null)
            return;

        GameObject root = new GameObject(
            "UnlockCelebrationUI",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(Image),
            typeof(UnlockCelebrationUI));
        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Image dim = root.GetComponent<Image>();
            dim.color = new Color(0f, 0.03f, 0.08f, 0.78f);

            RectTransform safe = GetOrCreateRect("SafeAreaContent", root.transform);
            Stretch(safe);
            RectTransform panel = GetOrCreateRect("BluePanel", safe);
            Center(panel, new Vector2(760f, 520f), Vector2.zero);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/MainMenu/Assets/Buttons/Frames/9Sliced.png");
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(0.05f, 0.31f, 0.55f, 1f);

            TMP_Text heading = GetOrCreateText(
                "Heading", panel, "NEW UNLOCK!", 48f, TextAlignmentOptions.Center);
            Center(heading.rectTransform, new Vector2(600f, 70f), new Vector2(0f, 198f));
            heading.fontStyle = FontStyles.Bold;

            RectTransform iconRect = GetOrCreateRect("UnlockIcon", panel);
            Center(iconRect, new Vector2(170f, 170f), new Vector2(-225f, 38f));
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;

            TMP_Text title = GetOrCreateText(
                "ItemName", panel, "ITEM NAME", 38f, TextAlignmentOptions.Left);
            Center(title.rectTransform, new Vector2(430f, 70f), new Vector2(105f, 95f));
            title.fontStyle = FontStyles.Bold;

            TMP_Text description = GetOrCreateText(
                "Description", panel, "Description", 26f, TextAlignmentOptions.TopLeft);
            Center(description.rectTransform, new Vector2(430f, 145f), new Vector2(105f, -18f));
            description.enableAutoSizing = true;
            description.fontSizeMin = 18f;
            description.fontSizeMax = 28f;

            TMP_Text location = GetOrCreateText(
                "Location", panel, "AVAILABLE IN THE COMPUTER", 21f, TextAlignmentOptions.Center);
            Center(location.rectTransform, new Vector2(620f, 45f), new Vector2(0f, -150f));
            location.color = new Color(0.78f, 0.93f, 1f, 1f);

            Button continueButton = CreateButton(
                "ContinueButton", panel, "CONTINUE", new Vector2(250f, 70f), new Vector2(0f, -218f),
                "Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/Green/Default/button_rectangle_depth_flat.png");
            Button close = CreateIconButton(
                "CloseButton", panel, new Vector2(68f, 68f), new Vector2(-18f, -18f),
                "Assets/_Project/UI/Assets/Legacy/Buttons/Menu/Close Button.png");
            RectTransform closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-18f, -18f);

            UnlockCelebrationUI controller = root.GetComponent<UnlockCelebrationUI>();
            controller.ConfigureReferences(
                safe, panel, icon, title, description, location,
                continueButton, close, root.GetComponent<CanvasGroup>());
            SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
            root.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, UnlockPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Button CreateButton(
        string name, Transform parent, string label, Vector2 size, Vector2 position, string spritePath)
    {
        RectTransform rect = GetOrCreateRect(name, parent);
        Center(rect, size, position);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        image.type = Image.Type.Sliced;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = GetOrCreateText("Label", rect, label, 28f, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        text.fontStyle = FontStyles.Bold;
        return button;
    }

    private static Button CreateIconButton(
        string name, Transform parent, Vector2 size, Vector2 position, string spritePath)
    {
        RectTransform rect = GetOrCreateRect(name, parent);
        Center(rect, size, position);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        image.preserveAspect = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static TMP_Text GetOrCreateText(
        string name, Transform parent, string value, float size, TextAlignmentOptions alignment)
    {
        Transform existing = parent.Find(name);
        TextMeshProUGUI text;
        if (existing != null)
            text = existing.GetComponent<TextMeshProUGUI>();
        else
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            text = go.GetComponent<TextMeshProUGUI>();
        }
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform GetOrCreateRect(string name, Transform parent)
    {
        Transform existing = parent.Find(name);
        if (existing is RectTransform rect)
            return rect;
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private static GameObject FindInScene(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindDeep(roots[i].transform, name);
            if (found != null) return found.gameObject;
        }
        return null;
    }

    private static void AddUniqueObjectReference(SerializedProperty list, Object value)
    {
        if (list == null || value == null) return;
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == value)
                return;
        }
        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).objectReferenceValue = value;
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void Center(RectTransform rect, Vector2 size, Vector2 position)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null || layer < 0) return;
        root.layer = layer;
        for (int i = 0; i < root.transform.childCount; i++)
            SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
