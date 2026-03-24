using UnityEngine;

public class TutorialGroupWatcher : MonoBehaviour
{
    private CustomerGroup group;

    private bool greetedReported;
    private bool seatedReported;
    private bool orderTakenReported;
    private bool foodServedReported;

    public void Init(CustomerGroup targetGroup)
    {
        group = targetGroup;
    }

    private void Update()
    {
        if (group == null || TutorialManager.Instance == null)
            return;

        if (!greetedReported && group.hasBeenGreeted)
        {
            greetedReported = true;
            TutorialManager.Instance.RegisterCustomerGreeted(group);
        }

        if (!seatedReported &&
            (group.state == CustomerGroup.GroupState.Seated ||
             group.state == CustomerGroup.GroupState.WaitingToOrder ||
             group.state == CustomerGroup.GroupState.ReadyToOrder ||
             group.state == CustomerGroup.GroupState.OrderTaken ||
             group.state == CustomerGroup.GroupState.Eating ||
             group.state == CustomerGroup.GroupState.NeedsBill ||
             group.state == CustomerGroup.GroupState.Leaving ||
             group.state == CustomerGroup.GroupState.AngryLeft ||
             group.state == CustomerGroup.GroupState.UnhappyLeft))
        {
            seatedReported = true;
            TutorialManager.Instance.RegisterTableAssigned(group);
        }

        if (!orderTakenReported && group.state == CustomerGroup.GroupState.OrderTaken)
        {
            orderTakenReported = true;
            TutorialManager.Instance.RegisterOrderTaken(group);
        }

        // IMPORTANT:
        // ServeFood phase should finish only when the customer is DONE eating
        if (!foodServedReported &&
            (group.state == CustomerGroup.GroupState.NeedsBill ||
             group.state == CustomerGroup.GroupState.Leaving ||
             group.state == CustomerGroup.GroupState.AngryLeft ||
             group.state == CustomerGroup.GroupState.UnhappyLeft))
        {
            foodServedReported = true;
            TutorialManager.Instance.RegisterFoodServed(group);
        }
    }
}