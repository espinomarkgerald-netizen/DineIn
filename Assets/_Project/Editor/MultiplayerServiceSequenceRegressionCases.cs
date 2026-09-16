#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

// Explicit external Play Mode runner only. Network/player-control acceptance
// remains necessary for greeting claims and delayed cleanup attachment.
public static class MultiplayerServiceSequenceRegressionCases
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

    public static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Use the external Play Mode runner.");
        var root = new GameObject("service sequencing fixture");
        root.SetActive(false);
        var group = root.AddComponent<CustomerGroup>();
        var trayRoot = new GameObject("used tray");
        trayRoot.SetActive(false);
        var tray = trayRoot.AddComponent<FoodTray>();
        var pickup = trayRoot.AddComponent<FoodTrayInteractable>();
        typeof(FoodTray).GetField("targetGroup", Hidden).SetValue(tray, group);
        typeof(FoodTrayInteractable).GetField("tray", Hidden).SetValue(pickup, tray);
        var available = typeof(FoodTrayInteractable).GetMethod("IsNetworkModeAvailable", Hidden);
        bool Visible(FoodTrayInteractable.TrayMode mode, bool complaint = false) =>
            (bool)available.Invoke(null, new object[] { mode, group, complaint });
        try
        {
            group.state = CustomerGroup.GroupState.OrderTaken;
            Assert(Visible(FoodTrayInteractable.TrayMode.Delivery), "A prepared meal lost its pickup action.");
            group.state = CustomerGroup.GroupState.Eating;
            Assert(!Visible(FoodTrayInteractable.TrayMode.Delivery), "A late pickup update became visible after eating started.");
            Assert(!Visible(FoodTrayInteractable.TrayMode.Cleanup), "Normal cleanup became available before the customer left.");
            Assert(Visible(FoodTrayInteractable.TrayMode.Cleanup, true), "Complaint removal was incorrectly hidden for a seated customer.");
            group.state = CustomerGroup.GroupState.NeedsBill;
            Assert(!Visible(FoodTrayInteractable.TrayMode.Delivery), "Payment-stage customer regained delivery pickup.");
            group.state = CustomerGroup.GroupState.Leaving;
            Assert(Visible(FoodTrayInteractable.TrayMode.Cleanup), "Departing customer's used tray did not permit cleanup.");
            Assert((bool)available.Invoke(null, new object[] { FoodTrayInteractable.TrayMode.Cleanup, null, false }),
                "Cleanup disappeared when the departed customer object was destroyed.");

            typeof(FoodTrayInteractable).GetField("mode", Hidden).SetValue(pickup, FoodTrayInteractable.TrayMode.Delivery);
            pickup.NotifyDeliveredToTable();
            Assert(pickup.CurrentMode == FoodTrayInteractable.TrayMode.None, "Delivery failed to clear pickup mode.");
            tray.NetworkCarryLocked = true;
            typeof(FoodTrayInteractable).GetMethod("CheckCleanupState", Hidden).Invoke(pickup, null);
            Assert(pickup.CurrentMode == FoodTrayInteractable.TrayMode.None, "A carried dirty tray restarted cleanup pickup.");
            CleanupMovementFence(root);
        }
        finally { Object.Destroy(trayRoot); Object.Destroy(root); }
    }

    private static void Assert(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    private static void CleanupMovementFence(GameObject root)
    {
        // Inactive fixtures avoid Awake, navigation, rooms and gameplay actions.
        var mover = root.AddComponent<PlayerMovement>();
        var service = root.AddComponent<MultiplayerServiceActions>();
        var serviceType = typeof(MultiplayerServiceActions);
        void Set(string field, object value) => serviceType.GetField(field, Hidden).SetValue(service, value);
        bool Held() => (bool)serviceType.GetMethod("ShouldHoldDirtyPosition", Hidden).Invoke(service, new object[] { mover });
        var command = Activator.CreateInstance(serviceType.GetNestedType("Command", BindingFlags.NonPublic), true);
        var context = new MultiplayerActionContext { operation = "cleanup_pickup" };
        command.GetType().GetField("context").SetValue(command, context);
        Set("pending", command);
        Set("dirtyPickupMover", mover);
        Set("dirtyPickupCommandVersion", mover.CommandVersion);
        Assert(Held(), "Idle return was not blocked while a dirty pickup reply was pending.");
        Set("pending", null);
        Set("deferredDirtyPickup", Activator.CreateInstance(serviceType.GetNestedType("Reply", BindingFlags.NonPublic), true));
        Assert(Held(), "Idle return resumed before an accepted dirty tray attached.");

        var registerCommand = typeof(PlayerMovement).GetMethod("RegisterCommand", Hidden);
        uint before = mover.CommandVersion;
        registerCommand.Invoke(mover, null);
        registerCommand.Invoke(mover, null); // Same-frame commands still have distinct identities.
        Assert(mover.CommandVersion == before + 2 && !Held(), "A newer movement intent did not supersede delayed cleanup continuation.");
        Set("deferredDirtyPickup", null);
        context.operation = "cleanup_wash";
        Set("pending", command);
        Set("dirtyDisposalMover", mover);
        Set("dirtyDisposalCommandVersion", mover.CommandVersion);
        Assert(Held(), "Idle return was not blocked until disposal acknowledgement.");
        registerCommand.Invoke(mover, null);
        Assert(!Held(), "An old disposal still held the player after a newer movement command.");
    }
}
#endif
