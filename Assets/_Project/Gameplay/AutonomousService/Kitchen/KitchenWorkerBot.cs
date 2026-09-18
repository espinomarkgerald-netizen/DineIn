using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gives a kitchen worker visual activity while real KitchenManager orders cook.
/// KitchenManager remains the authority for timing and tray creation; this class
/// only selects work points and delegates movement to AutonomousStaffBot.
/// </summary>
public class KitchenWorkerBot : MonoBehaviour
{
    [Header("Employee Assignment")]
    [Tooltip("Only the employee made Active for this role appears and works during the shift.")]
    [SerializeField] private EmployeeRole employeeRole = EmployeeRole.Chef;

    [Header("Stations")]
    [SerializeField] private Transform homePoint;
    [SerializeField] private Transform[] workPoints;
    [SerializeField] private float waitAtPoint = 1.5f;

    [Header("Navigation")]
    [SerializeField, Range(0, 99)] private int avoidancePriority = 60;

    private readonly HashSet<int> activeOrders = new HashSet<int>();
    private AutonomousStaffBot staffBot;
    private KitchenManager kitchenManager;
    private int currentIndex;
    private int hygieneStationIndex;
    private bool subscribed;

    public EmployeeRole EmployeeRole => employeeRole;

    private void Awake()
    {
        MultiplayerWorldRegistry.Track(this);
        staffBot = GetComponent<AutonomousStaffBot>();
        if (staffBot == null)
            staffBot = gameObject.AddComponent<AutonomousStaffBot>();

        staffBot.ConfigureHome(homePoint, avoidancePriority);
    }

    private void OnEnable()
    {
        ConfigureEmployeePerformance();
        BindKitchenManager();
    }

    private void ConfigureEmployeePerformance()
    {
        if (staffBot == null || EmployeeManager.Instance == null)
            return;
        staffBot.ConfigurePerformance(EmployeeManager.Instance.GetAssignedEmployee(employeeRole));
    }

    private void OnDisable()
    {
        UnbindKitchenManager();
        activeOrders.Clear();
    }

    private void Update()
    {
        if (MultiplayerRestaurantBridge.IsObserver) return;
        if (activeOrders.Count == 0 && HygieneManager.Instance?.State.Cleaning != true || staffBot == null || staffBot.IsBusy)
            return;

        staffBot.StartTask(WorkWhileOrdersAreActive());
    }

    private void BindKitchenManager()
    {
        if (subscribed)
            return;

        kitchenManager = FindFirstObjectByType<KitchenManager>();
        if (kitchenManager == null)
        {
            Debug.LogError($"[KitchenWorkerBot] {name} could not find KitchenManager.", this);
            return;
        }

        kitchenManager.OrderStarted += HandleOrderStarted;
        kitchenManager.OrderFinished += HandleOrderFinished;
        subscribed = true;
    }

    private void UnbindKitchenManager()
    {
        if (subscribed && kitchenManager != null)
        {
            kitchenManager.OrderStarted -= HandleOrderStarted;
            kitchenManager.OrderFinished -= HandleOrderFinished;
        }

        subscribed = false;
        kitchenManager = null;
    }

    private void HandleOrderStarted(CustomerGroup group, int orderNumber)
    {
        activeOrders.Add(orderNumber);
    }

    private void HandleOrderFinished(CustomerGroup group, int orderNumber, bool succeeded)
    {
        activeOrders.Remove(orderNumber);
    }

    private IEnumerator WorkWhileOrdersAreActive()
    {
        if (workPoints == null || workPoints.Length == 0 || staffBot == null)
            yield break;

        currentIndex = Mathf.Clamp(currentIndex, 0, workPoints.Length - 1);

        while (activeOrders.Count > 0 || HygieneManager.Instance?.State.Cleaning == true)
        {
            Transform target = FindNextWorkPoint();

            if (target == null)
            {
                Debug.LogError($"[KitchenWorkerBot] {name} has no valid work points.", this);
                yield break;
            }

            Vector3 approach = target.position;
            Transform equipment = null;
            bool registeredStation = HygieneManager.Instance != null && HygieneManager.Instance.TryGetWorkStation(
                employeeRole, ref hygieneStationIndex, transform.position, out equipment, out approach);
            if (registeredStation) yield return staffBot.MoveWithin(approach, .25f, .5f, 12f);
            else yield return staffBot.MoveTo(target);

            if (staffBot.LastMoveSucceeded && (activeOrders.Count > 0 || HygieneManager.Instance?.State.Cleaning == true))
            {
                bool wasCleaning = HygieneManager.Instance?.State.Cleaning == true;
                yield return staffBot.WorkFor(waitAtPoint);
                if (!wasCleaning && activeOrders.Count > 0) HygieneManager.Instance?.RecordKitchenUse(registeredStation ? equipment : target);
            }
        }
    }

    private Transform FindNextWorkPoint()
    {
        for (int inspected = 0; inspected < workPoints.Length; inspected++)
        {
            Transform target = workPoints[currentIndex];
            currentIndex = (currentIndex + 1) % workPoints.Length;

            if (target != null)
                return target;
        }

        return null;
    }
}
