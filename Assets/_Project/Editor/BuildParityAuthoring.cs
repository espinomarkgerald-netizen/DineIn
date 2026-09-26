#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>Explicit, non-destructive migration. Never runs on import or in a player.</summary>
public static class BuildParityAuthoring
{
    public const string ReadyPath = "Assets/_Project/Resources/UI/MultiplayerReadyPrompt.prefab";
    public const string SettingsPath = "Assets/_Project/Resources/UI/MobileUISettings.asset";

    [MenuItem("Dine In/Polish/Author Build Parity UI")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring.");
        if (AssetDatabase.LoadAssetAtPath<MobileUISettings>(SettingsPath) == null)
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<MobileUISettings>(), SettingsPath);
        AuthorPause(LobbyPauseMenuPrefabInstaller.PrefabPath);
        AuthorPause("Assets/_Project/Resources/UI/LobbyHUD.prefab");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ReadyPath) == null)
        {
            var helper = new GameObject("Readiness authoring"); helper.SetActive(false);
            GameObject prompt = null;
            try
            {
                prompt = helper.AddComponent<MultiplayerSessionUI>().AuthorPrompt();
                var view = prompt.GetComponent<MultiplayerReadyView>();
                var style = Resources.Load<MultiplayerReadyStyle>("UI/MultiplayerReadyStyle");
                var card = new GameObject("Team Card", typeof(RectTransform), typeof(Image));
                card.transform.SetParent(view.safeArea, false); card.transform.SetAsFirstSibling();
                var rect = card.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(.13f,.025f); rect.anchorMax = new Vector2(.87f,.93f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var image = card.GetComponent<Image>(); image.sprite = style.button; image.type = Image.Type.Sliced;
                image.color = new Color(.1f,.61f,.78f); image.raycastTarget = false;
                PrefabUtility.SaveAsPrefabAsset(prompt, ReadyPath);
            }
            finally { if (prompt != null) UnityEngine.Object.DestroyImmediate(prompt); UnityEngine.Object.DestroyImmediate(helper); }
        }
        // Keep the small runtime-selected outline shaders and their passes in both players.
        var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var included = graphics.FindProperty("m_AlwaysIncludedShaders");
        foreach (string shaderName in new[] { "Dine In/World Outline", "Custom/Outline Fill", "Custom/Outline Mask" })
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing shader: " + shaderName);
            bool exists = false;
            for (int i=0; i<included.arraySize; i++) exists |= included.GetArrayElementAtIndex(i).objectReferenceValue == shader;
            if (!exists) { included.InsertArrayElementAtIndex(included.arraySize); included.GetArrayElementAtIndex(included.arraySize-1).objectReferenceValue = shader; }
        }
        graphics.ApplyModifiedPropertiesWithoutUndo();
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets/Settings" }))
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
            // Unity maintains the local-fileID recovery map when serializing these subassets.
            var serialized = new SerializedObject(renderer);
            var map = serialized.FindProperty("m_RendererFeatureMap");
            if (map != null)
            {
                map.arraySize = renderer.rendererFeatures.Count;
                for (int i=0; i<map.arraySize; i++)
                {
                    var feature = renderer.rendererFeatures[i];
                    if (feature == null) throw new InvalidOperationException(renderer.name + " has a missing renderer feature.");
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long id);
                    map.GetArrayElementAtIndex(i).longValue = id;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            renderer.SetDirty(); EditorUtility.SetDirty(renderer);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Build parity] Authored pause, team readiness, mobile settings and outline references.");
    }

    private static void AuthorPause(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        var helper = new GameObject("Pause authoring"); helper.SetActive(false);
        try
        {
            var view = root.GetComponentInChildren<LobbyPauseMenuView>(true);
            var canvas = view.GetComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 32766;
            var scaler = view.GetComponent<CanvasScaler>(); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var window = (RectTransform)view.Overlay.transform.Find("PauseWindow");
            if (window.Find("SettingsContent") == null)
            {
                helper.AddComponent<LobbyPauseMenu>().AuthorSettings(view);
                window.sizeDelta = new Vector2(1400, 930);
                window.GetComponent<Image>().color = new Color(.09f,.60f,.78f,1);
                var content = (RectTransform)window.Find("SettingsContent"); content.sizeDelta = new Vector2(1320,560);
                SetRect(content.Find("AudioTitle"), new Vector2(-330,210), new Vector2(600,65));
                SetRect(content.Find("AccessibilityTitle"), new Vector2(330,210), new Vector2(600,65));
                SetRect(content.Find("MusicVolume"), new Vector2(-330,85), new Vector2(600,130));
                SetRect(content.Find("SfxVolume"), new Vector2(-330,-95), new Vector2(600,130));
                SetRect(content.Find("LargeTextButton"), new Vector2(330,95), new Vector2(600,110));
                SetRect(content.Find("ReducedMotionButton"), new Vector2(330,-45), new Vector2(600,110));
                SetRect(content.Find("HighContrastButton"), new Vector2(330,-185), new Vector2(600,110));
                SetRect(view.ResumeButton.transform, new Vector2(-330,-350), new Vector2(600,130));
                SetRect(view.GameMenuButton.transform, new Vector2(330,-350), new Vector2(600,130));
                view.ResumeButton.GetComponent<Image>().color = new Color(.05f,.69f,.43f);
                view.GameMenuButton.GetComponent<Image>().color = new Color(.87f,.12f,.25f);
                window.Find("Title").GetComponent<TMP_Text>().text = "TAKE A BREATHER";
                foreach (var text in window.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.fontSize = text.fontSizeMax = text.name == "Title" ? 58 : 34;
                    text.fontSizeMin = text.name == "Title" ? 40 : 28;
                    text.raycastTarget = false;
                }
                foreach (var slider in content.GetComponentsInChildren<Slider>(true))
                {
                    ((RectTransform)slider.transform).sizeDelta = new Vector2(330,40);
                    slider.handleRect.sizeDelta = new Vector2(56,76);
                }
                // These labels are useful previews; runtime binding reflects saved preferences.
                content.Find("LargeTextButton/Label").GetComponent<TMP_Text>().text = "LARGE TEXT   OFF";
                content.Find("ReducedMotionButton/Label").GetComponent<TMP_Text>().text = "REDUCED MOTION   OFF";
                content.Find("HighContrastButton/Label").GetComponent<TMP_Text>().text = "HIGH CONTRAST   OFF";
                view.ResumeButton.GetComponentInChildren<TMP_Text>().text = "BACK TO GAME";
                view.GameMenuButton.GetComponentInChildren<TMP_Text>().text = "LEAVE RESTAURANT";
            }
            view.Overlay.SetActive(false);
            AddSafeArea(canvas);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { UnityEngine.Object.DestroyImmediate(helper); PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void SetRect(Transform target, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)target; rect.anchoredPosition = position; rect.sizeDelta = size;
    }

    public static void AddSafeArea(Canvas canvas)
    {
        if (canvas.renderMode == RenderMode.WorldSpace || canvas.GetComponent<UIScreenSafeArea>() != null) return;
        var targets = canvas.transform.Cast<Transform>().OfType<RectTransform>()
            .Where(t => !t.name.Contains("Background") && !t.name.Contains("Backdrop") && !t.name.Contains("Shield")).ToArray();
        if (targets.Length == 0) return;
        canvas.gameObject.AddComponent<UIScreenSafeArea>().Capture(targets);
    }

    [MenuItem("Dine In/Polish/Author Safe Areas in Open Scene")]
    public static void AuthorOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas.isRootCanvas) AddSafeArea(canvas);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("Dine In/Polish/Author Kitchen Touch Layout")]
    public static void AuthorKitchen()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("Lobby2");
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open Lobby2 first.");
        var view = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FastFoodCookingView>(true)).First();
        var canvas = view.transform.Find("Fast Food Kitchen HUD").GetComponent<Canvas>();
        var station = canvas.transform.Find("Station");
        SetRect(station.Find("Stations"),new Vector2(-258,-24),new Vector2(210,144));
        SetRect(station.Find("Exit Kitchen"),new Vector2(-32,-24),new Vector2(210,144));
        SetRect(station.Find("Supplies ready"),new Vector2(-32,-200),new Vector2(210,144));
        SetRect(station.Find("Previous Station"),new Vector2(-200,-24),new Vector2(144,144));
        SetRect(station.Find("Next Station"),new Vector2(200,-24),new Vector2(144,144));
        SetRect(station.Find("Station Navigation Label"),new Vector2(0,-54),new Vector2(224,52));
        var header = (RectTransform)station.Find("Station Header");
        header.anchorMax = new Vector2(.33f,1); header.sizeDelta = new Vector2(0,164);
        SetRect(station.Find("Staff Activity Card"),new Vector2(156,-194),new Vector2(550,62));
        SetRect(station.Find("Live Orders"),new Vector2(32,-278),new Vector2(410,360));
        SetRect(station.Find("Restaurant Notifications"),new Vector2(-32,-360),new Vector2(460,340));
        SetRect(station.Find("Serve order"),new Vector2(0,210),new Vector2(340,144));
        SetRect(station.Find("Pointer guidance"),new Vector2(0,380),new Vector2(780,68));
        SetRect(station.Find("Discard Box"),new Vector2(-32,220),new Vector2(310,150));
        foreach (var button in canvas.GetComponentsInChildren<Button>(true))
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            { label.fontSize = label.fontSizeMax = 42; label.fontSizeMin = 28; label.enableAutoSizing = true; }
        foreach (var label in header.GetComponentsInChildren<TMP_Text>(true))
        { label.fontSize = label.fontSizeMax = 40; label.fontSizeMin = 30; label.enableAutoSizing = true; }
        AddSafeArea(canvas);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("Dine In/Polish/Save Build Scene UI Layouts")]
    public static void AuthorBuildScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var original = EditorSceneManager.GetSceneManagerSetup();
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
            if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save your scene edits before running the UI migration.");
        try
        {
            foreach(var entry in BuildReferenceIntegrityGuard.EffectiveScenes().Where(s=>s.enabled))
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(entry.path);
                if(!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(entry.path,OpenSceneMode.Additive);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
                AuthorOpenScene();
                if(scene.name == "Lobby2") AuthorKitchen();
                if(scene.name == "Lobby1 Multiplayer")
                {
                    // Retain the obsolete artwork for reference, but only the shared
                    // LobbyHUD is allowed to provide pause/settings in this scene.
                    foreach(var root in scene.GetRootGameObjects())
                    {
                        if(root.name == "CanvasGameMenu") root.SetActive(false);
                        if(root.GetComponent<SettingsManager>() != null) root.GetComponent<SettingsManager>().enabled = false;
                    }
                }
                if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + entry.path);
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(original); }
        AuthorSharedSafeAreas();
    }

    public static void AuthorSharedSafeAreas()
    {
        foreach(string path in new[]{"Assets/_Project/Resources/UI/LobbyHUD.prefab", "Assets/_Project/Resources/RestockFlow/RestockFlowHUD.prefab", "Assets/_Project/Resources/ManagerComplaints/ManagerComplaintSystem.prefab", "Assets/_Project/Resources/UI/CardPaymentUI.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var canvas in root.GetComponentsInChildren<Canvas>(true)) if(canvas.isRootCanvas) AddSafeArea(canvas);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }
}
#endif
