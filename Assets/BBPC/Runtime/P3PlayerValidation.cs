using System;
using System.IO;
using System.Collections;
using BBPC.Gameplay;
using UnityEngine;
namespace BBPC.UnityRuntime
{
    public static class P3PlayerValidation
    {
        [Serializable] public sealed class Result
        {
            public bool success,loadingObserved,normalPairPrepared,fnfPairPrepared,clockAdvanced,pauseFrozen,resumeAdvanced,syntheticInputIdentity,clockAligned,playroomLayoutReady,gameplayReady;
            public float normalSeconds,fnfSeconds,normalFrameDurationDifference,fnfFrameDurationDifference,clockAudioDifferenceMs;
            public int normalActions,fnfActions,captures,physicalInputSamples;
            public string revision,error;
        }
        private static bool Step(Result result,Action action)
        { try{action();return true;}catch(Exception error){result.error=error.ToString();Debug.LogException(error);return false;} }
        private static void Require(bool condition,string error){if(!condition)throw new InvalidOperationException(error);}
        public static IEnumerator Run(RecoveredFrontend frontend,string output,Result result)
        {
            Directory.CreateDirectory(output);result.revision=RecoveredFrontend.Revision;
            Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;frontend.PlayRoom.Audio.PauseOnFocusLoss=false;frontend.PlayRoom.InputSource.ReadHardware=false; // Explicit unattended validation, never the interactive default.
            if(!Step(result,()=>
            {
                frontend.PrepareLocalTutorial("normal");
                result.loadingObserved=(frontend.Phase==FrontendPhase.SongPreparation||frontend.Phase==FrontendPhase.StageCountdown)&&frontend.Loading.Root.activeInHierarchy;
                Require(result.loadingObserved,"Local content did not enter real song Loading");
                frontend.CaptureCurrentView(Path.Combine(output,"loading-entry-9x16.png"),900,1600);result.captures++;
            }))yield break;
            float start=Time.realtimeSinceStartup;
            while((frontend.Phase==FrontendPhase.SongPreparation||frontend.Phase==FrontendPhase.StageCountdown)&&Time.realtimeSinceStartup-start<15)yield return null;
            if(!Step(result,()=>FreeplayPlayerValidation.ClickStart(frontend)))yield break;
            while(frontend.PlayRoom.State==RecoveredPlayState.Countdown&&Time.realtimeSinceStartup-start<15)yield return null;
            if(!Step(result,()=>
            {
                Require(frontend.Phase==FrontendPhase.PlayRoom&&frontend.PlayRoom.Content!=null,frontend.TutorialPreparationError??"Normal tutorial preparation timed out");
                var content=frontend.PlayRoom.Content;result.normalSeconds=content.Audio.length;result.normalActions=content.Chart.actions.Length;
                // Unity MPEG reports an estimated clip length. Exact source identity is checked by SHA256 before decoding.
                // Independent frame scan: 2216*1152/48000=53.184s; record decoder discrepancy explicitly.
                result.normalFrameDurationDifference=result.normalSeconds-53.184f;
                result.normalPairPrepared=content.Definition.sid=="beginner_guider"&&result.normalActions==46&&content.Audio.frequency==48000&&content.Audio.channels==2&&Math.Abs(result.normalFrameDurationDifference)<1f&&result.normalSeconds*1000>=content.Chart.LastEndMs;
                Require(result.normalPairPrepared,"Packaged normal pair metadata mismatch");

            }))yield break;
            yield return new WaitForSecondsRealtime(2.2f);
            long paused=0;
            if(!Step(result,()=>
            {
                var audio=frontend.PlayRoom.Audio;result.clockAdvanced=audio.Clock.PlayingTimestamp()>500&&audio.Source.time>0;
                Require(result.clockAdvanced,"Actual local audio/clock did not advance");
                result.clockAudioDifferenceMs=Math.Abs(audio.Clock.PlayingTime()-audio.Source.time*1000);
                result.clockAligned=result.clockAudioDifferenceMs<=150f; // Diagnostic audio/clock tolerance, never a judgement window.
                Require(result.clockAligned,"Audio/clock drift="+result.clockAudioDifferenceMs+" time="+Time.time+" timeStart="+audio.State.TimeStartTime+" dsp="+AudioSettings.dspTime+" dspStart="+audio.State.DspStartTime+" source="+audio.Source.time);
                var input=frontend.PlayRoom.InputSource;var contact=input.State.Begin(0,1,audio.Clock.PlayingTime());input.Sample(contact);
                result.syntheticInputIdentity=ReferenceEquals(contact,input.State.Continue(0,1))&&ReferenceEquals(contact,input.State.End(0));input.Sample(contact);
                Require(result.syntheticInputIdentity&&frontend.PlayRoom.LastInputPhase==LaneInputPhase.Ended,"Input adapter did not preserve press identity");
                result.physicalInputSamples=input.Samples;
                audio.Pause();paused=audio.Clock.PlayingTimestamp();
                // RenderTexture PNG work blocks Time.time frames; capture while paused, not during clock alignment measurement.
                if(frontend.inspectionMode)frontend.ToggleInspection();
                frontend.CaptureCurrentView(Path.Combine(output,"playroom-9x16.png"),900,1600);result.captures++;
                frontend.CaptureCurrentView(Path.Combine(output,"playroom-16x9.png"),1600,900);result.captures++;
                result.playroomLayoutReady=true;
                foreach(var key in new[]{"KeyBoard1","KeyBoard2","KeyBoard3","KeyBoard4"})
                {
                    var rt=frontend.PlayRoom.View.Find(key).GetComponent<RectTransform>();var corners=new Vector3[4];rt.GetWorldCorners(corners);
                    foreach(var corner in corners)if(!frontend.PortraitCamera.pixelRect.Contains(RectTransformUtility.WorldToScreenPoint(frontend.PortraitCamera,corner)))result.playroomLayoutReady=false;
                }
            }))yield break;
            yield return new WaitForSecondsRealtime(0.2f);
            if(!Step(result,()=>{var audio=frontend.PlayRoom.Audio;result.pauseFrozen=audio.Clock.PlayingTimestamp()==paused;Require(result.pauseFrozen,"Paused clock advanced");audio.Resume();}))yield break;
            yield return new WaitForSecondsRealtime(0.25f);
            if(!Step(result,()=>{result.resumeAdvanced=frontend.PlayRoom.Audio.Clock.PlayingTimestamp()>paused;Require(result.resumeAdvanced,"Resumed clock did not advance");frontend.ShowMainRoom();frontend.PrepareLocalTutorial("fnf");}))yield break;
            start=Time.realtimeSinceStartup;
            while((frontend.Phase==FrontendPhase.SongPreparation||frontend.Phase==FrontendPhase.StageCountdown)&&Time.realtimeSinceStartup-start<15)yield return null;
            if(!Step(result,()=>
            {
                Require(frontend.PlayRoom.Content!=null,frontend.TutorialPreparationError??"FNF tutorial preparation timed out");
                var content=frontend.PlayRoom.Content;result.fnfSeconds=content.Audio.length;result.fnfActions=content.Chart.actions.Length;
                // Independent frame scan: 2493*1152/44100=65.1232653s. This is not a remote integrity hash.
                result.fnfFrameDurationDifference=result.fnfSeconds-65.1232653f;
                result.fnfPairPrepared=result.fnfActions==56&&content.Audio.frequency==44100&&content.Audio.channels==2&&Math.Abs(result.fnfFrameDurationDifference)<1f&&result.fnfSeconds*1000>=content.Chart.LastEndMs;
                Require(result.fnfPairPrepared,"Packaged FNF pair metadata mismatch");frontend.ShowMainRoom();
                if(frontend.inspectionMode)frontend.ToggleInspection();
                result.gameplayReady=false; // Physical input, actors/camera/animation and complete original mode orchestration are not validated.
                result.success=true;
            }))yield break;
            var pipeline=new P4PlayerValidation.Result();string path=Path.GetFullPath(Path.Combine(output,"../../p3-gameplay/player/"+RecoveredFrontend.Revision));
            yield return P4PlayerValidation.Run(frontend,path,pipeline);
            File.WriteAllText(Path.Combine(path,"gameplay-results.json"),JsonUtility.ToJson(pipeline,true));
            if(!pipeline.success){result.success=false;result.error=pipeline.error;}
        }
    }
}
