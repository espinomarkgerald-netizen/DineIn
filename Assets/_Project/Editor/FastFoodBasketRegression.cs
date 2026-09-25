using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FastFoodBasketRegression
{
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Basket regression: " + message); }
    [MenuItem("Dine In/Fast Food/Check Basket Collection")]
    public static string Run()
    {
        Require(!Application.isPlaying, "Run isolated checks outside Play Mode.");
        var fries = MenuCatalog.Default.Products.First(r => r.kitchenItemType == ItemTypeKitchen.Fries);
        var burger = MenuCatalog.Default.Products.First(r => r.kitchenItemType == ItemTypeKitchen.Burger);
        int consumed = 0;
        var state = new FastFoodCookingState(_ => 1000, _ => { consumed++; return true; });
        state.EnsureReserve(new[] { fries }, 8); state.Enter(FastFoodStationMode.Fry);
        var ingredient = FastFoodCookingState.Steps(fries)[0].item;
        var first = state.NextLoad(ingredient); Require(state.Load(first, ingredient, 0), "First basket load.");
        state.Tick(2, true, false);
        var second = state.NextLoad(ingredient); Require(state.Load(second, ingredient, 1), "Second basket load.");
        state.Tick(state.cookSeconds - 2, true, false);
        Require(!state.BasketRaised(0) && state.CollectBasket(0) == 0 && !state.Collect(first), "Partly cooked basket was harvested.");
        state.Tick(2, true, false); Require(state.BasketRaised(0), "Finished basket did not rise.");
        state.Tick(30, true, false); Require(first.stage == FastFoodCookingStage.Ready && second.stage == FastFoodCookingStage.Ready, "Lifted food burnt.");
        Require(state.CollectBasket(0) == 2 && consumed == 2 && state.FryRackCount == 2, "Basket did not transfer exactly two portions.");
        Require(state.CollectBasket(0) == 0 && consumed == 2 && !state.BasketRaised(0), "Double tap duplicated food or left basket raised.");
        Require(state.AtSlot(FastFoodStationMode.Fry, 0) == null && state.AtSlot(FastFoodStationMode.Fry, 1) == null, "Collected slots stayed occupied.");
        var third = state.NextLoad(ingredient); Require(state.Load(third, ingredient, 2), "Single item load.");
        state.Tick(state.cookSeconds, true, false);
        state.fryRackCapacity = 2; Require(state.CollectBasket(2) == 0 && third.stage == FastFoodCookingStage.Ready && consumed == 2, "Full rack lost food.");
        Require(state.Accept(9001, new[] { fries }), "Ready stock could not be allocated.");
        state.Enter(FastFoodStationMode.Assembler); state.Activate(9001);
        var serving = state.PlayerTicket.portions[0];
        Require(state.BeginServingDrag(serving) && state.Place(serving) && state.FryRackCount == 1 && consumed == 2, "Assembly failed to consume ready rack stock exactly once.");
        state.Enter(FastFoodStationMode.Fry); Require(state.CollectBasket(2) == 1 && consumed == 3, "Single raised basket could not fill the released rack space.");
        Require(!state.BasketRaised(-1) && !state.BasketRaised(9), "Invalid basket indices accepted.");

        consumed = 0; state = new FastFoodCookingState(_ => 1000, _ => { consumed++; return true; });
        state.EnsureReserve(new[] { burger }, 2); state.Enter(FastFoodStationMode.Grill);
        ingredient = FastFoodCookingState.Steps(burger)[0].item;
        first = state.NextLoad(ingredient); Require(state.Load(first, ingredient, 0), "Grill first load.");
        second = state.NextLoad(ingredient); Require(state.Load(second, ingredient, 1), "Grill second load.");
        state.Tick(state.cookSeconds, true, false); state.Tick(.1f, true, false);
        Require(first.stage == FastFoodCookingStage.Ready && second.stage == FastFoodCookingStage.Ready, "Player grill auto-collected.");
        Require(state.Collect(first) && second.stage == FastFoodCookingStage.Ready && !state.PrepReady, "One tap collected multiple patties.");
        Require(!state.Collect(first) && state.Collect(second) && state.PrepReady && consumed == 0, "Grill collection consumed ingredients early or duplicated.");
        Require(state.StoredProteinCount(burger) == 2, "Collected proteins missing from hotbar count.");
        var assembly = FastFoodCookingState.AssemblySteps(burger);
        int proteinStep = assembly.FindIndex(s => s.item == ingredient);
        Require(proteinStep >= 0, "Burger needs an explicit protein assembly step.");
        for (int i = 0; i < proteinStep; i++)
        {
            Require(state.Load(first, assembly[i].item), "Could not place an ingredient before the protein.");
            Require(state.StoredProteinCount(burger) == 2, "Protein count changed before placement.");
        }
        Require(state.Load(first, ingredient) && state.StoredProteinCount(burger) == 1, "Placing protein did not remove exactly one hotbar unit.");
        state.Enter(FastFoodStationMode.None); state.Tick(state.assemblySeconds, false, false);
        Require(consumed == 2, "Staff could not complete released prep work.");

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var fry = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FastFoodCookingStation>(true)).Single(s => s.mode == FastFoodStationMode.Fry);
        Require(fry.baskets.Length == 4 && fry.baskets.All(b => b != null && b.movingBasket != null && b.GetComponent<Collider>() != null), "Basket scene references missing.");
        foreach (var basket in fry.baskets)
        {
            var slots = fry.Slots.Where(s => s.slotIndex >= basket.firstSlot && s.slotIndex < basket.firstSlot + 2).ToArray();
            Require(slots.Length == 2 && slots.All(s => s.foodAnchor.IsChildOf(basket.movingBasket)), "Basket does not carry its two food anchors.");
        }
        Require(fry.completedFoodAnchors.Length == 8, "Drying rack needs eight anchors.");
        var shader = Shader.Find("Dine In/World Outline");
        Require(shader != null && !ShaderUtil.ShaderHasError(shader), "World outline shader failed.");
        return "PASS: staggered cooking, automatic lift, safe holding, one/two-item collection, full rack, duplicate taps, assembler consumption, grill individual collection, staff handoff, saved basket anchors and outline shader.";
    }
}
