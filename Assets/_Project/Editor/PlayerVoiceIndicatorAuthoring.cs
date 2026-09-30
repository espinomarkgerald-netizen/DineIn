#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit, repeatable authoring of the multiplayer-only nameplate accessory.</summary>
public static class PlayerVoiceIndicatorAuthoring
{
    public const string Folder = "Assets/_Project/Player/NameTag/";
    public const string PrefabPath = Folder + "PlayerVoiceSpeakingIndicator.prefab";
    private const string PlayerPath = "Assets/Resources/ManagerMultiplayer.prefab";
    private const string IconPath = Folder + "VoiceMicrophone.png";

    [MenuItem("Dine In/Polish/Author Player Speaking Indicator")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring.");
        // Validate the attachment before writing anything; preserve all existing nameplate properties.
        var playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        var tag = playerAsset.GetComponentInChildren<NameTagBillboard>(true);
        if (tag == null || tag.GetComponentInChildren<Canvas>(true) == null)
            throw new InvalidOperationException("Multiplayer nameplate Canvas is missing.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) CreatePrefab();
        var player = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            var existing = player.GetComponentsInChildren<PlayerVoiceSpeakingIndicator>(true);
            if (existing.Length > 1) throw new InvalidOperationException("Duplicate speaking indicators; inspect before authoring.");
            if (existing.Length == 0)
            {
                var canvas = player.GetComponentInChildren<NameTagBillboard>(true).GetComponentInChildren<Canvas>(true);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), canvas.transform);
                instance.name = "Voice Speaking Indicator";
                var rect = (RectTransform)instance.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2(0f, 94f);
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        Debug.Log(Validate());
    }

    private static void CreatePrefab()
    {
        var background = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/Blue/Double/button_rectangle_depth_flat.png");
        if (background == null) throw new InvalidOperationException("Existing blue UI sprite not found.");
        CreateMicrophoneSprite();
        var root = Node(null, "PlayerVoiceSpeakingIndicator", new Vector2(144f, 80f), Vector2.zero);
        try
        {
            var presentation = root.gameObject.AddComponent<PlayerVoiceSpeakingIndicator>();
            var visual = Node(root, "Visual", root.sizeDelta, Vector2.zero);
            var group = visual.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;
            var bubble = AddImage(visual, "Bubble", root.sizeDelta, Vector2.zero, background);
            bubble.type = UnityEngine.UI.Image.Type.Sliced;
            var mic = AddImage(visual, "Microphone", new Vector2(40f, 60f), new Vector2(-32f, 3f), AssetDatabase.LoadAssetAtPath<Sprite>(IconPath));
            mic.preserveAspect = true;
            var pill = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var bars = new RectTransform[3];
            float[] heights = { 28f, 44f, 34f };
            for (int i = 0; i < bars.Length; i++)
            {
                var bar = AddImage(visual, "Voice Bar " + (i + 1), new Vector2(9f, heights[i]), new Vector2(11f + i * 17f, 3f), pill);
                bar.type = UnityEngine.UI.Image.Type.Sliced;
                bars[i] = bar.rectTransform;
            }
            var data = new SerializedObject(presentation);
            data.FindProperty("visual").objectReferenceValue = visual;
            data.FindProperty("group").objectReferenceValue = group;
            var array = data.FindProperty("voiceBars"); array.arraySize = bars.Length;
            for (int i = 0; i < bars.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = bars[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            visual.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
    }

    private static RectTransform Node(Transform parent, string name, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size; rect.anchoredPosition = position;
        return rect;
    }

    private static UnityEngine.UI.Image AddImage(Transform parent, string name, Vector2 size, Vector2 position, Sprite sprite)
    {
        var image = Node(parent, name, size, position).gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = sprite; image.color = Color.white; image.raycastTarget = false;
        return image;
    }

    // Small antialiased silhouette, authored once as an ordinary editable sprite asset.
    private static void CreateMicrophoneSprite()
    {
        if (File.Exists(IconPath)) return;
        const int width = 64, height = 96, samples = 4;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int covered = 0;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    var p = new Vector2(x + (sx + .5f) / samples, y + (sy + .5f) / samples);
                    bool capsule = Capsule(p, new Vector2(32, 49), new Vector2(32, 77), 10);
                    float radius = Vector2.Distance(p, new Vector2(32, 45));
                    bool cradle = p.y <= 45 && radius >= 17 && radius <= 22;
                    bool sides = Capsule(p, new Vector2(12.5f, 45), new Vector2(12.5f, 58), 2.5f)
                        || Capsule(p, new Vector2(51.5f, 45), new Vector2(51.5f, 58), 2.5f);
                    bool stand = Capsule(p, new Vector2(32, 13), new Vector2(32, 23), 3)
                        || Capsule(p, new Vector2(21, 11), new Vector2(43, 11), 3);
                    if (capsule || cradle || sides || stand) covered++;
                }
                pixels[y * width + x] = new Color(1, 1, 1, covered / (float)(samples * samples));
            }
            texture.SetPixels(pixels); texture.Apply();
            File.WriteAllBytes(IconPath, texture.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear; importer.SaveAndReimport();
    }

    private static bool Capsule(Vector2 point, Vector2 a, Vector2 b, float radius)
    {
        var edge = b - a;
        var nearest = a + edge * Mathf.Clamp01(Vector2.Dot(point - a, edge) / edge.sqrMagnitude);
        return (point - nearest).sqrMagnitude <= radius * radius;
    }

    public static string Validate()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        var indicators = player.GetComponentsInChildren<PlayerVoiceSpeakingIndicator>(true);
        if (indicators.Length != 1) throw new InvalidOperationException("Expected exactly one indicator per multiplayer avatar.");
        var indicator = indicators[0];
        if (indicator.GetComponentInParent<NameTagBillboard>(true) == null) throw new InvalidOperationException("Indicator must inherit the existing billboard.");
        var group = indicator.GetComponentInChildren<CanvasGroup>(true);
        if (group == null || group.alpha != 0f || group.gameObject.activeSelf || group.blocksRaycasts)
            throw new InvalidOperationException("Indicator must begin hidden and non-interactive.");
        if (indicator.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Any(g => g.raycastTarget))
            throw new InvalidOperationException("Voice visuals must not intercept input.");
        return "[Voice indicator] PASS: one authored instance under the existing billboard, initially hidden, no input interception.";
    }
}
#endif