using System;
using System.Collections.Generic;
using System.Linq;

public enum FastFoodStationMode { None, Grill, Fry, Assembler }
public enum FastFoodCookingStage { Waiting, Cooking, Ready, Preparing, Burnt, Complete }
public enum FastFoodCookingSlotOwner { Player, Staff }

/// <summary>Shared kitchen ledger. Reservations are not consumption. Only Finish debits stock.</summary>
public sealed class FastFoodCookingState
{
    public sealed class Portion
    {
        public Recipe recipe;
        public int order = -1;
        public FastFoodCookingStage stage;
        public bool player, placed, dragging;
        public int ingredientStep;
        public float elapsed, assignedFor;
        public Batch batch;
        public int slot = -1;
        public bool proteinReady;
    }
    public sealed class Ticket
    {
        public int number;
        public bool player, submitted, active, assemblyStarted;
        public float elapsed, assemblyElapsed;
        public readonly List<Portion> portions = new List<Portion>();
        public bool Ready => portions.Count > 0 && portions.All(p => p.placed);
    }
    public sealed class Batch
    {
        public Recipe recipe;
        public int target, completed;
        public float elapsed;
        public bool sealedTarget;
    }

    public readonly List<Portion> Portions = new List<Portion>();
    public readonly List<Ticket> Tickets = new List<Ticket>();
    public readonly List<Batch> Batches = new List<Batch>();
    public readonly List<Portion> PlayerWork = new List<Portion>();
    public int playerBatchSize = 6, grillSlots = 2, frySlots = 4;
    public FastFoodStationMode Mode { get; private set; }
    public int playerEvery = 10, staffSlotsPerStation = 4;
    public float cookSeconds = 8, overcookSeconds = 8, staffMultiplier = 2, assemblySeconds = 2, deadlineSeconds = 300;
    public float completionHoldSeconds = .9f;
    private float completedBatchFor;
    private readonly Dictionary<FastFoodStationMode, FastFoodCookingSlotOwner[]> slotOwners = new();
    private readonly HashSet<Portion> staffFinishing = new();
    public bool StationWorkDone(Portion p) => p.stage == FastFoodCookingStage.Complete || p.proteinReady && PreparationStation(p.recipe) != Mode;
    public bool ShowingCompletedBatch => PlayerWork.Count > 0 && PlayerWork.All(StationWorkDone) && completedBatchFor < completionHoldSeconds;
    private readonly Func<ItemType, int> stock;
    private readonly Func<Recipe, bool> consume;
    public event Action Changed;

    public FastFoodCookingState(Func<ItemType, int> stock, Func<Recipe, bool> consume)
    { this.stock = stock; this.consume = consume; }

    public static FastFoodStationMode Station(Recipe recipe)
    {
        if (recipe.category == MenuProductCategory.Drink) return FastFoodStationMode.Assembler;
        if (recipe.cookingStation == FastFoodStationMode.Grill || recipe.cookingStation == FastFoodStationMode.Fry) return recipe.cookingStation;
        return recipe.kitchenItemType == ItemTypeKitchen.Burger || recipe.kitchenItemType == ItemTypeKitchen.ChickenSandwich ||
            recipe.kitchenItemType == ItemTypeKitchen.FishFilletSandwich ? FastFoodStationMode.Grill : FastFoodStationMode.Fry;
    }
    public static List<RecipeIngredient> Steps(Recipe recipe)
    {
        var steps = recipe.ingredients == null ? new List<RecipeIngredient>() :
            recipe.ingredients.Where(i => i != null && i.item != null && i.amount > 0).ToList();
        // Protein is cooked first; the remaining recipe ingredients are placed at the prep board.
        int protein = recipe.firstCookingIngredient != null ? steps.FindIndex(i=>i.item==recipe.firstCookingIngredient) :
            steps.FindIndex(i => i.item.itemType == ItemType.Patty || i.item.itemType == ItemType.ChickenPatty || i.item.itemType == ItemType.FrozenFishFillet);
        if (protein > 0) { var first = steps[protein]; steps.RemoveAt(protein); steps.Insert(0, first); }
        return steps;
    }
    public static Dictionary<ItemType, int> Requirements(Recipe recipe)
    {
        var result = new Dictionary<ItemType, int>();
        foreach (var step in Steps(recipe))
        { result.TryGetValue(step.item.itemType, out int n); result[step.item.itemType] = n + step.amount; }
        return result;
    }
    private static bool Reserved(Portion p) => p.stage != FastFoodCookingStage.Complete && p.stage != FastFoodCookingStage.Burnt;
    public int Available(ItemType item)
    {
        int n = stock(item);
        foreach (var p in Portions.Where(Reserved))
            if (Requirements(p.recipe).TryGetValue(item, out int used)) n -= used;
        return Math.Max(0, n);
    }
    public int ReadyStock(Recipe recipe) => Portions.Count(p => p.recipe == recipe && p.order < 0 && p.stage == FastFoodCookingStage.Complete && !p.dragging);
    public Ticket FindTicket(int order) => Tickets.Find(t => t.number == order);
    public bool ReassignOrderNumber(int previous, int current)
    {
        var ticket = FindTicket(previous);
        if (ticket == null || FindTicket(current) != null) return false;
        ticket.number = current;
        foreach (var portion in ticket.portions) portion.order = current;
        return true;
    }
    public static FastFoodStationMode PreparationStation(Recipe recipe) => recipe.preparationStation != FastFoodStationMode.None ? recipe.preparationStation : Station(recipe);
    public static List<KitchenAssemblyStep> AssemblySteps(Recipe recipe) => recipe.kitchenAssemblySteps != null && recipe.kitchenAssemblySteps.Count > 0 ? recipe.kitchenAssemblySteps :
        PreparationStation(recipe) == FastFoodStationMode.Grill ? Steps(recipe).Skip(1).Select(i => new KitchenAssemblyStep { item = i.item, label = i.item.displayName }).ToList() : new List<KitchenAssemblyStep>();
    public static FastFoodStationMode WorkStation(Portion p) => p.stage == FastFoodCookingStage.Preparing ? PreparationStation(p.recipe) : Station(p.recipe);
    public int Capacity(FastFoodStationMode station) => station == FastFoodStationMode.Grill ? Math.Max(1, grillSlots) : Math.Max(1, frySlots);
    public void ConfigureSlots(FastFoodStationMode station, FastFoodCookingSlotOwner[] owners)
    {
        if (station != FastFoodStationMode.Grill && station != FastFoodStationMode.Fry) return;
        if (owners == null || owners.Length == 0) throw new ArgumentException("A cooking station needs authored positions.");
        slotOwners[station] = (FastFoodCookingSlotOwner[])owners.Clone();
        if (station == FastFoodStationMode.Grill) grillSlots = owners.Length; else frySlots = owners.Length;
    }
    public bool CanUseSlot(FastFoodStationMode station, int slot, bool player)
    {
        if (slot < 0 || slot >= Capacity(station)) return false;
        if (!slotOwners.TryGetValue(station, out var owners)) return true;
        // Staff can finish abandoned food in place, but new work uses its saved side.
        return owners[slot] == (player ? FastFoodCookingSlotOwner.Player : FastFoodCookingSlotOwner.Staff);
    }
    public Portion AtSlot(FastFoodStationMode station, int slot) => Portions.Find(p => Station(p.recipe) == station && p.slot == slot && p.stage != FastFoodCookingStage.Complete);
    public int FreeSlot(FastFoodStationMode station, bool player = false)
    { for (int i = 0; i < Capacity(station); i++) if (CanUseSlot(station, i, player) && AtSlot(station, i) == null) return i; return -1; }
    public bool PrepReady => Mode == FastFoodStationMode.Grill && PlayerWork.Any(p => p.player && p.stage == FastFoodCookingStage.Preparing) && PlayerWork.All(p => p.proteinReady || p.stage == FastFoodCookingStage.Complete);
    public Portion PlayerPortion => PrepReady ? PlayerWork.Find(p => p.player && p.stage == FastFoodCookingStage.Preparing) :
        PlayerWork.Find(p => p.player && p.stage == FastFoodCookingStage.Waiting && WorkStation(p) == Mode) ?? PlayerWork.Find(p => p.player && p.stage != FastFoodCookingStage.Complete);
    public Portion NextLoad(ItemData item) => PlayerWork.Find(p => p.player && p.stage == FastFoodCookingStage.Waiting && WorkStation(p) == Mode && Steps(p.recipe)[0].item == item);
    private bool PlayerControls(Portion p) => p.player && WorkStation(p) == Mode;
    public Ticket PlayerTicket => Mode == FastFoodStationMode.Assembler ? Tickets.Find(t => t.player && t.active && !t.submitted) : null;
    public bool CanAccept(IReadOnlyList<Recipe> products)
    {
        if (products == null || products.Count == 0 || products.Any(p => p == null || Steps(p).Count == 0)) return false;
        var free = new List<Portion>(Portions.Where(p => p.order < 0 && p.stage != FastFoodCookingStage.Burnt && !p.dragging));
        var needed = new Dictionary<ItemType, int>();
        foreach (var recipe in products)
        {
            var p = free.Find(x => x.recipe == recipe);
            if (p != null) { free.Remove(p); continue; }
            foreach (var cost in Requirements(recipe))
            { needed.TryGetValue(cost.Key, out int n); needed[cost.Key] = n + cost.Value; }
        }
        return needed.All(kv => Available(kv.Key) >= kv.Value);
    }
    public bool Accept(int order, IReadOnlyList<Recipe> products)
    {
        if (FindTicket(order) != null) return true;
        if (!CanAccept(products)) return false;
        var ticket = new Ticket { number = order };
        foreach (var recipe in products)
        {
            var p = Portions.Find(x => x.recipe == recipe && x.order < 0 && x.stage != FastFoodCookingStage.Burnt && !x.dragging);
            if (p == null) p = AddPortion(recipe);
            p.order = order;
            ticket.portions.Add(p);
        }
        Tickets.Add(ticket);
        Changed?.Invoke();
        return true;
    }
    private Portion AddPortion(Recipe recipe)
    {
        var p = new Portion { recipe = recipe };
        if (recipe.category == MenuProductCategory.Food)
        {
            var batch = Batches.LastOrDefault(b => b.recipe == recipe && !b.sealedTarget && b.target < 10);
            if (batch == null) { batch = new Batch { recipe = recipe }; Batches.Add(batch); }
            batch.target++;
            p.batch = batch;
        }
        Portions.Add(p);
        return p;
    }
    public void EnsureReserve(IEnumerable<Recipe> recipes, int target)
    {
        bool changed = false;
        foreach (var recipe in recipes.Where(r => r != null && r.category == MenuProductCategory.Food && Steps(r).Count > 0))
        {
            int count = Portions.Count(p => p.recipe == recipe && p.order < 0 && p.stage != FastFoodCookingStage.Burnt);
            while (count++ < target && Requirements(recipe).All(kv => Available(kv.Key) >= kv.Value)) { AddPortion(recipe); changed = true; }
        }
        if (changed) Changed?.Invoke();
    }
    public void Activate(int order)
    {
        var t = FindTicket(order);
        if (t == null || t.active) return;
        t.active = true;
        t.player = Mode == FastFoodStationMode.Assembler;
        Changed?.Invoke();
    }
    public void Enter(FastFoodStationMode mode)
    {
        foreach (var p in Portions) { p.player = false; p.dragging = false; }
        foreach (var t in Tickets) t.player = false;
        PlayerWork.Clear();
        staffFinishing.Clear();
        foreach (var p in Portions.Where(p => p.stage != FastFoodCookingStage.Waiting && p.stage != FastFoodCookingStage.Complete))
            staffFinishing.Add(p);
        completedBatchFor = 0;
        Mode = mode;
        // Offer one unstarted piece of work on entry; never steal a staff operation already underway.
        if (mode == FastFoodStationMode.Assembler)
        {
            foreach (var t in Tickets.Where(x => x.active && !x.submitted && !x.assemblyStarted && x.assemblyElapsed == 0 && x.portions.All(p => !p.placed)))
                t.player = true;
        }
        else OfferWaitingPortion(true);
        Changed?.Invoke();
    }
    private bool CanWork(Portion p) => p.order < 0 || FindTicket(p.order)?.active == true;
    private void ClaimStationQueue()
    {
        if (Mode != FastFoodStationMode.Grill && Mode != FastFoodStationMode.Fry) return;
        foreach (var p in Portions)
            if (CanWork(p) && !staffFinishing.Contains(p) &&
                (p.stage == FastFoodCookingStage.Waiting && (Station(p.recipe) == Mode || PreparationStation(p.recipe) == Mode) ||
                 p.stage == FastFoodCookingStage.Preparing && PreparationStation(p.recipe) == Mode))
                p.player = true;
    }
    private void OfferWaitingPortion(bool onEntry)
    {
        ClaimStationQueue();
        if (Mode != FastFoodStationMode.Grill && Mode != FastFoodStationMode.Fry || ShowingCompletedBatch || PlayerWork.Any(p => p.player && !StationWorkDone(p))) return;
        PlayerWork.Clear();
        completedBatchFor = 0;
        foreach (var p in Portions)
        {
            if (!CanWork(p) || !p.player || (p.stage != FastFoodCookingStage.Waiting && p.stage != FastFoodCookingStage.Preparing)) continue;
            foreach (var member in Portions.Where(x => x.recipe == p.recipe && x.player &&
                (x.stage == FastFoodCookingStage.Waiting || x.stage == FastFoodCookingStage.Preparing) && CanWork(x)).Take(Math.Max(1, playerBatchSize)))
            { member.player = true; member.assignedFor = 0; PlayerWork.Add(member); }
            return;
        }
    }
    public bool CanLoad(Portion p, ItemData item, int slot)
    {
        if (p == null || !PlayerControls(p) || item == null) return false;
        if (p.stage == FastFoodCookingStage.Waiting) return Steps(p.recipe)[0].item == item && CanUseSlot(Mode, slot, true) && AtSlot(Mode, slot) == null;
        var steps = AssemblySteps(p.recipe);
        return PrepReady && p.stage == FastFoodCookingStage.Preparing && p.ingredientStep < steps.Count && steps[p.ingredientStep].item == item;
    }
    public bool Load(Portion p, ItemData item, int slot = -1)
    {
        if (slot < 0) slot = FreeSlot(Mode, true);
        if (!CanLoad(p, item, slot)) return false;
        if (p.stage == FastFoodCookingStage.Waiting)
        { p.slot = slot; p.stage = FastFoodCookingStage.Cooking; p.elapsed = 0; Changed?.Invoke(); return true; }
        var steps = AssemblySteps(p.recipe);
        if (p.stage == FastFoodCookingStage.Preparing)
        {
            if (p.ingredientStep == steps.Count - 1) return Finish(p);
            p.ingredientStep++;
            Changed?.Invoke(); return true;
        }
        return false;
    }
    public bool Collect(Portion p)
    {
        if (p == null || !p.player || p.stage != FastFoodCookingStage.Ready) return false;
        return CollectCooked(p);
    }
    private bool CollectCooked(Portion p)
    {
        if (AssemblySteps(p.recipe).Count > 0)
        {
            p.stage = FastFoodCookingStage.Preparing; p.proteinReady = true; p.slot = -1; p.elapsed = 0; p.ingredientStep = 0;
            if (p.player && PreparationStation(p.recipe) != Mode) p.player = false;
            Changed?.Invoke(); return true;
        }
        return Finish(p);
    }
    public bool Discard(Portion p)
    {
        if (p == null || p.stage != FastFoodCookingStage.Burnt) return false;
        if (!Requirements(p.recipe).All(kv => Available(kv.Key) >= kv.Value)) return false;
        p.stage = FastFoodCookingStage.Waiting; p.elapsed = 0; p.ingredientStep = 0; p.slot = -1; p.proteinReady = false;
        Changed?.Invoke(); return true;
    }
    private bool Finish(Portion p)
    {
        if (p.stage == FastFoodCookingStage.Complete || p.stage == FastFoodCookingStage.Burnt) return false;
        var previous = p.stage;
        // Inventory raises availability events after its transaction. They must see the finished
        // serving and no longer subtract its reservation from already-debited raw quantities.
        p.stage = FastFoodCookingStage.Complete;
        try { if (!consume(p.recipe)) { p.stage = previous; return false; } }
        catch { p.stage = previous; throw; }
        p.player = false;
        p.slot = -1;
        if (p.batch != null) p.batch.completed++;
        Changed?.Invoke(); return true;
    }
    public bool BeginServingDrag(Portion p)
    {
        var t = p == null ? null : FindTicket(p.order);
        if (t == null || t != PlayerTicket || t.submitted || p.placed || p.dragging) return false;
        if (p.recipe.category != MenuProductCategory.Drink && p.stage != FastFoodCookingStage.Complete) return false;
        p.dragging = true; return true;
    }
    public bool Place(Portion p)
    {
        var t = p == null ? null : FindTicket(p.order);
        if (t == null || t != PlayerTicket || t.submitted || p.placed || !p.dragging) return false;
        p.dragging = false;
        if (p.recipe.category == MenuProductCategory.Drink && p.stage != FastFoodCookingStage.Complete && !Finish(p)) return false;
        if (p.stage != FastFoodCookingStage.Complete) return false;
        p.placed = true; Changed?.Invoke(); return true;
    }
    public bool Serve(int order)
    {
        var t = FindTicket(order);
        if (t == null || t.submitted || !t.Ready) return false;
        t.submitted = true; Changed?.Invoke(); return true;
    }
    public void Release(int order, bool delivered)
    {
        var t = FindTicket(order);
        if (t == null) return;
        foreach (var p in t.portions)
        {
            if (!delivered && p.stage == FastFoodCookingStage.Complete)
            { p.order = -1; p.placed = p.dragging = p.player = false; }
            else
            {
                if (p.stage != FastFoodCookingStage.Complete && p.batch != null) p.batch.target--;
                Portions.Remove(p);
                if (p.stage != FastFoodCookingStage.Complete) PlayerWork.Remove(p);
            }
        }
        Tickets.Remove(t); Changed?.Invoke();
    }
    public void Tick(float delta, bool holdNew, bool paused)
    {
        if (paused || delta <= 0) return;
        if (PlayerWork.Count > 0 && PlayerWork.All(StationWorkDone)) completedBatchFor += delta;
        foreach (var b in Batches) { b.sealedTarget = true; if (b.completed < b.target) b.elapsed += delta; }
        Batches.RemoveAll(b => b.completed == b.target && !Portions.Any(p => p.batch == b && p.stage != FastFoodCookingStage.Complete));
        foreach (var t in Tickets.Where(t => t.active && !t.submitted))
        { if (!t.player || t == PlayerTicket) t.elapsed += delta; }
        if (!holdNew) OfferWaitingPortion(false);
        foreach (var p in Portions.ToArray())
        {
            if (!CanWork(p) || p.recipe.category == MenuProductCategory.Drink || p.stage == FastFoodCookingStage.Complete) continue;
            if (p.player && PlayerWork.Contains(p)) p.assignedFor += delta;
            if (p.stage == FastFoodCookingStage.Burnt) { if (!p.player && !holdNew) Discard(p); continue; }
            if (p.stage == FastFoodCookingStage.Waiting)
            {
                if (PlayerControls(p) || holdNew || Station(p.recipe) == Mode) continue;
                var station = Station(p.recipe);
                int busy = Portions.Count(x => !PlayerControls(x) && Station(x.recipe) == station && x.stage == FastFoodCookingStage.Cooking);
                int free = FreeSlot(station);
                if (free < 0 || busy >= staffSlotsPerStation) continue;
                p.slot = free; p.stage = FastFoodCookingStage.Cooking; p.elapsed = 0;
            }
            if (p.stage == FastFoodCookingStage.Cooking)
            {
                p.elapsed += delta * (PlayerControls(p) ? 1 : staffMultiplier);
                if (p.elapsed >= cookSeconds) { p.stage = FastFoodCookingStage.Ready; p.elapsed = 0; }
            }
            else if (p.stage == FastFoodCookingStage.Ready)
            {
                // Sandwich proteins settle onto the holding tray, freeing the bay for the next wave.
                if (AssemblySteps(p.recipe).Count > 0 && Station(p.recipe) == FastFoodStationMode.Grill || !PlayerControls(p)) CollectCooked(p);
                else if ((p.elapsed += delta) > overcookSeconds) { p.stage = FastFoodCookingStage.Burnt; Changed?.Invoke(); }
            }
            else if (p.stage == FastFoodCookingStage.Preparing && !PlayerControls(p))
            { p.elapsed += delta * staffMultiplier; if (p.elapsed >= assemblySeconds) Finish(p); }
        }
        foreach (var t in Tickets.Where(t => t.active && !t.player && !t.submitted))
        {
            if (t.Ready) { Serve(t.number); continue; }
            var p = t.portions.Find(x => !x.placed && !x.dragging && (x.stage == FastFoodCookingStage.Complete || x.recipe.category == MenuProductCategory.Drink));
            if (p == null) continue;
            t.assemblyStarted = true;
            t.assemblyElapsed += delta * staffMultiplier;
            if (t.assemblyElapsed < assemblySeconds) continue;
            if (p.stage != FastFoodCookingStage.Complete && !Finish(p)) continue;
            p.placed = true; t.assemblyElapsed = 0;
            if (t.Ready) Serve(t.number);
        }
    }
    public void RestoreReady(Recipe recipe, int count)
    { for (int i = 0; i < count; i++) Portions.Add(new Portion { recipe = recipe, stage = FastFoodCookingStage.Complete }); }
}
