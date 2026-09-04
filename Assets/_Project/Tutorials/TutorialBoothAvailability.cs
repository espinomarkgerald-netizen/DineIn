using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes the single booth used by the Basic Controls interaction lesson available
/// in Lobby1Tutorial without changing EquipmentManager purchases or saved progress.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class TutorialBoothAvailability : MonoBehaviour
{
    private const string TutorialBoothId = "booth01";
    private readonly List<(GameObject booth, bool active)> authoredStates = new();
    private GameObject tutorialBooth;

    private void Awake()
    {
        EquipmentLink[] links = FindObjectsByType<EquipmentLink>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (EquipmentLink link in links)
        {
            if (link == null || string.IsNullOrEmpty(link.itemID) ||
                !link.itemID.StartsWith("booth", StringComparison.OrdinalIgnoreCase))
                continue;

            authoredStates.Add((link.gameObject, link.gameObject.activeSelf));
            if (string.Equals(link.itemID, TutorialBoothId, StringComparison.OrdinalIgnoreCase))
                tutorialBooth = link.gameObject;
        }

        ApplyTutorialAvailability();
    }

    private void LateUpdate() => ApplyTutorialAvailability();

    private void ApplyTutorialAvailability()
    {
        foreach (var state in authoredStates)
        {
            if (state.booth == null) continue;
            bool shouldBeAvailable = state.booth == tutorialBooth;
            if (state.booth.activeSelf != shouldBeAvailable)
                state.booth.SetActive(shouldBeAvailable);
        }
    }

    private void OnDestroy()
    {
        foreach (var state in authoredStates)
            if (state.booth != null)
                state.booth.SetActive(state.active);
    }
}
