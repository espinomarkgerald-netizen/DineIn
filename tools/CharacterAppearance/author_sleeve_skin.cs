// Correct covered arm weighting in a derived mesh, never the supplied model or skeleton.
if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var catalog=DineIn.Appearance.AppearanceCatalog.Load();var results=new System.Collections.Generic.List<object>();
foreach(var body in catalog.bodies)
{
    var parts=body.model.GetComponentsInChildren<SkinnedMeshRenderer>();var hands=parts.Single(r=>r.name=="Hands");var sleeve=parts.Single(r=>r.name=="UpperBody");
    var source=hands.sharedMesh;var vertices=source.vertices;var weights=source.boneWeights;
    var garment=sleeve.sharedMesh;var garmentVertices=garment.vertices;var garmentWeights=garment.boneWeights;
    float cuff=garment.bounds.extents.x;int changed=0;
    var boneMap=sleeve.bones.Select(b=>System.Array.FindIndex(hands.bones,h=>h.name==b.name)).ToArray();
    if(boneMap.Any(i=>i<0))throw new System.Exception("Incompatible bone map");
    void Add(System.Collections.Generic.Dictionary<int,float> map,BoneWeight w,float amount,bool garmentBone)
    {
        var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var values=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
        for(int j=0;j<4;j++){int id=garmentBone?boneMap[ids[j]]:ids[j];map.TryGetValue(id,out float old);map[id]=old+values[j]*amount;}
    }
    for(int i=0;i<vertices.Length;i++)
    {
        var v=vertices[i];float x=Mathf.Abs(v.x);
        // Inner skin lies inside the sleeve. Blend only across the cuff; retain hand/wrist weights exactly.
        float original=Mathf.SmoothStep(0,1,Mathf.InverseLerp(cuff-.07f,cuff+.04f,x));if(original>=1)continue;
        var closest=garmentVertices.Select((p,index)=>new{index,d=(p-v).sqrMagnitude}).OrderBy(p=>p.d).Take(3).ToArray();
        float sum=closest.Sum(p=>1/Mathf.Max(.000001f,p.d));var blend=new System.Collections.Generic.Dictionary<int,float>();
        foreach(var p in closest)Add(blend,garmentWeights[p.index],(1-original)/Mathf.Max(.000001f,p.d)/sum,true);
        Add(blend,weights[i],original,false);var sorted=blend.OrderByDescending(p=>p.Value).Take(4).ToArray();float total=sorted.Sum(p=>p.Value);
        int Id(int j)=>j<sorted.Length?sorted[j].Key:0;float W(int j)=>j<sorted.Length?sorted[j].Value/total:0;
        weights[i]=new BoneWeight{boneIndex0=Id(0),boneIndex1=Id(1),boneIndex2=Id(2),boneIndex3=Id(3),weight0=W(0),weight1=W(1),weight2=W(2),weight3=W(3)};changed++;
    }
    string path="Assets/_Project/Player/Assets/Appearance/Models/"+body.id+"-sleeve-skin.asset";
    var derived=UnityEngine.Object.Instantiate(source);derived.name=body.id+" Sleeve Skin";derived.boneWeights=weights;
    var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
    if(existing==null){AssetDatabase.CreateAsset(derived,path);existing=derived;}else{EditorUtility.CopySerialized(derived,existing);UnityEngine.Object.DestroyImmediate(derived);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing);}
    body.sleeveSkinMesh=existing;
    if(!source.vertices.SequenceEqual(existing.vertices)||!source.uv.SequenceEqual(existing.uv)||!source.triangles.SequenceEqual(existing.triangles)||!source.bindposes.SequenceEqual(existing.bindposes))throw new System.Exception("Derived mesh altered geometry, UV or bind poses");
    results.Add(new{body.id,changed,total=vertices.Length,cuff});
}
EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);return results;
