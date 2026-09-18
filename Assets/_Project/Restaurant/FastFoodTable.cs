using UnityEngine;

/// <summary>Editable service anchors for furniture imported as part of the restaurant model.</summary>
[RequireComponent(typeof(Booth))]
public sealed class FastFoodTable : MonoBehaviour
{
    [SerializeField] private Transform furnitureRoot;
    [SerializeField] private string furnitureName;
    public Renderer Furniture
    {
        get
        {
            if (furnitureRoot == null) return null;
            foreach (var renderer in furnitureRoot.GetComponentsInChildren<Renderer>(true))
                if (renderer.name == furnitureName) return renderer;
            return null;
        }
    }
    public bool Contains(Renderer renderer) => renderer != null && furnitureRoot != null
        && renderer.transform.IsChildOf(furnitureRoot) && renderer.name == furnitureName;
    private void OnDrawGizmosSelected()
    {
        Booth booth = GetComponent<Booth>();
        Gizmos.color = Color.cyan;
        if (booth.approachPoint != null) Gizmos.DrawWireSphere(booth.approachPoint.position, .4f);
        Gizmos.color = Color.green;
        foreach (var seat in booth.seats)
            if (seat != null) { Gizmos.DrawWireSphere(seat.position, .25f); Gizmos.DrawRay(seat.position, seat.forward * .5f); }
    }
}
