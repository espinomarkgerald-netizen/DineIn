using System.Collections.Generic;
using UnityEngine;

// Bounded render history. Reject old packets and interpolate at a shared network
// time, instead of repeatedly chasing the newest position each rendered frame.
public sealed class MultiplayerPoseBuffer
{
    private struct Sample { public double at; public Vector3 position; public Quaternion rotation; }
    private readonly List<Sample> samples = new(8);
    private double transitSeconds, lastReadAt = double.NaN, lastRenderAt = double.NaN;
    public int Count => samples.Count;
    public bool Add(double at, Vector3 position, Quaternion rotation, bool teleport = false, double receivedAt = double.NaN)
    {
        if (double.IsNaN(at) || double.IsInfinity(at) || !Finite(position.x) || !Finite(position.y) || !Finite(position.z)
            || !Finite(rotation.x) || !Finite(rotation.y) || !Finite(rotation.z) || !Finite(rotation.w)
            || (samples.Count > 0 && at <= samples[samples.Count - 1].at)) return false;
        // A long outage has no continuous path left to interpolate. Recover at
        // the first new sample, just like an explicit teleport.
        if (teleport || (samples.Count > 0 && at - samples[samples.Count - 1].at > 1d))
        { samples.Clear(); lastReadAt = lastRenderAt = double.NaN; transitSeconds = 0d; }
        if (!double.IsNaN(receivedAt) && !double.IsInfinity(receivedAt))
        {
            double age = System.Math.Clamp(receivedAt - at, 0d, 0.5d);
            // Absorb additional transit/jitter immediately; shed surplus slowly.
            // The 100 ms interpolation history must remain after packet travel.
            transitSeconds = System.Math.Max(age, transitSeconds * 0.9d + age * 0.1d);
        }
        if (samples.Count == 8) samples.RemoveAt(0);
        samples.Add(new Sample { at = at, position = position, rotation = rotation });
        return true;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public bool ReadBuffered(double now, out Vector3 position, out Quaternion rotation)
    {
        if (samples.Count == 0 || double.IsNaN(now) || double.IsInfinity(now))
        { position = default; rotation = Quaternion.identity; return false; }
        double renderAt = now - transitSeconds - 0.1d;
        if (!double.IsNaN(lastRenderAt))
        {
            // A late packet may increase the delay. Hold rather than run backwards;
            // recovering from jitter catches up gently instead of skipping ahead.
            double elapsed = System.Math.Max(0d, now - lastReadAt);
            renderAt = System.Math.Max(lastRenderAt, System.Math.Min(renderAt, lastRenderAt + elapsed * 1.1d));
        }
        // Never retain a clock beyond the available history during packet loss.
        // That would make later arrivals snap straight to their newest endpoint.
        renderAt = System.Math.Clamp(renderAt, samples[0].at, samples[samples.Count - 1].at);
        lastReadAt = now;
        lastRenderAt = renderAt;
        return Read(renderAt, out position, out rotation);
    }
    public bool Read(double renderAt, out Vector3 position, out Quaternion rotation)
    {
        position = default; rotation = Quaternion.identity;
        if (samples.Count == 0) return false;
        while (samples.Count > 2 && samples[1].at <= renderAt) samples.RemoveAt(0);
        var a = samples[0]; var b = samples.Count > 1 ? samples[1] : a;
        float t = b.at > a.at ? Mathf.Clamp01((float)((renderAt - a.at) / (b.at - a.at))) : 1f;
        position = Vector3.Lerp(a.position, b.position, t);
        rotation = Quaternion.Slerp(a.rotation, b.rotation, t);
        return true;
    }
}
