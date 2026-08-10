using UnityEngine;
using TMPro;
using DG.Tweening;

public class ButtonTextAnimator : MonoBehaviour
{
    [SerializeField] private Color hoverColor = Color.yellow;
    [SerializeField] private float duration = 0.2f;
    
    private TMP_Text _text;
    private Color _originalColor;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        _originalColor = _text.color;
    }

    public void OnPointerEnter()
    {
        _text.DOKill(); // Prevent flicker
        _text.DOColor(hoverColor, duration);
        _text.rectTransform.DOPunchScale(new Vector3(0.1f, 0.1f, 0), duration, 0);
    }

    public void OnPointerExit()
    {
        _text.DOKill(); // Prevent flicker
        _text.DOColor(_originalColor, duration);
    }
}