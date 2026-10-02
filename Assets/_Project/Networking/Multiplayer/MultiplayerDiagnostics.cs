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
    private float nextSample, frameTotal, worst, lastSample, lastReport;
    private int frames, outgoing, incoming;
    private ProfilerRecorder allocations;
    public static readonly ProfilerMarker CustomerState = new("DineIn.Multiplayer.CustomerState");
    public static readonly ProfilerMarker ObjectState = new("DineIn.Multiplayer.ObjectState");
    public static readonly ProfilerMarker EconomyState = new("DineIn.Multiplayer.EconomyState");
    [SerializeField] private bool logTaskDecisions;
    [SerializeField, Min(1f)] private float reportIntervalSeconds = 10f;
    private float nextReport;
    private static MultiplayerDiagnostics active;
    private int customerMessages, customerBytes;
    public static void RecordCustomerState(string json)
    {
        if (active == null) return;
        active.customerMessages++;
        active.customerBytes += System.Text.Encoding.UTF8.GetByteCount(json);
    }
    public static void TraceTask(string task, int actor, bool accepted, long revision, string reason, int completedActor)
    {
        if (active == null || (!active.logTaskDecisions && accepted)) return;
        Debug.Log($"[MultiplayerTask] time={Time.realtimeSinceStartup:F2} task={task} actor={actor} revision={revision} accepted={accepted} completedActor={completedActor} reason={reason}");
    }
    public static void TraceAction(MultiplayerActionResult result)
    {
        if (active == null || result?.context == null || (!active.logTaskDecisions && result.Accepted)) return;
        var c = result.context;
        if (!result.Accepted) Rejected();
        Debug.Log($"[MultiplayerAction] time={Time.realtimeSinceStartup:F2} actor={c.actor} epoch={c.loadEpoch} operation={c.operation} customer={c.customerView} order={c.orderNumber} lease={c.lease} expectedRevision={c.expectedRevision} revision={result.revision} outcome={result.outcome}");
    }
    public static void Rejected() { if (Debug.isDebugBuild) RejectedActions++; }
    private void Awake()
    {
        if (!Debug.isDebugBuild) { enabled = false; return; }
        active = this;
        var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
        peer.TrafficStatsEnabled = true;
        outgoing = peer.TrafficStatsOutgoing.TotalPacketBytes; incoming = peer.TrafficStatsIncoming.TotalPacketBytes;
        lastSample = lastReport = Time.unscaledTime;
        nextSample = lastSample + 1f; nextReport = lastReport + reportIntervalSeconds;
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
        float elapsed = Mathf.Max(0.001f, Time.unscaledTime - lastSample); lastSample = Time.unscaledTime;
        outgoingBytesPerSecond = Mathf.RoundToInt(Mathf.Max(0, sent - outgoing) / elapsed);
        incomingBytesPerSecond = Mathf.RoundToInt(Mathf.Max(0, received - incoming) / elapsed);
        outgoing = sent; incoming = received; queuedCommands = peer.QueuedOutgoingCommands; pingMs = PhotonNetwork.GetPing();
        managedBytes = Profiler.GetMonoUsedSizeLong(); actionBubbles = MultiplayerTaskPresentation.LiveCount;
        allocatedBytesPerFrame = allocations.Valid ? allocations.LastValue : -1;
        if (Time.unscaledTime >= nextReport)
        {
            nextReport = Time.unscaledTime + reportIntervalSeconds;
            Debug.Log($"[MultiplayerMetrics] role={(PhotonNetwork.IsMasterClient ? "host" : "guest")} windowSeconds={Time.unscaledTime - lastReport:F1} frameMs={averageFrameMs:F1} worstMs={worstFrameMs:F1} gcBytes={allocatedBytesPerFrame} outBps={outgoingBytesPerSecond} inBps={incomingBytesPerSecond} queue={queuedCommands} pingMs={pingMs} bubbles={actionBubbles} rejected={RejectedActions} customerMessages={customerMessages} customerPayloadBytes={customerBytes}");
            customerMessages = customerBytes = 0; lastReport = Time.unscaledTime;
        }
    }
    private void OnDestroy() { allocations.Dispose(); if (active == this) active = null; }
}
