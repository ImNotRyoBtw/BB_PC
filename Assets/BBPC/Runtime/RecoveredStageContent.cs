using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
namespace BBPC.UnityRuntime
{
    // General SID/AID pipeline. A verified Windows role pack is mandatory; Android bundles are not executed.
    public static class RecoveredStageContent
    {
        [Serializable] public sealed class Registry {public string[] manifests;}
        private static Dictionary<string,TutorialDefinition> definitions;
        public static void EnsureLocalBindings()
        {
            if(definitions!=null)return;definitions=new Dictionary<string,TutorialDefinition>();
            var config=Resources.Load<TextAsset>("LocalStages606/registry");if(config==null)throw new InvalidDataException("Verified local content registry missing");
            foreach(var path in JsonUtility.FromJson<Registry>(config.text).manifests)
            {
                var asset=Resources.Load<TextAsset>(path);if(asset==null)throw new InvalidDataException("Verified local manifest missing");
                var manifest=JsonUtility.FromJson<TutorialManifest>(asset.text);if(manifest.version!="6.0.6"||manifest.items==null)throw new InvalidDataException("Invalid local stage manifest");
                foreach(var item in manifest.items){definitions.Add(item.variant,item);SongSelection.RegisterVerifiedLocal(item.sid,item.aid,item.variant);}
            }
        }
        public static TutorialDefinition LocalDefinition(string variant)
        {EnsureLocalBindings();TutorialDefinition definition;if(!definitions.TryGetValue(variant,out definition))throw new ArgumentException("Unknown verified local stage variant");return definition;}
        public static string PreparationBlocker(SongItem item)
        {
            if(item?.stage==null)return "stage/info no disponible";
            if(item.restriction?.unlock_status!=true)return "Etapa sin acceso remoto confirmado; no se simula recompensa publicitaria.";
            if(!RecoveredReadContracts.ValidRemoteFile(item.stage.beatmap)||!RecoveredReadContracts.ValidRemoteFile(item.stage.beatmap_data))return "Faltan descriptores BPM/BPMD válidos";
            var source=RecoveredRoleAssets.ForStage(item.stage.sid,item.stage.aid);
            if(source==null)return RecoveredReadContracts.ValidRemoteFile(item.stage.game_config?.asset)&&File.Exists(Path.Combine(Application.streamingAssetsPath,"Recovered606/StageConverter/StagePackWorker.exe"))?"":"Falta descriptor/conversor Windows de los actores/escenario: "+string.Join(", ",(item.stage.game_config?.bosses??new string[0]).Concat(item.stage.game_config?.bgs??new string[0]));
            var cfg=item.stage.game_config;
            if(cfg?.asset!=null&&!string.Equals(cfg.asset.md5,source.stage.bundleMd5,StringComparison.OrdinalIgnoreCase))return "El pack Windows no corresponde al MD5 del asset original";
            if(cfg==null||!Same(cfg.bfs,source.stage.bfs)||!Same(cfg.gfs,source.stage.gfs)||!Same(cfg.bosses,source.stage.bosses)||!Same(cfg.bgs,source.stage.bgs))return "El pack Windows no corresponde a game_config";
            foreach(var name in source.stage.bfs.Concat(source.stage.gfs).Concat(source.stage.bosses).Concat(source.stage.bgs))
                if(name!="NoAnimation"&&!RecoveredDynamicRoles.HasPrefab(source.stage.bundleMd5,name)&&Resources.Load<GameObject>(RecoveredRoleAssets.PrefabResource(source.stage.bundleMd5,name))==null)return "Prefab Windows no disponible: "+name;
            return "";
        }
        private static bool Same(string[] left,string[] right)=>(left??new string[0]).SequenceEqual(right??new string[0]);
        // stage/info occasionally omits asset. Reuse only the same catalog variant and exact actor arrays.
        public static void CompleteAssetDescriptor(SongItem item,SongCatalog catalog)
        {
            var cfg=item?.stage?.game_config;if(cfg==null||cfg.asset!=null||catalog?.items==null)return;
            var original=SongCatalog.Enumerate(catalog.items).SingleOrDefault(x=>x?.stage?.Key==item.stage.Key)?.stage?.game_config;
            if(original==null||!RecoveredReadContracts.ValidRemoteFile(original.asset)||!Same(cfg.bfs,original.bfs)||!Same(cfg.gfs,original.gfs)||!Same(cfg.bosses,original.bosses)||!Same(cfg.bgs,original.bgs)||!Same(cfg.others,original.others)||(cfg.groups??new int[0]).SequenceEqual(original.groups??new int[0])==false)return;
            cfg.asset=new RemoteSongFile{url=original.asset.url,md5=original.asset.md5};
        }
        // Explicit index of songs whose content is already on this disk, so the catalog can mark them
        // offline-ready and the transport is not downloaded twice. Written under BBPC.Content606.
        [Serializable] public sealed class CachedSong {public string sid,aid,variant,beatmapMd5,beatmapDataMd5,assetMd5;public long cachedAtMs;}
        [Serializable] public sealed class CachedSongIndex {public CachedSong[] items;}
        private static string SongIndexPath=>Path.Combine(RecoveredStorage.Root,"BBPC.Content606/transport","songs.json");
        private static CachedSongIndex ReadSongIndex()
        {
            try
            {
                var path=SongIndexPath;if(!File.Exists(path))return null;
                var index=JsonUtility.FromJson<CachedSongIndex>(File.ReadAllText(path));
                return index!=null&&index.items!=null?index:null;
            }
            catch(FormatException){return null;}catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}
        }
        public static CachedSong[] CachedSongs(){var index=ReadSongIndex();return index==null?new CachedSong[0]:index.items;}
        // Recorded only after the decoded content reached this disk. A failed download never enters the index.
        public static void RecordCachedSong(SongItem item,string variant)
        {
            var stage=item?.stage;if(stage==null||string.IsNullOrEmpty(stage.sid))return;
            var index=ReadSongIndex();var items=new List<CachedSong>(index?.items??new CachedSong[0]);
            items.RemoveAll(c=>c!=null&&c.sid==stage.sid&&(c.aid??"")==(stage.aid??""));
            items.Add(new CachedSong{sid=stage.sid,aid=stage.aid,variant=variant,beatmapMd5=stage.beatmap?.md5,beatmapDataMd5=stage.beatmap_data?.md5,assetMd5=stage.game_config?.asset?.md5,cachedAtMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()});
            try{Write(SongIndexPath,Encoding.UTF8.GetBytes(JsonUtility.ToJson(new CachedSongIndex{items=items.ToArray()})));}catch(IOException){}catch(UnauthorizedAccessException){}
        }
        // True only when every required descriptor already has its verified bytes on this disk.
        public static bool IsContentCached(SongMetadata song)
        {
            if(song==null||string.IsNullOrEmpty(song.sid))return false;
            var index=ReadSongIndex();if(index==null)return false;
            foreach(var cached in index.items)
            {
                if(cached==null||cached.sid!=song.sid||(cached.aid??"")!=(song.aid??""))continue;
                if(!TransportPresent(cached.beatmapMd5)||!TransportPresent(cached.beatmapDataMd5))return false;
                if(!string.IsNullOrEmpty(cached.assetMd5)&&!TransportPresent(cached.assetMd5))return false;
                return File.Exists(Path.Combine(RecoveredStorage.Root,"BBPC.Content606",(cached.beatmapDataMd5??"").ToLowerInvariant()+".mp3"));
            }
            return false;
        }
        private static bool TransportPresent(string md5)
        {
            if(string.IsNullOrEmpty(md5))return false;
            return File.Exists(Path.Combine(RecoveredStorage.Root,"BBPC.Content606/transport",md5.ToLowerInvariant()));
        }
        public static IEnumerator Prepare(SongItem item,Action<PreparedTutorial> ready,Action<string> failed,Action<float> progress,Func<bool> cancelled,bool allowNetwork=true,Action<string> phase=null)
        {
            string blocker=PreparationBlocker(item);if(blocker!=""){failed(blocker);yield break;}
            if(RecoveredRoleAssets.ForStage(item.stage.sid,item.stage.aid)==null)
            {
                byte[] bundle=null;string packError=null;
                phase?.Invoke("BundleTransport");
                yield return Fetch(item.stage.game_config.asset,64*1024*1024,b=>bundle=b,e=>packError=e,cancelled,allowNetwork);
                if(cancelled())yield break;if(packError!=null){failed(packError);yield break;}
                string pathBundle=Path.Combine(RecoveredStorage.Root,"BBPC.Content606/transport",item.stage.game_config.asset.md5.ToLowerInvariant());
                var store=Task.Run(()=>Write(pathBundle,bundle));while(!store.IsCompleted)yield return null;
                if(store.IsFaulted){failed(store.Exception.GetBaseException().Message);yield break;}if(cancelled())yield break;
                phase?.Invoke("ConverterAndActorAssets");yield return RecoveredDynamicRoles.Prepare(item,pathBundle,e=>packError=e,x=>progress(x*.3f),cancelled,allowNetwork);
                if(cancelled())yield break;if(packError!=null){failed(packError);yield break;}
                if(RecoveredRoleAssets.ForStage(item.stage.sid,item.stage.aid)==null){failed("Conversión sin pack validado");yield break;}
                blocker=PreparationBlocker(item);if(blocker!=""){failed(blocker);yield break;}
            }
            byte[] chartBytes=null,audioBytes=null;string error=null;
            phase?.Invoke("ChartTransport");progress(.3f);yield return Fetch(item.stage.beatmap,2*1024*1024,b=>chartBytes=b,e=>error=e,cancelled,allowNetwork);if(cancelled())yield break;
            if(error!=null){failed(error);yield break;}progress(.4f);
            phase?.Invoke("AudioTransport");yield return Fetch(item.stage.beatmap_data,24*1024*1024,b=>audioBytes=b,e=>error=e,cancelled,allowNetwork);if(cancelled())yield break;
            if(error!=null){failed(error);yield break;}progress(.8f);
            
            // All three expensive managed operations used to run synchronously inside
            // a Unity coroutine step. Keep Unity AudioClip/UnityWebRequest on main thread.
            string path=Path.Combine(RecoveredStorage.Root,"BBPC.Content606",item.stage.beatmap_data.md5.ToLowerInvariant()+".mp3");
            string variant="remote:"+item.stage.Key, sid=item.stage.sid,aid=item.stage.aid,title=item.stage.title,artist=item.stage.artist;
            int hp=item.stage.hp,difficulty=item.stage.CurrentDifficulty,playType=item.stage.play_type,declaredDurationMs=(int)item.stage.duration;
            var beatmapLocation=item.stage.beatmap;var audioLocation=item.stage.beatmap_data;
            phase?.Invoke("DecodeAndValidate");Task<DecodedStage> decode=Task.Run(() => DecodeAndStore(beatmapLocation,chartBytes,audioLocation,audioBytes,path,
                variant,sid,aid,title,artist,hp,difficulty,playType,declaredDurationMs));
            while(!decode.IsCompleted)
            {
                yield return null;
            }
            if(decode.IsFaulted||decode.IsCanceled)
            {
                var fault=decode.Exception?.GetBaseException();
                if(fault!=null)Debug.LogException(fault);
                failed(fault==null?"Content preparation cancelled":"Chart/audio: "+fault.GetType().Name+": "+fault.Message);
                yield break;
            }
            if(cancelled())yield break;
            RecordCachedSong(item,variant);
            RecoveredBeatmap chart=decode.Result.Chart;
            TutorialDefinition definition=decode.Result.Definition;
            progress(.9f);
            if(cancelled())yield break;
            phase?.Invoke("UnityAudio");yield return RecoveredTutorialContent.PrepareAudio(definition,chart,path,ready,failed,x=>progress(.9f+x*.1f));
        }
        
        private sealed class DecodedStage {public TutorialDefinition Definition;public RecoveredBeatmap Chart;}
        // No Unity API on this worker: only cryptography, managed JSON and disk I/O.
        private static DecodedStage DecodeAndStore(RemoteSongFile beatmap,byte[] beatmapBytes,RemoteSongFile audioLocation,byte[] audioBytes,string path,
            string variant,string sid,string aid,string title,string artist,int hp,int difficulty,int playType,int durationMs)
        {
            byte[] jsonBytes=RecoveredContentBytes.Decode(beatmap,beatmapBytes);
            byte[] mp3Bytes=RecoveredContentBytes.Decode(audioLocation,audioBytes);
            var chart=RecoveredBeatmap.Parse(new UTF8Encoding(false,true).GetString(jsonBytes));
            if(durationMs<=0||chart.LastEndMs>durationMs)throw new InvalidDataException("Chart exceeds stage duration");
            Write(path,mp3Bytes);
            return new DecodedStage{
                Chart=chart,
                Definition=new TutorialDefinition{variant=variant,sid=sid,aid=aid,title=title,artist=artist,hp=hp,difficulty=difficulty,
                    playType=playType,declaredDurationMs=durationMs,actions=chart.actions.Length,
                    chartSha256=RecoveredContentBytes.Hash(jsonBytes),audioSha256=RecoveredContentBytes.Hash(mp3Bytes)}
            };
        }
        private static void Write(string path,byte[] bytes)
        {Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(temp,bytes);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
        private static IEnumerator Fetch(RemoteSongFile location,int limit,Action<byte[]> ready,Action<string> failed,Func<bool> cancelled,bool allowNetwork)
        {
            string key=location.md5.ToLowerInvariant();string disk=Path.Combine(RecoveredStorage.Root,"BBPC.Content606/transport",key);
            string bundled=Path.Combine(Application.streamingAssetsPath,"Recovered606/Transport",key);
            foreach(var file in new[]{bundled,disk})
            {
                if(!File.Exists(file))continue;
                var load=Task.Run(()=>{if(new FileInfo(file).Length>limit)return null;var value=File.ReadAllBytes(file);return RecoveredContentBytes.Hash(value,true)==key?value:null;});
                while(!load.IsCompleted)yield return null;
                if(load.IsFaulted){failed(load.Exception.GetBaseException().GetType().Name);yield break;}if(cancelled())yield break;
                if(load.Result!=null){ready(load.Result);yield break;}
                // Corrupt cache never becomes accepted content or skips transport validation.
            }
            if(!allowNetwork){failed("Verified transport cache missing (offline)");yield break;}
            // A truncated transfer is retried once. The cache is never poisoned: bytes are written only
            // after the MD5 matches the descriptor the server gave us.
            for(int attempt=1;attempt<=2;attempt++)
            {
                using(var request=UnityWebRequest.Get(location.url))
                {
                    request.timeout=30;request.redirectLimit=0;UnityWebRequestAsyncOperation operation=null;string startError=null;
                    try{operation=request.SendWebRequest();}catch(InvalidOperationException e){startError=e.Message;}
                    if(startError!=null){failed(startError);yield break;}
                    while(!operation.isDone){if(cancelled()){request.Abort();yield break;}if(request.downloadedBytes>(ulong)limit){request.Abort();break;}yield return null;}
                    if(request.result!=UnityWebRequest.Result.Success||request.responseCode!=200||request.downloadedBytes>(ulong)limit)
                    {
                        if(attempt<2)continue;
                        failed("Content HTTP "+request.responseCode);yield break;
                    }
                    byte[] bytes=request.downloadHandler.data;string storeError=null;
                    var store=Task.Run(()=>{if(RecoveredContentBytes.Hash(bytes,true)!=key)throw new InvalidDataException("Transport MD5 mismatch");Write(disk,bytes);});
                    while(!store.IsCompleted)yield return null;
                    if(store.IsFaulted)storeError=store.Exception.GetBaseException().Message;
                    if(cancelled())yield break;
                    if(storeError==null){ready(bytes);yield break;}
                    if(attempt<2&&storeError.IndexOf("Transport MD5 mismatch",StringComparison.Ordinal)>=0)continue;
                    failed(storeError);yield break;
                }
            }
            failed("Content transfer failed");
        }
    }
}
