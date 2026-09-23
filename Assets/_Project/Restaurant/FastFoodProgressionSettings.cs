using System.Collections.Generic;
using UnityEngine;

/// <summary>Fast Food campaign tuning; uses the existing unlock, purchase and save systems.</summary>
[CreateAssetMenu(menuName = "Dine In/Fast Food Progression")]
public sealed class FastFoodProgressionSettings : ScriptableObject
{
    public const string ResourcePath = "FastFood/Progression";
    public const string SecondCashierID = "ff_second_cashier";
    public List<Equipment> equipment = new();
    [Header("Day curves (X is the actual day)")]
    public AnimationCurve groups = AnimationCurve.Linear(1, 20, 15, 44);
    public AnimationCurve groupsPerMinute = AnimationCurve.Linear(1, 3, 15, 6);
    public AnimationCurve spawnSeconds = AnimationCurve.Linear(1, 20, 15, 11);
    public AnimationCurve patienceSeconds = AnimationCurve.Linear(1, 90, 15, 60);
    public AnimationCurve concurrentGroups = AnimationCurve.Linear(1, 4, 15, 9);
    public AnimationCurve salesQuota = AnimationCurve.Linear(1, 1800, 15, 4800);
    [Min(1)] public int difficultyCeilingDay = 15;
    [Min(1)] public int pinkCustomerDay = 5;
    [Min(1)] public int blueCustomerDay = 10;
    [Range(0f, 1f)] public float takeoutChance = .55f;
    [Range(0f, 1f)] public float tableDeliveryChance = .3f;

    private static FastFoodProgressionSettings cached;
    public static FastFoodProgressionSettings Current =>
        CampaignSaveStore.IsFastFood && !CampaignSaveStore.ProtectedSession
            ? (cached != null ? cached : cached = Resources.Load<FastFoodProgressionSettings>(ResourcePath))
            : null;
    public float Evaluate(AnimationCurve curve, int day) =>
        curve.Evaluate(Mathf.Clamp(day, 1, Mathf.Max(1, difficultyCeilingDay)));
    public static bool HasSecondCashier => Current != null &&
        EquipmentManager.Instance != null && EquipmentManager.Instance.Purchased(SecondCashierID) &&
        (StaffHiringProgressionSettings.CurrentDay >= StaffHiringProgressionSettings.UnlockDay(EmployeeRole.Cashier) ||
         EmployeeManager.Instance != null && EmployeeManager.Instance.GetHiredCount(EmployeeRole.Cashier) > 1);
}
