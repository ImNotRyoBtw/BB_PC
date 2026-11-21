using System;using System.IO;using System.Collections;using System.Linq;using UnityEngine;using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
 public static class FreeplayPlayerValidation
 {
  [Serializable] public sealed class Result {public bool success,startButton,startWait,ghostTap,focusResume,countdown,pausedCountdown,pausedPreheat,resumedPreheat,death,retry,exit;public string revision,error;public int blood,maximum,miss,captures;public double resumedDspWaitMs,resumedClockMs,resumedSourceSeconds,resumedPlayedSeconds;}
  static bool Step(Result r,Action a){try{a();return true;}catch(Exception e){r.error=e.ToString();Debug.LogException(e);return false;}}
  static void Require(bool b,string m){if(!b)throw new InvalidOperationException(m);}
  public static void ClickStart(RecoveredFrontend frontend)
  {
   Canvas.ForceUpdateCanvases();var button=frontend.PlayRoom.Overlays.StartButton;
   Require(button.gameObject.activeInHierarchy&&button.interactable,"Original central StartButton is hidden or disabled");
   var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(frontend.PortraitCamera,button.transform.position),button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
   var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
   Require(hits.Count>0&&UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"Central StartButton raycast obstructed: "+(hits.Count==0?"none":hits[0].gameObject.name));
   UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
  }
  public static IEnumerator Run(RecoveredFrontend frontend,string output,Result r)
  {
   Directory.CreateDirectory(output);r.revision=RecoveredFrontend.Revision;
   if(!Step(r,()=>{frontend.ShowMainRoom();frontend.PrepareLocalTutorial("normal");}))yield break;
   float limit=Time.realtimeSinceStartup+15;
   while(frontend.Phase==FrontendPhase.SongPreparation&&Time.realtimeSinceStartup<limit)yield return null;
   var room=frontend.PlayRoom;
   if(!Step(r,()=>{Require(room.State==RecoveredPlayState.Prepared&&!room.Audio.Playing&&!room.InputSource.Reading,"Prepared stage started automatically");frontend.CaptureCurrentView(Path.Combine(output,"start-button-9x16.png"),900,1600);frontend.CaptureCurrentView(Path.Combine(output,"start-button-16x9.png"),1600,900);r.captures+=2;}))yield break;
   yield return new WaitForSecondsRealtime(.2f);
   if(!Step(r,()=>{Require(room.State==RecoveredPlayState.Prepared&&room.Audio.Clock.PlayingTime()==0,"Start gate did not wait");r.startWait=true;ClickStart(frontend);r.startButton=true;}))yield break;
   if(!Step(r,()=>{Require(room.State==RecoveredPlayState.Countdown&&!room.Audio.Playing&&!room.InputSource.Reading,"Music/input began before original countdown");Require(room.Session.Score.Blood==10&&!room.IsLocalSandbox,"Production tutorial HP must come from manifest");}))yield break;
   while(room.Countdown.Remaining>3&&Time.realtimeSinceStartup<limit)yield return null;
   if(!Step(r,()=>{Require(room.Countdown.Remaining==3,"Ready sprite interval missing");Require(room.Overlays.CountdownView.Find("SpriteImage").GetComponent<Image>().sprite==RecoveredUiBuilder.Asset<Sprite>(room.Overlays.Data,"sharedassets2.assets:193"),"Original Ready sprite missing");frontend.CaptureCurrentView(Path.Combine(output,"countdown-ready-9x16.png"),900,1600);r.captures++;room.Pause();Require(room.State==RecoveredPlayState.Paused,"Countdown pause failed");r.countdown=true;frontend.CaptureCurrentView(Path.Combine(output,"countdown-paused-9x16.png"),900,1600);r.captures++;}))yield break;
   double elapsed=room.Countdown.Elapsed;yield return new WaitForSecondsRealtime(.15f);
   if(!Step(r,()=>{Require(room.Countdown.Elapsed==elapsed&&!room.Audio.Playing,"Paused countdown advanced");r.pausedCountdown=true;ClickStart(frontend);}))yield break;
   limit=Time.realtimeSinceStartup+10;while(room.State==RecoveredPlayState.Countdown&&Time.realtimeSinceStartup<limit)yield return null;
   if(!Step(r,()=>{Require(room.State==RecoveredPlayState.Playing&&room.Audio.State.DspStartTime>AudioSettings.dspTime&&room.Audio.Clock.PlayingTime()==0,"Expected original audio preheat after countdown");room.Overlays.PauseButton.onClick.Invoke();Require(room.State==RecoveredPlayState.Paused&&room.Audio.State.PlayedDuration<0,"Preheat pause lost scheduled delay");frontend.CaptureCurrentView(Path.Combine(output,"pause-16x9.png"),1600,900);r.captures++;}))yield break;
   float frozen=room.Audio.Clock.PlayingTime();yield return new WaitForSecondsRealtime(.15f);
   if(!Step(r,()=>{Require(room.Audio.Clock.PlayingTime()==frozen&&!room.InputSource.Reading,"Paused clock/input moved");r.pausedPreheat=true;ClickStart(frontend);}))yield break;
   limit=Time.realtimeSinceStartup+10;while(room.State==RecoveredPlayState.Countdown&&Time.realtimeSinceStartup<limit)yield return null;
   if(!Step(r,()=>{r.resumedDspWaitMs=(room.Audio.State.DspStartTime-AudioSettings.dspTime)*1000;r.resumedClockMs=room.Audio.Clock.PlayingTime();r.resumedSourceSeconds=room.Audio.Source.time;r.resumedPlayedSeconds=room.Audio.State.PlayedDuration;Require(room.State==RecoveredPlayState.Playing&&r.resumedDspWaitMs>0&&r.resumedClockMs==0&&r.resumedSourceSeconds<1,"Resume preheat: state="+room.State+" waitMs="+r.resumedDspWaitMs+" clock="+r.resumedClockMs+" source="+r.resumedSourceSeconds+" played="+r.resumedPlayedSeconds);r.resumedPreheat=true;}))yield break;
   if(!Step(r,()=>
   {
    int score=room.Session.Score.Score,combo=room.Session.Score.CurrentCombo,musical=room.Effects.MusicalEmissions,hit=room.Effects.HitEmissions;
    for(int key=0;key<4;key++){var contact=room.InputSource.State.Begin(200+key,key,0);room.InputSource.Sample(contact);room.InputSource.Sample(room.InputSource.State.Continue(200+key,key));room.InputSource.Sample(room.InputSource.State.End(200+key));}
    Require(room.Effects.HitEmissions==hit+4&&room.Effects.MusicalEmissions==musical&&room.Session.Score.Score==score&&room.Session.Score.CurrentCombo==combo,"Ghost tapping changed scoring or failed to animate");
    room.Effects.Advance(.05f);for(int key=1;key<=4;key++)Require(room.View.Find("Drum"+key).GetComponent<Image>().color.a>0,"Ghost effect invisible");
    room.Audio.Pause();frontend.CaptureCurrentView(Path.Combine(output,"ghost-tapping-9x16.png"),900,1600);r.captures++;r.ghostTap=true;
   }))yield break;
   yield return null;room.Audio.Resume();
   if(!Step(r,()=>{room.Audio.PauseOnFocusLoss=true;room.SendMessage("OnApplicationFocus",false);Require(room.State==RecoveredPlayState.Paused,"Focus loss did not pause");room.SendMessage("OnApplicationFocus",true);Require(room.State==RecoveredPlayState.Paused&&room.Overlays.StartRoot.activeInHierarchy,"Focus return auto-resumed or hid central button");room.Audio.PauseOnFocusLoss=false;}))yield break;
   yield return null; // A user cannot click a reactivated Graphic before its first rendered frame.
   if(!Step(r,()=>{ClickStart(frontend);r.focusResume=true;}))yield break;
   limit=Time.realtimeSinceStartup+10;while(room.State==RecoveredPlayState.Countdown&&Time.realtimeSinceStartup<limit)yield return null;
   limit=Time.realtimeSinceStartup+60;while(room.State==RecoveredPlayState.Playing&&Time.realtimeSinceStartup<limit)yield return null;
   if(!Step(r,()=>{Require(room.State==RecoveredPlayState.Dead&&frontend.Phase==FrontendPhase.Defeat&&!room.Audio.Playing&&!room.InputSource.Reading,"Production death failed to stop music/input");Require(room.Result==null&&!room.ResultView.Root.activeInHierarchy,"Death falsely entered ResultRoom");r.blood=room.Session.Score.Blood;r.maximum=room.Session.Score.CurrentMaxBlood;r.miss=room.Session.Summary().miss;Require(r.blood==0&&r.maximum==10&&r.miss==10,"Production HP/miss mismatch");Require(room.Overlays.DeathView.Root.activeInHierarchy,"Original death options hidden");foreach(var shape in new[]{new[]{900,1600},new[]{1600,900}}){frontend.CaptureCurrentView(Path.Combine(output,"death-"+(shape[0]==900?"9x16":"16x9")+".png"),shape[0],shape[1]);r.captures++;}r.death=true;}))yield break;
   if(!Step(r,()=>
   {
    var session=room.Session;bool revived=false;Action onRevived=()=>revived=true;
    room.Overlays.Revived+=onRevived;room.Overlays.RequestRevive=ok=>ok(true);
    try
    {
     var revive=room.Overlays.DeathView.Find("CountinuePlayButton").GetComponent<Button>();
     Require(revive.gameObject.activeInHierarchy&&revive.interactable,"Original revive inaccessible");revive.onClick.Invoke();
     Require(revived&&room.Session==session&&!room.Session.Dead&&room.State==RecoveredPlayState.Countdown&&frontend.Phase==FrontendPhase.StageCountdown&&!room.Overlays.DeathView.Root.activeInHierarchy,"Original revive did not resume the existing stage");
     r.retry=true; // Legacy serialized flag: validates the original revive, not a stage restart.
    }
    finally{room.Overlays.RequestRevive=null;room.Overlays.Revived-=onRevived;}
    room.Overlays.Exit();Require(frontend.Phase==FrontendPhase.MainRoom&&room.Session==null&&!room.Audio.Playing,"Exit did not release stage/audio");r.exit=true;r.success=true;
   }))yield break;
  }
 }
}
