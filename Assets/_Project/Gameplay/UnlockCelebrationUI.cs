using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct UnlockPresentation
{
    public readonly string id;
    public readonly string title;
    public readonly string description;
    public readonly string location;
    public readonly Sprite icon;

    public UnlockPresentation(
        string presentationID,
        string presentationTitle,
        string presentationDescription,
        string presentationLocation,
        Sprite presentationIcon)
    {
        id = presentationID;
        title = presentationTitle;
        description = presentationDescription;
        location = presentationLocation;
        icon = presentationIcon;
    }
}

/// <summary>Editable visual shell for one unlock celebration.</summary>
public sealed class UnlockCelebrationUI : MonoBehaviour
{
    [SerializeField] private RectTransform safeAreaContent;
    [SerializeField] private RectTransform panel;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text locationText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField, Min(0f)] private float entranceDuration = 0.28f;
    [SerializeField, Min(0f)] private float safeAreaPadding = 20f;

    private Action dismissed;
    private Rect lastSafeArea = new Rect(-1f, -1f, -1f, -1f);

    public void ConfigureReferences(
        RectTransform configuredSafeArea,
        RectTransform configuredPanel,
        Image configuredIcon,
        TMP_Text configuredTitle,
        TMP_Text configuredDescription,
        TMP_Text configuredLocation,
        Button configuredContinue,
        Button configuredClose,
        CanvasGroup configuredCanvasGroup)
    {
        safeAreaContent = configuredSafeArea;
        panel = configuredPanel;
        icon = configuredIcon;
        titleText = configuredTitle;
        descriptionText = configuredDescription;
        locationText = configuredLocation;
        continueButton = configuredContinue;
        closeButton = configuredClose;
        canvasGroup = configuredCanvasGroup;
    }

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        continueButton?.onClick.AddListener(Dismiss);
        closeButton?.onClick.AddListener(Dismiss);
        if (dismissed == null)
            gameObject.SetActive(false);
    }

    private void Update()
    {
        if (gameObject.activeSelf && lastSafeArea != Screen.safeArea)
            ApplySafeArea();
    }

    public void Show(UnlockPresentation presentation, Action onDismissed)
    {
        dismissed = onDismissed;
        if (icon != null)
        {
            icon.sprite = presentation.icon;
            icon.enabled = presentation.icon != null;
            icon.preserveAspect = true;
        }
        if (titleText != null) titleText.text = presentation.title.ToUpperInvariant();
        if (descriptionText != null) descriptionText.text = presentation.description;
        if (locationText != null) locationText.text = presentation.location.ToUpperInvariant();

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        GameplayUIBlocker.Instance?.SetPanelBlocksGameplay(gameObject, true);
        ApplySafeArea();
        StopAllCoroutines();
        StartCoroutine(AnimateEntrance());
    }

    private System.Collections.IEnumerator AnimateEntrance()
    {
        if (panel == null || entranceDuration <= 0f)
            yield break;
        float elapsed = 0f;
        panel.localScale = Vector3.one * 0.72f;
        while (elapsed < entranceDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / entranceDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float bounce = Mathf.Sin(t * Mathf.PI) * 0.08f;
            panel.localScale = Vector3.one * (Mathf.Lerp(0.72f, 1f, eased) + bounce);
            yield return null;
        }
        panel.localScale = Vector3.one;
    }

    private void Dismiss()
    {
        Action callback = dismissed;
        dismissed = null;
        gameObject.SetActive(false);
        callback?.Invoke();
    }

    private void ApplySafeArea()
    {
        if (safeAreaContent == null || Screen.width <= 0 || Screen.height <= 0)
            return;
        Rect safe = Screen.safeArea;
        safeAreaContent.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        safeAreaContent.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        safeAreaContent.offsetMin = new Vector2(safeAreaPadding, safeAreaPadding);
        safeAreaContent.offsetMax = new Vector2(-safeAreaPadding, -safeAreaPadding);
        lastSafeArea = safe;
    }
}
