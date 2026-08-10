using UnityEngine;
using UnityEngine.UI;

public class AngryBubbleUI : MonoBehaviour
{
    public Image icon;

    public void SetIcon(Sprite sprite)
    {
        if (icon != null) icon.sprite = sprite;
    }
}
