using UnityEngine;

/// <summary>Small presentation layer for the authored district; selection still belongs to RestaurantSelector.</summary>
public sealed class RestaurantDistrictPresentation : MonoBehaviour
{
    [SerializeField] private RestaurantSelector selector;
    [SerializeField] private Transform[] stopRims;
    [SerializeField] private ParticleSystem[] ambientEffects;
    [SerializeField] private Color selectedColor = new Color(1, .66f, .12f);
    [SerializeField] private Color inactiveColor = new Color(.72f, .53f, .30f);
    [SerializeField, Range(0, .08f)] private float pulseAmount = .025f;
    [SerializeField, Min(.1f)] private float pulseSpeed = 2f;
    private Vector3[] restScales;
    private Renderer[] rims;
    private MaterialPropertyBlock colors;
    private int selected;

    private void Awake()
    {
        restScales = new Vector3[stopRims.Length];
        rims = new Renderer[stopRims.Length];
        colors = new MaterialPropertyBlock();
        for (int i = 0; i < stopRims.Length; i++)
            if (stopRims[i] != null)
            {
                restScales[i] = stopRims[i].localScale;
                rims[i] = stopRims[i].GetComponent<Renderer>();
            }
    }
    private void OnEnable()
    {
        if (selector != null) selector.OnRestaurantSelected += Select;
        LevelOneUIAccessibility.SettingsChanged += ApplyMotionPreference;
        ApplyMotionPreference();
    }
    private void Start() => Select(selector != null ? selector.SelectedRestaurantIndex : 0);
    private void Select(int index)
    {
        selected = index;
        for (int i = 0; i < rims.Length; i++)
        {
            if (rims[i] == null) continue;
            stopRims[i].localScale = restScales[i];
            rims[i].GetPropertyBlock(colors);
            colors.SetColor("_BaseColor", i == selected ? selectedColor : inactiveColor);
            rims[i].SetPropertyBlock(colors);
        }
    }
    private void Update()
    {
        if (selected < 0 || selected >= stopRims.Length || stopRims[selected] == null) return;
        float pulse = LevelOneUIAccessibility.ReducedMotion ? 1 : 1 + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
        var scale = restScales[selected]; scale.x *= pulse; scale.z *= pulse;
        stopRims[selected].localScale = scale;
    }
    private void ApplyMotionPreference()
    {
        foreach (var effect in ambientEffects)
        {
            if (effect == null) continue;
            if (LevelOneUIAccessibility.ReducedMotion) effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            else effect.Play();
        }
    }
    private void OnDisable()
    {
        if (selector != null) selector.OnRestaurantSelected -= Select;
        LevelOneUIAccessibility.SettingsChanged -= ApplyMotionPreference;
        if (restScales == null) return;
        for (int i = 0; i < stopRims.Length; i++)
            if (stopRims[i] != null) stopRims[i].localScale = restScales[i];
    }
}
