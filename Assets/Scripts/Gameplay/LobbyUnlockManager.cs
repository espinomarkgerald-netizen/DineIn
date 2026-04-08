using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Progressively activates booth GameObjects as the current day increases,
/// providing a natural scaffolding ramp across the first 10 days without a
/// separate tutorial system.
///
/// Setup:
///   - Attach to a GO in the Office scene (e.g. LobbyUnlockManager under Managers).
///   - Populate boothEntries in the Inspector: each entry holds a reference to the
///     booth root GameObject and the day on which it should first appear.
///   - All booths whose unlockDay is greater than the current day start deactivated.
/// </summary>
public class LobbyUnlockManager : MonoBehaviour
{
    [System.Serializable]
    public struct BoothEntry
    {
        [Tooltip("Root GameObject of the booth to unlock.")]
        public GameObject booth;

        [Tooltip("The game day on which this booth becomes available (inclusive).")]
        public int unlockDay;
    }

    [SerializeField] private List<BoothEntry> boothEntries = new();

    private void Start()
    {
        ApplyUnlocks();
    }

    /// <summary>
    /// Activates or deactivates each booth based on the current day reported by
    /// GameFlowManager. Safe to call multiple times (e.g. after a day advances
    /// while still in the Office scene).
    /// </summary>
    public void ApplyUnlocks()
    {
        int currentDay = GameFlowManager.Instance != null
            ? GameFlowManager.Instance.CurrentDay
            : 1;

        foreach (BoothEntry entry in boothEntries)
        {
            if (entry.booth == null)
                continue;

            bool shouldBeActive = currentDay >= entry.unlockDay;
            entry.booth.SetActive(shouldBeActive);
        }
    }
}
