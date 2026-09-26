using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class FastFoodCookingView
{
    [Header("Assembler drag feel")]
    [SerializeField, Min(0)] private float assemblyDragSmoothing=.025f;
    [SerializeField, Min(1)] private float assemblySnapRadius=100;
    [SerializeField, Range(0,1)] private float assemblyMagnetStrength=.8f;
    [SerializeField, Min(0)] private float assemblySnapDuration=.16f, assemblyReturnDuration=.2f;
    [SerializeField, Min(1)] private float touchSnapMultiplier=1.3f;
    [SerializeField] private Vector2 assemblyTouchOffset=new Vector2(0,60);
    private Plane assemblyDragPlane;
    private Vector3 assemblyGrabOffset, assemblySourcePosition, assemblyReleasePosition;
    private bool assemblyTouch, assemblyHasRelease;
    private float assemblyPreviewHalfHeight;
    private readonly List<GameObject> returningAssemblyFood=new();
    private bool IsAssembler => activeStation!=null && State.Mode==FastFoodStationMode.Assembler;
    private Transform AssemblySurface => activeStation.assemblySurface!=null?activeStation.assemblySurface:activeStation.prepFoodAnchor;

    private static Bounds FoodBounds(GameObject food)
    {
        var renderers=food.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
        var bounds=new Bounds(food.transform.position,Vector3.zero);
        if(renderers.Length>0){bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);}
        return bounds;
    }
    private Vector3 AssemblyContact(FastFoodCookingState.Portion portion,out Vector2 footprint)
    {
        var ticket=State.PlayerTicket;
        int index=ticket!=null?Mathf.Max(0,ticket.portions.IndexOf(portion)):0;
        return activeStation.AssemblySlot(index,ticket?.portions.Count??1,out footprint);
    }
    private float AssemblyOffset(FastFoodCookingState.Portion portion)
    {
        float offset=activeStation.assemblySurfaceOffset;
        foreach(var entry in activeStation.foodSurfaceOffsets)if(entry.recipe==portion.recipe){offset+=entry.offset;break;}
        return offset;
    }
    private Vector3 SeatAssemblyFood(GameObject food,FastFoodCookingState.Portion portion)
    {
        var contact=AssemblyContact(portion,out var footprint);
        food.transform.rotation=AssemblySurface.rotation;
        var bounds=FoodBounds(food);
        float fit=Mathf.Min(1,Mathf.Min(footprint.x/Mathf.Max(.001f,bounds.size.x),footprint.y/Mathf.Max(.001f,bounds.size.z)));
        food.transform.localScale*=fit;
        bounds=FoodBounds(food);
        // Centre the actual mesh footprint and rest its bottom on the tray, independently of its pivot.
        food.transform.position+=contact+Vector3.up*AssemblyOffset(portion)-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        return food.transform.position;
    }
    private Vector3 AssemblyLandingPosition()
    {
        var contact=AssemblyContact(drag.portion,out _);
        return contact+Vector3.up*(assemblyPreviewHalfHeight+AssemblyOffset(drag.portion));
    }
    private void BeginAssemblyMotion(Vector2 pointer,bool touch)
    {
        assemblyTouch=touch;
        SeatAssemblyFood(preview,drag.portion);
        assemblyPreviewHalfHeight=FoodBounds(preview).extents.y;
        var landing=preview.transform.position;
        assemblyDragPlane=new Plane(AssemblySurface.up,landing);
        var ray=stationCamera.ScreenPointToRay(pointer);
        var point=assemblyDragPlane.Raycast(ray,out var distance)?ray.GetPoint(distance):landing;
        bool world=!(drag.transform is RectTransform);
        var source=world?FoodBounds(drag.gameObject).center:point;
        assemblyGrabOffset=world?Vector3.ProjectOnPlane(source-point,AssemblySurface.up):Vector3.zero;
        assemblySourcePosition=point+assemblyGrabOffset;
        preview.transform.position=assemblySourcePosition;
        previewPositionInitialized=true;
    }
    private Vector3 AssemblyPointerPosition(Vector2 pointer)
    {
        if(assemblyTouch)pointer+=assemblyTouchOffset*Mathf.Max(.01f,canvas.scaleFactor);
        var ray=stationCamera.ScreenPointToRay(pointer);
        if(!assemblyDragPlane.Raycast(ray,out var distance))return preview.transform.position;
        var local=AssemblySurface.InverseTransformPoint(ray.GetPoint(distance)+assemblyGrabOffset);
        local.x=Mathf.Clamp(local.x,-activeStation.assemblyArea.x*2,activeStation.assemblyArea.x*2);
        local.z=Mathf.Clamp(local.z,-activeStation.assemblyArea.y*2,activeStation.assemblyArea.y*2);
        return AssemblySurface.TransformPoint(local);
    }
    private void MoveAssemblyDrag(Vector2 pointer)
    {
        var raw=AssemblyPointerPosition(pointer);
        var projected=stationCamera.WorldToScreenPoint(raw);
        hovered=ResolveDropTarget(projected,stationCamera.ScreenPointToRay(pointer));
        if(IsDropBlocked(pointer))hovered=null;
        var destination=raw;
        if(CanDrop(hovered))
        {
            var landing=AssemblyLandingPosition();
            float distance=Vector2.Distance(projected,stationCamera.WorldToScreenPoint(landing));
            float strength=assemblyMagnetStrength*Mathf.SmoothStep(0,1,1-distance/AssemblyCaptureRadius());
            destination=Vector3.Lerp(raw,landing,strength);
        }
        float alpha=assemblyDragSmoothing<=0?1:1-Mathf.Exp(-Time.unscaledDeltaTime/assemblyDragSmoothing);
        preview.transform.position=Vector3.Lerp(preview.transform.position,destination,alpha);
        var tint=CanDrop(hovered)?activeStation.validDropColor:Color.white;tint.a=ghostOpacity;Tint(preview,tint);
        UpdateDropHints();
    }
    private float AssemblyCaptureRadius()=>assemblySnapRadius*Mathf.Max(.01f,canvas.scaleFactor)*(assemblyTouch?touchSnapMultiplier:1);
    private void AnimateAssemblyPlacement(GameObject food,FastFoodCookingState.Portion portion)
    {
        var end=SeatAssemblyFood(food,portion);
        if(portion!=acceptedPortion || !assemblyHasRelease || LevelOneUIAccessibility.ReducedMotion)return;
        var start=assemblyReleasePosition+(food.transform.position-FoodBounds(food).center);
        StartCoroutine(MoveAssemblyFood(food,start,end,assemblySnapDuration,false));
    }
    private IEnumerator MoveAssemblyFood(GameObject food,Vector3 start,Vector3 end,float seconds,bool remove)
    {
        if(remove)returningAssemblyFood.Add(food);
        for(float elapsed=0;food!=null && elapsed<seconds;elapsed+=Time.unscaledDeltaTime)
        {
            if(!opened || !IsAssembler || LevelOneUIAccessibility.ReducedMotion)break;
            food.transform.position=Vector3.Lerp(start,end,Mathf.SmoothStep(0,1,elapsed/Mathf.Max(.001f,seconds)));
            yield return null;
        }
        if(food!=null){food.transform.position=end;if(remove)Destroy(food);}
        if(remove)returningAssemblyFood.Remove(food);
    }
    private void ClearAssemblyMotion()
    {
        foreach(var food in returningAssemblyFood)if(food!=null)Destroy(food);
        returningAssemblyFood.Clear();assemblyHasRelease=false;
    }
}
