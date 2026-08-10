using UnityEngine;

[CreateAssetMenu(fileName = "AlmanacEntry", menuName = "DineIn/Almanac Entry")]
public class AlmanacEntryData : ScriptableObject
{
    [Header("Identity")]
    public string entryName;
    public string subTitle;

    [TextArea(3, 8)]
    public string description;

    [Header("Visuals")]
    public Sprite icon;
}