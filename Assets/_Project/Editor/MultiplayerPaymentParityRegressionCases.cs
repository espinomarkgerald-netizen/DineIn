#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

// Called only by the explicit external regression runner, never on import.
public static class MultiplayerPaymentParityRegressionCases
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

    public static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Run in the external runner's isolated Play Mode scene.");
        var objects = new List<GameObject>();
        GameObject Fixture(string name)
        {
            var root = new GameObject(name); root.SetActive(false); objects.Add(root); return root;
        }
        var targetId = typeof(RestaurantTaskClaim).GetField("activePlayerTargetId", Fields);
        var target = typeof(RestaurantTaskClaim).GetField("activePlayerTarget", Fields);
        object previousId = targetId.GetValue(null), previousTarget = target.GetValue(null);
        int claimChanges = 0;
        void ClaimChanged() => claimChanges++;
        RestaurantTaskClaim.PlayerTaskChanged += ClaimChanged;
        try
        {
            var group = Fixture("payment parity customer").AddComponent<CustomerGroup>();
            group.currentOrderNumber = 37;
            group.state = CustomerGroup.GroupState.NeedsBill;
            var booth = Fixture("payment parity booth").AddComponent<Booth>();
            var spawner = booth.gameObject.AddComponent<BoothMoneySpawner>();
            group.assignedBooth = booth;
            var money = Fixture("canonical cash").AddComponent<MoneyPickup>();
            money.Init(group, 500, booth.transform);
            var a = Fixture("cash holder A").AddComponent<WaiterHands>();
            var b = Fixture("cash holder B").AddComponent<WaiterHands>();
            targetId.SetValue(null, money.GetInstanceID()); target.SetValue(null, money);

            int identity = money.GetInstanceID();
            a.PresentMoneyPickup(money);
            for (int i = 0; i < 20; i++) a.PresentMoneyPickup(money);
            Assert(a.HeldMoney == money && a.holdingMoneyFor == group && a.holdingMoneyAmount == 500 && money.IsPickedUp,
                "Repeated carried snapshots lost the canonical payment or its amount.");
            b.PresentMoneyPickup(money);
            Assert(!a.HasMoney && a.holdingMoneyFor == null && a.holdingMoneyAmount == 0 && b.HeldMoney == money,
                "Transferring a payment left money in both actors' hands.");
            Assert(money.transform.parent == b.MoneyHoldPoint, "Payment did not attach to the actual holder.");
            Assert(money.PresentAtPickup(), "Cancellation could not return the retained payment to its booth.");
            Assert(!b.HasMoney && b.holdingMoneyFor == null && !money.IsPickedUp && money.GetInstanceID() == identity
                && money.Amount == 500 && money.OrderNumber == 37 && money.transform.parent == spawner.MoneySpawnPoint,
                "Cancellation duplicated money, changed its identity, or stranded the old holder.");
            Assert(claimChanges == 0 && RestaurantTaskClaim.IsClaimedByPlayer(money),
                "Presentation dispatched gameplay claim completion or cancellation.");

            a.PresentMoneyPickup(money);
            var teardownParent = money.transform.parent;
            a.DetachMoneyPickup(money, reparent: false);
            Assert(!a.HasMoney && a.holdingMoneyFor == null && a.holdingMoneyAmount == 0
                && money.transform.parent == teardownParent,
                "Destruction cleanup changed the dying payment's parent or retained holder state.");
            Assert(money.PresentAtPickup() && money.transform.parent == spawner.MoneySpawnPoint,
                "Normal payment return stopped restoring its pickup parent.");

            typeof(BoothMoneySpawner).GetField("spawned", Fields).SetValue(spawner, money);
            spawner.ForgetObservedMoney(null);
            Assert(spawner.HasMoneySpawned, "An unrelated despawn removed the current payment registry entry.");
            spawner.ForgetObservedMoney(money);
            Assert(!spawner.HasMoneySpawned && money != null, "Forgetting a replica destroyed its payment or retained its old registry entry.");
            group.currentOrderNumber = 38;
            Assert(money.OrderNumber == 37, "An old payment adopted a new order identity.");
            a.holdingMoneyFor = group; a.holdingMoneyAmount = 500;
            a.ClearMoney();
            Assert(a.holdingMoneyFor == null && a.holdingMoneyAmount == 0 && !a.HasMoney,
                "Clearing a legacy virtual payment retained group/amount without a MoneyPickup object.");

            var tray = Fixture("retained service tray").AddComponent<FoodTray>();
            tray.orderNumber = 38;
            a.holdingTray = tray;
            a.ReconcileHeldItemReferences();
            Assert(!a.HasTray && tray.transform.parent == null && tray != null,
                "A tray already at its destination still blocked the old holder.");
            tray.transform.SetParent(a.TrayHoldPoint, true);
            a.holdingTray = tray; b.holdingTray = tray;
            a.ReconcileHeldItemReferences(); b.ReconcileHeldItemReferences();
            Assert(a.holdingTray == tray && !b.HasTray && tray.transform.parent == a.TrayHoldPoint,
                "Reconciling an old holder disturbed the actor actually carrying the food.");
            Assert(a.MultiplayerHeldItemBlocker()?.Contains("Deliver the food") == true,
                "A genuinely held food tray lost its next-step guidance.");
            var busser = Fixture("dirty tray holder").AddComponent<BusserHands>();
            busser.holdingTray = tray;
            busser.ReconcileHeldTrayReference();
            Assert(!busser.HasTray && a.HasTray, "Dirty-tray reconciliation stole another holder's food.");
            tray.transform.SetParent(busser.TrayHoldPoint, true);
            busser.holdingTray = tray;
            busser.ReconcileHeldTrayReference(); a.ReconcileHeldItemReferences();
            Assert(busser.holdingTray == tray && !a.HasTray, "A genuine carried dirty tray was discarded.");

            var bill = Fixture("retained bill").AddComponent<BillPaper>();
            bill.Init(group); a.PresentBillPaper(bill);
            a.ReconcileHeldItemReferences();
            Assert(a.HasBill && bill.transform.parent == a.BillHoldPoint,
                "A genuine undelivered held bill was forgotten.");
            group.IsNetworkObserver = true;
            group.PresentMultiplayerBillDelivered(true);
            a.ReconcileHeldItemReferences();
            Assert(!a.HasBill && bill != null && bill.transform.parent == a.BillHoldPoint,
                "Delivered-bill reconciliation retained its hand lock or moved the physical bill.");

            group.PresentServiceState(new MultiplayerServiceActions.GroupState { paid = true });
            a.PresentMoneyPickup(money);
            var paidMoneyParent = money.transform.parent;
            a.ReconcileHeldItemReferences();
            Assert(!a.HasMoney && a.holdingMoneyFor == null && money != null && money.transform.parent == paidMoneyParent
                && money.Amount == 500 && group.MultiplayerPaymentComplete,
                "Settled cash kept an invisible hand lock or reconciliation changed the payment.");

            var ticketGroup = Fixture("unsubmitted ticket customer").AddComponent<CustomerGroup>();
            ticketGroup.state = CustomerGroup.GroupState.OrderTaken;
            a.holdingTicketFor = ticketGroup;
            a.ReconcileHeldItemReferences();
            Assert(a.HasTicket, "An outstanding order ticket was discarded.");
            ticketGroup.state = CustomerGroup.GroupState.Eating;
            a.ReconcileHeldItemReferences();
            Assert(!a.HasTicket && claimChanges == 0, "An already served order retained its ticket lock or completed a claim twice.");

            var orphanMoney = Fixture("orphan payment").AddComponent<MoneyPickup>();
            orphanMoney.Init(ticketGroup, 200, booth.transform);
            a.PresentMoneyPickup(orphanMoney); a.ReconcileHeldItemReferences();
            Assert(a.HeldMoney == orphanMoney, "Unpaid attached cash was cleared before its customer disappeared.");
            typeof(MoneyPickup).GetField("targetGroup", Fields).SetValue(orphanMoney, null);
            a.ReconcileHeldItemReferences();
            Assert(!a.HasMoney && orphanMoney.transform.parent == a.MoneyHoldPoint,
                "An orphan payment kept an impossible cashier task or was reparented during reference cleanup.");
            var orphanBill = Fixture("orphan bill").AddComponent<BillPaper>();
            orphanBill.Init(ticketGroup); a.PresentBillPaper(orphanBill);
            typeof(BillPaper).GetField("targetGroup", Fields).SetValue(orphanBill, null);
            a.ReconcileHeldItemReferences();
            Assert(!a.HasBill && orphanBill.transform.parent == a.BillHoldPoint,
                "An orphan bill kept an impossible delivery task or was moved during reference cleanup.");
        }
        finally
        {
            RestaurantTaskClaim.PlayerTaskChanged -= ClaimChanged;
            targetId.SetValue(null, previousId); target.SetValue(null, previousTarget);
            foreach (var root in objects) if (root != null) Object.Destroy(root);
        }
    }

    private static void Assert(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
#endif
