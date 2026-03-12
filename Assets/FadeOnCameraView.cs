using UnityEngine;

public class FadeOnCameraView : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Renderer targetRenderer;

    [Header("Fade")]
    [SerializeField] private float visibleAlpha = 0.25f;
    [SerializeField] private float hiddenAlpha = 1f;
    [SerializeField] private float fadeSpeed = 5f;

    private Material runtimeMaterial;
    private float currentAlpha;
    private float targetAlpha;

    private Camera mainCam;

    void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        runtimeMaterial = targetRenderer.material;
        mainCam = Camera.main;

        currentAlpha = hiddenAlpha;
    }

    void Update()
    {
        if (mainCam == null || targetRenderer == null) return;

        Vector3 viewportPos = mainCam.WorldToViewportPoint(targetRenderer.bounds.center);

        bool inView =
            viewportPos.z > 0 &&
            viewportPos.x > 0 && viewportPos.x < 1 &&
            viewportPos.y > 0 && viewportPos.y < 1;

        targetAlpha = inView ? visibleAlpha : hiddenAlpha;

        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        SetAlpha(currentAlpha);
    }

    void SetAlpha(float a)
    {
        Color c = runtimeMaterial.GetColor("_BaseColor");
        c.a = a;
        runtimeMaterial.SetColor("_BaseColor", c);
    }
}