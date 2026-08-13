using UnityEngine;

[DisallowMultipleComponent]
public sealed class ManagementComputerStation : MonoBehaviour, IInteractable
{
    [SerializeField] private ManagementComputerController controller;
    [SerializeField] private Transform standPoint;
    [SerializeField] private float interactRadius = 2.5f;

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => false;

    public void Configure(ManagementComputerController configuredController, Transform configuredStandPoint)
    {
        controller = configuredController;
        standPoint = configuredStandPoint;
    }

    public bool CanInteract()
    {
        return enabled && controller != null && !controller.IsOpen && ManagerPlayer.Active != null;
    }

    public void Interact(PlayerMovement mover)
    {
        if (controller == null)
            return;

        ManagerPlayer manager = mover != null ? mover.GetComponent<ManagerPlayer>() : ManagerPlayer.Active;
        if (manager != null)
            controller.OpenComputer(manager, this);
    }

    public float GetInteractRadius() => Mathf.Max(0.5f, interactRadius);
}
