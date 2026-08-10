using UnityEngine;

public class ApprovalDebugWatcher : MonoBehaviour
{
    private void Start()
    {
        if (AlienApprovalManager.Instance != null)
            AlienApprovalManager.Instance.OnApprovalChanged += OnChanged;
        else
            Debug.LogWarning("[ApprovalDebugWatcher] Manager not ready yet.");
    }

    private void OnDisable()
    {
        if (AlienApprovalManager.Instance != null)
            AlienApprovalManager.Instance.OnApprovalChanged -= OnChanged;
    }

    private void OnChanged(int approval) =>
        Debug.Log($"[Approval] Changed → {approval}/100");
}
