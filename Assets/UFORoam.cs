using UnityEngine;

public class UFORoamer : MonoBehaviour
{
    [Header("Waypoints")]
    [SerializeField] private Transform[] roamPoints;

    [Header("Movement")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float turnSpeed = 2.5f;
    [SerializeField] private float arriveDistance = 3f;

    [Header("Altitude")]
    [SerializeField] private float altitudeVariation = 6f;
    [SerializeField] private float altitudeSpeed = 0.5f;

    [Header("Model")]
    [SerializeField] private Transform model;
    [SerializeField] private float spinSpeed = 40f;
    [SerializeField] private float floatHeight = 0.5f;
    [SerializeField] private float floatSpeed = 2f;

    private Transform currentTarget;
    private Vector3 targetPos;
    private float baseAltitude;

    private void Start()
    {
        baseAltitude = transform.position.y;
        PickTarget();
    }

    private void Update()
    {
        Move();
        AnimateModel();
    }

    private void Move()
    {
        if (currentTarget == null) return;

        Vector3 dir = targetPos - transform.position;

        if (dir.magnitude < arriveDistance)
        {
            PickTarget();
            return;
        }

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);

        transform.position += transform.forward * speed * Time.deltaTime;

        float altitudeOffset = Mathf.Sin(Time.time * altitudeSpeed) * altitudeVariation;
        Vector3 pos = transform.position;
        pos.y = baseAltitude + altitudeOffset;
        transform.position = pos;
    }

    private void AnimateModel()
    {
        if (model == null) return;

        float bob = Mathf.Sin(Time.time * floatSpeed) * floatHeight;

        model.localPosition = new Vector3(0f, bob, 0f);

        Quaternion baseRot = Quaternion.Euler(-90f, 0f, 0f);
        Quaternion spin = Quaternion.Euler(0f, Time.time * spinSpeed, 0f);

        model.localRotation = spin * baseRot;
    }

    private void PickTarget()
    {
        if (roamPoints == null || roamPoints.Length == 0) return;

        currentTarget = roamPoints[Random.Range(0, roamPoints.Length)];
        targetPos = currentTarget.position;

        baseAltitude = targetPos.y;
    }
}