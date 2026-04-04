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
        // If the register is open, do not apply the disabled state — doing so would
        // call SetActive(false) on managed UI objects and kill the POS panel externally.
        bool registerIsOpen = CashierRegisterUI.Instance != null && CashierRegisterUI.Instance.IsOpen;
        if (registerIsOpen)
        {
            Debug.LogWarning("[RoleLimiter] OnDisable skipped ApplyCashierDayState(false) — register is open.", this);
            return;
        }

        ApplyCashierDayState(false);
        lastCashierDayState = false;
    }

    private void ApplyCashierDayState(bool cashierDayActive)
    {
        bool registerIsOpen = CashierRegisterUI.Instance != null && CashierRegisterUI.Instance.IsOpen;

        if (disableOnCashierDay != null)
        {
            for (int i = 0; i < disableOnCashierDay.Length; i++)
            {
                if (disableOnCashierDay[i] == null)
                    continue;

                bool next = !cashierDayActive;
                Debug.Log($"[RoleLimiter] disableOnCashierDay[{i}]={disableOnCashierDay[i].name} → SetActive({next})", disableOnCashierDay[i]);
                disableOnCashierDay[i].SetActive(next);
            }
        }

        if (enableOnCashierDay != null)
        {
            for (int i = 0; i < enableOnCashierDay.Length; i++)
            {
                if (enableOnCashierDay[i] == null)
                    continue;

                // Never force-disable a UI object while the POS register is open —
                // that would silently kill the panel without going through Hide().
                if (!cashierDayActive && registerIsOpen)
                {
                    Debug.LogWarning($"[RoleLimiter] SKIPPED SetActive(false) on enableOnCashierDay[{i}]={enableOnCashierDay[i].name} because CashierRegisterUI is open.", enableOnCashierDay[i]);
                    continue;
                }

                Debug.Log($"[RoleLimiter] enableOnCashierDay[{i}]={enableOnCashierDay[i].name} → SetActive({cashierDayActive})", enableOnCashierDay[i]);
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