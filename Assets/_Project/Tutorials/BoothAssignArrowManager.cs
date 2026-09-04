using System.Collections.Generic;
using UnityEngine;

public class BoothAssignArrowManager : MonoBehaviour
{
    public static BoothAssignArrowManager Instance;

    [Header("UI")]
    [SerializeField] private GameObject arrowPrefab;

    [Header("Position")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.9f, 0f);

    [Header("Arrow Size")]
    [SerializeField] private float arrowScale = 0.7f;

    private GameObject activeArrow;
    private GameObject activeAnchor;
    private Booth activeSuggestedBooth;
    private Booth[] booths;
    public Booth ActiveSuggestedBooth => activeSuggestedBooth;

    private void Awake()
    {
        Instance = this;

        RefreshBooths();
    }

    public void RefreshBooths()
    {
        booths = FindObjectsByType<Booth>(FindObjectsSortMode.None);
    }

    public void ShowValidBooths(CustomerGroup group)
    {
        HideAll();

        if (group == null)
            return;

        if (booths == null || booths.Length == 0)
            RefreshBooths();

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[BoothAssignArrowManager] No main camera found.");
            return;
        }

        Booth bestBooth = GetBestBooth(group);
        if (bestBooth == null)
            return;

        SpawnArrowForBooth(bestBooth, cam);
    }

    public bool HasValidBooth(CustomerGroup group)
    {
        if (group == null)
            return false;

        if (booths == null || booths.Length == 0)
            RefreshBooths();

        for (int i = 0; i < booths.Length; i++)
        {
            Booth booth = booths[i];
            if (IsValidBooth(booth, group))
                return true;
        }

        return false;
    }

    public void HideAll()
    {
        if (activeArrow != null)
        {
            Destroy(activeArrow);
            activeArrow = null;
        }
        if (activeAnchor != null)
        {
            Destroy(activeAnchor);
            activeAnchor = null;
        }
        activeSuggestedBooth = null;
    }

    private void SpawnArrowForBooth(Booth booth, Camera cam)
    {
        if (arrowPrefab == null || booth == null)
            return;

        activeArrow = Instantiate(arrowPrefab);
        activeArrow.name = $"AssignArrow_{booth.name}";
        activeSuggestedBooth = booth;

        RectTransform rect = activeArrow.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.localScale = Vector3.one * arrowScale;
            rect.anchoredPosition3D = Vector3.zero;
        }

        activeAnchor = new GameObject("Tutorial Booth Arrow Anchor");
        activeAnchor.transform.SetParent(booth.transform, true);
        activeAnchor.transform.position = CalculateLiveTableAnchor(booth);
        Transform anchor = activeAnchor.transform;

        BoothAssignArrowUI ui = activeArrow.GetComponent<BoothAssignArrowUI>();
        if (ui != null)
        {
            ui.Init(anchor, worldOffset, cam);
        }
        else
        {
            UIFollowWorldPoint follow = activeArrow.GetComponent<UIFollowWorldPoint>();
            if (follow != null)
                follow.Init(anchor, worldOffset, cam);
        }
    }

    public Booth GetSuggestedBooth(CustomerGroup group)
    {
        if (booths == null || booths.Length == 0) RefreshBooths();
        return GetBestBooth(group);
    }

    public Transform GetSuggestionTarget(CustomerGroup group)
    {
        if (activeSuggestedBooth != null && IsValidBooth(activeSuggestedBooth, group) && activeAnchor != null)
            return activeAnchor.transform;
        return GetSuggestedBooth(group)?.transform;
    }

    private static Vector3 CalculateLiveTableAnchor(Booth booth)
    {
        Vector3 seatCenter = Vector3.zero;
        float seatTop = float.NegativeInfinity;
        int activeSeats = 0;
        foreach (Transform seat in booth.seats)
        {
            if (seat == null || !seat.gameObject.activeInHierarchy || SeatAnchor.IsSeatOccupied(seat)) continue;
            seatCenter += seat.position;
            seatTop = Mathf.Max(seatTop, seat.position.y);
            activeSeats++;
        }
        if (activeSeats > 0) seatCenter /= activeSeats;
        else seatCenter = booth.transform.position;

        float tableTop = float.NegativeInfinity;
        float nearestSqr = float.PositiveInfinity;
        Vector3 tableCenter = seatCenter;
        foreach (Renderer renderer in booth.GetComponentsInChildren<Renderer>(false))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            Vector3 delta = renderer.bounds.center - seatCenter;
            delta.y = 0f;
            if (delta.sqrMagnitude > nearestSqr) continue;
            nearestSqr = delta.sqrMagnitude;
            tableCenter = renderer.bounds.center;
            tableTop = renderer.bounds.max.y;
        }
        if (float.IsPositiveInfinity(nearestSqr))
        {
            foreach (Collider collider in booth.GetComponentsInChildren<Collider>(false))
            {
                if (!collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy) continue;
                Vector3 delta = collider.bounds.center - seatCenter;
                delta.y = 0f;
                if (delta.sqrMagnitude > nearestSqr) continue;
                nearestSqr = delta.sqrMagnitude;
                tableCenter = collider.bounds.center;
                tableTop = collider.bounds.max.y;
            }
        }
        if (float.IsNegativeInfinity(tableTop)) tableTop = float.IsNegativeInfinity(seatTop) ? booth.transform.position.y : seatTop;
        return new Vector3(tableCenter.x, tableTop, tableCenter.z);
    }

    private Booth GetBestBooth(CustomerGroup group)
    {
        if (group == null || booths == null || booths.Length == 0)
            return null;

        List<Booth> validBooths = new List<Booth>();

        for (int i = 0; i < booths.Length; i++)
        {
            Booth booth = booths[i];
            if (IsValidBooth(booth, group))
                validBooths.Add(booth);
        }

        if (validBooths.Count == 0)
            return null;

        int bestSeatCount = int.MaxValue;

        for (int i = 0; i < validBooths.Count; i++)
        {
            int seatCount = validBooths[i].seats != null ? validBooths[i].seats.Count : 0;
            if (seatCount >= group.Size && seatCount < bestSeatCount)
                bestSeatCount = seatCount;
        }

        List<Booth> smallestFitBooths = new List<Booth>();

        for (int i = 0; i < validBooths.Count; i++)
        {
            int seatCount = validBooths[i].seats != null ? validBooths[i].seats.Count : 0;
            if (seatCount == bestSeatCount)
                smallestFitBooths.Add(validBooths[i]);
        }

        smallestFitBooths.Sort(CompareBoothPriority);
        return smallestFitBooths[0];
    }

    private int CompareBoothPriority(Booth a, Booth b)
    {
        int aPriority = GetBoothPriority(a);
        int bPriority = GetBoothPriority(b);
        return aPriority.CompareTo(bPriority);
    }

    private int GetBoothPriority(Booth booth)
    {
        if (booth == null)
            return int.MaxValue;

        string boothName = booth.name.ToLower();

        int number = ExtractTrailingNumber(boothName);

        if (boothName.Contains("long table"))
            return 1000 + number;

        return number;
    }

    private int ExtractTrailingNumber(string value)
    {
        if (string.IsNullOrEmpty(value))
            return int.MaxValue;

        string digits = "";

        for (int i = value.Length - 1; i >= 0; i--)
        {
            if (char.IsDigit(value[i]))
                digits = value[i] + digits;
            else if (digits.Length > 0)
                break;
        }

        if (int.TryParse(digits, out int result))
            return result;

        return int.MaxValue;
    }

    private bool IsValidBooth(Booth booth, CustomerGroup group)
    {
        if (booth == null || group == null)
            return false;

        if (!booth.gameObject.activeInHierarchy || booth.approachPoint == null ||
            !booth.approachPoint.gameObject.activeInHierarchy)
            return false;

        if (booth.CurrentGroup != null)
            return false;

        if (booth.seats == null)
            return false;

        int seatCount = 0;
        for (int i = 0; i < booth.seats.Count; i++)
        {
            Transform seat = booth.seats[i];
            if (seat == null || !seat.gameObject.activeInHierarchy || SeatAnchor.IsSeatOccupied(seat)) continue;
            seatCount++;
        }
        int groupSize = group.Size;

        if (seatCount < groupSize)
            return false;

        if (!booth.IsAvailableFor(groupSize))
            return false;

        return true;
    }
}
