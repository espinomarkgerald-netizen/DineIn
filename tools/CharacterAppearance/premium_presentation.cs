// Focused, repeatable authoring. Does not touch careers, gameplay scenes, or source meshes.
if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
const string path="Assets/_Project/Scenes/NewMenu/NewGameMenu.unity";
var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
if(!opened&&scene.isDirty)throw new System.Exception("Save NewGameMenu edits first.");
if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
try
{
    var ui=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DineIn.Appearance.AppearanceCustomizationPanel>(true)).Single();
    if(ui.GetComponentInChildren<DineIn.Appearance.CosmeticOptionLayout>(true)!=null)throw new System.Exception("This legacy authoring pass is superseded. Use redesign_customization.cs for the current screen.");
    var data=new SerializedObject(ui);var frames=data.FindProperty("categoryPresentation");frames.arraySize=7;
    int[] modes={0,1,2,3,1,4,5};float[] centers={.5f,.5f,.79f,.79f,.79f,.4f,.79f};float[] heights={1,1,.49f,.61f,.61f,.78f,.61f};
    for(int i=0;i<7;i++)
    {
        var f=frames.GetArrayElementAtIndex(i);f.FindPropertyRelative("thumbnailMode").enumValueIndex=modes[i];
        f.FindPropertyRelative("thumbnailEuler").vector3Value=new Vector3(i==6?12:0,i==3||i==5||i==6?155:180,0);
        f.FindPropertyRelative("thumbnailMargin").floatValue=i==6?1.2f:1.12f;
        f.FindPropertyRelative("thumbnailBackground").colorValue=new Color(.84f,.87f,.91f,1);
        f.FindPropertyRelative("previewCenter").floatValue=centers[i];f.FindPropertyRelative("previewHeight").floatValue=heights[i];
    }
    data.FindProperty("framingSeconds").floatValue=.45f;
    data.FindProperty("optionCardSize").vector2Value=new Vector2(120,176);
    var template=(UnityEngine.UI.Button)data.FindProperty("optionTemplate").objectReferenceValue;
    void Fill(Transform target,Vector2 min,Vector2 max){var r=(RectTransform)target;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
    Fill(((UnityEngine.UI.ScrollRect)data.FindProperty("scroll").objectReferenceValue).transform,new Vector2(.055f,.245f),new Vector2(.945f,.68f));
    Fill(template.transform.Find("Icon"),new Vector2(.125f,.42f),new Vector2(.875f,.875f));
    template.transform.Find("Icon").GetComponent<UnityEngine.UI.Image>().preserveAspect=true;
    Fill(template.transform.Find("Label"),new Vector2(.125f,.17f),new Vector2(.875f,.42f));
    var label=template.transform.Find("Label").GetComponent<TMPro.TMP_Text>();label.fontSize=12;label.enableAutoSizing=false;label.margin=new Vector4(2,1,2,1);label.alignment=TMPro.TextAlignmentOptions.Center;
    var grid=((RectTransform)data.FindProperty("options").objectReferenceValue).GetComponent<UnityEngine.UI.GridLayoutGroup>();grid.cellSize=new Vector2(120,176);
    foreach(var item in new[]{("cancelButton","Red"),("applyButton","Green")})
    {
        var button=(UnityEngine.UI.Button)data.FindProperty(item.Item1).objectReferenceValue;var image=button.GetComponent<UnityEngine.UI.Image>();
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/"+item.Item2+"/Double/button_rectangle_depth_border.png");
        if(sprite==null)throw new System.Exception("Matching button frame missing");image.sprite=sprite;image.type=UnityEngine.UI.Image.Type.Sliced;image.pixelsPerUnitMultiplier=1;image.preserveAspect=false;
        var text=button.GetComponentInChildren<TMPro.TMP_Text>();text.color=new Color(.04f,.24f,.31f);text.margin=new Vector4(3,2,3,2);text.alignment=TMPro.TextAlignmentOptions.Center;
        Fill(text.transform,new Vector2(.06f,.13f),new Vector2(.94f,.94f));
    }
    var selector=data.FindProperty("restaurantSelector").objectReferenceValue;var selectorData=new SerializedObject(selector);
    var happy=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Animations/PlayerAnimation/Happy Idle.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
    foreach(var name in new[]{"bodyReactions","headReactions","faceReactions"}){var pool=selectorData.FindProperty(name);pool.arraySize=1;pool.GetArrayElementAtIndex(0).objectReferenceValue=happy;}
    selectorData.ApplyModifiedPropertiesWithoutUndo();data.ApplyModifiedPropertiesWithoutUndo();
    var catalog=DineIn.Appearance.AppearanceCatalog.Load();
    DineIn.Appearance.AppearanceCatalog.Palette ColorEntry(string id,string label,string hex){ColorUtility.TryParseHtmlString(hex,out var c);return new DineIn.Appearance.AppearanceCatalog.Palette{id=id,label=label,color=c};}
    var hair=catalog.hairColors.ToList();
    foreach(var c in new[]{ColorEntry("dark-brown","Dark Brown","#291A12"),ColorEntry("light-brown","Light Brown","#8C603E"),ColorEntry("dark-blonde","Dark Blonde","#967240"),ColorEntry("red","Red","#B04F29"),ColorEntry("white","White","#E3DFD5")})if(!hair.Any(x=>x.id==c.id))hair.Add(c);
    catalog.hairColors=hair.ToArray();
    var skins=catalog.skins.ToList();foreach(var c in new[]{ColorEntry("fair","Fair","#F8D6BC"),ColorEntry("medium","Medium","#AD7957"),ColorEntry("rich","Rich","#6B422C")})if(!skins.Any(x=>x.id==c.id))skins.Add(c);
    catalog.skins=skins.ToArray();
    foreach(var outfit in catalog.outfits)
    {
        string prefix=outfit.id.Contains("fastfood")?"Fast Food":outfit.id.Contains("casual")?"Casual Dining":"Fine Dining";
        string role=outfit.id.EndsWith("chef")?"Chef":outfit.id.EndsWith("receptionist")?"Receptionist":outfit.id.EndsWith("waiter")?(outfit.Fits("female")?"Waitress":"Waiter"):outfit.id.EndsWith("maitre-d")?"Maitre d":"Uniform";
        outfit.label=prefix+"\n"+role;
    }
    foreach(var hat in catalog.hats)hat.previewHeadroom=hat.prefab==null?0:hat.id=="witch-hat"?.24f:hat.id=="chef-hat"?.16f:.10f;
    // Rear-angle inspection found three crowns crossing the tapered witch hat, not the cowboy brim.
    var witch=DineIn.Appearance.AppearanceCatalog.Find(catalog.hats,"witch-hat");
    var fits=witch.hairFits.ToList();
    foreach(var hairId in new[]{"male-hair-05","female-hair-02","female-hair-05"})
    {
        if(fits.Any(f=>f.hairId==hairId))continue;
        // Imported hair uses local Z for height. Keep width and side/back hair; only tuck the crown.
        fits.Add(new DineIn.Appearance.AppearanceCatalog.HairFit{hairId=hairId,scale=new Vector3(1,1,.9f)});
    }
    witch.hairFits=fits.ToArray();
    EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    return new{cards="120 x 176, preserved aspect",paletteHair=catalog.hairColors.Length,paletteSkin=catalog.skins.Length,reactions="Existing Happy Idle; other suitable variations unavailable"};
}
finally{if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
