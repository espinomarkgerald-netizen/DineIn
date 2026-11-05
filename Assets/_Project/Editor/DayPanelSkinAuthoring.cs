using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>Explicit V2 authoring. No automatic scene, save, account or Play Mode changes.</summary>
public static class DayPanelSkinAuthoring
{
    public const string Pack = "Assets/_Project/UI/Assets/DayPanelSpritePackV2";
    private const string Fonts = Pack + "/Fonts";
    private const string HeadingSource = "Assets/_Project/UI/Assets/Fonts/Fonts/LilitaOne-Regular.ttf";
    private const string BodySource = Fonts + "/Sources/NunitoSans-Bold.ttf";
    private const string BoldSource = Fonts + "/Sources/NunitoSans-ExtraBold.ttf";
    private static readonly string[] Scenes = {
        "Assets/_Project/Scenes/RoleBased/Lobby1.unity", "Assets/_Project/Scenes/RoleBased/Lobby1 Multiplayer.unity",
        "Assets/_Project/Scenes/RoleBased/Lobby2.unity", "Assets/_Project/Scenes/RoleBased/Lobby2 Multiplayer.unity",
        "Assets/_Project/Scenes/RoleBased/Lobby3.unity", "Assets/_Project/Scenes/RoleBased/Lobby3 Multiplayer.unity",
        "Assets/_Project/Scenes/TutorialScenes/Lobby1Tutorial.unity", "Assets/_Project/Scenes/RoleBased/Lobby2Tutorial.unity",
        "Assets/_Project/Scenes/RoleBased/Lobby3Tutorial.unity", "Assets/_Project/Scenes/RoleBased/Multiplayer.unity" };
    private static TMP_FontAsset heading, body;
    private static Sprite[] sprites;

    [MenuItem("Dine In/UI/Day Panels/Apply V2 to Loaded Scenes")]
    public static void ApplyLoadedScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        PrepareAssets();
        foreach (GameDayManager manager in UnityEngine.Object.FindObjectsByType<GameDayManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Apply(manager);
    }

    [MenuItem("Dine In/UI/Day Panels/Apply V2 to Restaurant Scene Assets")]
    public static void ApplySceneAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        PrepareAssets();
        Scene active = SceneManager.GetActiveScene();
        var report = new List<string>();
        try
        {
            foreach (string path in Scenes)
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool loaded = scene.IsValid() && scene.isLoaded;
                if (loaded && scene.isDirty) { report.Add("SKIPPED dirty loaded scene: " + path); continue; }
                if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    GameDayManager[] managers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameDayManager>(true)).ToArray();
                    if (managers.Length != 1) throw new InvalidOperationException(path + " must contain exactly one GameDayManager.");
                    Apply(managers[0]);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
                    report.Add("AUTHORED " + path);
                }
                finally { if (!loaded && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
            }
        }
        finally
        {
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            WriteReport("scene-authoring.txt", string.Join("\n", report));
        }
        if (report.Any(line => line.StartsWith("SKIPPED", StringComparison.Ordinal)))
            throw new InvalidOperationException("Unsaved scene work was preserved; see Temp/DayPanelV2/scene-authoring.txt for skipped paths.");
    }

    public static void Apply(GameDayManager manager)
    {
        if (manager == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        PrepareAssets();
        var so = new SerializedObject(manager);
        GameObject intro = Ref<GameObject>(so, "dayIntroPanel"), results = Ref<GameObject>(so, "resultsPanel");
        TMP_Text introTitle = Ref<TMP_Text>(so, "dayIntroTitleText"), resultTitle = Ref<TMP_Text>(so, "resultsTitleText");
        Button play = Ref<Button>(so, "playButton"), action = Ref<Button>(so, "resultsActionButton");
        if (intro == null || results == null || introTitle == null || resultTitle == null || play == null || action == null)
            throw new InvalidOperationException("V2 authoring needs the existing modal roots, titles and action buttons.");
        int playId = play.GetInstanceID(), actionId = action.GetInstanceID();
        int playListeners = play.onClick.GetPersistentEventCount(), actionListeners = action.onClick.GetPersistentEventCount();
        Undo.RecordObject(manager, "Apply V2 day panels");
        DayPanelView view = manager.GetComponent<DayPanelView>();
        if (view == null) view = Undo.AddComponent<DayPanelView>(manager.gameObject);
        view.Author(introTitle.transform.parent as RectTransform, resultTitle.transform.parent as RectTransform, play, action, heading, body, sprites);
        Set(so, "dayPanelV2View", view);
        Set(so, "dayIntroSurface", view.IntroSurface);
        Set(so, "resultsSurface", view.ResultsSurface);
        SetArray(so, "dayIntroRevealRoots", view.IntroSections);
        SetArray(so, "resultsRevealRoots", view.ResultsSections);
        for (int i = 0; i < 3; i++) Set(so, "star" + (i + 1), view.EarnedStars[i]);
        Set(so, "dayPanelSecondaryButtonSprite", sprites[Array.IndexOf(DayPanelView.SpriteNames, "SecondaryButton")]);
        SerializedProperty enabled = so.FindProperty("styledDayPanels");
        if (enabled != null) enabled.boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        Dimmer(intro); Dimmer(results);
        if (play.GetInstanceID() != playId || action.GetInstanceID() != actionId || play.onClick.GetPersistentEventCount() != playListeners || action.onClick.GetPersistentEventCount() != actionListeners)
            throw new InvalidOperationException("V2 authoring replaced an existing action or listener.");
        EditorUtility.SetDirty(view); EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
    }

    [MenuItem("Dine In/UI/Day Panels/Validate Saved Scene Assets")]
    private static void ValidateSceneAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene active = SceneManager.GetActiveScene();var report = new List<string>();
        try
        {
            foreach (string path in Scenes)
            {
                Scene scene = SceneManager.GetSceneByPath(path);bool loaded = scene.IsValid() && scene.isLoaded;
                if (loaded && scene.isDirty) throw new InvalidOperationException("Preserving dirty scene " + path);
                if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var managers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameDayManager>(true)).ToArray();
                    if (managers.Length != 1) throw new InvalidOperationException("Expected one original day controller: " + path);
                    var so = new SerializedObject(managers[0]);var view = Ref<DayPanelView>(so,"dayPanelV2View");
                    if (view == null || view != managers[0].GetComponent<DayPanelView>() || !so.FindProperty("styledDayPanels").boolValue)
                        throw new InvalidOperationException("Missing V2 view: " + path);
                    if (MonoScript.FromMonoBehaviour(managers[0]).GetClass() != typeof(GameDayManager))
                        throw new InvalidOperationException("Original GameDayManager script unresolved: " + path);
                    if (view.IntroSurface == null || view.ResultsSurface == null || view.IntroSections.Length != 3 || view.ResultsSections.Length != 3 || view.EarnedStars.Length != 3)
                        throw new InvalidOperationException("Missing layout binding: " + path);
                    foreach (var button in new[] { Ref<Button>(so,"playButton"), Ref<Button>(so,"resultsActionButton") })
                        if (button == null || button.targetGraphic == null || !button.targetGraphic.raycastTarget)
                            throw new InvalidOperationException("Missing usable original action: " + path);
                    foreach (var surface in new[] { view.IntroSurface, view.ResultsSurface })
                    {
                        for (var frame=surface.parent as RectTransform;frame!=null&&frame.GetComponent<Canvas>()==null;frame=frame.parent as RectTransform)
                            if (frame.anchorMin!=Vector2.zero||frame.anchorMax!=Vector2.one||frame.offsetMin!=Vector2.zero||frame.offsetMax!=Vector2.zero||frame.localScale!=Vector3.one)
                                throw new InvalidOperationException("Constrained modal ancestor " + frame.name + ": " + path);
                        if (surface.GetComponentsInChildren<Image>(true).Any(image => image.sprite == null && image.name != "Report Viewport" && !image.name.StartsWith("Divider ",StringComparison.Ordinal)))
                            throw new InvalidOperationException("Missing production sprite: " + path);
                        if (surface.GetComponentsInChildren<TMP_Text>(true).Any(text => text.font == null || !AssetDatabase.GetAssetPath(text.font).StartsWith(Fonts + "/", StringComparison.Ordinal)))
                            throw new InvalidOperationException("Missing dedicated font: " + path);
                        if (surface.GetComponentsInChildren<Transform>(true).Any(child => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0))
                            throw new InvalidOperationException("Missing visual script: " + path);
                    }
                    ScrollRect scroll = view.ResultsSurface.GetComponentInChildren<ScrollRect>(true);
                    if (scroll == null || scroll.viewport == null || scroll.content == null || scroll.verticalScrollbar == null || !scroll.verticalScrollbar.targetGraphic.raycastTarget)
                        throw new InvalidOperationException("Missing report scrolling: " + path);
                    report.Add("PASS " + path + " | original controller/actions, dedicated fonts, sprites, stars and scroll references");
                }
                finally { if (!loaded && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene,true); }
            }
        }
        finally { if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);WriteReport("scene-validation.txt",string.Join("\n",report)); }
    }

    public static void PrepareAssets(bool force = false)
    {
        if (!force && sprites != null && heading != null && body != null) return;
        EnsureFolder(Fonts);
        foreach (string name in DayPanelView.SpriteNames)
        {
            string path = Pack + "/Components/" + name + ".png";
            if (!File.Exists(path)) throw new FileNotFoundException("Missing V2 production sprite", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) throw new InvalidOperationException("No texture importer " + path);
            if (imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single || imp.mipmapEnabled || !imp.alphaIsTransparency || imp.textureCompression != TextureImporterCompression.Uncompressed)
            {
                imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false; imp.alphaIsTransparency = true; imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.filterMode = FilterMode.Bilinear; imp.maxTextureSize = 4096; imp.SaveAndReimport();
            }
        }
        sprites = DayPanelView.SpriteNames.Select(name => AssetDatabase.LoadAssetAtPath<Sprite>(Pack + "/Components/" + name + ".png")).ToArray();
        if (sprites.Any(s => s == null)) throw new InvalidOperationException("A V2 PNG is not a single Sprite.");
        body = EnsureFont("DayPanelBody", BodySource, true, false);
        TMP_FontAsset bold = EnsureFont("DayPanelBodyBold", BoldSource, true, true);
        var weights = body.fontWeightTable; weights[7].regularTypeface = bold;
        heading = EnsureFont("DayPanelHeading", HeadingSource, false, false);
        heading.fallbackFontAssetTable = new List<TMP_FontAsset> { body };
        EditorUtility.SetDirty(body); EditorUtility.SetDirty(heading);
        AssetDatabase.SaveAssetIfDirty(body); AssetDatabase.SaveAssetIfDirty(heading);
        WriteReport("assets.txt", "Sprites: " + sprites.Length + "\nHeading: " + AssetDatabase.GetAssetPath(heading) + "\nBody: " + AssetDatabase.GetAssetPath(body) +
            "\nCurrency/punctuation: " + string.Join(", ", "₱–•…".Select(c => c + "=" + body.HasCharacter(c, true))));
    }

    private static TMP_FontAsset EnsureFont(string name, string sourcePath, bool currency, bool bold)
    {
        string path = Fonts + "/" + name + ".asset";
        TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        UnityEngine.Font source = AssetDatabase.LoadAssetAtPath<UnityEngine.Font>(sourcePath);
        if (source == null) throw new FileNotFoundException("Missing local font source", sourcePath);
        if (asset != null && asset.sourceFontFile != source) { AssetDatabase.DeleteAsset(path); asset = null; }
        if (asset == null)
        {
            asset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            asset.name = name; asset.normalStyle = 0;
            AssetDatabase.CreateAsset(asset, path);
        }
        string chars = new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray()) + "–•…" + (currency ? "₱" : string.Empty);
        string needed = new string(chars.Where(c => !asset.HasCharacter(c)).Distinct().ToArray());
        if (needed.Length > 0) asset.TryAddCharacters(needed, out _);
        string missing = new string(chars.Where(c => !asset.HasCharacter(c)).Distinct().ToArray());
        if (missing.Length > 0) throw new InvalidOperationException(name + " missing glyphs " + missing);
        asset.material.name = name + " Material";
        asset.material.SetFloat(ShaderUtilities.ID_OutlineWidth, currency ? 0 : .10f);
        asset.material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(.035f, .075f, .15f, 1));
        if (!AssetDatabase.Contains(asset.material)) AssetDatabase.AddObjectToAsset(asset.material, asset);
        foreach (Texture2D texture in asset.atlasTextures)
            if (texture != null && !AssetDatabase.Contains(texture)) { texture.name = name + " Atlas"; AssetDatabase.AddObjectToAsset(texture, asset); }
        EditorUtility.SetDirty(asset); EditorUtility.SetDirty(asset.material); AssetDatabase.SaveAssetIfDirty(asset);
        return asset;
    }

    private static T Ref<T>(SerializedObject so, string field) where T : UnityEngine.Object => so.FindProperty(field)?.objectReferenceValue as T;
    private static void Set(SerializedObject so, string field, UnityEngine.Object value)
    { SerializedProperty p = so.FindProperty(field); if (p != null) p.objectReferenceValue = value; }
    private static void SetArray(SerializedObject so, string field, RectTransform[] values)
    { SerializedProperty p = so.FindProperty(field); if (p == null) return; p.arraySize = values.Length; for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; }
    private static void Dimmer(GameObject panel)
    { Image image = panel.GetComponent<Image>(); if (image == null) return; Undo.RecordObject(image,"Style V2 dimmer"); image.color = new Color(.025f,.055f,.12f,.48f); image.raycastTarget = true; EditorUtility.SetDirty(image); }
    private static void EnsureFolder(string path)
    { if (AssetDatabase.IsValidFolder(path)) return; string parent = Path.GetDirectoryName(path).Replace('\\','/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
    private static void WriteReport(string name, string content)
    { Directory.CreateDirectory("Temp/DayPanelV2"); File.WriteAllText("Temp/DayPanelV2/" + name, content); }

    [MenuItem("Dine In/UI/Day Panels/Render Acceptance Previews")]
    private static void RenderAcceptance()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        PrepareAssets();
        var report = new List<string>();
        RenderCase("intro-landscape", 1920, 1080, false, true, "normal", report);
        RenderCase("report-landscape", 1920, 1080, false, false, "normal", report);
        RenderCase("report-zero", 1920, 1080, false, false, "zero", report);
        RenderCase("report-one-star", 1920, 1080, false, false, "one", report);
        RenderCase("report-large-values", 1920, 1080, false, false, "large", report);
        RenderCase("report-failure", 1920, 1080, false, false, "failure", report);
        RenderCase("report-victory", 1920, 1080, false, false, "victory", report);
        RenderCase("intro-portrait", 1080, 1920, true, true, "normal", report);
        RenderCase("report-portrait", 1080, 1920, true, false, "normal", report);
        RenderCase("report-portrait-large-text", 1080, 1920, true, false, "normal", report, true);
        RenderCase("report-portrait-bottom", 1080, 1920, true, false, "normal", report, false, true);
        RenderCase("report-portrait-failure", 1080, 1920, true, false, "failure", report, true);
        RenderCase("report-recovery-prompt", 1920, 1080, false, false, "recovery", report);
        RenderCase("intro-long-hours", 1920, 1080, false, true, "large", report, true);
        WriteReport("render-validation.txt", string.Join("\n", report));
        if (report.Any(line => line.StartsWith("FAIL", StringComparison.Ordinal)))
            throw new InvalidOperationException("V2 rendered validation found issues; see Temp/DayPanelV2/render-validation.txt.");
    }

    private static DayPanelReportSnapshot PreviewReport(string mode)
    {
        bool zero = mode == "zero", large = mode == "large", failed = mode == "failure", victory = mode == "victory";
        return new DayPanelReportSnapshot(failed ? "Restaurant Bankrupt" : victory ? "Earth Is Safe!" : "Day 2 Report",
            failed ? "RUN ENDED" : victory ? "CAMPAIGN COMPLETE" : zero ? "QUIET SHIFT" : "GOOD SERVICE",
            large ? "Keep counters staffed, meals available and tables clean to reduce waits." : "Shorten waits to turn neutral guests happy.",
            failed ? "<b>Restart free:</b> Day 1 and 30% approval. Staff, stock, money, purchases and unlocks reset; your restaurant stays.\n<b>Continue • 500 GC:</b> Keep this day, restaurant and unlocks." :
                victory ? "Continue your restaurant in Endless Service." : string.Empty,
            failed ? "Funds reached ₱0 after expenses." : victory ? "Day 30 completed with enough approval." : string.Empty,
            "A", victory ? 30 : 2, zero ? 0 : large ? 123456789 : 6850, large ? 98765432 : 6500,
            zero ? 0 : large ? 123456 : 120, zero ? 0 : large ? 999 : 0,
            zero ? 0 : large ? 12345 : 19, zero ? 0 : large ? 12344 : 18,
            zero ? 0 : 14, zero ? 0 : 3, zero ? 0 : 1, 75,
            zero ? 0 : 4, zero ? 0 : 4, 0, zero ? 0 : mode == "one" ? 1 : victory ? 3 : 2,
            !zero, !zero, failed || victory, victory);
    }

    private static void RenderCase(string name, int width, int height, bool portrait, bool intro, string mode, List<string> report, bool largeText = false, bool scrollToBottom = false)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        Texture2D readback = null;
        RenderTexture previousTarget = RenderTexture.active;
        try
        {
            var cameraObject = new GameObject("V2 offscreen UI camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = height * .5f;
            camera.aspect = width / (float)height;
            camera.transform.position = new Vector3(0, 0, -100);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .08f, .12f, 1);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 200;
            camera.cullingMask = 1 << 31;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            var canvasObject = new GameObject("V2 offscreen canvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            canvasObject.layer = 31;
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            RectTransform canvasRect = canvasObject.transform as RectTransform;
            canvasRect.sizeDelta = new Vector2(width, height);
            // Reproduce the production ancestry, including the old fixed-size modal wrapper.
            RectTransform introWrapper = PreviewRect(canvasRect, "DayIntro");
            introWrapper.anchorMin=introWrapper.anchorMax=new Vector2(.5f,0);
            introWrapper.sizeDelta=new Vector2(100,100);introWrapper.anchoredPosition=new Vector2(0,227);
            RectTransform introHost = PreviewRect(PreviewRect(introWrapper,"DayIntroPanel"), "Legacy intro host");
            RectTransform resultHost = PreviewRect(PreviewRect(PreviewRect(canvasRect,"Results root"),"Results panel"), "Legacy result host");
            Button play = PreviewButton(introHost, "Play");
            Button action = PreviewButton(resultHost, "Next day");
            var owner = new GameObject("V2 isolated view", typeof(DayPanelView));
            SceneManager.MoveGameObjectToScene(owner, scene);
            owner.transform.SetParent(canvasObject.transform, false);
            DayPanelView view = owner.GetComponent<DayPanelView>();
            view.Author(introHost, resultHost, play, action, heading, body, sprites);
            view.SetPreviewViewport(width, height);
            play.GetComponentInChildren<TMP_Text>(true).text = "PLAY";
            action.GetComponentInChildren<TMP_Text>(true).text = mode == "failure" ? "RESTART DAY 1" : mode == "victory" ? "CONTINUE ENDLESS" : "NEXT DAY";
            view.BindIntro(mode == "large" ? new DayPanelIntroSnapshot("Day 120", "11 AM–11 PM", 123456789, 999, 99, 45) : new DayPanelIntroSnapshot("Day 2", "5–10 PM", 6500, 19, 2, 60));
            DayPanelReportSnapshot snapshot = PreviewReport(mode);
            view.BindReport(snapshot);
            if (mode == "failure")
            {
                Button paid = UnityEngine.Object.Instantiate(action, resultHost);
                paid.name = "V2 isolated paid Continue";
                paid.GetComponentInChildren<TMP_Text>(true).text = "CONTINUE • 500 GC";
                view.LayoutActions(action, paid);
            }
            view.Prepare(true, true, 0);
            view.Finish(true);
            view.Prepare(false, true, snapshot.EarnedStars);
            view.Finish(false);
            view.RefreshLayout(portrait, largeText);
            foreach (var surface in new[] { view.IntroSurface, view.ResultsSurface })
            {
                float expectedFit=Mathf.Min(width*.95f/surface.rect.width,height*.95f/surface.rect.height);
                if (Mathf.Abs(surface.localScale.x-expectedFit)>.001f)
                    report.Add("FAIL " + name + " modal fitted against a constrained ancestor");
            }
            VerifyPresentationReset(view, snapshot, name, report);
            if (mode == "recovery")
            {
                string revenue = view.ResultsSurface.Find("Report Viewport/Cards/Card 0/Revenue").GetComponent<TMP_Text>().text;
                view.SetNotice("Continue needs 500 GC. Your wallet balance is too low. Open the store, then return here to try again. Your results remain available above.");
                if (view.ResultsSurface.Find("Report Viewport/Cards/Card 0/Revenue").GetComponent<TMP_Text>().text != revenue)
                    report.Add("FAIL " + name + " recovery prompt changed revenue");
            }
            introHost.gameObject.SetActive(intro);
            resultHost.gameObject.SetActive(!intro);
            foreach (Transform child in canvasObject.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            Canvas.ForceUpdateCanvases();
            if (scrollToBottom)
            {
                ScrollRect scroll = view.ResultsSurface.GetComponentInChildren<ScrollRect>(true);
                scroll.verticalNormalizedPosition = 0;
                Canvas.ForceUpdateCanvases();
                if (!scroll.vertical || scroll.content.anchoredPosition.y <= 0 || scroll.verticalScrollbar == null)
                    report.Add("FAIL " + name + " stacked cards cannot scroll to the final card");
            }
            foreach (TMP_Text text in canvasObject.GetComponentsInChildren<TMP_Text>(false))
            {
                text.ForceMeshUpdate(true, true);
                if (text.isTextOverflowing)
                    report.Add("FAIL " + name + " text overflow: " + text.name + " = " + text.text.Replace('\n', '|'));
                if (text.textWrappingMode == TextWrappingModes.NoWrap && text.textInfo.lineCount > 1)
                    report.Add("FAIL " + name + " atomic value wrapped: " + text.name);
            }
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "V2 acceptance render" };
            target.Create();
            camera.targetTexture = target;
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
            {
                var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target, mipLevel = 0, slice = 0, face = CubemapFace.Unknown };
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else camera.Render();
            RenderTexture.active = target;
            readback = new Texture2D(width, height, TextureFormat.RGBA32, false);
            readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readback.Apply();
            Directory.CreateDirectory("Temp/DayPanelV2");
            File.WriteAllBytes("Temp/DayPanelV2/" + name + ".png", readback.EncodeToPNG());
            report.Add("RENDERED " + name + " " + width + "x" + height);
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    private static void VerifyPresentationReset(DayPanelView view, DayPanelReportSnapshot snapshot, string name, List<string> report)
    {
        // Exercise a cancelled partial reveal, then the same immediate-final path used by restores/reduced motion.
        view.Prepare(false, false, snapshot.EarnedStars);
        view.SetMetricProgress(.37f);view.SetSectionProgress(false, 1, .25f);view.SetStarProgress(0, .4f);
        view.Finish(false);
        view.Prepare(false, true, snapshot.EarnedStars);view.Finish(false);
        if (view.EarnedStars.Count(star => star.gameObject.activeSelf) != snapshot.EarnedStars)
            report.Add("FAIL " + name + " final earned-star count");
        foreach (RectTransform card in view.ResultsSections)
            if (card.localScale != Vector3.one || card.GetComponent<CanvasGroup>().alpha != 1)
                report.Add("FAIL " + name + " interrupted card did not settle");
        foreach (Image star in view.EarnedStars)
            if (star.rectTransform.localScale != Vector3.one || star.rectTransform.localRotation != Quaternion.identity)
                report.Add("FAIL " + name + " interrupted star did not settle");
        string revenue = view.ResultsSurface.Find("Report Viewport/Cards/Card 0/Revenue").GetComponent<TMP_Text>().text;
        if (revenue != "₱" + snapshot.Revenue.ToString("N0", System.Globalization.CultureInfo.InvariantCulture))
            report.Add("FAIL " + name + " final numeric reveal");
        report.Add("CHECKED " + name + " interrupted/immediate-final presentation and " + snapshot.EarnedStars + " earned stars");
    }

    private static RectTransform PreviewRect(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.transform as RectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Button PreviewButton(RectTransform parent, string name)
    {
        RectTransform rect = PreviewRect(parent, name);
        Image image = rect.gameObject.AddComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        RectTransform labelRect = PreviewRect(rect, "Label");
        labelRect.gameObject.AddComponent<TextMeshProUGUI>().text = name;
        return button;
    }
}
