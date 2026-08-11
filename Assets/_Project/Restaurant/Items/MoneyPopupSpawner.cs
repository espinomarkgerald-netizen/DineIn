using UnityEngine;

public class MoneyPopupSpawner : MonoBehaviour
{
    public static MoneyPopupSpawner Instance { get; private set; }

    [SerializeField] private GameObject moneyPopupPrefab;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Spawn(int amount, Transform worldAnchor, Vector3 worldOffset, Camera cam)
    {
        if (moneyPopupPrefab == null) return;
        if (worldAnchor == null) return;

        var go = Instantiate(moneyPopupPrefab);

        var follow = go.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            follow.Init(worldAnchor, worldOffset, cam);

        var ui = go.GetComponent<MoneyPopupUI>();
        if (ui != null)
            ui.Init(amount);
    }
}
