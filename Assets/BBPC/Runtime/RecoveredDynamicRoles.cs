using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine;
namespace BBPC.UnityRuntime
{
    // Windows-only asset adapter. The sidecar reads serialized data; it does not load Android code.
    public static class RecoveredDynamicRoles
    {
        private sealed class Pack {public RecoveredRoleSource source;public RecoveredCoverCache icons;public Texture2D dark,light;public readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();public readonly List<Texture2D> textures=new List<Texture2D>();public readonly Dictionary<string,Texture2D> textureFiles=new Dictionary<string,Texture2D>();}
        public sealed class Job {public string bundle;public SongItem item;public Dictionary<string,string> fallbacks;public float cameraSize;}
        public sealed class Defaults {public Dictionary<string,string> resources;public float cameraSize;}
        public sealed class FileEntry {public string path,sha256;}
        public sealed class Files {public FileEntry[] files;}
        // DCS treats [Serializable] fields as required in the Unity Player. Error/success envelopes differ.
        public sealed class Receipt {public bool success;public string error,sourceSha256;}
        public static Receipt ReadReceipt(string json)=>SongCatalog.Read<Receipt>(json);
        private static readonly Dictionary<string,Pack> stages=new Dictionary<string,Pack>();
        private static readonly Dictionary<string,Pack> bundles=new Dictionary<string,Pack>();
        public static RecoveredRoleSource ForStage(string sid,string aid)=>stages.TryGetValue(sid+"\u001f"+(aid??""),out var pack)?pack.source:null;
        public static bool Contains(string md5)=>bundles.ContainsKey(md5);
        public static Texture2D ProgressTexture(string md5,bool light)=>bundles.TryGetValue(md5,out var pack)?light?pack.light:pack.dark:null;
        public static bool HasPrefab(string md5,string name)=>bundles.TryGetValue(md5,out var pack)&&pack.source.prefabs.Any(p=>p.name==name);
        public static string Json<T>(T value)
        {using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(T),new DataContractJsonSerializerSettings{UseSimpleDictionaryFormat=true,MaxItemsInObjectGraph=2000000}).WriteObject(stream,value);return Encoding.UTF8.GetString(stream.ToArray());}}
        public static string SafePath(string root,string relative)
        {
            string prefix=Path.GetFullPath(root)+Path.DirectorySeparatorChar,path=Path.GetFullPath(Path.Combine(root,relative));
            if(Path.IsPathRooted(relative)||!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Converted path escapes cache");return path;
        }
        private static RecoveredRoleSource Convert(string converter,string directory,Job job,CancellationToken cancellation)
        {
            // Pinned local tools and schema are included in the build fingerprint. No downloaded executable.
            var files=SongCatalog.Read<Files>(File.ReadAllText(Path.Combine(converter,"files.json")));
            foreach(var entry in files.files)if(RecoveredContentBytes.Hash(File.ReadAllBytes(SafePath(converter,entry.path)))!=entry.sha256)throw new InvalidDataException("Stage converter file integrity failed");
            Directory.CreateDirectory(directory);string sourcePath=Path.Combine(directory,"source.json"),receiptPath=Path.Combine(directory,"result.json");
            if(!File.Exists(receiptPath)||!File.Exists(sourcePath)||!ReadReceipt(File.ReadAllText(receiptPath)).success)
            {
                string jobPath=Path.Combine(directory,"job.json");File.WriteAllText(jobPath,Json(job));
                string exe=Path.Combine(converter,"StagePackWorker.exe"),schema=Path.Combine(converter,"schema.bundle");
                foreach(string value in new[]{jobPath,directory,schema})if(value.Contains("\""))throw new InvalidDataException("Invalid converter argument");
                var start=new ProcessStartInfo(exe,"\""+jobPath+"\" \""+directory+"\" \""+schema+"\""){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=converter,RedirectStandardError=true,RedirectStandardOutput=true};
                using(var process=Process.Start(start))
                {
                    var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
                    var watch=Stopwatch.StartNew();
                    while(!process.WaitForExit(100))
                    {
                        if(cancellation.IsCancellationRequested){process.Kill();throw new OperationCanceledException(cancellation);}
                        if(watch.Elapsed.TotalSeconds>180){process.Kill();throw new TimeoutException("Owned stage converter exceeded 180 seconds");}
                    }
                    Task.WaitAll(stdout,stderr);File.WriteAllText(Path.Combine(directory,"conversion.log"),stdout.Result+stderr.Result);
                    if(process.ExitCode!=0)throw new NotSupportedException(File.Exists(receiptPath)?ReadReceipt(File.ReadAllText(receiptPath)).error:"Stage converter failed without receipt");
                }
            }
            var receipt=ReadReceipt(File.ReadAllText(receiptPath));byte[] bytes=File.ReadAllBytes(sourcePath);
            if(!receipt.success||RecoveredContentBytes.Hash(bytes)!=receipt.sourceSha256)throw new InvalidDataException("Converted manifest integrity failed");
            var source=RecoveredRoleAssets.ReadSource(Encoding.UTF8.GetString(bytes));
            if(source.version!="6.0.6"||source.converterVersion!=2||source.stage.bundleMd5!=job.item.stage.game_config.asset.md5)throw new InvalidDataException("Converted asset identity/version mismatch");
            var cfg=job.item.stage.game_config;
            if(!source.stage.bfs.SequenceEqual(cfg.bfs)||!source.stage.gfs.SequenceEqual(cfg.gfs)||!source.stage.bosses.SequenceEqual(cfg.bosses)||!source.stage.bgs.SequenceEqual(cfg.bgs))throw new InvalidDataException("Converted actor configuration mismatch");
            foreach(var texture in source.assets.GroupBy(sprite=>sprite.resource))
                if(texture.Select(sprite=>sprite.sha256).Distinct().Count()!=1||RecoveredContentBytes.Hash(File.ReadAllBytes(SafePath(directory,texture.Key)))!=texture.First().sha256)throw new InvalidDataException("Converted sprite/atlas integrity failed");
            // The art cache is shared by identical bundle/config across variants, never their access grants.
            source.stage.sid=job.item.stage.sid;source.stage.aid=job.item.stage.aid;
            return source;
        }
        public static IEnumerator Prepare(SongItem item,string bundle,Action<string> failed,Action<float> progress,Func<bool> cancelled,bool allowNetwork)
        {
            string converter=Path.Combine(Application.streamingAssetsPath,"Recovered606/StageConverter");
            if(!File.Exists(Path.Combine(converter,"StagePackWorker.exe"))){failed("Conversor Windows no incluido en esta build");yield break;}
            string key=RecoveredContentBytes.Hash(Encoding.UTF8.GetBytes("Windows2022.3.62f3/adapter2\n"+Json(item.stage.game_config)+"\n"+item.stage.game_config.asset.md5+"\n"+RecoveredContentBytes.Hash(File.ReadAllBytes(Path.Combine(converter,"files.json"))))).Substring(0,32);
            string directory=Path.Combine(RecoveredStorage.Root,"BBPC.Content606/packs-v2",key);
            // Native camera fallback and original default actors already validated in Dunk/PlayRoom.
            var defaults=SongCatalog.Read<Defaults>(File.ReadAllText(Path.Combine(converter,"defaults.json")));
            var job=new Job{bundle=bundle,item=item,cameraSize=defaults.cameraSize,fallbacks=new Dictionary<string,string>()};
            foreach(var entry in defaults.resources)
            {var load=Resources.LoadAsync<GameObject>(entry.Value);yield return load;if(cancelled())yield break;if(load.asset!=null)job.fallbacks.Add(entry.Key,entry.Value);}
            var cancellation=new CancellationTokenSource();var task=Task.Run(()=>Convert(converter,directory,job,cancellation.Token));
            // Observe completion even if the selected screen is cancelled; do not leave an unobserved fault.
            while(!task.IsCompleted){if(cancelled())cancellation.Cancel();yield return null;}
            cancellation.Dispose();
            if(task.IsFaulted){var error=task.Exception.GetBaseException();DebugException(error);if(!cancelled())failed("Conversión Windows: "+error.GetType().Name+": "+error.Message);yield break;}
            if(cancelled())yield break;
            var pack=new Pack{source=task.Result};string errorText=null;
            for(int i=0;i<pack.source.assets.Length;i++)
            {
                if(cancelled()){Release(pack);yield break;}
                var asset=pack.source.assets[i];
                try
                {
                    if(!pack.textureFiles.TryGetValue(asset.resource,out var texture))
                    {
                        texture=new Texture2D(2,2,TextureFormat.RGBA32,false){name=asset.id,wrapMode=TextureWrapMode.Clamp};pack.textures.Add(texture);
                        if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(SafePath(directory,asset.resource)),true))throw new InvalidDataException("Original sprite PNG failed to decode");
                        pack.textureFiles.Add(asset.resource,texture);
                    }
                    var rect=asset.rect==null?new Rect(0,0,texture.width,texture.height):new Rect(asset.rect[0],asset.rect[1],asset.rect[2],asset.rect[3]);
                    var sprite=Sprite.Create(texture,rect,new Vector2(asset.pivot[0],asset.pivot[1]),asset.ppu,0,SpriteMeshType.FullRect);
                    if(asset.vertices!=null)sprite.OverrideGeometry(asset.vertices.Select(v=>new Vector2(v[0],v[1])).ToArray(),asset.triangles);
                    sprite.name=asset.id;pack.sprites.Add(asset.id,sprite);
                }
                catch(Exception e){errorText=e.GetType().Name+": "+e.Message;}
                if(errorText!=null){Release(pack);failed(errorText);yield break;}
                progress((i+1f)/pack.source.assets.Length);yield return null;
            }
            var icons=new GameObject("Original stage progress artwork");pack.icons=icons.AddComponent<RecoveredCoverCache>();pack.icons.AllowRemote=allowNetwork;
            foreach(bool light in new[]{false,true})
            {
                var descriptor=light?item.stage.progress_icon?.light:item.stage.progress_icon?.dark;string url=RecoveredCoverCache.PublicUrl(descriptor);if(url==null)continue;
                var image=pack.icons.Acquire(RecoveredCoverCache.Key(url));
                if(image==null&&allowNetwork)yield return pack.icons.Fetch(url,value=>image=value,cancelled);
                if(cancelled()){Release(pack);yield break;}if(light)pack.light=image;else pack.dark=image;
            }
            // Keep only the selected dynamic pack. Previous PlayRoom has closed before a new JUGAR.
            foreach(var old in stages.Values.Distinct())Release(old);stages.Clear();bundles.Clear();
            stages.Add(item.stage.Key,pack);bundles.Add(pack.source.stage.bundleMd5,pack);
        }
        private static void Release(Pack pack){foreach(var sprite in pack.sprites.Values)UnityEngine.Object.Destroy(sprite);foreach(var texture in pack.textures)UnityEngine.Object.Destroy(texture);if(pack.icons!=null)UnityEngine.Object.Destroy(pack.icons.gameObject);}
        private static void DebugException(Exception error){UnityEngine.Debug.LogException(error);}
        public static GameObject Instantiate(string md5,string name,Transform parent)
        {
            var pack=bundles[md5];var prefab=pack.source.prefabs.Single(p=>p.name==name);
            if(!string.IsNullOrEmpty(prefab.resource))
            {var resource=Resources.Load<GameObject>(prefab.resource);if(resource==null)throw new InvalidDataException("Original fallback prefab absent");return UnityEngine.Object.Instantiate(resource,parent,false);}
            var root=Node(prefab.root,pack,parent);return root;
        }
        private static GameObject Node(RecoveredRoleNode node,Pack pack,Transform parent)
        {
            var go=new GameObject(node.name);go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(node.position[0],node.position[1],node.position[2]);go.transform.localRotation=new Quaternion(node.rotation[0],node.rotation[1],node.rotation[2],node.rotation[3]);go.transform.localScale=new Vector3(node.scale[0],node.scale[1],node.scale[2]);
            if(node.hasRenderer){var r=node.renderer;var sr=go.AddComponent<SpriteRenderer>();if(!string.IsNullOrEmpty(r.sprite))sr.sprite=pack.sprites[r.sprite];sr.color=new Color(r.color[0],r.color[1],r.color[2],r.color[3]);sr.flipX=r.flipX;sr.flipY=r.flipY;sr.enabled=r.enabled;sr.sortingLayerID=r.sortingLayer;sr.sortingOrder=r.sortingOrder;}
            if(node.bossExtra!=null){var config=go.AddComponent<RecoveredBossExtraConfig>();config.orderInLayer=node.bossExtra.orderInLayer;config.enabled=node.bossExtra.enabled;}
            foreach(var child in node.children)Node(child,pack,go.transform);
            if(!string.IsNullOrEmpty(node.controller)){var animation=go.AddComponent<RecoveredSpriteAnimator>();animation.Initialize(pack.source,node.controller,pack.sprites);animation.KeepStateOnDisable=node.keepAnimatorState;if(node.hasAnimatorSettings)animation.enabled=node.animatorEnabled;}
            go.SetActive(node.active);return go;
        }
    }
    // Verified data-only 6.0.6 component (TypeDef1288); native ctor defaults10.
    // Normal prefabs retain their serialized renderer orders. RoleDecorContainer consumers may read this metadata;
    // this is not a shooting callback and must not override every child layer arbitrarily.
    public sealed class RecoveredBossExtraConfig : MonoBehaviour {public int orderInLayer=10;}
    // Samples the recovered paths and original curves explicitly; no fabricated state transitions.
    public sealed class RecoveredSpriteAnimator : MonoBehaviour
    {
        private RecoveredRoleSource source;private RecoveredRoleController controller;private Dictionary<string,Sprite> sprites;
        private RecoveredRoleState state;private RecoveredRoleClip clip;private float transitionOffset;private readonly Dictionary<Transform,Vector3[]> defaults=new Dictionary<Transform,Vector3[]>();
        public string DefaultState=>controller.states.Any(s=>s.name=="idle")?"idle":controller.states[controller.defaultState].name;
        public string NativeDefaultState=>controller.states[controller.defaultState].name;
        public bool KeepStateOnDisable {get;set;}
        public bool HasState(string name)=>controller.states.Any(s=>s.name==name);
        public bool Loop=>clip!=null&&clip.loop;public float Length=>state==null||clip==null?0:clip.duration/Mathf.Abs(state.speed);
        public Sprite CurrentSprite=>GetComponentsInChildren<SpriteRenderer>(true).Select(r=>r.sprite).FirstOrDefault(s=>s!=null);public int missingStates;
        public void Initialize(RecoveredRoleSource data,string id,Dictionary<string,Sprite> images)
        {source=data;controller=data.controllers.Single(c=>c.id==id);sprites=images;foreach(var t in GetComponentsInChildren<Transform>(true))defaults[t]=new[]{t.localPosition,t.localScale,t.localEulerAngles};}
        public bool Play(string name)
        {
            var next=controller.states.SingleOrDefault(s=>s.name==name);
            // Unity Animator.Play on an absent state is a silent no-op, not an exception.
            if(next==null){missingStates++;return false;}
            state=next;transitionOffset=0;
            if(state.mirror||state.speed<=0)throw new NotSupportedException("Mirrored/reversed animation state requires adapter");clip=string.IsNullOrEmpty(state.clip)?null:source.clips.Single(c=>c.id==state.clip);
            foreach(var pair in defaults){pair.Key.localPosition=pair.Value[0];pair.Key.localScale=pair.Value[1];pair.Key.localEulerAngles=pair.Value[2];}Sample(0);return true;
        }
        private Transform Target(string path)=>string.IsNullOrEmpty(path)?transform:transform.Find(path)??throw new InvalidDataException("Original animation path missing: "+path);
        private void SpriteTrack(string path,RecoveredRoleKey[] keys,float time)
        {if(keys==null||keys.Length==0)return;int i=0;while(i+1<keys.Length&&keys[i+1].time<=time)i++;Target(path).GetComponent<SpriteRenderer>().sprite=sprites[keys[i].sprite];}
        public void Sample(float elapsed)
        {
            if(clip==null)return;
            for(int n=0;n<16&&state.transitions!=null&&state.transitions.Length>0;n++)
            {
                var next=state.transitions[0];float length=clip.duration/state.speed*next.exitTime;if(length<=0||elapsed-transitionOffset<length)break;
                float offset=transitionOffset+length;Play(controller.states[next.destination].name);transitionOffset=offset;
            }
            float time=Mathf.Max(0,(elapsed-transitionOffset)*state.speed);time=clip.loop&&clip.duration>0?time%clip.duration:Mathf.Min(time,clip.duration);
            SpriteTrack("",clip.keys,time);foreach(var track in clip.tracks??new RecoveredRoleTrack[0])SpriteTrack(track.path,track.keys,time);
            foreach(var curve in clip.floats??new RecoveredRoleFloat[0])
            {
                float value=Evaluate(curve.keys,time);var target=Target(curve.path);string property=curve.property;
                if(curve.target=="GameObject"){target.gameObject.SetActive(value>=.5f);continue;}
                if(curve.target=="Transform")
                {
                    int axis="xyzw".IndexOf(property[property.Length-1]);
                    if(property.StartsWith("m_LocalRotation",StringComparison.Ordinal)){var rotation=target.localRotation;rotation[axis]=value;target.localRotation=rotation;continue;}
                    Vector3 vector=property.StartsWith("m_LocalPosition",StringComparison.Ordinal)?target.localPosition:property.StartsWith("m_LocalScale",StringComparison.Ordinal)?target.localScale:target.localEulerAngles;vector[axis]=value;
                    if(property.StartsWith("m_LocalPosition",StringComparison.Ordinal))target.localPosition=vector;else if(property.StartsWith("m_LocalScale",StringComparison.Ordinal))target.localScale=vector;else target.localEulerAngles=vector;
                }
                else {var sr=target.GetComponent<SpriteRenderer>();if(property=="m_Enabled")sr.enabled=value>=.5f;else{var color=sr.color;color["rgba".IndexOf(property[property.Length-1])]=value;sr.color=color;}}
            }
        }
        public static float Evaluate(RecoveredRoleFloatKey[] keys,float time)
        {
            if(keys==null||keys.Length==0)throw new InvalidDataException("Empty recovered animation curve");int i=0;while(i+1<keys.Length&&keys[i+1].time<=time)i++;if(i+1==keys.Length||time<=keys[i].time)return keys[i].value;
            var a=keys[i];var b=keys[i+1];if(a.infiniteOut||b.infiniteIn||float.IsInfinity(a.outSlope)||float.IsInfinity(b.inSlope))return a.value;float dt=b.time-a.time,t=(time-a.time)/dt,t2=t*t,t3=t2*t;
            return (2*t3-3*t2+1)*a.value+(t3-2*t2+t)*dt*a.outSlope+(-2*t3+3*t2)*b.value+(t3-t2)*dt*b.inSlope;
        }
    }
}
