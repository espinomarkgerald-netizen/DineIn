if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var catalog=DineIn.Appearance.AppearanceCatalog.Load();
var folder="Assets/_Project/Player/Assets/Appearance/Hats";
System.IO.Directory.CreateDirectory(folder);AssetDatabase.Refresh();
var hats=catalog.hats.ToList();
foreach(var item in new[]{("chef-hat","Chef Hat","ChefHat",.82f,.70f,Color.white),("cowboy-hat","Cowboy Hat","CowboyHat",1.05f,.66f,new Color(.35f,.18f,.08f)),("witch-hat","Witch Hat","WitchHat",1.0f,.67f,new Color(.12f,.07f,.19f))})
{
    if(hats.Any(h=>h.id==item.Item1))continue;
    var paths=AssetDatabase.FindAssets(item.Item3+" t:Model").Select(AssetDatabase.GUIDToAssetPath).Where(p=>System.IO.Path.GetFileNameWithoutExtension(p)==item.Item3).ToArray();
    if(paths.Length!=1)throw new System.Exception("Ambiguous hat "+item.Item3);
    var root=new GameObject(item.Item2);
    try
    {
        var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(paths[0]),root.transform,false);
        var renderers=model.GetComponentsInChildren<Renderer>();
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        float scale=item.Item4/Mathf.Max(bounds.size.x,bounds.size.z);
        model.transform.localScale*=scale;
        model.transform.localPosition=new Vector3(-bounds.center.x*scale,item.Item5-bounds.min.y*scale,-bounds.center.z*scale);
        var material=new Material(catalog.hairMaterial){name=item.Item3}; material.SetColor("_BaseColor",item.Item6);
        AssetDatabase.CreateAsset(material,folder+"/"+item.Item3+".mat");
        foreach(var r in renderers)r.sharedMaterials=Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray();
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,folder+"/"+item.Item3+".prefab");
        hats.Add(new DineIn.Appearance.AppearanceCatalog.Attachment{id=item.Item1,label=item.Item2,prefab=prefab,hidesHair=true});
    }
    finally{UnityEngine.Object.DestroyImmediate(root);}
}
catalog.hats=hats.ToArray();EditorUtility.SetDirty(catalog);
foreach(var restaurant in new[]{"Casual","FastFood"})
{
    string path="Assets/Resources/"+restaurant+"EmployeeUniforms.asset";
    if(AssetDatabase.LoadAssetAtPath<DineIn.Appearance.EmployeeUniforms>(path)!=null)continue;
    var uniforms=ScriptableObject.CreateInstance<DineIn.Appearance.EmployeeUniforms>();
    var entries=new System.Collections.Generic.List<DineIn.Appearance.EmployeeUniforms.Assignment>();
    foreach(EmployeeRole role in System.Enum.GetValues(typeof(EmployeeRole)))
        foreach(var body in catalog.bodies)
        {
            string outfit=role==EmployeeRole.Host||role==EmployeeRole.Cashier?"receptionist":role==EmployeeRole.Waiter||role==EmployeeRole.Busser||role==EmployeeRole.Barista?"waiter":"chef";
            entries.Add(new(){role=role,bodyId=body.id,outfitId=body.id+"-casual-"+outfit,requiredHatId=outfit=="chef"?"chef-hat":"none"});
        }
    uniforms.assignments=entries.ToArray();AssetDatabase.CreateAsset(uniforms,path);
}
AssetDatabase.SaveAssets();return new{hats=catalog.hats.Length,uniforms="Separate Casual and FastFood assignments authored"};
