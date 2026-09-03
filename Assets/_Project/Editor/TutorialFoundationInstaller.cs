#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TutorialFoundationInstaller
{
    public const string LobbyScenePath = "Assets/_Project/Scenes/RoleBased/Lobby1.unity";
    public const string TutorialScenePath = "Assets/_Project/Scenes/TutorialScenes/Lobby1Tutorial.unity";
    private const string LegacyTutorialScenePath = "Assets/_Project/Scenes/TutorialScenes/Lobby1 Tutorial.unity";
    private const string PortraitFolder = "Assets/_Project/UI/Assets/Tutorial Images/";

    [MenuItem("Tools/Dine In/Tutorial/Install Lobby1 Tutorial Foundation")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[TutorialFoundationInstaller] Exit Play mode before installing.");
            return;
        }

        EnsureTutorialSceneAsset();
        Scene scene = EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);

        Canvas hudCanvas = FindSceneObject<Canvas>(scene, "CanvasMainHUD");
        if (hudCanvas == null)
            throw new MissingReferenceException("Lobby1Tutorial is missing CanvasMainHUD.");

        Transform dialogueRoot = hudCanvas.transform.Find("TutorialDialogue");
        if (dialogueRoot == null)
            throw new MissingReferenceException("CanvasMainHUD/TutorialDialogue was not found.");

        Transform dialogueBox = dialogueRoot.Find("DialogueBox");
        TMP_Text speaker = dialogueBox != null
            ? dialogueBox.Find("Name/Text")?.GetComponent<TMP_Text>()
            : null;
        TMP_Text body = dialogueBox != null
            ? dialogueBox.Find("Dialogue")?.GetComponent<TMP_Text>()
            : null;
        Image portrait = dialogueRoot.Find("Big Boss")?.GetComponent<Image>();

        if (dialogueBox == null || speaker == null || body == null || portrait == null)
            throw new MissingReferenceException("The existing TutorialDialogue concept is missing its speaker, body, or Big Boss portrait.");

        Button nextButton = EnsureNextButton(dialogueBox, body);
        TutorialDialogueUI dialogueUI = GetOrAdd<TutorialDialogueUI>(dialogueRoot.gameObject);
        CanvasGroup dialogueGroup = GetOrAdd<CanvasGroup>(dialogueRoot.gameObject);
        dialogueGroup.alpha = 1f;
        dialogueGroup.interactable = true;
        dialogueGroup.blocksRaycasts = true;
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        SerializedObject dialogueSerialized = new SerializedObject(dialogueUI);
        dialogueSerialized.FindProperty("root").objectReferenceValue = dialogueRoot.gameObject;
        dialogueSerialized.FindProperty("speakerText").objectReferenceValue = speaker;
        dialogueSerialized.FindProperty("bodyText").objectReferenceValue = body;
        dialogueSerialized.FindProperty("portraitImage").objectReferenceValue = portrait;
        dialogueSerialized.FindProperty("nextButton").objectReferenceValue = nextButton;
        dialogueSerialized.FindProperty("typeSpeed").floatValue = 0.018f;
        dialogueSerialized.FindProperty("portraitFadeDuration").floatValue = 0.08f;
        dialogueSerialized.ApplyModifiedPropertiesWithoutUndo();

        speaker.text = "Big Boss";
        body.text = "Welcome to Dine In!";

        TutorialTargetIndicator indicator = EnsureTargetIndicator(hudCanvas);
        TutorialHandIndicator handIndicator = EnsureHandIndicator(hudCanvas);
        GameObject systemRoot = FindRoot(scene, "TutorialSystem");
        if (systemRoot == null)
            systemRoot = new GameObject("TutorialSystem");

        TutorialSystem tutorial = GetOrAdd<TutorialSystem>(systemRoot);
        GetOrAdd<TutorialSceneRuntimeMarker>(systemRoot);

        GroupSpawner spawner = Object.FindFirstObjectByType<GroupSpawner>(FindObjectsInactive.Include);
        MainCameraController cameraController = Object.FindFirstObjectByType<MainCameraController>(FindObjectsInactive.Include);
        TapOutlineSelector tapSelector = Object.FindFirstObjectByType<TapOutlineSelector>(FindObjectsInactive.Include);
        Transform interactionTarget = FindTutorialBooth(scene, Camera.main);
        if (cameraController == null || tapSelector == null || interactionTarget == null)
            throw new MissingReferenceException("Lobby1Tutorial needs its real camera, tap selector, and a selectable table/booth.");

        Sprite welcome = LoadPortrait("Welcome  Greeting Pose.png");
        Sprite explaining = LoadPortrait("Tutorial  Explaining Pose.png");
        Sprite success = LoadPortrait("Success  Thumbs Up Pose.png");
        portrait.sprite = welcome;

        SerializedObject tutorialSerialized = new SerializedObject(tutorial);
        tutorialSerialized.FindProperty("dialogueUI").objectReferenceValue = dialogueUI;
        tutorialSerialized.FindProperty("targetIndicator").objectReferenceValue = indicator;
        tutorialSerialized.FindProperty("handIndicator").objectReferenceValue = handIndicator;
        tutorialSerialized.FindProperty("cameraController").objectReferenceValue = cameraController;
        tutorialSerialized.FindProperty("tapSelector").objectReferenceValue = tapSelector;
        tutorialSerialized.FindProperty("groupSpawner").objectReferenceValue = spawner;
        tutorialSerialized.FindProperty("suppressAutomaticSpawningDuringOpening").boolValue = true;
        tutorialSerialized.FindProperty("restoreAutomaticSpawningAfterOpening").boolValue = true;
        tutorialSerialized.FindProperty("startAutomatically").boolValue = true;

        SerializedProperty steps = tutorialSerialized.FindProperty("steps");
        steps.arraySize = 10;
        ConfigureStep(steps.GetArrayElementAtIndex(0), "welcome", "Big Boss",
            "Welcome to Dine In!", welcome, TutorialSystem.TutorialStepType.ManualContinue,
            TutorialSystem.TutorialAction.None, TutorialSystem.TutorialHintMode.None, null, true);
        ConfigureStep(steps.GetArrayElementAtIndex(1), "controls_introduction", "Big Boss",
            "Before we open the restaurant, let me show you the basic controls.", explaining,
            TutorialSystem.TutorialStepType.ManualContinue, TutorialSystem.TutorialAction.None,
            TutorialSystem.TutorialHintMode.None, null, true);
        ConfigureStep(steps.GetArrayElementAtIndex(2), "pan_instruction", "Big Boss",
            "Swipe across the screen to move the camera around the restaurant.", explaining,
            TutorialSystem.TutorialStepType.ManualContinue, TutorialSystem.TutorialAction.None,
            TutorialSystem.TutorialHintMode.None, null, true);
        ConfigureStep(steps.GetArrayElementAtIndex(3), "pan_practice", "Big Boss", string.Empty,
            explaining, TutorialSystem.TutorialStepType.WaitForGameplayAction,
            TutorialSystem.TutorialAction.CameraPanned, TutorialSystem.TutorialHintMode.Swipe,
            null, true);
        ConfigureStep(steps.GetArrayElementAtIndex(4), "pan_success", "Big Boss",
            "Great job! You can use that anytime to look around your restaurant.", success,
            TutorialSystem.TutorialStepType.ManualContinue, TutorialSystem.TutorialAction.None,
            TutorialSystem.TutorialHintMode.None, null, true);
        ConfigureStep(steps.GetArrayElementAtIndex(5), "interaction_introduction", "Big Boss",
            "Now let's try interacting with something. Just tap an object you want to use.", explaining,
            TutorialSystem.TutorialStepType.ManualContinue, TutorialSystem.TutorialAction.None,
            TutorialSystem.TutorialHintMode.None, null, true);
        ConfigureStep(steps.GetArrayElementAtIndex(6), "tap_practice", "Big Boss", string.Empty,
            explaining, TutorialSystem.TutorialStepType.WaitForGameplayAction,
            TutorialSystem.TutorialAction.TableInteracted, TutorialSystem.TutorialHintMode.Tap,
            interactionTarget, true);
        ConfigureStep(steps.GetArrayElementAtIndex(7), "tap_success", "Big Boss",
            "Nice! You're picking it up quickly.", success,
            TutorialSystem.TutorialStepType.ManualContinue, TutorialSystem.TutorialAction.None,
            TutorialSystem.TutorialHintMode.None, null, true);
        ConfigureStep(steps.GetArrayElementAtIndex(8), "interaction_summary", "Big Boss",
            "You'll use tapping to interact with customers, tables, equipment, and other important objects.", explaining,
            TutorialSystem.TutorialStepType.ManualContinue, TutorialSystem.TutorialAction.None,
            TutorialSystem.TutorialHintMode.None, null, true);
        ConfigureStep(steps.GetArrayElementAtIndex(9), "first_customer", "Big Boss",
            "Now let's help your first customer.", explaining,
            TutorialSystem.TutorialStepType.ManualContinue, TutorialSystem.TutorialAction.None,
            TutorialSystem.TutorialHintMode.None, null, false);
        tutorialSerialized.ApplyModifiedPropertiesWithoutUndo();

        dialogueRoot.gameObject.SetActive(true);
        indicator.gameObject.SetActive(false);
        handIndicator.gameObject.SetActive(false);
        EditorUtility.SetDirty(dialogueUI);
        EditorUtility.SetDirty(tutorial);
        EditorUtility.SetDirty(indicator);
        EditorUtility.SetDirty(handIndicator);
        EditorUtility.SetDirty(systemRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeGameObject = systemRoot;
        Debug.Log("[TutorialFoundationInstaller] Lobby1Tutorial foundation installed using the existing gameplay scene and TutorialDialogue HUD.");
    }

    private static void EnsureTutorialSceneAsset()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TutorialScenePath) != null)
            return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LegacyTutorialScenePath) != null)
        {
            string error = AssetDatabase.MoveAsset(LegacyTutorialScenePath, TutorialScenePath);
            if (!string.IsNullOrEmpty(error))
                throw new System.InvalidOperationException(error);
            return;
        }

        if (!AssetDatabase.CopyAsset(LobbyScenePath, TutorialScenePath))
            throw new System.InvalidOperationException("Could not duplicate Lobby1 as Lobby1Tutorial.");
    }

    private static Button EnsureNextButton(Transform dialogueBox, TMP_Text body)
    {
        Transform existing = dialogueBox.Find("NextButton");
        GameObject buttonObject;
        if (existing != null)
        {
            buttonObject = existing.gameObject;
        }
        else
        {
            buttonObject = new GameObject("NextButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.layer = dialogueBox.gameObject.layer;
            buttonObject.transform.SetParent(dialogueBox, false);
        }

        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-14f, -14f);
        rect.sizeDelta = new Vector2(92f, 34f);

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = new Color(0.08f, 0.56f, 0.84f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.18f, 0.68f, 0.95f, 1f);
        colors.pressedColor = new Color(0.05f, 0.40f, 0.65f, 1f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;

        TMP_Text label = buttonObject.transform.Find("Label")?.GetComponent<TMP_Text>();
        if (label == null)
        {
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.layer = buttonObject.layer;
            labelObject.transform.SetParent(buttonObject.transform, false);
            label = labelObject.GetComponent<TextMeshProUGUI>();
        }

        RectTransform labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.font = body.font;
        label.fontSize = 18f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = "NEXT";

        return button;
    }

    private static TutorialTargetIndicator EnsureTargetIndicator(Canvas canvas)
    {
        Transform existing = canvas.transform.Find("TutorialTargetIndicator");
        GameObject indicatorObject;
        if (existing != null)
        {
            indicatorObject = existing.gameObject;
        }
        else
        {
            indicatorObject = new GameObject("TutorialTargetIndicator", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            indicatorObject.layer = canvas.gameObject.layer;
            indicatorObject.transform.SetParent(canvas.transform, false);
        }

        RectTransform rect = (RectTransform)indicatorObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(110f, 110f);

        Image image = indicatorObject.GetComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.fillCenter = false;
        image.color = new Color(1f, 0.78f, 0.12f, 0.96f);
        image.raycastTarget = false;

        CanvasGroup group = indicatorObject.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        TutorialTargetIndicator indicator = GetOrAdd<TutorialTargetIndicator>(indicatorObject);
        SerializedObject serialized = new SerializedObject(indicator);
        serialized.FindProperty("indicatorRect").objectReferenceValue = rect;
        serialized.FindProperty("targetCanvas").objectReferenceValue = canvas;
        serialized.FindProperty("worldCamera").objectReferenceValue = Camera.main;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return indicator;
    }

    private static TutorialHandIndicator EnsureHandIndicator(Canvas canvas)
    {
        Transform existing = canvas.transform.Find("TutorialHandIndicator");
        GameObject handObject;
        if (existing != null)
        {
            handObject = existing.gameObject;
        }
        else
        {
            handObject = new GameObject("TutorialHandIndicator", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            handObject.layer = canvas.gameObject.layer;
            handObject.transform.SetParent(canvas.transform, false);
        }

        RectTransform rect = (RectTransform)handObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(118f, 118f);

        Image image = handObject.GetComponent<Image>();
        image.sprite = LoadPortrait("Hand.png");
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;

        CanvasGroup group = handObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        TutorialHandIndicator indicator = GetOrAdd<TutorialHandIndicator>(handObject);
        SerializedObject serialized = new SerializedObject(indicator);
        serialized.FindProperty("handRect").objectReferenceValue = rect;
        serialized.FindProperty("handImage").objectReferenceValue = image;
        serialized.FindProperty("canvasGroup").objectReferenceValue = group;
        serialized.FindProperty("targetCanvas").objectReferenceValue = canvas;
        serialized.FindProperty("worldCamera").objectReferenceValue = Camera.main;
        serialized.FindProperty("swipeSprite").objectReferenceValue = LoadPortrait("Hand.png");
        serialized.FindProperty("tapSprite").objectReferenceValue = LoadPortrait("Hand Click.png");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return indicator;
    }

    private static void ConfigureStep(
        SerializedProperty step,
        string id,
        string speaker,
        string message,
        Sprite portrait,
        TutorialSystem.TutorialStepType stepType,
        TutorialSystem.TutorialAction requiredAction,
        TutorialSystem.TutorialHintMode hintMode,
        Transform highlightTarget,
        bool restrictInteractions)
    {
        step.FindPropertyRelative("id").stringValue = id;
        step.FindPropertyRelative("speaker").stringValue = speaker;
        step.FindPropertyRelative("message").stringValue = message;
        step.FindPropertyRelative("portrait").objectReferenceValue = portrait;
        step.FindPropertyRelative("stepType").enumValueIndex = (int)stepType;
        step.FindPropertyRelative("requiredAction").enumValueIndex = (int)requiredAction;
        step.FindPropertyRelative("hintMode").enumValueIndex = (int)hintMode;
        step.FindPropertyRelative("highlightTarget").objectReferenceValue = highlightTarget;
        step.FindPropertyRelative("restrictUnrelatedInteractions").boolValue = restrictInteractions;
    }

    private static Transform FindTutorialBooth(Scene scene, Camera camera)
    {
        Booth best = null;
        float bestDistance = float.MaxValue;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Booth[] booths = root.GetComponentsInChildren<Booth>(true);
            for (int i = 0; i < booths.Length; i++)
            {
                Booth booth = booths[i];
                if (!booth.gameObject.activeInHierarchy ||
                    booth.GetComponentInChildren<Outline>(true) == null)
                    continue;

                float distance = 0f;
                if (camera != null)
                {
                    Vector3 viewport = camera.WorldToViewportPoint(booth.transform.position);
                    if (viewport.z <= 0f || viewport.x < 0.08f || viewport.x > 0.92f ||
                        viewport.y < 0.08f || viewport.y > 0.92f)
                        continue;
                    distance = ((Vector2)viewport - new Vector2(0.5f, 0.5f)).sqrMagnitude;
                }

                if (best == null || distance < bestDistance)
                {
                    best = booth;
                    bestDistance = distance;
                }
            }
        }

        return best != null ? best.transform : null;
    }

    private static Sprite LoadPortrait(string fileName)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitFolder + fileName);
        if (sprite == null)
            throw new MissingReferenceException("Tutorial portrait not found: " + fileName);
        return sprite;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static GameObject FindRoot(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
                return root;
        }

        return null;
    }

    private static T FindSceneObject<T>(Scene scene, string objectName) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name != objectName)
                    continue;

                T component = transforms[i].GetComponent<T>();
                if (component != null)
                    return component;
            }
        }

        return null;
    }
}
#endif
