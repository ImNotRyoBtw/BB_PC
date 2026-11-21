using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    // Dialog opened by "Get" on the VS Legacy / Premium Legacy shop panels (original VSLegacyLotteryDialog / VSLegacyPremiumLotteryDialog,
    // bundle lightdialog32, exported to Resources/RecoveredLegacy606). Layout names come from the prefab; behaviour from dump.cs
    // (VSLegacyLotteryPresenter: two LotteryDrawButtons by payment index, remaining time, probability-up, progress container, close).
    //
    // NOT replicated yet (needs more disassembly): the draw animation (showcase/Skip), Current/Finished widgets, ProbabilityUp value, the AllBFDisplay dialog. Those widgets are hidden instead of showing invented values.
    // The exhibition art is the CDN prefab (zone.cover_res_pack); only the s16 pack is exported locally. Other events get no art.
    // Spending: same rules as the shop (two-step confirmation, one request at a time, never retried).
    public sealed class RecoveredLegacyDialog : MonoBehaviour
    {
        private RecoveredFrontend frontend;
        private RecoveredRewardsDialog rewards;
        private RecoveredUiData legacy;
        private RecoveredUiTree tree;
        private Transform parent;
        private Text status;
        private bool busy;
        private Button armed; private Text armedLabel; private string armedText; private float armedAt;
        private Action changed;
        private RecoveredServiceClient Services => frontend.Services;
        public RecoveredUiData Sliders; public RecoveredCoverCache Covers;
        public bool IsOpen => tree != null && tree.Root != null;

        public void Initialize(RecoveredFrontend host, Transform dialogParent)
        {
            frontend = host; parent = dialogParent;
            var a = Resources.Load<TextAsset>("RecoveredLegacy606/legacy");
            if (a == null) throw new InvalidOperationException("Legacy dialog prefabs unavailable");
            legacy = JsonUtility.FromJson<RecoveredUiData>(a.text);
            rewards = frontend.gameObject.GetComponent<RecoveredRewardsDialog>() ?? frontend.gameObject.AddComponent<RecoveredRewardsDialog>();
            rewards.Initialize(frontend, dialogParent);
        }

        public void Open(RecoveredRaffleZone zone, bool premium, Action onChanged)
        {
            Close();
            changed = onChanged;
            string name = premium ? "VSLegacyPremiumLotteryDialog" : "VSLegacyLotteryDialog";
            var scene = Array.Find(legacy.scenes, s => s.name == name);
            if (scene == null) throw new InvalidOperationException("Missing " + name);
            tree = RecoveredUiBuilder.Build(legacy, scene, parent, frontend.PortraitCamera);
            RecoveredUiBuilder.UseAuthoredLayoutRoot(tree, name, parent);
            var rect = tree.Root.transform as RectTransform; if (rect != null) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
            tree.Root.transform.SetAsLastSibling();
            foreach (var pair in tree.Objects)
            {
                if (pair.Value.name == "LoadingIndicator") pair.Value.SetActive(false);
                if (pair.Value.TryGetComponent<Button>(out var b)) b.interactable = false;
            }
            string main = "Dialog/MainPanel/";
            // VSLegacyLotteryDialog.CheckAndSwitchStyle (Ghidra): PoolRotateModel.HasObtainedAll = every all_rewards entry has its reward_id in wins ->
            // the Finished presenter replaces the NotFinished one (draw buttons/progress). Current has no recovered owner and stays hidden.
            bool finished = HasObtainedAll(zone);
            Hide(main + "Current");
            if (finished) { Hide(main + "NotFinished"); var fin = Find(main + "Finished"); if (fin != null) fin.gameObject.SetActive(true); } else Hide(main + "Finished");
            if (!finished) RecoveredProgressSliders.Fill(frontend, Covers, Sliders, Find(main + "NotFinished/Progress"), zone.progresses);
            Hide("Dialog/DrawingMaskImage"); Hide("Dialog/SkipDrawingMaskImage"); Hide("Dialog/SkipButton");
            var close = Find(main + "CloseButton")?.GetComponent<Button>();
            if (close != null) { close.interactable = true; close.onClick.RemoveAllListeners(); close.onClick.AddListener(Close); }
            ApplyProbability(Find(main + "ProbabilityParent"), zone);
            var remain = Find(main + "RemainingTime");
            if (remain != null && remain.TryGetComponent<Text>(out var rt)) rt.text = Remaining(zone.expiration);
            // One button per guess (1 and 10). Premium has two 10-draw payments; the free daily one (ptype 6) is not wired yet.
            BindButton(main + "NotFinished/1Button", zone, 1);
            BindButton(main + "NotFinished/10Button", zone, 10);
            AddExhibition(main + "Container/PrefabParent", zone);
            var go = new GameObject("LegacyStatus", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(tree.Root.transform, false);
            var r = (RectTransform)go.transform; r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(.5f, 0); r.sizeDelta = new Vector2(-32, 110); r.anchoredPosition = new Vector2(0, 40);
            status = go.GetComponent<Text>(); status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); status.fontSize = 26; status.alignment = TextAnchor.LowerCenter; status.color = Color.yellow; status.raycastTarget = false;
        }

        public RecoveredRewardsDialog Rewards => rewards;

        public static bool HasObtainedAll(RecoveredRaffleZone zone)
        {
            var rotate = zone?.rotate; if (rotate?.all_rewards == null) return false;
            var wins = new HashSet<string>(rotate.wins ?? new string[0]);
            foreach (var cr in rotate.all_rewards) if (cr == null || !wins.Contains(cr.reward_id)) return false;
            return true;
        }

        // Probability-up badge: value = zone.display_probability_up ("20%"), style child = zone.probability_up_style (authored children 0..4; mapping inferred).
        public static void ApplyProbability(Transform prob, RecoveredRaffleZone zone)
        {
            if (prob == null) return;
            bool any = !string.IsNullOrEmpty(zone.display_probability_up);
            prob.gameObject.SetActive(any);
            var comp = prob.Find("ProbabilityUpComponent");
            if (!any || comp == null) return;
            for (int i = 0; i < comp.childCount; i++) { var c = comp.GetChild(i); if (c.name.Length == 1 && char.IsDigit(c.name[0])) c.gameObject.SetActive(c.name[0] - '0' == zone.probability_up_style); }
            var v = comp.Find("Text"); if (v != null && v.TryGetComponent<Text>(out var vt)) vt.text = zone.display_probability_up;
        }

        public static string RemainingText(long expirationMs) => Remaining(expirationMs);

        public void Close()
        {
            if (tree != null && tree.Root != null) { tree.Root.SetActive(false); Destroy(tree.Root); }
            tree = null; armed = null;
        }

        private Transform Find(string path) => tree?.Root.transform.Find(path);
        private void Hide(string path) { var t = Find(path); if (t != null) t.gameObject.SetActive(false); }

        internal static string Remaining(long expirationMs) => RecoveredTimeText.EndsInDraw(expirationMs);

        // RoleExhibitionComponent.Refresh / RoleExhibitionCell (decompiled): each cell GameObject is named after a role id; its Image shows
        // cellSprite[owned ? 0 : 1] = "<rid>_z" when owned, "<rid>_a" when not; ownedText = "owned/total".
        // The prefab itself is the converted CDN pack named by zone.cover_res_pack (RecoveredUiPacks).
        public Transform AddExhibition(Transform slot, RecoveredRaffleZone zone, RecoveredFrontend host, string warning)
        {
            if (slot == null || zone?.cover_res_pack == null) return null;
            var pack = RecoveredUiPacks.Load(zone.cover_res_pack);
            if (pack == null) { Debug.LogWarning("[BBPC.Legacy] " + RecoveredUiPacks.LastError); return null; }
            var scene = Array.Find(pack.scenes, x => x.name == "RoleExhibition") ?? pack.scenes[0];
            var t = RecoveredUiBuilder.Build(pack, scene, slot, host.PortraitCamera);
            RecoveredUiBuilder.UseAuthoredLayoutRoot(t, scene.name, slot);
            foreach (var pair in t.Objects) if (pair.Value.TryGetComponent<Button>(out var b)) b.interactable = false;
            int owned = 0, total = 0;
            var cellsRoot = t.Root.transform.Find("CellObjects");
            if (cellsRoot != null)
                for (int i = 0; i < cellsRoot.childCount; i++)
                {
                    var cell = cellsRoot.GetChild(i); total++;
                    bool has = host.Services.OwnsRole(new RecoveredBfOperation(RecoveredProfileFlag.BF, cell.name));
                    if (has) owned++;
                    var sprite = RecoveredUiPacks.Find(pack, cell.name + (has ? "_z" : "_a"));
                    if (sprite != null && cell.TryGetComponent<Image>(out var img)) img.sprite = sprite;
                }
            var text = t.Root.transform.Find("OwnedText"); if (text != null && text.TryGetComponent<Text>(out var ot)) ot.text = owned + "/" + total;
            return t.Root.transform;
        }

        private void AddExhibition(string path, RecoveredRaffleZone zone) { AddExhibition(Find(path), zone, frontend, null); }

        private void BindButton(string path, RecoveredRaffleZone zone, int guess)
        {
            var node = Find(path); if (node == null) return;
            RecoveredPayment payment = null;
            if (zone.payments != null) foreach (var p in zone.payments) if (p != null && p.guess == guess && p.ptype == 1) { payment = p; break; }
            if (payment == null) { node.gameObject.SetActive(false); return; }
            var priceNode = node.Find("parent/Text"); if (priceNode != null && priceNode.TryGetComponent<Text>(out var pt)) pt.text = payment.quantity.ToString();
            var button = node.GetComponent<Button>(); if (button == null) return;
            var label = node.Find("Text") != null ? node.Find("Text").GetComponent<Text>() : null;
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
        private void Say(string message) { Debug.Log("[BBPC.Legacy] " + message); if (status != null) status.text = message ?? ""; }

        private IEnumerator Spin(RecoveredRaffleZone zone, RecoveredPayment payment)
        {
            if (busy) yield break;
            busy = true;
            try
            {
                Say("Sorteando…");
                RecoveredLotteryResultData result = null;
                yield return Services.SpinLottery(zone.id, payment.guess, payment.ptype, v => result = v);
                if (result != null)
                {
                    var names = new List<string>();
                    if (result.rewards != null) foreach (var r in result.rewards) if (r?.role != null) names.Add(r.role.title ?? r.role.rid);
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
