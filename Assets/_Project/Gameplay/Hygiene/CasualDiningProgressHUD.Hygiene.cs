using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class CasualDiningProgressHUD
{
    [Header("Kitchen cleaning HUD placement")]
    [SerializeField, Min(100f)] private float kitchenCleaningMaxWidth = 540f;
    [SerializeField, Min(0f)] private float kitchenCleaningNavigationGap = 18f;
    private RectTransform kitchenCleaningAnchor;
    private bool hygieneWasKitchen;
    private RectTransform hygieneRow, hygieneFill, hygieneShine;
    private TMP_Text hygieneLabel, hygieneValue;
    private CanvasGroup hygieneGroup;
    private float hygieneDisplay, hygieneShownAt = -10f;
    private int hygieneDay;
    private bool hygieneLayoutActive;
    private int hygieneRows;
    private RectTransform lobbyCleanRow, lobbyCleanFill, lobbyCleanShine;
    private TMP_Text lobbyCleanLabel, lobbyCleanValue;

    public void SetKitchenCleaningAnchor(RectTransform anchor)
    {
        if (kitchenCleaningAnchor == anchor) return;
        kitchenCleaningAnchor = anchor;
        PlaceHygieneRow();
    }

    public void ClearKitchenCleaningAnchor(RectTransform anchor)
    {
        // A closing view may release only its own presentation placement.
        if (kitchenCleaningAnchor == anchor) SetKitchenCleaningAnchor(null);
    }

    private void PlaceHygieneRow()
    {
        if (hygieneRow == null || approvalRow == null) return;
        bool inKitchen = hygieneWasKitchen && kitchenCleaningAnchor != null && kitchenCleaningAnchor.gameObject.activeInHierarchy;
        var parent = inKitchen ? kitchenCleaningAnchor : approvalRow;
        if (hygieneRow.parent != parent) hygieneRow.SetParent(parent, false);
        hygieneRow.sizeDelta = approvalRow.rect.size;
        if (inKitchen)
        {
            // Reuse the actual lobby row outside its hidden HUD branch. Anchor
            // below the real navigation label so Windows/mobile layouts agree.
            hygieneRow.anchorMin = hygieneRow.anchorMax = new Vector2(.5f, 0f);
            hygieneRow.pivot = new Vector2(.5f, 1f);
            hygieneRow.anchoredPosition = new Vector2(0f, -kitchenCleaningNavigationGap);
            var area = kitchenCleaningAnchor.parent as RectTransform;
            float width = area != null ? Mathf.Min(kitchenCleaningMaxWidth, area.rect.width * .6f) : kitchenCleaningMaxWidth;
            hygieneRow.localScale = Vector3.one * Mathf.Min(1f, width / Mathf.Max(1f, hygieneRow.rect.width));
        }
        else
        {
            hygieneRow.anchorMin = hygieneRow.anchorMax = hygieneRow.pivot = new Vector2(0f, 1f);
            hygieneRow.localScale = Vector3.one;
            hygieneRow.anchoredPosition = new Vector2(0f, -approvalRow.rect.height - 4f);
        }
    }

    private void RefreshHygieneProgress()
    {
        var hygiene = HygieneManager.Instance;
        bool kitchenCleaning = hygiene != null && hygiene.State.Cleaning;
        bool lobbyCleaning = hygiene != null && hygiene.State.lobbyCleaningRequested;
        bool cleaning = kitchenCleaning || lobbyCleaning;
        if (cleaning) hygieneWasKitchen = kitchenCleaning;
        if (lobbyCleanRow != null) lobbyCleanRow.gameObject.SetActive(kitchenCleaning && lobbyCleaning);
        if (hygiene == null || hygieneDay != hygiene.State.day)
        {
            hygieneDay = hygiene != null ? hygiene.State.day : 0;
            hygieneShownAt = -10f;
            hygieneDisplay = 0f;
        }
        if (cleaning) hygieneShownAt = Time.unscaledTime;
        bool visible = cleaning || hygiene != null && Time.unscaledTime - hygieneShownAt < 1f;
        if (approvalRow == null) return;
        int desiredRows = !visible ? 0 : kitchenCleaning && lobbyCleaning ? 2 : 1;
        if (hygieneRows != desiredRows)
        {
            if (!useLobbyHudRedesignLayout)
            {
                float shift = (hygieneRows - desiredRows) * (approvalRow.rect.height + 4f);
                if (neutralRow != null) neutralRow.anchoredPosition += new Vector2(0f, shift);
                if (angryRow != null) angryRow.anchoredPosition += new Vector2(0f, shift);
            }
            hygieneLayoutActive = visible;
            hygieneRows = desiredRows;
            RefreshResponsiveLayout(true);
        }
        if (hygieneRow == null && visible)
        {
            // Copy the authored approval bar, including its frame, stripes and gloss.
            hygieneRow = Instantiate(approvalRow, approvalRow.parent, false);
            hygieneRow.name = "KitchenCleaningProgress";
            hygieneRow.SetParent(approvalRow, false);
            hygieneFill = FindRowRect(hygieneRow, "Track/Fill");
            hygieneShine = FindRowRect(hygieneRow, "Track/Fill/Shine");
            hygieneLabel = FindRowText(hygieneRow, "Label");
            hygieneValue = FindRowText(hygieneRow, "Value");
            hygieneGroup = hygieneRow.GetComponent<CanvasGroup>();
            if (hygieneGroup == null) hygieneGroup = hygieneRow.gameObject.AddComponent<CanvasGroup>();
            hygieneGroup.interactable = hygieneGroup.blocksRaycasts = false;
            if (hygieneLabel != null) hygieneLabel.text = "Kitchen Cleaning";
            if (hygieneFill != null) hygieneFill.GetComponent<Image>().color = new Color(.62f, .27f, .88f);
            var icon = hygieneRow.Find("BadgeRim/BadgeCore/Icon");
            if (icon != null) icon.gameObject.SetActive(false);
            var authoredIcon = hygieneRow.Find("Icon");
            if (authoredIcon != null) authoredIcon.gameObject.SetActive(false);
            hygieneDisplay = 0f;
        }
        if (hygieneRow == null) return;
        hygieneRow.gameObject.SetActive(visible);
        if (!visible) { hygieneDisplay = 0f; return; }
        PlaceHygieneRow();
        if (hygieneLabel != null) hygieneLabel.text = hygieneWasKitchen ? "Kitchen Cleaning" : "Lobby Cleaning";
        float progress = cleaning ? kitchenCleaning ? hygiene.CleaningProgress :
            (MultiplayerRestaurantBridge.IsObserver ? hygiene.State.floorWorkProgress : hygiene.LobbyCleaningProgress)
            : !hygieneWasKitchen && hygiene.State.floorRouteTotal > 0 ? hygiene.State.floorWorkProgress : 1f;
        if (progress < hygieneDisplay - .1f) hygieneDisplay = progress;
        hygieneDisplay = Mathf.MoveTowards(hygieneDisplay, progress, Time.unscaledDeltaTime * 2f);
        hygieneGroup.alpha = cleaning ? 1f : Mathf.Clamp01(1f - (Time.unscaledTime - hygieneShownAt));
        SetFill(hygieneFill, hygieneDisplay);
        AnimateShine(hygieneShine, Time.unscaledTime, LevelOneUIAccessibility.ReducedMotion);
        if (hygieneValue != null) hygieneValue.text = Mathf.RoundToInt(hygieneDisplay * 100f) + "%";
        if (kitchenCleaning && lobbyCleaning && lobbyCleanRow == null)
        {
            lobbyCleanRow = Instantiate(hygieneRow, approvalRow, false);
            lobbyCleanRow.name = "LobbyCleaningProgress";
            lobbyCleanFill = FindRowRect(lobbyCleanRow, "Track/Fill");
            lobbyCleanShine = FindRowRect(lobbyCleanRow, "Track/Fill/Shine");
            lobbyCleanLabel = FindRowText(lobbyCleanRow, "Label");
            lobbyCleanValue = FindRowText(lobbyCleanRow, "Value");
            if (lobbyCleanLabel != null) lobbyCleanLabel.text = "Lobby Cleaning";
        }
        if (lobbyCleanRow != null && kitchenCleaning && lobbyCleaning)
        {
            lobbyCleanRow.anchorMin = lobbyCleanRow.anchorMax = lobbyCleanRow.pivot = new Vector2(0f, 1f);
            lobbyCleanRow.localScale = Vector3.one;
            lobbyCleanRow.sizeDelta = approvalRow.rect.size;
            lobbyCleanRow.anchoredPosition = new Vector2(0, -2f * (approvalRow.rect.height + 4f));
            float lobbyProgress = MultiplayerRestaurantBridge.IsObserver ? hygiene.State.floorWorkProgress : hygiene.LobbyCleaningProgress;
            SetFill(lobbyCleanFill, lobbyProgress);
            if (lobbyCleanValue != null) lobbyCleanValue.text = Mathf.RoundToInt(lobbyProgress * 100f) + "%";
            AnimateShine(lobbyCleanShine, Time.unscaledTime, LevelOneUIAccessibility.ReducedMotion);
        }
    }
}
