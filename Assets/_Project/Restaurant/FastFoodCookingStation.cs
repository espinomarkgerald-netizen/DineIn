using UnityEngine;
using TMPro;

/// <summary>Saved station geometry and presentation. Runtime never repositions the authored camera or targets.</summary>
public sealed class FastFoodCookingStation : MonoBehaviour
{
    public FastFoodStationMode mode;
    public Camera stationCamera;
    public FastFoodCookingDropTarget cooking, preparation, discard;
    public RectTransform labels;
    public TMP_Text status, workload;
    public Transform foodAnchor, prepFoodAnchor;
    public Transform[] trayAnchors;
    [Min(.01f)] public float visualScale = 1;
    public GameObject cookingFoodTemplate, dragPreviewTemplate, servingTemplate, drinkTemplate;
    public Color cookingColor = new Color(.75f,.22f,.2f), readyColor = new Color(.7f,.43f,.15f), burntColor = Color.black;
    public Color validDropColor = new Color(.3f,1,.5f), invalidDropColor = new Color(1,.25f,.2f);
    [Min(.1f)] public float rayDistance = 20, previewDistance = 2;
    [Min(0)] public float previewLift = .14f;
    public string stationTitle = "KITCHEN HELP";
}
