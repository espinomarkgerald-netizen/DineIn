using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

// A day fence for transient interaction packets. No packet from an old shift may mutate a new one.
public static class MultiplayerWire
{
    public static bool Raise(byte code, object payload, RaiseEventOptions options, SendOptions send)
    {
        var session = MultiplayerSessionManager.Instance;
        return session != null && session.Run != null && GameFlowManager.Instance != null && session.IsConnected && !session.Ended && PhotonNetwork.RaiseEvent(code,
            new object[] { session.RunId, GameFlowManager.Instance.CurrentDay, payload }, options, send);
    }
    public static bool TryRead(EventData ev, out object payload)
    {
        payload = null;
        var session = MultiplayerSessionManager.Instance;
        if (session == null || session.Run == null || GameFlowManager.Instance == null || !session.IsConnected || session.Ended || ev.CustomData is not object[] envelope
            || envelope.Length != 3 || envelope[0] is not string run || run != session.RunId
            || envelope[1] is not int day || day != GameFlowManager.Instance.CurrentDay) return false;
        if (ev.Sender != session.Run.hostActor && !session.ValidActor(ev.Sender)) return false;
        payload = envelope[2];
        return true;
    }
}
