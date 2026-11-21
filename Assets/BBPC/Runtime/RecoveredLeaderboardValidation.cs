using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    public static partial class RecoveredResult606Validation
    {
        private static IEnumerator CheckLeaderboardPresenters(RecoveredFrontend f,Transform panel,RecoveredLeaderboardPayload area,RecoveredLeaderboardPayload world,string output,Receipt r)
        {
            var cache=f.GetComponent<RecoveredCoverCache>();int attempts=cache.NetworkAttempts;
            bool remote=cache.AllowRemote;string previous=cache.ValidationDirectory;
            string directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"bb-leaderboard-"+Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            var texture=new Texture2D(2,2);texture.SetPixels(new[]{Color.cyan,Color.blue,Color.blue,Color.cyan});texture.Apply();
            var descriptor=new SongImage{urls=new[]{"https://cdn.makeawesomeapps.com/mtools/beatmusic/fixture-leaderboard-"+Guid.NewGuid().ToString("N")+".png"}};
            try
            {
                cache.AllowRemote=true;cache.ValidationDirectory=directory;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,RecoveredCoverCache.Key(descriptor.urls[0])+".png"),texture.EncodeToPNG());
                var parsed=RecoveredReadContracts.ReadLeaderboard("{\"items\":[{\"name_box\":{\"box_id\":\"fixture\"},\"guild\":{\"guild_name\":\"Fixture Guild\"}}]}");
                Require(parsed.items[0].name_box.box_id=="fixture"&&parsed.items[0].guild.guild_name=="Fixture Guild","Leaderboard presentation fields dropped");
                area.items[0].avatar=descriptor;area.items[0].name_box=new RecoveredServerBox{fore_box=descriptor};area.items[0].guild=parsed.items[0].guild;
                area.me.avatar=descriptor;area.me.name_box=area.items[0].name_box;area.me.guild=area.items[0].guild;
                f.HomeDialogs.BindLeaderboardData(area,world);
                yield return null;yield return null;
                var content=panel.Find("RegionList/RScrollView/Viewport/Content");var row=content.GetChild(0);
                var photo=row.Find("PhotoBgImage/PhotoRawImage").GetComponent<RawImage>();
                Require(!row.Find("PhotoBgImage").gameObject.activeSelf,"Rival legacy avatar must remain hidden");
                var ownPhoto=panel.Find("RegionList/DownImage/SelfCellView/PhotoBgImage/PhotoRawImage").GetComponent<RawImage>();
                Require(ownPhoto.enabled&&ownPhoto.GetComponent<RecoveredCoverBinding>().Ready&&ownPhoto.rectTransform.sizeDelta==new Vector2(114,114),"Cached own leaderboard avatar missing or wrong fit");
                var box=row.Find("NameBoxComponent");
                Require(box.GetComponent<Image>().enabled&&box.GetComponent<Image>().sprite!=null,"Cached leaderboard border missing");
                Require(box.Find("NameParent/GuildParent/Horizontal").gameObject.activeInHierarchy&&box.Find("NameParent/GuildParent/Horizontal/GuildTextInside").GetComponent<Text>().text=="Fixture Guild","Authored inside guild missing");
                Require(!box.Find("GuildParent").gameObject.activeSelf&&!box.Find("FriendOrEmoji").gameObject.activeSelf,"Guild outside or friend UI leaked");
                Require(row.GetComponent<Image>().sprite!=content.GetChild(2).GetComponent<Image>().sprite,"Own rank highlight missing");
                Capture(f,output,"leaderboard-presenters-local-fixture",r);
                area.me.avatar=new SongImage{urls=new[]{"https://cdn.makeawesomeapps.com/mtools/beatmusic/fixture-missing-"+Guid.NewGuid().ToString("N")+".png"}};
                area.me.name_box=null;area.me.guild=null;area.items[0].avatar=null;area.items[0].name_box=null;area.items[0].guild=null;
                f.HomeDialogs.RenderLeaderboard(false);
                yield return null;yield return null;
                var own=panel.Find("RegionList/DownImage/SelfCellView");var placeholder=own.Find("PhotoBgImage/PhotoRawImage").GetComponent<RawImage>();
                Require(placeholder.enabled&&placeholder.texture!=null&&placeholder.rectTransform.sizeDelta==new Vector2(38,60)&&!placeholder.GetComponent<RecoveredCoverBinding>().Ready,"Missing avatar retained cached player texture");
                Require(!own.Find("NameBoxComponent").GetComponent<Image>().enabled&&!own.Find("NameBoxComponent/NameParent/GuildParent/Horizontal").gameObject.activeSelf,"Pinned row retained border or guild");
                Require(cache.NetworkAttempts==attempts&&cache.ActiveRequests==0,"Leaderboard presentation attempted network with remote enabled");
                var originalPlaceholder=placeholder.texture;
                own.gameObject.SetActive(false);yield return null;
                own.gameObject.SetActive(true);yield return null;yield return null;
                Require(placeholder.enabled&&placeholder.texture==originalPlaceholder&&placeholder.rectTransform.sizeDelta==new Vector2(38,60)&&!placeholder.GetComponent<RecoveredCoverBinding>().Ready,"Missing-avatar placeholder lost after lifecycle rebind");
                var nullPhoto=panel.Find("RegionList/RScrollView/Viewport/Content").GetChild(0).Find("PhotoBgImage/PhotoRawImage").GetComponent<RawImage>();
                var nullPlaceholder=nullPhoto.texture;
                nullPhoto.gameObject.SetActive(false);yield return null;
                nullPhoto.gameObject.SetActive(true);yield return null;yield return null;
                Require(!nullPhoto.transform.parent.gameObject.activeSelf,"Rival avatar reappeared after lifecycle rebind");
                Require(cache.NetworkAttempts==attempts&&cache.ActiveRequests==0,"Avatar lifecycle rebind attempted network");
                Capture(f,output,"leaderboard-presenters-missing-fixture",r);
            }
            finally
            {
                cache.AllowRemote=remote;cache.ValidationDirectory=previous;UnityEngine.Object.Destroy(texture);
                System.IO.Directory.Delete(directory,true);
            }
        }

        private static IEnumerator CheckLeaderboard(RecoveredFrontend f,string output,Receipt r)
        {
            if(!Check(r,()=>
            {
                string day=RecoveredLocalization.Text("day_singular","_$$_ day").Replace("_$$_","1");
                string days=RecoveredLocalization.Text("day_plural","_$$_ days").Replace("_$$_","2");
                string sixDays=RecoveredLocalization.Text("day_plural","_$$_ days").Replace("_$$_","6");
                string hour=RecoveredLocalization.Text("hour_singular","_$$_ hour").Replace("_$$_","1");
                string minute=RecoveredLocalization.Text("minute_plural","_$$_ minutes").Replace("_$$_","2");
                Require(RecoveredHomeDialogs.LeaderboardRemainingTime(86400000)==RecoveredLocalization.Text("ends_in","Ends in")+" <color=#FEC53D>"+day+"</color>","Leaderboard day format wrong");
                Require(RecoveredHomeDialogs.LeaderboardRemainingTime(172800000)==RecoveredLocalization.Text("ends_in","Ends in")+" <color=#FEC53D>"+days+"</color>","Leaderboard day plural format wrong");
                Require(RecoveredHomeDialogs.LeaderboardRemainingTime(518400000)==RecoveredLocalization.Text("ends_in","Ends in")+" <color=#FEC53D>"+sixDays+"</color>","Leaderboard six-day bubble format wrong");
                Require(RecoveredHomeDialogs.LeaderboardRemainingTime(3600000)==RecoveredLocalization.Text("ends_in","Ends in")+" <color=#FEC53D>"+hour+"</color>","Leaderboard hour format wrong");
                Require(RecoveredHomeDialogs.LeaderboardRemainingTime(120000)==RecoveredLocalization.Text("ends_in","Ends in")+" <color=#FEC53D>"+minute+"</color>","Leaderboard minute format wrong");
                Require(RecoveredHomeDialogs.LeaderboardRemainingTime(0)==RecoveredLocalization.Text("leaderboard_timepassed",""),"Leaderboard expired format wrong");
                var button=Array.Find(f.MainRoom.Root.GetComponentsInChildren<Button>(),b=>b.name=="RankPanelButton");
                Require(button!=null,"Original leaderboard Home button missing");Click(f,button);
            }))yield break;
            yield return null;yield return null;
            var loading=f.GetComponent<RecoveredClaimLoading>();
            float deadline=Time.realtimeSinceStartup+5f;
            while(loading!=null&&loading.References>0&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!Check(r,()=>Require(loading==null||loading.References==0,"Leaderboard initial load did not complete offline")))yield break;
            var area=new RecoveredLeaderboardPayload{title="Area",items=new[]{new RecoveredLeaderboardItem{rank=1,score=1400,screen_name="Fixture One"},new RecoveredLeaderboardItem{rank=2,score=1200,screen_name="Fixture Two"},new RecoveredLeaderboardItem{rank=4,score=800,screen_name="Fixture Four"}},me=new RecoveredLeaderboardItem{rank=4,score=800,screen_name="Fixture Self"}};
            var world=new RecoveredLeaderboardPayload{title="Global",items=new[]{new RecoveredLeaderboardItem{rank=3,score=2000,screen_name="Fixture Global"}},me=new RecoveredLeaderboardItem{rank=100,percent=12.5,score=100,screen_name="Fixture Self"}};
            if(!Check(r,()=>f.HomeDialogs.BindLeaderboardData(area,world)))yield break;
            yield return null;yield return null;
            var canvas=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="RankPanel");
            if(!Check(r,()=>
            {
                Require(canvas!=null,"Original RankPanel not opened");
                f.HomeDialogs.BindLeaderboardCountdown(6L*24*60*60*1000,0);
                var countdown=canvas.transform.Find("SayImage/RestText").GetComponent<Text>();
                Require(countdown.text==RecoveredLocalization.Text("ends_in","Ends in")+" <color=#FEC53D>"+RecoveredLocalization.Text("day_plural","_$$_ days").Replace("_$$_","6")+"</color>","Six-day countdown bubble wrong");
                Require(countdown.enabled&&countdown.gameObject.activeInHierarchy,"Countdown text is not visibly authored on RestText");
                var countdownRect=countdown.rectTransform.rect;
                Require(countdownRect.width>0&&countdownRect.height>0,"Countdown text has no visible rect");
                Capture(f,output,"leaderboard-countdown-six-days",r);
                var content=canvas.transform.Find("RegionList/RScrollView/Viewport/Content");
                Require(content.GetComponentsInChildren<Text>().Length>0&&content.childCount==3,"Original leaderboard rows missing");
                var barRect=(RectTransform)canvas.transform.Find("RegionList/AreaChildBar");
                var listRect=(RectTransform)canvas.transform.Find("RegionList/RScrollView");
                Require(listRect.offsetMax.y<=barRect.offsetMin.y,"Leaderboard list overlaps sub-tabs");
                var scroll=listRect.GetComponent<ScrollRect>();
                Require(!scroll.horizontal&&scroll.vertical&&scroll.movementType==ScrollRect.MovementType.Clamped,"Leaderboard scroll must constrain both bounds vertically");
                Require(scroll.viewport.GetComponent<Mask>()!=null||scroll.viewport.GetComponent<RectMask2D>()!=null,"Leaderboard viewport lacks clipping");
                Require(Mathf.Abs(((RectTransform)content.GetChild(0)).rect.width-((RectTransform)content).rect.width)<.1f,"Leaderboard row does not fill content width");
                Require(content.GetChild(0).Find("OrderImage").gameObject.activeInHierarchy&&!content.GetChild(0).Find("OrderText").gameObject.activeSelf,"First place trophy binding wrong");
                Require(content.GetChild(2).Find("OrderText").GetComponent<Text>().text=="4","Fourth place rank missing");
                Require(canvas.transform.Find("RegionList/DownImage/SelfCellView/ScoreText").GetComponent<Text>().text=="800","Pinned self row missing");
                Capture(f,output,"leaderboard-area-original",r);
            }))yield break;
            var presenters=CheckLeaderboardPresenters(f,canvas.transform,area,world,output,r);
            try
            {
                bool next=true;
                while(next)
                {
                    if(!Check(r,()=>next=presenters.MoveNext()))yield break;
                    if(next)yield return presenters.Current;
                }
            }
            finally{(presenters as IDisposable)?.Dispose();}
            if(!Check(r,()=>Click(f,canvas.transform.Find("RegionList/AreaChildBar/GlobalButton").GetComponent<Button>())))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var content=canvas.transform.Find("RegionList/RScrollView/Viewport/Content");
                Require(content.childCount==1&&content.GetChild(0).Find("ScoreText").GetComponent<Text>().text=="2000","Global tab did not replace list");
                Require(canvas.transform.Find("RegionList/GlobalChildBar").gameObject.activeInHierarchy,"Global selected bar missing");
                Require(canvas.transform.Find("RegionList/DownImage/SelfCellView/OrderText").GetComponent<Text>().text=="100","Pinned nonzero rank must remain numeric");
                var contract=RecoveredReadContracts.ReadLeaderboard("{\"status\":1,\"data\":{\"timezone\":-6,\"items\":[]}}");
                Require(contract.timezone==-6,"Leaderboard timezone must remain numeric");
                Require(RecoveredReadContracts.LeaderboardPrizeBody("00000000000000000000000000000001","fixture-prize")=="{\"client_id\":\"00000000000000000000000000000001\",\"id\":\"fixture-prize\"}","Challenge prize body differs from native contract");
                Require(!RecoveredReadContracts.IsAllowedWrite("/beatmusic/leaderboard/challenge/prize"),"Competition prize write must stay unauthorized");
                Capture(f,output,"leaderboard-global-original",r);
                world.me.rank=0;f.HomeDialogs.RenderLeaderboard(true);
                Require(canvas.transform.Find("RegionList/DownImage/SelfCellView/OrderText").GetComponent<Text>().text==RecoveredLocalization.Text("top_display","Top _$$_ %").Replace("_$$_","13"),"Rank zero percent must truncate plus one");
                world.me.percent=100;f.HomeDialogs.RenderLeaderboard(true);
                Require(canvas.transform.Find("RegionList/DownImage/SelfCellView/OrderText").GetComponent<Text>().text==RecoveredLocalization.Text("top_display","Top _$$_ %").Replace("_$$_","99"),"Rank zero percent cap wrong");
                Click(f,canvas.transform.Find("IntroduceButton").GetComponent<Button>());
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var rules=Array.Find(f.GetComponentsInChildren<Transform>(),c=>c.name=="LeaderRulesDialog");
                Require(rules!=null,"Original leaderboard rules dialog missing");
                var rulesCanvas=rules.GetComponentInChildren<Canvas>(true);
                Require(rulesCanvas!=null&&rulesCanvas.transform.localScale==Vector3.one&&rulesCanvas.sortingOrder>canvas.sortingOrder,"Rules canvas hidden or behind leaderboard");
                Require(f.HomeDialogs.IsOpen&&canvas.gameObject.activeInHierarchy,"Rules replaced leaderboard instead of overlaying it");
                Require(RecoveredReadContracts.AllowsParameter(RecoveredReadContracts.LeaderboardRules,"id")&&!RecoveredReadContracts.AllowsParameter(RecoveredReadContracts.LeaderboardWorld,"id"),"Rules parameter whitelist wrong");
                Require(RecoveredReadContracts.ReadLeaderboardRules("{\"status\":1,\"data\":{\"content\":\"Fixture rules\"}} ")=="Fixture rules","Rules content parser wrong");
                rules.transform.Find("Canvas/Background/ExplainText").GetComponent<Text>().text="Fixture rules";
                Capture(f,output,"leaderboard-rules-original",r);
                Click(f,rules.transform.Find("Canvas/Background/Close").GetComponent<Button>());
            }))yield break;
            yield return null;
            if(!Check(r,()=>
            {
                var challenge=RecoveredReadContracts.ReadLeaderboard("{\"status\":1,\"data\":{\"items\":[],\"seats\":50,\"open_condition\":{\"need_round\":1,\"current_round\":0,\"need_score\":1,\"current_score\":0,\"status\":0}}}");
                Require(challenge.items.Length==0&&challenge.seats==50,"Sanitized leaderboard gate fixture shape changed");
                Require(f.HomeDialogs.BindLeaderboardUnlock(challenge),"Challenge round gate missing");
                Require(canvas.transform.Find("OpenByGame").gameObject.activeSelf&&!canvas.transform.Find("ChallengeList").gameObject.activeSelf,"Challenge gate visibility wrong");
                Require(canvas.transform.Find("OpenByGame/NumSlider/NumText").GetComponent<Text>().text=="0/1","Challenge round progress wrong");
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                Capture(f,output,"leaderboard-unlock-rounds-original",r);
                Require(f.HomeDialogs.BindLeaderboardUnlock(new RecoveredLeaderboardPayload{open_condition=new RecoveredLeaderboardOpenCondition{need_round=3,current_round=3,need_score=1,current_score=0}}),"Challenge score gate missing");
                Require(canvas.transform.Find("OpenByGame/OpenScoreText").gameObject.activeInHierarchy&&!canvas.transform.Find("OpenByGame/NumSlider").gameObject.activeSelf,"Challenge score gate visibility wrong");
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                Capture(f,output,"leaderboard-unlock-score-original",r);
                Require(f.HomeDialogs.BindLeaderboardUnlock(new RecoveredLeaderboardPayload{open_condition=new RecoveredLeaderboardOpenCondition{status=1}}),"Challenge clearing state missing");
                Require(canvas.transform.Find("SettlementPanel").gameObject.activeInHierarchy&&!canvas.transform.Find("OpenByGame").gameObject.activeSelf,"Clearing state did not take priority over unlock");
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                Capture(f,output,"leaderboard-clearing-original",r);
                var challenge=new RecoveredLeaderboardPayload{region_name="Fixture Area",seats=4,items=area.items,me=area.me,section=new[]{new RecoveredLeaderboardSection{rank=2,diamond=300}},open_condition=new RecoveredLeaderboardOpenCondition{need_round=3,current_round=3,need_score=1,current_score=1}};
                Require(!f.HomeDialogs.BindLeaderboardUnlock(challenge),"Unlocked Challenge remains gated");
                f.HomeDialogs.RenderLeaderboard(false);
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                Click(f,canvas.transform.Find("RegionList/RegionBar/ChallengeButton").GetComponent<Button>());
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var content=canvas.transform.Find("ChallengeList/CScrollView/Viewport/Content");
                Require(canvas.transform.Find("ChallengeList").gameObject.activeInHierarchy&&content.childCount==5,"Challenge seat/boundary count wrong");
                Require(canvas.transform.Find("ChallengeList/ChallengeBar/RegionButton/Text").GetComponent<Text>().text=="Fixture Area","Challenge region label missing");
                Require(content.GetChild(2).name=="BoundCellView"&&content.GetChild(2).Find("DiamText").GetComponent<Text>().text=="300","Challenge section index or reward wrong");
                Require(content.GetChild(4).name=="NobodyCellView"&&content.GetChild(4).Find("OrderText").GetComponent<Text>().text=="4","Challenge empty seat wrong");
                Capture(f,output,"leaderboard-challenge-original",r);
                Click(f,canvas.transform.Find("ChallengeList/ChallengeBar/RegionButton").GetComponent<Button>());
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                Require(canvas.transform.Find("RegionList").gameObject.activeInHierarchy&&!canvas.transform.Find("ChallengeList").gameObject.activeSelf,"Challenge return to Area failed");
                Require(!f.HomeDialogs.BindLeaderboardPrize(new RecoveredLeaderboardPrize{rank=0,rewards=new[]{new RecoveredTaskReward{type=1,quantity=300}}}),"Invalid prize rank opened dialog");
                Require(f.HomeDialogs.BindLeaderboardPrize(new RecoveredLeaderboardPrize{id="fixture-prize",rank=2,rewards=new[]{new RecoveredTaskReward{type=1,quantity=300}}}),"Original Challenge prize missing");
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var prize=Array.Find(f.GetComponentsInChildren<Transform>(),t=>t.name=="ChallengeRewardDialog");
                Require(prize!=null,"Challenge prize root hidden");
                var background=prize.Find("Canvas/Background");
                Require(background.Find("SubTitleText/RankText").GetComponent<Text>().text==RecoveredLocalization.Text("rank_number","No._$$_").Replace("_$$_","2"),"Prize rank label wrong");
                Require(background.Find("DiaImage/RewardText").GetComponent<Text>().text=="X300","Prize quantity wrong");
                Require(!background.Find("DoneButton").GetComponent<Button>().interactable,"Unauthorized competition claim enabled");
                Require(prize.GetComponentInChildren<Canvas>().sortingOrder>canvas.sortingOrder,"Prize behind leaderboard");
                Capture(f,output,"leaderboard-prize-original",r);
                f.HomeDialogs.Close();
                Require(!f.HomeDialogs.IsOpen&&f.Services.ReadAttempts.Count==0,"Leaderboard close or isolation failed");
            }))yield break;
            yield return null;
        }
    }
}
