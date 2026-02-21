using UnityEngine;

public class CleanableEvent : MonoBehaviour
{
    [Header("Cleaning")]
    [Tooltip("Hold duration required to clean this spill.")]
    public float holdToCleanSeconds = 1f;

    [Tooltip("Optional: play when cleaned.")]
    public AudioClip cleanSfx;

    [Tooltip("Optional: VFX prefab to spawn when cleaned.")]
    public GameObject cleanVfxPrefab;

    public bool IsCleaned { get; private set; }

    public void Clean()
    {
        if (IsCleaned) return;
        IsCleaned = true;

        if (cleanVfxPrefab != null)
            Instantiate(cleanVfxPrefab, transform.position, Quaternion.identity);

        if (cleanSfx != null)
            AudioSource.PlayClipAtPoint(cleanSfx, transform.position);

        Destroy(gameObject);
    }
}
