using UnityEngine;
using UnityEngine.EventSystems;

public class RaycastDebugger : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask mask;
    [SerializeField] private float dist = 500f;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        if (cam == null) cam = Camera.main;
        Debug.Log($"CLICK. cam={(cam ? cam.name : "NULL")}  mask={mask.value}  overUI={(EventSystem.current!=null && EventSystem.current.IsPointerOverGameObject())}");

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, dist, mask, QueryTriggerInteraction.Collide))
        {
            Debug.Log($"HIT: {hit.collider.name}  layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}  parent={hit.collider.transform.root.name}");
        }
        else
        {
            Debug.Log("NO HIT");
        }
    }
}
