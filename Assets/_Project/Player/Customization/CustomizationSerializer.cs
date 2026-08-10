using UnityEngine;

public static class CustomizationSerializer
{
    public static string ColorToString(Color c)
    {
        return $"{c.r},{c.g},{c.b},{c.a}";
    }

    public static Color StringToColor(string s)
    {
        string[] parts = s.Split(',');
        return new Color(
            float.Parse(parts[0]),
            float.Parse(parts[1]),
            float.Parse(parts[2]),
            float.Parse(parts[3])
        );
    }
}
