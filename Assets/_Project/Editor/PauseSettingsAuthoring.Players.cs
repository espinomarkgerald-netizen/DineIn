#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Targeted addition to existing pause prefabs; keeps all unrelated controls and references.</summary>
public static partial class PauseSettingsAuthoring
{
    [MenuItem("Dine In/Polish/Add Multiplayer Players Pause Tab")]
    public static void AuthorPlayersTabs()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before saving pause controls.");
        foreach(string path in new[]{"Assets/_Project/Gameplay/UI/Resources/LobbyPauseMenu.prefab","Assets/_Project/Resources/UI/LobbyHUD.prefab"})
        {
            var stage=UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if(stage!=null && stage.assetPath==path && stage.scene.isDirty)
                throw new InvalidOperationException("Save the open pause prefab first; its unsaved edits were preserved.");
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view=root.GetComponentInChildren<LobbyPauseMenuView>(true);
                var panel=root.GetComponentInChildren<PauseSettingsPanel>(true);
                if(view==null || panel==null) throw new InvalidOperationException("Missing existing pause settings in "+path);
                AuthorPlayers(panel);
                view.Overlay.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        Debug.Log("[Pause players] Saved multiplayer-only Players controls in both existing pause prefabs.");
    }

    private static void AuthorPlayers(PauseSettingsPanel panel)
    {
        var tabBar=panel.transform.Find("Tabs");
        if(tabBar==null || panel.pages.Length<4) throw new InvalidOperationException("Existing pause tabs/pages are missing.");
        var button=Button(tabBar,"PLAYERS","PLAYERS",Blue);
        button.GetComponent<UnityEngine.UI.Image>().sprite=panel.idleTabSprite;
        button.GetComponentInChildren<TMP_Text>().color=Navy;
        var page=Node(panel.transform,"PLAYERS Page");
        var template=(RectTransform)panel.pages[0].transform;
        Stretch(page,template.anchorMin,template.anchorMax,template.offsetMin,template.offsetMax);
        var contents=Content(page);
        var list=contents.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        list.spacing=10;list.padding=new RectOffset(20,20,14,14);
        var controller=Component<PausePlayersPanel>(page);

        var master=Row(contents,PauseSettingsPanel.Setting.Master,"Master voice","",0,0,1,"MasterVoice");
        controller.masterVolume=master.slider;controller.masterValue=master.value;
        master.slider.SetValueWithoutNotify(.8f);master.value.text="80%";

        var local=Node(contents,"LocalVoice");
        Component<UnityEngine.UI.LayoutElement>(local).preferredHeight=160;
        Paint(local,Color.white);
        var title=Text(local,"Label","Your voice and microphone",32,Navy);
        Stretch(title.rectTransform,new Vector2(.015f,.65f),new Vector2(.99f,.99f));
        controller.voiceToggle=Button(local,"VoiceEnabled","VOICE ON",Blue);
        Stretch((RectTransform)controller.voiceToggle.transform,new Vector2(.02f,.08f),new Vector2(.34f,.64f));
        controller.voiceToggleLabel=controller.voiceToggle.GetComponentInChildren<TMP_Text>();
        controller.microphoneToggle=Button(local,"Microphone","VOICE UNAVAILABLE",Blue);
        Stretch((RectTransform)controller.microphoneToggle.transform,new Vector2(.37f,.08f),new Vector2(.98f,.64f));
        controller.microphoneLabel=controller.microphoneToggle.GetComponentInChildren<TMP_Text>();
        controller.microphoneLabel.fontSize=controller.microphoneLabel.fontSizeMax=32;
        controller.microphoneLabel.fontSizeMin=26;

        controller.voiceStatus=Text(contents,"VoiceStatus","Voice connects automatically during multiplayer.",30,Navy);
        Component<UnityEngine.UI.LayoutElement>(controller.voiceStatus.rectTransform).preferredHeight=86;
        controller.emptyRoster=Text(contents,"EmptyRoster","Other players will appear here.",32,Navy);
        Component<UnityEngine.UI.LayoutElement>(controller.emptyRoster.rectTransform).preferredHeight=90;
        controller.remoteRows=new PausePlayersPanel.RemoteRow[3];
        for(int i=0;i<controller.remoteRows.Length;i++)
        {
            var controls=Row(contents,PauseSettingsPanel.Setting.Master,"Player","",0,0,1,"RemotePlayer"+(i+1));
            Component<UnityEngine.UI.LayoutElement>(controls.root).preferredHeight=160;
            var name=controls.root.Find("Label").GetComponent<TMP_Text>();name.richText=false;
            Stretch(name.rectTransform,new Vector2(.015f,.5f),new Vector2(.43f,.98f));
            var status=Text(controls.root,"Status","Connected",30,Navy);
            Stretch(status.rectTransform,new Vector2(.015f,.04f),new Vector2(.50f,.50f));
            var speaking=Node(controls.root,"Speaking");
            speaking.anchorMin=speaking.anchorMax=new Vector2(.46f,.74f);speaking.pivot=new Vector2(.5f,.5f);
            speaking.anchoredPosition=Vector2.zero;speaking.sizeDelta=new Vector2(24,24);
            var indicator=Paint(speaking,controller.quietColor);
            indicator.sprite=Art("Grey","check_round_color");indicator.type=UnityEngine.UI.Image.Type.Simple;indicator.preserveAspect=true;
            Stretch((RectTransform)controls.slider.transform,new Vector2(.52f,.48f),new Vector2(.81f,.97f));
            Stretch(controls.value.rectTransform,new Vector2(.83f,.52f),new Vector2(.98f,.95f));
            var mute=Button(controls.root,"Mute","MUTE",Blue);
            Stretch((RectTransform)mute.transform,new Vector2(.66f,.06f),new Vector2(.98f,.45f));
            controller.remoteRows[i]=new PausePlayersPanel.RemoteRow {
                root=controls.root.gameObject,name=name,status=status,speaking=indicator,
                volume=controls.slider,volumeValue=controls.value,mute=mute,muteLabel=mute.GetComponentInChildren<TMP_Text>()
            };
            controls.root.gameObject.SetActive(false);
        }
        var tabs=panel.tabs.Where(t=>t!=null && t!=button).ToList();tabs.Add(button);panel.tabs=tabs.ToArray();
        var pages=panel.pages.Where(p=>p!=null && p!=page.gameObject).ToList();pages.Add(page.gameObject);panel.pages=pages.ToArray();
        panel.playersTabIndex=panel.pages.Length-1;
        panel.SetMultiplayerTabsVisible(false);
        panel.SelectTab(0);
    }
}
#endif