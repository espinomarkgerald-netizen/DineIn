using UnityEngine;

public interface IInteractable
{
    Transform StandPoint { get; }
    bool CanInteract();
    bool AutoReturnHome { get; }     // true = go home right after arriving
    void Interact(PlayerMovement mover);
}
