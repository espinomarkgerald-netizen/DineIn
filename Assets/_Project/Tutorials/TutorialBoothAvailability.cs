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
    private bool seatingRefreshPending;
    private readonly HashSet<GameObject> practiceBooths = new();
    public void OpenPracticeBooths(int additionalBooths = 3)
    {
        foreach (var state in authoredStates)
        {
            if (practiceBooths.Count >= additionalBooths) break;
            if (state.booth != null && state.booth != tutorialBooth && state.booth.scene == gameObject.scene)
                practiceBooths.Add(state.booth);
        }
        ApplyTutorialAvailability();
    }

    private void Awake()
    {
        EquipmentLink[] links = FindObjectsByType<EquipmentLink>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (EquipmentLink link in links)
        {
            if (link == null || link.gameObject.scene != gameObject.scene || string.IsNullOrEmpty(link.itemID) ||
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
            bool shouldBeAvailable = state.booth == tutorialBooth || practiceBooths.Contains(state.booth);
            if (state.booth.activeSelf != shouldBeAvailable)
            {
                state.booth.SetActive(shouldBeAvailable);
                seatingRefreshPending = true;
            }
        }

        // Seating caches active booths. Refresh after changing visibility so newly
        // opened practice booths participate in the normal availability query.
        BoothAssignArrowManager seating = BoothAssignArrowManager.Instance;
        if (seatingRefreshPending && seating != null && seating.gameObject.scene == gameObject.scene)
        {
            seating.RefreshBooths();
            seatingRefreshPending = false;
        }
    }

    private void OnDestroy()
    {
        foreach (var state in authoredStates)
            if (state.booth != null)
                state.booth.SetActive(state.active);
    }
}
