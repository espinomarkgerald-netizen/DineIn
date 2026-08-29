using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Mobile-only accessibility pass. It leaves authored reference resolutions intact,
/// makes screen-space canvases fit inside unusual mobile aspect ratios, and gives
/// small controls a larger invisible pointer target without stretching their artwork.
///
/// Reference resolution is part of the coordinate system used by every child
/// RectTransform. Changing it without converting the complete hierarchy enlarges or
/// shrinks fixed-size UI. In particular, converting a 1920 x 1080 canvas to
/// 600 x 337.5 made the Level 1 UI roughly 3.2 times larger on Android.
/// </summary>
public sealed class MobileUIAccessibility : MonoBehaviour
{
    private const string RuntimeName = "[Mobile UI Accessibility]";
    private const string TouchAreaName = "[Mobile Touch Area]";
    private const float ScanInterval = 0.75f;
    private static MobileUIAccessibility instance;
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!Application.isMobilePlatform || instance != null)
            return;

        GameObject root = new GameObject(RuntimeName);
        instance = root.AddComponent<MobileUIAccessibility>();
        DontDestroyOnLoad(root);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyToLoadedUI();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan)
            return;

        nextScan = Time.unscaledTime + ScanInterval;
        ApplyToLoadedUI();
    }

    private void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        nextScan = 0f;
        ApplyToLoadedUI();
    }

    private static void ApplyToLoadedUI()
    {
        CanvasScaler[] scalers = FindObjectsByType<CanvasScaler>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < scalers.Length; i++)
            ConfigureCanvasForMobile(scalers[i]);

        Button[] buttons = FindObjectsByType<Button>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
            EnsureTouchArea(buttons[i]);
    }

    /// <summary>
    /// Applies the mobile aspect-ratio policy without changing the canvas coordinate
    /// system. Public so the editor regression validator can exercise the exact
    /// runtime code used by Android builds.
    /// </summary>
    public static void ConfigureCanvasForMobile(CanvasScaler scaler)
    {
        Canvas canvas = scaler != null ? scaler.GetComponent<Canvas>() : null;
        if (scaler == null || canvas == null || canvas.renderMode == RenderMode.WorldSpace ||
            scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            return;

        // Expand selects the smaller of the width/height scale factors. The complete
        // authored reference rectangle therefore remains visible on extra-wide phones
        // such as 20:9 devices instead of losing its top or bottom edges.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
    }

    private static void EnsureTouchArea(Button button)
    {
        if (button == null || button.transform.Find(TouchAreaName) != null)
            return;

        RectTransform buttonRect = button.transform as RectTransform;
        if (buttonRect == null)
            return;

        Rect rect = buttonRect.rect;
        float extraX = Mathf.Max(0f, 56f - rect.width) * 0.5f;
        float extraY = Mathf.Max(0f, 56f - rect.height) * 0.5f;
        if (extraX <= 0f && extraY <= 0f)
            return;

        GameObject area = new GameObject(TouchAreaName, typeof(RectTransform), typeof(Image));
        RectTransform areaRect = area.GetComponent<RectTransform>();
        areaRect.SetParent(button.transform, false);
        areaRect.anchorMin = Vector2.zero;
        areaRect.anchorMax = Vector2.one;
        areaRect.offsetMin = new Vector2(-extraX, -extraY);
        areaRect.offsetMax = new Vector2(extraX, extraY);
        areaRect.SetAsFirstSibling();

        Image image = area.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
    }
}
