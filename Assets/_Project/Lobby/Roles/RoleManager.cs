using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoleManager : MonoBehaviour
{
    public static RoleManager Instance;

    /// <summary>
    /// Raised whenever the player successfully switches to a different role.
    /// Subscribers receive the call after the switch is fully applied.
    /// </summary>
    public event Action OnRoleSwitched;

    [Header("Staff")]
    public GameObject host;
    public GameObject waiter;
    public GameObject cashier;
    public GameObject busser;

    [Header("Default Role")]
    [SerializeField] private GameObject defaultRole;

    [Header("Role Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button waiterButton;
    [SerializeField] private Button cashierButton;
    [SerializeField] private Button busserButton;

    [Header("Role Display")]
    [SerializeField] private TMP_Text currentRoleText;

    [Header("Camera Anchors")]
    [SerializeField] private Transform hostCameraAnchor;
    [SerializeField] private Transform waiterCameraAnchor;
    [SerializeField] private Transform cashierCameraAnchor;
    [SerializeField] private Transform busserCameraAnchor;

    private GameObject activeRole;
    private RoleCameraController cameraController;

    private void Awake()
    {
        Instance = this;
        cameraController = GetComponent<RoleCameraController>();
        InitializeDefaultRole();
    }

    private void Start()
    {
        RefreshButtonVisuals();
        UpdateRoleUI();

        if (cameraController != null && activeRole != null)
            cameraController.PanToTarget(activeRole.transform);
    }

    private void InitializeDefaultRole()
    {
        if (defaultRole == null)
            defaultRole = waiter != null ? waiter : host;

        InitializeRole(host, false);
        InitializeRole(waiter, false);
        InitializeRole(cashier, false);
        InitializeRole(busser, false);

        activeRole = defaultRole;

        SetPlayerControlled(activeRole, true);
        SetIndicator(activeRole, true);

        UpdateRoleUI();
    }

    private void InitializeRole(GameObject obj, bool playerControlled)
    {
        if (obj == null) return;

        var move = obj.GetComponent<PlayerMovement>();
        if (move != null)
        {
            move.enabled = true;
            move.SetPlayerControlled(playerControlled);
            move.CancelAutoFinish();
        }

        SetIndicator(obj, playerControlled);
    }

    public void SwitchToHost()
    {
        TrySwitch(host);
    }

    public void SwitchToWaiter()
    {
        TrySwitch(waiter);
    }

    public void SwitchToCashier()
    {
        TrySwitch(cashier);
    }

    public void SwitchToBusser()
    {
        TrySwitch(busser);
    }

    private void TrySwitch(GameObject nextRole)
    {
        if (nextRole == null) return;
        if (activeRole == nextRole) return;

        var currentMove = activeRole != null ? activeRole.GetComponent<PlayerMovement>() : null;
        var nextMove = nextRole.GetComponent<PlayerMovement>();

        if (nextMove == null) return;

        if (currentMove != null)
        {
            currentMove.SetPlayerControlled(false);
            currentMove.BeginAutoFinish();
        }

        SetIndicator(host, false);
        SetIndicator(waiter, false);
        SetIndicator(cashier, false);
        SetIndicator(busser, false);

        activeRole = nextRole;

        nextMove.CancelAutoFinish();
        nextMove.SetPlayerControlled(true);

        SetIndicator(activeRole, true);

        RefreshButtonVisuals();
        UpdateRoleUI();

        if (cameraController != null)
            cameraController.PanToTarget(activeRole.transform);

        OnRoleSwitched?.Invoke();
    }

    private void RefreshButtonVisuals()
    {
        ApplyButtonVisual(hostButton, activeRole == host);
        ApplyButtonVisual(waiterButton, activeRole == waiter);
        ApplyButtonVisual(cashierButton, activeRole == cashier);
        ApplyButtonVisual(busserButton, activeRole == busser);
    }

    private void ApplyButtonVisual(Button button, bool selected)
    {
        if (button == null || button.image == null) return;

        if (selected)
        {
            var selectedSprite = button.spriteState.selectedSprite;
            button.image.overrideSprite = selectedSprite != null ? selectedSprite : null;
        }
        else
        {
            button.image.overrideSprite = null;
        }
    }

    private void UpdateRoleUI()
    {
        if (currentRoleText == null) return;

        currentRoleText.text = GetActiveRoleDisplayName();
    }

    private string GetActiveRoleDisplayName()
    {
        if (activeRole == null)
            return "None";

        StaffRole staffRole = activeRole.GetComponent<StaffRole>();
        if (staffRole == null)
            return activeRole.name;

        switch (staffRole.role)
        {
            case StaffRole.Role.Host:
                return "Host";
            case StaffRole.Role.Waiter:
                return "Waiter";
            case StaffRole.Role.Cashier:
                return "Cashier";
            case StaffRole.Role.Busser:
                return "Busser";
            default:
                return activeRole.name;
        }
    }

    private void SetPlayerControlled(GameObject obj, bool value)
    {
        if (obj == null) return;

        var move = obj.GetComponent<PlayerMovement>();
        if (move != null)
            move.SetPlayerControlled(value);
    }

    private void SetIndicator(GameObject obj, bool value)
    {
        if (obj == null) return;

        var indicator = obj.GetComponent<RoleIndicator>();
        if (indicator != null)
            indicator.SetSelected(value);
    }

    public bool IsActiveRole(GameObject obj)
    {
        if (ManagerPlayer.Active != null && obj == ManagerPlayer.Active.gameObject)
            return true;

        if (!enabled)
            return false;

        return activeRole == obj;
    }

    public string ActiveRoleName()
    {
        return GetActiveRoleDisplayName();
    }

    public StaffRole.Role ActiveRoleType()
    {
        if (activeRole == null) return StaffRole.Role.Waiter;

        var sr = activeRole.GetComponent<StaffRole>();
        return sr != null ? sr.role : StaffRole.Role.Waiter;
    }

    public bool IsActiveRoleType(StaffRole.Role role)
    {
        if (ManagerPlayer.Active != null && ManagerPlayer.Active.CanPerform(role))
            return true;

        if (!enabled)
            return false;

        return ActiveRoleType() == role;
    }

    public PlayerMovement GetActivePlayerMovement()
    {
        if (ManagerPlayer.Active != null)
            return ManagerPlayer.Active.Movement;

        if (!enabled)
            return null;

        if (activeRole == null) return null;
        return activeRole.GetComponent<PlayerMovement>();
    }

    /// <summary>
    /// Used by LobbyAutonomousService in the no-player Casual Dining preview.
    /// Other scenes retain their current role-switching behaviour because they
    /// never call this method.
    /// </summary>
    public void DisablePlayerRoleControl()
    {
        SetPlayerControlled(host, false);
        SetPlayerControlled(waiter, false);
        SetPlayerControlled(cashier, false);
        SetPlayerControlled(busser, false);

        SetIndicator(host, false);
        SetIndicator(waiter, false);
        SetIndicator(cashier, false);
        SetIndicator(busser, false);

        if (hostButton != null) hostButton.gameObject.SetActive(false);
        if (waiterButton != null) waiterButton.gameObject.SetActive(false);
        if (cashierButton != null) cashierButton.gameObject.SetActive(false);
        if (busserButton != null) busserButton.gameObject.SetActive(false);
        if (currentRoleText != null) currentRoleText.gameObject.SetActive(false);

        // Keep the compatibility service discoverable. Runtime interaction code
        // uses it to resolve the independent Manager even though role switching
        // and this MonoBehaviour's Update lifecycle are disabled.
        enabled = false;
    }
}
