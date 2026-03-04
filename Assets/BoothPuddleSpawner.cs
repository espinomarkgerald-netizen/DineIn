using UnityEngine;

public class BoothPuddleSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private CleanableEvent puddlePrefab;

    [Header("Spawn Points (placed around THIS booth/table)")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Spawn Timing")]
    [Range(0f, 1f)]
    [SerializeField] private float spawnChance = 1f;

    [Tooltip("Optional delay AFTER they are seated before spawning.")]
    [SerializeField] private float spawnDelayMin = 0f;

    [Tooltip("Optional delay AFTER they are seated before spawning.")]
    [SerializeField] private float spawnDelayMax = 0f;

    [Header("Random Look")]
    [Tooltip("Random Y rotation (yaw) on the floor.")]
    [SerializeField] private Vector2 randomYawRange = new Vector2(0f, 360f);

    [Tooltip("Uniform scale range.")]
    [SerializeField] private Vector2 uniformScaleRange = new Vector2(0.85f, 1.25f);

    [Tooltip("Extra non-uniform variation on X/Z (0 = none).")]
    [SerializeField] private float nonUniformJitter = 0.10f;

    [Header("Rules")]
    [Tooltip("Only allow one puddle alive for this booth at a time.")]
    [SerializeField] private bool oneAtATime = true;

    [Tooltip("If true, despawn puddle when booth becomes empty.")]
    [SerializeField] private bool clearWhenNoOneSitting = false;

    [Tooltip("How close counts as 'occupied' near a spawn point.")]
    [SerializeField] private float occupiedCheckRadius = 0.25f;

    private Booth booth;
    private CleanableEvent activePuddle;

    private CustomerGroup lastSpawnedForGroup;
    private bool spawnQueued;
    private float spawnAtTime;

    void Awake()
    {
        booth = GetComponent<Booth>();
    }

    void Update()
    {
        var group = GetSeatedGroupForThisBooth();

        if (group == null)
        {
            lastSpawnedForGroup = null;

            if (clearWhenNoOneSitting)
                ClearPuddle();

            spawnQueued = false;
            return;
        }

        // New seated group, spawn once
        if (group != lastSpawnedForGroup)
        {
            lastSpawnedForGroup = group;

            if (Random.value > spawnChance) return;

            if (oneAtATime && activePuddle != null) return;

            float delay = Random.Range(
                Mathf.Min(spawnDelayMin, spawnDelayMax),
                Mathf.Max(spawnDelayMin, spawnDelayMax)
            );

            if (delay <= 0f)
                SpawnNow();
            else
            {
                spawnQueued = true;
                spawnAtTime = Time.time + delay;
            }
        }

        // delayed spawn
        if (spawnQueued && Time.time >= spawnAtTime)
        {
            spawnQueued = false;

            if (oneAtATime && activePuddle != null) return;

            // still seated?
            var stillSeated = GetSeatedGroupForThisBooth();
            if (stillSeated != null && stillSeated == lastSpawnedForGroup)
                SpawnNow();
        }
    }

    private CustomerGroup GetSeatedGroupForThisBooth()
    {
        // No need to modify CustomerGroup.
        // We scan all groups and check assignedBooth/state.
        var groups = FindObjectsOfType<CustomerGroup>();

        for (int i = 0; i < groups.Length; i++)
        {
            var g = groups[i];
            if (g == null) continue;

            if (g.assignedBooth != booth) continue;

            // "sitting" states: adjust if you want (Seated / WaitingToOrder / ReadyToOrder / OrderTaken / Eating / NeedsBill)
            if (g.state == CustomerGroup.GroupState.Seated ||
                g.state == CustomerGroup.GroupState.WaitingToOrder ||
                g.state == CustomerGroup.GroupState.ReadyToOrder ||
                g.state == CustomerGroup.GroupState.OrderTaken ||
                g.state == CustomerGroup.GroupState.Eating ||
                g.state == CustomerGroup.GroupState.NeedsBill)
            {
                return g;
            }
        }

        return null;
    }

    private void SpawnNow()
    {
        if (puddlePrefab == null) return;
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        Transform sp = GetRandomFreeSpawnPoint();
        if (sp == null) sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        if (sp == null) return;

        float yaw = Random.Range(randomYawRange.x, randomYawRange.y);
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

        activePuddle = Instantiate(puddlePrefab, sp.position, rot);

        // random scale
        float u = Random.Range(uniformScaleRange.x, uniformScaleRange.y);
        float jx = Random.Range(-nonUniformJitter, nonUniformJitter);
        float jz = Random.Range(-nonUniformJitter, nonUniformJitter);

        Vector3 s = new Vector3(
            u * (1f + jx),
            u,
            u * (1f + jz)
        );

        activePuddle.transform.localScale = s;
    }

    private Transform GetRandomFreeSpawnPoint()
    {
        // collect free points
        int count = 0;
        Transform[] free = new Transform[spawnPoints.Length];

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            var p = spawnPoints[i];
            if (p == null) continue;

            if (!IsOccupied(p.position))
            {
                free[count] = p;
                count++;
            }
        }

        if (count == 0) return null;
        return free[Random.Range(0, count)];
    }

    private bool IsOccupied(Vector3 pos)
    {
        var hits = Physics.OverlapSphere(pos, occupiedCheckRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null) continue;

            if (h.GetComponentInParent<CleanableEvent>() != null)
                return true;
        }
        return false;
    }

    public void ClearPuddle()
    {
        if (activePuddle != null)
        {
            Destroy(activePuddle.gameObject);
            activePuddle = null;
        }
    }
}