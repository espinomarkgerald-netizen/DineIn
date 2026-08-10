using System.Collections.Generic;
using System.Reflection;
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

    [Header("Messy Customer")]
    [SerializeField] private GameObject puddle;
    [SerializeField] private GameObject cleanUIRoot;
    [SerializeField] private BoothMessCleanUI cleanUI;
    [SerializeField] private float messHoldSeconds = 1.25f;
    [SerializeField] private float messAppearDelayAfterEating = 0.5f;
    [SerializeField] private bool isDirty;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    [Header("Runtime")]
    [SerializeField] private GameObject menuInstance;
    [SerializeField] private CustomerGroup currentGroup;

    private bool messSpawnedForCurrentGroup;
    private float eatingTimer = -1f;

    public CustomerGroup CurrentGroup => currentGroup;
    public bool IsDirty => isDirty;
    public float MessHoldSeconds => messHoldSeconds;

    private void Awake()
    {
        if (cleanUI == null && cleanUIRoot != null)
            cleanUI = cleanUIRoot.GetComponentInChildren<BoothMessCleanUI>(true);

        if (cleanUI != null)
            cleanUI.Setup(this, FindSceneCamera());

        ApplyDirtyVisuals();
        RefreshCleanUIVisibility();
    }

    private void Update()
    {
        TrackMessyCustomerSpill();
        RefreshCleanUIVisibility();
    }

    public void SetCurrentGroup(CustomerGroup g)
    {
        currentGroup = g;
        messSpawnedForCurrentGroup = false;
        eatingTimer = -1f;

        if (cleanUI != null)
            cleanUI.Setup(this, FindSceneCamera());

        if (debugLogs)
        {
            string typeName = g != null ? g.CurrentCustomerType.ToString() : "NULL";
            bool messy = g != null && g.IsMessy;
            string stateName = g != null ? g.state.ToString() : "NULL";

            Debug.Log($"[Booth] {name} SetCurrentGroup -> {(g != null ? g.name : "NULL")} | type={typeName} | messy={messy} | state={stateName}", this);
        }

        RefreshCleanUIVisibility();
    }

    public void ClearCurrentGroup()
    {
        if (debugLogs)
            Debug.Log($"[Booth] {name} ClearCurrentGroup", this);

        currentGroup = null;
        eatingTimer = -1f;

        RefreshCleanUIVisibility();
    }

    public bool IsAvailableFor(int groupSize)
    {
        if (isDirty) return false;
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
        Vector3 dir = tableLookTarget != null ? tableLookTarget.position - seatPos : transform.forward;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;

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

    public void ClearBoothProps()
    {
        ClearMenuBook();
    }

    public void SetDirty(bool value)
    {
        if (isDirty == value)
            return;

        isDirty = value;

        if (debugLogs)
            Debug.Log($"[Booth] {name} SetDirty = {isDirty}", this);

        ApplyDirtyVisuals();
        RefreshCleanUIVisibility();
    }

    public void CleanMess()
    {
        SetDirty(false);
    }

    public void OnTableCleaned()
    {
        CleanMess();
    }

    public void ForceDirtyForTest()
    {
        SetDirty(true);
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

        if (TryInvokeComponentMethod(tray, "TrayHoldToClean", "Arm", this, group)) return;
        if (TryInvokeComponentMethod(tray, "TrayCleanable", "ArmForCleaning", this)) return;
    }

    private void TrackMessyCustomerSpill()
    {
        if (currentGroup == null)
            return;

        if (!currentGroup.IsMessy)
            return;

        if (messSpawnedForCurrentGroup)
            return;

        if (currentGroup.state != CustomerGroup.GroupState.Eating)
        {
            eatingTimer = -1f;
            return;
        }

        if (eatingTimer < 0f)
            eatingTimer = 0f;

        eatingTimer += Time.deltaTime;

        if (eatingTimer < messAppearDelayAfterEating)
            return;

        messSpawnedForCurrentGroup = true;
        SetDirty(true);

        if (debugLogs)
            Debug.Log($"[Booth] {name} spawned spill because messy group started eating.", this);
    }

    private void ApplyDirtyVisuals()
    {
        if (puddle != null)
            puddle.SetActive(isDirty);
    }

    private void RefreshCleanUIVisibility()
    {
        if (cleanUIRoot == null)
            return;

        bool show = ShouldShowCleanUI();
        cleanUIRoot.SetActive(show);

        if (cleanUI != null && cleanUI.gameObject.activeSelf != show)
            cleanUI.gameObject.SetActive(show);
    }

    private bool ShouldShowCleanUI()
    {
        if (!isDirty)
            return false;

        if (currentGroup != null)
            return false;

        if (HasTrayOnTable())
            return false;

        return true;
    }

    private Camera FindSceneCamera()
    {
        Camera main = Camera.main;
        if (main != null)
            return main;

        GameObject tagged = GameObject.FindGameObjectWithTag("RoomCamera");
        if (tagged != null)
            return tagged.GetComponent<Camera>();

        return FindFirstObjectByType<Camera>();
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

    private static bool TryInvokeComponentMethod(Component host, string componentName, string methodName, params object[] args)
    {
        if (host == null) return false;

        var comp = host.GetComponent(componentName);
        if (comp == null) return false;

        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var methods = comp.GetType().GetMethods(flags);

        for (int i = 0; i < methods.Length; i++)
        {
            var method = methods[i];
            if (method.Name != methodName) continue;

            var parameters = method.GetParameters();
            if (parameters.Length != args.Length) continue;

            method.Invoke(comp, args);
            return true;
        }

        return false;
    }
}