using System;
using System.Collections.Generic;
using BBPC.Gameplay;
namespace BBPC.UnityRuntime
{
    // Test oracle, never enabled by normal gameplay. Frame stalls must not rewrite recorded input times.
    public sealed class RecoveredStageReplay
    {
        private struct Event { public int Time,Kind;public RecoveredStageNote Note; }
        private readonly List<Event> events=new List<Event>();
        private readonly RecoveredStageSession session;
        private readonly PersistentLaneInput input;
        private readonly Action<LaneContact,float> sample;
        private int cursor;
        public float MaxDeliveryDelayMs {get;private set;}
        public int Delivered {get;private set;}
        public readonly List<string> Timing=new List<string>();
        public RecoveredStageReplay(RecoveredStageSession session,PersistentLaneInput input,Action<LaneContact,float> sample)
        {
            this.session=session;this.input=input;this.sample=sample;
            foreach(var note in session.Notes)
            {
                if(!note.Human)continue;
                events.Add(new Event{Time=note.Action.ts,Kind=0,Note=note});
                foreach(int time in note.CheckpointTimes)events.Add(new Event{Time=time,Kind=1,Note=note});
                events.Add(new Event{Time=note.Action.ts+note.Action.duration+2,Kind=2,Note=note});
            }
            events.Sort((a,b)=>{int order=a.Time.CompareTo(b.Time);if(order==0)order=a.Kind.CompareTo(b.Kind);return order!=0?order:a.Note.Action.NodeId.CompareTo(b.Note.Action.NodeId);});
        }
        public void AdvanceTo(float frameTime)
        {
            while(cursor<events.Count&&events[cursor].Time<=frameTime&&!session.Dead&&!session.Finished)
            {
                var e=events[cursor++];int slot=1000+e.Note.Action.NodeId;
                MaxDeliveryDelayMs=Math.Max(MaxDeliveryDelayMs,frameTime-e.Time);
                // Advance spawning/deadlines chronologically, before submitting the recorded contact.
                session.ComponentsUpdate(e.Time);
                var contact=e.Kind==0?input.Begin(slot,e.Note.Action.key,e.Time):e.Kind==1?input.Continue(slot,e.Note.Action.key):input.End(slot);
                sample(contact,e.Time);Delivered++;
                if(e.Kind==0)Timing.Add("node="+e.Note.Action.NodeId+" ts="+e.Time+" input="+e.Time+" deliveredAt="+frameTime);
            }
        }
    }
}
