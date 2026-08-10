using UnityEngine;

public class FloatingRock : MonoBehaviour
{
    [Header("Float")]
    [SerializeField] private float amplitude = 0.5f;
    [SerializeField] private float frequency = 1.2f;

    [Header("Rotation")]
    [SerializeField] private bool rotate = true;
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 0f, 0f);

    private Vector3 startPos;
    private float offset;

    private void Awake()
    {
        startPos = transform.position;
        offset = Random.Range(0f, 100f); 
    }

    private void Update()
    {
        float y = Mathf.Sin((Time.time + offset) * frequency) * amplitude;
        transform.position = startPos + Vector3.up * y;

        if (rotate)
            transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
    }
}