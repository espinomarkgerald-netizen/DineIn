using System;
using System.Collections.Generic;
using System.Linq;

// Exact, non-transactional replication of the existing kitchen ledger. Presentation
// selection and drag previews remain local; ingredients are consumed only by Finish.
public sealed partial class FastFoodCookingState
{
    public bool Networked { get; set; }
    public Action<FastFoodStationMode> NetworkStationChanged;
    private int nextPortionId;
    private readonly Dictionary<int, FastFoodStationMode> networkStations = new();
    public IEnumerable<int> NetworkParticipants => networkStations.Keys.ToArray();
    public Portion FindPortion(int id) => id > 0 ? Portions.Find(p => p.networkId == id) : null;

    public void SetParticipantStation(int actor, FastFoodStationMode mode)
    {
        if (!Networked || actor <= 0) return;
        if (networkStations.TryGetValue(actor, out var previous) && previous == mode) return;
        networkStations.Remove(actor);
        if (mode != FastFoodStationMode.None) networkStations[actor] = mode;
        foreach (var occupied in playerStations.ToArray())
            if (!networkStations.Values.Contains(occupied)) HandOff(occupied);
        foreach (var occupied in networkStations.Values.Distinct())
        {
            if (!playerStations.Add(occupied)) continue;
            foreach (var p in Portions.Where(p => !p.player && p.stage != FastFoodCookingStage.Waiting && p.stage != FastFoodCookingStage.Complete))
                staffFinishing.Add(p);
            if (occupied == FastFoodStationMode.Assembler)
                foreach (var ticket in Tickets.Where(t => t.active && !t.submitted && !t.assemblyStarted && t.assemblyElapsed == 0 && t.portions.All(p => !p.placed)))
                    ticket.player = true;
        }
        stationAway.Clear();
        RefreshNetworkWork();
        Changed?.Invoke();
    }
    public FastFoodStationMode ParticipantStation(int actor) => networkStations.TryGetValue(actor, out var mode) ? mode : FastFoodStationMode.None;
    private void RefreshNetworkWork()
    {
        var localMode = Mode;
        try
        {
            foreach (var occupied in playerStations.ToArray())
            {
                Mode = occupied;
                OfferWaitingPortion(false);
            }
        }
        finally { Mode = localMode; }
    }
    public bool CanPreviewServing(Portion p)
    {
        var ticket = p == null ? null : FindTicket(p.order);
        return ticket != null && ticket == PlayerTicket && !ticket.submitted && !p.placed && !p.dragging
            && (p.recipe.category == MenuProductCategory.Drink || p.stage == FastFoodCookingStage.Complete);
    }
    public static string PortionVersion(Portion p) => p == null ? string.Empty :
        p.networkId + ":" + p.order + ":" + (int)p.stage + ":" + p.ingredientStep + ":" + p.slot + ":" + p.prepSlot + ":" + p.placed;
    public string TicketVersion(int order)
    {
        var ticket = FindTicket(order);
        return ticket == null ? string.Empty : ticket.number + ":" + ticket.active + ":" + ticket.submitted + ":" + string.Join("|", ticket.portions.Select(PortionVersion));
    }
    public string BasketVersion(int slot) => string.Join("|", Portions.Where(p => Station(p.recipe) == FastFoodStationMode.Fry && p.slot >= slot && p.slot < slot + 2).Select(PortionVersion));

    // Called by the authority after validating sender, stage, resources and claims.
    // Temporarily selecting a work surface must never change the host's own camera.
    public bool ExecuteNetworkAction(string operation, FastFoodStationMode mode, int portionId, int itemType, int slot, int order)
    {
        var localMode = Mode;
        var localPrepFocus = PrepFocus;
        var p = FindPortion(portionId);
        try
        {
            Mode = mode;
            switch (operation)
            {
                case "Load":
                    var item = p == null ? null : Steps(p.recipe).Select(s => s.item)
                        .Concat(AssemblySteps(p.recipe).Select(s => s.item)).FirstOrDefault(i => i != null && (int)i.itemType == itemType);
                    return Load(p, item, slot);
                case "Collect": return Collect(p);
                case "Basket": return CollectBasket(slot) > 0;
                case "Discard": return p != null && PlayerControls(p) && WorkStation(p) == mode && Discard(p);
                case "Place":
                    if (p == null) return false;
                    p.dragging = false;
                    return BeginServingDrag(p) && Place(p);
                case "Serve": return PlayerTicket?.number == order && Serve(order);
                default: return false;
            }
        }
        finally { if (p != null) p.dragging = false; Mode = localMode; PrepFocus = localPrepFocus; }
    }

    [Serializable] public sealed class NetworkPortion
    {
        public int id, order, stage, step, prepSlot, slot, batch;
        public string product;
        public bool player, placed, proteinReady, needsRestock, staffFinishing;
        public float elapsed, assignedFor;
    }
    [Serializable] public sealed class NetworkTicket
    {
        public int number;
        public bool player, submitted, active, assemblyStarted;
        public float elapsed, assemblyElapsed;
        public int[] portions;
    }
    [Serializable] public sealed class NetworkBatch
    { public string product; public int target, completed; public float elapsed; public bool sealedTarget; }
    [Serializable] public sealed class NetworkStation
    { public int mode; public int[] portions; public float completed; }
    [Serializable] public sealed class NetworkParticipant
    { public int actor, mode; }
    [Serializable] public sealed class NetworkSnapshot
    {
        public int nextId;
        public float cookSeconds;
        public List<NetworkPortion> portions = new();
        public List<NetworkTicket> tickets = new();
        public List<NetworkBatch> batches = new();
        public List<NetworkStation> stations = new();
        public List<NetworkParticipant> participants = new();
    }
    public NetworkSnapshot CaptureNetworkState()
    {
        var result = new NetworkSnapshot { nextId = nextPortionId, cookSeconds = cookSeconds };
        // Completed batches can leave Batches while their finished servings remain.
        var batches = Batches.Concat(Portions.Select(p => p.batch).Where(b => b != null)).Distinct().ToList();
        foreach (var b in batches) result.batches.Add(new NetworkBatch { product = b.recipe.ProductId, target = b.target, completed = b.completed, elapsed = b.elapsed, sealedTarget = b.sealedTarget });
        foreach (var p in Portions) result.portions.Add(new NetworkPortion {
            id = p.networkId, product = p.recipe.ProductId, order = p.order, stage = (int)p.stage,
            step = p.ingredientStep, prepSlot = p.prepSlot, slot = p.slot, batch = p.batch != null ? batches.IndexOf(p.batch) : -1,
            player = p.player, placed = p.placed, proteinReady = p.proteinReady, needsRestock = p.needsRestock,
            elapsed = p.elapsed, assignedFor = p.assignedFor, staffFinishing = staffFinishing.Contains(p) });
        foreach (var t in Tickets) result.tickets.Add(new NetworkTicket { number = t.number, player = t.player, submitted = t.submitted,
            active = t.active, assemblyStarted = t.assemblyStarted, elapsed = t.elapsed, assemblyElapsed = t.assemblyElapsed,
            portions = t.portions.Select(p => p.networkId).ToArray() });
        foreach (var pair in stationWork) result.stations.Add(new NetworkStation { mode = (int)pair.Key,
            portions = pair.Value.Select(p => p.networkId).ToArray(), completed = completionTime.TryGetValue(pair.Key, out var completed) ? completed : 0 });
        foreach (var pair in networkStations) result.participants.Add(new NetworkParticipant { actor = pair.Key, mode = (int)pair.Value });
        return result;
    }
    public bool ApplyNetworkState(NetworkSnapshot snapshot)
    {
        if (snapshot?.portions == null || snapshot.tickets == null || snapshot.batches == null || snapshot.stations == null || snapshot.participants == null) return false;
        if (snapshot.portions.Any(p => p.id <= 0 || MenuCatalog.Default.FindProduct(p.product) == null || p.batch < -1 || p.batch >= snapshot.batches.Count)
            || snapshot.portions.Select(p => p.id).Distinct().Count() != snapshot.portions.Count
            || snapshot.batches.Any(b => MenuCatalog.Default.FindProduct(b.product) == null)
            || snapshot.tickets.Any(t => t.portions == null || t.portions.Any(id => !snapshot.portions.Any(p => p.id == id)))) return false;
        // Preserve object identities referenced by local drags, timers and hotbar entries.
        var old = Portions.ToDictionary(p => p.networkId);
        var oldTickets = Tickets.ToDictionary(t => t.number);
        Portions.Clear(); Tickets.Clear(); Batches.Clear(); staffFinishing.Clear();
        foreach (var b in snapshot.batches) Batches.Add(new Batch { recipe = MenuCatalog.Default.FindProduct(b.product), target = b.target, completed = b.completed, elapsed = b.elapsed, sealedTarget = b.sealedTarget });
        foreach (var value in snapshot.portions)
        {
            if (!old.TryGetValue(value.id, out var p)) p = new Portion();
            p.networkId = value.id; p.recipe = MenuCatalog.Default.FindProduct(value.product); p.order = value.order;
            p.stage = (FastFoodCookingStage)value.stage; p.ingredientStep = value.step; p.prepSlot = value.prepSlot; p.slot = value.slot;
            p.batch = value.batch >= 0 ? Batches[value.batch] : null; p.player = value.player; p.placed = value.placed;
            p.proteinReady = value.proteinReady; p.needsRestock = value.needsRestock; p.elapsed = value.elapsed; p.assignedFor = value.assignedFor;
            if (p.placed || p.stage != FastFoodCookingStage.Complete && p.recipe.category != MenuProductCategory.Drink) p.dragging = false;
            Portions.Add(p); if (value.staffFinishing) staffFinishing.Add(p);
        }
        foreach (var value in snapshot.tickets)
        {
            if (!oldTickets.TryGetValue(value.number, out var t)) t = new Ticket();
            t.number = value.number; t.player = value.player; t.submitted = value.submitted; t.active = value.active;
            t.assemblyStarted = value.assemblyStarted; t.elapsed = value.elapsed; t.assemblyElapsed = value.assemblyElapsed;
            t.portions.Clear(); foreach (var id in value.portions) t.portions.Add(FindPortion(id)); Tickets.Add(t);
        }
        stationWork.Clear(); completionTime.Clear(); playerStations.Clear(); networkStations.Clear(); stationAway.Clear();
        foreach (var value in snapshot.stations)
        {
            var mode = (FastFoodStationMode)value.mode;
            WorkFor(mode).AddRange((value.portions ?? Array.Empty<int>()).Select(FindPortion).Where(p => p != null));
            completionTime[mode] = value.completed;
        }
        foreach (var value in snapshot.participants)
        { networkStations[value.actor] = (FastFoodStationMode)value.mode; playerStations.Add((FastFoodStationMode)value.mode); }
        nextPortionId = Math.Max(snapshot.nextId, Portions.Select(p => p.networkId).DefaultIfEmpty(0).Max());
        cookSeconds = snapshot.cookSeconds;
        if (PrepFocus != null && !Portions.Contains(PrepFocus)) PrepFocus = null;
        Changed?.Invoke();
        return true;
    }
}
