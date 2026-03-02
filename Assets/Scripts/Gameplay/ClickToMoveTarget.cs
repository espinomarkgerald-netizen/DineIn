using UnityEngine;

public class ClickToMoveTarget : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform standPoint;

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => true;   // auto-return after arriving

    public bool CanInteract()
    {
        // ✅ If holding a tray, don't let this "empty" interactable steal the click.
        if (WaiterHands.Instance != null && WaiterHands.Instance.HasTray)
            return false;

        return true;
    }

    public void Interact(PlayerMovement mover)
    {
        // intentionally empty (just a move target)
    }
}