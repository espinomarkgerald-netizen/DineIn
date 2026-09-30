#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicit, menu-only checks; does not enter Play Mode or write player/save data.</summary>
public static class AlmanacValidation
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, Fields).GetValue(value);
    private static object Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, Fields).Invoke(value, args);
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Almanac check failed: " + message); }

    [MenuItem("Dine In/Almanac/Validate Reader and Previews")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run these checks in Edit Mode.");
        var menu = UnityEngine.Object.FindFirstObjectByType<AlmanacMenu>();
        Check(menu != null, "menu is installed");
        var catalog = AssetDatabase.LoadAssetAtPath<AlmanacCatalog>("Assets/_Project/UI/Almanac/AlmanacCatalog.asset");
        Check(catalog != null, "catalog is installed");
        var entries = catalog.LoadEntries();
        Check(catalog.entries.All(e => e != null), "null catalog entry");
        Check(catalog.entries.Select(e => e.entryId).Distinct().Count() == catalog.entries.Length, "duplicate catalog IDs");
        var issues = AlmanacContentAuthoring.ValidateCatalog();
        Check(issues.Trim() == "Entries: " + entries.Count, issues);
        var stage = menu.GetComponent<AlmanacPreviewStage>();
        Check(stage != null, "shared preview stage is installed");
        int models = 0, poses = 0, variants = 0, yawChecks = 0;
        int resolution = 0, samples = 0;
        var materialSnapshots = new Dictionary<Material, string>();
        menu.Close();
        try
        {
            ValidatePresentationAssets(menu, entries);
            Texture previous = null;
            foreach (var entry in entries.Where(e => e.previewKind != AlmanacPreviewKind.Image))
            {
                int count = Mathf.Max(1, entry.variants?.Length ?? 0);
                for (int variant = 0; variant < count; variant++)
                {
                    var source = entry.variants != null && variant < entry.variants.Length && entry.variants[variant]?.prefab != null
                        ? entry.variants[variant].prefab : entry.previewPrefab;
                    foreach (var material in source.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null))
                        if (!materialSnapshots.ContainsKey(material)) materialSnapshots.Add(material, EditorJsonUtility.ToJson(material));
                    Check(stage.Show(entry, variant), entry.entryId + " preview missing");
                    Check(previous == null || previous == stage.Texture, "RenderTexture was recreated between models");
                    previous = stage.Texture;
                    var texture = stage.Texture as RenderTexture;
                    var camera = Field<Camera>(stage, "previewCamera");
                    Check(texture != null && texture.width == texture.height && texture.width == stage.EffectiveTextureSize, "square preview texture resolution");
                    Check(texture.width >= 1024 && Mathf.Abs(camera.aspect - 1) < .001f, "desktop preview must provide at least 1024 square pixels");
                    Check(texture.filterMode == FilterMode.Bilinear && !texture.useMipMap, "preview UI sampling settings");
                    var descriptor = texture.descriptor; descriptor.msaaSamples = 4;
                    Check(texture.antiAliasing == Mathf.Max(1, SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor)), "supported preview MSAA");
                    resolution = texture.width; samples = texture.antiAliasing;
                    var model = Field<GameObject>(stage, "modelOffset");
                    Check(model.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "gameplay behaviours cloned into " + entry.entryId);
                    Check(model.GetComponentsInChildren<Collider>(true).Length == 0, "physics cloned into " + entry.entryId);
                    Check(model.GetComponentsInChildren<AudioSource>(true).Length == 0, "audio cloned into " + entry.entryId);
                    Check(model.GetComponentsInChildren<AudioListener>(true).Length == 0, "audio listener cloned into " + entry.entryId);
                    CheckMaterialsMatch(source.transform, model.transform.GetChild(0), entry.entryId);
                    foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>()) Check(skin.bones.All(b => b != null), entry.entryId + " null skin bone");
                    Check(Resources.FindObjectsOfTypeAll<Camera>().Count(c => c.name == "Preview Camera" && c.enabled) == 1, "multiple live preview cameras");
                    if (entry.previewKind == AlmanacPreviewKind.Character)
                    {
                        var animator = model.GetComponentsInChildren<Animator>().FirstOrDefault(a => a.avatar != null && a.avatar.isHuman);
                        Check(animator != null, entry.entryId + " needs a humanoid avatar");
                        var bone = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                        Check(bone != null, entry.entryId + " missing humanoid arm");
                        var start = bone.localRotation;
                        Call(stage, "EvaluateAnimation", .5f);
                        Check(Quaternion.Angle(start, bone.localRotation) > .005f, entry.entryId + " idle/intro did not move");
                        poses++;
                        // Every core customer is an acceptance case; include hat-bearing staff too.
                        if (variant == 0 && (entry.entryId == "staff-chef" || entry.category == AlmanacCategory.Customers))
                            yawChecks += CheckCharacterFraming(stage, model, camera, entry.entryId);
                    }
                    variants++;
                }
                models++;
            }
            foreach (var snapshot in materialSnapshots)
                Check(EditorJsonUtility.ToJson(snapshot.Key) == snapshot.Value, "preview mutated source material " + snapshot.Key.name);
            stage.Hide(); Check(stage.Texture == null, "texture not released on hide");

            var menuGroup = Field<CanvasGroup>(menu, "menuGroup");
            float alpha = menuGroup.alpha;
            bool interactable = menuGroup.interactable, blocks = menuGroup.blocksRaycasts;
            menu.Open(); Check(menu.IsOpen && !menuGroup.interactable && !menuGroup.blocksRaycasts, "modal capture");
            Check(!Field<GameObject>(menu, "overlay").GetComponentsInChildren<Transform>(true).Any(t => t.name == "Related stories"), "related-entry strip still exists");
            var raw = Field<RawImage>(menu, "liveImage");
            var aspect = raw.GetComponent<AspectRatioFitter>();
            Check(aspect != null && aspect.aspectMode == AspectRatioFitter.AspectMode.FitInParent && Mathf.Abs(aspect.aspectRatio - 1) < .001f, "live image stretches preview proportions");
            ValidateGroupedIndex(menu, entries, AlmanacCategory.Food);
            ValidateGroupedIndex(menu, entries, AlmanacCategory.Staff);
            ValidateSearchAndPaging(menu, entries);
            ValidateHistoryAndVariants(menu, entries);

            var cashier = entries.First(e => e.entryId == "staff-cashier");
            Call(menu, "Navigate", cashier, false);
            var detail = Field<CanvasGroup>(menu, "detailGroup");
            detail.alpha = .37f; detail.transform.localScale = Vector3.one * .987f;
            var fade = (IEnumerator)Call(menu, "ChangePage", cashier, 0);
            try
            {
                fade.MoveNext();
                Check(Mathf.Abs(detail.alpha - .37f) < .001f && !detail.interactable && !detail.blocksRaycasts, "interrupted page transition");
            }
            finally { (fade as IDisposable)?.Dispose(); }
            Call(menu, "Populate", cashier, 0);
            menu.Close();
            Check(stage.Texture == null && !menu.IsOpen, "close release");
            Check(Mathf.Abs(menuGroup.alpha - alpha) < .001f && menuGroup.interactable == interactable && menuGroup.blocksRaycasts == blocks, "menu state not restored");
            menu.Open(); Check(Field<CanvasGroup>(menu, "paperGroup").interactable, "reopen interaction"); menu.Close();
            var report = "PASS: " + entries.Count + " entries and all links/images; " + models + " model entries, " + variants + " visual variants, " + poses + " animated variants; " +
                resolution + "px square preview with " + samples + "x MSAA; one reused camera/texture; unchanged material copies; " + yawChecks + " sampled character yaw framings; " +
                "actual restaurant materials and Order Check capture; Food/Staff groups and headings; display-name/alias search, restored grouping, filtered paging/disabled edges, history query reset, labeled variants, no related strip, interrupted fade, close/reopen and menu restoration. " +
                "Edit Mode checks only: full live input/playback and mobile performance were not tested.";
            System.IO.Directory.CreateDirectory("../output/almanac");
            System.IO.File.WriteAllText("../output/almanac/validation.txt", report);
            Debug.Log("[Almanac] " + report);
        }
        finally { menu.Close(); stage.Release(); }
    }

    private static void ValidatePresentationAssets(AlmanacMenu menu, List<AlmanacEntryData> entries)
    {
        var restaurants = menu.gameObject.scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Restaurants");
        Check(restaurants != null, "authored district restaurants are present");
        var names = new Dictionary<string, string>
        {
            { "restaurant-casual-dining", "CasualDiningExterior (1)" },
            { "restaurant-fast-food", "FastFoodRestaurant_Exterior" },
            { "restaurant-fine", "diner (2)" }
        };
        foreach (var pair in names)
        {
            var entry = entries.Find(e => e.entryId == pair.Key);
            Check(entry != null && entry.icon != null && entry.previewPrefab != null, pair.Key + " presentation assets");
            var source = restaurants.transform.Find(pair.Value);
            Check(source != null, pair.Key + " authored district instance");
            CheckMaterialsMatch(source, entry.previewPrefab.transform, pair.Key + " district material identity");
            Check(entry.previewPrefab.GetComponentsInChildren<Renderer>(true).Length == source.GetComponentsInChildren<Renderer>(true).Length, pair.Key + " copied model completeness");
        }
        var order = entries.Find(e => e.entryId == "service-order-pad");
        Check(order != null && order.entryName == "Order Check" && order.previewKind == AlmanacPreviewKind.Image && order.widePreview, "current Order Check presentation");
        Check(AssetDatabase.GetAssetPath(order.icon) == "Assets/_Project/UI/Almanac/Thumbnails/service-order-ui.png", "Order Check uses obsolete notepad art");
        Check(order.icon.texture.width >= 1024, "Order Check capture resolution");
    }

    private static void CheckMaterialsMatch(Transform source, Transform copy, string label)
    {
        foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
        {
            var indices = new List<int>();
            for (var cursor = renderer.transform; cursor != copy; cursor = cursor.parent) indices.Add(cursor.GetSiblingIndex());
            var sourceTransform = source;
            for (int i = indices.Count - 1; i >= 0; i--)
            {
                Check(indices[i] < sourceTransform.childCount, label + " hierarchy correspondence");
                sourceTransform = sourceTransform.GetChild(indices[i]);
            }
            var original = sourceTransform.GetComponent(renderer.GetType()) as Renderer;
            Check(original != null && renderer.sharedMaterials.SequenceEqual(original.sharedMaterials), label + " altered source materials on " + renderer.name);
            if (renderer is SkinnedMeshRenderer skin)
                Check(skin.sharedMesh == ((SkinnedMeshRenderer)original).sharedMesh, label + " altered skin mesh");
            else if (renderer.TryGetComponent<MeshFilter>(out var mesh))
                Check(sourceTransform.TryGetComponent<MeshFilter>(out var originalMesh) && mesh.sharedMesh == originalMesh.sharedMesh, label + " altered source mesh");
        }
    }

    private static void ValidateGroupedIndex(AlmanacMenu menu, List<AlmanacEntryData> entries, AlmanacCategory category)
    {
        Call(menu, "SelectCategory", category);
        var expected = entries.Where(e => e.category == category).OrderBy(e => e.sectionOrder).ThenBy(e => e.listSection).ThenBy(e => e.entryName).ToList();
        Check(expected.Count > 1 && expected.All(e => !string.IsNullOrWhiteSpace(e.listSection)), category + " entries need meaningful groups");
        Check(Field<List<AlmanacEntryData>>(menu, "visible").SequenceEqual(expected), category + " displayed order");
        var content = Field<RectTransform>(menu, "indexContent");
        var actual = new List<string>();
        foreach (Transform child in content)
        {
            if (!child.gameObject.activeSelf) continue;
            if (child.name == "Section heading") actual.Add("H:" + child.GetComponent<TMP_Text>().text);
            else if (child.TryGetComponent<Button>(out _)) actual.Add("E:" + child.name);
        }
        var expectedRows = new List<string>();
        foreach (var section in expected.GroupBy(e => e.listSection))
        {
            expectedRows.Add("H:" + section.Key.ToUpperInvariant());
            expectedRows.AddRange(section.Select(e => "E:" + e.entryName));
        }
        Check(actual.SequenceEqual(expectedRows), category + " headings/cards are missing or interleaved");
        if (category == AlmanacCategory.Food)
        {
            var sections = expected.Select(e => e.listSection).Distinct().ToList();
            Check(sections.Contains("Dishes") && sections.Contains("Ingredients") && sections.IndexOf("Dishes") < sections.IndexOf("Ingredients"), "dishes must precede ingredients");
        }
    }

    private static void ValidateSearchAndPaging(AlmanacMenu menu, List<AlmanacEntryData> entries)
    {
        Call(menu, "SelectCategory", AlmanacCategory.Food);
        var search = Field<TMP_InputField>(menu, "search");
        var allFood = entries.Where(e => e.category == AlmanacCategory.Food).ToList();
        search.text = allFood[0].entryName.ToUpperInvariant();
        Check(Field<List<AlmanacEntryData>>(menu, "visible").Contains(allFood[0]), "case-insensitive display-name search");
        const string alias = "stock";
        Check(allFood.Any(e => (e.searchAliases ?? "").IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0 && e.entryName.IndexOf(alias, StringComparison.OrdinalIgnoreCase) < 0), "food alias search fixture");
        search.text = alias;
        var filtered = allFood.Where(e => (e.entryName + " " + e.searchAliases).IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        Check(filtered.Count > 1 && filtered.Count < allFood.Count && Field<List<AlmanacEntryData>>(menu, "visible").SequenceEqual(filtered), "alias filtering/order");
        Call(menu, "Navigate", filtered[0], false);
        Check(!Field<Button>(menu, "previousButton").interactable && Field<Button>(menu, "nextButton").interactable, "first filtered-entry navigation");
        Call(menu, "Step", -1); Check(Field<AlmanacEntryData>(menu, "selected") == filtered[0], "previous wraps filtered list");
        Call(menu, "Step", 1); Check(Field<AlmanacEntryData>(menu, "selected") == filtered[1], "next ignores filtered display order");
        Call(menu, "Navigate", filtered[filtered.Count - 1], false);
        Check(!Field<Button>(menu, "nextButton").interactable && Field<Button>(menu, "previousButton").interactable, "last filtered-entry navigation");
        Call(menu, "Step", 1); Check(Field<AlmanacEntryData>(menu, "selected") == filtered[filtered.Count - 1], "next wraps filtered list");
        search.text = "there-is-no-such-entry";
        Check(Field<List<AlmanacEntryData>>(menu, "visible").Count == 0, "empty search");
        Check(!Field<Button>(menu, "nextButton").interactable && !Field<Button>(menu, "previousButton").interactable, "empty-search navigation");
        search.text = "";
        Check(Field<List<AlmanacEntryData>>(menu, "visible").SequenceEqual(allFood), "clearing search does not restore group ordering");
        ValidateGroupedIndex(menu, entries, AlmanacCategory.Food);
    }

    private static void ValidateHistoryAndVariants(AlmanacMenu menu, List<AlmanacEntryData> entries)
    {
        var history = Field<List<string>>(menu, "history"); history.Clear(); Call(menu, "RefreshNavigation");
        Check(!Field<Button>(menu, "backButton").gameObject.activeSelf, "Back duplicates close at root browsing state");
        var cashier = entries.First(e => e.entryId == "staff-cashier");
        var chef = entries.First(e => e.entryId == "staff-chef");
        var family = entries.First(e => e.entryId == "customer-family");
        Call(menu, "Navigate", cashier, false); Call(menu, "Navigate", chef, true);
        var search = Field<TMP_InputField>(menu, "search"); search.text = chef.entryName;
        Call(menu, "GoBack");
        Check(Field<AlmanacEntryData>(menu, "selected") == cashier && search.text == "" && Field<List<AlmanacEntryData>>(menu, "visible").Contains(cashier), "Back must restore browsing history and clear stale search");
        Check(Field<ScrollRect>(menu, "variantScroll").gameObject.activeSelf && Field<TMP_Text>(menu, "variantLabel").gameObject.activeSelf && Field<TMP_Text>(menu, "variantLabel").text == "UNIFORM / RESTAURANT", "staff uniform selector has no clear label");
        Call(menu, "SelectVariant", cashier, cashier.variants.Length - 1);
        Check(Field<AlmanacEntryData>(menu, "selected") == cashier, "variant changes selection");
        Call(menu, "SelectVariant", family, 0);
        Check(Field<AlmanacEntryData>(menu, "selected") == cashier && Field<TMP_Text>(menu, "title").text == cashier.entryName.ToUpperInvariant(), "stale variant replaced current page");
        Call(menu, "Navigate", family, false);
        Check(!Field<ScrollRect>(menu, "variantScroll").gameObject.activeSelf && !Field<TMP_Text>(menu, "variantLabel").gameObject.activeSelf, "unneeded variant strip remains visible");
    }

    private static void CheckRenderedCharacter(AlmanacPreviewStage stage, Camera camera, string label, int yaw)
    {
        // Inspect actual rasterized pixels as well as bounds: the previous bounds-only
        // check repeated the skin-scale error and accepted nearly invisible customers.
        var rt = (RenderTexture)stage.Texture;
        var previous = RenderTexture.active;
        var pixels = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        try
        {
            camera.Render(); RenderTexture.active = rt;
            pixels.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); pixels.Apply();
            var colors = pixels.GetPixels32();
            int minX=rt.width, minY=rt.height, maxX=-1, maxY=-1, count=0;
            for (int i=0; i<colors.Length; i++)
            {
                if (colors[i].a < 64) continue;
                int x=i%rt.width, y=i/rt.width;
                minX=Mathf.Min(minX,x); minY=Mathf.Min(minY,y);
                maxX=Mathf.Max(maxX,x); maxY=Mathf.Max(maxY,y); count++;
            }
            Check(count > colors.Length*.04f, label + " has no substantial visible character pixels");
            float height=(maxY-minY+1f)/rt.height;
            Check(height>.60f && height<.95f, label + " rendered height " + height + " at yaw " + yaw*90);
            Check(minX>rt.width*.02f && maxX<rt.width*.98f && minY>rt.height*.09f && maxY<rt.height*.98f,
                label + " rendered body/antennae clipped or feet overlap the hint at yaw " + yaw*90);
            Check(Mathf.Abs((minX+maxX)*.5f/rt.width-.5f)<.10f, label + " visual center drifts during rotation");
            string folder=System.IO.Path.GetFullPath("../output/almanac/framing");
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,"verified-"+label+"-"+yaw+".png"),pixels.EncodeToPNG());
        }
        finally { RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(pixels); }
    }

    private static int CheckCharacterFraming(AlmanacPreviewStage stage, GameObject model, Camera camera, string label)
    {
        var turntable = Field<Transform>(stage, "turntable");
        var rotation = turntable.localRotation;
        var baked = new Mesh();
        try
        {
            for (int yaw = 0; yaw < 4; yaw++)
            {
                turntable.localRotation = Quaternion.AngleAxis(yaw * 90, Vector3.up) * rotation;
                float minY = 1, maxY = 0;
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled) continue;
                    Bounds bounds;
                    if (renderer is SkinnedMeshRenderer skin)
                    {
                        baked.Clear(); skin.BakeMesh(baked, true); baked.RecalculateBounds(); bounds = baked.bounds;
                    }
                    else if (renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null) bounds = filter.sharedMesh.bounds;
                    else continue;
                    // Imported renderer bounds often include unrelated poses. Project the
                    // current visible geometry instead, including the actual hat meshes.
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var sign = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                        var point = camera.WorldToViewportPoint(renderer.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, sign)));
                        Check(point.z > 0 && point.x >= -.025f && point.x <= 1.025f && point.y >= -.025f && point.y <= 1.025f, label + " sampled hat/body cropped at yaw " + yaw * 90);
                        minY = Mathf.Min(minY, point.y); maxY = Mathf.Max(maxY, point.y);
                    }
                }
                Check(maxY - minY > .60f, label + " remains tiny in the portrait frame");
                var position = camera.transform.position;
                var size = camera.orthographicSize;
                Call(stage, "EvaluateAnimation", .7f);
                Check(camera.transform.position == position && camera.orthographicSize == size, label + " camera chases the idle pose");
                CheckRenderedCharacter(stage, camera, label, yaw);
            }
            return 4;
        }
        finally { turntable.localRotation = rotation; UnityEngine.Object.DestroyImmediate(baked); }
    }
}
#endif