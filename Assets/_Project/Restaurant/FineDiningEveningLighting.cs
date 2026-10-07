using UnityEngine;

/// <summary>Authored lighting checkpoints, evaluated using the existing service clock.</summary>
public sealed class FineDiningEveningLighting : MonoBehaviour
{
    public GameDayManager clock;
    public Light sun;
    public Light[] interiorLights;
    public Light[] exteriorLights;
    [Range(17,22)] public float exteriorOnHour = 18f;
    public Color sunset = new Color(1f, .81f, .62f);
    public Color dusk = new Color(.64f, .69f, .9f);
    public Color night = new Color(.42f, .51f, .75f);
    public AnimationCurve sunIntensity = new AnimationCurve(new Keyframe(17, 1f), new Keyframe(18.5f, .65f), new Keyframe(20, .35f));
    public AnimationCurve ambientIntensity = new AnimationCurve(new Keyframe(17, 1f), new Keyframe(18.5f, .85f), new Keyframe(20, .7f));
    private Color originalSunColor;
    private float originalSunIntensity, originalAmbient;
    private float nextUpdate;
    private void OnEnable()
    {
        if (sun != null) { originalSunColor = sun.color; originalSunIntensity = sun.intensity; }
        originalAmbient = RenderSettings.ambientIntensity;
        Apply();
    }
    private void Update()
    {
        if (Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + .25f;
        Apply();
    }
    private void Apply()
    {
        float hour = clock != null ? clock.CurrentGameHour : 17f;
        if (sun != null)
        {
            sun.color = hour < 18.5f ? Color.Lerp(sunset, dusk, Mathf.InverseLerp(17, 18.5f, hour)) : Color.Lerp(dusk, night, Mathf.InverseLerp(18.5f, 20, hour));
            sun.intensity = sunIntensity.Evaluate(hour);
        }
        RenderSettings.ambientIntensity = ambientIntensity.Evaluate(hour);
        if(exteriorLights != null) foreach(var light in exteriorLights)if(light!=null)light.enabled=hour>=exteriorOnHour;
        if (interiorLights != null) foreach (var light in interiorLights) if (light != null) light.enabled = true;
    }
    private void OnDisable()
    {
        if (sun != null) { sun.color = originalSunColor; sun.intensity = originalSunIntensity; }
        RenderSettings.ambientIntensity = originalAmbient;
    }
}
