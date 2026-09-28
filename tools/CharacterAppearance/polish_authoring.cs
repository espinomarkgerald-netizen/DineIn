// In-place authoring only. Refuses unsaved scene edits or Play Mode.
if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var catalog=DineIn.Appearance.AppearanceCatalog.Load();
catalog.adultHeight=3.81f;catalog.modelHeight=2;
foreach(var hat in catalog.hats)
{
    hat.hidesHair=false;
    hat.incompatibleHairIds=hat.prefab!=null?new[]{"female-hair-03"}:System.Array.Empty<string>();
    hat.bareHeadPositionOffset=new Vector3(0,-.2f,0);
    if(hat.prefab==null)continue;
    var path=AssetDatabase.GetAssetPath(hat.prefab);var root=PrefabUtility.LoadPrefabContents(path);
    try
    {
        var model=root.transform.GetChild(0);var renderers=model.GetComponentsInChildren<Renderer>();
        root.transform.localScale=Vector3.one;model.localScale=Vector3.one;model.localPosition=Vector3.zero;
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        float width=hat.id=="chef-hat"?1.45f:hat.id=="cowboy-hat"?1.85f:1.75f;
        float height=hat.id=="chef-hat"?.7f:hat.id=="cowboy-hat"?.36f:.9f;
        var ratio=new Vector3(width/Mathf.Max(bounds.size.x,bounds.size.z),height/bounds.size.y,width/Mathf.Max(bounds.size.x,bounds.size.z));
        root.transform.localScale=ratio;
        bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        model.position+=new Vector3(-bounds.center.x,.79f-bounds.min.y,-bounds.center.z);
        PrefabUtility.SaveAsPrefabAsset(root,path);
    }
    finally{PrefabUtility.UnloadPrefabContents(root);}
}
EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
int normalized=0,legacyHats=0;
void PolishLook(DineIn.Appearance.CharacterAppearance look,bool gameplay)
{
    var data=new SerializedObject(look);data.FindProperty("normalizeAdultHeight").boolValue=gameplay;
    if(gameplay)normalized++;
    var a=(Animator)data.FindProperty("animator").objectReferenceValue;
    var list=data.FindProperty("originalRenderers");var renderers=Enumerable.Range(0,list.arraySize).Select(i=>list.GetArrayElementAtIndex(i).objectReferenceValue as Renderer).Where(r=>r!=null).ToList();
    // Only the explicitly authored legacy head socket's rigid headwear, never carried props/particles.
    foreach(var socket in a.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="headSocket"))
        foreach(var r in socket.GetComponentsInChildren<MeshRenderer>(true))if(!renderers.Contains(r)){renderers.Add(r);legacyHats++;}
    list.arraySize=renderers.Count;for(int i=0;i<renderers.Count;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=renderers[i];
    data.ApplyModifiedPropertiesWithoutUndo();
}
foreach(var path in new[]{"Assets/_Project/Player/Manager.prefab","Assets/Resources/ManagerMultiplayer.prefab","Assets/Resources/Player.prefab"})
{
    var root=PrefabUtility.LoadPrefabContents(path);
    try{PolishLook(root.GetComponent<DineIn.Appearance.CharacterAppearance>(),true);PrefabUtility.SaveAsPrefabAsset(root,path);}
    finally{PrefabUtility.UnloadPrefabContents(root);}
}
foreach(var path in new[]{"Assets/_Project/Scenes/RoleBased/Lobby1.unity","Assets/_Project/Scenes/RoleBased/Lobby2.unity","Assets/_Project/Scenes/RoleBased/Lobby1 Multiplayer.unity","Assets/_Project/Scenes/TutorialScenes/Lobby1Tutorial.unity","Assets/_Project/Scenes/RoleBased/Lobby2Tutorial.unity","Assets/_Project/Scenes/NewMenu/NewGameMenu.unity"})
{
    var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
    if(!opened && scene.isDirty)throw new System.Exception("Unsaved edits: "+path);
    if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
    try
    {
        var objects=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        bool menu=path.Contains("NewGameMenu");
        foreach(var look in objects.Select(t=>t.GetComponent<DineIn.Appearance.CharacterAppearance>()).Where(c=>c!=null))PolishLook(look,!menu);
        if(menu)
        {
            var ui=objects.Select(t=>t.GetComponent<DineIn.Appearance.AppearanceCustomizationPanel>()).Single(c=>c!=null);
            var data=new SerializedObject(ui);var root=((GameObject)data.FindProperty("panel").objectReferenceValue).transform;
            var window=root.Find("Options Panel");
            var navy=new Color(.04f,.24f,.31f);
            Sprite Art(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/"+name+".png");
            void Layout(Transform t,Vector2 min,Vector2 max)
            {var rect=(RectTransform)t;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;}
            void Text(Transform t,float size,Color color)
            {var text=t.GetComponent<TMPro.TMP_Text>();text.fontSize=size;text.color=color;text.margin=new Vector4(3,2,3,2);}
            Layout(window,new Vector2(.45f,.035f),new Vector2(.98f,.965f));
            Layout(window.Find("Title"),new Vector2(.055f,.865f),new Vector2(.945f,.955f));Text(window.Find("Title"),25,navy);window.Find("Title").GetComponent<TMPro.TMP_Text>().text="CUSTOMIZE";
            string[] tabs={"Body","Skin","Face","Hair","Hair Color","Outfit","Hats"};
            for(int i=0;i<tabs.Length;i++)
            {
                float width=i<4?.216f:.292f;int column=i<4?i:i-4;float left=.055f+column*(width+.01f);float top=i<4?.845f:.76f;
                var tab=window.Find(tabs[i]);Layout(tab,new Vector2(left,top-.075f),new Vector2(left+width,top));
                tab.GetComponent<UnityEngine.UI.Image>().sprite=Art("Grey/Double/button_rectangle_depth_border");Text(tab.Find("Label"),15,navy);
            }
            Layout(window.Find("Options Scroll"),new Vector2(.055f,.255f),new Vector2(.945f,.66f));
            Layout(window.Find("Status"),new Vector2(.055f,.18f),new Vector2(.945f,.24f));Text(window.Find("Status"),12,navy);
            foreach(var name in new[]{"Cancel","Apply"})
            {
                var button=window.Find(name);Layout(button,new Vector2(name=="Cancel"?.06f:.525f,.065f),new Vector2(name=="Cancel"?.475f:.94f,.16f));
                button.GetComponent<UnityEngine.UI.Image>().sprite=Art((name=="Cancel"?"Red":"Green")+"/Double/button_rectangle_depth_flat");Text(button.Find("Label"),21,Color.white);
            }
            Layout(root.Find("Preview Rotation Area"),new Vector2(.02f,.13f),new Vector2(.43f,.94f));
            Layout(root.Find("Preview Hint"),new Vector2(.035f,.045f),new Vector2(.425f,.11f));Text(root.Find("Preview Hint"),17,Color.white);
            var template=(UnityEngine.UI.Button)data.FindProperty("optionTemplate").objectReferenceValue;
            Layout(template.transform.Find("Icon"),new Vector2(.10f,.34f),new Vector2(.90f,.87f));
            Layout(template.transform.Find("Label"),new Vector2(.065f,.08f),new Vector2(.935f,.32f));Text(template.transform.Find("Label"),11,navy);
            var check=(RectTransform)template.transform.Find("Selected");check.anchorMin=check.anchorMax=new Vector2(1,1);check.pivot=Vector2.one;check.anchoredPosition=new Vector2(-7,-7);check.sizeDelta=new Vector2(21,21);
            var grid=((RectTransform)data.FindProperty("options").objectReferenceValue).GetComponent<UnityEngine.UI.GridLayoutGroup>();grid.cellSize=new Vector2(126,138);grid.childAlignment=TextAnchor.UpperLeft;grid.spacing=new Vector2(9,9);grid.padding=new RectOffset(3,3,3,3);
            data.FindProperty("optionCardSize").vector2Value=new Vector2(126,138);data.FindProperty("colorSwatchSize").vector2Value=new Vector2(58,64);
            data.FindProperty("selectedTabSprite").objectReferenceValue=Art("Blue/Double/button_rectangle_depth_flat");
            data.FindProperty("previewScreenHeight").floatValue=.8f;
            var otherMenu=ui.transform.Cast<Transform>().Where(t=>t!=root).ToArray();var presentation=data.FindProperty("menuPresentation");presentation.arraySize=otherMenu.Length;
            for(int i=0;i<otherMenu.Length;i++)presentation.GetArrayElementAtIndex(i).objectReferenceValue=otherMenu[i].gameObject;
            var selector=objects.Select(t=>t.GetComponent<RestaurantSelector>()).Single(c=>c!=null);data.FindProperty("restaurantSelector").objectReferenceValue=selector;data.ApplyModifiedPropertiesWithoutUndo();
            var motion=new SerializedObject(selector);motion.FindProperty("animatePresentation").boolValue=true;
            motion.FindProperty("menuAnimator").objectReferenceValue=selector.character.GetComponent<Animator>();motion.FindProperty("menuCamera").objectReferenceValue=data.FindProperty("previewCamera").objectReferenceValue;
            // This menu is moved explicitly between travel points; clips must stay in place.
            var menuAnimator=selector.character.GetComponent<Animator>();menuAnimator.applyRootMotion=false;menuAnimator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            AnimationClip Clip(string path)=>AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Animations/PlayerAnimation/"+path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            motion.FindProperty("idleClip").objectReferenceValue=Clip("Idle.anim");motion.FindProperty("runClip").objectReferenceValue=Clip("Running.anim");motion.FindProperty("applyReaction").objectReferenceValue=Clip("Happy Idle.fbx");
            // No wave/inspection clips exist in the supplied project. Keep explicit optional slots unassigned.
            motion.ApplyModifiedPropertiesWithoutUndo();
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }
    finally{if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
}
return new{normalized,legacyHats};
