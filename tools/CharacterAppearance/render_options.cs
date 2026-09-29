if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var catalog=DineIn.Appearance.AppearanceCatalog.Load();
var folder="Assets/_Project/Player/Assets/Appearance/Thumbnails";System.IO.Directory.CreateDirectory(folder);
var items=new System.Collections.Generic.List<(DineIn.Appearance.AppearanceCatalog.Option option,DineIn.Appearance.AppearanceRecipe recipe,int category)>();
foreach(var body in catalog.bodies)items.Add((body,body.defaults.Copy(),0));
foreach(var outfit in catalog.outfits){var recipe=AppearanceCatalogRecipe(outfit.bodies.First());recipe.outfitId=outfit.id;items.Add((outfit,recipe,5));}
foreach(var face in catalog.faces){var recipe=catalog.defaults.Copy();recipe.faceId=face.id;items.Add((face,recipe,2));}
foreach(var hair in catalog.hairs){var recipe=AppearanceCatalogRecipe(hair.bodies.Length>0?hair.bodies[0]:catalog.defaults.bodyId);recipe.hairId=hair.id;items.Add((hair,recipe,3));}
foreach(var hat in catalog.hats){var recipe=catalog.defaults.Copy();recipe.hatId=hat.id;if(hat.prefab==null)recipe.hairId="bald";items.Add((hat,recipe,6));}
DineIn.Appearance.AppearanceRecipe AppearanceCatalogRecipe(string body)=>DineIn.Appearance.AppearanceCatalog.Find(catalog.bodies,body).defaults.Copy();
var settingsScene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/NewMenu/NewGameMenu.unity");
try
{
var settings=settingsScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DineIn.Appearance.AppearanceCustomizationPanel>(true)).Single();
var categoryBounds=new System.Collections.Generic.Dictionary<int,Bounds>();
var categoryRadius=new System.Collections.Generic.Dictionary<int,float>();
// First measure the whole category, then render it at one shared scale/crop.
for(int pass=0;pass<2;pass++)
foreach(var item in items)
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
            var framing=settings.Presentation(item.category);if(framing==null)throw new System.Exception("Author category presentation first");
            var headwear=(GameObject)typeof(DineIn.Appearance.CharacterAppearance).GetField("hat",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(appearance);
            var mode=framing.thumbnailMode;
            var visible=root.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
            bool Keep(Renderer r)=>mode switch
            {
                DineIn.Appearance.AppearanceCustomizationPanel.FramingMode.Head or DineIn.Appearance.AppearanceCustomizationPanel.FramingMode.HeadThreeQuarter => r.name=="BodyHead"||r.name=="Face"||r.name=="Ears"||r is MeshRenderer,
                DineIn.Appearance.AppearanceCustomizationPanel.FramingMode.IsolatedHeadwear => headwear!=null?r.transform.IsChildOf(headwear.transform):r.name=="BodyHead"||r.name=="Ears"||r.name=="Face",
                DineIn.Appearance.AppearanceCustomizationPanel.FramingMode.TorsoThreeQuarter => r.name=="UpperBody"||r.name=="LowerBody"||r.name=="Hands",
                _=>true
            };
            var pointsToFrame=new System.Collections.Generic.List<Vector3>();
            foreach(var renderer in visible)
            {
                renderer.enabled=Keep(renderer);if(!renderer.enabled)continue;
                if(renderer is SkinnedMeshRenderer skin)
                {
                    var baked=new Mesh();skin.BakeMesh(baked);
                    try{pointsToFrame.AddRange(baked.vertices.Select(v=>skin.transform.TransformPoint(v)));}
                    finally{UnityEngine.Object.DestroyImmediate(baked);}
                }
                else pointsToFrame.AddRange(renderer.GetComponent<MeshFilter>().sharedMesh.vertices.Select(v=>renderer.transform.TransformPoint(v)));
            }
            preview.camera.transform.rotation=Quaternion.Euler(framing.thumbnailEuler);
            var inverse=Quaternion.Inverse(preview.camera.transform.rotation);var projected=new Bounds(inverse*pointsToFrame[0],Vector3.zero);
            foreach(var point in pointsToFrame)projected.Encapsulate(inverse*point);
            if(pass==0)
            {
                if(categoryBounds.TryGetValue(item.category,out var all)){all.Encapsulate(projected);categoryBounds[item.category]=all;}else categoryBounds[item.category]=projected;
                float radius=Mathf.Max(projected.extents.x,projected.extents.y);categoryRadius[item.category]=Mathf.Max(categoryRadius.TryGetValue(item.category,out var oldRadius)?oldRadius:0,radius);
                continue;
            }
            // Isolated hats share scale but center their own geometry; head/body crops share world bounds too.
            if(item.category!=6)projected=categoryBounds[item.category];
            preview.camera.orthographic=true;preview.camera.orthographicSize=(item.category==6?categoryRadius[6]:Mathf.Max(projected.extents.x,projected.extents.y))*framing.thumbnailMargin;
            preview.camera.transform.position=preview.camera.transform.rotation*projected.center-preview.camera.transform.forward*5;
            preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=20;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=framing.thumbnailBackground;
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
EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);return new{thumbnails=items.Count};
}
finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(settingsScene);}
