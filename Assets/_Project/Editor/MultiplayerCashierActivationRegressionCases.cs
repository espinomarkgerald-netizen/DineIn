#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Invoked only by the explicit external Play Mode regression runner.
public static class MultiplayerCashierActivationRegressionCases
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

    public static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Use the isolated external Play Mode runner.");
        var instanceField = typeof(CashierRegisterUI).GetField("<Instance>k__BackingField", Fields);
        var previous = CashierRegisterUI.Instance;
        if (previous != null) throw new InvalidOperationException("Cashier activation fixture requires the runner's empty scene.");
        var root = new GameObject("inactive cashier fixture", typeof(RectTransform));
        root.SetActive(false);
        var pos = new GameObject("POS", typeof(RectTransform), typeof(CanvasGroup));
        pos.transform.SetParent(root.transform, false);
        var buttonRoot = new GameObject("100 peso button", typeof(RectTransform));
        buttonRoot.transform.SetParent(pos.transform, false);
        var button = buttonRoot.AddComponent<Button>();
        var register = root.AddComponent<CashierRegisterUI>();
        var customerRoot = new GameObject("cashier customer");
        customerRoot.SetActive(false);
        var group = customerRoot.AddComponent<CustomerGroup>();
        group.currentOrder = null;
        Set(register, "root", root);
        Set(register, "bill100Button", button);
        try
        {
            Assert(CashierRegisterUI.Instance == null, "An inactive fixture unexpectedly Awoke before lookup.");
            Assert(CashierRegisterUI.ResolveInstance() == register, "The first payment could not resolve an authored inactive register.");
            Assert(!root.activeInHierarchy && !pos.activeSelf && !register.IsOpen,
                "Authority-only initialization displayed a remote player's register UI.");

            register.OpenForPayment(group, 500, 345);
            Assert(register.IsOpen && root.activeInHierarchy && pos.activeInHierarchy, "Accepted cash payment did not show its inactive panel.");
            Assert(Get<CustomerGroup>(register, "activeGroup") == group && Get<int>(register, "receivedAmount") == 500
                && Get<int>(register, "totalAmount") == 345 && Get<int>(register, "expectedChange") == 155,
                "Activation lost the accepted transaction's customer or amounts.");
            button.onClick.Invoke();
            Assert(Get<int>(register, "inputChangeAmount") == 100, "Initialization did not bind the change input exactly once.");
            typeof(CashierRegisterUI).GetMethod("Awake", Fields).Invoke(register, null);
            Assert(CashierRegisterUI.ResolveInstance() == register && register.IsOpen
                && Get<int>(register, "inputChangeAmount") == 100 && Get<CustomerGroup>(register, "activeGroup") == group,
                "A later Awake/lookup reset or hid an active transaction.");
            button.onClick.Invoke();
            Assert(Get<int>(register, "inputChangeAmount") == 200, "Repeated initialization multiplied input listeners.");
        }
        finally
        {
            register.Hide();
            instanceField.SetValue(null, previous);
            Object.Destroy(root);
            Object.Destroy(customerRoot);
        }
    }

    private static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Fields).GetValue(owner);
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Fields).SetValue(owner, value);
    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
