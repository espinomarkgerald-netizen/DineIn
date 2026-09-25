using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>Isolated ledger and saved-content checks. Never starts or saves a campaign.</summary>
public static class FastFoodCozyGameplayRegression
{
    static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("[Cozy kitchen] " + message); }
    static FastFoodCookingState State(Func<Recipe, bool> consume)
    {
        var state = new FastFoodCookingState(_ => 1000, consume);
        var roles = Enumerable.Repeat(FastFoodCookingSlotOwner.Player, 8).ToArray();
        state.ConfigureSlots(FastFoodStationMode.Grill, roles); state.ConfigureSlots(FastFoodStationMode.Fry, roles);
        return state;
    }
    static Recipe Recipe(ItemTypeKitchen type) => MenuCatalog.Default.Products.First(r => r.kitchenItemType == type);
    static void FillPlayer(FastFoodCookingState state, Recipe recipe)
    {
        var ingredient = FastFoodCookingState.Steps(recipe)[0].item;
        for (int i = 0; i < 8; i++)
        {
            var next = state.NextLoad(ingredient); if (next == null) break;
            Require(state.Load(next, ingredient, i), "Position " + i + " rejected a valid input.");
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
        int consumed = 0;
        var state = State(recipe => { if(recipe==burger)consumed++; return true; });
        state.EnsureReserve(new[]{burger, fries}, 8); state.Enter(FastFoodStationMode.Grill); FillPlayer(state, burger);
        Require(Enumerable.Range(0,8).All(i => state.AtSlot(FastFoodStationMode.Grill,i)?.player == true), "Not all grill positions are available.");
        Require(state.FreeSlot(FastFoodStationMode.Grill,true) == -1, "Grill exceeded eight patties.");
        var retained=state.PlayerWork.ToArray();
        state.Enter(FastFoodStationMode.Fry);FillPlayer(state,fries);
        Require(state.FreeSlot(FastFoodStationMode.Fry,true)==-1,"Fryers exceeded eight portions.");
        state.Tick(2,true,false);
        Require(retained.All(p=>p.player&&Math.Abs(p.elapsed-2)<.001f),"Switching accelerated or released player cooking before grace.");
        state.Enter(FastFoodStationMode.Grill);
        Require(state.PlayerWork.SequenceEqual(retained),"Returning after two seconds lost the grill batch.");
        state.Tick(6,true,false);state.Tick(.01f,true,false);
        Require(retained.All(p=>p.stage==FastFoodCookingStage.Ready),"Grill must wait for a player tap.");
        foreach(var ready in retained)Require(state.Collect(ready),"Tapped grill patty was not collected.");
        Require(state.PrepReady&&consumed==0,"Eight-patty prep was not reached without consuming stock.");
        var meat=FastFoodCookingState.Steps(burger)[0].item;
        var portion=state.PlayerPortion;int before=state.Available(meat.itemType);
        Require(!state.Load(portion,meat)&&before==state.Available(meat.itemType),"Invalid assembly changed stock.");
        Assemble(state,burger);
        Require(consumed==8&&state.ShowingCompletedBatch,"Burger assembly did not consume exactly eight recipes.");
        Require(!state.Load(portion,meat)&&consumed==8,"Finished item consumed twice.");
        state.Tick(state.completionHoldSeconds+.1f,true,false);
        Require(!state.ShowingCompletedBatch,"Completed batch never released.");

        state=State(_=>true);state.EnsureReserve(new[]{fries},8);state.Enter(FastFoodStationMode.Fry);FillPlayer(state,fries);
        var fry=state.PlayerWork[0];state.FryerPrepPaused=true;state.Tick(20,true,false);
        Require(fry.stage==FastFoodCookingStage.Cooking&&fry.elapsed==0,"Fryer cooked during prep view.");
        state.FryerPrepPaused=false;state.Tick(state.cookSeconds,true,false);state.FryerPrepPaused=true;state.Tick(20,true,false);
        Require(fry.stage==FastFoodCookingStage.Ready&&fry.elapsed==0,"Fryer overcooked during prep view.");
        state.FryerPrepPaused=false;state.Tick(state.overcookSeconds+.1f,true,false);
        Require(fry.stage==FastFoodCookingStage.Ready,"Lifted fryer food should stay safe until collected.");
        // Old saves can still contain burnt fryer food and must retain the discard path.
        fry.stage=FastFoodCookingStage.Burnt;
        Require(fry.stage==FastFoodCookingStage.Burnt&&state.Discard(fry)&&fry.slot==-1,"Discard did not free burnt slot.");
        Require(!state.Load(fry,FastFoodCookingState.Steps(fries)[0].item,0),"Raised basket accepted a fresh load before its ready food was collected.");
        Require(state.CollectBasket(0)==1,"Ready basket mate could not be collected after discarding burnt food.");
        Require(state.Load(fry,FastFoodCookingState.Steps(fries)[0].item,0),"Discarded position could not be reused.");

        state=State(_=>true);state.EnsureReserve(new[]{fries},8);state.Enter(FastFoodStationMode.Fry);FillPlayer(state,fries);
        fry=state.PlayerWork[0];state.Enter(FastFoodStationMode.Grill);state.Tick(4.9f,true,false);
        Require(fry.player,"Staff claimed a batch before five seconds.");state.Tick(.11f,true,false);
        Require(!fry.player,"Staff did not take over after five seconds.");state.Enter(FastFoodStationMode.Fry);
        Require(!fry.player,"Re-entry stole work staff already claimed.");state.Enter(FastFoodStationMode.None);
        Require(state.Portions.All(p=>!p.player),"Kitchen exit retained player ownership.");

        state=State(_=>true);state.EnsureReserve(new[]{fries},8);state.Tick(.25f,false,false);
        var started=state.Portions.Where(p=>p.stage==FastFoodCookingStage.Cooking).ToArray();state.Enter(FastFoodStationMode.Fry);
        Require(started.Length>0&&started.All(p=>!p.player),"Entry stole existing staff work.");
        Require(state.PlayerWork.Count>0&&state.PlayerWork.All(p=>p.stage==FastFoodCookingStage.Waiting),"Unstarted work was not offered to the player.");

        foreach(var type in new[]{ItemTypeKitchen.ChickenSandwich,ItemTypeKitchen.FishFilletSandwich})
        {
            var recipe=Recipe(type);consumed=0;state=State(_=>{consumed++;return true;});state.EnsureReserve(new[]{recipe},4);state.Enter(FastFoodStationMode.Grill);
            for(int i=0;i<80&&!state.PrepReady;i++)state.Tick(.25f,false,false);
            Require(state.PrepReady&&consumed==0,"Fryer-to-grill handoff failed for "+recipe.name);
            Assemble(state,recipe);Require(consumed==4,"Cross-station sandwich consumption was not exact.");
        }
        state=State(_=>true);Require(state.Accept(91,new[]{fries}),"Test order rejected.");state.Enter(FastFoodStationMode.Assembler);
        Require(state.PlayerTicket==null,"Inactive order got a player ticket.");state.Activate(91);Require(state.PlayerTicket?.number==91,"Active order has no ticket.");
        var ticket=state.PlayerTicket;state.Enter(FastFoodStationMode.Grill);state.Tick(2,true,false);state.Enter(FastFoodStationMode.Assembler);
        Require(state.PlayerTicket==ticket,"Brief station switch lost the assembler ticket.");
        state.Enter(FastFoodStationMode.None);Require(state.PlayerTicket==null&&!ticket.player,"Complaint/kitchen exit retained a player ticket.");
        state.Enter(FastFoodStationMode.Assembler);Require(state.PlayerTicket==ticket,"Returning did not reacquire the unstarted ticket.");
        state.Release(91,false);Require(state.PlayerTicket==null,"Released order left a stale ticket.");
        state.Enter(FastFoodStationMode.Grill);state.Accept(92,new[]{fries});state.Activate(92);
        Require(state.FindTicket(92).player,"New ticket bypassed assembler grace ownership.");
        CheckAuthoredContent();
        return "PASS: 8 grill + 8 fryer positions, two-second return, five-second handoff, staff completion protection, fryer prep pause/resume, burnt discard/reload, exact consumption, sandwich handoffs, assembler exit/re-entry/release, saved references and ticket layout.";
    }
    static void CheckAuthoredContent()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach(var rig in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<FastFoodCookingStation>(true)).Where(r=>r.mode!=FastFoodStationMode.Assembler))
        {
            Require(rig.Slots.Length==8&&rig.Slots.All(s=>s.owner==FastFoodCookingSlotOwner.Player),rig.name+" is not eight shared positions.");
            Require(rig.Slots.Select(s=>s.slotIndex).OrderBy(i=>i).SequenceEqual(Enumerable.Range(0,8)),"Duplicate or missing slot index.");
            Require(rig.Slots.All(s=>s.foodAnchor!=null&&s.timer!=null&&s.hint!=null&&s.cookingFeedback!=null&&s.GetComponent<Collider>().enabled),"Saved slot is missing presentation/input.");
            Require(rig.completedFoodAnchors.Length>=8,"Eight servings cannot fit the authored prep staging.");
            Require(rig.cookingAudio!=null&&rig.cookingAudio.clip!=null&&rig.cookingAudio.outputAudioMixerGroup!=null,"Station audio is not saved/routed.");
        }
        Require(HygieneSettings.Current.maxKitchenDirtCyclesPerDay==2,"Kitchen dirt limit changed.");
        FastFoodKitchenFixRegression.CheckTicket();
    }
}
