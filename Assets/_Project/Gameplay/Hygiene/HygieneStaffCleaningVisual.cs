using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Runs after guest staff interpolation. Props never own an interaction or task.
[DefaultExecutionOrder(11000)]
[DisallowMultipleComponent]
public sealed class HygieneStaffCleaningVisual : MonoBehaviour
{
    [SerializeField] private HygieneCleaningPresentation presentation;
    [SerializeField] private Transform mopAnchor;
    [SerializeField] private Transform bucketAnchor;
    private Transform mop, bucket;
    private readonly Queue<Vector3> trail = new();
    private Vector3 lastTrail, previous;
    private float bodyHeight, mopHeight, bucketHeight, floorHeight;
    private bool visible;
    private Animator cleaningAnimator;
    private Avatar cleaningAvatar;
    private Transform leftUpper, leftLower, leftHand, rightUpper, rightLower, rightHand;
    private int shownDay;
    private string shownRun;
    public bool IsCleaning => visible;

    public static void Ensure(GameObject root)
    {
        if (root != null && root.GetComponent<HygieneStaffCleaningVisual>() == null)
            root.AddComponent<HygieneStaffCleaningVisual>();
    }

    private bool EnsureProps()
    {
        if (mop != null && bucket != null) return true;
        if (presentation == null) presentation = HygieneCleaningPresentation.Load();
        if (presentation == null || presentation.mop == null || presentation.bucket == null) return false;
        cleaningAnimator = GetComponentInChildren<Animator>(true);
        CacheArms();
        if (mopAnchor == null) mopAnchor = Anchor("CleaningMopAnchor", transform);
        if (bucketAnchor == null) bucketAnchor = Anchor("CleaningBucketAnchor", transform);
        // The first skin may contain only the arms. Use the complete character,
        // otherwise modular staff receive a miniature mop and bucket.
        var look = GetComponent<DineIn.Appearance.CharacterAppearance>();
        bodyHeight = look != null && look.TryGetBodyBounds(out var bounds)
            ? Mathf.Clamp(bounds.size.y, .7f, 5f) : 3f;
        mopHeight = bodyHeight * .95f; bucketHeight = bodyHeight * .45f;
        mop = CreateProp(presentation.mop, "Cleaning Mop", mopHeight);
        bucket = CreateProp(presentation.bucket, "Cleaning Bucket", bucketHeight);
        // Keep explicit anchors for authoring while compensating inherited rig scale.
        mop.SetParent(mopAnchor, true);
        // The bucket follows world-space breadcrumbs. Parenting it to a moving
        // character would move it twice and defeat corner following.
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(bucket.gameObject, gameObject.scene);
        return true;
    }

    private static Transform Anchor(string name, Transform parent)
    {
        var anchor = new GameObject(name).transform; anchor.SetParent(parent, false); return anchor;
    }

    private static Transform CreateProp(GameObject model, string name, float height)
    {
        var root = new GameObject(name).transform;
        var visual = Instantiate(model, root, false).transform;
        // Both imported props use the same authored -90 degree X correction.
        visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        visual.localScale = Vector3.one;
        foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
        foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 2;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float scale = height / Mathf.Max(.0001f, bounds.size.y);
            visual.localScale *= scale;
            visual.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
        }
        return root;
    }

    private void LateUpdate()
    {
        var manager = HygieneManager.Instance;
        var session = MultiplayerSessionManager.Instance;
        bool active = manager != null && manager.State.floorRouteActive && manager.State.lobbyCleaningRequested
            && GameDayManager.Instance?.ServiceActive == true
            && (!MultiplayerDayBridge.IsActive || session != null && !session.Ended && session.IsConnected);
        if (!active) { Hide(); return; }
        if (!EnsureProps()) return;
        if (!visible || shownDay != manager.State.day || shownRun != manager.State.run)
        {
            visible = true; shownDay = manager.State.day; shownRun = manager.State.run;
            mop.gameObject.SetActive(true); bucket.gameObject.SetActive(true);
            trail.Clear(); previous = lastTrail = transform.position;
            floorHeight = transform.position.y;
            var start = Ground(transform.position - transform.forward * bodyHeight * .45f);
            bucket.position = start; bucket.rotation = transform.rotation;
        }
        if (Time.deltaTime <= 0f) return;
        Vector3 now = transform.position;
        if ((now - previous).sqrMagnitude > 9f)
        { trail.Clear(); bucket.position = Ground(now); lastTrail = now; }
        if ((now - lastTrail).sqrMagnitude > .01f)
        {
            trail.Enqueue(Ground(now)); lastTrail = now;
            while (trail.Count > 64) trail.Dequeue();
        }
        float travelled = (now - previous).magnitude;
        previous = now;
        // Follow the same breadcrumb corners, instead of cutting through tables.
        float movement = travelled + Time.deltaTime * .15f;
        if (trail.Count > 0 && Vector3.Distance(bucket.position, now) > bodyHeight * .4f)
        {
            Vector3 destination = trail.Peek();
            Vector3 direction = destination - bucket.position; direction.y = 0f;
            bucket.position = Vector3.MoveTowards(bucket.position, destination, movement);
            if (direction.sqrMagnitude > .001f)
                bucket.rotation = Quaternion.Slerp(bucket.rotation, Quaternion.LookRotation(direction), 1f - Mathf.Exp(-12f * Time.deltaTime));
            if (Vector3.Distance(bucket.position, destination) < .06f) trail.Dequeue();
        }
        float phase = Time.time * Mathf.PI * 2f / Mathf.Max(.5f, presentation.mopCycleSeconds);
        ApplyMoppingPose(now, phase);
    }

    private void ApplyMoppingPose(Vector3 now, float phase)
    {
        CacheArms();
        float sweep = Mathf.Sin(phase) * bodyHeight * presentation.mopSweepWidth;
        Vector3 head = Ground(now + transform.forward * bodyHeight * (.34f + .055f * Mathf.Cos(phase))
            + transform.right * sweep);
        Vector3 grip = now + Vector3.up * bodyHeight * .53f + transform.forward * bodyHeight * .12f;
        Vector3 shaft = (grip - head).normalized;
        mop.SetPositionAndRotation(head, Quaternion.FromToRotation(Vector3.up, shaft));
        SetWorldScale(mop, 1f);
        SetWorldScale(bucket, 1f);
        if (leftHand != null && rightHand != null)
        {
            Quaternion l = leftHand.rotation, r = rightHand.rotation;
            AlienProceduralAnimation.ApplyCcdArm(rightUpper, rightLower, rightHand, head + shaft * mopHeight * .58f, 1f, 12);
            AlienProceduralAnimation.ApplyCcdArm(leftUpper, leftLower, leftHand, head + shaft * mopHeight * .76f, 1f, 12);
            leftHand.rotation = l; rightHand.rotation = r;
        }
    }

    private void CacheArms()
    {
        if (cleaningAnimator == null || !cleaningAnimator.isHuman) return;
        if (cleaningAvatar == cleaningAnimator.avatar && leftHand != null) return;
        cleaningAvatar = cleaningAnimator.avatar;
        leftUpper = cleaningAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        leftLower = cleaningAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        leftHand = cleaningAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
        rightUpper = cleaningAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rightLower = cleaningAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rightHand = cleaningAnimator.GetBoneTransform(HumanBodyBones.RightHand);
    }

    private Vector3 Ground(Vector3 point)
    {
        if (NavMesh.SamplePosition(point, out var hit, 1f, NavMesh.AllAreas)) floorHeight = hit.position.y;
        point.y = floorHeight + .015f;
        return point;
    }
    private static void SetWorldScale(Transform target, float scale)
    {
        Vector3 inherited = target.parent != null ? target.parent.lossyScale : Vector3.one;
        target.localScale = new Vector3(scale / Mathf.Max(.0001f, Mathf.Abs(inherited.x)),
            scale / Mathf.Max(.0001f, Mathf.Abs(inherited.y)), scale / Mathf.Max(.0001f, Mathf.Abs(inherited.z)));
    }
    private void Hide()
    {
        visible = false; trail.Clear();
        if (mop != null) mop.gameObject.SetActive(false);
        if (bucket != null) bucket.gameObject.SetActive(false);
    }
    private void OnDisable()
    {
        Hide();
        if (MultiplayerServiceStaffBridge.CanSimulate) HygieneManager.Instance?.CancelLobbyCleaning();
    }
    private void OnDestroy()
    {
        if (mop != null) Destroy(mop.gameObject);
        if (bucket != null) Destroy(bucket.gameObject);
    }
}
