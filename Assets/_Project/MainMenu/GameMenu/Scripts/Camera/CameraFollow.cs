using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Tooltip("The character (Chef) for the camera to follow.")]
    public Transform target;

    [Tooltip("How smoothly the camera follows the target.")]
    public float smoothSpeed = 5f;

    private Vector3 offset;
    private bool isInitialized = false;

    void Start()
    {
        if (target != null)
        {
            // Automatically capture the exact distance/offset between 
            // your camera's manual starting position and the target.
            offset = transform.position - target.position;
            isInitialized = true;
        }
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