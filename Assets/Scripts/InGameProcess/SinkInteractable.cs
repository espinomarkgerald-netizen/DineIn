using UnityEngine;

public class SinkInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform standPoint;

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => true;

    public bool CanInteract()
    {
        return WaiterHands.Instance != null && WaiterHands.Instance.HasTray;
    }

    public void Interact(PlayerMovement player)
    {
        var hands = WaiterHands.Instance;
        if (hands == null) return;
        if (!hands.HasTray) return;

        hands.DisposeTray(true);  
    }
}