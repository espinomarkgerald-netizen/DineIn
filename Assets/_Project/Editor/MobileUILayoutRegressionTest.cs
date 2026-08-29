#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class MobileUILayoutRegressionTest
{
    [MenuItem("Tools/Dine In/Validate Mobile UI Scaling")]
    public static void Run()
    {
        ValidateReferenceResolution(new Vector2(1920f, 1080f));
        ValidateReferenceResolution(new Vector2(800f, 450f));
        ValidateReferenceResolution(new Vector2(800f, 600f));
        Debug.Log(
            "[MobileUILayoutRegressionTest] PASS — authored canvas coordinates are preserved " +
            "and mobile screen-space UI uses non-cropping Expand scaling.");
    }

    private static void ValidateReferenceResolution(Vector2 authoredReference)
    {
        GameObject root = new GameObject(
            "Mobile UI Scale Test",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));

        try
        {
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = authoredReference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            MobileUIAccessibility.ConfigureCanvasForMobile(scaler);

            Assert(scaler.referenceResolution == authoredReference,
                $"Mobile policy changed {authoredReference.x} x {authoredReference.y} canvas coordinates.");
            Assert(scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand,
                "Mobile policy did not select non-cropping Expand scaling.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
