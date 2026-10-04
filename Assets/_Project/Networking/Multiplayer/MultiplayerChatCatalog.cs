using System;
using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "Dine In/Multiplayer/Chat Catalog")]
public sealed class MultiplayerChatCatalog : ScriptableObject
{
    [Serializable] public sealed class Entry
    {
        public string id, label;
        [Tooltip("-1 for a phrase, otherwise an index in the bundled sprite asset.")]
        public int spriteIndex = -1;
        [Tooltip("Emotes only: humanoid clip and state on the Multiplayer Emotes animator layer.")]
        public AnimationClip clip;
        public string animatorState;
        [Min(0.2f)] public float duration = 3f;
    }
    [Range(1, 160)] public int characterLimit = 160;
    [Min(1f)] public float sendCooldown = 1f;
    [Range(1, 50)] public int historyLimit = 50;
    public TMP_SpriteAsset emojiSprites;
    public Entry[] phrases, emojis, emotes;
    public Entry Find(string id, bool emoji) => Array.Find(emoji ? emojis : phrases, e => e.id == id);
    public Entry Find(string id, int kind) => kind == 3
        ? Array.Find(emotes ?? Array.Empty<Entry>(), e => e.id == id) : Find(id, kind == 2);
}
