using System;
using System.IO;
using BBPC.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    public enum FrontendPhase { Splash, MainRoom, SongPreparation, StageCountdown, PlayRoom, Defeat, ResultRoom }
    // P2 integration entry. Diagnostic controls are temporary and clearly separated from original UI.
    [RequireComponent(typeof(PortBootstrap))]
    public sealed class RecoveredFrontend : MonoBehaviour
    {
        public const string ScenePath = "Assets/BBPC/Scenes/RecoveredFrontend.unity";
        public TextAsset catalogSnapshot; public CatalogOrigin catalogOrigin;
        public RecoveredUiData Data { get; private set; }
        public RecoveredUiTree Splash { get; private set; }
        public RecoveredUiTree Loading { get; private set; }
        public RecoveredUiTree MainRoom { get; private set; }
        public RecoveredServiceClient Services { get; private set; }
        public RecoveredLocalAccountState Account { get; private set; }
        public SongSelection Selection { get; private set; } = new SongSelection();
        public int CatalogCount { get; private set; }
        public RecoveredCatalogView FullCatalog {get;private set;}
        public RecoveredStoryView Stories {get;private set;}
        public RecoveredShopView Skins {get;private set;}
        public RecoveredShopView Store {get;private set;}
        public RecoveredResultPanel Results {get;private set;}
        public RecoveredHomeDialogs HomeDialogs {get;private set;}
        public RecoveredShopScreen ShopScreen {get;private set;}
        public RecoveredSkinsScreen SkinsScreen {get;private set;}
        public RecoveredBattlePassBar BattlePassBar {get;private set;}
        public const string PopupAdsPurchaseKey = "iap_remove_popup_ad_299_android_sku";
        public static bool HasPopupAdsEntitlement(RecoveredWealthData wealth)
        { return wealth?.purchases != null && wealth.purchases.TryGetValue(PopupAdsPurchaseKey, out var count) && count > 0; }
        public bool Initialized { get; private set; }

        public Camera PortraitCamera { get; private set; }
        public RecoveredNavigation Navigation { get; private set; }
        public RecoveredPlayRoom PlayRoom { get; private set; }
        public string TutorialPreparationError { get; private set; }
        private bool preparingTutorial;
        public FrontendPhase Phase { get; private set; } = FrontendPhase.Splash;
        public bool IsInspection { get; private set; }
        public int InspectionToggleCount { get; private set; }
        public bool inspectionMode;
        private GameObject diagnostics;
        public bool ServerReportsRequested {get;private set;}
        public bool LocalReportsEnabled {get;private set;}=true;
        public static Rect PortraitViewport(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException("Display dimensions");
            const float narrowestPortrait = 828f / 1792f, widestPortrait = 9f / 16f;
            float available = (float)width / height;
            return available > widestPortrait
                ? new Rect((1 - widestPortrait / available) * 0.5f, 0, widestPortrait / available, 1)
                : available < narrowestPortrait ? new Rect(0, (1 - available / narrowestPortrait) * 0.5f, 1, available / narrowestPortrait) : new Rect(0, 0, 1, 1);
        }
        public void ConfigureViewport(int width, int height)
        {
            PortraitCamera.rect = PortraitViewport(width, height);
            foreach (var scaler in GetComponentsInChildren<RecoveredCanvasScaler>(true)) scaler.RefreshViewport();
        }
        private void LateUpdate()
        {
            if (PortraitCamera != null && PortraitCamera.targetTexture == null) ConfigureViewport(Screen.width, Screen.height);
        }
        private void Update()
        {
            if(notice!=null && Time.unscaledTime>=noticeUntil)notice.gameObject.SetActive(false);
            if (Input.GetKeyDown(KeyCode.F1) || ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.I)))
            ToggleInspection();
        }

        public void ToggleInspection()
        { inspectionMode = !inspectionMode; InspectionToggleCount++; if (diagnostics != null) diagnostics.SetActive(inspectionMode); Debug.Log("BBPC inspection toggle; focused=" + Application.isFocused); }
        public const string Revision = "Freeplay-v4.20";
        private void Awake()
        {
            // A hidden unattended Player must keep ticking from the first Splash frame.
            // Setting this in P3 validation is too late: bootstrap can stall before reaching it.
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-bbpc-access-check")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-bbpc-cover-check")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-bbpc-validate")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-bbpc-online-check")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-bbpc-account-probe")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-bbpc-dunk-check")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-bbpc-account-wealth-probe")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-bbpc-stage-check")>=0)
            {Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;Debug.Log("BBPC unattended validation: "+Revision);}
        }
        public void CaptureCurrentView(string file, int width, int height)
        {
            var viewport = PortraitViewport(width, height);
            int viewWidth = Mathf.RoundToInt(width * viewport.width), viewHeight = Mathf.RoundToInt(height * viewport.height);
            // Camera.targetTexture ignores rect/pixelRect. Render the portrait viewport at its pixel size,
            // then copy it into the black display frame, matching the on-screen camera rectangle.
            var rt = RenderTexture.GetTemporary(viewWidth, viewHeight, 24);
            var previous = RenderTexture.active; var previousTarget = PortraitCamera.targetTexture;
            Texture2D image = null;
            try
            {
                PortraitCamera.targetTexture = rt; PortraitCamera.rect = new Rect(0, 0, 1, 1);
                foreach (var scaler in GetComponentsInChildren<RecoveredCanvasScaler>(true)) scaler.RefreshViewport();
                Canvas.ForceUpdateCanvases();
                foreach (var fitter in GetComponentsInChildren<AspectRatioFitter>()) LayoutRebuilder.ForceRebuildLayoutImmediate(fitter.GetComponent<RectTransform>());
                Canvas.ForceUpdateCanvases();
                RenderTexture.active = rt; GL.Clear(true, true, Color.black); PortraitCamera.Render(); RenderTexture.active = rt;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.SetPixels32(new Color32[width * height]);
                image.ReadPixels(new Rect(0, 0, viewWidth, viewHeight), Mathf.RoundToInt(width * viewport.x), Mathf.RoundToInt(height * viewport.y)); image.Apply();
                File.WriteAllBytes(file, ImageConversion.EncodeToPNG(image));
                var colors = new System.Collections.Generic.HashSet<int>();
                foreach (var pixel in image.GetPixels32()) { colors.Add(pixel.r << 16 | pixel.g << 8 | pixel.b); if (colors.Count >= 16) break; }
                if (colors.Count < 16) throw new InvalidOperationException("Blank/incomplete UI capture: " + file);
            }
            finally
            {
                if (image != null) { if (Application.isPlaying) Destroy(image); else DestroyImmediate(image); }
                PortraitCamera.targetTexture = previousTarget; RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt); ConfigureViewport(Screen.width, Screen.height);
            }
        }
        [Serializable] private sealed class PlayerCheck { public bool success, coreInitialized, preparationBlocked, bootstrapMainRoom, initialFitsDesktop, resizedPortrait, resizedLandscape, keyboardFocused; public int captures, navigationPointerTransitions, initialWidth, initialHeight; public string revision, unityVersion, error,validationId,runtimeAssemblySha256; }
        private System.Collections.IEnumerator ValidateNavigationPointer(PlayerCheck check)
        {
            foreach (var page in new[] { MainPage.Shop, MainPage.Versus, MainPage.Character, MainPage.Adventure, MainPage.Home })
            {
                yield return null; // Real UI frame between activations, as with physical user clicks.
                Canvas.ForceUpdateCanvases(); bool failed = false;
                try
                {
                    var button = Navigation.ButtonFor(page); var corners = new Vector3[4]; button.targetGraphic.rectTransform.GetWorldCorners(corners);
                    var min = RectTransformUtility.WorldToScreenPoint(PortraitCamera, corners[0]); var max = RectTransformUtility.WorldToScreenPoint(PortraitCamera, corners[2]); var view = PortraitCamera.pixelRect;
                    var visible = Rect.MinMaxRect(Math.Max(min.x,view.xMin),Math.Max(min.y,view.yMin),Math.Min(max.x,view.xMax),Math.Min(max.y,view.yMax));
                    if(visible.width<=0 || visible.height<=0) throw new InvalidOperationException("Tab outside visible viewport: "+page);
                    var pointer = new PointerEventData(EventSystem.current) { position = visible.center, button = PointerEventData.InputButton.Left };
                    var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
                    if(hits.Count==0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) != button.gameObject) throw new InvalidOperationException("Tab raycast obstructed: "+page+"; top="+(hits.Count==0?"none":hits[0].gameObject.name)+"; handler="+(hits.Count==0?"none":ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)?.name));
                    ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
                    if(Navigation.Current!=page || Loading.Root.activeSelf) throw new InvalidOperationException("Tab pointer routing failed");
                    check.navigationPointerTransitions++;

                }
                catch(Exception e) { check.error=e.ToString(); failed=true; }
                if(failed) yield break;
                yield return null;
            }
        }
        private System.Collections.IEnumerator ValidatePlayer(string output)
        {
            var check = new PlayerCheck { revision = Revision, unityVersion = Application.unityVersion };
            var command=Environment.GetCommandLineArgs();int runId=Array.IndexOf(command,"-bbpc-validation-id");check.validationId=runId>=0&&runId+1<command.Length?command[runId+1]:"";
            using(var sha=System.Security.Cryptography.SHA256.Create())check.runtimeAssemblySha256=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(typeof(RecoveredFrontend).Assembly.Location))).Replace("-","").ToLowerInvariant();
            Directory.CreateDirectory(output); inspectionMode = false; diagnostics.SetActive(false);
            yield return null; yield return null; // Exercise actual Start/Awake before rendering.
            var area = Screen.mainWindowDisplayInfo.workArea;
            check.initialWidth = Screen.width; check.initialHeight = Screen.height; check.keyboardFocused = Application.isFocused;
            check.initialFitsDesktop = Screen.width <= area.width - 32 && Screen.height <= area.height - 64;
            check.bootstrapMainRoom = Phase == FrontendPhase.MainRoom && !IsInspection && MainRoom.Root.activeInHierarchy && !Loading.Root.activeSelf;
            int portraitHeight = Math.Min(960, area.height - 64), portraitWidth = portraitHeight * 9 / 16;
            Screen.SetResolution(portraitWidth, portraitHeight, FullScreenMode.Windowed); yield return null; yield return null;
            check.resizedPortrait = Screen.width == portraitWidth && Screen.height == portraitHeight;
            yield return ValidateNavigationPointer(check);
            int landscapeWidth = Math.Min(960, area.width - 32), landscapeHeight = landscapeWidth * 9 / 16;
            Screen.SetResolution(landscapeWidth, landscapeHeight, FullScreenMode.Windowed); yield return null; yield return null;
            check.resizedLandscape = Screen.width == landscapeWidth && Screen.height == landscapeHeight;
            yield return ValidateNavigationPointer(check);
            Screen.SetResolution(check.initialWidth, check.initialHeight, FullScreenMode.Windowed); yield return null; yield return null;
            yield return ValidateNavigationPointer(check);
            try
            {
                if (!string.IsNullOrEmpty(check.error)) throw new InvalidOperationException("Navigation pointer test failed: " + check.error);
                if (!check.bootstrapMainRoom || !check.initialFitsDesktop || !check.resizedPortrait || !check.resizedLandscape) throw new InvalidOperationException("Bootstrap/window sizing checks failed");
                check.coreInitialized = GetComponent<PortBootstrap>().Clock != null && GetComponent<PortBootstrap>().NormalScores != null;
                RecoveredReadValidation.Run(Path.GetFullPath(Path.Combine(output,"../../network-v412/player")),Resources.Load<TextAsset>("LocalStages606/stage").text);
                if (!check.coreInitialized) throw new InvalidOperationException("P1 did not initialize in Player");
                catalogSnapshot=Resources.Load<TextAsset>("Catalog606/captured-catalog");catalogOrigin=CatalogOrigin.Captured606Partial;ShowCatalog();
                if (catalogOrigin != CatalogOrigin.Captured606Partial || CatalogCount != 3) throw new InvalidOperationException("Expected protected partial local fixture");
                var catalog = SongCatalog.Parse(catalogSnapshot.text);
                var list = MainRoom.Find("SonglistScrollView").GetComponent<ScrollRect>().content; Button choose = null;
                foreach (var button in list.GetComponentsInChildren<Button>(true))
                {
                    // Verified local rows intentionally enable Play; the original partial row must remain disabled.
                    if(button.name=="PlayButton"&&button.transform.parent.GetComponentInChildren<Text>()?.text?.StartsWith("SID:")==true&&button.interactable)throw new InvalidOperationException("Partial row play enabled");
                    if(button.name=="SongText" && button.GetComponent<Text>().text=="SID: "+catalog.items[0].stage.sid)choose=button;
                }
                if (choose == null) throw new InvalidOperationException("Catalog selection binding missing");
                choose.onClick.Invoke();
                if (Selection.Song == null || Selection.Song.sid != catalog.items[0].stage.sid) throw new InvalidOperationException("SID selection mismatch");
                check.preparationBlocked = Selection.PreparationBlocker().Contains("truncada");
                if (!check.preparationBlocked) throw new InvalidOperationException("Partial content preparation was enabled");
                foreach (var phase in new[] { "Splash", "MainRoom", "Loading" })
                {
                    if (phase == "Splash") ShowSplash(); else if (phase == "Loading") ShowLoading(); else ShowMainRoom();
                    CaptureCurrentView(Path.Combine(output, phase.ToLowerInvariant()+"-9x16.png"), 900, 1600); check.captures++;
                    CaptureCurrentView(Path.Combine(output, phase.ToLowerInvariant()+"-16x9.png"), 1600, 900); check.captures++;
                }
                foreach(var page in new[]{MainPage.Versus,MainPage.Shop,MainPage.Character,MainPage.Adventure})
                {
                    ShowMainRoom(); Navigation.Select(page);
                    CaptureCurrentView(Path.Combine(output,page.ToString().ToLowerInvariant()+"-9x16.png"),900,1600); check.captures++;
                    CaptureCurrentView(Path.Combine(output,page.ToString().ToLowerInvariant()+"-16x9.png"),1600,900); check.captures++;
                }
                check.success = true;
            }
            catch (Exception e) { check.error = e.ToString(); }
            if(check.success)
            {
                var freeplay=new RecoveredFreeplayValidation.Result();
                yield return RecoveredFreeplayValidation.Run(this,Path.GetFullPath(Path.Combine(output,"../../freeplay-v411/player")),freeplay);
                if(!freeplay.success){check.success=false;check.error=freeplay.error;}
            }
            if(check.success)
            {
                var integration=new RecoveredV415Validation.Result();string path=Path.GetFullPath(Path.Combine(output,"../../integration-v419/player"));
                yield return RecoveredV415Validation.Run(this,path,integration);
                File.WriteAllText(Path.Combine(path,"integration-results.json"),JsonUtility.ToJson(integration,true));
                if(!integration.success){check.success=false;check.error=integration.error;}
            }
            if(check.success)
            {
                var dialog=new RecoveredV416Validation.UiResult();RecoveredV416Validation.CheckDialog(this,Path.GetFullPath(Path.Combine(output,"../../integration-v419/dialog-player")),dialog);
                if(!dialog.success){check.success=false;check.error=dialog.error;}
            }
            if(check.success)
            {
                var stage=new RecoveredRemoteStageValidation.Result();string stageOutput=Path.GetFullPath(Path.Combine(output,"../../integration-v419/star-player"));
                yield return RecoveredRemoteStageValidation.Run(this,"RemoteStages606/StarWhenever/stage",stageOutput,stage);
                File.WriteAllText(Path.Combine(stageOutput,"stage-results.json"),JsonUtility.ToJson(stage,true));
                if(!stage.success){check.success=false;check.error=stage.error;}
            }
            if(check.success)
            {
                var stages=SongCatalog.Read<RecoveredStageRegressionList>(Resources.Load<TextAsset>("RemoteStages606/regressions").text);
                foreach(var entry in stages.entries)
                {
                    var stage=new RecoveredRemoteStageValidation.Result();string stageOutput=Path.GetFullPath(Path.Combine(output,"../../integration-v419/stages/"+entry.sid));
                    yield return RecoveredRemoteStageValidation.Run(this,entry.resource,stageOutput,stage);
                    File.WriteAllText(Path.Combine(stageOutput,"stage-results.json"),JsonUtility.ToJson(stage,true));
                    if(!stage.success){check.success=false;check.error=stage.error;break;}
                }
            }
            if(check.success)
            {
                var dunk=new RecoveredDunkValidation.Result();string path=Path.GetFullPath(Path.Combine(output,"../../integration-v419/dunk-player"));
                yield return RecoveredDunkValidation.Run(this,path,dunk,true);
                File.WriteAllText(Path.Combine(path,"dunk-results.json"),JsonUtility.ToJson(dunk,true));
                if(!dunk.success){check.success=false;check.error=dunk.error;}
            }
            if(check.success)
            {
                var local=new P3PlayerValidation.Result();string localOutput=Path.GetFullPath(Path.Combine(output,"../../p3-content/player"));
                yield return P3PlayerValidation.Run(this,localOutput,local);
                File.WriteAllText(Path.Combine(localOutput,"preparation-results.json"),JsonUtility.ToJson(local,true));
                if(!local.success){check.success=false;check.error=local.error;}
            }
            File.WriteAllText(Path.Combine(output, "player-results.json"), JsonUtility.ToJson(check, true));
            Application.Quit(check.success ? 0 : 2);
        }
        [Serializable] private sealed class OnlineCheck
        {public bool success,identityBefore,identityAfter,finished,timeSynced,configVerified,dynamicVerified,profileVerified,wealthVerified,ownRolesVerified,playerIdReceived,reusedPreviousPair;public int main,variants;public string revision,status;public RecoveredServiceClient.ReadAttempt[] requests;}
        private System.Collections.IEnumerator ValidateOwnOnlineRead(string output)
        {
            Directory.CreateDirectory(output);Application.runInBackground=true;
            var result=new OnlineCheck{revision=Revision,identityBefore=Services.HasPersistedPair};
            var priorPair=Services.Identity.HasPair?Services.Identity.Auid.Length+":"+Services.Identity.Auid+Services.Identity.Udid:"";
            StartOwnOnlineTest();float deadline=Time.realtimeSinceStartup+130;
            while(!Services.Finished&&Time.realtimeSinceStartup<deadline)yield return null;
            result.finished=Services.Finished;result.identityAfter=Services.HasPersistedPair;result.status=Services.Status;result.requests=Services.ReadAttempts.ToArray();
            result.timeSynced=Services.TimeSynced;result.configVerified=Services.ConfigVerified;result.dynamicVerified=Services.DynamicConfigVerified;result.profileVerified=Services.ProfileVerified;result.wealthVerified=Services.WealthVerified;result.ownRolesVerified=Services.OwnRolesVerified;
            result.playerIdReceived=Services.ProfileVerified&&!string.IsNullOrEmpty(Account.ServerProfile?.user?.player_id);
            var ownPair=Services.Identity.HasPair?Services.Identity.Auid.Length+":"+Services.Identity.Auid+Services.Identity.Udid:"";result.reusedPreviousPair=result.identityBefore&&priorPair!=""&&priorPair==ownPair;
            if(Services.Catalog!=null){result.main=Services.Catalog.items.Length;result.variants=new RecoveredCatalogIndex(Services.Catalog).Entries.Length-result.main;}
            result.success=result.profileVerified&&result.wealthVerified&&result.ownRolesVerified&&result.configVerified&&result.dynamicVerified&&result.playerIdReceived&&Services.Catalog!=null;
            if(result.identityAfter&&Services.TimeSynced&&result.configVerified&&Services.Catalog!=null){RecoveredPreferences.SetInt("BBPC.Windows.OnlineEnabled",1);RecoveredPreferences.Save();}
            // No screenshots here: an authenticated profile contains private account data.
            File.WriteAllText(Path.Combine(output,"online-results.json"),JsonUtility.ToJson(result,true));Application.Quit(result.success?0:3);
        }
        private Text status, notice; private float noticeUntil; private bool mainShown;
        private System.Collections.IEnumerator ValidateExistingAccount(string output,bool withVerifiedWealth=false)
        {
            Directory.CreateDirectory(output);
            var result=new OnlineCheck{revision=Revision,identityBefore=Services.HasPersistedPair};
            if(!result.identityBefore)throw new InvalidOperationException("Probe requires existing persisted identity");
            string auid=Services.Identity.Auid,udid=Services.Identity.Udid;
            yield return Services.ProbeExistingAccount(withVerifiedWealth);
            result.identityAfter=Services.HasPersistedPair;result.reusedPreviousPair=auid==Services.Identity.Auid&&udid==Services.Identity.Udid;
            result.finished=true;result.timeSynced=Services.TimeSynced;result.configVerified=Services.ConfigVerified;result.profileVerified=Services.ProfileVerified;result.ownRolesVerified=Services.OwnRolesVerified;result.wealthVerified=Services.WealthVerified;
            result.playerIdReceived=Services.ProfileVerified&&!string.IsNullOrEmpty(Account.ServerProfile?.user?.player_id);
            result.requests=Services.ReadAttempts.ToArray();result.success=result.profileVerified&&result.ownRolesVerified&&result.playerIdReceived;result.status=Services.Status;
            File.WriteAllText(Path.Combine(output,"account-probe-results.json"),JsonUtility.ToJson(result,true));Application.Quit(result.success?0:3);
        }
        private System.Collections.IEnumerator Start()
        {
            var core = GetComponent<PortBootstrap>();
            if (core.Clock == null || core.NormalScores == null) throw new InvalidOperationException("P1 gameplay core did not initialize");
            Initialize();
            yield return BootstrapLocalFrontend();
            var args = Environment.GetCommandLineArgs(); int test = Array.IndexOf(args, "-bbpc-validate");
            int stages=Array.IndexOf(args,"-bbpc-stage-check");
            if(stages>=0&&stages+1<args.Length){yield return RecoveredStageProbe.Run(this,args[stages+1]);yield break;}
            int access=Array.IndexOf(args,"-bbpc-access-check");
            if(access>=0&&access+1<args.Length){yield return RecoveredV416Validation.Remote(this,args[access+1],Array.IndexOf(args,"-bbpc-unlock-once")>=0);yield break;}
            int covers=Array.IndexOf(args,"-bbpc-cover-check");
            if(covers>=0&&covers+1<args.Length)
            {
                var check=new RecoveredV415Validation.Result();yield return RecoveredV415Validation.Run(this,args[covers+1],check,true);
                File.WriteAllText(Path.Combine(args[covers+1],"integration-results.json"),JsonUtility.ToJson(check,true));Application.Quit(check.success?0:2);yield break;
            }
            if(test>=0&&test+1<args.Length&&Array.IndexOf(args,"-bbpc-result606-check")>=0)
            {
                var check=new RecoveredResult606Validation.Receipt();
                yield return RecoveredResult606Validation.Run(this,args[test+1],check);
                File.WriteAllText(Path.Combine(args[test+1],"result606-results.json"),JsonUtility.ToJson(check,true));
                Application.Quit(check.success?0:2);yield break;
            }
            if (test >= 0 && test + 1 < args.Length) yield return ValidatePlayer(args[test + 1]);
            int probe=Array.IndexOf(args,"-bbpc-account-probe");
            int wealthProbe=Array.IndexOf(args,"-bbpc-account-wealth-probe");
            int dunkTest=Array.IndexOf(args,"-bbpc-dunk-check");
            int online=Array.IndexOf(args,"-bbpc-online-check");
            if(dunkTest>=0&&dunkTest+1<args.Length)
            {
                var result=new RecoveredDunkValidation.Result();yield return RecoveredDunkValidation.Run(this,args[dunkTest+1],result);
                File.WriteAllText(Path.Combine(args[dunkTest+1],"dunk-results.json"),JsonUtility.ToJson(result,true));Application.Quit(result.success?0:2);
            }
            else if(wealthProbe>=0&&wealthProbe+1<args.Length)yield return ValidateExistingAccount(args[wealthProbe+1],true);
            else if(probe>=0&&probe+1<args.Length)yield return ValidateExistingAccount(args[probe+1]);
            else if(online>=0&&online+1<args.Length)yield return ValidateOwnOnlineRead(args[online+1]);
            else if(test<0&&RecoveredPreferences.GetInt("BBPC.Windows.OnlineEnabled",0)==1)StartOwnOnlineTest();
        }
        private System.Collections.IEnumerator BootstrapLocalFrontend()
        {
            // Offline Windows bootstrap. This is not the original network/bootstrap implementation.
            ShowCatalog(); ShowSplash(); float readyAt = Time.time;
            while (Time.time - readyAt < 0.75f) yield return null; // Native SplashPresenter.Update minimum; no fade.
            ShowMainRoom(); Navigation.Select(MainPage.Home);
        }
        public void Initialize()
        {
            if (Initialized) return;

            var clearObject = new GameObject("Windows letterbox clear", typeof(Camera)); clearObject.transform.SetParent(transform, false);
            var clear = clearObject.GetComponent<Camera>(); clear.clearFlags = CameraClearFlags.SolidColor; clear.backgroundColor = Color.black; clear.cullingMask = 0; clear.depth = -101;
            var cameraObject = new GameObject("Windows portrait UI camera", typeof(Camera), typeof(AudioListener)); cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 0, -10);
            PortraitCamera = cameraObject.GetComponent<Camera>(); PortraitCamera.clearFlags = CameraClearFlags.SolidColor; PortraitCamera.backgroundColor = Color.black;
            PortraitCamera.enabled = true; PortraitCamera.targetDisplay = 0; PortraitCamera.depth = -100; PortraitCamera.orthographic = true; PortraitCamera.orthographicSize = 896;
            PortraitCamera.nearClipPlane = 0.1f; PortraitCamera.farClipPlane = 1000;
            var source = Resources.Load<TextAsset>("Recovered606/ui");
            if (source == null) throw new InvalidOperationException("Missing recovered UI data");
            Data = JsonUtility.FromJson<RecoveredUiData>(source.text);
            if (Data.sourceVersion != "6.0.6") throw new InvalidOperationException("Incorrect asset version");
            Splash = RecoveredUiBuilder.Build(Data, Data.scenes[0], transform, PortraitCamera);
            MainRoom = RecoveredUiBuilder.Build(Data, Data.scenes[1], transform, PortraitCamera);
            var loadingSource = Resources.Load<TextAsset>("Recovered606/loading");
            if (loadingSource != null)
            {
                var loadingData = JsonUtility.FromJson<RecoveredUiData>(loadingSource.text);
                Loading = RecoveredUiBuilder.Build(loadingData, loadingData.scenes[0], transform, PortraitCamera); Loading.Root.SetActive(false);
            }
            var playHost = new GameObject("Recovered PlayRoom host"); playHost.transform.SetParent(transform,false);
            RecoveredStageContent.EnsureLocalBindings();
            PlayRoom = playHost.AddComponent<RecoveredPlayRoom>(); PlayRoom.Initialize(PortraitCamera);PlayRoom.ReturnToMenu=ShowMainRoom;PlayRoom.Replay=variant=>{if(variant.StartsWith("remote:",StringComparison.Ordinal)&&SelectedStageInfo!=null)StartCoroutine(PrepareRemoteStageFlow(SelectedStageInfo));else PrepareLocalTutorial(variant);};PlayRoom.StateChanged=state=>{if(state==RecoveredPlayState.Countdown)Phase=FrontendPhase.StageCountdown;else if(state==RecoveredPlayState.Dead)Phase=FrontendPhase.Defeat;else if(state==RecoveredPlayState.Playing||state==RecoveredPlayState.Paused)Phase=FrontendPhase.PlayRoom;};PlayRoom.ResultsShown=OnResultsShown;
            MainRoom.Root.SetActive(false);
            Navigation = new RecoveredNavigation(MainRoom, PortraitCamera, OnMainPageChanged);
            // Exact native customNewHint/friendNewHint references; Awake hides both.
            MainRoom.Objects["level3:5"].SetActive(false); MainRoom.Objects["level3:24"].SetActive(false);
            var battlePassBarRoot = MainRoom.Find("BattlePassBar");
            if (battlePassBarRoot != null)
            {
                BattlePassBar = gameObject.AddComponent<RecoveredBattlePassBar>();
                BattlePassBar.Initialize(this, battlePassBarRoot);
            }
            if (catalogSnapshot == null)
            {
                catalogSnapshot = Resources.Load<TextAsset>("Catalog606/public-freeplay");
                if (catalogSnapshot != null) catalogOrigin = CatalogOrigin.PublicOffline606;
                else {catalogSnapshot=Resources.Load<TextAsset>("Catalog606/captured-catalog");if(catalogSnapshot!=null)catalogOrigin=CatalogOrigin.Captured606Partial;}
            }
            // Do not display serialized authoring counts as a player's saved progress.
            foreach (var name in new[] { "DiamondsText", "CountText", "NumText", "Countdown" })
                foreach (var pair in MainRoom.Objects) if (pair.Value.name == name && pair.Value.TryGetComponent<Text>(out var text)) text.text = "—";
            foreach (var pair in MainRoom.Objects)
                if (pair.Value.TryGetComponent<Button>(out var button)) { button.interactable = false; }
            Bind("FreePlayButton", () => ShowCatalog());
            Bind("StoryModeButton", ShowStoryCatalog);
            // MainRoom buttons recovered from Recovered606/ui.json. Each one answers from verified
            // server data, or states plainly that the original panel is not reconstructed yet.
            Bind("FavoritesButton", ShowFavoriteSongs);
            MainRoom.Find("RankPanelButton")?.SetActive(true);
            Bind("RankPanelButton", ShowLeaderboard);
            Bind("SignInButton", ShowDailyTasks);
            Bind("RecommendButton", ShowFreeStage);
            Bind("VipButton", ShowVipStatus);
            Bind("ExtraHpButton", () => HomeDialogs.ShowExtraHp());
            Bind("RemoveADButton", ShowPopupAdsEntitlement);
            Bind("SettingButton", () => HomeDialogs.ShowSettings());
            // Exact observed path: avoid binding unrelated AddButtons in other widgets/pages.
            var diamondAdd = MainRoom.Find("HomePageTopView")?.transform.Find("DiamondPurchaseComponent/AddButton")?.GetComponent<Button>();
            if(diamondAdd!=null)
            {
                diamondAdd.interactable=true;diamondAdd.onClick.RemoveAllListeners();
                diamondAdd.onClick.AddListener(()=>Navigation.Select(MainPage.Shop));
            }
            Bind("BattlePassBar", () => HomeDialogs.ShowBattlePass());
            if (FindObjectOfType<EventSystem>() == null) new GameObject("BB_PC input", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform);
            if (GetComponent<RecoveredDebugCapture>() == null) gameObject.AddComponent<RecoveredDebugCapture>();
            Services = GetComponent<RecoveredServiceClient>() ?? gameObject.AddComponent<RecoveredServiceClient>();
            Account = Services.Account;
            Services.AccountChanged = RefreshOwnAccountHud;
            Services.FavoritesChanged = () => FullCatalog?.ApplyServerFavorites(Services.ServerFavorites);
            Services.Completed = () => ApplyCatalogArrival(Services.Catalog);
            // Navigation already builds the recovered Shop/Character pages. Do not cover them
            // with the procedural Windows shop controls; original data bindings remain pending.
            Results=gameObject.AddComponent<RecoveredResultPanel>();Results.Build(this);
            HomeDialogs=gameObject.AddComponent<RecoveredHomeDialogs>();HomeDialogs.Initialize(this);
            // A missing slot in these pages must never take the whole frontend down: log and leave the page as authored.
            try{ShopScreen=gameObject.AddComponent<RecoveredShopScreen>();ShopScreen.Initialize(this);}catch(Exception e){Debug.LogException(e);if(ShopScreen!=null)Destroy(ShopScreen);ShopScreen=null;}
            try{SkinsScreen=gameObject.AddComponent<RecoveredSkinsScreen>();SkinsScreen.Initialize(this);}catch(Exception e){Debug.LogException(e);if(SkinsScreen!=null)Destroy(SkinsScreen);SkinsScreen=null;}
            if(BattlePassBar!=null)BattlePassBar.OnClickAction=HomeDialogs.ShowBattlePass;
            CreateDiagnostics(); Initialized = true; RefreshOwnAccountHud(); ConfigureViewport(Screen.width, Screen.height);
            Report("Recursos 6.0.6 recuperados. Inicialización remota pendiente. Usa MainRoom para inspeccionar el menú.");
        }
        private void Bind(string name, UnityEngine.Events.UnityAction action)
        {
            var go = MainRoom.Find(name); if (go == null || !go.TryGetComponent<Button>(out var b)) return;
            b.interactable = true; b.onClick.RemoveAllListeners(); b.onClick.AddListener(action);
        }
        public void ShowMainRoom()
        {
            Initialize(); ClosePlayback(); Splash.Root.SetActive(false); if (Loading != null) Loading.Root.SetActive(false); MainRoom.Root.SetActive(true); mainShown = true; Phase = FrontendPhase.MainRoom; IsInspection = false;
            Report("MainRoom recuperado. Datos de cuenta, modos y animaciones aún pendientes. Catálogo: dependencia remota.");
        }
        public void ShowSplash() { Initialize(); ClosePlayback(); Phase = FrontendPhase.Splash; IsInspection = false; if (Loading != null) Loading.Root.SetActive(false); MainRoom.Root.SetActive(false); Splash.Root.SetActive(true); mainShown = false; }
        public void ShowLoading()
        {
            Initialize(); ClosePlayback(); Splash.Root.SetActive(false); MainRoom.Root.SetActive(false); mainShown = false;
            if (Loading == null) throw new InvalidOperationException("Original Loading hierarchy unavailable");
            Loading.Root.SetActive(true); SetLoadingProgress(1); IsInspection = true; Report("Inspección de Loading de partida original; no representa el arranque inicial ni progreso real.");
        }
        private void ClosePlayback() { HomeDialogs?.Close(); Results?.Close(); if(PlayRoom!=null)PlayRoom.Close(); }
        private void OnMainPageChanged(MainPage page)
        {
            HomeDialogs?.Close();
            Skins?.SetOpen(page==MainPage.Character); Store?.SetOpen(page==MainPage.Shop); ShopScreen?.SetOpen(page==MainPage.Shop); SkinsScreen?.SetOpen(page==MainPage.Character);
            Report(page==MainPage.Home?"Catálogo local; acceso de cada canción sujeto a verificación.":page.ToString());
        }
        public void ShowPopupAdsEntitlement()
        {
            bool known=Services.WealthVerified||Account.HasPersistedServerWealth;
            ShowUnavailable(!known?"Anuncios popup: entitlement todavía no verificado.":
                (HasPopupAdsEntitlement(Account.Wealth)?"Compra restaurada de cuenta móvil, reportada por servidor: elimina interstitials únicamente.":"Compra de quitar interstitials no registrada en esta cuenta.")+
                (Services.WealthVerified?" (servidor).":" (último estado conocido).")+
                " Windows no integra SDK de anuncios; no se muestran anuncios ni se simulan recompensas publicitarias.");
        }
        public void ShowUnavailable(string message)
        { if(notice!=null){notice.text=message;notice.gameObject.SetActive(true);noticeUntil=Time.unscaledTime+5;}Report(message); }
        public void DismissNotice() { if(notice!=null)notice.gameObject.SetActive(false); }
        private void ShowFavoriteSongs()
        {
            ShowCatalog();
            if(FullCatalog==null||(catalogOrigin!=CatalogOrigin.Verified606Snapshot&&catalogOrigin!=CatalogOrigin.PublicOffline606)){ShowUnavailable("Favoritos: la vista de catalogo completa no esta disponible en esta fuente.");return;}
            FullCatalog.Filter("",0,0,true);
            ShowUnavailable("Favoritos: lista local y del servidor.");
        }
        public void ShowLeaderboard() { if(Services!=null)HomeDialogs.ShowLeaderboard(); }
        public void ShowDailyTasks() { if(Services!=null)HomeDialogs.ShowDailyTasks(); }
        public void ShowFreeStage() { if(Services!=null)HomeDialogs.ShowFreeStage(); }
        public void ShowVipStatus()
        {
            if(Services==null)return;
            if(!Services.WealthVerified){ShowUnavailable("VIP: requiere identidad propia verificada y riqueza del servidor.");return;}
            string until=Account.Wealth.expiration>0?DateTimeOffset.FromUnixTimeMilliseconds(Account.Wealth.expiration).ToLocalTime().ToString("yyyy-MM-dd"):"sin vencimiento registrado";
            ShowUnavailable("VIP (servidor): "+Account.Wealth.vip+"; "+until);
        }
        private void SetLoadingProgress(float progress)
        {
            var slider=Loading.Find("LoadingSlider").GetComponent<Slider>();slider.normalizedValue=progress;
            var label=Loading.Find("LoadingLabel").GetComponent<Text>();label.text=RecoveredLocalization.Text("loading","loading")+" "+Mathf.RoundToInt(progress*100)+"%";
        }
        public void PrepareLocalTutorial(string variant)
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Local audio preparation requires Play Mode or Player");
            if(preparingTutorial)throw new InvalidOperationException("Local preparation already running");
            StartCoroutine(PrepareLocalTutorialFlow(variant));
        }
        public System.Collections.IEnumerator PrepareLocalTutorialFlow(string variant)
        {
            if(preparingTutorial)throw new InvalidOperationException("Local preparation already running");
            Initialize(); ShowLoading();Phase=FrontendPhase.SongPreparation;IsInspection=true;preparingTutorial=true;TutorialPreparationError=null;SetLoadingProgress(0);
            yield return null; // Give real Loading a frame; no invented loading duration.
            PreparedTutorial content=null;
            yield return RecoveredTutorialContent.Prepare(variant,x=>content=x,error=>TutorialPreparationError=error,SetLoadingProgress);
            if(content==null){preparingTutorial=false;ShowMainRoom();ShowUnavailable(TutorialPreparationError??"Tutorial preparation failed");yield break;}
            PlayRoom.Open(content);Canvas.ForceUpdateCanvases();
            yield return null; // Finish stage layout/render before taking the native Time.time/DSP preheat samples.
            preparingTutorial=false;Loading.Root.SetActive(false);Phase=FrontendPhase.PlayRoom;IsInspection=false;
            inspectionMode=false;diagnostics.SetActive(false);
            Report("Contenido auténtico local. D/F/J/K o mouse; Espacio pausa/reanuda; Esc vuelve.");
        }
        public void OpenCapturedFlow() { ShowMainRoom(); Navigation.Select(MainPage.Home); ShowCatalog(); }
        public bool TryPrepareSelectedSong()
        {
            if(Selection.Origin==CatalogOrigin.ValidatedLocal606&&string.IsNullOrEmpty(Selection.PreparationBlocker())){PrepareLocalTutorial(SongSelection.LocalVariant(Selection.Song));return true;}
            // Entry gate: metadata alone never authorizes Loading/PlayRoom.
            string blocker = Selection.PreparationBlocker();
            if (!string.IsNullOrEmpty(blocker)) { Report(blocker); return false; }
            throw new NotSupportedException("Validated audio/chart and PlayRoom lifecycle are required before entering song preparation");
        }
        public SongItem SelectedStageInfo { get; private set; }
        public RecoveredPreparationTrace CurrentPreparation {get;private set;}
        private string readingStageKey;private int stageReadGeneration;private bool readingStagePrepare;
        private Action<string> queuedStagePrepare;
        public void ReadSelectedStageInfo(SongMetadata song,Action<string> report,bool prepare=false)
        {
            if(song==null)throw new ArgumentNullException(nameof(song));
            if(readingStageKey==song.Key)
            {
                if(prepare&&!readingStagePrepare)queuedStagePrepare=report;
                report(prepare&&!readingStagePrepare?"Preparación solicitada; esperando los metadatos en curso…":"Solicitud de esta variante en curso");return;
            }
            queuedStagePrepare=null;readingStagePrepare=prepare;
            readingStageKey=song.Key;StartCoroutine(ReadSelectedStageInfoFlow(song,report,prepare,++stageReadGeneration));
        }
        public void ApplyCatalogArrival(SongCatalog available)
        {
            // A late bootstrap response may refresh Home/Freeplay, never close Story, another tab or a live game.
            if(available!=null&&Phase==FrontendPhase.MainRoom&&Navigation.Current==MainPage.Home&&Stories?.Catalog==null)ShowCatalog();
            else Report(Services.Status);
        }
        private System.Collections.IEnumerator ReadSelectedStageInfoFlow(SongMetadata song,Action<string> report,bool prepare,int generation)
        {
            var trace=prepare?new RecoveredPreparationTrace(song.sid,song.aid):null;
            if(trace!=null){CurrentPreparation=trace;trace.Mark("StageInfoAccess");}
            SelectedStageInfo=null;report(prepare?"Comprobando acceso remoto de esta variante…":"Consultando metadatos de esta variante…");
            SongItem details=null;
            try{if(prepare)yield return Services.EnsureStageAccess(song,value=>details=value);else yield return Services.FetchStageInfo(song,value=>details=value);}
            finally{if(generation==stageReadGeneration)readingStageKey=null;}
            if(generation!=stageReadGeneration||Selection.Song==null||Selection.Song.Key!=song.Key||Phase!=FrontendPhase.MainRoom)yield break;
            if(!prepare&&queuedStagePrepare!=null)
            {
                var next=queuedStagePrepare;queuedStagePrepare=null;ReadSelectedStageInfo(song,next,true);yield break;
            }
            if(details==null){trace?.Finish("access-or-info-failed");report(Services.Status);yield break;}
            SelectedStageInfo=details;
            if(!prepare){report("Metadatos verificados para "+details.stage.title+". "+RecoveredStageContent.PreparationBlocker(details));yield break;}
            string blocker=RecoveredStageContent.PreparationBlocker(details);
            if(blocker!=""){report("Acceso remoto confirmado. Contenido Windows pendiente: "+blocker);yield break;}
            GetComponent<RecoveredChoiceLevelDialog>()?.Close();
            trace?.Mark("AccessConfirmed");yield return PrepareRemoteStageFlow(details,true,trace);
        }
        public System.Collections.IEnumerator PrepareRemoteStageFlow(SongItem details,bool allowNetwork=true,RecoveredPreparationTrace trace=null)
        {
            if(preparingTutorial)throw new InvalidOperationException("Content preparation already running");
            trace=trace??new RecoveredPreparationTrace(details.stage.sid,details.stage.aid);CurrentPreparation=trace;
            SelectedStageInfo=details;var selectedKey=Selection.Song?.Key;trace.Mark("StageInfoAccessConfirmed");
            ShowLoading();Phase=FrontendPhase.SongPreparation;preparingTutorial=true;TutorialPreparationError=null;SetLoadingProgress(0);
            Exception failure=null;
            yield return RecoveredPreparationTrace.Guard(PrepareRemoteStageCore(details,allowNetwork,trace,selectedKey),error=>{failure=error;Debug.LogException(error);},()=>false);
            if(failure!=null)
            {
                preparingTutorial=false;TutorialPreparationError=trace.phase+": "+failure.GetType().Name+": "+failure.Message;
                ShowMainRoom();ShowUnavailable(TutorialPreparationError);trace.Finish("failed",failure);
            }
            else if(trace.status=="running"){preparingTutorial=false;trace.Finish("cancelled");}
        }
        private System.Collections.IEnumerator PrepareRemoteStageCore(SongItem details,bool allowNetwork,RecoveredPreparationTrace trace,string selectedKey)
        {
            yield return null;
            PreparedTutorial content=null;
            yield return RecoveredStageContent.Prepare(details,x=>content=x,e=>TutorialPreparationError=e,SetLoadingProgress,()=>Selection.Song?.Key!=selectedKey||Phase!=FrontendPhase.SongPreparation,allowNetwork,trace.Mark);
            if(Selection.Song?.Key!=selectedKey||Phase!=FrontendPhase.SongPreparation){preparingTutorial=false;if(content!=null)UnityEngine.Object.Destroy(content.Audio);yield break;}
            if(content==null)throw new InvalidDataException(TutorialPreparationError??"Preparación sin callback Ready");
            trace.Mark("ActorCreation");PlayRoom.Open(content);trace.Mark("CameraAndLayout");Canvas.ForceUpdateCanvases();trace.Mark("PlayRoomReady");yield return null;
            if(Selection.Song?.Key!=selectedKey||Phase==FrontendPhase.MainRoom||Phase==FrontendPhase.Splash){preparingTutorial=false;yield break;}
            preparingTutorial=false;Loading.Root.SetActive(false);Phase=FrontendPhase.PlayRoom;IsInspection=false;inspectionMode=false;diagnostics.SetActive(false);
            trace.Mark("LoadingDismissed");trace.Finish("ready");
            Report("Contenido verificado. D/F/J/K o mouse; Espacio pausa; Esc vuelve.");
        }
        private void RefreshOwnAccountHud()
        {
            // Serialized BFTicketCountComponent/CountText and DiamondPurchaseComponent/DiamondsText.
            // Local defaults and test fixtures are never displayed as server-confirmed account balances.
            if(Services.ProfileVerified)
            {
                foreach(var page in Navigation.Pages.Values)
                {
                    ApplyProfileName(page,Account.Profile.screen_name);
                }
            }
            bool known=Services.WealthVerified||Account.HasPersistedServerWealth;
            string suffix=Services.WealthVerified?"":" (último saldo conocido)";
            foreach(var tree in new[]{MainRoom,Navigation.Bar})
            {
                SetText(tree,"DiamondsText",known?Account.Wealth.diamond.ToString(System.Globalization.CultureInfo.InvariantCulture)+suffix:"—");
                SetText(tree,"CountText",known?Account.Wealth.bf_draw_ticket.ToString(System.Globalization.CultureInfo.InvariantCulture)+suffix:"—");
            }
            foreach(var tree in Navigation.Pages.Values)
                SetText(tree,"DiamondsText",known?Account.Wealth.diamond.ToString(System.Globalization.CultureInfo.InvariantCulture)+suffix:"—");
            var remove=MainRoom.Find("RemoveADButton");
            if(remove!=null)foreach(var label in remove.GetComponentsInChildren<Text>(true))
                label.text=known&&HasPopupAdsEntitlement(Account.Wealth)?"Popup ads: adquirido":"Quitar popup ads";
            Skins?.Refresh();Store?.Refresh();SkinsScreen?.Refresh();
        }
        // Only bind verified fields. Guild/rank/emoji and a downloadable nameframe need their own models.
        public static void ApplyProfileName(RecoveredUiTree page,string screenName)
        {
            var box=page.Find("NameBoxComponent");if(box!=null)
            {
                box.SetActive(true);
                foreach(var text in box.GetComponentsInChildren<Text>(true))if(text.name=="NameText"){text.supportRichText=false;text.text=screenName??"";}
                foreach(var child in box.GetComponentsInChildren<Transform>(true))if(child.name=="GuildParent"||child.name=="FriendOrEmoji"||child.name=="rankingText")child.gameObject.SetActive(false);
            }
            foreach(var go in page.Objects.Values)if(go!=null&&go.name=="UserNameText"&&go.TryGetComponent<Text>(out var text)){text.supportRichText=false;text.text=screenName??"";}
        }
        [ContextMenu("Online / initialize own Windows test identity once")]
        public void StartOwnOnlineTest()
        {
            Initialize(); if (Services.Attempted) { Report(Services.Status); return; }
            Report("Inicialización de identidad propia; no es Loading de partida."); Services.StartOwnOnlineTest();
        }
        public void ShowCatalog()
        {
            if (!mainShown) ShowMainRoom();
            Navigation.Select(MainPage.Home);
            Stories?.Close();
            MainRoom.Find("StoryMode")?.SetActive(false);MainRoom.Find("FreePlayMode")?.SetActive(true);
            FullCatalog?.Close();
            SongCatalog catalog;
            if (Services != null && Services.Catalog != null) { catalog = Services.Catalog; catalogOrigin = CatalogOrigin.Verified606Snapshot; }
            else
            {
                if (catalogSnapshot == null || catalogOrigin == CatalogOrigin.Unavailable) { Report(Selection.PreparationBlocker()); return; }
                catalog = catalogOrigin==CatalogOrigin.PublicOffline606?CatalogEnvelope.Parse(catalogSnapshot.text):SongCatalog.Parse(catalogSnapshot.text);
            }
            var localTutorial=RecoveredTutorialContent.Definition("normal");
            var tutorialStage=new SongMetadata{sid=localTutorial.sid,aid=localTutorial.aid,title=localTutorial.title,artist=localTutorial.artist,hp=localTutorial.hp,play_type=localTutorial.playType,difficulty_v2=localTutorial.difficulty};
            var elasticStage=SongCatalog.Read<SongMetadata>(Resources.Load<TextAsset>("LocalStages606/stage").text);
            if(catalogOrigin==CatalogOrigin.PublicOffline606||catalogOrigin==CatalogOrigin.Verified606Snapshot)
            {
                var entries=new System.Collections.Generic.List<SongItem>(catalog.items);entries.Insert(0,new SongItem{stage=tutorialStage});
                var offline=new SongCatalog{items=entries.ToArray(),filters=catalog.filters};CatalogCount=entries.Count;
                FullCatalog=GetComponent<RecoveredCatalogView>()??gameObject.AddComponent<RecoveredCatalogView>();FullCatalog.Open(this,offline);
                Report(catalogOrigin==CatalogOrigin.PublicOffline606?"Catálogo público offline; favoritos guardados solo en Windows.":"Catálogo del servicio; recursos y acceso de cada canción aún requieren validación.");return;
            }
            var displayed=new System.Collections.Generic.List<SongItem>(catalog.items);displayed.Add(new SongItem{stage=tutorialStage});displayed.Add(new SongItem{stage=elasticStage});
            CatalogCount = displayed.Count;
            // Windows adapter: no reconstructed filter selection exists, and this catalog has finished parsing.
            // Keep original objects/resources; do not display an authoring selection or a fictitious pending load.
            MainRoom.Find("DiffSelected")?.SetActive(false); MainRoom.Find("LoadingIndicator")?.SetActive(false);
            var scroll = MainRoom.Find("SonglistScrollView")?.GetComponent<ScrollRect>();
            if (scroll == null || scroll.content == null) throw new InvalidOperationException("Original song list content unresolved");
            for (int i = scroll.content.childCount - 1; i >= 0; --i)
            {
                var child = scroll.content.GetChild(i).gameObject; if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            float height = Math.Abs(Data.row.objects[0].rect[7]);
            if (height <= 0) throw new InvalidOperationException("Original SongCellView height invalid");
            // Native GetCellView(0) uses MainHeadView; GetCellViewSize(0) reserves 550 + outSideTop.
            // Windows viewport has no Android safe-area inset; reuse original HomePageTopView height (550).
            float headerHeight = 0; foreach (var original in Data.scenes[1].objects) if (original.name == "HomePageTopView") headerHeight = Math.Abs(original.rect[7]);
            if (headerHeight <= 0) throw new InvalidOperationException("Original catalog header height unavailable");
            int index = 0;
            foreach (var item in displayed)
            {
                bool local=ReferenceEquals(item.stage,tutorialStage)||ReferenceEquals(item.stage,elasticStage);var rowOrigin=local?CatalogOrigin.ValidatedLocal606:catalogOrigin;
                var row = RecoveredUiBuilder.Build(Data, Data.row, scroll.content);
                var rt = row.Objects[Data.row.objects[0].id].GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0,1); rt.anchorMax = new Vector2(1,1); rt.pivot = new Vector2(0.5f, 1); rt.sizeDelta = new Vector2(0, rt.sizeDelta.y); rt.anchoredPosition = new Vector2(0, -headerHeight - height * index++);
                rt.gameObject.SetActive(true); scroll.content.gameObject.SetActive(true);
                SetText(row, "SongText", string.IsNullOrEmpty(item.stage.title) ? "SID: " + item.stage.sid : item.stage.title);
                SetText(row, "ArtistText", !local && catalogOrigin == CatalogOrigin.Captured606Partial ? "Captura truncada; título no recuperado" : item.stage.artist);
                foreach (var pair in row.Objects) if (pair.Value.TryGetComponent<Button>(out var b)) b.interactable = false;
                // Restriction/achievement are deliberately not projected into a false unlocked state.
                foreach (var n in new[] { "Stars", "ADButton", "HotTag", "NewTag", "DifficultyText", "StarNumberText" }) { var go = row.Find(n); if (go != null) go.SetActive(false); }
                var play = row.Find("PlayButton")?.GetComponent<Button>();
                if(play!=null){play.interactable=local;if(local)play.onClick.AddListener(()=>{Selection.Select(item.stage,rowOrigin);TryPrepareSelectedSong();});}
                if(local)row.Find("CoverIcon")?.SetActive(false);
                // Selecting a record does not prepare or play missing content.
                var title = row.Find("SongText")?.GetComponent<Text>();
                if (title != null)
                {
                    title.raycastTarget = true; var select = title.GetComponent<Button>() ?? title.gameObject.AddComponent<Button>();
                    select.targetGraphic = title; select.interactable = true;
                    select.onClick.AddListener(() => { Selection.Select(item.stage, rowOrigin); Report(item.stage.title + " | " + Selection.PreparationBlocker()); });
                }
            }
            scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, headerHeight + height * index);
            var up = MainRoom.Find("PageUpButton"); if (up != null) up.SetActive(false); // No scrolled content in the partial fixture.
            Report(catalogOrigin + ": " + CatalogCount + " registros locales/capturados; no catálogo remoto completo. Tutorial y Elastic locales disponibles.");
        }
        private static void SetText(RecoveredUiTree tree, string name, string value) { var t = tree.Find(name)?.GetComponent<Text>(); if (t != null) t.text = value ?? ""; }
        public void ShowStoryCatalog()
        {
            ShowMainRoom();Navigation.Select(MainPage.Home);FullCatalog?.Close();Stories?.Close();
            MainRoom.Find("StoryMode")?.SetActive(true);MainRoom.Find("FreePlayMode")?.SetActive(false);
            Stories=GetComponent<RecoveredStoryView>()??gameObject.AddComponent<RecoveredStoryView>();Stories.Open(this);
            Report("Story offline: 42 historias y 125 capítulos originales; contenido y progreso de cuenta aún pendientes.");
        }
        public void Report(string text) { if (status != null) status.text = text; }
        private void OnResultsShown()
        {
            Phase = FrontendPhase.ResultRoom; IsInspection = false;
            var played=PlayRoom.Result;if(played==null)return;
            Services.BuildOwnRecommendation(played.sid,played.aid);
            Results.BeginResult(played);
            if(LocalReportsEnabled)
            {
                string directory=Path.Combine(Application.persistentDataPath,"LocalReports");Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory,played.roundId+".json"),JsonUtility.ToJson(played,true));
            }
            if(PlayRoom.UsedBotplay||PlayRoom.IsLocalSandbox||PlayRoom.UsedLocalRevive||RecoveredStorage.Isolated)
            {Results.SetReportStatus(played,PlayRoom.UsedBotplay?"Botplay local: reporte servidor bloqueado":"Prueba offline: sin reporte ni cambio de saldo real");return;}
            TryReportOwnResult();
        }
        private void TryReportOwnResult()
        {
            StartCoroutine(ReportOwnResultFlow(PlayRoom.Result));
        }
        private System.Collections.IEnumerator ReportOwnResultFlow(RecoveredStageResult played)
        {
            RecoveredReportResultData result = null;
            yield return Services.ReportOwnResult(played, value => result = value);
            if(!Results.IsCurrent(played))yield break;
            if (result == null) { Results.SetReportStatus(played,"Reporte fallido; sin recompensa confirmada."); yield break; }
            Results.SetReportStatus(played,"Reporte confirmado por el servidor",result);
            bool refreshed=false;
            yield return Services.RefreshWealth(WealthRefreshTrigger.Reward, wealth => refreshed=wealth!=null);
            if(!Results.IsCurrent(played))yield break;
            Results.SetReportStatus(played,refreshed?"Reporte confirmado · saldo actualizado":"Reporte confirmado · actualización de saldo fallida",result);
        }
        private void CreateDiagnostics()
        {
            var go = new GameObject("P2 diagnostic controls — temporary", typeof(RectTransform), typeof(Canvas), typeof(RecoveredCanvasScaler), typeof(GraphicRaycaster)); go.transform.SetParent(transform, false);
            diagnostics = go; go.SetActive(inspectionMode);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = PortraitCamera; canvas.planeDistance = 100; canvas.sortingOrder = 1000;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(828, 1792); scaler.matchWidthOrHeight = 1;
            Font font = null; foreach (var a in Data.assets) if (a.type == "Font") { font = Resources.Load<Font>(a.resource); break; }
            status = Label(go.transform, "Estado P2 — controles temporales", font, new Vector2(0, 150), new Vector2(810, 150));
            ActionButton(go.transform, "Splash", font, -310, ShowSplash); ActionButton(go.transform, "Loading", font, -103, ShowLoading); ActionButton(go.transform, "MainRoom", font, 103, ShowMainRoom); ActionButton(go.transform, "Tutorial local", font, 310, () => PrepareLocalTutorial("normal"));
            var bot=Label(go.transform,"Botplay próxima partida: OFF",font,new Vector2(-260,330),new Vector2(250,110));bot.raycastTarget=true;
            bot.gameObject.AddComponent<Button>().onClick.AddListener(()=>{PlayRoom.BotplayEnabled=!PlayRoom.BotplayEnabled;bot.text="Botplay próxima partida: "+(PlayRoom.BotplayEnabled?"ON":"OFF");});
            var local=Label(go.transform,"Report local: ON",font,new Vector2(0,330),new Vector2(250,110));local.raycastTarget=true;
            local.gameObject.AddComponent<Button>().onClick.AddListener(()=>{LocalReportsEnabled=!LocalReportsEnabled;local.text="Report local: "+(LocalReportsEnabled?"ON":"OFF");});
            var server=Label(go.transform,"Servidor: OFF",font,new Vector2(260,330),new Vector2(250,110));server.raycastTarget=true;
            server.gameObject.AddComponent<Button>().onClick.AddListener(()=>{ServerReportsRequested=!ServerReportsRequested;server.text=ServerReportsRequested?"Servidor: bloqueado; sin autorización":"Servidor: OFF";});
            var messages = new GameObject("Port availability notice",typeof(RectTransform),typeof(Canvas),typeof(RecoveredCanvasScaler));messages.transform.SetParent(transform,false);
            var messageCanvas=messages.GetComponent<Canvas>();messageCanvas.renderMode=RenderMode.ScreenSpaceCamera;messageCanvas.worldCamera=PortraitCamera;messageCanvas.planeDistance=100;messageCanvas.sortingOrder=999;
            var messageScaler=messages.GetComponent<RecoveredCanvasScaler>();messageScaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;messageScaler.referenceResolution=new Vector2(828,1792);
            notice=Label(messages.transform,"",font,new Vector2(0,330),new Vector2(790,130));notice.fontSize=26;notice.gameObject.SetActive(false);
        }
        private static Text Label(Transform parent, string value, Font font, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(value, typeof(RectTransform), typeof(Text)); var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0); rt.pivot = new Vector2(0.5f, 0); rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = go.GetComponent<Text>(); t.font = font; t.fontSize = 28; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.text = value; t.raycastTarget = false; return t;
        }
        private static void ActionButton(Transform parent, string label, Font font, float x, UnityEngine.Events.UnityAction action)
        {
            var t = Label(parent, label, font, new Vector2(x, 40), new Vector2(195, 90)); t.raycastTarget = true;
            var b = t.gameObject.AddComponent<Button>(); b.targetGraphic = t; b.onClick.AddListener(action);
        }
    }
}
