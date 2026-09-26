using System;
using UnityEngine;

/// <summary>Maps authored anchors into the device safe area without changing offsets or animation scales.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class UIScreenSafeArea : MonoBehaviour
{
    [Serializable] public struct Target
    {
        public RectTransform rect;
        public Vector2 anchorMin, anchorMax;
    }
    [Tooltip("Authored anchor baselines. Exclude full-screen dimmers/backgrounds. Recapture after layout edits.")]
    [SerializeField] private Target[] targets = Array.Empty<Target>();
    [SerializeField] private bool applyInEditor = true;
    private Rect previousSafe;
    private Vector2Int previousSize;
    private void OnEnable() => Apply(true);
    private void LateUpdate() => Apply(false);
    private void Apply(bool force)
    {
        if (!gameObject.scene.IsValid() || (!Application.isPlaying && !applyInEditor) || Screen.width <= 0 || Screen.height <= 0) return;
        Rect safe = Screen.safeArea;
        var size = new Vector2Int(Screen.width, Screen.height);
        if (!force && safe == previousSafe && size == previousSize) return;
        previousSafe = safe; previousSize = size;
        Vector2 min = new Vector2(safe.xMin / size.x, safe.yMin / size.y);
        Vector2 span = new Vector2(safe.width / size.x, safe.height / size.y);
        foreach (var target in targets)
        {
            if (target.rect == null) continue;
            target.rect.anchorMin = min + Vector2.Scale(target.anchorMin, span);
            target.rect.anchorMax = min + Vector2.Scale(target.anchorMax, span);
        }
    }
#if UNITY_EDITOR
    public void Capture(RectTransform[] rects)
    {
        targets = new Target[rects.Length];
        for (int i = 0; i < rects.Length; i++)
            targets[i] = new Target { rect = rects[i], anchorMin = rects[i].anchorMin, anchorMax = rects[i].anchorMax };
    }
#endif
}
