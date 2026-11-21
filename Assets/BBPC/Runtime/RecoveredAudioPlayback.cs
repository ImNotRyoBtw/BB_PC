using System;
using BBPC.Gameplay;
using UnityEngine;
namespace BBPC.UnityRuntime
{
    public sealed class RecoveredAudioPlayback : MonoBehaviour
    {
        public readonly AudioClockState State=new AudioClockState();
        public RecoveredAudioClock Clock { get; private set; }
        public AudioSource Source { get; private set; }
        public bool Playing { get; private set; }
        public bool PauseOnFocusLoss=true;
        public bool PreparedPlaybackStarted => preheated;
        private bool preheated,pausing;
        private void Awake() { Initialize(); }
        public void Initialize() { if(Source!=null)return; Source=gameObject.AddComponent<AudioSource>();Source.playOnAwake=false;Source.loop=false;Clock=UnityAudioClock.CreateForWindowsPlayback(State); }
        public void Prepare(AudioClip clip)
        {
            if(clip==null) throw new ArgumentNullException(nameof(clip));
            Source.Stop();Source.clip=clip;Playing=false;pausing=false;preheated=false;
            State.Mode=0;State.DspStartTime=-1;State.TimeStartTime=-1;State.PlayedDuration=0;
        }
        public void Play()
        {
            if(Source.clip==null) throw new InvalidOperationException("Audio not prepared");if(Playing)return;
            Playing=true;
            if(!preheated)
            {
                // PreheatPlay 0x2CE7DF8 and PlayRoomConstants..cctor 0x2CE9490: scheduled +1 second.
                State.TimeStartTime=(double)Clock.GetTime();State.DspStartTime=AudioSettings.dspTime+1.0;Source.PlayScheduled(State.DspStartTime);preheated=true;
            }
            else { State.DspStartTime=AudioSettings.dspTime;State.TimeStartTime=(double)Clock.GetTime();Source.Play(); }
        }
        public void Pause()
        {
            if(!Playing)return;
            State.PlayedDuration+=AudioSettings.dspTime-State.DspStartTime;State.DspStartTime=-1;State.TimeStartTime=-1;
            Source.Pause();Playing=false;pausing=true; // Native normal Pause 0x2CE81F0.
        }
        public void Resume()
        {
            if(Playing || Source.clip==null)return;
            if(State.PlayedDuration<0){double wait=-State.PlayedDuration;State.PlayedDuration=0;State.DspStartTime=AudioSettings.dspTime+wait;State.TimeStartTime=(double)Clock.GetTime();Source.Stop();Source.PlayScheduled(State.DspStartTime);Playing=true;pausing=false;return;}
            Playing=true;State.DspStartTime=AudioSettings.dspTime;State.TimeStartTime=(double)Clock.GetTime();
            float target=Source.time;
            if((target<=0 && State.PlayedDuration>0) || target>=Source.clip.length)target=Source.clip.length-0.1f;
            Source.time=target;State.PlayedDuration=(double)target;
            if(pausing)Source.UnPause();else Source.Play();pausing=false;
        }
        public void StopPlayback() { Source.Stop();Playing=false;State.DspStartTime=-1;State.TimeStartTime=-1;State.PlayedDuration=0; }
        private void OnApplicationFocus(bool focus) { if(!focus && Playing && PauseOnFocusLoss)Pause(); }
    }
}
