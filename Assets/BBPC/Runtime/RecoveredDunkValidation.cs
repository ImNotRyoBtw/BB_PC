using System;using System.IO;using System.Collections;using System.Linq;using UnityEngine;
namespace BBPC.UnityRuntime
{
    // Offline replay of the real unlocked SID/AID pair. Input is explicitly synthetic, audio is real.
    public static class RecoveredDunkValidation
    {
        [Serializable] public sealed class Result
        {
            public bool success,loading,roles,paused,physicalInputVerified;
            public bool generalPipeline;
            public int actions,holds,ambients,perfect,score,blood,maxBlood,gfFrames,gfRequests,cameraEvents,captures;
            public float audioSeconds,maxAudioClockDifferenceMs;public string revision,error;
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private static bool Step(Result result,Action action)
        {try{action();return true;}catch(Exception e){result.error=e.ToString();Debug.LogException(e);return false;}}
        public static IEnumerator Run(RecoveredFrontend frontend,string output,Result result,bool general=false)
        {
            Directory.CreateDirectory(output);result.revision=RecoveredFrontend.Revision;
            Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
            var room=frontend.PlayRoom;room.Audio.PauseOnFocusLoss=false;room.InputSource.ReadHardware=false;
            frontend.ShowCatalog();frontend.FullCatalog.Filter("Dunk");
            yield return null;yield return null;
            if(!Step(result,()=>
            {
                var stage=SongCatalog.Read<SongMetadata>(Resources.Load<TextAsset>("LocalStages606/Dunk/stage").text);
                Require(RecoveredCatalogView.IsVerifiedLocal(stage),"Validated Dunk variant not playable in catalog");
                Require(!RecoveredCatalogView.IsVerifiedLocal(new SongMetadata{sid=stage.sid,aid="unverified-variant"}),"Other Dunk variant enabled without content");
                if(general)frontend.Selection.Select(stage,CatalogOrigin.ValidatedLocal606);
                if(general)
                {
                    var item=new SongItem{stage=stage,restriction=new SongRestriction{unlock_type=0,unlock_status=true}};
                    frontend.StartCoroutine(frontend.PrepareRemoteStageFlow(item,false));result.generalPipeline=true;
                }
                else
                {
                    Canvas.ForceUpdateCanvases();
                    frontend.CaptureCurrentView(Path.Combine(output,"catalog-dunk.png"),450,800);result.captures++;
                    var content=frontend.MainRoom.Find("SonglistScrollView").GetComponent<UnityEngine.UI.ScrollRect>().content;
                    var title=content.GetComponentsInChildren<UnityEngine.UI.Text>().First(t=>t.name=="SongText"&&t.text==stage.title);
                    var button=title.transform.parent.GetComponentsInChildren<UnityEngine.UI.Button>().FirstOrDefault(b=>b.name=="PlayButton");
                    Require(button!=null,"Dunk catalog PlayButton missing");
                    RecoveredResult606Validation.Click(frontend,button);
                    Require(frontend.Selection.Song?.Key==stage.Key,"Catalog click selected wrong Dunk variant");
                }
                result.loading=frontend.Phase==FrontendPhase.SongPreparation&&frontend.Loading.Root.activeInHierarchy;
                Require(result.loading,"Song Loading was skipped");
                frontend.CaptureCurrentView(Path.Combine(output,"loading-9x16.png"),900,1600);result.captures++;
            }))yield break;
            float deadline=Time.realtimeSinceStartup+20;
            while(frontend.Phase==FrontendPhase.SongPreparation&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!Step(result,()=>
            {
                Require(frontend.Phase==FrontendPhase.PlayRoom&&(general?room.Content?.Definition.variant=="remote:lJY\u001fMZm":room.Content?.Definition.variant=="dunk"),frontend.TutorialPreparationError??"Dunk preparation timeout");
                var content=room.Content;result.actions=content.Chart.actions.Length;result.holds=content.Chart.actions.Count(a=>a.duration>0);result.ambients=content.Chart.ambients.Length;result.audioSeconds=content.Audio.length;
                Require(result.actions==207&&result.holds==37&&result.ambients==24&&result.audioSeconds*1000>=content.Chart.LastEndMs,"Authentic Dunk pair differs");
                Require(content.Audio.channels==2&&room.Session.Health.Maximum==3&&!room.IsLocalSandbox,"Dunk real audio/stage HP not applied");
                Require(room.Roles.Available&&room.Roles.BfName.StartsWith("BF_Default")&&room.Roles.BossName.StartsWith("Hex_all")&&room.Roles.GfName.StartsWith("GF_Default"),"Dunk original role mapping missing");
                Require(room.View.Find("FirstImg").GetComponent<UnityEngine.UI.RawImage>().texture==Resources.Load<Texture2D>("RoleStages606/Dunk/progressG"),"Dunk incorrectly reused another character's progress icon");
                FreeplayPlayerValidation.ClickStart(frontend);
            }))yield break;
            while(room.State==RecoveredPlayState.Countdown&&Time.realtimeSinceStartup<deadline)yield return null;
            var session=room.Session;var replay=new RecoveredStageReplay(session,room.InputSource.State,room.SampleInput);room.BeforeSessionUpdate=replay.AdvanceTo;
            int sample=0;bool pauseTest=false;deadline=Time.realtimeSinceStartup+125;
            while(!session.Finished&&!session.Dead&&Time.realtimeSinceStartup<deadline)
            {
                float now=room.Audio.Clock.PlayingTime();
                if(room.Audio.Playing&&room.Audio.Source.time>0)result.maxAudioClockDifferenceMs=Math.Max(result.maxAudioClockDifferenceMs,Math.Abs(now-room.Audio.Source.time*1000));
                if(sample<3&&now>=new[]{14000,38000,70000}[sample])
                {
                    room.Audio.Pause();long stamp=room.Audio.Clock.PlayingTimestamp();
                    if(!Step(result,()=>
                    {
                        Require(room.Roles.GfFrames>1&&room.Roles.GfRequests>1,"GF did not alternate original clips");
                        if(sample==1)Require(room.View.Find("FirstImg").GetComponent<UnityEngine.UI.RawImage>().texture==Resources.Load<Texture2D>("RoleStages606/Dunk/progressO"),"Dunk first milestone does not use Hex light icon");
                        room.Roles.Sample(now);
                        frontend.CaptureCurrentView(Path.Combine(output,"play-"+sample+"-9x16.png"),900,1600);
                        frontend.CaptureCurrentView(Path.Combine(output,"play-"+sample+"-16x9.png"),1600,900);result.captures+=2;
                    }))yield break;
                    if(!pauseTest){yield return new WaitForSecondsRealtime(.2f);result.paused=room.Audio.Clock.PlayingTimestamp()==stamp;pauseTest=true;}
                    yield return null;room.Audio.Resume();sample++;
                }
                yield return null;
            }
            if(!Step(result,()=>
            {
                var summary=room.Result;int expected=session.Notes.Sum(n=>n.Hold?1+n.CheckpointTimes.Length:1);
                Require(session.Finished&&!session.Dead&&summary!=null&&frontend.Phase==FrontendPhase.ResultRoom,"Dunk failed to reach ResultRoom");
                Require(summary.sid=="lJY"&&summary.aid=="MZm"&&summary.miss==0&&summary.perfect==expected&&summary.score==expected*3,"Dunk Perfect ledger differs from P1 oracle");
                Require(session.SpawnedCount==207&&session.RecycledCount==207&&session.AmbientCursor==24,"Dunk lifecycle incomplete");
                Require(result.paused&&result.maxAudioClockDifferenceMs<=150&&sample==3,"Dunk pause/clock/captures incomplete");
                Require(room.StageDifficulty==1&&room.ResultDifficultyText.text==RecoveredDifficultyStyles.Label(1),"Dunk native Easy style/result differs");
                result.perfect=summary.perfect;result.score=summary.score;result.blood=summary.blood;result.maxBlood=session.Score.CurrentMaxBlood;result.gfFrames=room.Roles.GfFrames;result.gfRequests=room.Roles.GfRequests;result.cameraEvents=room.Roles.CameraChanges;result.roles=true;
                File.WriteAllText(Path.Combine(output,"result-summary.json"),JsonUtility.ToJson(summary,true));
                File.WriteAllLines(Path.Combine(output,"input-timing.txt"),replay.Timing);File.WriteAllLines(Path.Combine(output,"ledger.txt"),session.Records);
                frontend.CaptureCurrentView(Path.Combine(output,"result-9x16.png"),900,1600);frontend.CaptureCurrentView(Path.Combine(output,"result-16x9.png"),1600,900);result.captures+=2;
                frontend.ShowMainRoom();result.success=true;
            }))yield break;
        }
    }
}
