using UnityEngine;

public class ExitManager : MonoBehaviour
{
    public static ExitManager Instance { get; private set; }

    [Header("Assign in Scene")]
    public Transform exitPoint;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public static Transform ExitPointOrNull()
    {
        return Instance != null ? Instance.exitPoint : null;
    }
}
