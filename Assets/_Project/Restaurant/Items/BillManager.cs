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
                    || session.GetComponent<MultiplayerTaskClaims>().GetOwner($"Customer:{customer.photonView.ViewID}:Bill") == 0)
                    continue;
            }

            Transform spawn = GetFreeSpawnPoint();
            if (spawn == null) spawn = billSpawnPoints[0];

            if (spawn != null)
            {
                Transform parent = billsRoot != null ? billsRoot : spawn;

                var go = Instantiate(billPaperPrefab, spawn.position, spawn.rotation, parent);

                var bill = go.GetComponentInChildren<BillPaper>(true);
                if (bill != null)
                {
                    bill.Init(group);

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
        return spawnPoint.GetComponentInChildren<BillPaper>(true) != null;
    }

    private bool HasExistingBillForGroup(CustomerGroup group)
    {
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
}
