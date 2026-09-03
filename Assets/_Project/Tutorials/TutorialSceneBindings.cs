using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Tutorial-side references for UI that is instantiated by the real HUD.</summary>
[DisallowMultipleComponent]
public sealed class TutorialSceneBindings : MonoBehaviour
{
    [Serializable]
    public struct UITarget
    {
        public string key;
        public RectTransform target;
    }

    [SerializeField] private UITarget[] uiTargets = Array.Empty<UITarget>();
    private Canvas revealedCanvas;
    private bool previousCanvasEnabled;
    private readonly List<(CanvasGroup group, float alpha, bool interactable, bool blocks)> groups = new();
    private readonly List<(GameObject obj, bool active)> objects = new();
    private Button revealedButton;
    private bool previousButtonInteractable;

    public RectTransform ResolveUI(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (UITarget binding in uiTargets)
            if (binding.key == key && binding.target != null) return binding.target;

        switch (key)
        {
            case "AlienApproval": return ResolveProgressTarget("ApprovalProgress");
            // This HUD reports EarnedToday / TotalRequiredEarningsToday, not wallet cash.
            // TODO Tutorial: teach MoneyManager.Money in the Management Finance lesson.
            case "TodaySales": return ResolveProgressTarget("SalesProgress");
            case "TodaySalesValue": return ResolveProgressTarget("SalesProgress", "Value");
            case "TodaySalesTrack": return ResolveProgressTarget("SalesProgress", "Track");
            case "Neutral": return ResolveProgressTarget("NeutralProgress");
            case "Angry": return ResolveProgressTarget("AngryProgress");
            case "LivePanel": return ResolveControl("LivePanel");
            case "LiveCounts": return ResolveControl("LivePanel/Counts");
            case "NewspaperButton": case "TaskButton": case "TaskMessage":
            case "CameraButton": case "ComputerButton": return ResolveControl(key);
            case "NewspaperClose": return FindNamedUI("Close Newspaper");
            case "DashboardButton":
                return FindNamedUI("DASHBOARD", FindFirstObjectByType<ManagementComputerController>(FindObjectsInactive.Include)?.transform);
            case "ManagementOverview":
                ManagementComputerController computer = FindFirstObjectByType<ManagementComputerController>(FindObjectsInactive.Include);
                return computer != null && computer.AppWindow != null && computer.AppWindow.Content != null
                    ? computer.AppWindow.Content.parent as RectTransform : null;
        }
        // TODO Tutorial: bind HUD buttons, Management tabs, restock controls, Start Shift.
        // World targets remain TutorialStep.highlightTarget / TutorialTargetIndicator references.
        // Gameplay adapters should subscribe/unsubscribe existing real events and call
        // TutorialSystem.NotifyAction(key, context). Do not synthesize successful actions.
        return null;
    }

    private static RectTransform ResolveControl(string path) =>
        LobbyHUDRedesign.Instance != null ? LobbyHUDRedesign.Instance.transform.Find("SafeArea/" + path) as RectTransform : null;

    private static RectTransform FindNamedUI(string name, Transform parent = null)
    {
        RectTransform[] candidates = parent != null ? parent.GetComponentsInChildren<RectTransform>(true)
            : FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (RectTransform candidate in candidates)
            if (candidate.name == name) return candidate;
        return null;
    }

    private static RectTransform ResolveProgressTarget(string rowName, string childPath = null)
    {
        // The real combined HUD is persistent and normally only visible in Lobby1.
        CasualDiningProgressHUD hud = CasualDiningProgressHUD.Instance;
        if (hud == null) return null;
        foreach (RectTransform rect in hud.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect.name != rowName || !rect.gameObject.activeInHierarchy) continue;
            return childPath == null ? rect : rect.Find(childPath) as RectTransform;
        }
        return null;
    }

    public void BeginUIFocus(RectTransform target)
    {
        EndUIFocus();
        if (target == null) return;
        // Only reveal the real HUD branches that exclude Lobby1Tutorial. Modal
        // panels must stay controlled by their actual open/close actions.
        if (target.GetComponentInParent<CasualDiningProgressHUD>(true) == null &&
            target.GetComponentInParent<LobbyHUDRedesign>(true) == null) return;
        revealedCanvas = target.GetComponentInParent<Canvas>(true);
        if (revealedCanvas != null) previousCanvasEnabled = revealedCanvas.enabled;
        for (Transform t = target; t != null; t = t.parent)
        {
            objects.Add((t.gameObject, t.gameObject.activeSelf));
            CanvasGroup group = t.GetComponent<CanvasGroup>();
            if (group != null) groups.Add((group, group.alpha, group.interactable, group.blocksRaycasts));
            if (revealedCanvas != null && t == revealedCanvas.transform) break;
        }
        revealedButton = target.GetComponent<Button>();
        if (revealedButton != null) previousButtonInteractable = revealedButton.interactable;
        LateUpdate();
    }

    private void LateUpdate()
    {
        // Temporary presentation override only; shared HUD still reads real game data.
        // Its normal Update hides unsupported scenes, so reveal after that update.
        if (revealedCanvas != null) revealedCanvas.enabled = true;
        foreach (var state in objects) if (state.obj != null) state.obj.SetActive(true);
        foreach (var state in groups)
            if (state.group != null)
            {
                state.group.alpha = 1f;
                state.group.interactable = state.group.blocksRaycasts = true;
            }
        if (revealedButton != null) revealedButton.interactable = true;
    }

    public void EndUIFocus()
    {
        if (revealedCanvas != null) revealedCanvas.enabled = previousCanvasEnabled;
        foreach (var state in groups)
            if (state.group != null)
            {
                state.group.alpha = state.alpha;
                state.group.interactable = state.interactable;
                state.group.blocksRaycasts = state.blocks;
            }
        foreach (var state in objects) if (state.obj != null) state.obj.SetActive(state.active);
        if (revealedButton != null) revealedButton.interactable = previousButtonInteractable;
        revealedCanvas = null;
        revealedButton = null;
        groups.Clear();
        objects.Clear();
    }

    private void OnDisable() => EndUIFocus();
}
