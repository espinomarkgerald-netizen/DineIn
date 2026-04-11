using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Attach to any root GameObject in the Office scene.
/// On Start, dumps a full input/canvas/blocker diagnostic to the console.
/// Press D at runtime to re-run the diagnostic at any time.
/// </summary>
public class OfficeInputDiagnostic : MonoBehaviour
{
    private void Start()
    {
        RunDiagnostic("AUTO (Office Start)");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.D))
            RunDiagnostic("MANUAL (D key)");
    }

    private void RunDiagnostic(string trigger)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine($"========== [OfficeInputDiagnostic] trigger={trigger} ==========");

        // ── 1. EventSystem ────────────────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("── 1. EVENT SYSTEMS ──");
        var allES = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"Count: {allES.Length}");
        foreach (var es in allES)
        {
            bool isPersistent = es.gameObject.scene.name == "DontDestroyOnLoad";
            sb.AppendLine($"  [{(es.isActiveAndEnabled ? "ACTIVE" : "INACTIVE")}] '{es.gameObject.name}'" +
                          $" scene='{es.gameObject.scene.name}'" +
                          $" persistent={isPersistent}" +
                          $" module={es.currentInputModule?.GetType().Name ?? "none"}");
        }

        // ── 2. All Canvases ───────────────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("── 2. CANVASES ──");
        var allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"Count: {allCanvases.Length}");
        foreach (var c in allCanvases)
        {
            bool isPersistent = c.gameObject.scene.name == "DontDestroyOnLoad";
            var gr = c.GetComponent<GraphicRaycaster>();
            sb.AppendLine($"  [{(c.isActiveAndEnabled ? "ACTIVE" : "inactive")}] '{GetPath(c.gameObject)}'" +
                          $" scene='{c.gameObject.scene.name}'" +
                          $" persistent={isPersistent}" +
                          $" renderMode={c.renderMode}" +
                          $" sortOrder={c.sortingOrder}" +
                          $" raycaster={(gr != null ? (gr.enabled ? "ON" : "OFF(disabled)") : "NONE")}");
        }

        // ── 3. Persistent (DontDestroyOnLoad) objects ─────────────────────────
        sb.AppendLine();
        sb.AppendLine("── 3. DONTDESTROYONLOAD OBJECTS ──");
        var dontDestroyScene = GetDontDestroyOnLoadScene();
        if (dontDestroyScene.IsValid())
        {
            var roots = dontDestroyScene.GetRootGameObjects();
            sb.AppendLine($"Root count: {roots.Length}");
            foreach (var r in roots)
            {
                sb.AppendLine($"  [{(r.activeSelf ? "ACTIVE" : "inactive")}] '{r.name}'" +
                              $" hasCanvas={r.GetComponentInChildren<Canvas>(true) != null}" +
                              $" hasGR={r.GetComponentInChildren<GraphicRaycaster>(true) != null}" +
                              $" hasImage={r.GetComponentInChildren<Image>(true) != null}");
                // List any Canvas children
                foreach (var c in r.GetComponentsInChildren<Canvas>(true))
                {
                    var gr = c.GetComponent<GraphicRaycaster>();
                    sb.AppendLine($"      Canvas '{GetPath(c.gameObject)}'" +
                                  $" active={c.isActiveAndEnabled}" +
                                  $" sortOrder={c.sortingOrder}" +
                                  $" raycaster={(gr != null ? (gr.enabled ? "ON" : "OFF") : "NONE")}");
                }
            }
        }
        else
        {
            sb.AppendLine("  DontDestroyOnLoad scene not accessible (no persistent objects).");
        }

        // ── 4. Full-screen potential blockers ─────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("── 4. FULLSCREEN / BLOCKING CANDIDATES ──");
        var images = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var img in images)
        {
            if (!img.raycastTarget) continue;
            var rt = img.rectTransform;
            // Full-screen: anchors span 0→1 in both axes
            bool isFullscreen = rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one;
            if (!isFullscreen) continue;

            bool isPersistent = img.gameObject.scene.name == "DontDestroyOnLoad";
            bool isActive = img.isActiveAndEnabled;
            sb.AppendLine($"  [{(isActive ? "ACTIVE **BLOCKER**" : "inactive")}] '{GetPath(img.gameObject)}'" +
                          $" scene='{img.gameObject.scene.name}'" +
                          $" persistent={isPersistent}" +
                          $" alpha={img.color.a:F2}" +
                          $" raycastTarget={img.raycastTarget}");
        }

        var canvasGroups = FindObjectsByType<CanvasGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var cg in canvasGroups)
        {
            if (cg.blocksRaycasts && cg.isActiveAndEnabled && cg.alpha > 0f)
            {
                bool isPersistent = cg.gameObject.scene.name == "DontDestroyOnLoad";
                sb.AppendLine($"  [ACTIVE CANVASGROUP blocks] '{GetPath(cg.gameObject)}'" +
                              $" scene='{cg.gameObject.scene.name}'" +
                              $" persistent={isPersistent}" +
                              $" alpha={cg.alpha:F2}" +
                              $" interactable={cg.interactable}");
            }
        }

        // ── 5. Office buttons health check ────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("── 5. OFFICE BUTTONS ──");
        var allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int officeButtonCount = 0;
        foreach (var btn in allButtons)
        {
            string sceneName = btn.gameObject.scene.name;
            if (sceneName != "Office" && sceneName != "DontDestroyOnLoad") continue;
            officeButtonCount++;

            // Check parent CanvasGroups
            var cgList = new List<CanvasGroup>();
            var t = btn.transform;
            while (t != null) { var cg = t.GetComponent<CanvasGroup>(); if (cg != null) cgList.Add(cg); t = t.parent; }

            bool cgBlocks = false;
            bool cgInteractable = true;
            foreach (var cg in cgList)
            {
                if (!cg.interactable) cgInteractable = false;
                if (!cg.blocksRaycasts) cgBlocks = true;
            }

            var canvas = btn.GetComponentInParent<Canvas>();
            var gr = canvas != null ? canvas.GetComponent<GraphicRaycaster>() : null;

            sb.AppendLine($"  [{(btn.isActiveAndEnabled ? "ACTIVE" : "inactive")}] '{GetPath(btn.gameObject)}'" +
                          $" interactable={btn.interactable}" +
                          $" cgInteractable={cgInteractable}" +
                          $" cgBlocksRaycasts={!cgBlocks}" +
                          $" canvas='{(canvas != null ? canvas.name : "NONE")}'" +
                          $" canvasActive={canvas?.isActiveAndEnabled}" +
                          $" graphicRaycaster={(gr != null ? (gr.enabled ? "ON" : "DISABLED") : "MISSING")}");
        }
        if (officeButtonCount == 0)
            sb.AppendLine("  No buttons found in Office or DontDestroyOnLoad scenes.");

        // ── 6. Raycast hit at screen center ───────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("── 6. POINTER RAYCAST AT SCREEN CENTER ──");
        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
        };
        var results = new List<RaycastResult>();
        EventSystem.current?.RaycastAll(pointerData, results);
        if (results.Count == 0)
        {
            sb.AppendLine("  Nothing hit at screen center.");
        }
        else
        {
            sb.AppendLine($"  {results.Count} hit(s) — topmost first:");
            foreach (var r in results)
            {
                bool isPersistent = r.gameObject.scene.name == "DontDestroyOnLoad";
                sb.AppendLine($"    depth={r.depth} sort={r.sortingOrder} '{GetPath(r.gameObject)}'" +
                              $" scene='{r.gameObject.scene.name}' persistent={isPersistent}");
            }
        }

        sb.AppendLine("==========================================================");
        Debug.Log(sb.ToString());
    }

    private static string GetPath(GameObject go)
    {
        if (go == null) return "<null>";
        var parts = new List<string>();
        var t = go.transform;
        while (t != null) { parts.Insert(0, t.name); t = t.parent; }
        return string.Join("/", parts);
    }

    private static Scene GetDontDestroyOnLoadScene()
    {
        // Unity 6: create a temporary DontDestroyOnLoad object to get scene reference
        var temp = new GameObject("__TempDDOL__");
        DontDestroyOnLoad(temp);
        var scene = temp.scene;
        Destroy(temp);
        return scene;
    }
}
