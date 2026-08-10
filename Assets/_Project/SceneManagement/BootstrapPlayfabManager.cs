using UnityEngine;

public class BootstrapPlayfabManager : MonoBehaviour
{
    [SerializeField] private GameObject playfabManagerPrefab;

    void Awake()
    {
        if (FindFirstObjectByType<PlayfabManager>() == null)
        {
            if (playfabManagerPrefab != null)
            {
                Instantiate(playfabManagerPrefab);
                Debug.Log("✅ Bootstrapped PlayfabManager");
            }
            else
            {
                Debug.LogError("❌ playfabManagerPrefab not assigned in BootstrapPlayfabManager.");
            }
        }
    }
}
