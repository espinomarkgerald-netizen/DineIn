using UnityEngine;

namespace DineIn.Appearance
{
    // Layout only: the existing panel still owns option filtering, selection and persistence.
    [AddComponentMenu("Layout/Cosmetic Option Layout")]
    public sealed class CosmeticOptionLayout : UnityEngine.UI.LayoutGroup
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private Vector2 cardSize = new(138, 174);
        [SerializeField] private Vector2 swatchSize = new(52, 58);
        [SerializeField] private Vector2 spacing = new(14, 14);
        [SerializeField, Range(1, 8)] private int paletteColumns = 4;
        private bool palette;
        private int columns, rows;
        private Vector2 cell;
        private float totalHeight;
        public int Columns => columns;
        public Vector2 CellSize => cell;
        public void SetPalette(bool value) { if (palette == value) return; palette = value; SetDirty(); }
        public void Refresh() => SetDirty();
        private void Measure()
        {
            int count = rectChildren.Count;
            float width = Mathf.Max(1, rectTransform.rect.width - padding.horizontal);
            cell = palette ? swatchSize : cardSize;
            // Authored sizes never shrink to avoid scrolling. Narrow layouts wrap to one column.
            columns = Mathf.Max(1, Mathf.Min(Mathf.Max(1, count), palette ? paletteColumns : 2,
                Mathf.FloorToInt((width + spacing.x) / (cell.x + spacing.x))));
            rows = Mathf.CeilToInt((float)count / columns);
            totalHeight = rows * cell.y + Mathf.Max(0, rows - 1) * spacing.y + padding.vertical;
        }
        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal(); Measure();
            SetLayoutInputForAxis(0, 0, -1, 0);
        }
        public override void CalculateLayoutInputVertical()
        {
            Measure();
            float height = Mathf.Max(totalHeight, viewport != null ? viewport.rect.height : 0);
            SetLayoutInputForAxis(height, height, -1, 1);
        }
        public override void SetLayoutHorizontal() => Arrange(0);
        public override void SetLayoutVertical() => Arrange(1);
        private void Arrange(int axis)
        {
            Measure();
            float top = padding.top + Mathf.Max(0, rectTransform.rect.height - totalHeight) * .5f;
            for (int i = 0; i < rectChildren.Count; i++)
            {
                int row = i / columns, col = i % columns;
                int rowCount = Mathf.Min(columns, rectChildren.Count - row * columns);
                float rowWidth = rowCount * cell.x + (rowCount - 1) * spacing.x;
                float left = padding.left + (rectTransform.rect.width - padding.horizontal - rowWidth) * .5f;
                SetChildAlongAxis(rectChildren[i], axis, axis == 0 ? left + col * (cell.x + spacing.x) : top + row * (cell.y + spacing.y), axis == 0 ? cell.x : cell.y);
            }
        }
    }
}
