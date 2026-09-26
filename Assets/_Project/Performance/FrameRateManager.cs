using UnityEngine;

public class FrameRateManager : MonoBehaviour
{
    [Header("Frame Settings")]
    public float TargetFrameRate = 60.0f;

    void Start()
    {
        // The legacy busy-wait capped the game independently of player settings.
        // Let Unity pace frames; never sleep/spin on its main thread.
        var settings = DineIn.NewMenu.SettingsManager.EnsureInstance();
        if (settings == null)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Mathf.Max(1, Mathf.RoundToInt(TargetFrameRate));
        }
    }
}
