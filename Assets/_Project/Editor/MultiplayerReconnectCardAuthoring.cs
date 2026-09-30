#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Adds the last-run card to the loaded menu without reopening or rebuilding its scene.</summary>
public static class MultiplayerReconnectCardAuthoring
{
    private const string MenuPath="Assets/_Project/Scenes/NewMenu/NewGameMenu.unity";

    [MenuItem("Dine In/Polish/Add Last Run Reconnect Card")]
    public static void AuthorActiveScene()
    {
        var scene=MenuScene();
        var controller=Controller(scene);
        var serialized=new SerializedObject(controller);
        var contents=(GameObject)serialized.FindProperty("joinContents").objectReferenceValue;
        var input=(TMP_InputField)serialized.FindProperty("codeInput").objectReferenceValue;
        var join=(UnityEngine.UI.Button)serialized.FindProperty("joinButton").objectReferenceValue;
        var code=(TMP_Text)serialized.FindProperty("joinCodeText").objectReferenceValue;
        var tab=(UnityEngine.UI.Button)serialized.FindProperty("joinTabButton").objectReferenceValue;
        var background=contents.transform.Find("Background") as RectTransform;
        if(background==null || input==null || join==null || code==null || tab==null)
            throw new InvalidOperationException("The existing Join controls are incomplete.");
        TMP_Text status=null;
        var statuses=serialized.FindProperty("statusTexts");
        for(int i=0;i<statuses.arraySize;i++)
        {
            var candidate=statuses.GetArrayElementAtIndex(i).objectReferenceValue as TMP_Text;
            if(candidate!=null && candidate.transform.IsChildOf(contents.transform)) {status=candidate;break;}
        }
        if(status==null) throw new InvalidOperationException("Join status label is missing.");
        int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Add last run reconnect card");
        var column=Node(background,"JoinActions");
        Place(column,new Vector2(.037f,.045f),new Vector2(.426f,.955f));
        var layout=Get<UnityEngine.UI.VerticalLayoutGroup>(column);
        Undo.RecordObject(layout,"Arrange Join controls");
        layout.childControlWidth=layout.childControlHeight=true;
        layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
        layout.childAlignment=TextAnchor.UpperLeft;layout.spacing=4;layout.padding=new RectOffset();

        var card=Node(column,"ReconnectLastRun");
        var cardImage=Get<UnityEngine.UI.Image>(card);CopyImage(join.GetComponent<UnityEngine.UI.Image>(),cardImage);
        cardImage.raycastTarget=false;
        Height(card,96);
        var heading=Label(card,"Heading",code,"RECONNECT TO LAST RUN",18);
        heading.color=new Color(.07f,.24f,.28f);
        Place(heading.rectTransform,new Vector2(.03f,.71f),new Vector2(.97f,.97f));
        var description=Label(card,"Description",code,"Casual Dining\nAvailability checked on reconnect.",16);
        description.color=heading.color;description.richText=false;description.fontSizeMin=12;
        Place(description.rectTransform,new Vector2(.04f,.36f),new Vector2(.96f,.72f));
        var reconnect=card.Find("Reconnect")?.GetComponent<UnityEngine.UI.Button>();
        if(reconnect==null)
        {
            var buttonObject=UnityEngine.Object.Instantiate(join.gameObject,card,false);buttonObject.name="Reconnect";
            Undo.RegisterCreatedObjectUndo(buttonObject,"Create reconnect button");
            reconnect=buttonObject.GetComponent<UnityEngine.UI.Button>();
        }
        Undo.RecordObject(reconnect,"Wire reconnect button");
        reconnect.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();
        CopyImage(tab.GetComponent<UnityEngine.UI.Image>(),reconnect.GetComponent<UnityEngine.UI.Image>());
        reconnect.targetGraphic=reconnect.GetComponent<UnityEngine.UI.Image>();reconnect.interactable=true;
        Place((RectTransform)reconnect.transform,new Vector2(.07f,.045f),new Vector2(.93f,.35f));
        var buttonLabel=reconnect.GetComponentInChildren<TMP_Text>(true);
        Undo.RecordObject(buttonLabel,"Label reconnect button");
        buttonLabel.text="RECONNECT";buttonLabel.color=Color.white;
        buttonLabel.enableAutoSizing=true;buttonLabel.fontSize=buttonLabel.fontSizeMax=20;buttonLabel.fontSizeMin=16;
        Place(buttonLabel.rectTransform,Vector2.zero,Vector2.one,new Vector2(5,1),new Vector2(-5,-1));
        card.SetSiblingIndex(0);

        MoveRow(code.rectTransform,column,1,20);
        Undo.RecordObject(code,"Fit Join code label");code.fontSize=code.fontSizeMax=18;code.fontSizeMin=14;
        code.alignment=TextAlignmentOptions.MidlineLeft;
        MoveRow((RectTransform)input.transform,column,2,30);
        MoveRow((RectTransform)join.transform,column,3,30);
        MoveRow(status.rectTransform,column,4,34);
        Undo.RecordObject(status,"Fit Join status");status.fontSize=16;status.fontSizeMax=16;status.fontSizeMin=12;
        status.enableAutoSizing=true;
        card.gameObject.SetActive(false);
        Undo.RecordObject(controller,"Bind last run reconnect controls");
        serialized.Update();
        serialized.FindProperty("reconnectCard").objectReferenceValue=card.gameObject;
        serialized.FindProperty("reconnectButton").objectReferenceValue=reconnect;
        serialized.FindProperty("reconnectDescription").objectReferenceValue=description;
        serialized.ApplyModifiedProperties();
        ValidateActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the current NewGameMenu scene.");
        Undo.CollapseUndoOperations(undo);
        Debug.Log("[Reconnect UI] Added the last-run card above existing Join controls; hidden cards collapse automatically.");
    }

    public static string ValidateActiveScene()
    {
        var controller=Controller(MenuScene());
        var so=new SerializedObject(controller);
        var card=so.FindProperty("reconnectCard").objectReferenceValue as GameObject;
        var reconnect=so.FindProperty("reconnectButton").objectReferenceValue as UnityEngine.UI.Button;
        var description=so.FindProperty("reconnectDescription").objectReferenceValue as TMP_Text;
        if(card==null || reconnect==null || description==null || !reconnect.transform.IsChildOf(card.transform) ||
            !description.transform.IsChildOf(card.transform)) throw new InvalidOperationException("Reconnect card references are incomplete.");
        var column=card.transform.parent as RectTransform;
        var layout=column.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        if(layout==null || card.transform.GetSiblingIndex()!=0) throw new InvalidOperationException("Reconnect card must precede the normal Join controls.");
        foreach(string field in new[]{"joinButton","codeInput","joinCodeText"})
        {
            var control=so.FindProperty(field).objectReferenceValue as Component;
            if(control==null || control.transform.parent!=column) throw new InvalidOperationException("Existing Join control was not preserved: "+field);
        }
        float needed=layout.spacing*Mathf.Max(0,column.childCount-1)+layout.padding.vertical;
        foreach(Transform child in column)
        {
            var element=child.GetComponent<UnityEngine.UI.LayoutElement>();
            if(element==null) throw new InvalidOperationException("A Join control has no preferred height.");
            needed+=Mathf.Max(element.preferredHeight,element.minHeight);
        }
        var parent=(RectTransform)column.parent;
        float available=parent.rect.height*(column.anchorMax.y-column.anchorMin.y)+column.sizeDelta.y;
        if(needed>available+.5f) throw new InvalidOperationException("Reconnect and normal Join controls exceed their left column.");
        if(reconnect.onClick.GetPersistentEventCount()!=0) throw new InvalidOperationException("Reconnect button copied an unrelated persistent listener.");
        return "PASS: reconnect references, preserved Join controls, card-first collapsing layout, and column height budget.";
    }

    private static Scene MenuScene()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before editing the menu.");
        var scene=SceneManager.GetActiveScene();
        if(!scene.isLoaded || scene.path!=MenuPath) throw new InvalidOperationException("Open NewGameMenu as the active scene first. No scenes were reloaded.");
        return scene;
    }
    private static MultiplayerMenuController Controller(Scene scene)
    {
        var matches=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<MultiplayerMenuController>(true)).ToArray();
        if(matches.Length!=1) throw new InvalidOperationException("Expected exactly one existing multiplayer menu controller.");
        return matches[0];
    }
    private static RectTransform Node(Transform parent,string name)
    {
        var existing=parent.Find(name) as RectTransform;if(existing!=null)return existing;
        var go=new GameObject(name,typeof(RectTransform));go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);
        Undo.RegisterCreatedObjectUndo(go,"Create reconnect controls");return (RectTransform)go.transform;
    }
    private static T Get<T>(RectTransform rect) where T:Component
    {
        var existing=rect.GetComponent<T>();return existing!=null?existing:Undo.AddComponent<T>(rect.gameObject);
    }
    private static void Place(RectTransform rect,Vector2 min,Vector2 max,Vector2 insetMin=default,Vector2 insetMax=default)
    {
        Undo.RecordObject(rect,"Place reconnect controls");
        rect.anchorMin=min;rect.anchorMax=max;rect.pivot=new Vector2(.5f,.5f);
        rect.offsetMin=insetMin;rect.offsetMax=insetMax;rect.localScale=Vector3.one;
    }
    private static void Height(RectTransform rect,float height)
    {
        var element=Get<UnityEngine.UI.LayoutElement>(rect);Undo.RecordObject(element,"Size Join row");
        element.minHeight=element.preferredHeight=height;element.flexibleHeight=0;
    }
    private static void MoveRow(RectTransform rect,RectTransform parent,int index,float height)
    {
        if(rect.parent!=parent)Undo.SetTransformParent(rect,parent,"Preserve Join control in new layout");
        Undo.RecordObject(rect,"Arrange Join controls");rect.localScale=Vector3.one;rect.SetSiblingIndex(index);Height(rect,height);
    }
    private static void CopyImage(UnityEngine.UI.Image source,UnityEngine.UI.Image target)
    {
        Undo.RecordObject(target,"Reuse existing menu art");
        target.sprite=source.sprite;target.material=source.material;target.type=source.type;
        target.color=source.color;target.pixelsPerUnitMultiplier=source.pixelsPerUnitMultiplier;
    }
    private static TMP_Text Label(Transform parent,string name,TMP_Text style,string text,float size)
    {
        var rect=Node(parent,name);var label=Get<TextMeshProUGUI>(rect);Undo.RecordObject(label,"Label reconnect card");
        label.font=style.font;label.fontSharedMaterial=style.fontSharedMaterial;label.fontStyle=style.fontStyle;
        label.text=text;label.color=style.color;label.raycastTarget=false;label.enableAutoSizing=true;
        label.fontSize=label.fontSizeMax=size;label.fontSizeMin=size*.8f;label.alignment=TextAlignmentOptions.Center;
        return label;
    }
}
#endif