using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class FastFoodCookingView
{
    [Header("Cozy action feedback")]
    [SerializeField] private AudioSource feedbackAudio;
    [SerializeField] private AudioClip placementSound, readySound, finishedSound;
    [SerializeField, Range(0, 1)] private float feedbackVolume = .32f;
    [SerializeField, Min(0)] private float placementSeconds = .18f;
    [SerializeField, Min(0)] private float placementLift = .065f;
    private FastFoodCookingState.Portion acceptedPortion;
    private int acceptedStep;
    private readonly Dictionary<FastFoodCookingState.Portion, FastFoodCookingStage> observedStages = new();
    private MaterialPropertyBlock foodProperties;

    private sealed class CookingVisual
    {
        public readonly Transform root;
        public readonly FastFoodCookingState.Portion portion;
        public readonly Vector3 position, scale;
        public readonly List<(Renderer renderer, int index, Color color)> surfaces = new();
        public int colorStep = -1;
        public CookingVisual(GameObject visual, FastFoodCookingState.Portion food)
        {
            root = visual.transform; portion = food;
            position = root.localPosition; scale = root.localScale;
            foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    var color = material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") :
                        material != null && material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                    surfaces.Add((renderer, i, color));
                }
            }
        }
    }

    private void UpdateFoodColor(CookingVisual visual)
    {
        var portion = visual.portion;
        int step = portion.stage == FastFoodCookingStage.Burnt ? 13 : portion.stage == FastFoodCookingStage.Cooking ?
            Mathf.RoundToInt(Mathf.Clamp01(portion.elapsed / Mathf.Max(.01f, State.cookSeconds)) * 12) : 12;
        if (step == visual.colorStep) return;
        visual.colorStep = step;
        if (foodProperties == null) foodProperties = new MaterialPropertyBlock();
        var multiplier = step == 13 ? activeStation.burntColor : Color.Lerp(portion.recipe.kitchenRawColorMultiplier, Color.white, step / 12f);
        foreach (var surface in visual.surfaces)
        {
            var color = surface.color * multiplier; color.a = surface.color.a;
            foodProperties.Clear();
            foodProperties.SetColor("_BaseColor", color); foodProperties.SetColor("_Color", color);
            surface.renderer.SetPropertyBlock(foodProperties, surface.index);
        }
    }

    private void PlayKitchenCue(AudioClip clip)
    {
        if (clip != null && feedbackAudio != null && opened) feedbackAudio.PlayOneShot(clip, feedbackVolume);
    }

    private void UpdateKitchenAudio()
    {
        if (activeStation == null) return;
        var source = activeStation.cookingAudio;
        bool cooking = false;
        foreach (var portion in State.Portions)
            if (portion.stage == FastFoodCookingStage.Cooking && FastFoodCookingState.Station(portion.recipe) == State.Mode)
            { cooking = true; break; }
        bool audible = cooking && Time.timeScale > 0 && !HygieneManager.KitchenPaused;
        if (source != null)
        {
            source.volume = activeStation.cookingVolume;
            if (audible && !source.isPlaying && source.clip != null) source.Play();
            else if (!audible && source.isPlaying) source.Stop();
        }
        bool ready = false;
        foreach (var portion in State.PlayerWork)
        {
            if (observedStages.TryGetValue(portion, out var previous) && previous == FastFoodCookingStage.Cooking &&
                (portion.stage == FastFoodCookingStage.Ready || portion.stage == FastFoodCookingStage.Preparing)) ready = true;
            observedStages[portion] = portion.stage;
        }
        if (State.PlayerWork.Count == 0) observedStages.Clear();
        if (ready) PlayKitchenCue(readySound);
    }

    private void AnimateAccepted(GameObject visual, FastFoodCookingState.Portion portion)
    {
        if (portion != acceptedPortion || LevelOneUIAccessibility.ReducedMotion || placementSeconds <= 0) return;
        // Cooking bobbing owns the root. The model settles independently within it.
        var target = visual.transform.Find("Model") ?? visual.transform;
        StartCoroutine(SettleFood(target));
    }

    private IEnumerator SettleFood(Transform target)
    {
        var position = target.localPosition; var scale = target.localScale;
        float elapsed = 0;
        while (target != null && elapsed < placementSeconds)
        {
            elapsed += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.01f, placementSeconds));
            float eased = Mathf.SmoothStep(0, 1, t);
            target.localPosition = position + Vector3.up * (placementLift * (1 - eased));
            target.localScale = scale * (Mathf.Lerp(.94f, 1, eased) + .035f * Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        if (target != null) { target.localPosition = position; target.localScale = scale; }
    }
}
