using UnityEngine;

public class TutorialCashierRoleLimiter : MonoBehaviour
{
    [Header("Scene UI To Hide On Cashier Day")]
    [SerializeField] private GameObject[] disableOnCashierDay;

    [Header("Scene UI To Show On Cashier Day")]
    [SerializeField] private GameObject[] enableOnCashierDay;

    [Header("Role Force")]
    [SerializeField] private bool forceCashierSelection = true;
    [SerializeField] private float reselectionInterval = 0.4f;

    private RoleManager roleManager;
    private bool lastCashierDayState;
    private float reselectionTimer;

    private void Awake()
    {
        roleManager = FindFirstObjectByType<RoleManager>();
    }

    private void Update()
    {
        TutorialManager tm = TutorialManager.Instance;
        bool cashierDayActive =
            tm != null &&
            tm.TutorialStarted &&
            tm.CurrentDay == TutorialManager.TutorialDay.Day3Cashier;

        if (cashierDayActive != lastCashierDayState)
        {
            ApplyCashierDayState(cashierDayActive);
            lastCashierDayState = cashierDayActive;
        }

        if (!cashierDayActive || !forceCashierSelection)
            return;

        reselectionTimer += Time.deltaTime;
        if (reselectionTimer < reselectionInterval)
            return;

        reselectionTimer = 0f;
        ForceCashierRole();
    }

    private void OnDisable()
    {
        ApplyCashierDayState(false);
        lastCashierDayState = false;
    }

    private void ApplyCashierDayState(bool cashierDayActive)
    {
        if (disableOnCashierDay != null)
        {
            for (int i = 0; i < disableOnCashierDay.Length; i++)
            {
                if (disableOnCashierDay[i] != null)
                    disableOnCashierDay[i].SetActive(!cashierDayActive);
            }
        }

        if (enableOnCashierDay != null)
        {
            for (int i = 0; i < enableOnCashierDay.Length; i++)
            {
                if (enableOnCashierDay[i] != null)
                    enableOnCashierDay[i].SetActive(cashierDayActive);
            }
        }

        if (cashierDayActive)
            ForceCashierRole();
    }

    private void ForceCashierRole()
    {
        if (roleManager == null)
            roleManager = FindFirstObjectByType<RoleManager>();

        if (roleManager == null)
            return;

        roleManager.SendMessage("SelectRoleByName", "Cashier", SendMessageOptions.DontRequireReceiver);
        roleManager.SendMessage("SwitchRoleByName", "Cashier", SendMessageOptions.DontRequireReceiver);
        roleManager.SendMessage("SetCurrentRoleByName", "Cashier", SendMessageOptions.DontRequireReceiver);
    }
}