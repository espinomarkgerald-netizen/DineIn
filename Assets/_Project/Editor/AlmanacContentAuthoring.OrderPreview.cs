#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static partial class AlmanacContentAuthoring
{
    [MenuItem("Dine In/Almanac/Render Current Order Preview")]
    private static void RenderOrderPreviewMenu() => Debug.Log("[Almanac] Captured " + RenderOrderPreview());

    /// <summary>Captures the actual order UI with an illustrative catalog order, never a live customer.</summary>
    public static string RenderOrderPreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Capture the order UI outside Play Mode.");
        const string sourcePath = "Assets/_Project/Scenes/RoleBased/Lobby1.unity";
        const string imagePath = "Assets/_Project/UI/Almanac/Thumbnails/service-order-ui.png";
        var catalog = AssetDatabase.LoadAssetAtPath<MenuCatalog>("Assets/_Project/Resources/CasualDiningMenuCatalog.asset");
        if (catalog == null) throw new InvalidOperationException("Missing Casual Dining menu catalog.");
        var soup = catalog.Products.Single(recipe => recipe != null && recipe.ProductId == "casual-tomato-soup");
        var drink = catalog.Products.Single(recipe => recipe != null && recipe.ProductId == "casual-iced-tea-pitcher");
        Scene sourceScene = default, previewScene = default;
        var priorRenderTarget = RenderTexture.active;
        RenderTexture target = null;
        Texture2D pixels = null;
        try
        {
            sourceScene = EditorSceneManager.OpenPreviewScene(sourcePath);
            var source = sourceScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<OrderChecklistUI>(true)).Single();
            previewScene = EditorSceneManager.NewPreviewScene();
            var canvasObject = new GameObject("Almanac Order UI Capture", typeof(RectTransform), typeof(Canvas));
            canvasObject.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(canvasObject, previewScene);
            canvasObject.SetActive(false);
            var canvas = canvasObject.GetComponent<Canvas>();
            var sourceCanvas = source.GetComponentInParent<Canvas>();
            if (sourceCanvas != null) EditorUtility.CopySerialized(sourceCanvas, canvas);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 0;
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.anchorMin = canvasRect.anchorMax = canvasRect.pivot = Vector2.one * .5f;
            canvasRect.sizeDelta = new Vector2(800, 450);
            canvasRect.position = Vector3.zero;
            canvasRect.rotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one * .01f;

            // The inactive parent prevents gameplay callbacks when copying the saved panel.
            var panel = UnityEngine.Object.Instantiate(source.gameObject, canvas.transform, false);
            var order = panel.GetComponent<OrderChecklistUI>();
            foreach (var behaviour in panel.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != order && !(behaviour is UIBehaviour) && !(behaviour is NotepadMenuEntryUI))
                    UnityEngine.Object.DestroyImmediate(behaviour);
            order.enabled = false;
            OrderPreviewInvoke(order, "ResolveUIReferences");
            OrderPreviewInvoke(order, "ResolveNotepadFont");
            OrderPreviewSet(order, "catalog", catalog);
            OrderPreviewSet(order, "useTypewriter", false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.pivot = Vector2.one * .5f;
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(800, 450); // Authored .5 scale gives the same 800 x 450 screen coverage.
            panelRect.localScale = Vector3.one * .5f;

            // Run only layout routines. Awake/Open/Confirm/RebuildMenu touch runtime services and are never called.
            foreach (string field in new[] { "foodContentRoot", "drinkContentRoot", "requestedIconsRoot", "availableItemsRoot" })
                OrderPreviewClear(OrderPreviewRead<RectTransform>(order, field));
            OrderPreviewInvoke(order, "ApplyCustomerMessageBounds");
            OrderPreviewInvoke(order, "ApplyCustomerInformationLayout");
            OrderPreviewInvoke(order, "ApplyMenuOrderPanelLayout");
            var foodScroll = OrderPreviewRead<ScrollRect>(order, "foodScrollRect");
            var drinkScroll = OrderPreviewRead<ScrollRect>(order, "drinkScrollRect");
            var foodRoot = (RectTransform)OrderPreviewInvoke(order, "EnsureMenuLayout", OrderPreviewRead<RectTransform>(order, "foodContentRoot"), foodScroll);
            var drinkRoot = (RectTransform)OrderPreviewInvoke(order, "EnsureMenuLayout", OrderPreviewRead<RectTransform>(order, "drinkContentRoot"), drinkScroll);
            OrderPreviewSet(order, "foodContentRoot", foodRoot);
            OrderPreviewSet(order, "drinkContentRoot", drinkRoot);
            var rowPrefab = OrderPreviewRead<NotepadMenuEntryUI>(order, "menuEntryPrefab") ??
                AssetDatabase.LoadAssetAtPath<NotepadMenuEntryUI>("Assets/_Project/Restaurant/UI/Prefabs/Notepad Menu Item.prefab");
            if (rowPrefab == null) throw new InvalidOperationException("Missing current order item prefab.");
            var style = OrderPreviewRead<NotepadMenuVisualStyle>(order, "menuStyle");
            var rows = new List<NotepadMenuEntryUI>();
            foreach (var recipe in catalog.Products.Where(recipe => recipe != null && recipe.availableOnMenu).OrderBy(recipe => recipe.menuSortOrder))
            {
                var row = NotepadMenuEntryUI.Create(rowPrefab,
                    recipe.category == MenuProductCategory.Drink ? drinkRoot : foodRoot, style);
                row.enabled = false;
                OrderPreviewClear(OrderPreviewRead<RectTransform>(row, "iconRoot"));
                row.Bind(recipe);
                rows.Add(row);
            }
            // Runtime SetIcons uses Destroy, so remove temporary children before its next layout pass in Edit Mode.
            foreach (var row in rows) OrderPreviewClear(OrderPreviewRead<RectTransform>(row, "iconRoot"));
            OrderPreviewInvoke(order, "FinalizeMenuLayout", foodRoot, foodScroll);
            OrderPreviewInvoke(order, "FinalizeMenuLayout", drinkRoot, drinkScroll);
            foreach (var row in rows)
            {
                // Illustrative screen-local stock/quantity only: no inventory, unlock or save is changed.
                var rowStyle = OrderPreviewRead<NotepadMenuVisualStyle>(row, "style");
                OrderPreviewSet(row, "availableQuantity", 12);
                OrderPreviewSet(row, "canSelect", true);
                OrderPreviewRead<Image>(row, "background").color = rowStyle.entryColor;
                OrderPreviewRead<TMP_Text>(row, "statusText").text = row.Product.category == MenuProductCategory.Drink ? "Drink" : "Food";
                row.SetQuantityWithoutNotify(row.Product == soup || row.Product == drink ? 1 : 0);
            }

            var requested = OrderPreviewRead<List<Recipe>>(order, "requestedProducts");
            requested.Clear(); requested.Add(soup); requested.Add(drink);
            OrderPreviewRead<List<CustomerGroup.OrderLine>>(order, "requestedOrderLines").Clear();
            OrderPreviewSet(order, "cachedOpeningMessage", "Hello!");
            OrderPreviewSet(order, "cachedCustomerTypeName", "Regular");
            var almanac = AssetDatabase.LoadAssetAtPath<AlmanacCatalog>(Root + "/AlmanacCatalog.asset");
            var green = almanac != null ? almanac.LoadEntries().FirstOrDefault(entry => entry.entryId == "customer-regular") : null;
            OrderPreviewSet(order, "cachedCustomerImage", green != null ? green.icon : null);
            OrderPreviewInvoke(order, "RefreshCustomerTypeUI");
            OrderPreviewInvoke(order, "RefreshMessageFromRequestedOrder");
            OrderPreviewInvoke(order, "RebuildRequestedIcons");
            OrderPreviewRead<TMP_Text>(order, "tableNumberText").text = "Table 1";
            OrderPreviewInvoke(order, "RebuildAvailableItems", catalog.Products.Where(recipe => recipe != null && recipe.category == MenuProductCategory.Food).ToList());
            var availability = OrderPreviewRead<RectTransform>(order, "availableItemsRoot");
            if (availability != null)
                foreach (var text in availability.GetComponentsInChildren<TMP_Text>(true))
                    if (text.text.Contains(":")) text.text = text.text.Substring(0, text.text.LastIndexOf(':') + 1) + " 12";
            OrderPreviewRead<GameObject>(order, "reviewOverlay")?.SetActive(false);
            OrderPreviewInvoke(order, "ShowFoodTab");
            OrderPreviewRead<Button>(order, "confirmButton").GetComponentInChildren<TMP_Text>(true).text = "CHECK ORDER";
            foreach (var group in panel.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1;
            foreach (var child in canvasObject.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;

            var cameraObject = new GameObject("Almanac Order Capture Camera", typeof(Camera));
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = previewScene;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 2.25f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 20;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.94f, .925f, .86f, 1);
            camera.cullingMask = 1 << 30;
            canvas.worldCamera = camera;
            target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear };
            target.Create(); camera.targetTexture = target;
            panel.SetActive(true); canvasObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            OrderPreviewInvoke(order, "FitNotepadColumns", panelRect);
            Canvas.ForceUpdateCanvases();
            OrderPreviewFrameGraphics(camera, panel, 1600f / 900f);
            camera.Render();
            RenderTexture.active = target;
            pixels = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
            EnsureFolder("Assets/_Project/UI/Almanac/Thumbnails");
            File.WriteAllBytes(imagePath, pixels.EncodeToPNG());
            AssetDatabase.ImportAsset(imagePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            var entry = AssetDatabase.LoadAssetAtPath<AlmanacEntryData>("Assets/Resources/Almanac/service-order-pad.asset");
            if (entry != null)
            {
                entry.icon = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
                entry.previewKind = AlmanacPreviewKind.Image;
                entry.widePreview = true;
                const string sampleNote = "Illustrative UI-only example: Tomato Soup x1 and Iced Tea Pitcher x1, with sample displayed stock of 12. Captured from the actual Lobby1 OrderChecklistUI and Notepad Menu Item prefab; no customer, campaign, order, inventory or save state was changed.";
                if (string.IsNullOrEmpty(entry.sourceNotes) || !entry.sourceNotes.Contains(sampleNote)) entry.sourceNotes += "\n" + sampleNote;
                EditorUtility.SetDirty(entry); AssetDatabase.SaveAssetIfDirty(entry);
            }
            return imagePath;
        }
        finally
        {
            RenderTexture.active = priorRenderTarget;
            if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
            if (sourceScene.IsValid()) EditorSceneManager.ClosePreviewScene(sourceScene);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    private static void OrderPreviewFrameGraphics(Camera camera, GameObject panel, float aspect)
    {
        // Fit the authored UI without moving it. Clip masked scroll content before calculating its bounds.
        Rect? visibleBounds = null;
        foreach (var graphic in panel.GetComponentsInChildren<Graphic>())
        {
            if (!graphic.isActiveAndEnabled || graphic.color.a <= 0f) continue;
            Rect bounds = OrderPreviewWorldRect(graphic.rectTransform);
            for (var ancestor = graphic.transform.parent; ancestor != null; ancestor = ancestor.parent)
            {
                var mask = ancestor.GetComponent<Mask>();
                var rectMask = ancestor.GetComponent<RectMask2D>();
                if ((mask != null && mask.isActiveAndEnabled) || (rectMask != null && rectMask.isActiveAndEnabled))
                {
                    Rect clip = OrderPreviewWorldRect((RectTransform)ancestor);
                    bounds = Rect.MinMaxRect(Mathf.Max(bounds.xMin, clip.xMin), Mathf.Max(bounds.yMin, clip.yMin),
                        Mathf.Min(bounds.xMax, clip.xMax), Mathf.Min(bounds.yMax, clip.yMax));
                    if (bounds.width <= 0f || bounds.height <= 0f) break;
                }
            }
            if (bounds.width <= 0f || bounds.height <= 0f) continue;
            visibleBounds = visibleBounds.HasValue
                ? Rect.MinMaxRect(Mathf.Min(visibleBounds.Value.xMin, bounds.xMin), Mathf.Min(visibleBounds.Value.yMin, bounds.yMin),
                    Mathf.Max(visibleBounds.Value.xMax, bounds.xMax), Mathf.Max(visibleBounds.Value.yMax, bounds.yMax))
                : bounds;
        }
        if (!visibleBounds.HasValue) throw new InvalidOperationException("No visible Order Check graphics to capture.");
        var frame = visibleBounds.Value;
        camera.transform.position = new Vector3(frame.center.x, frame.center.y, -10f);
        camera.orthographicSize = Mathf.Max(frame.height * .5f, frame.width / (2f * aspect)) * 1.035f;
    }

    private static Rect OrderPreviewWorldRect(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners.Min(point => point.x), corners.Min(point => point.y),
            corners.Max(point => point.x), corners.Max(point => point.y));
    }
    private static T OrderPreviewRead<T>(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(target.GetType().Name, name);
        return (T)field.GetValue(target);
    }
    private static void OrderPreviewSet(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }
    private static object OrderPreviewInvoke(object target, string name, params object[] arguments)
    {
        var method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length);
        return method.Invoke(method.IsStatic ? null : target, arguments);
    }
    private static void OrderPreviewClear(Transform root)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject);
    }
}
#endif
