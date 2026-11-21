using System;
using BBPC.Gameplay;
namespace BBPC.UnityRuntime
{
    // Values are inputs from verified configuration/entitlements, never inferred purchases.
    // AddExtraHp 0x2CDDB2C; Refresh ARM64 0x2CF74B8..0x2CF74D0.
    public sealed class RecoveredHealth
    {
        public readonly int Base, Extra, Maximum;
        public readonly bool Sandbox;
        private RecoveredHealth(int stageHp,int extra,bool sandbox)
        { Base=stageHp;Extra=extra;Maximum=Math.Max(1,unchecked(stageHp+extra));Sandbox=sandbox; }
        public static RecoveredHealth Stage(int stageHp) => new RecoveredHealth(stageHp,0,false);
        public static RecoveredHealth FromVerifiedEntitlements(int stageHp,int abExtra,bool vip,bool extra2,bool extra5,bool extra10,bool extra3)
            => new RecoveredHealth(stageHp,unchecked(abExtra+(vip?5:0)+(extra2?2:0)+(extra5?5:0)+(extra10?10:0)+(extra3?3:0)),false);
        // No persistence and no remote sink. Test-only controls explicitly create a sandbox session.
        public static RecoveredHealth LocalSandbox(int stageHp,int extra) => new RecoveredHealth(stageHp,extra,true);
        public void Initialize(NormalScoreState state)
        { if(state==null)throw new ArgumentNullException(nameof(state));state.Blood=state.CurrentMaxBlood=Maximum;state.LatelyDeadTimestamp=null; }
    }
    public static class RecoveredProgress
    {
        // Binary double constants preserve the promoted float thresholds. Final comparison is strict >.
        public static int Icon(double progress)
        { if(progress>0.99000000953674316)return 3;if(progress>=0.99000000953674316)return 0;if(progress>=0.66666668653488159)return 2;if(progress>=0.3333333432674408)return 1;return 0; }
    }
}
