using TMPro;
using UnityEngine;

/// <summary>
/// Positions this nametag above the player and keeps it facing the scene camera.
///
/// The billboard GameObject is a plain Transform anchor; the World-Space Canvas is its child
/// and controls its own visual size via its own localScale (default 0.01) and RectTransform size.
/// This script must NOT modify localScale — doing so collapses the Canvas to an invisible size.
///
/// Positioning: each LateUpdate the transform is placed at followTarget.position + worldOffset.
/// Billboard: the transform is rotated so its forward points toward the camera (viewer reads head-on).
/// Camera: resolved via PlayerSetup.FindActiveSceneCamera() on Start and retried every 0.5 s if lost.
/// </summary>
public class NameTagBillboard : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private TMP_Text nameText;

    [Header("Position")]
    [Tooltip("World-space offset added on top of the followTarget position. " +
             "followTarget is typically the head bone (~Y 1.7 m), so 0.4 puts the tag just above.")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.35f, 0f);

    [Header("Billboard")]
    [Tooltip("When true the canvas always rotates so the text faces the camera.")]
    [SerializeField] private bool faceCamera = true;

    private Camera activeCam;

    private const float CameraRetryInterval = 0.5f;
    private float cameraRetryTimer;

    private void Start()
    {
        if (nameText == null)
            nameText = GetComponentInChildren<TMP_Text>(true);

        if (followTarget == null && transform.parent != null)
            followTarget = transform.parent;

        // Best-effort camera resolve at Start (scene is more likely ready than at Awake).
        activeCam = PlayerSetup.FindActiveSceneCamera();

        // Reset the timer so the first retry attempt happens after one interval, not immediately.
        cameraRetryTimer = CameraRetryInterval;
    }

    private void LateUpdate()
    {
        // Retry camera resolution periodically when the reference is missing.
        if (activeCam == null)
        {
            cameraRetryTimer -= Time.deltaTime;
            if (cameraRetryTimer <= 0f)
            {
                activeCam = PlayerSetup.FindActiveSceneCamera();
                cameraRetryTimer = CameraRetryInterval;
            }

            if (activeCam == null) return;
        }

        // Follow target position.
        if (followTarget != null)
            transform.position = followTarget.position + worldOffset;

        // Rotate so the canvas forward points toward the camera (viewer reads it head-on).
        if (faceCamera)
        {
            Vector3 dir = transform.position - activeCam.transform.position;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Assigns the camera this billboard faces. Called by PlayerSetup after spawn.</summary>
    public void SetCamera(Camera cam) => activeCam = cam;

    /// <summary>Sets the displayed name text.</summary>
    public void SetName(string newName)
    {
        if (nameText != null)
            nameText.text = newName;
    }

    /// <summary>Overrides the transform this nametag tracks above.</summary>
    public void SetFollowTarget(Transform t) => followTarget = t;
}

