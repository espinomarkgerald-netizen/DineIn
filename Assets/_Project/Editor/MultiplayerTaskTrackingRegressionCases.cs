#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Explicit external runner only. No import-time execution or gameplay bootstrap.
public static class MultiplayerTaskTrackingRegressionCases
{
    private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    public static void Run()
    {
        var roots = new List<GameObject>();
        GameObject Root(string name) { var root = new GameObject(name); root.SetActive(false); roots.Add(root); return root; }
        var kitchenField = typeof(MultiplayerWorldRegistry).GetField("kitchen", Fields);
        var handsField = typeof(WaiterHands).GetField("<Instance>k__BackingField", Fields);
        object previousKitchen = kitchenField.GetValue(null), previousHands = handsField.GetValue(null);
        try
        {
            var human = Root("human").AddComponent<WaiterHands>();
            var staff = Root("staff").AddComponent<WaiterHands>();
            handsField.SetValue(null, human);
            var group = Root("order").AddComponent<CustomerGroup>();
            group.currentOrderNumber = 41;
            group.state = CustomerGroup.GroupState.OrderTaken;
            var flow = Root("ticket flow").AddComponent<OrderFlowManager>();
            flow.SpawnTicket(group, staff);
            Check(staff.holdingTicketFor == group && human.holdingTicketFor == null, "Staff ticket contaminated human hands.");
            var kitchen = Root("kitchen").AddComponent<KitchenManager>();
            kitchenField.SetValue(null, kitchen);
            human.holdingTicketFor = group;
            human.ReconcileHeldItemReferences();
            Check(human.HasTicket, "An unsubmitted OrderTaken ticket was discarded.");
            var cooking = (HashSet<int>)typeof(KitchenManager).GetField("cookingOrders", Fields).GetValue(kitchen);
            cooking.Add(41);
            human.ReconcileHeldItemReferences();
            Check(!human.HasTicket && group.state == CustomerGroup.GroupState.OrderTaken,
                "Kitchen-accepted ticket still blocked an otherwise idle human.");
            var activity = MultiplayerActorActivity.Read(human.gameObject);
            Check(activity.phase == MultiplayerActorActivity.Phase.Idle && activity.blocker == null,
                "Task guidance and eligibility disagree after accepted order cleanup.");
            Check(staff.holdingTicketFor == group, "Reconciling a human changed another actor's hands.");

            var snapshotType = typeof(MultiplayerDayBridge).GetNestedType("Snapshot", BindingFlags.NonPublic);
            var normalize = typeof(MultiplayerDayBridge).GetMethod("NormalizeReadiness", Fields);
            object RoundTrip(bool active, bool running, MultiplayerDayReadiness ready)
            {
                var snapshot = Activator.CreateInstance(snapshotType, true);
                snapshotType.GetField("day").SetValue(snapshot, 1);
                snapshotType.GetField("running").SetValue(snapshot, running);
                snapshotType.GetField("readinessActive").SetValue(snapshot, active);
                snapshotType.GetField("readiness").SetValue(snapshot, ready);
                return JsonUtility.FromJson(JsonUtility.ToJson(snapshot), snapshotType);
            }
            var started = RoundTrip(false, true, null);
            Check((bool)normalize.Invoke(null, new[] { started }) && snapshotType.GetField("readiness").GetValue(started) == null,
                "Serialized cleared readiness rejected a running day.");
            var malformed = RoundTrip(true, true, new MultiplayerDayReadiness());
            Check((bool)normalize.Invoke(null, new[] { malformed }), "Obsolete malformed readiness vetoed the running phase.");
            var waiting = RoundTrip(true, false, new MultiplayerDayReadiness());
            Check(!(bool)normalize.Invoke(null, new[] { waiting }), "An invalid active request was accepted.");
            var cancelled = RoundTrip(false, false, new MultiplayerDayReadiness());
            Check((bool)normalize.Invoke(null, new[] { cancelled }) && snapshotType.GetField("readiness").GetValue(cancelled) == null,
                "Cancelled readiness retained its input lock.");

            var ringRoot = new GameObject("stationary ring", typeof(RectTransform));
            ringRoot.SetActive(false); roots.Add(ringRoot);
            var graphic = ringRoot.AddComponent<MultiplayerWorkCircleGraphic>();
            graphic.rectTransform.sizeDelta = new Vector2(24, 24);
            Vector3 before = graphic.rectTransform.localPosition;
            graphic.SetAngle(90);
            using (var mesh = new VertexHelper())
            {
                typeof(MultiplayerWorkCircleGraphic).GetMethod("OnPopulateMesh", Fields).Invoke(graphic, new object[] { mesh });
                var vertex = new UIVertex(); mesh.PopulateUIVertex(ref vertex, 0);
                Check(Mathf.Approximately(((Vector2)vertex.position - graphic.rectTransform.rect.center).magnitude, 8f),
                    "24-unit ring did not use 4-unit thickness.");
            }
            Check(graphic.rectTransform.localPosition == before && graphic.rectTransform.localRotation == Quaternion.identity,
                "Spinner animation moved or rotated its UI transform.");
        }
        finally
        {
            kitchenField.SetValue(null, previousKitchen); handsField.SetValue(null, previousHands);
            foreach (var root in roots) if (root != null) Object.Destroy(root);
        }
    }
    public static void RunStabilityCases()
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        var root = new GameObject("Stability regression (temporary)");
        root.SetActive(false);
        var recipeEvent = typeof(UnlockManager).GetField("OnRecipeUnlocked", fields);
        var equipmentEvent = typeof(UnlockManager).GetField("OnEquipmentUnlocked", fields);
        var ingredientEvent = typeof(UnlockManager).GetField("OnIngredientUnlocked", fields);
        var restoredEvent = typeof(UnlockManager).GetField("OnStateRestored", fields);
        object restoredBefore = restoredEvent.GetValue(null);
        object recipesBefore = recipeEvent.GetValue(null), equipmentBefore = equipmentEvent.GetValue(null), ingredientsBefore = ingredientEvent.GetValue(null);
        try
        {
            int recipeNotices = 0, equipmentNotices = 0, restores = 0;
            restoredEvent.SetValue(null, (Action)(() => restores++));
            var unlocks = root.AddComponent<UnlockManager>();
            recipeEvent.SetValue(null, (Action<string>)(id => { Check(unlocks.IsRecipeUnlocked(id), "Recipe event preceded state installation."); recipeNotices++; }));
            equipmentEvent.SetValue(null, (Action<string>)(id => { Check(unlocks.IsEquipmentUnlocked(id), "Equipment event preceded state installation."); equipmentNotices++; }));
            ingredientEvent.SetValue(null, null);
            var baseline = new GameSaveData();
            baseline.unlockedRecipeIDs.Add("regression.recipe.1");
            baseline.unlockedEquipmentIDs.Add("regression.equipment.1");
            Check(unlocks.ApplyMultiplayerState(baseline, false), "Join baseline did not install unlocks.");
            Check(recipeNotices == 0 && equipmentNotices == 0 && restores == 1, "Join must refresh UI without replaying old unlock notices.");
            var next = new GameSaveData();
            next.unlockedRecipeIDs.AddRange(baseline.unlockedRecipeIDs); next.unlockedRecipeIDs.Add("regression.recipe.2");
            next.unlockedEquipmentIDs.AddRange(baseline.unlockedEquipmentIDs); next.unlockedEquipmentIDs.Add("regression.equipment.2");
            Check(unlocks.ApplyMultiplayerState(next, true), "New host unlocks were ignored.");
            Check(recipeNotices == 1 && equipmentNotices == 1, "New host unlocks did not notify exactly once.");
            Check(!unlocks.ApplyMultiplayerState(next, true), "Duplicate snapshot appeared changed.");
            Check(recipeNotices == 1 && equipmentNotices == 1, "Duplicate snapshot replayed notices.");

            var claims = root.AddComponent<MultiplayerTaskClaims>();
            var map = (Dictionary<string, int>)typeof(MultiplayerTaskClaims).GetField("claims", fields).GetValue(claims);
            var leases = (Dictionary<string, long>)typeof(MultiplayerTaskClaims).GetField("leases", fields).GetValue(claims);
            var reconcile = typeof(MultiplayerTaskClaims).GetMethod("Reconcile", fields);
            void Apply(long stamp, string[] keys, int[] owners, long[] tokens) => reconcile.Invoke(claims, new object[] { stamp, keys, owners, tokens });
            string key = "Customer:42:Order";
            Apply(5, new[] { key }, new[] { 2 }, new long[] { 4 });
            Check(map[key] == 2 && leases[key] == 4, "Guest snapshot lost the owner's lease.");
            Apply(4, Array.Empty<string>(), Array.Empty<int>(), Array.Empty<long>());
            Check(map.ContainsKey(key), "Older empty snapshot cleared a newer claim.");
            Apply(6, new[] { key }, Array.Empty<int>(), new long[] { 5 });
            Check(map[key] == 2 && leases[key] == 4, "Malformed snapshot damaged a valid claim.");
            Apply(7, Array.Empty<string>(), Array.Empty<int>(), Array.Empty<long>());
            Check(map.Count == 0 && leases.Count == 0, "Authoritative release retained a stale lease.");
            var action = new MultiplayerActionContext { run = "test", day = 1, actor = 2, loadEpoch = 2, operation = "food_pickup", expectedStage = "Prepared", actionId = "00000000000000000000000000000001" };
            var replay = JsonUtility.FromJson<MultiplayerActionContext>(JsonUtility.ToJson(action));
            Check(action.MatchesAction(replay), "Action epoch did not survive JSON round trip.");
            replay.loadEpoch++;
            Check(!action.MatchesAction(replay), "An old connection's reply matched a new connection's action.");
            var scope = typeof(MultiplayerTaskClaims).GetMethod("ScopeGeneration", fields);
            var matches = typeof(MultiplayerTaskClaims).GetMethod("GenerationMatchesEpoch", fields);
            long generation = (long)scope.Invoke(null, new object[] { 2, 1L });
            Check((bool)matches.Invoke(null, new object[] { generation, 2 }) && !(bool)matches.Invoke(null, new object[] { generation, 3 }),
                "A previous connection's action remained valid after rejoin.");
        }
        finally
        {
            recipeEvent.SetValue(null, recipesBefore); equipmentEvent.SetValue(null, equipmentBefore); ingredientEvent.SetValue(null, ingredientsBefore);
            restoredEvent.SetValue(null, restoredBefore);
            Object.DestroyImmediate(root);
        }
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
#endif
