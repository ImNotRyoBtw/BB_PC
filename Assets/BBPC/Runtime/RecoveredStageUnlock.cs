using System;
using System.Collections;
namespace BBPC.UnityRuntime
{
    // Optional legacy advertising seam. The measured server request does not require a PC SDK callback.
    public interface IRecoveredRewardedAdProvider
    {IEnumerator Show(string sid,Action<bool> completed);}
    public sealed class RecoveredStageUnlockData {public string sid,story_id;public bool? unlock_status;}
    public static class RecoveredStageUnlock
    {
        public const string Route="/beatmusic/stage/unlocking";
        public static bool MayRequest(SongRestriction restriction)=>restriction?.unlock_type==1&&restriction.unlock_status==false;
        public static string Body(string sid)
        {
            if(string.IsNullOrEmpty(sid)||sid.Length>128)throw new ArgumentException("Selected SID required");
            foreach(char c in sid)if(!char.IsLetterOrDigit(c)&&c!='_'&&c!='-')throw new ArgumentException("Unexpected SID encoding");
            return "{\"reason\":\"ad\",\"sid\":\""+sid+"\"}";
        }
        public static RecoveredStageUnlockData Confirm(long http,string body,string sid)
        {
            if(http!=200)throw new FormatException("Unlock HTTP not accepted");
            var data=RecoveredReadContracts.Data<RecoveredStageUnlockData>(body);
            if(data.sid!=sid||data.unlock_status!=true)throw new FormatException("Unlock SID/status not confirmed");
            return data;
        }
        public static void Reconcile(SongCatalog catalog,RecoveredStageUnlockData confirmed)
        {
            if(confirmed?.unlock_status!=true||string.IsNullOrEmpty(confirmed.sid))throw new ArgumentException("Server confirmation required");
            // Server grant is by SID; different SIDs in the same group remain untouched.
            foreach(var item in SongCatalog.Enumerate(catalog.items))if(item.stage.sid==confirmed.sid)
            {if(item.restriction==null)item.restriction=new SongRestriction();item.restriction.unlock_status=true;}
        }
    }
}
