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
            BusserHands.Instance.DisposeTray(true);
            return;
        }
    }

    public float GetInteractRadius()
    {
        return 0.5f;
    }
}