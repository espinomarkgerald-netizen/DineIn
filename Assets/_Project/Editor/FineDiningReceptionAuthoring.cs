#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Explicit, repeat-safe authoring for existing Lobby3 reception; never opens or saves a scene.</summary>
public static class FineDiningReceptionAuthoring
{
    public const string ReservationIndicatorPath = "Assets/_Project/ManagementComputer/Prefabs/FineDiningReservationIndicator.prefab";
    public const string DrinkRequestPath = "Assets/_Project/ManagementComputer/Prefabs/FineDiningDrinkRequest.prefab";
    private const string BubbleArt = "Assets/_Project/UI/Assets/Legacy/Buttons/InGameHUD/Interactables/Popup Frame.png";
    private const string TableArt = "Assets/_Project/UI/Assets/Legacy/Bubbles/table.png";
    private const string RegularFont = "Assets/_Project/UI/Assets/Fonts/Atkinson_Hyperlegible/AtkinsonHyperlegible-Regular SDF.asset";

    public static string Apply(Scene scene)
    {
        if (EditorApplication.isPlaying || !scene.IsValid() || !scene.isLoaded || scene.name != "Lobby3")
            throw new InvalidOperationException("Apply to loaded Lobby3 in Edit Mode.");
        var nodes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var reservations = nodes.Select(n => n.GetComponent<FineDiningReservations>()).Single(r => r != null);
        var waiting = reservations.waitingArea != null ? reservations.waitingArea : reservations.GetComponent<FineDiningWaitingArea>();
        if (waiting == null || waiting.seats == null || waiting.seats.Length == 0)
            throw new InvalidOperationException("The existing authored reception chairs must be bound first.");
        waiting.reservations = reservations;
        waiting.receptionLine = reservations.receptionLine;
        reservations.waitingArea = waiting;
        ValidateChairBindings(waiting);

        var report = new StringBuilder();
        waiting.reservationIndicatorPrefab = AuthorReservationIndicator();
        reservations.drinkRequestBubblePrefab = AuthorDrinkRequest();
        report.AppendLine("Bound compact party and drink task icons using existing Dine In artwork.");
        int created = AuthorStandingPositions(waiting);
        report.AppendLine("Preserved " + waiting.seats.Length + " waiting chairs; " + waiting.standingPositions.Length +
            " reachable standing positions (" + created + " newly authored).");
        report.AppendLine(Validate(scene));
        EditorUtility.SetDirty(waiting);
        EditorUtility.SetDirty(reservations);
        EditorSceneManager.MarkSceneDirty(scene);
        // The caller owns scene persistence after its backup/safety checks.
        AssetDatabase.SaveAssets();
        return report.ToString();
    }

    public static string Validate(Scene scene)
    {
        var waiting = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FineDiningWaitingArea>(true)).Single();
        ValidateChairBindings(waiting);
        Vector3 origin = NavigationOrigin(waiting);
        var spots = waiting.standingPositions ?? Array.Empty<Transform>();
        var path = new NavMeshPath();
        foreach (var point in spots)
        {
            if (point == null || !NavMesh.CalculatePath(origin, point.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("Reception standing position is missing or unreachable: " + (point != null ? point.name : "null"));
        }
        for (int i = 0; i < spots.Length; i++)
            for (int j = i + 1; j < spots.Length; j++)
                if (PlanarDistance(spots[i].position, spots[j].position) < 1.5f)
                    throw new InvalidOperationException("Reception standing positions overlap.");
        foreach (var seat in waiting.seats)
            if (!NavMesh.CalculatePath(origin, seat.approach.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("Reception chair approach is unreachable: " + seat.approach.name);
        if (waiting.reservationIndicatorPrefab == null || waiting.reservationIndicatorPrefab.GetComponent<UIFollowWorldPoint>() == null)
            throw new InvalidOperationException("Compact reservation indicator is not bound.");
        var request = waiting.reservations.drinkRequestBubblePrefab;
        if (request == null || request.GetComponent<OrderBubbleUI>()?.drinkImage == null ||
            request.GetComponent<Button>() == null || request.GetComponent<UIFollowWorldPoint>() == null)
            throw new InvalidOperationException("Drink request task references are incomplete.");
        return "PASS authored references, distinct standing positions, and complete reception NavMesh paths.";
    }

    private static void ValidateChairBindings(FineDiningWaitingArea waiting)
    {
        var seats = new HashSet<Transform>();
        var approaches = new HashSet<Transform>();
        foreach (var chair in waiting.seats)
            if (chair == null || chair.seat == null || chair.approach == null ||
                !seats.Add(chair.seat) || !approaches.Add(chair.approach))
                throw new InvalidOperationException("Waiting chairs require unique seat and approach anchors.");
    }

    private static Vector3 NavigationOrigin(FineDiningWaitingArea waiting)
    {
        var candidates = waiting.receptionLine != null ? waiting.receptionLine.linePoints : Array.Empty<Transform>();
        foreach (var point in candidates)
            if (point != null && NavMesh.SamplePosition(point.position, out var hit, 1f, NavMesh.AllAreas))
                return hit.position;
        if (NavMesh.SamplePosition(waiting.seats[0].approach.position, out var fallback, 1f, NavMesh.AllAreas))
            return fallback.position;
        throw new InvalidOperationException("Reception has no reachable NavMesh origin.");
    }

    private static int AuthorStandingPositions(FineDiningWaitingArea waiting)
    {
        // Preserve Inspector edits on repeat runs.
        if (waiting.standingPositions != null && waiting.standingPositions.Length > 0) return 0;
        Vector3 origin = NavigationOrigin(waiting);
        Vector3 center = waiting.seats.Aggregate(Vector3.zero, (sum, chair) => sum + chair.seat.position) / waiting.seats.Length;
        var root = waiting.transform.Find("Reception Standing Positions");
        if (root == null) { root = new GameObject("Reception Standing Positions").transform; root.SetParent(waiting.transform, false); }
        var points = new List<Transform>();
        var candidates = new List<Vector3>();
        foreach (var chair in waiting.seats)
        {
            Vector3 inward = chair.approach.position - chair.seat.position;
            inward.y = 0f;
            inward.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            foreach (float distance in new[] { 1.8f, 3.4f })
                foreach (float tangent in new[] { 0f, -1.7f, 1.7f })
                    candidates.Add(chair.approach.position + inward * distance + side * tangent);
        }
        var path = new NavMeshPath();
        foreach (Vector3 candidate in candidates)
        {
            if (points.Count >= 10) break;
            if (!NavMesh.SamplePosition(candidate, out var hit, .45f, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(origin, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                continue;
            if (NavMesh.FindClosestEdge(hit.position, out var edge, NavMesh.AllAreas) && edge.distance < .35f) continue;
            if (waiting.seats.Any(s => PlanarDistance(s.seat.position, hit.position) < 1.8f ||
                PlanarDistance(s.approach.position, hit.position) < 1.5f) ||
                points.Any(p => PlanarDistance(p.position, hit.position) < 1.6f)) continue;
            if (Physics.CheckCapsule(hit.position + Vector3.up * .75f, hit.position + Vector3.up * 1.7f,
                .5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            var point = root.Find("Reception Standing " + (points.Count + 1));
            if (point == null)
            {
                point = new GameObject("Reception Standing " + (points.Count + 1)).transform;
                point.SetParent(root, false);
            }
            point.position = hit.position;
            Vector3 facing = center - hit.position;
            facing.y = 0;
            point.rotation = facing.sqrMagnitude > .001f ? Quaternion.LookRotation(facing) : Quaternion.identity;
            points.Add(point);
        }
        if (points.Count < 4)
            throw new InvalidOperationException("Fewer than four safe reception standing positions found; inspect the authored chair approaches before saving.");
        waiting.standingPositions = points.ToArray();
        return points.Count;
    }

    private static float PlanarDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }

    public static void ApplyBubblePresentation(FineDiningReservations reservations)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || reservations == null)
            throw new InvalidOperationException("Edit Mode only.");
        reservations.waitingArea.reservationIndicatorPrefab = AuthorReservationIndicator();
        reservations.drinkRequestBubblePrefab = AuthorDrinkRequest();
        EditorUtility.SetDirty(reservations.waitingArea);
        EditorUtility.SetDirty(reservations);
    }

    private static GameObject AuthorReservationIndicator()
    {
        GameObject root = OpenPrefab(ReservationIndicatorPath, "FineDiningReservationIndicator");
        try
        {
            ConfigureRoot(root, new Vector2(152, 168), true);
            Ensure<Button>(root).targetGraphic = root.GetComponent<Image>();
            Ensure<CustomerGreetBubbleUI>(root);
            var icon = EnsureImage(root.transform, "TableIcon", LoadSprite(TableArt), new Vector2(100, 100), new Vector2(0, 14));
            icon.preserveAspect = true;
            var details = EnsureRect(root.transform, "Details");
            details.anchorMin = details.anchorMax = new Vector2(1, .5f);
            details.pivot = new Vector2(0, .5f);
            details.sizeDelta = new Vector2(280, 80);
            details.anchoredPosition = new Vector2(8, 0);
            var frame = Ensure<Image>(details.gameObject);
            var reference = AssetDatabase.LoadAssetAtPath<ManagementReservationPanel>("Assets/_Project/ManagementComputer/Prefabs/ManagementReservationsPanel.prefab");
            frame.sprite = reference != null && reference.computerPanelSprite != null ? reference.computerPanelSprite
                : reference != null && reference.cardNormal != null ? reference.cardNormal : LoadSprite(BubbleArt);
            frame.type = frame.sprite != null && frame.sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
            frame.color = new Color(.96f, .98f, 1f);
            frame.raycastTarget = false;
            var labelRect = EnsureRect(details, "ReservationDetails");
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10, 5); labelRect.offsetMax = new Vector2(-10, -5);
            var label = Ensure<TextMeshProUGUI>(labelRect.gameObject);
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFont);
            label.fontSize = 22;
            label.color = new Color(.08f, .2f, .28f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableAutoSizing = true;
            label.fontSizeMin = 16; label.fontSizeMax = 22;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            label.text = "";
            details.gameObject.SetActive(false);
            ApplyDefaultCustomerBubbleSizing(root);
            return PrefabUtility.SaveAsPrefabAsset(root, ReservationIndicatorPath);
        }
        finally { ClosePrefab(root); }
    }

    private static GameObject AuthorDrinkRequest()
    {
        GameObject root = OpenPrefab(DrinkRequestPath, "FineDiningDrinkRequest");
        try
        {
            ConfigureRoot(root, new Vector2(176, 200), true);
            var button = Ensure<Button>(root);
            button.targetGraphic = root.GetComponent<Image>();
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.highlightedColor = new Color(.9f, .96f, 1f);
            colors.pressedColor = new Color(.74f, .87f, 1f);
            colors.fadeDuration = .15f;
            button.colors = colors;
            var drink = AssetDatabase.LoadAssetAtPath<Recipe>("Assets/_Project/Office/Manager/Recipe/Fine Dining/Sparkling Water.asset");
            if (drink == null || drink.sprite == null) throw new InvalidOperationException("The existing drink recipe preview is missing.");
            var icon = EnsureImage(root.transform, "DrinkIcon", drink.sprite, new Vector2(88, 92), new Vector2(0, 34));
            icon.preserveAspect = true;
            var captionRect = EnsureRect(root.transform, "RequestKind");
            captionRect.anchorMin = captionRect.anchorMax = captionRect.pivot = new Vector2(.5f, .5f);
            captionRect.sizeDelta = new Vector2(136, 30);
            captionRect.anchoredPosition = new Vector2(0, -28);
            var caption = Ensure<TextMeshProUGUI>(captionRect.gameObject);
            caption.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFont);
            caption.fontSize = 22; caption.fontStyle = FontStyles.Bold;
            caption.enableAutoSizing = true; caption.fontSizeMin = 18; caption.fontSizeMax = 22;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.overflowMode = TextOverflowModes.Ellipsis;
            caption.alignment = TextAlignmentOptions.Center;
            caption.color = new Color(.08f, .2f, .28f); caption.raycastTarget = false;
            caption.text = "DRINK";
            var ui = Ensure<OrderBubbleUI>(root);
            ui.drinkImage = icon;
            var serialized = new SerializedObject(ui);
            serialized.FindProperty("optionalDrinkLabel").objectReferenceValue = caption;
            serialized.FindProperty("openButton").objectReferenceValue = button;
            serialized.FindProperty("patienceSlider").objectReferenceValue = null;
            serialized.FindProperty("greetingLabel").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ApplyDefaultCustomerBubbleSizing(root);
            return PrefabUtility.SaveAsPrefabAsset(root, DrinkRequestPath);
        }
        finally { ClosePrefab(root); }
    }

    private static void ApplyDefaultCustomerBubbleSizing(GameObject root)
    {
        // Use the established Lobby1/2 seating bubble, including Android and zoom scaling.
        var reference = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/UI/Assets/Legacy/Bubbles/CustomerGreetBubble.prefab");
        var frame = (RectTransform)reference.GetComponentInChildren<Button>(true).targetGraphic.transform;
        var rect = (RectTransform)root.transform;
        Vector2 ratio = new Vector2(frame.rect.width / rect.rect.width, frame.rect.height / rect.rect.height);
        foreach (var child in root.GetComponentsInChildren<RectTransform>(true))
        {
            if (child == rect) continue;
            child.sizeDelta = Vector2.Scale(child.sizeDelta, ratio);
            child.anchoredPosition = Vector2.Scale(child.anchoredPosition, ratio);
        }
        float textRatio = Mathf.Min(ratio.x, ratio.y);
        foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            label.fontSize *= textRatio;
            label.fontSizeMin *= textRatio;
            label.fontSizeMax *= textRatio;
        }
        rect.sizeDelta = frame.rect.size;
        rect.localScale = reference.transform.localScale;
        var source = new SerializedObject(reference.GetComponentInChildren<UIFollowWorldPoint>(true));
        var destination = new SerializedObject(root.GetComponent<UIFollowWorldPoint>());
        foreach (string field in new[] { "presentationSpace", "preserveScreenSize", "worldUnitsPerUiUnit",
            "visualScale", "mobileVisualScale", "mobileSafeAreaPadding", "dynamicPixelsPerUnit" })
            destination.CopyFromSerializedProperty(source.FindProperty(field));
        destination.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject OpenPrefab(string path, string name)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(path) != null
            ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name, typeof(RectTransform));
    }

    private static void ClosePrefab(GameObject root)
    {
        if (EditorSceneManager.IsPreviewSceneObject(root)) PrefabUtility.UnloadPrefabContents(root);
        else UnityEngine.Object.DestroyImmediate(root);
    }

    private static void ConfigureRoot(GameObject root, Vector2 size, bool interactive)
    {
        root.layer = LayerMask.NameToLayer("UI");
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = size; rect.localScale = Vector3.one;
        var image = Ensure<Image>(root);
        image.sprite = LoadSprite(BubbleArt); image.color = Color.white; image.preserveAspect = false;
        image.raycastTarget = interactive;
        var canvas = Ensure<Canvas>(root); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 100;
        var group = Ensure<CanvasGroup>(root); group.interactable = interactive; group.blocksRaycasts = interactive;
        var follow = new SerializedObject(Ensure<UIFollowWorldPoint>(root));
        follow.FindProperty("stickToTarget").boolValue = true;
        follow.FindProperty("avoidBubbleOverlaps").boolValue = false;
        follow.ApplyModifiedPropertiesWithoutUndo();
        if (interactive) Ensure<GraphicRaycaster>(root);
    }

    private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path)
        ?? throw new InvalidOperationException("Missing sprite: " + path);
    private static T Ensure<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        if (component == null) component = go.AddComponent<T>();
        return component;
    }
    private static RectTransform EnsureRect(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child == null) { child = new GameObject(name, typeof(RectTransform)).transform; child.SetParent(parent, false); }
        child.gameObject.layer = LayerMask.NameToLayer("UI");
        return (RectTransform)child;
    }
    private static Image EnsureImage(Transform parent, string name, Sprite sprite, Vector2 size, Vector2 position)
    {
        var rect = EnsureRect(parent, name);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size; rect.anchoredPosition = position;
        var image = Ensure<Image>(rect.gameObject);
        image.sprite = sprite; image.color = Color.white; image.raycastTarget = false;
        return image;
    }
}
#endif
