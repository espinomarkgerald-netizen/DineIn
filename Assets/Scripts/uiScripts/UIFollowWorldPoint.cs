using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UIFollowWorldPoint : MonoBehaviour
{
    public Transform target;
    public Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);

    private RectTransform rect;
    private Camera cam;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    // ✅ Supports 3 args (matches your CustomerGroup calls)
    public void Init(Transform followTarget, Vector3 offset, Camera followCam)
    {
        target = followTarget;
        worldOffset = offset;
        cam = followCam != null ? followCam : Camera.main;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(target.position + worldOffset);

        // Hide if behind camera
        if (screenPos.z < 0f)
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            return;
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        // Puddle-style follow (Screen Space Overlay recommended)
        rect.position = screenPos;
    }
}
