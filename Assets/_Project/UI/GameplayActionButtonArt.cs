using System;
using TMPro;
using UnityEngine;

/// <summary>Shared, editable artwork and sizing for restaurant action controls.</summary>
[CreateAssetMenu(menuName = "Dine In/Gameplay Action Button Art")]
public sealed class GameplayActionButtonArt : ScriptableObject
{
    [Serializable] public struct Entry { public string name; public Sprite sprite; }
    public Entry[] sprites;
    public TMP_FontAsset headingFont;
    public Material headingMaterial;
    public TMP_FontAsset bodyFont;
    public Material bodyMaterial;
    [Min(20f)] public float labelSize = 28f;
    [Range(.5f, 1.5f)] public float worldVisualScale = 1.1f;
    [Range(1f, 2f)] public float fineDiningVisualScale = 1.4f;
    [Min(16f)] public float detailSize = 22f;
    [Min(200f)] public float worldButtonWidth = 280f;
    [Min(300f)] public float maximumWorldWidth = 440f;
    [Min(.01f)] public float changeFadeSeconds = .15f;
    [Min(12f)] public float patienceBarHeight = 20f;
    [Min(4f)] public float patienceBarGap = 12f;
    [Header("Customer comments")]
    [Min(240f)] public float commentWidth = 388f;
    [Min(20f)] public float commentFontSize = 26f;
    [Min(100f)] public float commentMinimumHeight = 136f;
    [Min(240f)] public float fineDiningCommentWidth = 300f;

    private static GameplayActionButtonArt cached;
    public static GameplayActionButtonArt Shared => cached != null ? cached :
        cached = Resources.Load<GameplayActionButtonArt>("UI/GameplayActionButtons");

    public Sprite Find(string spriteName)
    {
        if (sprites != null)
            foreach (var entry in sprites)
                if (entry.name == spriteName) return entry.sprite;
        return null;
    }
}
