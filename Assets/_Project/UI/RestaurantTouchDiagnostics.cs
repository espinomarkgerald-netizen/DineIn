#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Development-only, one trace per touch start; no touch injection or UI mutation.
public sealed class RestaurantTouchDiagnostics : MonoBehaviour
{
    readonly List<RaycastResult> hits = new List<RaycastResult>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var go = new GameObject("Restaurant Touch Diagnostics");
        DontDestroyOnLoad(go);
        go.AddComponent<RestaurantTouchDiagnostics>();
    }
    void Update()
    {
        if (Touchscreen.current == null || EventSystem.current == null) return;
        foreach (var touch in Touchscreen.current.touches)
            if (touch.press.wasPressedThisFrame) Trace(touch.position.ReadValue());
    }
    void Trace(Vector2 position)
    {
        var selector = FindFirstObjectByType<RestaurantSelector>();
        if (selector == null) return;
        var es = EventSystem.current;
        hits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = position }, hits);
        var log = new StringBuilder("[RestaurantTouch] position=" + position + " selection=" + selector.SelectedRestaurantIndex +
            " eventSystem=" + Path(es.transform) + " module=" + es.currentInputModule + " hits=" + hits.Count);
        for (int i = 0; i < hits.Count; i++)
            log.Append("\nHIT ").Append(i).Append(" sorting=").Append(hits[i].sortingOrder).Append(" depth=").Append(hits[i].depth)
                .Append(" ").Append(Describe(hits[i].gameObject));
        foreach (var b in selector.gameObject.scene.GetRootGameObjects())
            foreach (var button in b.GetComponentsInChildren<Button>(true))
                if (button.name == "PlayButton" || button.name == "PrevButton" || button.name == "NextButton" || button.name == "Customize")
                    log.Append("\nCONTROL ").Append(Describe(button.gameObject));
        Debug.Log(log.ToString());
    }
    static string Describe(GameObject go)
    {
        var g = go.GetComponent<Graphic>();
        var b = go.GetComponentInParent<Button>();
        var c = go.GetComponentInParent<Canvas>();
        var r = go.transform as RectTransform;
        var sb = new StringBuilder(Path(go.transform));
        sb.Append(" active=").Append(go.activeInHierarchy).Append(" graphic=").Append(g == null ? "none" : g.GetType().Name)
          .Append(" raycast=").Append(g != null && g.raycastTarget).Append(" canvas=").Append(c == null ? "none" : Path(c.transform))
          .Append(" order=").Append(c == null ? 0 : c.sortingOrder)
          .Append(" button=").Append(b == null ? "none" : b.name + "/enabled=" + b.enabled + "/interactable=" + b.IsInteractable());
        if (r != null)
        {
            var corners = new Vector3[4]; r.GetWorldCorners(corners);
            var camera = c == null || c.renderMode == RenderMode.ScreenSpaceOverlay ? null : c.worldCamera;
            sb.Append(" screenCorners=");
            foreach (var corner in corners) sb.Append(RectTransformUtility.WorldToScreenPoint(camera, corner)).Append(";");
        }
        foreach (var group in go.GetComponentsInParent<CanvasGroup>(true))
            sb.Append(" group=").Append(Path(group.transform)).Append("/alpha=").Append(group.alpha)
              .Append("/interactable=").Append(group.interactable).Append("/blocks=").Append(group.blocksRaycasts)
              .Append("/ignoreParents=").Append(group.ignoreParentGroups);
        return sb.ToString();
    }
    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
}
#endif
