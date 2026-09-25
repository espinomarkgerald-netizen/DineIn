using UnityEngine;
using TMPro;

/// <summary>Saved station geometry and presentation. The camera blends between editable view anchors; targets stay authored.</summary>
public sealed class FastFoodCookingStation : MonoBehaviour
{
    public FastFoodStationMode mode;
    public Camera stationCamera;
    [Tooltip("Existing appliance/counter used when authoring the station framing.")]
    public Renderer workSurface;
    [Tooltip("Kitchen pointer targets, independent of staff and appliance colliders.")]
    public LayerMask interactionLayers = ~0;
    public FastFoodCookingDropTarget cooking, preparation, discard;
    [Header("Batch cooking and prep camera")]
    public FastFoodCookingDropTarget[] cookingSlots = System.Array.Empty<FastFoodCookingDropTarget>();
    public Transform cookingViewAnchor, preparationViewAnchor;
    [Min(.05f)] public float cameraShiftSeconds = .65f;
    [Min(.5f)] public float preparationViewSize = 2;
    [Range(20,90)] public float preparationFieldOfView = 45;
    public Transform[] completedFoodAnchors = System.Array.Empty<Transform>();
    [Header("Fryer baskets and holding rack")]
    public FastFoodFryerBasket[] baskets = System.Array.Empty<FastFoodFryerBasket>();
    public FastFoodCookingDropTarget[] collectionSurfaces = System.Array.Empty<FastFoodCookingDropTarget>();
    public FastFoodCookingDropTarget[] Slots => cookingSlots.Length > 0 ? cookingSlots : cooking != null ? new[] { cooking } : System.Array.Empty<FastFoodCookingDropTarget>();
    public RectTransform labels;
    public TMP_Text status, workload;
    public Transform foodAnchor, prepFoodAnchor;
    public Transform[] trayAnchors;
    [Min(.01f)] public float visualScale = 1;
    public GameObject cookingFoodTemplate, dragPreviewTemplate, servingTemplate, drinkTemplate;
    [Header("Cooking feedback")]
    [Tooltip("Optional authored steam/heat object, shown only while food is cooking.")]
    public GameObject cookingFeedback;
    [Tooltip("Saved station loop. Runtime only changes playback while food is cooking.")]
    public AudioSource cookingAudio;
    [Range(0, 1)] public float cookingVolume = .12f;
    [Min(0)] public float cookingBobHeight = .003f;
    [Min(0)] public float cookingBobFrequency = 2f;
    [Range(0f, .15f)] public float readyPulseAmount = .035f;
    [Min(0)] public float readyPulseFrequency = 1f;
    public Color cookingColor = new Color(.75f,.22f,.2f), readyColor = new Color(.7f,.43f,.15f), burntColor = Color.black;
    public Color validDropColor = new Color(.3f,1,.5f), invalidDropColor = new Color(1,.25f,.2f);
    [Min(.1f)] public float rayDistance = 20, previewDistance = 2;
    [Min(0)] public float previewLift = .14f;
    public string stationTitle = "KITCHEN HELP";
}
