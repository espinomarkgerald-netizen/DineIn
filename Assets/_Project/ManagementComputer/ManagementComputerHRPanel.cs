using System;
using System.Collections;
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
    private Coroutine applicantFocusRoutine;
    private Coroutine scrollRestoreRoutine;

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
        CancelApplicantFocus();
        CancelScrollRestore();
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
        PlayPanelTransition();
    }

    public void ShowApplicants()
    {
        CancelApplicantFocus();
        CancelScrollRestore();
        CurrentView = ManagementHRView.Applicants;
        ClearSections();
        manager?.MarkApplicantsSeen();

        bool hasApplicants = HasAnyApplicants();

        if (departmentTitle != null)
            departmentTitle.text = hasApplicants ? "ALL APPLICANTS" : "NO APPLICANTS AVAILABLE";
        if (departmentDescription != null)
            departmentDescription.text = !hasApplicants && manager != null
                ? "The applicant pool is empty • New applicants arrive on Day " +
                  manager.ApplicantNextRefreshDay
                : manager != null
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
        PlayPanelTransition();
    }

    public void ShowApplicantsForRole(EmployeeRole role)
    {
        bool hasMatchingApplicant = HasApplicantsForRole(role);
        ShowApplicants();

        if (!hasMatchingApplicant)
        {
            if (departmentTitle != null) departmentTitle.text = "NO MATCHING APPLICANTS";
            if (departmentDescription != null)
                departmentDescription.text = manager != null
                    ? $"No {role} applicant is currently available • New applicants refresh on Day {manager.ApplicantNextRefreshDay}"
                    : $"No {role} applicant is currently available";
            return;
        }

        applicantFocusRoutine = StartCoroutine(FocusApplicantsAfterLayout(role));
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
                showApplicants,
                ShowApplicantsForRole);
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

    private bool HasAnyApplicants()
    {
        if (manager == null || manager.allEmployees == null)
            return false;

        for (int i = 0; i < manager.allEmployees.Count; i++)
        {
            EmployeeData employee = manager.allEmployees[i];
            if (employee != null && !employee.hired)
                return true;
        }

        return false;
    }

    private IEnumerator FocusApplicantsAfterLayout(EmployeeRole role)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(sectionsRoot);
        Canvas.ForceUpdateCanvases();

        ManagementHRRoleSectionUI matchingSection = null;
        ManagementHRRoleSectionUI[] sections = sectionsRoot.GetComponentsInChildren<ManagementHRRoleSectionUI>(false);
        for (int i = 0; i < sections.Length; i++)
        {
            if (sections[i].Role == role)
            {
                matchingSection = sections[i];
                break;
            }
        }

        if (matchingSection == null)
        {
            applicantFocusRoutine = null;
            yield break;
        }

        ScrollSectionIntoView(matchingSection.transform as RectTransform);
        if (matchingSection.ApplicantScroll != null)
        {
            matchingSection.ApplicantScroll.StopMovement();
            matchingSection.ApplicantScroll.horizontalNormalizedPosition = 0f;
        }

        yield return new WaitForSecondsRealtime(0.22f);
        ManagementEmployeeCardUI[] cards = matchingSection.ApplicantContent
            .GetComponentsInChildren<ManagementEmployeeCardUI>(false);
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i].Employee != null && cards[i].Employee.role == role)
                cards[i].PlayAttentionBop();
        }

        applicantFocusRoutine = null;
    }

    private void ScrollSectionIntoView(RectTransform sectionRect)
    {
        if (sectionRect == null || bodyScroll == null || bodyScroll.content == null)
            return;

        RectTransform viewport = bodyScroll.viewport != null
            ? bodyScroll.viewport
            : bodyScroll.transform as RectTransform;
        if (viewport == null)
            return;

        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, sectionRect);
        float deltaY = viewport.rect.center.y - bounds.center.y;
        Vector2 position = bodyScroll.content.anchoredPosition;
        position.y += deltaY;
        float maximumY = Mathf.Max(0f, bodyScroll.content.rect.height - viewport.rect.height);
        position.y = Mathf.Clamp(position.y, 0f, maximumY);
        bodyScroll.StopMovement();
        bodyScroll.content.anchoredPosition = position;
    }

    private void CancelApplicantFocus()
    {
        if (applicantFocusRoutine == null)
            return;

        StopCoroutine(applicantFocusRoutine);
        applicantFocusRoutine = null;
    }

    private IEnumerator RestoreScrollAfterLayout(float normalizedPosition)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (sectionsRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(sectionsRoot);
        RestoreBodyScrollPosition(normalizedPosition);
        scrollRestoreRoutine = null;
    }

    private void RestoreBodyScrollPosition(float normalizedPosition)
    {
        if (bodyScroll == null)
            return;

        bodyScroll.StopMovement();
        bodyScroll.verticalNormalizedPosition = Mathf.Clamp01(normalizedPosition);
    }

    private void CancelScrollRestore()
    {
        if (scrollRestoreRoutine == null)
            return;

        StopCoroutine(scrollRestoreRoutine);
        scrollRestoreRoutine = null;
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
        {
            float previousPosition = bodyScroll != null
                ? bodyScroll.verticalNormalizedPosition
                : 1f;
            ShowApplicants();
            RestoreBodyScrollPosition(previousPosition);
            scrollRestoreRoutine = StartCoroutine(RestoreScrollAfterLayout(previousPosition));
        }
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

    private void PlayPanelTransition()
    {
        GetComponent<UIRevealAnimation>()?.Play();
    }
}
