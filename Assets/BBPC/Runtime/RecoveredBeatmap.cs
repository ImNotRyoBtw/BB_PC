using System;
using System.Runtime.Serialization;
using BBPC.Gameplay;
namespace BBPC.UnityRuntime
{
    // JSON wire fields from 6.0.6. Unknown fields, including editor-only audio metadata, are ignored.
    public sealed class RecoveredBeatmapAction
    {
        public int ts, action_type, action_flag, key, hit_type, duration;
        [IgnoreDataMember] public int NodeId;
        [IgnoreDataMember] public float Bpm;
        public CellKind Kind => action_type==2?CellKind.SingleHold:CellKind.SingleHit; // 0x2CC7D10
    }
    public sealed class RecoveredAmbient
    {
        public int ts,camera_move_time,camera_turn_type,bgm_time_line_type,role_animation_type,bf_index,gf_index,boss_index,background_index;
        public int? ambient_type; public int[] other_index,other_type;
        [OnDeserializing] private void Defaults(StreamingContext context) { camera_move_time=500;camera_turn_type=-1;bgm_time_line_type=-1;role_animation_type=-1;bf_index=gf_index=boss_index=background_index=-1; }
    }
    public sealed class RecoveredBeatmap
    {
        public int total_time; public float bpm, speed;
        public RecoveredBeatmapAction[] actions; public RecoveredAmbient[] ambients;
        public static RecoveredBeatmap Parse(string json)
        {
            var model=SongCatalog.Read<RecoveredBeatmap>(json);
            if(model==null || model.actions==null || model.actions.Length==0 || float.IsNaN(model.bpm) || float.IsInfinity(model.bpm) || model.bpm<=0) throw new FormatException("Incomplete beatmap");
            for(int i=0;i<model.actions.Length;i++)
            {
                var action=model.actions[i];
                if(action==null || action.ts<0 || action.duration<0 || action.key<0 || action.key>3) throw new FormatException("Invalid beatmap action scalar");
                checked { int end=action.ts+action.duration; if(end<action.ts) throw new FormatException("Invalid beatmap end"); }
                action.NodeId=i; action.Bpm=model.bpm; // StageJsonParser 0x2CC8168, original list order.
            }
            if(model.ambients==null)model.ambients=Array.Empty<RecoveredAmbient>();
            for(int i=0;i<model.ambients.Length;i++)if(model.ambients[i]==null||model.ambients[i].ts<0)throw new FormatException("Invalid ambient timestamp");
            return model;
        }
        public int LastEndMs { get { int end=0;foreach(var action in actions) end=Math.Max(end,checked(action.ts+action.duration));return end; } }
        public void RequireNormalTutorialDomain()
        {
            foreach(var action in actions)
                if((action.action_type!=0 && action.action_type!=2) || action.action_flag!=0 || action.hit_type!=2) throw new NotSupportedException("Special action/player assignment requires its native controller");
        }
    }
}
