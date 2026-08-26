using UnityEngine;

/// <summary>
/// Dims a competing action control while the player is committed elsewhere.
/// It never changes the represented gameplay state and never touches world-UI
/// scale, so hidden/dimmed controls can safely return when the claim clears.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerTaskBubbleFocus : MonoBehaviour
{
    public static float BackgroundAlpha { get; set; } = 0.28f;

    private CanvasGroup focusGroup;
    private UnityEngine.Object target;

    public static PlayerTaskBubbleFocus Bind(
        GameObject visualRoot,
        UnityEngine.Object taskTarget)
    {
        if (visualRoot == null)
            return null;

        PlayerTaskBubbleFocus focus =
            visualRoot.GetComponent<PlayerTaskBubbleFocus>();
        if (focus == null)
            focus = visualRoot.AddComponent<PlayerTaskBubbleFocus>();

        focus.target = taskTarget;
        focus.Refresh();
        return focus;
    }

    private void Awake()
    {
        // Always use a dedicated group. Other systems may already own a
        // CanvasGroup for world-space visibility or animation.
        focusGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        RestaurantTaskClaim.PlayerTaskChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        RestaurantTaskClaim.PlayerTaskChanged -= Refresh;
    }

    private void OnDestroy()
    {
        RestaurantTaskClaim.PlayerTaskChanged -= Refresh;
    }

    public void Refresh()
    {
        if (focusGroup == null)
            return;

        bool competingTask = target != null &&
                             RestaurantTaskClaim.PlayerHasActiveTask &&
                             !RestaurantTaskClaim.IsClaimedByPlayer(target);

        focusGroup.alpha = competingTask
            ? Mathf.Clamp(BackgroundAlpha, 0.08f, 0.75f)
            : 1f;
        focusGroup.interactable = !competingTask;
        focusGroup.blocksRaycasts = !competingTask;
    }
}
