using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Present only in the multiplayer gameplay scene; no persistent session state.
[DefaultExecutionOrder(-200)]
public class MultiplayerSessionManager : MonoBehaviourPunCallbacks
{
    public static MultiplayerSessionManager Instance { get; private set; }
    private readonly Dictionary<int, MultiplayerManagerRegistration> managers = new();

    public bool IsMultiplayerSession => Instance == this && isActiveAndEnabled && PhotonNetwork.InRoom;
    public bool IsAuthority => IsMultiplayerSession && PhotonNetwork.IsMasterClient;
    public Player LocalPhotonPlayer => IsMultiplayerSession ? PhotonNetwork.LocalPlayer : null;
    public int LocalActorNumber => LocalPhotonPlayer?.ActorNumber ?? 0;
    public Player[] ConnectedPlayers => IsMultiplayerSession ? PhotonNetwork.PlayerList : Array.Empty<Player>();
    public int ConnectedHumanCount => IsMultiplayerSession ? PhotonNetwork.CurrentRoom.PlayerCount : 0;
    public int PartySize => IsMultiplayerSession ? PhotonNetwork.CurrentRoom.MaxPlayers : 0;
    public string RestaurantType => IsMultiplayerSession
        ? PhotonNetwork.CurrentRoom.CustomProperties["RestaurantType"] as string ?? string.Empty
        : string.Empty;

    public GameObject LocalManager => TryGetManager(LocalActorNumber, out var manager)
        && manager.GetComponent<PhotonView>().IsMine ? manager : null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }
        Instance = this;
    }

    public bool TryGetManager(int actorNumber, out GameObject manager)
    {
        manager = null;
        if (!IsMultiplayerSession || !managers.TryGetValue(actorNumber, out var entry) || entry == null)
            return false;
        manager = entry.gameObject;
        return true;
    }

    internal void Register(MultiplayerManagerRegistration manager)
    {
        var view = manager.GetComponent<PhotonView>();
        if (!IsMultiplayerSession || manager.gameObject.scene != gameObject.scene || view.Owner == null)
            return;
        managers[view.OwnerActorNr] = manager;
    }

    internal void Unregister(int actorNumber, MultiplayerManagerRegistration manager)
    {
        if (managers.TryGetValue(actorNumber, out var current) && current == manager)
            managers.Remove(actorNumber);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer) => managers.Remove(otherPlayer.ActorNumber);
    public override void OnLeftRoom() => managers.Clear();
    public override void OnDisconnected(DisconnectCause cause) => managers.Clear();

    private void OnDestroy()
    {
        managers.Clear();
        if (Instance == this) Instance = null;
    }
}
