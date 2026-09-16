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
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
#endif
