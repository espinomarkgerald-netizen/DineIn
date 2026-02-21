using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HoldToCleanUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private Slider radialFill;

    [Header("Follow")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.15f, 0f);
    [SerializeField] private float screenYOffset = 30f;

    [Header("Refs")]
    [SerializeField] private Camera cam;
    [SerializeField] private RectTransform uiRoot;

    private CleanableEvent target;
    private float t;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (uiRoot == null) uiRoot = transform as RectTransform;
        HideInternal();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (cam == null) cam = Camera.main;
        if (cam != null && uiRoot != null)
        {
            Vector3 screenPos = cam.WorldToScreenPoint(target.transform.position + worldOffset);
            screenPos.y += screenYOffset;
            uiRoot.position = screenPos;
        }
    }

    public void Begin(CleanableEvent cleanable)
    {
        target = cleanable;
        t = 0f;

        if (label != null) label.text = "Clean";
        if (radialFill != null) radialFill.value = 0f;

        SetVisible(true);
    }

    public void TickHold(float deltaTime, bool holding)
    {
        if (target == null) { HideInternal(); return; }

        if (holding) t += deltaTime;
        else t = 0f;

        float dur = Mathf.Max(0.05f, target.holdToCleanSeconds);
        float pct = Mathf.Clamp01(t / dur);

        if (radialFill != null) radialFill.value = pct;

        if (pct >= 1f)
        {
            target.Clean();
            HideInternal();
        }
    }

    public void Cancel()
    {
        HideInternal();
    }

    private void HideInternal()
    {
        target = null;
        t = 0f;

        if (radialFill != null) radialFill.value = 0f;
        SetVisible(false);
    }

    private void SetVisible(bool on)
    {
        if (uiRoot != null) uiRoot.gameObject.SetActive(on);
        else gameObject.SetActive(on);
    }
}
