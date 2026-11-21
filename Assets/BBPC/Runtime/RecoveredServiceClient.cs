using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BBPC.Identity;
using BBPC.Network;
using UnityEngine;
using UnityEngine.Networking;
namespace BBPC.UnityRuntime
{
    [Serializable] public sealed class IdentityEnvelope { public int status; public IdentityPayload data; }
    [Serializable] public sealed class IdentityPayload { public string auid, unique_device_id; }
    [Serializable] public sealed class TimeEnvelope { public int status; public TimePayload data; }
    [Serializable] public sealed class TimePayload { public long timestamp; }
    // Concrete Windows adapter. No credentials/identifiers from captures are used here.
    public sealed class RecoveredServiceClient : MonoBehaviour
    {
        public RecoveredIdentity Identity { get; private set; }
        // Validation-only substitution; checked before identity, request construction or transport.
        public Func<string,string,string> OfflinePost;
        public Func<string> OfflineWealthResponse;
        public int OfflinePostCount { get; private set; }
        private RecoveredLocalAccountState account;
        public RecoveredLocalAccountState Account => account ?? (account = new RecoveredLocalAccountState());
        public SongCatalog Catalog { get; private set; }
        public string Status { get; private set; } = "Online not started";
        // Validated against original ARM64 math + independent AOSP default TYPE_3 rand reference.
        // AppConfigManager.Jmo falls back to 0xf29c when no typed jmo config exists.
        private int jmo = RecoveredJms.DefaultJmo;
        public void ApplyVerifiedJmo(int value) { if (Attempted) throw new InvalidOperationException("Configure before network test"); jmo = value; }
        private long serverDelta;
        private long lastResponseAt;
        [Serializable] public sealed class ReadAttempt
        {
            public string route,result,failure,requestId,contentType,bodySha256;
            public RecoveredRequestEvidence request;
            public bool contractAccepted,cacheUsed,cookieSent,setCookieReceived,vipParameterPresent;
            public long http,responseBytes,atUtcMs;public int envelopeStatus=-999,errCode=-999;
        }
        public sealed class ErrorStatus {public int? status,err_code;public long? retry_after;}
        [Serializable] private sealed class ResponseStatus{public int status=0;}
        public readonly List<ReadAttempt> ReadAttempts=new List<ReadAttempt>();
        public bool Finished{get;private set;}
        public bool TimeSynced{get;private set;}
        public long? CurrentServerTimestamp => TimeSynced ? unchecked(Now()+serverDelta) : (long?)null;
        public bool ConfigVerified{get;private set;}
        public bool DynamicConfigVerified{get;private set;}
        public bool OwnRolesVerified{get;private set;}
        public RecoveredConfiguration Configuration{get;private set;}
        public RecoveredConfiguration DynamicConfiguration{get;private set;}
        public bool ProfileVerified{get;private set;}
        public bool WealthVerified{get;private set;}
        public bool AccountRefreshRunning{get;private set;}
        public string CacheError{get;private set;}
        public Action AccountChanged;
        public bool HasPersistedPair{get{EnsureIdentity();return Identity.HasPair;}}
        public bool Attempted { get; private set; }
        public Action Completed;
        // OBSERVED (user LogF.txt 2026-10-30): GET /beatmusic/favorite/list answers
        // CollectionFavListRespModel{items:[StageModel]} and POST /beatmusic/favorite/{add,delete}
        // answers CollectionChangeFavStatusRespModel{sids}. Mirrored locally only on confirmed remote success.
        public string[] ServerFavorites { get; private set; }
        public bool FavoritesVerified { get; private set; }
        public Action FavoritesChanged;
        private void EnsureIdentity()
        {
            if (Identity == null) Identity = new RecoveredIdentity((k,d) => RecoveredPreferences.GetString(k,d), (k,v) => RecoveredPreferences.SetString(k,v ?? ""), RecoveredPreferences.Save);
        }
        private void Awake(){if(account==null)account=new RecoveredLocalAccountState();}
        public static IdentityPayload ParseIdentity(string body)
        {
            var e = JsonUtility.FromJson<IdentityEnvelope>(body);
            if (e == null || e.status != 1 || e.data == null || string.IsNullOrEmpty(e.data.auid) || string.IsNullOrEmpty(e.data.unique_device_id)) throw new FormatException("Invalid identity envelope");
            return e.data;
        }
        // Recovered GetTimeZone uses BaseUtcOffset.Hours and appends .5 only for Minutes == +30.
        public static string FormatOriginalTimeZone(TimeSpan offset) => offset.Hours.ToString(CultureInfo.InvariantCulture) + (offset.Minutes == 30 ? ".5" : "");
        private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private static string Sha(byte[] bytes)
        {using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        private string CookieKey()=>"BBPC.Windows.Cookie606."+Sha(System.Text.Encoding.UTF8.GetBytes(Identity.Auid+"\n"+Identity.Udid));
        private bool ApplyCookie(UnityWebRequest request)
        {
            if(!Identity.HasPair)return false;
            string value=RecoveredPreferences.GetString(CookieKey(),"");
            if(string.IsNullOrEmpty(value))return false;
            if(value.Length>16384||value.IndexOfAny(new[]{'\r','\n'})>=0)throw new FormatException("Invalid own cookie header");
            request.SetRequestHeader("Cookie",value);return true;
        }
        private bool StoreCookie(UnityWebRequest request)
        {
            string value=request.GetResponseHeader("Set-Cookie");
            if(string.IsNullOrEmpty(value)||!Identity.HasPair)return false;
            if(value.Length>16384||value.IndexOfAny(new[]{'\r','\n'})>=0)return false;
            // CookieHelper forwards the received string and persists it with PlayerPrefsSaveManager.
            // Windows storage is scoped to our issued pair; no Android cookie/import path exists.
            RecoveredPreferences.SetString(CookieKey(),value);RecoveredPreferences.Save();return true;
        }
        private string CommonQuery()
        {
            EnsureIdentity(); long local = Now(), corrected = unchecked(local + serverDelta);
            string install=RecoveredIdentity.GetInstallId(()=>RecoveredPreferences.GetString("BBPC.Windows.InstallId",""),value=>{RecoveredPreferences.SetString("BBPC.Windows.InstallId",value);RecoveredPreferences.Save();},local);
            // phonetype is the original backend protocol label (controlled A/B evidence, Oct22).
            // Hardware/device_type/model/OS remain Windows; this does not emulate an Android device.
            var p = new Dictionary<string,string> { ["appid"]="com.happy.game.beat.music.fight.android", ["appname"]="beatmusic", ["v"]="6.0.6", ["osn"]=SystemInfo.operatingSystem, ["osv"]=Environment.OSVersion.Version.ToString(), ["build"]=SystemInfo.operatingSystem, ["model"]=SystemInfo.deviceModel, ["device_type"]=SystemInfo.deviceType.ToString(), ["phonetype"]="android", ["langes"]=CultureInfo.CurrentUICulture.Name, ["resolution"]=Screen.width+"x"+Screen.height, ["install_id"]=install, ["lts"]=local.ToString(CultureInfo.InvariantCulture), ["ts"]=corrected.ToString(CultureInfo.InvariantCulture), ["timezone"]=FormatOriginalTimeZone(TimeZoneInfo.Local.BaseUtcOffset) };
            // Forward demonstrated runtime configuration only when actually supplied. SDK attribution
            // and Android signature fields have no verified Windows source and are never fabricated.
            foreach(var key in new[]{"ab_flag","extra_blood","API_HOST","WEBSOCKET_HOST","fast_ad_disable","waterfall_ad_delay","launch_daily_song_window","configuration_minimum_fetch_interval","dense_enable","dense_default","open_ad_enable","open_ad_min_launch_count","open_ad_min_install_duration","open_ad_min_show_interval","open_ad_min_back_duration","open_ad_max_wait_duration","play_touch_style","free_song_policy","can_buy_pass"})
            {
                object value;if(Configuration!=null&&Configuration.Values.TryGetValue(key,out value)&&(value is string||value is long||value is bool))p[key]=value is bool?((bool)value?"True":"False"):Convert.ToString(value,CultureInfo.InvariantCulture);
            }
            if(WealthVerified)p["vip"]=Account.Wealth.vip.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(Identity.Auid)) p["auid"] = Identity.Auid;
            if (!string.IsNullOrEmpty(Identity.Udid))
            {
                // Captured issued UDID is ASCII. The native UTF conversion helper is not executed by the math oracle.
                // Fail closed for a new non-ASCII identity until that marshalling domain is validated independently.
                foreach (char character in Identity.Udid) if (character > 127) throw new NotSupportedException("UDID outside validated request domain");
                // System.Random supplies only the managed nonce, as in JiaMuSi. Bionic rand is isolated inside RecoveredJms.
                int nonce = new System.Random().Next(0, int.MaxValue);
                p["unique_device_id"] = Identity.Udid; string signature = RecoveredJms.Compute(nonce, Identity.Udid, corrected, jmo);
                if (string.IsNullOrEmpty(signature)) throw new InvalidOperationException("Missing own jms"); p["jms"] = signature;
            }
            var values = new List<string>(); foreach (var pair in p) values.Add(Uri.EscapeDataString(pair.Key)+"="+Uri.EscapeDataString(pair.Value)); return string.Join("&",values);
        }
        public RecoveredRequestEvidence CaptureOwnRequestShape(string route)
        {
            EnsureIdentity();if(!Identity.HasPair||(route!=RecoveredReadContracts.Profile&&route!=RecoveredReadContracts.OwnRoles))throw new InvalidOperationException("Existing own identity and bounded diagnostic route required");
            using(var request=UnityWebRequest.Get("https://api.makeawesomeapps.com"+route+"?"+CommonQuery())){ApplyCookie(request);return RecoveredRequestEvidence.Capture(request);}
        }
        // Explicit observed GET contracts only. No mutation, reporting, payment or WS is enabled here.
        private IEnumerator Get(string route,Action<string> receive,bool cache=false,Dictionary<string,string> parameters=null)
        {
            if(!RecoveredReadContracts.IsAllowed(route))throw new ArgumentException("Route not allowed");
            string query;try{query=CommonQuery();}catch(NotSupportedException){Status="UDID fuera del dominio ASCII validado; petición detenida.";yield break;}catch(ArgumentException){Status="Identidad propia no serializable para JMS";yield break;}
            if(parameters!=null)foreach(var pair in parameters)
            {
                if(!RecoveredReadContracts.AllowsParameter(route,pair.Key))throw new ArgumentException("Unverified route parameter");
                query+="&"+Uri.EscapeDataString(pair.Key)+"="+Uri.EscapeDataString(pair.Value??"");
            }
            string owner=Identity.HasPair?Identity.Auid+"\n"+Identity.Udid:"";
            var store=new RecoveredReadCache(RecoveredStorage.Root);RecoveredReadCache.Entry cached=null;
            Action<string> validate=body=>{CatalogEnvelope.Parse(body);};
            if(cache)
            {
                if(route!=RecoveredReadContracts.Catalog)throw new ArgumentException("ETag policy not recovered for this route");
                try{cached=store.Load(route,owner,validate);}catch(IOException e){CacheError=e.GetType().Name;}catch(UnauthorizedAccessException e){CacheError=e.GetType().Name;}
            }
            using(var request=UnityWebRequest.Get("https://api.makeawesomeapps.com"+route+"?"+query))
            {
                request.timeout=60;request.redirectLimit=0;
                bool cookieSent=ApplyCookie(request);
                if(cached!=null&&!string.IsNullOrEmpty(cached.Etag))request.SetRequestHeader("If-None-Match",cached.Etag);
                if(RecoveredStorage.Isolated)throw new InvalidOperationException("Remote API disabled during isolated offline validation");
                    yield return request.SendWebRequest();lastResponseAt=Now();
                var bytes=request.downloadHandler.data??new byte[0];
                var attempt=new ReadAttempt{route=route,http=request.responseCode,result=request.result.ToString(),requestId=Guid.NewGuid().ToString("N"),atUtcMs=lastResponseAt,contentType=request.GetResponseHeader("Content-Type"),responseBytes=(long)request.downloadedBytes,bodySha256=Sha(bytes),cookieSent=cookieSent,setCookieReceived=StoreCookie(request),vipParameterPresent=query.Contains("&vip=")||query.StartsWith("vip=",StringComparison.Ordinal)};attempt.request=RecoveredRequestEvidence.Capture(request);ReadAttempts.Add(attempt);
                if(bytes.Length>0&&bytes.Length<65536&&attempt.contentType!=null&&attempt.contentType.IndexOf("json",StringComparison.OrdinalIgnoreCase)>=0)
                {try{var error=SongCatalog.Read<ErrorStatus>(System.Text.Encoding.UTF8.GetString(bytes));if(error!=null){attempt.envelopeStatus=error.status??-999;attempt.errCode=error.err_code??-999;}}catch(FormatException){/* Raw error bodies are deliberately not logged. */}}
                string body;
                if(request.responseCode==304&&cache)
                {
                    if(cached==null){attempt.failure="CacheMissingOrInvalid";Status="304 sin cuerpo de caché verificado para esta identidad";yield break;}
                    body=cached.Body;attempt.cacheUsed=true;
                }
                else
                {
                    if(request.result!=UnityWebRequest.Result.Success||request.responseCode!=200){attempt.failure="TransportOrHttp";Status="Solicitud no completada: "+request.result+"; HTTP "+request.responseCode+"; "+bytes.Length+" bytes recibidos";yield break;}
                    if(request.downloadedBytes>16*1024*1024){attempt.failure="ResponseLimit";Status="Respuesta supera límite de lectura local";yield break;}
                    body=request.downloadHandler.text;
                }
                try
                {
                    var envelope=JsonUtility.FromJson<ResponseStatus>(body);if(envelope!=null)attempt.envelopeStatus=envelope.status;
                    if(envelope==null||envelope.status!=1||attempt.errCode!=-999&&attempt.errCode!=0)throw new FormatException("Application envelope rejected");
                    if(cache&&!attempt.cacheUsed)
                    {
                        // Never replace a known-good cache with an app error or incomplete JSON.
                        validate(body);
                        try{store.Store(route,owner,body,request.GetResponseHeader("ETag"),validate);}catch(IOException e){CacheError=e.GetType().Name;}catch(UnauthorizedAccessException e){CacheError=e.GetType().Name;}
                    }
                    receive(body);attempt.contractAccepted=true;
                }
                catch(ArgumentException){attempt.failure="InvalidJson";Status="Respuesta JSON no válida";}
                catch(FormatException){attempt.failure="ContractRejected";Status="Contrato de respuesta rechazado";LogRejected(route,attempt,body);}
            }
        }
        // This is a separate opt-in read: account refresh never means the local store created an account.
        [ContextMenu("Online / read own profile and wealth after verified identity and time")]
        public void StartOwnAccountRefresh()
        {
            EnsureIdentity();if(AccountRefreshRunning||!Identity.HasPair||!TimeSynced){Status="Perfil pendiente: identidad propia y hora verificadas requeridas";return;}
            StartCoroutine(ReadAccount());
        }
        private IEnumerator ReadAccount(WealthRefreshTrigger trigger=WealthRefreshTrigger.Manual)
        {
            AccountRefreshRunning=true;
            try
            {
                bool profileReceived=false,wealthReceived=false;
                // SplashPresenter initializes wealth independently after UDID. A profile HTTP error must not block it.
                yield return Get(RecoveredReadContracts.Wealth,body=>{Account.ApplyServerWealth(body,trigger);WealthVerified=true;wealthReceived=true;AccountChanged?.Invoke();});
                yield return Get(RecoveredReadContracts.Profile,body=>{Account.ApplyServerProfile(body,Identity.Auid);ProfileVerified=true;profileReceived=true;AccountChanged?.Invoke();});
                Status=profileReceived&&wealthReceived?"Perfil y riqueza propios recibidos; compras y progreso remoto sin implementar":profileReceived?"Perfil recibido; riqueza no verificada":wealthReceived?"Riqueza propia recibida; perfil remoto pendiente":"Perfil y riqueza no verificados";
            }
            finally{AccountRefreshRunning=false;}
        }
        public IEnumerator FetchStageInfo(SongMetadata selected,Action<SongItem> receive)
        {
            if(selected==null||string.IsNullOrWhiteSpace(selected.sid))throw new ArgumentException("Select a stage first");
            EnsureIdentity();
            if(!Identity.HasPair||!TimeSynced||Catalog==null){Status="stage/info requiere identidad propia, hora y catálogo remoto verificados";yield break;}
            bool listed=false;foreach(var item in SongCatalog.Enumerate(Catalog.items))if(item.stage.Key==selected.Key){listed=true;break;}
            if(!listed){Status="La variante seleccionada no pertenece al catálogo de esta sesión";yield break;}
            var sid=selected.sid;var aid=selected.aid;
            yield return Get(RecoveredReadContracts.StageInfo,body=>{var item=RecoveredReadContracts.ReadStageInfo(body,sid,aid);RecoveredStageContent.CompleteAssetDescriptor(item,Catalog);foreach(var entry in SongCatalog.Enumerate(Catalog.items))if(entry.stage.Key==item.stage.Key)entry.restriction=item.restriction;new RecoveredReadCache(RecoveredStorage.Root).Store(RecoveredReadContracts.StageInfo+"/"+selected.Key,Identity.Auid+"\n"+Identity.Udid,body,null,value=>RecoveredReadContracts.ReadStageInfo(value,sid,aid));Status="Metadatos stage/info verificados; audio/chart/assets y acceso aún pendientes";receive(item);},false,new Dictionary<string,string>{{"sid",sid},{"aid",aid??""},{"game_mode","free"}});
        }
        // One bounded diagnostic on the already-issued pair. No identity endpoint, mutation or download.
        public IEnumerator ProbeExistingAccount(bool withVerifiedWealth=false)
        {
            EnsureIdentity();if(!Identity.HasPair)throw new InvalidOperationException("Existing own pair required; probe never registers");
            var configuration=RecoveredPreferences.GetString("BBPC.Windows.Config606.Envelope","");
            if(!string.IsNullOrEmpty(configuration)){Configuration=RecoveredConfiguration.Parse(configuration);ConfigVerified=true;int value;if(Configuration.TryInteger("jmo",out value))jmo=value;}
            long before=Now();
            yield return Get(RecoveredReadContracts.Time,body=>{var e=JsonUtility.FromJson<TimeEnvelope>(body);if(e?.data==null||e.data.timestamp<=0)throw new FormatException("Invalid time envelope");serverDelta=RecoveredIdentity.MeasureTimeDelta(before,lastResponseAt,e.data.timestamp);TimeSynced=true;});
            if(!TimeSynced)yield break;
            // The initial diagnostic omitted the independently read wealth source used by the real
            // bootstrap. This opt-in variant tests that dependency without inventing a VIP value.
            if(withVerifiedWealth)yield return ReadAccount();
            else yield return Get(RecoveredReadContracts.Profile,body=>{Account.ApplyServerProfile(body,Identity.Auid);ProfileVerified=true;AccountChanged?.Invoke();});
            yield return Get(RecoveredReadContracts.OwnRoles,body=>{Account.ApplyOwnRoles(body);OwnRolesVerified=true;AccountChanged?.Invoke();});
            if(withVerifiedWealth)yield break;
            var cache=new RecoveredReadCache(RecoveredStorage.Root);
            var entry=cache.Load(RecoveredReadContracts.Catalog,Identity.Auid+"\n"+Identity.Udid,b=>CatalogEnvelope.Parse(b));
            if(entry==null)yield break;Catalog=CatalogEnvelope.Parse(entry.Body);
            int read=0;
            foreach(var song in SongCatalog.Enumerate(Catalog.items))
            {
                if(song.restriction?.unlock_status!=true)continue;
                if(++read>3)break;var selected=song.stage;
                yield return Get(RecoveredReadContracts.StageInfo,body=>
                {
                    RecoveredReadContracts.ReadStageInfo(body,selected.sid,selected.aid);
                    cache.Store(RecoveredReadContracts.StageInfo+"/"+selected.Key,Identity.Auid+"\n"+Identity.Udid,body,null,b=>RecoveredReadContracts.ReadStageInfo(b,selected.sid,selected.aid));
                },false,new Dictionary<string,string>{{"sid",selected.sid},{"aid",selected.aid??""},{"game_mode","free"}});
            }
        }
        public IRecoveredRewardedAdProvider RewardedAds {get;set;} // Legacy integration seam; not required by the measured PC request contract.
        private readonly HashSet<string> unlockInFlight=new HashSet<string>();
        public IEnumerator RefreshOwnCatalog()
        {
            EnsureIdentity();if(!Identity.HasPair||!TimeSynced)throw new InvalidOperationException("Existing identity/time required");
            yield return Get(RecoveredReadContracts.Catalog,body=>Catalog=CatalogEnvelope.Parse(body),true);
        }
        // Only an explicit Play request calls this method. Never called by preview, scroll or cell refresh.
        public IEnumerator EnsureStageAccess(SongMetadata selected,Action<SongItem> receive)
        {
            if(selected==null||receive==null)throw new ArgumentNullException("Selected stage/callback required");
            EnsureIdentity();if(!Identity.HasPair||!TimeSynced||Catalog==null){Status="Acceso remoto pendiente: identidad propia, hora y catálogo requeridos";yield break;}
            SongItem listed=null;foreach(var item in SongCatalog.Enumerate(Catalog.items))if(item.stage.Key==selected.Key){listed=item;break;}
            if(listed==null){Status="Acceso remoto: variante ajena al catálogo actual";yield break;}
            if(!unlockInFlight.Add(selected.sid)){Status="Solicitud de acceso de este SID en curso";yield break;}
            try
            {
                // Reconcile stale catalog restrictions with a live server read before any POST.
                SongItem current=null;yield return FetchStageInfo(selected,value=>current=value);
                if(current==null){Status="Solicitud HTTP/contrato de acceso fallida: "+Status;yield break;}
                if(current.restriction?.unlock_status==true){receive(current);yield break;}
                if(!RecoveredStageUnlock.MayRequest(current.restriction)){Status="Acceso remoto bloqueado: compra u otro requisito; no se convierte en anuncio";yield break;}
                RecoveredStageUnlockData confirmation=null;
                // User-controlled Android experiment confirmed this exact request without video.
                // PC still requires a real matching server grant; no SDK callback or local grant is fabricated.
                using(var request=new UnityWebRequest("https://api.makeawesomeapps.com"+RecoveredStageUnlock.Route+"?"+CommonQuery(),"POST"))
                {
                    request.uploadHandler=new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(RecoveredStageUnlock.Body(selected.sid)));request.downloadHandler=new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type","application/json");bool cookie=ApplyCookie(request);request.timeout=15;request.redirectLimit=0;
                    if(RecoveredStorage.Isolated)throw new InvalidOperationException("Remote API disabled during isolated offline validation");
                    yield return request.SendWebRequest();lastResponseAt=Now();
                    var bytes=request.downloadHandler.data??new byte[0];
                    var attempt=new ReadAttempt{route=RecoveredStageUnlock.Route,http=request.responseCode,result=request.result.ToString(),requestId=Guid.NewGuid().ToString("N"),atUtcMs=lastResponseAt,contentType=request.GetResponseHeader("Content-Type"),responseBytes=bytes.Length,bodySha256=Sha(bytes),cookieSent=cookie,setCookieReceived=StoreCookie(request),request=RecoveredRequestEvidence.Capture(request)};
                    ReadAttempts.Add(attempt);
                    if(request.result==UnityWebRequest.Result.Success&&bytes.Length<65536)
                    {try{confirmation=RecoveredStageUnlock.Confirm(request.responseCode,request.downloadHandler.text,selected.sid);attempt.contractAccepted=true;attempt.envelopeStatus=1;}catch(FormatException){attempt.failure="ContractRejected";}}
                    else attempt.failure="TransportOrHttp";
                    if(confirmation==null){Status="Solicitud de desbloqueo no confirmada: HTTP "+request.responseCode+"; no se concede acceso local";yield break;}
                }
                RecoveredStageUnlock.Reconcile(Catalog,confirmation);
                SongItem verified=null;yield return FetchStageInfo(selected,value=>verified=value);
                if(verified?.restriction?.unlock_status!=true){Status="POST aceptado, pero stage/info no confirmó acceso";yield break;}
                yield return RefreshOwnCatalog();
                RecoveredStageUnlock.Reconcile(Catalog,confirmation);Status="Acceso confirmado por servidor; comprobando contenido Windows";receive(verified);
            }
            finally{unlockInFlight.Remove(selected.sid);}
        }
        public bool BfChangeRunning{get;private set;}
        public RecoveredBfOperation PendingBfOperation{get;private set;}
        private readonly HashSet<string> sentBfOperations=new HashSet<string>();
        // Caller must request this specific change. There is no automatic equip, reward or retry path.
        // Generalized from the captured BF body: the same {"rid","decors":{}} shape is sent under the
        // JSON key that matches the profile flag (bf / gf / revive). Only the flag value and the
        // ownership check differ; the request itself is byte-for-byte the captured BF template.
        public IEnumerator ChangeRoleEquip(RecoveredBfOperation operation,Action<bool> receive)
        {
            EnsureIdentity();
            if(operation==null||receive==null)throw new ArgumentNullException("Role operation/callback required");
            if(BfChangeRunning||!Identity.HasPair||!TimeSynced||!ProfileVerified||!OwnRolesVerified)throw new InvalidOperationException("Verified current own profile and roles required");
            if(!OwnsRole(operation))throw new InvalidOperationException("Role ownership not confirmed");
            if(!sentBfOperations.Add(operation.ClientId))throw new InvalidOperationException("Operation already sent; uncertain results require explicit reconciliation");
            PendingBfOperation=operation;BfChangeRunning=true;bool accepted=false;
            try
            {
                using(var request=new UnityWebRequest("https://api.makeawesomeapps.com"+RecoveredBfOperation.Route+"?"+CommonQuery(),"POST"))
                {
                    request.uploadHandler=new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(operation.Json));request.downloadHandler=new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type","application/json");ApplyCookie(request);request.timeout=15;request.redirectLimit=0;
                    if(RecoveredStorage.Isolated)throw new InvalidOperationException("Remote API disabled during isolated offline validation");
                    yield return request.SendWebRequest();StoreCookie(request);
                    if(request.result==UnityWebRequest.Result.Success&&request.responseCode==200&&request.downloadedBytes<1024*1024)
                    {try{accepted=RecoveredBfOperation.Succeeded(request.downloadHandler.text);}catch(FormatException){Status="Respuesta de cambio de role no valida; no se equipa localmente";}}
                }
                if(accepted)
                {
                    ProfileVerified=false;OwnRolesVerified=false;
                    yield return Get(RecoveredReadContracts.Profile,body=>{Account.ApplyServerProfile(body,Identity.Auid);ProfileVerified=true;AccountChanged?.Invoke();});
                    yield return Get(RecoveredReadContracts.OwnRoles,body=>{Account.ApplyOwnRoles(body);OwnRolesVerified=true;AccountChanged?.Invoke();});
                    if(ProfileVerified&&OwnRolesVerified&&SelectedRole(operation.Flag)==operation.Rid)PendingBfOperation=null;
                }
                receive(accepted);
            }
            finally{BfChangeRunning=false;}
        }
        public bool OwnsRole(RecoveredBfOperation operation)
        {
            if(operation==null)return false;
            var roles=Account.ServerRoles;if(roles==null)return false;
            switch(operation.Flag)
            {
                case RecoveredProfileFlag.BF:
                    return Array.IndexOf(Account.Roles.own_bfs,operation.Rid)>=0||roles.default_bf?.rid==operation.Rid;
                case RecoveredProfileFlag.GF:
                    return Array.IndexOf(Account.Roles.own_gfs,operation.Rid)>=0||roles.default_gf?.rid==operation.Rid;
                case RecoveredProfileFlag.Revive:
                    return Array.IndexOf(Account.Roles.own_revives,operation.Rid)>=0||roles.default_revive?.rid==operation.Rid;
            }
            return false;
        }
        public string SelectedRole(RecoveredProfileFlag flag)
        {
            switch(flag)
            {
                case RecoveredProfileFlag.BF:return Account.Profile.bf;
                case RecoveredProfileFlag.GF:return Account.Profile.gf;
                case RecoveredProfileFlag.Revive:return Account.Profile.revive;
            }
            return null;
        }
        public bool OwnRolesRefreshRunning{get;private set;}
        public IEnumerator RefreshOwnRoles(Action<bool> receive)
        {
            EnsureIdentity();
            if(receive==null)throw new ArgumentNullException("callback required");
            if(OwnRolesRefreshRunning){receive(OwnRolesVerified);yield break;}
            if(!Identity.HasPair||!TimeSynced){Status="Roles requieren identidad propia y hora verificadas";receive(false);yield break;}
            if(RecoveredStorage.Isolated){Status="API remota desactivada en validacion aislada";receive(false);yield break;}
            OwnRolesRefreshRunning=true;bool ok=false;
            try
            {
                yield return Get(RecoveredReadContracts.OwnRoles,body=>{try{Account.ApplyOwnRoles(body);OwnRolesVerified=true;AccountChanged?.Invoke();}catch(FormatException e){Status="Roles rejected: "+e.Message;OwnRolesVerified=false;}});
                ok=OwnRolesVerified;
            }
            finally{OwnRolesRefreshRunning=false;}
            receive(ok);
        }
        [ContextMenu("Online / request own test identity once (no captured IDs)")]
        public void StartOwnOnlineTest() { if (!Attempted) { Attempted = true; StartCoroutine(RunOnce()); } }
        // OBSERVED (user LogF.txt): GET /beatmusic/favorite/list answers CollectionFavListRespModel{items:[StageModel]}.
        // Best-effort read after identity/time/catalog; it never creates or modifies a remote list.
        public IEnumerator FetchFavoriteList(Action<string[]> receive)
        {
            EnsureIdentity();
            if (!Identity.HasPair || !TimeSynced) { Status = "Favoritos requieren identidad propia y hora verificadas"; yield break; }
            string[] sids = null;
            yield return Get(RecoveredReadContracts.FavoriteList, body => { sids = RecoveredReadContracts.ReadFavoriteList(body); ServerFavorites = sids; FavoritesVerified = true; FavoritesChanged?.Invoke(); });
            if (sids != null && receive != null) receive(sids);
        }
        // Best-effort read of an observed GET route that has no account side effect.
        public IEnumerator FetchObserved(string route, Action<string> receive)
        {
            EnsureIdentity();
            if (!Identity.HasPair || !TimeSynced) { Status = "Lectura remota: identidad propia y hora verificadas requeridas"; yield break; }
            yield return Get(route, receive);
        }
        public IEnumerator FetchLeaderboard(string route, Action<RecoveredLeaderboardPayload> receive)
        {
            string body = null;
            yield return FetchObserved(route, value => body = value);
            if (body == null) yield break;
            try { receive(RecoveredReadContracts.ReadLeaderboard(body)); }
            catch (FormatException e) { Status = "Clasificacion rechazada: " + e.Message; }
        }
        public IEnumerator FetchLeaderboardRules(string id,Action<string> receive)
        {
            if(string.IsNullOrEmpty(id))yield break;
            EnsureIdentity();
            if(!Identity.HasPair||!TimeSynced){Status="Reglas requieren identidad propia y hora verificadas";yield break;}
            string body=null;
            yield return Get(RecoveredReadContracts.LeaderboardRules,value=>body=value,false,new Dictionary<string,string>{{"id",id}});
            if(body==null)yield break;
            string content;
            try{content=RecoveredReadContracts.ReadLeaderboardRules(body);}
            catch(FormatException e){Status="Reglas rechazadas: "+e.Message;yield break;}
            receive?.Invoke(content);
        }
        public IEnumerator FetchBattlePass(Action<RecoveredBattlePassData> receive)
        {
            string body=null;
            yield return FetchObserved(RecoveredReadContracts.TaskBattle,value=>body=value);
            if(body==null)yield break;
            try { receive(RecoveredReadContracts.ReadBattlePass(body)); }
            catch(FormatException e) { Status="Pase rechazado: "+e.Message; }
        }
        public IEnumerator FetchPeriodicTasks(Action<RecoveredTaskPeriodList> receive)
        {
            string body=null;
            yield return FetchObserved(RecoveredReadContracts.TaskPeriodList,value=>body=value);
            if(body==null)yield break;
            try { receive(RecoveredReadContracts.ReadTaskPeriodList(body)); }
            catch(FormatException e) { Status="Tareas periódicas rechazadas: "+e.Message; }
        }
        public IEnumerator FetchSignInTasks(Action<RecoveredTaskPeriodItem[]> receive)
        {
            if(receive==null)throw new ArgumentNullException(nameof(receive));
            RecoveredTaskPeriodList period=null;
            yield return FetchPeriodicTasks(value=>period=value);
            if(period!=null)receive(period.signin.tasks);
        }
        public IEnumerator FetchLegacySignInTasks(Action<RecoveredSignInTask[]> receive)
        {
            if(receive==null)throw new ArgumentNullException(nameof(receive));
            string body=null;
            yield return FetchObserved(RecoveredReadContracts.SignIn,value=>body=value);
            if(body==null)yield break;
            try { receive(RecoveredReadContracts.ReadLegacySignInTasks(body)); }
            catch(FormatException e) { Status="Asistencia clásica rechazada: "+e.Message; }
        }
        public IEnumerator FetchFreeStage(Action<SongMetadata> receive)
        {
            string body = null;
            yield return FetchObserved(RecoveredReadContracts.StageFree, value => body = value);
            if (body == null) yield break;
            try { receive(RecoveredReadContracts.ReadStageFree(body)); }
            catch (FormatException e) { Status = "Etapa gratuita rechazada: " + e.Message; }
        }
        // OBSERVED (user LogF.txt): POST /beatmusic/favorite/{add,delete} body
        // {"client_id":"<GuidN>","sids":["<sid>"]}; the server answers {"status":1,"data":{"sids":[...]}}.
        // The local mirror is only updated after the SID is confirmed present in the remote answer.
        // A failure never withdraws a local favorite: the caller reconciles the local chip separately.
        public IEnumerator SetFavorite(string sid, bool add, Action<bool> receive)
        {
            if (receive == null) throw new ArgumentNullException(nameof(receive));
            if (string.IsNullOrEmpty(sid)) throw new ArgumentException("Favorite SID required");
            EnsureIdentity();
            if (!Identity.HasPair || !TimeSynced) { Status = "Favoritos requieren identidad propia y hora verificadas"; receive(false); yield break; }
            string route = add ? RecoveredReadContracts.FavoriteAdd : RecoveredReadContracts.FavoriteDelete;
            string[] confirmed = null;
            yield return PostJson(route, RecoveredReadContracts.FavoriteBody(sid), body => confirmed = RecoveredReadContracts.ReadFavoriteChange(body));
            if (confirmed == null || Array.IndexOf(confirmed, sid) < 0) { Status = "Favorito no confirmado por el servidor"; receive(false); yield break; }
            var set = new HashSet<string>(ServerFavorites ?? new string[0]);
            if (add) set.Add(sid); else set.Remove(sid);
            ServerFavorites = new List<string>(set).ToArray(); FavoritesVerified = true; FavoritesChanged?.Invoke();
            receive(true);
        }
        public void LogResultEvent(string route,string outcome,int? gain=null,int? balance=null)
        {
            if(route!=RecoveredReadContracts.ResultReport&&route!=RecoveredReadContracts.ResultReward)return;
            var line=DateTimeOffset.UtcNow.ToString("o",CultureInfo.InvariantCulture)+" route="+route+" outcome="+outcome+
                (gain.HasValue?" gain_diamond="+gain.Value.ToString(CultureInfo.InvariantCulture):"")+
                (balance.HasValue?" balance="+balance.Value.ToString(CultureInfo.InvariantCulture):"");
            Debug.Log("BBPC result: "+line);
            try{File.AppendAllText(Path.Combine(RecoveredStorage.Root,"result-network.log"),line+Environment.NewLine);}
            catch(IOException){Debug.LogWarning("BBPC result log write failed: IO");}
            catch(UnauthorizedAccessException){Debug.LogWarning("BBPC result log write failed: access");}
        }
        // Shared allowlisted JSON transport. Never logs raw responses or identity.
        private IEnumerator PostJson(string route, string body, Action<string> receive, string reportQueryValue = null)
        {
            if (!RecoveredReadContracts.IsAllowedWrite(route)) throw new ArgumentException("Route not allowed");
            if (OfflinePost != null)
            {
                if (!RecoveredStorage.Isolated) throw new InvalidOperationException("Fake transport requires isolated storage");
                OfflinePostCount++;
                receive(OfflinePost(route,body));yield break;
            }
            string query; try { query = CommonQuery(); } catch (NotSupportedException) { Status = "UDID fuera del dominio ASCII validado; petición detenida."; yield break; } catch (ArgumentException) { Status = "Identidad propia no serializable para JMS"; yield break; }
            if (reportQueryValue != null)
            {
                if (route != RecoveredReadContracts.ResultReport) throw new ArgumentException("RJM is report-only");
                query += "&rjm=" + Uri.EscapeDataString(reportQueryValue);
            }
            using (var request = new UnityWebRequest("https://api.makeawesomeapps.com" + route + "?" + query, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body)); request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json"); bool cookieSent = ApplyCookie(request); request.timeout = 15; request.redirectLimit = 0;
                if (RecoveredStorage.Isolated) throw new InvalidOperationException("Remote API disabled during isolated offline validation");
                yield return request.SendWebRequest(); lastResponseAt = Now();
                var bytes = request.downloadHandler.data ?? new byte[0];
                var attempt = new ReadAttempt { route = route, http = request.responseCode, result = request.result.ToString(), requestId = Guid.NewGuid().ToString("N"), atUtcMs = lastResponseAt, contentType = request.GetResponseHeader("Content-Type"), responseBytes = (long)request.downloadedBytes, bodySha256 = Sha(bytes), cookieSent = cookieSent, setCookieReceived = StoreCookie(request), request = RecoveredRequestEvidence.Capture(request) };
                ReadAttempts.Add(attempt);
                LogResultEvent(route,"http:"+request.responseCode+" transport:"+request.result+" bytes:"+bytes.Length);
                if (bytes.Length > 0 && bytes.Length < 65536 && attempt.contentType != null && attempt.contentType.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0)
                { try { var error = SongCatalog.Read<ErrorStatus>(System.Text.Encoding.UTF8.GetString(bytes)); if (error != null) { attempt.envelopeStatus = error.status ?? -999; attempt.errCode = error.err_code ?? -999; } } catch (FormatException) { /* Raw error bodies are deliberately not logged. */ } }
                LogResultEvent(route,"status:"+(attempt.envelopeStatus==-999?"absent":attempt.envelopeStatus.ToString(CultureInfo.InvariantCulture))+" err_code:"+(attempt.errCode==-999?"absent":attempt.errCode.ToString(CultureInfo.InvariantCulture)));
                if (request.result != UnityWebRequest.Result.Success || request.responseCode != 200) { attempt.failure = "TransportOrHttp"; Status = "Solicitud no completada; HTTP " + request.responseCode; yield break; }
                if (bytes.Length > 65536) { attempt.failure = "ResponseLimit"; Status = "Respuesta supera límite de lectura local"; yield break; }
                string text = request.downloadHandler.text;
                try
                {
                    var envelope = JsonUtility.FromJson<ResponseStatus>(text); if (envelope != null) attempt.envelopeStatus = envelope.status;
                    if (envelope == null || envelope.status != 1 || attempt.errCode != -999 && attempt.errCode != 0) throw new FormatException("Application envelope rejected");
                    receive(text); attempt.contractAccepted = true;
                }
                catch (ArgumentException) { attempt.failure = "InvalidJson"; Status = "Respuesta JSON no válida"; }
                catch (FormatException) { attempt.failure = "ContractRejected"; Status = "Contrato de respuesta rechazado"; LogRejected(route, attempt, text); }
            }
        }
        // Diagnostic: when the app envelope is rejected, say why (status / err_code / start of the body) in Player.log and BBPC.Logs.
        private static void LogRejected(string route, ReadAttempt attempt, string text)
        {
            string head = string.IsNullOrEmpty(text) ? "" : (text.Length > 300 ? text.Substring(0, 300) : text);
            Debug.LogWarning("[BBPC.Net] " + route + " rechazado status=" + attempt.envelopeStatus + " err_code=" + attempt.errCode + " http=" + attempt.http + " body=" + head);
        }
        // OBSERVED (user evidence 2026-10-30): rsids are computed client-side from stage/list.
        // Evidence: "Beat Battle 7.7.7 — rsids y etiqueta hot (guía para el port a Unity PC).md"
        // and SongRecommender.cs with two real cases validated in Python.
        public RecoveredRecommendation LastRecommendation { get; private set; }
        public RecoveredRecommendation BuildOwnRecommendation(string playingSid, string playingAid)
        {
            LastRecommendation = Catalog == null ? null : RecoveredSongRecommender.Build(Catalog.items, playingSid, playingAid, RecoveredRecommendPlayMode.Stage);
            return LastRecommendation;
        }
        // OBSERVED (user LogReport.txt 2026-10-30): POST /beatmusic/result/report answers
        // {new_record,gain_diamond,point,multiple_factor,rewards[],multiple_factors[]}.
        // Player-initiated only. Gated on own identity, time and profile; a finished stage result is required.
        // Isolation mode throws the standard message from PostJson; no success is ever fabricated locally.
        public string LastReportClientId { get; private set; }
        public string LastReportSid { get; private set; }
        public string LastReportAid { get; private set; }
        public string BuildOwnReportBody(RecoveredStageResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (string.IsNullOrEmpty(result.sid)) throw new FormatException("Stage result without sid cannot be reported");
            // RoundID belongs to preparation, not serialization; repeat serialization preserves it.
            if (!Guid.TryParseExact(result.roundId, "D", out var round)) throw new FormatException("Missing prepared RoundID");
            string[] rsids = LastRecommendation?.Rsids.ToArray() ?? new string[0];
            string clientId = round.ToString("D");
            LastReportClientId = clientId; LastReportSid = result.sid; LastReportAid = result.aid;
            // OBSERVED (user LogReport.txt vs LogAll.txt 2026-10-31): the default role placeholders are
            // __DEFAULT_BF__/__DEFAULT_GF__/__DEFAULT_REVIVES__ when nothing is equipped, and the report
            // body emits them verbatim. extra_blood is rebuilt from the paid entitlements in Wealth.
            string defBf = "__DEFAULT_BF__", defGf = "__DEFAULT_GF__", defRev = "__DEFAULT_REVIVES__", defDef = "__DEFAULT__";
            return RecoveredReadContracts.ReportBody(new RecoveredReportInput
            {
                client_id = clientId,
                sid = result.sid, aid = result.aid, role = result.playType == 1 ? "Bf" : "Normal",
                progress = result.progress, score = result.score, perfect = result.perfect,
                great = result.great, good = result.good, miss = result.miss,
                blood = result.blood, full_score = result.maxScore, c_count = result.clickCount,
                has_full_combo = result.allCombo, game_mode = "free",
                bf = string.IsNullOrEmpty(Account.Profile.bf) ? defBf : Account.Profile.bf,
                gf = string.IsNullOrEmpty(Account.Profile.gf) ? defGf : Account.Profile.gf,
                revive = string.IsNullOrEmpty(Account.Profile.revive) ? defRev : Account.Profile.revive,
                arrow = string.IsNullOrEmpty(Account.Profile.arrow) ? defDef : Account.Profile.arrow,
                click_effect = string.IsNullOrEmpty(Account.Profile.click_effect) ? defDef : Account.Profile.click_effect,
                actual_bf = string.IsNullOrEmpty(Account.Profile.bf) ? defBf : Account.Profile.bf,
                actual_gf = string.IsNullOrEmpty(Account.Profile.gf) ? defGf : Account.Profile.gf,
                rsids = rsids,
                wealth = Account.Wealth
            });
        }
        public IEnumerator ReportOwnResult(RecoveredStageResult result, Action<RecoveredReportResultData> receive)
        {
            if (receive == null) throw new ArgumentNullException(nameof(receive));
            if (result == null) throw new ArgumentNullException(nameof(result));
            bool offline=RecoveredStorage.Isolated&&OfflinePost!=null;
            EnsureIdentity();
            if (!offline&&(!Identity.HasPair || !TimeSynced || !ProfileVerified)) { Status = "Report requiere identidad propia, hora y perfil verificados"; receive(null); yield break; }
            string body = null;
            if (RecoveredStorage.Isolated&&!offline) { Status = "API remota desactivada en validación aislada"; receive(null); yield break; }
            string payload = BuildOwnReportBody(result);
            string rjm = offline?null:RecoveredResult606.ReportQueryValue(result.score, Identity.Udid, Identity.Auid, LastReportClientId, result.sid);
            yield return PostJson(RecoveredReadContracts.ResultReport, payload, value => body = value, rjm);
            if (body == null) { receive(null); yield break; }
            try { var answer=RecoveredReadContracts.ReadReportResult(body);LogResultEvent(RecoveredReadContracts.ResultReport,"accepted",answer.gain_diamond);receive(answer); }
            catch (FormatException e) { Status = "Report rechazado: " + e.Message; receive(null); }
        }
        // OBSERVED (user LogReport.txt 2026-10-30): POST /beatmusic/result/reward reuses the
        // report client_id and carries factor_index. Player-initiated, no ad mediation.
        public IEnumerator ClaimReward(int factorIndex, Action<RecoveredRewardResultData> receive)
        {
            if (receive == null) throw new ArgumentNullException(nameof(receive));
            if (string.IsNullOrEmpty(LastReportClientId)) throw new InvalidOperationException("Reward requires a prior report with client_id");
            EnsureIdentity();
            bool offline=RecoveredStorage.Isolated&&OfflinePost!=null;
            if (!offline&&(!Identity.HasPair || !TimeSynced || !ProfileVerified)) { Status = "Recompensa requiere identidad propia, hora y perfil verificados"; receive(null); yield break; }
            string body = null;
            if (RecoveredStorage.Isolated&&!offline) { Status = "API remota desactivada en validación aislada"; receive(null); yield break; }
            yield return PostJson(RecoveredReadContracts.ResultReward, RecoveredReadContracts.RewardBody(LastReportClientId, LastReportSid, LastReportAid, factorIndex), value => body = value);
            if (body == null) { receive(null); yield break; }
            try { var answer=RecoveredReadContracts.ReadRewardResult(body);LogResultEvent(RecoveredReadContracts.ResultReward,"accepted",answer.gain_diamond);receive(answer); }
            catch (FormatException e) { Status = "Recompensa rechazada: " + e.Message; receive(null); }
        }
        // OBSERVED (user LogAll.txt 2026-10-31): POST /beatmusic/task/period/reward.
        // Player-initiated only: sign-in tasks (type "1") and daily/battle-pass tasks (type "2").
        // A reward is never granted locally; the server answer is the only source of the gained diamonds.
        public string LastTaskRewardClientId { get; private set; }
        public string LastTaskRewardTid { get; private set; }
        public string LastTaskRewardType { get; private set; }
        public System.Collections.IEnumerator ClaimTaskReward(string tid, string type, Action<RecoveredTaskRewardResultData> receive)
        {
            if (receive == null) throw new ArgumentNullException(nameof(receive));
            if (string.IsNullOrEmpty(tid)) throw new ArgumentNullException(nameof(tid));
            if (string.IsNullOrEmpty(type)) throw new ArgumentNullException(nameof(type));
            EnsureIdentity();
            bool offline=RecoveredStorage.Isolated&&OfflinePost!=null;
            if (!offline&&(!Identity.HasPair || !TimeSynced)) { Status = "Recompensa de tarea requiere identidad propia y hora verificadas"; receive(null); yield break; }
            if (RecoveredStorage.Isolated&&!offline) { Status = "API remota desactivada en validación aislada"; receive(null); yield break; }
            LastTaskRewardClientId = RecoveredReadContracts.NewClientId();
            LastTaskRewardTid = tid; LastTaskRewardType = type;
            string body = null;
            yield return PostJson(RecoveredReadContracts.TaskPeriodReward, RecoveredReadContracts.TaskRewardBody(LastTaskRewardClientId, tid, type), value => body = value);
            if (body == null) { receive(null); yield break; }
            try { receive(RecoveredReadContracts.ReadTaskRewardResult(body)); }
            catch (FormatException e) { Status = "Recompensa de tarea rechazada: " + e.Message; receive(null); }
        }
        public IEnumerator ClaimBattlePassReward(string bid,string tid,int type,Action<RecoveredTaskRewardResultData> receive)
        {
            if(receive==null)throw new ArgumentNullException(nameof(receive));
            string payload=RecoveredReadContracts.BattlePassRewardBody(RecoveredReadContracts.NewClientId(),bid,tid,type);
            EnsureIdentity();
            bool offline=RecoveredStorage.Isolated&&OfflinePost!=null;
            if(!offline&&(!Identity.HasPair||!TimeSynced)){Status="Recompensa de pase requiere identidad propia y hora verificadas";receive(null);yield break;}
            if(RecoveredStorage.Isolated&&!offline){receive(null);yield break;}
            string body=null;
            yield return PostJson(RecoveredReadContracts.BattlePassReward,payload,value=>body=value);
            if(body==null){receive(null);yield break;}
            try{receive(RecoveredReadContracts.ReadBattlePassRewardResult(body));}
            catch(FormatException e){Status="Recompensa de pase rechazada: "+e.Message;receive(null);}
        }
        // OBSERVED (user LogReport.txt 2026-10-30): POST /beatmusic/shop/goods/purchase body
        // {"client_id":"<32hex>","type":2,"id":"<rid>","rids":["<rid>"],"diamond":7800}.
        public IEnumerator PurchaseGoods(int type, string id, string[] rids, int diamond, Action<RecoveredPurchaseResultData> receive)
        {
            if (receive == null) throw new ArgumentNullException(nameof(receive));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            EnsureIdentity();
            if (!Identity.HasPair || !TimeSynced || !ProfileVerified) { Status = "Compra requiere identidad propia, hora y perfil verificados"; receive(null); yield break; }
            string body = null;
            if (RecoveredStorage.Isolated) { Status = "API remota desactivada en validación aislada"; receive(null); yield break; }
            yield return PostJson(RecoveredReadContracts.ShopGoodsPurchase, RecoveredReadContracts.PurchaseBody(RecoveredReadContracts.NewClientId(), type, id, rids, diamond), value => body = value);
            if (body == null) { receive(null); yield break; }
            try { receive(RecoveredReadContracts.ReadPurchaseResult(body)); }
            catch (FormatException e) { Status = "Compra rechazada: " + e.Message; receive(null); }
        }
        // OBSERVED (user LogShopown.txt 2026-10-31): POST /beatmusic/shop/goods/lottery.
// Player-initiated only. A spin spends the diamond guess; the answer is the only source of the won role.
        public string LastLotteryClientId { get; private set; }
        public string LastLotteryId { get; private set; }
        public int LastLotteryGuess { get; private set; }
        public int LastLotteryPtype { get; private set; }
        public System.Collections.IEnumerator SpinLottery(string id, int guess, int ptype, Action<RecoveredLotteryResultData> receive)
        {
            if (receive == null) throw new ArgumentNullException(nameof(receive));
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            EnsureIdentity();
            if (!Identity.HasPair || !TimeSynced || !ProfileVerified) { Status = "Ruleta requiere identidad propia, hora y perfil verificados"; receive(null); yield break; }
            if (RecoveredStorage.Isolated) { Status = "API remota desactivada en validación aislada"; receive(null); yield break; }
            LastLotteryClientId = RecoveredReadContracts.NewClientId();
            LastLotteryId = id; LastLotteryGuess = guess; LastLotteryPtype = ptype;
            string body = null;
            yield return PostJson(RecoveredReadContracts.ShopGoodsLottery, RecoveredReadContracts.LotteryBody(LastLotteryClientId, id, guess, ptype), value => body = value);
            if (body == null) { receive(null); yield break; }
            try { receive(RecoveredReadContracts.ReadLotteryResult(body)); }
            catch (FormatException e) { Status = "Ruleta rechazada: " + e.Message; receive(null); }
        }
        // OBSERVED (user own shop response 6.0.6): GET /beatmusic/shop/goods answers
        // {banners,bf_store.role_goods_list,diamond_zone,vs_legacy_zone,...}.
        public IEnumerator FetchShopGoods(Action<RecoveredShopPayload> receive)
        {
            if (receive == null) throw new ArgumentNullException(nameof(receive));
            EnsureIdentity();
            if (!Identity.HasPair || !TimeSynced) { Status = "Tienda requiere identidad propia y hora verificadas"; receive(null); yield break; }
            string body = null;
            if (RecoveredStorage.Isolated) { Status = "API remota desactivada en validación aislada"; receive(null); yield break; }
            yield return Get(RecoveredReadContracts.ShopGoods, value => body = value);
            if (body == null) { receive(null); yield break; }
            try { receive(RecoveredReadContracts.ReadShopPayload(body)); }
            catch (FormatException e) { Status = "Tienda rechazada: " + e.Message; receive(null); }
        }
        // Player-initiated. OBSERVED live: POST shop/goods/discount {"client_id"} answers the active BF-store discount.
        public IEnumerator RequestShopDiscount(Action<RecoveredShopDiscount> receive)
        {
            if (receive == null) throw new ArgumentNullException(nameof(receive));
            EnsureIdentity();
            if (!Identity.HasPair || !TimeSynced || !ProfileVerified) { Status = "Descuento requiere identidad propia, hora y perfil verificados"; receive(null); yield break; }
            if (RecoveredStorage.Isolated) { Status = "API remota desactivada en validación aislada"; receive(null); yield break; }
            string body = null;
            yield return PostJson(RecoveredReadContracts.ShopGoodsDiscount, RecoveredReadContracts.DiscountBody(RecoveredReadContracts.NewClientId()), value => body = value);
            if (body == null) { receive(null); yield break; }
            try { receive(RecoveredReadContracts.ReadDiscountResult(body)); }
            catch (FormatException e) { Status = "Descuento rechazado: " + e.Message; receive(null); }
        }
        // Pin/hide a role in the own list (CustomBfPanel). OBSERVED: body {"action":1,"state":S,"ids":[rid]}, answer {}.
        // The own roles are re-read afterwards: arrange_state is only trusted from role/own.
        public IEnumerator ArrangeRole(string rid, int state, Action<bool> receive)
        {
            if (receive == null) throw new ArgumentNullException(nameof(receive));
            EnsureIdentity();
            if (!Identity.HasPair || !TimeSynced || !ProfileVerified || !OwnRolesVerified) { Status = "Fijar/ocultar requiere perfil y roles verificados"; receive(false); yield break; }
            if (RecoveredStorage.Isolated) { Status = "API remota desactivada en validación aislada"; receive(false); yield break; }
            string body = null;
            yield return PostJson(RecoveredReadContracts.RoleAction, RecoveredReadContracts.ArrangeBody(state, rid), value => body = value);
            if (body == null) { receive(false); yield break; }
            yield return RefreshOwnRoles(value => { });
            receive(true);
        }
        public IEnumerator RefreshWealth(WealthRefreshTrigger trigger, Action<RecoveredWealthData> receive)
        {
            if (receive == null) throw new ArgumentNullException(nameof(receive));
            string body = null;
            if(OfflineWealthResponse!=null)
            {
                if(!RecoveredStorage.Isolated)throw new InvalidOperationException("Fake wealth requires isolated storage");
                body=OfflineWealthResponse();
            }
            else
            {
                EnsureIdentity();
                if (!Identity.HasPair || !TimeSynced) { Status = "Riqueza requiere identidad propia y hora verificadas"; receive(null); yield break; }
                if (RecoveredStorage.Isolated) { Status = "API remota desactivada en validación aislada"; receive(null); yield break; }
                yield return Get(RecoveredReadContracts.Wealth, value => body = value);
            }
            if (body == null) { receive(null); yield break; }
            try { Account.ApplyServerWealth(body, trigger); WealthVerified = true; AccountChanged?.Invoke(); if (receive != null) receive(Account.Wealth); }
            catch (FormatException e) { Status = "Riqueza rechazada: " + e.Message; receive(null); }
        }

        private IEnumerator RunOnce()
        {
            try { yield return BootstrapOwnIdentity(); } finally { Finished=true;Completed?.Invoke(); }
        }
        private IEnumerator BootstrapOwnIdentity()
        {
            EnsureIdentity();Status="Inicialización Windows propia: configuración e identidad";
            yield return Get(RecoveredReadContracts.Advertise,body=>{});
            yield return Get(RecoveredReadContracts.Config,body=>
            {
                Configuration=RecoveredConfiguration.Parse(body);ConfigVerified=true;
                int configured;if(Configuration.TryInteger("jmo",out configured))jmo=configured;
                bool flag;if(Configuration.TryBoolean("decor_enable",out flag))Account.Config.decor_enable=flag;
                if(Configuration.TryBoolean("close_tutorial",out flag))Account.Config.close_tutorial=flag;
                if(Configuration.TryBoolean("close_story_mode",out flag))Account.Config.close_story_mode=flag;
                if(Configuration.TryInteger("audio_dsp_buffer",out configured))Account.Config.audio_dsp_buffer=configured;
                RecoveredPreferences.SetString("BBPC.Windows.Config606.Envelope",body);Account.Save();
            });
            // INFERRED (route exists in the 6.0.6 URL table; no captured response): /common/init/config
            // shares the flat key->value envelope shape already validated for /app_config. Best-effort.
            yield return Get(RecoveredReadContracts.InitConfig,body=>
            {
                var init=RecoveredConfiguration.Parse(body);int value;bool flag;
                if(init.TryInteger("jmo",out value))jmo=value;
                if(init.TryBoolean("decor_enable",out flag))Account.Config.decor_enable=flag;
                if(init.TryBoolean("close_tutorial",out flag))Account.Config.close_tutorial=flag;
                if(init.TryBoolean("close_story_mode",out flag))Account.Config.close_story_mode=flag;
                if(init.TryInteger("audio_dsp_buffer",out value))Account.Config.audio_dsp_buffer=value;
                RecoveredPreferences.SetString("BBPC.Windows.InitConfig606.Envelope",body);Account.Save();
            });
            if(!ConfigVerified)yield break;
            if(!Identity.HasPair)
            {
                if(Identity.RegistrationPending||Identity.HasPartialPair){Status="Identidad parcial o registro anterior sin respuesta: no se crea otra cuenta automáticamente";yield break;}
                Identity.BeginRegistration();
                yield return Get(RecoveredReadContracts.Identity,body=>{var pair=ParseIdentity(body);Identity.AcceptServerPair(pair.auid,pair.unique_device_id);});
                if(!Identity.HasPair)yield break;
            }
            long before=Now();
            yield return Get(RecoveredReadContracts.Time,body=>{var e=JsonUtility.FromJson<TimeEnvelope>(body);if(e==null||e.status!=1||e.data==null||e.data.timestamp<=0)throw new FormatException("Invalid time envelope");serverDelta=RecoveredIdentity.MeasureTimeDelta(before,lastResponseAt,e.data.timestamp);TimeSynced=true;});
            if(!TimeSynced)yield break;
            yield return Get(RecoveredReadContracts.DynamicConfig,body=>{DynamicConfiguration=RecoveredConfiguration.Parse(body);DynamicConfigVerified=true;RecoveredPreferences.SetString("BBPC.Windows.DynamicConfig606.Envelope",body);RecoveredPreferences.Save();});
            yield return ReadAccount(WealthRefreshTrigger.Splash);
            yield return Get(RecoveredReadContracts.OwnRoles,body=>{Account.ApplyOwnRoles(body);OwnRolesVerified=true;AccountChanged?.Invoke();});
            yield return Get(RecoveredReadContracts.Catalog,body=>{Catalog=CatalogEnvelope.Parse(body);Status=ProfileVerified?"Perfil propio y catálogo recibidos; recursos y servicios restantes pendientes":"Catálogo recibido; perfil no verificado";},true);
            // OBSERVED (user LogF.txt): GET /beatmusic/favorite/list answers CollectionFavListRespModel{items:[StageModel]}.
            yield return FetchFavoriteList(sids=>{});
        }
    }
}
