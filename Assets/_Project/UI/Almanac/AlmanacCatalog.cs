using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "DineIn/Almanac Catalog")]
public sealed class AlmanacCatalog : ScriptableObject
{
    [Tooltip("Existing project entries. New entries in Resources/Almanac are discovered automatically.")]
    public AlmanacEntryData[] entries = Array.Empty<AlmanacEntryData>();

    public List<AlmanacEntryData> LoadEntries()
    {
        return entries.Concat(Resources.LoadAll<AlmanacEntryData>("Almanac"))
            .Where(e => e != null && !string.IsNullOrWhiteSpace(e.entryId))
            .GroupBy(e => e.entryId).Select(g => g.First())
            .OrderBy(e => e.category).ThenBy(e => e.entryName).ToList();
    }
}
