using DineIn.Rendering;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CozyToonGlobalSettings))]
public sealed class CozyToonGlobalSettingsEditor : Editor
{
    private const string SettingsPath = "Assets/_Project/Resources/CozyToonGlobalSettings.asset";

    private SerializedProperty toonEnabled;
    private SerializedProperty previewInEditMode;
    private SerializedProperty shadowColor;
    private SerializedProperty shadowTintStrength;
    private SerializedProperty shadowBrightness;
    private SerializedProperty deepShadowBrightness;
    private SerializedProperty lightTint;
    private SerializedProperty lightTintStrength;
    private SerializedProperty shadowThreshold;
    private SerializedProperty deepShadowThreshold;
    private SerializedProperty highlightThreshold;
    private SerializedProperty bandSoftness;
    private SerializedProperty ambientColor;
    private SerializedProperty ambientStrength;
    private SerializedProperty sceneLightColorInfluence;
    private SerializedProperty useSceneFog;
    private SerializedProperty paletteGradeEnabled;
    private SerializedProperty saturation;
    private SerializedProperty warmth;
    private SerializedProperty rimEnabled;
    private SerializedProperty rimColor;
    private SerializedProperty rimIntensity;
    private SerializedProperty rimPower;
    private SerializedProperty specularEnabled;
    private SerializedProperty specularColor;
    private SerializedProperty specularIntensity;
    private SerializedProperty specularSize;
    private SerializedProperty outlineEnabled;
    private SerializedProperty outlineColor;
    private SerializedProperty outlineWidth;
    private SerializedProperty rescanInterval;
    private SerializedProperty includeInactiveRenderers;
    private SerializedProperty includeTransparentMaterials;
    private SerializedProperty excludedLayers;
    private SerializedProperty excludedObjectNameFragments;
    private SerializedProperty excludedShaderNameFragments;

    private void OnEnable()
    {
        toonEnabled = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.toonEnabled));
        previewInEditMode = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.previewInEditMode));
        shadowColor = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.shadowColor));
        shadowTintStrength = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.shadowTintStrength));
        shadowBrightness = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.shadowBrightness));
        deepShadowBrightness = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.deepShadowBrightness));
        lightTint = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.lightTint));
        lightTintStrength = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.lightTintStrength));
        shadowThreshold = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.shadowThreshold));
        deepShadowThreshold = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.deepShadowThreshold));
        highlightThreshold = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.highlightThreshold));
        bandSoftness = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.bandSoftness));
        ambientColor = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.ambientColor));
        ambientStrength = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.ambientStrength));
        sceneLightColorInfluence = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.sceneLightColorInfluence));
        useSceneFog = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.useSceneFog));
        paletteGradeEnabled = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.paletteGradeEnabled));
        saturation = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.saturation));
        warmth = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.warmth));
        rimEnabled = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.rimEnabled));
        rimColor = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.rimColor));
        rimIntensity = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.rimIntensity));
        rimPower = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.rimPower));
        specularEnabled = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.specularEnabled));
        specularColor = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.specularColor));
        specularIntensity = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.specularIntensity));
        specularSize = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.specularSize));
        outlineEnabled = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.outlineEnabled));
        outlineColor = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.outlineColor));
        outlineWidth = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.outlineWidth));
        rescanInterval = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.rescanInterval));
        includeInactiveRenderers = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.includeInactiveRenderers));
        includeTransparentMaterials = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.includeTransparentMaterials));
        excludedLayers = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.excludedLayers));
        excludedObjectNameFragments = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.excludedObjectNameFragments));
        excludedShaderNameFragments = serializedObject.FindProperty(nameof(CozyToonGlobalSettings.excludedShaderNameFragments));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "This one asset controls the Cozy Toon look for supported 3D models in every scene. " +
            "It preserves the original textures and materials, and updates spawned models automatically.",
            MessageType.Info);

        EditorGUILayout.PropertyField(toonEnabled, new GUIContent("Enable Cozy Toon Everywhere"));
        EditorGUILayout.PropertyField(previewInEditMode, new GUIContent("Preview In Edit Mode"));

        using (new EditorGUI.DisabledScope(!toonEnabled.boolValue))
        {
            DrawSection("Cel-Shaded Lighting");
            EditorGUILayout.PropertyField(shadowThreshold, new GUIContent("Base To First Shade Step"));
            EditorGUILayout.PropertyField(deepShadowThreshold, new GUIContent("First To Second Shade Step"));
            EditorGUILayout.PropertyField(bandSoftness, new GUIContent("Band Edge Softness"));
            EditorGUILayout.PropertyField(shadowBrightness, new GUIContent("First Shade Brightness"));
            EditorGUILayout.PropertyField(deepShadowBrightness, new GUIContent("Second Shade Brightness"));
            EditorGUILayout.PropertyField(shadowColor, new GUIContent("Shadow Tint"));
            EditorGUILayout.PropertyField(shadowTintStrength, new GUIContent("Shadow Tint Strength"));
            EditorGUILayout.PropertyField(highlightThreshold, new GUIContent("Bright Band Start"));
            EditorGUILayout.PropertyField(lightTint, new GUIContent("Bright Band Tint"));
            EditorGUILayout.PropertyField(lightTintStrength, new GUIContent("Bright Tint Strength"));
            EditorGUILayout.PropertyField(ambientColor, new GUIContent("Ambient Color"));
            EditorGUILayout.PropertyField(ambientStrength, new GUIContent("Ambient Strength"));
            EditorGUILayout.PropertyField(sceneLightColorInfluence, new GUIContent("Scene Light Color Influence"));
            EditorGUILayout.PropertyField(useSceneFog, new GUIContent("Use Scene Fog"));
            if (useSceneFog.boolValue)
                EditorGUILayout.HelpBox("Lobby1 uses strong blue fog starting at distance zero. Enabling this will visibly tint toon materials blue.", MessageType.Warning);
            else
                EditorGUILayout.HelpBox("Scene fog is ignored by toon materials, preventing Lobby1's blue fog from washing over the restaurant.", MessageType.None);

            DrawSection("Optional Palette Grade");
            EditorGUILayout.PropertyField(paletteGradeEnabled, new GUIContent("Enable Palette Recoloring"));
            using (new EditorGUI.DisabledScope(!paletteGradeEnabled.boolValue))
            {
                EditorGUILayout.PropertyField(saturation, new GUIContent("Colorfulness"));
                EditorGUILayout.PropertyField(warmth, new GUIContent("Warm / Cool"));
            }
            if (!paletteGradeEnabled.boolValue)
                EditorGUILayout.HelpBox("Original material colors are preserved. This is the recommended default.", MessageType.None);

            DrawSection("Rim Light");
            EditorGUILayout.PropertyField(rimEnabled, new GUIContent("Enabled"));
            using (new EditorGUI.DisabledScope(!rimEnabled.boolValue))
            {
                EditorGUILayout.PropertyField(rimColor, new GUIContent("Color"));
                EditorGUILayout.PropertyField(rimIntensity, new GUIContent("Strength"));
                EditorGUILayout.PropertyField(rimPower, new GUIContent("Tightness"));
            }

            DrawSection("Toon Highlights");
            EditorGUILayout.PropertyField(specularEnabled, new GUIContent("Enabled"));
            using (new EditorGUI.DisabledScope(!specularEnabled.boolValue))
            {
                EditorGUILayout.PropertyField(specularColor, new GUIContent("Color"));
                EditorGUILayout.PropertyField(specularIntensity, new GUIContent("Strength"));
                EditorGUILayout.PropertyField(specularSize, new GUIContent("Size"));
            }

            DrawSection("Outlines");
            EditorGUILayout.PropertyField(outlineEnabled, new GUIContent("Enabled"));
            using (new EditorGUI.DisabledScope(!outlineEnabled.boolValue))
            {
                EditorGUILayout.PropertyField(outlineColor, new GUIContent("Color"));
                EditorGUILayout.PropertyField(outlineWidth, new GUIContent("Width (Screen Pixels)"));
            }

            DrawSection("Automatic Coverage");
            EditorGUILayout.PropertyField(rescanInterval, new GUIContent("New Model Check Interval"));
            EditorGUILayout.PropertyField(includeInactiveRenderers, new GUIContent("Prepare Disabled Models"));
            EditorGUILayout.PropertyField(includeTransparentMaterials, new GUIContent("Include Transparent Materials"));
            if (includeTransparentMaterials.boolValue)
                EditorGUILayout.HelpBox("Glass and some visual effects may look different when this is enabled.", MessageType.Warning);
            EditorGUILayout.PropertyField(excludedLayers, new GUIContent("Excluded Layers"));
            EditorGUILayout.PropertyField(excludedObjectNameFragments, new GUIContent("Excluded Object Words"), true);
            EditorGUILayout.PropertyField(excludedShaderNameFragments, new GUIContent("Excluded Shader Words"), true);
        }

        bool changed = serializedObject.ApplyModifiedProperties();
        if (changed && CozyToonRuntime.Instance != null)
            CozyToonRuntime.Instance.RefreshNow(true);

        CozyToonRuntime runtime = CozyToonRuntime.Instance;
        if (runtime != null)
        {
            DrawSection(Application.isPlaying ? "Play Mode Status" : "Edit Mode Preview Status");
            EditorGUILayout.LabelField("Converted Renderers", runtime.ConvertedRendererCount.ToString());
            EditorGUILayout.LabelField("Generated Toon Materials", runtime.GeneratedMaterialCount.ToString());
            if (GUILayout.Button("Rebuild Toon Materials Now"))
                runtime.RebuildAllMaterials();
        }
    }

    [MenuItem("Tools/Dine In/Cozy Toon/Select Global Settings")]
    private static void SelectGlobalSettings()
    {
        CozyToonGlobalSettings settings = AssetDatabase.LoadAssetAtPath<CozyToonGlobalSettings>(SettingsPath);
        if (settings == null)
        {
            Debug.LogError($"Cozy Toon settings asset was not found at {SettingsPath}.");
            return;
        }

        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }

    private static void DrawSection(string title)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }
}
