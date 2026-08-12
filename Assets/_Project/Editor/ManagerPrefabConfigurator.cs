#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// One-click, repeatable setup for the temporary Manager prefab.
/// It deliberately does not add AI staff components or role identity components.
/// </summary>
public static class ManagerPrefabConfigurator
{
    private const string PrefabPath = "Assets/_Project/Player/Manager.prefab";
    private const string HostPrefabPath = "Assets/_Project/Lobby/Assets/Host/Host.prefab";
    private const string WaiterPrefabPath = "Assets/_Project/Lobby/Assets/Waiter/Waiter.prefab";
    private const string BusserPrefabPath = "Assets/_Project/Lobby/Assets/Busser/Busser.prefab";
    private const string ManagerModelPath = "Assets/_Project/MainMenu/NewDesign/Characters/Chef/Chef.fbx";
    private const string PlayerControllerPath = "Assets/Resources/Player.controller";

    [MenuItem("Dine In/Configure Manager Player Prefab")]
    public static void Configure()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError($"[ManagerPrefabConfigurator] Could not load {PrefabPath}.");
            return;
        }

        try
        {
            root.name = "Manager";

            GameObject hostPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HostPrefabPath);
            NavMeshAgent hostAgent = hostPrefab != null ? hostPrefab.GetComponent<NavMeshAgent>() : null;
            PlayerMovement hostMovement = hostPrefab != null ? hostPrefab.GetComponent<PlayerMovement>() : null;
            RoleBasedAssignController hostInput = hostPrefab != null
                ? hostPrefab.GetComponent<RoleBasedAssignController>()
                : null;

            NavMeshAgent agent = GetOrAdd<NavMeshAgent>(root);
            if (hostAgent != null)
            {
                EditorUtility.CopySerialized(hostAgent, agent);
                // Native NavMeshAgent fields are not consistently copied across
                // imported-model prefab roots, so mirror the blueprint explicitly.
                agent.agentTypeID = hostAgent.agentTypeID;
                agent.radius = hostAgent.radius;
                agent.height = hostAgent.height;
                agent.baseOffset = hostAgent.baseOffset;
                agent.stoppingDistance = hostAgent.stoppingDistance;
                agent.obstacleAvoidanceType = hostAgent.obstacleAvoidanceType;
                agent.avoidancePriority = hostAgent.avoidancePriority;
                agent.areaMask = hostAgent.areaMask;
            }
            agent.speed = 8f;
            agent.acceleration = 99999f;
            agent.angularSpeed = 0f;
            agent.autoRepath = true;
            agent.updateRotation = false;

            PlayerMovement movement = GetOrAdd<PlayerMovement>(root);
            if (hostMovement != null)
            {
                EditorUtility.CopySerialized(hostMovement, movement);
                SerializedObject movementObject = new SerializedObject(movement);
                movementObject.FindProperty("homePoint").objectReferenceValue = null;
                movementObject.FindProperty("animator").objectReferenceValue = null;
                movementObject.ApplyModifiedPropertiesWithoutUndo();
            }

            GetOrAdd<ManagerPlayer>(root);
            RoleBasedAssignController managerInput = GetOrAdd<RoleBasedAssignController>(root);
            if (hostInput != null)
            {
                EditorUtility.CopySerialized(hostInput, managerInput);
                managerInput.agent = agent;
            }

            ConfigureAnimator(root);
            ConfigureMovementParticles(root, hostPrefab);
            ConfigureHands(root);
            Transform speechAnchor = EnsureAnchor(
                root.transform,
                "HostSpeechBubbleAnchor",
                new Vector3(0f, 1.9f, 0f),
                Quaternion.identity);
            GetOrAdd<HostSpeechBubbleAnchor>(speechAnchor.gameObject);

            PlayerAnimationController duplicateAnimationDriver =
                root.GetComponent<PlayerAnimationController>();
            if (duplicateAnimationDriver != null)
                Object.DestroyImmediate(duplicateAnimationDriver);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[ManagerPrefabConfigurator] Manager now mirrors the original player movement, animator controller, movement VFX, interaction masks, and carry anchors.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static T GetOrAdd<T>(GameObject root) where T : Component
    {
        T component = root.GetComponent<T>();
        return component != null ? component : root.AddComponent<T>();
    }

    private static void ConfigureAnimator(GameObject root)
    {
        ModelImporter importer = AssetImporter.GetAtPath(ManagerModelPath) as ModelImporter;
        if (importer != null)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.SaveAndReimport();
        }

        Avatar avatar = null;
        Object[] modelAssets = AssetDatabase.LoadAllAssetsAtPath(ManagerModelPath);
        for (int i = 0; i < modelAssets.Length; i++)
        {
            if (modelAssets[i] is Avatar candidate)
            {
                avatar = candidate;
                if (candidate.isValid && candidate.isHuman)
                    break;
            }
        }

        Animator animator = root.GetComponent<Animator>();
        if (animator == null)
            animator = root.AddComponent<Animator>();

        animator.runtimeAnimatorController =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerControllerPath);
        animator.applyRootMotion = false;
        animator.avatar = avatar;

        if (avatar == null || !avatar.isValid || !avatar.isHuman)
            Debug.LogError("[ManagerPrefabConfigurator] Chef.fbx does not currently produce a valid Humanoid Avatar. Open its Rig import settings, choose Humanoid, fix the bone mapping, Apply, then run this command again.");
    }

    private static void ConfigureMovementParticles(GameObject root, GameObject hostPrefab)
    {
        Transform particle = FindChild(root.transform, "Particle");
        if (particle == null && hostPrefab != null)
        {
            Transform source = FindChild(hostPrefab.transform, "Particle");
            if (source != null)
            {
                GameObject copy = Object.Instantiate(source.gameObject, root.transform);
                copy.name = "Particle";
                copy.transform.localPosition = source.localPosition;
                copy.transform.localRotation = source.localRotation;
                copy.transform.localScale = source.localScale;
            }
        }

        GetOrAdd<PlayerMovementParticles>(root);
    }

    private static void ConfigureHands(GameObject root)
    {
        // These are the exact proven local transforms from the production
        // Waiter/Busser prefabs, normalized against the Manager FBX import
        // scale so their world-space offsets remain identical.
        Vector3 rootScale = root.transform.localScale;
        Transform tray = EnsureAnchor(
            root.transform,
            "TrayHolder",
            DivideComponents(new Vector3(1.18f, 0.52f, 2.9f), rootScale),
            Quaternion.Euler(-90f, 0f, 0f));
        Transform bill = EnsureAnchor(
            root.transform,
            "BillHolder",
            DivideComponents(new Vector3(1.54f, 0.89f, 0.08f), rootScale),
            Quaternion.identity);

        WaiterHands waiterHands = GetOrAdd<WaiterHands>(root);
        GameObject waiterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WaiterPrefabPath);
        WaiterHands sourceWaiterHands = waiterPrefab != null
            ? waiterPrefab.GetComponent<WaiterHands>()
            : null;
        if (sourceWaiterHands != null)
            EditorUtility.CopySerialized(sourceWaiterHands, waiterHands);

        SerializedObject waiterObject = new SerializedObject(waiterHands);
        waiterObject.FindProperty("holdingTicketFor").objectReferenceValue = null;
        waiterObject.FindProperty("holdingBillFor").objectReferenceValue = null;
        waiterObject.FindProperty("holdingTray").objectReferenceValue = null;
        waiterObject.FindProperty("holdingMoneyFor").objectReferenceValue = null;
        waiterObject.FindProperty("holdingMoneyAmount").intValue = 0;
        waiterObject.FindProperty("trayHoldPoint").objectReferenceValue = tray;
        waiterObject.FindProperty("billHoldPoint").objectReferenceValue = bill;
        // The production waiter uses BillHolder for both bills and money.
        waiterObject.FindProperty("moneyHoldPoint").objectReferenceValue = bill;
        waiterObject.ApplyModifiedPropertiesWithoutUndo();

        BusserHands busserHands = GetOrAdd<BusserHands>(root);
        GameObject busserPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BusserPrefabPath);
        BusserHands sourceBusserHands = busserPrefab != null
            ? busserPrefab.GetComponent<BusserHands>()
            : null;
        if (sourceBusserHands != null)
            EditorUtility.CopySerialized(sourceBusserHands, busserHands);

        SerializedObject busserObject = new SerializedObject(busserHands);
        busserObject.FindProperty("holdingTray").objectReferenceValue = null;
        busserObject.FindProperty("trayHoldPoint").objectReferenceValue = tray;
        busserObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform EnsureAnchor(
        Transform root,
        string childName,
        Vector3 localPosition,
        Quaternion localRotation)
    {
        Transform child = FindChild(root, childName);
        if (child == null)
        {
            GameObject childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(root, false);
        }

        child.localPosition = localPosition;
        child.localRotation = localRotation;
        child.localScale = Vector3.one;
        return child;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
                return children[i];
        }

        return null;
    }

    private static Vector3 DivideComponents(Vector3 value, Vector3 divisor)
    {
        return new Vector3(
            Mathf.Abs(divisor.x) > 0.0001f ? value.x / divisor.x : value.x,
            Mathf.Abs(divisor.y) > 0.0001f ? value.y / divisor.y : value.y,
            Mathf.Abs(divisor.z) > 0.0001f ? value.z / divisor.z : value.z);
    }
}
#endif
