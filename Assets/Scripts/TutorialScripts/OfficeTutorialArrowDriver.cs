using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Positions and activates a <see cref="BoothAssignArrowUI"/> to point at each
/// tutorial button in the OfficeTutorial scene.
///
/// Because all four targets are UI RectTransforms inside a Screen Space – Overlay
/// canvas (not world-space objects), <see cref="UIFollowWorldPoint"/> cannot be
/// used directly. This driver sets the arrow's RectTransform position to match
/// the target button's screen-space position each frame so it tracks correctly.
/// </summary>
public class OfficeTutorialArrowDriver : MonoBehaviour
{
    [Header("Arrow UI")]
    [SerializeField] private RectTransform arrowRect;

    [Header("Button targets")]
    [SerializeField] private RectTransform hrButtonRect;
    [SerializeField] private RectTransform restockButtonRect;
    [SerializeField] private RectTransform equipmentButtonRect;
    [SerializeField] private RectTransform recipeButtonRect;

    [Header("Offset above the button (in screen pixels)")]
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 80f);

    private RectTransform currentTarget;
    private bool isTracking;

    private void Awake()
    {
        SetArrowVisible(false);
    }

    private void LateUpdate()
    {
        if (!isTracking || currentTarget == null || arrowRect == null)
            return;

        // Both arrow and targets share the same Screen Space – Overlay canvas,
        // so their RectTransform.position values are already in screen space.
        Vector3 targetScreenPos = currentTarget.position;
        arrowRect.position = targetScreenPos + new Vector3(screenOffset.x, screenOffset.y, 0f);
    }

    /// <summary>
    /// Called by <see cref="OfficeTutorialManager.SetPhase"/> on every phase change.
    /// Points the arrow at the button relevant to the given phase; hides it otherwise.
    /// </summary>
    public void OnPhaseEntered(OfficeTutorialManager.OfficeTutorialPhase phase)
    {
        switch (phase)
        {
            case OfficeTutorialManager.OfficeTutorialPhase.HRButton:
                TrackTarget(hrButtonRect);
                break;

            case OfficeTutorialManager.OfficeTutorialPhase.RestockButton:
                TrackTarget(restockButtonRect);
                break;

            case OfficeTutorialManager.OfficeTutorialPhase.EquipmentButton:
                TrackTarget(equipmentButtonRect);
                break;

            case OfficeTutorialManager.OfficeTutorialPhase.RecipeButton:
                TrackTarget(recipeButtonRect);
                break;

            default:
                // Inside panels or complete – hide the arrow.
                Hide();
                break;
        }
    }

    // ─── Internal ─────────────────────────────────────────────────────────────

    private void TrackTarget(RectTransform target)
    {
        if (target == null)
        {
            Hide();
            return;
        }

        currentTarget = target;
        isTracking = true;
        SetArrowVisible(true);
    }

    /// <summary>Hides the arrow and stops tracking.</summary>
    public void Hide()
    {
        isTracking = false;
        currentTarget = null;
        SetArrowVisible(false);
    }

    private void SetArrowVisible(bool visible)
    {
        if (arrowRect != null)
            arrowRect.gameObject.SetActive(visible);
    }
}
