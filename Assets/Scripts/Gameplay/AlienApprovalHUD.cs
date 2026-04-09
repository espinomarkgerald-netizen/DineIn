using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Displays the Alien Approval Rating on a HUD (lobby or office).
/// Safe for multiple HUDs and avoids race conditions with the singleton.
/// </summary>
public class AlienApprovalHUD : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI approvalLabel;
    [SerializeField] private Slider approvalSlider;
    [SerializeField] private Image sliderFill;

    [Header("Fill Colors")]
    [SerializeField] private Color highColor = new Color(0.2f, 0.8f, 0.2f, 1f); // green
    [SerializeField] private Color midColor  = new Color(1.0f, 0.8f, 0.1f, 1f); // yellow
    [SerializeField] private Color lowColor  = new Color(0.9f, 0.2f, 0.2f, 1f); // red

    private void Start()
    {
        if (AlienApprovalManager.Instance == null)
        {
            Debug.LogWarning("[AlienApprovalHUD] AlienApprovalManager.Instance is null.");
            return;
        }
        else
        {
            StartCoroutine(WaitForManager());
        }

        AlienApprovalManager.Instance.OnApprovalChanged += UpdateDisplay;
        UpdateDisplay(AlienApprovalManager.Instance.Approval);
    }

    private void OnDisable()
    {
        if (AlienApprovalManager.Instance != null)
            AlienApprovalManager.Instance.OnApprovalChanged -= UpdateDisplay;
    }

    private IEnumerator WaitForManager()
    {
        while (AlienApprovalManager.Instance == null)
            yield return null;

        Subscribe();
    }

    private void Subscribe()
    {
        AlienApprovalManager.Instance.OnApprovalChanged += UpdateDisplay;
        UpdateDisplay(AlienApprovalManager.Instance.Approval);
    }

    private void UpdateDisplay(int approval)
    {
        if (approvalLabel != null)
            approvalLabel.text = $"Alien Approval\n{approval}/100";

        if (approvalSlider != null)
            approvalSlider.value = approval / 100f;

        if (sliderFill != null)
        {
            sliderFill.color = approval >= 60 ? highColor
                             : approval >= 40 ? midColor
                             : lowColor;
        }
    }
}