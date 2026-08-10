using UnityEngine;

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
    private float t;
    private float originalZ;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
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

        if (resetRotation && rect != null)
        {
            rect.localRotation = Quaternion.Euler(0f, 0f, originalZ);
        }
    }

    private void LateUpdate()
    {
        if (!shake || rect == null) return;

        t += Time.unscaledDeltaTime;

        float angle = maxAngle;

        if (rampUp)
            angle *= Mathf.Lerp(1f, rampMultiplier, Mathf.Clamp01(t));

        float tilt = Mathf.Sin(t * frequency) * angle;

        rect.localRotation = Quaternion.Euler(0f, 0f, originalZ + tilt);
    }
}
