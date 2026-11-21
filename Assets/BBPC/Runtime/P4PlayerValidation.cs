using System;using System.IO;using System.Collections;using System.Linq;using UnityEngine;
namespace BBPC.UnityRuntime
{
 public static class P4PlayerValidation
 {
  [Serializable] public sealed class Result
  {public bool healthSamplerClamp,success,normalResult,normalScriptedResult,fnfResult,elasticPrepared,elasticResult,movingNotes,holdGeometryValidated,noteEffects,effectsPauseFrozen,ratingAnimation,ratingLayout,holdLoops,heldHeadHidden,physicalInputVerified,originalRolesRendered,originalCameraDispatched;public int normalSpawned,normalRecycled,normalMiss,normalScore,normalScriptedScore,normalScriptedPerfect,musicalEmissions,hitEmissions,fnfSpawned,fnfMiss,elasticActions,elasticAmbients,elasticPerfect,elasticScore,elasticCameraEvents,elasticAmbientCursor,captures,roleCaptures;public float elasticSeconds,elasticMaxScriptedInputLatenessMs,elasticDeathTimeMs,maxScriptedInputLatenessMs,clockAudioDifferenceMs,longRunClockAudioDifferenceMs;public string revision,error;}
  static bool Step(Result r,Action action){try{action();return true;}catch(Exception e){r.error=e.ToString();Debug.LogException(e);return false;}}
  static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
  public static IEnumerator Run(RecoveredFrontend frontend,string output,Result r)
  {
   Directory.CreateDirectory(output);r.revision=RecoveredFrontend.Revision;
   var freeplay=new FreeplayPlayerValidation.Result();yield return FreeplayPlayerValidation.Run(frontend,Path.Combine(output,"freeplay-v48"),freeplay);File.WriteAllText(Path.Combine(output,"freeplay-v48/results.json"),JsonUtility.ToJson(freeplay,true));if(!freeplay.success){r.error=freeplay.error??"Freeplay validation incomplete";yield break;}
   foreach(string caseName in new[]{"normal","fnf","normal-scripted"})
   {
    string variant=caseName=="normal-scripted"?"normal":caseName;bool scripted=caseName=="normal-scripted";
    if(!Step(r,()=>{frontend.ShowMainRoom();frontend.PlayRoom.NextSandboxExtra=scripted?(int?)null:100-RecoveredTutorialContent.Definition(variant).hp;frontend.PrepareLocalTutorial(variant);}))yield break;
    float deadline=Time.realtimeSinceStartup+15;
    while((frontend.Phase==FrontendPhase.SongPreparation||frontend.Phase==FrontendPhase.StageCountdown)&&Time.realtimeSinceStartup<deadline)yield return null;
    if(!Step(r,()=>Require(frontend.Phase==FrontendPhase.PlayRoom,frontend.TutorialPreparationError??"Preparation failed")))yield break;
    if(!Step(r,()=>FreeplayPlayerValidation.ClickStart(frontend)))yield break;
    while(frontend.PlayRoom.State==RecoveredPlayState.Countdown&&Time.realtimeSinceStartup<deadline)yield return null;
    var room=frontend.PlayRoom;var session=room.Session;
    if(caseName=="normal")
    {
     room.Audio.Pause();
     if(!Step(r,()=>
     {
      var health=room.View.Find("Health").GetComponent<UnityEngine.UI.Image>().sprite.texture;
      Require(health.wrapMode==TextureWrapMode.Clamp,"De-atlased Health sprite still repeats the opposite edge");
      try{health.wrapMode=TextureWrapMode.Repeat;frontend.CaptureCurrentView(Path.Combine(output,"health-repeat-control.png"),900,1600);}
      finally{health.wrapMode=TextureWrapMode.Clamp;}
      frontend.CaptureCurrentView(Path.Combine(output,"health-clamp-corrected.png"),900,1600);r.captures+=2;r.healthSamplerClamp=true;
      var f=room.Feedback;f.Reset();f.Show(BBPC.Gameplay.HitQuality.Perfect,123);
      Require(f.NumberLayout.localPosition==new Vector3(67.5f,-10,0)&&f.Rating.rectTransform.localPosition==new Vector3(0,-100,0),"Original feedback local positions differ");
      Require(f.Rating.rectTransform.localScale==Vector3.one*.5f,"Rating did not restart at half size");f.Advance(.1f);
      Require(Math.Abs(f.Rating.rectTransform.localScale.x-.875f)<.0001f&&f.Rating.color.a==1,"Rating OutQuad expansion differs");
      frontend.CaptureCurrentView(Path.Combine(output,"rating-expand-9x16.png"),900,1600);frontend.CaptureCurrentView(Path.Combine(output,"rating-expand-16x9.png"),1600,900);r.captures+=2;
      f.Advance(.1f);Require(f.Rating.rectTransform.localScale==Vector3.one,"Rating expansion duration differs");f.Advance(.5f);f.Advance(.2f);
      Require(Math.Abs(f.Rating.color.a-.25f)<.0001f,"Original rating fade curve differs");
      frontend.CaptureCurrentView(Path.Combine(output,"rating-fade-9x16.png"),900,1600);r.captures++;f.Advance(.3f);Require(f.Rating.color.a==0,"Rating remains static after fade");
      f.Show(BBPC.Gameplay.HitQuality.Miss,0);Require(!f.NumberLayout.gameObject.activeSelf&&f.Rating.color.a==1,"Miss does not hide numbers or restart rating");
      f.Advance(.05f);f.Show(BBPC.Gameplay.HitQuality.Great,10);Require(f.NumberLayout.gameObject.activeSelf&&Math.Abs(f.NumberLayout.localPosition.x-93.75f)<.0001f,"Combo did not recover after Miss");
      r.ratingAnimation=r.ratingLayout=true;f.Reset();
     }))yield break;
     yield return null;room.Audio.Resume();
    }
    if(!Step(r,()=>Require(room.View.Find("HPCountLabel").GetComponent<UnityEngine.UI.Text>().text==(scripted?"10/10":"100/100"),"Initial/replay HP HUD is stale")))yield break;
    var replay=scripted?new RecoveredStageReplay(session,room.InputSource.State,room.SampleInput):null;
    if(replay!=null)room.BeforeSessionUpdate=replay.AdvanceTo;
    File.WriteAllText(Path.Combine(output,caseName+"-timing.txt"),"revision="+r.revision+" case="+caseName+"\n");bool captured=scripted,effectsCaptured=false;float sampleAt=0;deadline=Time.realtimeSinceStartup+95;
    while(!session.Finished&&Time.realtimeSinceStartup<deadline)
    {
     if(room.Audio.Playing&&room.Audio.Source.time>0)r.longRunClockAudioDifferenceMs=Math.Max(r.longRunClockAudioDifferenceMs,Math.Abs(room.Audio.Clock.PlayingTime()-room.Audio.Source.time*1000));
     if(Time.realtimeSinceStartup>=sampleAt){sampleAt=Time.realtimeSinceStartup+5;File.AppendAllText(Path.Combine(output,caseName+"-timing.txt"),"wall="+Time.realtimeSinceStartup+" clock="+room.Audio.Clock.PlayingTime()+" dsp="+AudioSettings.dspTime+" audio="+room.Audio.Source.time+" playing="+room.Audio.Playing+" focus="+Application.isFocused+" delta="+Time.deltaTime+"\n");}
     if(!captured&&session.Notes.Any(n=>n.Hold&&n.Spawned&&!n.Complete&&n.Y>-300&&n.Y<400))
     {
      if(!Step(r,()=>
      {
       r.clockAudioDifferenceMs=Math.Max(r.clockAudioDifferenceMs,Math.Abs(room.Audio.Clock.PlayingTime()-room.Audio.Source.time*1000));
       foreach(var body in room.View.Root.GetComponentsInChildren<UnityEngine.UI.Image>().Where(i=>i.name=="Line2D"))
       {
        var tail=body.transform.parent.Find("TailNode").GetComponent<RectTransform>();
        Require(Math.Abs(body.rectTransform.localPosition.y-(tail.localPosition.y-25))<.1f,"Original hold tail/body seam differs");
        Require(Vector3.Dot(body.rectTransform.up,body.transform.parent.up)<-.99f,"Original 180-degree hold orientation lost");
        r.holdGeometryValidated=true;
       }
       Require(r.holdGeometryValidated,"No active hold geometry audited");
       room.Audio.Pause();frontend.CaptureCurrentView(Path.Combine(output,variant+"-notes-9x16.png"),900,1600);r.captures++;
       frontend.CaptureCurrentView(Path.Combine(output,variant+"-notes-16x9.png"),1600,900);r.captures++;
       Require(room.CreatedViews>0&&session.SpawnedCount>0,"No original note views generated");r.movingNotes=true;captured=true;
      }))yield break;
      yield return null;room.Audio.Resume(); // Resume on a fresh Time.time frame after synchronous PNG encoding.
     }
     if(scripted&&!effectsCaptured&&session.Score.CurrentCombo>0&&room.Effects.FirstMusicalY>2)
     {
      float y=room.Effects.FirstMusicalY,feedbackAlpha=room.Feedback.Rating.color.a,feedbackScale=room.Feedback.Rating.rectTransform.localScale.x;int count=room.Effects.MusicalEmissions;
      if(!Step(r,()=>{Require(room.Effects.HitEmissions>0&&room.View.Find("Drum1").GetComponent<UnityEngine.UI.Image>()!=null,"Original Hit effect missing");room.Pause();room.Overlays.Hide();frontend.CaptureCurrentView(Path.Combine(output,"hit-musical-9x16.png"),900,1600);frontend.CaptureCurrentView(Path.Combine(output,"hit-musical-16x9.png"),1600,900);r.captures+=2;r.noteEffects=true;}))yield break;
      yield return new WaitForSecondsRealtime(.15f);
      if(!Step(r,()=>{Require(room.Effects.FirstMusicalY==y&&room.Effects.MusicalEmissions==count,"Paused VFX advanced or duplicated");Require(room.Feedback.Rating.color.a==feedbackAlpha&&room.Feedback.Rating.rectTransform.localScale.x==feedbackScale,"Paused rating animation advanced");r.effectsPauseFrozen=true;effectsCaptured=true;room.Resume();}))yield break;
     }
     yield return null;
    }
    if(!Step(r,()=>
    {
     Require(session.Finished&&room.Result!=null&&frontend.Phase==FrontendPhase.ResultRoom&&room.ResultView.Root.activeInHierarchy,"No ResultRoom: clock="+room.Audio.Clock.PlayingTime()+" playing="+room.Audio.Playing+" source="+room.Audio.Source.time+" phase="+frontend.Phase);
     Require(room.ResultDifficultyText.text==RecoveredDifficultyStyles.Label(room.StageDifficulty),"Result difficulty lost native style");
     File.WriteAllText(Path.Combine(output,caseName+"-result.json"),JsonUtility.ToJson(room.Result,true));
     File.WriteAllLines(Path.Combine(output,caseName+"-ledger.txt"),session.Records);
     if(replay!=null){r.maxScriptedInputLatenessMs=replay.MaxDeliveryDelayMs;File.WriteAllLines(Path.Combine(output,caseName+"-input-timing.txt"),replay.Timing);}
     File.WriteAllLines(Path.Combine(output,caseName+"-incomplete.txt"),session.Notes.Where(n=>n.Hold&&n.Checkpoints.Any(c=>!c.HasValue)).Select(n=>"node="+n.Action.NodeId+" head="+n.Head+" hit="+n.Hit+" missing="+string.Join(",",n.CheckpointTimes.Where((t,i)=>!n.Checkpoints[i].HasValue))));
     if(scripted){r.normalScriptedScore=room.Result.score;r.normalScriptedPerfect=room.Result.perfect;r.musicalEmissions=room.Effects.MusicalEmissions;r.hitEmissions=room.Effects.HitEmissions;Require(r.musicalEmissions==room.Result.perfect+room.Result.great+room.Result.good&&r.hitEmissions==session.ClickCount&&r.hitEmissions==session.Notes.Count,"Effects differ from accepted valid points/heads (miss excluded)");r.normalScriptedScore=room.Result.score;r.normalScriptedPerfect=room.Result.perfect;Require(room.Result.perfect==74&&room.Result.score==222&&room.Result.miss==0,"Scripted original normal chart failed Perfect oracle");r.normalScriptedResult=true;r.normalScriptedScore=room.Result.score;r.normalScriptedPerfect=room.Result.perfect;}
     else Require(room.Result.miss==session.Notes.Count&&room.Result.score==0&&session.ClickCount==0,"No-input result differs from ledger: clicks="+session.ClickCount+" misses="+room.Result.miss+" notes="+session.Notes.Count);
     Require(session.SpawnedCount==session.Notes.Count&&session.RecycledCount==session.Notes.Count,"Spawn/recycle lifecycle incomplete");
     if(variant=="normal"&&!scripted){r.normalResult=true;r.normalSpawned=session.SpawnedCount;r.normalRecycled=session.RecycledCount;r.normalMiss=room.Result.miss;r.normalScore=room.Result.score;}
     else if(variant=="fnf"){Require(session.Notes.All(n=>n.Action.hit_type==0),"FNF ALL was rewritten");r.fnfResult=true;r.fnfSpawned=session.SpawnedCount;r.fnfMiss=room.Result.miss;}
     File.WriteAllText(Path.Combine(output,caseName+"-result.json"),JsonUtility.ToJson(room.Result,true));
     File.WriteAllLines(Path.Combine(output,caseName+"-ledger.txt"),session.Records);
     frontend.CaptureCurrentView(Path.Combine(output,caseName+"-result-9x16.png"),900,1600);r.captures++;
     frontend.CaptureCurrentView(Path.Combine(output,caseName+"-result-16x9.png"),1600,900);r.captures++;
    }))yield break;
   }
   frontend.ShowMainRoom();frontend.PrepareLocalTutorial("elastic");float end=Time.realtimeSinceStartup+20;
   while((frontend.Phase==FrontendPhase.SongPreparation||frontend.Phase==FrontendPhase.StageCountdown)&&Time.realtimeSinceStartup<end)yield return null;
   if(!Step(r,()=>FreeplayPlayerValidation.ClickStart(frontend)))yield break;
   while(frontend.PlayRoom.State==RecoveredPlayState.Countdown&&Time.realtimeSinceStartup<end)yield return null;
   if(!Step(r,()=>{var room=frontend.PlayRoom;Require(frontend.Phase==FrontendPhase.PlayRoom&&room.Content.Definition.sid=="gEYv","Elastic loading failed");r.elasticActions=room.Session.Notes.Count;r.elasticAmbients=room.Content.Chart.ambients.Length;r.elasticSeconds=room.Content.Audio.length;Require(r.elasticActions==1319&&r.elasticAmbients==308,"Elastic content differs");r.elasticPrepared=true;}))yield break;
   var elasticRoom=frontend.PlayRoom;var elasticSession=elasticRoom.Session;
   var elasticReplay=new RecoveredStageReplay(elasticSession,elasticRoom.InputSource.State,elasticRoom.SampleInput);elasticRoom.BeforeSessionUpdate=elasticReplay.AdvanceTo;
   var holdDirections=new System.Collections.Generic.HashSet<int>();bool holdCaptured=false;end=Time.realtimeSinceStartup+225;
   // Exclude only deliberate PNG-capture pauses from the bounded playback wait.
   float capturePauseStart=0;int nextRoleCapture=0;int[] roleSamples={0,30000,75000,130000,175000};
   var lastRoleHold=elasticSession.Notes.Last(n=>n.Hold);int holdRoleCapture=0;
   var holdBfSprites=new System.Collections.Generic.HashSet<string>();var holdBossSprites=new System.Collections.Generic.HashSet<string>();
   var holdRoleFrames=new System.Collections.Generic.List<string>{"musicalMs,bfState,bfSprite,bossState,bossSprite,bfRequests,bossRequests,skipped"};
   File.WriteAllText(Path.Combine(output,"elastic-timing.txt"),"revision="+r.revision+" case=elastic-scripted\n");float elasticSampleAt=0;
   while(!elasticSession.Finished&&!elasticSession.Dead&&Time.realtimeSinceStartup<end)
   {
    float now=elasticRoom.Audio.Clock.PlayingTime();
    if(elasticRoom.Audio.Playing&&elasticRoom.Audio.Source.time>0)r.longRunClockAudioDifferenceMs=Math.Max(r.longRunClockAudioDifferenceMs,Math.Abs(now-elasticRoom.Audio.Source.time*1000));
    if(nextRoleCapture<roleSamples.Length&&now>=roleSamples[nextRoleCapture])
    {
     int sample=roleSamples[nextRoleCapture++];
     if(!Step(r,()=>
     {
      capturePauseStart=Time.realtimeSinceStartup;elasticRoom.Audio.Pause();var roles=elasticRoom.Roles;roles.Sample(now);
      Require(roles.Available&&roles.BundleMd5=="51d2a95d1a3f72c98bc78229e0c77fa8"&&roles.BfName!=null&&roles.BossName!=null,"Exact original roles absent in live Player");
      if(sample>0)Require(roles.BfFrames>1&&roles.BossFrames>1,"Original role sprites did not animate with AudioClock");
      string prefix=Path.Combine(output,"original-roles-t"+(sample/1000));
      frontend.CaptureCurrentView(prefix+"-9x16.png",900,1600);frontend.CaptureCurrentView(prefix+"-16x9.png",1600,900);r.captures+=2;r.roleCaptures+=2;
      File.WriteAllText(prefix+".txt","revision="+r.revision+" clockMs="+now+" audioMs="+(elasticRoom.Audio.Source.time*1000)+" bf="+roles.BfName+"/"+roles.BfState+" boss="+roles.BossName+"/"+roles.BossState+" bfFrames="+roles.BfFrames+" bossFrames="+roles.BossFrames+" camera="+roles.SceneCamera.transform.localPosition+" background="+roles.BackgroundIndex);
     }))yield break;
     yield return null;end+=Time.realtimeSinceStartup-capturePauseStart;elasticRoom.Audio.Resume();
    }
    if(Time.realtimeSinceStartup>=elasticSampleAt){elasticSampleAt=Time.realtimeSinceStartup+5;File.AppendAllText(Path.Combine(output,"elastic-timing.txt"),"wall="+Time.realtimeSinceStartup+" clock="+now+" audio="+elasticRoom.Audio.Source.time+" delta="+Time.deltaTime+"\n");}
    var held=elasticSession.Notes.FirstOrDefault(n=>n.Hold&&n.Head==BBPC.Gameplay.HitQuality.Perfect&&!n.Complete&&!holdDirections.Contains(n.Action.key)&&now>=n.Action.ts+n.Action.duration*.45f);
    if(held!=null)
    {
     if(!Step(r,()=>
     {
      capturePauseStart=Time.realtimeSinceStartup;elasticRoom.Audio.Pause();holdDirections.Add(held.Action.key);
      var shapes=elasticRoom.View.Root.GetComponentsInChildren<UnityEngine.UI.Image>().Where(i=>i.name=="HeadShapeChangeNode").ToArray();
      Require(shapes.Length>0,"Held note has no original Loop node");
      foreach(var shape in shapes)
      {
       Require(!shape.transform.parent.Find("HeadNode").gameObject.activeSelf,"Held arrow stayed frozen at receptor");
       Require(shape.sprite!=null&&Math.Abs(shape.rectTransform.parent.GetComponent<RectTransform>().anchoredPosition.y+shape.rectTransform.localPosition.y+373)<.1f,"Loop not at original judgement line");
      }
      var image=shapes[0];var sprite=image.sprite;var size=image.rectTransform.sizeDelta;var pivot=image.rectTransform.pivot;
      elasticRoom.Effects.SampleHold(image,held.Action.key,0);var first=image.sprite;elasticRoom.Effects.SampleHold(image,held.Action.key,.1f);
      Require(image.sprite!=first,"Loop sprite did not animate");image.sprite=sprite;image.rectTransform.sizeDelta=size;image.rectTransform.pivot=pivot;r.holdLoops=r.heldHeadHidden=true;
      frontend.CaptureCurrentView(Path.Combine(output,"hold-"+held.Action.key+"-held-9x16.png"),900,1600);
      frontend.CaptureCurrentView(Path.Combine(output,"hold-"+held.Action.key+"-held-16x9.png"),1600,900);r.captures+=2;
      File.WriteAllText(Path.Combine(output,"hold-"+held.Action.key+"-sample.txt"),"revision="+r.revision+" node="+held.Action.NodeId+" key="+held.Action.key+" chartTs="+held.Action.ts+" duration="+held.Action.duration+" clock="+now+" head="+held.Head+"\n");
     }))yield break;
     yield return null;end+=Time.realtimeSinceStartup-capturePauseStart;elasticRoom.Audio.Resume();
    }
    if(now>=lastRoleHold.Action.ts+500&&now<lastRoleHold.Action.ts+lastRoleHold.Action.duration-200)
    {
     var roles=elasticRoom.Roles;holdBfSprites.Add(roles.BfSprite);holdBossSprites.Add(roles.BossSprite);
     holdRoleFrames.Add(now+","+roles.BfState+","+roles.BfSprite+","+roles.BossState+","+roles.BossSprite+","+roles.BfRequests+","+roles.BossRequests+","+roles.HoldRefreshSkipped);
     if(holdRoleCapture<3&&now>=179000+holdRoleCapture*80)
     {
      if(!Step(r,()=>{capturePauseStart=Time.realtimeSinceStartup;elasticRoom.Audio.Pause();frontend.CaptureCurrentView(Path.Combine(output,"last-hold-role-"+holdRoleCapture+".png"),900,1600);holdRoleCapture++;r.captures++;}))yield break;
      yield return null;end+=Time.realtimeSinceStartup-capturePauseStart;elasticRoom.Audio.Resume();
     }
    }
    if(!holdCaptured&&now>=12400)
    {
     if(!Step(r,()=>{Require(elasticSession.SpawnedCount>0,"Elastic first hold missing");capturePauseStart=Time.realtimeSinceStartup;elasticRoom.Audio.Pause();frontend.CaptureCurrentView(Path.Combine(output,"elastic-first-hold-9x16.png"),900,1600);r.captures++;holdCaptured=true;}))yield break;
     yield return null;end+=Time.realtimeSinceStartup-capturePauseStart;elasticRoom.Audio.Resume();
    }
    yield return null;
   }
   if(!Step(r,()=>
   {
    var summary=elasticRoom.Result;int points=elasticSession.Notes.Sum(n=>n.Hold?1+n.CheckpointTimes.Length:1);
    r.elasticMaxScriptedInputLatenessMs=elasticReplay.MaxDeliveryDelayMs;
    File.WriteAllLines(Path.Combine(output,"elastic-ledger.txt"),elasticSession.Records);File.WriteAllLines(Path.Combine(output,"elastic-input-timing.txt"),elasticReplay.Timing);
    if(elasticSession.Dead)r.elasticDeathTimeMs=elasticSession.TimeMs;
    File.WriteAllText(Path.Combine(output,"elastic-diagnostic.json"),JsonUtility.ToJson(elasticSession.Summary(),true));
    Require(elasticSession.Finished&&summary!=null&&frontend.Phase==FrontendPhase.ResultRoom,"Elastic did not reach ResultRoom: phase="+frontend.Phase+" time="+elasticSession.TimeMs+" dead="+elasticSession.Dead+" maxInputLatenessMs="+r.elasticMaxScriptedInputLatenessMs+" summary="+JsonUtility.ToJson(elasticSession.Summary()));
    File.WriteAllText(Path.Combine(output,"elastic-result.json"),JsonUtility.ToJson(summary,true));File.WriteAllLines(Path.Combine(output,"elastic-ledger.txt"),elasticSession.Records);
    File.WriteAllLines(Path.Combine(output,"elastic-incomplete-checkpoints.txt"),elasticSession.Notes.Where(n=>n.Hold&&n.Checkpoints.Any(c=>!c.HasValue)).Select(n=>"node="+n.Action.NodeId+" ts="+n.Action.ts+" head="+n.Head+" hit="+n.Hit+" missing="+string.Join(",",n.CheckpointTimes.Where((t,i)=>!n.Checkpoints[i].HasValue))));
    Require(summary.miss==0&&summary.perfect==points&&summary.score==points*3,"Elastic scripted ledger differs from Perfect oracle: "+JsonUtility.ToJson(summary));
    Require(elasticSession.SpawnedCount==1319&&elasticSession.RecycledCount==1319&&elasticSession.AmbientCursor==308&&elasticSession.CameraEvents==17&&elasticSession.BgmStartEvents==1&&elasticSession.BgmEndEvents==2,"Elastic lifecycle/timeline mismatch");
    Require(elasticRoom.StageDifficulty==6&&elasticRoom.ResultDifficultyText.text==RecoveredDifficultyStyles.Label(6),"Elastic ResultRoom confused legacy Insane with Ultimate");
    r.elasticResult=true;r.elasticPerfect=summary.perfect;r.elasticScore=summary.score;r.elasticCameraEvents=elasticSession.CameraEvents;r.elasticAmbientCursor=elasticSession.AmbientCursor;
    File.WriteAllText(Path.Combine(output,"elastic-result.json"),JsonUtility.ToJson(summary,true));File.WriteAllLines(Path.Combine(output,"elastic-ledger.txt"),elasticSession.Records);
    frontend.CaptureCurrentView(Path.Combine(output,"elastic-result-9x16.png"),900,1600);r.captures++;
    frontend.CaptureCurrentView(Path.Combine(output,"elastic-result-16x9.png"),1600,900);r.captures++;
    Require(r.roleCaptures==10&&elasticRoom.Roles.CameraChanges==17,"Original role/camera live Player validation incomplete");
    File.WriteAllLines(Path.Combine(output,"last-hold-role-frames.csv"),holdRoleFrames);
    Require(elasticRoom.Roles.HoldRefreshSkipped>0&&holdRoleCapture==3,"Original hold alternation absent in live Player");
    int role=lastRoleHold.RoleAnimation;
    if(role==0||role==2||role==6||role==7)Require(holdBfSprites.Count>1,"BF frozen during live Player final hold");
    if(role==0||role==1||role==5||role==7)Require(holdBossSprites.Count>1,"Boss frozen during live Player final hold");
    r.originalRolesRendered=true;r.originalCameraDispatched=true;
    File.WriteAllText(Path.Combine(output,"elastic-stage.json"),"{\"sid\":\"gEYv\",\"actions\":"+r.elasticActions+",\"ambients\":"+r.elasticAmbients+",\"actor_render_verified\":true,\"camera_dispatch_verified\":true,\"android_timing_equivalence_verified\":false}");
    Require(r.longRunClockAudioDifferenceMs<=150,"Long session audio/clock diagnostic bound exceeded: "+r.longRunClockAudioDifferenceMs);
    frontend.ShowMainRoom();r.physicalInputVerified=false;r.success=true;
   }))yield break;
  }
 }
}
