using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace BBPC.UnityRuntime
{
    [Flags] public enum RecoveredProfileFlag : long
    {Default=0,BF=1,GF=2,Revive=4,BattleBFs=8,RoleDecor=16,RoomDecor=32,EntryEffect=64}

    // POST /beatmusic/role/config body.
    //
    // BF + empty-decor is DEMONSTRATED: two Android captures (LogF.txt) send
    // {"client_id":"<32hex>","mflag":1,"bf":{"rid":"bf_s28_bird","decors":{}}} and answer {"result":0}.
    //
    // GF (mflag 2) and Revive (mflag 4) are INFERRED, never captured:
    //   * the native MFlag enum (dump.cs TypeDefIndex 5205, NMNH.Game) defines exactly BF=1, GF=2,
    //     Revive=4, Battle_BFs=8, Role_Decor=16, Room_Decor=32, Entry_Effect=64, so the mflag values
    //     are the enum's own constants, not invented;
    //   * /beatmusic/role/own returns gfs and revives as the SAME CustomBfModel wrapper as bfs
    //     (RecoveredOwnRolesPayload.bfs/gfs/revives, each {role:RoleModel, restriction, arrange_state}),
    //     so the {"rid","decors":{}} shape is carried over from the BF capture.
    // No role/config body for GF/Revive was ever observed. LogReal606.txt contains zero role/config
    // POSTs (the test account owns no GF or Revive, so tab switching makes no request).
    public sealed class RecoveredBfOperation
    {
        public const string Route="/beatmusic/role/config";
        [DataContract] private sealed class EmptyDecor {}
        [DataContract] private sealed class Role
        {[DataMember(Order=0)] public string rid;[DataMember(Order=1)] public EmptyDecor decors=new EmptyDecor();}
        [DataContract] private sealed class Payload
        {
            [DataMember(Order=0)] public string client_id;
            [DataMember(Order=1)] public long mflag;
            [DataMember(Order=2,EmitDefaultValue=false)] public Role bf;
            [DataMember(Order=3,EmitDefaultValue=false)] public Role gf;
            [DataMember(Order=4,EmitDefaultValue=false)] public Role revive;
        }
        public sealed class ResultData {public int? result=null;}
        public string ClientId {get;}
        public RecoveredProfileFlag Flag {get;}
        public string Key {get;}        // "bf" | "gf" | "revive" — the JSON key under which the role sits
        public string Rid {get;}
        public string Json {get;}
        public RecoveredBfOperation(string rid):this(RecoveredProfileFlag.BF,rid){}
        public RecoveredBfOperation(RecoveredProfileFlag flag,string rid)
        {
            if(string.IsNullOrWhiteSpace(rid)||rid.Length>256)throw new ArgumentException("Role RID required");
            if(flag!=RecoveredProfileFlag.BF&&flag!=RecoveredProfileFlag.GF&&flag!=RecoveredProfileFlag.Revive)
                throw new NotSupportedException("Only BF, GF and Revive equip is reconstructed; decor and entry-effect flags need their own controller");
            Rid=rid;Flag=flag;Key=flag.ToString().ToLowerInvariant();
            ClientId=Guid.NewGuid().ToString("N");
            using(var stream=new MemoryStream())
            {
                var payload=new Payload{client_id=ClientId,mflag=(long)flag};
                var role=new Role{rid=rid};
                if(flag==RecoveredProfileFlag.BF)payload.bf=role;
                else if(flag==RecoveredProfileFlag.GF)payload.gf=role;
                else payload.revive=role;
                new DataContractJsonSerializer(typeof(Payload)).WriteObject(stream,payload);
                Json=Encoding.UTF8.GetString(stream.ToArray());
            }
        }
        public static bool Succeeded(string body)
        {
            var result=RecoveredReadContracts.Data<ResultData>(body);
            if(!result.result.HasValue)throw new FormatException("Missing PostHttpResult");
            return result.result.Value==0; // PostHttpResult.Succeed, not Boolean true or UpdateProfileResult.
        }
    }
}