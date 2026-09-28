if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
const string path="Assets/_Project/Scenes/NewMenu/NewGameMenu.unity";
var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
if(!opened&&scene.isDirty)throw new System.Exception("Save NewGameMenu edits first.");
if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
try
{
    var ui=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DineIn.Appearance.AppearanceCustomizationPanel>(true)).Single();var data=new SerializedObject(ui);var canvas=ui.transform;
    void Fixed(Transform target,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
    {var r=(RectTransform)target;r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=position;r.sizeDelta=size;}
    void Fill(Transform target,Vector2 min,Vector2 max)
    {var r=(RectTransform)target;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
    Fixed(canvas.Find("BackButton"),new Vector2(0,1),new Vector2(0,1),new Vector2(20,-10),new Vector2(50,50));
    Fixed(canvas.Find("ShopButton"),Vector2.one,Vector2.one,new Vector2(-20,-10),new Vector2(50,50));
    Fixed(canvas.Find("Customize"),new Vector2(1,0),new Vector2(1,0),new Vector2(-20,30),new Vector2(160,46));
    var wallet=canvas.Find("WalletUIController");Fill(wallet,Vector2.zero,Vector2.one);
    var money=wallet.Find("MoneyUI");Fixed(money,Vector2.one,Vector2.one,new Vector2(-82,-10),new Vector2(200,50));
    Fill(money.Find("CurrencyBar"),Vector2.zero,Vector2.one);
    Fill(money.Find("GemsText"),new Vector2(.16f,.08f),new Vector2(.51f,.92f));
    Fill(money.Find("MoneyText"),new Vector2(.50f,.08f),new Vector2(.97f,.92f));
    Fixed(money.Find("WebsiteRedirectButton"),new Vector2(0,.5f),new Vector2(.5f,.5f),new Vector2(19,0),new Vector2(18,18));
    Fixed(money.Find("RefreshButton"),new Vector2(0,.5f),new Vector2(.5f,.5f),new Vector2(-29,0),new Vector2(28,28));money.Find("RefreshButton").localScale=Vector3.one;
    Sprite Art(string color)=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/"+color+"/Double/button_rectangle_depth_border.png");
    var selected=Art("Blue");var neutral=Art("Grey");if(selected==null||neutral==null)throw new System.Exception("Matching tab artwork missing");
    var categories=data.FindProperty("categories");
    for(int i=0;i<categories.arraySize;i++)
    {
        var button=(UnityEngine.UI.Button)categories.GetArrayElementAtIndex(i).objectReferenceValue;var image=button.GetComponent<UnityEngine.UI.Image>();
        image.sprite=neutral;image.type=UnityEngine.UI.Image.Type.Sliced;image.pixelsPerUnitMultiplier=1;image.preserveAspect=false;
        var label=button.GetComponentInChildren<TMPro.TMP_Text>();label.alignment=TMPro.TextAlignmentOptions.Center;label.margin=new Vector4(3,2,3,2);
    }
    data.FindProperty("selectedTabSprite").objectReferenceValue=selected;
    data.FindProperty("optionCardSize").vector2Value=new Vector2(112,122);
    var template=(UnityEngine.UI.Button)data.FindProperty("optionTemplate").objectReferenceValue;
    Fill(template.transform.Find("Icon"),new Vector2(.10f,.49f),new Vector2(.90f,.90f));
    Fill(template.transform.Find("Label"),new Vector2(.06f,.12f),new Vector2(.94f,.46f));
    var text=template.transform.Find("Label").GetComponent<TMPro.TMP_Text>();text.fontSize=12;text.enableAutoSizing=false;text.textWrappingMode=TMPro.TextWrappingModes.Normal;text.alignment=TMPro.TextAlignmentOptions.Center;text.margin=new Vector4(2,1,2,1);
    var grid=((RectTransform)data.FindProperty("options").objectReferenceValue).GetComponent<UnityEngine.UI.GridLayoutGroup>();grid.cellSize=new Vector2(112,122);
    data.ApplyModifiedPropertiesWithoutUndo();
    // Safe-area baselines must reflect the new authored anchors, not restore the old centered ones.
    foreach(var safe in canvas.GetComponentsInChildren<UIScreenSafeArea>(true))
    {
        var safeData=new SerializedObject(safe);var targets=safeData.FindProperty("targets");
        for(int i=0;i<targets.arraySize;i++){var target=targets.GetArrayElementAtIndex(i);var rect=(RectTransform)target.FindPropertyRelative("rect").objectReferenceValue;if(rect==null)continue;target.FindPropertyRelative("anchorMin").vector2Value=rect.anchorMin;target.FindPropertyRelative("anchorMax").vector2Value=rect.anchorMax;}
        safeData.ApplyModifiedPropertiesWithoutUndo();
    }
    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    return new{scene=path,cards="112 x 122",categories="Matching depth-border sprites",hud="Corner anchored; separate wallet and Shop"};
}
finally{if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
