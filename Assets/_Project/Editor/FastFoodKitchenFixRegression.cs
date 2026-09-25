#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit checks in disposable preview scenes. Never loads or saves a campaign.
public static class FastFoodKitchenFixRegression
{
    static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("[Kitchen fixes] " + message); }

    [MenuItem("Dine In/Fast Food/Check Kitchen Fix Regressions")]
    public static void Run()
    {
        Require(!Application.isPlaying, "Run outside Play Mode.");
        CheckKitchenHandoff();
        var scene = EditorSceneManager.NewPreviewScene();
        var oldInventory = InventoryManager.Instance;
        GameObject Create(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
        try
        {
            var inventory = Create("Test inventory").AddComponent<InventoryManager>();
            InventoryManager.Instance = inventory;
            var item = MenuCatalog.Default.Ingredients.First(i => i != null && i.worldContainerPrefab != null);
            var catalog = new[] { item };
            inventory.ConfigureItems(catalog.ToList(), false);
            int size = Mathf.Max(1, item.unitsPerBox);
            inventory.AddStockBatch(item, size * 2 + 1, 3, out string originalID, out int expiry);
            var manager = Create("Test storage").AddComponent<RestockOrderManager>();
            var shelf = new RestockOrderManager.StorageShelf { id="test-shelf", storage=item.requiredStorage, columns=5, rows=1 };
            var save = new GameSaveData();
            save.restockStoredContainers.Add(new RestockStoredContainerSaveData { containerID="dead", stockBatchID="missing", shelfID=shelf.id });
            manager.ApplySaveData(save);
            manager.ReconcileStorageRecords(new[] { shelf }, catalog);
            Require(inventory.GetStock(item.itemType) == size * 2 + 1, "Recovery changed stock units.");
            Require(inventory.TryGetBatch(originalID, out _), "Recovery replaced an existing batch ID.");
            Require(inventory.StockBatches.All(b => b.expiresDay == expiry && b.receivedDay == 3), "Recovery reset expiry or received day.");
            Require(manager.StoredContainers.Count == 3 && inventory.GetStorageContainerCount(item.requiredStorage, catalog) == 3,
                "Starter stock and partial containers disagree with visible capacity.");
            Require(manager.StoredContainers.Select(e => e.column).Distinct().Count() == 3, "Recovered boxes overlap.");
            string ids = string.Join(",", manager.StoredContainers.Select(e => e.containerID));
            manager.ReconcileStorageRecords(new[] { shelf }, catalog);
            Require(ids == string.Join(",", manager.StoredContainers.Select(e => e.containerID)), "Reopening duplicated/reassigned stock.");
            inventory.DiscardContainerStock(item.itemType, size, originalID);
            manager.ReconcileStorageRecords(new[] { shelf }, catalog);
            Require(manager.StoredContainers.Count == 2, "An empty box still occupies storage.");
            var otherRoom = item.requiredStorage == RestockStorageType.Dry ? RestockStorageType.Frozen : RestockStorageType.Dry;
            inventory.UpdateBatchStorage(inventory.StockBatches[0].batchID, otherRoom, true, 1f);
            Require(inventory.GetStorageContainerCount(otherRoom, catalog) == 1 && inventory.GetStorageContainerCount(item.requiredStorage, catalog) == 1,
                "Capacity ignores a box's actual room.");

            var bot = Create("Test delivery worker").AddComponent<AutonomousStaffBot>();
            var tray = Create("Test tray").AddComponent<FoodTray>();
            try
            {
                Require(RestaurantTaskClaim.TryClaimBot(tray, bot, 0), "Could not acquire test claim.");
                RestaurantTaskClaim.ReleaseJob(bot, bot.JobGeneration + 1);
                Require(!RestaurantTaskClaim.IsClaimedByBot(tray), "Single-player interruption leaked its claim.");
                Require(RestaurantTaskClaim.TryClaimBot(tray, bot, 0), "Released delivery cannot be retried.");
                typeof(AutonomousStaffBot).GetField("<JobGeneration>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bot, bot.JobGeneration + 1);
                var service = Create("Test service").AddComponent<LobbyAutonomousService>();
                var task = (IEnumerator)typeof(LobbyAutonomousService).GetMethod("RunClaimedTask", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(service, new object[] { tray, bot, EmptyTask() });
                Require(task.MoveNext(), "Claimed task did not yield its work.");
                ((IDisposable)task).Dispose();
                Require(!RestaurantTaskClaim.IsClaimedByBot(tray), "Disposing claimed work leaked ownership.");
                var hands = bot.gameObject.AddComponent<WaiterHands>();
                Require(hands.PickupTray(tray), "Could not stage a carried tray.");
                typeof(AutonomousStaffBot).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bot, null);
                Require(!hands.HasTray && !tray.transform.IsChildOf(bot.transform), "An interrupted worker kept its carried tray.");
            }
            finally { RestaurantTaskClaim.Complete(tray); }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            InventoryManager.Instance = oldInventory;
        }
        CheckTicket();
        Debug.Log("[Kitchen fixes] PASS: stock conservation/expiry, partial boxes, recovery idempotence, depleted boxes, actual storage room, single-player claim release, ticket rows.");
    }
    static IEnumerator EmptyTask() { yield return null; }

    static void CheckKitchenHandoff()
    {
        var catalog=MenuCatalog.Default;
        foreach(var mode in new[]{FastFoodStationMode.Grill,FastFoodStationMode.Fry,FastFoodStationMode.Assembler})
        {
            var recipe=catalog.Products.First(r=>r!=null && r.category==MenuProductCategory.Food &&
                FastFoodCookingState.Station(r)==(mode==FastFoodStationMode.Fry?mode:FastFoodStationMode.Grill));
            var drink=catalog.Products.First(r=>r!=null && r.category==MenuProductCategory.Drink);
            int consumed=0;
            var state=new FastFoodCookingState(_=>1000,_=>{consumed++;return true;});
            Require(state.Accept(99,new[]{recipe,drink}), "Handoff order was rejected.");
            state.Activate(99); state.Enter(mode);
            if(mode!=FastFoodStationMode.Assembler)
            {
                Require(state.Load(state.PlayerPortion,FastFoodCookingState.Steps(recipe)[0].item), "Player cooking could not begin.");
                Require(consumed==0, "Loading raw food consumed ingredients prematurely.");
            }
            else
            {
                var portion=state.PlayerTicket.portions.First(p=>p.recipe==drink);
                Require(state.BeginServingDrag(portion) && state.Place(portion), "Player assembly could not place a drink.");
            }
            state.Enter(FastFoodStationMode.None);
            for(int i=0;i<60 && !state.FindTicket(99).submitted;i++) state.Tick(.5f,false,false);
            Require(state.FindTicket(99).submitted && consumed==2, mode+" exit stranded or duplicated the order.");
            Require(state.PlayerPortion==null && state.PlayerTicket==null, "Kitchen exit retained a player assignment.");
        }
    }

    public static void CheckTicket()
    {
        var root = PrefabUtility.LoadPrefabContents(FastFoodCookingAuthoring.TemplateFolder + "/Order Ticket.prefab");
        try
        {
            var ticket = root.GetComponent<FastFoodCookingTicketView>();
            var viewport = (RectTransform)ticket.products.parent;
            var grid = ticket.products.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
            Require(viewport.rect.height >= grid.cellSize.y * 2 + grid.spacing.y + grid.padding.vertical,
                "Ticket clips the second product row.");
            Require(viewport.rect.width >= grid.cellSize.x * 3 + grid.spacing.x * 2 + grid.padding.horizontal,
                "Ticket clips its third column.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static string CaptureTicketPreview()
    {
        Require(!Application.isPlaying, "Preview outside Play Mode.");
        var scene=EditorSceneManager.NewPreviewScene();
        var texture=new RenderTexture(760,640,24);
        var previous=RenderTexture.active;
        Texture2D image=null;
        try
        {
            var cameraObject=new GameObject("Ticket preview camera",typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.GetComponent<Camera>(); camera.scene=scene;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.1f,.61f,.79f);
            camera.targetTexture=texture; camera.orthographic=true; camera.orthographicSize=320;
            var canvasObject=new GameObject("Ticket preview",typeof(RectTransform),typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasObject,scene);
            var canvas=canvasObject.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FastFoodCookingAuthoring.TemplateFolder+"/Order Ticket.prefab"),canvas.transform);
            root.SetActive(true);
            var rect=(RectTransform)root.transform; rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f); rect.anchoredPosition=Vector2.zero; rect.sizeDelta=new Vector2(360,300);
            var ticket=root.GetComponent<FastFoodCookingTicketView>(); ticket.title.text="ORDER #6"; ticket.timer.text="4:56";
            foreach(var recipe in MenuCatalog.Default.Products.Where(r=>r!=null && r.sprite!=null).Take(5))
            {
                var product=UnityEngine.Object.Instantiate(ticket.productTemplate,ticket.products);
                product.gameObject.SetActive(true); product.enabled=false; product.icon.sprite=recipe.sprite; product.count.text="0/2";
            }
            Canvas.ForceUpdateCanvases(); UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            camera.Render(); RenderTexture.active=texture;
            image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); image.Apply();
            string path=System.IO.Path.GetFullPath("Temp/KitchenTicketPreview.png");
            System.IO.File.WriteAllBytes(path,image.EncodeToPNG()); return path;
        }
        finally
        {
            RenderTexture.active=previous;
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(texture);
            if(image!=null)UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
#endif
