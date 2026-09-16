using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// One local circle per actor. Existing authoritative task snapshots identify
// work; animation never issues a command and adds no network tick of its own.
[DisallowMultipleComponent]
public sealed class MultiplayerWorkIndicator : MonoBehaviour
{
    private sealed class Circle
    {
        public Transform actor;
        public Transform anchor, head;
        public Renderer body;
        public GameObject root;
        public MultiplayerWorkCircleGraphic graphic;
        public int seen;
    }
    private MultiplayerSessionManager session;
    private MultiplayerTaskClaims claims;
    private readonly Dictionary<string, Circle> circles = new();
    private readonly List<string> removed = new();
    private int frame;
    private void Awake()
    {
        session = GetComponent<MultiplayerSessionManager>();
        claims = GetComponent<MultiplayerTaskClaims>();
    }
    private void LateUpdate()
    {
        frame++;
        bool visible = session != null && claims != null && session.IsConnected && !session.Ended
            && MultiplayerProgressionContext.Ready && !MultiplayerRestockView.Active;
        if (visible)
        {
            foreach (var work in claims.ActorWorkStates)
            {
                if (work == null || string.IsNullOrEmpty(work.actor)) continue;
                if (!circles.TryGetValue(work.actor, out var circle) || circle.actor == null || circle.root == null)
                {
                    if (circle?.root != null) Destroy(circle.root);
                    if (circle?.anchor != null) Destroy(circle.anchor.gameObject);
                    Transform actor = ResolveActor(work.actor);
                    if (actor == null || !actor.gameObject.activeInHierarchy) continue;
                    circle = CreateCircle(actor);
                    circles[work.actor] = circle;
                }
                if (!circle.actor.gameObject.activeInHierarchy) continue;
                circle.seen = frame;
                circle.root.SetActive(true);
                circle.anchor.position = circle.head != null ? circle.head.position + Vector3.up * 0.3f
                    : circle.body != null ? new Vector3(circle.body.bounds.center.x, circle.body.bounds.max.y + 0.15f, circle.body.bounds.center.z)
                    : circle.actor.position + Vector3.up * 2f;
                bool timed = TryProgress(work, PhotonNetwork.Time, out float progress);
                circle.graphic.SetAmount(timed ? progress : 0.24f);
                circle.graphic.SetAngle(timed ? 0f : -Time.unscaledTime * 220f % 360f);
            }
        }
        removed.Clear();
        foreach (var pair in circles)
        {
            var circle = pair.Value;
            if (circle.actor == null || circle.root == null)
            {
                if (circle.root != null) Destroy(circle.root);
                if (circle.anchor != null) Destroy(circle.anchor.gameObject);
                removed.Add(pair.Key);
            }
            else if (circle.seen != frame) circle.root.SetActive(false);
        }
        foreach (string id in removed) circles.Remove(id);
    }
    internal static bool TryProgress(MultiplayerTaskClaims.ActorWork work, double now, out float progress)
    {
        progress = 0f;
        if (work == null || work.phase != MultiplayerTaskClaims.WorkPhase.Working || work.duration <= 0f
            || float.IsNaN(work.duration) || float.IsInfinity(work.duration)
            || double.IsNaN(work.startedAt) || double.IsInfinity(work.startedAt)
            || double.IsNaN(now) || double.IsInfinity(now)) return false;
        progress = Mathf.Clamp01((float)((now - work.startedAt) / work.duration));
        return true;
    }
    private Transform ResolveActor(string id)
    {
        if (id.StartsWith("player:") && int.TryParse(id.Substring(7), out int actor))
            return session.TryGetManager(actor, out var manager) ? manager.transform : null;
        foreach (var bot in MultiplayerWorldRegistry.All<AutonomousStaffBot>())
            if (bot != null && id == "staff:" + bot.name) return bot.transform;
        return null;
    }
    private static Circle CreateCircle(Transform actor)
    {
        var root = new GameObject("Work activity", typeof(RectTransform));
        ((RectTransform)root.transform).sizeDelta = new Vector2(14.4f, 14.4f);
        var ring = new GameObject("Circle", typeof(RectTransform), typeof(CanvasRenderer), typeof(MultiplayerWorkCircleGraphic));
        ring.transform.SetParent(root.transform, false);
        var rect = (RectTransform)ring.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var graphic = ring.GetComponent<MultiplayerWorkCircleGraphic>();
        graphic.raycastTarget = false;
        graphic.color = new Color(0.20f, 0.85f, 1f, 1f);
        var anchor = new GameObject("Work head anchor").transform;
        anchor.SetParent(actor, false);
        Transform head = null;
        foreach (var animator in actor.GetComponentsInChildren<Animator>(true))
            if (animator.avatar != null && animator.isHuman)
            { head = animator.GetBoneTransform(HumanBodyBones.Head); if (head != null) break; }
        Renderer body = null;
        foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (body == null || renderer.bounds.size.sqrMagnitude > body.bounds.size.sqrMagnitude) body = renderer;
        root.AddComponent<UIFollowWorldPoint>().InitAboveTarget(anchor, Vector3.zero,
            PlayerSetup.FindActiveSceneCamera(), gapPixels: 18f);
        return new Circle { actor = actor, anchor = anchor, head = head, body = body, root = root, graphic = graphic };
    }
    private void OnDisable()
    { foreach (var circle in circles.Values) if (circle.root != null) circle.root.SetActive(false); }
    private void OnDestroy()
    {
        foreach (var circle in circles.Values)
        { if (circle.root != null) Destroy(circle.root); if (circle.anchor != null) Destroy(circle.anchor.gameObject); }
        circles.Clear();
    }
}
