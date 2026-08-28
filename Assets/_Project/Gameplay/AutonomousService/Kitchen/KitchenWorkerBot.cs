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
    private bool subscribed;

    public EmployeeRole EmployeeRole => employeeRole;

    private void Awake()
    {
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
        if (activeOrders.Count == 0 || staffBot == null || staffBot.IsBusy)
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

        while (activeOrders.Count > 0)
        {
            Transform target = FindNextWorkPoint();

            if (target == null)
            {
                Debug.LogError($"[KitchenWorkerBot] {name} has no valid work points.", this);
                yield break;
            }

            yield return staffBot.MoveTo(target);

            if (activeOrders.Count > 0)
                yield return staffBot.WorkFor(waitAtPoint);
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
