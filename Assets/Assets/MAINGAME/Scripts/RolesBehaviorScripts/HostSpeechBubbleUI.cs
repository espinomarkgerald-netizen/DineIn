using TMPro;
using UnityEngine;

public class HostSpeechBubbleUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    public void SetText(string message)
    {
        if (label != null)
            label.text = message;
    }
}