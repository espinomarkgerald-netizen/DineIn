using System.Reflection;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class TutorialWaiterCashHandoff : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CashierBoothInteractable cashierBooth;

    [Header("Handoff")]
    [SerializeField] private float handoffRadius = 1.5f;
    [SerializeField] private bool usePlanarDistance = true;
    [SerializeField] private bool autoCompleteCustomerLeave = true;
    [SerializeField] private bool registerPaymentCompletedInGameDay = true;

    [Header("Fallback Rules")]
    [SerializeField] private bool fallbackAllowOnlyOnWaiterDay = true;
    [SerializeField] private bool fallbackBlockOnMasteryLikeDays = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private float lastHandoffTime = -999f;

    private void Awake()
    {
        if (cashierBooth == null)
            cashierBooth = GetComponent<CashierBoothInteractable>();
    }

    private void Update()
    {
        if (!ShouldRunTutorialHandoff())
            return;

        if (Time.time - lastHandoffTime < 0.15f)
            return;

        var hands = WaiterHands.Instance;
        if (hands == null || !hands.HasMoney)
            return;

        if (RoleManager.Instance == null || !RoleManager.Instance.IsActiveRoleType(StaffRole.Role.Waiter))
            return;

        var mover = RoleManager.Instance.GetActivePlayerMovement();
        if (mover == null)
            return;

        Transform targetPoint = cashierBooth != null ? cashierBooth.StandPoint : transform;

        Vector3 a = mover.transform.position;
        Vector3 b = targetPoint.position;

        if (usePlanarDistance)
        {
            a.y = 0f;
            b.y = 0f;
        }

        float dist = Vector3.Distance(a, b);
        if (dist > handoffRadius)
            return;

        CompleteWaiterTutorialHandoff(hands);
    }

    private bool ShouldRunTutorialHandoff()
    {
        if (!TutorialSceneRuntimeMarker.IsTutorialRuntimeActive)
            return false;

        var tm = TutorialManager.Instance;
        if (tm == null)
            return false;

        if (!tm.TutorialStarted)
            return false;

        if (IsMasteryGameplayActive(tm))
            return false;

        return ShouldUseTutorialWaiterCashHandoff(tm);
    }

    private void CompleteWaiterTutorialHandoff(WaiterHands hands)
    {
        if (hands == null || !hands.HasMoney)
            return;

        var tm = TutorialManager.Instance;
        if (tm != null && IsMasteryGameplayActive(tm))
            return;

        lastHandoffTime = Time.time;

        CustomerGroup paidGroup = hands.holdingMoneyFor;

        if (debugLogs)
            Debug.Log("[TutorialWaiterCashHandoff] Completing waiter tutorial cash handoff.");

        hands.ClearMoney();

        if (registerPaymentCompletedInGameDay)
            GameDayManager.Instance?.RegisterPaymentCompleted();

        if (autoCompleteCustomerLeave && paidGroup != null)
            paidGroup.PayAndLeave();

        if (tm != null)
            tm.OnMoneyGivenToCashier(paidGroup);

        if (CashierRegisterUI.Instance != null && CashierRegisterUI.Instance.IsOpen)
            CashierRegisterUI.Instance.CloseRegister();
    }

    private bool IsMasteryGameplayActive(TutorialManager tm)
    {
        if (tm == null)
            return false;

        if (TryCallBoolMethod(tm, "IsMasteryGameplayActive", out bool result))
            return result;

        if (!fallbackBlockOnMasteryLikeDays)
            return false;

        string dayName = ReadMemberAsString(tm, "currentDay");
        if (string.IsNullOrEmpty(dayName))
            dayName = ReadMemberAsString(tm, "CurrentDay");

        string phaseName = ReadMemberAsString(tm, "currentPhase");
        if (string.IsNullOrEmpty(phaseName))
            phaseName = ReadMemberAsString(tm, "CurrentPhase");

        return ContainsAny(dayName, "day5", "mastery", "alltogether", "alltogethergameplay")
            || ContainsAny(phaseName, "mastery", "alltogether", "alltogethergameplay");
    }

    private bool ShouldUseTutorialWaiterCashHandoff(TutorialManager tm)
    {
        if (tm == null)
            return false;

        if (TryCallBoolMethod(tm, "ShouldUseTutorialWaiterCashHandoff", out bool result))
            return result;

        if (!fallbackAllowOnlyOnWaiterDay)
            return !IsMasteryGameplayActive(tm);

        string dayName = ReadMemberAsString(tm, "currentDay");
        if (string.IsNullOrEmpty(dayName))
            dayName = ReadMemberAsString(tm, "CurrentDay");

        string phaseName = ReadMemberAsString(tm, "currentPhase");
        if (string.IsNullOrEmpty(phaseName))
            phaseName = ReadMemberAsString(tm, "CurrentPhase");

        bool looksLikeWaiter = ContainsAny(dayName, "day2", "waiter")
                            || ContainsAny(phaseName, "waiter");

        bool looksLikeOtherRole = ContainsAny(dayName, "day1", "host", "day3", "cashier", "day4", "busser")
                               || ContainsAny(phaseName, "host", "cashier", "busser");

        if (looksLikeOtherRole)
            return false;

        if (IsMasteryGameplayActive(tm))
            return false;

        return looksLikeWaiter;
    }

    private bool TryCallBoolMethod(object target, string methodName, out bool value)
    {
        value = false;

        if (target == null)
            return false;

        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var method = target.GetType().GetMethod(methodName, flags);

        if (method == null)
            return false;

        if (method.ReturnType != typeof(bool))
            return false;

        if (method.GetParameters().Length != 0)
            return false;

        try
        {
            object result = method.Invoke(target, null);
            if (result is bool boolResult)
            {
                value = boolResult;
                return true;
            }
        }
        catch (System.Exception ex)
        {
            if (debugLogs)
                Debug.LogWarning($"[TutorialWaiterCashHandoff] Failed calling {methodName}: {ex.Message}");
        }

        return false;
    }

    private string ReadMemberAsString(object target, string memberName)
    {
        if (target == null || string.IsNullOrEmpty(memberName))
            return string.Empty;

        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = target.GetType();

        var field = type.GetField(memberName, flags);
        if (field != null)
        {
            object value = field.GetValue(target);
            return value != null ? value.ToString() : string.Empty;
        }

        var property = type.GetProperty(memberName, flags);
        if (property != null)
        {
            object value = property.GetValue(target, null);
            return value != null ? value.ToString() : string.Empty;
        }

        return string.Empty;
    }

    private bool ContainsAny(string source, params string[] terms)
    {
        if (string.IsNullOrEmpty(source) || terms == null || terms.Length == 0)
            return false;

        string lowered = source.ToLowerInvariant();

        for (int i = 0; i < terms.Length; i++)
        {
            if (!string.IsNullOrEmpty(terms[i]) && lowered.Contains(terms[i].ToLowerInvariant()))
                return true;
        }

        return false;
    }
}