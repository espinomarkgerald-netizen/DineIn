using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CustomerTypeProfile",
                 menuName = "DineIn/Customer Type Profile")]
public class CustomerTypeProfile : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Regular";
    public Sprite customerImage;

    [Header("Small Talk / Intro Lines")]
    [TextArea(2, 4)]
    public string[] openingMessages = new string[3];

    [Header("Patience Multipliers")]
    [Tooltip("1 = normal drain. 2 = drains twice as fast.")]
    public float orderPatienceMultiplier = 1f;
    public float linePatienceMultiplier = 1f;

    [Header("Eating")]
    [Tooltip("1 = normal duration. 1.8 = messy/slow eater.")]
    public float eatDurationMultiplier = 1f;

    [Header("Tip")]
    [Tooltip("Flat tip added to payment on happy result. 0 = no tip.")]
    public int tipAmount = 0;

    [Header("Busser")]
    public bool isMessy = false;

    public string GetRandomOpeningMessage()
    {
        if (openingMessages == null || openingMessages.Length == 0)
            return string.Empty;

        List<string> valid = new List<string>();

        for (int i = 0; i < openingMessages.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(openingMessages[i]))
                valid.Add(openingMessages[i]);
        }

        if (valid.Count == 0)
            return string.Empty;

        return valid[Random.Range(0, valid.Count)];
    }
}