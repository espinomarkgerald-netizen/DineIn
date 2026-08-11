using UnityEngine;
using TMPro;

public class PaymentPopupUI : MonoBehaviour
{
    public TMP_Text text;
    public float autoCloseSeconds = 1.5f;

    public void Show(int amount, int orderNumber)
    {
        if (text != null)
            text.text = $"PAID ₱{amount}\nOrder #{orderNumber}";

        UIFollowWorldPoint follow = GetComponent<UIFollowWorldPoint>();
        if (follow != null && follow.target == null)
            follow.enabled = false;

        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group != null)
            group.alpha = 1f;

        // Delayed Destroy works even if a parent canvas is temporarily inactive.
        Destroy(gameObject, autoCloseSeconds);
    }
}
