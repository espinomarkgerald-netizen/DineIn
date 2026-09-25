using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

public static partial class FastFoodCozyPolishAuthoring
{
    const string FeedbackFolder = "Assets/_Project/Restaurant/CookingAssets/Feedback";

    [MenuItem("Dine In/Fast Food/Apply Cozy Gameplay Polish")]
    public static string ApplyGameplay()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before saving kitchen objects.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Lobby2") throw new InvalidOperationException("Open Lobby2 first.");
        var controller = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FastFoodCookingController>(true)).Single();
        var view = controller.GetComponent<FastFoodCookingView>();
        Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "Cozy cooking gameplay");
        foreach (var rig in controller.GetComponentsInChildren<FastFoodCookingStation>(true))
            if (rig.mode != FastFoodStationMode.Assembler) AuthorOwnedSlots(rig);
        AuthorGriddleTops(controller);
        AuthorStationSignVisibility(view);
        AuthorGameplayUI(view);
        AuthorDrinkGlasses();
        AuthorGameplayAudio(controller, view);
        foreach (var recipe in MenuCatalog.Default.Products.Where(r => r.category == MenuProductCategory.Food))
        {
            Undo.RecordObject(recipe, "Readable cooking stages");
            if (recipe.kitchenItemType == ItemTypeKitchen.Fries || recipe.kitchenItemType == ItemTypeKitchen.ChickenNuggets)
                recipe.kitchenIngredientIcon = recipe.sprite;
            if (recipe.kitchenRawColorMultiplier == Color.white)
                recipe.kitchenRawColorMultiplier = recipe.kitchenItemType == ItemTypeKitchen.Burger ? new Color(1.15f, .65f, .68f) : new Color(1.08f, 1.04f, .88f);
            EditorUtility.SetDirty(recipe);
        }
        EditorUtility.SetDirty(controller); EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Lobby2.");
        return "Saved 8 shared grill positions and 8 shared fryer positions, editable effects, glass drinks, quiet feedback audio, and compact animated UI.";
    }

    static void AuthorOwnedSlots(FastFoodCookingStation rig)
    {
        var original = rig.GetComponentsInChildren<FastFoodCookingDropTarget>(true)
            .Where(b => b.kind == 0 && b.name.StartsWith("COOK", StringComparison.Ordinal) && b.foodAnchor != null)
            .OrderBy(b => b.transform.position.x).ToArray();
        int expected = rig.mode == FastFoodStationMode.Grill ? 2 : 4;
        if (original.Length != expected) throw new InvalidOperationException(rig.name + " needs its " + expected + " original physical bays.");
        var root = rig.transform.Find("Cooking Positions");
        if (root == null) { var go = new GameObject("Cooking Positions"); Undo.RegisterCreatedObjectUndo(go, "Author cooking positions"); root = go.transform; root.SetParent(rig.transform, false); }
        var slots = new List<FastFoodCookingDropTarget>();
        int perBay = rig.mode == FastFoodStationMode.Grill ? 4 : 2;
        for (int b = 0; b < original.Length; b++)
        {
            var source = original[b];
            var collider = source.GetComponent<BoxCollider>();
            var size = Vector3.Scale(collider.size, source.transform.lossyScale);
            var owner = b < original.Length / 2 ? FastFoodCookingSlotOwner.Staff : FastFoodCookingSlotOwner.Player;
            for (int n = 0; n < perBay; n++)
            {
                string name = owner + " " + rig.mode + " " + (b + 1) + " Position " + (n + 1);
                var child = root.Find(name);
                var target = child != null ? child.GetComponent<FastFoodCookingDropTarget>() : null;
                if (target == null)
                {
                    var go = new GameObject(name, typeof(BoxCollider), typeof(FastFoodCookingDropTarget));
                    Undo.RegisterCreatedObjectUndo(go, "Author cooking position"); go.transform.SetParent(root, false);
                    target = go.GetComponent<FastFoodCookingDropTarget>(); target.kind = 0; target.owner = owner;
                    float x = perBay == 4 ? (n % 2 == 0 ? -.23f : .23f) * size.x : 0;
                    float z = (n / (perBay == 4 ? 2 : 1) == 0 ? -.23f : .23f) * size.z;
                    var offset = source.transform.right * x + source.transform.forward * z;
                    target.transform.SetPositionAndRotation(source.transform.position + offset, source.transform.rotation);
                    var hit = go.GetComponent<BoxCollider>(); hit.isTrigger = true;
                    hit.center = new Vector3(0, .06f, 0); hit.size = new Vector3(size.x / (perBay == 4 ? 2 : 1) * .94f, .25f, size.z * .47f);
                    var anchor = new GameObject("Food Anchor"); anchor.transform.SetParent(target.transform, false);
                    anchor.transform.position = source.foodAnchor.position + offset;
                    if (rig.mode == FastFoodStationMode.Fry)
                    {
                        var oil = source.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "Oil Surface");
                        if (oil == null) throw new InvalidOperationException("Missing saved oil surface on " + source.name);
                        var position = anchor.transform.position; position.y = oil.bounds.center.y - .035f; anchor.transform.position = position;
                    }
                    target.foodAnchor = anchor.transform;
                    var timer = UnityEngine.Object.Instantiate(source.timer.gameObject, rig.labels);
                    timer.name = name + " Timer"; target.timer = timer.GetComponent<FastFoodCookingTimer>(); target.status = target.timer.seconds;
                    ((RectTransform)timer.transform).sizeDelta = new Vector2(64, 64);
                    target.timer.seconds.fontSize = 24; target.timer.caption.fontSize = 16; target.timer.worldLift = .16f; target.timer.screenOffset = new Vector2(0, 30);
                    timer.transform.Find("Circle Centre").GetComponent<UnityEngine.UI.Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GreyCircle);
                    timer.SetActive(false);
                    if (source.hint != null)
                    {
                        target.hint = UnityEngine.Object.Instantiate(source.hint, rig.labels); target.hint.name = name + " Hint";
                        target.hint.GetComponentInChildren<TMP_Text>(true).text = "DROP HERE"; target.hint.SetActive(false);
                    }
                    target.cookingFeedback = UnityEngine.Object.Instantiate(source.cookingFeedback, anchor.transform);
                    target.cookingFeedback.name = rig.mode == FastFoodStationMode.Fry ? "Frying Bubbles" : "Grill Steam";
                    target.cookingFeedback.transform.localPosition = Vector3.up * .055f;
                    target.cookingFeedback.transform.localScale = Vector3.one;
                    AuthorGentleParticles(target.cookingFeedback, rig.mode == FastFoodStationMode.Fry);
                    target.cookingFeedback.SetActive(false);
                }
                var dropBox = target.GetComponent<BoxCollider>();
                if (Mathf.Approximately(dropBox.size.y,.25f) && Mathf.Approximately(dropBox.center.y,.06f))
                {
                    // Keep the drop surface below the food so PhysicsRaycaster can
                    // pick up the cooked model rather than hitting an invisible box.
                    dropBox.center = target.transform.InverseTransformPoint(target.foodAnchor.position) - Vector3.up * .04f;
                    dropBox.size = new Vector3(dropBox.size.x,.06f,dropBox.size.z);
                }
                target.owner = FastFoodCookingSlotOwner.Player;
                target.gameObject.layer = source.gameObject.layer; target.slotIndex = slots.Count;
                EditorUtility.SetDirty(target); slots.Add(target);
            }
            // Preserve the user's physical bay and oil transforms. Only their former
            // single-slot hitbox is retired; the new saved child positions own input.
            source.enabled = false; collider.enabled = false;
            var renderer = source.GetComponent<Renderer>(); if (renderer != null) renderer.enabled = false;
            if (source.timer != null) source.timer.gameObject.SetActive(false);
            if (source.hint != null) source.hint.SetActive(false);
            if (source.cookingFeedback != null) source.cookingFeedback.SetActive(false);
            EditorUtility.SetDirty(source);
        }
        rig.cookingSlots = slots.ToArray(); rig.cooking = slots.First(s => s.owner == FastFoodCookingSlotOwner.Player);
        EditorUtility.SetDirty(rig);
    }

    static void AuthorGriddleTops(FastFoodCookingController controller)
    {
        var scene = controller.gameObject.scene;
        var rig = controller.GetComponentsInChildren<FastFoodCookingStation>(true).Single(r => r.mode == FastFoodStationMode.Grill);
        foreach (var filter in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true))
            .Where(f => (f.name == "Grill.002" || f.name == "Grill.003") && f.transform.parent != null && f.transform.parent.name == "Fast Food Revamp"))
        {
            string path = Batch + filter.name + " Cooktop.asset";
            if (AssetDatabase.GetAssetPath(filter.sharedMesh) == path) continue;
            var renderer = filter.GetComponent<MeshRenderer>();
            var source = filter.sharedMesh;
            if (renderer == null || renderer.sharedMaterials.Length < 3 || source.subMeshCount != 3)
                throw new InvalidOperationException("Inspect the changed griddle mesh before applying polish: " + filter.name);
            var vertices = source.vertices; var kept = new List<int>(); var top = new List<int>();
            float topY = 0; var triangles = source.GetTriangles(0);
            float anchorY = rig.Slots.First().foodAnchor.position.y;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = filter.transform.TransformPoint(vertices[triangles[i]]);
                var b = filter.transform.TransformPoint(vertices[triangles[i+1]]);
                var c = filter.transform.TransformPoint(vertices[triangles[i+2]]);
                var cross = Vector3.Cross(b-a,c-a); float y = (a.y+b.y+c.y)/3;
                bool cooktop = Vector3.Dot(cross.normalized,Vector3.up) > .95f && cross.magnitude * .5f > .5f && y < anchorY && y > anchorY-.4f;
                var destination = cooktop ? top : kept;
                destination.Add(triangles[i]); destination.Add(triangles[i+1]); destination.Add(triangles[i+2]);
                if (cooktop) topY = y;
            }
            if (top.Count != 6) throw new InvalidOperationException("Expected just the two cooking-plane triangles on " + filter.name);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = UnityEngine.Object.Instantiate(source); mesh.name = filter.name + " Cooktop";
                mesh.subMeshCount = 4; mesh.SetTriangles(kept,0); mesh.SetTriangles(top,3); AssetDatabase.CreateAsset(mesh,path);
            }
            Undo.RecordObjects(new UnityEngine.Object[]{filter,renderer},"Use authored dark cooking surface");
            var materials = renderer.sharedMaterials.ToList(); materials.Add(materials[2]);
            filter.sharedMesh = mesh; renderer.sharedMaterials = materials.ToArray();
            EditorUtility.SetDirty(filter); EditorUtility.SetDirty(renderer);
            foreach (var slot in rig.Slots.Where(s => Mathf.Abs(s.foodAnchor.position.x-renderer.bounds.center.x) < renderer.bounds.extents.x))
            {
                var position = slot.foodAnchor.position; position.y = topY + .015f; slot.foodAnchor.position = position;
                var box = slot.GetComponent<BoxCollider>(); box.center = slot.transform.InverseTransformPoint(position)-Vector3.up*.04f;
                EditorUtility.SetDirty(slot.foodAnchor); EditorUtility.SetDirty(box);
            }
        }
    }

    static void AuthorStationSignVisibility(FastFoodCookingView view)
    {
        var data = new SerializedObject(view); var list = data.FindProperty("staffNameplates");
        var signs = view.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TMP_Text>(true))
            .Where(t => t.text == "Grill Station" || t.text == "Fry Station")
            .Select(t => t.GetComponentInParent<Canvas>()).Where(c => c != null && c.renderMode == RenderMode.WorldSpace).Distinct();
        foreach (var canvas in signs)
        {
            bool present = false;
            for (int i=0;i<list.arraySize;i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == canvas) present = true;
            if (!present) { int index = list.arraySize++; list.GetArrayElementAtIndex(index).objectReferenceValue = canvas; }
        }
        data.ApplyModifiedProperties();
    }

    static void AuthorGentleParticles(GameObject root, bool fryer)
    {
        foreach (var particles in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.loop = true; main.playOnAwake = true; main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .8f);
            main.startSize = new ParticleSystem.MinMaxCurve(fryer ? .035f : .065f, fryer ? .075f : .12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.06f, fryer ? .12f : .22f);
            main.startColor = fryer ? new Color(1, .88f, .48f, .65f) : new Color(1, .97f, .88f, .25f);
            main.gravityModifier = 0;
            var emission = particles.emission; emission.rateOverTime = fryer ? 5 : 4;
            var shape = particles.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(.38f, .025f, .38f); shape.position = Vector3.zero;
            var color = particles.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.2f),new GradientAlphaKey(0,1)}); color.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>(); renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
    }

    static void Fixed(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    { rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = size; }
    static void Reveal(GameObject root)
    {
        var reveal = root.GetComponent<UIRevealAnimation>() ?? Undo.AddComponent<UIRevealAnimation>(root);
        reveal.ConfigureForEditor(true, .18f, 0, .96f, Vector2.zero); EditorUtility.SetDirty(reveal);
    }
    static void AuthorGameplayUI(FastFoodCookingView view)
    {
        var data = new SerializedObject(view);
        T Ref<T>(string name) where T : UnityEngine.Object => (T)data.FindProperty(name).objectReferenceValue;
        var station = Ref<RectTransform>("station"); var heading = Ref<TMP_Text>("heading"); var goal = Ref<TMP_Text>("progress");
        var header = (RectTransform)heading.transform.parent; header.sizeDelta = new Vector2(650, 112);
        Stretch(heading.rectTransform, new Vector2(.04f,.6f), new Vector2(.96f,.94f));
        Stretch(goal.rectTransform, new Vector2(.04f,.20f), new Vector2(.96f,.62f));
        var staff = Ref<TMP_Text>("staffActivity"); var staffPanel = Child(station, "Staff Activity Card");
        Fixed(staffPanel, new Vector2(0,1), new Vector2(156,-144), new Vector2(650,38));
        var staffImage = staffPanel.GetComponent<UnityEngine.UI.Image>() ?? Undo.AddComponent<UnityEngine.UI.Image>(staffPanel.gameObject);
        staffImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Restaurant/CookingAssets/Theme/Grey depth_flat.asset");
        staffImage.type = UnityEngine.UI.Image.Type.Sliced; staffImage.color = Color.white; staffImage.raycastTarget = false;
        staff.transform.SetParent(staffPanel, false); Stretch(staff.rectTransform, new Vector2(.025f,.08f),new Vector2(.975f,.94f));
        staff.color = Ink; staff.fontSharedMaterial = staff.font.material; staff.fontSize = 21;
        var tickets = Ref<RectTransform>("tickets"); tickets.anchoredPosition = new Vector2(32,-202);
        var notification = Ref<RectTransform>("notification"); notification.sizeDelta = new Vector2(340,300);
        var noticeLabel = Ref<TMP_Text>("noticeButtonLabel");
        FastFoodCookingView.ApplyNaturalUIAssetColors(noticeLabel.transform.parent.gameObject);
        var go = notification.Find("Go to Restock").gameObject; FastFoodCookingView.ApplyNaturalUIAssetColors(go);
        var card = Child(notification,"Stock Notice Card"); Stretch(card,new Vector2(.04f,.27f),new Vector2(.96f,.95f));
        var image = card.GetComponent<UnityEngine.UI.Image>() ?? Undo.AddComponent<UnityEngine.UI.Image>(card.gameObject);
        image.sprite = staffImage.sprite; image.type = UnityEngine.UI.Image.Type.Sliced; image.color = Color.white; image.raycastTarget = false;
        var alerts = Ref<TMP_Text>("alerts"); alerts.transform.SetParent(card,false); Stretch(alerts.rectTransform,new Vector2(.045f,.06f),new Vector2(.955f,.96f));
        alerts.color = Ink; alerts.fontSharedMaterial = alerts.font.material; alerts.fontSize = 24; alerts.enableAutoSizing = false;
        alerts.alignment = TextAlignmentOptions.MidlineLeft; alerts.textWrappingMode = TextWrappingModes.Normal;
        Stretch((RectTransform)go.transform,new Vector2(.06f,.04f),new Vector2(.94f,.23f));
        var feedback = Ref<TMP_Text>("feedback"); ((RectTransform)feedback.transform.parent).sizeDelta = new Vector2(740,54);
        feedback.color = Ink; feedback.fontSize = 24;
        data.FindProperty("hintColor").colorValue = Ink; data.FindProperty("successColor").colorValue = Ink; data.FindProperty("errorColor").colorValue = Ink;
        data.FindProperty("prepStepFormat").stringValue = "Add {0}  ·  {1}/{2}";
        data.FindProperty("stockNoticeFormat").stringValue = "{0}: {1} units";
        data.FindProperty("noticeCountFormat").stringValue = "Restock ({0})";
        data.FindProperty("pointerHint").stringValue = "";
        data.FindProperty("cookingStepText").stringValue = "Cooking — watch the progress rings";
        data.ApplyModifiedProperties();
        foreach (var root in new[]{Ref<RectTransform>("selection").gameObject, notification.gameObject, header.gameObject, go}) Reveal(root);
        foreach (var button in new[]{go,noticeLabel.transform.parent.gameObject})
            if (button.GetComponent<UISubtlePressFeedback>() == null) Undo.AddComponent<UISubtlePressFeedback>(button);
        var template = Ref<FastFoodCookingTicketView>("ticketTemplate");
        Reveal(template.gameObject);
        string path = FastFoodCookingAuthoring.TemplateFolder + "/Order Ticket.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try { Reveal(prefab); PrefabUtility.SaveAsPrefabAsset(prefab,path); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        // Normalize any circular timer sprite left from the earlier authoring pass.
        foreach (var ui in view.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            string source = AssetDatabase.GetAssetPath(ui.sprite);
            if (!source.Contains("UI Elements/PNG/") || !source.Contains("/Default/")) continue;
            var doubleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(source.Replace("/Default/","/Double/"));
            if (doubleSprite != null) { ui.sprite = doubleSprite; EditorUtility.SetDirty(ui); }
        }
        notification.gameObject.SetActive(false); staffPanel.gameObject.SetActive(false);
    }

    static void AuthorDrinkGlasses()
    {
        const string sourceFolder = "Assets/_Project/Restaurant/Assets/Level1/GameObjects/RestaurantObjects/Customers/Foods/Drinks/";
        foreach (var recipe in MenuCatalog.Default.Products.Where(r => r.category == MenuProductCategory.Drink))
        {
            string path = Batch + recipe.name + " - Glass.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourceFolder + recipe.name + ".prefab");
                if (source == null) throw new InvalidOperationException("Missing drink glass: " + recipe.name);
                var root = new GameObject(recipe.name + " - Glass"); var model = new GameObject("Model"); model.transform.SetParent(root.transform, false);
                try
                {
                    foreach (var mesh in source.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var renderer = mesh.GetComponent<MeshRenderer>(); if (renderer == null) continue;
                        var copy = new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer)); copy.transform.SetParent(model.transform,false);
                        copy.transform.SetPositionAndRotation(mesh.transform.position,mesh.transform.rotation); copy.transform.localScale = mesh.transform.lossyScale;
                        copy.GetComponent<MeshFilter>().sharedMesh = mesh.sharedMesh; var surface = copy.GetComponent<MeshRenderer>();
                        surface.sharedMaterials = renderer.sharedMaterials; surface.shadowCastingMode = ShadowCastingMode.Off; surface.receiveShadows = false;
                    }
                    var renderers = root.GetComponentsInChildren<MeshRenderer>();
                    if (renderers.Length == 0) throw new InvalidOperationException("Drink prefab has no glass mesh: " + recipe.name);
                    Bounds Bounds() { var bounds = renderers[0].bounds; foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds); return bounds; }
                    var bounds = Bounds(); model.transform.localScale *= .47f / Mathf.Max(.01f,bounds.size.y);
                    bounds = Bounds(); model.transform.localPosition -= new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                    prefab = PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            Undo.RecordObject(recipe,"Use existing glass drink visuals"); recipe.kitchenServingPrefab = recipe.kitchenPreviewPrefab = prefab; EditorUtility.SetDirty(recipe);
        }
    }

    static AudioClip FeedbackClip(string name, float duration, int kind)
    {
        string path = FeedbackFolder + "/" + name + ".wav";
        var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path); if (existing != null) return existing;
        // A short, saved sound asset, not a runtime sound generator. Replace it freely in the Inspector.
        const int rate = 22050; int samples = Mathf.RoundToInt(rate * duration); var random = new System.Random(214);
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
            float previousNoise = 0;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / rate; float edge = Mathf.Min(1, Mathf.Min(t, duration-t) / .015f);
                float noise = (float)random.NextDouble() * 2 - 1; float value;
                if (kind == 3) { value = (noise - previousNoise) * .13f * (.8f + .2f * Mathf.Sin(t * 31)); previousNoise = noise; }
                else if (kind == 0) value = .45f * Mathf.Sin(2 * Mathf.PI * (240 * t - 320 * t * t)) * Mathf.Exp(-t * 34) + noise * .08f * Mathf.Exp(-t * 65);
                else value = (.24f * Mathf.Sin(2*Mathf.PI*(kind==1?660:784)*t) + .12f * Mathf.Sin(2*Mathf.PI*(kind==1?880:1046)*t)) * Mathf.Exp(-t*8);
                writer.Write((short)(Mathf.Clamp(value * edge,-1,1) * short.MaxValue));
            }
        }
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    static AudioSource SavedAudio(Transform parent, string name, AudioMixerGroup mixer)
    {
        var child = parent.Find(name);
        if (child == null) { var go = new GameObject(name,typeof(AudioSource)); Undo.RegisterCreatedObjectUndo(go,"Author kitchen sound"); child=go.transform;child.SetParent(parent,false); }
        var source = child.GetComponent<AudioSource>(); source.playOnAwake=false;source.spatialBlend=0;source.outputAudioMixerGroup=mixer;return source;
    }
    static void AuthorGameplayAudio(FastFoodCookingController controller, FastFoodCookingView view)
    {
        if (!AssetDatabase.IsValidFolder(FeedbackFolder)) AssetDatabase.CreateFolder("Assets/_Project/Restaurant/CookingAssets","Feedback");
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/_Project/Audio/AudioMixer.mixer").FindMatchingGroups("SFX").First();
        var feedback = SavedAudio(controller.transform,"Kitchen Feedback Audio",mixer);
        var data = new SerializedObject(view); data.FindProperty("feedbackAudio").objectReferenceValue=feedback;
        data.FindProperty("placementSound").objectReferenceValue=FeedbackClip("Soft Placement",.14f,0);
        data.FindProperty("readySound").objectReferenceValue=FeedbackClip("Gentle Ready",.35f,1);
        data.FindProperty("finishedSound").objectReferenceValue=FeedbackClip("Dish Complete",.4f,2);data.ApplyModifiedProperties();
        var sizzle=FeedbackClip("Quiet Sizzle",2,3);
        foreach(var rig in controller.GetComponentsInChildren<FastFoodCookingStation>(true).Where(r=>r.mode!=FastFoodStationMode.Assembler))
        {
            var source=SavedAudio(rig.transform,"Cooking Sizzle",mixer);source.clip=sizzle;source.loop=true;source.volume=rig.cookingVolume;source.pitch=rig.mode==FastFoodStationMode.Fry?.9f:1;
            rig.cookingAudio=source;EditorUtility.SetDirty(rig);
        }
    }
}
