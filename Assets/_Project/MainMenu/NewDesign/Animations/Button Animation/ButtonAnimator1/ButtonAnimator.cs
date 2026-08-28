using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

[RequireComponent(typeof(Button), typeof(CanvasGroup))]
public class ButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private ButtonAnimationProfile profile;
    
    private Button _button;
    private CanvasGroup _canvasGroup;
    private Vector3 _originalScale;
    private Color _originalColor;
    private Image _image;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _image = GetComponent<Image>();
        _originalScale = transform.localScale;
        _originalColor = _image != null ? _image.color : Color.white;
    }

    private void Animate(Vector3 targetScale, Color targetColor, Ease ease)
    {
        if (_button == null || !_button.interactable)
            return;
        
        transform.DOKill();
        if (_image != null)
            _image.DOKill();
        
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

    public void OnPointerEnter(PointerEventData eventData) => Animate(
        profile != null ? profile.hoverScale : Vector3.Scale(_originalScale, new Vector3(1.04f, 1.04f, 1f)),
        profile != null ? profile.hoverColor : Color.Lerp(_originalColor, Color.white, 0.12f),
        profile != null ? profile.hoverEase : Ease.OutBack);

    public void OnPointerExit(PointerEventData eventData) => Animate(
        _originalScale,
        _originalColor,
        profile != null ? profile.hoverEase : Ease.OutBack);

    public void OnPointerDown(PointerEventData eventData) => Animate(
        profile != null ? profile.pressScale : Vector3.Scale(_originalScale, new Vector3(0.94f, 0.94f, 1f)),
        profile != null ? profile.pressColor : new Color(
            _originalColor.r * 0.9f, _originalColor.g * 0.9f, _originalColor.b * 0.9f, _originalColor.a),
        profile != null ? profile.pressEase : Ease.OutQuad);

    public void OnPointerUp(PointerEventData eventData)
    {
        if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, eventData.position))
            OnPointerEnter(eventData);
        else
            OnPointerExit(eventData);
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

    private void ScanButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button != null && button.GetComponent<ButtonAnimator>() == null)
                button.gameObject.AddComponent<ButtonAnimator>();
        }
    }
}
