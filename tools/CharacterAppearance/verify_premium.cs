// Temporary Edit-mode objects only. Never commits a draft or writes profile data.
if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
int assertions=0;void Check(bool value,string message){assertions++;if(!value)throw new System.Exception(message);}
var catalog=DineIn.Appearance.AppearanceCatalog.Load();
foreach(var body in catalog.bodies)
{
    var original=body.model.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name=="Hands").sharedMesh;var fixedMesh=body.sleeveSkinMesh;
    Check(fixedMesh!=null,"Missing sleeve correction");
    Check(original.vertices.SequenceEqual(fixedMesh.vertices)&&original.uv.SequenceEqual(fixedMesh.uv)&&original.triangles.SequenceEqual(fixedMesh.triangles)&&original.bindposes.SequenceEqual(fixedMesh.bindposes),"Changed body geometry/UV/bind poses");
    var weights=fixedMesh.boneWeights;var old=original.boneWeights;var vertices=original.vertices;
    for(int i=0;i<vertices.Length;i++)
    {
        var w=weights[i];Check(Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)<.0001f,"Weights not normalized");
        if(Mathf.Abs(vertices[i].x)>.651f)Check(w.Equals(old[i]),"Changed visible forearm/wrist weight");
    }
}
var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/NewMenu/NewGameMenu.unity");
try
{
    var ui=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DineIn.Appearance.AppearanceCustomizationPanel>(true)).Single();
    var data=new SerializedObject(ui);var camera=(Camera)data.FindProperty("previewCamera").objectReferenceValue;
    var preview=(DineIn.Appearance.CharacterAppearance)data.FindProperty("preview").objectReferenceValue;
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    object Field(string name)=>ui.GetType().GetField(name,flags).GetValue(ui);
    void Call(string method,params object[] args)=>ui.GetType().GetMethod(method,flags).Invoke(ui,args);
    data.FindProperty("restaurantSelector").objectReferenceValue=null;data.ApplyModifiedPropertiesWithoutUndo();Call("Awake");
    var originalPosition=camera.transform.position;var originalSize=camera.orthographicSize;
    ui.Open();Call("AdvanceFraming",1f);ui.RotatePreview(130);var rotation=preview.transform.rotation;
    var start=camera.transform.position;Call("SelectCategory",2);var goal=(Vector3)Field("frameTo");
    Check(camera.transform.position==start,"Category selection snapped camera");Call("AdvanceFraming",.15f);
    Check(Vector3.Distance(camera.transform.position,goal)<Vector3.Distance(start,goal),"Framing did not approach target");
    Check(Vector3.Distance(camera.transform.position,start)<=Vector3.Distance(start,goal),"Framing overshot");
    Call("SelectCategory",6);Call("Choose","witch-hat");goal=(Vector3)Field("frameTo");Call("AdvanceFraming",2f);
    Check(Vector3.Distance(camera.transform.position,goal)<.0001f,"Rapid selection did not settle on latest target");
    Check(preview.transform.rotation==rotation,"Framing overwrote manual rotation");
    var hatLens=camera.orthographicSize;Call("Choose","none");Call("AdvanceFraming",2f);Check(camera.orthographicSize<hatLens,"Removing large hat did not reduce headroom");
    Call("SelectCategory",3);Call("AdvanceFraming",2f);var stablePosition=camera.transform.position;var stableLens=camera.orthographicSize;
    var draft=(DineIn.Appearance.AppearanceRecipe)Field("draft");var nextHair=catalog.hairs.First(h=>h.Fits(draft.bodyId)&&h.id!=draft.hairId&&h.prefab!=null);
    Call("Choose",nextHair.id);Call("AdvanceFraming",1f);Check(camera.transform.position==stablePosition&&camera.orthographicSize==stableLens,"Similar hair selection pumped camera");
    Call("SelectCategory",4);Call("AdvanceFraming",1f);Check(Vector3.Distance(camera.transform.position,stablePosition)<.0001f&&Mathf.Abs(camera.orthographicSize-stableLens)<.0001f,"Color category lost relevant framing");
    var options=(RectTransform)data.FindProperty("options").objectReferenceValue;
    Call("SelectCategory",6);Check(options.GetComponentsInChildren<UnityEngine.UI.Image>().Where(i=>i.name=="Icon").All(i=>i.preserveAspect),"Thumbnail aspect ratio disabled");
    ui.Cancel();Check(camera.transform.position==originalPosition&&camera.orthographicSize==originalSize,"Cancel did not restore menu camera");
}
finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
return new{assertions,scope="Edit-mode data/transition checks, not runtime playback"};
