using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class FastFoodCozyPolishAuthoring
{
    [MenuItem("Dine In/Fast Food/Apply Magnetic Drop and HUD Polish")]
    public static string ApplyDragPolish()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode before authoring.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.name!="Lobby2")throw new InvalidOperationException("Open Lobby2 first.");
        var view=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<FastFoodCookingView>(true)).Single();
        Undo.RegisterFullObjectHierarchyUndo(view.gameObject,"Magnetic kitchen drops and HUD");
        var data=new SerializedObject(view);
        T Ref<T>(string key) where T:UnityEngine.Object=>(T)data.FindProperty(key).objectReferenceValue;
        var station=Ref<RectTransform>("station");
        foreach(var name in new[]{"Previous Station","Next Station"})
            Fixed((RectTransform)station.Find(name),new Vector2(.5f,1),new Vector2(name=="Previous Station"?-145:145,-26),new Vector2(58,58));
        Fixed((RectTransform)station.Find("Station Navigation Label"),new Vector2(.5f,1),new Vector2(0,-31),new Vector2(220,42));
        var header=(RectTransform)Ref<TMP_Text>("heading").transform.parent;
        header.anchorMax=new Vector2(.38f,1);
        foreach(var rect in new[]{(RectTransform)station.Find("Stations"),(RectTransform)station.Find("Exit Kitchen"),(RectTransform)Ref<TMP_Text>("noticeButtonLabel").transform.parent})
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,164);
            EditorUtility.SetDirty(rect);
        }
        CleanProgressBar(Ref<UnityEngine.UI.Image>("progressFill"));
        string ticketPath=FastFoodCookingAuthoring.TemplateFolder+"/Order Ticket.prefab";
        var root=PrefabUtility.LoadPrefabContents(ticketPath);
        try{CleanProgressBar(root.GetComponent<FastFoodCookingTicketView>().progressFill);PrefabUtility.SaveAsPrefabAsset(root,ticketPath);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
        CleanProgressBar(Ref<FastFoodCookingTicketView>("ticketTemplate").progressFill);
        string materialPath=Batch+"Kitchen Drop Destination.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(Ref<Material>("ghostMaterial")){name="Kitchen Drop Destination"};AssetDatabase.CreateAsset(material,materialPath);}
        material.SetColor("_BaseColor",new Color(1,1,1,.22f));
        // The dashed landing mesh is visible from either side of a perspective table.
        material.SetFloat("_Cull",0);EditorUtility.SetDirty(material);
        data.FindProperty("dropGhostMaterial").objectReferenceValue=material;
        data.ApplyModifiedProperties();
        foreach(var rig in view.GetComponentsInChildren<FastFoodCookingStation>(true))
            foreach(var target in rig.Slots.Concat(new[]{rig.preparation,rig.discard}).Where(t=>t!=null))
                if(target.hint!=null){target.hint.SetActive(false);EditorUtility.SetDirty(target.hint);}
        EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
        if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Lobby2 could not be saved.");
        return "Saved centered station navigation, equal 164-wide buttons, clean progress fills and white destination material.";
    }
    static void CleanProgressBar(UnityEngine.UI.Image fill)
    {
        foreach(Transform child in fill.transform)
            if(child.name.StartsWith("HUD Stripe",StringComparison.Ordinal)){child.gameObject.SetActive(false);EditorUtility.SetDirty(child.gameObject);}
        var mask=fill.GetComponent<UnityEngine.UI.Mask>();if(mask!=null)mask.enabled=false;
        var clip=fill.GetComponent<UnityEngine.UI.RectMask2D>();if(clip!=null)clip.enabled=false;
        Stretch(fill.rectTransform,Vector2.zero,Vector2.one);
        fill.rectTransform.offsetMin=new Vector2(4,4);fill.rectTransform.offsetMax=new Vector2(-4,-4);
        fill.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Restaurant/CookingAssets/Theme/Green depth_flat.asset");
        fill.type=UnityEngine.UI.Image.Type.Filled;fill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;
        fill.fillOrigin=0;fill.color=Color.white;fill.raycastTarget=false;
        EditorUtility.SetDirty(fill);EditorUtility.SetDirty(fill.rectTransform);
    }
}
