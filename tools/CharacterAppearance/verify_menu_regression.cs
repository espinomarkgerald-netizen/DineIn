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
    var grid=((RectTransform)data.FindProperty("options").objectReferenceValue).GetComponent<DineIn.Appearance.CosmeticOptionLayout>();
    foreach(float scale in new[]{.85f,1f,1.15f})foreach(var size in new[]{new Vector2Int(3840,2160),new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1024,768)})
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
            Check(grid.Columns==2,"Model cards must use two columns at supported landscape sizes");Check(grid.CellSize==new Vector2(134,176),"Authored model card dimensions changed");
            foreach(var label in grid.GetComponentsInChildren<TMPro.TMP_Text>())
            {label.ForceMeshUpdate();Check(label.textInfo.lineCount<=2&&!label.isTextOverflowing,"Option label overflow: "+label.text+" lines="+label.textInfo.lineCount+" rect="+label.rectTransform.rect+" preferred="+label.GetPreferredValues(label.text,label.rectTransform.rect.width,1000)+" margin="+label.margin);}
            layouts.Add(new{scale,size=size.ToString(),columns=grid.Columns});
            Call("SelectCategory",4);Canvas.ForceUpdateCanvases();Check(grid.CellSize==new Vector2(52,58),"Palette swatches stretched");
            foreach(int category in new[]{0,1,2,3,4,5,6})
            {
                Call("SelectCategory",category);Canvas.ForceUpdateCanvases();Call("ResizeGrid");Canvas.ForceUpdateCanvases();
                var cells=grid.transform.Cast<Transform>().Where(t=>t.gameObject.activeSelf).Cast<RectTransform>().ToArray();
                var scroller=(UnityEngine.UI.ScrollRect)data.FindProperty("scroll").objectReferenceValue;
                scroller.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
                if(cells.Length>0){var last=Bounds(cells[cells.Length-1]);var view=Bounds(scroller.viewport);Check(last.yMin>=view.yMin-.002f&&last.yMax<=view.yMax+.002f,"Last option cannot scroll fully into view");}
                scroller.verticalNormalizedPosition=1;Canvas.ForceUpdateCanvases();
                foreach(var cell in cells)
                {
                    var icon=cell.Find("Icon").GetComponent<UnityEngine.UI.Image>();
                    Check(icon.type==UnityEngine.UI.Image.Type.Simple,"Sliced image ignores thumbnail aspect ratio");
                    var label=cell.Find("Label").GetComponent<TMPro.TMP_Text>();
                    if(label.gameObject.activeSelf){label.ForceMeshUpdate();Check(!label.isTextOverflowing,"Clipped category label: "+label.text);}
                }
                for(int row=0;row<cells.Length;row+=grid.Columns)
                {
                    int end=Mathf.Min(row+grid.Columns,cells.Length)-1;
                    float left=cells[row].anchoredPosition.x-cells[row].rect.width*cells[row].pivot.x;
                    float right=cells[end].anchoredPosition.x+cells[end].rect.width*(1-cells[end].pivot.x);
                    Check(Mathf.Abs(left+right-((RectTransform)grid.transform).rect.width)<.1f,"Incomplete row is not centered");
                }
                foreach(string field in new[]{"applyButton","cancelButton"})
                {var b=Bounds((RectTransform)((UnityEngine.UI.Button)data.FindProperty(field).objectReferenceValue).transform);Check(b.xMin>=0&&b.xMax<=1&&b.yMin>=0&&b.yMax<=1,"Footer outside screen");}
            }
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
