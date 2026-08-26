using System;
using UnityEngine;

public enum ManagerComplaintType
{
    WrongOrder = 1,
    BurntFood = 2
}

public enum ManagerComplaintResponseQuality
{
    Professional,
    Acceptable,
    Poor
}

[Serializable]
public sealed class ManagerComplaintResponseDefinition
{
    public ManagerComplaintResponseQuality quality;
    public string buttonHeading;
    [TextArea(2, 3)] public string managerLine;
    [TextArea(2, 4)] public string coachingFeedback;
    [Min(0f)] public float orderCostMultiplier;
    public Color feedbackColor = Color.white;
}

[Serializable]
public sealed class ManagerComplaintDefinition
{
    public ManagerComplaintType type;
    public string headline;
    public Sprite fallbackPortrait;
    [TextArea(2, 4)] public string[] customerLines;
    public ManagerComplaintResponseDefinition professional;
    public ManagerComplaintResponseDefinition acceptable;
    public ManagerComplaintResponseDefinition poor;

    public string PickCustomerLine()
    {
        if (customerLines == null || customerLines.Length == 0)
            return headline;

        int start = UnityEngine.Random.Range(0, customerLines.Length);
        for (int i = 0; i < customerLines.Length; i++)
        {
            string candidate = customerLines[(start + i) % customerLines.Length];
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        return headline;
    }
}

[CreateAssetMenu(
    fileName = "ManagerComplaintSettings",
    menuName = "DineIn/Manager/Complaint Settings")]
public sealed class ManagerComplaintSettings : ScriptableObject
{
    public const string ResourcePath = "ManagerComplaints/ManagerComplaintSettings";

    [Header("Occurrence")]
    [Range(0, 2)] public int maximumEncountersPerWeek = 2;
    [Range(0f, 1f)] public float eligibleIncidentChance = 0.45f;
    [Min(0)] public int minimumCompletedDaysBetweenEncounters = 1;
    [Min(5f)] public float unansweredTimeoutSeconds = 30f;

    [Header("Presentation")]
    [Min(0.1f)] public float responseFeedbackSeconds = 2.2f;
    [Min(0.1f)] public float markerPulseSpeed = 3.6f;
    [Range(1f, 1.5f)] public float markerPulseScale = 1.12f;
    [Min(0f)] public float screenEdgePadding = 58f;
    public Vector3 markerWorldOffset = new Vector3(0f, 0.65f, 0f);

    [Header("Camera Focus")]
    [Min(1f)] public float focusedOrthographicSize = 7f;
    public Vector3 cameraFramingOffset = new Vector3(0f, 0f, -0.8f);

    [Header("Complaint Content")]
    public ManagerComplaintDefinition wrongOrder;
    public ManagerComplaintDefinition burntFood;

    public ManagerComplaintDefinition GetDefinition(ManagerComplaintType type)
    {
        return type == ManagerComplaintType.BurntFood ? burntFood : wrongOrder;
    }
}
