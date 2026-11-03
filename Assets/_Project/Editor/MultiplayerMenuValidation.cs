#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class MultiplayerMenuValidation
{
    private const string MenuPath = "Assets/_Project/Scenes/NewMenu/NewGameMenu.unity";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void CheckAndRunRepair() { Check(); MultiplayerRepairRegressionTest.RunBatch(); }

    // Explicit authoring only; never runs on import or invokes a player build.
    public static void AuthorAndCheck()
    {
        Directory.CreateDirectory("Logs");
        if (!File.Exists("Logs/NewGameMenu.before-multiplayer-fix.unity"))
            File.Copy(MenuPath, "Logs/NewGameMenu.before-multiplayer-fix.unity");
        var scene = EditorSceneManager.OpenScene(MenuPath);
        var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MultiplayerMenuController>(true)).Single();
        var root = (RectTransform)controller.transform;
        Stretch(root);
        var viewport = (RectTransform)root.Find("Main Panel"); Stretch(viewport);
        var frame = (RectTransform)viewport.Find("Container");
        frame.anchorMin = frame.anchorMax = frame.pivot = Vector2.one * .5f;
        frame.sizeDelta = new Vector2(700, 350); frame.localScale = Vector3.one;
        var close = viewport.GetComponentsInChildren<Button>(true).Single(b => b.name.Trim() == "CancelButton");
        var closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0, 1);
        closeRect.pivot = Vector2.one * .5f;
        closeRect.sizeDelta = new Vector2(56, 56); closeRect.anchoredPosition = new Vector2(44, -40);
        close.onClick = new Button.ButtonClickedEvent(); // One shared runtime handler also owns Escape/Android Back.
        foreach (string name in new[] { "Background", "Foreground" }) {
            var background = (RectTransform)root.Find(name); Stretch(background);
            foreach (var graphic in background.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            background.SetAsFirstSibling();
        }
        viewport.SetAsLastSibling(); close.transform.SetAsLastSibling();
        var safe = root.GetComponent<UIScreenSafeArea>() ?? root.gameObject.AddComponent<UIScreenSafeArea>();
        safe.Capture(new[] { viewport });
        var safeData = new SerializedObject(safe);
        safeData.FindProperty("applyInEditor").boolValue = false; safeData.ApplyModifiedPropertiesWithoutUndo();

        var data = new SerializedObject(controller);
        var sourceButton = (Button)data.FindProperty("joinTabButton").objectReferenceValue;
        var sourceText = sourceButton.GetComponentInChildren<TMP_Text>(true);
        var toggle = Node(viewport, "ConnectionDetailsButton");
        var image = toggle.GetComponent<Image>() ?? toggle.gameObject.AddComponent<Image>();
        image.sprite = sourceButton.GetComponent<Image>().sprite; image.type = Image.Type.Sliced; image.color = Color.white;
        var button = toggle.GetComponent<Button>() ?? toggle.gameObject.AddComponent<Button>();
        button.targetGraphic = image; button.onClick = new Button.ButtonClickedEvent();
        toggle.anchorMin = toggle.anchorMax = new Vector2(.5f, 0); toggle.pivot = Vector2.one * .5f;
        toggle.sizeDelta = new Vector2(260, 28); toggle.anchoredPosition = new Vector2(0, 20);
        var label = Text(toggle, "Label", sourceText);
        label.text = "Connection details"; label.fontSize = label.fontSizeMax = 16; label.fontSizeMin = 12;
        label.color = Color.white; label.alignment = TextAlignmentOptions.Center; Stretch(label.rectTransform);
        var details = Text(viewport, "ConnectionDetails", sourceText);
        details.fontSize = details.fontSizeMax = 14; details.fontSizeMin = 10;
        details.alignment = TextAlignmentOptions.BottomLeft; details.color = Color.white;
        details.rectTransform.anchorMin = new Vector2(.04f, 0); details.rectTransform.anchorMax = new Vector2(.96f, 0);
        details.rectTransform.pivot = new Vector2(.5f, 0); details.rectTransform.sizeDelta = new Vector2(0, 96);
        details.rectTransform.anchoredPosition = new Vector2(0, 40); details.gameObject.SetActive(false);
        Bind(data, "closeButton", close); Bind(data, "menuViewport", viewport); Bind(data, "contentFrame", frame);
        Bind(data, "connectionDetailsButton", button); Bind(data, "connectionDetailsText", details);
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save multiplayer menu authoring.");
        Check();
    }
    private static void Bind(SerializedObject data, string field, Object value) => data.FindProperty(field).objectReferenceValue = value;
    private static RectTransform Node(Transform parent, string name)
    {
        var existing = parent.Find(name) as RectTransform; if (existing != null) return existing;
        var go = new GameObject(name, typeof(RectTransform)); go.layer = parent.gameObject.layer; go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
    private static TMP_Text Text(Transform parent, string name, TMP_Text source)
    {
        var rect = Node(parent, name);
        var text = rect.GetComponent<TMP_Text>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = source.font; text.fontSharedMaterial = source.fontSharedMaterial;
        text.richText = false; text.raycastTarget = false; text.enableAutoSizing = true;
        return text;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    public static void Check()
    {
        var preview = EditorSceneManager.OpenPreviewScene(MenuPath);
        try {
            var controller = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MultiplayerMenuController>(true)).Single();
            var data = new SerializedObject(controller);
            foreach (string field in new[] { "closeButton", "menuViewport", "contentFrame", "connectionDetailsButton", "connectionDetailsText" })
                Require(data.FindProperty(field).objectReferenceValue != null, "Missing room menu reference: " + field);
            var close = (Button)data.FindProperty("closeButton").objectReferenceValue;
            Require(close.onClick.GetPersistentEventCount() == 0, "Close still bypasses the shared cancellation handler.");
            var viewport = (RectTransform)data.FindProperty("menuViewport").objectReferenceValue;
            var frame = (RectTransform)data.FindProperty("contentFrame").objectReferenceValue;
            var root = (RectTransform)controller.transform;
            var fixture = new GameObject("Isolated room-menu canvas", typeof(RectTransform), typeof(Canvas));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fixture, preview);
            fixture.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.SetParent(fixture.transform, false);
            var fixtureRect = (RectTransform)fixture.transform;
            int cases = 0;
            foreach (var resolution in new[] { new Vector2(1920,1080), new Vector2(1440,900), new Vector2(2560,1080), new Vector2(2400,1080), new Vector2(1280,800) })
            foreach (float uiScale in new[] { .85f, 1f, 1.15f })
            foreach (bool expanded in new[] { false, true }) {
                float factor = Mathf.Min(resolution.x/800, resolution.y/450) * uiScale;
                fixtureRect.sizeDelta = resolution/factor;
                // Simulate landscape cutouts on both sides and a navigation inset below.
                viewport.offsetMin = new Vector2(32/factor, 20/factor); viewport.offsetMax = new Vector2(-32/factor, 0);
                typeof(MultiplayerMenuController).GetField("detailsExpanded", Private).SetValue(controller, expanded);
                typeof(MultiplayerMenuController).GetField("layoutDirty", Private).SetValue(controller, true);
                typeof(MultiplayerMenuController).GetMethod("LateUpdate", Private).Invoke(controller, null);
                Canvas.ForceUpdateCanvases();
                Inside(viewport, (RectTransform)close.transform); Inside(viewport, frame);
                Require(((RectTransform)close.transform).rect.width * factor >= 44, "Close hit target is too small.");
                Require(frame.localScale.x > 0 && frame.localScale.x <= 1, "Room frame scale invalid.");
                cases++;
            }
            Require(PhotonBootstrap.NormalizeRegion("ASIA/cluster") == "asia", "Region normalization changed.");
            Require(PhotonBootstrap.EffectiveConnectionVersion == PhotonBootstrap.ConnectionVersion + "_" + Photon.Pun.PhotonNetwork.PunVersion, "PUN suffix mismatch.");
            CheckCancellation(controller);
            var rootSettings = File.ReadAllText("ProjectSettings/ProjectSettings.asset");
            string version = System.Text.RegularExpressions.Regex.Match(rootSettings, @"(?m)^  bundleVersion: (.+)").Groups[1].Value.Trim();
            foreach (string profile in Directory.GetFiles("Assets/Settings/Build Profiles", "*.asset")) {
                var contents = File.ReadAllText(profile);
                var saved = System.Text.RegularExpressions.Regex.Match(contents, @"bundleVersion: ([^'\r\n]+)");
                Require(!saved.Success || saved.Groups[1].Value.Trim() == version, "Profile separates Photon versions: " + profile);
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/MultiplayerMenuValidation.result", $"PASS: {cases} authored room-menu layout cases; close references/hit target; PUN suffix/region normalization; effective profile versions; request cancellation/late failure/queued submission. No player builds or network requests.");
            Debug.Log("[MultiplayerMenuValidation] PASS: " + cases + " layout cases and connection configuration checks.");
        } finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
    private static void Inside(RectTransform parent, RectTransform child)
    {
        var corners = new Vector3[4]; child.GetWorldCorners(corners);
        foreach (var corner in corners) {
            Vector3 local = parent.InverseTransformPoint(corner);
            Require(parent.rect.xMin - .5f <= local.x && local.x <= parent.rect.xMax + .5f &&
                parent.rect.yMin - .5f <= local.y && local.y <= parent.rect.yMax + .5f, "Clipped room control: " + child.name);
        }
    }
    private static void CheckCancellation(MultiplayerMenuController source)
    {
        var root = new GameObject("Isolated cancelled room request"); root.SetActive(false);
        var controller = root.AddComponent<MultiplayerMenuController>();
        bool originalSync = Photon.Pun.PhotonNetwork.AutomaticallySyncScene;
        var instanceField = typeof(PhotonBootstrap).GetField("<Instance>k__BackingField", Private | BindingFlags.Static);
        var originalBootstrap = PhotonBootstrap.Instance;
        void Set(string field, object value) => typeof(MultiplayerMenuController).GetField(field, Private).SetValue(controller, value);
        object Get(string field) => typeof(MultiplayerMenuController).GetField(field, Private).GetValue(controller);
        void Call(string method) => typeof(MultiplayerMenuController).GetMethod(method, Private).Invoke(controller, null);
        try {
            foreach (string field in new[] { "createButton", "joinButton", "joinTabButton", "createTabButton" }) {
                var button = new GameObject(field, typeof(RectTransform), typeof(Button)).GetComponent<Button>();
                button.transform.SetParent(root.transform, false);
                Text(button.transform, "Label", source.GetComponentInChildren<TMP_Text>(true));
                Set(field, button);
            }
            var code = Text(root.transform, "Code", source.GetComponentInChildren<TMP_Text>(true)); Set("codeText", code);
            Set("rosterTexts", Array.Empty<TMP_Text>());
            var status = Text(root.transform, "Status", code); Set("statusTexts", new[] { status });
            Set("restaurantDropdown", Node(root.transform,"Restaurant").gameObject.AddComponent<TMP_Dropdown>());
            Set("partySizeDropdown", Node(root.transform,"Party").gameObject.AddComponent<TMP_Dropdown>());
            Set("codeInput", Node(root.transform,"Input").gameObject.AddComponent<TMP_InputField>());
            Set("busy", true); Set("pendingCode", "DINE-TEST"); Set("pendingAccountId", "isolated-fixture"); Set("awaitingAdmission", true);
            var bootstrap = Node(root.transform, "Isolated connection").gameObject.AddComponent<PhotonBootstrap>();
            instanceField.SetValue(null, bootstrap);
            typeof(PhotonBootstrap).GetField("<AccountConnectionPending>k__BackingField", Private).SetValue(bootstrap, true);
            controller.OnDisconnected(Photon.Realtime.DisconnectCause.DisconnectByClientLogic);
            Require((bool)Get("busy") && (string)Get("pendingCode") == "DINE-TEST", "Account/settings reconnect lost its queued room request.");
            Call("CancelPendingOperation");
            Require(!(bool)Get("busy") && Get("pendingCode") == null && Get("pendingAccountId") == null && !(bool)Get("awaitingAdmission"), "Cancelled request retained pending state.");
            status.text = "cancelled";
            controller.OnJoinRoomFailed(Photon.Realtime.ErrorCode.GameDoesNotExist, "late callback");
            Require(status.text == "cancelled", "Late failure changed a cancelled operation.");
            Call("Submit");
            Require(Get("pendingCode") == null && !(bool)Get("starting"), "Queued submission revived a cancelled request.");
        } finally {
            Photon.Pun.PhotonNetwork.AutomaticallySyncScene = originalSync;
            instanceField.SetValue(null, originalBootstrap);
            Object.DestroyImmediate(root);
        }
    }
}
#endif
