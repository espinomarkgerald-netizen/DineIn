using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Editable world-space call marker that follows the complaining customer.</summary>
public sealed class ManagerComplaintMarker : MonoBehaviour
{
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private RectTransform visual;
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text symbolText;

    private Transform target;
    private Vector3 worldOffset;
    private Action clicked;
    private float pulseSpeed = 3.6f;
    private float pulseScale = 1.12f;

    public Vector3 WorldPosition => target != null
        ? target.position + worldOffset
        : transform.position;

    public void Bind(
        Transform followTarget,
        Vector3 offset,
        float configuredPulseSpeed,
        float configuredPulseScale,
        Action onClicked)
    {
        target = followTarget;
        worldOffset = offset;
        pulseSpeed = Mathf.Max(0.1f, configuredPulseSpeed);
        pulseScale = Mathf.Max(1f, configuredPulseScale);
        clicked = onClicked;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClick);
        }

        gameObject.SetActive(target != null);
    }

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClick);
        }

        if (symbolText != null)
            symbolText.text = "!";
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            gameObject.SetActive(false);
            return;
        }

        transform.position = WorldPosition;
        Camera camera = Camera.main;
        if (camera != null)
        {
            transform.rotation = camera.transform.rotation;
            if (worldCanvas != null)
                worldCanvas.worldCamera = camera;
        }

        if (visual != null)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            visual.localScale = Vector3.one * Mathf.Lerp(1f, pulseScale, pulse);
        }
    }

    public bool IsVisibleFrom(Camera camera)
    {
        if (camera == null || target == null)
            return false;

        Vector3 viewport = camera.WorldToViewportPoint(WorldPosition);
        return viewport.z > 0f &&
               viewport.x >= 0.03f && viewport.x <= 0.97f &&
               viewport.y >= 0.05f && viewport.y <= 0.95f;
    }

    public void SetWorldMarkerVisible(bool visible)
    {
        if (visual != null && visual.gameObject.activeSelf != visible)
            visual.gameObject.SetActive(visible);
    }

    private void HandleClick() => clicked?.Invoke();
}
