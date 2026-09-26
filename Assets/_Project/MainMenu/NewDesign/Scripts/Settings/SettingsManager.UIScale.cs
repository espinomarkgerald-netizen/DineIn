using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DineIn.NewMenu
{
    public partial class SettingsManager
    {
        private sealed class ScaleBaseline
        {
            public CanvasScaler scaler;
            public Vector2 reference, appliedReference;
            public float pixels, appliedPixels;
            public CanvasScaler.ScaleMode mode;
        }
        private readonly List<ScaleBaseline> uiScales = new List<ScaleBaseline>();
        private float nextUIScan;

        // Scale root canvases only. Nested canvases inherit their parent's scale.
        // Observe authored/mobile layout changes without multiplying our own result again.
        private void ApplyGlobalUIScale(bool discover = false)
        {
            if (!Application.isPlaying) return;
            if (discover || Time.unscaledTime >= nextUIScan)
            {
                nextUIScan = Time.unscaledTime + 1f;
                foreach (var scaler in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    RegisterCanvas(scaler);
            }
            for (int i = uiScales.Count - 1; i >= 0; i--)
            {
                var b = uiScales[i];
                if (b.scaler == null) { uiScales.RemoveAt(i); continue; }
                ApplyCanvasScale(b);
            }
        }

        private ScaleBaseline RegisterCanvas(CanvasScaler scaler)
        {
            if(scaler==null)return null;
            var canvas=scaler.GetComponent<Canvas>();
            // isRootCanvas is false on some inactive canvases. Include closed menus
            // now so they already have the correct scale when first opened.
            var parentCanvas=scaler.transform.parent!=null?scaler.transform.parent.GetComponentInParent<Canvas>(true):null;
            if(parentCanvas!=null || canvas.renderMode==RenderMode.WorldSpace)return null;
            var baseline=uiScales.Find(b=>b.scaler==scaler);
            if(baseline!=null)return baseline;
            baseline=new ScaleBaseline {scaler=scaler,reference=scaler.referenceResolution,appliedReference=scaler.referenceResolution,
                pixels=scaler.scaleFactor,appliedPixels=scaler.scaleFactor,mode=scaler.uiScaleMode};
            uiScales.Add(baseline);return baseline;
        }

        private void ApplyCanvasScale(ScaleBaseline b)
        {
            if (b.scaler.uiScaleMode != b.mode || b.scaler.referenceResolution != b.appliedReference) b.reference = b.scaler.referenceResolution;
            if (b.scaler.uiScaleMode != b.mode || !Mathf.Approximately(b.scaler.scaleFactor, b.appliedPixels)) b.pixels = b.scaler.scaleFactor;
            b.mode = b.scaler.uiScaleMode;
            if (b.mode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                b.scaler.referenceResolution = b.appliedReference = b.reference / Current.uiScale;
            else if (b.mode == CanvasScaler.ScaleMode.ConstantPixelSize)
                b.scaler.scaleFactor = b.appliedPixels = b.pixels * Current.uiScale;
        }

        // Also usable by a UI presenter immediately after creating a canvas, and by isolated Editor checks.
        public void ApplyUIScaleToCanvas(CanvasScaler scaler)
        {
            var baseline=RegisterCanvas(scaler);
            if(baseline!=null)ApplyCanvasScale(baseline);
        }

        public static float UnscaledCanvasFactor(Canvas source)
        {
            if(source==null)return 1f;
            var scaler=source.rootCanvas.GetComponent<CanvasScaler>();
            if(scaler==null)return source.scaleFactor;
            var baseline=Instance!=null?Instance.uiScales.Find(b=>b.scaler==scaler):null;
            if(scaler.uiScaleMode==CanvasScaler.ScaleMode.ConstantPixelSize)return baseline!=null?baseline.pixels:scaler.scaleFactor;
            if(scaler.uiScaleMode!=CanvasScaler.ScaleMode.ScaleWithScreenSize)return source.scaleFactor;
            Vector2 reference=baseline!=null?baseline.reference:scaler.referenceResolution;
            Vector2 size=source.rootCanvas.renderingDisplaySize;
            float x=size.x/Mathf.Max(1,reference.x),y=size.y/Mathf.Max(1,reference.y);
            if(scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Expand)return Mathf.Min(x,y);
            if(scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Shrink)return Mathf.Max(x,y);
            return Mathf.Pow(2,Mathf.Lerp(Mathf.Log(Mathf.Max(.001f,x),2),Mathf.Log(Mathf.Max(.001f,y),2),scaler.matchWidthOrHeight));
        }

        // Shared by both existing camera controllers; no new input manager.
        public static Vector2 EdgePanDirection(float margin)
        {
            if (!EdgePanEnabled || !Application.isFocused || LobbyPauseMenu.IsAnyOpen || DevSettingsConsole.IsWindowOpen ||
                ManagementComputerController.IsAnyOpen || GameplayUIBlocker.IsBlocked() || Time.timeScale <= 0 || Input.touchCount > 0 ||
                (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())) return Vector2.zero;
            Vector2 p = Input.mousePosition;
            if (p.x < 0 || p.y < 0 || p.x > Screen.width || p.y > Screen.height) return Vector2.zero;
            return new Vector2(p.x < margin ? -1 : p.x > Screen.width - margin ? 1 : 0,
                p.y < margin ? -1 : p.y > Screen.height - margin ? 1 : 0).normalized;
        }
    }
}
