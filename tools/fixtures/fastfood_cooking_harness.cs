using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine
{
    public class Object { }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { } }
}

// Minimal asset adapters. The production ledger below is compiled from the actual gameplay source.
public enum ItemType { Patty, Bun, Cheese, ChickenPatty, FrozenFishFillet, Potato, Syrup }
public enum ItemTypeKitchen { Burger, ChickenSandwich, FishFilletSandwich, Fries, Coke }
public enum MenuProductCategory { Food, Drink }
public class ItemData { public ItemType itemType; public string displayName => itemType.ToString(); }
public class RecipeIngredient { public ItemData item; public int amount; }
public class KitchenAssemblyStep { public ItemData item; public string label; }
public class Recipe
{
    public FastFoodStationMode cookingStation;
    public FastFoodStationMode preparationStation;
    public List<KitchenAssemblyStep> kitchenAssemblySteps = new();
    public ItemData firstCookingIngredient;
    public MenuProductCategory category;
    public ItemTypeKitchen kitchenItemType;
    public List<RecipeIngredient> ingredients = new();
}

public static class CookingHarness
{
    private sealed class Fixture
    {
        public readonly Dictionary<ItemType,int> stock = Enum.GetValues<ItemType>().ToDictionary(x=>x,x=>30);
        public readonly Recipe burger = Make(ItemTypeKitchen.Burger,ItemType.Bun,ItemType.Patty,ItemType.Cheese);
        public readonly Recipe fries = Make(ItemTypeKitchen.Fries,ItemType.Potato);
        public readonly Recipe drink = Make(ItemTypeKitchen.Coke,ItemType.Syrup);
        public readonly FastFoodCookingState state;
        public int consumes;
        public Fixture()
        {
            drink.category=MenuProductCategory.Drink;
            state=new FastFoodCookingState(i=>stock[i],r=>
            {
                var costs=FastFoodCookingState.Requirements(r);
                if(costs.Any(kv=>stock[kv.Key]<kv.Value))return false;
                foreach(var cost in costs)stock[cost.Key]-=cost.Value;
                consumes++;return true;
            });
        }
        public FastFoodCookingState.Portion PlayerBurger()
        { state.Accept(1,new[]{burger});state.Activate(1);state.Enter(FastFoodStationMode.Grill);return state.PlayerPortion; }
        public void Cook(FastFoodCookingState.Portion p)
        { Assert(state.Load(p,FastFoodCookingState.Steps(p.recipe)[0].item),"load");state.Tick(8,false,false); }
        public void FinishBurger(FastFoodCookingState.Portion p)
        { Cook(p);Assert(state.Collect(p),"collect");foreach(var s in FastFoodCookingState.Steps(burger).Skip(1))Assert(state.Load(p,s.item),"add ingredient"); }
    }
    private static Recipe Make(ItemTypeKitchen type,params ItemType[] items)=>new Recipe
    { kitchenItemType=type, ingredients=items.Select(i=>new RecipeIngredient{item=new ItemData{itemType=i},amount=1}).ToList() };
    private static void Assert(bool value,string message) { if(!value)throw new Exception(message); }
    private static void Test(string name,Action body) { body();Console.WriteLine("PASS "+name); }
    public static void Main()
    {
        Test("acceptance and raw placement reserve but do not consume",()=>
        {var f=new Fixture();var p=f.PlayerBurger();Assert(f.stock[ItemType.Patty]==30&&f.state.Available(ItemType.Patty)==29,"reservation");f.Cook(p);Assert(f.consumes==0,"cooked protein is not full burger");});
        Test("full burger consumes every ingredient exactly once",()=>
        {var f=new Fixture();var p=f.PlayerBurger();f.FinishBurger(p);Assert(f.consumes==1&&f.stock[ItemType.Patty]==29&&f.stock[ItemType.Bun]==29&&f.stock[ItemType.Cheese]==29,"costs");Assert(!f.state.Collect(p)&&!f.state.Load(p,f.burger.ingredients[0].item),"no second completion");});
        Test("wrong ingredient cannot progress or charge stock",()=>
        {var f=new Fixture();var p=f.PlayerBurger();Assert(!f.state.Load(p,f.fries.ingredients[0].item)&&f.consumes==0,"invalid load");});
        Test("uncollected grilled food burns and releases its reservation without consuming",()=>
        {var f=new Fixture();var p=f.PlayerBurger();f.Cook(p);f.state.Tick(9,false,false);Assert(p.stage==FastFoodCookingStage.Burnt&&f.consumes==0&&f.state.Available(ItemType.Patty)==30,"burn");Assert(f.state.Discard(p)&&f.state.Available(ItemType.Patty)==29,"retry reservation");});
        Test("last ingredients cannot be promised twice",()=>
        {var f=new Fixture();f.stock[ItemType.Patty]=1;Assert(f.state.Accept(1,new[]{f.burger}),"first");Assert(!f.state.Accept(2,new[]{f.burger})&&f.stock[ItemType.Patty]==1,"second rejected, no debit");});
        Test("duplicate order acceptance creates no duplicate portions",()=>
        {var f=new Fixture();f.state.Accept(1,new[]{f.burger});f.state.Accept(1,new[]{f.burger});Assert(f.state.Portions.Count==1,"duplicate");});
        Test("cancel unfinished order releases ingredients",()=>
        {var f=new Fixture();f.PlayerBurger();f.state.Release(1,false);Assert(f.state.Available(ItemType.Patty)==30&&f.consumes==0,"cancel");});
        Test("completed cancelled food becomes reusable stock",()=>
        {var f=new Fixture();var p=f.PlayerBurger();f.FinishBurger(p);f.state.Release(1,false);Assert(f.state.ReadyStock(f.burger)==1,"ready pool");Assert(f.state.Accept(2,new[]{f.burger})&&f.state.Available(ItemType.Patty)==29,"reassign no extra reservation");});
        Test("leaving cooking transfers timers and staff completes",()=>
        {var f=new Fixture();var p=f.PlayerBurger();f.state.Load(p,FastFoodCookingState.Steps(f.burger)[0].item);f.state.Tick(3,false,false);f.state.Enter(FastFoodStationMode.None);Assert(p.elapsed==3&&!p.player,"handoff");for(int i=0;i<20;i++)f.state.Tick(1,false,false);Assert(f.consumes==1&&f.state.FindTicket(1).submitted,"staff completes");});
        Test("staff runs all production and assembly without player",()=>
        {var f=new Fixture();f.state.Accept(1,new[]{f.burger,f.fries,f.drink});f.state.Activate(1);for(int i=0;i<20;i++)f.state.Tick(1,false,false);Assert(f.state.FindTicket(1).submitted&&f.consumes==3,"staff complete");});
        Test("unpaid/inactive order does not cook",()=>
        {var f=new Fixture();f.state.Accept(1,new[]{f.burger});for(int i=0;i<20;i++)f.state.Tick(1,false,false);Assert(f.consumes==0,"inactive");});
        Test("hygiene pause stops timers",()=>
        {var f=new Fixture();var p=f.PlayerBurger();f.state.Load(p,FastFoodCookingState.Steps(f.burger)[0].item);f.state.Tick(30,false,true);Assert(p.elapsed==0&&f.consumes==0,"paused");});
        Test("instant drink fills once and serving does not recharge",()=>
        {var f=new Fixture();f.state.Accept(1,new[]{f.drink});f.state.Activate(1);f.state.Enter(FastFoodStationMode.Assembler);var p=f.state.FindTicket(1).portions[0];Assert(f.state.BeginServingDrag(p)&&f.consumes==0,"drag");Assert(f.state.Place(p)&&f.consumes==1,"fill");Assert(f.state.Serve(1)&&!f.state.Serve(1)&&f.consumes==1,"serve once");});
        Test("drag cancellation keeps serving available",()=>
        {var f=new Fixture();f.state.RestoreReady(f.burger,1);f.state.Accept(1,new[]{f.burger});f.state.Activate(1);f.state.Enter(FastFoodStationMode.Assembler);var p=f.state.FindTicket(1).portions[0];Assert(f.state.BeginServingDrag(p)&&!f.state.BeginServingDrag(p),"exclusive drag");p.dragging=false;Assert(f.state.BeginServingDrag(p)&&f.consumes==0,"retry");});
        Test("staff serves a completed tray handed over before Serve",()=>
        {var f=new Fixture();f.state.RestoreReady(f.burger,1);f.state.Accept(1,new[]{f.burger});f.state.Activate(1);f.state.Enter(FastFoodStationMode.Assembler);var p=f.state.FindTicket(1).portions[0];f.state.BeginServingDrag(p);f.state.Place(p);f.state.Enter(FastFoodStationMode.None);f.state.Tick(1,false,false);Assert(f.state.FindTicket(1).submitted,"handoff of full tray");});
        Test("occupied assembler owns all new tickets, one actionable at a time",()=>
        {var f=new Fixture();f.state.Enter(FastFoodStationMode.Assembler);for(int i=1;i<=10;i++){f.state.Accept(i,new[]{f.drink});f.state.Activate(i);}Assert(f.state.Tickets.Count(t=>t.player)==10,"player ownership");Assert(!f.state.BeginServingDrag(f.state.Tickets[1].portions[0]),"queued ticket blocked");f.state.Tick(2,false,false);Assert(f.state.Tickets[1].elapsed==0&&f.consumes==0,"queue not timed or auto served");});
        Test("reserve stock is bounded and not charged until finished",()=>
        {var f=new Fixture();f.state.EnsureReserve(new[]{f.burger},2);f.state.EnsureReserve(new[]{f.burger},2);Assert(f.state.Portions.Count==2&&f.consumes==0,"bounded");for(int i=0;i<15;i++)f.state.Tick(1,false,false);Assert(f.state.ReadyStock(f.burger)==2&&f.consumes==2,"reserve cooked");});
        Test("final commit rejects changed stock without creating food",()=>
        {var f=new Fixture();var p=f.PlayerBurger();f.Cook(p);f.state.Collect(p);f.stock[ItemType.Cheese]=0;var steps=FastFoodCookingState.Steps(f.burger);f.state.Load(p,steps[1].item);Assert(!f.state.Load(p,steps[2].item)&&p.stage!=FastFoodCookingStage.Complete&&f.consumes==0,"atomic rejection");});
        Test("batch production stays completed after serving",()=>
        {var f=new Fixture();var p=f.PlayerBurger();f.FinishBurger(p);var b=p.batch;f.state.Enter(FastFoodStationMode.Assembler);f.state.BeginServingDrag(p);f.state.Place(p);f.state.Serve(1);f.state.Release(1,true);Assert(b.completed==1&&f.consumes==1,"historical batch progress");});
        Test("deadline does not steal work while player occupies station",()=>
        {var f=new Fixture();var p=f.PlayerBurger();f.state.Tick(301,false,false);Assert(p.player&&f.consumes==0,"occupied ownership");});
        Test("remake order number preserves a single reservation",()=>
        {var f=new Fixture();f.state.Accept(1,new[]{f.burger});Assert(f.state.ReassignOrderNumber(1,2)&&f.state.FindTicket(1)==null&&f.state.FindTicket(2).portions[0].order==2,"rebind");Assert(f.state.Portions.Count==1&&f.state.Available(ItemType.Patty)==29,"one reservation");});
        Test("failed final ingredient can be retried after restocking",()=>
        {var f=new Fixture();var p=f.PlayerBurger();f.Cook(p);f.state.Collect(p);var steps=FastFoodCookingState.Steps(f.burger);f.state.Load(p,steps[1].item);f.stock[ItemType.Cheese]=0;Assert(!f.state.Load(p,steps[2].item),"reject");f.stock[ItemType.Cheese]=1;Assert(f.state.Load(p,steps[2].item)&&f.consumes==1,"retry succeeds");});
        Test("kitchen focus filters lobby tasks before priority selection",()=>
        {PlayerTaskGuidance.SetTask("lobby","lobby","Pay bill","",9999,null,PlayerTaskCategory.Service);PlayerTaskGuidance.SetTask("cook","grill","Cook burger","",1,null,PlayerTaskCategory.Kitchen);PlayerTaskGuidance.SetKitchenFocus(true);Assert(PlayerTaskGuidance.Current.Key=="grill","filtered priority");});
        Test("restock notification remains available while cooking",()=>
        {PlayerTaskGuidance.SetTask("restock","restock","Restock patties","",99999,null,PlayerTaskCategory.Restock);Assert(PlayerTaskGuidance.Current.Key=="grill"&&PlayerTaskGuidance.RestockNotification.IsValid,"separate channel");});
        Test("idle kitchen never falls back to a lobby task",()=>
        {PlayerTaskGuidance.ClearTask("cook");Assert(!PlayerTaskGuidance.Current.IsValid,"no fallback");PlayerTaskGuidance.SetKitchenFocus(false);Assert(PlayerTaskGuidance.Current.Key=="restock","lobby restored");});
        Test("authored station overrides legacy food mapping",()=>
        {var f=new Fixture();f.burger.cookingStation=FastFoodStationMode.Fry;Assert(FastFoodCookingState.Station(f.burger)==FastFoodStationMode.Fry,"station override");});
        Test("authored first ingredient preserves costs and remaining order",()=>
        {var f=new Fixture();var expected=FastFoodCookingState.Requirements(f.burger);f.burger.firstCookingIngredient=f.burger.ingredients.Last().item;var steps=FastFoodCookingState.Steps(f.burger);Assert(steps[0].item==f.burger.firstCookingIngredient,"first step");Assert(expected.All(x=>FastFoodCookingState.Requirements(f.burger)[x.Key]==x.Value),"unchanged cost");});
        Test("staff finishes started grill work but starts no fresh work after entry",()=>
        {
            var f=new Fixture();f.state.staffSlotsPerStation=2;f.state.EnsureReserve(new[]{f.burger},3);f.state.Tick(1,false,false);
            var started=f.state.Portions.Where(p=>p.stage==FastFoodCookingStage.Cooking).ToArray();
            Assert(started.Length==2,"two prestarted jobs");
            f.state.Enter(FastFoodStationMode.Grill);
            Assert(started.All(p=>!p.player)&&f.state.PlayerWork.Count==1,"ownership at entry");
            for(int i=0;i<20;i++)f.state.Tick(1,false,false);
            Assert(started.All(p=>p.stage==FastFoodCookingStage.Complete)&&f.consumes==2,"staff work completes");
            Assert(f.state.PlayerPortion.stage==FastFoodCookingStage.Waiting,"new work remains player-owned");
        });
        Test("staff completes already-started assembler ticket while new tickets queue for player",()=>
        {
            var f=new Fixture();f.state.Accept(1,new[]{f.drink,f.drink});f.state.Activate(1);f.state.Tick(.25f,false,false);
            f.state.Enter(FastFoodStationMode.Assembler);Assert(!f.state.FindTicket(1).player,"prestarted ticket retained");
            f.state.Accept(2,new[]{f.drink});f.state.Activate(2);
            for(int i=0;i<6;i++)f.state.Tick(1,false,false);
            Assert(f.state.FindTicket(1).submitted&&f.state.PlayerTicket.number==2&&f.consumes==2,"mixed ownership");
        });
        Test("switching stations preserves partial prep until the staff grace period expires",()=>
        {
            var f=new Fixture();var p=f.PlayerBurger();f.Cook(p);f.state.Collect(p);
            f.state.Load(p,FastFoodCookingState.AssemblySteps(f.burger)[0].item);
            int step=p.ingredientStep;f.state.Enter(FastFoodStationMode.Fry);
            Assert(p.player&&p.ingredientStep==step,"partial prep preserved during grace period");
            f.state.Tick(f.state.stationGraceSeconds,false,false);
            Assert(!p.player,"staff takeover after grace period");
            for(int i=0;i<10;i++)f.state.Tick(1,false,false);
            Assert(f.consumes==1,"staff completes without double debit");
        });
        Console.WriteLine("30 cooking and guidance scenarios passed. Unity lifecycle and rendering are not simulated.");
    }
}
