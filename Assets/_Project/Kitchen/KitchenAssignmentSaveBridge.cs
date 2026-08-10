using UnityEngine;

public class KitchenAssignmentSaveBridge : MonoBehaviour
{
    public static KitchenAssignmentSaveBridge Instance { get; private set; }

    private const string ChefNameKey = "DineIn_AssignedChefName";
    private const string ChefStarsKey = "DineIn_AssignedChefStars";
    private const string BaristaNameKey = "DineIn_AssignedBaristaName";
    private const string BaristaStarsKey = "DineIn_AssignedBaristaStars";

    [Header("Loaded Assignment")]
    [SerializeField] private string assignedChefName;
    [SerializeField] private int assignedChefStars;
    [SerializeField] private string assignedBaristaName;
    [SerializeField] private int assignedBaristaStars;

    public string AssignedChefName => assignedChefName;
    public int AssignedChefStars => assignedChefStars;
    public string AssignedBaristaName => assignedBaristaName;
    public int AssignedBaristaStars => assignedBaristaStars;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadKitchenAssignment();
    }

    public void SaveAssignedEmployee(EmployeeData employee)
    {
        if (employee == null)
            return;

        string roleName = employee.role.ToString();

        if (roleName == "Chef")
        {
            assignedChefName = employee.employeeName;
            assignedChefStars = employee.stars;

            PlayerPrefs.SetString(ChefNameKey, assignedChefName);
            PlayerPrefs.SetInt(ChefStarsKey, assignedChefStars);

            Debug.Log($"[KitchenAssignment] Saved Chef: {assignedChefName} ({assignedChefStars}★)");
        }
        else if (roleName == "Barista")
        {
            assignedBaristaName = employee.employeeName;
            assignedBaristaStars = employee.stars;

            PlayerPrefs.SetString(BaristaNameKey, assignedBaristaName);
            PlayerPrefs.SetInt(BaristaStarsKey, assignedBaristaStars);

            Debug.Log($"[KitchenAssignment] Saved Barista: {assignedBaristaName} ({assignedBaristaStars}★)");
        }

        PlayerPrefs.Save();
    }

    public void LoadKitchenAssignment()
    {
        assignedChefName = PlayerPrefs.GetString(ChefNameKey, "");
        assignedChefStars = PlayerPrefs.GetInt(ChefStarsKey, 0);
        assignedBaristaName = PlayerPrefs.GetString(BaristaNameKey, "");
        assignedBaristaStars = PlayerPrefs.GetInt(BaristaStarsKey, 0);
    }

    public float GetChefSpawnTime()
    {
        return GetTimeFromStars(assignedChefStars);
    }

    public float GetBaristaSpawnTime()
    {
        return GetTimeFromStars(assignedBaristaStars);
    }

    public float GetMealSpawnTime()
    {
        float total = GetChefSpawnTime() + GetBaristaSpawnTime();
        return Mathf.Max(1f, total);
    }

    private float GetTimeFromStars(int stars)
    {
        switch (stars)
        {
            case 1: return 3f;
            case 2: return 2.5f;
            case 3: return 2f;
            case 4: return 1.5f;
            case 5: return 0.5f;
            default: return 3f;
        }
    }

    public void SaveKitchenAssignment()
    {
        PlayerPrefs.SetString(ChefNameKey, assignedChefName);
        PlayerPrefs.SetInt(ChefStarsKey, assignedChefStars);
        PlayerPrefs.SetString(BaristaNameKey, assignedBaristaName);
        PlayerPrefs.SetInt(BaristaStarsKey, assignedBaristaStars);
        PlayerPrefs.Save();

        Debug.Log($"[KitchenAssignment] Saved Chef: {assignedChefName} ({assignedChefStars}★)");
        Debug.Log($"[KitchenAssignment] Saved Barista: {assignedBaristaName} ({assignedBaristaStars}★)");
    }

    public void SetChef(string name, int stars)
    {
        assignedChefName = name;
        assignedChefStars = stars;
    }

    public void SetBarista(string name, int stars)
    {
        assignedBaristaName = name;
        assignedBaristaStars = stars;
    }
}