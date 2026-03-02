using UnityEngine;

public class TrayHoldToClean : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private float holdSeconds = 2f;
    [SerializeField] private string tableCleanUiTag = "TableCleanUi";

    private Booth booth;
    private CustomerGroup group;
    private bool armed;

    private CleanableEvent cleanable;
    private HoldToCleanUI ui;
    private bool isHolding;

    private void Awake()
    {
        cleanable = GetComponent<CleanableEvent>();
        if (cleanable == null)
            cleanable = gameObject.AddComponent<CleanableEvent>();

        cleanable.holdToCleanSeconds = holdSeconds;
        cleanable.OnCleaned += OnCleaned;

        enabled = false;
    }

    public void Arm(Booth b, CustomerGroup g)
    {
        booth = b;
        group = g;
        armed = true;

        ui = ResolveTableCleanUI();

        enabled = true;
    }

    private HoldToCleanUI ResolveTableCleanUI()
    {
        var obj = GameObject.FindGameObjectWithTag(tableCleanUiTag);
        if (obj == null) return null;

        var direct = obj.GetComponent<HoldToCleanUI>();
        if (direct != null) return direct;

        return obj.GetComponentInChildren<HoldToCleanUI>(true);
    }

    private bool CanCleanNow()
    {
        if (!armed) return false;
        if (ui == null) return false;

        if (WaiterHands.Instance != null && WaiterHands.Instance.HasTray)
            return false;

        if (booth != null && booth.CurrentGroup != null)
            return false;

        if (group == null) return true;

        return group.state == CustomerGroup.GroupState.Leaving
            || group.state == CustomerGroup.GroupState.AngryLeft;
    }

    private void OnMouseDown()
    {
        if (!CanCleanNow()) return;

        isHolding = true;
        ui.Begin(cleanable);
    }

    private void OnMouseUp()
    {
        if (ui == null) return;

        isHolding = false;
        ui.Cancel();
    }

    private void Update()
    {
        if (!isHolding) return;

        if (!CanCleanNow())
        {
            isHolding = false;
            if (ui != null) ui.Cancel();
            return;
        }

        ui.TickHold(Time.deltaTime, true);
    }

    private void OnCleaned(CleanableEvent e)
    {
        if (booth != null)
            booth.OnTableCleaned();

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (cleanable != null)
            cleanable.OnCleaned -= OnCleaned;
    }
}