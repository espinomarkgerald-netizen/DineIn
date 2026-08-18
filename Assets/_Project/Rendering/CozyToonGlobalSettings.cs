using UnityEngine;

namespace DineIn.Rendering
{
    [CreateAssetMenu(fileName = ResourceName, menuName = "Dine In/Rendering/Cozy Toon Global Settings")]
    public sealed class CozyToonGlobalSettings : ScriptableObject
    {
        public const string ResourceName = "CozyToonGlobalSettings";

        [SerializeField, HideInInspector] private Shader toonShader;

        public Shader ToonShader => toonShader;

        [Header("Master")]
        [Tooltip("Applies the Cozy Toon material to supported 3D models. Turn this off to restore their original materials.")]
        public bool toonEnabled = true;
        [Tooltip("Shows the toon materials in the Scene and Game views without entering Play Mode. Preview materials are temporary and are removed before a scene is saved.")]
        public bool previewInEditMode = true;

        [ColorUsage(false, true)] public Color shadowColor = new(0.48f, 0.55f, 0.7f, 1f);
        [Range(0f, 1f)] public float shadowTintStrength;
        [Range(0f, 1f)] public float shadowBrightness = 0.72f;
        [Range(0f, 1f)] public float deepShadowBrightness = 0.5f;
        [Range(0f, 1f)] public float shadowThreshold = 0.56f;
        [Range(0f, 1f)] public float deepShadowThreshold = 0.3f;
        [ColorUsage(false, true)] public Color lightTint = new(1f, 0.96f, 0.82f, 1f);
        [Range(0f, 1f)] public float lightTintStrength;
        [Range(0f, 1f)] public float highlightThreshold = 0.82f;
        [Range(0.001f, 0.25f)] public float bandSoftness = 0.035f;
        [ColorUsage(false, true)] public Color ambientColor = Color.white;
        [Range(0f, 2f)] public float ambientStrength = 0.2f;
        [Tooltip("How much the hue of scene lights affects a model. Lower values prevent one colored light from tinting the whole scene.")]
        [Range(0f, 1f)] public float sceneLightColorInfluence;
        [Tooltip("Applies Unity scene fog to toon materials. Disabled by default because Lobby1 uses strong blue fog starting at distance zero.")]
        public bool useSceneFog;

        [Tooltip("Disabled by default. Enable only when you intentionally want to recolor every toon-shaded model.")]
        public bool paletteGradeEnabled;
        [Range(0f, 2f)] public float saturation = 1f;
        [Range(-1f, 1f)] public float warmth;

        public bool rimEnabled = true;
        [ColorUsage(false, true)] public Color rimColor = new(1f, 0.86f, 0.62f, 1f);
        [Range(0f, 2f)] public float rimIntensity = 0.1f;
        [Range(0.5f, 12f)] public float rimPower = 3.2f;

        public bool specularEnabled = true;
        [ColorUsage(false, true)] public Color specularColor = new(1f, 0.94f, 0.78f, 1f);
        [Range(0f, 2f)] public float specularIntensity = 0.16f;
        [Range(0.01f, 0.5f)] public float specularSize = 0.08f;

        public bool outlineEnabled = true;
        public Color outlineColor = new(0.08f, 0.1f, 0.16f, 0.82f);
        [Range(0f, 5f)] public float outlineWidth = 0.8f;

        [Tooltip("How often the system checks for newly spawned characters, customers, food and furniture.")]
        [Range(0.1f, 10f)] public float rescanInterval = 0.75f;
        [Tooltip("Also prepares models that are currently disabled so they are toon-shaded as soon as they appear.")]
        public bool includeInactiveRenderers = true;
        [Tooltip("Transparent materials are skipped by default to protect glass, particles and special effects.")]
        public bool includeTransparentMaterials;
        [Tooltip("Objects on these layers retain their original materials. UI is excluded by default.")]
        public LayerMask excludedLayers = 1 << 5;
        [Tooltip("A renderer is skipped when its object name contains one of these words.")]
        public string[] excludedObjectNameFragments = { "Canvas", "UI", "VFX", "Particle", "Effect" };
        [Tooltip("A material is skipped when its shader name contains one of these words.")]
        public string[] excludedShaderNameFragments =
        {
            "TextMeshPro", "UI/", "Sprites/", "Particles/", "Skybox/", "Hidden/", "QuickOutline"
        };

        private void OnValidate()
        {
            shadowThreshold = Mathf.Clamp01(shadowThreshold);
            deepShadowThreshold = Mathf.Clamp(deepShadowThreshold, 0f, shadowThreshold);
            highlightThreshold = Mathf.Clamp(highlightThreshold, shadowThreshold, 1f);
            bandSoftness = Mathf.Max(0.001f, bandSoftness);
            rescanInterval = Mathf.Max(0.1f, rescanInterval);

            if (CozyToonRuntime.Instance != null)
                CozyToonRuntime.Instance.RefreshNow();
        }
    }
}
