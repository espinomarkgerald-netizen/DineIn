using System;

// Pure presentation math: deterministic travel with acceleration, cruise and deceleration.
public static class DailyRewardSlotMotion
{
    public static float Travel(float time)
    {
        double t = Math.Max(0, Math.Min(1, time));
        const double a = .15, d = .35, cruise = 1 - a - d;
        double distance = t < a ? t * t / (2 * a) : t <= 1 - d ? a / 2 + t - a :
            a / 2 + cruise + (t - (1 - d)) - Math.Pow(t - (1 - d), 2) / (2 * d);
        return (float)(distance / (a / 2 + cruise + d / 2));
    }
    public static float Settle(float time) => time <= 0 || time >= 1 ? 0 : (float)(Math.Sin(time * Math.PI * 2) * (1 - time) * 5);
    public static float Fit(float width, float height, float safeWidth, float safeHeight, float maxWidth, float maxHeight)
        => (float)Math.Max(.05, Math.Min(1, Math.Min(safeWidth * maxWidth / width, safeHeight * maxHeight / height)));
}
