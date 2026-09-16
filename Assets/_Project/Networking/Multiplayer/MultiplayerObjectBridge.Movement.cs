using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed partial class MultiplayerObjectBridge
{
    private const byte PoseEvent = 160;
    private string previousObjects, readObjects;
    private Snapshot pendingObjects;
    [Serializable] private sealed class MotionFrame
    { public int sequence; public double at; public List<Pose> workers, trolleys; }
    private sealed class Track
    { public Pose current; public readonly MultiplayerPoseBuffer poses = new(); public GameObject root; }
    private readonly Dictionary<string, Track> poseTargets = new();
    private int poseSequence, receivedPoseSequence;
    private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);
    private void SendPoses(List<Pose> workers, List<Pose> trolleys)
    {
        var frame = new MotionFrame { sequence = ++poseSequence, at = PhotonNetwork.Time, workers = workers, trolleys = trolleys };
        MultiplayerWire.Raise(PoseEvent, JsonUtility.ToJson(frame),
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendUnreliable);
    }
    public void OnEvent(EventData ev)
    {
        if (ev.Code != PoseEvent || session == null || session.IsAuthority || ev.Sender != session.Run?.hostActor
            || !MultiplayerWire.TryRead(ev, out var payload) || payload is not string json || json.Length > 65536) return;
        MotionFrame frame;
        try { frame = JsonUtility.FromJson<MotionFrame>(json); } catch (ArgumentException) { return; }
        if (frame == null || frame.sequence <= receivedPoseSequence || frame.workers == null || frame.trolleys == null) return;
        receivedPoseSequence = frame.sequence;
        foreach (var pose in frame.workers) QueuePose(pose, frame.at, true);
        foreach (var pose in frame.trolleys) QueuePose(pose, frame.at, false);
    }
    private void QueuePose(Pose pose, double at, bool worker)
    {
        if (pose == null) return;
        string key = (worker ? "worker:" : "trolley:") + pose.id;
        if (!poseTargets.TryGetValue(key, out var track) || track.root == null)
        {
            GameObject root = ResolvePoseRoot(pose, worker);
            if (root == null) return;
            track = new Track { root = root };
            poseTargets[key] = track;
            ApplyPose(root, pose, worker);
        }
        if (track.poses.Add(at, pose.position, pose.rotation, receivedAt: PhotonNetwork.Time))
            track.current = pose;
    }
    private static GameObject ResolvePoseRoot(Pose pose, bool worker)
    {
        if (pose == null) return null;
        if (worker)
        { foreach (var bot in MultiplayerWorldRegistry.All<KitchenWorkerBot>()) if (bot != null && ((int)bot.EmployeeRole).ToString() == pose.id) return bot.gameObject; }
        else
        { foreach (var bot in MultiplayerWorldRegistry.All<BotTrolleyCarrier>()) if (bot != null && ((int)bot.Effect).ToString() == pose.id) return bot.gameObject; }
        return null;
    }
    private void InterpolatePoses()
    {
        if (session == null || session.IsAuthority || !session.IsConnected || session.Ended) return;
        double now = PhotonNetwork.Time;
        foreach (var track in poseTargets.Values)
        {
            if (track.root == null || track.current == null) continue;
            var pose = track.current;
            if (track.poses.ReadBuffered(now, out var position, out var rotation)) track.root.transform.SetPositionAndRotation(position, rotation);
            var animator = Animation(track.root);
            animator.Set(Speed, pose.speed); animator.Set(Moving, pose.moving); animator.Set(Carrying, pose.carrying);
        }
    }
}
