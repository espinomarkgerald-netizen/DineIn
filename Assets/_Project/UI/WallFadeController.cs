using System.Collections.Generic;
using UnityEngine;

public class FakeWallFadeSwapController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;

    [Header("Detection")]
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float headHeight = 1.5f;
    [SerializeField] private float cameraHitIgnoreDistance = 0.75f;

    [Header("Materials")]
    [SerializeField] private Material opaqueMat;
    [SerializeField] private Material transparentMat;

    [Header("Fade")]
    [Range(0f, 1f)]
    [SerializeField] private float fadedAlpha = 0.55f;
    [SerializeField] private float fadeSpeed = 7f;

    [Tooltip("How long a wall must NOT be hit before it restores (prevents flicker).")]
    [SerializeField] private float restoreDelay = 0.15f;

    private class WallState
    {
        public float currentAlpha = 1f;
        public bool usingTransparent = false;
        public float lastHitTime = 0f;
    }

    private readonly Dictionary<Renderer, WallState> tracked = new();
    private RaycastHit[] hitBuffer = new RaycastHit[32];
    private MaterialPropertyBlock mpb;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    void Awake() => mpb = new MaterialPropertyBlock();

    void Update()
    {
        if (!player || !opaqueMat || !transparentMat) return;

        Vector3 start = transform.position;
        Vector3 end = player.position + Vector3.up * headHeight;
        Vector3 dir = end - start;

        float dist = dir.magnitude;
        if (dist <= 0.01f) return;

        int hitCount = Physics.RaycastNonAlloc(start, dir.normalized, hitBuffer, dist, wallLayer);
        if (hitCount == hitBuffer.Length)
        {
            hitBuffer = new RaycastHit[hitBuffer.Length * 2];
            hitCount = Physics.RaycastNonAlloc(start, dir.normalized, hitBuffer, dist, wallLayer);
        }

        // Mark blockers this frame
        for (int i = 0; i < hitCount; i++)
        {
            var hit = hitBuffer[i];
            if (hit.distance < cameraHitIgnoreDistance) continue;

            var rend = hit.collider.GetComponentInChildren<Renderer>();
            if (!rend) continue;

            if (!tracked.TryGetValue(rend, out var state))
            {
                state = new WallState();
                tracked.Add(rend, state);
            }

            state.lastHitTime = Time.time;

            EnsureTransparent(rend, state);

            state.currentAlpha = Mathf.Lerp(state.currentAlpha, fadedAlpha, Time.deltaTime * fadeSpeed);
            ApplyAlpha(rend, state.currentAlpha);
        }

        // Restore anything not hit recently
        var keys = new List<Renderer>(tracked.Keys);
        foreach (var rend in keys)
        {
            if (!rend)
            {
                tracked.Remove(rend);
                continue;
            }

            var state = tracked[rend];

            if (Time.time - state.lastHitTime < restoreDelay)
                continue;

            state.currentAlpha = Mathf.Lerp(state.currentAlpha, 1f, Time.deltaTime * fadeSpeed);
            ApplyAlpha(rend, state.currentAlpha);

            if (Mathf.Abs(state.currentAlpha - 1f) < 0.02f)
            {
                RestoreOpaque(rend, state);
                tracked.Remove(rend);
            }
        }
    }

    private void EnsureTransparent(Renderer rend, WallState state)
    {
        if (state.usingTransparent) return;

        rend.sharedMaterials = new Material[] { transparentMat };

        state.usingTransparent = true;
        state.currentAlpha = 1f;

        ApplyAlpha(rend, 1f);
    }

    private void RestoreOpaque(Renderer rend, WallState state)
    {
        rend.sharedMaterials = new Material[] { opaqueMat };

        mpb.Clear();
        rend.SetPropertyBlock(mpb);

        state.usingTransparent = false;
        state.currentAlpha = 1f;
    }

    private void ApplyAlpha(Renderer rend, float a)
    {
        rend.GetPropertyBlock(mpb);

        var mat = rend.sharedMaterial;
        Color c = Color.white;

        if (mat != null && mat.HasProperty(BaseColorID))
            c = mat.GetColor(BaseColorID);
        else if (mat != null && mat.HasProperty(ColorID))
            c = mat.GetColor(ColorID);

        c.a = a;

        if (mat != null && mat.HasProperty(BaseColorID))
            mpb.SetColor(BaseColorID, c);
        else
            mpb.SetColor(ColorID, c);

        rend.SetPropertyBlock(mpb);
    }
}
