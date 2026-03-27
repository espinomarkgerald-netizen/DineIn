using UnityEngine;

public class GameplayUIBlocker : MonoBehaviour
{
    public static GameplayUIBlocker Instance { get; private set; }

    [System.Serializable]
    public class BlockingEntry
    {
        public GameObject target;
        public bool blocksGameplay = true;
    }

    [Header("Blocking Panels")]
    [SerializeField] private BlockingEntry[] blockingPanels;

    private void Awake()
    {
        Instance = this;
    }

    public static bool IsBlocked()
    {
        if (Instance == null) return false;

        var entries = Instance.blockingPanels;
        if (entries == null || entries.Length == 0) return false;

        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry == null) continue;
            if (!entry.blocksGameplay) continue;
            if (entry.target == null) continue;
            if (!entry.target.activeInHierarchy) continue;

            CanvasGroup cg = entry.target.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                if (cg.alpha > 0.01f && cg.blocksRaycasts)
                    return true;

                continue;
            }

            return true;
        }

        return false;
    }

    public static bool IsBlockedExcept(GameObject exemptObject)
    {
        if (Instance == null) return false;

        var entries = Instance.blockingPanels;
        if (entries == null || entries.Length == 0) return false;

        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry == null) continue;
            if (!entry.blocksGameplay) continue;
            if (entry.target == null) continue;
            if (!entry.target.activeInHierarchy) continue;

            if (exemptObject != null)
            {
                if (entry.target == exemptObject) continue;
                if (exemptObject.transform.IsChildOf(entry.target.transform)) continue;
                if (entry.target.transform.IsChildOf(exemptObject.transform)) continue;
            }

            CanvasGroup cg = entry.target.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                if (cg.alpha > 0.01f && cg.blocksRaycasts)
                    return true;

                continue;
            }

            return true;
        }

        return false;
    }
}