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
        public bool proteinReady, needsRestock;
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
    private readonly Dictionary<FastFoodStationMode, List<Portion>> stationWork = new();
    private readonly Dictionary<FastFoodStationMode, float> stationAway = new();
    private readonly Dictionary<FastFoodStationMode, float> completionTime = new();
    private readonly HashSet<FastFoodStationMode> playerStations = new();
    public List<Portion> PlayerWork => WorkFor(Mode);
    private List<Portion> WorkFor(FastFoodStationMode mode)
    {
        if (!stationWork.TryGetValue(mode, out var work)) stationWork[mode] = work = new List<Portion>();
        return work;
    }
    public float stationGraceSeconds = 5;
    public bool FryerPrepPaused { get; set; }
    public int playerBatchSize = 8, grillSlots = 8, frySlots = 8;
    public int fryRackCapacity = 8;
    public static bool OnFryRack(Portion p) => Station(p.recipe) == FastFoodStationMode.Fry && !p.placed &&
        (p.stage == FastFoodCookingStage.Complete && PreparationStation(p.recipe) == FastFoodStationMode.Fry ||
         p.stage == FastFoodCookingStage.Preparing && p.proteinReady && p.ingredientStep == 0);
    public int FryRackCount => Portions.Count(OnFryRack);
    public bool BasketRaised(int firstSlot)
    {
        if (firstSlot < 0 || firstSlot % 2 != 0 || firstSlot >= frySlots) return false;
        var contents = Portions.Where(p => Station(p.recipe) == FastFoodStationMode.Fry && p.slot >= firstSlot && p.slot < firstSlot + 2).ToArray();
        return contents.Length > 0 && contents.All(p => p.stage == FastFoodCookingStage.Ready || p.stage == FastFoodCookingStage.Burnt);
    }
    public int CollectBasket(int firstSlot)
    {
        if (Mode != FastFoodStationMode.Fry || !BasketRaised(firstSlot)) return 0;
        var ready = Portions.Where(p => p.player && Station(p.recipe) == FastFoodStationMode.Fry &&
            p.slot >= firstSlot && p.slot < firstSlot + 2 && p.stage == FastFoodCookingStage.Ready).ToArray();
        if (FryRackCount + ready.Length > fryRackCapacity) return 0;
        int collected = 0;
        foreach (var portion in ready) if (Collect(portion)) collected++;
        return collected;
    }
    public FastFoodStationMode Mode { get; private set; }
    public int playerEvery = 10, staffSlotsPerStation = 4;
    public float cookSeconds = 8, overcookSeconds = 8, staffMultiplier = 2, assemblySeconds = 2, deadlineSeconds = 300;
    public float completionHoldSeconds = .9f;
    private float completedBatchFor
    {
        get => completionTime.TryGetValue(Mode, out var value) ? value : 0;
        set => completionTime[Mode] = value;
    }
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
    private static bool Reserved(Portion p) => !p.needsRestock && p.stage != FastFoodCookingStage.Complete && p.stage != FastFoodCookingStage.Burnt;
    public int Available(ItemType item)
    {
        int n = stock(item);
        foreach (var p in Portions.Where(Reserved))
            if (Requirements(p.recipe).TryGetValue(item, out int used)) n -= used;
        return Math.Max(0, n);
    }
    public int ReadyStock(Recipe recipe) => Portions.Count(p => p.recipe == recipe && p.order < 0 && p.stage == FastFoodCookingStage.Complete && !p.dragging);
    // A view of reserved portions, never a second inventory or a stock credit.
    public static bool HasStoredProtein(Portion p)
    {
        if (p == null || !p.proteinReady || p.stage != FastFoodCookingStage.Preparing) return false;
        var protein = Steps(p.recipe).FirstOrDefault()?.item;
        int step = AssemblySteps(p.recipe).FindIndex(s => s.item == protein);
        return step < 0 || p.ingredientStep <= step;
    }
    public int StoredProteinCount(Recipe recipe) => WorkFor(FastFoodStationMode.Grill)
        .Count(p => p.player && p.recipe == recipe && HasStoredProtein(p));
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
        // Food reserves its actual slot; equipment has no permanently assigned side.
        if (station == FastFoodStationMode.Fry)
            for (int i = slot / 2 * 2; i < slot / 2 * 2 + 2; i++)
            {
                var food = AtSlot(station, i);
                if (food != null && (food.stage == FastFoodCookingStage.Ready || food.stage == FastFoodCookingStage.Burnt)) return false;
            }
        return true;
    }
    public Portion AtSlot(FastFoodStationMode station, int slot) => Portions.Find(p => Station(p.recipe) == station && p.slot == slot && p.stage != FastFoodCookingStage.Complete);
    public int FreeSlot(FastFoodStationMode station, bool player = false)
    { for (int i = 0; i < Capacity(station); i++) if (CanUseSlot(station, i, player) && AtSlot(station, i) == null) return i; return -1; }
    public bool PrepReady => Mode == FastFoodStationMode.Grill && PlayerWork.Any(p => p.player && p.stage == FastFoodCookingStage.Preparing) && PlayerWork.All(p => p.proteinReady || p.stage == FastFoodCookingStage.Complete);
    public Portion PlayerPortion => PrepReady ? PlayerWork.Find(p => p.player && p.stage == FastFoodCookingStage.Preparing) :
        PlayerWork.Find(p => p.player && p.stage == FastFoodCookingStage.Waiting && WorkStation(p) == Mode) ?? PlayerWork.Find(p => p.player && p.stage != FastFoodCookingStage.Complete);
    public Portion NextLoad(ItemData item) => PlayerWork.Find(p => p.player && p.stage == FastFoodCookingStage.Waiting && WorkStation(p) == Mode && Steps(p.recipe)[0].item == item);
    private bool PlayerControls(Portion p) => p.player && playerStations.Contains(WorkStation(p));
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
        t.player = playerStations.Contains(FastFoodStationMode.Assembler);
        Changed?.Invoke();
    }
    public void Enter(FastFoodStationMode mode)
    {
        if (mode == FastFoodStationMode.None)
        {
            foreach (var owned in playerStations.ToArray()) HandOff(owned);
            FryerPrepPaused = false;
            Mode = mode;
            Changed?.Invoke();
            return;
        }
        if (Mode != mode && playerStations.Contains(Mode)) stationAway[Mode] = 0;
        bool returning = playerStations.Contains(mode);
        Mode = mode;
        FryerPrepPaused = false;
        playerStations.Add(mode);
        stationAway.Remove(mode);
        if (!returning)
            foreach (var p in Portions.Where(p => !p.player && p.stage != FastFoodCookingStage.Waiting && p.stage != FastFoodCookingStage.Complete))
                staffFinishing.Add(p);
        // Offer one unstarted piece of work on entry; never steal a staff operation already underway.
        if (mode == FastFoodStationMode.Assembler)
        {
            foreach (var t in Tickets.Where(x => x.active && !x.submitted && !x.assemblyStarted && x.assemblyElapsed == 0 && x.portions.All(p => !p.placed)))
                t.player = true;
        }
        else OfferWaitingPortion(true);
        Changed?.Invoke();
    }
    public void SelectStations()
    {
        if (playerStations.Contains(Mode)) stationAway[Mode] = 0;
        Mode = FastFoodStationMode.None;
        FryerPrepPaused = false;
        Changed?.Invoke();
    }
    private void HandOff(FastFoodStationMode station)
    {
        foreach (var p in Portions.Where(p => p.player && WorkStation(p) == station))
        {
            p.player = p.dragging = false;
            if (p.stage != FastFoodCookingStage.Waiting && p.stage != FastFoodCookingStage.Complete) staffFinishing.Add(p);
        }
        if (station == FastFoodStationMode.Assembler)
            foreach (var ticket in Tickets) { ticket.player = false; foreach (var p in ticket.portions) p.dragging = false; }
        playerStations.Remove(station);
        stationAway.Remove(station);
        WorkFor(station).Clear();
        completionTime.Remove(station);
    }
    private bool CanWork(Portion p) => p.order < 0 || FindTicket(p.order)?.active == true;
    private void ClaimStationQueue()
    {
        if (Mode != FastFoodStationMode.Grill && Mode != FastFoodStationMode.Fry) return;
        foreach (var p in Portions)
            if (CanWork(p) && !staffFinishing.Contains(p) &&
                (p.stage == FastFoodCookingStage.Waiting && Station(p.recipe) == Mode ||
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
            if (!CanWork(p) || !p.player || WorkStation(p) != Mode || (p.stage != FastFoodCookingStage.Waiting && p.stage != FastFoodCookingStage.Preparing)) continue;
            foreach (var member in Portions.Where(x => x.recipe == p.recipe && x.player && WorkStation(x) == Mode &&
                (x.stage == FastFoodCookingStage.Waiting || x.stage == FastFoodCookingStage.Preparing) && CanWork(x)).Take(Math.Max(1, playerBatchSize)))
            { member.player = true; member.assignedFor = 0; PlayerWork.Add(member); }
            return;
        }
    }
    public bool CanLoad(Portion p, ItemData item, int slot)
    {
        if (p == null || !PlayerControls(p) || WorkStation(p) != Mode || item == null) return false;
        if (p.needsRestock && !Requirements(p.recipe).All(kv => Available(kv.Key) >= kv.Value)) return false;
        if (p.stage == FastFoodCookingStage.Waiting) return Steps(p.recipe)[0].item == item && CanUseSlot(Mode, slot, true) && AtSlot(Mode, slot) == null;
        var steps = AssemblySteps(p.recipe);
        return PrepReady && p.stage == FastFoodCookingStage.Preparing && p.ingredientStep < steps.Count && steps[p.ingredientStep].item == item;
    }
    public bool Load(Portion p, ItemData item, int slot = -1)
    {
        if (slot < 0) slot = FreeSlot(Mode, true);
        if (!CanLoad(p, item, slot)) return false;
        if (p.stage == FastFoodCookingStage.Waiting)
        { p.needsRestock = false; p.slot = slot; p.stage = FastFoodCookingStage.Cooking; p.elapsed = 0; Changed?.Invoke(); return true; }
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
        if (p == null || !Portions.Contains(p) || !PlayerControls(p) || WorkStation(p) != Mode || p.stage != FastFoodCookingStage.Ready) return false;
        if (Mode == FastFoodStationMode.Fry && !BasketRaised(p.slot / 2 * 2)) return false;
        return CollectCooked(p);
    }
    private bool CollectCooked(Portion p)
    {
        if (Station(p.recipe) == FastFoodStationMode.Fry && FryRackCount >= fryRackCapacity) return false;
        if (AssemblySteps(p.recipe).Count > 0)
        {
            p.stage = FastFoodCookingStage.Preparing; p.proteinReady = true; p.slot = -1; p.elapsed = 0; p.ingredientStep = 0;
            if (PreparationStation(p.recipe) != Station(p.recipe))
            {
                // Frying is finished. The next operation belongs to the prep station's owner.
                staffFinishing.Remove(p);
                p.player = playerStations.Contains(PreparationStation(p.recipe));
            }
            Changed?.Invoke(); return true;
        }
        return Finish(p);
    }
    public bool Discard(Portion p)
    {
        if (p == null || p.stage != FastFoodCookingStage.Burnt) return false;
        p.needsRestock = !Requirements(p.recipe).All(kv => Available(kv.Key) >= kv.Value);
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
        staffFinishing.Remove(p);
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
                foreach (var work in stationWork.Values) work.Remove(p);
                staffFinishing.Remove(p);
            }
        }
        Tickets.Remove(t); Changed?.Invoke();
    }
    public void Tick(float delta, bool holdNew, bool paused)
    {
        if (paused || delta <= 0) return;
        foreach (var station in stationAway.Keys.ToArray())
        {
            stationAway[station] += delta;
            if (stationAway[station] >= stationGraceSeconds) { HandOff(station); Changed?.Invoke(); }
        }
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
                if (p.needsRestock)
                {
                    if (!Requirements(p.recipe).All(kv => Available(kv.Key) >= kv.Value)) continue;
                    p.needsRestock = false;
                }
                if (PlayerControls(p) || holdNew || playerStations.Contains(Station(p.recipe))) continue;
                var station = Station(p.recipe);
                int busy = Portions.Count(x => !PlayerControls(x) && Station(x.recipe) == station && x.stage == FastFoodCookingStage.Cooking);
                int free = FreeSlot(station);
                if (free < 0 || busy >= staffSlotsPerStation) continue;
                p.slot = free; p.stage = FastFoodCookingStage.Cooking; p.elapsed = 0;
                staffFinishing.Add(p);
            }
            if (FryerPrepPaused && Station(p.recipe) == FastFoodStationMode.Fry &&
                (p.stage == FastFoodCookingStage.Cooking || p.stage == FastFoodCookingStage.Ready)) continue;
            if (p.stage == FastFoodCookingStage.Cooking)
            {
                p.elapsed += delta * (PlayerControls(p) ? 1 : staffMultiplier);
                if (p.elapsed >= cookSeconds) { p.stage = FastFoodCookingStage.Ready; p.elapsed = 0; }
            }
            else if (p.stage == FastFoodCookingStage.Ready)
            {
                if (!PlayerControls(p)) CollectCooked(p);
                // Fryers stop heating finished food and lift when both occupied positions are ready.
                // Grill food still needs an individual tap before its overcook window expires.
                else if (Station(p.recipe) != FastFoodStationMode.Fry && (p.elapsed += delta) > overcookSeconds)
                { p.stage = FastFoodCookingStage.Burnt; Changed?.Invoke(); }
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
