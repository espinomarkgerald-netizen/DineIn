#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit external regression runner; never executes on import.</summary>
public static class FastFoodServiceRegressionCases
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Dine In/Fast Food/Run Service Regressions (Empty Play Mode Scene)")]
    public static void Run()
    {
        if (!Application.isPlaying || GameDayManager.Instance != null || TakeoutQueueManager.Instance != null ||
            SceneManager.GetSceneByName("Lobby2").IsValid())
            throw new InvalidOperationException("On the external test machine, enter Play Mode in an empty scene, without a running restaurant.");
        var scene = SceneManager.CreateScene("Lobby2");
        var fixtures = new List<GameObject>();
        GameObject Fixture(string name, bool active = true)
        {
            var root = new GameObject(name);
            root.SetActive(active);
            SceneManager.MoveGameObjectToScene(root, scene);
            fixtures.Add(root);
            return root;
        }
        CustomerGroup Customer(string name)
        {
            var group = Fixture(name).AddComponent<CustomerGroup>();
            group.SetServiceType(CustomerGroup.ServiceType.Takeout);
            group.state = CustomerGroup.GroupState.Waiting;
            return group;
        }
        try
        {
            var orderPoint = Fixture("order").transform;
            var firstSlot = Fixture("queue 1").transform;
            var secondSlot = Fixture("queue 2").transform;
            var overflow = Fixture("overflow").transform;
            overflow.position = new Vector3(0, 0, -10);
            var queueRoot = Fixture("queue", false);
            var queue = queueRoot.AddComponent<TakeoutQueueManager>();
            Set(queue, "orderPoint", orderPoint);
            Set(queue, "exitPoint", Fixture("exit").transform);
            Set(queue, "queuePoints", new[] { firstSlot, secondSlot });
            Set(queue, "overflowRoot", overflow);
            queueRoot.SetActive(true);
            var a = Customer("A"); var b = Customer("B"); var c = Customer("C");
            queue.Enqueue(a); queue.Enqueue(b); queue.Enqueue(c); queue.Enqueue(b);
            Assert(queue.Count == 3 && queue.CurrentFront == a, "FIFO/duplicate enqueue failed.");
            b.SetTakeoutQueueState(CustomerGroup.TakeoutQueueState.WaitingInQueue);
            var d = Customer("D"); queue.Enqueue(d);
            Assert(b.CurrentTakeoutQueueState == CustomerGroup.TakeoutQueueState.WaitingInQueue,
                "An arriving customer restarted an unchanged queue slot.");
            Object.DestroyImmediate(a.gameObject);
            Call(queue, "Update");
            Assert(queue.CurrentFront == b && queue.Count == 3, "Destroyed front did not promote the next group.");
            queue.Remove(b); queue.Remove(b);
            Assert(queue.CurrentFront == c && queue.Count == 2, "Repeated removal skipped a customer.");
            queue.ReleaseGroup(c); queue.ReleaseGroup(c);
            Assert(queue.CurrentFront == d && queue.Count == 1, "Repeated departure did not preserve the next customer.");
            var args = new object[] { 2, Vector3.zero, Vector3.zero };
            Assert((bool)Call(queue, "TryGetQueueSlotPose", args) && (Vector3)args[1] == overflow.position,
                "First overflow slot ignored the authored anchor.");

            var flow = Fixture("flow", false).AddComponent<TakeoutFlowManager>();
            Set(flow, "queueManager", queue);
            Set(flow, "activeGroup", d);
            Set(flow, "currentPhase", TakeoutFlowManager.TakeoutPhase.WaitingForPayment);
            var restaurantRoot = Fixture("services", false);
            var restaurant = restaurantRoot.AddComponent<FastFoodRestaurant>();
            Set(restaurant, "counterQueue", queue); Set(restaurant, "counterFlow", flow);
            var kiosk = Fixture("kiosk", false).AddComponent<FastFoodServiceStation>();
            Set(kiosk, "kind", FastFoodServiceStation.StationKind.Kiosk);
            Set(kiosk, "queue", queue); Set(kiosk, "flow", flow); Set(kiosk, "staffApproach", orderPoint);
            var otherQueue = Fixture("second queue", false).AddComponent<TakeoutQueueManager>();
            Set(otherQueue, "orderPoint", orderPoint); Set(otherQueue, "exitPoint", overflow);
            Set(otherQueue, "queuePoints", new[] { firstSlot, secondSlot });
            otherQueue.gameObject.SetActive(true);
            var otherFlow = Fixture("second flow", false).AddComponent<TakeoutFlowManager>();
            Set(otherFlow, "queueManager", otherQueue);
            var counter = Fixture("counter", false).AddComponent<FastFoodServiceStation>();
            Set(counter, "queue", otherQueue); Set(counter, "flow", otherFlow); Set(counter, "staffApproach", orderPoint);
            Set(restaurant, "serviceStations", new[] { kiosk, counter });
            restaurantRoot.SetActive(true);
            Call(d, "ConfigureFastFood", restaurant, false);
            var ownership = (Dictionary<CustomerGroup, FastFoodServiceStation>)typeof(FastFoodRestaurant)
                .GetField("stationOwners", Fields).GetValue(restaurant);
            ownership.Add(d, kiosk);
            d.currentOrderNumber = 77;
            d.state = CustomerGroup.GroupState.OrderTaken;
            Set(d, "hasConfirmedOrder", true);
            d.SetTakeoutQueueState(CustomerGroup.TakeoutQueueState.AtOrderPoint);
            restaurant.TransferToCounter(d);
            Assert(otherQueue.CurrentFront == d && queue.Count == 0 && d.currentOrderNumber == 77 && d.HasConfirmedOrder,
                "Kiosk transfer lost the order or retained the original queue slot.");
            Assert(!restaurant.CanSettle(d), "Payment was accepted before arrival at the counter.");
            d.SetTakeoutQueueState(CustomerGroup.TakeoutQueueState.AtOrderPoint);
            Call(otherFlow, "SyncFrontCustomer");
            Assert(TakeoutFlowManager.For(d) == otherFlow && TakeoutQueueManager.For(d) == otherQueue &&
                otherFlow.CurrentPhase == TakeoutFlowManager.TakeoutPhase.WaitingForPayment,
                "A kiosk order was reviewed again or used the wrong station.");
            Assert(restaurant.ReserveSettlement(d) && !restaurant.ReserveSettlement(d), "Payment could be reserved twice.");
            Assert(!restaurant.ReserveSettlement(b), "Non-front customer could reserve payment.");
            flow.ResetFastFoodDay(); queue.ResetFastFoodDay();
            otherFlow.ResetFastFoodDay(); otherQueue.ResetFastFoodDay();
            flow.ResetFastFoodDay(); queue.ResetFastFoodDay();
            Assert(queue.Count == 0 && queue.CurrentFront == null && flow.ActiveGroup == null &&
                flow.CurrentPhase == TakeoutFlowManager.TakeoutPhase.None, "Day reset retained queue/payment state.");

            // Selection only changes presentation. It must not issue a movement command.
            var selector = Fixture("selector").AddComponent<TapOutlineSelector>();
            var mover = Fixture("manager", false).AddComponent<PlayerMovement>();
            var targetRoot = Fixture("selectable", false);
            var target = targetRoot.AddComponent<ClickToMoveTarget>();
            var outline = targetRoot.AddComponent<Outline>(); outline.enabled = false;
            targetRoot.SetActive(true);
            uint command = mover.CommandVersion;
            TapOutlineSelector.PresentFor(mover, target);
            Assert(outline.enabled && selector.CurrentSelection == target.transform && mover.CommandVersion == command,
                "Selection issued a gameplay command or lost its accepted outline.");
            Object.DestroyImmediate(targetRoot);
            Call(selector, "LateUpdate");
            Assert(selector.CurrentSelection == null && mover.CommandVersion == command, "Destroyed selection retained a target or issued input.");
            Debug.Log("[FastFood] PASS: FIFO, unchanged slots, destroyed front, duplicate removal/departure, overflow, settlement reservation, day reset, presentation-only selection.");
        }
        finally
        {
            for (int i = fixtures.Count - 1; i >= 0; i--)
                if (fixtures[i] != null) Object.DestroyImmediate(fixtures[i]);
            SceneManager.UnloadSceneAsync(scene);
        }
    }

    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Fields).Invoke(target, args);
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
