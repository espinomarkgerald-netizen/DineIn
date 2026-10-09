using UnityEngine;

// Follow the animated hand after tray arm posing, without inheriting a paper holder's tilt.
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class WineBottleCarryPose : MonoBehaviour
{
    private Transform actor, hand;
    private Vector3 offset, euler;

    public void Bind(Transform owner, Transform fallback, Vector3 positionOffset, Vector3 rotationOffset)
    {
        actor = owner;
        var animator = owner != null ? owner.GetComponentInChildren<Animator>() : null;
        hand = animator != null && animator.isHuman
            ? animator.GetBoneTransform(HumanBodyBones.RightHand) : fallback;
        if (hand == null) hand = fallback;
        offset = positionOffset;
        euler = rotationOffset;
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (actor == null || hand == null) return;
        Quaternion facing = Quaternion.Euler(0f, actor.eulerAngles.y, 0f);
        transform.SetPositionAndRotation(hand.position + facing * offset, facing * Quaternion.Euler(euler));
    }
}
