using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    // Windows controls over the recovered contracts; artwork remains CDN-backed.
    public sealed class RecoveredShopView : MonoBehaviour
    {
        public enum PanelKind { Skins, Store }
        public PanelKind Kind { get; private set; }
        public RecoveredFrontend Frontend { get; private set; }
        public bool IsOpen => root != null && root.activeInHierarchy;
        private sealed class Spec
        {
            public string title, info, action;
            public bool enabled = true, armed;
            public Action act;
        }
        private const float RowHeight = 84, NavBand = 160;
        private GameObject root;
        private Text header, status;
        private RectTransform content;
        private ScrollRect scroll;
        private Font font;
        private Button[] tabs;
        private int tab;
        private bool busy;
        private RecoveredShopPayload store;
        private readonly List<Spec> specs = new List<Spec>();
        private readonly List<GameObject> rows = new List<GameObject>();
        private RecoveredServiceClient Services => Frontend.Services;

        public void Build(RecoveredFrontend frontend, PanelKind kind)
        {
            if (root != null) return;
            Frontend = frontend; Kind = kind;
            font = FindFont(frontend);
            root = new GameObject(kind == PanelKind.Skins ? "Windows skins panel" : "Windows store panel",
                typeof(RectTransform), typeof(Canvas), typeof(RecoveredCanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(frontend.MainRoom.Root.transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = frontend.PortraitCamera;
            canvas.planeDistance = 100; canvas.sortingOrder = 10;
            var scaler = root.GetComponent<RecoveredCanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(828, 1792); scaler.matchWidthOrHeight = 1;
            scaler.referencePixelsPerUnit = 100;
            var background = Image(root.transform, "Background", new Color(.025f, .025f, .045f, .98f));
            Stretch(background.rectTransform);
            header = Label(root.transform, "Header", kind == PanelKind.Skins ? "Skins" : "Tienda", 40);
            Place(header.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), new Vector2(-220, 56), new Vector2(-100, -16));
            var reload = MakeButton(root.transform, "Actualizar", Reload);
            Place(reload.GetComponent<RectTransform>(), Vector2.one, Vector2.one, Vector2.one, new Vector2(190, 52), new Vector2(-16, -18));
            status = Label(root.transform, "Status", "", 22);
            Place(status.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(.5f, 0), new Vector2(-24, 60), new Vector2(0, NavBand + 4));
            if (kind == PanelKind.Skins)
            {
                tabs = new Button[3];
                var names = new[] { "BF", "GF", "Revive" };
                for (int i = 0; i < tabs.Length; i++)
                {
                    int index = i;
                    tabs[i] = MakeButton(root.transform, names[i], () => { tab = index; Refresh(); scroll.verticalNormalizedPosition = 1; });
                    Place(tabs[i].GetComponent<RectTransform>(), new Vector2(i / 3f, 1), new Vector2((i + 1) / 3f, 1), new Vector2(.5f, 1), new Vector2(-16, 52), new Vector2(0, -76));
                }
            }
            var scrollImage = Image(root.transform, "Scroll", new Color(0, 0, 0, .12f));
            Stretch(scrollImage.rectTransform); scrollImage.rectTransform.offsetMin = new Vector2(0, NavBand + 72);
            scrollImage.rectTransform.offsetMax = new Vector2(0, kind == PanelKind.Skins ? -140 : -80);
            scroll = scrollImage.gameObject.AddComponent<ScrollRect>();
            var viewport = Image(scroll.transform, "Viewport", Color.white);
            viewport.color = new Color(1, 1, 1, .01f); Stretch(viewport.rectTransform);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport.transform, false);
            Place(content, new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), Vector2.zero, Vector2.zero);
            scroll.viewport = viewport.rectTransform; scroll.content = content; scroll.horizontal = false;
            scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Elastic;
            root.SetActive(false);
        }

        public void SetOpen(bool open)
        {
            if (root == null) return;
            bool opening = open && !root.activeSelf;
            root.SetActive(open);
            if (opening) { Refresh(); Reload(); }
        }

        public void Refresh()
        {
            if (root == null) return;
            specs.Clear();
            if (Kind == PanelKind.Skins) SkinSpecs(); else StoreSpecs();
            if (specs.Count == 0) specs.Add(new Spec { title = "Sin datos confirmados", info = "Actualizar para consultar la cuenta propia.", enabled = false });
            if (tabs != null) for (int i = 0; i < tabs.Length; i++) tabs[i].interactable = !busy && i != tab;
            RenderRows();
        }

        private void Reload() { if (!busy) StartCoroutine(Load()); }
        private IEnumerator Load()
        {
            busy = true; SetStatus("Consultando..."); Refresh();
            try
            {
                if (Kind == PanelKind.Store) yield return Services.FetchShopGoods(value => store = value);
                else yield return Services.RefreshOwnRoles(value => { });
                SetStatus(Kind == PanelKind.Store && store != null ? "Tienda actualizada; precios sujetos a confirmación." : Services.Status);
            }
            finally { busy = false; Refresh(); }
        }

        private RecoveredProfileFlag Flag => tab == 0 ? RecoveredProfileFlag.BF : tab == 1 ? RecoveredProfileFlag.GF : RecoveredProfileFlag.Revive;
        private void SkinSpecs()
        {
            var own = Frontend.Account.ServerRoles;
            if (own == null) return;
            var roles = tab == 0 ? own.bfs : tab == 1 ? own.gfs : own.revives;
            var defaultRole = tab == 0 ? own.default_bf : tab == 1 ? own.default_gf : own.default_revive;
            var seen = new HashSet<string>();
            if (defaultRole != null) AddSkin(defaultRole, seen);
            if (roles != null) foreach (var item in roles) if (item?.role != null) AddSkin(item.role, seen);
            if (tab != 0) specs.Add(new Spec { title = "Contrato inferido", info = "GF/Revive: mflag conocido; cuerpo sin captura de equipamiento.", enabled = false });
        }
        private void AddSkin(RecoveredServerRole role, HashSet<string> seen)
        {
            if (string.IsNullOrWhiteSpace(role.rid) || !seen.Add(role.rid)) return;
            var operation = new RecoveredBfOperation(Flag, role.rid);
            bool owned = Services.OwnsRole(operation), selected = Services.SelectedRole(Flag) == role.rid;
            specs.Add(new Spec { title = role.title ?? role.rid, info = "R" + (role.rarity_v2 ?? role.rarity) + " · " + (selected ? "EN USO" : owned ? "DISPONIBLE" : "BLOQUEADO"),
                action = selected ? "USANDO" : owned ? "Equipar" : "BLOQUEADO", enabled = owned && !selected,
                act = () => StartCoroutine(Equip(operation)) });
        }

        private void StoreSpecs()
        {
            if (store == null) return;
            if (store.bf_store != null)
            {
                var discount = store.bf_store.discount;
                if (discount != null) specs.Add(new Spec { title = "Descuento de tienda", info = "off: " + discount.off + " · disponibles: " + discount.available + " · precio final no observado", enabled = false });
                AddGoods(store.bf_store.role_goods_list);
            }
            AddZones("Ruleta", store.lottery_zone_v2); AddZones("Temática", store.themed_lottery_zone);
            AddZones("Versus legacy", store.vs_legacy_zone); AddZones("Versus premium", store.vs_legacy_premium_zone);
            if (store.banners != null) foreach (var banner in store.banners)
            {
                if (banner == null || banner.enable == 0) continue;
                specs.Add(new Spec { title = banner.title ?? "Oferta", info = "Oferta recibida del servidor", enabled = false });
                AddGoods(banner.role_goods_list);
            }
            if (store.diamond_zone != null) foreach (var sku in store.diamond_zone)
            {
                if (sku == null) continue;
                specs.Add(new Spec { title = sku.quantity + " diamantes", info = "Compra de dinero real no implementada en Windows.", action = "No disponible", enabled = false });
            }
        }
        private void AddGoods(RecoveredRoleGoods[] goods)
        {
            if (goods == null) return;
            foreach (var item in goods)
            {
                if (item?.role == null || string.IsNullOrWhiteSpace(item.role.rid)) continue;
                var good = item; var role = good.role;
                var operation = new RecoveredBfOperation(RecoveredProfileFlag.BF, role.rid);
                bool owned = good.owned || Services.OwnsRole(operation);
                bool selected = Services.SelectedRole(RecoveredProfileFlag.BF) == role.rid;
                var spec = new Spec { title = role.title ?? role.rid, info = owned ? "ADQUIRIDO" : good.original_price + " diamantes (precio original)",
                    action = owned ? selected ? "USANDO" : "Equipar BF" : "Comprar", enabled = owned ? !selected : good.original_price >= 0 };
                if (owned) spec.act = () => StartCoroutine(Equip(operation));
                else spec.act = () => Confirm(spec, () => StartCoroutine(Buy(good)));
                specs.Add(spec);
            }
        }
        private void AddZones(string title, RecoveredRaffleZone[] zones)
        {
            if (zones == null) return;
            foreach (var zone in zones)
            {
                if (zone?.payments == null) continue;
                foreach (var payment in zone.payments)
                {
                    if (payment == null) continue;
                    var z = zone; var p = payment;
                    var spec = new Spec { title = title, info = "Cantidad: " + p.quantity + " · ptype: " + p.ptype + " · guess: " + p.guess, action = "Girar", enabled = !string.IsNullOrEmpty(z.id) };
                    spec.act = () => Confirm(spec, () => StartCoroutine(Spin(z, p))); specs.Add(spec);
                }
            }
        }
        private void Confirm(Spec spec, Action action)
        {
            if (busy) return;
            if (!spec.armed)
            {
                foreach (var row in specs) if (row.armed) { row.armed = false; row.action = "Revisar"; }
                spec.armed = true; spec.action = "Confirmar"; SetStatus("Confirmar envía una operación que puede gastar saldo. Sin reintento automático."); RenderRows();
            }
            else { spec.armed = false; action(); }
        }
        private IEnumerator Equip(RecoveredBfOperation operation)
        {
            if (busy) yield break;
            if (RecoveredStorage.Isolated || !Services.Identity.HasPair || !Services.TimeSynced || !Services.ProfileVerified || !Services.OwnRolesVerified || Services.BfChangeRunning || !Services.OwnsRole(operation))
            { SetStatus("Equipar requiere perfil y propiedad verificados; no se modifica el inventario local."); yield break; }
            busy = true; Refresh();
            try
            {
                bool accepted = false; yield return Services.ChangeRoleEquip(operation, value => accepted = value);
                SetStatus(accepted && Services.SelectedRole(operation.Flag) == operation.Rid ? "Skin equipada y confirmada." : Services.Status);
            }
            finally { busy = false; Refresh(); }
        }
        private IEnumerator Buy(RecoveredRoleGoods good)
        {
            if (busy) yield break;
            busy = true; Refresh();
            try
            {
                RecoveredPurchaseResultData result = null;
                yield return Services.PurchaseGoods(2, good.role.rid, new[] { good.role.rid }, good.original_price, value => result = value);
                if (result != null) { yield return Reconcile(); SetStatus("Compra respondida; revisa saldo e inventario actualizados."); }
                else SetStatus(Services.Status + " No se reintenta: consulta inventario antes de otra compra.");
            }
            finally { busy = false; Refresh(); }
        }
        private IEnumerator Spin(RecoveredRaffleZone zone, RecoveredPayment payment)
        {
            if (busy) yield break;
            busy = true; Refresh();
            try
            {
                RecoveredLotteryResultData result = null;
                yield return Services.SpinLottery(zone.id, payment.guess, payment.ptype, value => result = value);
                if (result != null) { yield return Reconcile(); SetStatus("Ruleta respondida; premios comprobables en inventario."); }
                else SetStatus(Services.Status + " No se reintenta: consulta inventario antes de otro giro.");
            }
            finally { busy = false; Refresh(); }
        }
        private IEnumerator Reconcile()
        {
            yield return Services.RefreshWealth(WealthRefreshTrigger.Payment, value => { });
            yield return Services.RefreshOwnRoles(value => { });
            yield return Services.FetchShopGoods(value => store = value);
        }
        private void RenderRows()
        {
            foreach (var row in rows) { row.SetActive(false); if (Application.isPlaying) Destroy(row); else DestroyImmediate(row); }
            rows.Clear(); content.sizeDelta = new Vector2(0, specs.Count * RowHeight);
            for (int i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                var background = Image(content, "Row" + i, i % 2 == 0 ? new Color(.13f, .14f, .2f) : new Color(.08f, .09f, .14f));
                Place(background.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), new Vector2(0, RowHeight), new Vector2(0, -i * RowHeight));
                rows.Add(background.gameObject);
                var title = Label(background.transform, "Title", spec.title, 26);
                Place(title.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(300, 64), new Vector2(16, 0));
                var info = Label(background.transform, "Info", spec.info, 20);
                info.rectTransform.anchorMin = new Vector2(0, .5f); info.rectTransform.anchorMax = new Vector2(1, .5f);
                info.rectTransform.offsetMin = new Vector2(316, -32); info.rectTransform.offsetMax = new Vector2(-196, 32);
                if (!string.IsNullOrEmpty(spec.action))
                {
                    var button = MakeButton(background.transform, spec.action, () => { if (!busy) spec.act?.Invoke(); });
                    Place(button.GetComponent<RectTransform>(), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(170, 52), new Vector2(-16, 0));
                    button.interactable = spec.enabled && !busy;
                }
            }
        }
        private Text Label(Transform parent, string name, string value, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>(); text.font = font; text.text = value ?? ""; text.fontSize = size;
            text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; text.supportRichText = false;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 12; text.resizeTextMaxSize = size; return text;
        }
        private Button MakeButton(Transform parent, string title, Action action)
        {
            var image = Image(parent, title, new Color(.22f, .25f, .39f));
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            var label = Label(image.transform, "Text", title, 22); Stretch(label.rectTransform); return button;
        }
        private static Image Image(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = true; return image;
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 size, Vector2 position)
        { rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position; }
        private static Font FindFont(RecoveredFrontend frontend)
        {
            foreach (var asset in frontend.Data.assets) if (asset.type == "Font") { var loaded = Resources.Load<Font>(asset.resource); if (loaded != null) return loaded; }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        private void SetStatus(string message) { if (status != null) status.text = message ?? ""; }
    }
}
