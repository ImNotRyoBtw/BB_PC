using System;
namespace BBPC.Gameplay
{
    public static class HoldCheckpointSchedule
    {
        // 0x2CFA3F0: double arithmetic after loading the chart's float32 BPM; truncation to int32.
        public static int[] Create(int timestamp,int duration,float bpm)
        {
            if(timestamp<0 || duration<0 || float.IsNaN(bpm) || float.IsInfinity(bpm) || bpm<=0) throw new ArgumentOutOfRangeException();
            int interval=(int)((60000.0/(double)bpm)*0.25);
            if(interval<1) throw new ArgumentOutOfRangeException(nameof(bpm),"Checkpoint interval must be positive");
            int count=duration/interval;
            if(count<2) return new[]{Math.Max(timestamp,checked(timestamp+duration-16))}; // GameDetailConstant static +0 =16.
            if(count>1000000) throw new ArgumentOutOfRangeException(nameof(duration));
            var checkpoints=new int[count]; for(int i=0;i<count;i++) checkpoints[i]=checked(timestamp+interval*(i+1));
            return checkpoints;
        }
    }
}
