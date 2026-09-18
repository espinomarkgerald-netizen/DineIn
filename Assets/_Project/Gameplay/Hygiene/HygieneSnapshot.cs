using System;
using System.IO;
using UnityEngine;

// Compact floor records keep the complete rejoin checkpoint small. They are
// applied atomically with the surface records, never replayed as gameplay events.
public static class HygieneSnapshot
{
    [Serializable] private sealed class Packet { public int version = 5; public HygieneState state; public string footprints; }
    public static string Encode(HygieneState state)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write((ushort)state.floorMarks.Count);
            foreach (var mark in state.floorMarks)
            {
                writer.Write(mark.position.x); writer.Write(mark.position.y); writer.Write(mark.position.z);
                writer.Write((ushort)Mathf.RoundToInt(Mathf.Repeat(mark.yaw, 360f) / 360f * 65535f));
                writer.Write((byte)Mathf.RoundToInt(Mathf.Clamp01(mark.dirt) * 255f));
                writer.Write(mark.spill);
                writer.Write(mark.color); writer.Write(mark.id);
            }
        }
        return JsonUtility.ToJson(new Packet { state = state, footprints = Convert.ToBase64String(stream.ToArray()) });
    }
    public static HygieneState Decode(string json)
    {
        if (string.IsNullOrEmpty(json) || json.Length > 65536) return null;
        try
        {
            var packet = JsonUtility.FromJson<Packet>(json);
            var state = packet?.state;
            if (packet == null || packet.version != 5 || state == null || state.surfaces == null || state.surfaces.Count > HygieneState.MaxSurfaces
                || !Finite(state.lobbyGraceRemainingHours) || state.lobbyGraceRemainingHours < 0f || state.lobbyGraceRemainingHours > 24f
                || !Finite(state.lobbyClockHour) || state.lobbyClockHour < -1f
                || state.floorRoute == null || state.floorRoute.Count > HygieneState.MaxFloorMarks
                || state.floorRouteTotal < 0 || state.floorRouteTotal > HygieneState.MaxFloorMarks
                || state.floorRouteDone < 0 || state.floorRouteDone > state.floorRouteTotal || !Unit(state.floorWorkProgress)
                || !Finite(state.floorWorkSeconds) || state.floorWorkSeconds < 0f || state.floorWorkSeconds > 3600f
                || state.floorRouteSkipped < 0 || state.floorRouteSkipped > state.floorRouteDone
                || double.IsNaN(state.feedbackExpires) || double.IsInfinity(state.feedbackExpires)) return null;
            var ids = new System.Collections.Generic.HashSet<int>();
            var routeIds = new System.Collections.Generic.HashSet<int>();
            foreach (int id in state.floorRoute) if (id <= 0 || !routeIds.Add(id)) return null;
            if (state.floorRouteDone + state.floorRoute.Count > state.floorRouteTotal
                || state.floorRouteActive && !state.lobbyCleaningRequested) return null;
            foreach (var surface in state.surfaces)
                if (surface == null || surface.id == 0 || !ids.Add(surface.id) || !Unit(surface.dirt)
                    || !Finite(surface.shineRemaining) || surface.shineRemaining < 0f || surface.shineRemaining > 3600f
                    || !Enum.IsDefined(typeof(HygieneArea), surface.area)) return null;
            byte[] bytes = Convert.FromBase64String(packet.footprints ?? "");
            using var reader = new BinaryReader(new MemoryStream(bytes));
            int count = reader.ReadUInt16();
            if (count > HygieneState.MaxFloorMarks || bytes.Length != 2 + count * 21) return null;
            var markIds = new System.Collections.Generic.HashSet<int>();
            state.floorMarks = new System.Collections.Generic.List<HygieneFloorMark>(count);
            for (int i = 0; i < count; i++)
            {
                Vector3 position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z)) return null;
                var mark = new HygieneFloorMark { position = position,
                    yaw = reader.ReadUInt16() / 65535f * 360f, dirt = reader.ReadByte() / 255f, spill = reader.ReadBoolean(),
                    color = reader.ReadByte(), id = reader.ReadInt32() };
                if (mark.color > 3 || mark.id <= 0 || !markIds.Add(mark.id)) return null;
                state.floorMarks.Add(mark);
            }
            if (!Unit(state.kitchenDirt) || !Unit(state.lobbyDirt) || !Finite(state.cleaningRemaining)
                || state.cleaningRemaining < 0f || state.cleaningRemaining > 3600f || !Finite(state.cleaningDuration)
                || state.cleaningDuration < 0f || state.cleaningDuration > 3600f || !Finite(state.clockScale)
                || state.clockScale < 0f || state.clockScale > 10f || double.IsNaN(state.sentAt) || double.IsInfinity(state.sentAt)
                || !Enum.IsDefined(typeof(KitchenCleaningMode), state.cleaningMode)
                || !Enum.IsDefined(typeof(HygieneArea), state.decisionArea)) return null;
            return state;
        }
        catch (Exception e) when (e is ArgumentException || e is FormatException || e is IOException) { return null; }
    }
    private static bool Unit(float value) => value >= 0f && value <= 1f;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
