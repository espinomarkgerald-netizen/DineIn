using UnityEngine;

public class WorldSpaceUIFaceCamera : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    [Header("Adjustment")]
    [Tooltip("Use this only if the UI appears backwards or needs a small rotation correction.")]
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    private void Awake()
    {
        FindCamera();
    }

    private void LateUpdate()
    {
        FindCamera();

        if (targetCamera == null)
            return;

        // Keeps the UI perfectly parallel to the camera,
        // making it feel like a screen-space UI.
        Quaternion cameraFacingRotation =
            Quaternion.LookRotation(
                targetCamera.transform.forward,
                targetCamera.transform.up
            );

        transform.rotation =
            cameraFacingRotation *
            Quaternion.Euler(rotationOffset);
    }

    private void FindCamera()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }
}
