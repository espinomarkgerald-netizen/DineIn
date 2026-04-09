using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class StartBlockedPanel : MonoBehaviour
{
    public static StartBlockedPanel Instance;

    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI messageText;

    private void Start()
    {
        Show(new List<string> { "UI TEST", "If you see this, it's working" });
    }

    private void Awake()
    {
        Instance = this;
        panel.SetActive(false);
    }

    public void Show(List<string> issues)
    {
        panel.SetActive(true);

        messageText.text = "Before starting, you must:\n\n";

        foreach (var issue in issues)
            messageText.text += "• " + issue + "\n";
    }

    public void Hide()
    {
        panel.SetActive(false);
    }
}