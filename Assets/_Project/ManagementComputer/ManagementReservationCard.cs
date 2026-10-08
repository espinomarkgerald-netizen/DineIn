using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored reservation/request card; only content and state change at runtime.</summary>
public sealed class ManagementReservationCard : MonoBehaviour
{
    public bool useComputerAppStyle;
    public Button button;
    public Image frame;
    public Image preview;
    public Image[] portraits;
    public TMP_Text title;
    public TMP_Text details;
    public TMP_Text status;
    public TMP_Text buttonLabel;
    public Image checkmark;
    public CanvasGroup visual;
    public ManagementItemCardFeedback feedback;
    private bool selected;

    public void SetPortraits(Sprite sprite, int count)
    {
        if (portraits == null) return;
        for (int i = 0; i < portraits.Length; i++)
        {
            portraits[i].sprite = sprite;
            portraits[i].gameObject.SetActive(sprite != null && i < count);
        }
    }

    public void SetState(bool chosen, bool available, Sprite normal, Sprite selectedFrame, Sprite disabled)
    {
        if (useComputerAppStyle) frame.color = chosen ? new Color(.85f,.94f,.89f) : Color.white;
        else frame.sprite = chosen ? selectedFrame : available ? normal : disabled;
        if (visual != null) visual.alpha = available || chosen ? 1f : .65f;
        if (checkmark != null) checkmark.gameObject.SetActive(chosen);
        if (selected != chosen && feedback != null && Application.isPlaying) feedback.SetSelected(chosen);
        selected = chosen;
    }
}
