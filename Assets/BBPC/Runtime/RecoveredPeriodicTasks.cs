using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    public sealed partial class RecoveredHomeDialogs
    {
        private static void ClearChildren(Transform parent)
        {
            for(int i=parent.childCount-1;i>=0;i--)
            {
                var child=parent.GetChild(i).gameObject;child.SetActive(false);
                if(Application.isPlaying)Destroy(child);else DestroyImmediate(child);
            }
        }
        private void RenderDailyTasks(RecoveredTaskPeriodBlock block)=>RenderPeriod(block,false);
        private void RenderWeeklyTasks(RecoveredTaskPeriodBlock block)=>RenderPeriod(block,true);
        private void RenderPeriod(RecoveredTaskPeriodBlock block,bool weekly)
        {
            if(block==null||!IsOpen||root.name!="PeriodicTaskDialog")return;
            var panel=RequirePath(root.transform,"BgPanel/"+(weekly?"WeeklyTask":"DailyTask"));
            var progress=RequirePath(panel,"Progress");
            RequirePath(progress,"Title").GetComponent<Text>().text=weekly?"Tareas semanales":"Tareas diarias";
            string reset="";
            var now=host.Services.CurrentServerTimestamp;
            if(now.HasValue&&block.expiration>0)
            {
                long hours=Math.Max(0,(block.expiration-now.Value)/3600000);
                reset=weekly?"Reinicia en "+hours/24+"d "+hours%24+"h":"Reinicia en "+hours+"h";
            }
            RequirePath(progress,"ResetIn").GetComponent<Text>().text=reset;
            var parent=(RectTransform)RequirePath(panel,"Tasks/Scroll View/Viewport/Content");
            ClearChildren(parent);
            var layout=parent.GetComponent<LayoutGroup>();if(layout!=null)layout.enabled=false;
            var fitter=parent.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=false;
            var data=JsonUtility.FromJson<RecoveredUiData>(Resources.Load<TextAsset>("RecoveredPeriodicTaskCells606/dialogs").text);
            var scene=Array.Find(data.scenes,s=>s.name==(weekly?"PeriodicTaskCellWeekly":"PeriodicTaskCellDaily"));
            int index=0;
            foreach(var task in block.point??Array.Empty<RecoveredTaskPeriodItem>())
            {
                if(task==null)continue;
                var tree=RecoveredUiBuilder.Build(data,scene,parent,host.PortraitCamera);
                RecoveredUiBuilder.UseAuthoredLayoutRoot(tree,scene.name,parent);
                var cell=(RectTransform)tree.Root.transform;
                cell.anchorMin=new Vector2(.5f,1);cell.anchorMax=cell.anchorMin;cell.pivot=new Vector2(.5f,1);
                cell.anchoredPosition=new Vector2(0,-index*150);index++;
                var bg=cell.Find("MainBgImage");
                // TaskCellBase.RefreshUI RVA 0x267F230: completed, short-term, or long-term background.
                long background=task.status==2?-1803258880616120930L:weekly?1079451647511378940L:8173646872781873648L;
                bg.GetComponent<Image>().sprite=RecoveredUiBuilder.Asset<Sprite>(data,"CAB-fc4ad835af77a0fdba838a4924a9c9bd:"+background);
                bg.Find("TaskNameText").GetComponent<Text>().text=task.desc??"";
                int points=0;foreach(var reward in task.rewards??Array.Empty<RecoveredTaskReward>())if(reward!=null&&reward.type==(weekly?14:13))points+=reward.quantity;
                bg.Find("ActivePointText").GetComponent<Text>().text=points+" Pts";
                var timer=bg.Find("BottomRefreshTimeText");timer.gameObject.SetActive(!task.need_progress);
                timer.GetComponent<Text>().text=reset;
                var slider=bg.Find("ProgressSlider").GetComponent<Slider>();
                slider.maxValue=Math.Max(1,task.target);slider.value=task.finish;
                slider.gameObject.SetActive(task.need_progress);
                bg.Find("ProgressSlider/ProgressText").GetComponent<Text>().text=task.finish+"/"+task.target;
                bg.Find("CompleteImage").gameObject.SetActive(task.status==2);
                var button=bg.Find("GetButton").GetComponent<Button>();
                button.gameObject.SetActive(task.status==1);
                BindPeriodClaim(button,task,weekly?"3":"2");
            }
            parent.anchorMin=new Vector2(0,1);parent.anchorMax=Vector2.one;parent.pivot=new Vector2(.5f,1);
            parent.sizeDelta=new Vector2(0,index*150);parent.anchoredPosition=Vector2.zero;
            var milestoneParent=RequirePath(progress,"Reward/Bar/Milestones");ClearChildren(milestoneParent);
            var milestones=block.reward??Array.Empty<RecoveredTaskPeriodItem>();
            // OBSERVED: MileStoneProgressModel .ctor RVA 0x29697C4 assigns current_progress from each task.finish.
            int current=0;foreach(var task in milestones)if(task!=null)current=task.finish;
            RequirePath(progress,"Reward/PtsText").GetComponent<Text>().text=current+" Pts";
            var fill=RequirePath(progress,"Reward/Bar/Fill").GetComponent<Image>();
            int maximum=milestones.Length>0?milestones[milestones.Length-1]?.target??0:0;
            fill.fillAmount=maximum>0?Mathf.Clamp01((float)current/maximum):0;
            var milestoneData=JsonUtility.FromJson<RecoveredUiData>(Resources.Load<TextAsset>("RecoveredPeriodicMilestones606/dialogs").text);
            var milestoneScene=milestoneData.scenes[0];
            for(int i=0;i<milestones.Length;i++)
            {
                var task=milestones[i];if(task==null)continue;
                var tree=RecoveredUiBuilder.Build(milestoneData,milestoneScene,milestoneParent,host.PortraitCamera);
                RecoveredUiBuilder.UseAuthoredLayoutRoot(tree,milestoneScene.name,milestoneParent);
                var cell=(RectTransform)tree.Root.transform;
                // Native Refresh RVA 0x292BD84 positions each target relative to TotalProgress (last target).
                cell.anchorMin=new Vector2(maximum>0?(float)task.target/maximum:0,.5f);cell.anchorMax=cell.anchorMin;cell.anchoredPosition=Vector2.zero;
                cell.Find("Pts").GetComponent<Text>().text=task.target.ToString();
                // PeriodicTaskMilestone.Refresh RVA 0x292BD84 uses the first reward and serialized state arrays.
                cell.Find("Obtained").gameObject.SetActive(task.status==2);
                cell.Find("Fill").gameObject.SetActive(task.status==2);
                cell.Find("Pts").gameObject.SetActive(task.status!=2);
                var rewards=task.rewards??Array.Empty<RecoveredTaskReward>();
                if(rewards.Length==0){cell.gameObject.SetActive(false);continue;}
                var reward=rewards[0];
                int diamonds=reward!=null&&reward.type==1?reward.quantity:0;
                int tickets=reward!=null&&reward.type==12?reward.quantity:0;
                cell.Find("DiamondParent").gameObject.SetActive(reward!=null&&reward.type==1);
                cell.Find("TicketParent").gameObject.SetActive(reward!=null&&reward.type==12);
                long diamondSprite=i==0?2424544928032781294L:i==1?-7099668833663386260L:-2167691532648794727L;
                cell.Find("DiamondParent/diamond").GetComponent<Image>().sprite=RecoveredUiBuilder.Asset<Sprite>(milestoneData,"CAB-fc4ad835af77a0fdba838a4924a9c9bd:"+diamondSprite);
                cell.Find("DiamondParent/Amount").GetComponent<Text>().text=diamonds.ToString();
                cell.Find("TicketParent/Amount").GetComponent<Text>().text=tickets.ToString();
                cell.Find("TicketParent/ticket").GetComponent<Image>().sprite=RecoveredRewardSprites.Ticket(tickets,false);
                cell.sizeDelta=new Vector2(120,200);
                var target=cell.gameObject;
                var image=cell.Find("Pin").GetComponent<Image>();
                foreach(var graphic in cell.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=true;
                var button=target.AddComponent<Button>();
                button.targetGraphic=image;
                BindPeriodClaim(button,task,weekly?"3":"2");
            }
            Canvas.ForceUpdateCanvases();
        }
        private void BindPeriodClaim(Button button,RecoveredTaskPeriodItem task,string type)
        {
            button.onClick.RemoveAllListeners();
            button.interactable=task.status==1&&!string.IsNullOrEmpty(task.tid)&&task.need_ad==0&&!pendingClaims.Contains(task.tid);
            int request=generation;
            button.onClick.AddListener(()=>
            {
                if(!Current(request)||!button.interactable||!pendingClaims.Add(task.tid))return;
                button.interactable=false;StartCoroutine(ClaimPeriod(task,type,request));
            });
        }
        // TaskData.UpdateStatus RVA 0x2928308 and MileStoneProgressModel RVA 0x2969A50.
        internal static void ApplyPeriodPoints(RecoveredTaskPeriodBlock block,RecoveredTaskReward[] rewards,int pointType)
        {
            if(block==null)return;
            int quantity=0;
            foreach(var reward in rewards??Array.Empty<RecoveredTaskReward>())
                if(reward!=null&&reward.type==pointType)quantity=checked(quantity+reward.quantity);
            if(quantity==0)return;
            var milestones=block.reward??Array.Empty<RecoveredTaskPeriodItem>();
            int current=0;foreach(var milestone in milestones)if(milestone!=null)current=milestone.finish;
            int progress=checked(current+quantity);
            bool inInterval=false;int start=0;
            foreach(var milestone in milestones)
            {
                if(milestone==null)continue;
                if(start<progress&&progress<=milestone.target)inInterval=true;
                start=milestone.target;
            }
            foreach(var milestone in milestones)
            {
                if(milestone==null)continue;
                milestone.finish=progress;
                if(inInterval&&milestone.status==0&&milestone.target<=progress)milestone.status=2;
            }
        }
        private IEnumerator ClaimPeriod(RecoveredTaskPeriodItem task,string type,int request)
        {
            RecoveredTaskRewardResultData result=null;
            using(RecoveredClaimLoading.Begin(host.transform,host.PortraitCamera))
                yield return host.Services.ClaimTaskReward(task.tid,type,value=>result=value);
            if(result!=null)
            {
                task.status=2;
                if(Current(request)&&cachedPeriodicData!=null&&(type=="2"||type=="3"))
                    ApplyPeriodPoints(type=="3"?cachedPeriodicData.weekly:cachedPeriodicData.daily,result.rewards,type=="3"?14:13);
            }
            if(Current(request)&&cachedPeriodicData!=null)RenderPeriodicTasks(cachedPeriodicData);
            if(result!=null&&Current(request))ShowClaimRewards(result.rewards,request,0,type=="2"||type=="3");
            // A failed parse does not establish that the server rejected the claim.
            {
                RecoveredTaskPeriodList refreshed=null;
                yield return host.Services.FetchPeriodicTasks(value=>refreshed=value);
                pendingClaims.Remove(task.tid);
                if(Current(request))
                {
                    if(refreshed!=null)RenderPeriodicTasks(refreshed);
                    else if(cachedPeriodicData!=null)RenderPeriodicTasks(cachedPeriodicData);
                }
                yield return host.Services.RefreshWealth(WealthRefreshTrigger.Reward,value=>{});
            }
        }
    }
}
