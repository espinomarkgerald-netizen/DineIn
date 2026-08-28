#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Fast asset check for the three connected upgrade features.</summary>
[InitializeOnLoad]
public static class CardPaymentTrolleyEquipmentSmokeTest
{
    private const string CardPath = "Assets/_Project/Resources/UI/CardPaymentUI.prefab";
    private const string WaiterPath = "Assets/_Project/Resources/Upgrades/WaiterTrolley.prefab";
    private const string BusserPath = "Assets/_Project/Resources/Upgrades/BusserTrolley.prefab";
    private const string WaiterBotPath = "Assets/_Project/Lobby/Assets/Waiter/Waiter.prefab";
    private const string BusserBotPath = "Assets/_Project/Lobby/Assets/Busser/Busser.prefab";
    private const string MoneyBubblePath = "Assets/_Project/Restaurant/Assets/Level1/UI/Money.prefab";
    private const string EquipmentCardPath = "Assets/_Project/Resources/ManagementComputer/ManagementEquipmentCard.prefab";
    private const string EquipmentSectionPath = "Assets/_Project/Resources/ManagementComputer/ManagementEquipmentSection.prefab";
    private const string UnlockPath = "Assets/_Project/Resources/UI/UnlockCelebrationUI.prefab";
    private const string LobbyPath = "Assets/_Project/Scenes/RoleBased/Lobby1.unity";
    private const string UpgradeFolder = "Assets/_Project/Office/Manager/Equipment/Upgrades";

    static CardPaymentTrolleyEquipmentSmokeTest()
    {
        // Authoring installers run on the first editor tick after compilation.
        // Validate on the following tick so the check observes saved assets.
        EditorApplication.delayCall += () => EditorApplication.delayCall += ValidateAssets;
    }

    [MenuItem("Tools/Dine In/Validate Card + Trolley + Equipment Assets")]
    public static void ValidateAssets()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        int failures = 0;
        failures += ValidateCardPayment();
        failures += ValidatePaymentBubble();
        failures += ValidateTrolley(WaiterPath, EquipmentUpgradeEffect.WaiterTrolley);
        failures += ValidateTrolley(BusserPath, EquipmentUpgradeEffect.BusserTrolley);
        failures += ValidateBotTrolleyGrip(WaiterBotPath, true);
        failures += ValidateBotTrolleyGrip(BusserBotPath, false);
        failures += ValidateTrolleyUpgradeAssetsAndParking();
        failures += ValidateEquipmentCatalog();
        failures += ValidateUnlockCelebration();

        if (failures == 0)
            Debug.Log("[UpgradeAssetSmokeTest] Card payment, trolley tools, and equipment catalog assets passed.");
        else
            Debug.LogError("[UpgradeAssetSmokeTest] " + failures + " authored asset check(s) failed.");
    }

    private static int ValidateCardPayment()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPath);
        CardPaymentUI ui = prefab != null ? prefab.GetComponent<CardPaymentUI>() : null;
        if (ui == null || ui.AuthoringVersion < 4)
            return Fail("CardPaymentUI prefab is missing or has not been migrated to version 4.", prefab);

        RectTransform root = prefab.transform as RectTransform;
        Transform foreground = FindDeep(prefab.transform, "Foreground");
        Transform pos = FindDeep(prefab.transform, "HandHeldPOS");
        Transform card = FindDeep(prefab.transform, "Card");
        Transform slot = pos != null ? pos.Find("CardSlotTarget") : null;
        int failures = 0;
        if (!IsFullStretch(root))
            failures += Fail("Card-payment background is not full-screen stretched.", prefab);
        if (!IsFullStretch(foreground as RectTransform))
            failures += Fail("Card-payment foreground is not stretched to its safe area.", prefab);
        if (card == null || Mathf.Abs(Mathf.DeltaAngle(card.localEulerAngles.z, 90f)) > 1f)
            failures += Fail("The payment card is not authored vertically.", prefab);
        if (slot == null || (slot as RectTransform).anchorMin.y > 0.2f)
            failures += Fail("The card slot is not authored at the bottom of the handheld POS.", prefab);
        RectTransform posRect = pos as RectTransform;
        RectTransform cardRect = card as RectTransform;
        if (posRect == null || posRect.sizeDelta.x > 400f || posRect.sizeDelta.y > 400f)
            failures += Fail("The handheld POS is oversized for the 800 x 450 gameplay canvas.", prefab);
        if (cardRect == null || cardRect.sizeDelta.x > 240f || cardRect.sizeDelta.y > 240f)
            failures += Fail("The payment card is oversized for the responsive interaction panel.", prefab);
        if (CountNamed(prefab.transform, "CardSlotTarget") != 1 ||
            CountNamed(prefab.transform, "TerminalStatusText") != 1)
            failures += Fail("Legacy card-payment targets or status labels are still duplicated.", prefab);
        return failures;
    }

    private static int ValidatePaymentBubble()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MoneyBubblePath);
        MoneyBubbleUI bubble = prefab != null ? prefab.GetComponentInChildren<MoneyBubbleUI>(true) : null;
        if (bubble == null)
            return Fail("Money bubble prefab is missing its MoneyBubbleUI.", prefab);

        SerializedObject serialized = new SerializedObject(bubble);
        bool hasFrame = serialized.FindProperty("paymentFrame")?.objectReferenceValue != null;
        bool hasCashFrame = serialized.FindProperty("cashFrameSprite")?.objectReferenceValue != null;
        bool hasCardFrame = serialized.FindProperty("cardFrameSprite")?.objectReferenceValue != null;
        bool hasCardIcon = serialized.FindProperty("cardIcon")?.objectReferenceValue != null;
        return hasFrame && hasCashFrame && hasCardFrame && hasCardIcon
            ? 0
            : Fail("Money bubble is missing editable cash/card frame or card-icon references.", prefab);
    }

    private static int ValidateTrolley(string path, EquipmentUpgradeEffect expectedEffect)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        BotTrolleyCarrier carrier = prefab != null ? prefab.GetComponent<BotTrolleyCarrier>() : null;
        int failures = 0;
        if (carrier == null || carrier.AuthoringVersion < 8 || carrier.Effect != expectedEffect)
            return Fail("Trolley prefab is missing its authored carrier configuration: " + path, prefab);
        if (CountTraySlots(prefab.transform) != 4 || carrier.TraySlots.Count != 4)
            failures += Fail("Trolley prefab must expose exactly four editable tray slots: " + path, prefab);
        Transform pivot = FindDeep(prefab.transform, "VisualPivot");
        Transform model = FindDeep(prefab.transform, "TrolleyModel");
        Transform holdingPoint = FindDeep(prefab.transform, "HoldingPoint");
        if (pivot == null || carrier.VisualRoot != pivot)
            failures += Fail("Trolley prefab is missing its editable visual pivot: " + path, prefab);
        if (holdingPoint == null || carrier.HoldingPoint != holdingPoint)
            failures += Fail("Trolley prefab is missing its editable HoldingPoint: " + path, prefab);
        if (!HasIdentityGameplayRoot(prefab.transform))
            failures += Fail("Trolley gameplay root must stay at identity; put visual corrections on VisualPivot: " + path, prefab);
        if (carrier.MinimumBatchSize != 2)
            failures += Fail("Purchased trolleys must batch two-to-four trays: " + path, prefab);
        if (carrier.MovementSpeedMultiplier <= 1f || carrier.MovementSpeedMultiplier > 1.5f)
            failures += Fail("Trolley movement boost must stay editable between 1.0x and 1.5x: " + path, prefab);
        if (carrier.AccelerationMultiplier < 1f || carrier.AccelerationMultiplier > 1.5f)
            failures += Fail("Trolley acceleration boost must stay editable between 1.0x and 1.5x: " + path, prefab);
        if (carrier.ParkingNavMeshSampleRadius < 0.25f)
            failures += Fail("Trolley needs an editable NavMesh parking approach search radius: " + path, prefab);
        if (model == null || model.localScale.x <= 0f ||
            !Mathf.Approximately(model.localScale.x, model.localScale.y) ||
            !Mathf.Approximately(model.localScale.x, model.localScale.z))
            failures += Fail("Trolley visual must use a positive uniform scale: " + path, prefab);
        if (holdingPoint != null)
        {
            Collider[] holdingColliders = holdingPoint.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < holdingColliders.Length; i++)
            {
                if (holdingColliders[i] != null && holdingColliders[i].enabled && !holdingColliders[i].isTrigger)
                    failures += Fail("HoldingPoint must not contain an enabled solid collider: " + path, prefab);
            }
            if (holdingPoint.GetComponent<Renderer>() != null ||
                holdingPoint.GetComponent<MeshFilter>() != null ||
                holdingPoint.GetComponent<Rigidbody>() != null)
            {
                failures += Fail("HoldingPoint must be an empty non-physical transform: " + path, prefab);
            }
            if ((holdingPoint.localScale - Vector3.one).sqrMagnitude > 0.000001f)
                failures += Fail("HoldingPoint must keep unit local scale: " + path, prefab);
        }

        for (int i = 0; i < carrier.TraySlots.Count; i++)
        {
            Transform slot = carrier.TraySlots[i];
            if (slot == null)
                continue;
            if ((slot.localScale - Vector3.one).sqrMagnitude > 0.000001f)
                failures += Fail($"TraySlot{i + 1} must keep unit local scale: {path}", prefab);
            if (slot.localPosition.y < -0.01f)
                failures += Fail($"TraySlot{i + 1} is below the trolley ground plane: {path}", prefab);
        }

        GameObject loadedRoot = null;
        try
        {
            // Renderer.bounds is not reliable on a persistent prefab asset because it has
            // never entered a preview scene. Validate the loaded prefab contents instead,
            // which matches the hierarchy and transform state used by a player build.
            loadedRoot = PrefabUtility.LoadPrefabContents(path);
            BotTrolleyCarrier loadedCarrier = loadedRoot.GetComponent<BotTrolleyCarrier>();
            Renderer[] renderers = loadedCarrier != null && loadedCarrier.VisualRoot != null
                ? loadedCarrier.VisualRoot.GetComponentsInChildren<Renderer>(true)
                : System.Array.Empty<Renderer>();
            if (renderers.Length == 0)
            {
                failures += Fail("Trolley prefab has no renderers: " + path, prefab);
            }
            else
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null || !renderer.enabled || renderer.sharedMaterial == null)
                        failures += Fail("Trolley has a disabled renderer or missing material: " + path, prefab);
                    else if (i > 0)
                        bounds.Encapsulate(renderer.bounds);
                }

                if (bounds.size.y < 0.75f || bounds.size.y > 4.25f)
                    failures += Fail(
                        $"Trolley visual height {bounds.size.y:0.###} is outside the usable bot-scale range: {path}",
                        prefab);
                if (Mathf.Abs(bounds.min.y - loadedRoot.transform.position.y) > 0.08f)
                    failures += Fail(
                        $"Trolley visual ground offset {bounds.min.y - loadedRoot.transform.position.y:0.###} is too large: {path}",
                        prefab);
            }
        }
        finally
        {
            if (loadedRoot != null)
                PrefabUtility.UnloadPrefabContents(loadedRoot);
        }
        return failures;
    }

    private static int ValidateBotTrolleyGrip(string path, bool waiter)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            return Fail("Missing trolley operator prefab: " + path, null);

        Transform grip = FindDeep(prefab.transform, "TrolleyGripPoint");
        bool assigned = waiter
            ? prefab.GetComponent<WaiterHands>()?.HasDedicatedTrolleyGrip == true
            : prefab.GetComponent<BusserHands>()?.HasDedicatedTrolleyGrip == true;
        int failures = 0;
        if (grip == null || !assigned)
            failures += Fail("Bot prefab is missing its assigned TrolleyGripPoint: " + path, prefab);
        if (grip != null && (grip.localScale - Vector3.one).sqrMagnitude > 0.000001f)
            failures += Fail("TrolleyGripPoint must keep unit local scale: " + path, prefab);
        if (grip != null && (grip.GetComponent<Collider>() != null || grip.GetComponent<Renderer>() != null))
            failures += Fail("TrolleyGripPoint must be an empty non-physical transform: " + path, prefab);
        return failures;
    }

    private static int ValidateTrolleyUpgradeAssetsAndParking()
    {
        EquipmentUpgrade waiter = AssetDatabase.LoadAssetAtPath<EquipmentUpgrade>(
            UpgradeFolder + "/Waiter Trolley.asset");
        EquipmentUpgrade busser = AssetDatabase.LoadAssetAtPath<EquipmentUpgrade>(
            UpgradeFolder + "/Busser Trolley.asset");
        int failures = 0;
        if (waiter == null || waiter.itemID != EquipmentUpgradeService.WaiterTrolleyID ||
            waiter.effect != EquipmentUpgradeEffect.WaiterTrolley)
        {
            failures += Fail("Waiter trolley upgrade asset is missing or has the wrong ID/effect.", waiter);
        }
        if (busser == null || busser.itemID != EquipmentUpgradeService.BusserTrolleyID ||
            busser.effect != EquipmentUpgradeEffect.BusserTrolley)
        {
            failures += Fail("Busser trolley upgrade asset is missing or has the wrong ID/effect.", busser);
        }

        if (!File.Exists(LobbyPath))
            return failures + Fail("Lobby1 scene asset is missing.", null);

        string sceneYaml = File.ReadAllText(LobbyPath);
        if (!sceneYaml.Contains("m_Name: WaiterTrolleyParkingPoint"))
            failures += Fail("Lobby1 is missing WaiterTrolleyParkingPoint.", null);
        if (!sceneYaml.Contains("m_Name: BusserTrolleyParkingPoint"))
            failures += Fail("Lobby1 is missing BusserTrolleyParkingPoint.", null);
        return failures;
    }

    private static int ValidateEquipmentCatalog()
    {
        GameObject card = AssetDatabase.LoadAssetAtPath<GameObject>(EquipmentCardPath);
        GameObject section = AssetDatabase.LoadAssetAtPath<GameObject>(EquipmentSectionPath);
        int failures = 0;
        if (card == null || card.GetComponent<ManagementEquipmentCardUI>() == null)
            failures += Fail("Editable equipment card prefab is missing.", card);
        if (section == null || section.GetComponent<ManagementEquipmentSectionUI>() == null ||
            FindDeep(section.transform, "Divider") == null || FindDeep(section.transform, "Cards") == null)
            failures += Fail("Editable equipment section prefab is missing its divider or responsive grid.", section);
        return failures;
    }

    private static int ValidateUnlockCelebration()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnlockPath);
        UnlockCelebrationUI ui = prefab != null ? prefab.GetComponent<UnlockCelebrationUI>() : null;
        if (ui == null || ui.AuthoringVersion < 2)
            return Fail("Unlock celebration prefab is missing its responsive mobile migration.", prefab);

        int failures = 0;
        RectTransform safe = FindDeep(prefab.transform, "SafeAreaContent") as RectTransform;
        RectTransform panel = FindDeep(prefab.transform, "BluePanel") as RectTransform;
        RectTransform close = FindDeep(prefab.transform, "CloseButton") as RectTransform;
        RectTransform continueButton = FindDeep(prefab.transform, "ContinueButton") as RectTransform;
        CanvasScaler scaler = prefab.GetComponent<CanvasScaler>();
        if (!IsFullStretch(safe))
            failures += Fail("Unlock celebration safe-area root is not stretched.", prefab);
        if (panel == null || panel.sizeDelta.x > 760f || panel.sizeDelta.y > 520f)
            failures += Fail("Unlock celebration panel is too large for the mobile layout.", prefab);
        if (close == null || close.sizeDelta.x < 80f || close.sizeDelta.y < 80f)
            failures += Fail("Unlock celebration close button is below the mobile tap target.", prefab);
        if (continueButton == null || continueButton.sizeDelta.y < 80f)
            failures += Fail("Unlock celebration continue button is below the mobile tap target.", prefab);
        if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            failures += Fail("Unlock celebration canvas is not screen-size responsive.", prefab);
        return failures;
    }

    private static int CountNamed(Transform root, string name)
    {
        if (root == null) return 0;
        int count = root.name == name ? 1 : 0;
        for (int i = 0; i < root.childCount; i++)
            count += CountNamed(root.GetChild(i), name);
        return count;
    }

    private static int CountTraySlots(Transform root)
    {
        if (root == null) return 0;
        int count = root.name.StartsWith("TraySlot") ? 1 : 0;
        for (int i = 0; i < root.childCount; i++)
            count += CountTraySlots(root.GetChild(i));
        return count;
    }

    private static bool IsFullStretch(RectTransform rect)
    {
        return rect != null && rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one;
    }

    private static bool HasIdentityGameplayRoot(Transform root)
    {
        return root != null &&
               root.localPosition.sqrMagnitude <= 0.000001f &&
               Quaternion.Angle(root.localRotation, Quaternion.identity) <= 0.001f &&
               (root.localScale - Vector3.one).sqrMagnitude <= 0.000001f;
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

    private static int Fail(string message, Object context)
    {
        Debug.LogError("[UpgradeAssetSmokeTest] " + message, context);
        return 1;
    }
}
#endif
