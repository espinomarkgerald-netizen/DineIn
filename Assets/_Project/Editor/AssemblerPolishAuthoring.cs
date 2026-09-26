#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AssemblerPolishAuthoring
{
    [MenuItem("Dine In/Fast Food/Polish Existing Assembler")]
    public static void Apply()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Exit Play Mode before authoring.");
        var view=UnityEngine.Object.FindObjectsByType<FastFoodCookingView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
        if(view.gameObject.scene.name!="Lobby2")throw new InvalidOperationException("Open Lobby2.");
        Undo.RegisterFullObjectHierarchyUndo(view.gameObject,"Polish assembler");
        var rig=view.GetComponentsInChildren<FastFoodCookingStation>(true).Single(s=>s.mode==FastFoodStationMode.Assembler);
        var tray=rig.preparation.transform.Find("Tray visual");
        var renderer=tray.GetComponent<MeshRenderer>();var bounds=renderer.bounds;
        // Sample the tray floor rather than the lip's maximum Y.
        var probe=new GameObject("Assembler floor probe"){hideFlags=HideFlags.HideAndDontSave};
        probe.transform.SetPositionAndRotation(tray.position,tray.rotation);probe.transform.localScale=tray.lossyScale;
        var collider=probe.AddComponent<MeshCollider>();
        collider.sharedMesh=tray.GetComponent<MeshFilter>().sharedMesh;
        Vector3 contact=bounds.center;
        try
        {
            Physics.SyncTransforms();
            if(!collider.Raycast(new Ray(bounds.center+Vector3.up,Vector3.down),out var hit,3))
                throw new InvalidOperationException("Cannot locate tray floor.");
            contact=hit.point;
        }
        finally{UnityEngine.Object.DestroyImmediate(probe);}
        if(rig.assemblySurface==null)
        {
            var point=new GameObject("Assembler Surface");Undo.RegisterCreatedObjectUndo(point,"Assembler surface");
            point.transform.SetParent(rig.transform,false);rig.assemblySurface=point.transform;
        }
        rig.assemblySurface.SetPositionAndRotation(contact,Quaternion.identity);
        rig.assemblyArea=new Vector2(bounds.size.x*.82f,bounds.size.z*.82f);
        rig.prepFoodAnchor.position=contact;
        var positions=new[]{new Vector2(-.68f,0),new Vector2(.68f,0),new Vector2(0,-.68f),new Vector2(0,.68f),
            new Vector2(-.68f,-.68f),new Vector2(.68f,-.68f),new Vector2(-.68f,.68f),new Vector2(.68f,.68f),Vector2.zero};
        for(int i=0;i<rig.trayAnchors.Length;i++)
            rig.trayAnchors[i].position=contact+new Vector3(positions[i%positions.Length].x,0,positions[i%positions.Length].y);
        rig.stationCamera.transform.LookAt(contact+Vector3.up*.25f);
        rig.cookingViewAnchor.SetPositionAndRotation(rig.stationCamera.transform.position,rig.stationCamera.transform.rotation);
        var data=new SerializedObject(view);
        T Ref<T>(string key) where T:UnityEngine.Object=>(T)data.FindProperty(key).objectReferenceValue;
        var staff=Ref<TMPro.TMP_Text>("staffActivity");
        var card=(RectTransform)staff.transform.parent;
        float laneTop=((RectTransform)Ref<TMPro.TMP_Text>("heading").transform.parent).offsetMin.y-14;
        Rect(card,new Vector2(.025f,1),new Vector2(.48f,1));
        card.offsetMin=new Vector2(0,laneTop-60);card.offsetMax=new Vector2(0,laneTop);
        staff.fontSize=30;staff.fontSizeMin=24;staff.fontSizeMax=30;staff.enableAutoSizing=true;
        staff.text="";card.gameObject.SetActive(false);
        var cue=Ref<FastFoodCookingToast>("floatingCue");
        cue.transform.SetParent(Ref<RectTransform>("station"),false);
        var cueRect=(RectTransform)cue.transform;
        Rect(cueRect,new Vector2(.50f,1),new Vector2(.94f,1));
        cueRect.offsetMin=new Vector2(0,laneTop-60);cueRect.offsetMax=new Vector2(0,laneTop);
        cue.rise=0;cue.startScale=1;cue.overshootScale=1;
        cue.label.fontSize=30;cue.label.fontSizeMin=24;cue.label.fontSizeMax=30;cue.label.enableAutoSizing=true;
        cue.label.textWrappingMode=TMPro.TextWrappingModes.NoWrap;cue.label.overflowMode=TMPro.TextOverflowModes.Ellipsis;
        cue.icon.rectTransform.sizeDelta=new Vector2(42,42);
        var tickets=Ref<RectTransform>("tickets");
        tickets.anchorMin=tickets.anchorMax=new Vector2(0,1);tickets.pivot=new Vector2(0,1);tickets.anchoredPosition=new Vector2(32,laneTop-10);
        var serve=Ref<UnityEngine.UI.Button>("serve");
        var serveRect=(RectTransform)serve.transform;
        serveRect.anchorMin=serveRect.anchorMax=new Vector2(.5f,0);serveRect.pivot=new Vector2(.5f,0);
        serveRect.anchoredPosition=new Vector2(0,32);serveRect.sizeDelta=new Vector2(310,104);
        var safe=Ref<Canvas>("canvas").GetComponent<UIScreenSafeArea>();
        if(safe!=null)
        {
            var settings=new SerializedObject(safe);var targets=settings.FindProperty("targets");
            for(int i=targets.arraySize-1;i>=0;i--)
                if(targets.GetArrayElementAtIndex(i).FindPropertyRelative("rect").objectReferenceValue==cue.transform)
                    targets.DeleteArrayElementAtIndex(i);
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorUtility.SetDirty(rig);EditorUtility.SetDirty(view);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(view.gameObject.scene);
        Debug.Log("[Assembler polish] Saved tray floor "+contact+", distinct slots, staff/cue lanes and compact Serve button in Lobby2.");
    }
    private static void Rect(RectTransform rect,Vector2 min,Vector2 max)
    {rect.anchorMin=min;rect.anchorMax=max;rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=rect.offsetMax=Vector2.zero;}
    [MenuItem("Dine In/Fast Food/Inspect Assembler Layout")]
    public static void Inspect()
    {
        var view=UnityEngine.Object.FindObjectsByType<FastFoodCookingView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
        var data=new SerializedObject(view);
        var rig=view.GetComponentsInChildren<FastFoodCookingStation>(true).Single(s=>s.mode==FastFoodStationMode.Assembler);
        Debug.Log("[Assembler audit] mode="+Application.isPlaying+" dirty="+view.gameObject.scene.isDirty+" prep="+rig.preparation.transform.position+" anchor="+rig.prepFoodAnchor.position+" camera="+rig.stationCamera.transform.position+" slots="+string.Join(";",rig.trayAnchors.Select(a=>a.position.ToString())));
        foreach(var r in rig.preparation.GetComponentsInChildren<Renderer>(true))Debug.Log("[Assembler audit] surface "+r.name+" "+r.bounds);
        foreach(var key in new[]{"staffActivity","floatingCue","serve","hotbarContainer","tickets","station"})
        {
            var c=(Component)data.FindProperty(key).objectReferenceValue;var rt=c.transform as RectTransform;
            Debug.Log("[Assembler audit] "+key+" "+c.name+" parent="+c.transform.parent.name+" anchors="+rt.anchorMin+"/"+rt.anchorMax+" pos="+rt.anchoredPosition+" size="+rt.sizeDelta);
        }
    }
}
#endif
