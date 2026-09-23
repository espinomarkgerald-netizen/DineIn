using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Extracts only presentation objects from the tutorial, never its lesson/gameplay systems.</summary>
[InitializeOnLoad]
public sealed class RestaurantUnlockGuideAuthoring : IPreprocessBuildWithReport
{
    private const string Source = "Assets/_Project/Scenes/TutorialScenes/Lobby1Tutorial.unity";
    private const string Destination = "Assets/_Project/Resources/UI/RestaurantUnlockGuide.prefab";
    public int callbackOrder => 0;

    static RestaurantUnlockGuideAuthoring()
    {
        EditorApplication.delayCall += EnsurePresentation;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EnsurePresentation();
            if (state == PlayModeStateChange.ExitingEditMode) CreateMissing();
        };
    }

    private static void EnsurePresentation()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling) CreateMissing();
    }

    public void OnPreprocessBuild(BuildReport report) => CreateMissing();

    [MenuItem("Dine In/Tutorial/Create Campaign Guide Presentation")]
    public static void CreateMissing()
    {
        // Existing designer-edited assets remain authoritative.
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Destination) != null) return;
        if (EditorApplication.isPlaying) return;
        var preview = EditorSceneManager.OpenPreviewScene(Source);
        GameObject root = null;
        try
        {
            var roots = preview.GetRootGameObjects();
            var sourceDialogue = roots.SelectMany(r => r.GetComponentsInChildren<TutorialDialogueUI>(true)).Single();
            var sourceHand = roots.SelectMany(r => r.GetComponentsInChildren<TutorialHandIndicator>(true)).Single();
            var sourceTarget = roots.SelectMany(r => r.GetComponentsInChildren<TutorialTargetIndicator>(true)).Single();
            root = new GameObject("Restaurant Unlock Guide", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(CanvasGroup));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
            root.SetActive(false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32759;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            var safe = Rect("Safe Area", root.transform);
            Stretch(safe);
            var dialogue = UnityEngine.Object.Instantiate(sourceDialogue, safe, false);
            dialogue.name = "Tutorial Dialogue";
            dialogue.gameObject.SetActive(true);
            var dialogueData = new SerializedObject(dialogue);
            dialogueData.FindProperty("useTutorialPresentation").boolValue = true;
            dialogueData.ApplyModifiedPropertiesWithoutUndo();
            // Prefab instantiation remaps internal references. Keep the exact tutorial sprites/fonts/poses.
            var hand = UnityEngine.Object.Instantiate(sourceHand, root.transform, false);
            var handData = new SerializedObject(hand);
            handData.FindProperty("targetCanvas").objectReferenceValue = canvas;
            handData.FindProperty("worldCamera").objectReferenceValue = null;
            handData.FindProperty("keepHintsWithOwner").boolValue = true;
            handData.ApplyModifiedPropertiesWithoutUndo();
            hand.gameObject.SetActive(true);
            var target = UnityEngine.Object.Instantiate(sourceTarget, root.transform, false);
            if (target.GetComponent<CanvasGroup>() == null) target.gameObject.AddComponent<CanvasGroup>();
            var targetData = new SerializedObject(target);
            targetData.FindProperty("targetCanvas").objectReferenceValue = canvas;
            targetData.FindProperty("worldCamera").objectReferenceValue = null;
            targetData.ApplyModifiedPropertiesWithoutUndo();
            target.gameObject.SetActive(true);

            // Small escape controls sit above the mask; they are not a replacement dialogue card.
            var chrome = Rect("Guide Controls", safe);
            Stretch(chrome);
            var chromeCanvas = chrome.gameObject.AddComponent<Canvas>();
            chromeCanvas.overrideSorting = true;
            chromeCanvas.sortingOrder = 32762;
            chrome.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            TMP_FontAsset font = (dialogueData.FindProperty("bodyText").objectReferenceValue as TMP_Text).font;
            var caption = Label("Step", chrome, font, 24f);
            Top(caption.rectTransform, new Vector2(24f, -20f), new Vector2(1100f, 42f));
            var objective = Label("Objective", chrome, font, 24f);
            Top(objective.rectTransform, new Vector2(24f, -65f), new Vector2(1100f, 62f));
            var controls = Rect("Later and Skip", chrome);
            controls.anchorMin = controls.anchorMax = new Vector2(1f, 1f);
            controls.pivot = new Vector2(1f, 1f);
            controls.anchoredPosition = new Vector2(-24f, -20f);
            controls.sizeDelta = new Vector2(280f, 64f);
            var later = Button("LATER", controls, font, 0f);
            var skip = Button("SKIP", controls, font, 144f);
            var view = root.AddComponent<RestaurantUnlockCoachUI>();
            var data = new SerializedObject(view);
            Set(data, "dialogue", dialogue); Set(data, "hand", hand); Set(data, "worldIndicator", target);
            Set(data, "safeArea", safe); Set(data, "dialogueRoot", dialogue.transform);
            Set(data, "controlsRoot", controls); Set(data, "caption", caption); Set(data, "objective", objective);
            Set(data, "laterButton", later); Set(data, "skipButton", skip);
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, Destination, out bool saved);
            if (!saved) throw new InvalidOperationException("Could not save the campaign tutorial presentation.");
            Debug.Log("[UnlockCoach] Created campaign presentation from Lobby1Tutorial. No gameplay scenes modified.");
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static void Set(SerializedObject data, string field, UnityEngine.Object value) =>
        data.FindProperty(field).objectReferenceValue = value;
    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static void Top(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f); rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, float size)
    {
        var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = size; text.color = Color.white;
        text.raycastTarget = false;
        var shadow = text.gameObject.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectDistance = new Vector2(1f, -1f);
        return text;
    }
    private static UnityEngine.UI.Button Button(string title, Transform parent, TMP_FontAsset font, float x)
    {
        var rect = Rect(title, parent);
        Top(rect, new Vector2(x, 0f), new Vector2(136f, 64f));
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color32(35, 30, 28, 235);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        var text = Label("Label", rect, font, 24f);
        text.text = title; text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform);
        return button;
    }
}
