using UnityEngine;

/// <summary>Visual seated performance; navigation and hiring remain owned by the worker.</summary>
[DefaultExecutionOrder(11000)]
public sealed class PianistPlayingPose : MonoBehaviour
{
    public KitchenWorkerBot worker;
    public Animator animator;
    [Tooltip("Seat surface, facing the keyboard. Does not move the NavMesh agent.")]
    public Transform seat;
    public Transform leftKeys;
    public Transform rightKeys;
    [Min(0)] public float hipClearance = .14f;
    [Range(0, .2f)] public float keyTravel = .055f;
    [Range(0, .2f)] public float handSweep = .12f;
    [Range(1, 12)] public float tempo = 5f;
    [Range(0, 5)] public float bodySway = 1.4f;

    private Transform[] bones;
    private Quaternion[] restRotations;
    private Vector3[] restPositions;
    private bool posed;
    private Avatar posedAvatar;
    private static readonly HumanBodyBones[] BoneIds = {
        HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand
    };

    private void LateUpdate()
    {
        if (worker != null && worker.IsPerforming) ApplyPose(Time.time);
        else RestorePose();
    }

    // Also used by the editor's isolated pose validation, without running campaign state.
    public void ApplyPose(float time)
    {
        if (seat == null || leftKeys == null || rightKeys == null || animator == null || !animator.isHuman) return;
        if (posed && (posedAvatar != animator.avatar || bones[0] == null)) RestorePose();
        if (!posed)
        {
            posedAvatar = animator.avatar;
            bones = new Transform[BoneIds.Length];
            restRotations = new Quaternion[bones.Length];
            restPositions = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i] = animator.GetBoneTransform(BoneIds[i]);
                if (bones[i] == null) continue;
                restRotations[i] = bones[i].localRotation;
                restPositions[i] = bones[i].localPosition;
            }
            posed = true;
        }
        ResetBones();
        if (bones[0] == null) return;
        Vector3 facing = bones[9] != null && bones[12] != null
            ? Vector3.Cross(bones[12].position - bones[9].position, Vector3.up).normalized : transform.forward;
        bones[0].rotation = Quaternion.AngleAxis(Vector3.SignedAngle(facing, seat.forward, Vector3.up), Vector3.up) * bones[0].rotation;
        bones[0].position = seat.position + Vector3.up * hipClearance;
        if (bones[1] != null)
            bones[1].rotation = Quaternion.AngleAxis(Mathf.Sin(time * tempo * .25f) * bodySway, seat.forward) * bones[1].rotation;
        BendLeg(3, seat.forward);
        BendLeg(6, seat.forward);
        PlayHand(9, leftKeys, time, 0);
        PlayHand(12, rightKeys, time, 2.1f);
    }

    private void BendLeg(int index, Vector3 forward)
    {
        var thigh = bones[index]; var shin = bones[index + 1]; var foot = bones[index + 2];
        if (thigh == null || shin == null) return;
        // Some of the game's short-legged humanoids omit a mapped foot.
        Transform end = foot != null ? foot : (shin.childCount > 0 ? shin.GetChild(0) : null);
        Aim(thigh, shin, thigh.position + forward);
        if (end != null) Aim(shin, end, shin.position + Vector3.down);
    }

    private void PlayHand(int index, Transform keys, float time, float phase)
    {
        var upper = bones[index]; var lower = bones[index + 1]; var hand = bones[index + 2];
        if (upper == null || lower == null || hand == null) return;
        var target = keys.position + seat.right * (Mathf.Sin(time * tempo * .31f + phase) * handSweep)
            + Vector3.up * (Mathf.Abs(Mathf.Sin(time * tempo + phase)) * keyTravel);
        for (int i = 0; i < 10; i++) { Aim(lower, hand, target); Aim(upper, hand, target); }
        hand.rotation = Quaternion.AngleAxis(Mathf.Sin(time * tempo + phase) * 7f, seat.right) * hand.rotation;
    }

    private static void Aim(Transform bone, Transform end, Vector3 target)
    {
        Vector3 current = end.position - bone.position, desired = target - bone.position;
        if (current.sqrMagnitude > .000001f && desired.sqrMagnitude > .000001f)
            bone.rotation = Quaternion.FromToRotation(current, desired) * bone.rotation;
    }

    private void ResetBones()
    {
        for (int i = 0; i < bones.Length; i++)
            if (bones[i] != null) { bones[i].localRotation = restRotations[i]; bones[i].localPosition = restPositions[i]; }
    }
    public void RestorePose() { if (!posed) return; ResetBones(); posed = false; }
    private void OnDisable() => RestorePose();
    private void OnDestroy() => RestorePose();
}





