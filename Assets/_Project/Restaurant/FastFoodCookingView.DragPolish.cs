using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class FastFoodCookingView
{
    [Header("Magnetic drops")]
    [Tooltip("Capture radius in Canvas reference pixels; scales with the HUD.")]
    [SerializeField, Min(0)] private float magnetRadius = 72;
    [SerializeField, Min(.01f)] private float magnetEaseSeconds = .065f;
    [SerializeField] private Material dropGhostMaterial;
    [SerializeField, Min(.001f)] private float dropOutlineWidth = .012f;
    [SerializeField, Min(0)] private float dropOutlinePadding = .035f;
    [Header("Progress animation")]
    [SerializeField, Min(.01f)] private float progressSmoothSeconds = .18f;
    private readonly List<(FastFoodCookingDropTarget target, GameObject ghost)> dropGhosts = new();
    private readonly List<RectTransform> dropBlockingButtons = new();
    private Vector3 ghostCenterOffset;
    private bool previewPositionInitialized;
    private Mesh dropOutlineMesh;
    private object batchProgressContext;
    private float batchProgressTarget, batchProgressVelocity;

    void SetBatchProgress(object context, float value)
    {
        batchProgressTarget = Mathf.Clamp01(value);
        if(!ReferenceEquals(batchProgressContext,context))
        {
            batchProgressContext=context; batchProgressVelocity=0;
            progressFill.fillAmount=batchProgressTarget;
        }
    }
    void AnimateBatchProgress()
    {
        if(!opened || activeStation==null || progressFill==null)return;
        progressFill.fillAmount=LevelOneUIAccessibility.ReducedMotion?batchProgressTarget:
            Mathf.SmoothDamp(progressFill.fillAmount,batchProgressTarget,ref batchProgressVelocity,
                progressSmoothSeconds,Mathf.Infinity,Time.unscaledDeltaTime);
    }

    Transform DropAnchor(FastFoodCookingDropTarget target)
    {
        if(target==null)return null;
        if(target.kind==0)return target.foodAnchor!=null?target.foodAnchor:target.transform;
        if(target.kind!=1)return target.transform;
        if(State.Mode==FastFoodStationMode.Assembler && activeStation.trayAnchors.Length>0)
            return activeStation.trayAnchors[State.PlayerTicket.portions.Count(p=>p.placed)%activeStation.trayAnchors.Length];
        if(State.Mode==FastFoodStationMode.Fry && drag?.item==null && drag?.portion!=null && activeStation.completedFoodAnchors.Length>0)
        {
            var staged=State.Portions.Where(p=>p==drag.portion || FastFoodCookingState.Station(p.recipe)==FastFoodStationMode.Fry && !p.placed &&
                (p.stage==FastFoodCookingStage.Complete && FastFoodCookingState.PreparationStation(p.recipe)==FastFoodStationMode.Fry ||
                 p.proteinReady && p.stage==FastFoodCookingStage.Preparing))
                .OrderByDescending(p=>State.PlayerWork.Contains(p)).ToList();
            return activeStation.completedFoodAnchors[Mathf.Clamp(staged.IndexOf(drag.portion),0,activeStation.completedFoodAnchors.Length-1)];
        }
        return activeStation.prepFoodAnchor!=null?activeStation.prepFoodAnchor:target.transform;
    }
    Vector3 DropGhostPosition(FastFoodCookingDropTarget target)
    {
        var anchor=DropAnchor(target);
        return anchor!=null?anchor.TransformPoint(ghostCenterOffset):preview.transform.position;
    }
    FastFoodCookingDropTarget ResolveDropTarget(Vector2 screen, Ray ray)
    {
        if(!stationCamera.pixelRect.Contains(screen))return null;
        var uiCamera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        // UI controls consume the pointer; magnets must never drop through them.
        foreach(var ui in new[]{hotbarContainer,notification,discardBox})
            if(ui!=null&&ui.gameObject.activeInHierarchy&&RectTransformUtility.RectangleContainsScreenPoint(ui,screen,uiCamera))return null;
        foreach(var button in dropBlockingButtons)
            if(button!=null&&button.gameObject.activeInHierarchy&&RectTransformUtility.RectangleContainsScreenPoint(button,screen,uiCamera))return null;
        float radius=magnetRadius*Mathf.Max(.01f,canvas.scaleFactor);
        FastFoodCookingDropTarget best=null;
        float bestDistance=float.PositiveInfinity;
        foreach(var entry in dropGhosts)
        {
            var target=entry.target;
            if(!CanDrop(target))continue;
            var projected=stationCamera.WorldToScreenPoint(DropGhostPosition(target));
            if(projected.z<=stationCamera.nearClipPlane||!stationCamera.pixelRect.Contains(projected))continue;
            float distance=Vector2.Distance(screen,projected);
            var collider=target.GetComponent<Collider>();
            bool direct=collider!=null&&collider.enabled&&collider.Raycast(ray,out _,activeStation.rayDistance);
            if((direct||distance<=radius)&&distance<bestDistance){best=target;bestDistance=distance;}
        }
        return best;
    }

    // Copy visual meshes only: no scripts, colliders, particles or outline material instances.
    // A wrapper places the grab pivot at the actual rendered centre, including root-mesh prefabs.
    internal static GameObject CreateFoodGhost(GameObject source,Material material,out Vector3 centerOffset)
    {
        var root=new GameObject("Kitchen Food Ghost");
        root.layer=2;
        Transform Copy(Transform original,Transform parent)
        {
            var copy=new GameObject(original.name).transform;
            copy.gameObject.layer=2;
            copy.SetParent(parent,false);
            copy.localPosition=original.localPosition;copy.localRotation=original.localRotation;copy.localScale=original.localScale;
            var mesh=original.GetComponent<MeshFilter>();
            var renderer=original.GetComponent<MeshRenderer>();
            if(mesh!=null&&mesh.sharedMesh!=null&&renderer!=null&&renderer.enabled)
            {
                copy.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
                var visual=copy.gameObject.AddComponent<MeshRenderer>();
                visual.sharedMaterials=material!=null?Enumerable.Repeat(material,mesh.sharedMesh.subMeshCount).ToArray():
                    renderer.sharedMaterials.Where(m=>m!=null&&!m.name.StartsWith("Outline")).ToArray();
                visual.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                visual.receiveShadows=false;
            }
            foreach(Transform child in original)if(child.gameObject.activeSelf)Copy(child,copy);
            return copy;
        }
        var model=Copy(source.transform,root.transform);
        var renderers=root.GetComponentsInChildren<MeshRenderer>();
        var bounds=new Bounds();
        if(renderers.Length>0)
        {
            bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        }
        centerOffset=bounds.center;
        model.position-=centerOffset;
        return root;
    }
    void CreateDropGhosts()
    {
        if(station!=null)
            foreach(var button in station.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                dropBlockingButtons.Add((RectTransform)button.transform);
        var renderers=preview.GetComponentsInChildren<MeshRenderer>();
        if(renderers.Length>0)
        {
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            dropOutlineMesh=CreateLandingOutline(bounds.size,dropOutlineWidth,dropOutlinePadding);
        }
        foreach(var target in activeStation.Slots.Concat(new[]{prepTarget,discardTarget}).Where(t=>t!=null).Distinct())
        {
            var ghost=Instantiate(preview,activeStation.transform);
            ghost.name="Drop Preview "+target.name;
            foreach(var renderer in ghost.GetComponentsInChildren<MeshRenderer>())
                if(dropGhostMaterial!=null)renderer.sharedMaterials=Enumerable.Repeat(dropGhostMaterial,renderer.sharedMaterials.Length).ToArray();
            if(dropOutlineMesh!=null)
            {
                var outline=new GameObject("Dashed landing outline",typeof(MeshFilter),typeof(MeshRenderer));
                outline.transform.SetParent(ghost.transform,false);
                outline.GetComponent<MeshFilter>().sharedMesh=dropOutlineMesh;
                var renderer=outline.GetComponent<MeshRenderer>();renderer.sharedMaterial=dropGhostMaterial;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                var properties=new MaterialPropertyBlock();properties.SetColor("_BaseColor",new Color(1,1,1,.95f));
                renderer.SetPropertyBlock(properties);
            }
            ghost.SetActive(false);
            dropGhosts.Add((target,ghost));
        }
    }
    internal static Mesh CreateLandingOutline(Vector3 size,float width,float padding)
    {
        float x=size.x*.5f+padding,z=size.z*.5f+padding,y=-size.y*.5f+.012f;
        var corners=new[]{new Vector3(-x,y,-z),new Vector3(x,y,-z),new Vector3(x,y,z),new Vector3(-x,y,z)};
        var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int edge=0;edge<4;edge++)
        {
            Vector3 a=corners[edge],b=corners[(edge+1)%4],side=Vector3.Cross((b-a).normalized,Vector3.up)*width*.5f;
            int count=Mathf.Max(3,Mathf.CeilToInt(Vector3.Distance(a,b)/.09f));
            for(int i=0;i<count;i++)
            {
                var start=Vector3.Lerp(a,b,(i+.12f)/count);var end=Vector3.Lerp(a,b,(i+.72f)/count);
                int v=vertices.Count;vertices.AddRange(new[]{start-side,start+side,end+side,end-side});
                triangles.AddRange(new[]{v,v+1,v+2,v,v+2,v+3});
            }
        }
        var mesh=new Mesh{name="Food-sized dashed landing outline"};
        mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();mesh.RecalculateNormals();
        return mesh;
    }
    void UpdateDropGhosts()
    {
        foreach(var entry in dropGhosts)
        {
            if(entry.ghost==null)continue;
            bool visible=drag!=null&&CanDrop(entry.target);
            if(visible)
            {
                var anchor=DropAnchor(entry.target);
                entry.ghost.transform.SetPositionAndRotation(DropGhostPosition(entry.target),anchor.rotation);
                var point=stationCamera.WorldToScreenPoint(entry.ghost.transform.position);
                visible=point.z>stationCamera.nearClipPlane&&stationCamera.pixelRect.Contains(point);
            }
            // The selected destination is represented by the green held ghost, without double drawing.
            entry.ghost.SetActive(visible&&entry.target!=hovered);
        }
    }
    void ClearDropGhosts()
    {
        foreach(var entry in dropGhosts)if(entry.ghost!=null){entry.ghost.SetActive(false);Destroy(entry.ghost);}
        dropGhosts.Clear();
        dropBlockingButtons.Clear();
        if(dropOutlineMesh!=null)Destroy(dropOutlineMesh);
        dropOutlineMesh=null;
    }
}
