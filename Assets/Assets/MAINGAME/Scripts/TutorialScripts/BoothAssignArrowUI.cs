using UnityEngine;

public class BoothAssignArrowUI : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private UIFollowWorldPoint follow;

    [Header("Bounce")]
    [SerializeField] private RectTransform bounceTarget;
    [SerializeField] private float bounceHeight = 18f;
    [SerializeField] private float bounceSpeed = 3f;

    [Header("Debug")]
    [SerializeField] private bool debugPinToScreenCenter = true;

    private Vector3 startLocalPos;
    private bool initialized;
    private RectTransform rootRect;

    private void Awake()
    {
        if (follow == null)
            follow = GetComponent<UIFollowWorldPoint>();

        if (bounceTarget == null && transform.childCount > 0)
            bounceTarget = transform.GetChild(0) as RectTransform;

        rootRect = GetComponent<RectTransform>();
        if (rootRect == null)
            rootRect = GetComponentInChildren<RectTransform>(true);
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
        if (debugPinToScreenCenter)
        {
            if (follow != null)
                follow.enabled = false;

            if (rootRect != null)
            {
                rootRect.anchorMin = new Vector2(0.5f, 0.5f);
                rootRect.anchorMax = new Vector2(0.5f, 0.5f);
                rootRect.pivot = new Vector2(0.5f, 0.5f);
                rootRect.anchoredPosition = Vector2.zero;
                rootRect.localScale = Vector3.one;
                rootRect.localRotation = Quaternion.identity;
            }

            if (bounceTarget != null)
                startLocalPos = bounceTarget.localPosition;

            Debug.Log("[BoothAssignArrowUI] DEBUG pin to screen center", this);
            initialized = true;
            return;
        }

        if (follow != null)
        {
            follow.enabled = true;
            follow.Init(target, worldOffset, cam);
        }

        if (bounceTarget != null)
            startLocalPos = bounceTarget.localPosition;

        initialized = true;
    }
}