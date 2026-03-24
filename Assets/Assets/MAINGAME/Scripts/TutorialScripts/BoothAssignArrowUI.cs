using UnityEngine;

public class BoothAssignArrowUI : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private UIFollowWorldPoint follow;

    [Header("Bounce")]
    [SerializeField] private RectTransform bounceTarget;
    [SerializeField] private float bounceHeight = 18f;
    [SerializeField] private float bounceSpeed = 3f;

    private Vector3 startLocalPos;
    private bool initialized;

    private void Awake()
    {
        if (follow == null)
            follow = GetComponent<UIFollowWorldPoint>();

        if (bounceTarget == null && transform.childCount > 0)
            bounceTarget = transform.GetChild(0) as RectTransform;
    }

    private void OnEnable()
    {
        if (bounceTarget != null)
            startLocalPos = bounceTarget.localPosition;
    }

    private void Update()
    {
        if (!initialized || bounceTarget == null)
            return;

        Vector3 pos = startLocalPos;
        pos.y += Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;
        bounceTarget.localPosition = pos;
    }

    public void Init(Transform target, Vector3 worldOffset, Camera cam)
    {
        if (follow != null)
            follow.Init(target, worldOffset, cam);

        if (bounceTarget != null)
            startLocalPos = bounceTarget.localPosition;

        initialized = true;
    }
}