using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class FastFoodCookingView
{
    [Header("Finished prep pickup")]
    [SerializeField,Min(.05f)] private float prepPickupSeconds=.35f;
    private readonly Dictionary<FastFoodCookingState.Portion,GameObject> preparedVisuals=new();
    private readonly List<GameObject> prepPickups=new();
    private GameObject prepDragTemplate;
    void OnPrepTransferred(FastFoodCookingState.Portion portion,int slot)
    {
        if(!opened || activeStation==null || State.Mode!=FastFoodStationMode.Grill ||
            slot<0 || slot>=activeStation.PrepTargets.Length)return;
        if(!preparedVisuals.TryGetValue(portion,out var food))
        {
            var target=activeStation.PrepTargets[slot];
            var template=portion.recipe.kitchenPreviewPrefab!=null?portion.recipe.kitchenPreviewPrefab:
                portion.recipe.kitchenServingPrefab!=null?portion.recipe.kitchenServingPrefab:activeStation.servingTemplate;
            if(template==null)return;
            food=SpawnVisual(template,target.foodAnchor!=null?target.foodAnchor:target.transform);
            transientVisuals.Remove(food);
        }
        preparedVisuals.Remove(portion);
        if(food==null)return;
        // The pickup is visual only; the board slot is already available for the next item.
        food.transform.SetParent(activeStation.transform,true);
        prepPickups.Add(food);
        StartCoroutine(PickupPreparedFood(food));
    }
    GameObject PrepIngredientVisual(FastFoodCookingState.Portion portion)
    {
        if(portion==null || portion.stage!=FastFoodCookingStage.Preparing)return null;
        var steps=FastFoodCookingState.AssemblySteps(portion.recipe);
        return portion.ingredientStep<steps.Count?steps[portion.ingredientStep].visual:null;
    }
    void MatchPrepPreview(FastFoodCookingDropTarget target)
    {
        if(State.Mode!=FastFoodStationMode.Grill || target==null || target.kind!=1 || drag?.item==null)return;
        var portion=State.PrepPortion(drag.item,target.slotIndex);
        var template=PrepIngredientVisual(portion);
        if(template==null || template==prepDragTemplate)return;
        prepDragTemplate=template;
        if(preview!=null)Destroy(preview);
        preview=CreateFoodGhost(template,ghostMaterial,out ghostCenterOffset);
        preview.transform.SetParent(activeStation.transform,true);
        if(target.foodAnchor!=null)preview.transform.localScale=target.foodAnchor.localScale;
        ClearDropGhosts();CreateDropGhosts();previewPositionInitialized=false;
    }
    void RefreshCompletedPrep()
    {
        if(State.Mode!=FastFoodStationMode.Grill)return;
        foreach(var entry in preparedVisuals.ToArray())
        {
            if(State.Portions.Contains(entry.Key) && entry.Key.prepSlot>=0 && !entry.Key.placed)continue;
            preparedVisuals.Remove(entry.Key);
            if(entry.Value==null)continue;
            if(entry.Key.placed)
            {
                prepPickups.Add(entry.Value);
                StartCoroutine(PickupPreparedFood(entry.Value));
            }
            else Destroy(entry.Value);
        }
        foreach(var portion in State.Portions.Where(p=>p.stage==FastFoodCookingStage.Complete && !p.placed && p.prepSlot>=0))
        {
            if(preparedVisuals.ContainsKey(portion) || portion.prepSlot>=activeStation.PrepTargets.Length)continue;
            var target=activeStation.PrepTargets[portion.prepSlot];
            var anchor=target.foodAnchor!=null?target.foodAnchor:target.transform;
            var template=portion.recipe.kitchenPreviewPrefab!=null?portion.recipe.kitchenPreviewPrefab:
                portion.recipe.kitchenServingPrefab!=null?portion.recipe.kitchenServingPrefab:activeStation.servingTemplate;
            if(template==null)continue;
            var food=SpawnVisual(template,anchor);
            transientVisuals.Remove(food);preparedVisuals[portion]=food;
            AnimateAccepted(food,portion);
        }
    }
    IEnumerator PickupPreparedFood(GameObject food)
    {
        Vector3 start=food.transform.position;
        var assembler=stations.FirstOrDefault(s=>s.mode==FastFoodStationMode.Assembler);
        Vector3 direction=assembler!=null && assembler.prepFoodAnchor!=null?assembler.prepFoodAnchor.position-start:Vector3.forward;
        Vector3 end=start+direction.normalized*.65f+Vector3.up*.15f;
        for(float t=0;t<1 && food!=null && opened;)
        {
            if(Time.timeScale<=0 || HygieneManager.KitchenPaused){yield return null;continue;}
            if(LevelOneUIAccessibility.ReducedMotion)break;
            food.transform.position=Vector3.Lerp(start,end,Mathf.SmoothStep(0,1,t))+Vector3.up*(Mathf.Sin(t*Mathf.PI)*collectArcHeight);
            t+=Time.unscaledDeltaTime/Mathf.Max(.05f,prepPickupSeconds);
            yield return null;
        }
        if(food!=null)Destroy(food);
        prepPickups.Remove(food);
    }
    void ClearPrepVisuals()
    {
        foreach(var food in preparedVisuals.Values)if(food!=null)Destroy(food);
        foreach(var food in prepPickups)if(food!=null)Destroy(food);
        preparedVisuals.Clear();prepPickups.Clear();
    }
}
