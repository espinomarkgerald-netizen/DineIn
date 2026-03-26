using UnityEngine;
using UnityEngine.UI;

public class LinePatienceUI : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private Image fillImage;

    [Header("Colors")]
    [SerializeField] private Color greenColor = new Color(0.25f, 0.85f, 0.35f);
    [SerializeField] private Color yellowColor = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color redColor = new Color(0.95f, 0.2f, 0.2f);

    [Header("Thresholds")]
    [SerializeField] private float yellowThreshold = 0.5f;
    [SerializeField] private float redThreshold = 0.2f;

    private UIFollowWorldPoint follow;

    private void Awake()
    {
        if (slider == null)
            slider = GetComponentInChildren<Slider>(true);

        if (fillImage == null && slider != null && slider.fillRect != null)
            fillImage = slider.fillRect.GetComponent<Image>();

        if (follow == null)
            follow = GetComponent<UIFollowWorldPoint>();

        if (follow == null)
            follow = GetComponentInChildren<UIFollowWorldPoint>(true);

        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
        }

        ApplyColor(1f);
    }

    public void Init(Transform target, Vector3 worldOffset, Camera cam)
    {
        if (follow == null)
            follow = GetComponent<UIFollowWorldPoint>();

        if (follow == null)
            follow = GetComponentInChildren<UIFollowWorldPoint>(true);

        if (follow != null)
            follow.Init(target, worldOffset, cam);
        else
            Debug.LogWarning("[LinePatienceUI] UIFollowWorldPoint not found on " + name);
    }

    public void SetProgress(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);

        if (slider != null)
            slider.value = normalized;

        ApplyColor(normalized);
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    private void ApplyColor(float normalized)
    {
        if (fillImage == null)
            return;

        if (normalized <= redThreshold)
            fillImage.color = redColor;
        else if (normalized <= yellowThreshold)
            fillImage.color = yellowColor;
        else
            fillImage.color = greenColor;
    }
}