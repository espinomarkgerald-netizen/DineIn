#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Text;
using DG.Tweening;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Runs in an isolated preview scene, without Play Mode or input-device changes.
public static class ButtonPointerRegression
{
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        Scene scene = EditorSceneManager.NewPreviewScene();
        ButtonAnimationProfile profile = ScriptableObject.CreateInstance<ButtonAnimationProfile>();
        var report = new StringBuilder();
        GameObject Make(string name, params Type[] types)
        {
            var go = new GameObject(name, types);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }
        void Check(bool value, string label)
        {
            if (!value) throw new Exception("FAIL " + label);
            report.AppendLine("PASS " + label);
        }
        try
        {
            foreach (string hudName in new[] { "CasualDiningProgressHUD", "Lobby Controls HUD", "Progress Day Time HUD", "Task HUD" })
            {
                var hud = Make(hudName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)).GetComponent<CanvasScaler>();
                hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                hud.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                hud.referenceResolution = new Vector2(1920, 1080);
                hud.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                hud.matchWidthOrHeight = 0;
                MobileUIAccessibility.ConfigureCanvasForMobile(hud);
                Check(hud.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand && hud.referenceResolution == new Vector2(1920, 1080),
                    hudName + " fits the mobile screen without changing authored coordinates");
            }
            var camera = Make("Test Camera", typeof(Camera)).GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 2;
            camera.transform.position = new Vector3(3, 2, -10);
            var canvas = Make("World Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.transform.position = new Vector3(3, 2, 0);
            canvas.transform.rotation = Quaternion.Euler(12, 8, 0);
            var rect = (RectTransform)Make("Button", typeof(RectTransform), typeof(Image), typeof(Button)).transform;
            rect.SetParent(canvas.transform, false);
            rect.sizeDelta = new Vector2(120, 30);
            Vector3 original = Vector3.one * .0029055686f;
            rect.localScale = original;
            var image = rect.GetComponent<Image>();
            var animator = rect.gameObject.AddComponent<ButtonAnimator>();
            typeof(ButtonAnimator).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(animator, null);
            profile.hoverScale = Vector3.one * .005f;
            profile.pressScale = Vector3.one * .006f;
            profile.duration = .2f;
            typeof(ButtonAnimator).GetField("profile", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(animator, profile);
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            Vector2 center = camera.WorldToScreenPoint(rect.position);
            // Outside the authored rectangle, inside the expanded animation rectangle.
            Vector2 outside = camera.WorldToScreenPoint(rect.TransformPoint(new Vector3(85, 0, 0)));
            Check(image.Raycast(center, camera), "world-space button accepts its projected center");
            Check(!image.Raycast(outside, camera), "world-space button rejects empty space");
            rect.localScale = profile.pressScale;
            Check(RectTransformUtility.RectangleContainsScreenPoint(rect, outside, camera), "regression point falls inside expanded artwork");
            Check(!image.Raycast(outside, camera), "expanded artwork cannot enlarge the raycast area");
            var label = Make("Child Graphic", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            label.transform.SetParent(rect, false);
            Check(!label.Raycast(outside, camera), "child graphics also respect the original button bounds");
            rect.localScale = original;
            // Restaurant controls live outside a graphics-free parent Button's rect.
            // That grouping component must not clip independently wired children.
            var group = Make("Grouping Button", typeof(RectTransform), typeof(Button));
            group.transform.SetParent(canvas.transform, false);
            ((RectTransform)group.transform).sizeDelta = new Vector2(.01f, .01f);
            var groupAnimator = group.AddComponent<ButtonAnimator>();
            typeof(ButtonAnimator).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(groupAnimator, null);
            rect.SetParent(group.transform, true);
            Check(image.Raycast(center, camera), "graphics-free ancestor does not reject child button center");
            Check(!image.Raycast(outside, camera), "child still rejects points outside its own bounds");
            typeof(MobileUIAccessibility).GetMethod("EnsureTouchArea", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { group.GetComponent<Button>() });
            Check(group.transform.childCount == 1, "grouping Button gets no invisible mobile hit target");
            rect.SetParent(canvas.transform, true);
            // Exercise the actual Android-only accessibility generator, which the
            // earlier pointer tests did not run. Its world-units-as-pixels error
            // created enormous invisible children, including on input fields.
            var fieldRect = (RectTransform)Make("World input field", typeof(RectTransform), typeof(Image), typeof(TMPro.TMP_InputField)).transform;
            fieldRect.SetParent(canvas.transform, false);
            fieldRect.localScale = original;
            fieldRect.sizeDelta = new Vector2(120, 30);
            var ensure = typeof(MobileUIAccessibility).GetMethod("EnsureTouchArea", BindingFlags.NonPublic | BindingFlags.Static);
            ensure.Invoke(null, new object[] { fieldRect.GetComponent<TMPro.TMP_InputField>() });
            var touchArea = fieldRect.Find("[Mobile Touch Area]") as RectTransform;
            Check(touchArea == null || touchArea.rect.width <= fieldRect.rect.width * 1.501f && touchArea.rect.height <= fieldRect.rect.height * 1.501f,
                "mobile world-space input target is bounded instead of covering the menu");
            if (touchArea != null)
            {
                Vector2 blank = camera.WorldToScreenPoint(fieldRect.TransformPoint(new Vector3(180, 0, 0)));
                Check(!RectTransformUtility.RectangleContainsScreenPoint(touchArea, blank, camera), "empty-space taps miss mobile input-field padding");
                Vector2 saved = touchArea.sizeDelta;
                ensure.Invoke(null, new object[] { fieldRect.GetComponent<TMPro.TMP_InputField>() });
                Check((touchArea.sizeDelta - saved).sqrMagnitude < .001f, "repeated mobile scans do not accumulate padding");
            }
            var physicalMethod = typeof(MobileUIAccessibility).GetMethod("GetPhysicalSize", BindingFlags.NonPublic | BindingFlags.Static);
            Vector2 physical = (Vector2)physicalMethod.Invoke(null, new object[] { fieldRect, canvas });
            camera.orthographicSize *= 2;
            Vector2 zoomed = (Vector2)physicalMethod.Invoke(null, new object[] { fieldRect, canvas });
            Check((physical * .5f - zoomed).sqrMagnitude < .01f, "world-space touch measurement follows camera zoom");
            camera.orthographicSize *= .5f;
            var touch = new ExtendedPointerEventData(null) { pointerId = 42, pointerType = UIPointerType.Touch, position = center,
                pointerCurrentRaycast = new RaycastResult { module = raycaster, gameObject = rect.gameObject },
                pointerPressRaycast = new RaycastResult { module = raycaster, gameObject = rect.gameObject } };
            void Complete() { rect.DOComplete(); image.DOComplete(); }
            animator.OnPointerEnter(touch); Complete();
            Check((rect.localScale - original).sqrMagnitude < 1e-12f, "touch enter does not create hover");
            animator.OnPointerDown(touch); Complete();
            Check((rect.localScale - profile.pressScale).sqrMagnitude < 1e-12f, "valid touch still has press feedback");
            animator.OnPointerUp(touch); Complete();
            Check((rect.localScale - original).sqrMagnitude < 1e-12f, "touch release restores normal state");
            animator.OnPointerDown(touch); Complete();
            touch.position = outside;
            animator.OnPointerExit(touch); Complete();
            animator.OnPointerUp(touch); Complete();
            Check((rect.localScale - original).sqrMagnitude < 1e-12f, "drag off and release leaves no stuck hover");
            animator.OnPointerDown(touch); Complete();
            Check((rect.localScale - original).sqrMagnitude < 1e-12f, "outside press cannot animate the wrong button");
            touch.pointerType = UIPointerType.MouseOrPen; touch.position = center;
            Check(!ButtonAnimator.IsTouchPointer(touch), "positive Input System mouse ID is not mistaken for touch");
            animator.OnPointerEnter(touch); Complete();
            Check((rect.localScale - profile.hoverScale).sqrMagnitude < 1e-12f, "desktop hover remains available");
            Check(ButtonAnimator.IsTouchPointer(new PointerEventData(null) { pointerId = 0 }), "legacy touch is recognized");
            Check(!ButtonAnimator.IsTouchPointer(new PointerEventData(null) { pointerId = -1 }), "legacy mouse is recognized");
            animator.OnPointerExit(touch); Complete();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.transform.rotation = Quaternion.identity;
            canvas.transform.localScale = Vector3.one;
            rect.position = new Vector3(200, 200, 0);
            Vector2 overlayCenter = RectTransformUtility.WorldToScreenPoint(null, rect.position);
            Vector2 overlayOutside = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(85, 0, 0)));
            rect.localScale = profile.pressScale;
            Check(image.Raycast(overlayCenter, null), "overlay button accepts its center with a null camera");
            Check(!image.Raycast(overlayOutside, null), "overlay animation also cannot expand its hit area");
            animator.enabled = false;
            // Edit-mode preview objects do not receive MonoBehaviour lifecycle callbacks.
            typeof(ButtonAnimator).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(animator, null);
            Check((rect.localScale - original).sqrMagnitude < 1e-12f, "closing a panel resets animation state");
            return report.ToString();
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }
}
#endif
