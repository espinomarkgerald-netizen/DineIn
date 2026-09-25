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
    [SerializeField] private TMPro.TMP_Text staffActivity;
    [SerializeField] private string staffCookingFormat = "Staff cooking: {0} x{1}";
    [SerializeField] private string staffPreparingFormat = "Staff preparing: {0} x{1}";
    [SerializeField] private string staffAssemblyFormat = "Staff assembling {0} other orders";
    [SerializeField] private string staffIdleText = "Staff ready to help";
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
        bool finishedPrep=State.ShowingCompletedBatch && State.PlayerWork.Any(p=>FastFoodCookingState.AssemblySteps(p.recipe).Count>0);
        bool wantsPrep=(State.PrepReady||finishedPrep||State.Mode==FastFoodStationMode.Fry&&Time.unscaledTime<fryerPrepUntil) && activeStation.preparationViewAnchor!=null;
        if(wantsPrep!=prepView && (drag==null||State.Mode==FastFoodStationMode.Fry))
        {
            prepView=wantsPrep;cameraBlend=0;cameraMoving=true;
            cameraStartPosition=stationCamera.transform.position;cameraStartRotation=stationCamera.transform.rotation;
            cameraStartSize=stationCamera.orthographicSize;cameraStartFov=stationCamera.fieldOfView;
        }
        State.FryerPrepPaused=State.Mode==FastFoodStationMode.Fry && (prepView || cameraMoving);
        if(!cameraMoving)return;
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
            .Select(p=>State.Portions.IndexOf(p)+"/"+p.slot+"/"+p.stage+"/"+p.ingredientStep+"/"+p.placed))+":"+State.PlayerWork.Count(p=>p.stage==FastFoodCookingStage.Complete);

    private void RefreshProductionHeader()
    {
        if(activeStation.status!=null)activeStation.status.transform.parent.gameObject.SetActive(false);
        var work=State.PlayerWork;
        var recipe=work.FirstOrDefault()?.recipe;
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
    private void RefreshStaffActivity()
    {
        if(staffActivity==null)return;
        var work=State.Portions.Where(p=>!p.player)
            .Where(p=>State.Mode==FastFoodStationMode.Assembler || FastFoodCookingState.WorkStation(p)==State.Mode
                || State.Mode==FastFoodStationMode.Grill && FastFoodCookingState.PreparationStation(p.recipe)==State.Mode).ToArray();
        var cooking=work.FirstOrDefault(p=>p.stage==FastFoodCookingStage.Cooking);
        var preparing=work.FirstOrDefault(p=>p.stage==FastFoodCookingStage.Preparing);
        int assembling=State.Tickets.Count(t=>t.active&&!t.player&&!t.submitted&&t.portions.All(p=>p.stage==FastFoodCookingStage.Complete||p.recipe.category==MenuProductCategory.Drink));
        staffActivity.text=cooking!=null?string.Format(staffCookingFormat,cooking.recipe.DisplayName,work.Count(p=>p.recipe==cooking.recipe&&p.stage==FastFoodCookingStage.Cooking)):
            preparing!=null?string.Format(staffPreparingFormat,preparing.recipe.DisplayName,work.Count(p=>p.recipe==preparing.recipe&&p.stage==FastFoodCookingStage.Preparing)):
            State.Mode==FastFoodStationMode.Assembler&&assembling>0?string.Format(staffAssemblyFormat,assembling):staffIdleText;
        staffActivity.transform.parent.gameObject.SetActive(false);
        string cue=cooking!=null||preparing!=null||State.Mode==FastFoodStationMode.Assembler&&assembling>0?staffActivity.text:"";
        if(cue!=lastStaffCue)
        {
            lastStaffCue=cue;
            var dish=cooking??preparing;
            if(floatingCue!=null)floatingCue.Show(cue,dish!=null?dish.recipe.sprite:null);
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
            bool animate=!LevelOneUIAccessibility.ReducedMotion&&!HygieneManager.KitchenPaused&&!State.FryerPrepPaused;
            float bob=animate&&State.Mode==FastFoodStationMode.Fry&&visual.portion.stage==FastFoodCookingStage.Cooking?Mathf.Sin(Time.time*activeStation.cookingBobFrequency*Mathf.PI*2)*activeStation.cookingBobHeight:0;
            float pulse=animate&&visual.portion.player&&visual.portion.stage==FastFoodCookingStage.Ready?activeStation.readyPulseAmount*(.5f+.5f*Mathf.Sin(Time.time*activeStation.readyPulseFrequency*Mathf.PI*2)):0;
            visual.root.localPosition=visual.position+Vector3.up*bob;
            visual.root.localScale=visual.scale*(1+pulse);
        }
    }
    private void RebuildProductionControls()
    {
        var p=State.PlayerPortion;
        if(State.PrepReady && p!=null || State.ShowingCompletedBatch && State.PlayerWork.Any(x=>FastFoodCookingState.AssemblySteps(x.recipe).Count>0))
        {
            var steps=p!=null?FastFoodCookingState.AssemblySteps(p.recipe):new List<KitchenAssemblyStep>();
            // One icon per remaining ingredient; the current step selects the correct bun half/model.
            foreach(var group in steps.Skip(p!=null?p.ingredientStep:0).GroupBy(s=>s.item))
            {
                var step=group.First();AddSlot(step.icon!=null?step.icon:step.item.sprite,step.label,step.item,p,1);
                hotbar.GetChild(hotbar.childCount-1).GetComponent<FastFoodCookingDragHandle>().previewTemplate=step.visual;
            }
            for(int i=0;p!=null&&i<p.ingredientStep;i++)if(steps[i].visual!=null)
            {
                var layer=SpawnVisual(steps[i].visual,activeStation.prepFoodAnchor);
                if(i==acceptedStep)AnimateAccepted(layer,p);
            }
            // Only deliberately placed assembly layers belong on this board.
            // Collected proteins are represented by their hotbar count.
        }
        else
        {
            var recipes=State.PlayerWork.Count>0?State.PlayerWork.Select(x=>x.recipe).Distinct():MenuCatalog.Default.Products.Where(r=>r!=null && r.IsUnlocked && MenuAvailabilityManager.IsProductAvailable(r));
            foreach(var recipe in recipes.Where(r=>FastFoodCookingState.Station(r)==State.Mode))
            {
                var item=FastFoodCookingState.Steps(recipe).FirstOrDefault()?.item;if(item==null)continue;
                AddSlot(recipe.kitchenIngredientIcon!=null?recipe.kitchenIngredientIcon:item.sprite,item.displayName,item,State.NextLoad(item),1);
                hotbar.GetChild(hotbar.childCount-1).GetComponent<FastFoodCookingDragHandle>().previewTemplate=recipe.kitchenCookingPrefab;
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
        if(State.Mode==FastFoodStationMode.Grill)BindStoredProteinSlots();
        FitHotbar();
    }
}
