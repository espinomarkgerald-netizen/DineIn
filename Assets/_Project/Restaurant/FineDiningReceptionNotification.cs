using UnityEngine;
using TMPro;

/// <summary>Scene-authored badge, coalescing unseen requests into one alert.</summary>
public sealed class FineDiningReceptionNotification : MonoBehaviour
{
    public FineDiningReservations reservations;
    public GameObject badge;
    public TMP_Text count;
    public Transform pulseTarget;
    public AudioSource sound;
    [Range(.1f,1f)] public float pulseSeconds=.4f;
    private int previous;
    private float pulseUntil;
    private Vector3 scale;
    private void Awake(){if(pulseTarget!=null)scale=pulseTarget.localScale;}
    private void OnEnable(){if(reservations!=null)reservations.Changed+=Refresh;Refresh();}
    private void OnDisable(){if(reservations!=null)reservations.Changed-=Refresh;if(pulseTarget!=null)pulseTarget.localScale=scale;}
    private void Refresh()
    {
        int unread=reservations!=null?reservations.UnreadRequests:0;
        if(badge!=null)badge.SetActive(unread>0);
        if(count!=null)count.text=unread.ToString();
        if(unread>0 && previous==0){pulseUntil=Time.unscaledTime+pulseSeconds;if(sound!=null && sound.clip!=null)sound.Play();}
        previous=unread;
    }
    private void Update()
    {
        if(pulseTarget!=null)pulseTarget.localScale=scale*(Time.unscaledTime<pulseUntil?1f+.14f*Mathf.Sin((pulseUntil-Time.unscaledTime)/pulseSeconds*Mathf.PI):1f);
        if(badge!=null && badge.activeSelf && Camera.main!=null)badge.transform.rotation=Camera.main.transform.rotation;
    }
}
