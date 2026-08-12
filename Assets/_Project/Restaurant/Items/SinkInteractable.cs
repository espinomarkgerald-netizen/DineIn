using UnityEngine;

public class SinkInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform standPoint;

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => true;

    public bool CanInteract()
    {
        bool waiterHasTray = WaiterHands.ActivePlayerHands != null && WaiterHands.ActivePlayerHands.HasTray;
        bool busserHasTray = BusserHands.ActivePlayerHands != null && BusserHands.ActivePlayerHands.HasTray;

        return waiterHasTray || busserHasTray;
    }

    public void Interact(PlayerMovement player)
    {
        WaiterHands waiterHands = WaiterHands.For(player);
        if (waiterHands != null && waiterHands.HasTray)
        {
            waiterHands.DisposeTray(true);
            return;
        }

        BusserHands busserHands = BusserHands.For(player);
        if (busserHands != null && busserHands.HasTray)
        {
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
