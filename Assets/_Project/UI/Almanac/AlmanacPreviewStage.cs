using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>A single, isolated turntable shared by every Almanac entry.</summary>
public sealed class AlmanacPreviewStage : MonoBehaviour
{
    [SerializeField, Range(0, 31)] private int previewLayer = 30;
    [Header("Preview quality")]
    [SerializeField, Range(512, 1536)] private int textureSize = 1024;
    [SerializeField, Range(512, 1024)] private int mobileTextureSize = 768;
    [Header("Framing")]
    [SerializeField, Range(0.65f, 0.95f)] private float characterFrameFill = 0.80f;
    [SerializeField, Range(0.65f, 0.95f)] private float equipmentFrameFill = 0.84f;
    [SerializeField, Range(0.65f, 0.95f)] private float smallObjectFrameFill = 0.90f;
    [Header("Local lighting")]
    [SerializeField, Range(0, 8)] private float keyLightIntensity = 3;
    [SerializeField, Range(0, 8)] private float fillLightIntensity = 2;
    [SerializeField] private Vector3 stagePosition = new Vector3(10000, -10000, 10000);
    [SerializeField, Range(0.1f, 1f)] private float degreesPerPixel = 0.35f;
    [SerializeField, Range(0.05f, 0.5f)] private float introductionBlendSeconds = 0.2f;

    private GameObject stage;
    private Transform turntable;
    private GameObject modelOffset;
    private Camera previewCamera;
    private RenderTexture texture;
    private Light keyLight, fillLight;
    private PlayableGraph animationGraph;
    private AnimationMixerPlayable animationMixer;
    private AnimationClipPlayable idlePlayable;
    private AnimationClipPlayable introductionPlayable;
    private float idleLength;
    private float introductionLength;
    private float animationTime;
    private readonly HashSet<int> warnedEntries = new HashSet<int>();

    public Texture Texture => texture;
    public int EffectiveTextureSize => Mathf.Min(SystemInfo.maxTextureSize,
        Mathf.Clamp(Application.isMobilePlatform ? mobileTextureSize : textureSize, 512, 1536));

    /// <summary>Variant zero is the first configured variant, or the entry's base prefab.</summary>
    public bool Show(AlmanacEntryData entry, int variant = 0)
    {
        ClearVisual();
        if (entry == null || entry.previewKind == AlmanacPreviewKind.Image)
        {
            Hide();
            return false;
        }

        GameObject source = entry.previewPrefab;
        if (entry.variants != null && variant >= 0 && variant < entry.variants.Length &&
            entry.variants[variant] != null && entry.variants[variant].prefab != null)
            source = entry.variants[variant].prefab;
        if (source == null)
        {
            WarnPreview(entry, "no preview prefab is assigned");
            Hide();
            return false;
        }

        EnsureStage();
        turntable.localRotation = Quaternion.Euler(entry.previewEuler);
        turntable.localScale = Vector3.one;
        modelOffset = new GameObject("Visual centering");
        modelOffset.transform.SetParent(turntable, false);
        modelOffset.layer = previewLayer;
        var visual = CreateVisualCopy(source, modelOffset.transform, previewLayer);
        visual.SetActive(true);
        if (entry.previewIgnoredRendererPaths != null)
            foreach (var path in entry.previewIgnoredRendererPaths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                var ignored = visual.transform.Find(path);
                if (ignored != null)
                    foreach (var renderer in ignored.GetComponents<Renderer>()) renderer.enabled = false;
            }

        if (entry.previewKind == AlmanacPreviewKind.Character)
            StartAnimation(visual, entry.idleClip, entry.introductionClip);

        if (!TryGetFramingBounds(visual, out var bounds, out float horizontalRadius))
        {
            WarnPreview(entry, "the assigned prefab has no valid visible mesh bounds");
            Hide();
            return false;
        }

        var framing = entry.previewFraming == AlmanacFraming.Automatic
            ? (entry.previewKind == AlmanacPreviewKind.Character ? AlmanacFraming.Character : AlmanacFraming.Equipment)
            : entry.previewFraming;
        FrameVisual(bounds, horizontalRadius, framing, entry.previewFrameFill);
        previewCamera.enabled = true;
        return true;
    }

    private void WarnPreview(AlmanacEntryData entry, string reason)
    {
        if (warnedEntries.Add(entry.GetInstanceID()))
            Debug.LogWarning($"[Almanac] Cannot show '{entry.entryId}' ({entry.previewPrefab}): {reason}. Using its archive artwork.", this);
    }

    public void Rotate(float pixels)
    {
        if (turntable != null && modelOffset != null)
            turntable.localRotation = Quaternion.AngleAxis(-pixels * degreesPerPixel, Vector3.up) * turntable.localRotation;
    }

    public void Hide()
    {
        ClearVisual();
        if (previewCamera != null)
        {
            previewCamera.enabled = false;
            previewCamera.targetTexture = null;
        }
        if (texture != null)
        {
            texture.Release();
            Dispose(texture);
            texture = null;
        }
        if (stage != null) stage.SetActive(false);
    }

    private void EnsureStage()
    {
        if (stage == null)
        {
            stage = new GameObject("Almanac Preview Stage") { hideFlags = HideFlags.HideAndDontSave };
            stage.layer = previewLayer;
            // Keep generated objects owned by this component even in Edit Mode,
            // where a never-awakened MonoBehaviour may not receive OnDestroy.
            stage.transform.SetParent(transform, true);
            stage.transform.SetPositionAndRotation(stagePosition, Quaternion.identity);
            turntable = new GameObject("Turntable").transform;
            turntable.SetParent(stage.transform, false);
            turntable.gameObject.layer = previewLayer;

            var cameraObject = new GameObject("Preview Camera");
            cameraObject.transform.SetParent(stage.transform, false);
            cameraObject.layer = previewLayer;
            cameraObject.transform.localPosition = new Vector3(0, 0.15f, -4);
            cameraObject.transform.localRotation = Quaternion.LookRotation(-cameraObject.transform.localPosition);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = 1.4f;
            previewCamera.aspect = 1;
            previewCamera.nearClipPlane = 0.1f;
            previewCamera.farClipPlane = 8;
            previewCamera.cullingMask = 1 << previewLayer;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.clear;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = true;
            var cameraData = previewCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = false;
            cameraData.requiresColorTexture = false;
            cameraData.requiresDepthTexture = false;

            // Both URP/Lit and Cozy Toon accept local additional pixel lights.
            // These modest lights lift faces without replacing the menu's sun or recoloring materials.
            keyLight = AddLight("Preview key", new Vector3(-1.5f, 1.8f, -3), new Color(1, 0.97f, 0.91f));
            fillLight = AddLight("Preview fill", new Vector3(1.8f, 0.7f, -2.8f), new Color(0.91f, 0.96f, 1));
        }
        stage.SetActive(true);
        keyLight.intensity = keyLightIntensity;
        fillLight.intensity = fillLightIntensity;
        int size = EffectiveTextureSize;
        var descriptor = new RenderTextureDescriptor(size, size, RenderTextureFormat.ARGB32, 24)
        {
            msaaSamples = 4,
            sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
            useMipMap = false,
            autoGenerateMips = false
        };
        descriptor.msaaSamples = Mathf.Max(1, SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor));
        if (texture != null && (texture.width != size || texture.antiAliasing != descriptor.msaaSamples))
        {
            previewCamera.targetTexture = null;
            texture.Release();
            Dispose(texture);
            texture = null;
        }
        if (texture == null)
        {
            texture = new RenderTexture(descriptor)
            {
                name = "Almanac Preview",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
        }
        previewCamera.targetTexture = texture;
    }

    private Light AddLight(string lightName, Vector3 position, Color color)
    {
        var lightObject = new GameObject(lightName);
        lightObject.transform.SetParent(stage.transform, false);
        lightObject.transform.localPosition = position;
        lightObject.layer = previewLayer;
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.cullingMask = 1 << previewLayer;
        light.color = color;
        light.range = 7;
        light.shadows = LightShadows.None;
        return light;
    }

    private void StartAnimation(GameObject visual, AnimationClip idle, AnimationClip introduction)
    {
        if (idle != null && idle.legacy) idle = null;
        if (introduction != null && (introduction.legacy || LevelOneUIAccessibility.ReducedMotion)) introduction = null;
        if (idle == null && introduction == null) return;
        var requiresHuman = (idle != null && idle.isHumanMotion) || (introduction != null && introduction.isHumanMotion);
        Animator animator = null;
        foreach (var candidate in visual.GetComponentsInChildren<Animator>())
        {
            if (!candidate.enabled || (requiresHuman && (candidate.avatar == null || !candidate.avatar.isValid || !candidate.avatar.isHuman))) continue;
            animator = candidate;
            break;
        }
        if (animator == null) return;

        animationGraph = PlayableGraph.Create("Almanac character preview");
        animationGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        animationMixer = AnimationMixerPlayable.Create(animationGraph, 2);
        var output = AnimationPlayableOutput.Create(animationGraph, "Preview pose", animator);
        output.SetSourcePlayable(animationMixer);
        if (idle != null)
        {
            idlePlayable = MakeClip(idle);
            animationGraph.Connect(idlePlayable, 0, animationMixer, 0);
            idleLength = Mathf.Max(idle.length, 0.01f);
        }
        if (introduction != null)
        {
            introductionPlayable = MakeClip(introduction);
            animationGraph.Connect(introductionPlayable, 0, animationMixer, 1);
            introductionLength = Mathf.Max(introduction.length, 0.01f);
        }
        animationTime = 0;
        animationGraph.Play();
        EvaluateAnimation(0);
    }

    private AnimationClipPlayable MakeClip(AnimationClip clip)
    {
        var playable = AnimationClipPlayable.Create(animationGraph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        return playable;
    }

    private void Update()
    {
        if (animationGraph.IsValid()) EvaluateAnimation(Time.unscaledDeltaTime);
    }

    private void EvaluateAnimation(float deltaTime)
    {
        animationTime += deltaTime;
        if (idlePlayable.IsValid()) idlePlayable.SetTime(animationTime % idleLength);
        float introductionWeight = 0;
        if (introductionPlayable.IsValid())
        {
            introductionPlayable.SetTime(Mathf.Min(animationTime, introductionLength));
            introductionWeight = idlePlayable.IsValid()
                ? 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(Mathf.Max(0, introductionLength - introductionBlendSeconds), introductionLength, animationTime))
                : 1;
        }
        animationMixer.SetInputWeight(0, idlePlayable.IsValid() ? 1 - introductionWeight : 0);
        animationMixer.SetInputWeight(1, introductionWeight);
        animationGraph.Evaluate(0);
    }

    private void ClearVisual()
    {
        if (animationGraph.IsValid()) animationGraph.Destroy();
        idlePlayable = default;
        introductionPlayable = default;
        idleLength = introductionLength = animationTime = 0;
        if (modelOffset != null)
        {
            // Destroy is deferred in Play Mode; deactivate immediately for fast entry switches.
            modelOffset.SetActive(false);
            Dispose(modelOffset);
            modelOffset = null;
        }
    }

    private void OnDisable() => Hide();

    /// <summary>Release the generated hierarchy as well as its GPU/animation resources.</summary>
    public void Release()
    {
        Hide();
        if (stage != null) Dispose(stage);
        stage = null;
        turntable = null;
        previewCamera = null;
        keyLight = fillLight = null;
    }

    private void OnDestroy() => Release();

    private static void Dispose(Object value)
    {
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }

    private void FrameVisual(Bounds bounds, float horizontalRadius, AlmanacFraming framing, float fillOverride)
    {
        // Keep centering outside the Animator hierarchy so animation cannot undo it.
        modelOffset.transform.position -= stage.transform.TransformVector(bounds.center);
        float halfHeight = Mathf.Max(0.001f, bounds.extents.y);
        float scale = 1 / Mathf.Max(halfHeight, horizontalRadius);
        turntable.localScale = Vector3.one * scale;
        float elevation = framing == AlmanacFraming.Character ? 3 : framing == AlmanacFraming.SmallObject ? 18 : 12;
        float radians = elevation * Mathf.Deg2Rad;
        previewCamera.transform.localPosition = new Vector3(0, Mathf.Sin(radians), -Mathf.Cos(radians)) * 4;
        previewCamera.transform.localRotation = Quaternion.LookRotation(-previewCamera.transform.localPosition);
        float verticalExtent = halfHeight * Mathf.Cos(radians) + horizontalRadius * Mathf.Sin(radians);
        float fill = framing == AlmanacFraming.Character ? characterFrameFill
            : framing == AlmanacFraming.SmallObject ? smallObjectFrameFill : equipmentFrameFill;
        if (fillOverride > 0) fill = fillOverride;
        // A cylinder, rather than a sphere, preserves useful portrait height and still fits every yaw.
        previewCamera.orthographicSize = Mathf.Max(verticalExtent, horizontalRadius) * scale / Mathf.Clamp(fill, 0.65f, 0.95f);
    }

    private bool TryGetFramingBounds(GameObject root, out Bounds bounds, out float horizontalRadius)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        var points = new List<Vector3>(renderers.Length * 24);
        var bakedMesh = new Mesh { name = "Almanac framing sample", hideFlags = HideFlags.HideAndDontSave };
        try
        {
            CaptureBounds(renderers, bakedMesh, points);
            if (animationGraph.IsValid())
            {
                // Imported skin bounds can contain a wide T-pose or unrelated animation.
                // Measure only the selected model and its preview clips, once per selection.
                if (idlePlayable.IsValid())
                {
                    idlePlayable.SetTime(idleLength * 0.5f);
                    animationMixer.SetInputWeight(0, 1);
                    animationMixer.SetInputWeight(1, 0);
                    animationGraph.Evaluate(0);
                    CaptureBounds(renderers, bakedMesh, points);
                }
                if (introductionPlayable.IsValid())
                {
                    introductionPlayable.SetTime(introductionLength * 0.5f);
                    animationMixer.SetInputWeight(0, 0);
                    animationMixer.SetInputWeight(1, 1);
                    animationGraph.Evaluate(0);
                    CaptureBounds(renderers, bakedMesh, points);
                }
            }
        }
        finally
        {
            if (animationGraph.IsValid()) EvaluateAnimation(0);
            Dispose(bakedMesh);
        }
        bounds = default;
        horizontalRadius = 0;
        if (points.Count == 0) return false;
        bounds = new Bounds(points[0], Vector3.zero);
        for (int i = 1; i < points.Count; i++) bounds.Encapsulate(points[i]);
        foreach (var point in points)
        {
            var offset = point - bounds.center;
            horizontalRadius = Mathf.Max(horizontalRadius, new Vector2(offset.x, offset.z).magnitude);
        }
        horizontalRadius = Mathf.Max(0.001f, horizontalRadius);
        return bounds.size.sqrMagnitude > 0.00000001f;
    }

    private void CaptureBounds(Renderer[] renderers, Mesh bakedMesh, List<Vector3> points)
    {
        foreach (var renderer in renderers)
        {
            if (!renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy ||
                renderer.GetComponentInParent<Canvas>() != null) continue;
            Bounds localBounds;
            if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
            {
                bakedMesh.Clear();
                // Imported skins can have a 100x renderer transform. The scale-aware bake
                // provides mesh-local vertices for localToWorldMatrix; the default bake
                // counted that scale twice, making customers tiny or effectively invisible.
                skin.BakeMesh(bakedMesh, true);
                bakedMesh.RecalculateBounds();
                localBounds = bakedMesh.bounds;
            }
            else if (renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
                localBounds = filter.sharedMesh.bounds;
            else continue;
            if (!IsFinite(localBounds.center) || !IsFinite(localBounds.extents)) continue;
            var matrix = stage.transform.worldToLocalMatrix * renderer.localToWorldMatrix;
            if (!IsFinite(matrix.MultiplyPoint3x4(localBounds.center)) ||
                !IsFinite(matrix.MultiplyVector(localBounds.extents))) continue;
            for (int corner = 0; corner < 8; corner++)
            {
                var sign = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                points.Add(matrix.MultiplyPoint3x4(localBounds.center + Vector3.Scale(localBounds.extents, sign)));
            }
        }
    }

    /// <summary>
    /// Copies visual components without ever instantiating the source prefab or its behaviours.
    /// Shared meshes/materials are borrowed; copied objects may be safely destroyed by the caller.
    /// </summary>
    public static GameObject CreateVisualCopy(GameObject source, Transform parent, int layer = 30)
    {
        if (source == null) return null;
        var transforms = new Dictionary<Transform, Transform>();
        var root = CopyTransforms(source.transform, parent, layer, transforms);
        root.localPosition = Vector3.zero;
        var lowerLodRenderers = new HashSet<Renderer>();
        foreach (var group in source.GetComponentsInChildren<LODGroup>(true))
        {
            var lods = group.GetLODs();
            for (int i = 1; i < lods.Length; i++)
                foreach (var renderer in lods[i].renderers)
                    if (renderer != null) lowerLodRenderers.Add(renderer);
            if (lods.Length > 0)
                foreach (var renderer in lods[0].renderers)
                    lowerLodRenderers.Remove(renderer);
        }

        foreach (var pair in transforms)
        {
            var original = pair.Key;
            var copy = pair.Value.gameObject;
            // Nameplates and other world-space UI are not part of the model portrait.
            if (original.GetComponentInParent<Canvas>() != null) continue;
            if (original.TryGetComponent<MeshFilter>(out var filter))
                copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            if (original.TryGetComponent<MeshRenderer>(out var meshRenderer) && !lowerLodRenderers.Contains(meshRenderer))
                CopyRenderer(meshRenderer, copy.AddComponent<MeshRenderer>());
            if (original.TryGetComponent<SkinnedMeshRenderer>(out var skin) && !lowerLodRenderers.Contains(skin))
            {
                var result = copy.AddComponent<SkinnedMeshRenderer>();
                result.sharedMesh = skin.sharedMesh;
                var sourceBones = skin.bones;
                var bones = new Transform[sourceBones.Length];
                for (int i = 0; i < bones.Length; i++)
                    if (sourceBones[i] != null) transforms.TryGetValue(sourceBones[i], out bones[i]);
                result.bones = bones;
                if (skin.rootBone != null && transforms.TryGetValue(skin.rootBone, out var rootBone)) result.rootBone = rootBone;
                result.localBounds = skin.localBounds;
                result.quality = skin.quality;
                result.updateWhenOffscreen = true;
                if (skin.sharedMesh != null)
                    for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++) result.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
                CopyRenderer(skin, result);
            }
            if (original.TryGetComponent<Animator>(out var animator))
            {
                var result = copy.AddComponent<Animator>();
                result.avatar = animator.avatar;
                result.runtimeAnimatorController = null;
                result.applyRootMotion = false;
                result.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                result.updateMode = AnimatorUpdateMode.UnscaledTime;
                result.enabled = animator.enabled;
            }
        }
        return root.gameObject;
    }

    private static bool IsFinite(Vector3 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
        !float.IsNaN(value.z) && !float.IsInfinity(value.z);

    private static Transform CopyTransforms(Transform source, Transform parent, int layer, Dictionary<Transform, Transform> map)
    {
        var result = new GameObject(source.name).transform;
        result.gameObject.SetActive(false);
        result.gameObject.layer = layer;
        result.SetParent(parent, false);
        result.localPosition = source.localPosition;
        result.localRotation = source.localRotation;
        result.localScale = source.localScale;
        map.Add(source, result);
        for (int i = 0; i < source.childCount; i++) CopyTransforms(source.GetChild(i), result, layer, map);
        result.gameObject.SetActive(source.gameObject.activeSelf);
        return result;
    }

    private static void CopyRenderer(Renderer source, Renderer target)
    {
        target.sharedMaterials = source.sharedMaterials;
        target.enabled = source.enabled;
        target.forceRenderingOff = source.forceRenderingOff;
        target.shadowCastingMode = ShadowCastingMode.Off;
        target.receiveShadows = false;
        target.lightProbeUsage = LightProbeUsage.Off;
        target.reflectionProbeUsage = ReflectionProbeUsage.Off;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
        if (source.HasPropertyBlock())
        {
            var properties = new MaterialPropertyBlock();
            source.GetPropertyBlock(properties);
            target.SetPropertyBlock(properties);
            for (int i = 0; i < source.sharedMaterials.Length; i++)
            {
                properties.Clear();
                source.GetPropertyBlock(properties, i);
                if (!properties.isEmpty) target.SetPropertyBlock(properties, i);
            }
        }
    }
}
