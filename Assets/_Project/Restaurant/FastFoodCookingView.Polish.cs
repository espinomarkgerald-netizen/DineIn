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
    private Coroutine stationTransition;
    private float fryerPrepUntil;
    private string lastInstruction="",lastStaffCue="";
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
        if(!opened||activeStation==null||stationTransition!=null)return;
        int next=((int)State.Mode-1+direction+3)%3+1;
        if(stationFade==null){Enter((FastFoodStationMode)next);return;}
        stationTransition=StartCoroutine(TransitionStation((FastFoodStationMode)next));
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
        stationTransition=null;
        if(stationFade!=null){stationFade.alpha=0;stationFade.blocksRaycasts=false;stationFade.gameObject.SetActive(false);}
        if(floatingCue!=null)floatingCue.Clear();
    }
    void ResetPolish()
    {
        batchProgressContext=null;
        fryerPrepUntil=0;lastInstruction="";lastStaffCue="";
        if(floatingCue!=null)floatingCue.Clear();
    }
}
