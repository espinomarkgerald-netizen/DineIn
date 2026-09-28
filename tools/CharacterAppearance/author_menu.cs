if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var path="Assets/_Project/Scenes/NewMenu/NewGameMenu.unity";
var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
if(!opened && scene.isDirty)throw new System.Exception("Save or discard your existing menu edits first.");
if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
try
{
    var transforms=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
    if(transforms.Any(t=>t.GetComponent<DineIn.Appearance.AppearanceCustomizationPanel>()!=null))return "Already authored; preserve Inspector edits.";
    var canvas=transforms.First(t=>t.name=="GameCanvas");
    var customize=transforms.First(t=>t.name=="Customize").GetComponent<UnityEngine.UI.Button>();
    var chef=scene.GetRootGameObjects().Single(g=>g.name=="Chef");
    // Normalize only this menu visual root, preserving the old model's exact world pose.
    var children=chef.transform.Cast<Transform>().Select(t=>new{t,pos=t.position,rot=t.rotation}).ToArray();
    chef.transform.rotation=Quaternion.identity;
    foreach(var child in children)child.t.SetPositionAndRotation(child.pos,child.rot);
    var appearance=chef.AddComponent<DineIn.Appearance.CharacterAppearance>();
    var binding=chef.AddComponent<DineIn.Appearance.PlayerAppearanceBinding>();
    var appearanceData=new SerializedObject(appearance);
    appearanceData.FindProperty("catalog").objectReferenceValue=DineIn.Appearance.AppearanceCatalog.Load();
    appearanceData.FindProperty("animator").objectReferenceValue=chef.GetComponent<Animator>();
    appearanceData.FindProperty("visualParent").objectReferenceValue=chef.transform;
    appearanceData.FindProperty("previewController").objectReferenceValue=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Resources/Player.controller");
    var originals=chef.GetComponentsInChildren<SkinnedMeshRenderer>(true);var originalList=appearanceData.FindProperty("originalRenderers");originalList.arraySize=originals.Length;
    for(int i=0;i<originals.Length;i++)originalList.GetArrayElementAtIndex(i).objectReferenceValue=originals[i];
    appearanceData.ApplyModifiedPropertiesWithoutUndo();
    var font=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Anton SDF.asset");
    var art="Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/";
    Sprite Sprite(string p)=>AssetDatabase.LoadAssetAtPath<Sprite>(art+p+".png");
    RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
        rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
    }
    UnityEngine.UI.Image Image(RectTransform rect,Sprite sprite,Color color)
    {var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=sprite;image.type=UnityEngine.UI.Image.Type.Sliced;image.color=color;return image;}
    TMPro.TextMeshProUGUI Label(string name,Transform parent,string text,Vector2 min,Vector2 max,float size)
    {var r=Rect(name,parent,min,max);var t=r.gameObject.AddComponent<TMPro.TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=size;t.color=Color.white;t.alignment=TMPro.TextAlignmentOptions.Center;t.raycastTarget=false;t.textWrappingMode=TMPro.TextWrappingModes.Normal;return t;}
    UnityEngine.UI.Button Button(string name,Transform parent,string text,Vector2 min,Vector2 max,string style="Grey/Double/button_square_depth_border",float size=19)
    {
        var r=Rect(name,parent,min,max);var img=Image(r,Sprite(style),Color.white);var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=img;
        var label=Label("Label",r,text,new Vector2(.04f,.05f),new Vector2(.96f,.95f),size);label.color=new Color(.04f,.24f,.31f);return b;
    }
    var panel=Rect("Appearance Customization",canvas,Vector2.zero,Vector2.one);
    Image(panel,null,new Color(0,0,0,.12f));
    var controller=canvas.gameObject.AddComponent<DineIn.Appearance.AppearanceCustomizationPanel>();
    var drag=Rect("Preview Rotation Area",panel,new Vector2(.02f,.08f),new Vector2(.47f,.90f));Image(drag,null,Color.clear);
    var dragComponent=drag.gameObject.AddComponent<DineIn.Appearance.AppearancePreviewDrag>();
    var dragData=new SerializedObject(dragComponent);dragData.FindProperty("panel").objectReferenceValue=controller;dragData.ApplyModifiedPropertiesWithoutUndo();
    Label("Preview Hint",panel,"DRAG TO ROTATE",new Vector2(.04f,.04f),new Vector2(.44f,.09f),17);
    var window=Rect("Options Panel",panel,new Vector2(.49f,.035f),new Vector2(.98f,.965f));
    Image(window,Sprite("Blue/Double/button_square_depth_border"),Color.white);
    Label("Title",window,"MAKE IT YOURS",new Vector2(.05f,.92f),new Vector2(.95f,.99f),25);
    var categoryButtons=new System.Collections.Generic.List<UnityEngine.UI.Button>();
    var categoryLabels=new[]{"Body","Skin","Face","Hair","Hair Color","Outfit","Hats"};
    for(int i=0;i<categoryLabels.Length;i++)
    {
        int column=i%4,row=i/4;float left=.035f+column*.235f,top=.91f-row*.065f;
        categoryButtons.Add(Button(categoryLabels[i],window,categoryLabels[i],new Vector2(left,top-.058f),new Vector2(left+.225f,top),"Grey/Double/button_square_depth_border",15));
    }
    var scrollRect=Rect("Options Scroll",window,new Vector2(.035f,.21f),new Vector2(.965f,.765f));
    var viewport=Rect("Viewport",scrollRect,Vector2.zero,Vector2.one);Image(viewport,null,new Color(1,1,1,.03f));viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
    var content=Rect("Content",viewport,new Vector2(0,1),new Vector2(1,1));content.pivot=new Vector2(.5f,1);
    var grid=content.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();grid.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;grid.spacing=new Vector2(8,8);grid.cellSize=new Vector2(166,120);grid.padding=new RectOffset(5,5,5,5);
    var fitter=content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();fitter.horizontalFit=UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;fitter.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
    var scroll=scrollRect.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;
    var template=Button("Option Template",content,"Option",Vector2.zero,Vector2.one);
    var optionLabel=template.transform.Find("Label").GetComponent<RectTransform>();optionLabel.anchorMin=new Vector2(.04f,.03f);optionLabel.anchorMax=new Vector2(.96f,.25f);optionLabel.offsetMin=optionLabel.offsetMax=Vector2.zero;optionLabel.GetComponent<TMPro.TMP_Text>().fontSize=15;
    var icon=Rect("Icon",template.transform,new Vector2(.07f,.28f),new Vector2(.93f,.95f));var iconImage=Image(icon,null,Color.white);iconImage.preserveAspect=true;iconImage.raycastTarget=false;
    var check=Rect("Selected",template.transform,new Vector2(.77f,.76f),new Vector2(.96f,.97f));var checkImage=Image(check,Sprite("Green/Double/check_square_color_checkmark"),Color.white);checkImage.raycastTarget=false;
    template.gameObject.SetActive(false);
    var cancel=Button("Cancel",window,"Cancel",new Vector2(.035f,.095f),new Vector2(.48f,.19f),"Red/Double/button_square_depth_border");
    var apply=Button("Apply",window,"Apply",new Vector2(.52f,.095f),new Vector2(.965f,.19f),"Green/Double/button_square_depth_border");
    var status=Label("Status",window,"Changes are saved only when you Apply.",new Vector2(.04f,.01f),new Vector2(.96f,.085f),13);
    var data=new SerializedObject(controller);
    void Ref(string field,UnityEngine.Object value)=>data.FindProperty(field).objectReferenceValue=value;
    Ref("panel",panel.gameObject);Ref("preview",appearance);Ref("binding",binding);Ref("openButton",customize);Ref("applyButton",apply);Ref("cancelButton",cancel);Ref("optionTemplate",template);Ref("options",content);Ref("scroll",scroll);Ref("status",status);
    var camera=transforms.Select(t=>t.GetComponent<Camera>()).First(c=>c!=null);Ref("previewCamera",camera);Ref("cameraFollow",camera.GetComponent<CameraFollow>());
    var categoryList=data.FindProperty("categories");categoryList.arraySize=categoryButtons.Count;for(int i=0;i<categoryButtons.Count;i++)categoryList.GetArrayElementAtIndex(i).objectReferenceValue=categoryButtons[i];
    var controls=transforms.SelectMany(t=>t.GetComponents<UnityEngine.UI.Selectable>()).ToArray();var controlsList=data.FindProperty("menuControls");controlsList.arraySize=controls.Length;for(int i=0;i<controls.Length;i++)controlsList.GetArrayElementAtIndex(i).objectReferenceValue=controls[i];
    data.ApplyModifiedPropertiesWithoutUndo();panel.gameObject.SetActive(false);
    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    return new{menu=path,categories=categoryLabels,originalControls=controls.Length};
}
finally{if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
