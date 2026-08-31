using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ManagementHRView
{
    Lobby,
    Kitchen,
    Applicants
}

/// <summary>Sticky department/applicant tabs and role sections for the management computer HR app.</summary>
public sealed class ManagementComputerHRPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text departmentTitle;
    [SerializeField] private TMP_Text departmentDescription;
    [SerializeField] private Button lobbyTab;
    [SerializeField] private TMP_Text lobbyTabLabel;
    [SerializeField] private Button kitchenTab;
    [SerializeField] private TMP_Text kitchenTabLabel;
    [SerializeField] private Button applicantsTab;
    [SerializeField] private TMP_Text applicantsTabLabel;
    [SerializeField] private GameObject applicantsBadge;
    [SerializeField] private TMP_Text applicantsBadgeText;
    [SerializeField] private ScrollRect bodyScroll;
    [SerializeField] private RectTransform sectionsRoot;
    [SerializeField] private ManagementHRRoleSectionUI sectionPrefab;
    [SerializeField] private ManagementEmployeeCardUI cardPrefab;

    private EmployeeManager manager;
    private bool editable;

    public EmployeeDepartment CurrentDepartment { get; private set; }
    public ManagementHRView CurrentView { get; private set; }
    public RectTransform SectionsRoot => sectionsRoot;
    public Button LobbyTab => lobbyTab;
    public Button KitchenTab => kitchenTab;
    public Button ApplicantsTab => applicantsTab;
    public ScrollRect BodyScroll => bodyScroll;

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

    public void ConfigureStickyReferences(
        Button configuredApplicantsTab,
        TMP_Text configuredApplicantsTabLabel,
        GameObject configuredApplicantsBadge,
        TMP_Text configuredApplicantsBadgeText,
        ScrollRect configuredBodyScroll)
    {
        applicantsTab = configuredApplicantsTab;
        applicantsTabLabel = configuredApplicantsTabLabel;
        applicantsBadge = configuredApplicantsBadge;
        applicantsBadgeText = configuredApplicantsBadgeText;
        bodyScroll = configuredBodyScroll;
    }

    public void Bind(EmployeeManager configuredManager, bool canEdit)
    {
        if (manager != null)
            manager.ApplicantsRefreshed -= RefreshApplicantBadge;
        manager = configuredManager;
        editable = canEdit;
        if (manager != null)
            manager.ApplicantsRefreshed += RefreshApplicantBadge;
        if (lobbyTab != null)
        {
            lobbyTab.onClick.RemoveAllListeners();
            lobbyTab.onClick.AddListener(() => ShowDepartment(EmployeeDepartment.Lobby));
        }
        if (kitchenTab != null)
        {
            kitchenTab.onClick.RemoveAllListeners();
            kitchenTab.onClick.AddListener(() => ShowDepartment(EmployeeDepartment.Kitchen));
        }
        if (applicantsTab != null)
        {
            applicantsTab.onClick.RemoveAllListeners();
            applicantsTab.onClick.AddListener(ShowApplicants);
        }
        RefreshApplicantBadge();
        ShowDepartment(EmployeeDepartment.Lobby);
    }

    private void OnDestroy()
    {
        if (manager != null)
            manager.ApplicantsRefreshed -= RefreshApplicantBadge;
    }

    public void ShowDepartment(EmployeeDepartment department)
    {
        CurrentDepartment = department;
        bool lobby = department == EmployeeDepartment.Lobby;
        CurrentView = lobby ? ManagementHRView.Lobby : ManagementHRView.Kitchen;
        ClearSections();

        if (departmentTitle != null) departmentTitle.text = lobby ? "LOBBY DEPARTMENT" : "KITCHEN DEPARTMENT";
        if (departmentDescription != null)
            departmentDescription.text = (lobby
                ? "Front-of-house service roles"
                : "Kitchen staffing is limited to Chef and Barista") +
                " • Choose one active employee per role";
        SetTabVisual(lobbyTab, lobbyTabLabel, lobby);
        SetTabVisual(kitchenTab, kitchenTabLabel, !lobby);
        SetTabVisual(applicantsTab, applicantsTabLabel, false);

        BuildSections(EmployeeRoleCatalog.GetRoles(department), true, false);
        FinalizeLayout();
    }

    public void ShowApplicants()
    {
        CurrentView = ManagementHRView.Applicants;
        ClearSections();
        manager?.MarkApplicantsSeen();

        if (departmentTitle != null) departmentTitle.text = "ALL APPLICANTS";
        if (departmentDescription != null)
            departmentDescription.text = manager != null
                ? "Compare role, ratings and asking salary • Full refresh on Day " +
                  manager.ApplicantNextRefreshDay
                : "Compare every current applicant";
        SetTabVisual(lobbyTab, lobbyTabLabel, false);
        SetTabVisual(kitchenTab, kitchenTabLabel, false);
        SetTabVisual(applicantsTab, applicantsTabLabel, true);

        BuildSections(EmployeeRoleCatalog.LobbyRoles, false, true);
        BuildSections(EmployeeRoleCatalog.KitchenRoles, false, true);
        RefreshApplicantBadge();
        FinalizeLayout();
    }

    private void BuildSections(
        IReadOnlyList<EmployeeRole> roles,
        bool showEmployed,
        bool showApplicants)
    {
        if (roles == null || sectionPrefab == null || sectionsRoot == null || manager == null)
            return;

        foreach (EmployeeRole role in roles)
        {
            if (showApplicants && !showEmployed && !HasApplicantsForRole(role))
                continue;

            ManagementHRRoleSectionUI section = Instantiate(sectionPrefab, sectionsRoot);
            section.name = "Role_" + role;
            section.gameObject.SetActive(true);
            section.Bind(
                role,
                manager,
                cardPrefab,
                editable,
                RefreshCurrentView,
                showEmployed,
                showApplicants);
            section.GetComponent<UIRevealAnimation>()?.Play();
        }
    }

    private bool HasApplicantsForRole(EmployeeRole role)
    {
        if (manager == null || manager.allEmployees == null)
            return false;

        for (int i = 0; i < manager.allEmployees.Count; i++)
        {
            EmployeeData employee = manager.allEmployees[i];
            if (employee != null && !employee.hired && employee.role == role)
                return true;
        }

        return false;
    }

    private void FinalizeLayout()
    {
        Canvas.ForceUpdateCanvases();
        if (sectionsRoot != null)
        {
            // ContentSizeFitter may resolve one frame late after the role list is
            // reparented into the sticky-tab viewport. Give the ScrollRect an
            // explicit, deterministic content height now so it cannot collapse.
            VerticalLayoutGroup layout = sectionsRoot.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                // Every generated role section publishes a preferred height.
                // Let the parent own that height so sections stack instead of
                // sharing the prefab's original centered position.
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }
            float contentHeight = layout != null
                ? layout.padding.top + layout.padding.bottom
                : 0f;
            int visibleSections = 0;
            for (int i = 0; i < sectionsRoot.childCount; i++)
            {
                RectTransform child = sectionsRoot.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf)
                    continue;

                float preferred = LayoutUtility.GetPreferredHeight(child);
                if (preferred <= 0f)
                    preferred = Mathf.Max(1f, child.rect.height);
                contentHeight += preferred;
                visibleSections++;
            }
            if (layout != null && visibleSections > 1)
                contentHeight += layout.spacing * (visibleSections - 1);

            sectionsRoot.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(1f, contentHeight));
            LayoutRebuilder.ForceRebuildLayoutImmediate(sectionsRoot);
        }
        if (bodyScroll != null)
        {
            bodyScroll.StopMovement();
            bodyScroll.verticalNormalizedPosition = 1f;
        }
    }

    private void RefreshCurrentView()
    {
        if (CurrentView == ManagementHRView.Applicants)
            ShowApplicants();
        else
            ShowDepartment(CurrentDepartment);
    }

    private void RefreshApplicantBadge()
    {
        bool visible = manager != null && manager.HasUnseenApplicants;
        if (applicantsBadge != null) applicantsBadge.SetActive(visible);
        if (applicantsBadgeText != null) applicantsBadgeText.text = "!";
    }

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
