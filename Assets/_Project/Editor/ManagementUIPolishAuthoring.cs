#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-time, non-destructive polish migration for the reusable management UI
/// prefabs. Existing designer changes remain authoritative after migration.
/// </summary>
internal static class ManagementUIPolishAuthoring
{
    private const string CatalogPanelPath =
        "Assets/_Project/ManagementComputer/Prefabs/ManagementComputerCatalogPanel.prefab";
    private const string EquipmentSectionPath =
        "Assets/_Project/Resources/ManagementComputer/ManagementEquipmentSection.prefab";
    private const string HRPanelPath =
        "Assets/_Project/ManagementComputer/Prefabs/ManagementHRPanel.prefab";
    private const string HRSectionPath =
        "Assets/_Project/ManagementComputer/Prefabs/ManagementHRRoleSection.prefab";
    private const string EmployeeCardPath =
        "Assets/_Project/ManagementComputer/Prefabs/ManagementEmployeeCard.prefab";
    private const string FilledStarPath =
        "Assets/_Project/UI/Assets/Legacy/Icons/Star/Lit Star.png";
    private const string EmptyStarPath =
        "Assets/_Project/UI/Assets/Legacy/Icons/Star/Unlit Star.png";

    private static readonly string[] FeedbackPrefabPaths =
    {
        "Assets/_Project/ManagementComputer/Prefabs/ManagementComputerAppWindow.prefab",
        "Assets/_Project/ManagementComputer/Prefabs/ManagementComputerRow.prefab",
        "Assets/_Project/ManagementComputer/Prefabs/ManagementHRPanel.prefab",
        "Assets/_Project/ManagementComputer/Prefabs/ManagementHRRoleSection.prefab",
        "Assets/_Project/ManagementComputer/Prefabs/ManagementEmployeeCard.prefab",
        "Assets/_Project/ManagementComputer/Prefabs/ManagementComputerCatalogPanel.prefab",
        "Assets/_Project/ManagementComputer/Prefabs/ManagementComputerCatalogCard.prefab",
        "Assets/_Project/ManagementComputer/Prefabs/ManagementComputerCheckoutLine.prefab",
        "Assets/_Project/Resources/ManagementComputer/ManagementEquipmentSection.prefab",
        "Assets/_Project/Resources/ManagementComputer/ManagementEquipmentCard.prefab"
    };

    [MenuItem("Tools/Dine In/UI/Reapply Missing Management UI Polish")]
    public static void ApplyMissingPolish()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        UpgradeCatalogLayout();
        UpgradeEquipmentLayout();
        UpgradeHRLayoutAndScrollbars();
        UpgradeEmployeeRatingIcons();
        AddMissingButtonFeedback();
        AssetDatabase.SaveAssets();
    }

    private static void UpgradeCatalogLayout()
    {
        EditPrefab(CatalogPanelPath, root =>
        {
            ManagementComputerCatalogPanelUI panel =
                root.GetComponent<ManagementComputerCatalogPanelUI>();
            if (panel == null)
                return false;

            SerializedObject serialized = new SerializedObject(panel);
            bool changed = false;
            changed |= SetVector2IfMissing(serialized, "menuCardSize", new Vector2(238f, 260f));
            changed |= SetIntIfMissing(serialized, "menuMaximumColumns", 4);
            changed |= SetFloatIfMissing(serialized, "menuRightRailProportion", 0.3f);
            changed |= SetVector2IfMissing(serialized, "menuRightRailWidthRange", new Vector2(360f, 480f));
            changed |= SetVector2IfMissing(serialized, "restockCardSize", new Vector2(238f, 292f));
            changed |= SetIntIfMissing(serialized, "restockMaximumColumns", 3);
            changed |= SetFloatIfMissing(serialized, "restockRightRailProportion", 0.39f);
            changed |= SetVector2IfMissing(serialized, "restockRightRailWidthRange", new Vector2(440f, 580f));
            if (changed)
                serialized.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        });
    }

    private static void UpgradeEquipmentLayout()
    {
        EditPrefab(EquipmentSectionPath, root =>
        {
            ManagementEquipmentSectionUI section = root.GetComponent<ManagementEquipmentSectionUI>();
            if (section == null)
                return false;

            SerializedObject serialized = new SerializedObject(section);
            bool changed = false;
            changed |= ReplaceFloat(serialized, "minimumCardWidth", 280f, 238f);
            changed |= SetFloatIfMissing(serialized, "maximumCardWidth", 258f);
            changed |= ReplaceFloat(serialized, "cardHeight", 330f, 280f);
            changed |= ReplaceFloat(serialized, "horizontalSpacing", 18f, 14f);
            changed |= ReplaceFloat(serialized, "verticalSpacing", 18f, 14f);
            changed |= ReplaceFloat(serialized, "sidePadding", 14f, 12f);
            changed |= ReplaceFloat(serialized, "headerHeight", 92f, 80f);
            if (changed)
                serialized.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        });
    }

    private static void UpgradeHRLayoutAndScrollbars()
    {
        EditPrefab(HRSectionPath, root =>
        {
            bool changed = false;
            ManagementHRRoleSectionUI section = root.GetComponent<ManagementHRRoleSectionUI>();
            if (section != null)
            {
                SerializedObject serialized = new SerializedObject(section);
                changed |= ReplaceVector2(serialized, "singleRailScrollPosition", new Vector2(0f, -249f), new Vector2(0f, -230f));
                changed |= ReplaceFloat(serialized, "singleRailScrollHeight", 330f, 286f);
                changed |= ReplaceFloat(serialized, "singleRailSectionHeight", 430f, 380f);
                if (changed)
                    serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            changed |= EnsureHorizontalScrollbar(
                FindDescendant(root.transform, "EmployedScroll")?.GetComponent<ScrollRect>());
            changed |= EnsureHorizontalScrollbar(
                FindDescendant(root.transform, "ApplicantScroll")?.GetComponent<ScrollRect>());
            return changed;
        });

        EditPrefab(HRPanelPath, root =>
        {
            ScrollRect body = FindDescendant(root.transform, "StaffBodyScroll")?.GetComponent<ScrollRect>();
            return EnsureVerticalScrollbar(body);
        });
    }

    private static void UpgradeEmployeeRatingIcons()
    {
        EditPrefab(EmployeeCardPath, root =>
        {
            ManagementEmployeeCardUI card = root.GetComponent<ManagementEmployeeCardUI>();
            Transform legacyStars = FindDescendant(root.transform, "Stars");
            if (card == null || legacyStars == null || legacyStars.parent == null)
                return false;

            Sprite filled = AssetDatabase.LoadAssetAtPath<Sprite>(FilledStarPath);
            Sprite empty = AssetDatabase.LoadAssetAtPath<Sprite>(EmptyStarPath);
            if (filled == null || empty == null)
                return false;

            bool changed = false;
            Transform ratingRoot = legacyStars.parent.Find("RatingStars");
            if (ratingRoot == null)
            {
                GameObject starsObject = CreateUI("RatingStars", legacyStars.parent);
                ratingRoot = starsObject.transform;
                CopyRect(legacyStars as RectTransform, starsObject.GetComponent<RectTransform>());
                HorizontalLayoutGroup layout = starsObject.AddComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.spacing = 4f;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;

                for (int i = 0; i < 5; i++)
                {
                    GameObject starObject = CreateUI("Star " + (i + 1), ratingRoot);
                    RectTransform rect = starObject.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(22f, 22f);
                    Image image = starObject.AddComponent<Image>();
                    image.sprite = empty;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    LayoutElement element = starObject.AddComponent<LayoutElement>();
                    element.preferredWidth = 22f;
                    element.preferredHeight = 22f;
                }
                changed = true;
            }

            Image[] images = ratingRoot.GetComponentsInChildren<Image>(true);
            SerializedObject serialized = new SerializedObject(card);
            SerializedProperty ratingStars = serialized.FindProperty("ratingStars");
            SerializedProperty filledStar = serialized.FindProperty("filledStarSprite");
            SerializedProperty emptyStar = serialized.FindProperty("emptyStarSprite");
            bool referencesChanged = ratingStars == null || ratingStars.arraySize != images.Length ||
                                     filledStar == null || filledStar.objectReferenceValue != filled ||
                                     emptyStar == null || emptyStar.objectReferenceValue != empty;
            if (!referencesChanged && ratingStars != null)
            {
                for (int i = 0; i < images.Length; i++)
                {
                    if (ratingStars.GetArrayElementAtIndex(i).objectReferenceValue != images[i])
                    {
                        referencesChanged = true;
                        break;
                    }
                }
            }

            if (referencesChanged)
            {
                card.ConfigureRatingIcons(images, filled, empty);
                EditorUtility.SetDirty(card);
                changed = true;
            }
            if (legacyStars.gameObject.activeSelf)
            {
                legacyStars.gameObject.SetActive(false);
                changed = true;
            }
            return changed;
        });
    }

    private static void AddMissingButtonFeedback()
    {
        foreach (string path in FeedbackPrefabPaths)
        {
            EditPrefab(path, root =>
            {
                bool changed = false;
                Button[] buttons = root.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    Button button = buttons[i];
                    if (button != null && button.GetComponent<UISubtlePressFeedback>() == null)
                    {
                        button.gameObject.AddComponent<UISubtlePressFeedback>();
                        changed = true;
                    }
                }
                return changed;
            });
        }
    }

    private static bool EnsureHorizontalScrollbar(ScrollRect scroll)
    {
        if (scroll == null || scroll.horizontalScrollbar != null)
            return false;

        Scrollbar scrollbar = CreateScrollbar(scroll.transform, "HorizontalScrollbar", false);
        RectTransform rect = scrollbar.transform as RectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(10f, 5f);
        rect.offsetMax = new Vector2(-10f, 23f);
        scroll.horizontalScrollbar = scrollbar;
        scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scroll.horizontalScrollbarSpacing = 4f;
        if (scroll.viewport != null)
            scroll.viewport.offsetMin = new Vector2(scroll.viewport.offsetMin.x, 26f);
        return true;
    }

    private static bool EnsureVerticalScrollbar(ScrollRect scroll)
    {
        if (scroll == null || scroll.verticalScrollbar != null)
            return false;

        Scrollbar scrollbar = CreateScrollbar(scroll.transform, "VerticalScrollbar", true);
        RectTransform rect = scrollbar.transform as RectTransform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.offsetMin = new Vector2(-22f, 8f);
        rect.offsetMax = new Vector2(-4f, -8f);
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scroll.verticalScrollbarSpacing = 4f;
        if (scroll.viewport != null)
            scroll.viewport.offsetMax = new Vector2(-26f, scroll.viewport.offsetMax.y);
        return true;
    }

    private static Scrollbar CreateScrollbar(Transform parent, string name, bool vertical)
    {
        GameObject track = CreateUI(name, parent);
        Image trackImage = track.AddComponent<Image>();
        trackImage.color = new Color(0.72f, 0.81f, 0.9f, 1f);

        GameObject slidingArea = CreateUI("Sliding Area", track.transform);
        Stretch(slidingArea.GetComponent<RectTransform>(), 2f, 2f, 2f, 2f);
        GameObject handleObject = CreateUI("Handle", slidingArea.transform);
        Stretch(handleObject.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        Image handle = handleObject.AddComponent<Image>();
        handle.color = new Color(0.08f, 0.55f, 0.88f, 1f);

        Scrollbar scrollbar = track.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        scrollbar.direction = vertical
            ? Scrollbar.Direction.BottomToTop
            : Scrollbar.Direction.LeftToRight;
        scrollbar.numberOfSteps = 0;
        return scrollbar;
    }

    private static void EditPrefab(string path, System.Func<GameObject, bool> edit)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        if (contents == null)
            return;
        try
        {
            if (edit(contents))
                PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static bool ReplaceFloat(SerializedObject serialized, string name, float oldValue, float newValue)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null || !Mathf.Approximately(property.floatValue, oldValue))
            return false;
        property.floatValue = newValue;
        return true;
    }

    private static bool ReplaceVector2(SerializedObject serialized, string name, Vector2 oldValue, Vector2 newValue)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null || property.vector2Value != oldValue)
            return false;
        property.vector2Value = newValue;
        return true;
    }

    private static bool SetFloatIfMissing(SerializedObject serialized, string name, float value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null || property.floatValue > 0f)
            return false;
        property.floatValue = value;
        return true;
    }

    private static bool SetIntIfMissing(SerializedObject serialized, string name, int value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null || property.intValue > 0)
            return false;
        property.intValue = value;
        return true;
    }

    private static bool SetVector2IfMissing(SerializedObject serialized, string name, Vector2 value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null || property.vector2Value.sqrMagnitude > 0.01f)
            return false;
        property.vector2Value = value;
        return true;
    }

    private static GameObject CreateUI(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
            result.layer = uiLayer;
        return result;
    }

    private static void CopyRect(RectTransform source, RectTransform destination)
    {
        if (source == null || destination == null)
            return;
        destination.anchorMin = source.anchorMin;
        destination.anchorMax = source.anchorMax;
        destination.pivot = source.pivot;
        destination.anchoredPosition = source.anchoredPosition;
        destination.sizeDelta = source.sizeDelta;
        destination.localScale = Vector3.one;
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
