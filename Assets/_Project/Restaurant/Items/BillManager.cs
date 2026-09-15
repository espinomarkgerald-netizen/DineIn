using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BillManager : MonoBehaviour
{
    public static BillManager Instance { get; private set; }

    [Header("Bill Printing")]
    [SerializeField] private GameObject billPaperPrefab;
    [SerializeField] private List<Transform> billSpawnPoints = new List<Transform>();
    [SerializeField] private float printSeconds = 3f;

    [Header("Optional Root (Organization)")]
    [SerializeField] private Transform billsRoot;

    private readonly Queue<CustomerGroup> queue = new Queue<CustomerGroup>();
    private readonly HashSet<CustomerGroup> queued = new HashSet<CustomerGroup>();
    private bool printing;
    private readonly Dictionary<CustomerGroup, BillPaper> papers = new();
    private readonly Dictionary<BillPaper, Transform> slots = new();
    public void Register(BillPaper paper)
    {
        if (paper == null || paper.TargetGroup == null) return;
        var group = paper.TargetGroup;
        if (papers.TryGetValue(group, out var existing) && existing != null && existing != paper && existing.Matches(group))
        { paper.gameObject.SetActive(false); Destroy(paper.gameObject); return; }
        if (existing != null && existing != paper)
        {
            existing.GetComponentInParent<WaiterHands>(true)?.DetachBillPaper(existing);
            slots.Remove(existing);
            existing.gameObject.SetActive(false); Destroy(existing.gameObject);
        }
        papers[group] = paper;
    }
    public void Unregister(BillPaper paper)
    {
        if (paper == null) return;
        slots.Remove(paper);
        if (paper.TargetGroup != null && papers.TryGetValue(paper.TargetGroup, out var existing) && existing == paper)
            papers.Remove(paper.TargetGroup);
    }
    public void ReturnUndeliveredBill(CustomerGroup group)
    {
        var paper = FindBillForGroup(group);
        if (paper == null) return;
        var holder = paper.GetComponentInParent<WaiterHands>(true);
        if (group == null || group.HasReceivedBill || group.MultiplayerPaymentComplete)
        { holder?.DetachBillPaper(paper); Unregister(paper); paper.gameObject.SetActive(false); Destroy(paper.gameObject); return; }
        if (!slots.TryGetValue(paper, out var slot) || slot == null) slot = GetFreeSpawnPoint();
        if (slot == null) return; // Its original reserved slot is retained while carried.
        holder?.DetachBillPaper(paper);
        slots[paper] = slot;
        paper.transform.SetParent(billsRoot != null ? billsRoot : slot, true);
        paper.transform.SetPositionAndRotation(slot.position, slot.rotation);
        paper.PresentHolder(null);
    }
    public void ResetMultiplayerDay()
    {
        if (!MultiplayerDayBridge.IsActive) return;
        StopAllCoroutines(); queue.Clear(); queued.Clear(); printing = false;
        foreach (var paper in new List<BillPaper>(papers.Values)) if (paper != null)
        { paper.GetComponentInParent<WaiterHands>(true)?.DetachBillPaper(paper); paper.gameObject.SetActive(false); Destroy(paper.gameObject); }
        papers.Clear(); slots.Clear();
    }
    public bool IsPrintingFor(CustomerGroup group) => group != null && queued.Contains(group);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void RequestBill(CustomerGroup group)
    {
        if (MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer &&
            !MultiplayerSessionManager.Instance.IsAuthority) return;
        if (group == null) return;
        if (billPaperPrefab == null) return;
        if (billSpawnPoints == null || billSpawnPoints.Count == 0) return;

        if (HasExistingBillForGroup(group)) return;
        if (queued.Contains(group)) return;

        queued.Add(group);
        queue.Enqueue(group);

        if (!printing)
            StartCoroutine(PrintLoop());
    }

    private IEnumerator PrintLoop()
    {
        printing = true;

        while (queue.Count > 0)
        {
            var group = queue.Dequeue();
            if (!MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer) queued.Remove(group);

            if (group == null)
                continue;

            if (HasExistingBillForGroup(group))
            {
                queued.Remove(group);
                continue;
            }

            if (!MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer)
                ProcessingBillIndicatorUI.Instance?.Show();

            yield return new WaitForSeconds(printSeconds);
            queued.Remove(group);
            if (group == null) continue;
            if (MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer)
            {
                var session = MultiplayerSessionManager.Instance;
                var customer = group.GetComponentInParent<MultiplayerCustomerSpawn>();
                if (!session.IsAuthority || customer == null || group.HasReceivedBill
                    || group.state != CustomerGroup.GroupState.NeedsBill
                    || (session.GetComponent<MultiplayerTaskClaims>().GetOwner($"Customer:{customer.photonView.ViewID}:Bill") == 0
                        && !RestaurantTaskClaim.IsClaimedByBot(group)))
                    continue;
            }

            Transform spawn = GetFreeSpawnPoint();
            while (spawn == null && MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer && group != null
                && group.state == CustomerGroup.GroupState.NeedsBill && !group.HasReceivedBill)
            { queued.Add(group); yield return null; spawn = GetFreeSpawnPoint(); }
            queued.Remove(group);
            if (MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer && (group == null || group.HasReceivedBill
                || group.state != CustomerGroup.GroupState.NeedsBill)) continue;
            if (spawn == null && !MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer) spawn = billSpawnPoints[0];

            if (spawn != null)
            {
                Transform parent = billsRoot != null ? billsRoot : spawn;

                var go = Instantiate(billPaperPrefab, spawn.position, spawn.rotation, parent);

                var bill = go.GetComponentInChildren<BillPaper>(true);
                if (bill != null)
                {
                    bill.Init(group);
                    slots[bill] = spawn;

                    var col = bill.GetComponentInChildren<Collider>(true);
                    if (col != null) col.enabled = true;
                }
            }

            if (queue.Count <= 0 && !MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer)
                ProcessingBillIndicatorUI.Instance?.Hide();
        }

        printing = false;
        if (!MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer)
            ProcessingBillIndicatorUI.Instance?.Hide();
    }

    private Transform GetFreeSpawnPoint()
    {
        for (int i = 0; i < billSpawnPoints.Count; i++)
        {
            var sp = billSpawnPoints[i];
            if (sp == null) continue;

            if (!SpawnPointHasBill(sp))
                return sp;
        }
        return null;
    }

    private bool SpawnPointHasBill(Transform spawnPoint)
    {
        if (spawnPoint == null) return false;
        if (MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer)
            foreach (var slot in slots) if (slot.Key != null && slot.Value == spawnPoint) return true;
        return spawnPoint.GetComponentInChildren<BillPaper>(true) != null;
    }

    private bool HasExistingBillForGroup(CustomerGroup group)
    {
        if (MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer) return FindBillForGroup(group) != null;
        if (group == null) return false;

        if (billsRoot != null)
        {
            var bills = billsRoot.GetComponentsInChildren<BillPaper>(true);
            for (int i = 0; i < bills.Length; i++)
            {
                var b = bills[i];
                if (b != null && b.Matches(group))
                    return true;
            }
            return false;
        }

        for (int i = 0; i < billSpawnPoints.Count; i++)
        {
            var sp = billSpawnPoints[i];
            if (sp == null) continue;

            var bills = sp.GetComponentsInChildren<BillPaper>(true);
            for (int k = 0; k < bills.Length; k++)
            {
                var b = bills[k];
                if (b != null && b.Matches(group))
                    return true;
            }
        }

        return false;
    }

    public BillPaper FindBillForGroup(CustomerGroup group)
    {
        if (group == null) return null;
        if (papers.TryGetValue(group, out var registered) && registered != null && registered.Matches(group)) return registered;
        if (MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer) return null;

        if (billsRoot != null)
        {
            var bills = billsRoot.GetComponentsInChildren<BillPaper>(true);
            for (int i = 0; i < bills.Length; i++)
            {
                var b = bills[i];
                if (b != null && b.Matches(group))
                    return b;
            }
            return null;
        }

        for (int i = 0; i < billSpawnPoints.Count; i++)
        {
            var sp = billSpawnPoints[i];
            if (sp == null) continue;

            var bills = sp.GetComponentsInChildren<BillPaper>(true);
            for (int k = 0; k < bills.Length; k++)
            {
                var b = bills[k];
                if (b != null && b.Matches(group))
                    return b;
            }
        }

        return null;
    }

    // A claimant's local copy of the already-printed authority bill, not a print request.
    public BillPaper PresentMultiplayerBill(CustomerGroup group, Vector3 position, Quaternion rotation)
    {
        if (!MultiplayerCustomerInteractionBridge.ReviewIsMultiplayer ||
            MultiplayerSessionManager.Instance.IsAuthority || billPaperPrefab == null) return null;
        var existing = FindBillForGroup(group);
        if (existing != null) return existing;
        var root = Instantiate(billPaperPrefab, position, rotation, billsRoot);
        var bill = root.GetComponentInChildren<BillPaper>(true);
        if (bill != null) bill.Init(group);
        else Destroy(root);
        return bill;
    }

    public void RemoveObservedBill(CustomerGroup group)
    {
        var paper = FindBillForGroup(group);
        if (paper == null) return;
        paper.GetComponentInParent<WaiterHands>(true)?.DetachBillPaper(paper);
        Unregister(paper); paper.gameObject.SetActive(false); Destroy(paper.gameObject);
    }
    private float nextCleanup;
    private void LateUpdate()
    {
        if (!MultiplayerDayBridge.IsActive || Time.unscaledTime < nextCleanup) return;
        nextCleanup = Time.unscaledTime + 0.5f;
        foreach (var entry in new List<KeyValuePair<CustomerGroup, BillPaper>>(papers))
        {
            if (entry.Key != null && entry.Value != null && !entry.Key.HasReceivedBill
                && entry.Key.state == CustomerGroup.GroupState.NeedsBill)
            {
                var holder = entry.Value.GetComponentInParent<WaiterHands>(true);
                if (MultiplayerSessionManager.Instance.IsAuthority && holder != null && !holder.isActiveAndEnabled)
                    ReturnUndeliveredBill(entry.Key);
                continue;
            }
            papers.Remove(entry.Key);
            if (entry.Value == null) continue;
            slots.Remove(entry.Value);
            entry.Value.GetComponentInParent<WaiterHands>(true)?.DetachBillPaper(entry.Value);
            entry.Value.gameObject.SetActive(false); Destroy(entry.Value.gameObject);
        }
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
