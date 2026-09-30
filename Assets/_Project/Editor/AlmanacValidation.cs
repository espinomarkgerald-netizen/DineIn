#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicit, menu-only checks; does not enter Play Mode or write player/save data.</summary>
public static class AlmanacValidation
{
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Fields).GetValue(value);
    private static void Set(object value,string name,object item)=>value.GetType().GetField(name,Fields).SetValue(value,item);
    private static object Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Fields).Invoke(value,args);
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException("Almanac check failed: "+message);}

    [MenuItem("Dine In/Almanac/Validate Reader and Previews")]
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Run these checks in Edit Mode.");
        var menu=UnityEngine.Object.FindFirstObjectByType<AlmanacMenu>();Check(menu!=null,"menu is installed");
        var catalog=AssetDatabase.LoadAssetAtPath<AlmanacCatalog>("Assets/_Project/UI/Almanac/AlmanacCatalog.asset");var entries=catalog.LoadEntries();
        Check(catalog.entries.Select(e=>e.entryId).Distinct().Count()==catalog.entries.Length,"duplicate catalog IDs");
        var issues=AlmanacContentAuthoring.ValidateCatalog();Check(issues.Trim()=="Entries: "+entries.Count,issues);
        var stage=menu.GetComponent<AlmanacPreviewStage>();int models=0,poses=0,variants=0;
        menu.Close();
        try
        {
            Texture previous=null;
            foreach(var entry in entries.Where(e=>e.previewKind!=AlmanacPreviewKind.Image))
            {
                int count=Mathf.Max(1,entry.variants?.Length??0);
                for(int variant=0;variant<count;variant++)
                {
                    Check(stage.Show(entry,variant),entry.entryId+" preview missing");
                    Check(previous==null || previous==stage.Texture,"RenderTexture was recreated between models");previous=stage.Texture;
                    var model=Field<GameObject>(stage,"modelOffset");
                    Check(model.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"gameplay behaviours cloned into "+entry.entryId);
                    Check(model.GetComponentsInChildren<Collider>(true).Length==0,"physics cloned into "+entry.entryId);
                    Check(model.GetComponentsInChildren<AudioSource>(true).Length==0,"audio cloned into "+entry.entryId);
                    foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())Check(skin.bones.All(b=>b!=null),entry.entryId+" null skin bone");
                    Check(Resources.FindObjectsOfTypeAll<Camera>().Count(c=>c.name=="Preview Camera"&&c.enabled)==1,"multiple live preview cameras");
                    if(entry.previewKind==AlmanacPreviewKind.Character)
                    {
                        var animator=model.GetComponentsInChildren<Animator>().FirstOrDefault(a=>a.avatar!=null&&a.avatar.isHuman);
                        Check(animator!=null,entry.entryId+" needs a humanoid avatar");
                        var bone=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);var start=bone.localRotation;
                        Call(stage,"EvaluateAnimation",.5f);
                        Check(Quaternion.Angle(start,bone.localRotation)>.005f,entry.entryId+" idle/intro did not move");poses++;
                    }
                    variants++;
                }
                models++;
            }
            stage.Hide();Check(stage.Texture==null,"texture not released on hide");
            var menuGroup=Field<CanvasGroup>(menu,"menuGroup");float alpha=menuGroup.alpha;bool interactable=menuGroup.interactable;
            menu.Open();Check(menu.IsOpen&&!menuGroup.interactable,"modal capture");
            var cashier=entries.Find(e=>e.entryId=="staff-cashier");var family=entries.Find(e=>e.entryId=="customer-family");
            Call(menu,"Navigate",cashier,false);Call(menu,"Navigate",family,true);Call(menu,"GoBack");Check(Field<AlmanacEntryData>(menu,"selected")==cashier,"related-entry history");
            Call(menu,"SelectVariant",cashier,3);Check(Field<AlmanacEntryData>(menu,"selected")==cashier,"variant changes selection");
            Call(menu,"SelectVariant",family,0);Check(Field<AlmanacEntryData>(menu,"selected")==cashier,"stale variant replaced current page");
            var search=Field<TMPro.TMP_InputField>(menu,"search");search.text="cashier";Check(Field<List<AlmanacEntryData>>(menu,"visible").Contains(cashier) && Field<List<AlmanacEntryData>>(menu,"visible").Count < entries.Count(e=>e.category==AlmanacCategory.Staff),"search filter");
            search.text="there-is-no-such-entry";Check(Field<List<AlmanacEntryData>>(menu,"visible").Count==0,"empty search");
            Check(!Field<UnityEngine.UI.Button>(menu,"nextButton").interactable&&!Field<UnityEngine.UI.Button>(menu,"previousButton").interactable,"empty-search navigation");search.text="";
            var detail=Field<CanvasGroup>(menu,"detailGroup");detail.alpha=.37f;detail.transform.localScale=Vector3.one*.987f;
            var fade=(IEnumerator)Call(menu,"ChangePage",cashier,0);fade.MoveNext();
            Check(Mathf.Abs(detail.alpha-.37f)<.001f&&!detail.interactable&&!detail.blocksRaycasts,"interrupted page transition");(fade as IDisposable)?.Dispose();
            Call(menu,"Populate",cashier,0);
            menu.Close();Check(stage.Texture==null&&!menu.IsOpen,"close release");Check(Mathf.Abs(menuGroup.alpha-alpha)<.001f&&menuGroup.interactable==interactable,"menu state not restored");
            menu.Open();Check(Field<CanvasGroup>(menu,"paperGroup").interactable,"reopen interaction");menu.Close();
            var report="PASS: "+entries.Count+" entries and all links/images; "+models+" model entries, "+variants+" visual variants, "+poses+" animated character variants; one reused preview camera/texture; safe visual copies; search, empty results, history, stale variants, interrupted fade, close/reopen and menu restoration.";
            System.IO.Directory.CreateDirectory("../output/almanac");System.IO.File.WriteAllText("../output/almanac/validation.txt",report);Debug.Log("[Almanac] "+report);
        }
        finally{menu.Close();stage.Release();}
    }
}
#endif
