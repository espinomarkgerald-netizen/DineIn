using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class FastFoodCookingView
{
    [Header("Tap collection")]
    [SerializeField, Min(.05f)] private float collectTravelSeconds = .5f;
    [SerializeField, Min(0)] private float collectArcHeight = .35f;
    [SerializeField, Min(0)] private float hotbarPickupArc = 120f;
    [SerializeField] private string storedProteinFormat = "READY x{0}";
    private readonly Dictionary<FastFoodCookingState.Portion, GameObject> collecting = new();
    private readonly Dictionary<Recipe, FastFoodCookingDragHandle> proteinSlots = new();
    private bool CanTapCollect => opened && activeStation != null && drag == null && !cameraMoving &&
        stationTransition == null && Time.timeScale > 0 && !HygieneManager.KitchenPaused;

    void UpdateBaskets()
    {
        foreach (var rig in stations)
            foreach (var basket in rig.baskets)
                if (basket != null) { basket.view = this; basket.Present(State.BasketRaised(basket.firstSlot)); }
    }
    public void CollectBasket(FastFoodFryerBasket basket)
    {
        if (!CanTapCollect || State.Mode != FastFoodStationMode.Fry || !activeStation.baskets.Contains(basket)) return;
        if (!State.BasketRaised(basket.firstSlot)) { Warn("Wait for the basket to lift"); return; }
        var food = State.Portions.Where(p => p.player && p.stage == FastFoodCookingStage.Ready &&
            FastFoodCookingState.Station(p.recipe) == FastFoodStationMode.Fry && p.slot >= basket.firstSlot && p.slot < basket.firstSlot + 2).ToArray();
        if (food.Length == 0) return;
        if (State.FryRackCount + food.Length > State.fryRackCapacity) { Warn("Drying rack full — assemble the ready food first"); return; }
        var flights = food.Select(CaptureCollection).ToArray();
        int count = State.CollectBasket(basket.firstSlot);
        for (int i = 0; i < food.Length; i++)
        {
            if (food[i].stage != FastFoodCookingStage.Ready && flights[i] != null) BeginCollectionFlight(food[i], flights[i]);
            else if (flights[i] != null) Destroy(flights[i]);
        }
        if (count > 0)
        {
            basket.ClearHighlight();
            fryerPrepUntil = Time.unscaledTime + prepLookSeconds + collectTravelSeconds;
            PlayKitchenCue(placementSound); Message(count + (count == 1 ? " item on the drying rack" : " items on the drying rack"), false);
            lastSignature = null; Refresh();
        }
    }
    public void CollectFromSurface(int slot, bool wholeSurface = false)
    {
        if (!CanTapCollect) return;
        if (State.Mode == FastFoodStationMode.Fry)
        { var basket = activeStation.baskets.FirstOrDefault(b => b != null && b.firstSlot == slot / 2 * 2); if (basket != null) CollectBasket(basket); return; }
        if (State.Mode != FastFoodStationMode.Grill) return;
        var ready = State.Portions.Where(p => p.player && p.stage == FastFoodCookingStage.Ready &&
            FastFoodCookingState.Station(p.recipe) == State.Mode && p.slot / 4 == slot / 4).ToArray();
        var selected = wholeSurface ? null : ready.FirstOrDefault(p => p.slot == slot);
        if (selected != null) CollectFood(selected);
        else if (ready.Length == 1) CollectFood(ready[0]);
        else if (wholeSurface && ready.Length > 1) Warn("Tap each ready patty to collect it");
    }
    public void CollectFood(FastFoodCookingState.Portion portion)
    {
        if (!CanTapCollect || portion == null || !portion.player || portion.stage != FastFoodCookingStage.Ready) return;
        if (State.Mode == FastFoodStationMode.Fry) { CollectFromSurface(portion.slot); return; }
        if (State.Mode != FastFoodStationMode.Grill) return;
        var source = cookingVisuals.FirstOrDefault(v => v.portion == portion)?.root;
        var point = source != null ? stationCamera.WorldToScreenPoint(source.position) : Vector3.zero;
        if (!State.Collect(portion)) return;
        lastSignature = null; Refresh();
        PlayKitchenCue(placementSound);
        if (point.z > 0 && !LevelOneUIAccessibility.ReducedMotion)
            StartCoroutine(FlyProteinToHotbar(portion, point));
        else PulseProteinSlot(portion.recipe);
    }

    void BindStoredProteinSlots()
    {
        proteinSlots.Clear();
        foreach (var group in State.PlayerWork.Where(p => p.player && FastFoodCookingState.HasStoredProtein(p)).GroupBy(p => p.recipe))
        {
            var recipe = group.Key;
            var protein = FastFoodCookingState.Steps(recipe).FirstOrDefault()?.item;
            if (protein == null) continue;
            var step = FastFoodCookingState.AssemblySteps(recipe).FirstOrDefault(s => s.item == protein);
            // Reuse the draggable ingredient slot once the batch is in prep.
            var slot = State.PrepReady ? hotbar.GetComponentsInChildren<FastFoodCookingDragHandle>()
                .FirstOrDefault(s => s.item == protein && s.portion?.recipe == recipe) : null;
            if (slot == null)
            {
                slot = Instantiate(ingredientTemplate, hotbar);
                slot.gameObject.SetActive(true);
                slot.view = this; slot.portion = group.First();
                slot.item = null; slot.enabled = false;
            }
            slot.storedProtein = true;
            slot.icon.sprite = step?.icon != null ? step.icon : protein.sprite;
            slot.count.text = string.Format(storedProteinFormat, State.StoredProteinCount(recipe));
            slot.count.color = new Color(.08f, .34f, .24f);
            slot.count.fontSize = Mathf.Min(slot.count.fontSize, 23);
            slot.gameObject.name = "Collected " + recipe.DisplayName + " protein";
            proteinSlots[recipe] = slot;
        }
    }

    void PulseProteinSlot(Recipe recipe)
    {
        if (proteinSlots.TryGetValue(recipe, out var slot) && slot != null)
            PulseUI(slot.icon.rectTransform);
    }

    IEnumerator FlyProteinToHotbar(FastFoodCookingState.Portion portion, Vector2 screenStart)
    {
        if (!proteinSlots.TryGetValue(portion.recipe, out var slot) || slot == null) yield break;
        var root = (RectTransform)canvas.transform;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenStart, uiCamera, out var start);
        var flight = new GameObject("Collected protein pickup", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var rect = (RectTransform)flight.transform;
        rect.SetParent(root, false);
        rect.anchorMin = rect.anchorMax = root.pivot;
        var icon = flight.GetComponent<UnityEngine.UI.Image>();
        icon.sprite = slot.icon.sprite; icon.preserveAspect = true; icon.raycastTarget = false;
        rect.sizeDelta = new Vector2(84, 84);
        collecting[portion] = flight;
        float duration = Mathf.Max(.05f, collectTravelSeconds);
        for (float elapsed = 0; elapsed < duration && flight != null && opened; elapsed += Time.unscaledDeltaTime)
        {
            // Other pickups rebuild controls; always target the current slot.
            if (!proteinSlots.TryGetValue(portion.recipe, out slot) || slot == null) break;
            var screenEnd = RectTransformUtility.WorldToScreenPoint(uiCamera, slot.icon.rectTransform.TransformPoint(slot.icon.rectTransform.rect.center));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenEnd, uiCamera, out var end);
            float t = Mathf.Clamp01(elapsed / duration), eased = 1 - Mathf.Pow(1 - t, 3);
            rect.anchoredPosition = Vector2.LerpUnclamped(start, end, eased) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * hotbarPickupArc);
            rect.localScale = Vector3.one * (Mathf.Lerp(1.15f, .68f, t) + .12f * Mathf.Sin(t * Mathf.PI));
            rect.localRotation = Quaternion.Euler(0, 0, 12 * Mathf.Sin(t * Mathf.PI));
            icon.color = new Color(1, 1, 1, 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.85f, 1, t)));
            yield return null;
        }
        if (flight != null) Destroy(flight);
        collecting.Remove(portion);
        if (opened && State.Mode == FastFoodStationMode.Grill) PulseProteinSlot(portion.recipe);
    }
    GameObject CaptureCollection(FastFoodCookingState.Portion portion)
    {
        var source = cookingVisuals.FirstOrDefault(v => v.portion == portion)?.root;
        if (source == null || LevelOneUIAccessibility.ReducedMotion) return null;
        var flight = CreateFoodGhost(source.gameObject, null, out var center);
        flight.name = "Collected food transfer";
        flight.transform.SetPositionAndRotation(source.parent.TransformPoint(center), source.parent.rotation);
        flight.transform.localScale = source.parent.lossyScale;
        return flight;
    }
    Transform HoldingAnchor(FastFoodCookingState.Portion portion)
    {
        var rack = State.Mode == FastFoodStationMode.Fry ? State.Portions.Where(FastFoodCookingState.OnFryRack).ToList() :
            State.PlayerWork.Where(p => p.proteinReady || p.stage == FastFoodCookingStage.Complete).ToList();
        int index = Mathf.Clamp(rack.IndexOf(portion), 0, activeStation.completedFoodAnchors.Length - 1);
        return activeStation.completedFoodAnchors.Length > 0 ? activeStation.completedFoodAnchors[index] : activeStation.prepFoodAnchor;
    }
    void BeginCollectionFlight(FastFoodCookingState.Portion portion, GameObject flight)
    { collecting[portion] = flight; StartCoroutine(FlyCollectedFood(portion, flight, HoldingAnchor(portion))); }
    IEnumerator FlyCollectedFood(FastFoodCookingState.Portion portion, GameObject flight, Transform destination)
    {
        Vector3 start = flight.transform.position;
        Vector3 end = destination.position + Vector3.up * .09f;
        Quaternion rotation = flight.transform.rotation;
        for (float t = 0; t < 1 && flight != null; t += Time.deltaTime / Mathf.Max(.05f, collectTravelSeconds))
        {
            float eased = Mathf.SmoothStep(0, 1, t);
            flight.transform.position = Vector3.Lerp(start, end, eased) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * collectArcHeight);
            flight.transform.rotation = Quaternion.Slerp(rotation, destination.rotation, eased);
            yield return null;
        }
        if (flight != null) Destroy(flight);
        collecting.Remove(portion); lastSignature = null;
    }
    void ClearCollections()
    {
        foreach (var flight in collecting.Values) if (flight != null) Destroy(flight);
        collecting.Clear();
        proteinSlots.Clear();
    }
    GameObject SpawnRackFood(GameObject template, Transform anchor, Recipe recipe)
    {
        var food = SpawnVisual(template, anchor);
        ArrangeRackFood(food, anchor, recipe);
        return food;
    }
    internal static void ArrangeRackFood(GameObject food, Transform anchor, Recipe recipe)
    {
        var renderers = food.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) return;
        Bounds Bounds() { var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds); return b; }
        var bounds = Bounds();
        food.transform.localScale *= Mathf.Min(1, .70f / Mathf.Max(bounds.size.x, bounds.size.z));
        bounds = Bounds();
        food.transform.position += anchor.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    }
}
