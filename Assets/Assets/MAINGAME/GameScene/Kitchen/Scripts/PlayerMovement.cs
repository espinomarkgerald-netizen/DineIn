using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    NavMeshAgent agent;
    Camera cam;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        cam = Camera.main;
    }

    void Update()
    {
        Vector2 pos;
        bool pressed = false;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            pos = Mouse.current.position.ReadValue();
            pressed = true;
        }
        else if (Touchscreen.current != null &&
                 Touchscreen.current.primaryTouch.phase.ReadValue() ==
                 UnityEngine.InputSystem.TouchPhase.Began)
        {
            pos = Touchscreen.current.primaryTouch.position.ReadValue();
            pressed = true;
        }
        else return;

        Ray ray = cam.ScreenPointToRay(pos);

        if (Physics.Raycast(ray, out RaycastHit hit))
            agent.SetDestination(hit.point);
    }
}