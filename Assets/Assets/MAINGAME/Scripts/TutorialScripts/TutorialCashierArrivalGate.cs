using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cashier Day tutorial gate: ensures the waiter visibly arrives at the cashier booth
/// before the POS register opens.
///
/// Flow:
///   1. When a handoff is ready (money in waiter's hands), highlight the cashier booth
///      destination with a visible arrow/glow.
///   2. Poll until the waiter is within <see cref="arrivalDistance"/> of the booth.
///   3. Once arrived, wait <see cref="posOpenDelay"/> seconds, then open the POS.
///   4. Clear the booth highlight.
///
/// This prevents the POS from popping up randomly — the player always sees:
///   waiter walks to booth → arrives → POS opens.
///
/// Wire up:
///   - Assign <see cref="cashierBoothTransform"/> to the cashier booth world position.
///   - Assign <see cref="boothHighlight"/> to the arrow/glow over the booth.
///   - Call <see cref="NotifyHandoffReady"/> when money reaches the handoff trigger.
///   - Assign <see cref="posOpenTarget"/> so the gate can call OpenPOS() on it.
/// </summary>
public class TutorialCashierArrivalGate : MonoBehaviour
{
    [Header("Booth")]
    [Tooltip("World-space transform of the cashier booth. The waiter must reach this position.")]
    [SerializeField] private Transform cashierBoothTransform;

    [Tooltip("Distance in world units at which the waiter is considered to have arrived.")]
    [SerializeField] private float arrivalDistance = 2.2f;

    [Header("Booth Highlight")]
    [Tooltip("Arrow or glow GameObject shown above the cashier booth as the destination indicator.")]
    [SerializeField] private GameObject boothHighlight;

    [Tooltip("Optional label shown while the waiter needs to walk to the booth.")]
    [SerializeField] private GameObject boothLabel;

    [Header("POS Timing")]
    [Tooltip("Seconds to wait after arrival before the POS register opens. " +
             "A short pause (0.5–1 s) feels like a natural cause-and-effect beat.")]
    [SerializeField] private float posOpenDelay = 0.6f;

    [Header("POS Target")]
    [Tooltip("The MonoBehaviour that exposes an OpenPOS() or OpenForPayment() method. " +
             "Leave null if POS opening is handled elsewhere via NotifyArrived().")]
    [SerializeField] private MonoBehaviour posOpenTarget;

    [Tooltip("Method name to call on posOpenTarget when the waiter arrives. Default: 'OpenPOS'.")]
    [SerializeField] private string posOpenMethodName = "OpenPOS";

    [Header("Tutorial Day Lock")]
    [SerializeField] private bool onlyOnDay3Cashier = true;

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    private bool gateActive;
    private bool arrived;
    private Coroutine waitRoutine;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called when the waiter picks up money and is heading to the booth.
    /// Starts the arrival watch and shows the booth highlight.
    /// </summary>
    public void NotifyHandoffReady()
    {
        if (onlyOnDay3Cashier && !IsDay3CashierActive())
            return;

        if (gateActive)
            return;

        gateActive = true;
        arrived = false;

        ShowBoothHighlight(true);

        if (waitRoutine != null)
            StopCoroutine(waitRoutine);

        waitRoutine = StartCoroutine(WatchForArrivalRoutine());
    }

    /// <summary>
    /// Cancels any active watch and resets the gate to its idle state.
    /// Call when the cashier day ends or the tutorial resets.
    /// </summary>
    public void Reset()
    {
        gateActive = false;
        arrived = false;

        if (waitRoutine != null)
        {
            StopCoroutine(waitRoutine);
            waitRoutine = null;
        }

        ShowBoothHighlight(false);
    }

    // -------------------------------------------------------------------------
    // Arrival watch
    // -------------------------------------------------------------------------

    private IEnumerator WatchForArrivalRoutine()
    {
        // Find the waiter player movement to track its world position.
        PlayerMovement waiterMovement = FindWaiterMovement();

        while (!arrived)
        {
            // Re-resolve the waiter if needed (scene could still be loading).
            if (waiterMovement == null)
                waiterMovement = FindWaiterMovement();

            if (waiterMovement != null && cashierBoothTransform != null)
            {
                float dist = Vector3.Distance(waiterMovement.transform.position, cashierBoothTransform.position);

                if (dist <= arrivalDistance)
                    arrived = true;
            }

            yield return null;
        }

        // Waiter has arrived — hide highlight and open POS after a beat.
        ShowBoothHighlight(false);

        yield return new WaitForSeconds(posOpenDelay);

        OpenPOS();

        gateActive = false;
        waitRoutine = null;
    }

    private PlayerMovement FindWaiterMovement()
    {
        if (RoleManager.Instance == null)
            return null;

        // Access the waiter field via reflection to avoid tight coupling.
        var waiterField = typeof(RoleManager).GetField(
            "waiter",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (waiterField == null)
            return null;

        var waiterObj = waiterField.GetValue(RoleManager.Instance) as GameObject;
        return waiterObj != null ? waiterObj.GetComponent<PlayerMovement>() : null;
    }

    // -------------------------------------------------------------------------
    // POS open
    // -------------------------------------------------------------------------

    private void OpenPOS()
    {
        if (posOpenTarget == null)
            return;

        posOpenTarget.SendMessage(posOpenMethodName, SendMessageOptions.DontRequireReceiver);
    }

    // -------------------------------------------------------------------------
    // Visual helpers
    // -------------------------------------------------------------------------

    private void ShowBoothHighlight(bool show)
    {
        if (boothHighlight != null)
            boothHighlight.SetActive(show);

        if (boothLabel != null)
            boothLabel.SetActive(show);
    }

    // -------------------------------------------------------------------------
    // Tutorial day guard
    // -------------------------------------------------------------------------

    private bool IsDay3CashierActive()
    {
        return TutorialManager.Instance != null &&
               TutorialManager.Instance.TutorialStarted &&
               TutorialManager.Instance.CurrentDay == TutorialManager.TutorialDay.Day3Cashier;
    }

    private void OnDisable()
    {
        Reset();
    }
}
