using System;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using Settings = DineIn.NewMenu.SettingsManager;

/// <summary>Local listening controls for the existing Photon roster. All visuals are prefab-authored.</summary>
public sealed class PausePlayersPanel : MonoBehaviour
{
    [Serializable]
    public sealed class RemoteRow
    {
        public GameObject root;
        public TMP_Text name, status, volumeValue, muteLabel;
        public UnityEngine.UI.Image speaking;
        public UnityEngine.UI.Slider volume;
        public UnityEngine.UI.Button mute;
        [NonSerialized] public int actorNumber;
        [NonSerialized] public string userId;
    }

    public UnityEngine.UI.Button voiceToggle, microphoneToggle;
    public TMP_Text voiceToggleLabel, microphoneLabel, voiceStatus, masterValue, emptyRoster;
    public UnityEngine.UI.Slider masterVolume;
    public RemoteRow[] remoteRows;
    [Min(.1f)] public float refreshInterval = .25f;
    public Color speakingColor = new Color(.12f,.7f,.37f), quietColor = new Color(.55f,.61f,.66f);
    private Settings settings;
    private bool bound;
    private float nextRefresh;

    private void OnEnable()
    {
        if(!Application.isPlaying) return;
        Initialize(); Refresh();
    }

    public void Initialize()
    {
        if(bound) return;
        settings=Settings.EnsureInstance();
        if(settings==null) return;
        bound=true;
        voiceToggle.onClick.AddListener(()=>settings.SetVoiceEnabled(!settings.Current.voiceEnabled));
        microphoneToggle.onClick.AddListener(()=>settings.SetMicrophoneMuted(!settings.Current.microphoneMuted));
        masterVolume.onValueChanged.AddListener(settings.SetMasterVoiceVolume);
        foreach(var row in remoteRows)
        {
            var captured=row;
            row.volume.onValueChanged.AddListener(value=>settings.SetPlayerVoiceVolume(captured.userId,value));
            row.mute.onClick.AddListener(()=>settings.SetPlayerVoiceMuted(captured.userId,!settings.GetPlayerVoiceMuted(captured.userId)));
            row.name.richText=false;
        }
        settings.OnSettingsLoaded+=SettingsChanged;
    }

    private void OnDestroy()
    {
        if(settings!=null) settings.OnSettingsLoaded-=SettingsChanged;
    }
    private void SettingsChanged(DineIn.NewMenu.UserSettings _) { if(isActiveAndEnabled) Refresh(); }
    private void Update()
    {
        if(Time.unscaledTime<nextRefresh) return;
        Refresh();
    }

    public void Refresh()
    {
        if(!bound || settings==null) return;
        nextRefresh=Time.unscaledTime+refreshInterval;
        var session=MultiplayerSessionManager.Instance;
        var voice=session!=null?session.GetComponent<MultiplayerVoiceController>():null;
        bool active=session!=null && session.IsMultiplayerSession;
        var prefs=settings.Current;
        voiceToggleLabel.text=prefs.voiceEnabled?"VOICE CHAT: ON":"VOICE CHAT: OFF";
        microphoneLabel.text=voice!=null?voice.MicrophoneStatus:"VOICE UNAVAILABLE";
        voiceStatus.text=!active?"Voice is available during multiplayer runs.":voice!=null?voice.Status:"Voice chat is unavailable right now.";
        masterVolume.SetValueWithoutNotify(prefs.masterVoiceVolume);
        masterValue.text=Mathf.RoundToInt(prefs.masterVoiceVolume*100)+"%";
        microphoneToggle.interactable=prefs.voiceEnabled;
        var players=PhotonNetwork.InRoom?PhotonNetwork.PlayerList:Array.Empty<Player>();
        Array.Sort(players,(a,b)=>a.ActorNumber.CompareTo(b.ActorNumber));
        int index=0;
        foreach(var player in players)
        {
            if(player.IsLocal || index>=remoteRows.Length) continue;
            var row=remoteRows[index++];
            row.root.SetActive(true);
            row.actorNumber=player.ActorNumber;row.userId=player.UserId;
            string nickname=string.IsNullOrWhiteSpace(player.NickName)?"Player":player.NickName.Replace('\n',' ').Replace('\r',' ');
            row.name.text=nickname.Length>28?nickname.Substring(0,28):nickname;
            float volume=settings.GetPlayerVoiceVolume(row.userId);
            bool muted=settings.GetPlayerVoiceMuted(row.userId);
            bool speaking=!player.IsInactive && prefs.voiceEnabled &&
                voice!=null && voice.IsSpeaking(row.actorNumber);
            row.speaking.color=speaking?speakingColor:quietColor;
            row.status.text=player.IsInactive?"Reconnecting":muted?(speaking?"Speaking (muted)":"Muted for you"):!prefs.voiceEnabled?"Voice off":
                voice==null || !voice.Available?"Voice unavailable":!voice.Connected?"Connecting":speaking?"Speaking":"Not speaking";
            row.volume.SetValueWithoutNotify(volume);
            row.volumeValue.text=Mathf.RoundToInt(volume*100)+"%";
            row.muteLabel.text=muted?"UNMUTE":"MUTE";
            // UserIds are published by existing rooms. Never share preferences under an empty identity.
            bool hasIdentity=!string.IsNullOrEmpty(row.userId);
            row.volume.interactable=hasIdentity;row.mute.interactable=hasIdentity;
        }
        for(int i=index;i<remoteRows.Length;i++)
        {
            remoteRows[i].root.SetActive(false);remoteRows[i].actorNumber=0;remoteRows[i].userId=null;
        }
        emptyRoster.gameObject.SetActive(index==0);
        emptyRoster.text=active && !PhotonNetwork.InRoom?"Reconnecting to your multiplayer run…":"Other players will appear here.";
    }
}