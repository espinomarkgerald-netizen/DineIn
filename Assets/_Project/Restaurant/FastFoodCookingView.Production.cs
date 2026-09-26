using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class FastFoodCookingView
{
    [Header("Batch presentation")]
    [SerializeField] private string cookingBatchFormat = "{0} · {1}/{2} cooked · {3}";
    [SerializeField] private string preparingBatchFormat = "{0} · {1}/{2} assembled · {3}";
    [SerializeField] private string fryerRequestFormat = "Grill needs {0} · {1} to fry";
    [SerializeField] private string fryerWaitingFormat = "Fryer is cooking {0} · {1}/{2} ready";
    [SerializeField] private string prepStepFormat = "Add {0}";
    [SerializeField, HideInInspector] private TMPro.TMP_Text staffActivity; // Legacy editor migration reference only.
    private readonly List<CookingVisual> cookingVisuals = new();
    [SerializeField] private string loadStepFormat="Drag {0} to any open cooking spot";
    [SerializeField] private string cookingStepText="Cooking — watch the progress rings";
    [SerializeField] private string collectStepText="Move the ready food to the tray";
    [SerializeField] private string completedBatchText="Batch ready — nicely done!";
    private bool prepView, cameraMoving;
    private float cameraBlend;
    private Vector3 cameraStartPosition;
    private Quaternion cameraStartRotation;
    private float cookingViewSize, cookingFieldOfView, cameraStartSize, cameraStartFov;

    private void ResetProductionCamera()
    {
        prepView=false; cameraMoving=false; cameraBlend=1;
        cookingViewSize=stationCamera.orthographicSize;cookingFieldOfView=stationCamera.fieldOfView;
        var anchor=activeStation.cookingViewAnchor;
        if(anchor!=null)stationCamera.transform.SetPositionAndRotation(anchor.position,anchor.rotation);
    }
    private void UpdateProductionCamera()
    {
        if(activeStation==null || stationCamera==null)return;
        bool finishedPrep=State.ShowingCompletedBatch && State.PlayerWork.Any(p=>FastFoodCookingState.AssemblySteps(p.recipe).Count>0) ||
            State.Mode==FastFoodStationMode.Grill && (prepPickups.Count>0 || State.Portions.Any(p=>p.prepSlot>=0 && !p.placed));
        bool wantsPrep=(State.PrepReady||finishedPrep||State.Mode==FastFoodStationMode.Fry&&Time.unscaledTime<fryerPrepUntil) && activeStation.preparationViewAnchor!=null;
        // Retained food on the prep table must not pull the camera away from an unfinished cooking batch.
        if(State.Mode==FastFoodStationMode.Grill && State.HasPendingCooking)wantsPrep=false;
        if(wantsPrep!=prepView && drag==null && Time.timeScale>0)
        {
            prepView=wantsPrep;cameraBlend=0;cameraMoving=true;
            cameraStartPosition=stationCamera.transform.position;cameraStartRotation=stationCamera.transform.rotation;
            cameraStartSize=stationCamera.orthographicSize;cameraStartFov=stationCamera.fieldOfView;
        }
        State.FryerPrepPaused=State.Mode==FastFoodStationMode.Fry && (prepView || cameraMoving);
        if(!cameraMoving || drag!=null || Time.timeScale<=0)return;
        var target=prepView?activeStation.preparationViewAnchor:activeStation.cookingViewAnchor;
        if(target==null){cameraMoving=false;return;}
        cameraBlend=LevelOneUIAccessibility.ReducedMotion?1:Mathf.Min(1,cameraBlend+Time.unscaledDeltaTime/Mathf.Max(.05f,activeStation.cameraShiftSeconds));
        float t=Mathf.SmoothStep(0,1,cameraBlend);
        stationCamera.transform.SetPositionAndRotation(Vector3.Lerp(cameraStartPosition,target.position,t),Quaternion.Slerp(cameraStartRotation,target.rotation,t));
        stationCamera.orthographicSize=Mathf.Lerp(cameraStartSize,prepView?activeStation.preparationViewSize:cookingViewSize,t);
        stationCamera.fieldOfView=Mathf.Lerp(cameraStartFov,prepView?activeStation.preparationFieldOfView:cookingFieldOfView,t);
        cameraMoving=cameraBlend<1;
    }
    private string ProductionSignature() => State.Mode==FastFoodStationMode.Assembler ? "" :
        State.PrepReady+":"+State.ShowingCompletedBatch+":"+string.Join(";",State.Portions.Where(p=>p.player || (p.slot>=0 || p.proteinReady || p.stage==FastFoodCookingStage.Complete) && FastFoodCookingState.Station(p.recipe)==State.Mode)
            .Select(p=>State.Portions.IndexOf(p)+"/"+p.slot+"/"+p.prepSlot+"/"+p.stage+"/"+p.ingredientStep+"/"+p.placed))+":"+State.PlayerWork.Count(p=>p.stage==FastFoodCookingStage.Complete);

    private void RefreshProductionHeader()
    {
        if(activeStation.status!=null)activeStation.status.transform.parent.gameObject.SetActive(false);
        var work=State.PlayerWork;
        var recipe=State.PlayerPortion?.recipe??work.FirstOrDefault()?.recipe;
        int ready=work.Count(p=>p.proteinReady || p.stage==FastFoodCookingStage.Ready || p.stage==FastFoodCookingStage.Complete);
        int complete=work.Count(p=>p.stage==FastFoodCookingStage.Complete);
        progress.text=recipe==null?idleProduction:string.Format(State.PrepReady?preparingBatchFormat:cookingBatchFormat,recipe.DisplayName,State.PrepReady?complete:ready,work.Count,TimeLabel(State.deadlineSeconds-work.Max(p=>p.assignedFor)));
        if(recipe!=null && State.Mode==FastFoodStationMode.Grill && FastFoodCookingState.Station(recipe)==FastFoodStationMode.Fry && !State.PrepReady)
            progress.text=string.Format(fryerWaitingFormat,recipe.DisplayName,ready,work.Count);
        if(recipe!=null && State.Mode==FastFoodStationMode.Fry && FastFoodCookingState.PreparationStation(recipe)==FastFoodStationMode.Grill)
            heading.text=activeStation.stationTitle+"  /  FOR GRILL PREP";
        progressFill.transform.parent.gameObject.SetActive(recipe!=null);
        SetBatchProgress((object)work.FirstOrDefault()?.batch??work.FirstOrDefault(),recipe==null?0:(float)(State.PrepReady?complete:ready)/Mathf.Max(1,work.Count));
        if(Time.unscaledTime>feedbackUntil)
        {
            string instruction="";
            if(State.ShowingCompletedBatch)instruction=completedBatchText;
            else if(State.PrepReady && State.PlayerPortion!=null)
            {
                var step=FastFoodCookingState.AssemblySteps(recipe)[State.PlayerPortion.ingredientStep];
                instruction=string.Format(prepStepFormat,step.label,State.PlayerPortion.ingredientStep+1,FastFoodCookingState.AssemblySteps(recipe).Count);
            }
            else if(State.PlayerPortion!=null && FastFoodCookingState.WorkStation(State.PlayerPortion)==State.Mode)
            {
                var portion=State.PlayerPortion;
                instruction=portion.stage==FastFoodCookingStage.Waiting?State.FreeSlot(State.Mode,true)<0?cookingStepText:string.Format(loadStepFormat,FastFoodCookingState.Steps(portion.recipe)[0].item.displayName):
                    portion.stage==FastFoodCookingStage.Ready?State.Mode==FastFoodStationMode.Fry?"Tap a raised basket to collect its food":"Tap each ready patty to collect":portion.stage==FastFoodCookingStage.Cooking?cookingStepText:"";
            }
            if(State.Mode==FastFoodStationMode.Fry && instruction.Length==0 && !State.ShowingCompletedBatch)
            {
                var request=State.PlayerWork.FirstOrDefault(p=>p.player && FastFoodCookingState.Station(p.recipe)==FastFoodStationMode.Fry && FastFoodCookingState.PreparationStation(p.recipe)==FastFoodStationMode.Grill && !p.proteinReady && p.stage!=FastFoodCookingStage.Complete);
                if(request!=null)instruction=string.Format(fryerRequestFormat,request.recipe.DisplayName,State.Portions.Count(p=>p.recipe==request.recipe&&!p.proteinReady&&p.stage!=FastFoodCookingStage.Complete));
            }
            if(instruction!=lastInstruction)
            {
                lastInstruction=instruction;
                if(floatingCue!=null)floatingCue.Show(instruction,recipe!=null?recipe.sprite:null,State.ShowingCompletedBatch?3:0);
            }
        }
    }
    private void UpdateBayFeedback()
    {
        if(activeStation==null)return;
        if(activeStation.cookingFeedback!=null)activeStation.cookingFeedback.SetActive(false);
        foreach(var bay in activeStation.Slots)
        {
            var portion=State.AtSlot(State.Mode,bay.slotIndex);
            bool cooking=portion!=null&&portion.stage==FastFoodCookingStage.Cooking;
            if(bay.cookingFeedback!=null && bay.cookingFeedback.activeSelf!=cooking)bay.cookingFeedback.SetActive(cooking);
            if(bay.timer!=null)
            {
                var screen=stationCamera.WorldToScreenPoint(bay.foodAnchor.position+Vector3.up*bay.timer.worldLift);
                bool visible=portion!=null&&!prepView&&screen.z>0;
                bay.timer.gameObject.SetActive(visible);
                if(visible)
                {
                    bool staff=!portion.player;
                    // cookSeconds already includes the hygiene slowdown in the controller.
                    float speed=cooking&&staff?State.staffMultiplier:1;
                    bay.timer.Present(portion,State.cookSeconds,State.overcookSeconds,speed,staff,Time.timeScale<=0||HygieneManager.KitchenPaused);
                    var rect=(RectTransform)bay.timer.transform;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent,screen,null,out var point);
                    rect.anchoredPosition=point+bay.timer.screenOffset;
                }
            }
            else if(bay.status!=null)
            {
                bay.status.transform.parent.gameObject.SetActive(portion!=null&&!prepView);
                bay.status.text=portion==null?"":cooking?TimeLabel(State.cookSeconds-portion.elapsed):portion.stage==FastFoodCookingStage.Burnt?"Burnt":"Ready";
                if(stationCamera!=null && canvas!=null && portion!=null)
                {
                    var screen=stationCamera.WorldToScreenPoint(bay.foodAnchor.position+Vector3.up*.25f);
                    var rect=(RectTransform)bay.status.transform.parent;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent,screen,null,out var point);
                    rect.anchoredPosition=point;
                }
            }
        }
        foreach(var visual in cookingVisuals)
        {
            if(visual.root==null)continue;
            UpdateFoodColor(visual);
            bool animate=!HygieneManager.KitchenPaused&&!State.FryerPrepPaused;
            float bob=animate&&State.Mode==FastFoodStationMode.Fry&&visual.portion.stage==FastFoodCookingStage.Cooking?Mathf.Sin(Time.time*activeStation.cookingBobFrequency*Mathf.PI*2)*activeStation.cookingBobHeight:0;
            float pulse=animate&&visual.portion.player&&visual.portion.stage==FastFoodCookingStage.Ready?activeStation.readyPulseAmount*(.5f+.5f*Mathf.Sin(Time.time*activeStation.readyPulseFrequency*Mathf.PI*2)):0;
            visual.root.localPosition=visual.position+Vector3.up*bob;
            visual.root.localScale=visual.scale*(1+pulse);
        }
    }
    private void RebuildProductionControls()
    {
        UpdateProductionHotbar();
        // Assigned prep layers remain on their own slots while protein is cooking.
        if(State.Mode==FastFoodStationMode.Grill)
            foreach(var portion in State.Portions.Where(x=>x.stage!=FastFoodCookingStage.Complete && !x.placed &&
                x.prepSlot>=0 && x.prepSlot<activeStation.PrepTargets.Length))
            {
                var target=activeStation.PrepTargets[portion.prepSlot];
                var anchor=target.foodAnchor!=null?target.foodAnchor:target.transform;
                var layers=FastFoodCookingState.AssemblySteps(portion.recipe);
                for(int i=0;i<portion.ingredientStep;i++)if(layers[i].visual!=null)
                {
                    var layer=SpawnVisual(layers[i].visual,anchor);
                    if(portion==acceptedPortion && i==acceptedStep)AnimateAccepted(layer,portion);
                }
            }
        foreach(var bay in activeStation.Slots)
        {
            var portion=State.AtSlot(State.Mode,bay.slotIndex);
            if(portion==null || bay.foodAnchor==null)continue;
            var visual=SpawnVisual(portion.recipe.kitchenCookingPrefab??activeStation.cookingFoodTemplate,bay.foodAnchor,portion.player,portion);
            cookingVisuals.Add(new CookingVisual(visual,portion));
            AnimateAccepted(visual,portion);
        }
        if(State.Mode==FastFoodStationMode.Fry)
        {
            int index=0;
            foreach(var finished in State.Portions.Where(FastFoodCookingState.OnFryRack))
            {
                if(index>=activeStation.completedFoodAnchors.Length)break;
                var anchor=activeStation.completedFoodAnchors[index++];
                if(collecting.ContainsKey(finished))continue;
                var template=finished.recipe.kitchenCookingPrefab;
                var dish=SpawnRackFood(template!=null?template:activeStation.cookingFoodTemplate,anchor,finished.recipe);
                AnimateAccepted(dish,finished);
            }
        }
        FitHotbar();
    }

    // Keep the authored cells alive for the whole batch, including temporarily unavailable ingredients.
    private readonly HashSet<FastFoodCookingState.Portion> hotbarBatch=new();
    private readonly List<(FastFoodCookingDragHandle slot,bool raw)> productionCells=new();
    private float productionCellSize,productionViewportWidth;
    private bool resetHotbarScroll=true;

    void ResetProductionHotbar()
    {
        if(hotbar!=null)
        {
            Clear(hotbar);
        }
        productionCellSize=productionViewportWidth=0;resetHotbarScroll=true;
        hotbarBatch.Clear();productionCells.Clear();proteinSlots.Clear();
    }

    void UpdateProductionHotbar()
    {
        if(hotbar==null || activeStation==null || State.Mode==FastFoodStationMode.Assembler)return;
        if(hotbarBatch.Count>0 && !State.PlayerWork.Any(hotbarBatch.Contains))ResetProductionHotbar();
        foreach(var portion in State.PlayerWork)hotbarBatch.Add(portion);
        int previousCellCount=productionCells.Count;
        // Reserve unlocked station ingredients before the batch frame is sized. Fryer deliveries
        // update an existing visible cell instead of appending beyond the masked viewport.
        var catalog=MenuCatalog.Default;
        if(catalog!=null)
            EnsureProductionRecipes(catalog.Products.Where(r=>r!=null && r.availableOnMenu && r.IsUnlocked)
                .OrderBy(r=>r.menuSortOrder).ThenBy(r=>r.ProductId));
        EnsureProductionRecipes(State.PlayerWork.Select(p=>p.recipe).Distinct());
        proteinSlots.Clear();
        foreach(var entry in productionCells)
        {
            var slot=entry.slot;
            var candidate=entry.raw
                ? (!State.PrepReady?State.NextLoad(slot.item):null)
                : (State.PrepReady?Enumerable.Range(0,State.grillPrepSlots).Select(i=>State.PrepPortion(slot.item,i)).FirstOrDefault(p=>p!=null):null);
            int units;
            if(slot.storedProtein)
            {
                units=State.PlayerWork.Count(p=>p.player && FastFoodCookingState.HasStoredProtein(p) &&
                    FastFoodCookingState.Steps(p.recipe).FirstOrDefault()?.item==slot.item);
                slot.count.text=string.Format(storedProteinFormat,units);
                foreach(var recipe in hotbarBatch.Select(p=>p.recipe).Distinct())
                    if(FastFoodCookingState.Steps(recipe).FirstOrDefault()?.item==slot.item)proteinSlots[recipe]=slot;
            }
            else
            {
                units=InventoryManager.Instance.GetStock(slot.item.itemType);
                slot.count.text=string.Format(slot.stockFormat,slot.item.displayName,units);
            }
            slot.portion=candidate;slot.enabled=candidate!=null && units>0;
            var color=ingredientTemplate.icon.color;color.a*=slot.enabled?1:.4f;
            slot.icon.color=color;
        }
        if(productionCells.Count!=previousCellCount)FitHotbar();
    }

    void EnsureProductionRecipes(IEnumerable<Recipe> recipes)
    {
        foreach(var recipe in recipes)
        {
            if(recipe==null || recipe.category!=MenuProductCategory.Food)continue;
            var protein=FastFoodCookingState.Steps(recipe).FirstOrDefault()?.item;
            if(protein!=null && FastFoodCookingState.Station(recipe)==State.Mode)
                EnsureProductionCell(protein,true,false,recipe.kitchenIngredientIcon!=null?recipe.kitchenIngredientIcon:protein.sprite,recipe.kitchenCookingPrefab);
            if(State.Mode!=FastFoodStationMode.Grill || FastFoodCookingState.PreparationStation(recipe)!=State.Mode)continue;
            foreach(var step in FastFoodCookingState.AssemblySteps(recipe))
                if(step.item!=null)EnsureProductionCell(step.item,false,step.item==protein,step.icon!=null?step.icon:step.item.sprite,step.visual);
        }
    }

    void EnsureProductionCell(ItemData item,bool raw,bool cooked,Sprite icon,GameObject visual)
    {
        if(productionCells.Any(c=>c.slot.item==item && c.raw==raw))return;
        var slot=Instantiate(ingredientTemplate,hotbar);slot.gameObject.SetActive(true);
        slot.view=this;slot.item=item;slot.storedProtein=cooked;
        slot.icon.sprite=icon;slot.previewTemplate=visual;
        if(cooked)
        {
            slot.count.color=new Color(.08f,.34f,.24f);
            slot.count.fontSize=Mathf.Min(slot.count.fontSize,23);
        }
        productionCells.Add((slot,raw));
    }
}
