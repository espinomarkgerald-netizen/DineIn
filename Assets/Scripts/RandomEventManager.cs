using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class RandomEventManager : MonoBehaviour
{
    [Header("Spawning")]
    [SerializeField] private List<CleanableEvent> eventPrefabs = new();
    [SerializeField] private List<Transform> spawnPoints = new();
    [SerializeField] private float spawnEverySecondsMin = 10f;
    [SerializeField] private float spawnEverySecondsMax = 25f;
    [SerializeField] private int maxActiveEvents = 3;

    [Header("Spawn Variation (Scale/Rotation)")]
    [SerializeField] private bool randomizeYaw = true;
    [SerializeField] private Vector2 yawRange = new Vector2(0f, 360f);

    [Tooltip("Random uniform scale multiplier applied on top of prefab scale.")]
    [SerializeField] private bool randomizeUniformScale = true;
    [SerializeField] private Vector2 uniformScaleRange = new Vector2(0.85f, 1.25f);

    [Tooltip("Optional: randomize XZ stretch (good for puddles). Ignored if Randomize Uniform Scale is off.")]
    [SerializeField] private bool randomizeXZStretch = false;
    [SerializeField] private Vector2 xScaleRange = new Vector2(0.8f, 1.3f);
    [SerializeField] private Vector2 zScaleRange = new Vector2(0.8f, 1.3f);

    [Tooltip("Keeps Y scale at 1 so it doesn't get thicker/thinner (recommended for flat puddles).")]
    [SerializeField] private bool lockYScaleToOne = true;

    [Header("Click/Hold")]
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask eventLayerMask = ~0;
    [SerializeField] private float maxRayDistance = 300f;

    [Header("UI")]
    [SerializeField] private HoldToCleanUI holdUi;

    private readonly List<CleanableEvent> activeEvents = new();
    private CleanableEvent currentHoldTarget;
    private bool isHolding;
    private float nextSpawnAt;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (holdUi == null) holdUi = FindFirstObjectByType<HoldToCleanUI>(FindObjectsInactive.Include);
    }

    void Start() => ScheduleNextSpawn();

    void ScheduleNextSpawn()
    {
        nextSpawnAt = Time.time + Random.Range(spawnEverySecondsMin, spawnEverySecondsMax);
    }

    void Update()
    {
        // Spawn loop
        activeEvents.RemoveAll(e => e == null);

        if (eventPrefabs.Count > 0 && spawnPoints.Count > 0)
        {
            if (Time.time >= nextSpawnAt && activeEvents.Count < maxActiveEvents)
            {
                SpawnRandomEvent();
                ScheduleNextSpawn();
            }
        }

        // UI block: don't START a new hold if pointer over UI
        if (EventSystem.current != null && IsPointerOverUI() && !isHolding)
            return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // Start hold
        if (PressedThisFrame())
        {
            var target = RaycastEventUnderPointer();
            if (target != null)
            {
                currentHoldTarget = target;
                isHolding = true;
                holdUi?.Begin(target);
            }
        }

        // Continue hold
        if (isHolding)
        {
            if (ReleasedThisFrame() || currentHoldTarget == null)
            {
                CancelHold();
            }
            else
            {
                var stillUnder = RaycastEventUnderPointer();
                if (stillUnder != currentHoldTarget)
                {
                    CancelHold();
                }
                else
                {
                    bool holdingNow =
                        (Input.touchCount > 0)
                            ? (Input.GetTouch(0).phase == TouchPhase.Moved || Input.GetTouch(0).phase == TouchPhase.Stationary)
                            : Input.GetMouseButton(0);

                    holdUi?.TickHold(Time.deltaTime, holdingNow);
                }
            }
        }
    }

    void CancelHold()
    {
        isHolding = false;
        currentHoldTarget = null;
        holdUi?.Cancel();
    }

    CleanableEvent RaycastEventUnderPointer()
    {
        Vector2 screenPos = GetPointerScreenPosition();
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, eventLayerMask, QueryTriggerInteraction.Collide))
            return hit.collider.GetComponentInParent<CleanableEvent>();

        return null;
    }

    void SpawnRandomEvent()
    {
        var prefab = eventPrefabs[Random.Range(0, eventPrefabs.Count)];
        var point = spawnPoints[Random.Range(0, spawnPoints.Count)];

        var spawned = Instantiate(prefab, point.position, point.rotation);

        // ===== Variation =====
        ApplySpawnVariation(spawned.transform, point);

        activeEvents.Add(spawned);
    }

    void ApplySpawnVariation(Transform spawned, Transform spawnPoint)
    {
        // Rotation: random yaw (spins around Y / up axis)
        if (randomizeYaw)
        {
            float yaw = Random.Range(yawRange.x, yawRange.y);
            spawned.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * spawnPoint.rotation;
        }

        // Scale: multiply prefab scale so you keep its original proportions
        Vector3 baseScale = spawned.localScale;

        if (randomizeUniformScale)
        {
            float u = Random.Range(uniformScaleRange.x, uniformScaleRange.y);
            baseScale *= u;

            if (randomizeXZStretch)
            {
                float sx = Random.Range(xScaleRange.x, xScaleRange.y);
                float sz = Random.Range(zScaleRange.x, zScaleRange.y);
                baseScale = new Vector3(baseScale.x * sx, baseScale.y, baseScale.z * sz);
            }

            if (lockYScaleToOne)
                baseScale = new Vector3(baseScale.x, 1f, baseScale.z);

            spawned.localScale = baseScale;
        }
    }

    // Input helpers
    bool PressedThisFrame()
    {
        if (Input.touchCount > 0) return Input.GetTouch(0).phase == TouchPhase.Began;
        return Input.GetMouseButtonDown(0);
    }

    bool ReleasedThisFrame()
    {
        if (Input.touchCount > 0)
        {
            var p = Input.GetTouch(0).phase;
            return p == TouchPhase.Ended || p == TouchPhase.Canceled;
        }
        return Input.GetMouseButtonUp(0);
    }

    Vector2 GetPointerScreenPosition()
    {
        if (Input.touchCount > 0) return Input.GetTouch(0).position;
        return Input.mousePosition;
    }

    bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        if (Input.touchCount > 0) return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return EventSystem.current.IsPointerOverGameObject();
    }
}
