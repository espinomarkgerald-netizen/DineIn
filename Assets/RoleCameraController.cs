using System.Collections;
using UnityEngine;

public class RoleCameraController : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float moveDuration = 0.35f;
    [SerializeField] private Vector3 framingOffset;

    private Coroutine moveRoutine;

    public void PanToTarget(Transform target)
    {
        if (targetCamera == null || target == null) return;

        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(PanRoutine(target));
    }

    private IEnumerator PanRoutine(Transform target)
    {
        Transform cam = targetCamera.transform;

        Vector3 startPos = cam.position;
        Quaternion fixedRot = cam.rotation;

        Vector3 endPos = new Vector3(
            target.position.x + framingOffset.x,
            startPos.y + framingOffset.y,
            target.position.z + framingOffset.z
        );

        float t = 0f;

        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / moveDuration);
            k = k * k * (3f - 2f * k);

            cam.position = Vector3.Lerp(startPos, endPos, k);
            cam.rotation = fixedRot;

            yield return null;
        }

        cam.position = endPos;
        cam.rotation = fixedRot;
        moveRoutine = null;
    }
}