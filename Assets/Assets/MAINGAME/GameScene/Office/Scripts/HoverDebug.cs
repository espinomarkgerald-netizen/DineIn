using UnityEngine;
using UnityEngine.InputSystem;

public class HoverDebug : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (Mouse.current == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            Debug.Log("Raycast hit: " + hit.collider.name);
        }
        else
        {
            Debug.Log("Raycast hit nothing");
        }
    }
}