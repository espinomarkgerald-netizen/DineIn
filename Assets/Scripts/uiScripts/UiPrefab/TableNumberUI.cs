using TMPro;
using UnityEngine;

public class TableNumberUI : MonoBehaviour
{
    public TMP_Text numberText;

    public void SetNumber(int number)
    {
        if (numberText != null)
            numberText.text = number.ToString();
    }
}
