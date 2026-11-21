using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using UnityEngine;

namespace BBPC.UnityRuntime
{
    public enum WealthRefreshTrigger { Splash, Foreground, Hud, Payment, Reward, Challenge, Manual }

    [Serializable,DataContract] public sealed class RecoveredProfileData
    {
        [DataMember] public string auid="",screen_name="",avatar="",name_box="",bf="__DEFAULT_BF__",gf="",revive="",arrow="__DEFAULT__",click_effect="",player_id="";
        [DataMember] public int change_name_price;
    }
    [Serializable,DataContract] public sealed class RecoveredWealthData
    {
        [DataMember] public int diamond,bf_draw_ticket,challenge_prize,vip; [DataMember] public long expiration,pass_expiration;
        // OBSERVED (user wealth response 6.0.6): the server records the paid entitlements it owns.
        [DataMember] public Dictionary<string,int> purchases=new Dictionary<string,int>();
    }
    [Serializable,DataContract] public sealed class RecoveredRoleSelectionData
    {
        [DataMember] public string selected_bf="__DEFAULT_BF__",selected_gf="",selected_revive=""; [DataMember] public string[] own_bfs=new string[0],own_gfs=new string[0],own_revives=new string[0];
    }
    [Serializable,DataContract] public sealed class RecoveredAppConfigData { [DataMember] public bool close_tutorial,close_story_mode,decor_enable; [DataMember] public int audio_dsp_buffer; }
    [Serializable,DataContract] public sealed class RecoveredDynamicConfigData { [DataMember] public string locale="es"; [DataMember] public string season=""; }

    // Local Windows state mirrors the native consumers. Remote writes stay opt-in and are never implied by UI clicks.
    public sealed class RecoveredLocalAccountState
    {
        private const string ProfileKey="BBPC.Profile606",WealthKey="BBPC.Wealth606",RolesKey="BBPC.Roles606",ConfigKey="BBPC.Config606",DynamicKey="BBPC.Dynamic606";
        private readonly Func<string,string> read;private readonly Action<string,string> write;private readonly Action save;
        public RecoveredProfileData Profile { get; private set; }
        public RecoveredWealthData Wealth { get; private set; }
        public bool HasPersistedServerWealth { get; private set; }
        public RecoveredRoleSelectionData Roles { get; private set; }
        public RecoveredAppConfigData Config { get; private set; }
        public RecoveredDynamicConfigData DynamicConfig { get; private set; }
        public RecoveredProfilePayload ServerProfile { get; private set; }
        public RecoveredOwnRolesPayload ServerRoles { get; private set; }
        public event Action<WealthRefreshTrigger> WealthChanged;
        public WealthRefreshTrigger LastWealthTrigger { get; private set; }
        public RecoveredLocalAccountState():this(k=>RecoveredPreferences.GetString(k,""),(k,v)=>RecoveredPreferences.SetString(k,v),RecoveredPreferences.Save){}
        public RecoveredLocalAccountState(Func<string,string> readValue,Action<string,string> writeValue,Action saveValue){read=readValue??throw new ArgumentNullException("readValue");write=writeValue??throw new ArgumentNullException("writeValue");save=saveValue??throw new ArgumentNullException("saveValue");Load();}
        public void Load()
        {
            Profile=Read(ProfileKey,new RecoveredProfileData());Wealth=Read(WealthKey,new RecoveredWealthData());Roles=Read(RolesKey,new RecoveredRoleSelectionData());Config=Read(ConfigKey,new RecoveredAppConfigData());DynamicConfig=Read(DynamicKey,new RecoveredDynamicConfigData());
            // Only a previously accepted server envelope establishes a known balance after restart.
            // Save()/ApplyWealthJson() alone must never turn local defaults or fixtures into server wealth.
            HasPersistedServerWealth=false;
            var wealthEnvelope=read("BBPC.ServerWealth606.Envelope");
            if(!string.IsNullOrEmpty(wealthEnvelope))
                try{Wealth=RecoveredReadContracts.ReadWealth(wealthEnvelope);HasPersistedServerWealth=true;}catch(FormatException){}
            ServerProfile=Read<RecoveredProfilePayload>("BBPC.ServerProfile606",null);
            // v4.13 used flat roles, which cannot establish ownership under the real wrapper schema.
            ServerRoles=Read<RecoveredOwnRolesPayload>("BBPC.ServerRoles606.v2",null);
            if(ServerRoles==null){Roles.own_bfs=new string[0];Roles.own_gfs=new string[0];Roles.own_revives=new string[0];}
            if(string.IsNullOrEmpty(Roles.selected_bf))Roles.selected_bf="__DEFAULT_BF__";if(string.IsNullOrEmpty(Profile.bf))Profile.bf=Roles.selected_bf;
        }
        public void Save(){Write(ProfileKey,Profile);Write(WealthKey,Wealth);Write(RolesKey,Roles);Write(ConfigKey,Config);Write(DynamicKey,DynamicConfig);save();}
        public void ApplyServerProfile(string body,string ownAuid)
        {
            var next=RecoveredReadContracts.ReadProfile(body,ownAuid);var user=next.user;
            // Preserve the old Windows schema. Remote RoleModel/CoverModel/ArrowModel remain typed separately.
            // A missing optional role never grants ownership or clears a local selection implicitly.
            Profile.auid=user.auid;Profile.screen_name=user.screen_name??"";Profile.player_id=user.player_id??"";
            Profile.avatar=user.avatar?.uri??"";Profile.name_box=user.name_box?.box_id??"";
            if(user.bf!=null){Profile.bf=user.bf.rid;Roles.selected_bf=user.bf.rid;}
            if(user.gf!=null){Profile.gf=user.gf.rid;Roles.selected_gf=user.gf.rid;}
            if(user.revive!=null){Profile.revive=user.revive.rid;Roles.selected_revive=user.revive.rid;}
            if(user.arrow!=null)Profile.arrow=user.arrow.arr_id;
            if(user.click_effect!=null)Profile.click_effect=user.click_effect.ceff_id;
            if(next.change_name_price.HasValue)Profile.change_name_price=next.change_name_price.Value;
            ServerProfile=next;Write("BBPC.ServerProfile606",next);
            // Private installation storage only: preserve unmodelled native fields without logging/export.
            write("BBPC.ServerProfile606.Envelope",body);Save();
        }
        public void ApplyServerWealth(string body,WealthRefreshTrigger trigger)
        {
            var next=RecoveredReadContracts.ReadWealth(body);Wealth=next;LastWealthTrigger=trigger;
            HasPersistedServerWealth=true;write("BBPC.ServerWealth606.Envelope",body);
            Write(WealthKey,Wealth);save();WealthChanged?.Invoke(trigger);
        }
        public void ApplyOwnRoles(string body)
        {
            var next=RecoveredReadContracts.Data<RecoveredOwnRolesPayload>(body);
            if(next.default_bf==null||string.IsNullOrEmpty(next.default_bf.rid)||next.bfs==null||next.gfs==null||next.revives==null)throw new FormatException("Incomplete own roles response");
            Func<RecoveredCustomRole[],string[]> ids=items=>
            {
                var result=new System.Collections.Generic.List<string>();var seen=new System.Collections.Generic.HashSet<string>();
                foreach(var item in items)
                {
                    if(item?.role==null||string.IsNullOrEmpty(item.role.rid)||item.restriction==null||!item.restriction.unlock_status.HasValue||!seen.Add(item.role.rid))throw new FormatException("Invalid owned role wrapper");
                    if(item.restriction.unlock_status==true)result.Add(item.role.rid);
                }
                return result.ToArray();
            };
            var bfs=ids(next.bfs);var gfs=ids(next.gfs);var revives=ids(next.revives);
            Roles.own_bfs=bfs;Roles.own_gfs=gfs;Roles.own_revives=revives;ServerRoles=next;Write("BBPC.ServerRoles606.v2",next);write("BBPC.ServerRoles606.Envelope",body);Save();
        }
        public bool ApplyProfileJson(string json){var next=Parse(json,new RecoveredProfileData());if(next==null||string.IsNullOrEmpty(next.bf))return false;Profile=next;if(!string.IsNullOrEmpty(next.bf))Roles.selected_bf=next.bf;Save();return true;}
        public bool ApplyWealthJson(string json,WealthRefreshTrigger trigger){var next=Parse(json,new RecoveredWealthData());if(next==null||next.diamond<0||next.bf_draw_ticket<0)return false;Wealth=next;LastWealthTrigger=trigger;Write(WealthKey,Wealth);save();WealthChanged?.Invoke(trigger);return true;}
        public void SetSelectedRole(string bf,string gf,string revive){if(string.IsNullOrEmpty(bf))throw new ArgumentException("bf required");Roles.selected_bf=bf;Roles.selected_gf=gf??"";Roles.selected_revive=revive??"";Profile.bf=Roles.selected_bf;Profile.gf=Roles.selected_gf;Profile.revive=Roles.selected_revive;Save();}
        private T Read<T>(string key,T fallback) where T:class{var json=read(key);if(string.IsNullOrEmpty(json))return fallback;try{var value=Deserialize<T>(json);return value??fallback;}catch(Exception){return fallback;}}
        private static T Parse<T>(string json,T fallback) where T:class{if(string.IsNullOrEmpty(json))return null;try{return Deserialize<T>(json);}catch(Exception){return null;}}
        private void Write<T>(string key,T value){write(key,Serialize(value));}
        private static string Serialize<T>(T value){using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(T),new DataContractJsonSerializerSettings{UseSimpleDictionaryFormat=true}).WriteObject(stream,value);return Encoding.UTF8.GetString(stream.ToArray());}}
        private static T Deserialize<T>(string json){return SongCatalog.Read<T>(json);}
    }
}
