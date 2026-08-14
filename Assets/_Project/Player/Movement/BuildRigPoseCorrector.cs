using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Corrects the imported staff-model bind pose that can be sideways in a player
/// build before its Animator has a valid sampled pose. The NavMesh character
/// root is never changed; only the nested visual rig is corrected.
/// </summary>
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class BuildRigPoseCorrector : MonoBehaviour
{
    private static readonly string[] AstronautVisualRoots = { "Arms", "Body", "Feet", "Head" };

    private Animator animator;
    private Transform visualRoot;
    private readonly List<Transform> uprightTargets = new List<Transform>();

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>(true);
        visualRoot = animator != null ? animator.transform : null;
    }

    private IEnumerator Start()
    {
        // Editor play mode already evaluates the authored rig correctly. This
        // safeguard exists exclusively for the standalone/mobile player build.
        if (Application.isEditor || animator == null || visualRoot == null)
            yield break;

        // Let Unity create the Animator hierarchy first, then force its authored
        // idle pose. In the player build the imported mesh roots can otherwise
        // remain at their FBX bind rotations.
        yield return null;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.Play("idle", 0, 0f);
        animator.Update(0f);

        ResolveUprightTargets();
        ApplyUprightPose();
        Debug.Log($"[BuildRigPoseCorrector] Keeping {uprightTargets.Count} rig root(s) upright for '{name}'.", this);
    }

    private void LateUpdate()
    {
        // The Animator writes the imported FBX bind rotation during its update.
        // This must run after Animator evaluation, otherwise the standalone player
        // re-applies the sideways -90 degree root before the camera renders.
        ApplyUprightPose();
    }

    private void ResolveUprightTargets()
    {
        uprightTargets.Clear();

        if (visualRoot == transform)
        {
            // Chef.fbx is the manager's root object. Its child named "root" has
            // the same imported -90 degree X bind rotation.
            AddTarget(visualRoot.Find("root"));
            return;
        }

        // Astronaut Final renders through four mesh roots, all authored at -90
        // degrees X. Correcting these leaves its Animator and NavMesh root alone.
        for (int i = 0; i < AstronautVisualRoots.Length; i++)
            AddTarget(visualRoot.Find(AstronautVisualRoots[i]));
    }

    private void AddTarget(Transform target)
    {
        if (target != null)
            uprightTargets.Add(target);
    }

    private void ApplyUprightPose()
    {
        for (int i = 0; i < uprightTargets.Count; i++)
        {
            if (uprightTargets[i] != null)
                uprightTargets[i].localRotation = Quaternion.identity;
        }
    }
}
