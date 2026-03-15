using UnityEngine;

public class SlotButton : MonoBehaviour
{
    public RoleSlot slot;
    public HRManager hrManager;

    public void AssignHere()
    {
        if(slot == null) { Debug.Log("Slot is null!"); return; }
        if(hrManager == null) { Debug.Log("HRManager is null!"); return; }
        hrManager.AssignEmployee(slot);
    }
}