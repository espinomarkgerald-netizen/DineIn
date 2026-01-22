using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private LayerMask groundLayer;
    
    // How many pixels can the finger move and still count as a "Tap"?
    [SerializeField] private float tapThreshold = 10f; 

    private NavMeshAgent agent;
    private Camera activeCam;
    private Vector2 touchStartPos;

    // Automatically find camera if not set
    public void SetCamera(Camera cam) { activeCam = cam; }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
    }

    void Start()
    {
        // Fallback: Find the main camera if the Lobby/Network didn't assign one
        if (activeCam == null) activeCam = Camera.main;
    }

    void Update()
    {
        if (activeCam == null) return;

        // 1. Handle Input (Mouse & Touch)
        if (Input.GetMouseButtonDown(0))
        {
            // Store where we touched
            touchStartPos = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
        {
            // 2. Check UI Blocking (Mobile & PC compatible)
            if (IsPointerOverUI()) return;

            // 3. Check if Camera is busy (Panning/Zooming)
            // If the CameraController says we are panning, DO NOT move the player.
            if (CameraController.IsPanning) return;

            // 4. Check for Drag vs Tap
            // If the finger moved too far while down, it was a drag, not a tap.
            float dist = Vector2.Distance(touchStartPos, Input.mousePosition);
            if (dist > tapThreshold) return;

            // 5. Execute Movement
            MovePlayer();
        }
    }

    void MovePlayer()
    {
        Ray ray = activeCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundLayer))
        {
            agent.SetDestination(hit.point);
        }
    }

    // Helper to fix UI clicks passing through on Mobile
    private bool IsPointerOverUI()
    {
        // PC Check
        if (EventSystem.current.IsPointerOverGameObject()) return true;

        // Mobile Check
        if (Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)) return true;

        return false;
    }
}