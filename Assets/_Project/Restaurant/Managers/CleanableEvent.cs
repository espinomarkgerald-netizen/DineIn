using System;
using UnityEngine;

public class CleanableEvent : MonoBehaviour
{
    [Header("Clean Settings")]
    public float holdToCleanSeconds = 2f;

    [Header("Feedback")]
    [Tooltip("Optional: VFX prefab to spawn when cleaned.")]
    public GameObject cleanVfxPrefab;

    [SerializeField] private AudioClip cleanSfx;

    public bool IsCleaned { get; private set; }

    public event Action<CleanableEvent> OnCleaned;

    private bool reportsHygiene;
    private void Start()
    {
        // Dirty trays already report through their pickup/cleanup lifecycle.
        reportsHygiene = GetComponentInParent<FoodTray>() == null;
        if (reportsHygiene) HygieneManager.Instance?.RecordLobbyIncident(true, transform.position);
    }

    public void Clean()
    {
        if (IsCleaned) return;
        IsCleaned = true;
        if (reportsHygiene) HygieneManager.Instance?.RecordLobbyIncident(false, transform.position);

        if (cleanVfxPrefab != null)
            Instantiate(cleanVfxPrefab, transform.position, Quaternion.identity);

        if (cleanSfx != null)
            AudioSource.PlayClipAtPoint(cleanSfx, transform.position);

        OnCleaned?.Invoke(this);
        Destroy(gameObject);
    }
}
