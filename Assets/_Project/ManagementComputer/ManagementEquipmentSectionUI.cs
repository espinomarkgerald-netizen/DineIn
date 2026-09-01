using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scrollable equipment-catalog section with an authored divider and a
/// responsive card grid. Card size and spacing are editable on the prefab.
/// </summary>
[ExecuteAlways]
public sealed class ManagementEquipmentSectionUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private Image divider;
    [SerializeField] private RectTransform cardsContainer;
    [SerializeField] private GridLayoutGroup grid;
    [SerializeField] private LayoutElement sectionLayout;
    [SerializeField] private ManagementEquipmentCardUI cardPrefab;

    [Header("Responsive Grid")]
    [SerializeField, Min(220f)] private float minimumCardWidth = 238f;
    [SerializeField, Min(220f)] private float maximumCardWidth = 258f;
    [SerializeField, Min(240f)] private float cardHeight = 280f;
    [SerializeField, Min(0f)] private float horizontalSpacing = 14f;
    [SerializeField, Min(0f)] private float verticalSpacing = 14f;
    [SerializeField, Min(0f)] private float sidePadding = 12f;
    [SerializeField, Min(0f)] private float headerHeight = 80f;
    [SerializeField, Range(1, 4)] private int maximumColumns = 4;

    private int itemCount;
    private float lastWidth = -1f;

    public void ConfigureReferences(
        TMP_Text configuredTitle,
        TMP_Text configuredSubtitle,
        Image configuredDivider,
        RectTransform configuredCardsContainer,
        GridLayoutGroup configuredGrid,
        LayoutElement configuredLayout,
        ManagementEquipmentCardUI configuredCardPrefab)
    {
        titleText = configuredTitle;
        subtitleText = configuredSubtitle;
        divider = configuredDivider;
        cardsContainer = configuredCardsContainer;
        grid = configuredGrid;
        sectionLayout = configuredLayout;
        cardPrefab = configuredCardPrefab;
    }

    public void Bind(string title, string subtitle)
    {
        if (titleText != null) titleText.text = title ?? string.Empty;
        if (subtitleText != null) subtitleText.text = subtitle ?? string.Empty;
        Reflow(true);
    }

    public ManagementEquipmentCardUI AddCard()
    {
        if (cardPrefab == null || cardsContainer == null)
            return null;

        ManagementEquipmentCardUI card = Instantiate(cardPrefab, cardsContainer);
        card.gameObject.SetActive(true);
        itemCount++;
        Reflow(true);
        return card;
    }

    private void OnEnable()
    {
        itemCount = cardsContainer != null ? cardsContainer.childCount : 0;
        Reflow(true);
    }

    private void OnRectTransformDimensionsChange()
    {
        Reflow(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Reflow(true);
    }
#endif

    public void Reflow(bool force)
    {
        if (cardsContainer == null || grid == null || sectionLayout == null)
            return;

        RectTransform root = transform as RectTransform;
        float width = root != null ? root.rect.width : 0f;
        if (width <= 1f && root != null && root.parent is RectTransform parent)
            width = parent.rect.width;
        if (width <= 1f)
            width = 900f;
        if (!force && Mathf.Abs(lastWidth - width) < 0.5f)
            return;
        lastWidth = width;

        float available = Mathf.Max(minimumCardWidth, width - sidePadding * 2f);
        int columns = Mathf.Clamp(
            Mathf.FloorToInt((available + horizontalSpacing) /
                             (minimumCardWidth + horizontalSpacing)),
            1,
            maximumColumns);
        float availablePerCard =
            (available - horizontalSpacing * (columns - 1)) / columns;
        float cardWidth = Mathf.Min(
            Mathf.Max(minimumCardWidth, maximumCardWidth),
            availablePerCard);
        int count = Application.isPlaying
            ? itemCount
            : cardsContainer.childCount;
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
        float cardsHeight = rows * cardHeight + Mathf.Max(0, rows - 1) * verticalSpacing;

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.cellSize = new Vector2(cardWidth, cardHeight);
        grid.spacing = new Vector2(horizontalSpacing, verticalSpacing);
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.padding = new RectOffset(
            Mathf.RoundToInt(sidePadding),
            Mathf.RoundToInt(sidePadding),
            0,
            0);

        cardsContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cardsHeight);
        float sectionHeight = headerHeight + cardsHeight + 22f;
        sectionLayout.minHeight = sectionHeight;
        sectionLayout.preferredHeight = sectionHeight;
        if (root != null)
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, sectionHeight);
        LayoutRebuilder.MarkLayoutForRebuild(cardsContainer);
        if (root != null)
            LayoutRebuilder.MarkLayoutForRebuild(root);
    }
}
