using System;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    // Equivalent binding from 6.0.6 RefreshCellView/SetTaskToRewardContainer and container-mapping.json.
    // Role/GF, NameBox and Decor use DTO images; rarity and other reward types remain pending.
    internal static class RecoveredBattlePassCells
    {
        internal static void Apply(Transform dialog, RecoveredBattlePassData pass, Camera camera, Action<RecoveredTaskPeriodItem,int,Button> claim=null, bool preserveScroll=false)
        {
            var parent=dialog.Find("Rewards/RewardScroll/Viewport/Content") as RectTransform;
            if(parent==null)throw new InvalidOperationException("Missing original BattlePass content");
            var scroll=dialog.Find("Rewards/RewardScroll").GetComponent<ScrollRect>();
            if(scroll!=null)scroll.StopMovement();
            var position=parent.anchoredPosition;
            for(int i=parent.childCount-1;i>=0;i--)
            {
                var child=parent.GetChild(i).gameObject;child.SetActive(false);
                if(Application.isPlaying)UnityEngine.Object.Destroy(child);else UnityEngine.Object.DestroyImmediate(child);
            }
            var resource=Resources.Load<TextAsset>("RecoveredBattlePassCells606/dialogs");
            if(resource==null)throw new InvalidOperationException("Missing original BattlePass cells");
            var data=JsonUtility.FromJson<RecoveredUiData>(resource.text);
            var scene=Array.Find(data.scenes,s=>s.name=="BattlePassRewardCell");
            if(scene==null)throw new InvalidOperationException("Missing BattlePassRewardCell scene");
            int count=0;
            // Scroller 0x29D52C4 uses ordinary reward cells and a separate extra-reward cell.
            foreach(var level in pass.level_tasks??Array.Empty<RecoveredBattlePassLevel>())
            {
                if(level==null)continue;
                var tree=RecoveredUiBuilder.Build(data,scene,parent,camera);
                RecoveredUiBuilder.UseAuthoredLayoutRoot(tree,scene.name,parent);
                var cell=tree.Root.transform as RectTransform;
                cell.anchoredPosition=new Vector2(0,-count*cell.sizeDelta.y);
                var label=cell.Find("Rank/Text").GetComponent<Text>();label.text=(level.level+1).ToString();
                // Number sprites require their native digit binding; retain the authored Text fallback.
                cell.Find("Rank/Numbers").gameObject.SetActive(false);
                cell.Find("CellMask").gameObject.SetActive(false);
                BindContainer(data,cell.Find("Free"),level.free);
                BindContainer(data,cell.Find("VIpPass"),level.vip);
                BindClaim(cell.Find("Free"),level.free,0,claim);
                BindClaim(cell.Find("VIpPass"),level.vip,0,pass.has_purchased?claim:null);
                count++;
            }
            // EnhancedScroller is not available in the port; retain authored cell height in ScrollRect.
            var height=scene.objects[0].rect[7];
            var extraScene=Array.Find(data.scenes,s=>s.name=="BattlePassExtraRewardCell");
            if(extraScene!=null)
            {
                var extra=RecoveredUiBuilder.Build(data,extraScene,parent,camera);
                RecoveredUiBuilder.UseAuthoredLayoutRoot(extra,extraScene.name,parent);
                var rect=(RectTransform)extra.Root.transform;
                rect.anchoredPosition=new Vector2(0,-count*height);
                var bonus=pass.bonus??Array.Empty<RecoveredTaskPeriodItem>();
                int received=0;RecoveredTaskPeriodItem available=null;
                foreach(var task in bonus)if(task!=null){if(task.status==2)received++;else if(task.status==1&&available==null)available=task;}
                // RefreshCellView RVA 0x2AF27A8 also exposes a level-based fallback when no task has status1.
                int unlocked=((pass.level_tasks?.Length??0)+bonus.Length)>0?RecoveredBattlePassProgress.From(pass).DisplayLevel:0;
                bool full=unlocked>=(pass.level_tasks?.Length??0);
                rect.Find("CellMask").gameObject.SetActive(!full);
                rect.Find("Background/BG/exParent/ReveivedText").GetComponent<Text>().text="Reclamado "+received+"/"+bonus.Length;
                rect.Find("Background/VipPassBtn").gameObject.SetActive(!pass.has_purchased);
                var button=rect.Find("Background/BG/exParent/ReveiceBtn").GetComponent<Button>();
                bool showReceive=received<bonus.Length&&(available!=null||(pass.level_tasks?.Length??0)+received<unlocked);
                button.gameObject.SetActive(showReceive);
                button.interactable=claim!=null&&full&&pass.has_purchased&&available!=null&&!string.IsNullOrEmpty(available.tid)&&available.need_ad==0;
                button.onClick.RemoveAllListeners();
                var selected=available;
                button.onClick.AddListener(()=>{if(button.interactable)claim(selected,0,button);});
                parent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,count*height+rect.sizeDelta.y);
            }
            else parent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,count*height);
            // Resolve new content/viewport bounds before restoring the existing list's offset.
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            Canvas.ForceUpdateCanvases();
            parent.anchoredPosition=preserveScroll?position:Vector2.zero;
            if(scroll!=null)
            {
                scroll.StopMovement();
                if(!preserveScroll)scroll.verticalNormalizedPosition=1;
            }
            Canvas.ForceUpdateCanvases();
        }
        private static void BindClaim(Transform container,RecoveredTaskPeriodItem task,int type,Action<RecoveredTaskPeriodItem,int,Button> claim)
        {
            foreach(var button in container.GetComponentsInChildren<Button>(true))
            {
                button.onClick.RemoveAllListeners();
                button.interactable=claim!=null&&task!=null&&task.status==1&&task.need_ad==0&&!string.IsNullOrEmpty(task.tid)&&button.name=="ReceiveBtn";
                var target=button;
                button.onClick.AddListener(()=>{if(target.interactable)claim(task,type,target);});
            }
        }
        // DiamondImage.SetDiamond, native-expanded 0x2AF4E48 and serialized DiamondTextures order.
        internal static int DiamondIndex(int quantity)
        {
            if(quantity<=0)return 0;
            if(quantity<=100)return (quantity-1)/25;
            if(quantity<=150)return 4;
            if(quantity<=250)return 5;
            if(quantity<=1500)return 6;
            if(quantity<=3000)return 7;
            if(quantity<=7000)return 8;
            return quantity<=12000?9:10;
        }
        private static readonly long[] DiamondSprites={3205420759337366382,-1572751035783244487,-2146947574363465261,-7766419136579984734,-7869597558075028958,-88553308187825991,-326585753505158145,-8803654637615215888,-1020985930838149902,-6168142473530558835,-956927641279571672};
        private static void BindCosmetic(Transform container,RecoveredTaskPeriodItem task,RecoveredTaskReward reward)
        {
            string variant=null,title=null,path=null;SongImage image=null;
            switch(reward.type)
            {
                case 2: variant="Role";title=reward.role?.title;image=reward.role?.icon;path="RateBg/BFCover/BFIcon";break;
                case 3: variant="NameBox";title=reward.name_box?.box_title;image=reward.name_box?.fore_box;path="RateBg/Image";break;
                case 5: variant="Decor";title=reward.decor?.title;image=reward.decor?.icon;path="RateBg/DecorCover/DecorIcon";break;
            }
            if(variant==null||image==null){container.gameObject.SetActive(false);return;}
            container.gameObject.SetActive(true);
            var view=container.Find(variant);view.gameObject.SetActive(true);
            view.Find("RoleName").GetComponent<Text>().text=title??"";
            // Quality/upgrade dictionaries remain pending; never retain the prefab's example rarity.
            foreach(var name in new[]{"RarityIcon","UpgradeIcon","IconImage"})
                foreach(var node in view.GetComponentsInChildren<Transform>(true))if(node.name==name)node.gameObject.SetActive(false);
            view.Find("RateBg/Cover Override").gameObject.SetActive(false);
            view.Find("RateBg/RoleMask").gameObject.SetActive(task.status==2);
            view.Find("RateBg/LoadingIndicator").gameObject.SetActive(false);
            view.Find("LockIcon").gameObject.SetActive(task.status==0);
            view.Find("ReceiveBtn").gameObject.SetActive(task.status==1&&task.need_ad==0);
            view.Find("ADReceiveBtn").gameObject.SetActive(task.status==1&&task.need_ad!=0);
            var host=container.GetComponentInParent<RecoveredFrontend>();
            var cache=host.GetComponent<RecoveredCoverCache>()??host.gameObject.AddComponent<RecoveredCoverCache>();
            view.gameObject.AddComponent<RecoveredCoverBinding>().Bind(cache,image,view.Find(path).GetComponent<RawImage>(),null,true);
            foreach(var button in view.GetComponentsInChildren<Button>(true))button.interactable=false;
        }
        private static void BindContainer(RecoveredUiData data,Transform container,RecoveredTaskPeriodItem task)
        {
            foreach(Transform variant in container)variant.gameObject.SetActive(false);
            var rewards=task?.rewards;
            if(rewards==null||rewards.Length!=1||rewards[0]==null)
            {container.gameObject.SetActive(false);return;}
            if(rewards[0].type!=1)
            {
                BindCosmetic(container,task,rewards[0]);return;
            }
            container.gameObject.SetActive(true);
            var diamond=container.Find("Diamond");diamond.gameObject.SetActive(true);
            diamond.Find("Number/DiamondNumber").GetComponent<Text>().text="+"+rewards[0].quantity;
            diamond.Find("Bg/Cover").GetComponent<Image>().sprite=RecoveredUiBuilder.Asset<Sprite>(data,
                "CAB-d4ae4a224753834d14d1971b7cbfabf3:"+DiamondSprites[DiamondIndex(rewards[0].quantity)]);
            diamond.Find("Bg/Cover Override").gameObject.SetActive(false);
            diamond.Find("Bg/DiamondMask").gameObject.SetActive(task.status==2);
            diamond.Find("Bg/LoadingIndicator").gameObject.SetActive(false);
            diamond.Find("LockIcon").gameObject.SetActive(task.status==0);
            diamond.Find("ReceiveBtn").gameObject.SetActive(task.status==1&&task.need_ad==0);
            diamond.Find("ADReceiveBtn").gameObject.SetActive(task.status==1&&task.need_ad!=0);
            // Battle-pass claims require their own verified contract, not the check-in type.
            foreach(var button in diamond.GetComponentsInChildren<Button>(true))button.interactable=false;
        }
    }
}
