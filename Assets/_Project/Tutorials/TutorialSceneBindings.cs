using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tutorial-side references for UI instantiated by the real HUD / Management Computer.
/// This script never edits shared gameplay code; it only resolves and temporarily presents
/// the real scene UI while a tutorial step is focused on it.
/// </summary>
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
        if (string.IsNullOrEmpty(key))
            return null;

        // Inspector bindings always win. This lets later tutorial lessons bind exact
        // authored controls without changing this script or the gameplay UI.
        foreach (UITarget binding in uiTargets)
            if (string.Equals(binding.key, key, StringComparison.Ordinal) && binding.target != null)
                return binding.target;

        switch (key)
        {
            // ── Progress HUD ──────────────────────────────────────────────────
            case "AlienApproval":
                return ResolveProgressTarget("ApprovalProgress");

            // The current green HUD bar is today's earned / required revenue goal,
            // not wallet cash. Keep this distinction in player-facing tutorial text.
            case "TodaySales":
                return ResolveProgressTarget("SalesProgress");
            case "TodaySalesValue":
                return ResolveProgressTarget("SalesProgress", "Value");
            case "TodaySalesTrack":
                return ResolveProgressTarget("SalesProgress", "Track");

            // Player-facing term is UNSATISFIED. Internal gameplay names stay Neutral.
            case "Unsatisfied":
            case "Neutral": // backwards compatibility with already-authored steps
                return ResolveProgressTarget("NeutralProgress");
            case "UnsatisfiedValue":
            case "NeutralValue":
                return ResolveProgressTarget("NeutralProgress", "Value");
            case "Angry":
                return ResolveProgressTarget("AngryProgress");
            case "AngryValue":
                return ResolveProgressTarget("AngryProgress", "Value");

            // ── Lobby HUD ─────────────────────────────────────────────────────
            case "LivePanel":
                return ResolveControl("LivePanel");
            case "LiveCounts":
                return ResolveControl("LivePanel/Counts");
            case "NewspaperButton":
            case "TaskButton":
            case "TaskMessage":
            case "CameraButton":
            case "ComputerButton":
                return ResolveControl(key);
            case "NewspaperClose":
                return FindNamedUI("Close Newspaper");

            // ── Management Computer navigation ───────────────────────────────
            case "DashboardButton":
                return ResolveManagementButton("DASHBOARD");
            case "StaffButton":
                return ResolveManagementButton("STAFF", "STAFF SCHEDULER");
            case "MenuButton":
                return ResolveManagementButton("MENU", "MENU EDITOR");
            case "EquipmentButton":
                return ResolveManagementButton("EQUIPMENT", "EQUIPMENT STORE");
            case "FinanceButton":
                return ResolveManagementButton("FINANCE");
            case "ObjectivesButton":
                return ResolveManagementButton("OBJECTIVES");
            case "RestockButton":
                return ResolveManagementButton("RESTOCK", "INGREDIENT RESTOCK");

            case "ManagementOverview":
            {
                ManagementComputerController computer = FindManagementComputer();
                return computer != null && computer.AppWindow != null && computer.AppWindow.Content != null
                    ? computer.AppWindow.Content.parent as RectTransform
                    : null;
            }
        }

        // Future detailed page targets (staff slots, applicant cards, menu controls,
        // finance cards, restock quantity/cart/checkout, etc.) should normally be added
        // through the inspector uiTargets list, keeping gameplay scripts untouched.
        return null;
    }

    private static RectTransform ResolveControl(string path) =>
        LobbyHUDRedesign.Instance != null
            ? LobbyHUDRedesign.Instance.transform.Find("SafeArea/" + path) as RectTransform
            : null;

    private static ManagementComputerController FindManagementComputer() =>
        FindFirstObjectByType<ManagementComputerController>(FindObjectsInactive.Include);

    /// <summary>
    /// Resolves the real Management navigation button by its visible label instead of
    /// relying on brittle hierarchy paths. Exact labels are preferred; contained labels
    /// are only used as a fallback (e.g. STAFF SCHEDULER).
    /// </summary>
    private static RectTransform ResolveManagementButton(params string[] labels)
    {
        ManagementComputerController computer = FindManagementComputer();
        if (computer == null || labels == null || labels.Length == 0)
            return null;

        Button[] buttons = computer.GetComponentsInChildren<Button>(true);

        // Pass 1: exact visible label.
        foreach (Button button in buttons)
        {
            string visible = GetButtonLabel(button);
            foreach (string label in labels)
                if (string.Equals(visible, NormalizeLabel(label), StringComparison.Ordinal))
                    return button.transform as RectTransform;
        }

        // Pass 2: contained visible label. Prefer the shortest matching text so a
        // sidebar "STAFF" button wins over unrelated long descriptions when possible.
        RectTransform best = null;
        int bestLength = int.MaxValue;
        foreach (Button button in buttons)
        {
            string visible = GetButtonLabel(button);
            if (string.IsNullOrEmpty(visible))
                continue;

            foreach (string label in labels)
            {
                string wanted = NormalizeLabel(label);
                if (visible.IndexOf(wanted, StringComparison.Ordinal) >= 0 ||
                    wanted.IndexOf(visible, StringComparison.Ordinal) >= 0)
                {
                    if (visible.Length < bestLength)
                    {
                        best = button.transform as RectTransform;
                        bestLength = visible.Length;
                    }
                }
            }
        }

        return best;
    }

    private static string GetButtonLabel(Button button)
    {
        if (button == null)
            return string.Empty;

        TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text label in labels)
        {
            string normalized = NormalizeLabel(label != null ? label.text : null);
            if (!string.IsNullOrEmpty(normalized))
                return normalized;
        }

        return NormalizeLabel(button.gameObject.name);
    }

    private static string NormalizeLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string[] pieces = value.Trim().ToUpperInvariant()
            .Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", pieces);
    }

    private static RectTransform FindNamedUI(string name, Transform parent = null)
    {
        RectTransform[] candidates = parent != null
            ? parent.GetComponentsInChildren<RectTransform>(true)
            : FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (RectTransform candidate in candidates)
            if (candidate.name == name)
                return candidate;

        return null;
    }

    private static RectTransform ResolveProgressTarget(string rowName, string childPath = null)
    {
        // The real combined HUD is persistent and normally only visible in Lobby1.
        CasualDiningProgressHUD hud = CasualDiningProgressHUD.Instance;
        if (hud == null)
            return null;

        foreach (RectTransform rect in hud.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect.name != rowName || !rect.gameObject.activeInHierarchy)
                continue;

            return childPath == null ? rect : rect.Find(childPath) as RectTransform;
        }

        return null;
    }

    public void BeginUIFocus(RectTransform target)
    {
        EndUIFocus();
        if (target == null)
            return;

        // Only force-reveal the persistent HUD branches that normally exclude
        // Lobby1Tutorial. Modal windows such as Newspaper / Management / Restock must
        // still be opened by their real gameplay buttons and are never force-opened here.
        if (target.GetComponentInParent<CasualDiningProgressHUD>(true) == null &&
            target.GetComponentInParent<LobbyHUDRedesign>(true) == null)
            return;

        revealedCanvas = target.GetComponentInParent<Canvas>(true);
        if (revealedCanvas != null)
            previousCanvasEnabled = revealedCanvas.enabled;

        for (Transform t = target; t != null; t = t.parent)
        {
            objects.Add((t.gameObject, t.gameObject.activeSelf));

            CanvasGroup group = t.GetComponent<CanvasGroup>();
            if (group != null)
                groups.Add((group, group.alpha, group.interactable, group.blocksRaycasts));

            if (revealedCanvas != null && t == revealedCanvas.transform)
                break;
        }

        revealedButton = target.GetComponent<Button>();
        if (revealedButton != null)
            previousButtonInteractable = revealedButton.interactable;

        LateUpdate();
    }

    private void LateUpdate()
    {
        // Presentation override only. Shared HUD still reads real gameplay data.
        // Reveal after its own Update may hide unsupported scenes.
        if (revealedCanvas != null)
            revealedCanvas.enabled = true;

        foreach (var state in objects)
            if (state.obj != null)
                state.obj.SetActive(true);

        foreach (var state in groups)
            if (state.group != null)
            {
                state.group.alpha = 1f;
                state.group.interactable = true;
                state.group.blocksRaycasts = true;
            }

        if (revealedButton != null)
            revealedButton.interactable = true;
    }

    public void EndUIFocus()
    {
        if (revealedCanvas != null)
            revealedCanvas.enabled = previousCanvasEnabled;

        foreach (var state in groups)
            if (state.group != null)
            {
                state.group.alpha = state.alpha;
                state.group.interactable = state.interactable;
                state.group.blocksRaycasts = state.blocks;
            }

        foreach (var state in objects)
            if (state.obj != null)
                state.obj.SetActive(state.active);

        if (revealedButton != null)
            revealedButton.interactable = previousButtonInteractable;

        revealedCanvas = null;
        revealedButton = null;
        groups.Clear();
        objects.Clear();
    }

    private void OnDisable() => EndUIFocus();
}
