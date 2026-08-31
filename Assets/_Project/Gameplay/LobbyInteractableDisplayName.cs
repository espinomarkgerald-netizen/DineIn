using UnityEngine;

/// <summary>
/// Optional designer-authored label for the Lobby HUD interaction indicator.
/// Add this to an interactable or one of its parents when its GameObject name
/// is not the player-facing name that should appear on screen.
/// </summary>
[DisallowMultipleComponent]
public sealed class LobbyInteractableDisplayName : MonoBehaviour
{
    [SerializeField] private string displayName = "Interactable";

    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? gameObject.name
        : displayName.Trim();
}
