using UnityEngine;

public class SinkInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform standPoint;
    private void OnEnable() => HygieneManager.Instance?.RegisterStation(this);

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => !MultiplayerServiceActions.IsActive;

    public bool CanInteract()
    {
        bool waiterHasTray = WaiterHands.ActivePlayerHands != null && WaiterHands.ActivePlayerHands.HasTray;
        bool busserHasTray = BusserHands.ActivePlayerHands != null && BusserHands.ActivePlayerHands.HasTray;
        if (MultiplayerServiceActions.IsActive) return MultiplayerSessionManager.Instance.CanAct && busserHasTray;

        return waiterHasTray || busserHasTray;
    }

    public void Interact(PlayerMovement player)
    {
        if (MultiplayerServiceActions.IsActive)
        {
            if (player != null && player.gameObject == MultiplayerSessionManager.Instance.LocalManager)
                MultiplayerServiceActions.WashDirtyTray();
            return;
        }
        WaiterHands waiterHands = WaiterHands.For(player);
        if (waiterHands != null && waiterHands.HasTray)
        {
            HygieneManager.Instance?.RecordKitchenUse(this);
            waiterHands.DisposeTray(true);
            return;
        }

        BusserHands busserHands = BusserHands.For(player);
        if (busserHands != null && busserHands.HasTray)
        {
            HygieneManager.Instance?.RecordKitchenUse(this);
            FoodTray cleanedTray = busserHands.holdingTray;
            busserHands.DisposeTray(true);
            NotifyTutorialTrayCleaned(cleanedTray);
            return;
        }
    }

    public float GetInteractRadius()
    {
        return 0.5f;
    }

    private void NotifyTutorialTrayCleaned(FoodTray tray)
    {
        if (TutorialManager.Instance == null || !TutorialManager.Instance.TutorialStarted)
            return;

        TutorialManager.Instance.RegisterDirtyTrayCleaned(tray);
    }
}
