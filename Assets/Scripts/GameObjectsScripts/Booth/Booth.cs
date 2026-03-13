using System.Collections.Generic;
using UnityEngine;

public class Booth : MonoBehaviour
{
    [Header("Approach / Seating")]
    public Transform approachPoint;
    public List<Transform> seats = new List<Transform>(4);

    [Header("Facing")]
    public Transform tableLookTarget;
    public float seatYawOffset = 0f;

    [Header("Table Props - Menu Book")]
    public GameObject menuBookPrefab;
    public Transform menuSpawnPoint;
    public bool parentMenuToSpawnPoint = true;

    [Header("Table Props - Other")]
    public Transform tableNumberAnchor;

    [Header("Runtime")]
    [SerializeField] private GameObject menuInstance;
    [SerializeField] private CustomerGroup currentGroup;

    public CustomerGroup CurrentGroup => currentGroup;

    public void SetCurrentGroup(CustomerGroup g) => currentGroup = g;
    public void ClearCurrentGroup() => currentGroup = null;

    public bool IsAvailableFor(int groupSize)
    {
        if (HasTrayOnTable()) return false;

        if (approachPoint == null) return false;
        if (seats == null || seats.Count < groupSize) return false;

        if (currentGroup != null) return false;

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
        Vector3 dir = (tableLookTarget != null) ? (tableLookTarget.position - seatPos) : transform.forward;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;

        return Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0f, seatYawOffset, 0f);
    }

    public void SpawnMenuBook()
    {
        if (menuSpawnPoint == null || menuBookPrefab == null) return;

        if (menuInstance == null)
            menuInstance = FindExistingMenu();

        if (menuInstance != null) return;

        menuInstance = parentMenuToSpawnPoint
            ? Instantiate(menuBookPrefab, menuSpawnPoint.position, menuSpawnPoint.rotation, menuSpawnPoint)
            : Instantiate(menuBookPrefab, menuSpawnPoint.position, menuSpawnPoint.rotation);
    }

    public void ClearMenuBook()
    {
        if (menuInstance == null)
            menuInstance = FindExistingMenu();

        if (menuInstance != null)
        {
            Destroy(menuInstance);
            menuInstance = null;
        }
    }

    private GameObject FindExistingMenu()
    {
        if (menuSpawnPoint == null) return null;

        if (menuSpawnPoint.childCount > 0)
        {
            for (int i = 0; i < menuSpawnPoint.childCount; i++)
            {
                var child = menuSpawnPoint.GetChild(i);
                if (child == null) continue;

                if (menuBookPrefab != null && child.name.StartsWith(menuBookPrefab.name))
                    return child.gameObject;
            }

            return menuSpawnPoint.GetChild(0).gameObject;
        }

        return null;
    }

    public void ClearBoothProps()
    {
        ClearMenuBook();
    }

    public void ArmTrayCleaningForCurrentGroup()
    {
        ArmTrayCleaningForGroup(currentGroup);
    }

    public void ArmTrayCleaningForGroup(CustomerGroup group)
    {
        var drop = FindTableFoodSpawn();
        if (drop == null) return;

        var tray = drop.GetComponentInChildren<FoodTray>(true);
        if (tray == null) return;

        var holdClean = tray.GetComponent<TrayHoldToClean>();
        if (holdClean != null)
        {
            holdClean.Arm(this, group);
            return;
        }

        var legacy = tray.GetComponent<TrayCleanable>();
        if (legacy != null)
        {
            legacy.ArmForCleaning(this);
            return;
        }
    }

    public void OnTableCleaned()
    {
    }

    private Transform FindTableFoodSpawn()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "TableFoodSpawn")
                return t;
        }
        return null;
    }

    private bool HasTrayOnTable()
    {
        var drop = FindTableFoodSpawn();
        if (drop == null) return false;

        return drop.GetComponentInChildren<FoodTray>(true) != null;
    }
}