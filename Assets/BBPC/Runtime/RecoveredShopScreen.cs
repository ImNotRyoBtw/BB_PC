using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    // Original Shop page (ShopPanel, already built by RecoveredNavigation from Recovered606/navigation) filled with the
    // original cell prefabs (RecoveredShop606/shop.json) bound to the own-account GET /beatmusic/shop/goods payload.
    //
    // Money rules (every purchase/spin spends in-game currency):
    //  * explicit two-step confirmation per button (first click arms, second click within 4 s sends);
    //  * a single request at a time, never retried automatically, inventory/wealth reconciled afterwards;
    //  * request bodies come from RecoveredServiceClient (PurchaseGoods/SpinLottery/RequestShopDiscount).
    // Purchase contracts (live captures):
    //  * banner offer : {type: banner.type, id: banner.id, rids:[rid], diamond: discount_price ?? original_price}
    //  * bf_store item: {type: 2, id: rid, rids:[rid], diamond: price shown}; with an active store discount the shown
    //    price is original * (100 - off) / 100 (real-game screenshots: 8800 -> 6160, 7800 -> 5460 at 30 % off).
    // Pending: "Check All" full-screen dialog (BFStoreDialog, local bundle heavydialog10), Legacy exhibition art (CDN NetPrefab),
    // probability/info dialogs, ad button, diamond SKUs (real money, disabled).
    public sealed class RecoveredShopScreen : MonoBehaviour
    {
        private const float ArmSeconds = 4f;
        private RecoveredFrontend frontend;
        private RecoveredUiData cells, sliderData; private RecoveredThemedDialog themedDialog; private RecoveredExplainDialog explainDialog;
        private RectTransform content;
        private RecoveredShopPayload store;
        private RecoveredCoverCache covers;
        private Text statusLabel;
        private RecoveredLegacyDialog legacyDialog;
        private bool busy, open, loadedOnce;
        private float loadedAt;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private Button armed; private Text armedLabel; private string armedText; private float armedAt;
        private RecoveredServiceClient Services => frontend.Services;
        public bool IsOpen => open;
        public int SpawnedCount => spawned.Count;

        // ---- setup -------------------------------------------------------------------------------------------------
        public void Initialize(RecoveredFrontend host)
        {
            frontend = host;
            var source = Resources.Load<TextAsset>("RecoveredShop606/shop");
            if (source == null) throw new InvalidOperationException("Original shop cell prefabs unavailable");
            cells = JsonUtility.FromJson<RecoveredUiData>(source.text);
            if (cells.sourceVersion != "6.0.6") throw new InvalidOperationException("Wrong shop prefab version");
            var sl = Resources.Load<TextAsset>("RecoveredSlider606/slider"); if (sl != null) sliderData = JsonUtility.FromJson<RecoveredUiData>(sl.text);
            var page = frontend.Navigation.Pages[MainPage.Shop];
            foreach (var pair in page.Objects)
                if (pair.Value.name == "Content" && pair.Value.GetComponent<VerticalLayoutGroup>() != null) { content = (RectTransform)pair.Value.transform; break; }
            if (content == null) throw new InvalidOperationException("Shop Content missing");
            covers = frontend.GetComponent<RecoveredCoverCache>() ?? frontend.gameObject.AddComponent<RecoveredCoverCache>();
            // Visible diagnostic line: the shop must never fail silently.
            var labelGo = new GameObject("ShopStatus", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(page.Root.transform, false);
            var lr = (RectTransform)labelGo.transform; lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(1, 0); lr.pivot = new Vector2(.5f, 0);
            lr.sizeDelta = new Vector2(-32, 90); lr.anchoredPosition = new Vector2(0, 170);
            statusLabel = labelGo.GetComponent<Text>(); statusLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusLabel.fontSize = 24; statusLabel.alignment = TextAnchor.LowerCenter; statusLabel.color = Color.yellow; statusLabel.raycastTarget = false; statusLabel.text = "";
            labelGo.transform.SetAsLastSibling();
            try { legacyDialog = frontend.gameObject.AddComponent<RecoveredLegacyDialog>(); legacyDialog.Initialize(frontend, page.Root.transform); legacyDialog.Sliders = sliderData; legacyDialog.Covers = covers; }
            catch (Exception e) { Debug.LogException(e); if (legacyDialog != null) Destroy(legacyDialog); legacyDialog = null; }
            try { explainDialog = frontend.gameObject.AddComponent<RecoveredExplainDialog>(); explainDialog.Initialize(frontend, page.Root.transform, covers, cells); }
            catch (Exception e) { Debug.LogException(e); explainDialog = null; }
            try { themedDialog = frontend.gameObject.AddComponent<RecoveredThemedDialog>(); themedDialog.Initialize(frontend, page.Root.transform, legacyDialog != null ? legacyDialog.Rewards : null, covers, cells); themedDialog.Explain = explainDialog; themedDialog.Sliders = sliderData; }
            catch (Exception e) { Debug.LogException(e); themedDialog = null; }
        }

        private void Say(string message)
        {
            Debug.Log("[BBPC.Shop] " + message);
            if (statusLabel != null) statusLabel.text = message ?? "";
            frontend.Report(message);
        }

        public void SetOpen(bool value)
        {
            open = value;
            if (value && !busy && (!loadedOnce || Time.unscaledTime - loadedAt > 60f)) StartCoroutine(Load());
        }

        public void Reload() { if (!busy) StartCoroutine(Load()); }

        private IEnumerator Load()
        {
            busy = true; Say("Tienda: consultando…");
            try
            {
                RecoveredShopPayload value = null;
                // If the online bootstrap is still running, wait for it (max 90 s) instead of failing with "sin datos".
                if (Services.Attempted && !Services.Finished)
                {
                    Say("Tienda: esperando identidad y hora…"); float giveUp = Time.unscaledTime + 90f;
                    while (!Services.Finished && Time.unscaledTime < giveUp) yield return null;
                }
                yield return Services.FetchShopGoods(v => value = v);
                if (value != null)
                {
                    store = value; loadedOnce = true; loadedAt = Time.unscaledTime;
                    try
                    {
                        Populate();
                        Say("");
                    }
                    catch (Exception e) { Debug.LogException(e); Say("Tienda: error al montar celdas: " + e.GetType().Name + " " + e.Message); }
                }
                else Say("Tienda sin datos. " + (string.IsNullOrEmpty(Services.Status) ? "(sin estado del servicio)" : Services.Status));
            }
            finally { busy = false; }
        }

        // ---- helpers -----------------------------------------------------------------------------------------------
        private void Clear()
        {
            foreach (var go in spawned) if (go != null) { go.SetActive(false); Destroy(go); }
            spawned.Clear(); armed = null;
        }

        private RecoveredUiTree Spawn(string prefab, Transform parent)
        {
            var scene = Array.Find(cells.scenes, s => s.name == prefab);
            if (scene == null) throw new InvalidOperationException("Missing shop prefab " + prefab);
            var tree = RecoveredUiBuilder.Build(cells, scene, parent, frontend.PortraitCamera);
            RecoveredUiBuilder.UseAuthoredLayoutRoot(tree, prefab, parent);
            foreach (var pair in tree.Objects)
            {
                if (pair.Value.name == "Loading" || pair.Value.name == "LoadingIndicator") pair.Value.SetActive(false);
                if (pair.Value.TryGetComponent<Button>(out var b)) b.interactable = false;
            }
            // Parent layout groups size their rows from LayoutElement: without it a nested panel reports no height and overlaps its neighbours.
            var r = scene.objects[0].rect;
            if (r != null && r.Length == 10)
            {
                var le = tree.Root.GetComponent<LayoutElement>() ?? tree.Root.AddComponent<LayoutElement>();
                if (r[7] > 0 && Mathf.Approximately(r[1], r[3])) le.preferredHeight = r[7];
                if (r[6] > 0 && Mathf.Approximately(r[0], r[2])) le.preferredWidth = r[6];
            }
            spawned.Add(tree.Root);
            return tree;
        }

        private static Transform P(RecoveredUiTree t, string path) => t.Root.transform.Find(path);
        private static void SetText(RecoveredUiTree t, string path, string value) { var x = P(t, path); if (x != null && x.TryGetComponent<Text>(out var text)) text.text = value ?? ""; }
        private static void Show(RecoveredUiTree t, string path, bool on) { var x = P(t, path); if (x != null) x.gameObject.SetActive(on); }
        private static void ClearSamples(Transform slot) { for (int i = slot.childCount - 1; i >= 0; i--) { var c = slot.GetChild(i).gameObject; c.SetActive(false); Destroy(c); } }
        private static void FitHeight(Transform t)
        {
            var f = t.GetComponent<ContentSizeFitter>() ?? t.gameObject.AddComponent<ContentSizeFitter>();
            f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static string Remaining(long expirationMs)
        {
            if (expirationMs <= 0) return "";
            var span = TimeSpan.FromMilliseconds(expirationMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (span.TotalSeconds <= 0) return "";
            return span.TotalHours >= 72 ? "Ends in " + (int)Math.Ceiling(span.TotalDays) + " Day(s)" : "Ends in " + (int)Math.Max(1, Math.Ceiling(span.TotalHours)) + " Hour(s)";
        }

        private void BindCover(RecoveredUiTree t, string path, SongImage image)
        {
            var node = P(t, path); if (node == null || !node.TryGetComponent<RawImage>(out var raw) || image == null) return;
            var binding = node.GetComponent<RecoveredCoverBinding>() ?? node.gameObject.AddComponent<RecoveredCoverBinding>();
            binding.Bind(covers, image, raw, null, false, true);
        }

        private void ApplyRarity(RecoveredUiTree t, string coverPath, RecoveredServerRole role)
        {
            var bg = P(t, coverPath); var icon = P(t, coverPath + "/RarityIcon");
            RecoveredRarity.Apply(cells, bg != null ? bg.GetComponent<Image>() : null, icon != null ? icon.GetComponent<Image>() : null, role);
        }

        // ---- layout ------------------------------------------------------------------------------------------------
        private void Populate()
        {
            Clear();
            if (store == null) return;
            // Bind into the authored slots of the original Content (GiftBagBanner, RafflePanel, BFStorePreviewPanel, ...).
            foreach (var name in new[] { "ThemeLotteryPanel", "VSLegacyPanel", "VSLegacyPremiumPanel", "RafflePanel" }) { var slot = content.Find(name); if (slot != null) FitHeight(slot); }
            EnsureSlotHeights();
            AddBanners();
            AddZones("ThemedLotteryPanel", "ThemeLotteryPanel", store.themed_lottery_zone);
            AddZones("VSLegacyPanel", "VSLegacyPanel", store.vs_legacy_zone);
            AddZones("VSLegacyPremiumPanel", "VSLegacyPremiumPanel", store.vs_legacy_premium_zone);
            AddZones("RafflePanel", "RafflePanel", store.lottery_zone_v2);
            AddBfStore();
            AddDiamonds();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            if (bfDialog != null) FillBfDialog();
            StartCoroutine(RelayoutNextFrames());
        }

        // Fonts, sprites and fitters settle a frame later; rebuilding again avoids the stacked/overlapping first paint.
        private IEnumerator RelayoutNextFrames()
        {
            for (int i = 0; i < 3; i++) { yield return null; if (content == null) yield break; Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(content); }
        }

        // Content's VerticalLayoutGroup reads LayoutElement; slots authored with a fixed sizeDelta but no LayoutElement collapse to 0
        // (the BF store preview vanished after the discount bar grew). Give each such slot its authored height.
        private void EnsureSlotHeights()
        {
            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i) as RectTransform; if (child == null) continue;
                if (child.GetComponent<LayoutElement>() != null || child.GetComponent<LayoutGroup>() != null && child.GetComponent<ContentSizeFitter>() != null) continue;
                if (!Mathf.Approximately(child.anchorMin.y, child.anchorMax.y) || child.sizeDelta.y <= 0f) continue;
                child.gameObject.AddComponent<LayoutElement>().preferredHeight = child.sizeDelta.y;
            }
        }

        private void AddDiamonds()
        {
            var rows = content.Find("Content");
            if (rows == null || store.diamond_zone == null) return;
            var rowSlots = new List<Transform>();
            for (int i = 0; i < rows.childCount; i++) if (rows.GetChild(i).name.StartsWith("LayoutGroup")) { ClearSamples(rows.GetChild(i)); rowSlots.Add(rows.GetChild(i)); }
            int n = 0;
            foreach (var sku in store.diamond_zone)
            {
                if (sku == null || rowSlots.Count == 0) continue;
                var t = Spawn("DiamondsPanel", rowSlots[Math.Min(n / 3, rowSlots.Count - 1)]); n++;
                SetText(t, "Text", sku.quantity.ToString());
                SetText(t, "BuyButton/Text", "—"); // real-money purchase: not implemented on Windows
            }
        }

        // ---- banners (swipe carousel) -------------------------------------------------------------------------------
        private sealed class BannerCarousel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public readonly List<RectTransform> Items = new List<RectTransform>();
            public readonly List<Image> Dots = new List<Image>();
            private RectTransform viewport; private int index; private float offset, velocity, autoAt, startOffset, startLocal; private bool dragging;
            public void Init(RectTransform host) { viewport = host; autoAt = Time.unscaledTime; }
            private float Width => Mathf.Max(1f, viewport.rect.width);
            private float Local(PointerEventData e) { RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, e.position, e.pressEventCamera, out var p); return p.x; }
            public void OnBeginDrag(PointerEventData e) { dragging = true; startOffset = offset; startLocal = Local(e); velocity = 0; }
            public void OnDrag(PointerEventData e) { offset = startOffset + (Local(e) - startLocal); }
            public void OnEndDrag(PointerEventData e)
            {
                dragging = false; autoAt = Time.unscaledTime; float threshold = Width * .2f;
                if (offset < -threshold) Step(1); else if (offset > threshold) Step(-1);
            }
            private void Step(int direction) { int n = Items.Count; if (n < 2) return; index = (index + direction + n) % n; offset += direction * Width; autoAt = Time.unscaledTime; }
            private void Update()
            {
                int n = Items.Count; if (n == 0 || viewport == null) return;
                if (!dragging)
                {
                    if (n > 1 && Time.unscaledTime - autoAt > 5f) Step(1);
                    offset = Mathf.SmoothDamp(offset, 0f, ref velocity, .18f, Mathf.Infinity, Time.unscaledDeltaTime);
                }
                float w = Width;
                for (int i = 0; i < n; i++)
                {
                    if (Items[i] == null) continue;
                    int d = ((i - index) % n + n) % n; if (d > n / 2) d -= n; // shortest circular distance
                    Items[i].anchoredPosition = new Vector2(d * w + offset, 0);
                    Items[i].gameObject.SetActive(Mathf.Abs(d * w + offset) < w * 1.01f);
                }
                for (int i = 0; i < Dots.Count; i++) if (Dots[i] != null) Dots[i].color = i == index ? Color.white : new Color(1, 1, 1, .45f);
            }
        }

        private void AddBanners()
        {
            var banner = content.Find("GiftBagBanner");
            var viewport = banner != null ? banner.Find("Viewport") : null;
            var table = banner != null ? banner.Find("Index Table") : null;
            bool any = false;
            if (viewport != null)
            {
                ClearSamples(viewport); if (table != null) ClearSamples(table);
                var carousel = banner.gameObject.GetComponent<BannerCarousel>();
                if (carousel != null) Destroy(carousel);
                carousel = banner.gameObject.AddComponent<BannerCarousel>(); carousel.Init((RectTransform)viewport);
                spawned.Add(null); // placeholder keeps indices stable; the component dies with Populate via Destroy above
                if (store.banners != null)
                    foreach (var b in store.banners) if (b != null && b.enable != 0 && b.role_goods_list != null)
                        foreach (var good in b.role_goods_list) if (good?.role != null)
                        {
                            var root = AddBanner(viewport, b, good); carousel.Items.Add((RectTransform)root.transform); any = true;
                            if (table != null) { var dot = Spawn("BannerIndicator", table); var img = dot.Root.GetComponent<Image>(); if (img != null) carousel.Dots.Add(img); }
                        }
            }
            // No offers: hide the empty carousel and its title instead of leaving a blank gap.
            if (banner != null) banner.gameObject.SetActive(any);
            var title = content.Find("GiftBagTitle"); if (title != null) title.gameObject.SetActive(any);
        }

        private GameObject AddBanner(Transform viewport, RecoveredBanner banner, RecoveredRoleGoods good)
        {
            var t = Spawn("BannerCell", viewport);
            bool owned = good.owned || Services.OwnsRole(new RecoveredBfOperation(RecoveredProfileFlag.BF, good.role.rid));
            int price = good.discount_price ?? good.original_price;
            BindCover(t, "Background", banner.background);
            SetText(t, "Content/OriginalPrice/Price/PriceText", good.original_price.ToString());
            SetText(t, "Content/DiscountPrice/Price/PriceText", price.ToString());
            Show(t, "Content/OriginalPrice", good.discount_price.HasValue && good.discount_price.Value != good.original_price);
            Show(t, "Content", !owned); Show(t, "Owned", owned);
            SetText(t, "RemainTime", Remaining(banner.expiration));
            var button = t.Root.GetComponent<Button>();
            if (button != null)
            {
                var label = P(t, "Content/DiscountPrice/Price/PriceText")?.GetComponent<Text>();
                var b = banner; var g = good; int p = price;
                Wire(button, label, owned || p < 0 ? null : (Action)(() => StartCoroutine(Buy(b.type, b.id, g.role.rid, p))));
            }
            return t.Root;
        }

        // ---- BF store ----------------------------------------------------------------------------------------------
        // BFStoreCell.UpdateDiscount (Ghidra): price = (int)((off * -0.01f + 1f) * originalPrice) in float, truncated; the discount only applies when the role
        // is not owned, available >= 1 and the server time is before expiration. Verified against the decompiled cell.
        private static int DiscountedPrice(int original, RecoveredShopDiscount d) => (int)((d.off.GetValueOrDefault() * -0.01f + 1f) * original);

        private void AddBfStore()
        {
            var goods = store.bf_store?.role_goods_list;
            var bar = content.Find("BFStoreDiscountBar");
            var d = store.bf_store?.discount;
            bool discounted = d != null && d.available.GetValueOrDefault() > 0 && d.off.GetValueOrDefault() > 0;
            BindDiscountBar(bar, d, discounted);
            var preview = content.Find("BFStorePreviewPanel");
            var rowCell = preview != null ? preview.Find("BFStoreRowCell") : null;
            var row = rowCell != null ? rowCell.Find("GameObject") : null;
            if (row == null) return;
            ClearSamples(row);
            if (goods == null) return;
            int shown = 0;
            foreach (var good in goods)
            {
                if (good?.role == null || string.IsNullOrWhiteSpace(good.role.rid)) continue;
                if (shown++ >= 3) break; // the preview row shows three; "Check All" lists the rest
                AddBfCell(row, good, discounted ? d : null);
            }
            var all = content.Find("BFStoreCheckAllButton");
            if (all != null && all.TryGetComponent<Button>(out var allButton))
            {
                allButton.onClick.RemoveAllListeners(); allButton.interactable = goods.Length > 3;
                var captured = goods; var dd = discounted ? d : null;
                allButton.onClick.AddListener(() => { try { OpenBfDialog(); } catch (Exception e) { Debug.LogException(e); Say("Tienda BF: " + e.Message); } });
            }
        }

        private void BindDiscountBar(Transform bar, RecoveredShopDiscount d, bool discounted)
        {
            if (bar == null) return;
            {
                var barElement = bar.GetComponent<LayoutElement>() ?? bar.gameObject.AddComponent<LayoutElement>();
                barElement.preferredHeight = discounted ? 145f : 94f; // BFStoreDiscountBar.FULL_HEIGHT / NO_DISCOUNT_HEIGHT (dump.cs)
                var info = bar.Find("DiscoountContent"); var ask = bar.Find("GetDicountButton");
                if (info != null) info.gameObject.SetActive(discounted);
                if (ask != null)
                {
                    ask.gameObject.SetActive(!discounted);
                    if (ask.TryGetComponent<Button>(out var askButton))
                    {
                        var askLabel = ask.Find("Text") != null ? ask.Find("Text").GetComponent<Text>() : null;
                        Wire(askButton, askLabel, () => StartCoroutine(AskDiscount()));
                    }
                }
                var infoText = info != null ? info.Find("DiscountInfo/DiscountInfoText") : null;
                if (discounted && infoText != null && infoText.TryGetComponent<Text>(out var it))
                    it.text = "<color=#FF5656> " + d.off + " % OFF</color> de descuento por las próximas " + d.available + " compras";
                var remain = info != null ? info.Find("DiscountRemainTime/DiscountRemainTimeText") : null;
                if (discounted && remain != null && remain.TryGetComponent<Text>(out var rt))
                {
                    var span = TimeSpan.FromMilliseconds(d.expiration - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    rt.text = "Ends in <color=#FFCC00>" + Math.Max(0, (int)Math.Ceiling(span.TotalDays)) + " días</color>";
                }
            
            }
        }

        private IEnumerator AskDiscount()
        {
            if (busy) yield break;
            busy = true;
            try
            {
                RecoveredShopDiscount result = null;
                yield return Services.RequestShopDiscount(v => result = v);
                if (result != null && store?.bf_store != null) { store.bf_store.discount = result; Populate(); Say("Descuento: " + result.off + " % en las próximas " + result.available + " compras."); }
                else Say((Services.Status ?? "") + " No se reintenta.");
            }
            finally { busy = false; }
        }

        // ---- BFStoreDialog ("Comprobar todo"; original prefab from heavydialog10) -----------------------------------------------
        private GameObject bfDialog;
        private RecoveredUiData bfStoreData;

        private void OpenBfDialog()
        {
            if (bfStoreData == null)
            {
                var a = Resources.Load<TextAsset>("RecoveredBfStore606/bfstore"); if (a == null) throw new InvalidOperationException("BFStoreDialog unavailable");
                bfStoreData = JsonUtility.FromJson<RecoveredUiData>(a.text);
            }
            CloseBfDialog();
            var scene = Array.Find(bfStoreData.scenes, x => x.name == "BFStoreDialog");
            var tree = RecoveredUiBuilder.Build(bfStoreData, scene, frontend.Navigation.Pages[MainPage.Shop].Root.transform, frontend.PortraitCamera);
            RecoveredUiBuilder.UseAuthoredLayoutRoot(tree, "BFStoreDialog", frontend.Navigation.Pages[MainPage.Shop].Root.transform);
            bfDialog = tree.Root;
            // The authored root has no RectTransform (plain Transform, rect = []); its Canvas child carries the layout.
            var rect = bfDialog.transform as RectTransform; if (rect != null) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
            bfDialog.transform.SetAsLastSibling();
            foreach (var pair in tree.Objects) if (pair.Value.TryGetComponent<Button>(out var b)) b.interactable = false;
            var back = bfDialog.transform.Find("Canvas/Container/BackButton")?.GetComponent<Button>();
            if (back != null) { back.interactable = true; back.onClick.RemoveAllListeners(); back.onClick.AddListener(CloseBfDialog); }
            FillBfDialog();
        }

        private void CloseBfDialog() { if (bfDialog != null) { bfDialog.SetActive(false); Destroy(bfDialog); bfDialog = null; } }

        private void FillBfDialog()
        {
            if (bfDialog == null || store?.bf_store == null) return;
            var d = store.bf_store.discount;
            bool discounted = d != null && d.available.GetValueOrDefault() > 0 && d.off.GetValueOrDefault() > 0;
            var diamonds = bfDialog.transform.Find("Canvas/Container/UserDiamondBar/DiamondsText");
            if (diamonds != null && diamonds.TryGetComponent<Text>(out var dt) && frontend.Account?.Wealth != null) dt.text = frontend.Account.Wealth.diamond.ToString();
            BindDiscountBar(bfDialog.transform.Find("Canvas/Container/Cotent/BFStoreDiscountBar"), d, discounted);
            var grid = bfDialog.transform.Find("Canvas/Container/Cotent/ScrollView/Viewport/Content");
            ClearSamples(grid);
            var g = grid.GetComponent<GridLayoutGroup>() ?? grid.gameObject.AddComponent<GridLayoutGroup>();
            var scene = Array.Find(cells.scenes, x => x.name == "BFStoreCell"); var size = scene.objects[0].rect;
            float width = Mathf.Max(300f, ((RectTransform)grid.parent).rect.width > 10f ? ((RectTransform)grid.parent).rect.width : 1080f);
            float cell = Mathf.Floor((width - 36f - 2 * 4f) / 3f);
            g.cellSize = new Vector2(cell, size[7]); g.spacing = new Vector2(4, 8); g.padding = new RectOffset(18, 18, 0, 20);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 3; g.childAlignment = TextAnchor.UpperCenter;
            var fit = grid.GetComponent<ContentSizeFitter>() ?? grid.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var cellsInGrid = new List<GameObject>();
            if (store.bf_store.role_goods_list != null)
                foreach (var good in store.bf_store.role_goods_list)
                    if (good?.role != null && !string.IsNullOrWhiteSpace(good.role.rid)) AddBfCell(grid, good, discounted ? d : null);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)grid);
        }

        private void AddBfCell(Transform parent, RecoveredRoleGoods good, RecoveredShopDiscount discount)
        {
            var t = Spawn("BFStoreCell", parent);
            bool owned = good.owned || Services.OwnsRole(new RecoveredBfOperation(RecoveredProfileFlag.BF, good.role.rid));
            bool sale = discount != null && !owned && discount.available.GetValueOrDefault() > 0 && discount.off.GetValueOrDefault() > 0 && (discount.expiration <= 0 || discount.expiration > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            int price = sale ? DiscountedPrice(good.original_price, discount) : good.original_price;
            SetText(t, "Container/SaleInfo/Name", good.role.title ?? good.role.rid);
            SetText(t, "Container/SaleInfo/Original/OriginalPrice/Price/PriceText", good.original_price.ToString());
            SetText(t, "Container/SaleInfo/Price/Price/PriceText", price.ToString());
            Show(t, "Container/SaleInfo/Original", sale);
            Show(t, "Container/SaleInfo/Price", !owned); Show(t, "Container/SaleInfo/Owned", owned);
            Show(t, "Container/Discount", sale);
            if (sale) SetText(t, "Container/Discount/DiscountText", discount.off + "% off");
            Show(t, "Container/BFCover/UpgradeIcon", false);
            ApplyRarity(t, "Container/BFCover", good.role);
            BindCover(t, "Container/BFCover/BFIcon", good.role.icon);
            var button = t.Root.GetComponent<Button>(); if (button == null) return;
            var label = P(t, "Container/SaleInfo/Price/Price/PriceText")?.GetComponent<Text>();
            var g = good; int p = price;
            Wire(button, label, owned || good.original_price < 0 ? null : (Action)(() => StartCoroutine(Buy(2, g.role.rid, g.role.rid, p))));
        }

        // ---- lotteries ---------------------------------------------------------------------------------------------
        private void AddZones(string prefab, string slotName, RecoveredRaffleZone[] zones)
        {
            var slot = content.Find(slotName); if (slot == null) return;
            ClearSamples(slot);
            if (zones == null) return;
            foreach (var zone in zones)
            {
                if (zone?.payments == null || string.IsNullOrEmpty(zone.id)) continue;
                var t = Spawn(prefab, slot); var z = zone;
                if (prefab == "RafflePanel") BindRaffle(t, zone);
                else
                {
                    string path = prefab == "ThemedLotteryPanel" ? "Image/Canvas/Button" : "bg/Button";
                    var button = P(t, path)?.GetComponent<Button>(); if (button == null) continue;
                    bool legacy = prefab == "VSLegacyPanel" || prefab == "VSLegacyPremiumPanel";
                    if (legacy)
                    {
                        try { BindLegacyPanel(t, zone, prefab == "VSLegacyPremiumPanel"); } catch (Exception e) { Debug.LogException(e); }
                        // "Get" opens the original lottery dialog (VSLegacyLotteryDialog); the draw itself happens there with its own confirmation.
                        bool premium = prefab == "VSLegacyPremiumPanel";
                        button.interactable = legacyDialog != null; button.transition = Selectable.Transition.None; button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(() => { try { legacyDialog.Open(z, premium, () => { loadedAt = 0; Reload(); }); } catch (Exception e) { Debug.LogException(e); Say("Diálogo Legacy: " + e.Message); } });
                        continue;
                    }
                    if (prefab == "ThemedLotteryPanel") { try { BindThemedPanel(t, zone); } catch (Exception e) { Debug.LogException(e); } }
                    if (prefab == "ThemedLotteryPanel" && themedDialog != null)
                    {
                        button.interactable = true; button.transition = Selectable.Transition.None; button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(() => { try { themedDialog.Open(z, () => { loadedAt = 0; Reload(); }); } catch (Exception e) { Debug.LogException(e); Say("Lotería temática: " + e.Message); } });
                        continue;
                    }
                    var payment = zone.payments.Length > 0 ? zone.payments[0] : null; if (payment == null) continue;
                    var label = P(t, path + "/Text")?.GetComponent<Text>();
                    Wire(button, label, () => StartCoroutine(Spin(z, payment)));
                }
            }
        }

        // ---- VS Legacy panels (VSLegacyPanel / VSLegacyPremiumPanel.Refresh) ----------------------------------------------------
        private static Transform Deep(Transform root, string name) { foreach (var c in root.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c; return null; }

        // ThemedLotteryPanel: Image/PrefabParent hosts the CDN cover prefab (zone.cover_res_pack, e.g. android_voidMatt_cover_*), RemainingTime is the countdown.
        // The cover pack only exports static images (bg/title/ex); its animated particle children have no exported components.
        private void BindThemedPanel(RecoveredUiTree t, RecoveredRaffleZone zone)
        {
            var load = Deep(t.Root.transform, "LoadingIndicator"); if (load != null) load.gameObject.SetActive(false);
            var remain = Deep(t.Root.transform, "RemainingTime");
            if (remain != null && remain.TryGetComponent<Text>(out var rt)) rt.text = RecoveredLegacyDialog.RemainingText(zone.expiration);
            var slot = P(t, "Image/PrefabParent"); if (slot == null || zone.cover_res_pack == null) return;
            var pack = RecoveredUiPacks.Load(zone.cover_res_pack);
            if (pack == null || pack.scenes == null || pack.scenes.Length == 0) { Debug.LogWarning("[BBPC.Shop] " + RecoveredUiPacks.LastError); return; }
            var sub = RecoveredUiBuilder.Build(pack, pack.scenes[0], slot, frontend.PortraitCamera);
            RecoveredUiBuilder.UseAuthoredLayoutRoot(sub, pack.scenes[0].name, slot);
            foreach (var pair in sub.Objects) if (pair.Value.TryGetComponent<Graphic>(out var g)) g.raycastTarget = false;
        }

        private void BindLegacyPanel(RecoveredUiTree t, RecoveredRaffleZone zone, bool premium)
        {
            var remain = Deep(t.Root.transform, "RemainingTime");
            if (remain != null && remain.TryGetComponent<Text>(out var rt)) rt.text = RecoveredLegacyDialog.RemainingText(zone.expiration);
            RecoveredLegacyDialog.ApplyProbability(Deep(t.Root.transform, "ProbabilityParent"), zone);
            if (!premium) { if (legacyDialog != null) legacyDialog.AddExhibition(P(t, "bg/Container/PrefabParent"), zone, frontend, null); return; }
            // Premium: RoleDisplayShelf shows the rotate.display_rewards roles in the three RoleShowCase slots.
            var shelf = P(t, "bg/RoleShelf"); if (shelf == null || zone.rotate?.display_rewards == null || zone.rotate.all_rewards == null) return;
            for (int i = 0; i < shelf.childCount && i < zone.rotate.display_rewards.Length; i++)
            {
                var slot = shelf.GetChild(i); RecoveredServerRole role = null;
                foreach (var reward in zone.rotate.all_rewards) if (reward?.reward?.role != null && reward.reward.role.rid == zone.rotate.display_rewards[i]) { role = reward.reward.role; break; }
                if (role == null) continue;
                var name = slot.Find("Name"); if (name != null && name.TryGetComponent<Text>(out var nt)) nt.text = role.title ?? role.rid;
                var back = slot.Find("BFCoverBack");
                if (back != null)
                {
                    var icon = back.Find("BFIcon"); var rarity = back.Find("RarityIcon");
                    if (icon != null) { icon.gameObject.SetActive(true); if (icon.TryGetComponent<RawImage>(out var raw) && role.icon != null) { var b = icon.GetComponent<RecoveredCoverBinding>() ?? icon.gameObject.AddComponent<RecoveredCoverBinding>(); b.Bind(covers, role.icon, raw, null, false, true); } }
                    RecoveredRarity.Apply(cells, back.GetComponent<Image>(), rarity != null ? rarity.GetComponent<Image>() : null, role);
                    if (rarity != null) rarity.gameObject.SetActive(true);
                }
                var obtained = slot.Find("Parent/Obtained"); if (obtained != null) obtained.gameObject.SetActive(Services.OwnsRole(new RecoveredBfOperation(RecoveredProfileFlag.BF, role.rid)));
            }
        }

        // ---- RafflePanel (RafflePanel.RefreshUI / RefreshProgress, decompiled) ---------------------------------------------------
        // title = featured role (probabilities[0]); subtitle = remaining time; colours from zone.config; one NormalSlider per progress
        // (icon, "progress/guarantee"); GetText = guarantee - progress of the last unfinished track; two draw buttons by guess (1, 10).
        // A button pays with BF tickets when zone.can_use_bf_ticket and wealth.bf_draw_ticket >= guess (PType.BFTicket = 5, cost = guess as in the video).
        private static Color Hex(string value, Color fallback) => !string.IsNullOrEmpty(value) && ColorUtility.TryParseHtmlString(value, out var c) ? c : fallback;
        private RecoveredServerRole Featured(RecoveredRaffleZone zone)
        {
            if (zone.probabilities != null) foreach (var p in zone.probabilities) if (p?.rewards != null) foreach (var r in p.rewards) if (r?.role != null) return r.role;
            return null;
        }

        private void BindRaffle(RecoveredUiTree t, RecoveredRaffleZone zone)
        {
            var z = zone;
            if (t.Root.TryGetComponent<Image>(out var bg)) bg.color = Hex(zone.config?.bg_color, bg.color);
            var edge = P(t, "EdgeImage"); if (edge != null && edge.TryGetComponent<Image>(out var ei)) { edge.gameObject.SetActive(true); ei.color = Hex(zone.config?.edge_color, ei.color); }
            var featured = Featured(zone);
            var title = P(t, "TitleText")?.GetComponent<Text>();
            if (title != null) { title.text = featured?.title ?? ""; title.color = Hex(zone.config?.title_color, title.color); }
            SetText(t, "SubTitleText", Remaining(zone.expiration).ToUpperInvariant());
            if (featured != null)
            {
                var cover = P(t, "ShowImage/BFImage"); if (cover != null) { cover.gameObject.SetActive(true); BindCover(t, "ShowImage/BFImage", featured.icon); }
                var rarity = P(t, "ShowImage/RarityImage");
                if (rarity != null && rarity.TryGetComponent<Image>(out var ri)) { RecoveredRarity.Apply(cells, null, ri, featured); rarity.gameObject.SetActive(ri.enabled); }
            }
            var content = P(t, "SliderScrollView/Viewport/Content");
            RecoveredProgressInfo open = null;
            if (content != null && zone.progresses != null && sliderData != null)
            {
                ClearSamples(content);
                var scene = sliderData.scenes[0];
                foreach (var progress in zone.progresses)
                {
                    if (progress == null) continue;
                    var s = RecoveredUiBuilder.Build(sliderData, scene, content, frontend.PortraitCamera);
                    RecoveredUiBuilder.UseAuthoredLayoutRoot(s, "NormalSlider", content);
                    spawned.Add(s.Root);
                    SetText(s, "Text", progress.progress + "/" + progress.guarantee);
                    BindCover(s, "BfImage", progress.icon);
                    float ratio = progress.guarantee > 0 ? Mathf.Clamp01((float)progress.progress / progress.guarantee) : 0f;
                    if (s.Root.TryGetComponent<Slider>(out var slider)) slider.value = ratio;
                    else { var fill = P(s, "Fill Area/Fill") as RectTransform; if (fill != null) fill.anchorMax = new Vector2(ratio, fill.anchorMax.y); }
                    if (progress.status == 0) open = progress;
                }
            }
            var get = P(t, "GetText")?.GetComponent<Text>();
            if (get != null) { get.gameObject.SetActive(open != null && open.guarantee > 0); if (open != null) get.text = "100% GET IN " + Math.Max(0, open.guarantee - open.progress) + " SORTEOS"; }
            Show(t, "ButtonsContainer/AdButton", false); Show(t, "ExplainButton", true);
            if (P(t, "ExplainButton") is Transform ex && ex.TryGetComponent<Button>(out var exb)) { exb.interactable = explainDialog != null; exb.onClick.RemoveAllListeners(); exb.onClick.AddListener(() => { try { explainDialog.Open(zone); } catch (Exception e) { Debug.LogException(e); } }); }
            int tickets = frontend.Account?.Wealth != null ? frontend.Account.Wealth.bf_draw_ticket : 0;
            foreach (int guess in new[] { 1, 10 })
            {
                string name = guess == 1 ? "ButtonsContainer/1Button" : "ButtonsContainer/10Button";
                RecoveredPayment diamond = null;
                if (zone.payments != null) foreach (var p in zone.payments) if (p != null && p.guess == guess && p.ptype == 1) { diamond = p; break; }
                if (diamond == null) { Show(t, name, false); continue; }
                bool ticket = zone.can_use_bf_ticket && tickets >= guess;
                var paid = ticket ? new RecoveredPayment { guess = guess, ptype = 5, quantity = guess } : diamond;
                Show(t, name, true);
                SetText(t, name + "/Text", "HAZ UN DRAW " + guess + " VECES");
                SetText(t, name + "/parent/Text", paid.quantity.ToString());
                Show(t, name + "/parent/Diamond", !ticket); Show(t, name + "/parent/Ticket", ticket);
                var button = P(t, name)?.GetComponent<Button>(); if (button == null) continue;
                var label = P(t, name + "/Text")?.GetComponent<Text>(); var pay = paid;
                Wire(button, label, () => StartCoroutine(Spin(z, pay)));
            }
        }

        // ---- confirmation + requests -------------------------------------------------------------------------------
        // Two-step confirmation. A null action leaves the button disabled (owned, unconfirmed price, etc.).
        private void Wire(Button button, Text label, Action action)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = action != null; if (action == null) return;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                if (busy) return;
                if (armed == button && Time.unscaledTime - armedAt < ArmSeconds) { Disarm(); action(); return; }
                Disarm(); armed = button; armedAt = Time.unscaledTime; armedLabel = label; armedText = label != null ? label.text : null;
                if (label != null) label.text = "¿Confirmar?";
                Say("Pulsa de nuevo para confirmar: gasta saldo, sin reintento automático.");
            });
        }

        private void Disarm()
        {
            if (armedLabel != null && armedText != null) armedLabel.text = armedText;
            armed = null; armedLabel = null; armedText = null;
        }

        private void Update() { if (armed != null && Time.unscaledTime - armedAt >= ArmSeconds) Disarm(); }

        private IEnumerator Buy(int type, string id, string rid, int diamond)
        {
            if (busy) yield break;
            busy = true;
            try
            {
                RecoveredPurchaseResultData result = null;
                yield return Services.PurchaseGoods(type, id, new[] { rid }, diamond, v => result = v);
                if (result != null) { yield return Reconcile(); Say("Compra respondida; saldo e inventario actualizados."); }
                else Say((Services.Status ?? "") + " No se reintenta: revisa inventario antes de otra compra.");
            }
            finally { busy = false; }
        }

        private IEnumerator Spin(RecoveredRaffleZone zone, RecoveredPayment payment)
        {
            if (busy) yield break;
            busy = true;
            try
            {
                RecoveredLotteryResultData result = null;
                yield return Services.SpinLottery(zone.id, payment.guess, payment.ptype, v => result = v);
                if (result != null)
                {
                    var names = new List<string>();
                    if (result.rewards != null) foreach (var r in result.rewards) if (r?.role != null) names.Add(r.role.title ?? r.role.rid);
                    yield return Reconcile();
                    if (legacyDialog != null && legacyDialog.Rewards != null) { int skipped = legacyDialog.Rewards.Show(result.rewards, null); Say(skipped > 0 ? skipped + " premio(s) no soportado(s) sin mostrar." : ""); }
                    else Say(names.Count > 0 ? "Premios: " + string.Join(", ", names) : "Ruleta respondida; revisa inventario.");
                }
                else Say((Services.Status ?? "") + " No se reintenta: revisa inventario antes de otro giro.");
            }
            finally { busy = false; }
        }

        private IEnumerator Reconcile()
        {
            yield return Services.RefreshWealth(WealthRefreshTrigger.Payment, v => { });
            yield return Services.RefreshOwnRoles(v => { });
            RecoveredShopPayload fresh = null;
            yield return Services.FetchShopGoods(v => fresh = v);
            if (fresh != null) { store = fresh; loadedAt = Time.unscaledTime; Populate(); }
        }
    }
}
