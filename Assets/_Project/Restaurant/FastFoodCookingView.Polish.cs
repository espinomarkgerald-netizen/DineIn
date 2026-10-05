using System.Collections;
using UnityEngine;

public sealed partial class FastFoodCookingView
{
    [Header("Perspective kitchen polish — saved scene references")]
    [SerializeField] private FastFoodCookingToast floatingCue;
    [SerializeField] private CanvasGroup stationFade;
    [SerializeField] private Material ghostMaterial;
    [SerializeField,Range(.1f,1)] private float ghostOpacity=.65f;
    [SerializeField,Min(.05f)] private float stationFadeSeconds=.16f;
    [SerializeField,Min(.1f)] private float prepLookSeconds=1.2f;
    // Reuse the persistent iris and its authored duration; never animate camera poses.
    private IrisScaleToggle kitchenIris;
    private bool kitchenTravelBusy;
    private bool ownsKitchenIris;
    private Coroutine stationTransition;
    private float fryerPrepUntil;
    private string lastInstruction="";
    private MaterialPropertyBlock previewProperties;
    [SerializeField] private RectTransform discardBox;
    private Vector2 pointerPosition;
    private float dragDepth;
    private bool discardHovered, stagingDrag;
    [SerializeField] private Outline[] equipmentOutlines=System.Array.Empty<Outline>();
    private readonly System.Collections.Generic.List<(Outline outline,bool enabled)> outlineStates=new();
    public bool ShowKitchenNotice(string message,int kind=2)
    {
        if(!opened || floatingCue==null)return false;
        floatingCue.Show(message,null,kind);
        return true;
    }
    void EnableEquipmentOutlines()
    {
        foreach(var outline in equipmentOutlines)
            if(outline!=null){outlineStates.Add((outline,outline.enabled));outline.enabled=true;}
    }
    void RestoreEquipmentOutlines()
    {
        foreach(var state in outlineStates)if(state.outline!=null)state.outline.enabled=state.enabled;
        outlineStates.Clear();
    }
    void LateUpdate() { if(drag!=null)MoveDrag(pointerPosition); }

    public void PreviousStation()=>SwitchStation(-1);
    public void NextStation()=>SwitchStation(1);
    void Warn(string text,Sprite icon=null) { if(floatingCue!=null)floatingCue.Show(text,icon,2); }
    void SwitchStation(int direction)
    {
        if(!opened||activeStation==null||stationTransition!=null||kitchenTravelBusy)return;
        int next=((int)State.Mode-1+direction+3)%3+1;
        if(FastFoodCookingController.IsMultiplayer || stationFade==null){Enter((FastFoodStationMode)next);return;}
        stationTransition=StartCoroutine(TransitionStation((FastFoodStationMode)next));
    }
    IEnumerator TravelToStation(FastFoodCookingStation target, int approach)
    {
        var localPlayer = MultiplayerSessionManager.Instance.LocalManager;
        kitchenMovement = localPlayer != null ? localPlayer.GetComponent<PlayerMovement>() : null;
        if (kitchenMovement == null) { Exit(); yield break; }
        CancelDrag();
        // Keep the current restaurant/close-up camera unchanged throughout navigation.
        bool arrived = false, cancelled = false;
        bool walking = kitchenMovement.UI_MoveToAction(target.playerWorkPoint, target.playerArrivalRadius,
            () => { if (opened && approach == stationApproach) arrived = true; },
            () => { if (opened && approach == stationApproach) cancelled = true; });
        while (walking && !arrived && !cancelled)
        {
            if (!opened || approach != stationApproach) yield break;
            if (MultiplayerSessionManager.Instance?.CanAct != true) { Exit(); yield break; }
            yield return null;
        }
        if (!opened || approach != stationApproach) yield break;
        if (!walking || cancelled)
        {
            WarningSlideUI.Instance?.Show("That station cannot be reached. Choose another station or try again.");
            yield return ReturnFromCloseUp(false); yield break;
        }
        if (MultiplayerSessionManager.Instance?.CanAct != true || kitchenMovement == null) { Exit(); yield break; }
        var distance = kitchenMovement.transform.position - target.playerWorkPoint.position;
        distance.y = 0;
        if (distance.sqrMagnitude > target.playerWorkRadius * target.playerWorkRadius)
        {
            WarningSlideUI.Instance?.Show("The station workpoint cannot be reached from here.");
            yield return ReturnFromCloseUp(false); yield break;
        }
        kitchenMovement.transform.rotation = Quaternion.Euler(0, target.playerWorkPoint.eulerAngles.y, 0);
        yield return SwitchKitchenView(target);
        if (!opened || approach != stationApproach) yield break;
        kitchenTravelBusy = false; stationTransition = null;
        station.gameObject.SetActive(true);
        activeStation.labels.gameObject.SetActive(true);
        lastSignature = null; Refresh();
    }
    IEnumerator ReturnFromCloseUp(bool showPicker)
    {
        CancelDrag();
        station.gameObject.SetActive(false);
        if (activeStation != null) activeStation.labels.gameObject.SetActive(false);
        yield return SwitchKitchenView(null);
        if (!opened) yield break;
        stationTransition = null; kitchenTravelBusy = false;
        if (showPicker) OpenStationPicker(); else Exit();
    }
    IEnumerator SwitchKitchenView(FastFoodCookingStation target)
    {
        if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) { Exit(); yield break; }
        kitchenIris = IrisScaleToggle.Instance;
        if (kitchenIris == null)
        {
            kitchenIris = IrisScaleToggle.Ensure();
            kitchenIris.RevealImmediately();
        }
        // Another screen owns a closed/running iris; do not cancel its transition.
        if (!kitchenIris.IsOpen) { Exit(); yield break; }
        ownsKitchenIris = true;
        bool complete = false;
        kitchenIris.PlayCloseThenInvoke(() => complete = true);
        while (!complete)
        {
            if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) { Exit(); yield break; }
            yield return null;
        }
        // All camera and local body/name changes happen together while fully covered.
        if (target != null)
        {
            ShowStation(target.mode);
            station.gameObject.SetActive(false);
            activeStation.labels.gameObject.SetActive(false);
        }
        else
        {
            DestroyWorld();
            if (previousCamera != null) previousCamera.enabled = previousCameraEnabled;
        }
        complete = false;
        kitchenIris.PlayOpen(() => complete = true);
        while (!complete)
        {
            if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) { Exit(); yield break; }
            yield return null;
        }
        ownsKitchenIris = false;
        kitchenIris = null;
    }
    IEnumerator TransitionStation(FastFoodStationMode next)
    {
        CancelDrag();
        stationFade.gameObject.SetActive(true);stationFade.blocksRaycasts=true;
        yield return FadeStation(1);
        Enter(next);
        yield return FadeStation(0);
        stationFade.blocksRaycasts=false;stationFade.gameObject.SetActive(false);
        stationTransition=null;
    }
    IEnumerator FadeStation(float target)
    {
        float start=stationFade.alpha;
        for(float t=0;t<1;t+=Time.unscaledDeltaTime/Mathf.Max(.05f,stationFadeSeconds))
        {
            stationFade.alpha=Mathf.Lerp(start,target,Mathf.SmoothStep(0,1,t));
            if(LevelOneUIAccessibility.ReducedMotion)break;
            yield return null;
        }
        stationFade.alpha=target;
    }
    void StopStationTransition()
    {
        if(stationTransition!=null)StopCoroutine(stationTransition);
        stationTransition=null; kitchenTravelBusy=false;
        if (ownsKitchenIris && kitchenIris != null)
        {
            if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) kitchenIris.SuspendForLoading();
            else kitchenIris.RevealImmediately();
        }
        ownsKitchenIris = false; kitchenIris = null;
        if(stationFade!=null){stationFade.alpha=0;stationFade.blocksRaycasts=false;stationFade.gameObject.SetActive(false);}
        if(floatingCue!=null)floatingCue.Clear();
    }
    void ResetPolish()
    {
        batchProgressContext=null;
        fryerPrepUntil=0;lastInstruction="";
        if(floatingCue!=null)floatingCue.Clear();
    }
}
