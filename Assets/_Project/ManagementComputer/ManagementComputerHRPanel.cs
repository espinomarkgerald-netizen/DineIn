using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Department tabs and role sections for the management computer HR app.</summary>
public sealed class ManagementComputerHRPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text departmentTitle;
    [SerializeField] private TMP_Text departmentDescription;
    [SerializeField] private Button lobbyTab;
    [SerializeField] private TMP_Text lobbyTabLabel;
    [SerializeField] private Button kitchenTab;
    [SerializeField] private TMP_Text kitchenTabLabel;
    [SerializeField] private RectTransform sectionsRoot;
    [SerializeField] private ManagementHRRoleSectionUI sectionPrefab;
    [SerializeField] private ManagementEmployeeCardUI cardPrefab;

    private EmployeeManager manager;
    private bool editable;

    public EmployeeDepartment CurrentDepartment { get; private set; }
    public RectTransform SectionsRoot => sectionsRoot;
    public Button LobbyTab => lobbyTab;
    public Button KitchenTab => kitchenTab;

    public void ConfigureReferences(
        TMP_Text configuredDepartmentTitle,
        TMP_Text configuredDepartmentDescription,
        Button configuredLobbyTab,
        TMP_Text configuredLobbyTabLabel,
        Button configuredKitchenTab,
        TMP_Text configuredKitchenTabLabel,
        RectTransform configuredSectionsRoot,
        ManagementHRRoleSectionUI configuredSectionPrefab,
        ManagementEmployeeCardUI configuredCardPrefab)
    {
        departmentTitle = configuredDepartmentTitle;
        departmentDescription = configuredDepartmentDescription;
        lobbyTab = configuredLobbyTab;
        lobbyTabLabel = configuredLobbyTabLabel;
        kitchenTab = configuredKitchenTab;
        kitchenTabLabel = configuredKitchenTabLabel;
        sectionsRoot = configuredSectionsRoot;
        sectionPrefab = configuredSectionPrefab;
        cardPrefab = configuredCardPrefab;
    }

    public void Bind(EmployeeManager configuredManager, bool canEdit)
    {
        manager = configuredManager;
        editable = canEdit;
        lobbyTab.onClick.RemoveAllListeners();
        lobbyTab.onClick.AddListener(() => ShowDepartment(EmployeeDepartment.Lobby));
        kitchenTab.onClick.RemoveAllListeners();
        kitchenTab.onClick.AddListener(() => ShowDepartment(EmployeeDepartment.Kitchen));
        ShowDepartment(EmployeeDepartment.Lobby);
    }

    public void ShowDepartment(EmployeeDepartment department)
    {
        CurrentDepartment = department;
        ClearSections();

        bool lobby = department == EmployeeDepartment.Lobby;
        if (departmentTitle != null) departmentTitle.text = lobby ? "LOBBY DEPARTMENT" : "KITCHEN DEPARTMENT";
        if (departmentDescription != null)
        {
            string refresh = manager != null
                ? " • New applicants on Day " + manager.ApplicantNextRefreshDay
                : string.Empty;
            departmentDescription.text = (lobby
                ? "Front-of-house service roles"
                : "Kitchen staffing is limited to Chef and Barista") + refresh;
        }
        SetTabVisual(lobbyTab, lobbyTabLabel, lobby);
        SetTabVisual(kitchenTab, kitchenTabLabel, !lobby);

        foreach (EmployeeRole role in EmployeeRoleCatalog.GetRoles(department))
        {
            ManagementHRRoleSectionUI section = Instantiate(sectionPrefab, sectionsRoot);
            section.name = "Role_" + role;
            section.Bind(role, manager, cardPrefab, editable, RefreshCurrentDepartment);
        }

        float preferredHeight = 112f + EmployeeRoleCatalog.GetRoles(department).Count * 774f;
        RectTransform rect = transform as RectTransform;
        if (rect != null) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredHeight);
        LayoutElement layout = GetComponent<LayoutElement>();
        if (layout != null) layout.preferredHeight = preferredHeight;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(sectionsRoot);
    }

    private void RefreshCurrentDepartment() => ShowDepartment(CurrentDepartment);

    private void ClearSections()
    {
        if (sectionsRoot == null) return;
        for (int i = sectionsRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = sectionsRoot.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private static void SetTabVisual(Button button, TMP_Text label, bool selected)
    {
        if (button != null && button.targetGraphic is Image image)
            image.color = selected ? new Color(0.08f, 0.55f, 0.88f) : new Color(0.12f, 0.27f, 0.42f);
        if (label != null) label.color = Color.white;
    }
}
