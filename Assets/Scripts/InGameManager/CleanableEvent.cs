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

    public void Clean()
    {
        if (IsCleaned) return;
        IsCleaned = true;

        if (cleanVfxPrefab != null)
            Instantiate(cleanVfxPrefab, transform.position, Quaternion.identity);

        if (cleanSfx != null)
            AudioSource.PlayClipAtPoint(cleanSfx, transform.position);

        OnCleaned?.Invoke(this);
        Destroy(gameObject);
    }
}