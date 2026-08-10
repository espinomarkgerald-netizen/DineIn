using UnityEngine;

public class LoopMovement : MonoBehaviour
{
    [Header("Positions")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;

    [Header("Settings")]
    [SerializeField] private float speed = 2.0f;

    private float progress = 0.0f;

    void Update()
    {
        // Increment progress based on time
        progress += Time.deltaTime * speed;

        // Use Lerp to move between positions
        transform.position = Vector3.Lerp(startPoint.position, endPoint.position, progress);

        // Reset the loop
        if (progress >= 1.0f)
        {
            progress = 0.0f;
        }
    }
}