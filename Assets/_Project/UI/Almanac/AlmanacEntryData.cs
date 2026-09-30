using System;
using UnityEngine;

public enum AlmanacCategory { Customers, Staff, Restaurants, Kitchen, Equipment, Food, Service, CleaningStorage, Management }
public enum AlmanacPreviewKind { Image, Character, Model }

[CreateAssetMenu(fileName = "AlmanacEntry", menuName = "DineIn/Almanac Entry")]
public class AlmanacEntryData : ScriptableObject
{
    [Header("Identity")]
    public string entryName;
    public string subTitle;
    [Tooltip("Permanent unique key used by related links and browsing history.")]
    public string entryId;
    public AlmanacCategory category;
    [TextArea(3, 8)] public string description;
    [TextArea(3, 10)] public string gameplayNotes;
    [TextArea(1, 3)] public string bossNote;
    public string[] relatedIds = Array.Empty<string>();

    [Header("Visuals")]
    public Sprite icon;
    public AlmanacPreviewKind previewKind;
    public GameObject previewPrefab;
    public Vector3 previewEuler = new Vector3(0, 155, 0);
    public AnimationClip idleClip;
    public AnimationClip introductionClip;
    public AlmanacPreviewVariant[] variants = Array.Empty<AlmanacPreviewVariant>();

    [Header("Development evidence (never displayed to players)")]
    [TextArea(2, 8)] public string sourceNotes;
}

[Serializable]
public sealed class AlmanacPreviewVariant
{
    public string label;
    public GameObject prefab;
}
