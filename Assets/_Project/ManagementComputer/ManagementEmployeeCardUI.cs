using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Editable portrait-style card used by the HR department rails.</summary>
public sealed class ManagementEmployeeCardUI : MonoBehaviour
{
    [SerializeField] private Image accent;
    [SerializeField] private Image avatarBackground;
    [SerializeField] private TMP_Text avatarInitial;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text starsText;
    [SerializeField] private Image[] ratingStars;
    [SerializeField] private Sprite filledStarSprite;
    [SerializeField] private Sprite emptyStarSprite;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private TMP_Text proText;
    [SerializeField] private TMP_Text conText;
    [SerializeField] private TMP_Text salaryText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button primaryButton;
    [SerializeField] private TMP_Text primaryLabel;
    [SerializeField] private Button secondaryButton;
    [SerializeField] private TMP_Text secondaryLabel;
    [SerializeField] private Button emptySlotButton;

    [Header("Applicant Attention (Editable)")]
    [SerializeField, Range(1f, 1.2f)] private float attentionBopScale = 1.06f;
    [SerializeField, Min(0.1f)] private float attentionBopDuration = 0.45f;
    [SerializeField, Range(1, 3)] private int attentionBopCount = 2;

    [Header("Action Feedback (Editable)")]
    [SerializeField, Min(0.05f)] private float positiveFeedbackDuration = 0.12f;
    [SerializeField, Range(1f, 1.12f)] private float positiveFeedbackScale = 1.04f;
    [SerializeField, Min(0.08f)] private float declineDuration = 0.18f;
    [SerializeField, Range(0.8f, 1f)] private float declineEndScale = 0.92f;

    private Coroutine attentionRoutine;
    private Coroutine actionRoutine;
    private CanvasGroup canvasGroup;

    public EmployeeData Employee { get; private set; }
    public Button PrimaryButton => primaryButton;
    public Button SecondaryButton => secondaryButton;

    public void ConfigureReferences(
        Image configuredAccent,
        Image configuredAvatarBackground,
        TMP_Text configuredAvatarInitial,
        TMP_Text configuredName,
        TMP_Text configuredRole,
        TMP_Text configuredStars,
        TMP_Text configuredStats,
        TMP_Text configuredPro,
        TMP_Text configuredCon,
        TMP_Text configuredSalary,
        TMP_Text configuredStatus,
        Button configuredPrimary,
        TMP_Text configuredPrimaryLabel,
        Button configuredSecondary,
        TMP_Text configuredSecondaryLabel,
        Button configuredEmptySlotButton = null)
    {
        accent = configuredAccent;
        avatarBackground = configuredAvatarBackground;
        avatarInitial = configuredAvatarInitial;
        nameText = configuredName;
        roleText = configuredRole;
        starsText = configuredStars;
        statsText = configuredStats;
        proText = configuredPro;
        conText = configuredCon;
        salaryText = configuredSalary;
        statusText = configuredStatus;
        primaryButton = configuredPrimary;
        primaryLabel = configuredPrimaryLabel;
        secondaryButton = configuredSecondary;
        secondaryLabel = configuredSecondaryLabel;
        emptySlotButton = configuredEmptySlotButton;
    }

#if UNITY_EDITOR
    public void ConfigureRatingIcons(
        Image[] configuredStars,
        Sprite configuredFilledStar,
        Sprite configuredEmptyStar)
    {
        ratingStars = configuredStars;
        filledStarSprite = configuredFilledStar;
        emptyStarSprite = configuredEmptyStar;
    }
#endif

    public void Bind(
        EmployeeData employee,
        SalaryConfig salaryConfig,
        string status,
        string primaryAction,
        UnityAction onPrimary,
        bool primaryEnabled,
        string secondaryAction,
        UnityAction onSecondary,
        bool secondaryEnabled,
        UnityAction onEmptySlot = null)
    {
        Employee = employee;
        string employeeName = employee != null ? employee.employeeName : "Empty Slot";
        EmployeeRole role = employee != null ? employee.role : EmployeeRole.Host;

        if (nameText != null) nameText.text = employeeName;
        if (roleText != null) roleText.text = employee != null ? role.ToString().ToUpperInvariant() : "AVAILABLE";
        if (avatarInitial != null)
            avatarInitial.text = employee != null && !string.IsNullOrWhiteSpace(employeeName)
                ? employeeName.Substring(0, 1).ToUpperInvariant()
                : "+";
        ApplyRating(employee != null ? Mathf.Clamp(employee.stars, 1, 5) : 0);
        if (statsText != null)
            statsText.text = employee != null
                ? $"SPEED  {employee.speed}%\nACCURACY  {employee.accuracy}%\nRELIABILITY  {employee.reliability}%" +
                  $"\nPERFORMANCE  {employee.recentPerformance}%\nROLE XP  {employee.roleExperience}"
                : "Hire an applicant to fill this role.";
        if (proText != null) proText.text = employee != null
            ? "+ " + employee.GetTraitLabel() + " • " + employee.GetPrimaryPro()
            : "+ Open position";
        if (conText != null) conText.text = employee != null ? "− " + employee.GetPrimaryCon() : string.Empty;
        if (salaryText != null)
            salaryText.text = employee != null && salaryConfig != null
                ? "₱" + employee.GetSalary(salaryConfig) + " / DAY"
                : "NO PAYROLL";
        if (statusText != null) statusText.text = status ?? string.Empty;

        Color roleColor = GetRoleColor(role);
        if (accent != null) accent.color = employee != null ? roleColor : new Color(0.52f, 0.58f, 0.65f);
        if (avatarBackground != null) avatarBackground.color = employee != null
            ? Color.Lerp(roleColor, Color.white, 0.52f)
            : new Color(0.82f, 0.85f, 0.89f);

        ConfigureButton(primaryButton, primaryLabel, primaryAction, onPrimary, primaryEnabled);
        ConfigureButton(secondaryButton, secondaryLabel, secondaryAction, onSecondary, secondaryEnabled);
        ConfigureEmptySlotButton(employee == null ? onEmptySlot : null);
    }

    public void PlayAttentionBop()
    {
        if (!isActiveAndEnabled || actionRoutine != null)
            return;

        if (attentionRoutine != null)
            StopCoroutine(attentionRoutine);
        attentionRoutine = StartCoroutine(AttentionBopRoutine());
    }

    public void PlayPositiveFeedback(Func<bool> action, UnityAction onSuccess)
    {
        StartActionFeedback(PositiveFeedbackRoutine(action, onSuccess));
    }

    public void PlayDeclineRemoval(Func<bool> action, UnityAction onSuccess)
    {
        StartActionFeedback(DeclineRemovalRoutine(action, onSuccess));
    }

    private void StartActionFeedback(IEnumerator routine)
    {
        if (!isActiveAndEnabled || actionRoutine != null)
            return;

        if (attentionRoutine != null)
        {
            StopCoroutine(attentionRoutine);
            attentionRoutine = null;
        }

        canvasGroup = canvasGroup != null ? canvasGroup : GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.interactable = false;
        transform.localScale = Vector3.one;
        actionRoutine = StartCoroutine(routine);
    }

    private IEnumerator PositiveFeedbackRoutine(Func<bool> action, UnityAction onSuccess)
    {
        float duration = LevelOneUIAccessibility.ReducedMotion
            ? 0f
            : Mathf.Max(0.05f, positiveFeedbackDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(progress * Mathf.PI);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, positiveFeedbackScale, pulse);
            yield return null;
        }

        RestoreActionVisuals();
        bool succeeded = action != null && action.Invoke();
        actionRoutine = null;
        if (succeeded)
            onSuccess?.Invoke();
    }

    private IEnumerator DeclineRemovalRoutine(Func<bool> action, UnityAction onSuccess)
    {
        float duration = LevelOneUIAccessibility.ReducedMotion
            ? 0f
            : Mathf.Max(0.08f, declineDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            if (canvasGroup != null)
                canvasGroup.alpha = 1f - progress;
            transform.localScale = Vector3.one * Mathf.Lerp(1f, declineEndScale, progress);
            yield return null;
        }

        bool succeeded = action != null && action.Invoke();
        actionRoutine = null;
        if (succeeded)
        {
            onSuccess?.Invoke();
            yield break;
        }

        RestoreActionVisuals();
    }

    private void RestoreActionVisuals()
    {
        transform.localScale = Vector3.one;
        if (canvasGroup == null)
            return;
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
    }

    private void ConfigureEmptySlotButton(UnityAction onEmptySlot)
    {
        if (emptySlotButton == null && avatarBackground != null && onEmptySlot != null)
        {
            emptySlotButton = avatarBackground.GetComponent<Button>();
            if (emptySlotButton == null)
                emptySlotButton = avatarBackground.gameObject.AddComponent<Button>();
            emptySlotButton.targetGraphic = avatarBackground;
            emptySlotButton.transition = Selectable.Transition.None;
        }

        if (emptySlotButton == null)
            return;

        emptySlotButton.onClick.RemoveAllListeners();
        if (onEmptySlot != null)
            emptySlotButton.onClick.AddListener(onEmptySlot);
        emptySlotButton.interactable = onEmptySlot != null;
    }

    private IEnumerator AttentionBopRoutine()
    {
        transform.localScale = Vector3.one;
        if (LevelOneUIAccessibility.ReducedMotion)
        {
            attentionRoutine = null;
            yield break;
        }

        float duration = Mathf.Max(0.1f, attentionBopDuration);
        int count = Mathf.Clamp(attentionBopCount, 1, 3);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float wave = Mathf.Abs(Mathf.Sin(progress * Mathf.PI * count));
            float eased = wave * wave * (3f - 2f * wave);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, attentionBopScale, eased);
            yield return null;
        }

        transform.localScale = Vector3.one;
        attentionRoutine = null;
    }

    private void OnDisable()
    {
        if (attentionRoutine != null)
        {
            StopCoroutine(attentionRoutine);
            attentionRoutine = null;
        }
        if (actionRoutine != null)
        {
            StopCoroutine(actionRoutine);
            actionRoutine = null;
        }
        RestoreActionVisuals();
    }

    private static void ConfigureButton(
        Button button,
        TMP_Text label,
        string action,
        UnityAction callback,
        bool enabled)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        if (callback != null)
            button.onClick.AddListener(callback);
        button.interactable = enabled && callback != null;
        button.gameObject.SetActive(!string.IsNullOrWhiteSpace(action));
        if (label != null) label.text = action ?? string.Empty;
    }

    private void ApplyRating(int stars)
    {
        bool useIcons = ratingStars != null && ratingStars.Length > 0 &&
                        filledStarSprite != null && emptyStarSprite != null;
        if (starsText != null)
        {
            starsText.gameObject.SetActive(!useIcons);
            if (!useIcons)
            {
                starsText.text = stars > 0
                    ? new string('★', stars) + new string('☆', Mathf.Max(0, 5 - stars))
                    : "☆☆☆☆☆";
            }
        }

        if (!useIcons)
            return;

        for (int i = 0; i < ratingStars.Length; i++)
        {
            Image star = ratingStars[i];
            if (star == null)
                continue;
            star.sprite = i < stars ? filledStarSprite : emptyStarSprite;
            star.enabled = star.sprite != null;
            star.preserveAspect = true;
        }
    }

    private static Color GetRoleColor(EmployeeRole role)
    {
        switch (role)
        {
            case EmployeeRole.Host: return new Color(0.20f, 0.64f, 0.88f);
            case EmployeeRole.Waiter: return new Color(0.25f, 0.72f, 0.56f);
            case EmployeeRole.Cashier: return new Color(0.95f, 0.62f, 0.20f);
            case EmployeeRole.Busser: return new Color(0.62f, 0.48f, 0.86f);
            case EmployeeRole.Chef: return new Color(0.91f, 0.34f, 0.28f);
            case EmployeeRole.Barista: return new Color(0.56f, 0.34f, 0.20f);
            default: return new Color(0.22f, 0.55f, 0.78f);
        }
    }
}
