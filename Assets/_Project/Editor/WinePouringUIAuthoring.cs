#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>Explicit wine UI authoring only. Never opens or saves a scene.</summary>
public static class WinePouringUIAuthoring
{
    public const string PrefabPath = "Assets/_Project/Resources/UI/WinePouringUI.prefab";
    private const string Art = "Assets/_Project/Art/Icons/GameIcons/WinePouring/";
    private const string Buttons = "Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/";
    private const string CardPrefab = "Assets/_Project/Resources/UI/CardPaymentUI.prefab";

    public static string Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Author the wine UI outside Play Mode.");
        ExportArtwork();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            return "Wine UI already exists; preserved its Inspector edits.";
        GameObject card = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefab);
        if (card == null || card.GetComponent<UnityEngine.UI.RawImage>() == null || card.GetComponent<Scroller>() == null)
            throw new InvalidOperationException("The exact Card Payment background is missing.");
        var root = new GameObject("WinePouringUI", typeof(RectTransform), typeof(CanvasGroup));
        try
        {
            root.layer = LayerMask.NameToLayer("UI");
            Fill(root.GetComponent<RectTransform>());
            var background = root.AddComponent<UnityEngine.UI.RawImage>();
            EditorUtility.CopySerialized(card.GetComponent<UnityEngine.UI.RawImage>(), background);
            background.raycastTarget = true;
            var scroller = root.AddComponent<Scroller>();
            EditorUtility.CopySerialized(card.GetComponent<Scroller>(), scroller);
            var scrollSettings = new SerializedObject(scroller);
            scrollSettings.FindProperty("img").objectReferenceValue = background;
            scrollSettings.ApplyModifiedPropertiesWithoutUndo();
            var ui = root.AddComponent<WinePouringUI>();
            ui.panelGroup = root.GetComponent<CanvasGroup>();
            ui.safeArea = Rect("SafeArea", root.transform, Vector2.zero, Vector2.zero);
            Fill(ui.safeArea);
            ui.stage = Rect("PouringStage", ui.safeArea, Vector2.zero, ui.authoredStageSize);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/_Project/UI/Assets/Fonts/Anton/Anton-Regular SDF.asset");

            var title = Label("Title", ui.safeArea, "WINE REFILL", font, 48f);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            title.rectTransform.sizeDelta = new Vector2(650f, 75f);
            ui.instruction = Label("Instruction", ui.safeArea, "Drag the bottle over the glass", font, 34f);
            ui.instruction.rectTransform.anchorMin = ui.instruction.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            ui.instruction.rectTransform.pivot = new Vector2(0.5f, 0f);
            ui.instruction.rectTransform.anchoredPosition = new Vector2(0f, 16f);
            ui.instruction.rectTransform.sizeDelta = new Vector2(790f, 64f);

            ui.liquid = Picture("WineFill", ui.stage, Load("WineFill"), new Vector2(-310f, -18f), new Vector2(270f, 236f));
            ui.liquid.type = UnityEngine.UI.Image.Type.Filled;
            ui.liquid.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
            ui.liquid.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Bottom;
            ui.liquid.fillAmount = ui.initialFill;
            ui.stream = Picture("WineStream", ui.stage, Load("WineStream"), Vector2.zero, new Vector2(20f, 200f));
            ui.splash = Picture("WineSplash", ui.stage, Load("SplashA"), Vector2.zero, new Vector2(64f, 46f));
            ui.splashFrames = new[] { Load("SplashA"), Load("SplashB"), Load("SplashC") };
            var glass = Picture("WineGlass", ui.stage, Load("WineGlass"), new Vector2(-310f, -45f), new Vector2(329.5f, 500f));
            glass.preserveAspect = true;
            ui.glassOpening = Rect("GlassOpening", ui.stage, new Vector2(-310f, 173f), new Vector2(220f, 70f));
            ui.targetMarker = Picture("TargetFillMarker", ui.stage, Load("TargetMarker"), new Vector2(-310f, 49f),
                new Vector2(280f, 50f)).rectTransform;
            var bottleImage = Picture("WineBottle", ui.stage, Load("WineBottle"), new Vector2(375f, -155f), new Vector2(170.2f, 480f));
            bottleImage.raycastTarget = true;
            ui.bottle = bottleImage.rectTransform;
            ui.bottle.pivot = new Vector2(0.5f, 0.35f);
            ui.bottleMouth = Rect("BottleMouth", ui.bottle, Vector2.zero, new Vector2(24f, 16f));
            ui.bottleMouth.anchorMin = ui.bottleMouth.anchorMax = new Vector2(0.5f, 0.966f);
            ui.bottle.gameObject.AddComponent<WinePouringBottleDrag>().controller = ui;
            ui.successMark = Picture("SuccessCheck", ui.stage, AssetDatabase.LoadAssetAtPath<Sprite>(
                Buttons + "Green/Double/icon_checkmark.png"), new Vector2(-310f, 280f), new Vector2(95f, 95f)).rectTransform;
            ui.successMark.gameObject.SetActive(false);
            ui.stream.gameObject.SetActive(false);
            ui.splash.gameObject.SetActive(false);

            var backImage = Picture("Back", ui.safeArea, AssetDatabase.LoadAssetAtPath<Sprite>(
                Buttons + "Blue/Double/button_rectangle_gloss.png"), Vector2.zero, new Vector2(210f, 76f));
            backImage.type = UnityEngine.UI.Image.Type.Sliced;
            backImage.raycastTarget = true;
            backImage.rectTransform.anchorMin = backImage.rectTransform.anchorMax = Vector2.zero;
            backImage.rectTransform.pivot = Vector2.zero;
            backImage.rectTransform.anchoredPosition = new Vector2(16f, 12f);
            ui.backButton = backImage.gameObject.AddComponent<UnityEngine.UI.Button>();
            ui.backButton.targetGraphic = backImage;
            var navigation = ui.backButton.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.None;
            ui.backButton.navigation = navigation;
            var backLabel = Label("Label", backImage.transform, "Back", font, 30f);
            backLabel.color = Color.white;
            Fill(backLabel.rectTransform);
            ui.feedbackAudio = root.AddComponent<AudioSource>();
            ui.feedbackAudio.playOnAwake = false;
            ui.feedbackAudio.spatialBlend = 0f;
            ui.successSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Restaurant/CookingAssets/Feedback/Dish Complete.wav");
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/_Project/Audio/AudioMixer.mixer");
            if (mixer != null)
            {
                var groups = mixer.FindMatchingGroups("SFX");
                if (groups.Length > 0) ui.feedbackAudio.outputAudioMixerGroup = groups[0];
            }
            root.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return "Created WinePouringUI.prefab with exact Card Payment background and transparent wine artwork.";
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    public static string RenderPreview(int width, int height, float amount = 0.05f, bool pouring = false)
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var target = new RenderTexture(width, height, 24);
        var oldTarget = RenderTexture.active;
        var canvasRoot = new GameObject("Wine UI Preview", typeof(RectTransform), typeof(Canvas));
        try
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasRoot, scene);
            var cameraRoot = new GameObject("Wine Preview Camera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraRoot, scene);
            var camera = cameraRoot.GetComponent<Camera>();
            camera.scene = scene;
            camera.targetTexture = target;
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.planeDistance = 1f;
            canvas.worldCamera = camera;
            float scale = Mathf.Sqrt(width / 1920f * height / 1080f);
            Vector2 size = new Vector2(width / scale, height / scale);
            var scaler = canvasRoot.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f,1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), canvasRoot.transform, false);
            root.SetActive(true);
            var ui = root.GetComponent<WinePouringUI>();
            ui.panelGroup.alpha = 1f;
            ui.safeArea.offsetMin = Vector2.one * ui.safeAreaPadding;
            ui.safeArea.offsetMax = -Vector2.one * ui.safeAreaPadding;
            Canvas.ForceUpdateCanvases();
            ui.stage.localScale = Vector3.one * Mathf.Min((size.x - 64f) / ui.authoredStageSize.x, (size.y - 204f) / ui.authoredStageSize.y);
            ui.liquid.fillAmount = amount;
            Rect liquidBounds = ui.liquid.rectTransform.rect;
            Vector3 markerWorld = ui.liquid.rectTransform.TransformPoint(new Vector3(0f, Mathf.Lerp(liquidBounds.yMin, liquidBounds.yMax, ui.targetFill), 0f));
            ui.targetMarker.localPosition = ui.stage.InverseTransformPoint(markerWorld);
            if (pouring)
            {
                Quaternion tilt = Quaternion.Euler(0f, 0f, ui.pourTiltDegrees);
                Vector2 mouthOffset = ui.bottle.InverseTransformPoint(ui.bottleMouth.position);
                Vector2 opening = ui.stage.InverseTransformPoint(ui.glassOpening.position);
                ui.bottle.localPosition = opening + ui.pourMouthOffset - (Vector2)(tilt * mouthOffset);
                ui.bottle.localRotation = tilt;
                ui.stream.gameObject.SetActive(true);
                ui.splash.gameObject.SetActive(true);
                var animate = typeof(WinePouringUI).GetMethod("AnimateStream", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                typeof(WinePouringUI).GetField("fill", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(ui, amount);
                animate.Invoke(ui, new object[] { (Vector2)ui.stage.InverseTransformPoint(ui.bottleMouth.position), opening, 0.1f });
                ui.instruction.text = "Hold to fill";
            }
            camera.orthographic = true;
            camera.orthographicSize = size.y * 0.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2000f;
            camera.transform.position = new Vector3(0f, 0f, -1000f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0,0,width,height),0,0);
            result.Apply();
            string path = "Temp/WinePouringReview/wine-" + width + "x" + height + (pouring ? "-pouring" : "-idle") + ".png";
            Directory.CreateDirectory("Temp/WinePouringReview");
            File.WriteAllBytes(path, result.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(result);
            return path;
        }
        finally { RenderTexture.active = oldTarget; UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(target); }
    }

    public static string RuntimeCheckResult { get; private set; } = "NOT RUN";
    private static WinePouringUI checkedUI;
    private static UnityEngine.EventSystems.PointerEventData checkedPointer;
    private static int checkPhase;
    private static int checkSuccesses;
    private static int checkCancels;
    private static float checkReleasedFill;
    private static float checkInitialFill;
    private static double checkDeadline;
    private static double touchStarted;

    /// <summary>Runs real Update/coroutine pointer checks only inside the caller's save-suspended Play Mode session.</summary>
    public static string StartRuntimeCheck()
    {
        if (!Application.isPlaying || !GameSaveManager.IsPersistenceSuspended)
            throw new InvalidOperationException("Wine runtime check requires Play Mode with persistence suspended.");
        if (checkPhase != 0) throw new InvalidOperationException("Wine runtime check is already running.");
        checkedUI = null;
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<WinePouringUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (candidate.gameObject.scene.IsValid() && candidate.gameObject.scene.name == "Lobby3") { checkedUI = candidate; break; }
        if (checkedUI == null) throw new InvalidOperationException("Authored Lobby3 wine UI is missing.");
        if (checkedUI.IsOpen) throw new InvalidOperationException("Close the current wine interaction before testing.");
        if (UnityEngine.EventSystems.EventSystem.current == null)
            throw new InvalidOperationException("A live EventSystem is required.");
        checkSuccesses = checkCancels = 0;
        if (!checkedUI.Open(() => checkSuccesses++, () => checkCancels++))
            throw new InvalidOperationException("Authored wine UI refused to open.");
        checkInitialFill = checkedUI.CurrentFill;
        try { checkedPointer = PourPointer(-1); }
        catch { checkedUI.CloseWithoutResult(); checkedUI = null; throw; }
        checkPhase = 1;
        checkDeadline = EditorApplication.timeSinceStartup + 2.0;
        RuntimeCheckResult = "RUNNING: mouse pour";
        EditorApplication.update -= AdvanceRuntimeCheck;
        EditorApplication.update += AdvanceRuntimeCheck;
        return RuntimeCheckResult;
    }

    private static UnityEngine.EventSystems.PointerEventData PourPointer(int pointerId)
    {
        Canvas.ForceUpdateCanvases();
        var canvas = checkedUI.GetComponentInParent<Canvas>();
        var raycaster = checkedUI.GetComponentInParent<UnityEngine.UI.GraphicRaycaster>();
        if (raycaster == null) throw new InvalidOperationException("Wine UI Canvas has no GraphicRaycaster.");
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        {
            pointerId = pointerId,
            button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(camera, checkedUI.bottle.position),
            pointerPressRaycast = new UnityEngine.EventSystems.RaycastResult { module = raycaster }
        };
        checkedUI.bottle.GetComponent<WinePouringBottleDrag>().OnBeginDrag(pointer);
        Quaternion tilt = Quaternion.Euler(0f, 0f, checkedUI.pourTiltDegrees);
        Vector2 localMouth = checkedUI.bottle.InverseTransformPoint(checkedUI.bottleMouth.position);
        Vector2 opening = checkedUI.stage.InverseTransformPoint(checkedUI.glassOpening.position);
        Vector2 destination = opening + checkedUI.pourMouthOffset - (Vector2)(tilt * localMouth);
        pointer.position = RectTransformUtility.WorldToScreenPoint(camera, checkedUI.stage.TransformPoint(destination));
        checkedUI.bottle.GetComponent<WinePouringBottleDrag>().OnDrag(pointer);
        return pointer;
    }

    private static void AdvanceRuntimeCheck()
    {
        try
        {
            if (!Application.isPlaying || !GameSaveManager.IsPersistenceSuspended || checkedUI == null)
                throw new InvalidOperationException("Save-suspended runtime session ended during the wine check.");
            double now = EditorApplication.timeSinceStartup;
            if (checkPhase == 1)
            {
                if (checkedUI.CurrentFill > checkInitialFill + 0.04f)
                {
                    checkedUI.bottle.GetComponent<WinePouringBottleDrag>().OnEndDrag(checkedPointer);
                    checkReleasedFill = checkedUI.CurrentFill;
                    checkPhase = 2;
                    checkDeadline = now + 0.3;
                    RuntimeCheckResult = "RUNNING: release stops pouring";
                }
                else if (now > checkDeadline) throw new InvalidOperationException("Mouse drag did not begin filling within two seconds.");
            }
            else if (checkPhase == 2 && now >= checkDeadline)
            {
                if (Mathf.Abs(checkedUI.CurrentFill - checkReleasedFill) > 0.001f)
                    throw new InvalidOperationException("Liquid continued filling after pointer release.");
                if (!checkedUI.IsOpen || checkSuccesses != 0)
                    throw new InvalidOperationException("Releasing early incorrectly completed the refill.");
                checkedUI.backButton.onClick.Invoke();
                checkedUI.Cancel();
                if (checkCancels != 1 || checkedUI.IsOpen)
                    throw new InvalidOperationException("Back did not cancel exactly once.");
                if (!checkedUI.Open(() => checkSuccesses++, () => checkCancels++))
                    throw new InvalidOperationException("Wine UI did not reopen after cancellation.");
                checkedPointer = PourPointer(0);
                checkPhase = 3;
                touchStarted = now;
                checkDeadline = now + 4.0;
                RuntimeCheckResult = "RUNNING: touch pour and timed completion";
            }
            else if (checkPhase == 3)
            {
                if (checkSuccesses == 1 && !checkedUI.IsOpen)
                {
                    if (now - touchStarted > 4.0) throw new InvalidOperationException("Touch completion exceeded four seconds.");
                    if (checkedUI.CurrentFill < checkedUI.targetFill)
                        throw new InvalidOperationException("Success happened before the target fill.");
                    checkPhase = 4;
                    checkDeadline = now + 0.2;
                    RuntimeCheckResult = "RUNNING: checking completion is single";
                }
                else if (now > checkDeadline)
                    throw new InvalidOperationException("Touch pour did not complete and close within four seconds.");
            }
            else if (checkPhase == 4 && now >= checkDeadline)
            {
                checkedUI.Cancel();
                if (checkSuccesses != 1 || checkCancels != 1 || checkedUI.IsOpen)
                    throw new InvalidOperationException("Completion produced duplicate callbacks or failed to close.");
                FinishRuntimeCheck("PASS: live mouse drag filled; release stopped; Back cancelled once; reopened; touch id 0 reached target, returned bottle and closed once within 4s. No customer stock/billing was exercised.");
            }
        }
        catch (Exception error) { FinishRuntimeCheck("FAIL: " + error.Message); }
    }

    private static void FinishRuntimeCheck(string result)
    {
        EditorApplication.update -= AdvanceRuntimeCheck;
        checkPhase = 0;
        if (checkedUI != null) checkedUI.CloseWithoutResult();
        checkedPointer = null;
        checkedUI = null;
        RuntimeCheckResult = result;
        Debug.Log("[WinePouringUI runtime check] " + result);
    }

    private static Sprite Load(string name)
    {
        Sprite result = AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");
        if (result == null) throw new InvalidOperationException("Missing wine artwork: " + name);
        return result;
    }

    public static void ExportArtwork()
    {
        // Extract illustrations into separate source PNGs. No Sprite Editor metadata is rewritten.
        // New Single sprites require no unsupported sprite-frame editing capability.
        var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            atlas.LoadImage(File.ReadAllBytes(Art + "WinePouringAtlas.png"));
            if (atlas.width != 1774 || atlas.height != 887)
                throw new InvalidOperationException("Wine atlas dimensions changed; review illustration regions first.");
            Export(atlas, "WineBottle", new RectInt(127, 351, 191, 524));
            Export(atlas, "WineGlass", new RectInt(506, 356, 319, 480));
            Export(atlas, "WineFill", new RectInt(956, 491, 305, 249));
            Export(atlas, "WineStream", new RectInt(1512, 351, 91, 524));
            Export(atlas, "SplashA", new RectInt(96, 61, 252, 218));
            Export(atlas, "SplashB", new RectInt(502, 61, 330, 218));
            Export(atlas, "SplashC", new RectInt(986, 61, 248, 218));
            Export(atlas, "TargetMarker", new RectInt(1394, 144, 316, 73));
        }
        finally { UnityEngine.Object.DestroyImmediate(atlas); }
    }

    private static void Export(Texture2D atlas, string name, RectInt rect)
    {
        string path = Art + name + ".png";
        if (!File.Exists(path))
        {
            var part = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false);
            try
            {
                part.SetPixels(atlas.GetPixels(rect.x, rect.y, rect.width, rect.height));
                part.Apply();
                File.WriteAllBytes(path, part.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(part); }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Wine PNG importer missing: " + path);
        bool changed = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
            importer.mipmapEnabled || !importer.alphaIsTransparency || importer.npotScale != TextureImporterNPOTScale.None;
        if (!changed) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static UnityEngine.UI.Image Picture(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        var image = Rect(name, parent, position, size).gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Label(string name, Transform parent, string text, TMP_FontAsset font, float size)
    {
        var label = Rect(name, parent, Vector2.zero, new Vector2(600f, 64f)).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = new Color(0.08f, 0.18f, 0.25f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static void Fill(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
#endif

