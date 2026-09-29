// Disposable scene, sampled idle only. No restaurant navigation, preferences or save writes.
if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
int assertions=0;void Check(bool c,string m){assertions++;if(!c)throw new System.Exception(m);}
var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/NewMenu/NewGameMenu.unity");
var graph=UnityEngine.Playables.PlayableGraph.Create("Normal composition check");
try
{
    var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
    var ui=all.Select(t=>t.GetComponent<DineIn.Appearance.AppearanceCustomizationPanel>()).Single(t=>t!=null);var d=new SerializedObject(ui);
    ((GameObject)d.FindProperty("panel").objectReferenceValue).SetActive(false);
    var look=(DineIn.Appearance.CharacterAppearance)d.FindProperty("preview").objectReferenceValue;look.Apply(look.Catalog.defaults.Copy());
    var selector=(RestaurantSelector)d.FindProperty("restaurantSelector").objectReferenceValue;
    var camera=(Camera)d.FindProperty("previewCamera").objectReferenceValue;camera.scene=scene;camera.enabled=false;
    var canvas=ui.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
    var animator=look.GetComponent<Animator>();animator.enabled=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Animations/PlayerAnimation/Idle.anim");var playable=UnityEngine.Animations.AnimationClipPlayable.Create(graph,clip);var output=UnityEngine.Animations.AnimationPlayableOutput.Create(graph,"Idle",animator);UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output,playable);graph.Play();graph.Evaluate(.2f);
    var follow=camera.GetComponent<CameraFollow>();var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    typeof(CameraFollow).GetMethod("Awake",flags).Invoke(follow,null);
    var play=(RectTransform)all.First(t=>t.name=="PlayButton");var corners=new Vector3[4];
    foreach(var size in new[]{new Vector2Int(3840,2160),new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1024,768)})
    {
        var rt=new RenderTexture(size.x,size.y,24);rt.Create();camera.targetTexture=rt;camera.aspect=(float)size.x/size.y;
        try
        {
            for(int index=0;index<selector.travelPoints.Length;index++)
            {
                selector.character.position=selector.travelPoints[index].position;follow.SnapToAuthoredComposition();Canvas.ForceUpdateCanvases();graph.Evaluate(.01f);
                var feet=camera.WorldToViewportPoint(selector.character.position);play.GetWorldCorners(corners);float playTop=corners.Max(p=>camera.WorldToViewportPoint(p).y);
                Check(Mathf.Abs(feet.x-.5f)<.01f,"Destination not centered in world presentation");Check(feet.y-playTop>.10f,"Feet too close to Play: "+(feet.y-playTop));
                var before=camera.transform.position;typeof(CameraFollow).GetMethod("Start",flags).Invoke(follow,null);Check(Vector3.Distance(before,camera.transform.position)<.0001f,"Follow Start recaptured unstable offset");
                camera.Render();var prior=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                try{image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();System.IO.File.WriteAllBytes("Temp/CharacterAppearance/normal-"+size.x+"-"+index+".png",image.EncodeToPNG());}
                finally{RenderTexture.active=prior;UnityEngine.Object.DestroyImmediate(image);}
            }
        }
        finally{camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
    }
    var sd=new SerializedObject(selector);foreach(string n in new[]{"bodyReactions","headReactions","faceReactions"})Check(sd.FindProperty(n).arraySize==0,"Placeholder inspection pool remains");
}
finally{graph.Destroy();UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
return new{assertions,scope="Authored camera/destination geometry and Edit-mode renders, not runtime travel"};
