using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class MultiplayerMenuController
{
    [Header("Room menu layout")]
    [SerializeField] private Button closeButton;
    [SerializeField] private RectTransform menuViewport, contentFrame;
    [SerializeField] private Button connectionDetailsButton;
    [SerializeField] private TMP_Text connectionDetailsText;
    [SerializeField, Min(0)] private float sideMargin = 16, topMargin = 76;
    [SerializeField, Min(24)] private float footerHeight = 40, detailsHeight = 96;
    private bool detailsExpanded, abandoningOperation;
    private Vector2 previousViewport;
    private bool layoutDirty = true;

    private void BindMenuPresentation()
    {
        if (closeButton != null) closeButton.onClick.AddListener(RequestClose);
        if (connectionDetailsButton != null) connectionDetailsButton.onClick.AddListener(ToggleConnectionDetails);
    }
    private void UnbindMenuPresentation()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(RequestClose);
        if (connectionDetailsButton != null) connectionDetailsButton.onClick.RemoveListener(ToggleConnectionDetails);
    }
    public void RequestClose()
    {
        if (starting) return; // A registered run is already transitioning to gameplay.
        if (busy) CancelPendingOperation();
        GetComponent<AnimatedPanel>()?.Close();
    }
    private void CancelPendingOperation()
    {
        TraceRoom("cancel request", submittedCode ?? pendingCode);
        busy = starting = pendingRejoin = awaitingAdmission = fallbackRejoin = false;
        pendingCode = pendingAccountId = startingRunId = null;
        rejoinFailure = null;
        var state = PhotonNetwork.NetworkClientState;
        abandoningOperation = state != ClientState.Disconnected && state != ClientState.PeerCreated;
        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonBootstrap.Instance?.CancelPendingConnection();
        RefreshRoom();
    }
    private void ToggleConnectionDetails()
    {
        detailsExpanded = !detailsExpanded;
        layoutDirty = true;
        RefreshConnectionDetails();
    }
    private void RefreshConnectionDetails()
    {
        if (connectionDetailsText == null) return;
        connectionDetailsText.gameObject.SetActive(detailsExpanded);
        if (detailsExpanded) connectionDetailsText.text = PhotonBootstrap.ConnectionDetails;
        if (connectionDetailsButton != null)
            connectionDetailsButton.GetComponentInChildren<TMP_Text>().text = detailsExpanded ? "Hide connection details" : "Connection details";
    }
    private void LateUpdate()
    {
        if (menuViewport == null || contentFrame == null) return;
        Vector2 size = menuViewport.rect.size;
        if (!layoutDirty && previousViewport == size) return;
        layoutDirty = false; previousViewport = size;
        float footer = footerHeight + (detailsExpanded ? detailsHeight : 0);
        Vector2 authored = contentFrame.sizeDelta;
        float scale = Mathf.Min(1, Mathf.Max(.01f, (size.x - sideMargin * 2) / Mathf.Max(1, authored.x)),
            Mathf.Max(.01f, (size.y - topMargin - footer) / Mathf.Max(1, authored.y)));
        contentFrame.localScale = Vector3.one * scale;
        contentFrame.anchoredPosition = new Vector2(0, (footer - topMargin) * .5f);
    }
}
