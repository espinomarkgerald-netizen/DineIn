using System.Collections.Generic;
using UnityEngine;

public class OfficeStartButtons : MonoBehaviour
{
    [SerializeField]
    private List<InventoryEntry> kitchenRequirements;

    private bool HasEnoughStock()
    {
        foreach (var req in kitchenRequirements)
        {
            int current = InventoryManager.Instance.GetStock(req.itemType);

            if (current < req.stock)
            {
                Debug.Log($"Not enough {req.itemType}");
                return false;
            }
        }

        return true;
    }

    private bool HasEmployeesAssigned()
    {
        foreach (var emp in EmployeeManager.Instance.allEmployees)
        {
            if (emp.assignedSlot != null)
                return true;
        }

        Debug.Log("No employees assigned.");
        return false;
    }

    private bool CanStart()
    {
        if (InventoryManager.Instance == null || EmployeeManager.Instance == null)
        {
            Debug.LogError("Missing managers.");
            return false;
        }

        if (!HasEnoughStock())
            return false;

        if (!HasEmployeesAssigned())
            return false;

        return true;
    }

    public void StartLobbyShift()
    {
        if (!CanStart())
            return;

        GameFlowManager.Instance.LoadLobbyScene();
    }

    public void StartKitchen()
    {
        if (!CanStart())
            return;

        GameFlowManager.Instance.StartKitchenShift();
    }
}