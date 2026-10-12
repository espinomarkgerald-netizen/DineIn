#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Copies the saved restaurant and binds the existing shared multiplayer components.</summary>
public static class FineDiningMultiplayerAuthoring
{
    public const string ScenePath = "Assets/_Project/Scenes/RoleBased/Lobby3 Multiplayer.unity";
    private const string SourcePath = "Assets/_Project/Scenes/RoleBased/Lobby3.unity";
    private const string SocialPath = "Assets/_Project/Resources/UI/MultiplayerSocialUI.prefab";

    [MenuItem("Dine In/Multiplayer/Create Prepared Fine Dining Scene")]
    public static void CreatePreparedScene()
    {
        RequireIdle();
        Scene previous = SceneManager.GetActiveScene();
        Scene existing = SceneManager.GetSceneByPath(ScenePath);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            if (existing.IsValid() && existing.isLoaded && existing.isDirty)
                throw new InvalidOperationException("Save Lobby3 Multiplayer before validating it.");
            bool opened = !existing.IsValid() || !existing.isLoaded;
            if (opened) existing = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try { Validate(existing); RegisterBuildScenes(); }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(existing, true);
                RestoreActive(previous);
            }
            Debug.Log("[Fine Dining Multiplayer] Existing scene validated; Inspector edits were preserved.");
            return;
        }
        Scene source = SceneManager.GetSceneByPath(SourcePath);
        if (source.IsValid() && source.isLoaded && source.isDirty)
            throw new InvalidOperationException("Save Lobby3 before copying its restaurant.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourcePath) == null)
            throw new InvalidOperationException("Saved Lobby3 scene is missing.");
        string staging = "Assets/_Project/Scenes/RoleBased/__Lobby3Multiplayer_" + Guid.NewGuid().ToString("N") + ".unity";
        Scene staged = default;
        bool copied = false, finalized = false;
        try
        {
            copied = AssetDatabase.CopyAsset(SourcePath, staging);
            if (!copied) throw new InvalidOperationException("Unity could not copy Lobby3.");
            staged = EditorSceneManager.OpenScene(staging, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(staged);
            Configure(staged);
            Validate(staged);
            EditorSceneManager.MarkSceneDirty(staged);
            if (!EditorSceneManager.SaveScene(staged)) throw new InvalidOperationException("Unity could not save the multiplayer scene.");
            EditorSceneManager.CloseScene(staged, true);
            staged = default;
            string error = AssetDatabase.MoveAsset(staging, ScenePath);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            finalized = true;
            RegisterBuildScenes();
            Debug.Log("[Fine Dining Multiplayer] Created a separate restaurant with shared session, staff, reservation and pouring bindings.");
        }
        finally
        {
            if (staged.IsValid() && staged.isLoaded) EditorSceneManager.CloseScene(staged, true);
            if (copied && !finalized) AssetDatabase.DeleteAsset(staging);
            RestoreActive(previous);
        }
    }

    private static void Configure(Scene scene)
    {
        if (FindAll<MultiplayerSessionManager>(scene).Any())
            throw new InvalidOperationException("Source Lobby3 already contains a multiplayer session; refusing to stack sessions.");
        var reservations = Require<FineDiningReservations>(scene);
        var roles = Require<RoleManager>(scene);
        var sourcePlayer = FindAll<ManagerPlayer>(scene).Single(p => p.name == "Manager");
        Vector3 origin = sourcePlayer.transform.position;
        Quaternion facing = sourcePlayer.transform.rotation;
        sourcePlayer.gameObject.SetActive(false);
        roles.gameObject.SetActive(false);
        var day = Require<GameDayManager>(scene);
        Set(day, "restaurantSceneName", FineDiningScene.Multiplayer);
        var root = new GameObject("Fine Dining Multiplayer Session");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.SetPositionAndRotation(origin, facing);
        var spawner = root.AddComponent<NetworkPlayerSpawner>();
        var session = root.AddComponent<MultiplayerSessionManager>();
        var binding = root.AddComponent<FineDiningMultiplayerSession>();
        var progression = root.AddComponent<MultiplayerProgressionContext>();
        var hud = root.AddComponent<MultiplayerHUDBridge>();
        var clock = root.AddComponent<MultiplayerDayBridge>();
        var restock = root.AddComponent<MultiplayerRestockBridge>();
        var service = root.AddComponent<MultiplayerServiceActions>();
        var objects = root.AddComponent<MultiplayerObjectBridge>();
        var claims = root.AddComponent<MultiplayerTaskClaims>();
        var roster = root.AddComponent<MultiplayerStaffRosterController>();
        var staff = root.AddComponent<MultiplayerServiceStaffBridge>();
        var interaction = root.AddComponent<MultiplayerCustomerInteractionBridge>();
        var customers = root.AddComponent<MultiplayerCustomerSpawnBridge>();
        root.AddComponent<MultiplayerSessionUI>();
        root.AddComponent<MultiplayerVoiceController>();
        var fine = root.AddComponent<MultiplayerFineDiningService>();
        var ui = Require<WinePouringUI>(scene);
        Set(fine, "reservations", reservations); Set(fine, "pourUI", ui);
        Set(session, "gameplayBinding", binding);
        string social = AssetDatabase.GUIDToAssetPath("e96f5e2d19c14bdb879ae80fb600db05");
        var socialPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(string.IsNullOrEmpty(social) ? SocialPath : social);
        if (socialPrefab == null) throw new InvalidOperationException("Existing multiplayer social/ready UI prefab is missing.");
        Set(session, "socialUIPrefab", socialPrefab);
        Set(binding, "progression", progression); Set(binding, "hudBridge", hud);
        Set(binding, "dayBridge", clock); Set(binding, "restockBridge", restock);
        Set(binding, "serviceBridge", service); Set(binding, "objectBridge", objects);
        Set(binding, "taskClaims", claims); Set(binding, "staffRoster", roster);
        Set(binding, "serviceStaffBridge", staff); Set(binding, "interactionBridge", interaction);
        Set(binding, "fineDiningService", fine); Set(binding, "reservations", reservations);
        Set(roster, "receptionist", roles.host); Set(roster, "waiter", roles.waiter);
        Set(roster, "cashier", roles.cashier); Set(roster, "busser", roles.busser);
        var second = FindAll<Transform>(scene).Single(t => t.name == "Second Waiter").gameObject;
        SetArray(roster, "additionalServiceStaff", new UnityEngine.Object[] { second });
        Set(customers, "spawner", Require<GroupSpawner>(scene)); Set(customers, "session", session);
        Set(spawner, "playerPrefabName", "ManagerMultiplayer");
        Set(spawner, "spawnPoint", root.transform);
        var spawnSO = new SerializedObject(spawner);
        spawnSO.FindProperty("usePartySlots").boolValue = true;
        spawnSO.FindProperty("spawnScatterRadius").floatValue = 0f;
        spawnSO.ApplyModifiedPropertiesWithoutUndo();
        var positions = new Vector3[] { Vector3.zero, new Vector3(2.5f, 0f, 0f), new Vector3(0f, 0f, 2.5f), new Vector3(2.5f, 0f, 2.5f) };
        var anchors = new UnityEngine.Object[4];
        for (int i = 0; i < anchors.Length; i++)
        {
            var point = new GameObject("Participant " + (i + 1) + " Spawn");
            point.transform.SetParent(root.transform, false);
            point.transform.localPosition = positions[i];
            anchors[i] = point.transform;
        }
        SetArray(spawner, "partySpawnPoints", anchors);
    }

    [MenuItem("Dine In/Multiplayer/Validate Fine Dining Scene References")]
    public static void ValidateSavedScene()
    {
        RequireIdle();
        Scene previous = SceneManager.GetActiveScene(), scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try { Validate(scene); Debug.Log("[Fine Dining Multiplayer] Scene references are complete. Runtime/device checks were not run."); }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); RestoreActive(previous); }
    }

    public static void Validate(Scene scene)
    {
        var session = Require<MultiplayerSessionManager>(scene);
        var binding = Require<FineDiningMultiplayerSession>(scene);
        if (session.gameObject != binding.gameObject || Reference(session, "gameplayBinding") != binding)
            throw new InvalidOperationException("Multiplayer session and gameplay binding do not share the authored root.");
        string[] fields = { "progression", "hudBridge", "dayBridge", "restockBridge", "serviceBridge", "objectBridge",
            "taskClaims", "interactionBridge", "staffRoster", "serviceStaffBridge", "fineDiningService", "reservations" };
        foreach (string field in fields)
        {
            var value = Reference(binding, field) as Component;
            if (value == null || value.gameObject.scene != scene)
                throw new InvalidOperationException("Missing or cross-scene session reference: " + field);
        }
        var reservations = Require<FineDiningReservations>(scene);
        var fine = Require<MultiplayerFineDiningService>(scene);
        var pour = Require<WinePouringUI>(scene);
        if (Reference(fine, "reservations") != reservations || Reference(fine, "pourUI") != pour || !pour.SupportsWaterService)
            throw new InvalidOperationException("Reservation/pouring bindings or existing water artwork are missing.");
        if (Reference(session, "socialUIPrefab") == null || !Require<KitchenManager>(scene).HasDrinkServiceOutput)
            throw new InvalidOperationException("Shared ready UI or existing bartender drink output is missing.");
        var roster = Require<MultiplayerStaffRosterController>(scene);
        var roles = Require<RoleManager>(scene);
        var expected = new[] { roles.host, roles.waiter, roles.cashier, roles.busser };
        for (int i = 0; i < expected.Length; i++)
            if (expected[i] == null || roster.GetStaffRoot(i) != expected[i])
                throw new InvalidOperationException("Missing/mismatched service staff role: " + i);
        if (roster.StaffRootCount != 5 || roster.GetStaffRoot(4)?.name != "Second Waiter")
            throw new InvalidOperationException("The authored second waiter is missing from the roster.");
        if (roles.gameObject.activeSelf || FindAll<ManagerPlayer>(scene).Any(p => p.name == "Manager" && p.gameObject.activeSelf))
            throw new InvalidOperationException("Single-player manager or role controls are still active in the multiplayer scene.");
        if (reservations.tables == null || reservations.tables.Length == 0 || reservations.tables.Any(t => t == null || t.Capacity <= 0)
            || reservations.tables.Select(t => t.tableId).Distinct().Count() != reservations.tables.Length
            || reservations.tables.Any(t => string.IsNullOrWhiteSpace(t.tableId)))
            throw new InvalidOperationException("Reservation table IDs, chair capacities or references are invalid.");
        if (reservations.receptionLine == null || reservations.dayManager == null || reservations.waitingArea == null)
            throw new InvalidOperationException("Existing reception/waiting/clock references are missing.");
        var stations = FindAll<ManagementComputerStation>(scene).ToArray();
        if (stations.Count(s => s.Purpose == RestaurantComputerPurpose.Reservations) != 1
            || stations.Count(s => s.Purpose == RestaurantComputerPurpose.Management) != 1)
            throw new InvalidOperationException("Expected separate management and reservation stations.");
        if (!FindAll<NavMeshSurface>(scene).Any(s => s.navMeshData != null && s.agentTypeID == 0))
            throw new InvalidOperationException("Existing baked navigation is missing.");
        var spawner = Require<NetworkPlayerSpawner>(scene);
        var points = new SerializedObject(spawner).FindProperty("partySpawnPoints");
        if (points == null || points.arraySize != 4) throw new InvalidOperationException("Four participant spawn anchors are required.");
        var unique = new HashSet<Transform>();
        for (int i = 0; i < points.arraySize; i++)
        {
            var point = points.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            if (point == null || point.gameObject.scene != scene || !unique.Add(point))
                throw new InvalidOperationException("Missing, duplicate or cross-scene participant spawn anchor.");
        }
        if (FindAll<FastFoodMultiplayerSession>(scene).Any() || FindAll<MultiplayerFastFoodCooking>(scene).Any()
            || FindAll<TutorialSystem>(scene).Any())
            throw new InvalidOperationException("Fine Dining must not contain Fast Food gameplay or tutorial bindings.");
    }

    private static void RegisterBuildScenes()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        int index = scenes.FindIndex(s => s.path == ScenePath);
        if (index < 0) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        else scenes[index] = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettings.scenes = scenes.ToArray();
        foreach (string guid in AssetDatabase.FindAssets("t:BuildProfile", new[] { "Assets/Settings/Build Profiles" }))
        {
            var profile = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            var data = new SerializedObject(profile);
            if (data.FindProperty("m_OverrideGlobalSceneList")?.boolValue != true) continue;
            var list = data.FindProperty("m_Scenes");
            if (list == null || !list.isArray) continue;
            int row = -1;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("m_path").stringValue == ScenePath) row = i;
            if (row < 0) { row = list.arraySize; list.InsertArrayElementAtIndex(row); }
            var entry = list.GetArrayElementAtIndex(row);
            entry.FindPropertyRelative("m_path").stringValue = ScenePath;
            entry.FindPropertyRelative("m_enabled").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }
        AssetDatabase.SaveAssets();
    }
    private static IEnumerable<T> FindAll<T>(Scene scene) where T : Component
        => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
    private static T Require<T>(Scene scene) where T : Component
        => FindAll<T>(scene).SingleOrDefault() ?? throw new InvalidOperationException("Expected one scene " + typeof(T).Name + ".");
    private static SerializedProperty Property(SerializedObject data, string field)
        => data.FindProperty(field) ?? throw new InvalidOperationException(data.targetObject.name + " is missing serialized field " + field);
    private static UnityEngine.Object Reference(UnityEngine.Object target, string field)
        => Property(new SerializedObject(target), field).objectReferenceValue;
    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    { var data = new SerializedObject(target); Property(data, field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(UnityEngine.Object target, string field, string value)
    { var data = new SerializedObject(target); Property(data, field).stringValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetArray(UnityEngine.Object target, string field, UnityEngine.Object[] values)
    {
        var data = new SerializedObject(target); var list = Property(data, field); list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void RequireIdle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Scene authoring requires the idle Editor outside Play Mode.");
    }
    private static void RestoreActive(Scene scene)
    { if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene); }
}
#endif
