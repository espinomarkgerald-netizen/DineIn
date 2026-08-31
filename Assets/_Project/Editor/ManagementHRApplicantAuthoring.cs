#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-time, non-destructive migration that gives the Staff app a sticky tab
/// bar, an Applicants tab, and new-applicant badges. Existing card prefabs and
/// direct designer edits remain authoritative.
/// </summary>
[InitializeOnLoad]
internal static class ManagementHRApplicantAuthoring
{
    private const string HRPrefabPath =
        "Assets/_Project/ManagementComputer/Prefabs/ManagementHRPanel.prefab";
    private const string DesktopPrefabPath =
        "Assets/_Project/ManagementComputer/Prefabs/ManagementComputerDesktop.prefab";
    private const string FontPath =
        "Assets/_Project/UI/Assets/Legacy/Fonts/Fredoka,Lilita_One/Fredoka/Fredoka-VariableFont_wdth,wght SDF.asset";
    private const string BadgeSpritePath =
        "Assets/_Project/MainMenu/NewDesign/UI Elements/PNG/Red/Double/button_square_line.png";

    static ManagementHRApplicantAuthoring()
    {
        EditorApplication.delayCall += UpgradeMissingAuthoring;
    }

    [MenuItem("Tools/Dine In/Management/Upgrade Staff Applicants UI")]
    public static void UpgradeMissingAuthoring()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        UpgradeHRPanel();
        UpgradeDesktopBadge();
        AssetDatabase.SaveAssets();
    }

    private static void UpgradeHRPanel()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(HRPrefabPath);
        if (contents == null)
            return;

        bool changed = false;
        try
        {
            ManagementComputerHRPanel panel = contents.GetComponent<ManagementComputerHRPanel>();
            if (panel == null)
                return;

            Button lobbyTab = FindDescendant(contents.transform, "LobbyDepartmentTab")?.GetComponent<Button>();
            Button kitchenTab = FindDescendant(contents.transform, "KitchenDepartmentTab")?.GetComponent<Button>();
            RectTransform sections = FindDescendant(contents.transform, "RoleSections") as RectTransform;
            if (lobbyTab == null || kitchenTab == null || sections == null)
                return;

            Button applicantsTab = FindDescendant(contents.transform, "ApplicantsDepartmentTab")?.GetComponent<Button>();
            if (applicantsTab == null)
            {
                GameObject clone = Object.Instantiate(kitchenTab.gameObject, contents.transform);
                clone.name = "ApplicantsDepartmentTab";
                applicantsTab = clone.GetComponent<Button>();
                RectTransform rect = clone.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(580f, -38f);
                TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.name = "ApplicantsTabLabel";
                    label.text = "APPLICANTS";
                }
                changed = true;
            }

            TMP_Text applicantsLabel = applicantsTab.GetComponentInChildren<TMP_Text>(true);
            GameObject applicantsBadge = EnsureBadge(applicantsTab.transform, out TMP_Text applicantsBadgeText);

            ScrollRect bodyScroll = FindDescendant(contents.transform, "StaffBodyScroll")?.GetComponent<ScrollRect>();
            if (bodyScroll == null)
            {
                GameObject scrollObject = CreateUI("StaffBodyScroll", contents.transform);
                RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
                scrollRect.anchorMin = Vector2.zero;
                scrollRect.anchorMax = Vector2.one;
                scrollRect.offsetMin = new Vector2(8f, 8f);
                scrollRect.offsetMax = new Vector2(-8f, -108f);
                Image background = scrollObject.AddComponent<Image>();
                background.color = new Color(1f, 1f, 1f, 0.012f);
                background.raycastTarget = true;

                bodyScroll = scrollObject.AddComponent<ScrollRect>();
                bodyScroll.horizontal = false;
                bodyScroll.vertical = true;
                bodyScroll.movementType = ScrollRect.MovementType.Elastic;
                bodyScroll.elasticity = 0.08f;
                bodyScroll.inertia = true;
                bodyScroll.decelerationRate = 0.12f;
                bodyScroll.scrollSensitivity = 0f;
                scrollObject.AddComponent<SmoothScrollRectInput>();

                GameObject viewportObject = CreateUI("Viewport", scrollObject.transform);
                RectTransform viewport = viewportObject.GetComponent<RectTransform>();
                Stretch(viewport, 0f, 0f, 0f, 0f);
                Image viewportImage = viewportObject.AddComponent<Image>();
                viewportImage.color = new Color(1f, 1f, 1f, 0.002f);
                viewportImage.raycastTarget = true;
                viewportObject.AddComponent<RectMask2D>();

                sections.SetParent(viewport, false);
                sections.anchorMin = new Vector2(0f, 1f);
                sections.anchorMax = new Vector2(1f, 1f);
                sections.pivot = new Vector2(0.5f, 1f);
                sections.anchoredPosition = Vector2.zero;
                sections.sizeDelta = Vector2.zero;
                bodyScroll.viewport = viewport;
                bodyScroll.content = sections;
                changed = true;
            }

            RectTransform title = FindDescendant(contents.transform, "DepartmentTitle") as RectTransform;
            RectTransform description = FindDescendant(contents.transform, "DepartmentDescription") as RectTransform;
            if (title != null && title.offsetMin.x < 700f)
            {
                title.offsetMin = new Vector2(710f, title.offsetMin.y);
                changed = true;
            }
            if (description != null && description.offsetMin.x < 700f)
            {
                description.offsetMin = new Vector2(710f, description.offsetMin.y);
                changed = true;
            }

            panel.ConfigureStickyReferences(
                applicantsTab,
                applicantsLabel,
                applicantsBadge,
                applicantsBadgeText,
                bodyScroll);
            EditorUtility.SetDirty(panel);
            changed = true;

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(contents, HRPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void UpgradeDesktopBadge()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(DesktopPrefabPath);
        if (contents == null)
            return;

        try
        {
            Transform staffButton = FindDescendant(contents.transform, "AppButton_1") ??
                                    FindDescendant(contents.transform, "STAFF");
            if (staffButton == null || staffButton.Find("NewApplicantBadge") != null)
                return;

            EnsureBadge(staffButton, out _);
            PrefabUtility.SaveAsPrefabAsset(contents, DesktopPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static GameObject EnsureBadge(Transform parent, out TMP_Text badgeText)
    {
        Transform existing = parent.Find("NewApplicantBadge");
        if (existing != null)
        {
            badgeText = existing.GetComponentInChildren<TMP_Text>(true);
            return existing.gameObject;
        }

        GameObject badge = CreateUI("NewApplicantBadge", parent);
        RectTransform rect = badge.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-10f, -8f);
        rect.sizeDelta = new Vector2(48f, 48f);
        Image image = badge.AddComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BadgeSpritePath);
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;

        GameObject textObject = CreateUI("BadgeText", badge.transform);
        Stretch(textObject.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        badgeText = textObject.AddComponent<TextMeshProUGUI>();
        badgeText.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        badgeText.text = "!";
        badgeText.fontSize = 30f;
        badgeText.fontStyle = FontStyles.Bold;
        badgeText.alignment = TextAlignmentOptions.Center;
        badgeText.color = Color.white;
        badgeText.raycastTarget = false;
        badge.SetActive(false);
        return badge;
    }

    private static GameObject CreateUI(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) result.layer = uiLayer;
        return result;
    }

    private static void Stretch(RectTransform rect, float left, float right, float bottom, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;
        foreach (Transform child in root)
        {
            Transform found = FindDescendant(child, name);
            if (found != null)
                return found;
        }
        return null;
    }
}
#endif
