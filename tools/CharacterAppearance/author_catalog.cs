if(EditorApplication.isPlayingOrWillChangePlaymode) throw new System.Exception("Edit Mode only");
var folder="Assets/_Project/Player/Assets/Appearance";
System.IO.Directory.CreateDirectory(folder+"/Materials");
AssetDatabase.Refresh();
var catalogPath="Assets/Resources/AppearanceCatalog.asset";
var catalog=AssetDatabase.LoadAssetAtPath<DineIn.Appearance.AppearanceCatalog>(catalogPath);
if(catalog!=null) return "Catalog already authored; leave Inspector edits intact.";
catalog=ScriptableObject.CreateInstance<DineIn.Appearance.AppearanceCatalog>();
Material Mat(string name,string texture=null,bool cutout=false)
{
    var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};
    m.SetFloat("_Smoothness",.15f);
    if(texture!=null) m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
    if(cutout){m.SetFloat("_AlphaClip",1);m.SetFloat("_Cutoff",.15f);m.EnableKeyword("_ALPHATEST_ON");m.renderQueue=2450;}
    AssetDatabase.CreateAsset(m,folder+"/Materials/"+name+".mat");return m;
}
catalog.skinMaterial=Mat("Skin"); catalog.hairMaterial=Mat("Hair");
catalog.skins=new[]{("light","Light",new Color(.98f,.74f,.52f)),("warm","Warm",new Color(.80f,.51f,.30f)),("tan","Tan",new Color(.62f,.35f,.19f)),("deep","Deep",new Color(.31f,.15f,.075f))}.Select(x=>new DineIn.Appearance.AppearanceCatalog.Palette{id=x.Item1,label=x.Item2,color=x.Item3}).ToArray();
catalog.hairColors=new[]{("brown","Brown",new Color(.24f,.095f,.035f)),("black","Black",new Color(.035f,.026f,.02f)),("blond","Blond",new Color(.84f,.57f,.24f)),("auburn","Auburn",new Color(.49f,.12f,.045f)),("gray","Gray",new Color(.60f,.60f,.60f))}.Select(x=>new DineIn.Appearance.AppearanceCatalog.Palette{id=x.Item1,label=x.Item2,color=x.Item3}).ToArray();
catalog.faces=Enumerable.Range(1,2).Select(i=>new DineIn.Appearance.AppearanceCatalog.Surface{id="face-0"+i,label="Face "+i,material=Mat("Face_0"+i,folder+"/Textures/Face_0"+i+".png",true)}).ToArray();
var outfits=new System.Collections.Generic.List<DineIn.Appearance.AppearanceCatalog.Surface>();
foreach(var path in System.IO.Directory.GetFiles(folder+"/Textures","*.png",System.IO.SearchOption.AllDirectories).Where(p=>!System.IO.Path.GetFileName(p).StartsWith("Face_")))
{
    string p=path.Replace('\\','/'), body=p.Contains("/Female/")?"female":"male";
    string role=p.Contains("MaitreD")?"maitre-d":p.Contains("Chef")?"chef":p.Contains("Receptionist")?"receptionist":"waiter";
    string restaurant=p.Contains("Fine Dining")?"fine":"casual", id=body+"-"+restaurant+"-"+role;
    outfits.Add(new DineIn.Appearance.AppearanceCatalog.Surface{id=id,label=(restaurant=="fine"?"Fine Dining ":"Casual Dining ")+role.Replace("-"," "),bodies=new[]{body},material=Mat(id,p)});
}
catalog.outfits=outfits.OrderBy(x=>x.id).ToArray();
var hairs=new System.Collections.Generic.List<DineIn.Appearance.AppearanceCatalog.Attachment>{new(){id="bald",label="Bald"}};
foreach(var body in new[]{"male","female"})
    for(int i=1;i<=(body=="male"?5:8);i++)hairs.Add(new(){id=body+"-hair-"+i.ToString("00"),label="Style "+i,bodies=new[]{body},prefab=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/Models/"+(body=="male"?"M":"F")+"_Hair_"+i.ToString("00")+".fbx")});
catalog.hairs=hairs.ToArray();
catalog.hats=new[]{new DineIn.Appearance.AppearanceCatalog.Attachment{id="none",label="No hat"}};
catalog.bodies=new[]{"male","female"}.Select(body=>new DineIn.Appearance.AppearanceCatalog.Body{id=body,label=body=="male"?"Male":"Female",model=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/Models/"+(body=="male"?"Male":"Female")+".fbx"),defaults=new(){bodyId=body,skinId="warm",faceId="face-01",hairId=body+"-hair-01",hairColorId="brown",outfitId=body+"-casual-chef",hatId="none"}}).ToArray();
catalog.defaults=catalog.bodies[0].defaults.Copy();
AssetDatabase.CreateAsset(catalog,catalogPath);AssetDatabase.SaveAssets();
return new{bodies=catalog.bodies.Length,outfits=catalog.outfits.Length,hairs=catalog.hairs.Length};
