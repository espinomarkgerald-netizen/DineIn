using UnityEngine;

public class BouncingArrow : MonoBehaviour {
    [Header("Bounce Settings")]
    public float speed = 5f;     // How fast it bobs
    public float height = 0.5f;  // How high it goes

    private Vector3 startLocalPos;

    void Start() {
        // We record its starting position inside the folder
        startLocalPos = transform.localPosition;
    }

    void Update() {
        // Mathf.Sin creates a perfect up-and-down wave based on the game's time!
        float newY = startLocalPos.y + Mathf.Sin(Time.time * speed) * height;
        transform.localPosition = new Vector3(startLocalPos.x, newY, startLocalPos.z);
    }
}