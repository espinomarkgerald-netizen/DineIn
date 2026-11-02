using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Transient scroll geometry for synchronous in-place updates, never save data.
public sealed class ManagementComputerScrollAnchor
{
    private sealed class Entry
    {
        public ScrollRect scroll;
        public Vector2 offset;
        public readonly List<RectTransform> items = new();
        public readonly List<Vector2> points = new();
    }
    private readonly List<Entry> entries = new();
    public ManagementComputerScrollAnchor(Transform root)
    {
        if (root == null) return;
        foreach (var scroll in root.GetComponentsInChildren<ScrollRect>(false))
        {
            if (scroll.content == null || scroll.viewport == null) continue;
            var entry = new Entry { scroll = scroll, offset = scroll.content.anchoredPosition };
            var children = new List<RectTransform>(); int first = -1;
            foreach (Transform child in scroll.content)
            {
                if (child is not RectTransform rect || !rect.gameObject.activeSelf) continue;
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, rect);
                if (first < 0 && scroll.viewport.rect.Overlaps(new Rect(bounds.min, bounds.size))) first = children.Count;
                children.Add(rect);
            }
            if (first < 0) first = 0;
            for (int distance = 0; distance < children.Count; distance++)
            {
                int a = first + distance, b = first - distance;
                if (a < children.Count) Capture(entry, children[a]);
                if (distance > 0 && b >= 0) Capture(entry, children[b]);
            }
            entries.Add(entry);
        }
    }
    private static void Capture(Entry entry, RectTransform item)
    {
        entry.items.Add(item);
        entry.points.Add(entry.scroll.viewport.InverseTransformPoint(item.position));
    }
    public void Restore()
    {
        Canvas.ForceUpdateCanvases();
        foreach (var entry in entries)
        {
            var scroll = entry.scroll;
            if (scroll == null || !scroll.isActiveAndEnabled || scroll.content == null) continue;
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            Vector2 destination = entry.offset;
            for (int i = 0; i < entry.items.Count; i++)
            {
                var item = entry.items[i];
                if (item == null || !item.gameObject.activeInHierarchy || !item.IsChildOf(scroll.content)) continue;
                Vector2 now = scroll.viewport.InverseTransformPoint(item.position);
                Vector3 delta = scroll.content.parent.InverseTransformVector(scroll.viewport.TransformVector(entry.points[i] - now));
                destination = scroll.content.anchoredPosition + new Vector2(scroll.horizontal ? delta.x : 0, scroll.vertical ? delta.y : 0);
                break;
            }
            scroll.content.anchoredPosition = destination;
            // ScrollRect computes bounds from current content geometry; clamp at removed-list edges.
            if (scroll.horizontal) scroll.horizontalNormalizedPosition = Mathf.Clamp01(scroll.horizontalNormalizedPosition);
            if (scroll.vertical) scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition);
        }
    }
}
