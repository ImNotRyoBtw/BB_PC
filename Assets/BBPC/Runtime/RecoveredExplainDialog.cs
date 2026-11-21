using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    // "?" probabilities dialog (original ExplainDialog.RefreshList(List<ProbabilitiesModel>), bundle lessuseddialog, exported to Resources/RecoveredExplain606).
    // Each row is a PoolGlPanel (GlPanel prefab): BFCover (icon + rarity), NameText = reward name, GlText = probabilityText (display_probability).
    // The explanatory text row (expTextObj) is hidden: its localization key is not recovered. Row order = server pos (the original sort is not recovered).
    public sealed class RecoveredExplainDialog : MonoBehaviour
    {
        private RecoveredFrontend frontend; private Transform parent; private RecoveredCoverCache covers; private RecoveredUiData cells, data; private RecoveredUiTree tree;

        public void Initialize(RecoveredFrontend host, Transform dialogParent, RecoveredCoverCache coverCache, RecoveredUiData rarityCells)
        {
            frontend = host; parent = dialogParent; covers = coverCache; cells = rarityCells;
            var a = Resources.Load<TextAsset>("RecoveredExplain606/shop");
            if (a == null) throw new InvalidOperationException("Explain dialog prefabs unavailable");
            data = JsonUtility.FromJson<RecoveredUiData>(a.text);
        }

        public void Open(RecoveredRaffleZone zone)
        {
            Close();
            var scene = Array.Find(data.scenes, s => s.name == "ExplainDialog"); var rowScene = Array.Find(data.scenes, s => s.name == "GlPanel");
            if (scene == null || rowScene == null) throw new InvalidOperationException("Missing ExplainDialog/GlPanel");
            tree = RecoveredUiBuilder.Build(data, scene, parent, frontend.PortraitCamera);
            RecoveredUiBuilder.UseAuthoredLayoutRoot(tree, scene.name, parent);
            var rect = tree.Root.transform as RectTransform; if (rect != null) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
            tree.Root.transform.SetAsLastSibling();
            Transform content = null, close = null;
            foreach (var c in tree.Root.GetComponentsInChildren<Transform>(true)) { if (c.name == "Content" && content == null) content = c; if (c.name == "Close") close = c; if (c.name == "Explain") c.gameObject.SetActive(false); }
            if (close != null && close.TryGetComponent<Button>(out var cb)) { cb.onClick.RemoveAllListeners(); cb.onClick.AddListener(Close); }
            if (content == null || zone.probabilities == null) return;
            var list = new List<RecoveredProbability>(zone.probabilities); list.Sort((x, y) => x.pos.CompareTo(y.pos));
            foreach (var pr in list)
            {
                if (pr == null) continue;
                var row = RecoveredUiBuilder.Build(data, rowScene, content, frontend.PortraitCamera);
                RecoveredUiBuilder.UseAuthoredLayoutRoot(row, rowScene.name, content);
                var role = pr.rewards != null && pr.rewards.Length > 0 ? pr.rewards[0].role : null;
                SetText(row, "NameText", role != null ? (role.title ?? role.rid) : pr.pid);
                SetText(row, "GlText", pr.display_probability);
                foreach (var n in new[] { "UpgradeIcon", "DecorCover", "KeepStar", "StarBooster", "FriendOrEmoji", "NameBoxComponent", "IconImage" }) Hide(row, n);
                if (role == null) continue;
                var cover = Sub(row.Root.transform, "BFCover"); if (cover == null) continue;
                var icon = cover.Find("BFIcon"); var rarity = cover.Find("RarityIcon");
                if (icon != null && icon.TryGetComponent<RawImage>(out var raw) && role.icon != null) { var b = icon.GetComponent<RecoveredCoverBinding>() ?? icon.gameObject.AddComponent<RecoveredCoverBinding>(); b.Bind(covers, role.icon, raw, null, false, true); }
                RecoveredRarity.Apply(cells, Sub(row.Root.transform, "BgImage")?.GetComponent<Image>(), rarity != null ? rarity.GetComponent<Image>() : null, role);
            }
        }

        public void Close() { if (tree != null && tree.Root != null) { tree.Root.SetActive(false); Destroy(tree.Root); } tree = null; }
        private static Transform Sub(Transform t, string name) { foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c; return null; }
        private static void Hide(RecoveredUiTree t, string name) { var x = Sub(t.Root.transform, name); if (x != null) x.gameObject.SetActive(false); }
        private static void SetText(RecoveredUiTree t, string name, string value) { var x = Sub(t.Root.transform, name); if (x != null && x.TryGetComponent<Text>(out var tx)) tx.text = value ?? ""; }
    }
}
