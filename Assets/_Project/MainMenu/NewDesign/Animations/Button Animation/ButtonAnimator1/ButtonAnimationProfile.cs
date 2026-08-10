using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(menuName = "UI/Button Animation Profile")]
public class ButtonAnimationProfile : ScriptableObject
{
    [Header("Scaling")]
    public Vector3 hoverScale = new Vector3(1.05f, 1.05f, 1f);
    public Vector3 pressScale = new Vector3(0.95f, 0.95f, 1f);
    
    [Header("Timing & Easing")]
    public float duration = 0.2f;
    public Ease hoverEase = Ease.OutBack;
    public Ease pressEase = Ease.OutQuad;
    
    [Header("Color")]
    public Color hoverColor = Color.white;
    public Color pressColor = new Color(0.9f, 0.9f, 0.9f);
}