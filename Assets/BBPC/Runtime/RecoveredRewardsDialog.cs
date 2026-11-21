using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    // Original Get10RewardDialog (bundle heavydialog2, exported to Resources/RecoveredRewards606) shown after a lottery/purchase.
    // Per-cell rules come from the disassembled Get10RewardDialog.Refresh:
    //  * reward type 1 (diamonds, field "quantity"): bgImage = DiamondSpriteHelper.GetDiamondSpriteByQuantity(quantity), NameText = quantity.
    //    Thresholds (decompiled): <26 25, <51 50, <76 75, <101 100, <151 150, <251 250, <1501 1500, <3001 3000, <7001 7000, <=12000 12000, <=30000 30000, else 60000.
    //  * reward type 2 (role): BFCover on; frame = diamondsSprites[0] (xxk) / [2] (sbfk when RarityV2 == S) / [1] (siwk for GF roles).
    //  * other reward types (5 = decor) are not rendered: skipped and counted, never invented.
    public sealed class RecoveredRewardsDialog : MonoBehaviour
    {
        private RecoveredFrontend frontend;
        private RecoveredUiData data, shopCells;
        private Transform parent;
        private GameObject root;
        private RecoveredCoverCache covers;
        public bool IsOpen => root != null;

        public void Initialize(RecoveredFrontend host, Transform dialogParent)
        {
            frontend = host; parent = dialogParent;
            var a = Resources.Load<TextAsset>("RecoveredRewards606/rewards");
            if (a == null) throw new InvalidOperationException("Reward dialog prefabs unavailable");
            data = JsonUtility.FromJson<RecoveredUiData>(a.text);
            var s = Resources.Load<TextAsset>("RecoveredShop606/shop");
            if (s != null) shopCells = JsonUtility.FromJson<RecoveredUiData>(s.text);
            covers = frontend.GetComponent<RecoveredCoverCache>() ?? frontend.gameObject.AddComponent<RecoveredCoverCache>();
        }

        public static string GemSpriteName(int q)
        {
            if (q < 26) return "gem_25"; if (q < 51) return "gem_50"; if (q < 76) return "gem_75"; if (q < 101) return "gem_100";
            if (q < 151) return "gem_150"; if (q < 251) return "gem_250"; if (q < 1501) return "gem_1500"; if (q < 3001) return "gem_3000";
            if (q < 7001) return "gem_7000"; if (q <= 12000) return "gem_12000"; if (q <= 30000) return "gem_30000"; return "gem_60000";
        }

        public int Show(RecoveredRewardModel[] rewards, Action closed)
        {
            Close();
            var scene = Array.Find(data.scenes, x => x.name == "Get10RewardDialog");
            var cell = Array.Find(data.scenes, x => x.name == "RewardPanel");
            var tree = RecoveredUiBuilder.Build(data, scene, parent, frontend.PortraitCamera);
            RecoveredUiBuilder.UseAuthoredLayoutRoot(tree, "Get10RewardDialog", parent);
            root = tree.Root;
            var rect = (RectTransform)root.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            root.transform.SetAsLastSibling();
            var content = root.transform.Find("MaskImage/Scroll View/Viewport/Content");
            var fit = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            int skipped = 0;
            if (rewards != null)
                foreach (var reward in rewards)
                {
                    if (reward == null) continue;
                    if (reward.type != 1 && !(reward.type == 2 && reward.role != null)) { skipped++; continue; }
                    var t = RecoveredUiBuilder.Build(data, cell, content, frontend.PortraitCamera);
                    RecoveredUiBuilder.UseAuthoredLayoutRoot(t, "RewardPanel", content);
                    foreach (var pair in t.Objects) if (pair.Value.name == "Lighting") pair.Value.SetActive(false);
                    Bind(t, reward);
                }
            var done = root.transform.Find("MaskImage/Scroll View/GetButton")?.GetComponent<Button>();
            if (done != null) { done.interactable = true; done.onClick.RemoveAllListeners(); done.onClick.AddListener(() => { Close(); closed?.Invoke(); }); }
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)content);
            return skipped;
        }

        private Sprite Load(string name) => Resources.Load<Sprite>("RecoveredRewards606/Sprites/" + name);

        private void Bind(RecoveredUiTree t, RecoveredRewardModel reward)
        {
            var bg = t.Root.transform.Find("bgImage")?.GetComponent<Image>();
            var name = t.Root.transform.Find("NameText")?.GetComponent<Text>();
            var cover = t.Root.transform.Find("BFCover");
            if (reward.type == 1)
            {
                if (bg != null) { var s = Load(GemSpriteName(reward.quantity)); if (s != null) bg.sprite = s; }
                if (name != null) name.text = reward.quantity.ToString();
                if (cover != null) cover.gameObject.SetActive(false);
                return;
            }
            var role = reward.role;
            if (name != null) name.text = role.title ?? role.rid;
            if (bg != null)
            {
                string frame = role.type == 1 ? null : RecoveredRarity.V2(role) == 20 ? "frame_sbfk" : "frame_xxk";
                var s = frame != null ? Load(frame) : Array.Find(data.assets, a => a.type == "Sprite" && a.name == "siwk") != null ? RecoveredRarity.Find(data, "siwk") : null;
                if (s != null) bg.sprite = s;
            }
            if (cover == null) return;
            cover.gameObject.SetActive(true);
            var icon = cover.Find("BFIcon");
            if (icon != null) { icon.gameObject.SetActive(true); if (icon.TryGetComponent<RawImage>(out var raw) && role.icon != null) { var b = icon.GetComponent<RecoveredCoverBinding>() ?? icon.gameObject.AddComponent<RecoveredCoverBinding>(); b.Bind(covers, role.icon, raw, null, false, true); } }
            var ri = cover.Find("RarityIcon");
            RecoveredRarity.Apply(shopCells ?? data, cover.GetComponent<Image>(), ri != null ? ri.GetComponent<Image>() : null, role);
        }

        public void Close() { if (root != null) { root.SetActive(false); Destroy(root); root = null; } }
    }
}
