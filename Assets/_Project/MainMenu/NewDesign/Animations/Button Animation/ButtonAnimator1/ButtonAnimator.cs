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
        _originalColor = _image.color;
    }

    private void Animate(Vector3 targetScale, Color targetColor, Ease ease)
    {
        if (!_button.interactable) return;
        
        transform.DOKill();
        _image.DOKill();
        
        transform.DOScale(targetScale, profile.duration).SetEase(ease);
        _image.DOColor(targetColor, profile.duration).SetEase(ease);
    }

    public void OnPointerEnter(PointerEventData eventData) => 
        Animate(profile.hoverScale, profile.hoverColor, profile.hoverEase);

    public void OnPointerExit(PointerEventData eventData) => 
        Animate(_originalScale, _originalColor, profile.hoverEase);

    public void OnPointerDown(PointerEventData eventData) => 
        Animate(profile.pressScale, profile.pressColor, profile.pressEase);

    public void OnPointerUp(PointerEventData eventData)
    {
        if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, eventData.position))
            Animate(profile.hoverScale, profile.hoverColor, profile.hoverEase);
        else
            Animate(_originalScale, _originalColor, profile.hoverEase);
    }
}