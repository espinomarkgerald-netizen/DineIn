using System.Collections;
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

        StartCoroutine(AutoClose());
    }

    private IEnumerator AutoClose()
    {
        yield return new WaitForSeconds(autoCloseSeconds);
        Destroy(gameObject);
    }
    
}