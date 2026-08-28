using UnityEngine;

/// <summary>Chooses a payment method once, when the customer's payment is created.</summary>
public static class CardPaymentService
{
    private static bool forceNextCardPayment;

    public static bool ShouldUseCardPayment()
    {
        if (forceNextCardPayment)
        {
            forceNextCardPayment = false;
            return true;
        }

        return EquipmentUpgradeService.IsPurchased(EquipmentUpgradeEffect.CardPayment) &&
               Random.value < EquipmentUpgradeService.CardPaymentChance;
    }

    public static void ForceNextCardPayment()
    {
        forceNextCardPayment = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        forceNextCardPayment = false;
    }
}
