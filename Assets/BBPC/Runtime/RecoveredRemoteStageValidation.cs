using System;using System.IO;using System.Linq;using System.Collections;using UnityEngine;
namespace BBPC.UnityRuntime
{
    public sealed class RecoveredStageRegressionList {public RecoveredStageRegressionEntry[] entries;}
    public sealed class RecoveredStageRegressionEntry {public string sid,aid,title,resource;}
    // Offline replay using an archived real grant and transported bytes. Not a fresh remote-access PASS.
    public static class RecoveredRemoteStageValidation
    {
        [Serializable] public sealed class Result
        {
            public bool success,loading,paused,originalRoles,physicalInputVerified;
            public int actions,holds,ambients,perfect,score,blood,maxBlood,cameraEvents,captures,bfFrames,bossFrames,gfFrames;
            public string revision,sid,aid,error,bfName,bossName,gfName;public float maxAudioClockDifferenceMs,maxPreparationFrameGapMs,preparationSeconds;public bool dynamicPack,originalProgressIcons;public int missingStates;
        }
        private static bool Step(Result r,Action action){try{action();return true;}catch(Exception e){r.error=e.ToString();Debug.LogException(e);return false;}}
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        public static IEnumerator Run(RecoveredFrontend host,string resource,string output,Result r)
        {
            Directory.CreateDirectory(output);r.revision=RecoveredFrontend.Revision;var room=host.PlayRoom;room.InputSource.ReadHardware=false;room.Audio.PauseOnFocusLoss=false;
            SongItem item=null;
            if(!Step(r,()=>
            {
                item=SongCatalog.Read<SongItem>(Resources.Load<TextAsset>(resource).text);r.sid=item.stage.sid;r.aid=item.stage.aid;
                Check(!RecoveredCatalogView.IsVerifiedLocal(item.stage),"Remote-only variant incorrectly added to offline-play registry");
                Check(RecoveredStageContent.PreparationBlocker(item)=="","Authentic Windows pack unavailable");
                host.Selection.Select(item.stage,CatalogOrigin.Verified606Snapshot);
                host.StartCoroutine(host.PrepareRemoteStageFlow(item,false));r.loading=host.Phase==FrontendPhase.SongPreparation;
                Check(r.loading,"Song Loading skipped");host.CaptureCurrentView(Path.Combine(output,"loading-9x16.png"),900,1600);r.captures++;
            }))yield break;
            float started=Time.realtimeSinceStartup,lastFrame=started,deadline=started+240;
            while(host.Phase==FrontendPhase.SongPreparation&&Time.realtimeSinceStartup<deadline){float frame=Time.realtimeSinceStartup;r.maxPreparationFrameGapMs=Mathf.Max(r.maxPreparationFrameGapMs,(frame-lastFrame)*1000);lastFrame=frame;yield return null;}
            r.preparationSeconds=Time.realtimeSinceStartup-started;
            if(!Step(r,()=>
            {
                Check(host.Phase==FrontendPhase.PlayRoom,host.TutorialPreparationError??"Remote stage preparation timeout");
                var content=room.Content;Check(content.Definition.variant=="remote:"+item.stage.Key,"Wrong SID/AID content loaded");
                Check(room.Roles.BundleMd5==item.stage.game_config.asset.md5,"Wrong original asset MD5 pack");
                r.dynamicPack=RecoveredDynamicRoles.Contains(item.stage.game_config.asset.md5);
                r.originalProgressIcons=room.Roles.ProgressTexture(false)!=null&&room.Roles.ProgressTexture(true)!=null;
                Check(r.originalProgressIcons,"Original stage progress artwork missing");
                Check(room.Roles.BfName.StartsWith(item.stage.game_config.bfs[0])&&room.Roles.BossName.StartsWith(item.stage.game_config.bosses[0]),"Original BF/boss not rendered");
                Check(!room.IsLocalSandbox,"Remote stage enabled diagnostic HP");
                r.actions=content.Chart.actions.Length;r.holds=content.Chart.actions.Count(x=>x.duration>0);r.ambients=content.Chart.ambients.Length;
                r.originalRoles=true;r.missingStates=room.Roles.MissingStates;FreeplayPlayerValidation.ClickStart(host);
            }))yield break;
            while(room.State==RecoveredPlayState.Countdown&&Time.realtimeSinceStartup<deadline)yield return null;
            var session=room.Session;var replay=new RecoveredStageReplay(session,room.InputSource.State,room.SampleInput);room.BeforeSessionUpdate=replay.AdvanceTo;
            int sample=0;float[] fractions={.17f,.52f,.90f};deadline=Time.realtimeSinceStartup+session.FinishDuration/1000f+40;
            while(!session.Finished&&!session.Dead&&Time.realtimeSinceStartup<deadline)
            {
                float now=room.Audio.Clock.PlayingTime();
                if(room.Audio.Playing&&room.Audio.Source.time>0)r.maxAudioClockDifferenceMs=Math.Max(r.maxAudioClockDifferenceMs,Math.Abs(now-room.Audio.Source.time*1000));
                if(sample<3&&now>=session.FinishDuration*fractions[sample])
                {
                    room.Audio.Pause();long stamp=room.Audio.Clock.PlayingTimestamp();room.Roles.Sample(now);
                    if(!Step(r,()=>{r.bfFrames=room.Roles.BfFrames;r.bossFrames=room.Roles.BossFrames;r.gfFrames=room.Roles.GfFrames;r.bfName=room.Roles.BfName;r.bossName=room.Roles.BossName;r.gfName=room.Roles.GfName;Check(room.Roles.BfFrames>1&&room.Roles.BossFrames>1,"Original actors did not animate");host.CaptureCurrentView(Path.Combine(output,"play-"+sample+"-9x16.png"),900,1600);host.CaptureCurrentView(Path.Combine(output,"play-"+sample+"-16x9.png"),1600,900);r.captures+=2;}))yield break;
                    yield return new WaitForSecondsRealtime(.2f);r.paused=room.Audio.Clock.PlayingTimestamp()==stamp;room.Audio.Resume();sample++;
                }
                yield return null;
            }
            if(!Step(r,()=>
            {
                var summary=room.Result;int expected=session.Notes.Where(n=>n.Human).Sum(n=>n.Hold?1+n.CheckpointTimes.Length:1);
                Check(session.Finished&&!session.Dead&&host.Phase==FrontendPhase.ResultRoom&&summary!=null,"General stage did not reach ResultRoom");
                Check(summary.sid==r.sid&&summary.aid==r.aid&&summary.miss==0&&summary.perfect==expected&&summary.score==expected*3,"General stage ledger differs from P1");
                Check(session.SpawnedCount==session.Notes.Count&&session.RecycledCount==session.Notes.Count&&session.AmbientCursor==r.ambients,"Stage lifecycle/ambient cursor incomplete");
                Check(r.paused&&sample==3&&r.maxAudioClockDifferenceMs<150,"Pause/clock/captures incomplete");
                r.perfect=summary.perfect;r.score=summary.score;r.blood=summary.blood;r.maxBlood=session.Score.CurrentMaxBlood;r.cameraEvents=room.Roles.CameraChanges;
                File.WriteAllText(Path.Combine(output,"result-summary.json"),JsonUtility.ToJson(summary,true));File.WriteAllLines(Path.Combine(output,"ledger.txt"),session.Records);File.WriteAllLines(Path.Combine(output,"input-timing.txt"),replay.Timing);
                host.CaptureCurrentView(Path.Combine(output,"result-9x16.png"),900,1600);host.CaptureCurrentView(Path.Combine(output,"result-16x9.png"),1600,900);r.captures+=2;r.success=true;host.ShowMainRoom();
            }))yield break;
            File.WriteAllText(Path.Combine(output,"stage-results.json"),JsonUtility.ToJson(r,true));
        }
    }
}
