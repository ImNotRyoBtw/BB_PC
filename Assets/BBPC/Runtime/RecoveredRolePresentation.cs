using System;
using System.Collections.Generic;
using System.Linq;
using BBPC.Gameplay;
using UnityEngine;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    // Windows rendering adapter for original prefabs/clips; session remains the only judgement/timeline owner.
    public sealed class RecoveredRolePresentation : IDisposable
    {
        private sealed class Actor
        {
            public GameObject root;public Animator animator;public RecoveredSpriteAnimator dynamicAnimator;public string state="idle";public float started,nextIdle;public int requests,frames;public Sprite previous;public bool girlDance,nextLeft;
            public RecoveredSpriteAnimator[] passive=new RecoveredSpriteAnimator[0];public float[] passiveTimes=new float[0];public bool[] passiveActive=new bool[0];public float passiveLast;
            public string IdleState=>dynamicAnimator!=null?dynamicAnimator.DefaultState:"idle";
            public Sprite Sprite=>dynamicAnimator!=null?dynamicAnimator.CurrentSprite:animator?.GetComponent<SpriteRenderer>()?.sprite;
            public void Play(string name,float time){if(dynamicAnimator!=null){if(dynamicAnimator.Play(name)){state=name;started=time;}requests++;return;}if(animator==null)return;int hash=Animator.StringToHash("Base Layer."+name);if(!animator.HasState(0,hash))throw new InvalidOperationException("Original animator state absent: "+name);state=name;started=time;animator.Play(hash,0,0);animator.Update(0);requests++;}
            public void Sample(float time,float interval,bool idle)
            {
                if(animator==null&&dynamicAnimator==null)return;
                var info=animator==null?default(AnimatorStateInfo):animator.GetCurrentAnimatorStateInfo(0);float length=dynamicAnimator!=null?dynamicAnimator.Length:info.length;float elapsed=Mathf.Max(0,time-started);float normalized=length>0?elapsed/length:0;
                if(dynamicAnimator!=null)dynamicAnimator.Sample(elapsed);else{animator.Play(Animator.StringToHash("Base Layer."+state),0,normalized);animator.Update(0);}
                if(idle&&(girlDance||dynamicAnimator==null||dynamicAnimator.HasState("idle"))&&time>=nextIdle)
                {
                    nextIdle=girlDance?nextIdle+interval:time+interval;
                    // GirlAnimator 0x2855E40 alternates the two triggers. The original GF_Default
                    // has no Idle trigger and useMove=false, so it does not wait for clip completion.
                    if(girlDance){Play(nextLeft?"IdleLeft":"IdleRight",time);nextLeft=!nextLeft;}
                    else if((state!=IdleState||!(dynamicAnimator!=null?dynamicAnimator.Loop:info.loop))&&normalized>=1)Play(IdleState,time);
                }
                float delta=Mathf.Max(0,time-passiveLast);passiveLast=time;
                for(int i=0;i<passive.Length;i++)
                {
                    bool active=passive[i].isActiveAndEnabled;
                    if(active)
                    {
                        if(!passiveActive[i]&&!passive[i].KeepStateOnDisable){passiveTimes[i]=0;passive[i].Play(passive[i].NativeDefaultState);}
                        else passiveTimes[i]+=delta;
                        passive[i].Sample(passiveTimes[i]);
                    }
                    passiveActive[i]=active;
                }
                var sprite=Sprite;if(sprite!=previous){previous=sprite;frames++;}
            }
        }
        private readonly Transform parent;private readonly Camera ui;private readonly RawImage image;private readonly GameObject world;private readonly Camera camera;private readonly RenderTexture texture;
        private readonly Actor[] actors=new Actor[3];private readonly List<Actor> decorations=new List<Actor>();
        private RecoveredRoleSource source;private RecoveredRoleStage stage;private GameObject background;private float time,bpm,idleInterval,cameraStart,cameraDuration;private Vector3 cameraFrom,cameraTo;
        private bool touching,noResult=true;private int currentHoldValue=-1,holdCount;
        private float cameraSizeFrom,cameraSizeTo;
        public int HoldRefreshSkipped {get;private set;}
        public int MissingStates => actors.Sum(a=>a?.dynamicAnimator?.missingStates??0);
        public string BfSprite=>actors[0]?.Sprite?.name;
        public string BossSprite=>actors[1]?.Sprite?.name;
        public int BfIndex {get;private set;}=-1;public int BossIndex {get;private set;}=-1;public int BackgroundIndex {get;private set;}=-1;
        public int CameraChanges {get;private set;}public int RoleChanges {get;private set;}public int BfRequests=>actors[0]?.requests??0;public int BossRequests=>actors[1]?.requests??0;public int BfFrames=>actors[0]?.frames??0;public int BossFrames=>actors[1]?.frames??0;
        public bool Available=>stage!=null;public string BundleMd5=>stage?.bundleMd5;public string BfName=>actors[0]?.root.name;public string BossName=>actors[1]?.root.name;
        public string BfState=>actors[0]?.state;public string BossState=>actors[1]?.state;public Camera SceneCamera=>camera;
        public string GfName=>actors[2]?.root.name;public string GfState=>actors[2]?.state;public int GfRequests=>actors[2]?.requests??0;public int GfFrames=>actors[2]?.frames??0;
        public Texture2D ProgressTexture(bool light)
        {
            if(stage!=null&&RecoveredDynamicRoles.Contains(stage.bundleMd5))return RecoveredDynamicRoles.ProgressTexture(stage.bundleMd5,light);
            string resource=stage==null?null:light?stage.progressLightResource:stage.progressDarkResource;
            return string.IsNullOrEmpty(resource)?null:Resources.Load<Texture2D>(resource);
        }
        public RecoveredRolePresentation(RecoveredUiTree view,Camera ui)
        {
            this.ui=ui;ui.cullingMask&=~(1<<30);parent=view.Root.transform;
            world=new GameObject("Recovered original scene");world.transform.SetParent(parent,false);
            var cam=new GameObject("Original PlayRoom camera",typeof(Camera));cam.transform.SetParent(world.transform,false);camera=cam.GetComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<30;camera.transform.localPosition=new Vector3(0,0,-10);
            texture=new RenderTexture(828,1792,24,RenderTextureFormat.ARGB32){name="Original PlayRoom portrait"};texture.Create();camera.targetTexture=texture;
            var go=new GameObject("Original scene composite",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(view.Find("Canvas").transform,false);go.transform.SetAsFirstSibling();image=go.GetComponent<RawImage>();image.texture=texture;image.raycastTarget=false;var rt=(RectTransform)go.transform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.sizeDelta=Vector2.zero;image.enabled=false;
        }
        public void Open(string sid,string aid,float stageBpm)
        {
            Clear();source=RecoveredRoleAssets.ForStage(sid,aid);if(source==null)return;var config=source.stage;
            stage=config;bpm=stageBpm;time=0;camera.orthographicSize=stage.cameraSize;camera.transform.localPosition=new Vector3(0,0,-10);cameraFrom=cameraTo=camera.transform.localPosition;
            if(stage.cameraPositions!=null){if(stage.cameraSizes==null||stage.cameraSizes.Length!=stage.cameraPositions.Length||stage.cameraSizes.Length==0)throw new InvalidOperationException("Original camera configuration mismatch");camera.transform.localPosition=new Vector3(stage.cameraPositions[0][0],stage.cameraPositions[0][1],-10);camera.orthographicSize=stage.cameraSizes[0];cameraFrom=cameraTo=camera.transform.localPosition;}
            cameraSizeFrom=cameraSizeTo=camera.orthographicSize;cameraDuration=0;
            // SetBpm 0x2D10F9C: smallest beat multiple with duration * minPercentage >= .5 for BPM > 120.
            idleInterval=60/bpm;if(bpm>120)while(idleInterval*.99f<.5f)idleInterval+=60/bpm;
            ChangeActor(0,stage.bfs,0);ChangeActor(1,stage.bosses,0);ChangeActor(2,stage.gfs,0);ChangeBackground(0);image.enabled=true;Sample(0);
        }
        private static void Layer(GameObject go){go.layer=30;foreach(Transform child in go.transform)Layer(child.gameObject);}
        private void ChangeActor(int slot,string[] names,int index)
        {
            if(index<0||names==null||index>=names.Length)return;
            var old=actors[slot];if(old!=null){old.root.SetActive(false);DestroyOwned(old.root);actors[slot]=null;}
            var go=RecoveredRoleAssets.Instantiate(stage.bundleMd5,names[index],world.transform);if(go!=null)
            {
                Layer(go);var animator=go.GetComponentInChildren<Animator>();var dynamicAnimator=go.GetComponentInChildren<RecoveredSpriteAnimator>(true);
                // The rendered character may sit on a child of a wrapper rig (Peace's boss is a
                // floating "fly" wrapper whose child carries the sprite and the reaction
                // controller set). Dispatch role animations to the animator that owns the
                // rendered sprite so the reaction states reach the character; the wrapper's
                // own animator stays sampled as a passive controller.
                var carrier=go.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r!=null&&r.enabled);
                var owned=carrier!=null?carrier.GetComponent<RecoveredSpriteAnimator>():null;
                if(owned!=null)dynamicAnimator=owned;
                if(animator==null&&dynamicAnimator==null)throw new InvalidOperationException("Original animated role has no controller: "+names[index]);
                if(animator!=null)animator.Update(0);
                // GirlAnimator alternates the two idle triggers. Detect the recovered controller family,
                // rather than restricting the behavior to the single shared GF_Default prefab name.
                bool dance=slot==2&&(dynamicAnimator!=null?dynamicAnimator.HasState("IdleLeft")&&dynamicAnimator.HasState("IdleRight")&&!dynamicAnimator.HasState("idle"):
                    animator.HasState(0,Animator.StringToHash("Base Layer.IdleLeft"))&&animator.HasState(0,Animator.StringToHash("Base Layer.IdleRight")));
                var passive=go.GetComponentsInChildren<RecoveredSpriteAnimator>(true).Where(a=>a!=dynamicAnimator).ToArray();
                foreach(var child in passive)child.Play(child.NativeDefaultState);
                actors[slot]=new Actor{root=go,animator=animator,dynamicAnimator=dynamicAnimator,nextIdle=time+(dance?60/bpm:idleInterval),girlDance=dance,nextLeft=false,passive=passive,passiveTimes=new float[passive.Length],passiveActive=passive.Select(a=>a.isActiveAndEnabled).ToArray(),passiveLast=time};
                actors[slot].Play(dance?"IdleLeft":actors[slot].IdleState,time);
            }
            if(slot==0)BfIndex=index;else if(slot==1)BossIndex=index;RoleChanges++;
        }
        private void ChangeBackground(int index)
        {
            if(index<0||stage.bgs==null||index>=stage.bgs.Length)return;
            decorations.Clear();if(background!=null){background.SetActive(false);DestroyOwned(background);}
            background=RecoveredRoleAssets.Instantiate(stage.bundleMd5,stage.bgs[index],world.transform);BackgroundIndex=index;RoleChanges++;
            if(background!=null){Layer(background);foreach(var animator in background.GetComponentsInChildren<Animator>()){animator.Update(0);var name=animator.GetCurrentAnimatorStateInfo(0);var controller=Array.Find(source.controllers,c=>c.states.Length==1&&animator.runtimeAnimatorController.name.StartsWith("controller-")&&animator.HasState(0,Animator.StringToHash("Base Layer."+c.states[0].name)));if(controller==null)throw new InvalidOperationException("Background state unresolved");var actor=new Actor{root=animator.gameObject,animator=animator};actor.Play(controller.states[0].name,time);decorations.Add(actor);}}
            if(background!=null)foreach(var animator in background.GetComponentsInChildren<RecoveredSpriteAnimator>(true)){var actor=new Actor{root=animator.gameObject,dynamicAnimator=animator};actor.Play(animator.DefaultState,time);decorations.Add(actor);}
        }
        public void Apply(RecoveredAmbient ambient)
        {
            if(stage==null)return;time=Mathf.Max(time,ambient.ts/1000f);
            if(ambient.bf_index>=0)ChangeActor(0,stage.bfs,ambient.bf_index);
            if(ambient.boss_index>=0)ChangeActor(1,stage.bosses,ambient.boss_index);
            if(ambient.gf_index>=0)ChangeActor(2,stage.gfs,ambient.gf_index);
            if(ambient.background_index>=0)ChangeBackground(ambient.background_index);
            int index=ambient.camera_turn_type;
            if(index>=0&&(stage.cameraPositions!=null?index<stage.cameraPositions.Length:index<=2))
            {
                cameraFrom=camera.transform.localPosition;cameraSizeFrom=camera.orthographicSize;cameraStart=ambient.ts/1000f;
                if(stage.cameraPositions!=null){cameraTo=new Vector3(stage.cameraPositions[index][0],stage.cameraPositions[index][1],-10);cameraSizeTo=stage.cameraSizes[index];cameraDuration=ambient.camera_move_time/1000f;}
                else{cameraTo=new Vector3(index==1?-1:index==2?1:0,0,-10);cameraSizeTo=camera.orthographicSize;cameraDuration=.5f;}
                if(cameraDuration<=0){camera.transform.localPosition=cameraTo;camera.orthographicSize=cameraSizeTo;}
                CameraChanges++;
            } // InitConfig/InitCameraByConfig/ChangeCamera ELF 0x2D0DCDC / 0x2D0E1B4 / 0x2D0E400.
        }
        public void Judged(RecoveredStageNote note,HitQuality quality,float timestamp)
        {
            if(stage==null)return;
            // RefreshWith ELF 0x2D116A4: human holds alternate refreshes while isTouch is set.
            // This gates presentation only; all accepted head/checkpoint/results still reach the ledger and HUD.
            if(!note.Hold){noResult=true;currentHoldValue=-1;}
            else if(note.Human)
            {
                if(!touching){noResult=true;currentHoldValue=-1;}
                if(noResult){noResult=false;holdCount=0;currentHoldValue=note.Action.key;}
                if(currentHoldValue!=note.Action.key&&currentHoldValue!=-1){HoldRefreshSkipped++;return;}
                if((holdCount&1)!=0){holdCount++;HoldRefreshSkipped++;return;}
                holdCount=1;
            }
            time=timestamp/1000f;int role=note.RoleAnimation;string direction=new[]{"left","down","up","right"}[note.Action.key];string state=direction+(quality==HitQuality.Miss?"_fail":"_success");
            if(role==0||role==2||role==6||role==7)actors[0]?.Play(state,time);
            if(role==0||role==1||role==5||role==7)actors[1]?.Play(state,time);
            if(role==4||role==5||role==6||role==7)actors[2]?.Play(state,time);
        }
        // Original finger handlers set isTouch AFTER dispatching JudgeTouchInfos (0x2D23624/0x2D23C8C).
        // Keep their latch semantics, including release order; keyboard and mouse use the same path.
        public void InputSampled(LaneContact contact)
        {if(contact.Ended)touching=false;else if(contact.Phase==LaneInputPhase.Began)touching=true;}
        public void Sample(float timestamp,bool render=true)
        {
            if(stage==null)return;time=Mathf.Max(0,timestamp/1000f);
            foreach(var actor in actors)actor?.Sample(time,actor.girlDance?60/bpm:idleInterval,true);foreach(var actor in decorations)actor.Sample(time,idleInterval,false);
            if(cameraDuration>0){float t=Mathf.Clamp01((time-cameraStart)/cameraDuration);camera.transform.localPosition=Vector3.LerpUnclamped(cameraFrom,cameraTo,t*(2-t));camera.orthographicSize=Mathf.LerpUnclamped(cameraSizeFrom,cameraSizeTo,t*(2-t));}
            camera.scene=ui.scene;if(render)camera.Render();
        }
        private static void DestroyOwned(UnityEngine.Object instance)
        {if(instance==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(instance);else UnityEngine.Object.DestroyImmediate(instance);}
        public void Clear()
        {
            for(int i=0;i<actors.Length;i++){if(actors[i]!=null){actors[i].root.SetActive(false);DestroyOwned(actors[i].root);}actors[i]=null;}
            if(background!=null){background.SetActive(false);DestroyOwned(background);}decorations.Clear();background=null;stage=null;image.enabled=false;BfIndex=BossIndex=BackgroundIndex=-1;CameraChanges=RoleChanges=0;
            touching=false;noResult=true;currentHoldValue=-1;holdCount=0;HoldRefreshSkipped=0;
        }
        public void Dispose(){Clear();DestroyOwned(world);DestroyOwned(image.gameObject);texture.Release();DestroyOwned(texture);}
    }
}
