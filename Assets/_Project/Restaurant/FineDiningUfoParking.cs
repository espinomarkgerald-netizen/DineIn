using UnityEngine;
using UnityEngine.AI;

/// <summary>Two authored parking bays feed the existing customer admission flow; never creates extra customers.</summary>
public sealed class FineDiningUfoParking : MonoBehaviour
{
    [System.Serializable]
    public sealed class Bay
    {
        public Transform craft, parkedPose, flightPose, passengerPoint;
        public NavMeshObstacle footprint;
        [System.NonSerialized] public CustomerGroup party;
        [System.NonSerialized] public int phase;
        [System.NonSerialized] public float elapsed;
    }
    public Bay[] bays;
    public GameDayManager clock;
    [Range(0, 1)] public float arrivalChance = .65f;
    [Min(1)] public float flightSeconds = 5f;
    [Min(1)] public float returnInterval = 18f;
    private readonly NavMeshPath route = new NavMeshPath();

    private void OnEnable()
    {
        if (bays == null) return;
        foreach (var bay in bays)
        {
            if (!Valid(bay)) continue;
            bay.party = null;
            bay.phase = 0;
            bay.elapsed = 0;
            bay.craft.gameObject.SetActive(false);
            if (bay.footprint != null) bay.footprint.enabled = false;
        }
    }
    private static bool Valid(Bay bay) => bay != null && bay.craft != null && bay.parkedPose != null && bay.flightPose != null && bay.passengerPoint != null;
    private void Update()
    {
        if (bays == null || MultiplayerDayBridge.IsActive) return;
        bool open = clock != null && clock.ServiceActive;
        foreach (var bay in bays)
        {
            if (!Valid(bay)) continue;
            bay.elapsed += Time.deltaTime;
            switch (bay.phase)
            {
                case 0: // Waiting offscreen until service opens.
                    if (open && bay.elapsed >= returnInterval) BeginFlight(bay, 1);
                    break;
                case 1: // Landing before customers can step out.
                    Fly(bay, bay.flightPose, bay.parkedPose);
                    if (bay.elapsed >= flightSeconds) { bay.phase = 2; bay.elapsed = 0; if (bay.footprint != null) bay.footprint.enabled = true; }
                    break;
                case 2:
                    if (!open) BeginFlight(bay, 4);
                    break;
                case 3: // The same party returns to its passenger point before being removed by CustomerGroup.
                    if (bay.party == null) BeginFlight(bay, 4);
                    break;
                case 4:
                    Fly(bay, bay.parkedPose, bay.flightPose);
                    if (bay.elapsed >= flightSeconds) { bay.craft.gameObject.SetActive(false); bay.phase = 0; bay.elapsed = 0; }
                    break;
            }
        }
    }
    private void BeginFlight(Bay bay, int phase)
    {
        bay.phase = phase;
        bay.elapsed = 0;
        bay.craft.gameObject.SetActive(true);
        if (bay.footprint != null) bay.footprint.enabled = false;
    }
    private void Fly(Bay bay, Transform from, Transform to)
    {
        float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(bay.elapsed / flightSeconds));
        bay.craft.SetPositionAndRotation(Vector3.Lerp(from.position, to.position, t), Quaternion.Slerp(from.rotation, to.rotation, t));
    }
    public int ChooseArrival(Vector3 fallback, out Vector3 origin)
    {
        origin = fallback;
        if (!isActiveAndEnabled || MultiplayerDayBridge.IsActive || bays == null || Random.value > arrivalChance) return -1;
        for (int i = 0; i < bays.Length; i++)
        {
            var bay = bays[i];
            if (!Valid(bay) || bay.phase != 2 || !NavMesh.SamplePosition(bay.passengerPoint.position, out var hit, 1f, NavMesh.AllAreas)) continue;
            if (!NavMesh.CalculatePath(hit.position, fallback, NavMesh.AllAreas, route) || route.status != NavMeshPathStatus.PathComplete) continue;
            origin = hit.position;
            return i;
        }
        return -1;
    }
    public void CommitArrival(int index, CustomerGroup group)
    {
        if (index < 0 || bays == null || index >= bays.Length || group == null) return;
        var bay = bays[index];
        bay.party = group;
        bay.phase = 3;
        bay.elapsed = 0;
        group.exitPoint = bay.passengerPoint;
    }
}
