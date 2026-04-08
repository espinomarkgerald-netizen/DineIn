using UnityEngine;

/// <summary>
/// Attach this to any GameObject (e.g. Panel, CashierRegisterUI, CanvasMainHUD)
/// to catch and log exactly who calls SetActive(false) on it.
/// Remove from the scene once the bug is identified.
/// </summary>
public class UIDeactivationWatcher : MonoBehaviour
{
#if UNITY_EDITOR
    [Tooltip("Label shown in the log so you know which object fired.")]
    [SerializeField] private string watchLabel = "Watched Object";

    private void OnDisable()
    {
        Debug.LogError(
            $"[UIDeactivationWatcher] '{watchLabel}' ({gameObject.name}) was DISABLED.\n" +
            $"{new System.Diagnostics.StackTrace(true)}",
            this);
    }

    private void OnEnable()
    {
        Debug.Log($"[UIDeactivationWatcher] '{watchLabel}' ({gameObject.name}) was ENABLED.", this);
    }
#endif
}
