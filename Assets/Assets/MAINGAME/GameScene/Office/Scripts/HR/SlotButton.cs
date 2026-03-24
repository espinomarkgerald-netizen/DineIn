using UnityEngine;

public class SlotButton : MonoBehaviour
{
    public RoleSlot slot;
    public HRManager hrManager;

    public void AssignHere()
    {
        hrManager.AssignEmployee(slot);
    }
}