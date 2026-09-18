using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HygieneDialogue : MonoBehaviour
{
    public sealed class View
    {
        public GameObject root;
        public TMP_Text title, message, response, coaching;
        public Image portrait;
        public Button[] buttons;
        public TMP_Text[] labels;
    }
    private GameObject canvasRoot;
    private View view;
    private int shownDecision = -1;
    private bool shownAuthority;
    public bool Initialize()
    {
        bool ready = EnsureView();
        SetVisible(false);
        return ready;
    }

    private bool EnsureView()
    {
        if (view != null) return true;
        canvasRoot = new GameObject("Staff Hygiene Decision", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        view = ManagerComplaintSystem.CreateHygieneDialogue(canvasRoot.transform, canvasRoot.GetComponent<CanvasScaler>());
        if (view == null) { Destroy(canvasRoot); canvasRoot = null; enabled = false; return false; }
        var blocker = GameplayUIBlocker.Instance;
        if (blocker != null) blocker.SetPanelBlocksGameplay(canvasRoot, true);
        return true;
    }
    public void Present(HygieneState state, bool authority, Action<int, HygieneDecision> choose)
    {
        if (!state.decisionOpen) { SetVisible(false); return; }
        if (!EnsureView()) return;
        SetVisible(true);
        if (shownDecision == state.decisionId && shownAuthority == authority) return;
        shownDecision = state.decisionId;
        shownAuthority = authority;
        bool kitchen = state.decisionArea == HygieneArea.Kitchen;
        view.title.text = kitchen ? "KITCHEN STAFF" : "LOBBY STAFF";
        view.message.text = kitchen
            ? "Sir, the kitchen is getting pretty dirty but is still operational. Would you like me to clean it or keep making orders?"
            : "Sir, the lobby needs cleaning. Could you help with one cleanup task while we keep serving?";
        if (view.response != null) view.response.text = "";
        if (view.coaching != null) view.coaching.text = authority
            ? (kitchen ? "Choose how the kitchen should continue." : "You will help with one available cleanup task.")
            : "Waiting for the host to decide...";
        if (view.portrait != null) view.portrait.gameObject.SetActive(false);
        var settings = HygieneSettings.Current;
        string[] labels = kitchen ? new[] { $"Clean now\nPause cooking for {settings.immediateCleanSeconds:0.#} seconds", $"Clean while cooking\n{settings.whileCookingCleanSeconds:0.#} seconds; cooking takes {(settings.cleaningCookMultiplier - 1f) * 100f:0.#}% longer", $"Keep cooking\n{settings.forcedCleanSeconds:0.#}-second clean when blocked" }
            : new[] { "Help clean now", "Keep serving", "Ask staff to clean the lobby" };
        HygieneDecision[] decisions = kitchen ? new[] { HygieneDecision.CleanNow, HygieneDecision.CleanWhileCooking, HygieneDecision.ContinueUntilBlocked }
            : new[] { HygieneDecision.HelpClean, HygieneDecision.KeepServing, HygieneDecision.CleanLobby };
        int id = state.decisionId;
        for (int i = 0; i < view.buttons.Length; i++)
        {
            var button = view.buttons[i];
            var decision = decisions[i];
            button.gameObject.SetActive(true);
            button.interactable = authority;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => choose(id, decision));
            view.labels[i].text = labels[i];
            view.labels[i].enableAutoSizing = true;
            view.labels[i].fontSizeMin = 12f;
        }
    }
    public void SetVisible(bool visible)
    {
        if (canvasRoot != null) canvasRoot.SetActive(visible);
        if (!visible) shownDecision = -1;
    }
    private void OnDestroy()
    {
        if (canvasRoot != null)
        {
            GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(canvasRoot, false);
            Destroy(canvasRoot);
        }
    }
}

// Clone only the existing dialogue presentation, never its gameplay controller.
public sealed partial class ManagerComplaintSystem
{
    public static HygieneDialogue.View CreateHygieneDialogue(Transform parent, CanvasScaler scaler)
    {
        var asset = Resources.Load<GameObject>(SystemResourcePath);
        var prefab = asset != null ? asset.GetComponent<ManagerComplaintSystem>() : null;
        if (prefab == null || prefab.dialogueRoot == null)
        { Debug.LogError("[Hygiene] The staff dialogue needs the existing complaint UI prefab."); return null; }
        var root = Instantiate(prefab.dialogueRoot, parent, false);
        root.name = "StaffDecision";
        root.SetActive(true);
        var sourceScaler = prefab.dialogueRoot.GetComponentInParent<CanvasScaler>(true);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = sourceScaler != null ? sourceScaler.referenceResolution : new Vector2(1920f, 1080f);
        scaler.screenMatchMode = sourceScaler != null ? sourceScaler.screenMatchMode : CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = sourceScaler != null ? sourceScaler.matchWidthOrHeight : .5f;
        T Map<T>(T original) where T : Component
        {
            if (original == null) return null;
            var indices = new System.Collections.Generic.List<int>();
            for (var t = original.transform; t != prefab.dialogueRoot.transform; t = t.parent)
            {
                if (t == null) return null;
                indices.Add(t.GetSiblingIndex());
            }
            var copy = root.transform;
            for (int i = indices.Count - 1; i >= 0; i--) copy = copy.GetChild(indices[i]);
            return copy.GetComponent<T>();
        }
        var view = new HygieneDialogue.View
        {
            root = root, title = Map(prefab.headlineText), message = Map(prefab.customerLineText),
            response = Map(prefab.managerResponseText), coaching = Map(prefab.coachingText), portrait = Map(prefab.portraitImage),
            buttons = new[] { Map(prefab.professionalButton), Map(prefab.acceptableButton), Map(prefab.poorButton) },
            labels = new[] { Map(prefab.professionalButtonText), Map(prefab.acceptableButtonText), Map(prefab.poorButtonText) }
        };
        if (view.title == null || view.message == null || Array.Exists(view.buttons, b => b == null) || Array.Exists(view.labels, l => l == null))
        { Destroy(root); Debug.LogError("[Hygiene] Complaint dialogue bindings are incomplete."); return null; }
        var style = HygieneCleaningPresentation.Load();
        if (style == null || style.choiceButton == null)
        { Destroy(root); Debug.LogError("[Hygiene] Missing green cleaning button asset."); return null; }
        foreach (var button in view.buttons)
        {
            var background = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (background == null) continue;
            background.sprite = style.choiceButton;
            background.overrideSprite = null;
            background.type = Image.Type.Sliced;
            background.color = Color.white;
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            button.spriteState = default;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.9f, 1f, .9f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.7f, .85f, .7f);
            colors.disabledColor = new Color(.6f, .6f, .6f, .65f);
            button.colors = colors;
        }
        return view;
    }
}
