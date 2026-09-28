if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var catalog=DineIn.Appearance.AppearanceCatalog.Load();
System.IO.Directory.CreateDirectory("Temp/CharacterAppearance");
var outputPaths=new System.Collections.Generic.List<string>();
foreach(var hat in catalog.hats.Where(h=>h.prefab!=null))
{
    var sheet=new Texture2D(768,768,TextureFormat.RGB24,false);var pixels=Enumerable.Repeat(new Color(.84f,.87f,.91f),768*768).ToArray();sheet.SetPixels(pixels);
    bool hidden=hat.hidesHair;hat.hidesHair=false;int index=0;
    try
    {
        foreach(var hair in catalog.hairs.Where(h=>h.prefab!=null))
        {
            var preview=new PreviewRenderUtility();
            try
            {
                var root=new GameObject("Fit inspection");root.AddComponent<Animator>();preview.AddSingleGO(root);
                var recipe=catalog.bodies.First(b=>hair.Fits(b.id)).defaults.Copy();recipe.hairId=hair.id;recipe.hatId=hat.id;
                root.AddComponent<DineIn.Appearance.CharacterAppearance>().Apply(recipe);
                var head=root.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="BodyHead");
                var center=head.bounds.center+Vector3.up*.22f;
                preview.camera.orthographic=true;preview.camera.orthographicSize=.8f;
                preview.camera.transform.position=center+new Vector3(.6f,.25f,5);preview.camera.transform.LookAt(center);
                preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=20;preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.84f,.87f,.91f);
                preview.lights[0].intensity=1.2f;preview.lights[0].transform.rotation=Quaternion.Euler(35,155,0);preview.lights[1].intensity=.7f;
                preview.BeginStaticPreview(new Rect(0,0,192,192));preview.Render(true,false);var tex=preview.EndStaticPreview();
                sheet.SetPixels((index%4)*192,(3-index/4)*192,192,192,tex.GetPixels());UnityEngine.Object.DestroyImmediate(tex);index++;
            }
            finally{preview.Cleanup();}
        }
        sheet.Apply();var path="Temp/CharacterAppearance/fit-"+hat.id+".png";System.IO.File.WriteAllBytes(path,sheet.EncodeToPNG());outputPaths.Add(path);
    }
    finally{hat.hidesHair=hidden;UnityEngine.Object.DestroyImmediate(sheet);}
}
return new{outputPaths,order=catalog.hairs.Where(h=>h.prefab!=null).Select(h=>h.id).ToArray()};
