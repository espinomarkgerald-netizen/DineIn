using UnityEngine;

/// <summary>Geometry-owned carry frame. Pure authoring data; does not change other restaurant trays.</summary>
[DisallowMultipleComponent]
public sealed class FoodTrayCarryPose : MonoBehaviour
{
    [SerializeField] private Transform carryOrigin;
    [SerializeField] private Transform leftGrip;
    [SerializeField] private Transform rightGrip;
    [Tooltip("Absolute uniform tray scale while carried by a Fast Food customer. Restored on release.")]
    [SerializeField, Min(.01f)] private float carryWorldScale = 160f;
    public Transform CarryOrigin => carryOrigin;
    public Transform LeftGrip => leftGrip;
    public Transform RightGrip => rightGrip;
    public float CarryWorldScale => carryWorldScale;
    public bool IsValid => carryOrigin != null && leftGrip != null && rightGrip != null;
}
