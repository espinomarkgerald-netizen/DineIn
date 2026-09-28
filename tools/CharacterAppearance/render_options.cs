if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var catalog=DineIn.Appearance.AppearanceCatalog.Load();
var folder="Assets/_Project/Player/Assets/Appearance/Thumbnails";System.IO.Directory.CreateDirectory(folder);
var items=new System.Collections.Generic.List<(DineIn.Appearance.AppearanceCatalog.Option option,DineIn.Appearance.AppearanceRecipe recipe)>();
foreach(var body in catalog.bodies)items.Add((body,body.defaults.Copy()));
foreach(var outfit in catalog.outfits){var recipe=AppearanceCatalogRecipe(outfit.bodies.First());recipe.outfitId=outfit.id;items.Add((outfit,recipe));}
foreach(var face in catalog.faces){var recipe=catalog.defaults.Copy();recipe.faceId=face.id;items.Add((face,recipe));}
foreach(var hair in catalog.hairs){var recipe=AppearanceCatalogRecipe(hair.bodies.Length>0?hair.bodies[0]:catalog.defaults.bodyId);recipe.hairId=hair.id;items.Add((hair,recipe));}
foreach(var hat in catalog.hats){var recipe=catalog.defaults.Copy();recipe.hatId=hat.id;items.Add((hat,recipe));}
DineIn.Appearance.AppearanceRecipe AppearanceCatalogRecipe(string body)=>DineIn.Appearance.AppearanceCatalog.Find(catalog.bodies,body).defaults.Copy();
foreach(var item in items.Where(x=>x.option.thumbnail==null))
{
    var preview=new UnityEditor.PreviewRenderUtility();
    try
    {
        var root=new GameObject("Appearance Thumbnail");root.AddComponent<Animator>();
        preview.AddSingleGO(root);
        var appearance=root.AddComponent<DineIn.Appearance.CharacterAppearance>();appearance.Apply(item.recipe);
        var animator=root.GetComponent<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var graph=UnityEngine.Playables.PlayableGraph.Create("ThumbnailPose");
        try
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Animations/PlayerAnimation/Idle.anim");
            var playable=UnityEngine.Animations.AnimationClipPlayable.Create(graph,clip);
            var output=UnityEngine.Animations.AnimationPlayableOutput.Create(graph,"Pose",animator);
            UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output,playable);graph.Play();graph.Evaluate(.2f);
            var visible=root.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
            var bounds=visible[0].bounds;foreach(var renderer in visible)bounds.Encapsulate(renderer.bounds);
            preview.camera.orthographic=true;preview.camera.orthographicSize=Mathf.Max(bounds.size.y,bounds.size.x)*.55f;
            preview.camera.transform.position=bounds.center+Vector3.forward*5;preview.camera.transform.rotation=Quaternion.Euler(0,180,0);
            preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=20;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.84f,.87f,.91f,1);
            preview.lights[0].intensity=1.1f;preview.lights[0].transform.rotation=Quaternion.Euler(35,155,0);
            preview.lights[1].intensity=.75f;preview.lights[1].transform.rotation=Quaternion.Euler(15,215,0);
            preview.BeginStaticPreview(new Rect(0,0,256,256));preview.Render(true,false);
            var image=preview.EndStaticPreview();
            string path=folder+"/"+item.option.id+".png";System.IO.File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.maxTextureSize=256;importer.SaveAndReimport();
            item.option.thumbnail=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        finally{graph.Destroy();}
    }
    finally{preview.Cleanup();}
}
EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();return new{thumbnails=items.Count};
