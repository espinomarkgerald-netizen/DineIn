using UnityEngine;

/// <summary>Read-only access to purchased upgrade effects for gameplay systems.</summary>
public static class EquipmentUpgradeService
{
    public const string BusserTrolleyID = "upgrade_busser_trolley";
    public const string WaiterTrolleyID = "upgrade_waiter_trolley";
    public const string CardPaymentID = "upgrade_card_payment";

    public static bool IsPurchased(EquipmentUpgradeEffect effect)
    {
        EquipmentUpgrade upgrade = Find(effect);
        return upgrade != null && EquipmentManager.Instance != null &&
               EquipmentManager.Instance.Purchased(upgrade.itemID);
    }

    public static EquipmentUpgrade Find(EquipmentUpgradeEffect effect)
    {
        EquipmentManager manager = EquipmentManager.Instance;
        if (manager?.AllEquipment == null)
            return null;

        for (int i = 0; i < manager.AllEquipment.Count; i++)
        {
            if (manager.AllEquipment[i] is EquipmentUpgrade upgrade && upgrade.effect == effect)
                return upgrade;
        }

        return null;
    }

    public static int GetCarryCapacity(EquipmentUpgradeEffect effect, int fallback = 4)
    {
        EquipmentUpgrade upgrade = Find(effect);
        return upgrade != null ? Mathf.Max(1, upgrade.carryCapacity) : Mathf.Max(1, fallback);
    }

    public static float CardPaymentChance
    {
        get
        {
            EquipmentUpgrade upgrade = Find(EquipmentUpgradeEffect.CardPayment);
            return upgrade != null ? Mathf.Clamp01(upgrade.cardPaymentChance) : 0.5f;
        }
    }

    public static float CardPaymentCloseDelay
    {
        get
        {
            EquipmentUpgrade upgrade = Find(EquipmentUpgradeEffect.CardPayment);
            return upgrade != null ? Mathf.Max(0f, upgrade.successCloseDelay) : 0.5f;
        }
    }
}
