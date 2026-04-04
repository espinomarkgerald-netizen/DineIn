using System;
using System.Collections.Generic;
using UnityEngine;

public class GameplayUIBlocker : MonoBehaviour
{
    public static GameplayUIBlocker Instance { get; private set; }

    [Serializable]
    public class BlockingPanel
    {
        public GameObject target;
        public bool blocksGameplay = true;
    }

    [SerializeField] private List<BlockingPanel> blockingPanels = new();
    [SerializeField] private bool debugBlocking = true;

    public bool IsGameplayBlocked => IsAnyBlockingPanelActive(null);

    public static bool IsBlocked()
    {
        return Instance != null && Instance.IsAnyBlockingPanelActive(null);
    }

    public static bool IsBlockedExcept(GameObject ignoredTarget)
    {
        return Instance != null && Instance.IsAnyBlockingPanelActive(ignoredTarget);
    }

    public static bool IsBlockedExcept(Component ignoredComponent)
    {
        return IsBlockedExcept(ignoredComponent != null ? ignoredComponent.gameObject : null);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool IsAnyBlockingPanelActive(GameObject ignoredTarget)
    {
        for (int i = 0; i < blockingPanels.Count; i++)
        {
            BlockingPanel panel = blockingPanels[i];

            if (panel == null || !panel.blocksGameplay || panel.target == null)
                continue;

            if (panel.target == ignoredTarget)
                continue;

            if (IsPanelActuallyBlocking(panel.target))
            {
                if (debugBlocking)
                    Debug.Log($"[GameplayUIBlocker] Blocking because of: {panel.target.name}", panel.target);

                return true;
            }
        }

        return false;
    }

    public bool IsPanelActuallyBlocking(GameObject target)
    {
        if (target == null)
            return false;

        if (!target.activeInHierarchy || !target.activeSelf)
            return false;

        CanvasGroup[] groups = target.GetComponentsInParent<CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            CanvasGroup cg = groups[i];
            if (cg == null || !cg.enabled)
                continue;

            if (cg.alpha <= 0.001f)
                return false;

            if (!cg.blocksRaycasts)
                return false;
        }

        Canvas[] canvases = target.GetComponentsInParent<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
                continue;

            if (!canvas.enabled)
                return false;
        }

        return true;
    }

    public void SetPanelBlocksGameplay(GameObject target, bool shouldBlock)
    {
        if (target == null)
            return;

        for (int i = 0; i < blockingPanels.Count; i++)
        {
            if (blockingPanels[i] != null && blockingPanels[i].target == target)
            {
                blockingPanels[i].blocksGameplay = shouldBlock;
                return;
            }
        }

        blockingPanels.Add(new BlockingPanel
        {
            target = target,
            blocksGameplay = shouldBlock
        });
    }
}