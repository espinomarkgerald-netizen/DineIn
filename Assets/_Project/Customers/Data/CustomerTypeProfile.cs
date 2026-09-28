using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CustomerTypeProfile",
                 menuName = "DineIn/Customer Type Profile")]
public class CustomerTypeProfile : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Regular";
    public Sprite customerImage;

    [Header("Normal campaign introductions")]
    public CustomerGroup.CustomerType customerType;
    public CustomerAgent customerPrefab;
    [Tooltip("Additional normal Fast Food types only (earliest Day 2). Legacy types retain the day manager's restaurant-specific schedule.")]
    [Min(1)] public int unlockDay = 1;
    [Tooltip("Additional Fast Food types only. Green/Pink/Blue keep their existing GroupSpawner weights.")]
    [Min(0)] public float spawnWeight = .2f;
    [TextArea(3, 6)] public string introduction;

    [Header("Fast Food visit (normal Lobby2 only)")]
    [Min(.1f)] public float orderingDurationMultiplier = 1f;
    [Range(.3f, 1.5f)] public float walkingSpeedMultiplier = 1f;
    public bool overrideTakeawayChance;
    [Range(0, 1)] public float takeawayChance = .7f;
    public bool assistedPriorityService;
    public bool family;
    [Range(.4f, .9f)] public float childVisualScale = .65f;
    [Min(2)] public float childPlayInterval = 10f;
    [Min(.5f)] public float childPlayRadius = 1.5f;
    [Min(1)] public float childPlayTravelSeconds = 5f;

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
