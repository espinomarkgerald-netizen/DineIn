using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Preserves task ownership and limits expanded action controls in Fine Dining.</summary>
[DefaultExecutionOrder(11900)]
[DisallowMultipleComponent]
public sealed class PlayerTaskBubbleFocus : MonoBehaviour
{
    public static float BackgroundAlpha { get; set; } = 0.28f;

    private CanvasGroup focusGroup;
    private UIFollowWorldPoint follow;
    private UnityEngine.Object target;
    private static readonly List<PlayerTaskBubbleFocus> Active = new();
    private static readonly List<PlayerTaskBubbleFocus> Candidates = new();
    private static int refreshedFrame = -1;
    private static long nextSequence;
    private CustomerGroup customer;
    private FineDiningReservations settings;
    private TMP_Text[] captions;
    private bool[] captionEnabled;
    private bool[] captionIdentifiers;
    private bool hasIcon;
    private bool passiveStatus;
    private int priority;
    private long sequence;
    private bool expanded;

    public static RectTransform GuidanceTarget(UnityEngine.Object task)
    {
        if (task == null) return null;
        foreach (var bubble in Active)
            if (bubble != null && bubble.isActiveAndEnabled && bubble.target == task && !bubble.passiveStatus)
                return bubble.transform as RectTransform;
        return null;
    }

    public static PlayerTaskBubbleFocus Bind(
        GameObject visualRoot,
        UnityEngine.Object taskTarget,
        bool statusOnly = false)
    {
        if (visualRoot == null)
            return null;

        var profile = taskTarget is Component component ? FineDiningReservations.For(component) : null;
        if (profile != null && FineDiningScene.Contains(profile.gameObject.scene))
        {
            var owner = visualRoot.GetComponentInParent<UIFollowWorldPoint>(true);
            if (owner == null) owner = visualRoot.GetComponentInChildren<UIFollowWorldPoint>(true);
            if (owner != null) visualRoot = owner.gameObject;
        }
        else profile = null;

        PlayerTaskBubbleFocus focus =
            visualRoot.GetComponent<PlayerTaskBubbleFocus>();
        if (focus == null)
            focus = visualRoot.AddComponent<PlayerTaskBubbleFocus>();

        bool changed = focus.target != taskTarget || focus.settings != profile || focus.passiveStatus != statusOnly;
        if (changed) focus.RestoreCaptions();
        focus.target = taskTarget;
        focus.settings = profile;
        focus.passiveStatus = statusOnly;
        focus.customer = taskTarget as CustomerGroup;
        if (taskTarget is FoodTray tray) focus.customer = tray.TargetGroup;
        if (taskTarget is MoneyPickup money) focus.customer = money.TargetGroup;
        if (changed)
        {
            focus.sequence = ++nextSequence;
            focus.CacheCaptions();
        }
        if (profile != null && focus.follow != null)
        {
            focus.follow.ConfigureFineDiningSizing(profile);
            if (changed) focus.SetCompact(true);
        }
        refreshedFrame = -1;
        focus.Refresh();
        return focus;
    }

    private void Awake()
    {
        follow = GetComponent<UIFollowWorldPoint>();
        if (follow == null)
        {
            focusGroup = GetComponent<CanvasGroup>();
            if (focusGroup == null) focusGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        if (!Active.Contains(this)) Active.Add(this);
        refreshedFrame = -1;
        RestaurantTaskClaim.PlayerTaskChanged += Refresh;
        Refresh();
        if (settings != null && follow != null) SetCompact(true);
    }

    private void OnDisable()
    {
        Active.Remove(this);
        refreshedFrame = -1;
        RestaurantTaskClaim.PlayerTaskChanged -= Refresh;
        RestoreCaptions();
        follow?.SetCompactCustomerPresentation(false);
    }

    private void OnDestroy()
    {
        Active.Remove(this);
        RestaurantTaskClaim.PlayerTaskChanged -= Refresh;
    }

    public void Refresh()
    {
        if (settings != null && follow != null)
        {
            // Existing click handlers still arbitrate claims. Compact controls remain tappable.
            follow.SetTaskFocus(1f, true);
            refreshedFrame = -1;
            return;
        }
        if (focusGroup == null && follow == null)
            return;

        bool competingTask = !MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer &&
                             target != null &&
                             RestaurantTaskClaim.PlayerHasActiveTask &&
                             !RestaurantTaskClaim.IsClaimedByPlayer(target);

        float alpha = competingTask ? Mathf.Clamp(BackgroundAlpha, 0.08f, 0.75f) : 1f;
        if (follow != null) { follow.SetTaskFocus(alpha, !competingTask); return; }
        focusGroup.alpha = alpha;
        focusGroup.interactable = !competingTask;
        focusGroup.blocksRaycasts = !competingTask;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        Active.Clear();
        Candidates.Clear();
        refreshedFrame = -1;
        nextSequence = 0;
    }

    private void LateUpdate()
    {
        if (settings != null) RefreshFineDiningPresentations();
    }

    private static void RefreshFineDiningPresentations()
    {
        if (refreshedFrame == Time.frameCount) return;
        refreshedFrame = Time.frameCount;
        Candidates.Clear();
        foreach (var focus in Active)
        {
            if (focus == null || focus.settings == null || focus.follow == null) continue;
            focus.expanded = false;
            if (focus.passiveStatus || focus.target == null || !focus.follow.IsCustomerPresentationOnScreen) continue;
            focus.priority = focus.ResolvePriority();
            Candidates.Add(focus);
        }
        Candidates.Sort(ComparePriority);
        int expanded = 0;
        foreach (var focus in Candidates)
        {
            if (expanded >= Mathf.Max(1, focus.settings.maxExpandedCustomerBubbles)) break;
            if (focus.priority >= 100) continue;
            focus.expanded = true;
            expanded++;
        }
        foreach (var focus in Active)
            if (focus != null && focus.settings != null && focus.follow != null)
                focus.SetCompact(!focus.expanded);
    }

    private int ResolvePriority()
    {
        var session = MultiplayerSessionManager.Instance;
        if (session != null && session.IsMultiplayerSession && session.RestaurantType == "FineDining")
        {
            var claims = session.GetComponent<MultiplayerTaskClaims>();
            string task = RestaurantTaskClaim.GetMultiplayerTaskId(target);
            if (claims != null && task != null)
            {
                int owner = claims.GetPresentationOwner(task);
                if (owner == session.LocalActorNumber || claims.IsPending(task)) return 0;
                if (owner != 0 || claims.GetBotClaim(task)?.committed == true) return 100;
            }
        }
        if (RestaurantTaskClaim.IsClaimedByPlayer(target)) return 0;
        if (customer != null && customer.isSelected) return 1;
        if (customer != null && customer.FineDiningBubblePatience01 <= settings.urgentCustomerBubblePatience) return 2;
        if (target is FoodTray || target is MoneyPickup ||
            (customer != null && customer.state == CustomerGroup.GroupState.NeedsBill)) return 3;
        return customer != null && customer.HasWineRefillRequest ? 5 : 4;
    }

    private static int ComparePriority(PlayerTaskBubbleFocus a, PlayerTaskBubbleFocus b)
    {
        int result = a.priority.CompareTo(b.priority);
        return result != 0 ? result : a.sequence.CompareTo(b.sequence);
    }

    private void CacheCaptions()
    {
        captions = GetComponentsInChildren<TMP_Text>(true);
        captionEnabled = new bool[captions.Length];
        captionIdentifiers = new bool[captions.Length];
        for (int i = 0; i < captions.Length; i++)
        {
            captionEnabled[i] = captions[i].enabled;
            captionIdentifiers[i] = int.TryParse(captions[i].text.Trim().TrimStart('#'), out _);
        }
        hasIcon = false;
        var button = GetComponentInChildren<Selectable>(true);
        Graphic frame = button != null ? button.targetGraphic : null;
        foreach (var image in GetComponentsInChildren<Image>(true))
        {
            if (image != frame && image.enabled && image.sprite != null && image.gameObject.activeInHierarchy &&
                image.type != Image.Type.Filled &&
                !image.name.StartsWith("Frame", System.StringComparison.OrdinalIgnoreCase) &&
                image.name.IndexOf("Background", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                image.GetComponentInChildren<TMP_Text>(true) == null)
            { hasIcon = true; break; }
        }
    }

    private void SetCompact(bool value)
    {
        follow.SetCompactCustomerPresentation(value);
        if (captions == null || passiveStatus || !hasIcon) return;
        for (int i = 0; i < captions.Length; i++)
        {
            var caption = captions[i];
            if (caption == null) continue;
            caption.enabled = captionEnabled[i] && (!value || captionIdentifiers[i]);
        }
    }

    private void RestoreCaptions()
    {
        if (captions != null && captionEnabled != null)
            for (int i = 0; i < captions.Length; i++)
                if (captions[i] != null) captions[i].enabled = captionEnabled[i];
    }
}
