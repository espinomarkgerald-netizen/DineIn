using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ManagementComputerController
{
    private readonly Dictionary<string, ManagementComputerRowUI> stableRows = new();
    private bool refreshingView;
    private readonly Dictionary<CanvasGroup, bool> desktopGateStates = new();
    private void SetDesktopNavigationBlocked(bool blocked)
    {
        if (appButtons == null) return;
        foreach (var button in appButtons)
        {
            if (button == null) continue;
            var group = button.GetComponent<CanvasGroup>();
            if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
            // Button.interactable belongs to tutorial/gameplay gates. Never overwrite it.
            if (blocked)
            { if (!desktopGateStates.ContainsKey(group)) desktopGateStates[group] = group.interactable; group.interactable = false; }
            else if (desktopGateStates.TryGetValue(group, out bool previous))
            { group.interactable = previous; desktopGateStates.Remove(group); }
        }
    }
    private void NavigateToApp(int index)
    {
        if (ReservationMode) return;
        CloseApp();
        OpenApp(index);
    }
    private void RefreshActiveApp()
    {
        sharedAppRefreshPending = false;
        if (!IsOpen || selectedApp < 0 || appWindow == null || !appWindow.gameObject.activeSelf || refreshingView) return;
        var anchor = new ManagementComputerScrollAnchor(appWindow.transform);
        refreshingView = true;
        try
        {
            switch ((ManagementComputerApp)selectedApp)
            {
                case ManagementComputerApp.Staff: appWindow.GetComponentInChildren<ManagementComputerHRPanel>()?.RefreshData(); break;
                case ManagementComputerApp.Menu:
                case ManagementComputerApp.Restock: appWindow.GetComponentInChildren<ManagementComputerCatalogPanelUI>()?.RefreshData(); break;
                case ManagementComputerApp.Equipment: PopulateEquipment(); break;
                case ManagementComputerApp.Dashboard: PopulateDashboard(); break;
                case ManagementComputerApp.Finances: PopulateFinances(); break;
                case ManagementComputerApp.Objectives: PopulateObjectives(); break;
                case ManagementComputerApp.Reservations: reservationPanel?.RefreshData(); break;
            }
            appWindow.RefreshContentLayout();
            anchor.Restore();
            RefreshStatusBar();
        }
        finally { refreshingView = false; }
    }
}
