using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AlmanacCardUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text subTitleText;
    [SerializeField] private TMP_Text descriptionText;

    public void Bind(AlmanacEntryData data)
    {
        if (data == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (iconImage != null)
        {
            iconImage.sprite = data.icon;
            iconImage.enabled = data.icon != null;
        }

        if (nameText != null)
            nameText.text = data.entryName;

        if (subTitleText != null)
            subTitleText.text = data.subTitle;

        if (descriptionText != null)
            descriptionText.text = data.description;
    }
}