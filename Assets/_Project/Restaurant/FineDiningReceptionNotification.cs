using UnityEngine;
using TMPro;

/// <summary>Scene-authored badge, coalescing unseen requests into one alert.</summary>
public sealed class FineDiningReceptionNotification : MonoBehaviour
{
    public static FineDiningReceptionNotification Active { get; private set; }
    public FineDiningReservations reservations;
    public GameObject badge;
    public TMP_Text count;
    public Transform pulseTarget;
    public AudioSource sound;
    [Range(.1f,1f)] public float pulseSeconds=.4f;
    [SerializeField, Range(0f, 1f)] private float bellGain = .64f;
    public int UnreadCount { get; private set; }
    private AudioClip bell;
    private ManagementComputerStation station;
    private float pulseUntil;
    private Vector3 scale;
    private void Awake()
    {
        if (pulseTarget != null) scale = pulseTarget.localScale;
        bell = Resources.Load<AudioClip>("Audio/ReservationBell");
    }

    private void OnEnable()
    {
        Active = this;
        if (reservations != null)
        {
            reservations.Changed += Refresh;
            reservations.NewRequests += NotifyNewRequests;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (Active == this) Active = null;
        if (reservations != null)
        {
            reservations.Changed -= Refresh;
            reservations.NewRequests -= NotifyNewRequests;
        }
        if (pulseTarget != null) pulseTarget.localScale = scale;
    }

    public bool TryGetReceptionBounds(out Bounds bounds)
    {
        bounds = default;
        if (!isActiveAndEnabled || reservations == null || reservations != FineDiningReservations.Instance ||
            !reservations.isActiveAndEnabled || UnreadCount <= 0) return false;
        if (station == null)
        {
            foreach (var candidate in FindObjectsByType<ManagementComputerStation>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == gameObject.scene && candidate.Purpose == RestaurantComputerPurpose.Reservations)
                {
                    station = candidate;
                    break;
                }
        }
        if (station == null || !station.isActiveAndEnabled) return false;
        var collider = station.GetComponent<Collider>();
        bounds = collider != null && collider.enabled
            ? collider.bounds : new Bounds(station.transform.position, Vector3.one);
        return true;
    }
    private void Refresh()
    {
        UnreadCount = reservations != null ? reservations.UnreadRequests : 0;
        if (badge != null) badge.SetActive(UnreadCount > 0);
        if (count != null) count.text = UnreadCount.ToString();
    }

    private void NotifyNewRequests()
    {
        Refresh();
        if (UnreadCount == 0 || GameplaySfx.PresentationSuspended) return;
        if (!LevelOneUIAccessibility.ReducedMotion)
            pulseUntil = Time.unscaledTime + pulseSeconds;
        GameplaySfx.PlayUiClip(bell != null ? bell : sound != null ? sound.clip : null, bellGain);
    }
    private void Update()
    {
        if(pulseTarget!=null)pulseTarget.localScale=scale*(Time.unscaledTime<pulseUntil?1f+.14f*Mathf.Sin((pulseUntil-Time.unscaledTime)/pulseSeconds*Mathf.PI):1f);
        if(badge!=null && badge.activeSelf && Camera.main!=null)badge.transform.rotation=Camera.main.transform.rotation;
    }
}
