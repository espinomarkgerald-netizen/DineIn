using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UIShake : MonoBehaviour
{
    [Header("Shake Toggle")]
    public bool shake;

    [Header("Tilt Settings")]
    [Tooltip("Maximum rotation angle in degrees.")]
    public float maxAngle = 8f;

    [Tooltip("How fast it tilts.")]
    public float frequency = 20f;

    [Tooltip("Optional: stronger over time while shaking.")]
    public bool rampUp = true;

    public float rampMultiplier = 2f;

    private RectTransform rect;
    private Canvas canvas;
    private float t;
    private float originalZ;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvas = GetComponent<Canvas>();
        originalZ = rect.localEulerAngles.z;
    }

    public void StartShake()
    {
        shake = true;
        t = 0f;
    }

    public void StopShake(bool resetRotation = true)
    {
        shake = false;
        t = 0f;

        if (!resetRotation || rect == null)
            return;

        if (TryGetFacingCamera(out Camera cam))
            rect.rotation = cam.transform.rotation;
        else
            rect.localRotation = Quaternion.Euler(0f, 0f, originalZ);
    }

    private void LateUpdate()
    {
        if (!shake || rect == null) return;

        t += LevelOneUIAccessibility.UnscaledAnimationDeltaTime;

        float angle = maxAngle;
        if (rampUp)
            angle *= Mathf.Lerp(1f, rampMultiplier, Mathf.Clamp01(t));

        float tilt = Mathf.Sin(t * frequency) * angle;

        if (TryGetFacingCamera(out Camera cam))
        {
            // Face the camera, then roll the tilt around that facing
            // rotation's own forward axis so it reads as a billboard wobble
            // instead of fighting UIFollowWorldPoint's camera-facing rotation.
            rect.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, tilt);
        }
        else
        {
            rect.localRotation = Quaternion.Euler(0f, 0f, originalZ + tilt);
        }
    }

    private bool TryGetFacingCamera(out Camera cam)
    {
        cam = null;

        if (canvas == null)
            canvas = GetComponent<Canvas>();

        if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
            return false;

        cam = canvas.worldCamera;
        return cam != null;
    }
}
