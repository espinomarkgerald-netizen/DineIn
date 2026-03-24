using UnityEngine;

public class KitchenDebugReader : MonoBehaviour
{
    private void Start()
    {
        var manager = EmployeeManager.Instance;

        if (manager == null)
        {
            Debug.Log("EmployeeManager not found");
            return;
        }

        foreach (var emp in manager.allEmployees)
        {
            if (emp.assignedSlot != null)
            {
                Debug.Log($"[KitchenDebug] Assigned: {emp.employeeName} | Role: {emp.role}");
            }
        }
    }
}