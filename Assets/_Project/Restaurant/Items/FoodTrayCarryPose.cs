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
    [Header("Staff carry")]
    [Tooltip("Grip midpoint relative to the actor's hips, in world metres along the actor's axes.")]
    [SerializeField] private Vector3 staffGripOffset = new Vector3(0f, .45f, .35f);
    [Tooltip("Rotation of the carry frame when this tray sits in a trolley rack.")]
    [SerializeField] private Vector3 trolleyEulerAngles;
    private Animator posedAnimator;
    private Avatar posedAvatar;
    private Transform hips, leftUpper, leftLower, leftHand, rightUpper, rightLower, rightHand;

    // The imported tray root is not necessarily horizontal. Align its authored
    // carry frame instead of assuming identity rotation means an upright tray.
    public void AlignOrigin(Vector3 position, Quaternion rotation)
    {
        if (!IsValid) return;
        Quaternion frame = Quaternion.Inverse(transform.rotation) * carryOrigin.rotation;
        transform.rotation = rotation * Quaternion.Inverse(frame);
        transform.position += position - carryOrigin.position;
    }

    public void AlignToTrolley(Transform slot, Vector3 offset, Vector3 euler)
    {
        Vector3 contact = slot.TransformPoint(offset);
        AlignOrigin(contact, slot.rotation * Quaternion.Euler(euler + trolleyEulerAngles));
        // Imported tray origins differ. Seat the actual base on the shelf marker.
        float bottom = float.PositiveInfinity;
        foreach (var renderer in GetComponentsInChildren<Renderer>())
            if (renderer.enabled) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
        if (!float.IsPositiveInfinity(bottom))
            transform.position += Vector3.up * (contact.y - bottom);
    }

    public void AlignStaffHands(Transform actor, Animator animator)
    {
        if (!IsValid || animator == null || !animator.isHuman) return;
        if (posedAnimator != animator || posedAvatar != animator.avatar || leftHand == null)
        {
            posedAnimator = animator; posedAvatar = animator.avatar;
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
        if (hips == null || leftHand == null || rightHand == null) return;
        Vector3 center = actor.position;
        center.y = hips.position.y;
        center += actor.rotation * staffGripOffset;
        AlignOrigin(carryOrigin.position, actor.rotation);
        transform.position += center - (leftGrip.position + rightGrip.position) * .5f;
        Quaternion l = leftHand.rotation, r = rightHand.rotation;
        AlienProceduralAnimation.ApplyCcdArm(leftUpper, leftLower, leftHand, leftGrip.position, 1f, 12);
        AlienProceduralAnimation.ApplyCcdArm(rightUpper, rightLower, rightHand, rightGrip.position, 1f, 12);
        leftHand.rotation = l; rightHand.rotation = r;
    }

    public Transform CarryOrigin => carryOrigin;
    public Transform LeftGrip => leftGrip;
    public Transform RightGrip => rightGrip;
    public float CarryWorldScale => carryWorldScale;
    public bool IsValid => carryOrigin != null && leftGrip != null && rightGrip != null;
}
