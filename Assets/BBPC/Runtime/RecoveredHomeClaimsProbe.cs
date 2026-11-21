using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BBPC.UnityRuntime
{
    public static class RecoveredHomeClaimsProbe
    {
        [Serializable] private sealed class Claim { public string group,outcome; public bool confirmed; public int received; }
        [Serializable] private sealed class Receipt { public bool finished,timeSynced,success; public string failure; public List<Claim> claims=new List<Claim>(); public List<Attempt> requests=new List<Attempt>(); }
        [Serializable] private sealed class Attempt { public string route,result,failure; public long http; public int status,error; public bool accepted; }
        private static RecoveredTaskPeriodItem Eligible(RecoveredTaskPeriodItem[] tasks)
        {return Array.Find(tasks??Array.Empty<RecoveredTaskPeriodItem>(),t=>t!=null&&t.status==1&&t.need_ad==0&&!string.IsNullOrEmpty(t.tid));}
        private static RecoveredTaskPeriodItem Period(RecoveredTaskPeriodList data,int group)
        {if(group==0)return Eligible(data?.signin?.tasks);var block=group==1?data?.daily:data?.weekly;return Eligible(block?.point)??Eligible(block?.reward);}
        private static RecoveredTaskPeriodItem Pass(RecoveredBattlePassData data)
        {
            foreach(var level in data?.level_tasks??Array.Empty<RecoveredBattlePassLevel>())
            {if(level==null)continue;var item=Eligible(new[]{level.free,data.has_purchased?level.vip:null});if(item!=null)return item;}
            return Eligible(data?.bonus);
        }
        private static bool Received(RecoveredTaskPeriodItem[] tasks,string tid)
        {return Array.Exists(tasks??Array.Empty<RecoveredTaskPeriodItem>(),t=>t!=null&&t.tid==tid&&t.status==2);}
        private static bool ReceivedPeriod(RecoveredTaskPeriodList data,int group,string tid)
        {if(group==0)return Received(data?.signin?.tasks,tid);var block=group==1?data?.daily:data?.weekly;return Received(block?.point,tid)||Received(block?.reward,tid);}
        private static bool ReceivedPass(RecoveredBattlePassData data,string tid)
        {foreach(var level in data?.level_tasks??Array.Empty<RecoveredBattlePassLevel>())if(level!=null&&Received(new[]{level.free,level.vip},tid))return true;return Received(data?.bonus,tid);}
        public static IEnumerator Run(RecoveredFrontend host,string output)
        {
            Directory.CreateDirectory(output);Application.runInBackground=true;
            var receipt=new Receipt();var services=host.Services;
            var work=Execute(services,receipt);var stack=new Stack<IEnumerator>();stack.Push(work);
            float deadline=Time.realtimeSinceStartup+210;
            while(stack.Count>0)
            {
                if(Time.realtimeSinceStartup>deadline){receipt.failure="Timeout; do not retry claims automatically";break;}
                object current=null;bool moved=false;
                try{moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}
                catch(Exception){receipt.failure="Probe exception; inspect sanitized transport outcomes";break;}
                if(!moved){var done=stack.Pop() as IDisposable;done?.Dispose();continue;}
                var nested=current as IEnumerator;if(nested!=null){stack.Push(nested);continue;}
                yield return current;
            }
            while(stack.Count>0){try{(stack.Pop() as IDisposable)?.Dispose();}catch(Exception){}}
            foreach(var request in services.ReadAttempts)receipt.requests.Add(new Attempt{route=request.route,http=request.http,result=request.result,failure=request.failure,status=request.envelopeStatus,error=request.errCode,accepted=request.contractAccepted});
            receipt.finished=receipt.failure==null;receipt.timeSynced=services.TimeSynced;
            receipt.success=receipt.finished&&receipt.timeSynced&&receipt.claims.Count==4&&receipt.claims.TrueForAll(c=>c.outcome=="unavailable"||c.outcome=="eligible-read-only"||c.confirmed);
            File.WriteAllText(Path.Combine(output,"home-claims-results.json"),JsonUtility.ToJson(receipt,true));Application.Quit(receipt.success?0:3);
        }
        private static IEnumerator Execute(RecoveredServiceClient services,Receipt receipt)
        {
            if(RecoveredStorage.Isolated||!services.HasPersistedPair){receipt.failure="Existing own persisted identity required";yield break;}
            yield return services.ProbeExistingAccount(true);
            if(!services.TimeSynced||!services.ProfileVerified){receipt.failure="Own account verification failed";yield break;}
            bool readOnly=Array.IndexOf(Environment.GetCommandLineArgs(),"-bbpc-home-claims-read-only")>=0;
            for(int group=0;group<4;group++)
            {
                var claim=new Claim{group=new[]{"check-in","daily","weekly","battle-pass"}[group]};receipt.claims.Add(claim);
                RecoveredTaskPeriodList periodic=null;RecoveredBattlePassData pass=null;
                if(group<3)yield return services.FetchPeriodicTasks(value=>periodic=value);
                else yield return services.FetchBattlePass(value=>pass=value);
                if(group<3?periodic==null:pass==null){claim.outcome="read-failed";continue;}
                if(group==3)
                {
                    foreach(var level in pass.level_tasks??Array.Empty<RecoveredBattlePassLevel>())
                    {if(level?.free?.status==2)claim.received++;if(level?.vip?.status==2)claim.received++;}
                    foreach(var bonus in pass.bonus??Array.Empty<RecoveredTaskPeriodItem>())if(bonus?.status==2)claim.received++;
                }
                var task=group<3?Period(periodic,group):Pass(pass);
                if(task==null){claim.outcome="unavailable";continue;}
                if(readOnly){claim.outcome="eligible-read-only";continue;}
                if(group==3&&string.IsNullOrEmpty(pass.bid)){claim.outcome="missing-pass-id";continue;}
                RecoveredTaskRewardResultData result=null;
                if(group<3)yield return services.ClaimTaskReward(task.tid,(group+1).ToString(),value=>result=value);
                else yield return services.ClaimBattlePassReward(pass.bid,task.tid,0,value=>result=value);
                claim.outcome=result==null?"unparsed-or-rejected":"accepted";
                if(group<3){periodic=null;yield return services.FetchPeriodicTasks(value=>periodic=value);claim.confirmed=ReceivedPeriod(periodic,group,task.tid);}
                else{pass=null;yield return services.FetchBattlePass(value=>pass=value);claim.confirmed=ReceivedPass(pass,task.tid);}
                yield return services.RefreshWealth(WealthRefreshTrigger.Reward,value=>{});
            }
        }
    }
}
