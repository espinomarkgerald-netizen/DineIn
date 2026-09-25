using System;
using System.Linq;
using UnityEditor;

public static class FastFoodBatchRegression
{
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static Recipe Recipe(ItemTypeKitchen type)=>MenuCatalog.Default.Products.First(r=>r.kitchenItemType==type);
    static void CookWave(FastFoodCookingState state,Recipe recipe,int count)
    {
        var item=FastFoodCookingState.Steps(recipe)[0].item;
        for(int i=0;i<count;i++)Require(state.Load(state.NextLoad(item),item,i),"Could not load bay "+i);
        var waiting=state.NextLoad(item);
        if(waiting!=null)Require(!state.Load(waiting,item,0),"Occupied bay accepted a second portion.");
        state.Tick(state.cookSeconds+.01f,false,false);state.Tick(.01f,false,false);
        if(state.Mode==FastFoodStationMode.Grill)
            foreach(var ready in state.PlayerWork.Where(p=>p.stage==FastFoodCookingStage.Ready).ToArray())
                Require(state.Collect(ready),"Tapped patty did not reach prep.");
    }
    static void AssembleAll(FastFoodCookingState state,Recipe recipe)
    {
        foreach(var portion in state.PlayerWork.ToArray())
            foreach(var step in FastFoodCookingState.AssemblySteps(recipe))Require(state.Load(portion,step.item),"Assembly failed at "+step.label);
    }
    [MenuItem("Dine In/Fast Food/Check Batch Cooking Regressions")]
    public static string Run()
    {
        int consumed=0;var burger=Recipe(ItemTypeKitchen.Burger);
        var state=new FastFoodCookingState(_=>1000,_=>{consumed++;return true;});
        state.EnsureReserve(new[]{burger},6);state.Enter(FastFoodStationMode.Grill);
        Require(state.PlayerWork.Count==6,"Six portion batch was not offered.");
        for(int wave=0;wave<3;wave++)
        {CookWave(state,burger,2);Require(consumed==0,"Cooked patties consumed the finished burger ingredients.");Require(state.PrepReady==(wave==2),"Prep became ready before the entire batch.");}
        AssembleAll(state,burger);Require(consumed==6,"Burger assembly did not consume exactly six recipes.");
        Require(!state.Load(state.PlayerWork[0],FastFoodCookingState.AssemblySteps(burger)[0].item),"Completed burger accepted duplicate input.");
        var fries=Recipe(ItemTypeKitchen.Fries);consumed=0;
        state=new FastFoodCookingState(_=>1000,_=>{consumed++;return true;});state.EnsureReserve(new[]{fries},4);state.Enter(FastFoodStationMode.Fry);
        CookWave(state,fries,4);Require(Enumerable.Range(0,4).All(i=>state.AtSlot(FastFoodStationMode.Fry,i)!=null),"Four fryer bays are not independent.");
        Require(consumed==0,"Fryer contents consumed before collection.");
        foreach(var portion in state.PlayerWork)Require(state.Collect(portion),"Could not collect cooked fries.");Require(consumed==4,"Fries consumed incorrectly.");
        foreach(var type in new[]{ItemTypeKitchen.ChickenSandwich,ItemTypeKitchen.FishFilletSandwich})
        {
            var recipe=Recipe(type);consumed=0;
            state=new FastFoodCookingState(_=>1000,_=>{consumed++;return true;});state.EnsureReserve(new[]{recipe},3);state.Enter(FastFoodStationMode.Grill);
            state.Tick(9,false,false);state.Tick(.01f,false,false);
            state.Tick(.01f,false,false); // The next update offers the newly handed-off prep work.
            Require(state.PrepReady&&consumed==0,"Fryer staff did not hand cooked protein to the player's grill prep.");
            AssembleAll(state,recipe);Require(consumed==3,"Sandwich consumed more than once across stations.");
            consumed=0;state=new FastFoodCookingState(_=>1000,_=>{consumed++;return true;});state.EnsureReserve(new[]{recipe},2);state.Enter(FastFoodStationMode.Fry);
            CookWave(state,recipe,2);Require(consumed==0,"Fryer handoff consumed sandwich ingredients.");
            foreach(var ready in state.PlayerWork.ToArray())Require(state.Collect(ready),"Fryer filling could not be staged on prep.");
            Require(state.PlayerWork.All(p=>p.proteinReady&&!p.player),"Fryer did not release cooked sandwich protein to grill.");
            state.Enter(FastFoodStationMode.None);for(int i=0;i<10;i++)state.Tick(1,false,false);
            Require(consumed==2&&state.Portions.All(p=>p.stage==FastFoodCookingStage.Complete),"Staff could not finish after station exit.");
        }
        return "PASS: six burger batch, two grill bays, four fryer bays, both sandwich handoffs, exact consumption and staff continuation.";
    }
}
