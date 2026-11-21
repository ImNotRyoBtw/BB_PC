using System;
using BBPC.Gameplay;
using UnityEngine;
namespace BBPC.UnityRuntime
{
    public sealed class WindowsLaneInput : MonoBehaviour
    {
        public readonly PersistentLaneInput State=new PersistentLaneInput();
        public Action<LaneContact> Sample;
        public Func<float> ReadPlayingTime;
        public Camera ViewportCamera;
        public bool Reading;
        // Automated replays use Sample directly. Do not mix desktop typing with their oracle.
        public bool ReadHardware=true;
        public int Samples { get; private set; }
        private static readonly KeyCode[] keys={KeyCode.D,KeyCode.F,KeyCode.J,KeyCode.K};
        private void Emit(LaneContact contact) { Samples++;Sample?.Invoke(contact); }
        private void Update()
        {
            if(!Reading || !ReadHardware || ReadPlayingTime==null || ViewportCamera==null)return;
            float now=ReadPlayingTime();
            for(int key=0;key<4;key++)
            {
                if(Input.GetKey(keys[key])) Emit(State.HasSlot(key)?State.Continue(key,key):State.Begin(key,key,now));
                else if(State.HasSlot(key)) Emit(State.End(key));
            }
            Rect rect=ViewportCamera.pixelRect;Vector2 point=Input.mousePosition;
            bool inside=rect.Contains(point);
            if(Input.GetMouseButton(0) && inside)
            {
                int lane=Mathf.Clamp((int)((point.x-rect.x)/rect.width*4),0,3);
                Emit(State.HasSlot(4)?State.Continue(4,lane):State.Begin(4,lane,now));
            }
            else if(State.HasSlot(4)) Emit(State.End(4,!inside));
        }
        public void CancelContacts()
        { for(int slot=0;slot<5;slot++)if(State.HasSlot(slot))Emit(State.End(slot,true)); }
        private void OnApplicationFocus(bool focus) { if(!focus)Reading=false; } // Controller freezes the session; contacts are sampled on explicit resume.
        private void OnDisable() { CancelContacts(); }
    }
}
