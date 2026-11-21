using UnityEngine;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    // Windows adaptation: stock CanvasScaler uses renderingDisplaySize, not the portrait camera's pixelRect.
    public sealed class RecoveredCanvasScaler : CanvasScaler
    {
        protected override void HandleScaleWithScreenSize()
        {
            var canvas = GetComponent<Canvas>();
            var camera = canvas.worldCamera;
            if (camera == null) { base.HandleScaleWithScreenSize(); return; }
            Vector2 size = camera.pixelRect.size;
            float width = size.x / referenceResolution.x, height = size.y / referenceResolution.y;
            float scale = screenMatchMode == ScreenMatchMode.Expand ? Mathf.Min(width, height)
                : screenMatchMode == ScreenMatchMode.Shrink ? Mathf.Max(width, height)
                : Mathf.Pow(2, Mathf.Lerp(Mathf.Log(width, 2), Mathf.Log(height, 2), matchWidthOrHeight));
            SetScaleFactor(scale);
            SetReferencePixelsPerUnit(referencePixelsPerUnit);
        }
        public void RefreshViewport() { Handle(); }
    }
}
