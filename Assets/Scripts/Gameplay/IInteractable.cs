using UnityEngine;

public interface IInteractable
{
    Transform StandPoint { get; }
    bool CanInteract();
    bool AutoReturnHome { get; }
    void Interact(PlayerMovement mover);
}