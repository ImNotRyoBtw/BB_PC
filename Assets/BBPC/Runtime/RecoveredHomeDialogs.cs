using System;
using System.Collections;
using BBPC.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    // Original Settings hierarchy with Windows bindings; remaining dialogs use legacy adapters.
    // Evidence: LogReal606 home routes; 6.0.6 PermanentExtraHPDialog fields; existing read DTOs.
    public sealed partial class RecoveredHomeDialogs : MonoBehaviour
    {
        private RecoveredFrontend host;
        private GameObject root;
        private RectTransform content;
        private ScrollRect scroll;
        private Font font;
        private int generation;
        private bool se, bgm;
        private long battlePassExpiration;
        private bool battlePassRewardsBound;
        private AudioSource[] audioSources;
        private readonly System.Collections.Generic.HashSet<string> pendingClaims = new System.Collections.Generic.HashSet<string>();
        private readonly System.Collections.Generic.HashSet<string> confirmedClaims = new System.Collections.Generic.HashSet<string>();
        private const string PreferencePrefix = "BBPC.Windows.Settings.";
        public bool IsOpen => root != null && root.activeSelf;
        public Action<string> CurrentOriginalTabChanged;

        public void Initialize(RecoveredFrontend frontend)
        {
            host = frontend;
            foreach (var asset in host.Data.assets)
                if (asset.type == "Font") { font = Resources.Load<Font>(asset.resource); if (font != null) break; }
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            se = Preference("SE"); bgm = Preference("BGM"); ApplyAudio();
        }

        private static bool Preference(string key) => RecoveredPreferences.GetInt(PreferencePrefix + key, 1) != 0;
        private void LateUpdate() { ApplyAudio(); }
        private void Update()
        {
            if(IsOpen&&Input.GetKeyDown(KeyCode.Escape)){if(leaderboardRulesRoot!=null)CloseLeaderboardRules();else if(rewardRoot!=null)CloseClaimReward();else Close();}
            if(IsOpen&&root.name=="BattlePassDialog")
            {
                var now=host.Services.CurrentServerTimestamp;
                BindBattlePassCountdown(battlePassExpiration,now);
            }
        }
        internal void BindBattlePassCountdown(long expiration,long? serverTimestamp)
        {
            battlePassExpiration=expiration;
            RequirePath(root.transform,"TopPanel/TopPanelRight/RemainDay/RemainDayText").GetComponent<Text>().text=
                serverTimestamp.HasValue&&expiration>0?RecoveredBattlePassHelpers.RemainingTime(expiration-serverTimestamp.Value)??"":"";
        }
        private void ApplyAudio()
        {
            // RecoveredPlayRoom.Initialize creates one playback source and one overlay effect source.
            // Muting preserves the DSP clock, scheduled playback and scoring timing.
            var room = host?.PlayRoom;
            if (room == null || room.Audio == null) return;
            if (audioSources == null) audioSources = room.GetComponents<AudioSource>();
            foreach (var source in audioSources)
                if (source != null) source.mute = source == room.Audio.Source ? !bgm : !se;
        }

        public void Close()
        {
            CloseLeaderboardRules();
            CloseClaimReward();
            generation++;
            if (root != null)
            {
                root.SetActive(false);
                if (Application.isPlaying) Destroy(root); else DestroyImmediate(root);
            }
            root = null; content = null; scroll = null; battlePassExpiration=0; battlePassRewardsBound=false; cachedPeriodicData=null;
        }
        private bool Current(int request) => IsOpen && request == generation;
        private void Open(string title)
        {
            Close(); host.DismissNotice();
            root = new GameObject("Windows Home dialog", typeof(RectTransform), typeof(Canvas), typeof(RecoveredCanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(host.transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = host.PortraitCamera; canvas.planeDistance = 100; canvas.sortingOrder = 950;
            var scaler = root.GetComponent<RecoveredCanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(828, 1792); scaler.matchWidthOrHeight = 1;
            var shade = Rect(root.transform, "Modal input blocker", Vector2.zero);
            shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.sizeDelta = Vector2.zero;
            shade.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .8f);
            var panel = Rect(root.transform, "Dialog", new Vector2(760, 1300));
            panel.gameObject.AddComponent<Image>().color = new Color(.12f, .07f, .22f, 1);
            var heading = Text(panel, "Title", title, 34); heading.alignment = TextAnchor.MiddleLeft;
            var hr = heading.rectTransform; hr.anchorMin = hr.anchorMax = new Vector2(.5f, 1); hr.pivot = new Vector2(.5f, 1);
            hr.sizeDelta = new Vector2(690, 90); hr.anchoredPosition = new Vector2(0, -16);
            var viewport = Rect(panel, "Viewport", Vector2.zero);
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(24, 120); viewport.offsetMax = new Vector2(-24, -116);
            viewport.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, .025f);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect(viewport, "Content", Vector2.zero);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 18;
            layout.padding = new RectOffset(12, 12, 16, 24); layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 65;
            var close = Rect(panel, "CloseButton", new Vector2(690, 78));
            close.anchorMin = close.anchorMax = new Vector2(.5f, 0); close.pivot = new Vector2(.5f, 0); close.anchoredPosition = new Vector2(0, 22);
            MakeButton(close, "Cerrar", Close);
        }
        private static RectTransform Rect(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f); rt.sizeDelta = size; return rt;
        }
        private Text Text(Transform parent, string name, string value, int size = 28)
        {
            var rt = Rect(parent, name, Vector2.zero); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
            var label = rt.gameObject.AddComponent<Text>(); label.font = font; label.fontSize = size;
            label.color = Color.white; label.supportRichText = false; label.raycastTarget = false;
            label.alignment = TextAnchor.UpperLeft; label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate; label.text = value ?? ""; return label;
        }
        private Text Paragraph(string value)
        {
            var label = Text(content, "Detail", value);
            // LayoutGroup supplies width before Text calculates its preferred wrapped height.
            label.gameObject.AddComponent<LayoutElement>().minHeight = 48; return label;
        }
        private Button MakeButton(RectTransform rect, string value, Action action)
        {
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.34f, .16f, .58f, 1);
            var label = Text(rect, "Label", value); label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.offsetMin = new Vector2(16, 8); label.rectTransform.offsetMax = new Vector2(-16, -8);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(() => action()); return button;
        }
        private Button ActionRow(string value, Action action, bool enabled = true)
        {
            var rt = Rect(content, "Action", Vector2.zero); rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;
            var button = MakeButton(rt, value, action); button.interactable = enabled; return button;
        }
        private void ClearContent()
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i).gameObject; child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            scroll.StopMovement(); content.anchoredPosition = Vector2.zero;
        }
        private void Setting(string key, string label, string support, Action<bool> apply)
        {
            bool enabled = Preference(key);
            Button button = null;
            button = ActionRow(label + ": " + (enabled ? "Sí" : "No"), () =>
            {
                enabled = !enabled; RecoveredPreferences.SetInt(PreferencePrefix + key, enabled ? 1 : 0); RecoveredPreferences.Save();
                apply?.Invoke(enabled); button.GetComponentInChildren<Text>().text = label + ": " + (enabled ? "Sí" : "No");
            });
            Paragraph(support);
        }
        private static Transform RequirePath(Transform parent, string path)
        {
            var target = parent.Find(path);
            if (target == null) throw new InvalidOperationException("Missing original Settings path: " + path);
            return target;
        }
        private void BindAudioSetting(Transform dialog, string key, Action<bool> apply)
        {
            var row = RequirePath(dialog, "BgImage/MiniBg/List/" + key);
            var target = RequirePath(row, "Parent/CheckBG");
            var button = target.GetComponent<Button>();
            if (button == null) throw new InvalidOperationException("Missing original Settings button: " + key);
            var checkmark = RequirePath(target, "Checkmark").GetComponent<Image>();
            if (checkmark == null) throw new InvalidOperationException("Missing original Settings checkmark: " + key);
            // 6.0.6 OnOff.Start/Click updates Image.enabled synchronously (RVA 0x29023AC/0x29023D0).
            checkmark.enabled = Preference(key);
            button.interactable = true;
            button.onClick.AddListener(() =>
            {
                bool value = !Preference(key);
                RecoveredPreferences.SetInt(PreferencePrefix + key, value ? 1 : 0);
                RecoveredPreferences.Save(); checkmark.enabled = value; apply(value);
            });
        }
        private void OpenOriginal(string name, string closePath, string resourcePath = "RecoveredHome606/dialogs")
        {
            Close(); host.DismissNotice();
            try
            {
                var resource = Resources.Load<TextAsset>(resourcePath);
                if (resource == null) throw new InvalidOperationException("Original Home dialogs are missing");
                var data = JsonUtility.FromJson<RecoveredUiData>(resource.text);
                UiScene scene = null;
                foreach (var candidate in data.scenes)
                    if (candidate.name == name) scene = candidate;
                if (scene == null) throw new InvalidOperationException("Original dialog is missing: " + name);
                var tree = RecoveredUiBuilder.Build(data, scene, host.transform, host.PortraitCamera);
                root = tree.Root;
                RecoveredUiBuilder.UseAuthoredLayoutRoot(tree, name, host.transform);
                root = tree.Root;
                // Serialized prefab is hidden at scale zero; opening restores its visible endpoint.
                // Original opening/closing tween timings remain pending native evidence.
                root.transform.localScale = Vector3.one;
                var canvas = root.GetComponentInChildren<Canvas>(true);
                if(canvas==null)throw new InvalidOperationException("Missing original dialog canvas: "+name);
                canvas.overrideSorting = true; canvas.sortingOrder = 950;
                var close = RequirePath(root.transform, closePath).GetComponent<Button>();
                if (close == null) throw new InvalidOperationException("Missing original close button: " + closePath);
                close.interactable = true; close.onClick.AddListener(Close);
                root.SetActive(true); Canvas.ForceUpdateCanvases();
            }
            catch
            {
                Close(); throw;
            }
        }
        public void ShowPeriodicTasks()
        {
            OpenOriginal("PeriodicTaskDialog", "BgPanel/CloseButton");
            foreach(var name in new[]{"SignIn","Daily","Weekly"})
            {
                var tab=RequirePath(root.transform,"BgPanel/Buttons/"+name);
                var button=tab.GetComponent<Button>();
                if(button==null)throw new InvalidOperationException("Missing original periodic-task tab: "+name);
                button.interactable=true;
                button.onClick.AddListener(()=>SelectPeriodicTab(name));
            }
            foreach(var path in new[]{"BgPanel/SignIn/Newbie","BgPanel/SignIn/Periodic","BgPanel/DailyTask","BgPanel/WeeklyTask"})
                RequirePath(root.transform,path).gameObject.SetActive(false);
            foreach(var text in root.GetComponentsInChildren<Text>(true))
                if(text.name=="Title"||text.name=="ResetIn"||text.name=="TitleText"||text.name=="ProgressText"||text.name=="PtsText")text.text="";
            ClearPeriodicSignIn();
            SelectPeriodicTab("SignIn");
            StartCoroutine(PeriodicTasks(generation));
        }
        private void SelectPeriodicTab(string name)
        {
            if(!IsOpen||root.name!="PeriodicTaskDialog")return;
            var prefix="BgPanel/";
            foreach(var tabName in new[]{"SignIn","Daily","Weekly"})
            {
                var tab=RequirePath(root.transform,prefix+"Buttons/"+tabName);
                bool selected=tabName==name;
                long spriteId=tabName=="Daily"?(selected?-438442784592724099L:-5732391765384448830L):(selected?3504406373124137108L:8306909274534071295L);
                tab.GetComponent<Image>().sprite=Resources.Load<Sprite>("RecoveredHome606/Sprites/CAB-fc4ad835af77a0fdba838a4924a9c9bd_"+spriteId);
                tab.GetComponentInChildren<Text>(true).color=selected?Color.white:new Color32(184,184,184,255);
            }
            foreach(var path in new[]{"SignIn/Newbie","SignIn/Periodic","DailyTask","WeeklyTask"})
            {
                var node=root.transform.Find(prefix+path);
                if(node!=null)node.gameObject.SetActive(false);
            }
            string target=name=="SignIn"?(cachedPeriodicData?.signin?.signin_task_type==0?"SignIn/Newbie":"SignIn/Periodic"):name=="Daily"?"DailyTask":"WeeklyTask";
            RequirePath(root.transform,prefix+target).gameObject.SetActive(true);
            // Re-render current tab data from cache to preserve state
            if(cachedPeriodicData!=null)
            {
                if(name=="Daily")RenderDailyTasks(cachedPeriodicData.daily);
                else if(name=="Weekly")RenderWeeklyTasks(cachedPeriodicData.weekly);
            }
            CurrentOriginalTabChanged?.Invoke(name);
        }
        public void ShowSettings()
        {
            OpenOriginal("SettingsDialog", "BgImage/MiniBg/CloseButton");
            BindAudioSetting(root.transform, "SE", value => { se = value; ApplyAudio(); });
            BindAudioSetting(root.transform, "BGM", value => { bgm = value; ApplyAudio(); });
            // Unsupported Windows services retain original visuals and disabled controls.
            foreach (var key in new[] { "Vibrate", "Notification" })
                RequirePath(root.transform, "BgImage/MiniBg/List/" + key + "/Parent/CheckBG/Checkmark").GetComponent<Image>().enabled = false;
        }
        public void ShowConnectionSettings()
        {
            Open("Conectividad de Windows");
            bool online = RecoveredPreferences.GetInt("BBPC.Windows.OnlineEnabled", 0) == 1;
            Button connection = null;
            connection = ActionRow("Conectividad: " + (online ? "activada" : "desactivada"), () =>
            {
                if (RecoveredStorage.Isolated) return;
                online = !online;
                RecoveredPreferences.SetInt("BBPC.Windows.OnlineEnabled", online ? 1 : 0);
                RecoveredPreferences.Save();
                connection.GetComponentInChildren<Text>().text = "Conectividad: " + (online ? "activada" : "desactivada al reiniciar");
                if (online) host.StartOwnOnlineTest();
            }, !RecoveredStorage.Isolated);
            Paragraph("Activar conecta con los servicios del juego y carga el catálogo. Las canciones se descargan y convierten al seleccionarlas. Desactivar requiere reiniciar para terminar la sesión actual. Si la conexión falla, reinicia antes de reintentar.");
            Setting("SE", "Efectos de sonido", "Aplica a la fuente de efectos de PlayRoom.", value => { se = value; ApplyAudio(); });
            Setting("BGM", "Música", "Silencia la canción sin detener el reloj de audio ni cambiar la sincronización.", value => { bgm = value; ApplyAudio(); });
            Setting("Vibrate", "Vibración", "Preferencia local guardada. No hay backend de vibración para Windows.", null);
            Setting("Notification", "Notificaciones", "Preferencia local guardada. No hay servicio de notificaciones para Windows.", null);
            Paragraph("Ajustes guardados en este equipo. Login y feedback originales pendientes de reconstrucción.");
        }
        public void ShowExtraHp()
        {
            OpenOriginal("PermanentExtraHPDialog", "Background/CloseButton");
            // Authored prices are prefab defaults, not verified Windows store offers.
            // Purchase/restore controls remain disabled until a Windows IAP backend exists.
            foreach (int tier in new[] { 2, 5, 10 })
                RequirePath(root.transform, "Background/Buy" + tier + "Button/PriceText").GetComponent<Text>().text = "—";
        }
        public void ShowLeaderboard()
        {
            OpenOriginal("RankPanel","CloseButton","RecoveredLeaderboard606/dialogs");
            BindLeaderboardControls();
            StartCoroutine(Leaderboard(generation));
        }
        private IEnumerator Leaderboard(int request)
        {
            RecoveredLeaderboardPayload challenge=null,area=null,world=null;
            using(RecoveredClaimLoading.Begin(host.transform,host.PortraitCamera))
            {
                yield return host.Services.FetchLeaderboard(RecoveredReadContracts.LeaderboardChallenge,value=>challenge=value);
                if(!Current(request))yield break;
                yield return host.Services.FetchLeaderboard(RecoveredReadContracts.LeaderboardRegion,value=>area=value);
                if(!Current(request))yield break;
                yield return host.Services.FetchLeaderboard(RecoveredReadContracts.LeaderboardWorld,value=>world=value);
            }
            if(!Current(request))yield break;
            leaderboardChallenge=challenge;
            BindLeaderboardData(area,world);
            BindLeaderboardPrize(challenge?.prize);
            if(challenge!=null)RenderLeaderboardChallenge();
        }
        public void ShowDailyTasks() { ShowPeriodicTasks(); }
        private RecoveredTaskPeriodList cachedPeriodicData;
        private IEnumerator PeriodicTasks(int request)
        {
            RecoveredTaskPeriodList data=null;
            using(RecoveredClaimLoading.Begin(host.transform,host.PortraitCamera))
                yield return host.Services.FetchPeriodicTasks(value=>data=value);
            if(!Current(request))yield break;
            if(data==null){CurrentOriginalTabChanged?.Invoke("Unavailable");yield break;}
            RenderPeriodicTasks(data);
        }
        internal void RenderPeriodicTasks(RecoveredTaskPeriodList data)
        {
            if(data==null||!IsOpen||root.name!="PeriodicTaskDialog")return;
            cachedPeriodicData=data;
            foreach(var tab in new[]{"SignIn","Daily","Weekly"})
            {
                var tasks=tab=="SignIn"?data.signin?.tasks:tab=="Daily"?data.daily?.point:data.weekly?.point;
                var rewards=tab=="Daily"?data.daily?.reward:tab=="Weekly"?data.weekly?.reward:null;
                bool ready=Array.Exists(tasks??Array.Empty<RecoveredTaskPeriodItem>(),t=>t!=null&&t.status==1)
                    ||Array.Exists(rewards??Array.Empty<RecoveredTaskPeriodItem>(),t=>t!=null&&t.status==1);
                RequirePath(root.transform,"BgPanel/Buttons/"+tab+"/NewImage").gameObject.SetActive(ready);
            }
            RenderPeriodicSignIn(data);
            RenderDailyTasks(data.daily);
            RenderWeeklyTasks(data.weekly);
        }
        private void ClearPeriodicSignIn()
        {
            var viewport=RequirePath(root.transform,"BgPanel/SignIn/Periodic/Viewport");
            foreach(Transform cell in RequirePath(viewport,"Content"))cell.gameObject.SetActive(false);
            RequirePath(viewport,"DailyPanel7").gameObject.SetActive(false);
        }
        private void ReadOnlyPeriodBlock(string title, RecoveredTaskPeriodBlock block)
        {
            Paragraph(title);
            if (block == null) { Paragraph("El servicio no publicó esta sección."); return; }
            if (block.expiration > 0)
            {
                try { Paragraph("Vencimiento: " + DateTimeOffset.FromUnixTimeMilliseconds(block.expiration).ToLocalTime().ToString("yyyy-MM-dd HH:mm")); }
                catch (ArgumentOutOfRangeException) { Paragraph("Vencimiento publicado fuera de rango."); }
            }
            ReadOnlyTaskItems("Recompensas por puntos", block.reward);
            ReadOnlyTaskItems("Tareas que otorgan puntos", block.point);
            Paragraph("Lectura solamente: no se ejecutan claims daily/weekly hasta confirmar el contrato de recompensa.");
        }
        private void ReadOnlyTaskItems(string title, RecoveredTaskPeriodItem[] items)
        {
            Paragraph(title);
            if (items == null || items.Length == 0) { Paragraph("Sin elementos publicados."); return; }
            foreach (var item in items)
                if (item != null)
                {
                    Paragraph((item.desc ?? "Tarea") + "\nProgreso: " + item.finish + "/" + item.target + "\nEstado: " + TaskStatus(item.status));
                    Paragraph(RewardText(item.rewards));
                }
        }
        public void ShowLegacySignIn()
        {
            Open("Check-in independiente"); Paragraph("Consultando /task/signin…");
            StartCoroutine(LegacySignIn(generation));
        }
        private IEnumerator LegacySignIn(int request)
        {
            RecoveredSignInTask[] tasks = null;
            yield return host.Services.FetchLegacySignInTasks(value => tasks = value);
            if (!Current(request)) yield break;
            ClearContent();
            if (tasks == null) Paragraph("Check-in independiente no disponible. Requiere lectura verificada del servicio.");
            else if (tasks.Length == 0) Paragraph("El servicio no publicó check-ins independientes.");
            else
            {
                int row = 0;
                foreach (var task in tasks)
                    if (task != null)
                    {
                        Paragraph("Check-in " + (++row) + "\n" + (task.desc ?? "") + "\nTipo: " + task.type + " · progreso: " + task.finish + "/" + task.target + "\nEstado: " + TaskStatus(task.status));
                        Paragraph(RewardText(task.rewards));
                    }
            }
            Paragraph("Solo lectura. No se reclama: no está confirmado el contrato de recompensa de /task/signin.");
            ActionRow("Volver a misiones periódicas", ShowDailyTasks);
        }
        private static string RewardText(RecoveredTaskReward[] rewards)
        {
            if (rewards == null || rewards.Length == 0) return "Recompensas no publicadas.";
            var text = new System.Text.StringBuilder("Recompensas:");
            foreach (var reward in rewards)
                if (reward != null) text.Append("\n").Append(reward.type == 1 ? "Diamantes" : "Tipo " + reward.type).Append(": ").Append(reward.quantity);
            return text.ToString();
        }
        private static bool RequiresAd(RecoveredTaskPeriodItem task) => task != null && task.need_ad != 0;
        private IEnumerator ClaimCheckIn(RecoveredTaskPeriodItem task, Button button, int request)
        {
            // Server tid and CompleteNotReceived only; never infer eligibility from finish/target.
            if (task.status != 1 || string.IsNullOrEmpty(task.tid) || RequiresAd(task)) { pendingClaims.Remove(task.tid ?? ""); yield break; }
            RecoveredTaskRewardResultData result = null;
            yield return host.Services.ClaimTaskReward(task.tid, "1", value => result = value);
            pendingClaims.Remove(task.tid);
            if (result != null) confirmedClaims.Add(task.tid);
            if (Current(request))
            {
                if (button != null)
                {
                    button.GetComponentInChildren<Text>().text = result == null ? "—" : "Reclamado";
                    var got=button.transform.parent.Find("Got");
                    if(got!=null&&result!=null)got.gameObject.SetActive(true);
                    button.interactable=false;
                }
            }
            // Closing the dialog does not cancel a reward already sent. Reconcile the real HUD anyway.
            if (result != null) yield return host.Services.RefreshWealth(WealthRefreshTrigger.Reward, wealth => { });
        }
        // Isolated layout inspection only: authored counters/rewards are prefab defaults,
        // never account state. Keep the public BattlePass route until real data binding exists.
        public void ShowBattlePassPreview()
        {
            if (!RecoveredStorage.Isolated)
                throw new InvalidOperationException("BattlePass prefab preview requires isolated validation storage");
            OpenOriginal("BattlePassDialog", "BottomPanel/CloseBtn");
        }
        // Reusable DTO binding on the original header, currently restricted to the isolated preview.
        public void BindBattlePassPreview(RecoveredBattlePassData pass)
        {
            if (!RecoveredStorage.Isolated)
                throw new InvalidOperationException("BattlePass prefab binding requires isolated validation storage");
            if (pass == null) throw new ArgumentNullException(nameof(pass));
            if (!IsOpen || root.name != "BattlePassDialog")
                throw new InvalidOperationException("Open the original BattlePass preview before binding");
            // 6.0.6 Awake (+0x88 button) hides Activate when the pass is bought.
            // Preview models only DTO ownership; the native alternate entitlement is pending.
            RequirePath(root.transform, "TopPanel/TopPanelRight/VipPassBtn").gameObject.SetActive(!pass.has_purchased);
            // Reward cells are external to this 196-node scene. Runtime diamond binding uses
            // RecoveredBattlePassCells606; bonus/cosmetic binding remains pending.
            // TODO: RefreshUnlockingLevel must map DTO point to CurrentLevelPoint/TargetPoint and
            // display level before binding PointSlider/PointProgressText/Point. point_by_diamond is not a maximum.
            // TODO: recover GetRemainingTimeText (0x266A130) before deriving remaining-day text.
        }
        public void ShowBattlePass()
        {
            OpenOriginal("BattlePassDialog", "BottomPanel/CloseBtn");
            // Dynamic rewards and prices require their recovered bindings; prefab examples are not account data.
            foreach(var path in new[]{"TopPanel/FinalSBF","Rewards/NextByDiamond","Rewards/BattlePassKeyRewrad","TopPanel/TopPanelRight/VipPassBtn"})
                RequirePath(root.transform,path).gameObject.SetActive(false);
            RequirePath(root.transform,"PointProgress/Point").GetComponent<Text>().text="—";
            RequirePath(root.transform,"PointProgress/PointProgressText").GetComponent<Text>().text="—";
            RequirePath(root.transform,"PointProgress/PointSlider").GetComponent<Slider>().value=0;
            RequirePath(root.transform,"TopPanel/TopPanelRight/RemainDay/RemainDayText").GetComponent<Text>().text="";
            StartCoroutine(OriginalBattlePass(generation));
        }
        private IEnumerator OriginalBattlePass(int request)
        {
            RecoveredBattlePassData pass=null;
            yield return host.Services.FetchBattlePass(value=>pass=value);
            if(!Current(request)||pass==null)yield break;
            BindBattlePassProgress(pass);
            BindBattlePassRewards(pass);
            host.BattlePassBar?.Apply(pass);
        }
        internal void BindBattlePassRewards(RecoveredBattlePassData pass)
        {
            if(!IsOpen||root.name!="BattlePassDialog")throw new InvalidOperationException("Open BattlePass before binding rewards");
            int request=generation;
            RecoveredBattlePassCells.Apply(root.transform,pass,host.PortraitCamera,(task,type,button)=>
            {
                if(!Current(request)||!pendingClaims.Add(task.tid))return;
                button.interactable=false;
                StartCoroutine(ClaimBattlePass(pass,task,type,request));
            },battlePassRewardsBound);
            battlePassRewardsBound=true;
        }
        private IEnumerator ClaimBattlePass(RecoveredBattlePassData pass,RecoveredTaskPeriodItem task,int type,int request)
        {
            RecoveredTaskRewardResultData result=null;
            using(RecoveredClaimLoading.Begin(host.transform,host.PortraitCamera))
                yield return host.Services.ClaimBattlePassReward(pass.bid,task.tid,type,value=>result=value);
            if(result!=null)task.status=2;
            if(Current(request))BindBattlePassRewards(pass);
            if(result!=null&&Current(request))ShowClaimRewards(result.rewards,request);
            // Reconcile even when an accepted response could not be parsed; never assume a retry is safe.
            {
                RecoveredBattlePassData refreshed=null;
                yield return host.Services.FetchBattlePass(value=>refreshed=value);
                pendingClaims.Remove(task.tid);
                if(Current(request))
                {
                    if(refreshed!=null){BindBattlePassProgress(refreshed);BindBattlePassRewards(refreshed);host.BattlePassBar?.Apply(refreshed);}
                    else BindBattlePassRewards(pass);
                }
                yield return host.Services.RefreshWealth(WealthRefreshTrigger.Reward,value=>{});
            }
        }
        internal void BindBattlePassProgress(RecoveredBattlePassData pass)
        {
            if(!IsOpen||root.name!="BattlePassDialog")throw new InvalidOperationException("Open the original BattlePass dialog before binding progress");
            BindBattlePassCountdown(pass.expiration,host.Services.CurrentServerTimestamp);
            var progress=RecoveredBattlePassProgress.From(pass);
            var slider=RequirePath(root.transform,"PointProgress/PointSlider").GetComponent<Slider>();
            slider.maxValue=progress.Target;slider.value=progress.Point;
            RequirePath(root.transform,"PointProgress/PointProgressText").GetComponent<Text>().text=progress.Point+" / "+progress.Target;
            RequirePath(root.transform,"PointProgress/Point").GetComponent<Text>().text=progress.DisplayLevel.ToString();
        }
        // OBSERVED: TaskRewardStatus, dump 6.0.6 lines 16234–16241.
        private static string TaskStatus(int status)
        {
            switch (status)
            {
                case 0: return "Incompleto (0)";
                case 1: return "Completo sin reclamar (1)";
                case 2: return "Reclamado (2)";
                default: return "Desconocido (" + status + ")";
            }
        }
        public void ShowFreeStage()
        {
            OpenOriginal("ProgressiveFreeSongDialog","Canvas/RecommendPanel/ShowPanel/Close","RecoveredNavigationDialogs606/dialogs");
            var panel=RequirePath(root.transform,"Canvas/RecommendPanel/ShowPanel");
            var later=RequirePath(panel,"Later").GetComponent<Button>();later.interactable=true;later.onClick.AddListener(Close);
            // Windows fill: translucent title strip, matched to the supplied 2026 reference.
            var strip=RequirePath(panel,"BG").GetComponent<Image>();
            strip.sprite=null;strip.color=new Color(0,0,0,.6f);
            // Prefab ticks and Get&Play are example progression, not verified account access.
            RequirePath(panel,"Container").gameObject.SetActive(false);
            foreach(var label in panel.GetComponentsInChildren<Text>(true))
                if(label.name=="SongText"||label.name=="ArtistText"||label.name=="DifficultyText"||label.name=="CountdownText"||label.name.StartsWith("UnlockStatusText"))label.text="";
            StartCoroutine(FreeStage(generation));
        }
        private IEnumerator FreeStage(int request)
        {
            SongMetadata song = null;
            yield return host.Services.FetchFreeStage(value => song = value);
            if (!Current(request)) yield break;
            if(song==null)yield break;
            BindFreeStage(song);
        }
        internal void BindFreeStage(SongMetadata song)
        {
            if(song==null)throw new ArgumentNullException(nameof(song));
            if(!IsOpen||root.name!="ProgressiveFreeSongDialog")throw new InvalidOperationException("Open Free Song before binding");
            var panel=RequirePath(root.transform,"Canvas/RecommendPanel/ShowPanel");
            RequirePath(panel,"SongText").GetComponent<Text>().text=song.title??"";
            RequirePath(panel,"ArtistText").GetComponent<Text>().text=song.artist??"";
            var current=RequirePath(panel,"BorderImage/Current");
            var covers=host.GetComponent<RecoveredCoverCache>()??host.gameObject.AddComponent<RecoveredCoverCache>();
            var binding=current.GetComponent<RecoveredCoverBinding>()??current.gameObject.AddComponent<RecoveredCoverBinding>();
            binding.Bind(covers,song.cover,
                RequirePath(current,"CoverIcon").GetComponent<RawImage>(),RequirePath(current,"CoverBackground").gameObject,true,true);
            RecoveredDifficultyStyles.ApplyLabel(RequirePath(panel,"DifficultyText").GetComponent<Text>(),song.CurrentDifficulty);
            var difficulty=(RectTransform)RequirePath(panel,"DifficultyText");
            var favorite=(RectTransform)RequirePath(panel,"FavoritesButton");
            difficulty.anchoredPosition=new Vector2(difficulty.anchoredPosition.x,-59.79f);
            favorite.anchoredPosition=new Vector2(favorite.anchoredPosition.x,-2f);
            var favoriteButton=favorite.GetComponent<Button>();favoriteButton.interactable=true;favoriteButton.onClick.RemoveAllListeners();
            Action refreshFavorite=()=>
            {
                var image=RequirePath(favorite,"FavoriteImageImage").GetComponent<Image>();
                bool selected=host.FullCatalog?.IsFavorite(song)??false;
                image.sprite=RecoveredUiBuilder.Asset<Sprite>(host.Data,selected?"resources.assets:1618":"resources.assets:1511");
            };
            refreshFavorite();
            favoriteButton.onClick.AddListener(()=>{host.FullCatalog?.ToggleFavorite(song);refreshFavorite();});
            // Only the supplied stage is recommended; authored progress is sample data.
            var container=RequirePath(panel,"Container");container.gameObject.SetActive(true);
            RequirePath(container,"Progress").gameObject.SetActive(false);
            var buttons=RequirePath(container,"Button");
            foreach(Transform child in buttons)child.gameObject.SetActive(child.name=="GetAndPlayButton");
            var play=RequirePath(buttons,"GetAndPlayButton").GetComponent<Button>();
            var textParent=(RectTransform)RequirePath(play.transform,"TextParent");
            // The authored HorizontalLayoutGroup otherwise drives the label back to fixed anchors.
            var labelLayout=textParent.GetComponent<HorizontalLayoutGroup>();if(labelLayout!=null)labelLayout.enabled=false;
            textParent.anchorMin=Vector2.zero;textParent.anchorMax=Vector2.one;
            textParent.anchoredPosition=Vector2.zero;textParent.sizeDelta=Vector2.zero;
            var label=RequirePath(play.transform,"TextParent/Text").GetComponent<Text>();
            label.text=RecoveredLocalization.Text("song_get","Obtener&Jugar").Replace("&"," & ").ToUpperInvariant();
            label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
            label.rectTransform.anchoredPosition=Vector2.zero;label.rectTransform.sizeDelta=Vector2.zero;
            label.alignment=TextAnchor.MiddleCenter;
            play.interactable=true;play.onClick.RemoveAllListeners();play.onClick.AddListener(()=>SelectFreeStage(song));
        }
        private void SelectFreeStage(SongMetadata song)
        {
            host.ShowCatalog();
            SongItem match = null;
            if ((host.catalogOrigin == CatalogOrigin.Verified606Snapshot || host.catalogOrigin == CatalogOrigin.PublicOffline606) && host.FullCatalog?.Index != null)
                foreach (var entry in host.FullCatalog.Index.Entries)
                    if (entry.stage != null && entry.stage.Key == song.Key) { match = entry; break; }
            if (match == null)
            {
                // ShowCatalog can change navigation and close this modal. Reopen with the concrete blocker.
                Open("Canción gratuita"); Paragraph("La variante recomendada no está en el catálogo disponible. No se modifica el acceso de la canción.");
                Paragraph((song.title ?? song.sid) + " · " + song.aid); return;
            }
            Close(); host.FullCatalog.SelectStage(match.stage, true);
        }
    }
}
