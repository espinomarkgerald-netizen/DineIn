using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>Focused ledger and saved-content checks. Never starts or saves a campaign.</summary>
public static class FastFoodCozyGameplayRegression
{
    static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("[Cozy kitchen] " + message); }

    static FastFoodCookingState State(Func<Recipe, bool> consume)
    {
        var state = new FastFoodCookingState(_ => 1000, consume);
        var roles = Enumerable.Repeat(FastFoodCookingSlotOwner.Staff, 4).Concat(Enumerable.Repeat(FastFoodCookingSlotOwner.Player, 4)).ToArray();
        state.ConfigureSlots(FastFoodStationMode.Grill, roles); state.ConfigureSlots(FastFoodStationMode.Fry, roles);
        return state;
    }
    static Recipe Recipe(ItemTypeKitchen type) => MenuCatalog.Default.Products.First(r => r.kitchenItemType == type);
    static void FillPlayer(FastFoodCookingState state, Recipe recipe)
    {
        var ingredient = FastFoodCookingState.Steps(recipe)[0].item;
        for (int i = 4; i < 8; i++)
        {
            var next = state.NextLoad(ingredient); if (next == null) break;
            Require(state.Load(next, ingredient, i), "Player position " + i + " rejected a valid input.");
        }
    }
    static void Assemble(FastFoodCookingState state, Recipe recipe)
    {
        foreach (var portion in state.PlayerWork.ToArray())
            foreach (var step in FastFoodCookingState.AssemblySteps(recipe))
                Require(state.Load(portion, step.item), "Assembly rejected " + step.label);
    }

    [MenuItem("Dine In/Fast Food/Check Cozy Gameplay Regressions")]
    public static string Run()
    {
        Require(!Application.isPlaying, "Run outside Play Mode.");
        var burger = Recipe(ItemTypeKitchen.Burger); var fries = Recipe(ItemTypeKitchen.Fries);
        var state = State(_ => true); state.EnsureReserve(new[]{burger, fries}, 10); state.Enter(FastFoodStationMode.Grill);
        var meat = FastFoodCookingState.Steps(burger)[0].item;
        Require(!state.Load(state.PlayerPortion, meat, 0), "Player could use an empty staff grill position.");
        FillPlayer(state, burger); state.Tick(.1f, false, false);
        Require(Enumerable.Range(0,4).All(i => state.AtSlot(FastFoodStationMode.Grill,i) == null), "Staff started new work while the player occupied Grill.");
        Require(Enumerable.Range(4,4).All(i => state.AtSlot(FastFoodStationMode.Grill,i)?.player == true), "Staff took a player grill position.");
        Require(Enumerable.Range(0,4).All(i => state.AtSlot(FastFoodStationMode.Fry,i) != null) && Enumerable.Range(4,4).All(i => state.AtSlot(FastFoodStationMode.Fry,i) == null), "Staff fryer work escaped the two left wells.");
        Require(state.FreeSlot(FastFoodStationMode.Grill,true) == -1, "Player grill accepted more than four patties.");

        int consumed = 0;
        state = State(_ => { consumed++; return true; }); state.EnsureReserve(new[]{burger}, 6); state.Enter(FastFoodStationMode.Grill);
        FillPlayer(state,burger); state.Tick(9,true,false); state.Tick(.01f,true,false);
        Require(consumed == 0 && !state.PrepReady, "Partial batch consumed stock or opened prep early.");
        FillPlayer(state,burger); state.Tick(9,true,false); state.Tick(.01f,true,false);
        Require(state.PrepReady && consumed == 0, "Six-patty batch did not reach prep with reservations intact.");
        var portion = state.PlayerPortion; int before = state.Available(meat.itemType);
        Require(!state.Load(portion, meat), "Assembly accepted protein before the bottom bun.");
        Require(before == state.Available(meat.itemType) && consumed == 0, "Invalid assembly changed inventory.");
        Assemble(state,burger); Require(consumed == 6 && state.ShowingCompletedBatch, "Finished batch was consumed incorrectly or skipped its display.");
        Require(!state.Load(portion, meat) && consumed == 6, "Finished product consumed twice.");
        state.Tick(state.completionHoldSeconds + .1f,false,false); Require(!state.ShowingCompletedBatch, "Completed batch display never released.");

        consumed = 0; state = State(_ => { consumed++; return true; }); state.EnsureReserve(new[]{fries},6); state.Enter(FastFoodStationMode.Fry);
        FillPlayer(state,fries); Require(state.FreeSlot(FastFoodStationMode.Fry,true) == -1, "Player fryer capacity exceeds four.");
        state.Tick(9,true,false); state.Tick(.01f,true,false); Require(consumed == 0,"Fried portions consumed before collection.");
        foreach(var ready in state.PlayerWork.Where(p=>p.stage==FastFoodCookingStage.Ready).ToArray())Require(state.Collect(ready),"Ready food could not be collected.");
        Require(consumed==4,"Fryer collection did not commit four recipes.");
        state.Enter(FastFoodStationMode.None); for(int i=0;i<40;i++)state.Tick(.5f,false,false);
        Require(consumed==6&&state.Portions.All(p=>p.stage==FastFoodCookingStage.Complete),"Station exit stranded the remainder.");

        foreach(var type in new[]{ItemTypeKitchen.ChickenSandwich,ItemTypeKitchen.FishFilletSandwich})
        {
            var recipe=Recipe(type);consumed=0;state=State(_=>{consumed++;return true;});state.EnsureReserve(new[]{recipe},6);state.Enter(FastFoodStationMode.Grill);
            for(int i=0;i<80&&!state.PrepReady;i++)state.Tick(.25f,false,false);
            Require(state.PrepReady&&consumed==0,"Fryer-to-grill handoff failed for "+recipe.name);
            Assemble(state,recipe);Require(consumed==6,"Cross-station sandwich consumption was not exact.");
        }
        state=State(_=>true);Require(state.Accept(91,new[]{fries}),"Test order rejected.");state.Enter(FastFoodStationMode.Assembler);
        Require(state.PlayerTicket==null,"Unpaid/inactive order got a player ticket.");state.Activate(91);Require(state.PlayerTicket?.number==91,"Active assigned order has no ticket.");
        state.Enter(FastFoodStationMode.None);Require(state.PlayerTicket==null,"Ticket survived kitchen exit.");
        CheckAuthoredContent();
        return "PASS: occupied-station ownership, 4-patty/4-fryer player limits, six-item waves, invalid drops, deferred/exact consumption, completion display, both sandwich handoffs, station exit, assigned-only tickets, and saved scene/prefab references.";
    }

    static void CheckAuthoredContent()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach(var rig in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<FastFoodCookingStation>(true)).Where(r=>r.mode!=FastFoodStationMode.Assembler))
        {
            Require(rig.Slots.Length==8,rig.name+" has wrong authored capacity.");
            Require(rig.Slots.Count(s=>s.owner==FastFoodCookingSlotOwner.Player)==4&&rig.Slots.Count(s=>s.owner==FastFoodCookingSlotOwner.Staff)==4,"Saved ownership is not 4/4.");
            Require(rig.Slots.Select(s=>s.slotIndex).OrderBy(i=>i).SequenceEqual(Enumerable.Range(0,8)),"Duplicate or missing slot index.");
            Require(rig.Slots.All(s=>s.foodAnchor!=null&&s.timer!=null&&s.hint!=null&&s.cookingFeedback!=null&&s.GetComponent<Collider>().enabled),"An authored slot is missing presentation/input.");
            Require(rig.Slots.Where(s=>s.owner==FastFoodCookingSlotOwner.Staff).Max(s=>s.transform.position.x)<rig.Slots.Where(s=>s.owner==FastFoodCookingSlotOwner.Player).Min(s=>s.transform.position.x),"Staff and player sides overlap.");
            Require(rig.cookingAudio!=null&&rig.cookingAudio.clip!=null&&rig.cookingAudio.outputAudioMixerGroup!=null,"Station audio is not saved/routed.");
        }
        foreach(var recipe in MenuCatalog.Default.Products.Where(r=>r.category==MenuProductCategory.Drink))
            Require(recipe.kitchenServingPrefab!=null&&recipe.kitchenPreviewPrefab==recipe.kitchenServingPrefab&&recipe.kitchenServingPrefab.GetComponentsInChildren<MeshRenderer>().Length>0,"Missing saved drink glass: "+recipe.name);
        Require(HygieneSettings.Current.maxKitchenDirtCyclesPerDay==2,"Kitchen dirt limit changed.");
        var ticket=AssetDatabase.LoadAssetAtPath<GameObject>(FastFoodCookingAuthoring.TemplateFolder+"/Order Ticket.prefab");
        Require(ticket.GetComponent<UIRevealAnimation>()!=null,"Ticket reveal is runtime-only or missing.");
        FastFoodKitchenFixRegression.CheckTicket();
    }
}
