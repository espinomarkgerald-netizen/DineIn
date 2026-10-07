using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ManagementComputerController
{
    [Header("Reception Reservations")]
    [SerializeField] private Sprite reservationSelectedTabSprite;
    [SerializeField] private Sprite reservationInactiveTabSprite;
    private FineDiningReservations reservations;
    private readonly Dictionary<GameObject, bool> managementVisibility = new Dictionary<GameObject, bool>();
    private bool ReservationMode => activeStation != null && activeStation.Purpose == RestaurantComputerPurpose.Reservations;

    private void ConfigureReservationStation()
    {
        if (!ReservationMode) return;
        reservations = FineDiningReservations.For(this);
        if (reservations != null) reservations.Changed += RefreshReservations;
        if (appButtons != null)
            foreach (var button in appButtons) if (button != null) HideManagementControl(button.gameObject);
        if (startShiftButton != null) HideManagementControl(startShiftButton.gameObject);
    }
    private void HideManagementControl(GameObject target)
    {
        managementVisibility[target] = target.activeSelf;
        target.SetActive(false);
    }
    private void ReleaseReservationStation()
    {
        if (reservations != null) reservations.Changed -= RefreshReservations;
        reservations = null;
        foreach (var item in managementVisibility) if (item.Key != null) item.Key.SetActive(item.Value);
        managementVisibility.Clear();
    }
    private void RefreshReservations()
    {
        if (!ReservationMode || !IsOpen) return;
        if (reservationPanel != null && reservationPanel.isActiveAndEnabled) reservationPanel.RefreshData();
        else sharedAppRefreshPending = true;
    }

    [SerializeField] private ManagementReservationPanel reservationPanelPrefab;
    private ManagementReservationPanel reservationPanel;

    private void PopulateReservations()
    {
        if (reservations == null || reservationPanelPrefab == null)
        { appWindow.SetMessage("Reservation setup is unavailable.", true); return; }
        reservations.EnsureDay();
        reservationPanel = Instantiate(reservationPanelPrefab, appWindow.Content);
        reservationPanel.name = "Reservations";
        reservationPanel.Bind(reservations, CloseApp);
        appWindow.SetMessage("");
        appWindow.SetFooter("", null, false);
    }
}
