using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class FaceCamera : MonoBehaviour {
    private Canvas _canvas;
    private Camera _cachedCamera;

    void Awake() {
        _canvas = GetComponent<Canvas>();
    }

    void LateUpdate() {
        // Refresh cached camera reference when it changes (e.g. network camera spawned at runtime)
        if (_cachedCamera == null || _cachedCamera != Camera.main) {
            _cachedCamera = Camera.main;
        }

        if (_cachedCamera == null) return;

        // Keep the canvas facing the camera
        transform.forward = _cachedCamera.transform.forward;

        // Re-assign the world-camera so the canvas renders correctly
        if (_canvas.worldCamera != _cachedCamera) {
            _canvas.worldCamera = _cachedCamera;
        }
    }
}