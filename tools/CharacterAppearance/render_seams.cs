// Disposable posed models only; never changes the source or enters Play Mode.
if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
System.IO.Directory.CreateDirectory("Temp/CharacterAppearance");
var catalog=DineIn.Appearance.AppearanceCatalog.Load();
var paths=new[]{"Idle.anim","Running.anim","WalkingCarry.anim","CarryIdle.anim","Happy Idle.fbx"};
var sheet=new Texture2D(1500,600,TextureFormat.RGB24,false);
try
{
    for(int row=0;row<2;row++)for(int col=0;col<paths.Length;col++)
    {
        var preview=new PreviewRenderUtility();var graph=UnityEngine.Playables.PlayableGraph.Create("Seam inspection");
        try
        {
            var root=new GameObject("Seams");var animator=root.AddComponent<Animator>();preview.AddSingleGO(root);
            var recipe=catalog.bodies[row].defaults.Copy();root.AddComponent<DineIn.Appearance.CharacterAppearance>().Apply(recipe);
            var clip=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Animations/PlayerAnimation/"+paths[col]).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
            var playable=UnityEngine.Animations.AnimationClipPlayable.Create(graph,clip);var output=UnityEngine.Animations.AnimationPlayableOutput.Create(graph,"Pose",animator);
            UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output,playable);graph.Play();graph.Evaluate(clip.length*.4f);
            var target=new Vector3(0,.95f,0);preview.camera.orthographic=true;preview.camera.orthographicSize=.65f;
            preview.camera.transform.position=target+new Vector3(2,.4f,-4);preview.camera.transform.LookAt(target);
            preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=20;preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.84f,.87f,.91f);
            preview.lights[0].intensity=1.2f;preview.lights[0].transform.rotation=Quaternion.Euler(35,155,0);preview.lights[1].intensity=.7f;
            preview.BeginStaticPreview(new Rect(0,0,300,300));preview.Render(true,false);var tex=preview.EndStaticPreview();
            sheet.SetPixels(col*300,(1-row)*300,300,300,tex.GetPixels());UnityEngine.Object.DestroyImmediate(tex);
        }
        finally{graph.Destroy();preview.Cleanup();}
    }
    sheet.Apply();System.IO.File.WriteAllBytes("Temp/CharacterAppearance/seams.png",sheet.EncodeToPNG());
}
finally{UnityEngine.Object.DestroyImmediate(sheet);}
return "Temp/CharacterAppearance/seams.png";
