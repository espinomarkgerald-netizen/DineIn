using UnityEngine;

// One tracker per actor, including remote humans on the authority. No input or
// animation event is generated here; only actual traveled distance is sampled.
[DisallowMultipleComponent]
public sealed class HygieneWalker : MonoBehaviour
{
    private HygieneState epoch;
    private Vector3 previous;
    private float nextSample, remainder;
    private bool rightFoot;
    private CustomerAgent customer;
    private HygieneStaffCleaningVisual cleaningVisual;
    public static void Ensure(GameObject actor)
    {
        if (actor.GetComponent<HygieneWalker>() == null) actor.AddComponent<HygieneWalker>();
    }
    private void Awake() { customer = GetComponent<CustomerAgent>(); cleaningVisual = GetComponent<HygieneStaffCleaningVisual>(); }
    private void OnEnable() { epoch = null; remainder = 0; }
    private void LateUpdate()
    {
        var manager = HygieneManager.Instance;
        if (cleaningVisual == null) cleaningVisual = GetComponent<HygieneStaffCleaningVisual>();
        if (cleaningVisual != null && manager != null && manager.State.floorRouteActive)
        { epoch = null; return; }
        if (manager == null || !manager.CanRecordLobbyActivity || gameObject.scene != manager.gameObject.scene)
        { epoch = null; return; }
        if (Time.time < nextSample) return;
        float elapsed = Mathf.Max(.1f, Time.time - (nextSample - .1f));
        nextSample = Time.time + .1f;
        Vector3 now = transform.position;
        if (epoch != manager.State || customer != null && customer.Agent != null && !customer.Agent.updatePosition)
        { epoch = manager.State; previous = now; remainder = 0; return; }
        Vector3 delta = now - previous;
        Vector3 planar = new Vector3(delta.x, 0, delta.z);
        float distance = planar.magnitude;
        if (distance > Mathf.Min(3f, elapsed * 12f) || Mathf.Abs(delta.y) > .6f)
        { previous = now; remainder = 0; return; }
        if (distance < .01f) { previous = now; return; }
        Vector3 forward = planar / distance;
        Vector3 right = new Vector3(forward.z, 0, -forward.x);
        float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        float stride = manager.WalkingSpacing;
        for (float step = stride - remainder; step <= distance; step += stride)
        {
            Vector3 position = Vector3.Lerp(previous, now, step / distance) + right * (rightFoot ? .11f : -.11f);
            var group = customer != null ? customer.GetComponentInParent<CustomerGroup>() : null;
            byte color = group == null ? (byte)0 : group.CurrentCustomerType == CustomerGroup.CustomerType.Green ? (byte)1
                : group.CurrentCustomerType == CustomerGroup.CustomerType.Pink ? (byte)2 : (byte)3;
            manager.RecordFootstep(position, yaw, color);
            rightFoot = !rightFoot;
        }
        remainder = (remainder + distance) % stride;
        previous = now;
    }
}
