using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using DG.Tweening;

[RequireComponent(typeof(Button), typeof(CanvasGroup))]
public class ButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ICanvasRaycastFilter
{
    [SerializeField] private ButtonAnimationProfile profile;
    
    private Button _button;
    private CanvasGroup _canvasGroup;
    private Vector3 _originalScale;
    private Color _originalColor;
    private Image _image;
    private RectTransform _rect;
    private bool _initialized;

    private void Awake()
    {
        _button = GetComponent<Button>();
        GameplayButtonSound.Ensure(_button);
        _canvasGroup = GetComponent<CanvasGroup>();
        _image = GetComponent<Image>();
        _rect = (RectTransform)transform;
        _originalScale = transform.localScale;
        _originalColor = _image != null ? _image.color : Color.white;
        _initialized = true;
    }

    public void PlayConfirmation(float strength = .035f)
    {
        if (!_initialized || !isActiveAndEnabled) return;
        transform.DOKill();
        if (_image != null) { _image.DOKill(); _image.color = Color.Lerp(_originalColor, Color.white, .12f); _image.DOColor(_originalColor, .18f).SetUpdate(true); }
        if (LevelOneUIAccessibility.ReducedMotion) { transform.localScale = _originalScale; return; }
        transform.DOScale(_originalScale * (1f + Mathf.Clamp(strength, 0f, .05f)), .08f).SetUpdate(true)
            .OnComplete(() => transform.DOScale(_originalScale, .1f).SetUpdate(true));
    }

    private void Animate(Vector3 targetScale, Color targetColor, Ease ease)
    {
        if (_button == null || !_button.interactable)
            return;
        
        transform.DOKill();
        if (_image != null)
            _image.DOKill();
        
        if (LevelOneUIAccessibility.ReducedMotion)
        {
            transform.localScale = _originalScale;
            if (_image != null) _image.color = targetColor;
            return;
        }
        float duration = profile != null ? profile.duration : 0.12f;
        transform.DOScale(targetScale, duration).SetEase(ease).SetUpdate(true);
        if (_image != null)
            _image.DOColor(targetColor, duration).SetEase(ease).SetUpdate(true);
    }

    /// <summary>
    /// Lets prefab-backed runtime styling replace the color captured in Awake.
    /// Without this, a button authored white can flash back to white after a click.
    /// </summary>
    public void SetBaseColor(Color color)
    {
        _originalColor = color;
        if (_image != null)
        {
            _image.DOKill();
            _image.color = color;
        }
    }

    public static bool IsTouchPointer(PointerEventData eventData) =>
        eventData is ExtendedPointerEventData extended
            ? extended.pointerType == UIPointerType.Touch
            : eventData != null && eventData.pointerId >= 0;

    // Touch has press/release feedback, not a persistent mouse hover. Input System
    // mouse IDs are also positive, so use its explicit pointer type when available.
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData == null || IsTouchPointer(eventData) ||
            !IsRaycastLocationValid(eventData.position, eventData.enterEventCamera)) return;
        Animate(
            profile != null ? profile.hoverScale : Vector3.Scale(_originalScale, new Vector3(1.04f, 1.04f, 1f)),
            profile != null ? profile.hoverColor : Color.Lerp(_originalColor, Color.white, 0.12f),
            profile != null ? profile.hoverEase : Ease.OutBack);
    }

    public void OnPointerExit(PointerEventData eventData) => Animate(
        _originalScale,
        _originalColor,
        profile != null ? profile.hoverEase : Ease.OutBack);

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left ||
            !IsRaycastLocationValid(eventData.position, eventData.pressEventCamera)) return;
        Animate(
            profile != null ? profile.pressScale : Vector3.Scale(_originalScale, new Vector3(0.94f, 0.94f, 1f)),
            profile != null ? profile.pressColor : new Color(
                _originalColor.r * 0.9f, _originalColor.g * 0.9f, _originalColor.b * 0.9f, _originalColor.a),
            profile != null ? profile.pressEase : Ease.OutQuad);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData != null && !IsTouchPointer(eventData) &&
            IsRaycastLocationValid(eventData.position, eventData.pressEventCamera))
            OnPointerEnter(eventData);
        else
            OnPointerExit(eventData);
    }

    // GraphicRaycaster also applies this filter to child labels. A bounce must
    // never enlarge the interactive rectangle into another button or empty space.
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        // A graphics-free grouping Button is not an interactive rectangle. Its
        // children own their hitboxes (e.g. restaurant Play/Prev/Next).
        if (!_initialized || !isActiveAndEnabled || _button.targetGraphic == null) return true;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rect, screenPoint, eventCamera, out Vector2 localPoint)) return false;
        if (Mathf.Approximately(_originalScale.x, 0f) || Mathf.Approximately(_originalScale.y, 0f)) return false;
        Vector3 scale = transform.localScale;
        localPoint.x *= scale.x / _originalScale.x;
        localPoint.y *= scale.y / _originalScale.y;
        return _rect.rect.Contains(localPoint);
    }

    private void OnDisable()
    {
        transform.DOKill();
        if (_image != null)
            _image.DOKill();

        transform.localScale = _originalScale;
        if (_image != null)
            _image.color = _originalColor;
    }

    private void OnDestroy()
    {
        transform.DOKill();
        if (_image != null)
            _image.DOKill();
    }
}

/// <summary>
/// Applies the same ButtonAnimator used by the new main menu to buttons that
/// are authored or spawned later by gameplay UI.
/// </summary>
internal sealed class InGameButtonAnimationInstaller : MonoBehaviour
{
    private float nextScanTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForLoadedScene()
    {
        InGameButtonAnimationInstaller installer =
            FindFirstObjectByType<InGameButtonAnimationInstaller>();
        if (installer == null)
        {
            GameObject runner = new GameObject("In-Game Button Animation Installer");
            installer = runner.AddComponent<InGameButtonAnimationInstaller>();
            DontDestroyOnLoad(runner);
        }

        installer.ScanButtons();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScanTime)
            return;

        nextScanTime = Time.unscaledTime + 1f;
        ScanButtons();
    }

    private void ScanButtons() => EnsureButtons(null);

    internal static void EnsureButtons(Transform root)
    {
        var effects = FindObjectsByType<UI.Effects.UIButtonEffects>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Button[] buttons = root != null ? root.GetComponentsInChildren<Button>(true) : FindObjectsByType<Button>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            GameplayButtonSound.Ensure(button);
            if (button == null || button.GetComponent<ButtonAnimator>() != null ||
                button.GetComponent<UISubtlePressFeedback>() != null || button.GetComponent<Animator>() != null) continue;
            bool authored = false;
            foreach (var effect in effects) if (effect.ControlsButton(button)) { authored = true; break; }
            if (!authored) button.gameObject.AddComponent<ButtonAnimator>();
        }
    }
}
