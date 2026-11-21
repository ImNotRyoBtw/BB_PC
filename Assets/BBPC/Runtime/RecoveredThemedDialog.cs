using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    // Themed lottery dialog (original ThemedLotteryDialog, prefab "MainDialog" inside the CDN pack zone.resource_pack,
    // e.g. android_VoidMatt_17e5d1f634.assetbundle, converted by Tools/ui_pack). Layout names come from the prefab.
    // Ported: roulette cells (probabilities[pos] -> cell pos), draw x1 / x10 buttons with price and two-step confirmation, close, "Ends in".
    // NOT ported: 3D RoleDisplay (camera/particles/spine), roulette spin animation, Skip, "?" dialog, pid_wins counters (dictionary,
    // JsonUtility cannot read it), emoticon/friend popups. Those nodes are hidden. Not compiled or run in Unity.
    public sealed class RecoveredThemedDialog : MonoBehaviour
    {
        private RecoveredFrontend frontend; private RecoveredRewardsDialog rewards; private Transform parent; private RecoveredCoverCache covers;
        private RecoveredUiTree tree; private RecoveredUiData cells; public RecoveredUiData Sliders; private Text status; private bool busy; private Action changed;
        private Button armed; private Text armedLabel; private string armedText; private float armedAt;
        public RecoveredExplainDialog Explain;
        private RecoveredServiceClient Services => frontend.Services;

        public void Initialize(RecoveredFrontend host, Transform dialogParent, RecoveredRewardsDialog rewardsDialog, RecoveredCoverCache coverCache, RecoveredUiData rarityCells)
        { frontend = host; parent = dialogParent; rewards = rewardsDialog; covers = coverCache; cells = rarityCells; }

        public void Open(RecoveredRaffleZone zone, Action onChanged)
        {
            Close(); changed = onChanged;
            var pack = RecoveredUiPacks.Load(zone.resource_pack);
            if (pack == null) throw new InvalidOperationException("Themed pack unavailable: " + RecoveredUiPacks.LastError);
            var scene = Array.Find(pack.scenes, s => s.name == "MainDialog");
            if (scene == null) throw new InvalidOperationException("MainDialog missing in pack");
            tree = RecoveredUiBuilder.Build(pack, scene, parent, frontend.PortraitCamera);
            RecoveredUiBuilder.UseAuthoredLayoutRoot(tree, scene.name, parent);
            var rect = tree.Root.transform as RectTransform; if (rect != null) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
            tree.Root.transform.SetAsLastSibling();
            foreach (var pair in tree.Objects)
            {
                var n = pair.Value.name;
                if (n == "RoleDisplay" || n == "DrawingMaskImage" || n == "SkipDrawingMaskImage" || n == "SkipButton" || n == "HaloMom") pair.Value.SetActive(false);
                if (pair.Value.TryGetComponent<Button>(out var b)) b.interactable = false;
            }
            string main = "Dialog/MainPanel/";
            var close = Find(main + "CloseButton")?.GetComponent<Button>() ?? Deep("CloseButton")?.GetComponent<Button>();
            if (close != null) { close.interactable = true; close.onClick.RemoveAllListeners(); close.onClick.AddListener(Close); }
            var rn = Deep("RewardNameText"); if (rn != null) rn.gameObject.SetActive(false);
            var ends = Deep("EndsIn"); if (ends != null && ends.TryGetComponent<Text>(out var et)) et.text = RecoveredLegacyDialog.RemainingText(zone.expiration);
            var roulette = Deep("Roulette"); var reward = roulette != null ? roulette.Find("RewardParent") : null;
            if (reward != null && zone.probabilities != null)
                foreach (var pr in zone.probabilities)
                {
                    if (pr?.rewards == null || pr.rewards.Length == 0) continue;
                    var cell = FindCell(reward, pr.pos); if (cell != null) BindCell(cell, zone, pr);
                }
            var xb = Deep("ExplainButton"); if (xb != null && Explain != null && xb.TryGetComponent<Button>(out var eb)) { eb.interactable = true; eb.onClick.RemoveAllListeners(); eb.onClick.AddListener(() => { try { Explain.Open(zone); } catch (Exception e) { Debug.LogException(e); } }); }
            // ThemedLotteryPresenter.RefreshUI (Ghidra): one NormalSlider per progress under drawProgressParent (SliderScrollView/Content).
            var sliderContent = Deep("SliderScrollView"); sliderContent = sliderContent != null ? sliderContent.Find("Viewport/Content") : null;
            RecoveredProgressSliders.Fill(frontend, covers, Sliders, sliderContent, zone.progresses);
            BindDraw("Drawx1", zone, 1); BindDraw("Drawx10", zone, 10);
            var go = new GameObject("ThemedStatus", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(tree.Root.transform, false);
            var r = (RectTransform)go.transform; r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(.5f, 0); r.sizeDelta = new Vector2(-32, 110); r.anchoredPosition = new Vector2(0, 40);
            status = go.GetComponent<Text>(); status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); status.fontSize = 26; status.alignment = TextAnchor.LowerCenter; status.color = Color.yellow; status.raycastTarget = false;
        }

        public void Close() { if (tree != null && tree.Root != null) { tree.Root.SetActive(false); Destroy(tree.Root); } tree = null; armed = null; }

        private Transform Deep(string name) { if (tree == null) return null; foreach (var c in tree.Root.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c; return null; }
        private Transform Find(string path) => tree?.Root.transform.Find(path);
        private static Transform FindCell(Transform rewardParent, int pos) { foreach (Transform c in rewardParent) if (c.name.EndsWith("_" + pos)) return c; return null; }
        private static Transform Sub(Transform t, string name) { foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c; return null; }

        // RouletteCell.RefreshData + ThemedLotteryRouletteCell.RefreshData (Ghidra): the cell is active only if the probability has rewards; ownedObj =
        // LotteryModelV2.CheckWins (themed: pid_wins[pid].HasAll) and the quality icon is hidden when owned; countText (only when the pid is limited)
        // shows count/max. countTextFormat is a serialized string that was not recovered: "{0}/{1}" is assumed. Selected/deselected: deselected by default.
        private void BindCell(Transform cell, RecoveredRaffleZone zone, RecoveredProbability pr)
        {
            var reward = pr.rewards != null && pr.rewards.Length > 0 ? pr.rewards[0] : null;
            if (reward == null) { cell.gameObject.SetActive(false); return; }
            cell.gameObject.SetActive(true);
            var role = reward.role;
            bool owned = RecoveredPidWins.CheckWins(zone, pr);
            var got = Sub(cell, "GotMaskImage"); if (got != null) got.gameObject.SetActive(owned);
            var info = RecoveredPidWins.Find(zone, pr.pid);
            var count = Sub(cell, "Count");
            if (count != null) { bool show = info != null && !info.IsUnlimited; count.gameObject.SetActive(show); if (show && count.TryGetComponent<Text>(out var ct)) ct.text = info.count + "/" + info.max; }
            var click = Sub(cell, "ClickState"); var normal = Sub(cell, "NormalState");
            if (normal != null) normal.gameObject.SetActive(true); if (click != null) click.gameObject.SetActive(false);
            var name = Sub(cell, "NameText"); if (name != null && name.TryGetComponent<Text>(out var nt)) nt.text = role != null ? (role.title ?? role.rid) : "";
            foreach (var n in new[] { "DiamondsImage", "DiamondText", "DecorCover", "KeepStar", "StarBooster", "Upgrade", "MAX", "UpgradeIcon", "CountText" }) { var x = Sub(cell, n); if (x != null) x.gameObject.SetActive(false); }
            if (role == null) return;
            var cover = Sub(cell, "BFCover"); if (cover == null) return;
            cover.gameObject.SetActive(true);
            var icon = cover.Find("BFIcon"); var rarity = cover.Find("RarityIcon");
            if (icon != null) { icon.gameObject.SetActive(true); if (icon.TryGetComponent<RawImage>(out var raw) && role.icon != null) { var b = icon.GetComponent<RecoveredCoverBinding>() ?? icon.gameObject.AddComponent<RecoveredCoverBinding>(); b.Bind(covers, role.icon, raw, null, false, true); } }
            RecoveredRarity.Apply(cells, cover.GetComponent<Image>(), rarity != null ? rarity.GetComponent<Image>() : null, role);
            if (rarity != null) rarity.gameObject.SetActive(!owned);
        }

        private void BindDraw(string name, RecoveredRaffleZone zone, int guess)
        {
            var node = Deep(name); if (node == null) return;
            RecoveredPayment payment = null;
            if (zone.payments != null) foreach (var p in zone.payments) if (p != null && p.guess == guess && p.ptype == 1) { payment = p; break; }
            if (payment == null) { node.gameObject.SetActive(false); return; }
            var price = Sub(node, "DrawPointText"); if (price != null && price.TryGetComponent<Text>(out var pt)) pt.text = payment.quantity.ToString();
            var dt = Sub(node, "DrawText"); var label = dt != null ? dt.GetComponent<Text>() : null;
            var button = node.GetComponent<Button>(); if (button == null) return;
            button.interactable = true; button.transition = Selectable.Transition.None; button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (busy) return;
                if (armed == button && Time.unscaledTime - armedAt < 4f) { Disarm(); StartCoroutine(Spin(zone, payment)); return; }
                Disarm(); armed = button; armedAt = Time.unscaledTime; armedLabel = label; armedText = label != null ? label.text : null;
                if (label != null) label.text = "¿Confirmar?";
                Say("Pulsa de nuevo para confirmar: gasta " + payment.quantity + " diamantes, sin reintento automático.");
            });
        }

        private void Disarm() { if (armedLabel != null && armedText != null) armedLabel.text = armedText; armed = null; armedLabel = null; armedText = null; }
        private void Update() { if (armed != null && Time.unscaledTime - armedAt >= 4f) Disarm(); }
        private void Say(string m) { Debug.Log("[BBPC.Themed] " + m); if (status != null) status.text = m ?? ""; }

        private IEnumerator Spin(RecoveredRaffleZone zone, RecoveredPayment payment)
        {
            if (busy) yield break; busy = true;
            try
            {
                Say("Sorteando…");
                RecoveredLotteryResultData result = null;
                yield return Services.SpinLottery(zone.id, payment.guess, payment.ptype, v => result = v);
                if (result != null)
                {
                    yield return Services.RefreshWealth(WealthRefreshTrigger.Payment, v => { });
                    yield return Services.RefreshOwnRoles(v => { });
                    int skipped = rewards.Show(result.rewards, () => changed?.Invoke());
                    Say(skipped > 0 ? skipped + " premio(s) de tipo no soportado no se muestran; saldo ya actualizado." : "");
                    changed?.Invoke();
                }
                else Say((Services.Status ?? "") + " No se reintenta: revisa inventario antes de otro giro.");
            }
            finally { busy = false; }
        }
    }
}
