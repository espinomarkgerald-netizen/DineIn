#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit runner only: no import hooks, save changes, or automatic Play Mode.
public static class StaffServicePolishRegressionCases
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("[Staff service] " + message); }

    public static string Run()
    {
        Check(!Application.isPlaying, "Run asset checks in Edit Mode.");
        HygieneRegressionCases.Run();
        Check(HygieneDialogue.GreetingFor("female") == "Ma'am" && HygieneDialogue.GreetingFor("male") == "Sir"
            && HygieneDialogue.GreetingFor(null) == "Hello", "Player greeting mapping.");
        CheckTrolley("WaiterTrolley"); CheckTrolley("BusserTrolley");
        CheckCashierRoutes();
        int covered = CheckSceneCoverage();
        return $"PASS: hygiene model/UI, both trolley prefabs, upright non-overlapping trays, grounded cart, cashier routes, {covered} service scenes.";
    }

    private static void CheckTrolley(string name)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject cart = null, actor = null;
        try
        {
            cart = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/Upgrades/" + name + ".prefab"));
            SceneManager.MoveGameObjectToScene(cart, scene);
            var carrier = cart.GetComponent<BotTrolleyCarrier>();
            string[] trayPaths = { "Assets/_Project/Restaurant/Assets/Fine Dining Service/Fine Dining Tray.prefab",
                "Assets/_Project/Restaurant/Assets/Level1/GameObjects/RestaurantObjects/Customers/Food Tray.prefab" };
            var bounds = new Bounds[4];
            for (int i = 0; i < 4; i++)
            {
                var tray = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(trayPaths[i % 2]));
                SceneManager.MoveGameObjectToScene(tray, scene);
                Check(carrier.TryAttach(tray.GetComponent<FoodTray>()), name + " could not attach slot " + i);
                var pose = tray.GetComponent<FoodTrayCarryPose>();
                Check(Vector3.Dot(pose.CarryOrigin.up, Vector3.up) > .999f, name + " has a tilted tray.");
                var renderers = tray.GetComponentsInChildren<Renderer>();
                bounds[i] = renderers[0].bounds;
                foreach (var r in renderers) bounds[i].Encapsulate(r.bounds);
                Check(Mathf.Abs(bounds[i].min.y - carrier.TraySlots[i].position.y) < .01f, name + " tray floats above or below the shelf marker.");
                for (int j = 0; j < i; j++) Check(!bounds[i].Intersects(bounds[j]), name + " tray slots overlap.");
            }
            actor = new GameObject("Trolley operator", typeof(NavMeshAgent), typeof(AutonomousStaffBot), typeof(WaiterHands));
            SceneManager.MoveGameObjectToScene(actor, scene);
            actor.transform.SetPositionAndRotation(new Vector3(1000, 3, 1000), Quaternion.Euler(0, 42, 0));
            var grip = new GameObject("Animated spine socket").transform;
            grip.SetParent(actor.transform, false); grip.localPosition = new Vector3(0, 8, -3); grip.localRotation = Quaternion.Euler(20, 180, 15);
            var hands = new SerializedObject(actor.GetComponent<WaiterHands>());
            hands.FindProperty("trolleyGripPoint").objectReferenceValue = grip; hands.ApplyModifiedPropertiesWithoutUndo();
            carrier.ConfigureRuntime(carrier.Effect, 4, actor.transform);
            Check(carrier.BeginUse(actor.GetComponent<AutonomousStaffBot>()), name + " cannot begin use.");
            Check(Mathf.Abs(cart.transform.position.y - actor.transform.position.y) < .01f, "Cart lifts off the floor with its animated socket.");
            Check(Vector3.Dot(cart.transform.up, Vector3.up) > .999f, "Cart inherits spine pitch/roll.");
            Check(Vector3.Dot(cart.transform.position - actor.transform.position, actor.transform.forward) > .5f, "Cart follows behind the operator.");
            carrier.EndUse(false);
        }
        finally
        {
            if (cart != null) Object.DestroyImmediate(cart);
            if (actor != null) Object.DestroyImmediate(actor);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static int CheckSceneCoverage()
    {
        int count = 0;
        foreach (var path in EditorBuildSettings.scenes.Where(entry => entry.enabled).Select(entry => entry.path))
        {
            if (path.Contains("Restock") || path.Contains("Office")) continue;
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var roots = scene.GetRootGameObjects();
                var kitchens = roots.SelectMany(r => r.GetComponentsInChildren<KitchenManager>(true)).ToArray();
                if (kitchens.Length == 0) continue;
                Check(kitchens.Length == 1, path + " has duplicate hygiene owners.");
                Check(roots.Any(r => r.GetComponentInChildren<GameDayManager>(true) != null), path + " has no service clock.");
                Check(roots.Any(r => r.GetComponentInChildren<HygieneSurface>(true) != null), path + " has no hygiene surfaces.");
                Check(roots.SelectMany(r => r.GetComponentsInChildren<StaffRole>(true))
                    .Any(actor => actor.role != StaffRole.Role.Cashier && actor.GetComponentInChildren<Animator>(true) != null),
                    path + " has no actual lobby worker for the portrait.");
                Check(roots.SelectMany(r => r.GetComponentsInChildren<KitchenWorkerBot>(true))
                    .Any(actor => actor.EmployeeRole != EmployeeRole.Pianist && actor.GetComponentInChildren<Animator>(true) != null),
                    path + " has no actual kitchen worker for the portrait.");
                foreach (var actor in roots.SelectMany(r => r.GetComponentsInChildren<BusserHands>(true)))
                    Check(actor.GetComponentInChildren<Animator>(true) != null, path + " has no busser animation rig.");
                count++;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        Check(count >= 7, "Not all built restaurant/tutorial/multiplayer scenes were checked.");
        return count;
    }

    // Pixel changes alone can pass when only a hat moves. Check the face skin itself.
    public static string CheckPortraitFaceAnimation(GameObject source, AnimationClip idle)
    {
        Check(source != null && idle != null, "A character source and idle clip are required.");
        var root = new GameObject("Portrait skin regression", typeof(Canvas));
        var sourceRoot = new GameObject("Customized portrait source");
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var sample = new Mesh();
        AlmanacPreviewStage stage = null;
        try
        {
            var customized = AlmanacPreviewStage.CreateVisualCopy(source, sourceRoot.transform);
            customized.AddComponent<DineIn.Appearance.CharacterAppearance>();
            DineIn.Appearance.EmployeeAppearance.Bind(customized,
                new EmployeeData("Maria", 3, EmployeeRole.Host), EmployeeRole.Host, false);
            customized.GetComponentInChildren<Animator>(true).enabled = false;
            customized.SetActive(false);
            stage = root.AddComponent<AlmanacPreviewStage>();
            int checkedMeshes = 0;
            foreach (string body in new[] { "male", "female" })
                foreach (EmployeeRole role in new[] { EmployeeRole.Host, EmployeeRole.Chef })
                {
                    var employee = new EmployeeData(body == "male" ? "Kyle" : "Maria", 3, role);
                    employee.appearance = DineIn.Appearance.AppearanceCatalog.Load().GenerateEmployee(employee.EmployeeID, body);
                    Check(stage.ShowStaffPortrait(customized, idle, employee), "Portrait failed to build.");
                    var visual = (GameObject)typeof(AlmanacPreviewStage).GetField("modelOffset", flags).GetValue(stage);
                    var animator = visual.GetComponentsInChildren<Animator>().First(a => a.enabled && a.isHuman);
                    var head = animator.GetBoneTransform(HumanBodyBones.Head);
                    var evaluate = typeof(AlmanacPreviewStage).GetMethod("EvaluateAnimation", flags);
                    var skins = visual.GetComponentsInChildren<SkinnedMeshRenderer>()
                        .Where(r => r.enabled && (r.name == "BodyHead" || r.name == "Face" || r.name == "Ears")).ToArray();
                    Check(skins.Length == 3, "Portrait must contain one head, face and ears.");
                    foreach (var skin in skins)
                    {
                        Check(skin.bones.Contains(head), skin.name + " uses a different skeleton from the animated head.");
                        skin.BakeMesh(sample);
                        var before = sample.vertices;
                        evaluate.Invoke(stage, new object[] { .8f });
                        skin.BakeMesh(sample);
                        var after = sample.vertices;
                        Check(before.Where((point, i) => Vector3.Distance(point, after[i]) > .001f).Any(),
                            body + " " + role + " " + skin.name + " remains static during idle.");
                        checkedMeshes++;
                    }
                }
            return "PASS: " + checkedMeshes + " head/face/ear meshes animate on the same skeleton (both bodies, with and without chef hats).";
        }
        finally
        {
            if (stage != null) stage.Release();
            Object.DestroyImmediate(sample);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(sourceRoot);
        }
    }

    public static void CheckCashierRoutes()
    {
        var scene = SceneManager.GetActiveScene();
        Check(scene.name == "Lobby3", "Open Lobby3 for the authored route checks.");
        var points = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        Vector3 target = points.Single(t => t.name == "Cashier Desk Approach").position;
        Check(NavMesh.SamplePosition(target, out var end, .3f, NavMesh.AllAreas), "Cashier front approach is off NavMesh.");
        var pocket = new Bounds(new Vector3(9.8f, .56f, -54.95f), new Vector3(7.5f, 10f, 2.6f));
        foreach (string name in new[] { "WaiterHomePoint", "BusserHomePoint", "Kitchen Pickup Approach", "WaiterTrolleyParkingPoint", "BusserTrolleyParkingPoint" })
        {
            Check(NavMesh.SamplePosition(points.Single(t => t.name == name).position, out var start, .5f, NavMesh.AllAreas), name + " is off NavMesh.");
            var path = new NavMeshPath();
            Check(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, name + " cannot reach cashier front.");
            for (int i = 1; i < path.corners.Length; i++)
            {
                var delta = path.corners[i] - path.corners[i - 1];
                Check(!pocket.IntersectRay(new Ray(path.corners[i - 1], delta.normalized), out float distance) || distance > delta.magnitude,
                    name + " still routes through the cashier's rear pocket.");
            }
        }
        FineDiningSceneTools.ValidateNavigation();
    }
}
#endif
