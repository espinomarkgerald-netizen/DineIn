using UnityEngine;

public class TableMessEvent : MonoBehaviour
{
    [SerializeField] private Booth booth;

    public void Init(Booth b)
    {
        booth = b;

        var cleanable = GetComponent<CleanableEvent>();
        if (cleanable != null)
            cleanable.OnCleaned += HandleCleaned;
    }

    private void OnDestroy()
    {
        var cleanable = GetComponent<CleanableEvent>();
        if (cleanable != null)
            cleanable.OnCleaned -= HandleCleaned;
    }

    private void HandleCleaned(CleanableEvent e)
    {
        if (booth != null)
            booth.OnTableCleaned();
    }
}