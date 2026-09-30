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
    [SerializeField, Range(128, 1024)] private int textureSize = 512;
    [SerializeField] private Vector3 stagePosition = new Vector3(10000, -10000, 10000);
    [SerializeField, Range(0.1f, 1f)] private float degreesPerPixel = 0.35f;
    [SerializeField, Range(0.05f, 0.5f)] private float introductionBlendSeconds = 0.2f;

    private GameObject stage;
    private Transform turntable;
    private GameObject modelOffset;
    private Camera previewCamera;
    private RenderTexture texture;
    private PlayableGraph animationGraph;
    private AnimationMixerPlayable animationMixer;
    private AnimationClipPlayable idlePlayable;
    private AnimationClipPlayable introductionPlayable;
    private float idleLength;
    private float introductionLength;
    private float animationTime;

    public Texture Texture => texture;

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

        if (entry.previewKind == AlmanacPreviewKind.Character)
            StartAnimation(visual, entry.idleClip, entry.introductionClip);

        if (!TryGetBounds(visual, out var bounds))
        {
            Hide();
            return false;
        }

        // Center above the Animator root, so root animation cannot undo the framing.
        // A bounding sphere also keeps wide models in frame while the player rotates them.
        modelOffset.transform.position += turntable.position - bounds.center;
        turntable.localScale = Vector3.one * (1.18f / Mathf.Max(0.001f, bounds.extents.magnitude));
        previewCamera.enabled = true;
        return true;
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

            // Local lights cannot replace the menu's directional main light in URP.
            AddLight("Preview key", new Vector3(-2, 2, -2), new Color(1, 0.92f, 0.8f), 7);
            AddLight("Preview fill", new Vector3(2, 0.5f, -1), new Color(0.78f, 0.88f, 1), 4);
        }
        stage.SetActive(true);
        if (texture == null)
        {
            texture = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32)
            {
                name = "Almanac Preview",
                hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
        }
        previewCamera.targetTexture = texture;
    }

    private void AddLight(string lightName, Vector3 position, Color color, float intensity)
    {
        var lightObject = new GameObject(lightName);
        lightObject.transform.SetParent(stage.transform, false);
        lightObject.transform.localPosition = position;
        lightObject.layer = previewLayer;
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.cullingMask = 1 << previewLayer;
        light.color = color;
        light.intensity = intensity;
        light.range = 7;
        light.shadows = LightShadows.None;
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
    }

    private void OnDestroy() => Release();

    private static void Dispose(Object value)
    {
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }

    private static bool TryGetBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found;
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
