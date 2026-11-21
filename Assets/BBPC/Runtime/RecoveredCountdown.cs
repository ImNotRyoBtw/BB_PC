using System;
namespace BBPC.UnityRuntime
{
    // ShowCountDownStartNode 0x2D1A9E8: int((60f/bpm)*1000f), four intervals.
    // b__19_0 0x2D0DC24: remaining=4-(tick+1); the initial image is blank.
    public sealed class RecoveredCountdown
    {
        public readonly int IntervalMs;
        public int Remaining {get;private set;}=4;
        public bool Paused,Completed;
        public double Elapsed {get;private set;}
        public Action<int> Changed;
        public Action Finished;
        public RecoveredCountdown(float bpm)
        { if(bpm<=0||float.IsNaN(bpm)||float.IsInfinity(bpm))throw new ArgumentOutOfRangeException(nameof(bpm));IntervalMs=(int)((60f/bpm)*1000f);if(IntervalMs<1)throw new ArgumentOutOfRangeException(nameof(bpm)); }
        public void Advance(double deltaMs)
        {
            if(deltaMs<0||double.IsNaN(deltaMs)||double.IsInfinity(deltaMs))throw new ArgumentOutOfRangeException(nameof(deltaMs));
            if(Paused||Completed)return;Elapsed+=deltaMs;
            while(Remaining>0&&Elapsed>=(5-Remaining)*IntervalMs){Remaining--;Changed?.Invoke(Remaining);}
            if(Remaining==0&&!Completed){Completed=true;Finished?.Invoke();}
        }
        public static int SpriteIndex(int remaining)=>remaining==3?1:remaining==2?2:(remaining==1||remaining==0)?3:0;
    }
}
