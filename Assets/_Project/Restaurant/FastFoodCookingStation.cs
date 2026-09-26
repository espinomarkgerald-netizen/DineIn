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
    [Header("Grill prep slots")]
    public FastFoodCookingDropTarget[] prepSlots=System.Array.Empty<FastFoodCookingDropTarget>();
    public FastFoodCookingDropTarget[] PrepTargets => prepSlots.Length>0?prepSlots:preparation!=null?new[]{preparation}:System.Array.Empty<FastFoodCookingDropTarget>();
    public Transform[] trayAnchors;
    [Header("Assembler placement")]
    [Tooltip("Tray floor, with local X/Z defining the interaction plane.")]
    public Transform assemblySurface;
    public Vector2 assemblyArea = new Vector2(2.1f,1.6f);
    [Min(0)] public float assemblySpacing=.08f, assemblySurfaceOffset=.008f;
    [System.Serializable] public struct FoodSurfaceOffset { public Recipe recipe; public float offset; }
    public FoodSurfaceOffset[] foodSurfaceOffsets=System.Array.Empty<FoodSurfaceOffset>();
    public Vector3 AssemblySlot(int index,int count,out Vector2 footprint)
    {
        var surface=assemblySurface!=null?assemblySurface:prepFoodAnchor;
        int columns=Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1,count))),rows=Mathf.CeilToInt((float)Mathf.Max(1,count)/columns);
        footprint=Vector2.Max(new Vector2(assemblyArea.x/columns,assemblyArea.y/rows)-Vector2.one*assemblySpacing,Vector2.one*.05f);
        if(count==1)return surface.position;
        if(trayAnchors!=null && count<=trayAnchors.Length && index<trayAnchors.Length && trayAnchors[index]!=null)
        {
            float nearest=float.PositiveInfinity;
            for(int i=0;i<count;i++)if(i!=index && trayAnchors[i]!=null)nearest=Mathf.Min(nearest,Vector3.Distance(trayAnchors[index].position,trayAnchors[i].position));
            footprint=Vector2.Min(footprint,Vector2.one*Mathf.Max(.05f,nearest-assemblySpacing));
            return trayAnchors[index].position;
        }
        // Extra portions get distinct cells; never wrap back onto an occupied point.
        return surface.TransformPoint(new Vector3(((index%columns+.5f)/columns-.5f)*assemblyArea.x,0,((index/columns+.5f)/rows-.5f)*assemblyArea.y));
    }
    private void OnDrawGizmosSelected()
    {
        if(mode!=FastFoodStationMode.Assembler || assemblySurface==null)return;
        Gizmos.color=new Color(.2f,.75f,1,.8f);Gizmos.matrix=assemblySurface.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero,new Vector3(assemblyArea.x,.015f,assemblyArea.y));Gizmos.matrix=Matrix4x4.identity;
        if(trayAnchors!=null)foreach(var point in trayAnchors)if(point!=null)Gizmos.DrawWireSphere(point.position,.06f);
    }
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
