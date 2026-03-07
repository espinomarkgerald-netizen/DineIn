using UnityEngine;

public class StaffRole : MonoBehaviour
{
    public enum Role
    {
        Host,
        Waiter,
        Cashier,
        Busser
    }

    public Role role;
}