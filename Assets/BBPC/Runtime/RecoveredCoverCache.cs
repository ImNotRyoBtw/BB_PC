using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    // Public artwork needs no account/bootstrap. Offline validation never enables network.
    public sealed class RecoveredCoverCache : MonoBehaviour
    {
        public bool AllowRemote;
        public int NetworkAttempts {get;private set;}
        public int ActiveRequests {get;private set;}
        public string LastError {get;private set;}
        internal string ValidationDirectory;
        private sealed class Entry {public Texture2D texture;public int users;public long used;}
        private readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
        private sealed class Job
        {
            public readonly List<Func<bool>> subscribers=new List<Func<bool>>();
            public bool Interested{get{foreach(var cancelled in subscribers)if(!cancelled())return true;return false;}}
        }
        private readonly Dictionary<string,Job> pending=new Dictionary<string,Job>();
        private readonly Dictionary<string,float> retryAfter=new Dictionary<string,float>();
        private long tick;
        public static string Key(string url){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(url))).Replace("-","").ToLowerInvariant();}
        public static string PublicUrl(SongImage cover)
        {
            foreach(var value in cover?.urls??new string[0])
            {Uri uri;if(Uri.TryCreate(value,UriKind.Absolute,out uri)&&(uri.Scheme=="https"||uri.Scheme=="http")&&uri.Host=="cdn.makeawesomeapps.com"&&uri.IsDefaultPort&&string.IsNullOrEmpty(uri.UserInfo)&&string.IsNullOrEmpty(uri.Query)&&uri.AbsolutePath.StartsWith("/mtools/beatmusic/",StringComparison.Ordinal)&&uri.AbsolutePath.EndsWith(".png",StringComparison.OrdinalIgnoreCase))return value;}
            return null;
        }
        private string DiskPath(string key)=>Path.Combine(ValidationDirectory??Path.Combine(RecoveredStorage.Root,"BBPC.Covers606"),key+".png");
        public Texture2D Acquire(string key)
        {
            Entry entry;if(entries.TryGetValue(key,out entry)){entry.users++;entry.used=++tick;return entry.texture;}
            string file=Path.Combine(Application.streamingAssetsPath,"Recovered606/Covers",key+".png");if(ValidationDirectory!=null||!File.Exists(file))file=DiskPath(key);
            if(!File.Exists(file))return null;
            try{return Decode(key,File.ReadAllBytes(file));}
            catch(IOException e){LastError=e.GetType().Name;return null;}
            catch(UnauthorizedAccessException e){LastError=e.GetType().Name;return null;}
        }
        private Texture2D Decode(string key,byte[] bytes)
        {
            if(bytes.Length>4*1024*1024)return null;
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,name="Cover-"+key};
            if(!ImageConversion.LoadImage(texture,bytes,true)||texture.width>4096||texture.height>4096){DestroyTexture(texture);return null;}
            while(entries.Count>=32)
            {
                string oldest=null;long stamp=long.MaxValue;foreach(var pair in entries)if(pair.Value.users==0&&pair.Value.used<stamp){oldest=pair.Key;stamp=pair.Value.used;}
                if(oldest==null){DestroyTexture(texture);return null;}DestroyTexture(entries[oldest].texture);entries.Remove(oldest);
            }
            entries.Add(key,new Entry{texture=texture,users=1,used=++tick});return texture;
        }
        public void Release(string key){Entry e;if(key!=null&&entries.TryGetValue(key,out e)){e.users=Math.Max(0,e.users-1);e.used=++tick;}}
        internal IEnumerator Fetch(string url,Action<Texture2D> completed,Func<bool> cancelled)
        {
            string key=Key(url);float until;
            if(cancelled()||!AllowRemote||retryAfter.TryGetValue(key,out until)&&Time.realtimeSinceStartup<until)yield break;
            var cached=Acquire(key);if(cached!=null){completed(cached);yield break;}
            Job job;bool added=!pending.TryGetValue(key,out job);if(added){job=new Job();pending.Add(key,job);}
            job.subscribers.Add(cancelled);
            if(added)StartCoroutine(Download(url,key,job));
            try
            {
                while(pending.ContainsKey(key)){if(cancelled())yield break;yield return null;}
                if(cancelled())yield break;
                var texture=Acquire(key);if(texture!=null)completed(texture);
            }
            finally{job.subscribers.Remove(cancelled);}
        }
        private IEnumerator Download(string url,string key,Job job)
        {
            bool active=false;
            try
            {
                while(ActiveRequests>=2){if(!job.Interested)yield break;yield return null;}
                if(!AllowRemote||!job.Interested)yield break;
                ActiveRequests++;active=true;NetworkAttempts++;
                using(var request=UnityWebRequest.Get(url))
                {
                    request.timeout=15;request.redirectLimit=0;UnityWebRequestAsyncOperation operation=null;
                    try{operation=request.SendWebRequest();}catch(InvalidOperationException e){LastError=e.Message;retryAfter[key]=Time.realtimeSinceStartup+30;}
                    if(operation==null)yield break;
                    while(!operation.isDone){if(!job.Interested){request.Abort();yield break;}if(request.downloadedBytes>4*1024*1024){request.Abort();break;}yield return null;}
                    if(request.result!=UnityWebRequest.Result.Success||request.responseCode!=200||request.downloadedBytes>4*1024*1024){LastError="Cover HTTP "+request.responseCode;retryAfter[key]=Time.realtimeSinceStartup+30;yield break;}
                    var bytes=request.downloadHandler.data;var texture=Decode(key,bytes);
                    if(texture==null){LastError="Cover image rejected";retryAfter[key]=Time.realtimeSinceStartup+30;yield break;}
                    Release(key);
                    try
                    {
                        string file=DiskPath(key);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllBytes(file+".tmp",bytes);
                        if(File.Exists(file))File.Replace(file+".tmp",file,null);else File.Move(file+".tmp",file);
                    }
                    catch(IOException e){LastError=e.GetType().Name;}
                    catch(UnauthorizedAccessException e){LastError=e.GetType().Name;}
                }
            }
            finally{if(active)ActiveRequests--;pending.Remove(key);}
        }
        private static void DestroyTexture(Texture2D texture){if(Application.isPlaying)UnityEngine.Object.Destroy(texture);else UnityEngine.Object.DestroyImmediate(texture);}
        private void OnDestroy(){foreach(var entry in entries.Values)if(entry.texture!=null)DestroyTexture(entry.texture);entries.Clear();}
    }
    public sealed class RecoveredCoverBinding : MonoBehaviour
    {
        private SongImage descriptor;private bool needsRefresh;private bool preserveLayout;private bool fitParent;
        private Image spriteImage;private Sprite ownedSprite;private bool localSprite,localTexture;
        private Vector2 localTextureBounds,localFallbackSize;
        private Texture localFallbackTexture;
        private RecoveredCoverCache cache;private string key;private int generation;private bool owns;private RawImage image;private GameObject fallback;
        public string BoundKey=>key;
        public bool Ready=>owns&&image!=null&&image.enabled&&image.gameObject.activeSelf;
        // StoragedSpritesManager.<LoadTexture>b__0 0x2CB6B1C; needBig=false for song cells.
        // Keep even the native wide-image clamp formula, rather than substituting a generic fitter.
        public static Vector2 OriginalCellSize(int width,int height)
        {if(width<=0||height<=0)throw new ArgumentOutOfRangeException("Cover dimensions");float w=(float)width/height*230f,h=230f;if(w>260f){h=260f/w*260f;w=260f;}return new Vector2(w,h);}
        public void Bind(RecoveredCoverCache source,SongImage cover,RawImage target,GameObject placeholder,bool preserveLayout=false,bool fitParent=false)
        {
            this.preserveLayout=preserveLayout;this.fitParent=fitParent;
            needsRefresh=false;localSprite=false;localTexture=false;descriptor=cover;ResetBinding();cache=source;spriteImage=null;image=target;fallback=placeholder;if(image==null)return;image.enabled=false;if(fallback!=null)fallback.SetActive(true);
            string url=RecoveredCoverCache.PublicUrl(cover);if(url==null)return;key=RecoveredCoverCache.Key(url);
            var texture=cache.Acquire(key);if(texture!=null){Display(texture);return;}
            if(cache.AllowRemote&&Application.isPlaying)
            {
                int version=generation;string boundKey=key;
                cache.StartCoroutine(cache.Fetch(url,result=>{if(version==generation&&isActiveAndEnabled)Display(result);else source.Release(boundKey);},()=>version!=generation));
            }
        }
        public bool BindLocalSprite(RecoveredCoverCache source,SongImage cover,Image target)
        {
            needsRefresh=false;localSprite=true;localTexture=false;descriptor=cover;ResetBinding();cache=source;image=null;fallback=null;spriteImage=target;
            if(target==null)return false;
            target.enabled=false;target.sprite=null;
            string url=RecoveredCoverCache.PublicUrl(cover);if(url==null)return false;
            key=RecoveredCoverCache.Key(url);
            var texture=cache.Acquire(key);if(texture==null)return false;
            owns=true;
            // TODO: recover BoxModel draw_info border conversion before supporting sliced borders.
            ownedSprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f));
            target.type=Image.Type.Simple;target.sprite=ownedSprite;target.enabled=true;
            return true;
        }
        public bool BindLocalTexture(RecoveredCoverCache source,SongImage cover,RawImage target,Vector2 bounds,Texture placeholder=null,Vector2 placeholderSize=default)
        {
            if(bounds.x<=0||bounds.y<=0)throw new ArgumentOutOfRangeException("bounds");
            needsRefresh=false;localSprite=false;localTexture=true;descriptor=cover;ResetBinding();
            cache=source;spriteImage=null;image=target;fallback=null;localTextureBounds=bounds;
            localFallbackTexture=placeholder;localFallbackSize=placeholderSize;
            if(target==null)return false;target.enabled=false;
            // Keep the optional authored fallback in the binding so lifecycle rebinds restore it.
            if(placeholder!=null)
            {
                target.texture=placeholder;target.rectTransform.sizeDelta=placeholderSize;
                target.enabled=true;target.gameObject.SetActive(true);
            }
            string url=RecoveredCoverCache.PublicUrl(cover);if(url==null)return false;
            key=RecoveredCoverCache.Key(url);var texture=cache.Acquire(key);if(texture==null)return false;
            owns=true;target.texture=texture;
            float height=bounds.y,width=height*texture.width/texture.height;
            if(width>bounds.x){height*=bounds.x/width;width=bounds.x;}
            target.rectTransform.sizeDelta=new Vector2(width,height);
            target.gameObject.SetActive(true);target.enabled=true;return true;
        }
        private void Display(Texture2D texture)
        {
            owns=true;image.texture=texture;
            if(fitParent)
            {
                var bounds=((RectTransform)image.transform.parent).rect.size;
                float scale=Mathf.Min(bounds.x/texture.width,bounds.y/texture.height);
                image.rectTransform.sizeDelta=new Vector2(texture.width*scale,texture.height*scale);
                image.rectTransform.anchoredPosition=Vector2.zero;
            }
            else if(!preserveLayout)image.rectTransform.sizeDelta=OriginalCellSize(texture.width,texture.height);
            image.gameObject.SetActive(true);image.enabled=true;if(fallback!=null)fallback.SetActive(false);
        }
        private void ResetBinding()
        {
            generation++;if(cache!=null&&owns)cache.Release(key);owns=false;
            if(image!=null){image.enabled=false;image.texture=null;}
            if(spriteImage!=null){spriteImage.enabled=false;spriteImage.sprite=null;}
            if(ownedSprite!=null){if(Application.isPlaying)Destroy(ownedSprite);else DestroyImmediate(ownedSprite);}ownedSprite=null;key=null;
        }
        private void OnEnable(){needsRefresh=cache!=null&&(descriptor!=null||localTexture&&localFallbackTexture!=null);}
        private void LateUpdate(){if(needsRefresh){needsRefresh=false;if(localSprite)BindLocalSprite(cache,descriptor,spriteImage);else if(localTexture)BindLocalTexture(cache,descriptor,image,localTextureBounds,localFallbackTexture,localFallbackSize);else Bind(cache,descriptor,image,fallback,preserveLayout,fitParent);}}
        private void OnDisable(){ResetBinding();}
        private void OnDestroy(){ResetBinding();}
    }
}
