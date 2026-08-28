#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Fast asset check for the three connected upgrade features.</summary>
[InitializeOnLoad]
public static class CardPaymentTrolleyEquipmentSmokeTest
{
    private const string CardPath = "Assets/_Project/Resources/UI/CardPaymentUI.prefab";
    private const string WaiterPath = "Assets/_Project/Resources/Upgrades/WaiterTrolley.prefab";
    private const string BusserPath = "Assets/_Project/Resources/Upgrades/BusserTrolley.prefab";
    private const string MoneyBubblePath = "Assets/_Project/Restaurant/Assets/Level1/UI/Money.prefab";
    private const string EquipmentCardPath = "Assets/_Project/Resources/ManagementComputer/ManagementEquipmentCard.prefab";
    private const string EquipmentSectionPath = "Assets/_Project/Resources/ManagementComputer/ManagementEquipmentSection.prefab";

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
        failures += ValidateEquipmentCatalog();

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
        if (carrier == null || carrier.AuthoringVersion < 3 || carrier.Effect != expectedEffect)
            return Fail("Trolley prefab is missing its authored carrier configuration: " + path, prefab);
        if (CountTraySlots(prefab.transform) != 4)
            failures += Fail("Trolley prefab must expose exactly four editable tray slots: " + path, prefab);
        Transform model = FindDeep(prefab.transform, "TrolleyModel");
        if (model == null || Quaternion.Angle(model.localRotation, Quaternion.identity) > 1f)
            failures += Fail("Trolley visual is not upright: " + path, prefab);
        if (model == null || model.localScale.x < 2f || model.localScale.y < 2f || model.localScale.z < 2f)
            failures += Fail("Trolley visual is still at the miniature FBX scale: " + path, prefab);
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
