using System;
using System.Collections.Generic;
namespace BBPC.Gameplay
{
    public enum LaneInputPhase { Began=0, Moved=1, Stationary=2, Ended=3, Canceled=4 }
    public sealed class LaneContact
    {
        public readonly int FingerId;
        public int Key { get; internal set; }
        public LaneInputPhase Phase { get; internal set; }
        public readonly double BeganTimestamp;
        internal LaneContact(int finger,int key,double time) { FingerId=finger;Key=key;BeganTimestamp=time;Phase=LaneInputPhase.Began; }
        public bool Ended => Phase==LaneInputPhase.Ended || Phase==LaneInputPhase.Canceled;
    }
    // Windows input adapter: one stable touch object per press, rather than a new touch every frame.
    public sealed class PersistentLaneInput
    {
        private readonly Dictionary<int,LaneContact> active=new Dictionary<int,LaneContact>();
        private int nextFingerId;
        public int ActiveCount => active.Count;
        public LaneContact Begin(int slot,int key,double time)
        {
            ValidateKey(key);
            if(active.ContainsKey(slot)) throw new InvalidOperationException("Input slot already held");
            var contact=new LaneContact(checked(++nextFingerId),key,time);active.Add(slot,contact);return contact;
        }
        public LaneContact Continue(int slot,int key)
        {
            ValidateKey(key);if(!active.TryGetValue(slot,out var contact)) throw new InvalidOperationException("Input slot not held");
            contact.Phase=contact.Key==key?LaneInputPhase.Stationary:LaneInputPhase.Moved;contact.Key=key;return contact;
        }
        public LaneContact End(int slot,bool canceled=false)
        {
            if(!active.TryGetValue(slot,out var contact)) throw new InvalidOperationException("Input slot not held");
            active.Remove(slot);contact.Phase=canceled?LaneInputPhase.Canceled:LaneInputPhase.Ended;return contact;
        }
        public bool HasSlot(int slot) => active.ContainsKey(slot);
        private static void ValidateKey(int key) { if(key<0 || key>3) throw new ArgumentOutOfRangeException(nameof(key)); }
    }
}
