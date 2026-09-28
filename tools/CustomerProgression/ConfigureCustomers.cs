using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Explicit, one-time Edit Mode authoring. Not a runtime bootstrap or a play/build hook.
public static class ConfigureCustomers
{
    const string Profiles = "Assets/_Project/Art/Models/Customer/New Alien Models/PlayerType/";
    const string Portraits = "Assets/_Project/Art/Models/Customer/New Alien Models/2dCloseupImages/";
    const string Models = "Assets/_Project/Art/Models/Customer/AdditionalAliens/";
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed)
            throw new InvalidOperationException("Author only in compiled Edit Mode.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if ((scene.name == "Lobby1" || scene.name == "Lobby2") && scene.isDirty)
                throw new InvalidOperationException("Save/review the dirty normal restaurant scene first: " + scene.name);
        }
        string[] names = { "Green", "Pink", "Blue", "Purple", "Orange", "Elderly", "Yellow" };
        int[] portraits = { 1, 3, 2, 6, 4, 7, 5 };
        int[] days = { 1, 5, 10, 2, 5, 10, 15 }; // Later Fast Food defaults are provisional and editable.
        string[] models = { null, null, null, "PurpleAlien", "OrangeAlien", "OldAlien", "YellowAlien" };
        string[] lines = {
            "Our first guests are arriving! Green customers give us a little time, so keep their orders moving and make them feel welcome.",
            "Pink guests will be joining us today. They expect prompt service, so give their requests attention and avoid keeping them waiting.",
            "Blue guests enjoy taking their time over a meal. Keep an eye on their tables: they tend to leave a mess.",
            "Word is spreading about our restaurant! Purple guests are starting to stop by. Keep their orders moving and the restaurant welcoming.",
            "Orange guests have busy schedules. Most take their meals to go, and those who stay eat quickly. Keep their orders moving so they can be on their way.",
            "We'll be welcoming some older guests today. They need more time to walk and choose their meals, but don't like being kept waiting. Give them priority at the counter, and bring dine-in meals to their table.",
            "Families are starting to visit! They come in groups of three or four, and the little ones like to play. Give them room to enjoy their meals, and watch for footprints on the floor."
        };
        var profiles = new CustomerTypeProfile[7];
        for (int i = 0; i < names.Length; i++)
        {
            string path = Profiles + names[i] + "Customer.asset";
            var profile = AssetDatabase.LoadAssetAtPath<CustomerTypeProfile>(path);
            bool created = profile == null;
            if (created)
            {
                if (i < 3) throw new InvalidOperationException("Missing legacy profile: " + path);
                profile = ScriptableObject.CreateInstance<CustomerTypeProfile>();
                profile.displayName = names[i];
                profile.unlockDay = days[i];
                profile.spawnWeight = i == 3 ? .7f : i == 4 ? .2f : .15f;
                if (i == 4) { profile.orderingDurationMultiplier = .75f; profile.eatDurationMultiplier = .7f; profile.overrideTakeawayChance = true; profile.takeawayChance = .7f; }
                if (i == 5) { profile.walkingSpeedMultiplier = .7f; profile.orderingDurationMultiplier = 1.4f; profile.orderPatienceMultiplier = profiles[1].orderPatienceMultiplier; profile.linePatienceMultiplier = profiles[1].linePatienceMultiplier; profile.assistedPriorityService = true; }
                if (i == 6) { profile.family = true; profile.eatDurationMultiplier = 1.5f; }
                profile.openingMessages = new[] { "Hello! We're looking forward to our meal." };
                AssetDatabase.CreateAsset(profile, path);
            }
            profile.customerType = (CustomerGroup.CustomerType)i;
            profile.customerImage = AssetDatabase.LoadAssetAtPath<Sprite>(Portraits + portraits[i] + ".png");
            if (profile.customerImage == null) throw new InvalidOperationException("Portrait is not imported as a Sprite: " + portraits[i]);
            if (string.IsNullOrWhiteSpace(profile.introduction)) profile.introduction = lines[i];
            if (models[i] != null)
            {
                profile.customerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Models + models[i] + "/" + models[i] + "Customer.prefab")?.GetComponent<CustomerAgent>();
                if (profile.customerPrefab == null) throw new InvalidOperationException("Missing rigged customer: " + models[i]);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            profiles[i] = profile;
        }
        ConfigureScene("Lobby1", profiles.Take(3).ToArray());
        ConfigureScene("Lobby2", profiles);
        ConfigurePresentation();
        return "Assigned 7 portraits/profiles, normal Lobby1/Lobby2 spawners, dining play zones, and campaign briefing showcase. No tutorials modified.";
    }

    static void ConfigureScene(string name, CustomerTypeProfile[] profiles)
    {
        var scene = SceneManager.GetSceneByName(name);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/RoleBased/" + name + ".unity", OpenSceneMode.Additive);
        try
        {
            var spawner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GroupSpawner>(true)).Single();
            var data = new SerializedObject(spawner);
            var list = data.FindProperty("campaignCustomers");
            list.arraySize = profiles.Length;
            for (int i = 0; i < profiles.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            // These four former Green visual variants now have their own identity, eligibility and behavior.
            data.FindProperty("regularCustomerVisualVariants").arraySize = 0;
            if (name == "Lobby2")
            {
                data.FindProperty("greenEnabled").boolValue = true;
                data.FindProperty("pinkEnabled").boolValue = true;
                data.FindProperty("blueEnabled").boolValue = true;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            if (name == "Lobby2")
                foreach (var table in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FastFoodTable>(true)))
                {
                    if (table.StandPoint == null) continue;
                    // Inspected Lobby2 dining approaches end at z=35.78; kitchen/storage start beyond z=40.
                    // A small local box around each dining approach cannot route into that service region.
                    Vector3 center = table.StandPoint.position;
                    if (center.z > 38f) throw new InvalidOperationException("Review dining zone before authoring: " + table.name);
                    var local = new Bounds(table.transform.InverseTransformPoint(center), Vector3.zero);
                    for (int x = -1; x <= 1; x += 2)
                        for (int y = -1; y <= 1; y += 2)
                            for (int z = -1; z <= 1; z += 2)
                                local.Encapsulate(table.transform.InverseTransformPoint(center + new Vector3(x * 2.4f, y, z * 2.4f)));
                    var tableData = new SerializedObject(table);
                    tableData.FindProperty("familyPlayBounds").boundsValue = local;
                    tableData.FindProperty("familyPlayEnabled").boolValue = true;
                    tableData.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(table);
                }
            PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    static void ConfigurePresentation()
    {
        const string path = "Assets/_Project/Resources/UI/RestaurantUnlockGuide.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var view = root.GetComponent<RestaurantUnlockCoachUI>();
            var data = new SerializedObject(view);
            var safe = (RectTransform)data.FindProperty("safeArea").objectReferenceValue;
            var showcase = data.FindProperty("customerShowcase").objectReferenceValue as RectTransform;
            if (showcase == null)
            {
                showcase = new GameObject("Customer Portrait Showcase", typeof(RectTransform)).GetComponent<RectTransform>();
                showcase.SetParent(safe, false);
                showcase.anchorMin = showcase.anchorMax = new Vector2(.5f, 0f);
                showcase.sizeDelta = new Vector2(440f, 270f);
                showcase.anchoredPosition = new Vector2(0f, 560f);
                var images = data.FindProperty("customerPortraits");
                images.arraySize = 4;
                for (int i = 0; i < 4; i++)
                {
                    var image = new GameObject("Guest Portrait " + (i + 1), typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    image.transform.SetParent(showcase, false);
                    image.rectTransform.sizeDelta = new Vector2(200f, 200f);
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    images.GetArrayElementAtIndex(i).objectReferenceValue = image;
                }
                data.FindProperty("customerShowcase").objectReferenceValue = showcase;
            }
            showcase.gameObject.SetActive(false);
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
