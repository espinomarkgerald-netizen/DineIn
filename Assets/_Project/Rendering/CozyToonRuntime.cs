using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DineIn.Rendering
{
    [ExecuteAlways]
    [DefaultExecutionOrder(-10000)]
    public sealed class CozyToonRuntime : MonoBehaviour
    {
        private const string ToonShaderName = "Dine In/Cozy Toon";

        private static readonly int ShadowColorId = Shader.PropertyToID("_CozyToonShadowColor");
        private static readonly int ShadowTintStrengthId = Shader.PropertyToID("_CozyToonShadowTintStrength");
        private static readonly int ShadowBrightnessId = Shader.PropertyToID("_CozyToonShadowBrightness");
        private static readonly int DeepShadowBrightnessId = Shader.PropertyToID("_CozyToonDeepShadowBrightness");
        private static readonly int DeepShadowThresholdId = Shader.PropertyToID("_CozyToonDeepShadowThreshold");
        private static readonly int LightTintId = Shader.PropertyToID("_CozyToonLightTint");
        private static readonly int LightTintStrengthId = Shader.PropertyToID("_CozyToonLightTintStrength");
        private static readonly int ShadowThresholdId = Shader.PropertyToID("_CozyToonShadowThreshold");
        private static readonly int HighlightThresholdId = Shader.PropertyToID("_CozyToonHighlightThreshold");
        private static readonly int BandSoftnessId = Shader.PropertyToID("_CozyToonBandSoftness");
        private static readonly int AmbientStrengthId = Shader.PropertyToID("_CozyToonAmbientStrength");
        private static readonly int AmbientColorId = Shader.PropertyToID("_CozyToonAmbientColor");
        private static readonly int SceneLightColorInfluenceId = Shader.PropertyToID("_CozyToonSceneLightColorInfluence");
        private static readonly int UseSceneFogId = Shader.PropertyToID("_CozyToonUseSceneFog");
        private static readonly int PaletteGradeEnabledId = Shader.PropertyToID("_CozyToonPaletteGradeEnabled");
        private static readonly int SaturationId = Shader.PropertyToID("_CozyToonSaturation");
        private static readonly int WarmthId = Shader.PropertyToID("_CozyToonWarmth");
        private static readonly int RimEnabledId = Shader.PropertyToID("_CozyToonRimEnabled");
        private static readonly int RimColorId = Shader.PropertyToID("_CozyToonRimColor");
        private static readonly int RimIntensityId = Shader.PropertyToID("_CozyToonRimIntensity");
        private static readonly int RimPowerId = Shader.PropertyToID("_CozyToonRimPower");
        private static readonly int SpecularEnabledId = Shader.PropertyToID("_CozyToonSpecularEnabled");
        private static readonly int SpecularColorId = Shader.PropertyToID("_CozyToonSpecularColor");
        private static readonly int SpecularIntensityId = Shader.PropertyToID("_CozyToonSpecularIntensity");
        private static readonly int SpecularSizeId = Shader.PropertyToID("_CozyToonSpecularSize");
        private static readonly int OutlineEnabledId = Shader.PropertyToID("_CozyToonOutlineEnabled");
        private static readonly int OutlineColorId = Shader.PropertyToID("_CozyToonOutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_CozyToonOutlineWidth");

        private static CozyToonRuntime instance;

        private readonly Dictionary<Renderer, Material[]> originalMaterials = new();
        private readonly Dictionary<Material, Material> toonMaterials = new();
        private readonly List<Renderer> deadRenderers = new();

        private CozyToonGlobalSettings settings;
        private Shader toonShader;
        private float nextScanTime;
        private int coverageHash;
        private bool materialsApplied;

        public static CozyToonRuntime Instance => instance;
        public CozyToonGlobalSettings Settings => settings;
        public int ConvertedRendererCount => originalMaterials.Count;
        public int GeneratedMaterialCount => toonMaterials.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null)
                return;

            GameObject host = new("[Cozy Toon Runtime]");
            DontDestroyOnLoad(host);
            host.AddComponent<CozyToonRuntime>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                DestroyTemporaryObject(gameObject);
                return;
            }

            instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            settings = Resources.Load<CozyToonGlobalSettings>(CozyToonGlobalSettings.ResourceName);
            toonShader = settings != null && settings.ToonShader != null
                ? settings.ToonShader
                : Shader.Find(ToonShaderName);

            if (settings == null)
            {
                Debug.LogError($"[Cozy Toon] Missing Resources/{CozyToonGlobalSettings.ResourceName}.asset. Models were left unchanged.");
                enabled = false;
                return;
            }

            if (toonShader == null)
            {
                Debug.LogError($"[Cozy Toon] Shader '{ToonShaderName}' was not included. Models were left unchanged.");
                enabled = false;
                return;
            }

            coverageHash = CalculateCoverageHash();
            ApplyGlobalShaderSettings();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            RefreshNow(true);
        }

        private void Update()
        {
            if (settings == null)
                return;

            ApplyGlobalShaderSettings();

            int currentCoverageHash = CalculateCoverageHash();
            if (currentCoverageHash != coverageHash)
            {
                coverageHash = currentCoverageHash;
                RebuildAllMaterials();
            }

            if (!settings.toonEnabled)
            {
                if (materialsApplied)
                    RestoreAllRenderers();
                return;
            }

            if (Time.realtimeSinceStartup >= nextScanTime)
                RefreshNow();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            RestoreAllRenderers();
            DestroyGeneratedMaterials();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Application.isPlaying)
                StartCoroutine(RefreshAfterSceneLoad());
            else
                RefreshNow(true);
        }

        private IEnumerator RefreshAfterSceneLoad()
        {
            yield return null;
            RefreshNow(true);
        }

        public void RefreshNow(bool forceImmediateRescan = false)
        {
            if (settings == null || toonShader == null)
                return;

            ApplyGlobalShaderSettings();
            nextScanTime = Time.realtimeSinceStartup + settings.rescanInterval;

            if (!settings.toonEnabled)
            {
                RestoreAllRenderers();
                return;
            }

            RemoveDestroyedRendererEntries();

            FindObjectsInactive inactiveMode = settings.includeInactiveRenderers
                ? FindObjectsInactive.Include
                : FindObjectsInactive.Exclude;
            Renderer[] renderers = FindObjectsByType<Renderer>(inactiveMode, FindObjectsSortMode.None);

            for (int i = 0; i < renderers.Length; i++)
                ApplyToRenderer(renderers[i]);

            materialsApplied = originalMaterials.Count > 0;

            if (forceImmediateRescan)
                nextScanTime = Time.realtimeSinceStartup + Mathf.Min(settings.rescanInterval, 0.25f);
        }

        private void ApplyToRenderer(Renderer targetRenderer)
        {
            if (!IsSupportedRenderer(targetRenderer) || originalMaterials.ContainsKey(targetRenderer))
                return;

            Material[] sourceMaterials = targetRenderer.sharedMaterials;
            if (sourceMaterials == null || sourceMaterials.Length == 0)
                return;

            Material[] replacementMaterials = new Material[sourceMaterials.Length];
            bool replacedAny = false;

            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                Material source = sourceMaterials[i];
                Material replacement = GetOrCreateToonMaterial(source);
                replacementMaterials[i] = replacement != null ? replacement : source;
                replacedAny |= replacement != null && replacement != source;
            }

            if (!replacedAny)
                return;

            originalMaterials.Add(targetRenderer, sourceMaterials);
            targetRenderer.sharedMaterials = replacementMaterials;
        }

        private bool IsSupportedRenderer(Renderer targetRenderer)
        {
            if (targetRenderer == null || (targetRenderer is not MeshRenderer && targetRenderer is not SkinnedMeshRenderer))
                return false;

            int layerBit = 1 << targetRenderer.gameObject.layer;
            if ((settings.excludedLayers.value & layerBit) != 0)
                return false;

            if (ContainsAny(targetRenderer.gameObject.name, settings.excludedObjectNameFragments))
                return false;

            return targetRenderer.GetComponentInParent<Canvas>() == null;
        }

        private Material GetOrCreateToonMaterial(Material source)
        {
            if (source == null || source.shader == null || source.shader == toonShader)
                return null;

            if (ContainsAny(source.shader.name, settings.excludedShaderNameFragments))
                return null;

            bool transparent = IsTransparent(source);
            if (transparent && !settings.includeTransparentMaterials)
                return null;

            if (toonMaterials.TryGetValue(source, out Material existing) && existing != null)
                return existing;

            Material toon = new(toonShader)
            {
                name = source.name + " (Cozy Toon Runtime)",
                hideFlags = HideFlags.DontSave,
                enableInstancing = source.enableInstancing,
                doubleSidedGI = source.doubleSidedGI,
                globalIlluminationFlags = source.globalIlluminationFlags
            };

            CopyTexture(source, toon, "_BaseMap", "_BaseMap", "_MainTex");
            CopyColor(source, toon, "_BaseColor", Color.white, "_BaseColor", "_Color");
            CopyTexture(source, toon, "_BumpMap", "_BumpMap", "_NormalMap");
            CopyFloat(source, toon, "_BumpScale", 1f, "_BumpScale", "_NormalScale");
            CopyTexture(source, toon, "_EmissionMap", "_EmissionMap");
            CopyColor(source, toon, "_EmissionColor", Color.black, "_EmissionColor");

            bool alphaClip = IsAlphaClipped(source);
            CopyFloat(source, toon, "_Cutoff", 0.5f, "_Cutoff", "_AlphaClipThreshold");
            toon.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            SetKeyword(toon, "_ALPHATEST_ON", alphaClip);
            SetKeyword(toon, "_NORMALMAP", toon.GetTexture("_BumpMap") != null);
            SetKeyword(toon, "_EMISSION", toon.GetTexture("_EmissionMap") != null || toon.GetColor("_EmissionColor").maxColorComponent > 0.001f);

            float cull = source.HasProperty("_Cull") ? source.GetFloat("_Cull") : (float)CullMode.Back;
            toon.SetFloat("_Cull", cull);

            if (transparent)
            {
                toon.SetOverrideTag("RenderType", "Transparent");
                toon.SetFloat("_Surface", 1f);
                toon.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                toon.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                toon.SetFloat("_ZWrite", 0f);
                toon.renderQueue = source.renderQueue >= (int)RenderQueue.Transparent
                    ? source.renderQueue
                    : (int)RenderQueue.Transparent;
            }
            else
            {
                toon.SetOverrideTag("RenderType", alphaClip ? "TransparentCutout" : "Opaque");
                toon.SetFloat("_Surface", 0f);
                toon.SetFloat("_SrcBlend", (float)BlendMode.One);
                toon.SetFloat("_DstBlend", (float)BlendMode.Zero);
                toon.SetFloat("_ZWrite", 1f);
                toon.renderQueue = alphaClip
                    ? Mathf.Max(source.renderQueue, (int)RenderQueue.AlphaTest)
                    : source.renderQueue;
            }

            toonMaterials.Add(source, toon);
            return toon;
        }

        private void ApplyGlobalShaderSettings()
        {
            Shader.SetGlobalColor(ShadowColorId, settings.shadowColor);
            Shader.SetGlobalFloat(ShadowTintStrengthId, settings.shadowTintStrength);
            Shader.SetGlobalFloat(ShadowBrightnessId, settings.shadowBrightness);
            Shader.SetGlobalFloat(DeepShadowBrightnessId, settings.deepShadowBrightness);
            Shader.SetGlobalFloat(DeepShadowThresholdId, settings.deepShadowThreshold);
            Shader.SetGlobalColor(LightTintId, settings.lightTint);
            Shader.SetGlobalFloat(LightTintStrengthId, settings.lightTintStrength);
            Shader.SetGlobalFloat(ShadowThresholdId, settings.shadowThreshold);
            Shader.SetGlobalFloat(HighlightThresholdId, settings.highlightThreshold);
            Shader.SetGlobalFloat(BandSoftnessId, settings.bandSoftness);
            Shader.SetGlobalFloat(AmbientStrengthId, settings.ambientStrength);
            Shader.SetGlobalColor(AmbientColorId, settings.ambientColor);
            Shader.SetGlobalFloat(SceneLightColorInfluenceId, settings.sceneLightColorInfluence);
            Shader.SetGlobalFloat(UseSceneFogId, settings.useSceneFog ? 1f : 0f);
            Shader.SetGlobalFloat(PaletteGradeEnabledId, settings.paletteGradeEnabled ? 1f : 0f);
            Shader.SetGlobalFloat(SaturationId, settings.saturation);
            Shader.SetGlobalFloat(WarmthId, settings.warmth);
            Shader.SetGlobalFloat(RimEnabledId, settings.rimEnabled ? 1f : 0f);
            Shader.SetGlobalColor(RimColorId, settings.rimColor);
            Shader.SetGlobalFloat(RimIntensityId, settings.rimIntensity);
            Shader.SetGlobalFloat(RimPowerId, settings.rimPower);
            Shader.SetGlobalFloat(SpecularEnabledId, settings.specularEnabled ? 1f : 0f);
            Shader.SetGlobalColor(SpecularColorId, settings.specularColor);
            Shader.SetGlobalFloat(SpecularIntensityId, settings.specularIntensity);
            Shader.SetGlobalFloat(SpecularSizeId, settings.specularSize);
            Shader.SetGlobalFloat(OutlineEnabledId, settings.outlineEnabled ? 1f : 0f);
            Shader.SetGlobalColor(OutlineColorId, settings.outlineColor);
            Shader.SetGlobalFloat(OutlineWidthId, settings.outlineWidth);
        }

        public void RebuildAllMaterials()
        {
            RestoreAllRenderers();
            DestroyGeneratedMaterials();
            RefreshNow(true);
        }

        private void RestoreAllRenderers()
        {
            foreach (KeyValuePair<Renderer, Material[]> entry in originalMaterials)
            {
                if (entry.Key != null)
                    entry.Key.sharedMaterials = entry.Value;
            }

            originalMaterials.Clear();
            materialsApplied = false;
        }

        private void DestroyGeneratedMaterials()
        {
            foreach (Material material in toonMaterials.Values)
            {
                if (material != null)
                    DestroyTemporaryObject(material);
            }

            toonMaterials.Clear();
        }

        private void RemoveDestroyedRendererEntries()
        {
            deadRenderers.Clear();
            foreach (Renderer targetRenderer in originalMaterials.Keys)
            {
                if (targetRenderer == null)
                    deadRenderers.Add(targetRenderer);
            }

            for (int i = 0; i < deadRenderers.Count; i++)
                originalMaterials.Remove(deadRenderers[i]);
        }

        private int CalculateCoverageHash()
        {
            unchecked
            {
                int hash = settings.includeInactiveRenderers ? 17 : 31;
                hash = (hash * 397) ^ (settings.includeTransparentMaterials ? 1 : 0);
                hash = (hash * 397) ^ settings.excludedLayers.value;
                hash = AppendArrayHash(hash, settings.excludedObjectNameFragments);
                return AppendArrayHash(hash, settings.excludedShaderNameFragments);
            }
        }

        private static int AppendArrayHash(int hash, string[] values)
        {
            if (values == null)
                return hash;

            unchecked
            {
                for (int i = 0; i < values.Length; i++)
                    hash = (hash * 397) ^ (values[i]?.GetHashCode() ?? 0);
                return hash;
            }
        }

        private static bool ContainsAny(string value, string[] fragments)
        {
            if (string.IsNullOrEmpty(value) || fragments == null)
                return false;

            for (int i = 0; i < fragments.Length; i++)
            {
                string fragment = fragments[i];
                if (!string.IsNullOrWhiteSpace(fragment) && value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool IsTransparent(Material material)
        {
            if (material.renderQueue >= (int)RenderQueue.Transparent)
                return true;

            if (material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f)
                return true;

            return string.Equals(material.GetTag("RenderType", false), "Transparent", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAlphaClipped(Material material)
        {
            if (material.IsKeywordEnabled("_ALPHATEST_ON"))
                return true;

            if (material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f)
                return true;

            return string.Equals(material.GetTag("RenderType", false), "TransparentCutout", StringComparison.OrdinalIgnoreCase);
        }

        private static void CopyTexture(Material source, Material destination, string destinationProperty, params string[] sourceProperties)
        {
            for (int i = 0; i < sourceProperties.Length; i++)
            {
                string property = sourceProperties[i];
                if (!source.HasProperty(property))
                    continue;

                Texture texture = source.GetTexture(property);
                if (texture == null)
                    continue;

                destination.SetTexture(destinationProperty, texture);
                destination.SetTextureScale(destinationProperty, source.GetTextureScale(property));
                destination.SetTextureOffset(destinationProperty, source.GetTextureOffset(property));
                return;
            }
        }

        private static void CopyColor(Material source, Material destination, string destinationProperty, Color fallback, params string[] sourceProperties)
        {
            for (int i = 0; i < sourceProperties.Length; i++)
            {
                if (source.HasProperty(sourceProperties[i]))
                {
                    destination.SetColor(destinationProperty, source.GetColor(sourceProperties[i]));
                    return;
                }
            }

            destination.SetColor(destinationProperty, fallback);
        }

        private static void CopyFloat(Material source, Material destination, string destinationProperty, float fallback, params string[] sourceProperties)
        {
            for (int i = 0; i < sourceProperties.Length; i++)
            {
                if (source.HasProperty(sourceProperties[i]))
                {
                    destination.SetFloat(destinationProperty, source.GetFloat(sourceProperties[i]));
                    return;
                }
            }

            destination.SetFloat(destinationProperty, fallback);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        private static void DestroyTemporaryObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
