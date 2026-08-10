using UnityEngine;
using UnityEngine.UI;

public class IconBubbleUI : MonoBehaviour
{
    [SerializeField] private Image icon;

    public void SetIcon(Sprite sprite)
    {
        if (icon != null) icon.sprite = sprite;
    }
}