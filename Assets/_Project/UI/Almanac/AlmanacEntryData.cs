using System;
using UnityEngine;

public enum AlmanacCategory { Customers, Staff, Restaurants, Kitchen, Equipment, Food, Service, CleaningStorage, Management }
public enum AlmanacPreviewKind { Image, Character, Model }
public enum AlmanacFraming { Automatic, Character, Equipment, SmallObject }

[CreateAssetMenu(fileName = "AlmanacEntry", menuName = "DineIn/Almanac Entry")]
public class AlmanacEntryData : ScriptableObject
{
    [Header("Identity")]
    public string entryName;
    public string subTitle;
    [Tooltip("Permanent unique key used by related links and browsing history.")]
    public string entryId;
    public AlmanacCategory category;
    [Tooltip("Optional heading inside the category index, e.g. Dishes or Fast Food.")]
    public string listSection;
    public int sectionOrder;
    [Tooltip("Additional plain-language search terms, separate from the displayed article.")]
    public string searchAliases;
    [TextArea(3, 8)] public string description;
    [TextArea(3, 10)] public string gameplayNotes;
    [TextArea(1, 3)] public string bossNote;
    public string[] relatedIds = Array.Empty<string>();

    [Header("Visuals")]
    public Sprite icon;
    public AlmanacPreviewKind previewKind;
    public AlmanacFraming previewFraming;
    [Tooltip("Use a wide article photograph for restaurants and UI presentations.")]
    public bool widePreview;
    public GameObject previewPrefab;
    public Vector3 previewEuler = new Vector3(0, 155, 0);
    [Tooltip("0 uses automatic framing. Override only for an unusually shaped preview; normal entries need no tuning.")]
    [Range(0, .95f)] public float previewFrameFill;
    [Tooltip("Optional exact transform paths relative to the preview prefab root. Excludes helper/effect renderers from the preview, never the gameplay prefab.")]
    public string[] previewIgnoredRendererPaths = Array.Empty<string>();
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
