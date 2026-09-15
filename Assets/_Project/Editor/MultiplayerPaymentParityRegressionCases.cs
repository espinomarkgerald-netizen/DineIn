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
