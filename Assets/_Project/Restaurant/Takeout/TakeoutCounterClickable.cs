using UnityEngine;

public class TakeoutCounterClickable : MonoBehaviour
{
    [SerializeField] private TakeoutQueueManager queueManager;

    private void OnMouseDown()
    {
        if (MultiplayerServiceActions.IsActive)
        {
            MultiplayerServiceActions.ApproachTakeout(TakeoutFlowManager.Instance?.ActiveGroup);
            return;
        }
        if (queueManager == null)
            return;

        CustomerGroup front = queueManager.CurrentFront;
        if (front == null)
            return;

        if (front.CurrentTakeoutQueueState != CustomerGroup.TakeoutQueueState.AtOrderPoint)
            return;

        queueManager.ReleaseFrontFromOrderPoint();
    }
}
