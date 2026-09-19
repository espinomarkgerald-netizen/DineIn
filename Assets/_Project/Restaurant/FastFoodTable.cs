using UnityEngine;

/// <summary>Authored furniture and service anchors owned by one Fast Food table prefab.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Booth))]
public sealed class FastFoodTable : MonoBehaviour
{
    [Tooltip("The furniture renderer inside this prefab. Seats and service points are its sibling objects.")]
    [SerializeField] private Renderer furniture;
    public Renderer Furniture => furniture;
    public bool Contains(Renderer renderer) => renderer != null && renderer.transform.IsChildOf(transform);
    private void OnDrawGizmosSelected()
    {
        Booth booth = GetComponent<Booth>();
        Gizmos.color = Color.cyan;
        if (booth.approachPoint != null) Gizmos.DrawWireSphere(booth.approachPoint.position, .4f);
        Gizmos.color = Color.green;
        foreach (var seat in booth.seats)
            if (seat != null) { Gizmos.DrawWireSphere(seat.position, .25f); Gizmos.DrawRay(seat.position, seat.forward * .5f); }
        if (booth.NetworkTrayPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(booth.NetworkTrayPoint.position, new Vector3(.75f, .1f, .5f));
        }
    }
}
