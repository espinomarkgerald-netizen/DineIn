using System.Collections;
using UnityEngine;

public class RoleCameraController : MonoBehaviour
{
    [SerializeField] private MainCameraController mainCameraController;
    [SerializeField] private float moveDuration = 0.35f;
    [SerializeField] private Vector3 framingOffset;

    private Coroutine moveRoutine;

    private void Awake()
    {
        if (mainCameraController == null)
            mainCameraController = FindFirstObjectByType<MainCameraController>();
    }

    public void PanToTarget(Transform target)
    {
        if (mainCameraController == null || target == null) return;

        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(PanRoutine(target));
    }

    private IEnumerator PanRoutine(Transform target)
    {
        Vector3 startPos = mainCameraController.transform.position;
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

            Vector3 pos = Vector3.Lerp(startPos, endPos, k);
            mainCameraController.SetRigTargetPosition(pos, true);

            yield return null;
        }

        mainCameraController.SetRigTargetPosition(endPos, true);
        moveRoutine = null;
    }
}