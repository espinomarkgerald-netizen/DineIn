using UnityEngine;

public class ClickToMoveTarget : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform standPoint;

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool CanInteract() => true;
    public bool AutoReturnHome => true;   // auto-return after arriving
    public void Interact(PlayerMovement mover) { }
}
