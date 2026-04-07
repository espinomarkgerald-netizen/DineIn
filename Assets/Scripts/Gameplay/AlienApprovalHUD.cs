using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays the Alien Approval Rating on the lobby shift HUD.
/// Listens to AlienApprovalManager.OnApprovalChanged and updates
/// the label text and slider fill color in real time.
///
/// Attach to the ApprovalHUD GameObject in CanvasMainHUD/AchievementSystem.
/// Wire approvalLabel, approvalSlider, and sliderFill in the Inspector.
/// </summary>
public class AlienApprovalHUD : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI approvalLabel;
    [SerializeField] private Slider approvalSlider;
    [SerializeField] private Image sliderFill;

    [Header("Fill Colors")]
    [SerializeField] private Color highColor   = new Color(0.2f, 0.8f, 0.2f, 1f); // green
    [SerializeField] private Color midColor    = new Color(1.0f, 0.8f, 0.1f, 1f); // yellow
    [SerializeField] private Color lowColor    = new Color(0.9f, 0.2f, 0.2f, 1f); // red

    private void OnEnable()
    {
        if (AlienApprovalManager.Instance == null)
        {
            Debug.LogWarning("[AlienApprovalHUD] AlienApprovalManager.Instance is null. " +
                             "Make sure it is present and initialized before this HUD activates.");
            return;
        }

        AlienApprovalManager.Instance.OnApprovalChanged += UpdateDisplay;
        UpdateDisplay(AlienApprovalManager.Instance.Approval);
    }

    private void OnDisable()
    {
        if (AlienApprovalManager.Instance != null)
            AlienApprovalManager.Instance.OnApprovalChanged -= UpdateDisplay;
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
