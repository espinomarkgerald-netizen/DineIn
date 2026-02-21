using UnityEngine;
using TMPro;

public class NameTagBillboard : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform followTarget;      // head bone / point on player
    [SerializeField] private TMP_Text nameText;           // TMP in world canvas OR TMP 3D

    [Header("Follow")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.35f, 0f);

    [Header("Billboard")]
    [SerializeField] private bool faceCamera = true;

    [Header("Consistent Screen Size")]
    [Tooltip("How tall the tag should look on screen (in pixels).")]
    [SerializeField] private float desiredPixelHeight = 40f;

    [Tooltip("If RectTransform doesn't exist, use this as the 'local height' reference for scaling.")]
    [SerializeField] private float fallbackLocalHeight = 1f;

    [Tooltip("Clamps to avoid extremes if super close/far.")]
    [SerializeField] private float minWorldScale = 0.002f;
    [SerializeField] private float maxWorldScale = 0.05f;

    private Camera cam;
    private RectTransform rect;
    private Vector3 baseLocalScale;

    void Awake()
    {
        cam = Camera.main;

        // Could be UI (RectTransform) OR normal Transform (no RectTransform)
        rect = GetComponent<RectTransform>();

        if (nameText == null)
            nameText = GetComponentInChildren<TMP_Text>(true);

        if (followTarget == null && transform.parent != null)
            followTarget = transform.parent; // fallback

        baseLocalScale = transform.localScale;
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // Follow
        if (followTarget != null)
            transform.position = followTarget.position + worldOffset;

        // Billboard
        if (faceCamera)
        {
            // Face camera nicely
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
        }

        // --- Screen-consistent scaling ---
        float distance = Vector3.Distance(cam.transform.position, transform.position);

        // viewHeight = 2 * d * tan(FOV/2)
        float viewHeight = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

        float desiredScreenFrac = desiredPixelHeight / Mathf.Max(1f, Screen.height);
        float targetWorldHeight = viewHeight * desiredScreenFrac;

        // Get local height reference
        float localHeight = fallbackLocalHeight;

        // If we DO have a RectTransform (world-space UI), use its rect height
        if (rect != null)
            localHeight = Mathf.Max(0.0001f, rect.rect.height);

        // Compute world scale needed so localHeight becomes targetWorldHeight
        float worldScale = targetWorldHeight / Mathf.Max(0.0001f, localHeight);
        worldScale = Mathf.Clamp(worldScale, minWorldScale, maxWorldScale);

        // Compensate for parent lossyScale (so it behaves if parent is scaled)
        float parentScale = 1f;
        if (transform.parent != null)
        {
            Vector3 ls = transform.parent.lossyScale;
            parentScale = (ls.x + ls.y + ls.z) / 3f;
            if (parentScale < 0.0001f) parentScale = 1f;
        }

        float correctedLocalScale = worldScale / parentScale;

        // IMPORTANT: keep the original proportions (don't normalize!)
        transform.localScale = baseLocalScale * correctedLocalScale;
    }

    public void SetName(string newName)
    {
        if (nameText != null)
            nameText.text = newName;
    }

    public void SetFollowTarget(Transform t) => followTarget = t;
}
