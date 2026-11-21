using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    // Original Character page (CustomBfPanel, built by RecoveredNavigation from Recovered606/navigation) filled with the
    // original TripleCell rows (RecoveredSkins606/skins.json) bound to the OWN account roles (GET role/own list).
    // Equip is the already-validated RecoveredServiceClient.ChangeRoleEquip (POST role/config); nothing is spent.
    // Pending: animated BF preview (needs the character animation bundles; a static cover is shown), sort/filter,
    // gamepad list, role decor panel, EX style, Get More, role challenge.
    public sealed class RecoveredSkinsScreen : MonoBehaviour
    {
        private RecoveredFrontend frontend;
        private RecoveredUiData cells;
        private RecoveredUiTree page;
        private Transform container, selectButton, previewHost, sortButton, pinButton, hideButton;
        // OwnRolesHelper.RarityOrder (Ghidra): 0 default, 1 by rarity, 2 by time, 3 all (hidden included); the button cycles (mode + 1) % 4.
        private int sortMode; private static readonly string[] SortKeys = { "order_by_default", "order_by_rarity", "order_by_time", "order_by_all" };
        private static readonly string[] SortFallback = { "Default", "By Rarity", "By Time", "All" };
        private RawImage preview;
        private RecoveredCoverCache covers;
        private Text statusLabel;
        private bool open, busy;
        private RecoveredProfileFlag flag = RecoveredProfileFlag.BF;
        private string previewRid;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly Dictionary<RecoveredProfileFlag, Button> tabs = new Dictionary<RecoveredProfileFlag, Button>();
        private RecoveredServiceClient Services => frontend.Services;

        public void Initialize(RecoveredFrontend host)
        {
            frontend = host;
            var source = Resources.Load<TextAsset>("RecoveredSkins606/skins");
            if (source == null) throw new InvalidOperationException("Original skin cell prefabs unavailable");
            cells = JsonUtility.FromJson<RecoveredUiData>(source.text);
            if (cells.sourceVersion != "6.0.6") throw new InvalidOperationException("Wrong skin prefab version");
            page = frontend.Navigation.Pages[MainPage.Character];
            var panel = page.Find("CustomBfPanel"); if (panel == null) throw new InvalidOperationException("CustomBfPanel missing");
            container = panel.transform.Find("MainPanel/IconScroll/Viewport/Container");
            selectButton = panel.transform.Find("MainPanel/ShowBF/SelectButton");
            previewHost = panel.transform.Find("MainPanel/ShowBF/BfImageButton");
            if (container == null || selectButton == null || previewHost == null) throw new InvalidOperationException("Skins slots missing");
            // Rows are plain layout children: the scroll content must follow them.
            var fitter = container.GetComponent<ContentSizeFitter>() ?? container.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            covers = frontend.GetComponent<RecoveredCoverCache>() ?? frontend.gameObject.AddComponent<RecoveredCoverCache>();
            var death = previewHost.Find("BfDeath"); if (death != null) death.gameObject.SetActive(false);
            var previewGo = new GameObject("BBPCPreview", typeof(RectTransform), typeof(RawImage));
            previewGo.transform.SetParent(previewHost, false); previewGo.transform.SetAsFirstSibling();
            var pr = (RectTransform)previewGo.transform; pr.anchorMin = pr.anchorMax = new Vector2(.5f, .5f); pr.sizeDelta = new Vector2(210, 210); pr.anchoredPosition = Vector2.zero;
            preview = previewGo.GetComponent<RawImage>(); preview.enabled = false; preview.raycastTarget = false;
            sortButton = panel.transform.Find("MainPanel/ShowBF/SortButton");
            pinButton = panel.transform.Find("MainPanel/ShowBF/StateSetButtons/TopButton");
            hideButton = panel.transform.Find("MainPanel/ShowBF/StateSetButtons/HideButton");
            if (sortButton != null && sortButton.TryGetComponent<Button>(out var sb)) { sb.interactable = true; sb.onClick.RemoveAllListeners(); sb.onClick.AddListener(() => { sortMode = (sortMode + 1) % SortKeys.Length; Populate(); }); }
            var tabRoot = panel.transform.Find("MainPanel/ShowBF/ListButtons/GameObject");
            if (tabRoot != null)
            {
                BindTab(tabRoot.Find("RoleButton"), RecoveredProfileFlag.BF);
                BindTab(tabRoot.Find("GfButton"), RecoveredProfileFlag.GF);
                BindTab(tabRoot.Find("ReviveButton"), RecoveredProfileFlag.Revive);
            }
            var labelGo = new GameObject("SkinsStatus", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(page.Root.transform, false);
            var lr = (RectTransform)labelGo.transform; lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(1, 0); lr.pivot = new Vector2(.5f, 0);
            lr.sizeDelta = new Vector2(-32, 70); lr.anchoredPosition = new Vector2(0, 170);
            statusLabel = labelGo.GetComponent<Text>(); statusLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusLabel.fontSize = 24; statusLabel.alignment = TextAnchor.LowerCenter; statusLabel.color = Color.yellow; statusLabel.raycastTarget = false;
            labelGo.transform.SetAsLastSibling();
        }

        private void BindTab(Transform t, RecoveredProfileFlag target)
        {
            if (t == null || !t.TryGetComponent<Button>(out var b)) return;
            tabs[target] = b; b.interactable = true; b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => { flag = target; previewRid = null; Populate(); });
        }

        private void Say(string message)
        {
            Debug.Log("[BBPC.Skins] " + message);
            if (statusLabel != null) statusLabel.text = message ?? "";
        }

        public void SetOpen(bool value)
        {
            open = value;
            if (value) StartCoroutine(Open());
        }

        public void Refresh() { if (open && !busy) Populate(); }

        private IEnumerator Open()
        {
            if (busy) yield break;
            busy = true;
            try
            {
                if (!Services.OwnRolesVerified && Services.Identity.HasPair && Services.TimeSynced && !RecoveredStorage.Isolated)
                {
                    Say("Skins: consultando tus personajes…");
                    yield return Services.RefreshOwnRoles(v => { });
                }
            }
            finally { busy = false; }
            Populate();
        }

        private void Clear()
        {
            foreach (var go in spawned) if (go != null) { go.SetActive(false); Destroy(go); }
            spawned.Clear();
        }

        private sealed class Entry { public RecoveredServerRole Role; public int State; public long Ts; public bool HasTs; public long UnlockTs; public int Index; public bool Default; }

        // arrange_state: 1 pinned, 2 hidden, 0 normal (role/own). The default role carries none.
        private List<Entry> Roles()
        {
            var list = new List<Entry>(); var seen = new HashSet<string>();
            var own = frontend.Account.ServerRoles; if (own == null) return list;
            var defaultRole = flag == RecoveredProfileFlag.BF ? own.default_bf : flag == RecoveredProfileFlag.GF ? own.default_gf : own.default_revive;
            var roles = flag == RecoveredProfileFlag.BF ? own.bfs : flag == RecoveredProfileFlag.GF ? own.gfs : own.revives;
            int i = 0;
            if (defaultRole != null && !string.IsNullOrWhiteSpace(defaultRole.rid) && seen.Add(defaultRole.rid)) list.Add(new Entry { Role = defaultRole, Index = i++, Default = true });
            if (roles != null) foreach (var item in roles) if (item?.role != null && !string.IsNullOrWhiteSpace(item.role.rid) && seen.Add(item.role.rid))
                list.Add(new Entry { Role = item.role, State = item.arrange_state != null ? item.arrange_state.state : 0, Ts = item.arrange_state != null ? item.arrange_state.ts : 0, HasTs = item.arrange_state != null, UnlockTs = item.restriction != null ? item.restriction.unlock_ts : 0, Index = i++ });
            return list;
        }

        // OwnRolesManager.GetOwnBfs / DealTopRoleState (Ghidra): modes 0/1/2 drop hidden roles (state 2), mode 3 keeps them. Mode 1 = stable descending rarity key
        // (ex_level + 100 for EX, else rarity_v2 < 100 as is, else 30; no rarity_v2 = lowest), mode 2 = descending restriction.unlock_ts, 0 and 3 keep the saved order.
        // Then pinned roles (state 1) come first - ordered by arrange_state.ts ascending (missing = -1) only in modes 0 and 3 - followed by the rest.
        // The default role is always inserted at index 0.
        private static int RarityKey(RecoveredServerRole r)
        {
            if (r.ex_level.HasValue) return r.ex_level.Value + 100;
            if (!r.rarity_v2.HasValue) return -1;
            return r.rarity_v2.Value < 100 ? r.rarity_v2.Value : 30;
        }

        private List<Entry> Ordered(List<Entry> all)
        {
            var result = new List<Entry>();
            var defaults = all.FindAll(e => e.Default); var rest = all.FindAll(e => !e.Default && (sortMode == 3 || e.State != 2));
            if (sortMode == 1) rest = StableSort(rest, (x, y) => RarityKey(y.Role).CompareTo(RarityKey(x.Role)));
            else if (sortMode == 2) rest = StableSort(rest, (x, y) => y.UnlockTs.CompareTo(x.UnlockTs));
            var pinned = rest.FindAll(e => e.State == 1); var others = rest.FindAll(e => e.State != 1);
            if (sortMode == 0 || sortMode == 3) pinned = StableSort(pinned, (x, y) => (x.HasTs ? x.Ts : -1).CompareTo(y.HasTs ? y.Ts : -1));
            result.AddRange(defaults); result.AddRange(pinned); result.AddRange(others);
            return result;
        }

        private static List<Entry> StableSort(List<Entry> list, Comparison<Entry> cmp)
        {
            var copy = new List<Entry>(list);
            copy.Sort((x, y) => { int c = cmp(x, y); return c != 0 ? c : x.Index.CompareTo(y.Index); });
            return copy;
        }

        private static Transform P(RecoveredUiTree t, string path) => t.Root.transform.Find(path);

        private void Populate()
        {
            Clear();
            foreach (var pair in tabs) if (pair.Value != null && pair.Value.targetGraphic != null) pair.Value.targetGraphic.color = pair.Key == flag ? Color.white : new Color(.65f, .65f, .65f, 1f);
            for (int i = container.childCount - 1; i >= 0; i--) { var c = container.GetChild(i).gameObject; if (!spawned.Contains(c)) { c.SetActive(false); Destroy(c); } }
            var all = Roles(); var roles = Ordered(all);
            string equipped = Services.SelectedRole(flag);
            // An empty profile slot (GF / Revive / BF never changed) means the default role of that kind is the one in use.
            if (string.IsNullOrEmpty(equipped)) equipped = all.Find(e => e.Default)?.Role.rid;
            if (string.IsNullOrEmpty(previewRid) || roles.Find(r => r.Role.rid == previewRid) == null) previewRid = roles.Find(r => r.Role.rid == equipped)?.Role.rid ?? (roles.Count > 0 ? roles[0].Role.rid : null);
            var scene = Array.Find(cells.scenes, s => s.name == "TripleCell");
            for (int start = 0; start < roles.Count; start += 3)
            {
                var tree = RecoveredUiBuilder.Build(cells, scene, container, frontend.PortraitCamera);
                RecoveredUiBuilder.UseAuthoredLayoutRoot(tree, "TripleCell", container);
                spawned.Add(tree.Root);
                foreach (var pair in tree.Objects)
                {
                    if (pair.Value.TryGetComponent<Button>(out var b)) b.interactable = false;
                    if (pair.Value.name == "MoreGfButton") pair.Value.SetActive(false);
                }
                for (int slot = 0; slot < 3; slot++)
                {
                    var cell = P(tree, "LayoutGroup/IconButton" + slot); if (cell == null) continue;
                    if (start + slot >= roles.Count) { cell.gameObject.SetActive(false); continue; }
                    BindCell(cell, roles[start + slot], equipped);
                }
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)container);
            RefreshPreview(roles, equipped);
            if (sortButton != null) { var t = sortButton.Find("Text"); if (t != null && t.TryGetComponent<Text>(out var st)) st.text = RecoveredLocalization.Text(SortKeys[sortMode], SortFallback[sortMode]); }
            Say(roles.Count == 0 ? (Services.OwnRolesVerified ? "No hay personajes en esta lista." : "Skins sin datos. " + Services.Status) : roles.Count + " personajes · en uso: " + (equipped ?? "—"));
        }

        private void BindCell(Transform cell, Entry entry, string equipped)
        {
            var role = entry.Role;
            var name = cell.Find("BfNameText"); if (name != null && name.TryGetComponent<Text>(out var text)) text.text = role.title ?? role.rid;
            foreach (var hidden in new[] { "NewImage", "RoleChallenge", "UpgradeIcon", "Upgrade", "MAX" }) { var h = cell.Find(hidden); if (h != null) h.gameObject.SetActive(false); }
            bool isEquipped = role.rid == equipped, isPreview = role.rid == previewRid;
            // IconButton.RefreshIconButton (libil2cpp, decompiled): SelectImage (check) active when rid == GetRidByType(type) = EQUIPPED;
            // sprite becomes `choice` (gold) when rid == OwnRolesManager.curRid = the cell being PREVIEWED (selected); otherwise normalR (normalD for revives).
            var select = cell.Find("SelectImage"); if (select != null) select.gameObject.SetActive(isEquipped);
            if (cell.TryGetComponent<Image>(out var frame))
            {
                var sprite = RecoveredRarity.Find(cells, isPreview ? "xuanzhongdi" : flag != RecoveredProfileFlag.BF ? "fuhuod" : "bfd"); if (sprite != null) frame.sprite = sprite;
            }
            var state = cell.Find("StateIconImage");
            if (state != null)
            {
                var s = entry.State == 1 ? RecoveredRarity.Find(cells, "topicon") : entry.State == 2 ? RecoveredRarity.Find(cells, "hideicon") : null;
                state.gameObject.SetActive(s != null);
                if (s != null && state.TryGetComponent<Image>(out var si)) si.sprite = s;
            }
            var bg = cell.Find("BFCover"); var ri = cell.Find("BFCover/RarityIcon");
            RecoveredRarity.Apply(cells, bg != null ? bg.GetComponent<Image>() : null, ri != null ? ri.GetComponent<Image>() : null, role);
            var icon = cell.Find("BFCover/BFIcon");
            if (icon != null && icon.TryGetComponent<RawImage>(out var raw) && role.icon != null)
            {
                var binding = icon.GetComponent<RecoveredCoverBinding>() ?? icon.gameObject.AddComponent<RecoveredCoverBinding>();
                binding.Bind(covers, role.icon, raw, null, false, true);
            }
            if (cell.TryGetComponent<Button>(out var button))
            {
                var rid = role.rid;
                button.interactable = true; button.transition = Selectable.Transition.None; button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => { previewRid = rid; Populate(); });
            }
        }

        private void RefreshPreview(List<Entry> roles, string equipped)
        {
            var entry = roles.Find(r => r.Role.rid == previewRid); var role = entry?.Role;
            if (preview != null)
            {
                preview.enabled = false;
                if (role?.icon != null)
                {
                    var binding = preview.GetComponent<RecoveredCoverBinding>() ?? preview.gameObject.AddComponent<RecoveredCoverBinding>();
                    binding.Bind(covers, role.icon, preview, null, true, true);
                }
            }
            bool owned = role != null && Services.OwnsRole(new RecoveredBfOperation(flag, role.rid));
            bool using_ = role != null && role.rid == equipped;
            // SelectButton authored Image (blue rectangle) is disabled in the prefab; the game shows it for ELEGIR and hides it for EN USO.
            if (selectButton.TryGetComponent<Image>(out var selectImage)) selectImage.enabled = role != null && !using_;
            if (selectButton.TryGetComponent<Button>(out var button))
            {
                var label = selectButton.Find("Text");
                if (label != null && label.TryGetComponent<Text>(out var t)) t.text = role == null ? "" : using_ ? "EN USO" : "ELEGIR";
                button.onClick.RemoveAllListeners();
                button.interactable = owned && !using_ && !busy;
                if (button.interactable) { var rid = role.rid; button.onClick.AddListener(() => StartCoroutine(Equip(rid))); }
            }
            // PIN / HIDE toggle state 1 / 2 <-> 0 through role/action. The default role has no arrange entry, so it is not offered.
            bool arrangeable = entry != null && owned && !entry.Default;
            BindArrange(pinButton, arrangeable, entry, 1, "PIN", "PINNED");
            BindArrange(hideButton, arrangeable, entry, 2, "HIDE", "HIDDEN");
        }

        private void BindArrange(Transform t, bool enabled, Entry entry, int target, string off, string on)
        {
            if (t == null || !t.TryGetComponent<Button>(out var b)) return;
            var label = t.Find("Text"); if (label != null && label.TryGetComponent<Text>(out var text)) text.text = entry != null && entry.State == target ? on : off;
            b.onClick.RemoveAllListeners(); b.interactable = enabled && !busy;
            if (!b.interactable) return;
            string rid = entry.Role.rid; int next = entry.State == target ? 0 : target;
            b.onClick.AddListener(() => StartCoroutine(Arrange(rid, next)));
        }

        private IEnumerator Arrange(string rid, int state)
        {
            if (busy) yield break;
            busy = true;
            try
            {
                Say(state == 1 ? "Fijando…" : state == 2 ? "Ocultando…" : "Restaurando…");
                bool ok = false;
                yield return Services.ArrangeRole(rid, state, v => ok = v);
                if (!ok) Say(Services.Status ?? "No se pudo cambiar.");
            }
            finally { busy = false; }
            Populate();
        }

        private IEnumerator Equip(string rid)
        {
            if (busy) yield break;
            var operation = new RecoveredBfOperation(flag, rid);
            if (RecoveredStorage.Isolated || !Services.Identity.HasPair || !Services.TimeSynced || !Services.ProfileVerified || !Services.OwnRolesVerified || Services.BfChangeRunning || !Services.OwnsRole(operation))
            { Say("Equipar requiere perfil y propiedad verificados; no se modifica el inventario local."); yield break; }
            busy = true;
            try
            {
                Say("Equipando…");
                bool accepted = false;
                yield return Services.ChangeRoleEquip(operation, v => accepted = v);
                Say(accepted && Services.SelectedRole(flag) == rid ? "Equipado y confirmado por el servidor." : (Services.Status ?? "No se pudo equipar."));
            }
            finally { busy = false; }
            Populate();
        }
    }
}
