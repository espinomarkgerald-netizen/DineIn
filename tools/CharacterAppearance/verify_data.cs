// Edit-mode, in-memory checks only. Never commits, saves, logs in, or publishes network data.
if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Edit Mode only");
var report=new System.Collections.Generic.List<string>();
void Check(bool condition,string name){if(!condition)throw new System.Exception(name);report.Add(name);}
var c=DineIn.Appearance.AppearanceCatalog.Load();
var groups=new DineIn.Appearance.AppearanceCatalog.Option[][]{c.bodies,c.skins,c.faces,c.hairs,c.hairColors,c.outfits,c.hats};
foreach(var group in groups)Check(group.Length>0&&group.All(o=>o!=null&&!string.IsNullOrWhiteSpace(o.id))&&group.Select(o=>o.id).Distinct().Count()==group.Length,"Unique populated category IDs: "+group[0].GetType().Name);
foreach(var body in c.bodies)
{
    Check(c.Validate(body.defaults).SameAs(body.defaults),body.id+" defaults valid");
    var avatar=body.model.GetComponent<Animator>().avatar;
    Check(avatar!=null&&avatar.isHuman&&avatar.isValid,body.id+" humanoid avatar valid");
}
Check(c.outfits.All(o=>o.material!=null&&o.material.mainTexture!=null),"Every outfit has a texture/material");
Check(c.bodies.Cast<DineIn.Appearance.AppearanceCatalog.Option>().Concat(c.outfits).Concat(c.faces).Concat(c.hairs).Concat(c.hats).All(o=>o.thumbnail!=null),"Every visual option has a thumbnail");
var original=c.defaults.Copy();var draft=original.Copy();draft.bodyId="female";draft=c.Validate(draft);
Check(original.SameAs(c.defaults)&&draft.bodyId=="female"&&DineIn.Appearance.AppearanceCatalog.Find(c.hairs,draft.hairId).Fits("female")&&DineIn.Appearance.AppearanceCatalog.Find(c.outfits,draft.outfitId).Fits("female"),"Draft copy and body compatibility isolation");
var invalid=new DineIn.Appearance.AppearanceRecipe{bodyId="unknown",skinId="unknown",faceId="unknown",hairId="unknown",hairColorId="unknown",outfitId="unknown",hatId="unknown"};
Check(c.Validate(invalid).SameAs(c.defaults),"Unknown IDs resolve to compatible defaults");
var randomState=UnityEngine.Random.state;
var employee=c.GenerateEmployee("persistent-employee-42");
Check(employee.SameAs(c.GenerateEmployee("persistent-employee-42")),"Employee identity generation deterministic");
Check(JsonUtility.ToJson(randomState)==JsonUtility.ToJson(UnityEngine.Random.state),"Employee cosmetics do not consume gameplay RNG");
foreach(var restaurant in new[]{"Casual","FastFood"})
{
    var uniforms=Resources.Load<DineIn.Appearance.EmployeeUniforms>(restaurant+"EmployeeUniforms");
    foreach(EmployeeRole role in System.Enum.GetValues(typeof(EmployeeRole)))foreach(var body in c.bodies)
    {
        var assignment=uniforms.assignments.Single(x=>x.role==role&&x.bodyId==body.id);
        Check(DineIn.Appearance.AppearanceCatalog.Find(c.outfits,assignment.outfitId)?.Fits(body.id)==true,restaurant+" uniform "+role+"/"+body.id);
        var personal=c.GenerateEmployee(role+body.id);personal.bodyId=body.id;personal=c.Validate(personal);var before=personal.Copy();
        var dressed=uniforms.Apply(personal,role,c);
        Check(personal.SameAs(before)&&dressed.skinId==personal.skinId&&dressed.faceId==personal.faceId&&dressed.hairId==personal.hairId,"Uniform preserves personal traits "+restaurant+role+body.id);
    }
}
var entry=new EmployeeSaveEntry{employeeID="persistent-employee-42",appearance=employee.Copy()};
Check(JsonUtility.FromJson<EmployeeSaveEntry>(JsonUtility.ToJson(entry)).appearance.SameAs(employee),"Employee recipe survives existing save/network entry serialization");
var oldEntry=JsonUtility.FromJson<EmployeeSaveEntry>("{\"employeeID\":\"persistent-employee-42\",\"employeeName\":\"Legacy Test\",\"stars\":2}");
var oldEmployee=new EmployeeData("Legacy Test",2,EmployeeRole.Waiter){appearance=oldEntry.appearance};oldEmployee.RestoreIdentity(oldEntry.employeeID);
DineIn.Appearance.EmployeeAppearance.Ensure(oldEmployee);
Check(oldEmployee.appearance.SameAs(employee),"Pre-customization employee save migrates its missing/empty recipe");
var established=oldEmployee.appearance;DineIn.Appearance.EmployeeAppearance.Ensure(oldEmployee);
Check(ReferenceEquals(established,oldEmployee.appearance),"Established employee identity is never rerolled");
Check(PlayerCustomizationData.TryReadAppearance("{\"Version\":1,\"HeadColorIndex\":2,\"BodyColorIndex\":1,\"OwnedHats\":[0,1]}",out var legacy)&&legacy==null,"V1 payload remains readable without replacing the legacy body");
foreach(var bad in new[]{"{}","{\"Version\":2}","{\"Appearance\":null}","garbage","{\"Version\":99,\"HeadColorIndex\":0}"})Check(!PlayerCustomizationData.TryReadAppearance(bad,out _),"Malformed/unsupported cloud payload rejected: "+bad);
var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
var cacheField=typeof(LocalSaveManager).GetField("_cache",flags);var accountField=typeof(PlayerCustomizationData).GetField("accountId",flags);
var oldCache=cacheField.GetValue(null);var oldAccount=accountField.GetValue(null);
try
{
    var json=(string)typeof(PlayerCustomizationData).GetMethod("Serialize",flags).Invoke(null,new object[]{employee});
    Check(PlayerCustomizationData.TryReadAppearance(json,out var decoded)&&decoded.SameAs(employee),"V2 player payload roundtrip");
    var record=new LocalSaveManager.AppearanceRecord{accountId="playfab:appearance-test",json=json,revision=3,pendingUpload=true};
    var local=new LocalSaveManager.SaveData{coins=123,appearances=new(){record,new(){accountId="guest",json="guest-record"}}};
    cacheField.SetValue(null,local);accountField.SetValue(null,record.accountId);
    Check(!PlayerCustomizationData.TryAcceptCloud(record.accountId,3,json),"Pending offline Apply protected from cloud response");
    record.pendingUpload=false;
    Check(!PlayerCustomizationData.TryAcceptCloud("playfab:other-account",3,json),"Account-switch stale callback rejected");
    Check(!PlayerCustomizationData.TryAcceptCloud(record.accountId,2,json),"Earlier revision response rejected");
    Check(!PlayerCustomizationData.TryAcceptCloud(record.accountId,3,"{}"),"Invalid cloud response preserves local data");
    Check(!PlayerCustomizationData.TryAcceptCloud(record.accountId,3,"{\"Version\":1,\"HeadColorIndex\":0}"),"Legacy cloud data cannot erase modern local recipe");
    var copy=PlayerCustomizationData.CommittedAppearance;copy.hairId="bald";
    Check(record.json==json&&local.appearances[1].json=="guest-record", "Draft edits cannot mutate account or guest committed data");
    var roundtrip=JsonUtility.FromJson<LocalSaveManager.SaveData>(JsonUtility.ToJson(local));
    Check(roundtrip.coins==123&&roundtrip.appearances.Count==2&&roundtrip.appearances[0].revision==3,"Existing local-save data and profile records roundtrip together");
}
finally{cacheField.SetValue(null,oldCache);accountField.SetValue(null,oldAccount);}
return new{passed=report.Count,checks=report,noDiskSaveOrNetworkCalls=true};
