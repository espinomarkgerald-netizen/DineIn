using Photon.Pun;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;

// Inspect on an external development player. No per-frame console output or uploads.
public sealed class MultiplayerDiagnostics : MonoBehaviour
{
    [SerializeField] private float averageFrameMs, worstFrameMs;
    [SerializeField] private long managedBytes;
    [SerializeField] private long allocatedBytesPerFrame;
    [SerializeField] private int outgoingBytesPerSecond, incomingBytesPerSecond, queuedCommands, pingMs, actionBubbles;
    public static int RejectedActions { get; private set; }
    private float nextSample, frameTotal, worst;
    private int frames, outgoing, incoming;
    private ProfilerRecorder allocations;
    public static readonly ProfilerMarker CustomerState = new("DineIn.Multiplayer.CustomerState");
    public static readonly ProfilerMarker ObjectState = new("DineIn.Multiplayer.ObjectState");
    public static readonly ProfilerMarker EconomyState = new("DineIn.Multiplayer.EconomyState");
    public static void Rejected() { if (Debug.isDebugBuild) RejectedActions++; }
    private void Awake()
    {
        if (!Debug.isDebugBuild) { enabled = false; return; }
        PhotonNetwork.NetworkingClient.LoadBalancingPeer.TrafficStatsEnabled = true;
        allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
    }
    private void Update()
    {
        float ms = Time.unscaledDeltaTime * 1000f;
        frameTotal += ms; frames++; worst = Mathf.Max(worst, ms);
        if (Time.unscaledTime < nextSample) return;
        nextSample = Time.unscaledTime + 1f;
        averageFrameMs = frameTotal / Mathf.Max(1, frames); worstFrameMs = worst;
        frameTotal = worst = 0; frames = 0;
        var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
        int sent = peer.TrafficStatsOutgoing.TotalPacketBytes, received = peer.TrafficStatsIncoming.TotalPacketBytes;
        outgoingBytesPerSecond = Mathf.Max(0, sent - outgoing); incomingBytesPerSecond = Mathf.Max(0, received - incoming);
        outgoing = sent; incoming = received; queuedCommands = peer.QueuedOutgoingCommands; pingMs = PhotonNetwork.GetPing();
        managedBytes = Profiler.GetMonoUsedSizeLong(); actionBubbles = MultiplayerTaskPresentation.LiveCount;
        allocatedBytesPerFrame = allocations.Valid ? allocations.LastValue : -1;
    }
    private void OnDestroy() => allocations.Dispose();
}
