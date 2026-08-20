using UnityEngine;

[DisallowMultipleComponent]
public sealed class ManagementComputerStation : MonoBehaviour, IInteractable
{
    [SerializeField] private ManagementComputerController controller;
    [SerializeField] private Transform standPoint;
    [SerializeField] private float interactRadius = 2.5f;

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public bool AutoReturnHome => false;

    private void Awake()
    {
        if (controller == null)
            controller = FindFirstObjectByType<ManagementComputerController>();

        BoxCollider clickCollider = GetComponent<BoxCollider>();
        if (clickCollider == null)
            clickCollider = gameObject.AddComponent<BoxCollider>();

        clickCollider.enabled = true;
        clickCollider.isTrigger = true;
    }

    public void Configure(ManagementComputerController configuredController, Transform configuredStandPoint)
    {
        controller = configuredController;
        standPoint = configuredStandPoint;
    }

    public bool CanInteract()
    {
        if (controller == null)
            controller = FindFirstObjectByType<ManagementComputerController>();

        return isActiveAndEnabled && controller != null && !controller.IsOpen;
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
