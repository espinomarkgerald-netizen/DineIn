using UnityEngine;

/// <summary>
/// Added at runtime to every spawned takeout CustomerGroup.
/// Participates in PlayerMovement.TryClickInteractable so the waiter can click
/// the customer to deliver the held TakeoutBag. Implements IInteractable so it
/// is discovered by the custom Physics.RaycastAll system in PlayerMovement.
///
/// Key detail: the CustomerGroup root transform sits at the SPAWN position and
/// never moves. The individual CustomerAgent members walk via their own agents.
/// StandPoint therefore uses a proxy child Transform whose position is updated
/// every frame to the live members centre, so PlayerMovement.MoveToInteractable
/// always sends the waiter to where the customers actually are standing.
///
/// Dine-in groups never receive this component.
/// </summary>
[RequireComponent(typeof(CustomerGroup))]
public class TakeoutCustomerInteractable : MonoBehaviour, IInteractable
{
    private CustomerGroup group;

    // A child GameObject whose position tracks the live members centre each frame.
    // PlayerMovement captures standPoint.position at click time, so this must be
    // current at the moment the player clicks — not at the time of component creation.
    private Transform standPointProxy;

    public Transform StandPoint => standPointProxy;
    public bool AutoReturnHome => false;

    private void Awake()
    {
        group = GetComponent<CustomerGroup>();

        // Create a lightweight child to act as the live stand-point.
        var proxyGO = new GameObject("TakeoutStandProxy");
        proxyGO.transform.SetParent(transform, false);
        standPointProxy = proxyGO.transform;
    }

    private void Update()
    {
        if (group == null || standPointProxy == null)
            return;

        // Keep the proxy at the live world-centre of all members so that
        // PlayerMovement always walks to the customer's current position.
        standPointProxy.position = group.GetCurrentWorldCenter();
    }

    /// <summary>
    /// Returns true only when the waiter is holding a TakeoutBag that belongs
    /// to this specific customer group.
    /// </summary>
    public bool CanInteract()
    {
        if (group == null || !group.IsTakeout)
            return false;

        if (!TakeoutBagInteractable.PlayerHasHeldBag)
            return false;

        return TakeoutBagInteractable.HeldBag.TargetGroup == group;
    }

    /// <summary>
    /// Delivers the held bag to this customer group when the waiter arrives.
    /// Skips all dine-in logic (bill, tray, booth, table).
    /// </summary>
    public void Interact(PlayerMovement mover)
    {
        if (!CanInteract())
            return;

        TakeoutBagInteractable.HeldBag.TryDeliverTo(group);
    }

    public float GetInteractRadius()
    {
        // 1.4 m gives a natural arm's-length handoff distance.
        // PlayerMovement multiplies this by interactStopMultiplier (0.85), so
        // the agent stops at ~1.19 m from the members centre — close enough to
        // look like a deliberate handoff without clipping into the customers.
        return 1.4f;
    }

    private void OnDestroy()
    {
        if (standPointProxy != null)
            Destroy(standPointProxy.gameObject);
    }
}
