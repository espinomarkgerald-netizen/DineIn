using System.Collections.Generic;
using UnityEngine;

public class Booth : MonoBehaviour
{
    [Header("Where the group walks to before seating")]
    public Transform approachPoint;

    [Header("Seat anchors (size up to 4)")]
    public List<Transform> seats = new List<Transform>(4);

    [Header("Facing")]
    [Tooltip("Empty object placed at the CENTER of the table. Customers will face this.")]
    public Transform tableLookTarget;

    [Tooltip("If customers face wrong direction, set to 180 / 90 / -90.")]
    public float seatYawOffset = 0f;

    // =========================
    // NEW: Table Props (Feature 1)
    // =========================
    [Header("Table Props - Menu Book")]
    [Tooltip("Prefab to spawn on the table once the group is seated.")]
    public GameObject menuBookPrefab;

    [Tooltip("Where the menu book should spawn (create an empty child on the tabletop).")]
    public Transform menuSpawnPoint;

    [Tooltip("If true, menu spawns as a child of menuSpawnPoint (recommended).")]
    public bool parentMenuToSpawnPoint = true;

    [Header("Table Props - Future Hooks (optional)")]
    [Tooltip("Where to spawn a table/order number UI later.")]
    public Transform tableNumberAnchor;

    // Runtime refs (so we can clean up)
    [SerializeField] private GameObject menuInstance;

    // ---- Availability / Seating ----

    public bool IsAvailableFor(int groupSize)
    {
        if (approachPoint == null) return false;
        if (seats == null || seats.Count < groupSize) return false;

        // Simple rule for now: booth must be fully empty
        for (int i = 0; i < seats.Count; i++)
        {
            if (seats[i] == null) continue;
            if (SeatAnchor.IsSeatOccupied(seats[i])) return false;
        }

        return true;
    }

    public Transform GetSeat(int index)
    {
        if (seats == null) return null;
        if (index < 0 || index >= seats.Count) return null;
        return seats[index];
    }

    public Quaternion GetSeatedRotation(Vector3 seatPos)
    {
        Vector3 dir;

        if (tableLookTarget != null)
            dir = tableLookTarget.position - seatPos;
        else
            dir = transform.forward;

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;

        return Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0f, seatYawOffset, 0f);
    }

    // =========================
    // NEW: Menu Book API
    // Call this when the group is fully seated (CustomerGroup.OnGroupSeated)
    // =========================

    /// <summary>
    /// Spawns the menu book on this booth (safe to call multiple times).
    /// </summary>
    public void SpawnMenuBook()
    {
        if (menuInstance != null) return; // already spawned

        if (menuBookPrefab == null)
        {
            Debug.LogWarning($"{name}: MenuBookPrefab is missing.");
            return;
        }

        if (menuSpawnPoint == null)
        {
            Debug.LogWarning($"{name}: MenuSpawnPoint is missing (create an empty child on the tabletop).");
            return;
        }

        if (parentMenuToSpawnPoint)
        {
            menuInstance = Instantiate(menuBookPrefab, menuSpawnPoint.position, menuSpawnPoint.rotation, menuSpawnPoint);
        }
        else
        {
            menuInstance = Instantiate(menuBookPrefab, menuSpawnPoint.position, menuSpawnPoint.rotation);
        }
    }

    /// <summary>
    /// Removes the menu book (call when customers leave / booth is cleared).
    /// </summary>
    public void ClearMenuBook()
    {
        if (menuInstance != null)
        {
            Destroy(menuInstance);
            menuInstance = null;
        }
    }

    /// <summary>
    /// Clears all booth props (menu now, more later like number UI).
    /// </summary>
    public void ClearBoothProps()
    {
        ClearMenuBook();
        // Later: ClearTableNumberUI(); ClearBillPaper(); etc.
    }
}
