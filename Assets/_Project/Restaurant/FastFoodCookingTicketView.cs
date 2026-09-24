using UnityEngine;
using TMPro;
public sealed class FastFoodCookingTicketView : MonoBehaviour
{
    public TMP_Text title, timer;
    public Transform products;
    public FastFoodCookingDragHandle productTemplate;
    public string titleFormat = "#{0} · {1}", timerFormat = "{0} · {1}", quantityFormat = "{0}/{1}";
    public string playerText = "YOUR ORDER", staffText = "STAFF", waitingText = "Waiting / cooking", preparingText = "Staff preparing";
    public Color placedColor = new Color(.45f,1,.65f), pendingColor = Color.white;
}
