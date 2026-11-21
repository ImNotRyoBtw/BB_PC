using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    public static class RecoveredV415Validation
    {
        [Serializable] public sealed class Result {public bool success,headacheDecoded,headachePlayable,physicalInputVerified;public int checks,captures,cdnRequests;public string revision,error,headacheBlocker;}
        [Serializable] public sealed class CoverProof {public Cover[] images;}
        [Serializable] public sealed class Cover {public string url,key,sha256,kind;}
        private static void Check(Result result,bool condition,string name){result.checks++;if(!condition)throw new InvalidOperationException(name);}
        private static void Reject(Result r,Action action,string name){bool rejected=false;try{action();}catch(FormatException){rejected=true;}Check(r,rejected,name);}
        public static IEnumerator Run(RecoveredFrontend host,string output,Result r,bool network=false)
        {
            Directory.CreateDirectory(output);r.revision=RecoveredFrontend.Revision;
            try
            {
                var proof=JsonUtility.FromJson<CoverProof>(Resources.Load<TextAsset>("LocalStages606/cover-proof").text);
                foreach(var image in proof.images)
                {
                    var file=Path.Combine(Application.streamingAssetsPath,"Recovered606/Covers",image.key+".png");
                    Check(r,RecoveredContentBytes.Hash(File.ReadAllBytes(file))==image.sha256,"Packaged cover differs from original HTTP200 bytes");
                }
                if(network)
                {
                    var beforeId=RecoveredPreferences.GetString("BBPC.Windows.InstallId","");
                    foreach(var route in new[]{RecoveredReadContracts.Profile,RecoveredReadContracts.OwnRoles})
                        File.WriteAllText(Path.Combine(output,route==RecoveredReadContracts.Profile?"profile-request-shape.json":"roles-request-shape.json"),JsonUtility.ToJson(host.Services.CaptureOwnRequestShape(route),true));
                    Check(r,RecoveredPreferences.GetString("BBPC.Windows.InstallId","")==beforeId||beforeId=="","Request audit rotated existing install ID");
                }
                Check(r,RecoveredStageUnlock.Body("2wQ4")=="{\"reason\":\"ad\",\"sid\":\"2wQ4\"}","Observed28byte unlock body differs");
                var granted=RecoveredStageUnlock.Confirm(200,"{\"status\":1,\"data\":{\"sid\":\"2wQ4\",\"unlock_status\":true}}","2wQ4");
                Reject(r,()=>RecoveredStageUnlock.Confirm(500,"{\"status\":1,\"data\":{\"sid\":\"2wQ4\",\"unlock_status\":true}}","2wQ4"),"HTTP500 accepted as grant");
                Reject(r,()=>RecoveredStageUnlock.Confirm(200,"{\"status\":1,\"data\":{\"sid\":\"other\",\"unlock_status\":true}}","2wQ4"),"Wrong SID grant accepted");
                Reject(r,()=>RecoveredStageUnlock.Confirm(200,"{\"status\":1,\"err_code\":15,\"data\":{\"sid\":\"2wQ4\",\"unlock_status\":true}}","2wQ4"),"LOGIN_INVALID ignored");
                var catalog=CatalogEnvelope.Parse("{\"status\":1,\"data\":{\"items\":[{\"stage\":{\"sid\":\"2wQ4\",\"aid\":\"Oa7J\"},\"restriction\":{\"unlock_status\":false},\"more\":[{\"stage\":{\"sid\":\"other\",\"aid\":\"other-aid\"},\"restriction\":{\"unlock_status\":false}}]}]}}");
                RecoveredStageUnlock.Reconcile(catalog,granted);Check(r,catalog.items[0].restriction.unlock_status==true&&catalog.items[0].more[0].restriction.unlock_status==false,"Unlock leaked to another SID");
                Check(r,host.Services.RewardedAds==null,"No verified Windows reward provider should be fabricated");
                var pending=SongCatalog.Read<SongItem>(Resources.Load<TextAsset>("LocalStages606/Pending/Headache").text);
                byte[] bpm=File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,"Recovered606/Transport",pending.stage.beatmap.md5));
                byte[] bpmd=File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,"Recovered606/Transport",pending.stage.beatmap_data.md5));
                var chart=RecoveredBeatmap.Parse(System.Text.Encoding.UTF8.GetString(RecoveredContentBytes.Decode(pending.stage.beatmap,bpm)));
                var audio=RecoveredContentBytes.Decode(pending.stage.beatmap_data,bpmd);
                Check(r,chart.actions.Length==254&&chart.actions.Count(a=>a.duration>0)==32&&chart.ambients.Length==47,"Headache original chart counts differ");
                Check(r,audio.Length>1000000&&audio[0]=='I'&&audio[1]=='D'&&audio[2]=='3',"Headache original MP3 ID3 missing");r.headacheDecoded=true;
                r.headacheBlocker=RecoveredStageContent.PreparationBlocker(pending);r.headachePlayable=r.headacheBlocker=="";
                Check(r,!r.headachePlayable&&r.headacheBlocker.Contains("Garcello_song1")&&r.headacheBlocker.Contains("bg_garcello_normal"),"Headache silently borrowed missing actors");
                var prepared=SongCatalog.Read<RecoveredStageRegressionList>(Resources.Load<TextAsset>("RemoteStages606/regressions").text);
                var real=SongCatalog.Read<SongItem>(Resources.Load<TextAsset>(prepared.entries.Single(x=>x.sid=="2Nq").resource).text);
                Check(r,RecoveredStageContent.PreparationBlocker(real)=="","Original Headache asset cannot enter general converter");
            }
            catch(Exception e){r.error=e.ToString();Debug.LogException(e);yield break;}
            foreach(var name in new[]{"Headache","Dunk"})
            {
                host.ShowCatalog();host.FullCatalog.Covers.AllowRemote=false;host.FullCatalog.Filter(name);yield return null;yield return null;
                try
                {
                    string sid=name=="Dunk"?"lJY":"2Nq";var item=host.FullCatalog.Index.MainEntries.Single(x=>x.stage.sid==sid);
                    string expected=RecoveredCoverCache.Key(RecoveredCoverCache.PublicUrl(item.stage.cover));
                    var binding=host.FullCatalog.GetComponentsInChildren<RecoveredCoverBinding>().FirstOrDefault(x=>x.BoundKey==expected);
                    Check(r,binding!=null&&binding.Ready,name+" still shows headphones");
                    var actual=binding.GetComponentsInChildren<RawImage>(true).Single(x=>x.name=="CoverIcon").rectTransform.sizeDelta;
                    Check(r,Mathf.Abs(actual.x-(name=="Dunk"?142.71583f:241.01064f))<.01f&&Mathf.Abs(actual.y-230)<.01f,"Original texture-aspect cover size not applied");
                    Check(r,!binding.GetComponentsInChildren<Transform>(true).Single(x=>x.name=="DefaultCoverImage").gameObject.activeSelf,"Placeholder remains above original cover");
                    var border=binding.GetComponentsInChildren<Image>(true).Single(x=>x.name=="BorderImage");
                    RecoveredDifficultyStyles.TryGet(1,out var style);Check(r,border.sprite==RecoveredUiBuilder.Asset<Sprite>(RecoveredDifficultyStyles.Data,style.resourceId),"Easy RectWhite layer missing");
                    foreach(var size in new[]{new[]{414,896},new[]{1600,900}}){host.CaptureCurrentView(Path.Combine(output,name.ToLowerInvariant()+"-"+size[0]+".png"),size[0],size[1]);r.captures++;}
                    binding.gameObject.SetActive(false);binding.gameObject.SetActive(true);
                }
                catch(Exception e){r.error=e.ToString();Debug.LogException(e);yield break;}
                yield return null;yield return null;
            }
            // A dedicated cache isolates two real CDN downloads from packaged fixtures and the user's cache.
            if(network)
            {
                var owner=new GameObject("Bounded public CDN validation");var cache=owner.AddComponent<RecoveredCoverCache>();
                cache.ValidationDirectory=Path.Combine(output,"cdn-cache-"+Guid.NewGuid().ToString("N"));cache.AllowRemote=true;
                var proof=JsonUtility.FromJson<CoverProof>(Resources.Load<TextAsset>("LocalStages606/cover-proof").text);
                foreach(var cover in proof.images)
                {
                    Texture2D first=null,second=null;bool done=false;
                    cache.StartCoroutine(cache.Fetch(cover.url,texture=>{first=texture;done=true;},()=>false));
                    yield return cache.Fetch(cover.url,texture=>second=texture,()=>false);
                    while(!done&&cache.ActiveRequests>0)yield return null;
                    try{Check(r,first!=null&&ReferenceEquals(first,second),"Duplicate CDN subscribers differ");Check(r,RecoveredContentBytes.Hash(File.ReadAllBytes(Path.Combine(cache.ValidationDirectory,cover.key+".png")))==cover.sha256,"CDN bytes changed from observed cover");}
                    catch(Exception e){r.error=e.ToString();Debug.LogException(e);yield break;}
                    cache.Release(cover.key);cache.Release(cover.key);
                    int attempts=cache.NetworkAttempts;Texture2D cached=null;
                    yield return cache.Fetch(cover.url,texture=>cached=texture,()=>false);
                    try{Check(r,cached!=null&&cache.NetworkAttempts==attempts,"Warm cache repeated CDN request");}
                    catch(Exception e){r.error=e.ToString();Debug.LogException(e);yield break;}
                    cache.Release(cover.key);
                }
                r.cdnRequests=cache.NetworkAttempts;
                try{Check(r,r.cdnRequests==2&&cache.ActiveRequests==0,"CDN downloads are redundant or incomplete");}
                catch(Exception e){r.error=e.ToString();Debug.LogException(e);yield break;}
                UnityEngine.Object.Destroy(owner);
            }
            r.success=true;host.ShowMainRoom();
        }
    }
}
