using UnityEngine;

/// <summary>
/// Fades windows around the active player's work area or across the camera's
/// sightline to the player. Shared asset materials are never modified.
/// </summary>
[DisallowMultipleComponent]
public sealed class CameraWindowTransparency : MonoBehaviour
{
    [Header("Camera detection")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("Viewport distance around screen centre that fades any overlapping part of a window.")]
    [SerializeField, Range(0f, 0.5f)] private float cameraCentreRadius = 0.2f;
    [SerializeField, Range(0f, 0.1f)] private float cameraRestorePadding = 0.03f;
    [Tooltip("Optional override; otherwise follows the active manager.")]
    [SerializeField] private Transform player;
    [SerializeField, Min(0f)] private float playerWorkDistance = 6f;
    [SerializeField, Min(0f)] private float playerFocusHeight = 1.5f;
    [SerializeField, Min(0f)] private float restorePadding = 1f;

    [Header("Opacity")]
    [Tooltip("Alpha while the player is nearby or obscured, in 0-255 units.")]
    [SerializeField, Range(0f, 255f)] private float fadedAlpha = 100f;
    [Tooltip("Alpha used when the window is not obstructing the camera, in 0-255 units.")]
    [SerializeField, Range(0f, 255f)] private float opaqueAlpha = 255f;
    [Tooltip("Seconds to transition between normal and faded opacity.")]
    [SerializeField, Min(0.01f)] private float fadeSeconds = 0.45f;

    private Renderer[] renderers;
    private Material[][] originalMaterials;
    private Material[][] transparentMaterials;
    private bool faded;
    private float currentAlpha;

    private void Awake()
    {
        CacheMaterials();
    }

    private void OnEnable()
    {
        CacheMaterials();
        faded = false;
        currentAlpha = Mathf.Clamp01(opaqueAlpha / 255f);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].sharedMaterials = transparentMaterials[i];
        ApplyOpacity(currentAlpha);
    }

    private void LateUpdate()
    {
        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null || renderers == null || renderers.Length == 0)
            return;

        bool shouldFade = false;
        Transform focus = player != null ? player : ManagerPlayer.Active != null ? ManagerPlayer.Active.transform : null;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if (OverlapsCameraCentre(cameraToUse, renderer.bounds))
            {
                shouldFade = true;
                break;
            }
            if (focus == null) continue;

            Vector3 target = focus.position + Vector3.up * playerFocusHeight;
            Vector3 viewport = cameraToUse.WorldToViewportPoint(target);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                continue;

            Bounds bounds = renderer.bounds;
            float distance = Vector3.Distance(target, bounds.ClosestPoint(target));
            Ray sight = cameraToUse.ViewportPointToRay(viewport);
            float targetDistance = Vector3.Dot(target - sight.origin, sight.direction);
            bool obscuresPlayer = bounds.IntersectRay(sight, out float hitDistance) && hitDistance < targetDistance;
            if (distance <= playerWorkDistance + (faded ? restorePadding : 0f) || obscuresPlayer)
            {
                shouldFade = true;
                break;
            }
        }

        faded = shouldFade;
        float targetAlpha = Mathf.Clamp01((faded ? fadedAlpha : opaqueAlpha) / 255f);
        float speed = Mathf.Abs(opaqueAlpha - fadedAlpha) / 255f / Mathf.Max(0.01f, fadeSeconds);
        float nextAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, speed * Time.unscaledDeltaTime);
        if (!Mathf.Approximately(currentAlpha, nextAlpha))
        {
            currentAlpha = nextAlpha;
            ApplyOpacity(currentAlpha);
        }
    }

    private bool OverlapsCameraCentre(Camera cameraToUse, Bounds bounds)
    {
        // Test the entire projected window, rather than its centre: either end
        // of a long window can obstruct the middle of the view while panning.
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool inFront = false;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                (corner & 1) == 0 ? -1f : 1f,
                (corner & 2) == 0 ? -1f : 1f,
                (corner & 4) == 0 ? -1f : 1f));
            Vector3 projected = cameraToUse.WorldToViewportPoint(point);
            if (projected.z < cameraToUse.nearClipPlane || projected.z > cameraToUse.farClipPlane) continue;
            inFront = true;
            min = Vector2.Min(min, new Vector2(projected.x, projected.y));
            max = Vector2.Max(max, new Vector2(projected.x, projected.y));
        }
        if (!inFront) return false;
        Vector2 nearest = new Vector2(Mathf.Clamp(0.5f, min.x, max.x), Mathf.Clamp(0.5f, min.y, max.y));
        float radius = cameraCentreRadius + (faded ? cameraRestorePadding : 0f);
        return (nearest - new Vector2(0.5f, 0.5f)).sqrMagnitude <= radius * radius;
    }

    private void CacheMaterials()
    {
        if (renderers != null && originalMaterials != null)
            return;

        renderers = GetComponentsInChildren<Renderer>(true);
        originalMaterials = new Material[renderers.Length][];
        transparentMaterials = new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] originals = renderers[i] != null ? renderers[i].sharedMaterials : null;
            originalMaterials[i] = originals ?? new Material[0];
            transparentMaterials[i] = new Material[originalMaterials[i].Length];

            for (int j = 0; j < originalMaterials[i].Length; j++)
            {
                Material source = originalMaterials[i][j];
                if (source == null)
                    continue;

                Material transparent = new Material(source)
                {
                    name = source.name + " (Camera Fade)"
                };
                ConfigureTransparentMaterial(transparent);
                transparentMaterials[i][j] = transparent;
            }
        }
    }

    private void ApplyOpacity(float alpha)
    {

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Material[] materials = transparentMaterials[i];
            for (int j = 0; j < materials.Length; j++)
                ApplyAlpha(materials[j], alpha);
        }
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    private static void ApplyAlpha(Material material, float alpha)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
        {
            Color color = material.GetColor("_BaseColor");
            color.a = alpha;
            material.SetColor("_BaseColor", color);
        }
        else if (material.HasProperty("_Color"))
        {
            Color color = material.GetColor("_Color");
            color.a = alpha;
            material.SetColor("_Color", color);
        }
    }

    private void OnDestroy()
    {
        OnDisable();
        if (transparentMaterials == null)
            return;

        for (int i = 0; i < transparentMaterials.Length; i++)
            for (int j = 0; j < transparentMaterials[i].Length; j++)
                if (transparentMaterials[i][j] != null)
                {
                    if (Application.isPlaying) Destroy(transparentMaterials[i][j]);
                    else DestroyImmediate(transparentMaterials[i][j]);
                }
    }

    private void OnDisable()
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].sharedMaterials = originalMaterials[i];
        faded = false;
    }
}
