#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opt-in test using the authored Lobby2 navigation, real kiosks/payment and kitchen.
// Only runtime settings are changed; no scenes or player saves are written.
[InitializeOnLoad]
public static class FastFoodPickupRegression
{
    const string Key = "DineIn.FastFoodPickupRegression";
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static bool initialized, sawPaid, sawTray, sawCarry, sawBag;
    static bool requestedPickup, requestedDelivery;
    static int test;
    static double started;
    static float caseStarted;
    static CustomerGroup group;
    static FastFoodRestaurant restaurant;
    static Booth[] tables;
    static Booth table;
    public static string Result => SessionState.GetString(Key + ".result", "Not run");

    static FastFoodPickupRegression()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                initialized = false; test = SessionState.GetInt(Key + ".start", 0); started = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            { EditorApplication.update -= Tick; SessionState.SetBool(Key, false); }
        };
    }

    [MenuItem("Dine In/Fast Food/Run Customer Pickup Regression")]
    public static void Run()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play mode first.");
        Require(SceneManager.GetActiveScene().name == "Lobby2", "Open Lobby2 first.");
        Require(!SceneManager.GetActiveScene().isDirty, "Save scene changes first.");
        SessionState.SetBool(Key, true);
        SessionState.SetInt(Key + ".start", 0);
        SessionState.SetString(Key + ".result", "RUNNING");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Dine In/Fast Food/Run Table Delivery Regression")]
    public static void RunTableDelivery()
    {
        Run();
        SessionState.SetInt(Key + ".start", 5);
    }

    static void Tick()
    {
        try
        {
            if (GameSaveManager.Instance != null) GameSaveManager.Instance.SuppressWritesForTests = true;
            Require(EditorApplication.timeSinceStartup - started < 600, "Regression exceeded wall-clock deadline.");
            if (!initialized)
            {
                if (EditorApplication.timeSinceStartup - started < 3) return;
                restaurant = UnityEngine.Object.FindFirstObjectByType<FastFoodRestaurant>();
                Require(restaurant != null, "Restaurant missing.");
                tables = (Booth[])Get(restaurant, "diningTables");
                InventoryManager.Instance.SetAllStock(100);
                MoneyManager.Instance.SetMoney(50000, "Save-suppressed pickup regression");
                Set(GameDayManager.Instance, "useManagementComputerForDayStart", false);
                GameDayManager.Instance.StartShift();
                Require(GameDayManager.Instance.ShiftRunning, "Shift failed to start.");
                var spawnRoutine = Get(GameDayManager.Instance, "spawnRoutine") as Coroutine;
                if (spawnRoutine != null) GameDayManager.Instance.StopCoroutine(spawnRoutine);
                // Freeze the day clock, not the service systems under test.
                GameDayManager.Instance.enabled = false;
                GroupSpawner.Instance.SetAutoSpawn(false);
                GroupSpawner.Instance.enabled = false; // Start-shift callbacks may re-enable automatic spawning.
                var kiosks = UnityEngine.Object.FindObjectsByType<FastFoodServiceStation>(FindObjectsSortMode.None)
                    .Where(s => s.IsKiosk).ToArray();
                Require(kiosks.Length > 0, "No authored kiosks.");
                Set(restaurant, "serviceStations", kiosks);
                Set(restaurant, "tableDeliveryChance", 0f);
                initialized = true;
                NextCase();
                return;
            }
            Time.timeScale = 3f;
            Require(Time.time - caseStarted < 180, "Case " + test + " timed out: " + (group == null ? "destroyed" : group.state.ToString()));
            Require(group != null, "Customer disappeared before service completed.");
            sawPaid |= group.FastFoodPaid;
            var tray = UnityEngine.Object.FindObjectsByType<FoodTray>(FindObjectsSortMode.None).FirstOrDefault(t => t.TargetGroup == group);
            var bag = UnityEngine.Object.FindObjectsByType<TakeoutBagInteractable>(FindObjectsSortMode.None).FirstOrDefault(b => b.TargetGroup == group);
            var hands = WaiterHands.ActivePlayerHands;
            if (tray != null)
            {
                sawTray = true;
                var interaction = tray.GetComponent<FoodTrayInteractable>();
                if (test == 6 || test == 7)
                {
                    Require(group.FastFoodRequestsTableDelivery && !group.FastFoodSelfPickup, "Delivery request was not honored.");
                    Require(!interaction.IsCustomerReserved, "Delivery customer attempted self-pickup.");
                    if (group.AllowsStaffFoodDelivery && !requestedPickup && interaction.CanInteract())
                    { requestedPickup = true; interaction.UI_RequestPickup(); }
                    if (hands != null && hands.holdingTray == tray)
                    {
                        sawCarry = true;
                        if (!requestedDelivery)
                        {
                            requestedDelivery = true;
                            Require(table.GetComponent<FastFoodTable>().CanInteract(), "Table click rejected held delivery tray.");
                            RoleManager.Instance.GetActivePlayerMovement().UI_MoveTo(table.GetComponent<FastFoodTable>());
                        }
                    }
                }
                else
                {
                    Require(!interaction.CanInteract(), "Player can pick up a self-service meal.");
                    Require(hands == null || !hands.PickupTray(tray), "Hands bypassed customer-only pickup.");
                    sawCarry |= interaction.IsCustomerReserved && !group.FastFoodRepresentative.IsSeated;
                }
            }
            if (bag != null)
            {
                sawBag = true;
                Require(!bag.CanInteract(), "Player can select customer-only takeout.");
                bag.TryPickup();
                Require(!bag.TryPickupForStaff(hands), "Staff can pick up customer-only takeout.");
                Require(TakeoutBagInteractable.HeldBag != bag, "Player took customer-only takeout.");
            }
            foreach (var member in group.members)
                if (member.IsSeated) Require(!member.Agent.enabled, "Seated member blocks the approach.");

            bool completed = test == 5 ? sawPaid && sawBag && group.IsFastFoodLeaving
                : group.state == CustomerGroup.GroupState.Eating;
            if (!completed)
            {
                Require(!group.IsFastFoodLeaving, "Customer left before eating in case " + test);
                return;
            }
            Require(sawPaid, "Payment was skipped.");
            if (test != 5)
            {
                Require(sawTray && sawCarry && group.HasReceivedCurrentFastFoodOrder, "Pickup/delivery was skipped.");
                Require(group.members.All(m => m.IsSeated), "Collector did not sit back down.");
                Require(tray != null && tray.transform.parent == table.NetworkTrayPoint, "Tray was not placed on the table.");
                Require(restaurant.ReservedPickupFor(group) == null, "Pickup slot leaked.");
            }
            Debug.Log("[FastFoodPickupRegression] PASS case " + test + (table == null ? " takeout" : " " + table.name));
            if (test == 9)
            { Finish("PASS: " + (SessionState.GetInt(Key + ".start", 0) == 0 ? "groups 1-4 self-pickup and pink takeout; " : "") + "mandatory pink and optional green table delivery; both back stool tables served four diners in delivery and self-pickup modes."); return; }
            // Isolate the next case only after asserting the completed transaction.
            foreach (var m in group.members) SeatAnchor.VacateAllFor(m.gameObject);
            UnityEngine.Object.Destroy(group.gameObject);
            if (tray != null) UnityEngine.Object.Destroy(tray.gameObject);
            if (table != null) table.ResetFastFoodDay();
            NextCase();
        }
        catch (Exception e) { Finish("FAIL: " + e); }
    }

    static void NextCase()
    {
        test++;
        int size = test >= 6 ? 4 : test == 5 ? 2 : test;
        string category = test == 1 ? "Long" : test == 2 ? "Round" : "Booth";
        if (test >= 6) category = "Back Stool Table " + (test % 2 == 0 ? "1" : "2");
        table = test == 5 ? null : tables.FirstOrDefault(b => b.name.IndexOf(category, StringComparison.OrdinalIgnoreCase) >= 0 && b.seats.Count >= size);
        if (test != 5) Require(table != null, "Missing " + category + " table for size " + test);
        Set(restaurant, "diningTables", table == null ? tables : new[] { table });
        Set(restaurant, "takeoutChance", test == 5 ? 1f : 0f);
        Set(restaurant, "tableDeliveryChance", test == 7 ? 1f : 0f);
        GroupSpawner.Instance.SetCustomerTypeAvailability(test != 5 && test != 6, test == 5 || test == 6, false);
        Set(GroupSpawner.Instance, "minGroupSize", size);
        Set(GroupSpawner.Instance, "maxGroupSize", size);
        requestedPickup = requestedDelivery = false;
        sawPaid = sawTray = sawCarry = sawBag = false;
        caseStarted = Time.time;
        group = GroupSpawner.Instance.SpawnGroup();
        Require(group != null, "Spawn rejected for case " + test);
        SessionState.SetString(Key + ".result", "RUNNING case " + test);
    }

    static void Finish(string result)
    {
        SessionState.SetString(Key + ".result", result);
        Debug.Log("[FastFoodPickupRegression] " + result);
        EditorApplication.update -= Tick;
        Time.timeScale = 1f;
        EditorApplication.ExitPlaymode();
    }
    static object Get(object obj, string name) => obj.GetType().GetField(name, Fields).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Fields).SetValue(obj, value);
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
