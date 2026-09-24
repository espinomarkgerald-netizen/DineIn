using System;
using System.Collections.Generic;
using System.Linq;

public enum FastFoodStationMode { None, Grill, Fry, Assembler }
public enum FastFoodCookingStage { Waiting, Cooking, Ready, Preparing, Burnt, Complete }

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
    }
    public sealed class Ticket
    {
        public int number;
        public bool player, submitted, active;
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
    public FastFoodStationMode Mode { get; private set; }
    public int playerEvery = 10, staffSlotsPerStation = 4;
    public float cookSeconds = 8, overcookSeconds = 8, staffMultiplier = 2, assemblySeconds = 2, deadlineSeconds = 300;
    private int productionSequence, ticketSequence;
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
    public Portion PlayerPortion => Portions.Find(p => p.player && Station(p.recipe) == Mode && p.stage != FastFoodCookingStage.Complete);
    public Ticket PlayerTicket => Tickets.Find(t => t.player && !t.submitted);
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
        t.player = Mode == FastFoodStationMode.Assembler && ticketSequence++ % Math.Max(1, playerEvery) == 0 && PlayerTicket == null;
        Changed?.Invoke();
    }
    public void Enter(FastFoodStationMode mode)
    {
        foreach (var p in Portions) { p.player = false; p.dragging = false; }
        foreach (var t in Tickets) t.player = false;
        Mode = mode;
        // Offer one unstarted piece of work on entry; never steal a staff operation already underway.
        if (mode == FastFoodStationMode.Assembler)
        {
            var t = Tickets.Find(x => x.active && !x.submitted && x.assemblyElapsed == 0 && x.portions.All(p => !p.placed));
            if (t != null) t.player = true;
        }
        else OfferWaitingPortion(true);
        Changed?.Invoke();
    }
    private bool CanWork(Portion p) => p.order < 0 || FindTicket(p.order)?.active == true;
    private void OfferWaitingPortion(bool onEntry)
    {
        if (Mode != FastFoodStationMode.Grill && Mode != FastFoodStationMode.Fry || PlayerPortion != null) return;
        foreach (var p in Portions)
        {
            if (!CanWork(p) || Station(p.recipe) != Mode || p.stage != FastFoodCookingStage.Waiting) continue;
            if (!onEntry && productionSequence++ % Math.Max(1, playerEvery) != 0) continue;
            p.player = true; p.assignedFor = 0; return;
        }
    }
    public bool Load(Portion p, ItemData item)
    {
        if (p == null || !p.player || Station(p.recipe) != Mode || item == null) return false;
        var steps = Steps(p.recipe);
        if (p.stage == FastFoodCookingStage.Waiting && steps[0].item == item)
        { p.stage = FastFoodCookingStage.Cooking; p.elapsed = 0; Changed?.Invoke(); return true; }
        if (p.stage == FastFoodCookingStage.Preparing && p.ingredientStep < steps.Count && steps[p.ingredientStep].item == item)
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
        if (Station(p.recipe) == FastFoodStationMode.Grill && Steps(p.recipe).Count > 1)
        { p.stage = FastFoodCookingStage.Preparing; p.ingredientStep = 1; Changed?.Invoke(); return true; }
        return Finish(p);
    }
    public bool Discard(Portion p)
    {
        if (p == null || p.stage != FastFoodCookingStage.Burnt) return false;
        if (!Requirements(p.recipe).All(kv => Available(kv.Key) >= kv.Value)) return false;
        p.stage = FastFoodCookingStage.Waiting; p.elapsed = 0; p.ingredientStep = 0;
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
        if (p.batch != null) p.batch.completed++;
        Changed?.Invoke(); return true;
    }
    public bool BeginServingDrag(Portion p)
    {
        var t = p == null ? null : FindTicket(p.order);
        if (t == null || !t.player || t.submitted || p.placed || p.dragging) return false;
        if (p.recipe.category != MenuProductCategory.Drink && p.stage != FastFoodCookingStage.Complete) return false;
        p.dragging = true; return true;
    }
    public bool Place(Portion p)
    {
        var t = p == null ? null : FindTicket(p.order);
        if (t == null || !t.player || t.submitted || p.placed || !p.dragging) return false;
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
            }
        }
        Tickets.Remove(t); Changed?.Invoke();
    }
    public void Tick(float delta, bool holdNew, bool paused)
    {
        if (paused || delta <= 0) return;
        foreach (var b in Batches) { b.sealedTarget = true; if (b.completed < b.target) b.elapsed += delta; }
        Batches.RemoveAll(b => b.completed == b.target && !Portions.Any(p => p.batch == b && p.stage != FastFoodCookingStage.Complete));
        foreach (var t in Tickets.Where(t => t.active && !t.submitted))
        { t.elapsed += delta; if (t.elapsed > deadlineSeconds) t.player = false; }
        if (!holdNew) OfferWaitingPortion(false);
        foreach (var p in Portions.ToArray())
        {
            if (!CanWork(p) || p.recipe.category == MenuProductCategory.Drink || p.stage == FastFoodCookingStage.Complete) continue;
            if (p.player && (p.assignedFor += delta) >= deadlineSeconds) p.player = false;
            if (p.stage == FastFoodCookingStage.Burnt) { if (!p.player && !holdNew) Discard(p); continue; }
            if (p.stage == FastFoodCookingStage.Waiting)
            {
                if (p.player || holdNew) continue;
                int busy = Portions.Count(x => !x.player && Station(x.recipe) == Station(p.recipe) && x.stage == FastFoodCookingStage.Cooking);
                if (busy >= staffSlotsPerStation) continue;
                p.stage = FastFoodCookingStage.Cooking; p.elapsed = 0;
            }
            if (p.stage == FastFoodCookingStage.Cooking)
            {
                p.elapsed += delta * (p.player ? 1 : staffMultiplier);
                if (p.elapsed >= cookSeconds) { p.stage = FastFoodCookingStage.Ready; p.elapsed = 0; }
            }
            else if (p.stage == FastFoodCookingStage.Ready)
            {
                if (!p.player) Finish(p);
                else if ((p.elapsed += delta) > overcookSeconds) { p.stage = FastFoodCookingStage.Burnt; Changed?.Invoke(); }
            }
            else if (p.stage == FastFoodCookingStage.Preparing && !p.player) Finish(p);
        }
        foreach (var t in Tickets.Where(t => t.active && !t.player && !t.submitted))
        {
            if (t.Ready) { Serve(t.number); continue; }
            var p = t.portions.Find(x => !x.placed && !x.dragging && (x.stage == FastFoodCookingStage.Complete || x.recipe.category == MenuProductCategory.Drink));
            if (p == null) continue;
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
