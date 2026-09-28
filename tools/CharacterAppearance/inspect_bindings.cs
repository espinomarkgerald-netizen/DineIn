if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var c=DineIn.Appearance.AppearanceCatalog.Load();
var results=new System.Collections.Generic.List<object>();
foreach(var a in c.hairs.Concat(c.hats))
    if(a.prefab!=null)results.Add(new{id=a.id,rotation=a.prefab.transform.localEulerAngles.ToString(),scale=a.prefab.transform.localScale.ToString(),position=a.prefab.transform.localPosition.ToString()});
var male=c.bodies[0].model.GetComponentsInChildren<SkinnedMeshRenderer>();
var female=c.bodies[1].model.GetComponentsInChildren<SkinnedMeshRenderer>();
foreach(var r in male)
{
    var f=female.First(x=>x.name==r.name);
    if(!r.bones.Select(b=>b.name).SequenceEqual(f.bones.Select(b=>b.name)))throw new System.Exception("Bone order mismatch: "+r.name);
    var a=r.sharedMesh.bindposes;var b=f.sharedMesh.bindposes;
    if(a.Length!=b.Length||a.Where((m,i)=>Enumerable.Range(0,16).Any(j=>Mathf.Abs(m[j]-b[i][j])>.0001f)).Any())throw new System.Exception("Bindpose mismatch: "+r.name);
}
return new{attachments=results,bodyRigCompatibility="identical bone order and bindposes"};
