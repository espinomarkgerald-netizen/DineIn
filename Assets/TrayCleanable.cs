using UnityEngine;

public class TrayCleanable : MonoBehaviour
{
    [SerializeField] private float holdSeconds = 2f;

    private Booth booth;
    private CleanableEvent cleanable;

    public bool IsArmed { get; private set; }

    public void ArmForCleaning(Booth b)
    {
        if (IsArmed) return;

        booth = b;
        IsArmed = true;

        cleanable = GetComponent<CleanableEvent>();
        if (cleanable == null)
            cleanable = gameObject.AddComponent<CleanableEvent>();

        cleanable.holdToCleanSeconds = holdSeconds;
        cleanable.OnCleaned += HandleCleaned;
    }

    private void HandleCleaned(CleanableEvent e)
    {
        if (booth != null)
            booth.OnTableCleaned();

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (cleanable != null)
            cleanable.OnCleaned -= HandleCleaned;
    }
}