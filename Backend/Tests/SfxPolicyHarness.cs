// Actual presentation-only math/pitch source. No Unity runtime, gameplay, or audio device.
using System;
public static class SfxPolicyHarness
{
    private static int checks;
    private static void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
    private static bool Near(float a, float b) => Math.Abs(a - b) < .00001f;
    private static float Distance(float radius) => radius <= 1 ? 1 - .45f * radius :
        radius <= 2 ? .55f - .45f * (radius - 1) : Math.Max(0, .1f - .1f * (radius - 2));
    private static float Zoom(float value) => 1 - .4f * value;
    public static void Main()
    {
        Check(Near(GameplaySfxView.Gain(.5f, .5f, 1, 0, Distance, Zoom), 1), "center");
        Check(Near(GameplaySfxView.Gain(1, .5f, 1, 0, Distance, Zoom), .55f), "edge");
        Check(Near(GameplaySfxView.Gain(1.5f, .5f, 1, 0, Distance, Zoom), .1f), "off screen");
        Check(Near(GameplaySfxView.Gain(2, .5f, 1, 0, Distance, Zoom), 0), "far silent");
        Check(Near(GameplaySfxView.Gain(.5f, .5f, 1, 1, Distance, Zoom), .6f), "zoom out");
        Check(Near(GameplaySfxView.Gain(.5f, .5f, -1, 0, Distance, Zoom), 0), "behind camera");
        Check(Near(GameplaySfxView.Gain(float.NaN, .5f, 1, 0, Distance, Zoom), 0), "invalid position");
        Check(Near(GameplaySfxView.Gain(.5f, .5f, float.PositiveInfinity, 0, Distance, Zoom), 0), "infinite position");
        Check(Near(GameplaySfxView.Pan(.5f, .35f), 0), "UI/center pan");
        Check(Near(GameplaySfxView.Pan(0, .35f), -.35f), "left");
        Check(Near(GameplaySfxView.Pan(1, .35f), .35f), "right");
        Check(Near(GameplaySfxView.Pan(9, .35f), .35f), "pan clamp");
        var pitches = new GameplaySfxPitch(new Random(731));
        float last = 0;
        for (int i = 0; i < 10000; i++)
        {
            float pitch = pitches.Next(1, .985f, 1.015f);
            Check(pitch >= .985f && pitch <= 1.015f, "short pitch bounds");
            Check(Math.Abs(pitch - last) >= .00239f, "short repeat differs subtly");
            last = pitch;
        }
        last = 0;
        for (int i = 0; i < 100; i++)
        {
            float pitch = pitches.Next(2, .995f, 1.005f);
            Check(pitch >= .995f && pitch <= 1.005f && Math.Abs(pitch - last) >= .00079f, "ambience loop pitch");
            last = pitch;
        }
        Check(pitches.Next(3, 1, 1) == 1, "explicit fixed pitch configuration");
        float swapped = pitches.Next(4, 1.015f, .985f);
        Check(swapped >= .985f && swapped <= 1.015f, "reversed editable range");
        Console.WriteLine("PASS: " + checks + " source-linked pitch/view math assertions; no listening or Unity runtime verification.");
    }
}
