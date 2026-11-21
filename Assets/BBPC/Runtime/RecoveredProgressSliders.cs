using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    // ProgressContainer.Refresh (decompiled fields: drawProgressParent + NormalSlider prefab, one instance per LotteryModelV2 progress).
    // Each slider: icon (progress.icon), text "progress/guarantee", fill = progress / guarantee. Same NormalSlider prefab as the Raffle panel
    // (Resources/RecoveredSlider606). The fill update animation of the original UpdateProgress is not replicated.
    public static class RecoveredProgressSliders
    {
        public static void Fill(RecoveredFrontend frontend, RecoveredCoverCache covers, RecoveredUiData sliderData, Transform parent, RecoveredProgressInfo[] progresses)
        {
            if (parent == null || sliderData == null || progresses == null || sliderData.scenes == null || sliderData.scenes.Length == 0) return;
            for (int i = parent.childCount - 1; i >= 0; i--) Object.Destroy(parent.GetChild(i).gameObject);
            foreach (var progress in progresses)
            {
                if (progress == null) continue;
                var s = RecoveredUiBuilder.Build(sliderData, sliderData.scenes[0], parent, frontend.PortraitCamera);
                RecoveredUiBuilder.UseAuthoredLayoutRoot(s, "NormalSlider", parent);
                var text = Sub(s.Root.transform, "Text"); if (text != null && text.TryGetComponent<Text>(out var tx)) tx.text = progress.progress + "/" + progress.guarantee;
                var icon = Sub(s.Root.transform, "BfImage");
                if (icon != null && covers != null && progress.icon != null && icon.TryGetComponent<RawImage>(out var raw))
                { var b = icon.GetComponent<RecoveredCoverBinding>() ?? icon.gameObject.AddComponent<RecoveredCoverBinding>(); b.Bind(covers, progress.icon, raw, null, false, true); }
                float ratio = progress.guarantee > 0 ? Mathf.Clamp01((float)progress.progress / progress.guarantee) : 0f;
                if (s.Root.TryGetComponent<Slider>(out var slider)) slider.value = ratio;
                else { var fill = Sub(s.Root.transform, "Fill") as RectTransform; if (fill != null) fill.anchorMax = new Vector2(ratio, fill.anchorMax.y); }
            }
        }
        private static Transform Sub(Transform t, string name) { foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c; return null; }
    }
}
