// Temporary Edit-mode UI instances only; no Apply, saves, network, or menu navigation.
if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
int assertions=0;void Check(bool condition,string message){assertions++;if(!condition)throw new System.Exception(message);}
var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/NewMenu/NewGameMenu.unity");
var layouts=new System.Collections.Generic.List<object>();
try
{
    var objects=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();var ui=objects.Select(t=>t.GetComponent<DineIn.Appearance.AppearanceCustomizationPanel>()).Single(c=>c!=null);
    var data=new SerializedObject(ui);var camera=(Camera)data.FindProperty("previewCamera").objectReferenceValue;camera.scene=scene;camera.enabled=false;
    var canvas=ui.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();var reference=scaler.referenceResolution;
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    void Call(string method,params object[] args)=>ui.GetType().GetMethod(method,flags).Invoke(ui,args);
    // UI-only lifecycle check: isolate menu travel/coroutines on this disposable scene copy.
    data.FindProperty("restaurantSelector").objectReferenceValue=null;data.ApplyModifiedPropertiesWithoutUndo();Call("Awake");
    Rect Bounds(RectTransform t)
    {var corners=new Vector3[4];t.GetWorldCorners(corners);var a=camera.WorldToViewportPoint(corners[0]);var b=camera.WorldToViewportPoint(corners[2]);return Rect.MinMaxRect(a.x,a.y,b.x,b.y);}
    var grid=((RectTransform)data.FindProperty("options").objectReferenceValue).GetComponent<UnityEngine.UI.GridLayoutGroup>();
    foreach(float scale in new[]{.85f,1f,1.15f})foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(1280,576)})
    {
        var rt=new RenderTexture(size.x,size.y,24);rt.Create();camera.targetTexture=rt;camera.aspect=(float)size.x/size.y;scaler.referenceResolution=reference/scale;
        try
        {
            Canvas.ForceUpdateCanvases();camera.Render();
            foreach(var name in new[]{"BackButton","ShopButton","CurrencyBar","PlayButton","PrevButton","NextButton","Customize"})
            {
                var rect=objects.OfType<RectTransform>().First(t=>t.name==name && (name!="BackButton"||t.parent==canvas.transform));var bounds=Bounds(rect);
                Check(rect.gameObject.activeInHierarchy&&bounds.xMin>=0&&bounds.yMin>=0&&bounds.xMax<=1.001f&&bounds.yMax<=1.001f,"Clipped HUD: "+name+" scale="+scale+" size="+size+" bounds="+bounds);
            }
            var shop=Bounds((RectTransform)canvas.transform.Find("ShopButton"));var wallet=Bounds((RectTransform)canvas.transform.Find("WalletUIController/MoneyUI/CurrencyBar"));Check(!shop.Overlaps(wallet),"Currency covers Shop");
            var controlRefs=data.FindProperty("menuControls");var controls=Enumerable.Range(0,controlRefs.arraySize).Select(i=>(UnityEngine.UI.Selectable)controlRefs.GetArrayElementAtIndex(i).objectReferenceValue).Where(c=>c!=null).ToArray();var before=controls.Select(c=>(c.gameObject.activeSelf,c.interactable)).ToArray();
            var siblings=canvas.transform.Cast<Transform>().Select(t=>t.gameObject).ToArray();var visible=siblings.Select(g=>g.activeSelf).ToArray();
            ui.Open();Call("SelectCategory",5);Canvas.ForceUpdateCanvases();Call("ResizeGrid");
            Check(grid.constraintCount>=2&&grid.constraintCount<=3,"Model cards must use 2-3 columns at supported landscape sizes");Check(grid.cellSize==new Vector2(112,122),"Cards stretched to fill few options");
            foreach(var label in grid.GetComponentsInChildren<TMPro.TMP_Text>())
            {label.ForceMeshUpdate();Check(label.textInfo.lineCount<=2&&!label.isTextOverflowing,"Option label overflow: "+label.text+" lines="+label.textInfo.lineCount+" rect="+label.rectTransform.rect+" preferred="+label.GetPreferredValues(label.text,label.rectTransform.rect.width,1000)+" margin="+label.margin);}
            layouts.Add(new{scale,size=size.ToString(),columns=grid.constraintCount});
            Call("SelectCategory",4);Check(grid.cellSize==new Vector2(58,64),"Palette swatches enlarged");
            ui.Cancel();
            Check(controls.Select((c,i)=>c.gameObject.activeSelf==before[i].Item1&&c.interactable==before[i].Item2).All(v=>v),"Control visibility/interactability not restored");
            Check(siblings.Select((g,i)=>g.activeSelf==visible[i]).All(v=>v),"Menu presentation not restored");
        }
        finally{camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
    }
    // Restoration must preserve deliberately hidden/disabled controls, not enable everything.
    var shopButton=canvas.transform.Find("ShopButton").gameObject;shopButton.SetActive(false);var play=objects.First(t=>t.name=="PlayButton").GetComponent<UnityEngine.UI.Button>();play.interactable=false;
    ui.Open();ui.Cancel();Check(!shopButton.activeSelf&&!play.interactable,"Cancel overwrote prior hidden/disabled state");
}
finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
return new{assertions,layouts,scope="Edit-mode layout/lifecycle checks only"};
