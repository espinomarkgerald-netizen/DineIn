using Photon.Pun;
using UnityEngine;

// Presentation metadata for an existing scaled-time operation. This never
// advances gameplay; when clock projection is uncertain the UI uses a spinner.
public sealed class MultiplayerWorkTiming
{
    private double startedAt;
    private float scaledStart, scale, duration;
    public void Begin(float seconds)
    {
        startedAt = PhotonNetwork.Time;
        scaledStart = Time.time;
        scale = Time.timeScale;
        duration = seconds;
    }
    public void Clear() => duration = 0f;
    public bool TryRead(out double start, out float seconds)
    {
        start = startedAt;
        seconds = scale > 0f ? duration / scale : 0f;
        return duration > 0f && scale > 0f && Mathf.Approximately(scale, Time.timeScale)
            && System.Math.Abs((PhotonNetwork.Time - startedAt) - (Time.time - scaledStart) / scale) < 0.15d;
    }
}
