using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A compact, prefab-editable delivery slot. It only forwards unified pointer input;
/// the HUD and room controller remain authoritative for selection and storage.
/// </summary>
[DisallowMultipleComponent]
public sealed class RestockHotbarSlotUI : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private GameObject selectedBorder;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image slotBackground;
    [SerializeField, Min(1f)] private float selectedScale = 1.08f;

    private RestockFlowHUD owner;
    private ItemData item;

    public ItemData Item => item;

    public void ConfigureReferences(
        Image configuredIcon,
        TMP_Text configuredCount,
        GameObject configuredSelectedBorder,
        CanvasGroup configuredCanvasGroup)
    {
        icon = configuredIcon;
        countText = configuredCount;
        selectedBorder = configuredSelectedBorder;
        canvasGroup = configuredCanvasGroup;
    }

    public void Bind(RestockFlowHUD configuredOwner, ItemData configuredItem, int count)
    {
        owner = configuredOwner;
        item = configuredItem;

        if (icon == null)
            icon = transform.Find("Icon")?.GetComponent<Image>();
        if (countText == null)
            countText = transform.Find("Count")?.GetComponent<TMP_Text>();
        if (selectedBorder == null)
            selectedBorder = transform.Find("SelectedBorder")?.gameObject;
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (slotBackground == null)
            slotBackground = GetComponent<Image>();

        if (icon != null)
        {
            icon.sprite = item != null ? item.sprite : null;
            icon.enabled = icon.sprite != null;
            icon.preserveAspect = true;
        }

        if (countText != null)
            countText.text = "x" + Mathf.Max(0, count);

        SetSelected(false);
        SetDragging(false);
    }

    public void SetSelected(bool selected)
    {
        if (selectedBorder != null)
            selectedBorder.SetActive(selected);
        transform.localScale = Vector3.one * (selected ? selectedScale : 1f);
    }

    public void SetDragging(bool dragging)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = dragging ? 0.45f : 1f;
    }

    public void RestoreVisualState()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
        if (icon != null)
            icon.enabled = icon.sprite != null;
        if (transform.localScale.sqrMagnitude < 0.01f)
            SetSelected(selectedBorder != null && selectedBorder.activeSelf);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!eventData.dragging)
            owner?.HandleSlotClicked(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData == null || !eventData.dragging)
            RestoreVisualState();
        owner?.HandleSlotHover(this, true);
    }
    public void OnPointerExit(PointerEventData eventData) => owner?.HandleSlotHover(this, false);
    public void OnBeginDrag(PointerEventData eventData) => owner?.HandleSlotDragBegin(this, eventData);
    public void OnDrag(PointerEventData eventData) => owner?.HandleSlotDrag(this, eventData);
    public void OnEndDrag(PointerEventData eventData) => owner?.HandleSlotDragEnd(this, eventData);

    private void OnDisable()
    {
        RestoreVisualState();
    }
}
