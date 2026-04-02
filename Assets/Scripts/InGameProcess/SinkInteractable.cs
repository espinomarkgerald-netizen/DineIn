using UnityEngine;

public class SinkInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform standPoint;

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => true;

    public bool CanInteract()
    {
        bool waiterHasTray = WaiterHands.Instance != null && WaiterHands.Instance.HasTray;
        bool busserHasTray = BusserHands.Instance != null && BusserHands.Instance.HasTray;

        return waiterHasTray || busserHasTray;
    }

    public void Interact(PlayerMovement player)
    {
        if (WaiterHands.Instance != null && WaiterHands.Instance.HasTray)
        {
            WaiterHands.Instance.DisposeTray(true);
            return;
        }

        if (BusserHands.Instance != null && BusserHands.Instance.HasTray)
        {
            FoodTray cleanedTray = BusserHands.Instance.holdingTray;
            BusserHands.Instance.DisposeTray(true);
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