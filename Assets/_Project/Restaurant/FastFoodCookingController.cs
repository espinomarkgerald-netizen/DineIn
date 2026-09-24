using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class FastFoodReadyFoodSaveEntry
{
    public string productId;
    public int quantity;
}

/// <summary>Lobby2 adapter for the shared ledger; existing KitchenManager still delivers orders.</summary>
public sealed class FastFoodCookingController : MonoBehaviour
{
    public static FastFoodCookingController Instance { get; private set; }
    public FastFoodCookingState State { get; private set; }
    public bool IsHelpingKitchen => view != null && view.IsOpen;
    [Header("Relaxed kitchen help")]
    [SerializeField, Min(2)] private int playerEvery = 10;
    [SerializeField, Min(1)] private int staffSlots = 4;
    [SerializeField, Min(0)] private int reserveServings = 2;
    [SerializeField, Min(1)] private float cookSeconds = 8, overcookSeconds = 8, batchDeadline = 300;
    [SerializeField, Min(1)] private float staffSpeed = 2;
    [SerializeField, Min(.1f)] private float assemblySeconds = 2;
    [Serializable] public class GuidanceText
    {
        public string action;
        [TextArea] public string detail;
        public GuidanceText(string action,string detail) { this.action=action; this.detail=detail; }
    }
    [Header("Task HUD wording (use {0} for the food, ingredient or order number)")]
    [SerializeField] private GuidanceText idleGuidance=new GuidanceText("Staff are handling production","A small assignment will appear when help is needed.");
    [SerializeField] private GuidanceText assemblyGuidance=new GuidanceText("Assemble order #{0}","Drag the pictured food and drinks onto your tray.");
    [SerializeField] private GuidanceText serveGuidance=new GuidanceText("Serve the completed order","Drag the pictured food and drinks onto your tray.");
    [SerializeField] private GuidanceText loadGuidance=new GuidanceText("Load {0}","Drag its hotbar icon onto the cooking slot.");
    [SerializeField] private GuidanceText cookGuidance=new GuidanceText("Cooking {0}","Wait for Ready, then move it to the preparation board.");
    [SerializeField] private GuidanceText collectGuidance=new GuidanceText("Collect the ready food","Drag it onto the preparation board before it burns.");
    [SerializeField] private GuidanceText prepareGuidance=new GuidanceText("Add {0}","Drop the ingredient onto the preparation board.");
    [SerializeField] private GuidanceText burntGuidance=new GuidanceText("Discard burnt food","Drag it to Discard. Unfinished food does not consume ingredients.");
    private readonly Dictionary<int, CustomerGroup> groups = new Dictionary<int, CustomerGroup>();
    private float reserveCheck;
    [SerializeField] private FastFoodCookingView view;
    [SerializeField, Min(.1f)] private float reserveRefreshSeconds = 2;
    public bool Active => gameObject.scene.name == "Lobby2" && !MultiplayerDayBridge.IsActive;
    public static bool Handles(CustomerGroup group) => group != null && group.FastFood != null && ForScene() != null;
    public static FastFoodCookingController ForScene()
    {
        if (MultiplayerDayBridge.IsActive) return null;
        var scene = SceneManager.GetSceneByName("Lobby2");
        if (!scene.isLoaded) return null;
        if (Instance != null && Instance.Active) return Instance;
        return scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FastFoodCookingController>(true)).FirstOrDefault();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if(view==null)view=GetComponent<FastFoodCookingView>();
        ResetState();
    }
    private void OnValidate() { if(State!=null)Configure(); }
    public void ResetState()
    {
        view?.Exit();
        groups.Clear();
        State = new FastFoodCookingState(type => InventoryManager.Instance != null ? InventoryManager.Instance.GetStock(type) : 0, Consume);
        State.Changed += () => { LobbyStockBridge.Instance?.RefreshKitchenAvailability(); GameSaveManager.Instance?.RequestSave(); };
        Configure();
    }
    private void Configure()
    {
        State.playerEvery = Mathf.Max(2, playerEvery); State.staffSlotsPerStation = Mathf.Max(1, staffSlots);
        State.cookSeconds = Mathf.Max(1, cookSeconds); State.overcookSeconds = Mathf.Max(1, overcookSeconds);
        State.staffMultiplier = Mathf.Max(1, staffSpeed); State.assemblySeconds = Mathf.Max(.1f, assemblySeconds);
        State.deadlineSeconds = Mathf.Max(1, batchDeadline);
    }
    private bool Consume(Recipe recipe)
    {
        var inv = InventoryManager.Instance;
        // The atomic transaction is also used by the existing order flow.
        return inv != null && inv.TryUseStockBatch(FastFoodCookingState.Requirements(recipe), null);
    }
    public void ResetForDay()
    {
        var ready = State.Portions.Where(p => p.stage == FastFoodCookingStage.Complete && State.FindTicket(p.order)?.submitted != true).Select(p => p.recipe).ToArray();
        ResetState();
        foreach (var recipe in ready) State.RestoreReady(recipe, 1);
    }
    public bool Accept(CustomerGroup group, IReadOnlyList<Recipe> recipes, Func<bool> commit)
    {
        int order = group.currentOrderNumber;
        bool existed = State.FindTicket(order) != null;
        if (!existed && !State.Accept(order, recipes)) return false;
        groups[order] = group;
        bool accepted = false;
        try { accepted = commit == null || commit(); RebindRemake(group); return accepted; }
        finally { if (!accepted && !existed) { Release(order, false); if (group.currentOrderNumber != order) Release(group.currentOrderNumber, false); } }
    }
    private void RebindRemake(CustomerGroup group)
    {
        if (State.FindTicket(group.currentOrderNumber) != null) return;
        foreach (var pair in groups.ToArray())
            if (pair.Value == group && State.ReassignOrderNumber(pair.Key, group.currentOrderNumber))
            { groups.Remove(pair.Key); groups[group.currentOrderNumber] = group; break; }
    }
    public IEnumerator WaitForAssembly(CustomerGroup group)
    {
        RebindRemake(group);
        int order = group.currentOrderNumber;
        if (State.FindTicket(order) == null && !Accept(group, group.currentOrder.ResolveProducts(), null)) yield break;
        // Reviewed acceptance may precede payment. Food only starts after the payment flow authorizes it.
        while (Valid(group, order) && !group.FastFoodPaid) yield return null;
        if (!Valid(group, order)) yield break;
        State.Activate(order);
        while (Valid(group, order) && State.FindTicket(order)?.submitted != true) yield return null;
    }
    public bool IsAssembled(int order) => State.FindTicket(order)?.submitted == true;
    public void Release(int order, bool delivered) { State.Release(order, delivered); groups.Remove(order); }
    private static bool Valid(CustomerGroup group, int order) => group != null && group.currentOrderNumber == order &&
        !group.IsFastFoodLeaving && group.HasConfirmedOrder &&
        (group.state == CustomerGroup.GroupState.OrderTaken || group.FastFoodAwaitingSeat);
    private void Update()
    {
        if (!Active || InventoryManager.Instance == null || GameSaveManager.Instance?.IsApplyingSave == true) return;
        State.cookSeconds = Mathf.Max(1, cookSeconds) * HygieneManager.CookMultiplier;
        foreach (var entry in groups.ToArray()) if (!Valid(entry.Value, entry.Key)) Release(entry.Key, false);
        State.Tick(Time.deltaTime, HygieneManager.HoldNewCooking, HygieneManager.KitchenPaused);
        if (Time.time >= reserveCheck)
        {
            reserveCheck = Time.time + reserveRefreshSeconds;
            if (groups.Values.Any(g => g != null && g.FastFoodPaid) && !HygieneManager.HoldNewCooking)
                State.EnsureReserve(MenuCatalog.Default.Products.Where(MenuAvailabilityManager.IsProductAvailable), reserveServings);
        }
        PublishGuidance();
    }
    private void PublishGuidance()
    {
        if (State.Mode == FastFoodStationMode.None) { PlayerTaskGuidance.ClearTask("FastFoodCooking"); return; }
        var p = State.PlayerPortion;
        GuidanceText text=idleGuidance; object argument="";
        if (State.Mode == FastFoodStationMode.Assembler)
        {
            var t = State.PlayerTicket;
            if (t != null) { text=t.Ready?serveGuidance:assemblyGuidance; argument=t.number; }
        }
        else if (p != null)
        {
            var steps = FastFoodCookingState.Steps(p.recipe);
            switch (p.stage)
            {
                case FastFoodCookingStage.Waiting: text=loadGuidance; argument=steps[0].item.displayName; break;
                case FastFoodCookingStage.Cooking: text=cookGuidance; argument=p.recipe.DisplayName; break;
                case FastFoodCookingStage.Ready: text=collectGuidance; break;
                case FastFoodCookingStage.Preparing: text=prepareGuidance; argument=steps[Mathf.Min(p.ingredientStep,steps.Count-1)].item.displayName; break;
                case FastFoodCookingStage.Burnt: text=burntGuidance; break;
            }
        }
        PlayerTaskGuidance.SetTask("FastFoodCooking", State.Mode.ToString(), string.Format(text.action,argument), string.Format(text.detail,argument), 1000, this, PlayerTaskCategory.Kitchen);
    }
    public void FillSaveData(GameSaveData data)
    {
        data.fastFoodReadyFood = State.Portions.Where(p => p.stage == FastFoodCookingStage.Complete && State.FindTicket(p.order)?.submitted != true)
            .GroupBy(p => p.recipe.ProductId).Select(g => new FastFoodReadyFoodSaveEntry { productId = g.Key, quantity = g.Count() }).ToList();
    }
    public void ApplySaveData(GameSaveData data)
    {
        ResetState();
        if (data.fastFoodReadyFood == null) return;
        foreach (var entry in data.fastFoodReadyFood)
        {
            var recipe = MenuCatalog.Default.FindProduct(entry.productId);
            if (recipe != null) State.RestoreReady(recipe, Mathf.Max(0, entry.quantity));
        }
    }
    private void OnDestroy()
    { view?.Exit(); PlayerTaskGuidance.ClearTask("FastFoodCooking"); PlayerTaskGuidance.SetKitchenFocus(false); if (Instance == this) Instance = null; }
}
