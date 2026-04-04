using UnityEngine;

public class TakeoutBagMarker : MonoBehaviour
{
    [SerializeField] private CustomerGroup targetGroup;
    [SerializeField] private int orderNumber = -1;

    public CustomerGroup TargetGroup => targetGroup;
    public int OrderNumber => orderNumber;

    public void Init(CustomerGroup group)
    {
        targetGroup = group;
        orderNumber = group != null ? group.currentOrderNumber : -1;
    }

    public bool Matches(CustomerGroup group)
    {
        return targetGroup == group;
    }
}