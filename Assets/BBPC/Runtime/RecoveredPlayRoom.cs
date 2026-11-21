using System;
using System.Collections.Generic;
using BBPC.Gameplay;
using UnityEngine;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    [Serializable] public sealed class RecoveredNoteBindings { public string[] heads,trails,tails,judgements,digits,digitObjects;public string combo; }
    public enum RecoveredPlayState { Closed,Prepared,Countdown,Playing,Paused,Dead,Results }
    public sealed class RecoveredPlayRoom : MonoBehaviour
    {
        public RecoveredUiTree View {get;private set;}
        public RecoveredUiData Data {get;private set;}
        public RecoveredUiData GameplayData => gameplay;
        public RecoveredAudioPlayback Audio {get;private set;}
        public WindowsLaneInput InputSource {get;private set;}
        public PreparedTutorial Content {get;private set;}
        public RecoveredStageSession Session {get;private set;}
        public RecoveredStageResult Result {get;private set;}
        public string RoundId {get;private set;}
        public bool UsedLocalRevive {get;private set;}
        public bool BotplayEnabled {get;set;}
        public bool UsedBotplay {get;private set;}
        private RecoveredStageReplay botplay;
        public int StageDifficulty {get;private set;}
        public Text ResultDifficultyText=>resultHeader.Find("DifficultyImg").GetComponentInChildren<Text>(true);
        public RecoveredUiTree ResultView {get;private set;}
        // The result-room header subtree (ResultHead) carries the observed reward widgets:
        // GetDiamonds, SlidingRewardBar, GetMoreButton. The Windows result panel drives them.
        public RecoveredUiTree ResultHeader => resultHeader;
        public Action ReturnToMenu,ResultsShown;
        // Offline deterministic replay only. Hardware input keeps the live audio clock.
        public Action<float> BeforeSessionUpdate;
        public Action<RecoveredPlayState> StateChanged;
        public RecoveredPlayState State {get;private set;}
        public RecoveredStageOverlays Overlays {get;private set;}
        public RecoveredCountdown Countdown {get;private set;}
        public RecoveredNoteEffects Effects {get;private set;}
        public RecoveredJudgementFeedback Feedback {get;private set;}
        public RecoveredRolePresentation Roles {get;private set;}
        public int? NextSandboxExtra;
        private double lastCountdownTime;
        private bool resuming;
        private void SetState(RecoveredPlayState state){State=state;StateChanged?.Invoke(state);}
        public bool IsLocalSandbox => Session!=null&&Session.Health.Sandbox;
        public Action<string> Replay;
        public int LastInputKey {get;private set;}=-1;
        public LaneInputPhase LastInputPhase {get;private set;}
        public int CreatedViews {get;private set;}
        private RecoveredUiData gameplay;
        private RecoveredNoteBindings bindings;
        private bool started;private RecoveredUiTree resultHeader;private UnityEngine.UI.Text localSummary;
        private readonly List<Coroutine> resultStarPulses=new List<Coroutine>();
        private readonly Dictionary<RecoveredStageNote,RecoveredUiTree> live=new Dictionary<RecoveredStageNote,RecoveredUiTree>();
        private readonly Queue<RecoveredUiTree> taps=new Queue<RecoveredUiTree>(),holds=new Queue<RecoveredUiTree>();
        private static readonly float[] lanes={-302,-100,100,302};
        public void Initialize(Camera camera)
        {
            if(View!=null)return;
            Data=JsonUtility.FromJson<RecoveredUiData>(Resources.Load<TextAsset>("Recovered606/playroom").text);
            gameplay=JsonUtility.FromJson<RecoveredUiData>(Resources.Load<TextAsset>("Recovered606/gameplay").text);
            bindings=JsonUtility.FromJson<RecoveredNoteBindings>(Resources.Load<TextAsset>("Recovered606/note-bindings").text);
            View=RecoveredUiBuilder.Build(Data,Data.scenes[0],transform,camera);
            ResultView=RecoveredUiBuilder.Build(gameplay,Array.Find(gameplay.scenes,x=>x.name=="ResultRoom"),transform,camera);
            var content=ResultView.Find("Content").GetComponent<RectTransform>();
            resultHeader=RecoveredUiBuilder.Build(gameplay,Array.Find(gameplay.scenes,x=>x.name=="ResultHead"),content,camera);
            var header=resultHeader.Find("ResultHeadView").GetComponent<RectTransform>();
            // EnhancedScroller's original cell is placed in the recovered ScrollRect, with its own authored height.
            content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=new Vector2(0,header.sizeDelta.y);content.anchoredPosition=Vector2.zero;
            header.anchorMin=header.anchorMax=new Vector2(.5f,1);header.pivot=new Vector2(.5f,1);header.anchoredPosition=Vector2.zero;
            foreach(var name in new[]{"RecordImg","RecordText","FavoritesButton","GetDiaImage","TryNewSongText","BlackBackground"})Hide(resultHeader,name);
            foreach(var name in new[]{"NextButton","PageUpButton"})Hide(ResultView,name);
            var summaryObject=new GameObject("WindowsLocalSummary",typeof(RectTransform),typeof(Text));summaryObject.transform.SetParent(ResultView.Find("DownUI").transform,false);localSummary=summaryObject.GetComponent<Text>();localSummary.font=ResultDifficultyText.font;localSummary.fontSize=24;localSummary.color=new Color(.8f,.8f,.8f,1);localSummary.alignment=TextAnchor.MiddleCenter;localSummary.raycastTarget=false;localSummary.resizeTextForBestFit=true;localSummary.resizeTextMinSize=16;localSummary.resizeTextMaxSize=24;
            var sr=summaryObject.GetComponent<RectTransform>();sr.anchorMin=sr.anchorMax=new Vector2(.5f,0);sr.pivot=new Vector2(.5f,0);sr.anchoredPosition=new Vector2(0,160);sr.sizeDelta=new Vector2(740,48);
            Wire(ResultView,"HomeButton",()=>ReturnToMenu?.Invoke());Wire(ResultView,"ReplayButton",()=>{if(Content!=null)Replay?.Invoke(Content.Definition.variant);});
            Roles=new RecoveredRolePresentation(View,camera);
            Effects=new RecoveredNoteEffects(View);Feedback=new RecoveredJudgementFeedback(View,gameplay,bindings);
            Audio=gameObject.AddComponent<RecoveredAudioPlayback>();Audio.Initialize();
            var effectSource=gameObject.AddComponent<AudioSource>();effectSource.playOnAwake=false;
            Overlays=new RecoveredStageOverlays(View,ResultView,camera,effectSource);Overlays.Pause=Pause;Overlays.Resume=()=>{if(State==RecoveredPlayState.Prepared)StartPreparedPlayback();else Resume();};Overlays.Retry=()=>{if(Content!=null)Replay?.Invoke(Content.Definition.variant);};Overlays.Exit=()=>ReturnToMenu?.Invoke();Overlays.Abort=()=>{if(State==RecoveredPlayState.Dead)ShowResult(Session.Summary());};
            Overlays.RequestRevive=completed=>completed(true);
            Overlays.Revived=()=>{if(State!=RecoveredPlayState.Dead)return;UsedLocalRevive=true;Session.ReviveLocally();Text(View,"HPCountLabel",Session.Score.Blood+"/"+Session.Score.CurrentMaxBlood);BeginCountdown(true);};
            InputSource=gameObject.AddComponent<WindowsLaneInput>();InputSource.ViewportCamera=camera;InputSource.ReadPlayingTime=()=>Audio.Clock.PlayingTime();
            InputSource.Sample=contact=>
            {
                SampleInput(contact,Audio.Clock.PlayingTime());
            };
            foreach(var name in new[]{"StartButton","bfTipImage","gfTipImage","bfTipText","gfTipText","FPS"})Hide(View,name);
            Hide(View,"ResultImage");Hide(View,"ResultNodes");
            foreach(var pair in View.Objects)if(pair.Value.TryGetComponent<Button>(out var button))button.interactable=false;
            View.Root.SetActive(false);ResultView.Root.SetActive(false);
        }
        private static void Hide(RecoveredUiTree tree,string name){var go=tree.Find(name);if(go!=null)go.SetActive(false);}
        public void SampleInput(LaneContact contact,float time)
        {
            LastInputKey=contact.Key;LastInputPhase=contact.Phase;
            if(State!=RecoveredPlayState.Playing||!Audio.Playing||Session==null)return;
            if(contact.Phase==LaneInputPhase.Began)Effects.Press(contact.Key);
            Session.Input(contact,time);Roles.InputSampled(contact);
        }
        private static void Wire(RecoveredUiTree tree,string name,Action action){var b=tree.Find(name)?.GetComponent<Button>();if(b!=null){b.interactable=true;b.onClick.AddListener(()=>action());}}
        private static void Text(RecoveredUiTree tree,string name,string value){var text=tree.Find(name)?.GetComponent<Text>();if(text!=null)text.text=value;}
        public void Open(PreparedTutorial content)
        {
            if(content==null||View==null)throw new InvalidOperationException("PlayRoom not prepared");
            BeforeSessionUpdate=null;Content=content;Result=null;RoundId=Guid.NewGuid().ToString("D");UsedLocalRevive=false;
            StageDifficulty=content.Definition.variant=="elastic"?SongCatalog.Read<SongMetadata>(Resources.Load<TextAsset>("LocalStages606/stage").text).CurrentDifficulty:Math.Max(1,Math.Min(8,content.Definition.difficulty));ResultView.Root.SetActive(false);View.Root.SetActive(true);
            float duration=content.Definition.declaredDurationMs>0?content.Definition.declaredDurationMs:content.Chart.total_time>0?content.Chart.total_time:(long)(content.Audio.length*1000f);
            var health=NextSandboxExtra.HasValue?RecoveredHealth.LocalSandbox(content.Definition.hp,NextSandboxExtra.Value):RecoveredHealth.Stage(content.Definition.hp);NextSandboxExtra=null;
            Session=new RecoveredStageSession(content.Chart,duration,content.Definition.playType,health);
            UsedBotplay=BotplayEnabled;
            botplay=BotplayEnabled?new RecoveredStageReplay(Session,new PersistentLaneInput(),SampleInput):null;
            Session.Spawn=Spawn;Session.Position=Position;Session.Recycle=Recycle;Session.Judged=Judged;Session.Ended=ShowResult;Session.Died=ShowDeath;Session.ComboIncreased=Effects.ComboIncreased;Effects.Clear();Feedback.Reset();
            Roles.Open(content.Definition.sid,content.Definition.aid,content.Chart.bpm);Session.Ambient=Roles.Apply;
            PrewarmNoteViews();Session.ComponentsUpdate(0);Session.LateUpdate(0);Roles.Sample(0);
            Text(View,"ScoreNumLabel","0");var scoreLabel=View.Find("ScoreNumLabel").GetComponent<Text>();if(scoreLabel.preferredHeight>scoreLabel.rectTransform.rect.height)scoreLabel.verticalOverflow=VerticalWrapMode.Overflow; // Windows reimported font metrics; keep original font, size and anchors.
            Text(View,"HPCountLabel",Session.Score.Blood+"/"+Session.Score.CurrentMaxBlood);View.Find("HPCountLabel")?.SetActive(true);
            Feedback.Reset();Hide(View,"DebugSpeedSlider");Hide(View,"TouchTracks");Hide(View,"FPS");Audio.Prepare(content.Audio);started=false;InputSource.Reading=false;Overlays.ShowStart();ResetProgress();SetState(RecoveredPlayState.Prepared);
        }
        public void StartPreparedPlayback(){if(Content==null||Session==null)throw new InvalidOperationException("No prepared stage");if(State==RecoveredPlayState.Prepared)BeginCountdown(false);}
        private void BeginCountdown(bool resume)
        {
            resuming=resume;started=true;InputSource.Reading=false;Countdown=new RecoveredCountdown(Content.Chart.bpm);
            Countdown.Changed=remaining=>{if(remaining>0)Overlays.ShowCountdown(remaining);};
            Countdown.Finished=()=>{Overlays.HideCountdown();if(resuming)Audio.Resume();else Audio.Play();InputSource.Reading=true;Overlays.Playing();SetState(RecoveredPlayState.Playing);};
            lastCountdownTime=Time.realtimeSinceStartupAsDouble;Overlays.ShowCountdown(4);SetState(RecoveredPlayState.Countdown);
        }
        public void Pause()
        {
            if(State!=RecoveredPlayState.Playing&&State!=RecoveredPlayState.Countdown)return;
            InputSource.Reading=false;Audio.Pause();
            if(Countdown!=null)Countdown.Paused=true;Overlays.HideCountdown();Overlays.ShowPaused(true);SetState(RecoveredPlayState.Paused);
        }
        public void Resume(){if(State==RecoveredPlayState.Paused)BeginCountdown(Audio.PreparedPlaybackStarted);}
        private void ShowDeath(RecoveredStageResult result)
        {
            InputSource.Reading=false;Audio.Pause();InputSource.CancelContacts();Overlays.ShowDeath();SetState(RecoveredPlayState.Dead);
        }
        private void ResetProgress()
        {
            var dark=Roles.ProgressTexture(false)??RecoveredUiBuilder.Asset<Texture2D>(Overlays.Data,"resources.assets:248");
            foreach(var name in new[]{"FirstImg","SecondImg","ThirdImg"}){var image=View.Find(name)?.GetComponent<RawImage>();if(image!=null)image.texture=dark;}
            var slider=View.Find("ProgressBar")?.GetComponent<Slider>();if(slider!=null)slider.value=0;
        }
        private void RefreshProgress()
        {
            var slider=View.Find("ProgressBar")?.GetComponent<Slider>();if(slider!=null&&slider.value<Session.Progress)slider.value=Session.Progress;
            int icon=RecoveredProgress.Icon((double)Session.Progress);if(icon>0){var image=View.Find(icon==1?"FirstImg":icon==2?"SecondImg":"ThirdImg")?.GetComponent<RawImage>();if(image!=null)image.texture=Roles.ProgressTexture(true)??RecoveredUiBuilder.Asset<Texture2D>(Overlays.Data,"sharedassets2.assets:63");}
        }
        private void PrewarmNoteViews()
        {
            foreach(var asset in gameplay.assets)
            {
                if(asset.type=="Sprite")Resources.Load<Sprite>(asset.resource);
                else if(asset.type=="Font"){var font=Resources.Load<Font>(asset.resource);if(font!=null&&font.dynamic)font.RequestCharactersInTexture("0123456789 /",50,FontStyle.Normal);}
            }
            foreach(bool hold in new[]{false,true})for(int i=0;i<8;i++)
            {
                var tree=RecoveredUiBuilder.Build(gameplay,Array.Find(gameplay.scenes,x=>x.name==(hold?"HoldCell":"HitCell")),View.Find("PlayerCellNodesContainer").transform);
                tree.Root.SetActive(false);(hold?holds:taps).Enqueue(tree);CreatedViews++;
            }
        }
        private void Spawn(RecoveredStageNote n)
        {
            var pool=n.Hold?holds:taps;RecoveredUiTree tree;
            if(pool.Count>0){tree=pool.Dequeue();tree.Root.SetActive(true);}
            else{tree=RecoveredUiBuilder.Build(gameplay,Array.Find(gameplay.scenes,x=>x.name==(n.Hold?"HoldCell":"HitCell")),View.Find(n.Human?"PlayerCellNodesContainer":"RivalCellNodesContainer").transform);CreatedViews++;}
            live.Add(n,tree);
            tree.Find("HeadNode").GetComponent<Image>().sprite=RecoveredUiBuilder.Asset<Sprite>(gameplay,bindings.heads[n.Action.key]);
            tree.Find("HeadNode").SetActive(true);
            if(n.Hold)
            {
                Hide(tree,"HeadShapeChangeNode");Hide(tree,"ShapeChangeLine");Hide(tree,"Line");
                tree.Find("TailNode").GetComponent<Image>().sprite=RecoveredUiBuilder.Asset<Sprite>(gameplay,bindings.tails[n.Action.key]);
                tree.Find("Line2D").GetComponent<Image>().sprite=RecoveredUiBuilder.Asset<Sprite>(gameplay,bindings.trails[n.Action.key]);
            }
        }
        private void Position(RecoveredStageNote n)
        {
            if(!live.TryGetValue(n,out var tree))return;
            var root=tree.Find(n.Hold?"SingleHoldCell":"SingleHitCell").GetComponent<RectTransform>();root.anchoredPosition=new Vector2(lanes[n.Action.key],n.Y);
            var head=tree.Find("HeadNode").GetComponent<RectTransform>();head.anchoredPosition=Vector2.zero;
            if(n.Hold)
            {
                bool holding=n.Head==HitQuality.Perfect&&!n.Complete;
                float length=Session.Speed*n.Action.duration,held=holding?-373-n.Y:0;
                // RefreshCurrentNode 0x2D0BB24 replaces HeadNode with the original looping node.
                // The scored head must not remain as a frozen arrow at the receptor.
                head.gameObject.SetActive(!holding);
                var changed=tree.Find("HeadShapeChangeNode").GetComponent<Image>();changed.gameObject.SetActive(holding);
                if(holding){changed.rectTransform.localPosition=new Vector3(0,held,0);Effects.SampleHold(changed,n.Action.key,(Session.TimeMs-n.HeadTimeMs)/1000f);}
                var tail=tree.Find("TailNode").GetComponent<RectTransform>();tail.localPosition=new Vector3(0,length+(holding?0:25),0);
                var body=tree.Find("Line2D").GetComponent<RectTransform>();// DealDrawing 0x2D0C0CC/0x2D0C4CC uses localPosition; original Line2D rotates 180 degrees around Z.
                // anchoredPosition would introduce -50 from its bottom anchor in the 100px root.
                body.localPosition=new Vector3(0,length-(holding?25:0),0);body.sizeDelta=new Vector2(body.sizeDelta.x,Math.Max(0,length-held-(holding?25:0)));
            }
            root.gameObject.SetActive(!n.Complete);
        }
        private void Recycle(RecoveredStageNote n)
        {if(live.TryGetValue(n,out var tree)){tree.Root.SetActive(false);(n.Hold?holds:taps).Enqueue(tree);live.Remove(n);}}
        private void Judged(RecoveredStageNote n,HitQuality quality)
        {
            Text(View,"ScoreNumLabel",Session.Score.Score.ToString());Text(View,"HPCountLabel",Session.Score.Blood+"/"+Session.Score.CurrentMaxBlood);
            Feedback.Show(quality,Session.Score.CurrentCombo);
            Roles.Judged(n,quality,Session.TimeMs);
        }
        private void ShowResult(RecoveredStageResult result)
        {
            Overlays.Hide();SetState(RecoveredPlayState.Results);
            result.sid=Content.Definition.sid;result.aid=Content.Definition.aid;result.roundId=RoundId;result.playType=Content.Definition.playType;Result=result;
            InputSource.Reading=false;Audio.StopPlayback();InputSource.CancelContacts();View.Root.SetActive(false);
            RecoveredDifficultyStyles.ApplyLabel(ResultDifficultyText,StageDifficulty);
            Text(resultHeader,"SongNameText",Content.Definition.title);Text(resultHeader,"ArtistText",Content.Definition.artist);
            Text(resultHeader,"ScoreText",result.score.ToString());Text(resultHeader,"PerfectScoreText",result.perfect.ToString());Text(resultHeader,"GreatScoreText",result.great.ToString());Text(resultHeader,"GoodScoreText",result.good.ToString());
            BindResultProgressIndicators(result);
            Text(resultHeader,"ComboText",result.maxCombo.ToString());
            localSummary.text="LOCAL  •  MISS "+result.miss+"  •  HP "+result.blood;
            ResultView.Root.SetActive(true);ResultsShown?.Invoke();
        }
        private void BindResultProgressIndicators(RecoveredStageResult result)
        {
            foreach(var pulse in resultStarPulses)if(pulse!=null)StopCoroutine(pulse);
            resultStarPulses.Clear();
            var data=gameplay;
            var light=RecoveredUiBuilder.Asset<Texture2D>(data,"sharedassets2.assets:63");
            var dark=RecoveredUiBuilder.Asset<Texture2D>(data,"resources.assets:248");
            var ids=new[]{"sharedassets4.assets:139","sharedassets4.assets:128","sharedassets4.assets:125"};
            int lit=(int)((float)result.progress/100f*3f);
            for(int i=0;i<ids.Length;i++)
            {
                var raw=resultHeader.Objects.TryGetValue(ids[i],out var star)?star.GetComponent<RawImage>():null;
                if(raw==null)throw new InvalidOperationException("Original result progress indicator missing: "+ids[i]);
                raw.texture=i<lit?light:dark;raw.gameObject.SetActive(true);
                if(i<lit)resultStarPulses.Add(StartCoroutine(PulseResultIndicator(raw.rectTransform)));
                else raw.rectTransform.localScale=Vector3.one;
            }
        }
        private System.Collections.IEnumerator PulseResultIndicator(RectTransform target)
        {
            while(target!=null)
            {
                yield return ScaleResultIndicator(target,Vector3.one,new Vector3(1.05f,1.05f,1f),.15f);
                yield return ScaleResultIndicator(target,new Vector3(1.05f,1.05f,1f),new Vector3(.95f,.95f,1f),.30f);
                yield return ScaleResultIndicator(target,new Vector3(.95f,.95f,1f),Vector3.one,.15f);
            }
        }
        private static System.Collections.IEnumerator ScaleResultIndicator(RectTransform target,Vector3 from,Vector3 to,float duration)
        {
            float elapsed=0;
            while(elapsed<duration&&target!=null)
            {
                elapsed+=Time.deltaTime;
                target.localScale=Vector3.LerpUnclamped(from,to,Mathf.Clamp01(elapsed/duration));
                yield return null;
            }
            if(target!=null)target.localScale=to;
        }
        public void Close()
        {
            foreach(var pulse in resultStarPulses)if(pulse!=null)StopCoroutine(pulse);resultStarPulses.Clear();
            Roles.Clear();BeforeSessionUpdate=null;InputSource.Reading=false;Audio.StopPlayback();InputSource.CancelContacts();Overlays.Hide();Effects.Clear();Feedback.Reset();Countdown=null;SetState(RecoveredPlayState.Closed);View.Root.SetActive(false);ResultView.Root.SetActive(false);
            foreach(var pair in live)Destroy(pair.Value.Root);live.Clear();while(taps.Count>0)Destroy(taps.Dequeue().Root);while(holds.Count>0)Destroy(holds.Dequeue().Root);
            if(Content!=null)Destroy(Content.Audio);Content=null;Session=null;started=false;
        }
        private void Update()
        {
            if(Content==null||!View.Root.activeInHierarchy)return;
            if(!started){if(InputSource.ReadHardware&&UnityEngine.Input.GetKeyDown(KeyCode.Escape))ReturnToMenu?.Invoke();return;}
            if(State==RecoveredPlayState.Dead)Overlays.AdvanceDeath();
            if(State==RecoveredPlayState.Countdown){double now=Time.realtimeSinceStartupAsDouble;Countdown.Advance((now-lastCountdownTime)*1000);lastCountdownTime=now;}
            if(State==RecoveredPlayState.Playing&&Audio.Playing){Effects.Advance(Time.deltaTime);Feedback.Advance(Time.deltaTime);float now=Audio.Clock.PlayingTime();botplay?.AdvanceTo(now);BeforeSessionUpdate?.Invoke(now);Session.ComponentsUpdate(now);RefreshProgress();}
            if(InputSource.ReadHardware&&UnityEngine.Input.GetKeyDown(KeyCode.Space)){if(State==RecoveredPlayState.Paused)Resume();else Pause();}
            if(InputSource.ReadHardware&&UnityEngine.Input.GetKeyDown(KeyCode.Escape)){if(State==RecoveredPlayState.Playing||State==RecoveredPlayState.Countdown)Pause();else{Close();ReturnToMenu?.Invoke();}}
        }
        private void OnApplicationFocus(bool focus){if(!focus&&Audio!=null&&Audio.PauseOnFocusLoss)Pause();}
        private void OnApplicationPause(bool paused){if(paused&&Audio!=null&&Audio.PauseOnFocusLoss)Pause();}
        private void LateUpdate(){if(Session!=null&&State==RecoveredPlayState.Playing&&Audio.Playing){float now=Audio.Clock.PlayingTime();Session.LateUpdate(now);Roles.Sample(now);}}
        private void OnDestroy(){Roles?.Dispose();}
    }
}
