using UnityEngine;

public class AutoInteractRadius : MonoBehaviour
{
    [SerializeField] private float radius = 1.5f;

    public float Radius => radius;

    public bool IsActiveRoleInRange(StaffRole.Role role)
    {
        if (RoleManager.Instance == null) return false;
        if (!RoleManager.Instance.IsActiveRoleType(role)) return false;

        var mover = RoleManager.Instance.GetActivePlayerMovement();
        if (mover == null) return false;

        return Vector3.Distance(mover.transform.position, transform.position) <= radius;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}