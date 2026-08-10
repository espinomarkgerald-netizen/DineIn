using UnityEngine;
using TMPro;
using UnityEngine.UI; // Required to interface with the Toggle component

public class PerformanceMonitor : MonoBehaviour
{
    [Header("UI Reference")]
    public TMP_Text fpsDisplay;

    [Header("Settings")]
    [SerializeField] private float updateInterval = 0.5f;

    private float timeLeft;
    private int frames = 0;
    private float currentFps;
    private float averageFps;
    private int frameCount = 0;
    private bool isVisible = false; // Default to off

    void Start()
    {
        timeLeft = updateInterval;
        // Ensure it starts hidden
        if (fpsDisplay != null) fpsDisplay.text = "";
    }

    void Update()
    {
        if (!isVisible) return;

        timeLeft -= Time.unscaledDeltaTime;
        frames++;

        if (timeLeft <= 0.0)
        {
            currentFps = frames / updateInterval;
            frameCount++;
            averageFps += (currentFps - averageFps) / frameCount;

            UpdateUI();

            timeLeft = updateInterval;
            frames = 0;
        }
    }

    // This method is called by your UI Toggle
    public void ToggleVisibility(bool isOn)
    {
        isVisible = isOn;
        if (!isVisible && fpsDisplay != null) fpsDisplay.text = "";
    }

    void UpdateUI()
    {
        if (fpsDisplay != null)
        {
            fpsDisplay.text = string.Format("Current: {0:0} FPS\nAverage: {1:0} FPS", currentFps, averageFps);
            if (currentFps >= 55) fpsDisplay.color = Color.green;
            else if (currentFps >= 30) fpsDisplay.color = Color.yellow;
            else fpsDisplay.color = Color.red;
        }
    }
}