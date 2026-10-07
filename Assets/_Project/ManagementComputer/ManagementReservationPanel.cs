using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Editable two-panel presentation over the existing reservation service.</summary>
public sealed class ManagementReservationPanel : MonoBehaviour
{
    [Header("Authored layout")]
    public Button requestsTab, tonightTab, backButton, acceptButton, declineButton;
    public TMP_Text requestsLabel, tonightLabel, acceptLabel, summary, emptyList;
    public ScrollRect requestsScroll, tablesScroll;
    public ManagementReservationCard requestTemplate, tableTemplate, selectedDetails;
    public RectTransform detailsRoot;
    public GridLayoutGroup tableGrid;
    public TMP_Text selectionHint, banner;
    public GameObject bannerRoot;
    public GameObject declineConfirmation;
    public Button cancelDecline, confirmDecline;
    public TMP_Text feedbackText;
    public Image feedbackIcon;
    public GameObject feedbackRoot;
    [Header("Existing Dine In assets")]
    public Sprite blueButton, greenButton, redButton, greyButton;
    public Sprite cardNormal, cardSelected, cardDisabled, checkmarkSprite;
    public CustomerTypeProfile[] customerProfiles;
    [Header("Layout and feedback tuning")]
    [Min(120)] public float tableCardHeight = 222f;
    [Min(1)] public int wideTableColumns = 3;
    [Min(1)] public float wideTableThreshold = 850f;
    [Min(0)] public float tableTopPadding = 184f;
    [Min(0)] public float bannerTableTopPadding = 250f;
    [Range(.12f, .25f)] public float feedbackDuration = .18f;

    private FineDiningReservations source;
    private string selectedId, selectedTableId;
    private bool tonight, busy;
    private float nextRefresh;
    private Coroutine toastRoutine;
    private readonly Dictionary<string, ManagementReservationCard> requests = new Dictionary<string, ManagementReservationCard>();
    private readonly Dictionary<string, ManagementReservationCard> tables = new Dictionary<string, ManagementReservationCard>();
    private static readonly Color Ink = new Color(.045f, .24f, .32f);
    private static readonly Color Green = new Color(.05f, .42f, .24f);
    private static readonly Color Red = new Color(.65f, .16f, .22f);

    public void Bind(FineDiningReservations service, Action back)
    {
        source = service;
        requestsTab.onClick.AddListener(() => SetTab(false));
        tonightTab.onClick.AddListener(() => SetTab(true));
        backButton.onClick.AddListener(() => back());
        acceptButton.onClick.AddListener(Accept);
        declineButton.onClick.AddListener(() => declineConfirmation.SetActive(true));
        cancelDecline.onClick.AddListener(() => declineConfirmation.SetActive(false));
        confirmDecline.onClick.AddListener(Decline);
        declineConfirmation.SetActive(false);
        feedbackRoot.SetActive(false);
        RefreshData();
    }

    private void Update()
    {
        // Occupancy and eating change independently of reservation decisions.
        if (source == null || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .5f;
        RefreshData();
        if (!tonight && source.UnreadRequests > 0) source.MarkRequestsViewed();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (tableGrid == null) return;
        float width = ((RectTransform)tableGrid.transform).rect.width;
        if (width <= 0f) return;
        int columns = width >= wideTableThreshold ? Mathf.Max(2, wideTableColumns) : 2;
        tableGrid.constraintCount = columns;
        tableGrid.cellSize = new Vector2(Mathf.Max(1f, (width - tableGrid.padding.horizontal - tableGrid.spacing.x * (columns-1)) / columns), tableCardHeight);
    }

    private void SetTab(bool value)
    {
        if (busy || tonight == value) return;
        tonight = value;
        selectedId = selectedTableId = null;
        declineConfirmation.SetActive(false);
        RefreshData();
        requestsScroll.verticalNormalizedPosition = 1f;
        requestsScroll.GetComponent<UIRevealAnimation>()?.Play();
    }

    private Sprite Portrait(FineDiningBooking booking) => customerProfiles?.FirstOrDefault(p => p != null && (int)p.customerType == booking.customerType)?.customerImage;
    private bool Expired(FineDiningBooking booking) => booking.status == FineDiningBookingStatus.Pending && !source.Preparing && booking.arrivalMinute < source.NowMinute;
    private string Status(FineDiningBooking b)
    {
        if (Expired(b)) return "EXPIRED";
        switch (b.status)
        {
            case FineDiningBookingStatus.Pending: return "AWAITING ASSIGNMENT";
            case FineDiningBookingStatus.Confirmed: return !source.Preparing && b.arrivalMinute - source.NowMinute <= source.reminderMinutes ? "ARRIVING SOON" : "UPCOMING";
            case FineDiningBookingStatus.Arrived: return "ARRIVED";
            case FineDiningBookingStatus.Seated: return source.IsDining(b.id) ? "DINING" : "SEATED";
            case FineDiningBookingStatus.Completed: return "COMPLETED";
            case FineDiningBookingStatus.Left: return "MISSED / DEPARTED";
            default: return "DECLINED";
        }
    }

    public void RefreshData()
    {
        if (source == null || busy) return;
        var shown = source.Bookings.Where(b => tonight ? b.status != FineDiningBookingStatus.Pending && b.status != FineDiningBookingStatus.Declined : b.status == FineDiningBookingStatus.Pending).OrderBy(b => b.arrivalMinute).ToArray();
        requestsLabel.text = "Requests  " + source.Bookings.Count(b => b.status == FineDiningBookingStatus.Pending);
        tonightLabel.text = "Tonight  " + source.Bookings.Count(b => b.status != FineDiningBookingStatus.Pending && b.status != FineDiningBookingStatus.Declined);
        StyleButton(requestsTab, !tonight ? blueButton : greyButton, !tonight);
        StyleButton(tonightTab, tonight ? blueButton : greyButton, tonight);
        summary.text = source.ReservedGuests + " booked guests  •  " + source.ExpectedWalkInGroups * 2 + " expected walk-ins";
        if (!shown.Any(b => b.id == selectedId)) { selectedId = shown.FirstOrDefault()?.id; selectedTableId = null; }
        var ids = new HashSet<string>(shown.Select(b => b.id));
        foreach (var old in requests.Keys.Where(id => !ids.Contains(id)).ToArray())
        { requests[old].gameObject.SetActive(false); Destroy(requests[old].gameObject); requests.Remove(old); }
        for (int i = 0; i < shown.Length; i++)
        {
            var booking = shown[i];
            if (!requests.TryGetValue(booking.id, out var card))
            {
                card = Instantiate(requestTemplate, requestsScroll.content);
                card.name = "Party " + booking.id;
                card.gameObject.SetActive(true);
                string id = booking.id;
                card.button.onClick.AddListener(() => SelectRequest(id));
                requests.Add(id, card);
            }
            card.gameObject.SetActive(true);
            card.transform.SetSiblingIndex(i);
            card.title.text = booking.guestName;
            card.details.text = FineDiningReservations.Clock(booking.arrivalMinute) + "  •  " + booking.size + (booking.size == 1 ? " guest" : " guests") + (tonight ? "\n" + source.TableName(booking.tableId) : "");
            string status = Status(booking);
            bool arriving = status == "ARRIVING SOON" && card.status.text != status;
            card.status.text = status;
            card.status.color = Expired(booking) ? Red : Ink;
            card.SetPortraits(Portrait(booking), booking.size);
            card.SetState(booking.id == selectedId, !Expired(booking), cardNormal, cardSelected, cardDisabled);
            if (arriving) card.feedback?.PlayValueFeedback(true);
        }
        emptyList.gameObject.SetActive(shown.Length == 0);
        emptyList.text = tonight ? "No bookings tonight yet.\nAccepted parties appear here." : "All caught up!\nNo requests to review.";
        var detail = source.Booking(selectedId);
        detailsRoot.gameObject.SetActive(detail != null);
        selectionHint.gameObject.SetActive(detail == null);
        if (detail != null) RenderDetails(detail);
        OnRectTransformDimensionsChange();
    }

    private void SelectRequest(string id)
    {
        if (busy || selectedId == id) return;
        selectedId = id; selectedTableId = null;
        declineConfirmation.SetActive(false);
        RefreshData();
        tablesScroll.verticalNormalizedPosition = 1f;
        selectedDetails.GetComponent<UIRevealAnimation>()?.Play();
    }

    private bool TableSelectable(FineDiningBooking booking, FineDiningTable table, out string label)
    {
        label = "AVAILABLE";
        if (!table.Usable) { label = !table.Unlocked ? "Unlocks Day " + table.unlockDay : "Unavailable"; return false; }
        if (table.Capacity < booking.size) { label = "Seats " + table.Capacity + " • Need " + booking.size; return false; }
        if (table.booth.CurrentGroup != null && table.booth.CurrentGroup.FineDiningReservationId != booking.id)
        { label = "Occupied"; return false; }
        if (table.booth.IsDirty) { label = "Needs cleaning"; return false; }
        var conflict = source.Bookings.FirstOrDefault(b => b.id != booking.id && b.tableId == table.tableId && FineDiningReservationRules.ProtectsTable(b) && FineDiningReservationRules.Overlaps(Mathf.Max(booking.arrivalMinute, source.Preparing ? booking.arrivalMinute : source.NowMinute), Mathf.Max(booking.arrivalMinute, source.Preparing ? booking.arrivalMinute : source.NowMinute) + source.WindowMinutes, b.arrivalMinute, b.arrivalMinute + source.WindowMinutes));
        if (conflict != null) { label = "Reserved at " + FineDiningReservations.Clock(conflict.arrivalMinute); return false; }
        if (!source.CanAssign(booking, table, out _)) { label = "Unavailable"; return false; }
        return true;
    }

    private void RenderDetails(FineDiningBooking booking)
    {
        selectedDetails.title.text = booking.guestName;
        selectedDetails.details.text = FineDiningReservations.Clock(booking.arrivalMinute) + "  •  " + booking.size + (booking.size == 1 ? " guest" : " guests") + "\n" + (string.IsNullOrEmpty(booking.tableId) ? "Seating: Choose a table below" : "Assigned: " + source.TableName(booking.tableId));
        selectedDetails.status.text = Status(booking);
        selectedDetails.SetPortraits(Portrait(booking), booking.size);
        bool expired = Expired(booking);
        bool accepting = booking.status == FineDiningBookingStatus.Pending;
        string unavailable = "";
        bool canAccept = !accepting || source.CanAcceptRequest(booking, out unavailable);
        bannerRoot.SetActive(!canAccept);
        var tableRect = (RectTransform)tablesScroll.transform;
        tableRect.offsetMax = new Vector2(tableRect.offsetMax.x, -(canAccept ? tableTopPadding : bannerTableTopPadding));
        banner.text = expired ? "RESERVATION EXPIRED\nThe requested arrival time has passed." : unavailable;
        declineButton.gameObject.SetActive(accepting);
        declineButton.interactable = source.CanDeclineRequest(booking);
        StyleButton(declineButton, declineButton.interactable ? redButton : greyButton, declineButton.interactable);
        foreach (var existing in tables.Values) existing.gameObject.SetActive(false);
        var ordered = (source.tables ?? Array.Empty<FineDiningTable>()).Where(t => t != null && t.Owned).OrderByDescending(t => TableSelectable(booking, t, out _)).ThenBy(t => t.tableId).ToArray();
        for (int i = 0; i < ordered.Length; i++)
        {
            var table = ordered[i];
            if (!tables.TryGetValue(table.tableId, out var card))
            {
                card = Instantiate(tableTemplate, tablesScroll.content);
                card.name = "Seating " + table.tableId;
                card.gameObject.SetActive(true);
                var target = table;
                card.button.onClick.AddListener(() => SelectTable(target));
                tables.Add(table.tableId, card);
            }
            card.transform.SetSiblingIndex(i);
            bool valid = TableSelectable(booking, table, out string state);
            bool chosen = selectedTableId == table.tableId || selectedTableId == null && booking.tableId == table.tableId;
            card.title.text = table.displayName;
            card.details.text = "Seats " + table.Capacity;
            card.status.text = chosen && valid ? "SELECTED" : state;
            card.status.color = valid ? Green : Ink;
            card.preview.sprite = table.reservationPreview;
            card.preview.enabled = table.reservationPreview != null;
            card.button.interactable = valid;
            card.buttonLabel.text = chosen && valid ? "Selected" : valid ? "Select" : "Unavailable";
            StyleButton(card.button, valid ? chosen ? blueButton : greenButton : greyButton, valid);
            card.SetState(chosen, valid, cardNormal, cardSelected, cardDisabled);
        }
        var selected = source.tables?.FirstOrDefault(t => t != null && t.tableId == selectedTableId);
        acceptButton.interactable = selected != null && TableSelectable(booking, selected, out _) && (accepting || booking.tableId != selected.tableId);
        acceptLabel.text = accepting ? "Accept Reservation" : "Save Table";
        StyleButton(acceptButton, acceptButton.interactable ? greenButton : greyButton, acceptButton.interactable);
    }

    private void SelectTable(FineDiningTable table)
    {
        if (busy) return;
        var booking = source.Booking(selectedId);
        if (booking == null || !TableSelectable(booking, table, out var reason))
        { Toast("Table is no longer available", false); tables[table.tableId].feedback?.PlayValueFeedback(false); return; }
        selectedTableId = table.tableId;
        RefreshData();
    }

    private void Accept()
    {
        if (busy) return;
        var booking = source.Booking(selectedId);
        var table = source.tables?.FirstOrDefault(t => t != null && t.tableId == selectedTableId);
        if (booking == null || table == null || !TableSelectable(booking, table, out _)) { Toast("Choose an available table", false); RefreshData(); return; }
        bool accepting = booking.status == FineDiningBookingStatus.Pending;
        busy = true;
        if (!source.Assign(booking.id, table, out var reason)) { busy = false; Toast(reason, false); RefreshData(); return; }
        tables[table.tableId].feedback?.PlayValueFeedback(true);
        acceptButton.interactable = false;
        acceptButton.GetComponent<UISubtlePressFeedback>()?.PlayConfirmation(.045f);
        Toast(accepting ? "Reservation Confirmed" : "Table Updated", true);
        StartCoroutine(FinishDecision(booking.id, false));
    }

    private void Decline()
    {
        if (busy) return;
        declineConfirmation.SetActive(false);
        busy = true;
        if (!source.Decline(selectedId, out var reason)) { busy = false; Toast(reason, false); RefreshData(); return; }
        Toast("Request Declined", false);
        StartCoroutine(FinishDecision(selectedId, true));
    }

    private IEnumerator FinishDecision(string id, bool declined)
    {
        // The booking has already been saved; this is presentation-only feedback.
        requests.TryGetValue(id, out var card);
        if (card != null)
        {
            card.status.text = declined ? "DECLINED" : "CONFIRMED";
            card.status.color = declined ? Red : Green;
        }
        float duration = LevelOneUIAccessibility.ReducedMotion ? 0f : feedbackDuration;
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        {
            if (card != null && card.visual != null) card.visual.alpha = 1f - .8f * t / duration;
            yield return null;
        }
        busy = false;
        selectedTableId = null;
        RefreshData();
    }

    private void Toast(string text, bool positive)
    {
        feedbackText.text = text;
        feedbackText.color = Color.white;
        feedbackIcon.sprite = checkmarkSprite;
        feedbackIcon.gameObject.SetActive(positive);
        feedbackRoot.SetActive(true);
        feedbackRoot.GetComponent<UIRevealAnimation>()?.Play();
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(HideToast());
    }
    private IEnumerator HideToast() { yield return new WaitForSecondsRealtime(2.2f); feedbackRoot.SetActive(false); toastRoutine = null; }
    private static void StyleButton(Button button, Sprite sprite, bool whiteText)
    {
        button.image.sprite = sprite;
        button.image.color = Color.white;
        var text = button.GetComponentInChildren<TMP_Text>();
        if (text != null) text.color = whiteText ? Color.white : Ink;
    }
}

