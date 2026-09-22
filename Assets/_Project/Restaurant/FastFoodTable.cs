using UnityEngine;

/// <summary>Authored furniture and service anchors owned by one Fast Food table prefab.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Booth))]
public sealed class FastFoodTable : MonoBehaviour, IInteractable
{
    [Tooltip("The furniture renderer inside this prefab. Seats and service points are its sibling objects.")]
    [SerializeField] private Renderer furniture;
    [Header("Progression")]
    [SerializeField] private Equipment seatingUpgrade;
    [SerializeField, Min(0)] private int starterSeats;
    public int AvailableSeats
    {
        get
        {
            int capacity = GetComponent<Booth>().seats.Count;
            if (FastFoodProgressionSettings.Current == null || seatingUpgrade == null ||
                EquipmentManager.Instance?.Purchased(seatingUpgrade.itemID) == true) return capacity;
            return Mathf.Min(capacity, starterSeats);
        }
    }
    public Renderer Furniture => furniture;
    public Transform StandPoint => GetComponent<Booth>().approachPoint;
    public bool AutoReturnHome => false;
    public float GetInteractRadius() => 1.5f;
    public bool CanInteract()
    {
        if (GetComponent<BoothDeliverInteractable>()?.CanInteract() == true) return true;
        var mover = RoleManager.Instance?.GetActivePlayerMovement();
        return gameObject.scene.name == "Lobby2" && StandPoint != null && HygieneManager.HandsEmpty(mover);
    }
    public void Interact(PlayerMovement mover)
    {
        if (AvailableSeats == 0)
        {
            WarningSlideUI.Instance?.Show("Unlock " + seatingUpgrade.displayName +
                " from Day " + seatingUpgrade.dayToUnlock + " in Computer > Equipment.");
            return;
        }
        var delivery = GetComponent<BoothDeliverInteractable>();
        if (delivery != null && delivery.CanInteract()) { delivery.Interact(mover); return; }
        if (!CanInteract()) return;
        var booth = GetComponent<Booth>();
        var tray = booth.NetworkTrayPoint != null
            ? booth.NetworkTrayPoint.GetComponentInChildren<FoodTrayInteractable>() : null;
        if (tray != null && tray.CanInteract()) { mover.UI_MoveTo(tray); return; }
        if (booth.CanRequestHumanCleanup) booth.OpenCleaningPrompt();
        // Clean/occupied tables remain selectable without starting a service task.
    }
    public bool Contains(Renderer renderer) => renderer != null && renderer.transform.IsChildOf(transform);
    public bool ValidateAuthoring(out string problem)
    {
        var booth = GetComponent<Booth>();
        problem = null;
        if (furniture == null || !furniture.transform.IsChildOf(transform))
            problem = "Assign the furniture renderer inside this prefab.";
        else if (booth == null || booth.approachPoint == null || booth.tableLookTarget == null ||
            booth.tableNumberAnchor == null || booth.NetworkTrayPoint == null)
            problem = "Assign ApproachPoint, FacingPoint, TableNumberAnchor and TableFoodSpawn.";
        else if (!booth.approachPoint.IsChildOf(transform) || !booth.tableLookTarget.IsChildOf(transform) ||
            !booth.tableNumberAnchor.IsChildOf(transform) || !booth.NetworkTrayPoint.IsChildOf(transform))
            problem = "Service anchors must belong to this prefab.";
        else if (booth.seats == null || booth.seats.Count == 0)
            problem = "Assign at least one usable seat; the Seats list defines capacity.";
        else
        {
            var seats = new System.Collections.Generic.HashSet<Transform>();
            var positions = new System.Collections.Generic.HashSet<Vector3>();
            foreach (var seat in booth.seats)
                if (seat == null || !seat.IsChildOf(transform) || !seats.Add(seat) || !positions.Add(seat.position))
                { problem = "Seats must be unique child transforms at distinct positions."; break; }
        }
        if (problem == null && (GetComponent<Collider>() == null || GetComponent<Outline>() == null ||
            !Application.isPlaying && GetComponent<Outline>().enabled))
            problem = "Author a click collider and a disabled Outline on the prefab root.";
        if (problem == null)
        {
            var mesh = furniture.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null || !mesh.isReadable)
                problem = "Furniture needs a readable mesh. Enable Read/Write in its model import settings so every material section can outline.";
        }
        if (problem == null && (GetComponentsInChildren<BoothDeliverInteractable>(true).Length != 1 ||
            GetComponent<BoothDeliverInteractable>() == null ||
            GetComponent<BoothDeliverInteractable>().DeliveryPoint != booth.NetworkTrayPoint))
            problem = "Keep exactly one root delivery interaction bound to TableFoodSpawn.";
        return problem == null;
    }

    [ContextMenu("Validate Table Authoring")]
    private void ValidateTable()
    {
        if (ValidateAuthoring(out var problem)) Debug.Log(name + ": table authoring is valid. Navigation still needs validation.", this);
        else Debug.LogError(name + ": " + problem, this);
    }
    private void OnDrawGizmosSelected()
    {
        Booth booth = GetComponent<Booth>();
        if (booth == null) return;
        Gizmos.color = Color.cyan;
        if (booth.approachPoint != null) Gizmos.DrawWireSphere(booth.approachPoint.position, .4f);
        Gizmos.color = Color.green;
        if (booth.seats != null) foreach (var seat in booth.seats)
            if (seat != null) { Gizmos.DrawWireSphere(seat.position, .25f); Gizmos.DrawRay(seat.position, seat.forward * .5f); }
        if (booth.NetworkTrayPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(booth.NetworkTrayPoint.position, new Vector3(.75f, .1f, .5f));
        }
    }
}
