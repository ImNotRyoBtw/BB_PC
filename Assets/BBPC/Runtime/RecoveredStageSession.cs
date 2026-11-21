using System;
using System.Collections.Generic;
using BBPC.Gameplay;
namespace BBPC.UnityRuntime
{
    public sealed class RecoveredStageNote
    {
        public readonly RecoveredBeatmapAction Action;
        public readonly CellKind Kind;
        public readonly int RoleAnimation;
        public readonly int[] CheckpointTimes;
        public readonly HitResult?[] Checkpoints;
        public HitQuality Head=HitQuality.None,Hit=HitQuality.None;
        public LaneContact Contact;
        public bool Spawned,Recycled;
        public float Y;
        public float HeadTimeMs;
        public RecoveredStageNote(RecoveredBeatmapAction action,CellKind kind,int role)
        { Action=action;Kind=kind;RoleAnimation=role;CheckpointTimes=action.Kind==CellKind.SingleHold?HoldCheckpointSchedule.Create(action.ts,action.duration,action.Bpm):Array.Empty<int>();Checkpoints=new HitResult?[CheckpointTimes.Length]; }
        public bool Hold => Action.Kind==CellKind.SingleHold;
        public bool Human => NoteJudgement.IsPlayerHitNote(Kind);
        public bool Complete => Hit!=HitQuality.None;
        public ResultCounts Counts => Hold?ResultCounts.Hold(Head,Checkpoints):ResultCounts.Tap(Hit);
    }
    [Serializable] public sealed class RecoveredStageResult
    {
        public int perfect,great,good,miss,blood,score,maxCombo,nodeCount,maxScore,progress,clickCount;
        public bool allCombo;
        // OBSERVED (user LogReport.txt vs LogAll.txt 2026-10-31): the report body "role" field is the
        // stage PlayType name, not the equipped BF role. StageMetaModel.PlayType { Normal=0, Bf=1 }.
        public int playType;
        public string sid,aid,roundId;
    }
    // Reconstructed normal pipeline. Native addresses and Windows-only boundaries are recorded in docs/p3-gameplay.md.
    public sealed class RecoveredStageSession
    {
        public readonly RecoveredBeatmap Chart;
        public readonly List<RecoveredStageNote> Notes=new List<RecoveredStageNote>();
        public readonly NormalScoreState Score=new NormalScoreState();
        public readonly NormalScoreLedger Ledger;
        public readonly List<string> Records=new List<string>();
        public readonly float Speed,FinishDuration;
        public int SpawnIndex,SpawnedCount,RecycledCount,RecordCallbacks,AmbientCursor,CameraEvents,BgmStartEvents,BgmEndEvents,AnimationBfRequests,AnimationBossRequests,AnimationGfRequests,InputSamples,ClickCount;
        public bool Finished;
        public bool Dead { get; private set; }
        public readonly RecoveredHealth Health;
        public void ReviveLocally()
        {
            if(!Dead||Finished)throw new InvalidOperationException("Revive requires a dead active session");
            Health.Initialize(Score);Dead=false;
        }
        public Action<RecoveredStageResult> Died;
        public float TimeMs,Progress;
        public Action<RecoveredStageNote> Spawn,Position,Recycle,ComboIncreased;
        public Action<RecoveredStageNote,HitQuality> Judged;
        public Action<RecoveredAmbient> Ambient;
        public Action<RecoveredStageResult> Ended;
        private readonly Dictionary<LaneContact,RecoveredStageNote> contacts=new Dictionary<LaneContact,RecoveredStageNote>();
        private readonly RecoveredAmbient[] temporal;
        public RecoveredStageSession(RecoveredBeatmap chart,float duration,int playType=0,RecoveredHealth health=null)
        {
            Health=health??RecoveredHealth.Stage(1);Health.Initialize(Score);
            Chart=chart??throw new ArgumentNullException(nameof(chart));
            if(duration<=0||float.IsNaN(duration)||float.IsInfinity(duration))throw new ArgumentOutOfRangeException(nameof(duration));
            if(playType<0||playType>2)throw new NotSupportedException("Unrecovered play type");
            Speed=chart.speed>0?chart.speed:1f;FinishDuration=duration;Ledger=new NormalScoreLedger(Score);
            temporal=(RecoveredAmbient[])chart.ambients.Clone();
            // Stable input order is required by the native forward cursors; do not silently reorder node IDs.
            for(int i=1;i<chart.actions.Length;i++)if(chart.actions[i].ts<chart.actions[i-1].ts)throw new FormatException("Unsorted actions");
            for(int i=1;i<temporal.Length;i++)if(temporal[i].ts<temporal[i-1].ts)throw new FormatException("Unsorted ambients");
            int cursor=0,role=3;
            foreach(var action in chart.actions)
            {
                if(action.action_flag!=0)throw new NotSupportedException("Special action flag needs its controller");
                // Refresh 0x2D06F00 carries the most recent valid role to each action. Ambient-only entries do not create cells.
                while(cursor<temporal.Length&&temporal[cursor].ts<=action.ts)
                { int value=temporal[cursor++].role_animation_type;if(value>=0&&value<=7)role=value; }
                Notes.Add(new RecoveredStageNote(action,CellType(action,playType),role));
            }
        }
        public static CellKind CellType(RecoveredBeatmapAction action,int playType)
        {
            // GenCellType 0x2D06BAC: ALL remains raw0; it is not duplicated or changed into BF.
            bool cpu=(playType==1&&action.hit_type==1)||(playType==2&&action.hit_type==2);
            return (CellKind)((action.Kind==CellKind.SingleHold?1:0)+(cpu?2:0));
        }
        public float PositionY(float timestamp,float time) => Math.Min(1050f,-373f+Speed*(timestamp-time));
        public void ComponentsUpdate(float time)
        {
            if(Finished||Dead)return;
            if(float.IsNaN(time)||float.IsInfinity(time))throw new ArgumentOutOfRangeException(nameof(time),"Non-finite playing time");
            TimeMs=time;Progress=Math.Max(Progress,Math.Max(0,Math.Min(1,time/FinishDuration)));
            // ComponentsUpdate 0x2D15DBC: Live -> Position -> State -> Finish.
            while(SpawnIndex<Notes.Count&&Notes[SpawnIndex].Action.ts<=time+1423f/Speed)
            { var n=Notes[SpawnIndex++];n.Spawned=true;SpawnedCount++;Spawn?.Invoke(n); }
            foreach(var n in Notes)if(n.Spawned&&!n.Recycled){n.Y=PositionY(n.Action.ts,time);Position?.Invoke(n);}
            foreach(var n in Notes)
            {
                if(!n.Spawned||n.Complete)continue;
                if(!n.Human&&time>n.Action.ts)
                {
                    // CPU holds need their own checkpoint controller; do not turn them into player input.
                    if(n.Hold)throw new NotSupportedException("CPU hold checkpoint controller not integrated");
                    n.Hit=HitQuality.Perfect;Record(n);continue;
                }
                if(n.Head==HitQuality.None&&NoteJudgement.MissDeadlinePassed(n.Action.ts,time))
                { n.Head=HitQuality.Miss;n.Hit=HitQuality.Miss;Record(n);if(Dead)break; }
            }
            // Windows view pool discards fully offscreen tails. This does not add ledger results.
            foreach(var n in Notes)if(n.Spawned&&!n.Recycled&&n.Complete&&PositionY(n.Action.ts+n.Action.duration,time)<-1100f)
            { n.Recycled=true;RecycledCount++;Recycle?.Invoke(n); }
            if(!Dead&&time>FinishDuration+1500f) { Finished=true;Ended?.Invoke(Summary()); }
        }
        public void LateUpdate(float time)
        {
            if(time<0||Dead)return;
            // Separate from judgement-driven RefreshWith; no invented dispatch based on ambient_type.
            while(AmbientCursor<temporal.Length&&temporal[AmbientCursor].ts<=time)
            {
                var a=temporal[AmbientCursor++];if(a.camera_turn_type>=0&&a.camera_turn_type<=2)CameraEvents++;
                if(a.bgm_time_line_type==0)BgmStartEvents++;if(a.bgm_time_line_type==1)BgmEndEvents++;
                Ambient?.Invoke(a);
            }
        }
        public void Input(LaneContact contact,float time)
        {
            if(contact==null)throw new ArgumentNullException(nameof(contact));if(Finished||Dead)return;InputSamples++;
            if(contact.Phase==LaneInputPhase.Began)ClickCount++;
            contacts.TryGetValue(contact,out var bound);
            foreach(var n in Notes)
            {
                if(!n.Spawned||!n.Human||n.Complete||(bound!=null&&!ReferenceEquals(bound,n)))continue;
                if(!n.Hold)
                {
                    if(contact.Ended)continue;
                    var d=NoteJudgement.Tap(n.Action.ts,time,contact.Key,n.Action.key);
                    if(d.State==DetectionState.Effected){contacts[contact]=n;n.Hit=d.Result.Value.Quality;Record(n);break;}
                    if(d.State==DetectionState.Miss)break;continue;
                }
                if(n.Head==HitQuality.None&&contact.Key==n.Action.key&&!contact.Ended)
                {
                    var d=NoteJudgement.HoldHead(n.Action.ts,time,contact.Key,n.Action.key);
                    if(d.State==DetectionState.Effecting){contacts[contact]=n;n.Contact=contact;n.Head=d.Result.Value.Quality;n.HeadTimeMs=time;Record(n);break;}
                    if(d.State==DetectionState.Miss)break;
                }
                bool same=ReferenceEquals(n.Contact,contact);int checkpoint=-1;
                if(n.Head!=HitQuality.None&&same&&contact.Key==n.Action.key)
                    for(int i=0;i<n.Checkpoints.Length;i++)if(!n.Checkpoints[i].HasValue&&NoteJudgement.CheckpointContains(n.CheckpointTimes[i],time)){checkpoint=i;break;}
                if(checkpoint>=0)
                {
                    n.Checkpoints[checkpoint]=new HitResult(HitQuality.Perfect,time);Record(n);
                    bool all=true;foreach(var result in n.Checkpoints)if(!result.HasValue)all=false;
                    if(contact.Ended||all)
                    {
                        int count=n.Checkpoints.Length;bool valid=count<3||(n.Checkpoints[count-3].HasValue&&ResultCounts.IsValid(n.Checkpoints[count-3].Value.Quality));
                        n.Hit=NoteJudgement.QualityAfterCheckpoint(count,valid);Record(n);
                    }
                    break;
                }
                if(NoteJudgement.ShouldEndHold(n.Action.ts,n.Action.duration,time,n.Head,same,contact.Ended,contact.Key,contact.Key,n.Action.key))
                { n.Hit=NoteJudgement.QualityOnHoldTermination(n.Head);Record(n);break; }
            }
            if(contact.Ended)contacts.Remove(contact);
        }
        private void Record(RecoveredStageNote note)
        {
            var record=new NormalRecord(note,note.Action.NodeId,note.Kind,note.Counts,note.Hit,note.Head);
            int previousCombo=Score.CurrentCombo;
            var outcome=Ledger.Record(record,()=>TimeMs);RecordCallbacks++;
            if(!outcome.Accepted)return;
            Records.Add(note.Action.NodeId+":"+note.Counts.Present+":"+note.Head+":"+note.Hit+":"+Score.Score);
            // RefreshWith is requested only after accepted record; actual actor linkage remains external.
            int role=note.RoleAnimation;
            if(role==0||role==2||role==6||role==7)AnimationBfRequests++;
            if(role==0||role==1||role==5||role==7)AnimationBossRequests++;
            if(role==4||role==5||role==6||role==7)AnimationGfRequests++;
            if(Score.CurrentCombo>previousCombo)ComboIncreased?.Invoke(note);
            Judged?.Invoke(note,outcome.NotificationQuality);
            if(Score.Blood<=0&&!Dead){Dead=true;Died?.Invoke(Summary());}
        }
        public RecoveredStageResult Summary()
        {
            var r=new RecoveredStageResult{score=Score.Score,blood=Score.Blood,maxCombo=Score.MaxCombo,progress=(int)(Progress*100f),clickCount=ClickCount};
            foreach(var n in Notes)if(n.Human)
            {
                r.nodeCount+=n.Hold?1+n.Checkpoints.Length:1;
                if(n.Hold){if(n.Hit==HitQuality.Miss)r.miss++;if(ResultCounts.IsValid(n.Head))r.perfect++;foreach(var c in n.Checkpoints)if(c.HasValue&&ResultCounts.IsValid(c.Value.Quality))r.perfect++;}
                else Count(r,n.Hit);
            }
            r.maxScore=r.nodeCount*NoteJudgement.HitScore(HitQuality.Perfect);r.allCombo=r.miss==0;return r;
        }
        private static void Count(RecoveredStageResult r,HitQuality quality)
        { if(quality==HitQuality.Perfect)r.perfect++;else if(quality==HitQuality.Great)r.great++;else if(quality==HitQuality.Good)r.good++;else if(quality==HitQuality.Miss)r.miss++; }
    }
}
