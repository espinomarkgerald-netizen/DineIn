#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TMPro;

public static class KitchenHudCleanup
{
    const string HudPath="Assets/_Project/Resources/UI/LobbyHUD.prefab";
    static T Ref<T>(SerializedObject data,string key) where T:UnityEngine.Object=>(T)data.FindProperty(key).objectReferenceValue;
    [MenuItem("Dine In/Fast Food/Inspect HUD Cleanup")]
    public static void Inspect()
    {
        var view=UnityEngine.Object.FindObjectsByType<FastFoodCookingView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
        var data=new SerializedObject(view);
        Debug.Log("[HUD cleanup] playing="+Application.isPlaying+" Lobby2 dirty="+view.gameObject.scene.isDirty);
        foreach(var key in new[]{"hotbar","hotbarContainer","notification","noticeButtonLabel"})
        {
            var c=Ref<Component>(data,key);var rt=(RectTransform)c.transform;
            Debug.Log("[HUD cleanup] "+key+" parent="+rt.parent.name+" anchors="+rt.anchorMin+"/"+rt.anchorMax+" pivot="+rt.pivot+" pos="+rt.anchoredPosition+" size="+rt.sizeDelta);
        }
    }
    [MenuItem("Dine In/Fast Food/Apply HUD Cleanup")]
    public static void Apply()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var view=UnityEngine.Object.FindObjectsByType<FastFoodCookingView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
        if(view.gameObject.scene.name!="Lobby2")throw new InvalidOperationException("Open Lobby2.");
        if(view.gameObject.scene.isDirty)throw new InvalidOperationException("Save Lobby2's current edits before applying HUD cleanup.");
        var stage=UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if(stage!=null && stage.assetPath==HudPath && stage.scene.isDirty)throw new InvalidOperationException("Save the open LobbyHUD prefab first.");
        Undo.RegisterFullObjectHierarchyUndo(view.gameObject,"Kitchen HUD cleanup");
        var data=new SerializedObject(view);
        var staff=Ref<TMP_Text>(data,"staffActivity");if(staff!=null){staff.text="";staff.transform.parent.gameObject.SetActive(false);}
        var station=Ref<RectTransform>(data,"station");
        float headerBottom=((RectTransform)Ref<TMP_Text>(data,"heading").transform.parent).offsetMin.y;
        var tickets=Ref<RectTransform>(data,"tickets");tickets.anchoredPosition=new Vector2(32,headerBottom-24);
        var bar=Ref<RectTransform>(data,"hotbarContainer");
        Fixed(bar,new Vector2(.5f,0),new Vector2(0,28),new Vector2(760,152));
        var scroll=bar.GetComponent<UnityEngine.UI.ScrollRect>();
        scroll.horizontal=scroll.vertical=false;scroll.inertia=false;
        var viewport=scroll.viewport;viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;viewport.offsetMin=Vector2.one*8;viewport.offsetMax=Vector2.one*-8;
        var content=scroll.content;content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1);content.anchoredPosition=Vector2.zero;
        var grid=content.GetComponent<UnityEngine.UI.GridLayoutGroup>();grid.padding=new RectOffset(4,4,4,4);grid.spacing=new Vector2(10,10);
        grid.startCorner=UnityEngine.UI.GridLayoutGroup.Corner.UpperLeft;grid.startAxis=UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;grid.childAlignment=TextAnchor.UpperLeft;
        var fitter=content.GetComponent<UnityEngine.UI.ContentSizeFitter>();
        if(fitter!=null){fitter.horizontalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;fitter.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;}
        var notice=Ref<RectTransform>(data,"notification");
        var toggle=(RectTransform)Ref<TMP_Text>(data,"noticeButtonLabel").transform.parent;
        var exit=(RectTransform)station.Find("Exit Kitchen");
        float toggleY=exit.offsetMin.y-12;
        Fixed(toggle,Vector2.one,new Vector2(-24,toggleY),new Vector2(190,56));
        Fixed(notice,Vector2.one,new Vector2(-24,toggleY-68),new Vector2(360,260));
        var alerts=Ref<TMP_Text>(data,"alerts");
        var status=(RectTransform)alerts.transform.parent;
        Stretch(status,new Vector2(.04f,.31f),new Vector2(.96f,.80f));
        Stretch(alerts.rectTransform,new Vector2(.04f,.05f),new Vector2(.96f,.95f));
        alerts.fontSize=24;alerts.enableAutoSizing=true;alerts.fontSizeMax=24;alerts.fontSizeMin=18;alerts.alignment=TextAlignmentOptions.TopLeft;
        var go=(RectTransform)notice.Find("Go to Restock");Stretch(go,new Vector2(.06f,.04f),new Vector2(.94f,.28f));
        var heading=notice.Find("Restock Heading") as RectTransform;
        if(heading==null)
        {
            var text=UnityEngine.Object.Instantiate(Ref<TMP_Text>(data,"noticeButtonLabel"),notice);
            text.name="Restock Heading";heading=text.rectTransform;
        }
        Stretch(heading,new Vector2(.06f,.82f),new Vector2(.94f,.98f));
        var title=heading.GetComponent<TMP_Text>();title.text="RESTOCK";title.fontSize=30;title.alignment=TextAlignmentOptions.MidlineLeft;title.raycastTarget=false;
        data.FindProperty("restockHeading").objectReferenceValue=title;data.ApplyModifiedPropertiesWithoutUndo();
        var prefab=PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            var presenter=prefab.GetComponentInChildren<PlayerTaskHUD>(true);
            if(presenter==null)throw new InvalidOperationException("LobbyHUD has no task presenter.");
            var p=new SerializedObject(presenter);var button=Ref<RectTransform>(p,"buttonRect");var panel=Ref<RectTransform>(p,"panelRect");
            var buttonDock=Dock(button.parent,"Kitchen Task Button Layout");
            var panelDock=Dock(panel.parent,"Kitchen Task Panel Layout");
            Fixed(buttonDock,new Vector2(1,0),new Vector2(-24,24),new Vector2(112,128));
            Fixed(panelDock,new Vector2(1,0),new Vector2(-24,160),new Vector2(340,148));
            p.FindProperty("kitchenButtonLayout").objectReferenceValue=buttonDock;p.FindProperty("kitchenPanelLayout").objectReferenceValue=panelDock;
            p.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(prefab,HudPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(prefab);}
        EditorUtility.SetDirty(view);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(view.gameObject.scene);
        Debug.Log("[HUD cleanup] Saved hidden staff bar, centered complete-row hotbar, restock hierarchy and kitchen task dock.");
    }
    static RectTransform Dock(Transform parent,string name)
    {
        var r=parent.Find(name) as RectTransform;
        if(r==null){r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);}
        return r;
    }
    [MenuItem("Dine In/Fast Food/Validate HUD Cleanup")]
    public static void Validate()
    {
        var view=UnityEngine.Object.FindObjectsByType<FastFoodCookingView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
        var data=new SerializedObject(view);
        void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("[HUD cleanup] "+message);}
        Check(!Ref<TMP_Text>(data,"staffActivity").transform.parent.gameObject.activeSelf,"Staff bar is active.");
        var canvas=Ref<Canvas>(data,"canvas");
        var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
        Rect Bounds(RectTransform r, Rect screen)
        {
            if(r==canvas.transform)return screen;
            var parent=Bounds((RectTransform)r.parent,screen);
            return Rect.MinMaxRect(parent.xMin+parent.width*r.anchorMin.x+r.offsetMin.x,parent.yMin+parent.height*r.anchorMin.y+r.offsetMin.y,
                parent.xMin+parent.width*r.anchorMax.x+r.offsetMax.x,parent.yMin+parent.height*r.anchorMax.y+r.offsetMax.y);
        }
        foreach(var pixels in new[]{new Vector2(1280,720),new Vector2(1920,1200),new Vector2(2560,1080),new Vector2(2340,1080),new Vector2(1920,1440)})
        foreach(float scale in new[]{.85f,1f,1.15f})
        {
            float units=Mathf.Pow(pixels.x/scaler.referenceResolution.x,1-scaler.matchWidthOrHeight)*Mathf.Pow(pixels.y/scaler.referenceResolution.y,scaler.matchWidthOrHeight)*scale;
            var screen=new Rect(Vector2.zero,(pixels-new Vector2(100,0))/units);
            var restock=Bounds(Ref<RectTransform>(data,"notification"),screen);
            var task=new Rect(screen.width-364,160,340,148);
            Check(!restock.Overlaps(task),"Restock/task overlap at "+pixels+" scale "+scale);
            Check(restock.yMin>=0 && restock.xMin>=0 && restock.xMax<=screen.width,"Restock clipped at "+pixels);
            for(int count=1;count<=12;count++)
            {
                float available=Mathf.Min(760,screen.width*.46f)-24;
                int columns=Mathf.Clamp(Mathf.FloorToInt((available+10)/106),1,count);
                float cell=Mathf.Min(128,(available-(columns-1)*10)/columns);
                float width=24+columns*cell+(columns-1)*10;
                Check(width<=screen.width && cell>=96,"Hotbar fit failed at "+pixels);
                var hotbar=new Rect((screen.width-width)/2,28,width,24+Mathf.Ceil(count/(float)columns)*cell+(Mathf.Ceil(count/(float)columns)-1)*10);
                Check(!hotbar.Overlaps(task),"Hotbar/task overlap at "+pixels);
            }
        }
        Debug.Log("[HUD cleanup] PASS: hidden staff bar; restock, task and 1–12 item hotbar bounds/separation at five landscape ratios and three UI scales with notch allowance. Geometry check only.");
    }
    static void Fixed(RectTransform r,Vector2 anchor,Vector2 position,Vector2 size)
    {r.anchorMin=r.anchorMax=r.pivot=anchor;r.anchoredPosition=position;r.sizeDelta=size;}
    static void Stretch(RectTransform r,Vector2 min,Vector2 max)
    {r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
}
#endif
