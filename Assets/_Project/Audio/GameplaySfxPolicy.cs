using System;
using System.Collections.Generic;

// Presentation-only math/randomness. Never consumes UnityEngine.Random's gameplay state.
public sealed class GameplaySfxPitch
{
    private readonly Random random;
    private readonly Dictionary<int, float> previous = new Dictionary<int, float>();
    public GameplaySfxPitch() : this(new Random()) { }
    public GameplaySfxPitch(Random random) { this.random = random; }
    public float Next(int clip, float minimum, float maximum)
    {
        float low = Math.Min(minimum, maximum), high = Math.Max(minimum, maximum);
        if (high <= low) return low;
        float value = low + (float)random.NextDouble() * (high - low);
        if (previous.TryGetValue(clip, out float last) && Math.Abs(value - last) < (high - low) * .08f)
            value = low + ((value - low + (high - low) * .5f) % (high - low));
        previous[clip] = value;
        return value;
    }
}

public static class GameplaySfxView
{
    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public static float Radius(float x, float y) => Math.Max(Math.Abs(x - .5f), Math.Abs(y - .5f)) * 2f;
    public static float Pan(float x, float maximum) => Math.Max(-1f, Math.Min(1f, (x - .5f) * 2f)) * maximum;
    public static float Gain(float x, float y, float z, float zoom, Func<float, float> distanceCurve, Func<float, float> zoomCurve)
    {
        if (!Finite(x) || !Finite(y) || !Finite(z) || !Finite(zoom) || z <= 0) return 0;
        return Math.Max(0, Math.Min(1, distanceCurve(Radius(x, y)))) *
               Math.Max(0, Math.Min(1, zoomCurve(Math.Max(0, Math.Min(1, zoom)))));
    }
}
