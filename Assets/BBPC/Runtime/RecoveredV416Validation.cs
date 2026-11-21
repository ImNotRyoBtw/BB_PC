using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using UnityEngine;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    // Explicit bounded own-account diagnostic. No identity registration or synthetic server response.
    public static class RecoveredV416Validation
    {
        [Serializable] public sealed class UiResult {public bool success;public int checks,captures;public string revision,error;}
        public static void CheckDialog(RecoveredFrontend host,string output,UiResult result)
        {
            Directory.CreateDirectory(output);result.revision=RecoveredFrontend.Revision;
            try
            {
                host.ShowCatalog();host.FullCatalog.Filter("Headache");
                var parent=host.FullCatalog.Index.MainEntries.First(x=>x.stage.sid=="2Nq");
                var dialog=host.gameObject.GetComponent<RecoveredChoiceLevelDialog>()??host.gameObject.AddComponent<RecoveredChoiceLevelDialog>();
                dialog.Open(host,host.FullCatalog,parent);
                string artist=dialog.Tree.Find("ArtistText").GetComponent<Text>().text;
                var old=dialog.StatusFor("old");var current=dialog.StatusFor(parent.stage.Key);
                current("Acceso remoto confirmado. Contenido Windows pendiente: Garcello_song1 / bg_garcello_normal.");
                old("STALE RESPONSE MUST NOT APPEAR");
                if(dialog.Tree.Find("ArtistText").GetComponent<Text>().text!=artist)throw new InvalidOperationException("Status overwrote artist");result.checks++;
                var status=dialog.Tree.Root.GetComponentsInChildren<Text>(true).Single(x=>x.name=="StatusText");
                if(status.text.Contains("STALE"))throw new InvalidOperationException("Foreign variant response changed status");result.checks++;
                Canvas.ForceUpdateCanvases();
                if(status.canvas==null||status.canvas!=dialog.Tree.Find("ChoiceLevelCanvasDialog").GetComponent<Canvas>()||!status.gameObject.activeInHierarchy)throw new InvalidOperationException("Status has no active rendering Canvas");result.checks++;
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var hostType=typeof(RecoveredFrontend);
                hostType.GetField("readingStageKey",flags).SetValue(host,parent.stage.Key);
                hostType.GetField("readingStagePrepare",flags).SetValue(host,false);
                host.ReadSelectedStageInfo(parent.stage,_=>{},true);
                if(hostType.GetField("queuedStagePrepare",flags).GetValue(host)==null)throw new InvalidOperationException("Play during preview was discarded");result.checks++;
                hostType.GetField("readingStageKey",flags).SetValue(host,null);hostType.GetField("queuedStagePrepare",flags).SetValue(host,null);
                var title=dialog.Tree.Find("SongText").GetComponent<Text>();title.text="Título de prueba de recorte: "+new string('W',180);
                Canvas.ForceUpdateCanvases();
                if(title.verticalOverflow!=VerticalWrapMode.Truncate||title.horizontalOverflow!=HorizontalWrapMode.Wrap)throw new InvalidOperationException("Long title not bounded");result.checks++;
                foreach(var size in new[]{new[]{900,1600},new[]{1600,900}}){host.CaptureCurrentView(Path.Combine(output,"selector-status-"+size[0]+".png"),size[0],size[1]);result.captures++;}
                dialog.Close();old("CLOSED RESPONSE");result.checks++;result.success=true;
            }
            catch(Exception e){result.error=e.ToString();Debug.LogException(e);}
            finally{host.GetComponent<RecoveredChoiceLevelDialog>()?.Close();host.ShowMainRoom();}
            File.WriteAllText(Path.Combine(output,"dialog-results.json"),JsonUtility.ToJson(result,true));
        }
        [Serializable] public sealed class RemoteResult
        {
            public string revision,error,sid="1G9a",aid="k08Z",contentBlocker;
            public bool success,identityReused,installIdPreserved,profileVerified,rolesVerified,playerIdReceived,persistedProfileRestored,persistedRolesRestored,playerIdMatchesPersisted,initiallyUnlocked,finallyUnlocked,unlockSent,unlockConfirmed,configVerified,dynamicVerified,wealthVerified;
            public int requests,unlockPosts;public RecoveredServiceClient.ReadAttempt[] attempts;
        }
        public static IEnumerator Remote(RecoveredFrontend host,string output,bool unlock)
        {
            Directory.CreateDirectory(output);var r=new RemoteResult{revision=RecoveredFrontend.Revision};var api=host.Services;
            if(!api.HasPersistedPair)throw new InvalidOperationException("Existing Windows identity required; registration prohibited");
            string auid=api.Identity.Auid,udid=api.Identity.Udid,install=RecoveredPreferences.GetString("BBPC.Windows.InstallId","");
            string player=host.Account.ServerProfile?.user?.player_id;
            r.persistedProfileRestored=!string.IsNullOrEmpty(player);r.persistedRolesRestored=host.Account.ServerRoles?.default_bf!=null;
            if(!unlock&&Array.IndexOf(Environment.GetCommandLineArgs(),"-bbpc-complete-bootstrap")>=0)
            {
                api.StartOwnOnlineTest();while(!api.Finished)yield return null;
            }
            else yield return api.ProbeExistingAccount(true);
            r.identityReused=auid==api.Identity.Auid&&udid==api.Identity.Udid;r.installIdPreserved=install==RecoveredPreferences.GetString("BBPC.Windows.InstallId","");
            r.profileVerified=api.ProfileVerified;r.rolesVerified=api.OwnRolesVerified;r.playerIdReceived=!string.IsNullOrEmpty(host.Account.ServerProfile?.user?.player_id);
            r.configVerified=api.ConfigVerified;r.dynamicVerified=api.DynamicConfigVerified;r.wealthVerified=api.WealthVerified;
            r.playerIdMatchesPersisted=r.persistedProfileRestored&&player==host.Account.ServerProfile?.user?.player_id;
            if(r.profileVerified&&r.rolesVerified&&r.playerIdReceived)
            {
                if(api.Catalog==null)yield return api.RefreshOwnCatalog();
                var selected=api.Catalog==null?null:SongCatalog.Enumerate(api.Catalog.items).SingleOrDefault(x=>x.stage.sid==r.sid&&x.stage.aid==r.aid);
                if(selected==null)r.error="Exact Star Whenever Both Sides not in own catalog; no guessed SID/unlock";
                else
                {
                    SongItem before=null;yield return api.FetchStageInfo(selected.stage,value=>before=value);
                    r.initiallyUnlocked=before?.restriction?.unlock_status==true;
                    SongItem final=before;
                    if(unlock&&before!=null)yield return api.EnsureStageAccess(selected.stage,value=>final=value);
                    r.finallyUnlocked=final?.restriction?.unlock_status==true;
                    if(final!=null)
                    {
                        r.contentBlocker=RecoveredStageContent.PreparationBlocker(final);
                        // Public song descriptors only; never export profile, identity or cookie envelopes.
                        using(var stream=new MemoryStream())
                        {
                            new DataContractJsonSerializer(typeof(SongItem),new DataContractJsonSerializerSettings{UseSimpleDictionaryFormat=true}).WriteObject(stream,final);
                            File.WriteAllBytes(Path.Combine(output,"stage-info-public.json"),stream.ToArray());
                        }
                    }
                }
            }
            r.attempts=api.ReadAttempts.ToArray();r.requests=r.attempts.Length;
            var posts=r.attempts.Where(x=>x.route==RecoveredStageUnlock.Route).ToArray();r.unlockPosts=posts.Length;r.unlockSent=posts.Length>0;r.unlockConfirmed=posts.Length==1&&posts[0].contractAccepted;
            r.success=r.identityReused&&r.installIdPreserved&&r.profileVerified&&r.rolesVerified&&r.playerIdReceived&&r.finallyUnlocked&&r.unlockPosts<=1;
            if(!unlock)r.success&=!r.unlockSent&&r.playerIdMatchesPersisted&&r.persistedRolesRestored;
            if(r.success){RecoveredPreferences.SetInt("BBPC.Windows.OnlineEnabled",1);RecoveredPreferences.Save();}
            File.WriteAllText(Path.Combine(output,"access-results.json"),JsonUtility.ToJson(r,true));
            Application.Quit(r.success?0:3);
        }
    }
}
