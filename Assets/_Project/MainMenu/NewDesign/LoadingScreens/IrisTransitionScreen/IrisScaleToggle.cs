using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reusable, persistent menu iris. Loads remain owned by SceneLoader.</summary>
public class IrisScaleToggle : MonoBehaviour
{
    [SerializeField] private RectTransform target; // Existing stencil child, retired on initialization.
    [SerializeField] private float duration = .45f;
    public static IrisScaleToggle Instance { get; private set; }
    private MenuIrisGraphic graphic;
    private Coroutine animation;
    public bool IsOpen => graphic != null && graphic.OpenAmount >= 1f && animation == null;

    public static bool IsMenuScene(string name) => name == "NewMainMenu" || name == "NewGameMenu";

    public static IrisScaleToggle Ensure()
    {
        if (Instance != null) return Instance;
        return new GameObject("Menu Iris", typeof(RectTransform), typeof(Canvas),
            typeof(GraphicRaycaster)).AddComponent<IrisScaleToggle>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null, false);
        transform.localScale = Vector3.one;
        DontDestroyOnLoad(gameObject);
        if (target != null && target.gameObject != gameObject) target.gameObject.SetActive(false);
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler != null) scaler.enabled = false;
        canvas.scaleFactor = 1f;
        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null) raycaster = gameObject.AddComponent<GraphicRaycaster>();
        raycaster.enabled = true;
        var layer = new GameObject("Circular Reveal", typeof(RectTransform), typeof(CanvasRenderer));
        layer.transform.SetParent(transform, false);
        var rect = (RectTransform)layer.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        graphic = layer.AddComponent<MenuIrisGraphic>();
        graphic.color = Color.black;
        SetCovered();
    }

    private IEnumerator Start()
    {
        yield return null;
        while (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) yield return null;
        if (!IsOpen && animation == null) PlayOpen();
    }

    public void SetCovered()
    {
        CancelAnimation();
        graphic.enabled = true;
        graphic.raycastTarget = true;
        graphic.SetAmount(0f);
    }

    public void SuspendForLoading()
    {
        CancelAnimation();
        graphic.enabled = false; // Existing burger/loading UI owns the screen during the load.
    }

    public void RevealImmediately()
    {
        CancelAnimation();
        graphic.SetAmount(1f);
        graphic.raycastTarget = false;
        graphic.enabled = false;
    }

    public void PlayOpen(Action onComplete = null) => Run(1f, onComplete);
    public void PlayCloseThenInvoke(Action onComplete) => Run(0f, onComplete);

    private void Run(float destination, Action onComplete)
    {
        CancelAnimation();
        graphic.enabled = true;
        graphic.raycastTarget = true;
        animation = StartCoroutine(Animate(destination, onComplete));
    }

    private IEnumerator Animate(float destination, Action onComplete)
    {
        float from = graphic.OpenAmount;
        float seconds = Mathf.Clamp(duration, .25f, .65f);
        for (float elapsed = 0; elapsed < seconds; elapsed += Mathf.Min(Time.unscaledDeltaTime, .05f))
        {
            float t = Mathf.SmoothStep(0, 1, elapsed / seconds);
            graphic.SetAmount(Mathf.Lerp(from, destination, t));
            yield return null;
        }
        graphic.SetAmount(destination);
        graphic.raycastTarget = destination < 1f;
        graphic.enabled = destination < 1f;
        animation = null;
        // Closing stays fully black until its caller is ready; never disable on a timer.
        onComplete?.Invoke();
    }

    private void CancelAnimation()
    {
        if (animation != null) StopCoroutine(animation);
        animation = null;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
}

/// <summary>Pixel-space circular aperture; no stretched sprite, stencil, or per-frame material.</summary>
internal sealed class MenuIrisGraphic : MaskableGraphic
{
    public float OpenAmount { get; private set; }
    public void SetAmount(float value) { OpenAmount = Mathf.Clamp01(value); SetVerticesDirty(); }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        if (OpenAmount >= 1f) return;
        Vector2 center = rect.center;
        if (OpenAmount <= 0f)
        {
            mesh.AddVert(new Vector3(rect.xMin, rect.yMin, 0), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMin, rect.yMax, 0), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMax, 0), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMin, 0), color, Vector2.zero);
            mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(0, 2, 3);
            return;
        }
        float diagonal = rect.size.magnitude;
        float radius = (diagonal * .5f + 3f) * OpenAmount;
        const int segments = 160;
        Color transparent = color; transparent.a = 0;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            mesh.AddVert(center + direction * radius, transparent, Vector2.zero);
            mesh.AddVert(center + direction * (radius + 1.5f), color, Vector2.zero);
            mesh.AddVert(center + direction * (diagonal + 4f), color, Vector2.zero);
            if (i == 0) continue;
            int p = (i - 1) * 3, n = i * 3;
            mesh.AddTriangle(p, n, n + 1); mesh.AddTriangle(p, n + 1, p + 1);
            mesh.AddTriangle(p + 1, n + 1, n + 2); mesh.AddTriangle(p + 1, n + 2, p + 2);
        }
    }
}
