#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GrillPrepSlotsAuthoring
{
    static FastFoodCookingStation Grill()=>UnityEngine.Object.FindObjectsByType<FastFoodCookingStation>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(s=>s.mode==FastFoodStationMode.Grill);
    [MenuItem("Dine In/Fast Food/Inspect Grill Prep Slots")]
    public static void Inspect()
    {
        var rig=Grill();var target=rig.preparation;
        Debug.Log("[Prep slots] playing="+Application.isPlaying+" dirty="+rig.gameObject.scene.isDirty+" target="+target.transform.position+" scale="+target.transform.lossyScale+" anchor="+rig.prepFoodAnchor.position+" collider="+target.GetComponent<Collider>().bounds+" children="+string.Join(",",target.GetComponentsInChildren<Transform>(true).Select(t=>t.name)));
    }
    [MenuItem("Dine In/Fast Food/Author Grill Prep Slots")]
    public static void Apply()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Use Edit Mode.");
        var rig=Grill();Undo.RegisterFullObjectHierarchyUndo(rig.gameObject,"Grill prep slots");
        var original=rig.preparation;
        var source=original.GetComponent<BoxCollider>();
        if(source==null)throw new InvalidOperationException("Expected the existing box prep target.");
        var table=rig.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true))
            .Single(r=>r.name=="Cube.028" && r.transform.parent.name=="Fast Food Revamp");
        var size=new Vector3(table.bounds.size.x*.9f,.05f,table.bounds.size.z*.82f);
        original.transform.SetPositionAndRotation(new Vector3(table.bounds.center.x,table.bounds.max.y+.035f,table.bounds.center.z),Quaternion.identity);
        original.transform.localScale=new Vector3(size.x/source.size.x,size.y/source.size.y,size.z/source.size.z);
        var center=original.transform.position+Vector3.up*.055f;
        rig.prepFoodAnchor.position=center;
        var slots=new FastFoodCookingDropTarget[3];
        for(int i=0;i<3;i++)
        {
            var name="Prep Slot "+(i+1);
            var existing=rig.transform.Find(name);
            var go=existing!=null?existing.gameObject:new GameObject(name);
            go.transform.SetParent(rig.transform,true);
            go.layer=original.gameObject.layer;
            go.transform.SetPositionAndRotation(center+original.transform.right*((i-1)*size.x/3),original.transform.rotation);
            var box=go.GetComponent<BoxCollider>();if(box==null)box=go.AddComponent<BoxCollider>();
            box.size=new Vector3(size.x/3*.9f,Mathf.Max(.04f,size.y),size.z*.9f);
            var target=go.GetComponent<FastFoodCookingDropTarget>();if(target==null)target=go.AddComponent<FastFoodCookingDropTarget>();
            var anchor=go.transform.Find("Food Anchor");
            if(anchor==null){anchor=new GameObject("Food Anchor").transform;anchor.SetParent(go.transform,false);}
            anchor.localPosition=Vector3.zero;anchor.localRotation=Quaternion.identity;anchor.localScale=Vector3.one;
            target.kind=1;target.slotIndex=i;target.foodAnchor=anchor;slots[i]=target;
        }
        rig.prepSlots=slots;
        // The old broad target would compete with the three discrete targets.
        original.GetComponent<Collider>().enabled=false;
        original.enabled=false;
        EditorUtility.SetDirty(rig);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(rig.gameObject.scene);
        Debug.Log("[Prep slots] Saved full-table working area "+size+", three spaced slots and normal food anchor scale (1,1,1).");
    }
    [MenuItem("Dine In/Fast Food/Validate Grill Prep Slots")]
    public static void Validate()
    {
        var recipes=AssetDatabase.FindAssets("t:Recipe").Select(id=>AssetDatabase.LoadAssetAtPath<Recipe>(AssetDatabase.GUIDToAssetPath(id)))
            .Where(r=>r!=null && FastFoodCookingState.PreparationStation(r)==FastFoodStationMode.Grill && FastFoodCookingState.AssemblySteps(r).Count>=2).ToArray();
        if(recipes.Length<2)throw new InvalidOperationException("Need two existing sandwich recipes.");
        int consumed=0;
        var state=new FastFoodCookingState(_=>1000,_=>{consumed++;return true;});
        var portions=new[]{recipes[0],recipes[1],recipes[0]}.Select(r=>new FastFoodCookingState.Portion{recipe=r,stage=FastFoodCookingStage.Preparing,proteinReady=true,player=true}).ToArray();
        state.Portions.AddRange(portions);state.Enter(FastFoodStationMode.Grill);
        void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("[Prep slots] "+why);}
        var rig=Grill();
        Check(rig.prepSlots.Length==3 && rig.prepSlots.All(s=>s!=null && s.foodAnchor!=null && s.GetComponent<BoxCollider>().size.x>0),"Three saved nonzero prep targets.");
        Check(rig.prepSlots.Select(s=>s.transform.position).Distinct().Count()==3,"Saved positions distinct.");
        foreach(var recipe in recipes)
        {
            var template=recipe.kitchenPreviewPrefab!=null?recipe.kitchenPreviewPrefab:recipe.kitchenServingPrefab;
            if(template==null)continue;
            var sample=UnityEngine.Object.Instantiate(template);sample.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                var renderers=sample.GetComponentsInChildren<MeshRenderer>(true);
                if(renderers.Length==0)continue;
                var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                Check(bounds.size.x*rig.prepSlots[0].foodAnchor.lossyScale.x<=rig.prepSlots[0].GetComponent<BoxCollider>().size.x,"Finished model fits its slot.");
            }
            finally{UnityEngine.Object.DestroyImmediate(sample);}
        }
        for(int i=0;i<3;i++)
        {
            var step=FastFoodCookingState.AssemblySteps(portions[i].recipe)[0];
            Check(state.Load(portions[i],step.item,i),"Start independent slot "+i);
        }
        Check(portions.Select(p=>p.prepSlot).Distinct().Count()==3,"Slots are independent.");
        Check(!state.Load(portions[1],FastFoodCookingState.AssemblySteps(portions[1].recipe)[1].item,0),"Wrong slot rejected.");
        int transfers=0;
        state.PrepTransferred+=(food,slot)=>{Check(slot>=0 && slot<3 && food.prepSlot<0,"Pickup event receives released slot.");transfers++;};
        for(int i=0;i<3;i++)
        {
            var steps=FastFoodCookingState.AssemblySteps(portions[i].recipe);
            while(portions[i].stage==FastFoodCookingStage.Preparing)
                Check(state.Load(portions[i],steps[portions[i].ingredientStep].item,i),"Complete slot "+i);
        }
        Check(consumed==3,"Existing recipe consumption occurs once per sandwich.");
        Check(portions.All(p=>p.stage==FastFoodCookingStage.Complete && p.prepSlot<0 && !p.placed) && transfers==3,"All three reserve sandwiches leave the board immediately.");
        Check(portions.Select(p=>p.recipe).Distinct().Sum(state.ReadyStock)==3,"Collected reserve food remains ready stock.");
        Check(state.Accept(9876,portions.Select(p=>p.recipe).ToArray()),"Existing order accepts finished food.");
        Check(state.Tickets.Single().portions.All(portions.Contains),"Later order reuses the actual finished portions.");
        state.Enter(FastFoodStationMode.Assembler);state.Activate(9876);
        foreach(var food in portions)
            Check(state.BeginServingDrag(food) && state.Place(food),"Ready stock is usable through the existing assembler.");
        Check(!state.BeginServingDrag(portions[0]) && !state.Place(portions[0]),"Transferred food cannot be collected twice.");
        state.Activate(9876);
        Check(transfers==3 && consumed==3,"Repeated activation cannot duplicate pickups or inventory consumption.");
        Check(state.PlayerTicket.Ready && !state.PlayerTicket.submitted,"Explicit Serve Order remains required for player ticket.");
        Check(state.Serve(9876),"Existing Serve Order works after transfer.");
        state.Enter(FastFoodStationMode.Grill);
        var next=new FastFoodCookingState.Portion{recipe=recipes[0],stage=FastFoodCookingStage.Preparing,proteinReady=true,player=true};
        state.Portions.Add(next);state.Tick(.01f,false,false);
        Check(state.Load(next,FastFoodCookingState.AssemblySteps(next.recipe)[0].item,0),"Freed slot reusable.");
        var pending=new FastFoodCookingState(_=>1000,_=>true);
        var waitingFood=new FastFoodCookingState.Portion{recipe=recipes[0],stage=FastFoodCookingStage.Preparing,proteinReady=true,player=true};
        pending.Portions.Add(waitingFood);pending.Enter(FastFoodStationMode.Grill);
        var waitingSteps=FastFoodCookingState.AssemblySteps(waitingFood.recipe);
        Check(pending.Load(waitingFood,waitingSteps[0].item,1),"Start prep before the customer order is assigned.");
        Check(pending.Accept(9877,new[]{waitingFood.recipe}),"Assign an inactive order.");
        while(waitingFood.stage==FastFoodCookingStage.Preparing)
            Check(pending.Load(waitingFood,waitingSteps[waitingFood.ingredientStep].item,1),"Finish food assigned to an inactive order.");
        Check(waitingFood.prepSlot<0 && !waitingFood.placed && waitingFood.order==9877 && !pending.FindTicket(9877).active,"Inactive order keeps its food without occupying the board.");
        pending.Enter(FastFoodStationMode.Assembler);pending.Activate(9877);
        Check(pending.BeginServingDrag(waitingFood) && pending.Place(waitingFood) && pending.Serve(9877),"Food for an inactive order remains usable after activation.");
        bool allowConsumption=false;int failedPickups=0;
        var failed=new FastFoodCookingState(_=>1000,_=>allowConsumption);
        var unfinished=new FastFoodCookingState.Portion{recipe=recipes[0],stage=FastFoodCookingStage.Preparing,proteinReady=true,player=true};
        failed.Portions.Add(unfinished);failed.Enter(FastFoodStationMode.Grill);
        failed.PrepTransferred+=(p,slot)=>failedPickups++;
        while(unfinished.ingredientStep<waitingSteps.Count-1)
            Check(failed.Load(unfinished,waitingSteps[unfinished.ingredientStep].item,0),"Build food before failed consumption.");
        Check(!failed.Load(unfinished,waitingSteps.Last().item,0) && unfinished.stage==FastFoodCookingStage.Preparing &&
            failed.AtPrepSlot(0)==unfinished && failedPickups==0,"Failed ingredient transaction preserves unfinished food and slot.");
        allowConsumption=true;
        Check(failed.Load(unfinished,waitingSteps.Last().item,0) && failed.AtPrepSlot(0)==null && failedPickups==1,"Successful retry releases the slot exactly once.");
        // Previously stuck complete portions are also released on the next ordinary update.
        unfinished.prepSlot=2;
        failed.Tick(.01f,true,false);
        Check(unfinished.prepSlot<0 && failed.ReadyStock(unfinished.recipe)==1 && failedPickups==2,"Previously retained reserve food is collected without being lost.");
        failed.Tick(.01f,true,false);
        Check(failedPickups==2,"Ordinary updates cannot repeat a completed pickup.");
        foreach(var recipe in recipes)
        {
            var flow=new FastFoodCookingState(_=>1000,_=>true);
            var food=new FastFoodCookingState.Portion{recipe=recipe,stage=FastFoodCookingStage.Waiting};
            flow.Portions.Add(food);flow.Enter(FastFoodStationMode.Grill);
            var steps=FastFoodCookingState.AssemblySteps(recipe);
            if(FastFoodCookingState.Station(recipe)==FastFoodStationMode.Fry)
            {
                Check(food.stage==FastFoodCookingStage.Waiting && !food.player,"Staff owns uncooked sandwich protein.");
                flow.Tick(flow.cookSeconds,false,true);
                Check(food.stage==FastFoodCookingStage.Waiting,"Pause prevents staff cooking.");
                flow.Tick(flow.cookSeconds/flow.staffMultiplier+.01f,false,false);
                flow.Tick(.01f,false,false);
                flow.Tick(.01f,false,false);
                Check(flow.Mode==FastFoodStationMode.Grill && food.proteinReady && food.player,"Staff returns protein without leaving Grill.");
                Check(flow.Load(food,steps[0].item,2),"Player starts sandwich after staff cooking.");
            }
            else
            {
                var raw=FastFoodCookingState.Steps(recipe)[0].item;
                Check(food.stage==FastFoodCookingStage.Waiting && !flow.PrepReady,"Grill starts with cooking, before prep.");
                Check(flow.NextLoad(raw)==food && flow.Load(food,raw),"Raw patty loads through existing flow.");
                flow.Tick(flow.cookSeconds,true,true);
                Check(food.stage==FastFoodCookingStage.Cooking && food.elapsed==0,"Pause preserves cooking progress.");
                flow.Tick(flow.cookSeconds,true,false);
                flow.Tick(.1f,true,false);
                Check(food.stage==FastFoodCookingStage.Ready && !flow.PrepReady,"Ready patty waits for manual collection.");
                Check(flow.Collect(food),"Ready patty collected through existing flow.");
                Check(flow.Load(food,steps[0].item,2),"Prep starts after patty collection.");
            }
            Check(food.prepSlot==2 && food.ingredientStep==1 && food.proteinReady,"Cooked ingredient returns to original slot.");
            Check(flow.PrepPortion(steps[1].item,2)==food && flow.Load(food,steps[1].item,2),"Original slot resumes with cooked ingredient.");
            Check(flow.Accept(9981,new[]{recipe}),"Assign food before completion.");
            flow.Activate(9981);
            int pickups=0;flow.PrepTransferred+=(p,slot)=>pickups++;
            while(food.stage==FastFoodCookingStage.Preparing)Check(flow.Load(food,steps[food.ingredientStep].item,2),"Finish returned food.");
            Check(food.placed && flow.AtPrepSlot(2)==null && pickups==1,"Completion transfers immediately without a tick or hold.");
            Check(!flow.Tickets.Single().submitted,"Transfer does not submit an order.");
            var replacement=new FastFoodCookingState.Portion{recipe=recipe,stage=FastFoodCookingStage.Preparing,proteinReady=true,player=true};
            flow.Portions.Add(replacement);flow.Tick(.01f,false,false);
            Check(flow.Load(replacement,steps[0].item,2),"Slot can immediately start another sandwich.");
        }
        var staffRecipes=recipes.Where(r=>FastFoodCookingState.Station(r)==FastFoodStationMode.Fry).ToArray();
        Check(staffRecipes.Length>=2,"Chicken and fish sandwich recipes are covered.");
        var mixed=new FastFoodCookingState(_=>1000,_=>true);
        var sandwiches=Enumerable.Range(0,3).Select(i=>new FastFoodCookingState.Portion{recipe=staffRecipes[i%staffRecipes.Length],stage=FastFoodCookingStage.Waiting}).ToArray();
        mixed.Portions.AddRange(sandwiches);mixed.Enter(FastFoodStationMode.Grill);
        for(int tick=0;tick<12 && sandwiches.Any(p=>!p.proteinReady);tick++)
            mixed.Tick(mixed.cookSeconds/mixed.staffMultiplier+.01f,false,false);
        mixed.Tick(.01f,false,false);
        Check(mixed.Mode==FastFoodStationMode.Grill && sandwiches.All(p=>p.proteinReady && p.player),"Staff cooks all three mixed sandwiches while player stays at Grill.");
        for(int i=0;i<3;i++)Check(mixed.Load(sandwiches[i],FastFoodCookingState.AssemblySteps(sandwiches[i].recipe)[0].item,i),"Start mixed sandwich slot "+i);
        Check(sandwiches.Select(p=>p.prepSlot).Distinct().Count()==3,"Three staff-supplied sandwiches assemble independently.");
        var burger=recipes.First(r=>r.kitchenItemType==ItemTypeKitchen.Burger);
        var grill=new FastFoodCookingState(_=>1000,_=>true){grillSlots=rig.Slots.Length};
        Check(grill.grillSlots>=3,"Authored Grill supports simultaneous patties.");
        var patties=Enumerable.Range(0,4).Select(_=>new FastFoodCookingState.Portion{recipe=burger,stage=FastFoodCookingStage.Waiting}).ToArray();
        grill.Portions.AddRange(patties);grill.Enter(FastFoodStationMode.Grill);
        var patty=FastFoodCookingState.Steps(burger)[0].item;
        Check(grill.PlayerWork.Count==4 && patties.All(p=>p.stage==FastFoodCookingStage.Waiting),"The whole batch stays available for cooking.");
        Check(grill.Load(grill.NextLoad(patty),patty,0) && grill.Load(grill.NextLoad(patty),patty,1),"Load two patties without waiting for collection.");
        grill.Tick(.25f,false,false);
        Check(grill.Load(grill.NextLoad(patty),patty,2),"Load a third while others are cooking.");
        Check(patties.Take(3).All(p=>p.stage==FastFoodCookingStage.Cooking),"Three patties cook concurrently.");
        grill.Tick(grill.cookSeconds-.25f,false,false);
        grill.Tick(.01f,false,false);
        Check(patties[0].stage==FastFoodCookingStage.Ready && patties[1].stage==FastFoodCookingStage.Ready &&
            patties[2].stage==FastFoodCookingStage.Cooking,"Ready patties stay on the Grill while another cooks.");
        Check(grill.Collect(patties[0]) && !grill.PrepReady && grill.HasPendingCooking,"One tap collects only one patty without opening prep.");
        Check(grill.AtSlot(FastFoodStationMode.Grill,1)==patties[1] && grill.Load(grill.NextLoad(patty),patty,0),"Freed cooking slot reloads while other patties remain.");
        Check(grill.Collect(patties[1]),"Second patty requires its own collection.");
        grill.Tick(.25f,false,false);
        Check(grill.Collect(patties[2]) && !grill.PrepReady,"Prep remains closed while last patty cooks.");
        grill.Tick(grill.cookSeconds,false,false);grill.Tick(.01f,false,false);
        Check(patties[3].stage==FastFoodCookingStage.Ready && !grill.PrepReady,"Last ready patty still requires a tap.");
        Check(grill.Collect(patties[3]) && grill.PrepReady && !grill.HasPendingCooking,"Prep opens only when the full batch is collected.");
        var bun=FastFoodCookingState.AssemblySteps(burger)[0].item;
        for(int i=0;i<3;i++)Check(grill.Load(patties[i],bun,i),"Start burger prep slot "+i);
        Check(!grill.Load(patties[3],bun,0) && patties.Take(3).Select(p=>p.prepSlot).Distinct().Count()==3,"Three prep slots keep independent portions and reject an occupied slot.");
        Debug.Log("[Prep slots] PASS: all three reserve sandwiches release their slots immediately; later orders reuse ready stock; inactive and active orders work; failed consumption preserves the slot; pickups and consumption occur once; parallel patties/manual collection/staff chicken/fish/explicit player Serve Order preserved. Isolated ledger checks; no Play Mode.");
    }

    [MenuItem("Dine In/Fast Food/Validate Stable Ingredient Hotbar")]
    public static void ValidateHotbar()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Use Edit Mode.");
        var source=UnityEngine.Object.FindObjectsByType<FastFoodCookingView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        object Read(object target,string name)=>target.GetType().GetField(name,flags).GetValue(target);
        void Write(object target,string name,object value)=>target.GetType().GetField(name,flags).SetValue(target,value);
        object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,flags).Invoke(target,args);
        void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("[Stable hotbar] "+why);}
        var template=(FastFoodCookingDragHandle)Read(source,"ingredientTemplate");
        var recipes=MenuCatalog.Default.Products.Where(r=>r!=null && r.category==MenuProductCategory.Food &&
            FastFoodCookingState.PreparationStation(r)==FastFoodStationMode.Grill).OrderBy(r=>r.menuSortOrder).ToArray();
        var burger=recipes.First(r=>r.kitchenItemType==ItemTypeKitchen.Burger);
        var fishRecipe=recipes.First(r=>r.kitchenItemType==ItemTypeKitchen.FishFilletSandwich);
        var fishItem=FastFoodCookingState.Steps(fishRecipe)[0].item;
        var item=FastFoodCookingState.Steps(burger)[0].item;
        var rig=UnityEngine.Object.FindObjectsByType<FastFoodCookingStation>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .First(r=>r.mode==FastFoodStationMode.Grill);
        foreach(float width in new[]{1920f,1728f,2520f,2340f,1440f})
        {
            var root=new GameObject("Isolated hotbar layout check",typeof(RectTransform));
            root.SetActive(false);
            var extraItems=Enumerable.Range(0,6).Select(_=>ScriptableObject.CreateInstance<ItemData>()).ToArray();
            var previousInventory=InventoryManager.Instance;
            try
            {
                var parent=(RectTransform)root.transform;parent.sizeDelta=new Vector2(width,1080);
                var controller=root.AddComponent<FastFoodCookingController>();
                var state=new FastFoodCookingState(_=>100,_=>true);
                state.EnsureReserve(new[]{burger},1);state.Enter(FastFoodStationMode.Grill);
                var patty=state.NextLoad(item);
                Check(state.Load(patty,item,0),"Initial patty loads.");
                state.Tick(state.cookSeconds+.01f,false,false);
                Check(state.Collect(patty),"Initial patty is manually collected.");
                typeof(FastFoodCookingController).GetProperty("State").SetValue(controller,state);
                var view=root.AddComponent<FastFoodCookingView>();Write(view,"owner",controller);
                Write(view,"activeStation",rig);
                var inventory=root.AddComponent<InventoryManager>();inventory.enabled=false;
                Write(inventory,"inventory",recipes.SelectMany(FastFoodCookingState.Steps).Select(s=>s.item.itemType).Distinct().ToDictionary(t=>t,_=>100));
                InventoryManager.Instance=inventory;
                var container=UnityEngine.Object.Instantiate((RectTransform)Read(source,"hotbarContainer"),parent,false);
                var hotbar=container.GetComponent<UnityEngine.UI.ScrollRect>().content;
                foreach(Transform child in hotbar.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
                Write(view,"hotbar",hotbar);Write(view,"hotbarContainer",container);Write(view,"ingredientTemplate",template);
                foreach(var field in new[]{"hotbarMaximumWidth","hotbarScreenFraction","hotbarCellRange"})Write(view,field,Read(source,field));
                controller.enabled=false;view.enabled=false;root.SetActive(true);
                // Include the unlocked day-25 station menu even when Edit Mode has no saved day.
                Call(view,"EnsureProductionRecipes",(object)recipes);
                Call(view,"UpdateProductionHotbar");
                Call(view,"FitHotbar");
                int initialCount=hotbar.childCount;
                Check(initialCount==6,"Burger, chicken and fish ingredients occupy six stable cells.");
                var fishCell=hotbar.GetComponentsInChildren<FastFoodCookingDragHandle>().Single(c=>c.item==fishItem && c.storedProtein);
                Vector3 fishPosition=fishCell.transform.position;
                Check(!fishCell.enabled,"Cooked fish is unavailable before its staff delivery.");
                var scroll=container.GetComponent<UnityEngine.UI.ScrollRect>();
                var grid=hotbar.GetComponent<UnityEngine.UI.GridLayoutGroup>();
                Check(grid.constraint==UnityEngine.UI.GridLayoutGroup.Constraint.FixedRowCount && grid.constraintCount==1,"Ingredient bar must remain one row.");
                Check(scroll.horizontal && !scroll.vertical,"Horizontal overflow scrolling is enabled.");
                var first=(RectTransform)hotbar.GetChild(0);var second=(RectTransform)hotbar.GetChild(1);
                Vector3 firstPosition=first.position,secondPosition=second.position;
                float rowY=first.position.y,height=container.rect.height;
                foreach(RectTransform cell in hotbar)
                {
                    Check(Mathf.Abs(cell.position.y-rowY)<.01f,"All station ingredients must share one row.");
                    var corners=new Vector3[4];cell.GetWorldCorners(corners);
                    Check(corners.All(p=>scroll.viewport.rect.Contains(scroll.viewport.InverseTransformPoint(p))),"All six station cells must fit inside the viewport.");
                }
                // Reproduce a delivery after the bar was sized for the current burger work.
                state.EnsureReserve(recipes.Where(r=>FastFoodCookingState.Station(r)==FastFoodStationMode.Fry),1);
                state.Tick(state.cookSeconds/state.staffMultiplier+.01f,false,false);
                state.Tick(.01f,false,false);state.Tick(.01f,false,false);
                var fish=state.PlayerWork.Single(p=>p.recipe==fishRecipe);
                var fishSteps=FastFoodCookingState.AssemblySteps(fishRecipe);
                Check(state.Mode==FastFoodStationMode.Grill && state.Load(fish,fishSteps[0].item,1),"Staff delivers fish and the bun can be placed without switching stations.");
                Call(view,"UpdateProductionHotbar");
                Check(fishCell.enabled && fishCell.portion==fish && fishCell.count.text==string.Format((string)Read(view,"storedProteinFormat"),1),"Existing fish cell updates its quantity and becomes draggable on the next refresh.");
                Check(hotbar.childCount==initialCount && fishCell.transform.position==fishPosition && first.position==firstPosition,"Staff delivery must not append hidden cells or move existing targets.");
                Check(state.Load(fish,fishItem,1),"Delivered fish can be placed on its sandwich.");
                Call(view,"UpdateProductionHotbar");
                Check(!fishCell.enabled && fishCell.count.text==string.Format((string)Read(view,"storedProteinFormat"),0),"Using fish updates its existing cell to zero.");
                Check(state.Load(fish,fishSteps.Last().item,1) && state.AtPrepSlot(1)==null,"Finished sandwich releases its prep slot.");
                state.EnsureReserve(new[]{fishRecipe},2);
                state.Tick(state.cookSeconds/state.staffMultiplier+.01f,false,false);
                state.Tick(.01f,false,false);state.Tick(.01f,false,false);
                var nextFish=state.PlayerWork.Single(p=>p.recipe==fishRecipe && p.stage==FastFoodCookingStage.Preparing);
                Check(state.Load(nextFish,fishSteps[0].item,1),"A second delivery can reuse the prep slot.");
                Call(view,"UpdateProductionHotbar");
                Check(fishCell.enabled && fishCell.portion==nextFish && fishCell.transform.position==fishPosition,"A repeat delivery re-enables the same fish cell without a station switch.");
                Call(view,"EnsureProductionCell",item,true,false,item.sprite,null);
                Call(view,"FitHotbar");
                Check(hotbar.childCount==initialCount && hotbar.GetChild(0)==first && first.position==firstPosition,"Ordinary refresh reuses cells.");
                foreach(var extra in extraItems)Call(view,"EnsureProductionCell",extra,false,false,item.sprite,null);
                Call(view,"FitHotbar");
                Check(Vector3.Distance(first.position,firstPosition)<.01f && Vector3.Distance(second.position,secondPosition)<.01f,"Appending an ingredient moved existing targets at width "+width);
                Check(Mathf.Abs(container.rect.height-height)<.01f && hotbar.rect.width>scroll.viewport.rect.width,"Extra ingredients extend horizontally without growing the panel.");
                foreach(RectTransform cell in hotbar)
                    Check(Mathf.Abs(cell.position.y-rowY)<.01f,"Overflow must not wrap to another row.");
                var frameCorners=new Vector3[4];container.GetWorldCorners(frameCorners);
                Check(frameCorners.All(p=>parent.rect.Contains(parent.InverseTransformPoint(p))),"The hotbar frame must stay inside the screen.");
                hotbar.anchoredPosition=new Vector2(-20,0);
                Call(view,"FitHotbar");
                Check(Mathf.Abs(hotbar.anchoredPosition.x+20)<.01f,"Refresh resets horizontal scroll offset.");
                scroll.horizontalNormalizedPosition=1;
                var last=(RectTransform)hotbar.GetChild(hotbar.childCount-1);
                var lastCorners=new Vector3[4];last.GetWorldCorners(lastCorners);
                Check(lastCorners.All(p=>scroll.viewport.rect.Contains(scroll.viewport.InverseTransformPoint(p))),"Last ingredient must be reachable by scrolling.");
            }
            finally
            {
                InventoryManager.Instance=previousInventory;
                UnityEngine.Object.DestroyImmediate(root);
                foreach(var extra in extraItems)UnityEngine.Object.DestroyImmediate(extra);
            }
        }
        Debug.Log("[Stable hotbar] PASS: six station ingredients visible at desktop/phone/tablet widths; late and repeat staff fish deliveries update the same visible, draggable cell without station switches; consumption and prep-slot reuse work; overflow stays one row with stable targets and scroll offset. Isolated state and authored-layout checks; no Play Mode/device test.");
    }
}
#endif
