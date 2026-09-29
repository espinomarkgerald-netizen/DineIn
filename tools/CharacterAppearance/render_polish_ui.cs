// Temporary preview scene only: no Apply, persistence, cloud, network or Play Mode.
if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/NewMenu/NewGameMenu.unity");
var output=new System.Collections.Generic.List<object>();
try
{
    var objects=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
    var controller=objects.Select(t=>t.GetComponent<DineIn.Appearance.AppearanceCustomizationPanel>()).Single(c=>c!=null);
    var data=new SerializedObject(controller);var panel=(GameObject)data.FindProperty("panel").objectReferenceValue;
    var look=(DineIn.Appearance.CharacterAppearance)data.FindProperty("preview").objectReferenceValue;
    var camera=(Camera)data.FindProperty("previewCamera").objectReferenceValue;
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    void Set(string field,object value)=>controller.GetType().GetField(field,flags).SetValue(controller,value);
    void Call(string method,params object[] args)=>controller.GetType().GetMethod(method,flags).Invoke(controller,args);
    Set("cameraPosition",camera.transform.position);
    Call("Awake");panel.SetActive(true);
    var controls=data.FindProperty("menuControls");for(int i=0;i<controls.arraySize;i++){var c=controls.GetArrayElementAtIndex(i).objectReferenceValue as UnityEngine.UI.Selectable;if(c!=null)c.gameObject.SetActive(false);}
    var presentation=data.FindProperty("menuPresentation");for(int i=0;i<presentation.arraySize;i++){var g=presentation.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;if(g!=null)g.SetActive(false);}
    var recipe=look.Catalog.defaults.Copy();look.Apply(recipe);Set("draft",recipe);
    var animator=look.GetComponent<Animator>();
    output.Add(new{animator.enabled,animator.applyRootMotion,culling=animator.cullingMode.ToString(),scale=look.transform.lossyScale.ToString()});
    animator.enabled=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
    var graph=UnityEngine.Playables.PlayableGraph.Create("PolishUIIdle");
    try
    {
        var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Animations/PlayerAnimation/Idle.anim");
        var playable=UnityEngine.Animations.AnimationClipPlayable.Create(graph,idle);var animation=UnityEngine.Animations.AnimationPlayableOutput.Create(graph,"Idle",animator);UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(animation,playable);graph.Play();graph.Evaluate(.2f);
        var direction=-camera.transform.forward;direction.y=0;look.transform.rotation=Quaternion.LookRotation(direction);
        camera.scene=scene;camera.enabled=false;
        var canvas=controller.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        foreach(var size in new[]{new Vector2Int(3840,2160),new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1024,768)})
        {
            var rt=new RenderTexture(size.x,size.y,24);rt.Create();camera.targetTexture=rt;camera.aspect=(float)size.x/size.y;
            try
            {
                foreach(int category in new[]{0,1,2,3,4,5,6})
                {
                    Call("SelectCategory",category);Canvas.ForceUpdateCanvases();Call("ResizeGrid");graph.Evaluate(.01f);Call("FramePreview");Call("AdvanceFraming",1f);Canvas.ForceUpdateCanvases();camera.Render();
                    var previous=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                    try{texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0);texture.Apply();var path="Temp/CharacterAppearance/menu-"+size.x+"-"+category+".png";System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());output.Add(new{path});}
                    finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(texture);}
                }
            }
            finally{camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
    }
    finally{graph.Destroy();}
}
finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
return output;
