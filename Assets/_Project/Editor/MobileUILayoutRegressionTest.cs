#if UNITY_EDITOR
using System;
using PlayFab.ClientModels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MobileUILayoutRegressionTest
{
    [MenuItem("Tools/Dine In/Validate Mobile UI Scaling")]
    public static void Run()
    {
        ValidateReferenceResolution(new Vector2(1920f, 1080f));
        ValidateReferenceResolution(new Vector2(800f, 450f));
        ValidateReferenceResolution(new Vector2(800f, 600f));
        ValidatePersistentHudScaling("PlayerTaskHUD(Clone)");
        ValidatePersistentHudScaling("CasualDiningProgressHUD(Clone)");
        ValidatePersistentHudScaling("LobbyPauseMenu(Clone)");
        ValidateManagementComputerScaling();
        ValidateRealme8SizingEnvelope();
        ValidateAuthoredNewGameMenuScene();
        ValidateLoadingCanvasProtection();
        ValidateDevConsoleAuthorizationBoundary();
        Debug.Log(
            "[MobileUILayoutRegressionTest] PASS — authored canvas coordinates are preserved " +
            "while full-screen panels, persistent HUD sizing, and physical touch targets " +
            "match the 1280 x 576 Android policy.");
    }

    private static void ValidateLoadingCanvasProtection()
    {
        const string prefabPath =
            "Assets/_Project/MainMenu/NewDesign/LoadingScreens/NormalLoadingScreen/LoadingScreen.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert(prefab != null, "Loading screen prefab is missing.");

        CanvasScaler scaler = prefab.GetComponentInChildren<CanvasScaler>(true);
        BurgerLoadingAnimation burger = prefab.GetComponentInChildren<BurgerLoadingAnimation>(true);
        Slider slider = prefab.GetComponentInChildren<Slider>(true);
        Assert(scaler != null && burger != null && slider != null,
            "Loading screen lost its canvas, burger animation, or progress bar.");

        CanvasScaler.ScreenMatchMode authoredMode = scaler.screenMatchMode;
        float authoredMatch = scaler.matchWidthOrHeight;
        Vector3 authoredBurgerScale = burger.transform.localScale;
        MobileUIAccessibility.ConfigureCanvasForMobile(scaler);

        Assert(scaler.screenMatchMode == authoredMode &&
               Mathf.Approximately(scaler.matchWidthOrHeight, authoredMatch),
            "Loading screen CanvasScaler was changed by the global mobile pass.");
        Assert(burger.gameObject.activeSelf && burger.enabled &&
               burger.transform.localScale == authoredBurgerScale,
            "Loading burger artwork was hidden or visually rescaled by the global mobile pass.");
    }

    private static void ValidateDevConsoleAuthorizationBoundary()
    {
        const string sessionPlayFabId = "TEST-OWNER-ID";
        UserAccountInfo owner = new UserAccountInfo
        {
            PlayFabId = sessionPlayFabId,
            Username = "Kali"
        };

        Assert(DevSettingsConsole.IsVerifiedAuthorizedAccount(owner, sessionPlayFabId, "Kali"),
            "The authenticated Kali test account was rejected by the dev-console policy.");
        Assert(!DevSettingsConsole.IsVerifiedAuthorizedAccount(owner, "DIFFERENT-SESSION-ID", "Kali"),
            "A PlayFab account response from a different login session was accepted.");

        owner.Username = "kali";
        Assert(!DevSettingsConsole.IsVerifiedAuthorizedAccount(owner, sessionPlayFabId, "Kali"),
            "The dev-console policy stopped requiring the exact unique PlayFab username.");

        owner.Username = "RegularPlayer";
        Assert(!DevSettingsConsole.IsVerifiedAuthorizedAccount(owner, sessionPlayFabId, "Kali"),
            "A non-owner PlayFab account was allowed to use dev commands.");
    }

    private static void ValidatePersistentHudScaling(string canvasName)
    {
        GameObject root = CreateCanvas(canvasName, new Vector2(1920f, 1080f));
        try
        {
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            MobileUIAccessibility.ConfigureCanvasForMobile(scaler);

            Assert(scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight,
                $"{canvasName} did not use the persistent-HUD mobile policy.");
            Assert(Mathf.Approximately(scaler.matchWidthOrHeight, 0f),
                $"{canvasName} was not width-scaled for mobile readability.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ValidateManagementComputerScaling()
    {
        GameObject root = CreateCanvas("ManagementComputerCanvas", new Vector2(1920f, 1080f));
        try
        {
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            MobileUIAccessibility.ConfigureCanvasForMobile(scaler);

            Assert(scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight,
                "Management computer did not use its mobile readability policy.");
            Assert(Mathf.Approximately(scaler.matchWidthOrHeight, 0f),
                "Management computer mobile scaling became too large or too small.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ValidateFullScreenPanelCoverage()
    {
        GameObject parent = new GameObject("Canvas Frame", typeof(RectTransform));
        GameObject panelObject = new GameObject("GamemodePopUpUI", typeof(RectTransform));
        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform));
        GameObject dialogueObject = new GameObject("Dialogue", typeof(RectTransform));
        GameObject titleObject = new GameObject("TitleFrame", typeof(RectTransform));
        GameObject campaignObject = new GameObject("CampaignButton", typeof(RectTransform));
        GameObject multiplayerObject = new GameObject("MultiplayerButton", typeof(RectTransform));
        GameObject closeObject = new GameObject("CancelButton ", typeof(RectTransform));

        try
        {
            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.SetParent(parent.transform, false);
            panel.anchorMin = new Vector2(0.2f, 0.1f);
            panel.anchorMax = new Vector2(0.8f, 0.9f);
            panel.sizeDelta = new Vector2(200f, 100f);

            RectTransform background = backgroundObject.GetComponent<RectTransform>();
            background.SetParent(panel, false);
            background.anchorMin = background.anchorMax = Vector2.zero;
            background.sizeDelta = new Vector2(1920f, 1080f);

            RectTransform dialogue = dialogueObject.GetComponent<RectTransform>();
            dialogue.SetParent(panel, false);
            dialogue.sizeDelta = new Vector2(450f, 260f);

            RectTransform title = titleObject.GetComponent<RectTransform>();
            title.SetParent(dialogue, false);
            title.sizeDelta = new Vector2(450f, 100f);
            RectTransform campaign = campaignObject.GetComponent<RectTransform>();
            campaign.SetParent(dialogue, false);
            campaign.sizeDelta = new Vector2(200f, 80f);
            RectTransform multiplayer = multiplayerObject.GetComponent<RectTransform>();
            multiplayer.SetParent(dialogue, false);
            multiplayer.sizeDelta = new Vector2(200f, 80f);
            RectTransform close = closeObject.GetComponent<RectTransform>();
            close.SetParent(dialogue, false);
            close.sizeDelta = new Vector2(50f, 50f);

            MobileUIAccessibility.ConfigureFullScreenPanelForMobile(panel);

            Assert(panel.anchorMin == Vector2.zero && panel.anchorMax == Vector2.one &&
                   panel.sizeDelta == Vector2.zero,
                "Full-screen popup root no longer covers the complete phone canvas.");
            Assert(background.anchorMin == new Vector2(0.5f, 0.5f) &&
                   background.anchorMax == new Vector2(0.5f, 0.5f) &&
                   background.sizeDelta == new Vector2(1920f, 1080f),
                "Popup backdrop no longer preserves its authored motif scale.");
            Assert(dialogue.sizeDelta == new Vector2(450f, 260f),
                "Full-screen coverage unexpectedly resized modal content.");
            Assert(title.localScale == new Vector3(2.1f, 2.1f, 1f) &&
                   title.anchoredPosition == new Vector2(0f, 80f),
                "Game-mode title escaped its phone content frame.");
            Assert(campaign.localScale == new Vector3(2.1f, 2.1f, 1f) &&
                   multiplayer.localScale == new Vector3(2.1f, 2.1f, 1f),
                "Game-mode choices are no longer sized for phone input.");
            Assert(close.sizeDelta == new Vector2(72f, 72f),
                "Game-mode close control is no longer visibly touch-sized.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(parent);
        }
    }

    private static void ValidateRealme8SizingEnvelope()
    {
        const float screenWidth = 1280f;
        const float screenHeight = 576f;
        float hudScale = screenWidth / 1920f;
        float pausePixels = Mathf.Max(MobileUIAccessibility.MinimumPersistentHudPixels, 82f * hudScale);
        float taskPixels = Mathf.Max(MobileUIAccessibility.MinimumPersistentHudPixels, 76f * hudScale);
        float touchPixels = MobileUIAccessibility.MinimumCanvasTouchSizeForScale(
            screenHeight / 1080f) * (screenHeight / 1080f);
        float lobbyCanvasScale = screenHeight / 450f;
        float notepadScale = 0.5f * OrderChecklistUI.MobileRootScaleMultiplier * lobbyCanvasScale;
        Vector2 notepadCardPixels = new Vector2(174f, 218f) * notepadScale;
        Vector2 cashierPanelPixels = new Vector2(700f, 350f) *
                                     CashierRegisterUI.MobilePanelScale * lobbyCanvasScale;
        float computerCanvasScale = screenWidth / 1920f;
        Vector2 computerLogicalScreen = new Vector2(
            screenWidth / computerCanvasScale,
            screenHeight / computerCanvasScale);
        Vector2 computerWindowPixels = Vector2.Scale(
            ManagementComputerResponsiveLayout.MobileLandscapeWindowMax -
            ManagementComputerResponsiveLayout.MobileLandscapeWindowMin,
            computerLogicalScreen) * computerCanvasScale - Vector2.one * (16f * computerCanvasScale);

        Assert(pausePixels >= 70f && pausePixels <= 76f,
            $"Pause HUD escaped its readable mobile size envelope ({pausePixels:0.0}px).");
        Assert(taskPixels >= 70f && taskPixels <= 76f,
            $"Task HUD escaped its readable mobile size envelope ({taskPixels:0.0}px).");
        Assert(Mathf.Approximately(touchPixels, 72f),
            $"Physical touch target is no longer 72px ({touchPixels:0.0}px).");
        Assert(notepadCardPixels.x >= 140f && notepadCardPixels.x <= 150f &&
               notepadCardPixels.y >= 175f && notepadCardPixels.y <= 185f,
            $"Notepad choices escaped their mobile readability envelope ({notepadCardPixels.x:0.0} x {notepadCardPixels.y:0.0}px).");
        Assert(cashierPanelPixels.x >= 1000f && cashierPanelPixels.x <= 1060f &&
               cashierPanelPixels.y >= 500f && cashierPanelPixels.y <= 530f &&
               cashierPanelPixels.x < screenWidth && cashierPanelPixels.y < screenHeight,
            $"Cashier panel no longer fits the phone ({cashierPanelPixels.x:0.0} x {cashierPanelPixels.y:0.0}px).");
        Assert(computerWindowPixels.x >= 1200f && computerWindowPixels.x < screenWidth &&
               computerWindowPixels.y >= 500f && computerWindowPixels.y < screenHeight,
            $"Management workspace escaped the phone safe frame ({computerWindowPixels.x:0.0} x {computerWindowPixels.y:0.0}px).");
    }

    private static void ValidateAuthoredNewGameMenuScene()
    {
        // This check intentionally mutates an additive scene in memory and then
        // closes it without saving. Keep interactive editor scenes completely
        // untouched; CI/batch validation is the authoritative authored-scene run.
        if (!Application.isBatchMode)
            return;

        const string scenePath = "Assets/_Project/Scenes/NewMenu/NewGameMenu.unity";
        Scene existing = SceneManager.GetSceneByPath(scenePath);
        if (existing.IsValid() && existing.isLoaded)
        {
            Debug.LogWarning(
                "[MobileUILayoutRegressionTest] NewGameMenu is already open; " +
                "skipping the non-destructive authored-scene check.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        GameObject validatorObject = null;
        try
        {
            validatorObject = new GameObject("[Mobile UI Scene Validator]");
            SceneManager.MoveGameObjectToScene(validatorObject, scene);
            MobileUIAccessibility validator = validatorObject.AddComponent<MobileUIAccessibility>();
            validator.ApplyNowForValidation();

            RectTransform gameCanvas = FindSceneRect(scene, "GameCanvas");
            Assert(gameCanvas != null, "NewGameMenu lost its GameCanvas.");
            CanvasScaler scaler = gameCanvas.GetComponent<CanvasScaler>();
            Assert(scaler != null &&
                   scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight &&
                   Mathf.Approximately(scaler.matchWidthOrHeight, 0f),
                "NewGameMenu authored CanvasScaler was changed by the global mobile pass.");

            RectTransform modePanel = FindSceneRect(scene, "GamemodePopUpUI");
            RectTransform shopPanel = FindSceneRect(scene, "ShopPanelUI");
            Assert(modePanel != null && shopPanel != null,
                "NewGameMenu lost a required full-screen panel.");

            RectTransform title = FindDescendant(modePanel, "TitleFrame");
            RectTransform campaign = FindDescendant(modePanel, "CampaignButton");
            RectTransform multiplayer = FindDescendant(modePanel, "MultiplayerButton");
            RectTransform backdrop = FindDescendant(modePanel, "Background");
            Assert(title != null && campaign != null && multiplayer != null && backdrop != null,
                "Game-mode popup hierarchy changed without updating its mobile layout.");
            Assert(title.localScale == Vector3.one && campaign.localScale == Vector3.one &&
                   multiplayer.localScale == Vector3.one,
                "Global mobile scaling changed the authored game-mode popup visuals.");

            RectTransform shopScroll = FindDescendant(shopPanel, "Vertical Scroll");
            Assert(shopScroll != null && shopScroll.localScale == Vector3.one,
                "Global mobile scaling changed the authored shop card layout.");

            RectTransform selector = FindSceneRect(scene, "RestaurantSelectorButtonsUI");
            RectTransform play = FindSceneRect(scene, "PlayButton");
            RectTransform selectorButtons = FindDirectChild(selector, "Buttons");
            RectTransform money = FindSceneRect(scene, "MoneyUI");
            RectTransform back = FindDirectChild(gameCanvas, "BackButton");
            RectTransform shop = FindDirectChild(gameCanvas, "ShopButton");
            Assert(selector != null && selector.gameObject.activeSelf && selector.localScale == Vector3.one,
                "Restaurant selector was hidden or visually rescaled on mobile.");
            Assert(play != null && play.gameObject.activeSelf && play.localScale == Vector3.one &&
                   play.rect.size == new Vector2(160f, 50f),
                "NewGameMenu Play button is missing or no longer uses its working authored layout.");
            Assert(selectorButtons != null && selectorButtons.gameObject.activeSelf,
                "NewGameMenu restaurant controls container is inactive.");

            // With the authored 800-wide CanvasScaler, a 1280 x 576 phone has a
            // 360-unit logical height. Verify the complete Play button stays inside it.
            const float realmeLogicalHeight = 576f / (1280f / 800f);
            float playCenterY = selector.anchoredPosition.y +
                                selectorButtons.anchoredPosition.y +
                                play.anchoredPosition.y;
            Assert(playCenterY - play.rect.height * 0.5f >= 0f &&
                   playCenterY + play.rect.height * 0.5f <= realmeLogicalHeight,
                "NewGameMenu Play button falls outside the Realme 8 viewport.");
            Assert(money != null && Mathf.Approximately(money.anchoredPosition.y, -75f),
                "Global mobile scaling moved the authored wallet controls.");
            Assert(back != null && back.rect.size == new Vector2(50f, 50f) &&
                   shop != null && shop.rect.size == new Vector2(50f, 50f),
                "Global mobile scaling changed the authored NewGameMenu corner controls.");
        }
        finally
        {
            if (validatorObject != null)
                UnityEngine.Object.DestroyImmediate(validatorObject);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static RectTransform FindSceneRect(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            RectTransform[] rects = roots[i].GetComponentsInChildren<RectTransform>(true);
            for (int r = 0; r < rects.Length; r++)
            {
                if (rects[r].name == objectName)
                    return rects[r];
            }
        }

        return null;
    }

    private static RectTransform FindDescendant(RectTransform root, string objectName)
    {
        if (root == null)
            return null;

        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i].name == objectName)
                return rects[i];
        }

        return null;
    }

    private static RectTransform FindDirectChild(RectTransform parent, string objectName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            if (parent.GetChild(i) is RectTransform child && child.name == objectName)
                return child;
        }

        return null;
    }

    private static GameObject CreateCanvas(string name, Vector2 referenceResolution)
    {
        GameObject root = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        return root;
    }

    private static void ValidateReferenceResolution(Vector2 authoredReference)
    {
        GameObject root = new GameObject(
            "Mobile UI Scale Test",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));

        try
        {
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = authoredReference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            MobileUIAccessibility.ConfigureCanvasForMobile(scaler);

            Assert(scaler.referenceResolution == authoredReference,
                $"Mobile policy changed {authoredReference.x} x {authoredReference.y} canvas coordinates.");
            Assert(scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand,
                "Mobile policy did not select non-cropping Expand scaling.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
