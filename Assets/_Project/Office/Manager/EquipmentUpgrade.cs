using UnityEngine;

public enum EquipmentUpgradeEffect
{
    None,
    BusserTrolley,
    WaiterTrolley,
    CardPayment
}

/// <summary>
/// Editable upgrade data that reuses the existing equipment purchase and save
/// pipeline. The effect values live on the asset instead of being hidden in UI
/// code, so balancing remains an Inspector operation.
/// </summary>
[CreateAssetMenu(menuName = "Game/Equipment Upgrade")]
public sealed class EquipmentUpgrade : Equipment
{
    [Header("Upgrade Effect")]
    public EquipmentUpgradeEffect effect;
    [Min(1)] public int carryCapacity = 4;
    [Range(0f, 1f)] public float cardPaymentChance = 0.5f;
    [Min(0f)] public float playerPrioritySeconds = 5f;
    [Min(0f)] public float successCloseDelay = 0.5f;

    private void OnValidate()
    {
        catalogSection = EquipmentCatalogSection.Upgrades;
        carryCapacity = Mathf.Max(1, carryCapacity);
        cardPaymentChance = Mathf.Clamp01(cardPaymentChance);
        playerPrioritySeconds = Mathf.Max(0f, playerPrioritySeconds);
        successCloseDelay = Mathf.Max(0f, successCloseDelay);
    }
}
