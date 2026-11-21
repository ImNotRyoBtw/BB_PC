using System;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    public sealed partial class RecoveredHomeDialogs
    {
        internal void RenderPeriodicSignIn(RecoveredTaskPeriodList data)
        {
            if(data?.signin==null||!IsOpen||root.name!="PeriodicTaskDialog")return;
            bool newbie=data.signin.signin_task_type==0;
            var signIn=RequirePath(root.transform,"BgPanel/SignIn");
            bool visible=signIn.Find("Newbie").gameObject.activeSelf||signIn.Find("Periodic").gameObject.activeSelf;
            signIn.Find("Newbie").gameObject.SetActive(visible&&newbie);
            signIn.Find("Periodic").gameObject.SetActive(visible&&!newbie);
            var viewport=RequirePath(signIn,(newbie?"Newbie":"Periodic")+"/Viewport");
            var content=RequirePath(viewport,"Content");
            foreach(Transform cell in content)cell.gameObject.SetActive(false);
            var last=viewport.Find("DailyPanel7");if(last!=null)last.gameObject.SetActive(false);
            var tasks=data.signin.tasks??Array.Empty<RecoveredTaskPeriodItem>();
            if(newbie&&tasks.Length>6&&Array.Exists(tasks[6]?.rewards??Array.Empty<RecoveredTaskReward>(),r=>r!=null&&(r.type==1||r.type==12))&&last!=null&&last.Find("Reward2")==null)
            {
                // Six regular cells are authored; currency day seven needs the two-reward layout.
                var banner=last;banner.gameObject.SetActive(false);banner.name="UnusedRoleDay7";
                last=Instantiate(RequirePath(signIn,"Periodic/Viewport/DailyPanel7").gameObject,viewport).transform;
                last.name="DailyPanel7";
            }
            for(int i=0;i<Math.Min(tasks.Length,7);i++)
            {
                var task=tasks[i];if(task==null)continue;
                var cell=i==6&&last!=null?last:content.GetChild(i);
                cell.gameObject.SetActive(true);
                bool wide=i==6&&last!=null;
                cell.Find("DayText").GetComponent<Text>().text=RecoveredLocalization.Language=="es"?"Día "+task.target:"DAY"+task.target;
                foreach(var name in new[]{"Cover","Rarity","Diamond","BFTicket","Reward1","Reward2"})
                {var node=cell.Find(name);if(node!=null)node.gameObject.SetActive(false);}
                var rewards=task.rewards??Array.Empty<RecoveredTaskReward>();
                var firstLabel=cell.Find(wide?"VerticalLayoutGroup/RewardContainer":"RewardContainer");
                var secondLabel=cell.Find("VerticalLayoutGroup/RewardContainer (1)");
                if(firstLabel!=null)firstLabel.gameObject.SetActive(false);
                if(secondLabel!=null)secondLabel.gameObject.SetActive(false);
                if(wide)
                {
                    // Original day-seven background supplied and confirmed locally by the user.
                    var background=cell.GetComponent<Image>();
                    background.sprite=Resources.Load<Sprite>("RecoveredHome606/Sprites/0715chouka03");
                    background.color=Color.white;background.type=Image.Type.Simple;
                    var reward1=cell.Find("Reward1");var reward2=cell.Find("Reward2");
                    if(reward1!=null)reward1.SetSiblingIndex(0);
                    if(reward2!=null)reward2.SetSiblingIndex(1);
                    var labels=cell.Find("VerticalLayoutGroup");
                    if(labels!=null){firstLabel?.SetSiblingIndex(0);secondLabel?.SetSiblingIndex(1);}
                }
                for(int r=0;r<rewards.Length;r++)
                {
                    var reward=rewards[r];if(reward==null)continue;
                    bool diamond=reward.type==1, ticket=reward.type==12;
                    var label=wide?(ticket?firstLabel:diamond?secondLabel:(r==0?firstLabel:secondLabel)):(r==0?firstLabel:secondLabel);
                    if(label!=null)
                    {
                        label.gameObject.SetActive(true);
                        label.Find("RewardText").GetComponent<Text>().text=ticket?(reward.quantity>1?"BF TICKET x "+reward.quantity:"BF TICKET"):(diamond?reward.quantity.ToString():"+"+reward.quantity);
                    }
                    if(diamond||ticket)
                    {
                        string icon=diamond?"Diamond":"BFTicket";
                        var view=wide?cell.Find(diamond?"Reward1":ticket?"Reward2":(r==0?"Reward1":"Reward2")):cell;
                        if(view!=null)
                        {
                            view.gameObject.SetActive(true);
                            var image=view.Find(icon);
                            if(image!=null)
                            {
                                image.gameObject.SetActive(true);
                                var graphic=image.GetComponent<Image>();
                                if(graphic!=null)graphic.sprite=ticket?RecoveredRewardSprites.Ticket(reward.quantity,true):RecoveredRewardSprites.Diamond(reward.quantity);
                            }
                        }
                        if(label!=null)
                        {
                            var iconImage=label.Find("DiamondImage").GetComponent<Image>();
                            iconImage.gameObject.SetActive(diamond);
                        }
                    }
                    else if(reward.type==2&&reward.role?.icon!=null)
                    {
                        var cover=cell.Find("Cover");cover.gameObject.SetActive(true);
                        foreach(var name in new[]{"RarityIcon","UpgradeIcon"}){var node=cover.Find(name);if(node!=null)node.gameObject.SetActive(false);}
                        var cache=host.GetComponent<RecoveredCoverCache>()??host.gameObject.AddComponent<RecoveredCoverCache>();
                        var binding=cover.GetComponent<RecoveredCoverBinding>()??cover.gameObject.AddComponent<RecoveredCoverBinding>();
                        binding.Bind(cache,reward.role.icon,cover.Find("BFIcon").GetComponent<RawImage>(),null,true);
                        if(label!=null)label.Find("RewardText").GetComponent<Text>().text=reward.role.title??"";
                    }
                }
                cell.Find("Got").gameObject.SetActive(task.status==2);
                var mask=cell.Find("MaskImage");
                if(mask!=null)mask.gameObject.SetActive(task.status!=1);
                else foreach(var path in new[]{"Cover/BFIcon","Rarity/RarityIcon"})
                {
                    var graphic=cell.Find(path)?.GetComponent<Graphic>();
                    if(graphic!=null)graphic.color=task.status==1?Color.white:new Color(.5f,.5f,.5f,1);
                }
                if(wide&&task.status!=1)
                {
                    // Day-seven cell has no authored MaskImage; the others veil with one. Mirror that
                    // veil by graying every graphic (background art, diamond/ticket icons, labels).
                    foreach(var graphic in cell.GetComponentsInChildren<Graphic>(true))
                        if(graphic!=null)graphic.color=new Color(.5f,.5f,.5f,1);
                }
                var button=cell.Find("GetButton").GetComponent<Button>();
                button.gameObject.SetActive(task.status==1);
                BindPeriodClaim(button,task,"1");
            }
            Canvas.ForceUpdateCanvases();
        }
    }
}
