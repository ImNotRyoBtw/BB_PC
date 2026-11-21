using System;
using UnityEngine;
namespace BBPC.UnityRuntime
{
    // Windows presentation policy, not an inferred Android gameplay rule.
    public static class WindowsWindow
    {
        public static Vector2Int FitInitialSize(int workWidth, int workHeight)
        {
            if (workWidth <= 0 || workHeight <= 0) throw new ArgumentOutOfRangeException("Desktop work area");
            int availableWidth = Math.Max(1, workWidth - 32), availableHeight = Math.Max(1, workHeight - 64);
            float scale = Math.Min(1f, Math.Min(availableWidth / 414f, availableHeight / 896f));
            return new Vector2Int(Math.Max(1, (int)(414 * scale)), Math.Max(1, (int)(896 * scale)));
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            if (Application.isEditor || Application.platform != RuntimePlatform.WindowsPlayer) return;
            var area = Screen.mainWindowDisplayInfo.workArea;
            if (area.width <= 0 || area.height <= 0) throw new InvalidOperationException("Windows desktop work area unavailable");
            var size = FitInitialSize(area.width, area.height);
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
        }
    }
}
