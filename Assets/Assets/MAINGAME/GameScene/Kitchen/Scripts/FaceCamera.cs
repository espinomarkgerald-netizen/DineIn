using UnityEngine;

public class FaceCamera : MonoBehaviour {
    void LateUpdate() {
        // This tells the UI to match the exact same rotation as your main camera
        if (Camera.main != null) {
            transform.forward = Camera.main.transform.forward;
        }
    }
}