if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var catalog=DineIn.Appearance.AppearanceCatalog.Load();
var outfits=catalog.outfits.ToList();
foreach(var body in catalog.bodies)
{
    string textureName=body.id=="male"?"FastFoodMale.png":"FastFood.png";
    string texturePath="Assets/_Project/Player/Assets/Appearance/Textures/"+textureName;
    System.IO.File.Copy("G:/Downloads/"+textureName,texturePath,true);
    AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
    string materialPath="Assets/_Project/Player/Assets/Appearance/Materials/"+(body.id=="male"?"FastFoodUniformMale":"FastFoodUniform")+".mat";
    var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
    if(material==null){material=new Material(catalog.outfits[0].material);AssetDatabase.CreateAsset(material,materialPath);}
    material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
    string id=body.id+"-fastfood-uniform";
    var entry=outfits.FirstOrDefault(o=>o.id==id);
    if(entry==null){entry=new DineIn.Appearance.AppearanceCatalog.Surface{id=id,label="Fast Food Uniform",bodies=new[]{body.id}};outfits.Add(entry);}
    entry.material=material;entry.thumbnail=null;EditorUtility.SetDirty(material);
}
catalog.outfits=outfits.ToArray();
var uniforms=Resources.Load<DineIn.Appearance.EmployeeUniforms>("FastFoodEmployeeUniforms");
foreach(var assignment in uniforms.assignments)assignment.outfitId=assignment.bodyId+"-fastfood-uniform";
EditorUtility.SetDirty(catalog);EditorUtility.SetDirty(uniforms);AssetDatabase.SaveAssets();
return "Separate male and female Fast Food textures imported; assignments ready for UV preview verification.";
