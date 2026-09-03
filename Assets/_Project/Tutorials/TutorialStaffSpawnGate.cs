using System;
using UnityEngine;

/// <summary>
/// Tutorial-only gate that keeps staff absent until TutorialSystem explicitly allows them.
/// Attach ONLY in Lobby1Tutorial (normally on TutorialSystem).
///
/// IMPORTANT:
/// - Shared staff scripts/prefabs are never modified.
/// - This component only enables/disables scene-instance spawners or scene objects.
/// - Staff permission should be enabled only after the FINAL Staff Management lesson step.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class TutorialStaffSpawnGate : MonoBehaviour
{
    [Header("Tutorial")]
    [SerializeField] private TutorialSystem tutorial;

    [Header("Scene-Instance Staff Spawners")]
    [Tooltip("Assign staff-spawning Behaviour components that exist in Lobby1Tutorial. They are disabled before staff permission and restored when permission opens.")]
    [SerializeField] private Behaviour[] staffSpawnersToGate = Array.Empty<Behaviour>();

    [Header("Optional Tutorial Staff Scene Objects")]
    [Tooltip("If tutorial staff already exist as scene objects instead of being spawned, assign their ROOT objects here. They stay inactive until staff permission opens.")]
    [SerializeField] private GameObject[] tutorialStaffObjectsToActivate = Array.Empty<GameObject>();

    private bool[] originalSpawnerEnabled = Array.Empty<bool>();
    private bool captured;
    private bool subscribed;
    private bool lastAllowed;

    public bool StaffAllowed => tutorial != null && tutorial.AllowStaffSpawning;

    private void Awake()
    {
        if (tutorial == null)
            tutorial = FindFirstObjectByType<TutorialSystem>(FindObjectsInactive.Include);

        CaptureOriginalSpawnerStates();

        // HARD GATE: staff are blocked immediately, before ordinary Start methods run.
        // This prevents staff from appearing during Basic Controls, HUD, Dashboard, or
        // the beginning of the Staff lesson.
        ApplyStaffPermission(false, true);
    }

    private void OnEnable()
    {
        Subscribe();
        ApplyStaffPermission(StaffAllowed, true);
    }

    private void Start()
    {
        // TutorialSystem may initialize after this component's Awake. Re-apply the
        // authoritative permission once all Awake calls are complete.
        ApplyStaffPermission(StaffAllowed, true);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        RestoreSpawnerStates();
    }

    private void Subscribe()
    {
        if (subscribed || tutorial == null)
            return;

        tutorial.SpawnPermissionsChanged += OnSpawnPermissionsChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || tutorial == null)
            return;

        tutorial.SpawnPermissionsChanged -= OnSpawnPermissionsChanged;
        subscribed = false;
    }

    private void OnSpawnPermissionsChanged(bool customersAllowed, bool staffAllowed)
    {
        ApplyStaffPermission(staffAllowed, false);
    }

    private void CaptureOriginalSpawnerStates()
    {
        if (captured)
            return;

        captured = true;
        originalSpawnerEnabled = new bool[staffSpawnersToGate != null ? staffSpawnersToGate.Length : 0];

        for (int i = 0; i < originalSpawnerEnabled.Length; i++)
        {
            Behaviour spawner = staffSpawnersToGate[i];
            originalSpawnerEnabled[i] = spawner != null && spawner.enabled;
        }
    }

    private void ApplyStaffPermission(bool allowed, bool force)
    {
        if (!force && lastAllowed == allowed)
            return;

        lastAllowed = allowed;
        CaptureOriginalSpawnerStates();

        // Spawner components keep their authored scene-instance enabled state once
        // permission opens. Before that, they are forcibly disabled.
        if (staffSpawnersToGate != null)
        {
            for (int i = 0; i < staffSpawnersToGate.Length; i++)
            {
                Behaviour spawner = staffSpawnersToGate[i];
                if (spawner == null)
                    continue;

                bool authoredEnabled = i < originalSpawnerEnabled.Length && originalSpawnerEnabled[i];
                spawner.enabled = allowed && authoredEnabled;
            }
        }

        // For staff authored directly into Lobby1Tutorial, keep roots completely absent
        // until permission opens. These must be tutorial-scene instances; do not assign
        // shared prefab assets here.
        if (tutorialStaffObjectsToActivate != null)
        {
            foreach (GameObject staffRoot in tutorialStaffObjectsToActivate)
                if (staffRoot != null && staffRoot.activeSelf != allowed)
                    staffRoot.SetActive(allowed);
        }
    }

    private void RestoreSpawnerStates()
    {
        if (!captured || staffSpawnersToGate == null)
            return;

        for (int i = 0; i < staffSpawnersToGate.Length; i++)
        {
            Behaviour spawner = staffSpawnersToGate[i];
            if (spawner != null && i < originalSpawnerEnabled.Length)
                spawner.enabled = originalSpawnerEnabled[i];
        }
    }
}
