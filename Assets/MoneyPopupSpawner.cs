using UnityEngine;

public class MoneyPopupSpawner : MonoBehaviour
{
    public static MoneyPopupSpawner Instance { get; private set; }

    [SerializeField] private GameObject moneyPopupPrefab;

    private Canvas gameplayCanvas;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        gameplayCanvas = UIRoot.GameplayCanvasOrNull();
        if (gameplayCanvas == null)
            gameplayCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
    }

    public void Spawn(int amount, Transform worldAnchor, Vector3 worldOffset, Camera cam)
    {
        if (moneyPopupPrefab == null) return;
        if (gameplayCanvas == null) return;
        if (worldAnchor == null) return;

        var go = Instantiate(moneyPopupPrefab, gameplayCanvas.transform);

        var follow = go.GetComponentInChildren<UIFollowWorldPoint>(true);
        if (follow != null)
            follow.Init(worldAnchor, worldOffset, cam);

        var ui = go.GetComponent<MoneyPopupUI>();
        if (ui != null)
            ui.Init(amount);
    }
}