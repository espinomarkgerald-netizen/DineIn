using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Authored basket mesh and the two cooking positions which travel with it.</summary>
public sealed class FastFoodFryerBasket : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public int firstSlot;
    public Transform movingBasket;
    public Vector3 loweredLocalPosition;
    public Outline interactionOutline;
    [Min(0)] public float liftHeight = .48f;
    [Min(.05f)] public float liftSeconds = .45f;
    [Min(0)] public float readyTiltDegrees = 3;
    [System.NonSerialized] public FastFoodCookingView view;
    private float lift, velocity;
    private Quaternion restRotation;
    private bool initialized;

    void Awake() { if (movingBasket != null) { restRotation = movingBasket.localRotation; initialized = true; } }
    public void Present(bool raised, bool immediate = false)
    {
        if (movingBasket == null) return;
        if (!initialized) { restRotation = movingBasket.localRotation; initialized = true; }
        lift = immediate ? (raised ? 1 : 0) :
            Mathf.SmoothDamp(lift, raised ? 1 : 0, ref velocity, liftSeconds * .35f, Mathf.Infinity, Time.deltaTime);
        movingBasket.localPosition = loweredLocalPosition + movingBasket.parent.InverseTransformVector(Vector3.up * liftHeight * lift);
        movingBasket.localRotation = restRotation * Quaternion.Euler(readyTiltDegrees * Mathf.Sin(lift * Mathf.PI), 0, 0);
    }
    public void OnPointerClick(PointerEventData e)
    { if (e.button == PointerEventData.InputButton.Left && !e.dragging) view?.CollectBasket(this); }
    public void OnPointerEnter(PointerEventData e)
    { if (interactionOutline != null) { interactionOutline.OutlineColor = Color.white; interactionOutline.enabled = true; } }
    public void OnPointerExit(PointerEventData e) => ClearHighlight();
    public void ClearHighlight() { if (interactionOutline != null) interactionOutline.enabled = false; }
    void OnDisable() => ClearHighlight();
}
