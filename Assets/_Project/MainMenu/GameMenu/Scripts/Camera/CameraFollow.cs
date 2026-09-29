using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Tooltip("The character (Chef) for the camera to follow.")]
    public Transform target;

    [Tooltip("How smoothly the camera follows the target.")]
    public float smoothSpeed = 5f;

    [Tooltip("Optional menu composition reference: the existing first restaurant destination. The scene camera is authored relative to this point, not the player's startup position.")]
    [SerializeField] private Transform compositionReference;

    private Vector3 offset;
    private bool isInitialized = false;

    private void Awake()
    {
        if (compositionReference == null) return;
        offset = transform.position - compositionReference.position;
        isInitialized = true;
    }

    public void SnapToAuthoredComposition()
    {
        if (compositionReference != null && isInitialized && target != null)
            transform.position = target.position + offset;
    }

    void Start()
    {
        if (target != null && !isInitialized)
        {
            // Automatically capture the exact distance/offset between 
            // your camera's manual starting position and the target.
            offset = transform.position - target.position;
            isInitialized = true;
        }
        SnapToAuthoredComposition();
    }

    void LateUpdate()
    {
        if (target == null || !isInitialized) return;

        // Calculate the target position maintaining your exact starting height and angle
        Vector3 desiredPosition = target.position + offset;

        // Smoothly move the camera to that position
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }
}
