using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    public static partial class RecoveredResult606Validation
    {
        private static void RequireClaimLoadingClosed(RecoveredFrontend f)
        {
            var loading=f.GetComponent<RecoveredClaimLoading>();
            Require(loading!=null&&loading.References==0&&loading.Root!=null&&!loading.Root.activeSelf,"Claim loading remained above reward popup or reconciliation GET");
        }
        private static IEnumerator CheckClaimLoading(RecoveredFrontend f,string output,Receipt r)
        {
            float previousScale=Time.timeScale;
            IDisposable first=null,nested=null;
            try
            {
                Time.timeScale=0;
                if(!Check(r,()=>
                {
                    first=RecoveredClaimLoading.Begin(f.transform,f.PortraitCamera);
                    var loading=f.GetComponent<RecoveredClaimLoading>();
                    Require(loading.Root.activeInHierarchy&&loading.References==1,"Claim loading must show synchronously");
                    var canvas=loading.Root.GetComponent<Canvas>();
                    var parentCanvas=canvas.transform.parent==null?null:canvas.transform.parent.GetComponentInParent<Canvas>();
                    Require(canvas.sortingOrder==32699&&(canvas.isRootCanvas||canvas.overrideSorting),"Claim loading sorting differs from native 32699: overrideSorting="+canvas.overrideSorting+", sortingOrder="+canvas.sortingOrder+", isRootCanvas="+canvas.isRootCanvas+", parent Canvas="+(parentCanvas==null?"none":parentCanvas.name));
                    Require(loading.Root.transform.localScale==Vector3.one,"Hidden loading prefab scale not normalized");
                    var mask=loading.Root.transform.Find("Mask").GetComponent<Image>();
                    Require(mask.raycastTarget&&mask.enabled&&Mathf.Abs(mask.color.a-64f/255f)<.0001f&&mask.color.r==0&&mask.color.g==0&&mask.color.b==0,"Recovered loading mask changed");
                    var spinner=(RectTransform)mask.transform.Find("Loading");
                    Require(spinner.sizeDelta==new Vector2(60,60)&&spinner.anchoredPosition==new Vector2(0,-75)&&spinner.GetComponent<Image>().sprite!=null,"Recovered spinner layout or resource missing");
                }))yield break;
                yield return null;
                if(!Check(r,()=>
                {
                    var loading=f.GetComponent<RecoveredClaimLoading>();
                    var canvas=loading.Root.GetComponent<Canvas>();
                    var mask=loading.Root.transform.Find("Mask").GetComponent<Image>();
                    Canvas.ForceUpdateCanvases();
                    var point=mask.rectTransform.TransformPoint(new Vector3(mask.rectTransform.rect.width*.2f,0,0));
                    var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(f.PortraitCamera,point)};
                    var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                    UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
                    var hitDetails="";foreach(var hit in hits)hitDetails+=hit.gameObject.name+":"+hit.sortingOrder+":"+hit.depth+";";
                    Require(hits.Count>0&&hits[0].gameObject==mask.gameObject,"Loading mask did not block dialog raycasts: hits="+hitDetails+", maskDepth="+mask.depth+", culled="+mask.canvasRenderer.cull+", registered="+UnityEngine.UI.GraphicRegistry.GetGraphicsForCanvas(canvas).Count+", localZ="+mask.transform.localPosition.z+", rootRotation="+canvas.transform.rotation+", first="+(hits.Count>0?hits[0].gameObject.name:"none")+", pointer="+pointer.position+", rect="+mask.rectTransform.rect+", canvasEnabled="+canvas.enabled+", camera="+(canvas.worldCamera==null?"none":canvas.worldCamera.name)+", raycaster="+canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>().enabled);
                    nested=RecoveredClaimLoading.Begin(f.transform,f.PortraitCamera);
                    first.Dispose();first.Dispose();
                    Require(loading.References==1&&loading.Root.activeInHierarchy,"Repeated dispose closed nested loading scope");
                    Capture(f,output,"claim-loading-over-dialog",r);
                }))yield break;
                var indicator=f.GetComponent<RecoveredClaimLoading>().Root.transform.Find("Mask/Loading");
                var before=indicator.localRotation;
                yield return null;yield return null;
                if(!Check(r,()=>
                {
                    Require(Time.timeScale==0&&Quaternion.Angle(before,indicator.localRotation)>.01f,"Loading spinner stopped with timeScale zero");
                    nested.Dispose();nested.Dispose();RequireClaimLoadingClosed(f);
                }))yield break;
            }
            finally
            {
                nested?.Dispose();first?.Dispose();Time.timeScale=previousScale;
            }
            var temporary=new GameObject("Claim loading host cleanup probe",typeof(RectTransform));
            IDisposable destroyedScope=null;GameObject destroyedRoot=null;
            try
            {
                if(!Check(r,()=>{destroyedScope=RecoveredClaimLoading.Begin(temporary.transform,f.PortraitCamera);destroyedRoot=temporary.GetComponent<RecoveredClaimLoading>().Root;UnityEngine.Object.Destroy(temporary);}))yield break;
                yield return null;yield return null;
                Check(r,()=>{Require(destroyedRoot==null,"Destroying host left loading UI alive");destroyedScope.Dispose();destroyedScope.Dispose();});
            }
            finally { destroyedScope?.Dispose();if(temporary!=null)UnityEngine.Object.Destroy(temporary); }
        }
        private static void DismissClaimReward(RecoveredFrontend f)
        {
            RequireClaimLoadingClosed(f);
            var popup=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="GetRewardDialog");
            Require(popup!=null&&popup.sortingOrder==960,"Successful currency claim must show original reward popup");
            var diamond=popup.transform.Find("MaskImage/BFCover/BFIcon/Diamond");
            if(diamond!=null&&diamond.gameObject.activeSelf)Require(diamond.gameObject.activeInHierarchy&&diamond.GetComponent<Image>().sprite!=null,"Claim diamond hidden by inactive parent");
            var buttons=popup.GetComponentsInChildren<Button>();
            var button=Array.Find(buttons,b=>b.name=="GetButton");
            Require(button!=null&&button.interactable,"Reward popup acknowledgment missing");
            Click(f,button);
            Require(!popup.gameObject.activeSelf,"Reward acknowledgment did not close popup");
        }
        private static void CheckClaimCover(RecoveredFrontend f)
        {
            var popup=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="GetRewardDialog");
            var cover=popup.transform.Find("MaskImage/BFCover");
            var binding=cover.GetComponent<RecoveredCoverBinding>();
            var icon=cover.Find("BFIcon").GetComponent<RawImage>();
            Require(binding!=null&&binding.Ready,"Claim response cover did not use packaged cache");
            Require(!icon.transform.Find("Diamond").gameObject.activeSelf,"Response cover retained diamond fallback");
            float scale=Mathf.Min(400f/icon.texture.width,520f/icon.texture.height);
            Require(Vector2.Distance(icon.rectTransform.sizeDelta,new Vector2(icon.texture.width*scale,icon.texture.height*scale))<.01f,"Claim cover differs from native 400x520 fit");
            Require(f.GetComponent<RecoveredCoverCache>().NetworkAttempts==0,"Claim cover attempted network access");
        }
        private static IEnumerator CheckHomeClaims(RecoveredFrontend f,string output,Receipt r)
        {
            var signin=new RecoveredTaskPeriodItem[7];
            for(int i=0;i<7;i++)signin[i]=new RecoveredTaskPeriodItem{tid="fixture-day-"+i,target=i+1,status=i==0?1:0,rewards=i==6?new[]{new RecoveredTaskReward{type=1,quantity=300},new RecoveredTaskReward{type=12,quantity=3}}:new[]{new RecoveredTaskReward{type=i%2==0?1:12,quantity=i%2==0?(i==4?200:100):(i==5?2:1)}}};
            var daily=new RecoveredTaskPeriodItem{tid="fixture-daily",desc="Enter game",status=1,finish=1,target=1,need_progress=true,rewards=new[]{new RecoveredTaskReward{type=13,quantity=20}}};
            var weekly=new RecoveredTaskPeriodItem{tid="fixture-weekly",desc="Finish 5 Freeplay(s)",status=1,finish=5,target=5,need_progress=true,rewards=new[]{new RecoveredTaskReward{type=14,quantity=30}}};
            var weeklyRows=new[]{
                new RecoveredTaskPeriodItem{desc="Get 2 new BF(s)",target=2,need_progress=true,rewards=new[]{new RecoveredTaskReward{type=14,quantity=40}}},
                new RecoveredTaskPeriodItem{desc="Win 10 VS battle(s)",target=10,need_progress=true,rewards=new[]{new RecoveredTaskReward{type=14,quantity=30}}},weekly};
            var milestone=new RecoveredTaskPeriodItem{tid="fixture-milestone",status=1,finish=30,target=30,rewards=new[]{new RecoveredTaskReward{type=1,quantity=50}}};
            var data=new RecoveredTaskPeriodList{signin=new RecoveredSignInBlock{signin_task_type=1,tasks=signin},daily=new RecoveredTaskPeriodBlock{point=new[]{daily},reward=new[]{milestone,new RecoveredTaskPeriodItem{target=100,finish=30,rewards=new[]{new RecoveredTaskReward{type=12,quantity=1}}}}},weekly=new RecoveredTaskPeriodBlock{point=weeklyRows,reward=new[]{new RecoveredTaskPeriodItem{target=30,rewards=new[]{new RecoveredTaskReward{type=1,quantity=200}}},new RecoveredTaskPeriodItem{target=60,rewards=new[]{new RecoveredTaskReward{type=1,quantity=400}}},new RecoveredTaskPeriodItem{target=100,rewards=new[]{new RecoveredTaskReward{type=12,quantity=3}}}}}};
            if(!Check(r,()=>
            {
                bool rejected=false;
                try{RecoveredReadContracts.ReadTaskRewardResult("{\"status\":1,\"data\":{}}");}catch(FormatException){rejected=true;}
                Require(rejected,"Missing rewards accepted as a successful claim");
                var single=RecoveredReadContracts.ReadBattlePassRewardResult("{\"status\":1,\"data\":{\"type\":12,\"quantity\":2}}");
                Require(single.rewards.Length==1&&single.rewards[0].type==12&&single.rewards[0].quantity==2,"Singular battle pass reward lost");
                rejected=false;try{RecoveredReadContracts.ReadBattlePassRewardResult("{\"status\":1,\"data\":{\"rewards\":[]}}");}catch(FormatException){rejected=true;}
                Require(rejected,"Battle pass accepted periodic reward envelope");
            }))yield break;
            if(!Check(r,()=>
            {
                var boundary=new RecoveredTaskPeriodBlock{reward=new[]{new RecoveredTaskPeriodItem{target=30,finish=20,status=2},new RecoveredTaskPeriodItem{target=60,finish=20},new RecoveredTaskPeriodItem{target=100,finish=20}}};
                RecoveredHomeDialogs.ApplyPeriodPoints(boundary,new[]{new RecoveredTaskReward{type=13,quantity=40},new RecoveredTaskReward{type=14,quantity=99}},13);
                Require(boundary.reward[0].status==2&&boundary.reward[1].status==2&&boundary.reward[2].status==0&&boundary.reward[2].finish==60,"Point boundary or reward-type isolation differs from native");
                RecoveredHomeDialogs.ApplyPeriodPoints(boundary,new[]{new RecoveredTaskReward{type=13,quantity=50}},13);
                Require(boundary.reward[2].finish==110&&boundary.reward[2].status==0,"Native overshoot interval guard lost");
                RecoveredHomeDialogs.ApplyPeriodPoints(boundary,Array.Empty<RecoveredTaskReward>(),13);
                Require(boundary.reward[2].finish==110&&boundary.reward[0].status==2,"Empty point rewards changed progress or received status");
            }))yield break;
            int claims=0,wealthReads=0;
            f.Services.OfflineWealthResponse=()=>{wealthReads++;return "{\"status\":1,\"data\":{\"diamond\":"+(1000+claims*50)+",\"bf_draw_ticket\":"+(10+claims*2)+",\"purchases\":{}}}";};
            f.Services.OfflinePost=(route,body)=>{Require(route==RecoveredReadContracts.TaskPeriodReward,"Unexpected home claim route");claims++;int rewardType=claims==2?13:claims==4?14:1;int quantity=claims==2?20:claims==4?30:claims==1?100:50;return "{\"status\":1,\"data\":{\"rewards\":[{\"type\":"+rewardType+",\"quantity\":"+quantity+"}]}}";};
            if(!Check(r,()=>{f.HomeDialogs.ShowPeriodicTasks();f.HomeDialogs.RenderPeriodicTasks(data);}))yield break;
            yield return null;yield return null;
            var dialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="PeriodicTaskDialog").transform;
            yield return CheckClaimLoading(f,output,r);
            if(!string.IsNullOrEmpty(r.error))yield break;
            if(!Check(r,()=>
            {
                var last=dialog.Find("BgPanel/SignIn/Periodic/Viewport/DailyPanel7");
                Require(last.Find("Reward1/Diamond").gameObject.activeInHierarchy&&last.Find("Reward2/BFTicket").gameObject.activeInHierarchy,"Mixed seventh-day rewards hidden");
                var homeData=JsonUtility.FromJson<RecoveredUiData>(Resources.Load<TextAsset>("RecoveredHome606/dialogs").text);
                var daySevenBackground=last.GetComponent<Image>().sprite;
                Require(daySevenBackground!=null&&daySevenBackground==Resources.Load<Sprite>("RecoveredHome606/Sprites/0715chouka03"),"Currency seventh day must use the original 0715chouka03 background");
                Require(daySevenBackground!=RecoveredUiBuilder.Asset<Sprite>(homeData,"CAB-fc4ad835af77a0fdba838a4924a9c9bd:7554234018364002247"),"Currency seventh day retained legacy Super Saiyan artwork");
                // Incomplete day seven has no authored MaskImage; it must instead veil every graphic (background + reward icons) with the .5 gray tint.
                var day7Bg=last.GetComponent<Image>().color;
                Require(day7Bg.r<.6f&&day7Bg.g<.6f&&day7Bg.b<.6f,"Incomplete seventh day is not dimmed (background stayed saturated): "+day7Bg);
                Canvas.ForceUpdateCanvases();
                var ticketLabel=last.Find("VerticalLayoutGroup/RewardContainer").GetComponent<RectTransform>();
                var diamondLabel=last.Find("VerticalLayoutGroup/RewardContainer (1)").GetComponent<RectTransform>();
                Require(ticketLabel.TransformPoint(ticketLabel.rect.center).y>diamondLabel.TransformPoint(diamondLabel.rect.center).y,"Seventh-day ticket is not above diamond after layout");
                Require(Mathf.Abs(ticketLabel.TransformPoint(ticketLabel.rect.center).y-diamondLabel.TransformPoint(diamondLabel.rect.center).y)>1f,"Seventh-day reward labels overlap");
                Require(last.Find("DayText").GetComponent<Text>().text=="Día 7","Seventh-day label must be localized");
                var cells=dialog.Find("BgPanel/SignIn/Periodic/Viewport/Content");
                Require(cells.GetChild(1).Find("BFTicket").gameObject.activeSelf&&!cells.GetChild(1).Find("Diamond").gameObject.activeSelf,"Ticket day retained diamond presentation");
                Require(cells.GetChild(1).Find("BFTicket").GetComponent<Image>().sprite==RecoveredRewardSprites.Ticket(1,true)&&cells.GetChild(5).Find("BFTicket").GetComponent<Image>().sprite==RecoveredRewardSprites.Ticket(2,true),"Ticket quantities retained placeholder sprites");
                Require(cells.GetChild(4).Find("Diamond").GetComponent<Image>().sprite==RecoveredRewardSprites.Diamond(200),"Fifth-day diamond amount sprite wrong");
                Require(last.Find("VerticalLayoutGroup/RewardContainer/RewardText").GetComponent<Text>().text=="BF TICKET x 3"&&last.Find("VerticalLayoutGroup/RewardContainer (1)/RewardText").GetComponent<Text>().text=="300","Seventh-day reward order or amount format wrong");
                Require(last.Find("VerticalLayoutGroup/RewardContainer").GetSiblingIndex()<last.Find("VerticalLayoutGroup/RewardContainer (1)").GetSiblingIndex(),"Seventh-day ticket must precede diamond");
                Require(cells.GetChild(0).Find("Diamond").GetComponent<Image>().sprite==RecoveredRewardSprites.Diamond(100),"Diamond amount sprite wrong");
                Require(RecoveredRewardSprites.Ticket(1,true)!=RecoveredRewardSprites.Ticket(2,true)&&RecoveredRewardSprites.Ticket(1,false)!=RecoveredRewardSprites.Ticket(1,true),"Ticket resource variants collapsed");
                Require(cells.GetChild(1).Find("DayText").GetComponent<Text>().text=="Día 2","Check-in day must use localized task target");
                Require(cells.GetChild(0).Find("RewardContainer/RewardText").GetComponent<Text>().text=="100","Check-in diamond amount must omit plus prefix");
                Require(!cells.GetChild(1).Find("GetButton").gameObject.activeSelf&&cells.GetChild(1).Find("MaskImage").gameObject.activeSelf,"Incomplete check-in must be masked without claim button");
                Require(cells.GetChild(0).Find("GetButton").gameObject.activeSelf&&!cells.GetChild(0).Find("MaskImage").gameObject.activeSelf,"Eligible check-in must show claim without mask");
                Capture(f,output,"check-in-seven-rewards",r);
                var claim=dialog.Find("BgPanel/SignIn/Periodic/Viewport/Content/SignInCell/GetButton").GetComponent<Button>();
                Click(f,claim);
            }))yield break;
            yield return null;yield return null;
            var lightingPopup=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="GetRewardDialog");
            if(!Check(r,()=>Require(lightingPopup!=null,"Missing reward popup for original Lighting animation")))yield break;
            var lighting=lightingPopup.transform.Find("MaskImage/Lighting");
            var fixedIcon=lightingPopup.transform.Find("MaskImage/BFCover/BFIcon");
            var initialIconRotation=fixedIcon.localRotation;
            var initialLightRotation=lighting.localRotation;
            float lightingStart=Time.time;
            yield return new WaitForSeconds(.625f);
            if(!Check(r,()=>{
                float elapsed=Time.time-lightingStart;
                Require(Quaternion.Angle(lighting.localRotation,initialLightRotation*Quaternion.Euler(0,0,-144f*elapsed))<5,"Claim Lighting does not follow original -144 degree/second curve");
                Require(Quaternion.Angle(fixedIcon.localRotation,initialIconRotation)<.001f,"Claim animation rotated BFIcon");
                var diamond=fixedIcon.Find("Diamond");
                Require(diamond!=null&&Quaternion.Angle(diamond.localRotation,Quaternion.identity)<.001f,"Claim animation rotated diamond");
                Capture(f,output,"claim-lighting-original-rotation",r);
            }))yield break;
            if(!Check(r,()=>{Require(signin[0].status==2&&claims==1,"Check-in click did not confirm claim");Require(Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="GetRewardDialog").transform.Find("MaskImage/NameText").GetComponent<Text>().text=="100","First check-in popup reward must be 100");Capture(f,output,"check-in-claim-popup",r);DismissClaimReward(f);Require(wealthReads==1&&f.MainRoom.Find("CountText").GetComponent<Text>().text=="12"&&f.MainRoom.Find("DiamondsText").GetComponent<Text>().text=="1050","Check-in did not refresh account balances in HUD");Click(f,dialog.Find("BgPanel/Buttons/Daily").GetComponent<Button>());}))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var milestones=dialog.Find("BgPanel/DailyTask/Progress/Reward/Bar/Milestones");
                Require(Mathf.Abs(((RectTransform)milestones.GetChild(0)).anchorMin.x-.3f)<.001f,"Milestone must use target/total, not index/count");
                Require(milestones.GetChild(1).Find("TicketParent").gameObject.activeSelf&&milestones.GetChild(1).Find("TicketParent/Amount").GetComponent<Text>().text=="1","Daily ticket milestone binding wrong");
                Require(!milestones.GetChild(0).Find("Fill").gameObject.activeSelf&&milestones.GetChild(0).Find("Pts").gameObject.activeSelf,"Unreceived milestone state wrong");
                Capture(f,output,"daily-original-rewards",r);
                var parent=dialog.Find("BgPanel/DailyTask/Tasks/Scroll View/Viewport/Content");
                var cell=Array.Find(parent.GetComponentsInChildren<Button>(),b=>b.name=="GetButton");
                Require(cell!=null,"Daily original Receive missing");Click(f,cell);
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>{Require(daily.status==2&&claims==2,"Daily click claim failed");Require(data.daily.reward[0].finish==50&&data.daily.reward[1].finish==50,"Daily points not applied locally");Require(Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="GetRewardDialog")==null,"Point-only daily claim opened reward popup");daily.need_progress=false;f.HomeDialogs.RenderPeriodicTasks(data);}))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>{var rows=dialog.Find("BgPanel/DailyTask/Tasks/Scroll View/Viewport/Content");var bg=rows.GetChild(0).Find("MainBgImage");Require(bg.Find("CompleteImage").gameObject.activeSelf&&!bg.Find("GetButton").gameObject.activeSelf&&!bg.Find("ProgressSlider").gameObject.activeSelf&&bg.Find("BottomRefreshTimeText").gameObject.activeSelf,"Completed task without progress must show check and reset text");Capture(f,output,"daily-task-claimed",r);var parent=dialog.Find("BgPanel/DailyTask/Progress/Reward/Bar/Milestones");Click(f,parent.GetComponentInChildren<Button>());}))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>{Require(milestone.status==2&&claims==3,"Milestone click claim failed");DismissClaimReward(f);var cell=dialog.Find("BgPanel/DailyTask/Progress/Reward/Bar/Milestones").GetChild(0);Require(cell.Find("Fill").gameObject.activeSelf&&cell.Find("Obtained").gameObject.activeSelf&&!cell.Find("Pts").gameObject.activeSelf,"Received milestone state wrong");Capture(f,output,"daily-milestone-claimed",r);Click(f,dialog.Find("BgPanel/Buttons/Weekly").GetComponent<Button>());}))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>{var milestones=dialog.Find("BgPanel/WeeklyTask/Progress/Reward/Bar/Milestones");Require(milestones.GetChild(2).Find("TicketParent/Amount").GetComponent<Text>().text=="3","Weekly ticket milestone binding wrong");Capture(f,output,"weekly-original-rewards",r);Click(f,dialog.Find("BgPanel/WeeklyTask/Tasks/Scroll View/Viewport/Content").GetComponentInChildren<Button>());}))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>{Require(weekly.status==2&&claims==4,"Weekly click claim failed");Require(data.weekly.reward[0].finish==30&&data.weekly.reward[0].status==2&&data.weekly.reward[1].status==0,"Weekly points or native milestone transition wrong");Require(Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="GetRewardDialog")==null,"Point-only weekly claim opened reward popup");f.HomeDialogs.Close();}))yield break;
            yield return null;
            data.signin.signin_task_type=0;
            if(!Check(r,()=>{f.HomeDialogs.ShowPeriodicTasks();f.HomeDialogs.RenderPeriodicTasks(data);}))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var newbie=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="PeriodicTaskDialog").transform.Find("BgPanel/SignIn/Newbie/Viewport/DailyPanel7");
                Require(!newbie.parent.Find("UnusedRoleDay7").gameObject.activeSelf&&!newbie.Find("Cover").gameObject.activeSelf&&!newbie.Find("Rarity").gameObject.activeSelf,"Currency fixture displayed Newbie role banner");
                Require(newbie.gameObject.activeInHierarchy&&newbie.Find("Reward1/Diamond").gameObject.activeInHierarchy&&newbie.Find("Reward2/BFTicket").gameObject.activeInHierarchy,"Newbie currency rewards missing from two-reward layout");
                Require(newbie.Find("VerticalLayoutGroup/RewardContainer (1)/RewardText").GetComponent<Text>().text=="300"&&newbie.Find("VerticalLayoutGroup/RewardContainer/RewardText").GetComponent<Text>().text=="BF TICKET x 3","Newbie currency amounts rendered incorrectly");
                Capture(f,output,"check-in-newbie-rewards",r);f.HomeDialogs.Close();
            }))yield break;
            yield return null;
            var bonus=new RecoveredTaskPeriodItem{tid="fixture-bonus-click",target=700,status=1,rewards=new[]{new RecoveredTaskReward{type=1,quantity=100}}};
            var levels=new RecoveredBattlePassLevel[6];
            for(int i=0;i<levels.Length;i++)levels[i]=new RecoveredBattlePassLevel{level=i,free=new RecoveredTaskPeriodItem{target=(i+1)*100,status=2,rewards=new[]{new RecoveredTaskReward{type=1,quantity=25*(i+1)}}},vip=new RecoveredTaskPeriodItem{target=(i+1)*100,status=2,rewards=new[]{new RecoveredTaskReward{type=1,quantity=100*(i+1)}}}};
            var pass=new RecoveredBattlePassData{bid="fixture-pass-click",point=700,has_purchased=true,level_tasks=levels,bonus=new[]{bonus}};
            f.Services.OfflinePost=(route,body)=>{Require(route==RecoveredReadContracts.BattlePassReward&&body.Contains("\"type\":0"),"Bonus click wrong contract");claims++;return "{\"status\":1,\"data\":{\"type\":1,\"quantity\":100,\"cover\":{\"urls\":[\"http://cdn.makeawesomeapps.com/mtools/beatmusic/image/Garcello_song1__0.png\"]}}}";};
            if(!Check(r,()=>{f.HomeDialogs.ShowBattlePass();f.HomeDialogs.BindBattlePassRewards(pass);}))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>{var passDialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="BattlePassDialog");Capture(f,output,"battle-pass-populated",r);passDialog.transform.Find("Rewards/RewardScroll").GetComponent<ScrollRect>().verticalNormalizedPosition=0;}))yield break;
            yield return null;yield return null;
            Vector2 bonusScrollPosition=Vector2.zero;
            if(!Check(r,()=>
            {
                var passDialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="BattlePassDialog");
                var passScroll=passDialog.transform.Find("Rewards/RewardScroll").GetComponent<ScrollRect>();
                bonusScrollPosition=passScroll.content.anchoredPosition;
                Require(passScroll.verticalNormalizedPosition<.01f,"Bonus test must reach bottom of scroll");
                f.HomeDialogs.BindBattlePassRewards(pass);
                Require(Vector2.Distance(passScroll.content.anchoredPosition,bonusScrollPosition)<.1f,"Same-list refresh reset BattlePass scroll immediately");
            }))yield break;
            // Destroy is deferred; let ScrollRect and clipping settle before the real raycast.
            yield return null;yield return null;
            if(!Check(r,()=>{var passDialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="BattlePassDialog");var passScroll=passDialog.transform.Find("Rewards/RewardScroll").GetComponent<ScrollRect>();Require(Vector2.Distance(passScroll.content.anchoredPosition,bonusScrollPosition)<.1f,"Same-list refresh reset BattlePass scroll after layout");Capture(f,output,"battle-pass-bonus",r);Click(f,Array.Find(passDialog.GetComponentsInChildren<Button>(),b=>b.name=="ReveiceBtn"));}))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>{Require(bonus.status==2&&claims==5,"Bonus click claim failed");CheckClaimCover(f);Capture(f,output,"battle-pass-claim-cover-fixture",r);DismissClaimReward(f);var passDialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="BattlePassDialog");Require(!Array.Find(passDialog.GetComponentsInChildren<Button>(true),b=>b.name=="ReveiceBtn").gameObject.activeSelf,"Exhausted bonus claim must be hidden");Capture(f,output,"battle-pass-bonus-claimed",r);Require(f.Services.ReadAttempts.Count==0,"Remote request in home claims validation");f.HomeDialogs.Close();}))yield break;
            var free=new RecoveredTaskPeriodItem{tid="fixture-free-click",status=1,rewards=new[]{new RecoveredTaskReward{type=1,quantity=25}}};
            var vip=new RecoveredTaskPeriodItem{tid="fixture-vip-click",status=1,rewards=new[]{new RecoveredTaskReward{type=1,quantity=100}}};
            pass.level_tasks=new[]{new RecoveredBattlePassLevel{level=0,free=free,vip=vip}};pass.bonus=Array.Empty<RecoveredTaskPeriodItem>();
            pass.has_purchased=false;
            if(!Check(r,()=>{f.HomeDialogs.ShowBattlePass();f.HomeDialogs.BindBattlePassRewards(pass);}))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var content=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="BattlePassDialog").transform.Find("Rewards/RewardScroll/Viewport/Content");
                Require(!content.GetChild(0).Find("VIpPass/Diamond/ReceiveBtn").GetComponent<Button>().interactable,"VIP claim enabled without entitlement");
                pass.has_purchased=true;f.HomeDialogs.BindBattlePassRewards(pass);
            }))yield break;
            yield return null;yield return null;
            var rewardRoot=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="BattlePassDialog").transform.Find("Rewards/RewardScroll/Viewport/Content");
            if(!Check(r,()=>Click(f,rewardRoot.GetChild(0).Find("Free/Diamond/ReceiveBtn").GetComponent<Button>())))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>{Require(free.status==2,"Free pass click claim failed");DismissClaimReward(f);}))yield break;
            // Popup destruction and rebuilt cell clipping complete at the frame boundary.
            yield return null;
            if(!Check(r,()=>
            {
                var passDialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="BattlePassDialog");
                var passScroll=passDialog.transform.Find("Rewards/RewardScroll").GetComponent<ScrollRect>();
                Canvas.ForceUpdateCanvases();
                var button=Array.Find(passScroll.content.GetComponentsInChildren<Button>(),b=>b.name=="ReceiveBtn"&&b.transform.parent.parent.name=="VIpPass");
                Require(button!=null,"VIP Receive missing after closing free reward popup");
                var rect=(RectTransform)button.transform;
                var center=passScroll.viewport.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
                Require(passScroll.viewport.rect.Contains(new Vector2(center.x,center.y)),"VIP Receive center outside viewport after closing popup");
                Click(f,button);
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>{Require(vip.status==2&&claims==7,"VIP pass must claim with Normal type 0");Require(wealthReads==7&&f.MainRoom.Find("CountText").GetComponent<Text>().text=="24"&&f.MainRoom.Find("DiamondsText").GetComponent<Text>().text=="1350","Home claims did not refresh account balances");Capture(f,output,"battle-pass-free-vip-claimed",r);f.HomeDialogs.Close();}))yield break;
            yield return null;
            f.Services.OfflinePost=(route,body)=>"{\"status\":1,\"data\":{}}";
            signin[0].status=1;
            if(!Check(r,()=>{f.HomeDialogs.ShowPeriodicTasks();f.HomeDialogs.RenderPeriodicTasks(data);}))yield break;
            yield return null;yield return null;
            var retry=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="PeriodicTaskDialog").transform.Find("BgPanel/SignIn/Newbie/Viewport/Content/SignInCell/GetButton").GetComponent<Button>();
            if(!Check(r,()=>Click(f,retry)))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>{Require(signin[0].status==1&&retry.interactable,"Failed claim must preserve eligibility and permit retry");f.HomeDialogs.Close();}))yield break;
            // Synthetic coverage of the reconstructed sequence, not evidence of original mixed-popup fidelity.
            int mixedClaims=0,mixedWealthReads=0;
            int mixedPostBaseline=f.Services.OfflinePostCount;
            var mixedTask=new RecoveredTaskPeriodItem{tid="fixture-signin-mixed-ticket3-diamond300",target=1,status=1,rewards=new[]{new RecoveredTaskReward{type=12,quantity=3},new RecoveredTaskReward{type=1,quantity=300},new RecoveredTaskReward{type=3,quantity=1,name_box=new RecoveredServerBox{box_title="Fixture Border"}},new RecoveredTaskReward{type=5,quantity=1,decor=new RecoveredServerDecor{title="Fixture Decor",locations=new[]{new RecoveredDecorLocation{location=9},new RecoveredDecorLocation{location=2},new RecoveredDecorLocation{location=7}}}}}};
            var mixedData=new RecoveredTaskPeriodList{signin=new RecoveredSignInBlock{signin_task_type=1,tasks=new[]{mixedTask}}};
            f.Services.OfflinePost=(route,body)=>
            {
                Require(route==RecoveredReadContracts.TaskPeriodReward&&body.Contains("\"tid\":\""+mixedTask.tid+"\"")&&body.Contains("\"type\":\"1\""),"Mixed check-in used wrong claim contract");
                mixedClaims++;
                return "{\"status\":1,\"data\":{\"rewards\":[{\"type\":12,\"quantity\":3},{\"type\":1,\"quantity\":300},{\"type\":3,\"quantity\":1,\"name_box\":{\"box_title\":\"Fixture Border\"}},{\"type\":5,\"quantity\":1,\"decor\":{\"title\":\"Fixture Decor\",\"locations\":[{\"location\":9},{\"location\":2},{\"location\":7}]}}]}}";
            };
            f.Services.OfflineWealthResponse=()=>{mixedWealthReads++;return "{\"status\":1,\"data\":{\"diamond\":1650,\"bf_draw_ticket\":27,\"purchases\":{}}}";};
            if(!Check(r,()=>{f.HomeDialogs.ShowPeriodicTasks();f.HomeDialogs.RenderPeriodicTasks(mixedData);}))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                Require(mixedTask.status==1&&mixedClaims==0&&mixedWealthReads==0&&f.Services.OfflinePostCount==mixedPostBaseline,"Mixed fixture claimed before click");
                var periodic=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="PeriodicTaskDialog");
                var day=periodic.transform.Find("BgPanel/SignIn/Periodic/Viewport/Content").GetChild(0);
                Require(day.Find("DayText").GetComponent<Text>().text=="Día 1","Mixed fixture must use localized repeated day one");
                Click(f,day.Find("GetButton").GetComponent<Button>());
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                Require(mixedTask.status==2&&mixedClaims==1&&f.Services.OfflinePostCount==mixedPostBaseline+1,"Mixed click must issue exactly one POST and confirm claim");
                Require(f.Services.LastTaskRewardTid==mixedTask.tid&&f.Services.LastTaskRewardType=="1"&&f.Services.ReadAttempts.Count==0,"Mixed claim identity or offline isolation failed");
                var popup=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="GetRewardDialog");
                Require(popup!=null&&popup.sortingOrder==960,"Mixed ticket popup missing");
                var ticket=popup.transform.Find("MaskImage/BFTicket");
                var icon=ticket.Find("CardRawImage").GetComponent<Image>();
                var count=ticket.Find("CountText").GetComponent<Text>();
                Require(ticket.gameObject.activeInHierarchy&&icon.gameObject.activeInHierarchy&&icon.enabled&&icon.sprite!=null&&icon.sprite==RecoveredRewardSprites.Ticket(3,true)&&count.gameObject.activeInHierarchy&&count.text=="x3","Mixed first popup must show ticket3 and x3");
                Require(!popup.transform.Find("MaskImage/BFCover").gameObject.activeSelf,"Mixed first popup retained diamond cover");
                Capture(f,output,"check-in-mixed-ticket3-popup",r);
                Require(mixedWealthReads==1&&f.MainRoom.Find("CountText").GetComponent<Text>().text=="27"&&f.MainRoom.Find("DiamondsText").GetComponent<Text>().text=="1650","Mixed claim did not refresh wealth");
                Click(f,popup.transform.Find("MaskImage/GetButton").GetComponent<Button>());
                var next=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="GetRewardDialog");
                Require(!popup.gameObject.activeSelf&&next!=null&&next!=popup&&next.gameObject.activeInHierarchy,"First acknowledgment must advance to next popup, not finish sequence");
                Require(mixedClaims==1&&f.Services.OfflinePostCount==mixedPostBaseline+1&&f.Services.ReadAttempts.Count==0,"First acknowledgment issued another POST or real transport");
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var popup=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="GetRewardDialog");
                Require(popup!=null&&popup.sortingOrder==960,"Mixed diamond popup missing after ticket acknowledgment");
                var diamond=popup.transform.Find("MaskImage/BFCover/BFIcon/Diamond").GetComponent<Image>();
                var amount=popup.transform.Find("MaskImage/NameText").GetComponent<Text>();
                Require(diamond.gameObject.activeInHierarchy&&diamond.enabled&&diamond.sprite!=null&&diamond.sprite==RecoveredRewardSprites.Diamond(300)&&amount.gameObject.activeInHierarchy&&amount.text=="300","Mixed second popup must show visible diamond300");
                Require(!popup.transform.Find("MaskImage/BFTicket").gameObject.activeSelf,"Mixed diamond popup retained ticket presenter");
                Require(mixedClaims==1&&f.Services.OfflinePostCount==mixedPostBaseline+1,"Mixed sequence posted again before final acknowledgment");
                Capture(f,output,"check-in-mixed-diamond300-popup",r);
                DismissClaimReward(f);
                var borderPopup=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="GetRewardDialog");
                Require(borderPopup!=null&&borderPopup.transform.Find("MaskImage/NameBoxComponent").gameObject.activeInHierarchy,"Name-box reward used wrong presenter");
                Require(borderPopup.transform.Find("MaskImage/NameText").GetComponent<Text>().text=="Fixture Border","Name-box title wrong");
                var border=borderPopup.transform.Find("MaskImage/NameBoxComponent");
                Require(!border.Find("NameParent/NameText").gameObject.activeSelf&&!border.Find("rankingText").gameObject.activeSelf&&!border.GetComponent<Image>().enabled,"Name-box retained authored examples or missing image placeholder");
                var cache=f.GetComponent<RecoveredCoverCache>()??f.gameObject.AddComponent<RecoveredCoverCache>();
                int attempts=cache.NetworkAttempts;bool remote=cache.AllowRemote;cache.AllowRemote=true;
                try
                {
                    var binding=border.GetComponent<RecoveredCoverBinding>();
                    Require(!binding.BindLocalSprite(cache,new SongImage{urls=new[]{"https://cdn.makeawesomeapps.com/mtools/beatmusic/fixture-border-missing.png"}},border.GetComponent<Image>())&&cache.NetworkAttempts==attempts,"Local-only border attempted network");
                    string directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"bb-border-"+Guid.NewGuid().ToString("N"));
                    System.IO.Directory.CreateDirectory(directory);
                    string previousDirectory=cache.ValidationDirectory;
                    var descriptor=new SongImage{urls=new[]{"https://cdn.makeawesomeapps.com/mtools/beatmusic/fixture-border-"+Guid.NewGuid().ToString("N")+".png"}};
                    var texture=new Texture2D(2,2);
                    try
                    {
                        texture.SetPixels(new[]{Color.cyan,Color.blue,Color.blue,Color.cyan});texture.Apply();
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,RecoveredCoverCache.Key(descriptor.urls[0])+".png"),texture.EncodeToPNG());
                        cache.ValidationDirectory=directory;
                        Require(binding.BindLocalSprite(cache,descriptor,border.GetComponent<Image>())&&border.GetComponent<Image>().sprite!=null&&border.GetComponent<Image>().enabled&&cache.NetworkAttempts==attempts,"Cached border sprite binding failed");
                    }
                    finally{cache.ValidationDirectory=previousDirectory;UnityEngine.Object.Destroy(texture);}
                    border.gameObject.SetActive(false);border.gameObject.SetActive(true);
                }
                finally{cache.AllowRemote=remote;}
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var restored=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="GetRewardDialog").transform.Find("MaskImage/NameBoxComponent").GetComponent<Image>();
                Require(restored.enabled&&restored.sprite!=null,"Local border lost after reactivation");
                Capture(f,output,"check-in-namebox-local-fixture",r);
                var textureHost=new GameObject("LocalDecorBindingFixture",typeof(RectTransform),typeof(RawImage));
                textureHost.transform.SetParent(f.transform,false);
                var target=textureHost.GetComponent<RawImage>();target.rectTransform.anchoredPosition=new Vector2(17,23);
                var textureBinding=textureHost.AddComponent<RecoveredCoverBinding>();
                var cache=f.GetComponent<RecoveredCoverCache>();int attempts=cache.NetworkAttempts;bool remote=cache.AllowRemote;
                string directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"bb-decor-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(directory);
                string previousDirectory=cache.ValidationDirectory;
                var descriptor=new SongImage{urls=new[]{"https://cdn.makeawesomeapps.com/mtools/beatmusic/fixture-decor-"+Guid.NewGuid().ToString("N")+".png"}};
                var texture=new Texture2D(8,2);
                try
                {
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,RecoveredCoverCache.Key(descriptor.urls[0])+".png"),texture.EncodeToPNG());
                    cache.AllowRemote=true;cache.ValidationDirectory=directory;
                    Require(textureBinding.BindLocalTexture(cache,descriptor,target,new Vector2(400,520)),"Cached decor texture missing");
                    Require(target.rectTransform.sizeDelta==new Vector2(400,100)&&target.rectTransform.anchoredPosition==new Vector2(17,23),"Decor fit changed native aspect or authored position");
                    Require(cache.NetworkAttempts==attempts,"Local decor attempted network");
                }
                finally{cache.AllowRemote=remote;cache.ValidationDirectory=previousDirectory;UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(textureHost);}
                DismissClaimReward(f);
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var decorPopup=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="GetRewardDialog");
                Require(decorPopup!=null&&decorPopup.transform.Find("MaskImage/DecorCover").gameObject.activeInHierarchy&&decorPopup.transform.Find("MaskImage/NameText").GetComponent<Text>().text=="Fixture Decor","Decor reward used wrong presenter or title");
                Require(!decorPopup.transform.Find("MaskImage/NameBoxComponent").gameObject.activeSelf&&decorPopup.transform.Find("MaskImage/DecorCover/IconImage").GetComponent<Image>().enabled&&decorPopup.transform.Find("MaskImage/DecorCover/IconImage").GetComponent<Image>().sprite==RecoveredRewardSprites.DecorLocation(mixedTask.rewards[3].decor)&&!decorPopup.transform.Find("MaskImage/DecorCover/DecorIcon").GetComponent<RawImage>().enabled,"Decor retained authored example image");
                Require(RecoveredRewardSprites.DecorLocationKey(mixedTask.rewards[3].decor.locations)=="TLR","Decor locations not ordered by native enum value");
                Require(RecoveredRewardSprites.DecorLocationKey(new[]{new RecoveredDecorLocation{location=7},new RecoveredDecorLocation{location=7}})=="LL","Decor location multiplicity lost");
                Require(RecoveredRewardSprites.DecorLocation(new RecoveredServerDecor())==null,"Missing decor location retained example icon");
                Capture(f,output,"check-in-decor-location-fixture",r);
                DismissClaimReward(f);
                Require(Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="GetRewardDialog")==null,"Mixed final acknowledgment left an active reward popup");
                Require(mixedTask.status==2&&mixedClaims==1&&f.Services.OfflinePostCount==mixedPostBaseline+1&&f.Services.ReadAttempts.Count==0,"Final acknowledgment issued another POST or real transport");
                Require(mixedWealthReads==1&&f.MainRoom.Find("CountText").GetComponent<Text>().text=="27"&&f.MainRoom.Find("DiamondsText").GetComponent<Text>().text=="1650","Mixed acknowledgments changed refreshed wealth");
                f.HomeDialogs.Close();
            }))yield break;
            f.Services.OfflinePost=null;f.Services.OfflineWealthResponse=null;
        }
    }
}
