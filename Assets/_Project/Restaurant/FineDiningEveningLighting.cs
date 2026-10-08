using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Authored lighting checkpoints, evaluated using the existing service clock.</summary>
public sealed class FineDiningEveningLighting : MonoBehaviour
{
    public GameDayManager clock;
    public Light sun;
    public Light[] interiorLights;
    public Light[] exteriorLights;
    [Range(17,22)] public float exteriorOnHour = 18f;
    public Color sunset = new Color(1f, .81f, .62f);
    public Color dusk = new Color(.64f, .69f, .9f);
    public Color night = new Color(.42f, .51f, .75f);
    public AnimationCurve sunIntensity = new AnimationCurve(new Keyframe(17, 1f), new Keyframe(18.5f, .65f), new Keyframe(20, .35f));
    public AnimationCurve ambientIntensity = new AnimationCurve(new Keyframe(17, 1f), new Keyframe(18.5f, .85f), new Keyframe(20, .7f));
    [Tooltip("Multiply authored indoor intensities as the exterior gets darker.")]
    public AnimationCurve interiorIntensity = new AnimationCurve(new Keyframe(17, 1f), new Keyframe(18.5f, 1.5f), new Keyframe(20, 2.2f));
    [Header("Uniform indoor fill")]
    public Bounds[] interiorZones;
    public Color afternoonFill = new Color(.45f, .34f, .23f);
    public Color nightFill = new Color(.85f, .78f, .66f);
    public GroupSpawner customerSpawner;
    [Header("Late afternoon tree light")]
    public Renderer[] afternoonDetails;
    [Tooltip("Scene-specific shadow coverage. Shared quality assets are not modified.")]
    public RenderPipelineAsset sunsetPipeline;
    public AnimationCurve sunElevation = new AnimationCurve(new Keyframe(17, 26), new Keyframe(18.5f, 8));
    public float sunsetAzimuth = 270f;
    public Color sunsetAmbient = new Color(.53f, .43f, .32f);
    [Tooltip("Cool exterior ambient after sunset; indoor fill remains independently authored.")]
    public Color nightAmbient = new Color(.12f, .19f, .32f);
    private AmbientMode originalAmbientMode;
    private Color originalAmbientLight;
    public Renderer[] sunsetRoofShadows;
    private RenderPipelineAsset originalQualityPipeline;
    private Quaternion originalSunRotation;
    private LightShadows originalSunShadows;
    private float originalShadowStrength;
    private static readonly int SunDirection = Shader.PropertyToID("_SunDirection");
    public AnimationCurve afternoonDetailOpacity = new AnimationCurve(new Keyframe(17, 1), new Keyframe(18, .35f), new Keyframe(18.5f, 0));
    private MaterialPropertyBlock afternoonBlock;
    private static readonly int AfternoonFade = Shader.PropertyToID("_HourFade");
    private sealed class FillTarget
    {
        public Renderer renderer;
        public LightProbeUsage originalUsage;
        public MaterialPropertyBlock originalBlock;
        public bool moving, applied;
    }
    private readonly List<FillTarget> fillTargets = new List<FillTarget>();
    private MaterialPropertyBlock fillBlock;
    private SphericalHarmonicsL2[] harmonics;
    private Vector4[] occlusion;
    private Color lastFill;
    private bool fillInitialized;
    private float[] originalInteriorIntensities;
    private bool[] originalInteriorEnabled, originalExteriorEnabled;
    private Color originalSunColor;
    private float originalSunIntensity, originalAmbient;
    private float nextUpdate;
    private void OnEnable()
    {
        if (sun != null) { originalSunColor = sun.color; originalSunIntensity = sun.intensity; }
        originalAmbient = RenderSettings.ambientIntensity;
        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbientLight = RenderSettings.ambientLight;
        originalQualityPipeline = QualitySettings.renderPipeline;
        if (sun != null) { originalSunRotation = sun.transform.rotation; originalSunShadows = sun.shadows; originalShadowStrength = sun.shadowStrength; }
        originalInteriorIntensities = new float[interiorLights?.Length ?? 0];
        originalInteriorEnabled = new bool[originalInteriorIntensities.Length];
        originalExteriorEnabled = new bool[exteriorLights?.Length ?? 0];
        for (int i = 0; i < originalInteriorIntensities.Length; i++)
            if (interiorLights[i] != null) { originalInteriorIntensities[i] = interiorLights[i].intensity; originalInteriorEnabled[i] = interiorLights[i].enabled; }
        for (int i = 0; i < originalExteriorEnabled.Length; i++)
            if (exteriorLights[i] != null) originalExteriorEnabled[i] = exteriorLights[i].enabled;
        fillTargets.Clear();
        fillBlock = new MaterialPropertyBlock();
        afternoonBlock = new MaterialPropertyBlock();
        harmonics = new SphericalHarmonicsL2[1];
        occlusion = new[] { Vector4.one };
        fillInitialized = false;
        if (interiorZones != null && interiorZones.Length > 0)
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (renderer.gameObject.scene == gameObject.scene)
                    RegisterRenderer(renderer);
        if (customerSpawner != null) customerSpawner.GroupCreated += RegisterGroup;
        Apply();
    }
    private bool IsInside(Vector3 position)
    {
        if (interiorZones != null)
            foreach (var zone in interiorZones) if (zone.Contains(position)) return true;
        return false;
    }
    private void RegisterRenderer(Renderer renderer)
    {
        if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) return;
        bool moving = renderer.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null;
        if (!moving && (renderer.bounds.size.x > 120f || !IsInside(renderer.bounds.center))) return;
        foreach (var target in fillTargets) if (target.renderer == renderer) return;
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        fillTargets.Add(new FillTarget { renderer = renderer, originalUsage = renderer.lightProbeUsage,
            originalBlock = block, moving = moving });
    }
    private void RegisterGroup(CustomerGroup group, bool takeout)
    {
        foreach (var renderer in group.GetComponentsInChildren<Renderer>(true)) RegisterRenderer(renderer);
    }
    private void ApplyFill(float hour)
    {
        Color color = Color.Lerp(afternoonFill, nightFill, Mathf.InverseLerp(17, 19, hour));
        bool changed = !fillInitialized || Mathf.Abs(color.r-lastFill.r) + Mathf.Abs(color.g-lastFill.g) + Mathf.Abs(color.b-lastFill.b) > .005f;
        if (changed)
        {
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(color);
            harmonics[0] = sh;
            lastFill = color;
            fillInitialized = true;
        }
        for (int i = fillTargets.Count - 1; i >= 0; i--)
        {
            var target = fillTargets[i];
            if (target.renderer == null) { fillTargets.RemoveAt(i); continue; }
            bool inside = !target.moving || IsInside(target.renderer.bounds.center);
            if (!inside && target.applied)
            {
                target.renderer.SetPropertyBlock(target.originalBlock);
                target.renderer.lightProbeUsage = target.originalUsage;
                target.applied = false;
            }
            else if (inside && (changed || !target.applied))
            {
                target.renderer.GetPropertyBlock(fillBlock);
                fillBlock.CopySHCoefficientArraysFrom(harmonics);
                fillBlock.CopyProbeOcclusionArrayFrom(occlusion);
                target.renderer.lightProbeUsage = LightProbeUsage.CustomProvided;
                target.renderer.SetPropertyBlock(fillBlock);
                target.applied = true;
            }
        }
    }
    private void Update()
    {
        if (Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + .25f;
        Apply();
    }
    private void Apply()
    {
        float hour = clock != null ? clock.CurrentGameHour : 17f;
        bool goldenHour = sunsetPipeline != null && hour < 18.75f;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(sunsetAmbient, nightAmbient, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(17.5f, 19f, hour)));
        var desiredPipeline = goldenHour ? sunsetPipeline : originalQualityPipeline;
        if (QualitySettings.renderPipeline != desiredPipeline) QualitySettings.renderPipeline = desiredPipeline;
        if (sunsetRoofShadows != null)
            foreach (var roof in sunsetRoofShadows) if (roof != null) roof.enabled = goldenHour;
        if (sun != null)
        {
            sun.color = hour < 18.5f ? Color.Lerp(sunset, dusk, Mathf.InverseLerp(17, 18.5f, hour)) : Color.Lerp(dusk, night, Mathf.InverseLerp(18.5f, 20, hour));
            sun.intensity = sunIntensity.Evaluate(hour);
            sun.transform.rotation = goldenHour ? Quaternion.Euler(sunElevation.Evaluate(hour), sunsetAzimuth, 0) : originalSunRotation;
            sun.shadows = goldenHour ? LightShadows.Soft : originalSunShadows;
            sun.shadowStrength = goldenHour ? .78f * (1f - Mathf.InverseLerp(18.35f, 18.75f, hour)) : originalShadowStrength;
        }
        ApplyFill(hour);
        if (afternoonDetails != null)
            foreach (var detail in afternoonDetails)
                if (detail != null)
                {
                    float fade = afternoonDetailOpacity.Evaluate(hour);
                    detail.enabled = fade > .001f;
                    detail.GetPropertyBlock(afternoonBlock);
                    afternoonBlock.SetFloat(AfternoonFade, fade);
                    if (sun != null) afternoonBlock.SetVector(SunDirection, sun.transform.forward);
                    detail.SetPropertyBlock(afternoonBlock);
                }
        RenderSettings.ambientIntensity = ambientIntensity.Evaluate(hour);
        if(exteriorLights != null) foreach(var light in exteriorLights)if(light!=null)light.enabled=hour>=exteriorOnHour;
        for (int i = 0; i < (originalInteriorIntensities?.Length ?? 0); i++)
            if (interiorLights[i] != null)
            {
                interiorLights[i].enabled = true;
                interiorLights[i].intensity = originalInteriorIntensities[i] * interiorIntensity.Evaluate(hour);
            }
    }
    private void OnDisable()
    {
        if (customerSpawner != null) customerSpawner.GroupCreated -= RegisterGroup;
        foreach (var target in fillTargets)
            if (target.renderer != null && target.applied)
            {
                target.renderer.SetPropertyBlock(target.originalBlock);
                target.renderer.lightProbeUsage = target.originalUsage;
            }
        fillTargets.Clear();
        if (afternoonDetails != null)
            foreach (var detail in afternoonDetails)
                if (detail != null) { detail.enabled = true; detail.SetPropertyBlock(null); }
        if (QualitySettings.renderPipeline == sunsetPipeline) QualitySettings.renderPipeline = originalQualityPipeline;
        if (sunsetRoofShadows != null)
            foreach (var roof in sunsetRoofShadows) if (roof != null) roof.enabled = false;
        if (sun != null) { sun.color = originalSunColor; sun.intensity = originalSunIntensity;
            sun.transform.rotation = originalSunRotation; sun.shadows = originalSunShadows; sun.shadowStrength = originalShadowStrength; }
        RenderSettings.ambientIntensity = originalAmbient;
        RenderSettings.ambientMode = originalAmbientMode;
        RenderSettings.ambientLight = originalAmbientLight;
        for (int i = 0; i < (originalInteriorIntensities?.Length ?? 0); i++)
            if (interiorLights[i] != null) { interiorLights[i].intensity = originalInteriorIntensities[i]; interiorLights[i].enabled = originalInteriorEnabled[i]; }
        for (int i = 0; i < (originalExteriorEnabled?.Length ?? 0); i++)
            if (exteriorLights[i] != null) exteriorLights[i].enabled = originalExteriorEnabled[i];
    }
}
