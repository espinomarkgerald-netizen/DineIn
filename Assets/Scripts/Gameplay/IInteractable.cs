using UnityEngine;

public interface IInteractable
{
    Transform StandPoint { get; }
    bool AutoReturnHome { get; }

    bool CanInteract();
    void Interact(PlayerMovement mover);

    float GetInteractRadius();
}