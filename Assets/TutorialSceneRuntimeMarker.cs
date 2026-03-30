using UnityEngine;

[DisallowMultipleComponent]
public class TutorialSceneRuntimeMarker : MonoBehaviour
{
    public static TutorialSceneRuntimeMarker Instance { get; private set; }

    public static bool IsTutorialRuntimeActive =>
        Instance != null && Instance.isActiveAndEnabled && Instance.gameObject.activeInHierarchy;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}