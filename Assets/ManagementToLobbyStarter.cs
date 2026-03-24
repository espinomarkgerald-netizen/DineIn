using UnityEngine;

public class ManagementToLobbyStarter : MonoBehaviour
{
    public void StartLobbyFromManagement()
    {
        SaveKitchenAssignmentFromEmployeeManager();
        if (DailyFinanceBridge.Instance != null)
        {
            DailyFinanceBridge.Instance.ResetDay();

            // temporary demo values for now
            DailyFinanceBridge.Instance.SetDailyCosts(
                500, // employee cost
                200, // marketing cost
                300, // bills
                400  // ingredients
            );

            Debug.Log("[DailyFinance] Target set to ₱" + DailyFinanceBridge.Instance.TotalRequiredEarningsToday);
        }
        if (GameFlowManager.Instance == null)
        {
            Debug.LogError("[ManagementToLobbyStarter] GameFlowManager not found.");
            return;
        }

        GameFlowManager.Instance.StartDay();
    }

    private void SaveKitchenAssignmentFromEmployeeManager()
    {
        if (EmployeeManager.Instance == null)
        {
            Debug.LogError("[ManagementToLobbyStarter] EmployeeManager not found.");
            return;
        }

        if (KitchenAssignmentSaveBridge.Instance == null)
        {
            Debug.LogError("[ManagementToLobbyStarter] KitchenAssignmentSaveBridge not found.");
            return;
        }

        EmployeeData chef = null;
        EmployeeData barista = null;

        foreach (var employee in EmployeeManager.Instance.allEmployees)
        {
            if (employee == null)
                continue;

            if (employee.assignedSlot == null)
                continue;

            string roleName = employee.role.ToString();

            if (roleName == "Chef")
                chef = employee;
            else if (roleName == "Barista")
                barista = employee;
        }

        if (chef != null)
        {
            KitchenAssignmentSaveBridge.Instance.SetChef(chef.employeeName, chef.stars);
            Debug.Log($"[ManagementToLobbyStarter] Chef found: {chef.employeeName} ({chef.stars}★)");
        }
        else
        {
            Debug.LogWarning("[ManagementToLobbyStarter] No assigned Chef found.");
        }

        if (barista != null)
        {
            KitchenAssignmentSaveBridge.Instance.SetBarista(barista.employeeName, barista.stars);
            Debug.Log($"[ManagementToLobbyStarter] Barista found: {barista.employeeName} ({barista.stars}★)");
        }
        else
        {
            Debug.LogWarning("[ManagementToLobbyStarter] No assigned Barista found.");
        }

        KitchenAssignmentSaveBridge.Instance.SaveKitchenAssignment();
        Debug.Log($"[ManagementToLobbyStarter] Final meal spawn time = {KitchenAssignmentSaveBridge.Instance.GetMealSpawnTime()}");
    }

    
}