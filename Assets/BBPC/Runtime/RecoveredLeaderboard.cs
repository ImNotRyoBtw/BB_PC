using System;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    public sealed partial class RecoveredHomeDialogs
    {
        private RecoveredLeaderboardPayload leaderboardArea,leaderboardWorld,leaderboardChallenge;
        private bool leaderboardGlobal;

        // RankPresenter.GetRemainingTimeText 0x28D3DB4: script.json resolves
        // 0x4C5327C to TimeSpan.FromMilliseconds and 0x4C52D34 to get_Days.
        internal static string LeaderboardRemainingTime(long milliseconds)
        {
            if(milliseconds<=0)return RecoveredLocalization.Text("leaderboard_timepassed","");
            var time=TimeSpan.FromMilliseconds(milliseconds);
            int value;string unit;
            if(time.Days>=1){value=time.Days;unit="day";}
            else if(time.Hours>=1){value=time.Hours;unit="hour";}
            else {value=time.Minutes;unit="minute";}
            string key=unit+(value<2?"_singular":"_plural");
            string text=RecoveredLocalization.Text(key,"_$$_ "+unit+(value<2?"":"s"))
                .Replace("_$$_",value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return RecoveredLocalization.Text("ends_in","Ends in")+" <color=#FEC53D>"+text+"</color>";
        }

        internal void BindLeaderboardCountdown(long expiration,long? serverTimestamp)
        {
            RequirePath(root.transform,"SayImage/TitleText").GetComponent<Text>().text=RecoveredLocalization.Text("leaderboard","Leaderboard");
            var rest=RequirePath(root.transform,"SayImage/RestText").GetComponent<Text>();
            var authoredDay=RequirePath(root.transform,"SayImage/DayText").GetComponent<Text>();
            authoredDay.text="";
            authoredDay.gameObject.SetActive(false);
            rest.text=serverTimestamp.HasValue
                ?LeaderboardRemainingTime(expiration-serverTimestamp.Value):"";
        }

        internal bool BindLeaderboardUnlock(RecoveredLeaderboardPayload challenge)
        {
            leaderboardChallenge=challenge;
            var condition=challenge?.open_condition;
            // Verified packing helper preserves input int; CheckOpen compares status==1.
            if(condition!=null&&condition.status==1)
            {
                RequirePath(root.transform,"OpenByGame").gameObject.SetActive(false);
                RequirePath(root.transform,"ChallengeList").gameObject.SetActive(false);
                RequirePath(root.transform,"RegionList").gameObject.SetActive(false);
                var settlement=RequirePath(root.transform,"SettlementPanel");settlement.gameObject.SetActive(true);
                var own=RequirePath(settlement,"DownImage");own.gameObject.SetActive(challenge.me!=null);
                if(challenge.me!=null)
                {
                    var resource=Resources.Load<TextAsset>("RecoveredLeaderboardCells606/dialogs");
                    if(resource==null)throw new InvalidOperationException("Missing original leaderboard cells");
                    BindLeaderboardRow(RequirePath(own,"SelfCellView"),challenge.me,JsonUtility.FromJson<RecoveredUiData>(resource.text),true);
                }
                return true;
            }
            int need=condition?.need_round??3,current=condition?.current_round??0;
            bool rounds=current<need;
            bool score=!rounds&&(condition?.current_score??0)<(condition?.need_score??1);
            var gate=RequirePath(root.transform,"OpenByGame");
            gate.gameObject.SetActive(rounds||score);
            if(!rounds&&!score)return false;
            RequirePath(root.transform,"RegionList").gameObject.SetActive(false);
            RequirePath(root.transform,"ChallengeList").gameObject.SetActive(false);
            RequirePath(root.transform,"SettlementPanel").gameObject.SetActive(false);
            RequirePath(gate,"OpenRoundText").gameObject.SetActive(rounds);
            RequirePath(gate,"NumSlider").gameObject.SetActive(rounds);
            RequirePath(gate,"OpenScoreText").gameObject.SetActive(score);
            if(rounds)
            {
                int remaining=need-current;
                string key=remaining<2?"leaderboard_unlock_1":"leaderboard_unlock_more";
                string fallback=remaining<2?"Unlock after playing _$$_ song":"Unlock after playing _$$_ songs";
                RequirePath(gate,"OpenRoundText").GetComponent<Text>().text=RecoveredLocalization.Text(key,fallback).Replace("_$$_",remaining.ToString());
                var slider=RequirePath(gate,"NumSlider").GetComponent<Slider>();
                slider.minValue=0;slider.maxValue=1;slider.value=(float)current/need;
                RequirePath(gate,"NumSlider/NumText").GetComponent<Text>().text=current+"/"+need;
            }
            var play=RequirePath(gate,"GoToPlayButton").GetComponent<Button>();
            play.interactable=true;play.onClick.RemoveAllListeners();play.onClick.AddListener(()=>{Close();host.ShowCatalog();});
            return true;
        }
        internal void RenderLeaderboardChallenge()
        {
            if(leaderboardChallenge==null)return;
            BindLeaderboardCountdown(leaderboardChallenge.expiration,host.Services.CurrentServerTimestamp);
            if(BindLeaderboardUnlock(leaderboardChallenge))return;
            RequirePath(root.transform,"RegionList").gameObject.SetActive(false);
            RequirePath(root.transform,"SettlementPanel").gameObject.SetActive(false);
            RequirePath(root.transform,"ChallengeList").gameObject.SetActive(true);
            string region=leaderboardChallenge.region_name;
            if(string.IsNullOrEmpty(region))region=RecoveredLocalization.Text("global","Global");
            foreach(var path in new[]{"ChallengeList/ChallengeBar/RegionButton/Text","RegionList/RegionBar/RegionButton/Text"})
                RequirePath(root.transform,path).GetComponent<Text>().text=region;
            var resource=Resources.Load<TextAsset>("RecoveredLeaderboardCells606/dialogs");
            if(resource==null)throw new InvalidOperationException("Missing original leaderboard cells");
            var data=JsonUtility.FromJson<RecoveredUiData>(resource.text);
            var parent=(RectTransform)RequirePath(root.transform,"ChallengeList/CScrollView/Viewport/Content");
            ClearChildren(parent);
            var layout=parent.GetComponent<LayoutGroup>();if(layout!=null)layout.enabled=false;
            var fitter=parent.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=false;
            var sections=leaderboardChallenge.section??Array.Empty<RecoveredLeaderboardSection>();
            var items=leaderboardChallenge.items??Array.Empty<RecoveredLeaderboardItem>();
            // Native Refresh/GetCellView: boundary index is section.rank + section ordinal.
            float height=0;int itemIndex=0;
            for(int index=0;index<leaderboardChallenge.seats+sections.Length;index++)
            {
                RecoveredLeaderboardSection boundary=null;
                for(int n=0;n<sections.Length;n++)
                    if(sections[n]!=null&&sections[n].rank+n==index){boundary=sections[n];break;}
                var item=itemIndex<items.Length?items[itemIndex]:null;
                string name=boundary!=null?"BoundCellView":item!=null?"NormalCellView":"NobodyCellView";
                var scene=Array.Find(data.scenes,s=>s.name==name);
                if(scene==null)throw new InvalidOperationException("Missing original "+name);
                var tree=RecoveredUiBuilder.Build(data,scene,parent,host.PortraitCamera);
                RecoveredUiBuilder.UseAuthoredLayoutRoot(tree,name,parent);
                var row=(RectTransform)tree.Root.transform;
                row.localScale=Vector3.one;row.anchorMin=new Vector2(0,1);row.anchorMax=Vector2.one;row.pivot=new Vector2(.5f,1);
                row.sizeDelta=new Vector2(0,row.sizeDelta.y);
                row.anchoredPosition=new Vector2(0,-height);height+=boundary!=null?100:143;
                if(boundary!=null)
                {
                    RequirePath(row,"DiamText").GetComponent<Text>().text=boundary.diamond.ToString();
                    RequirePath(row,"DiamImage").GetComponent<Image>().sprite=RecoveredRewardSprites.Diamond(boundary.diamond);
                }
                else
                {
                    if(item!=null)BindLeaderboardRow(row,item,data,false,item.rank>0&&item.rank==leaderboardChallenge.me?.rank);
                    else
                    {
                        int rank=itemIndex+1;
                        var trophy=RequirePath(row,"OrderImage").GetComponent<Image>();
                        trophy.gameObject.SetActive(rank<=3);
                        if(rank<=3)trophy.sprite=RecoveredUiBuilder.Asset<Sprite>(data,Array.Find(data.assets,a=>a.name=="jb"+rank).id);
                        var order=RequirePath(row,"OrderText").GetComponent<Text>();order.gameObject.SetActive(rank>3);order.text=rank.ToString();
                    }
                    itemIndex++;
                }
            }
            parent.anchorMin=new Vector2(0,1);parent.anchorMax=Vector2.one;parent.pivot=new Vector2(.5f,1);
            parent.sizeDelta=new Vector2(0,height);parent.anchoredPosition=Vector2.zero;
            var scroller=RequirePath(root.transform,"ChallengeList/CScrollView").GetComponent<ScrollRect>();
            ConfigureLeaderboardScroll(scroller);
            scroller.StopMovement();
            int ownRank=leaderboardChallenge.me?.rank??0;
            scroller.verticalNormalizedPosition=ownRank<=5?1:leaderboardChallenge.seats-ownRank<6?0:1-(float)(ownRank-1)/(leaderboardChallenge.seats+1);
            Canvas.ForceUpdateCanvases();
        }
        internal bool BindLeaderboardPrize(RecoveredLeaderboardPrize prize)
        {
            if(prize==null||prize.rank<=0||prize.rewards==null||prize.rewards.Length==0)return false;
            var reward=prize.rewards[0];
            // Native Refresh only presents diamond rewards; other presenter behavior is pending.
            if(reward==null||reward.type!=1)return false;
            CloseClaimReward();
            var resource=Resources.Load<TextAsset>("RecoveredLeaderboardDialogs606/dialogs");
            if(resource==null)throw new InvalidOperationException("Missing original leaderboard prize dialog");
            var data=JsonUtility.FromJson<RecoveredUiData>(resource.text);
            var scene=Array.Find(data.scenes,s=>s.name=="ChallengeRewardDialog");
            if(scene==null)throw new InvalidOperationException("Missing original ChallengeRewardDialog");
            var tree=RecoveredUiBuilder.Build(data,scene,host.transform,host.PortraitCamera);
            RecoveredUiBuilder.UseAuthoredLayoutRoot(tree,scene.name,host.transform);
            rewardRoot=tree.Root;rewardRoot.transform.localScale=Vector3.one;
            var canvas=rewardRoot.GetComponentInChildren<Canvas>(true);
            if(canvas==null)throw new InvalidOperationException("Missing original prize canvas");
            canvas.transform.localScale=Vector3.one;canvas.overrideSorting=true;canvas.sortingOrder=960;
            var background=RequirePath(rewardRoot.transform,"Canvas/Background");
            RequirePath(background,"SubTitleText/RankText").GetComponent<Text>().text=RecoveredLocalization.Text("rank_number","No._$$_").Replace("_$$_",prize.rank.ToString());
            RequirePath(background,"DiaImage/RewardText").GetComponent<Text>().text="X"+reward.quantity;
            RequirePath(background,"DiaImage").GetComponent<Image>().sprite=RecoveredRewardSprites.Diamond(reward.quantity);
            // Competition writes remain unauthorized. Never turn a local display into a successful claim.
            var receive=RequirePath(background,"DoneButton").GetComponent<Button>();
            receive.interactable=false;receive.onClick.RemoveAllListeners();
            rewardRoot.SetActive(true);Canvas.ForceUpdateCanvases();
            return true;
        }
        private GameObject leaderboardRulesRoot;

        private void CloseLeaderboardRules()
        {
            if(leaderboardRulesRoot==null)return;
            leaderboardRulesRoot.SetActive(false);
            if(Application.isPlaying)Destroy(leaderboardRulesRoot);else DestroyImmediate(leaderboardRulesRoot);
            leaderboardRulesRoot=null;
        }

        private void ShowLeaderboardRules()
        {
            CloseLeaderboardRules();
            var resource=Resources.Load<TextAsset>("RecoveredLeaderboardDialogs606/dialogs");
            if(resource==null)throw new InvalidOperationException("Missing original leaderboard rules dialog");
            var data=JsonUtility.FromJson<RecoveredUiData>(resource.text);
            var scene=Array.Find(data.scenes,s=>s.name=="LeaderRulesDialog");
            if(scene==null)throw new InvalidOperationException("Missing original LeaderRulesDialog");
            var tree=RecoveredUiBuilder.Build(data,scene,host.transform,host.PortraitCamera);
            RecoveredUiBuilder.UseAuthoredLayoutRoot(tree,scene.name,host.transform);
            leaderboardRulesRoot=tree.Root;leaderboardRulesRoot.transform.localScale=Vector3.one;
            var canvas=leaderboardRulesRoot.GetComponentInChildren<Canvas>(true);
            if(canvas==null){CloseLeaderboardRules();throw new InvalidOperationException("Missing original rules canvas");}
            canvas.transform.localScale=Vector3.one;
            canvas.overrideSorting=true;canvas.sortingOrder=960;
            var close=RequirePath(leaderboardRulesRoot.transform,"Canvas/Background/Close").GetComponent<Button>();
            close.gameObject.SetActive(true);close.interactable=true;close.onClick.AddListener(CloseLeaderboardRules);
            RequirePath(leaderboardRulesRoot.transform,"Canvas/Background/ExplainText").GetComponent<Text>().text="";
            leaderboardRulesRoot.SetActive(true);
            var board=RequirePath(root.transform,"ChallengeList").gameObject.activeSelf?leaderboardChallenge:leaderboardGlobal?leaderboardWorld:leaderboardArea;
            StartCoroutine(LoadLeaderboardRules(generation,leaderboardRulesRoot,board?.id));
        }

        private System.Collections.IEnumerator LoadLeaderboardRules(int request,GameObject dialog,string id)
        {
            string text=null;
            yield return host.Services.FetchLeaderboardRules(id,value=>text=value);
            if(!Current(request)||dialog==null||dialog!=leaderboardRulesRoot)yield break;
            RequirePath(dialog.transform,"Canvas/Background/LoadingIndicator").gameObject.SetActive(false);
            if(text!=null)RequirePath(dialog.transform,"Canvas/Background/ExplainText").GetComponent<Text>().text=text;
        }

        private void BindLeaderboardControls()
        {
            leaderboardArea=null;leaderboardWorld=null;leaderboardChallenge=null;leaderboardGlobal=false;
            RequirePath(root.transform,"BgPanel").gameObject.SetActive(true);
            RequirePath(root.transform,"ChallengeList").gameObject.SetActive(false);
            RequirePath(root.transform,"SettlementPanel").gameObject.SetActive(false);
            RequirePath(root.transform,"OpenByGame").gameObject.SetActive(false);
            RequirePath(root.transform,"RegionList").gameObject.SetActive(true);
            foreach(var bar in new[]{"AreaChildBar","GlobalChildBar"})
            {
                var parent=RequirePath(root.transform,"RegionList/"+bar);
                var area=RequirePath(parent,"AreaButton").GetComponent<Button>();
                var global=RequirePath(parent,"GlobalButton").GetComponent<Button>();
                area.interactable=true;global.interactable=true;
                area.onClick.AddListener(()=>RenderLeaderboard(false));
                global.onClick.AddListener(()=>RenderLeaderboard(true));
            }
            foreach(var path in new[]{"RegionList/RegionBar/ChallengeButton","ChallengeList/ChallengeBar/ChallengeButton"})
            {
                var challenge=RequirePath(root.transform,path).GetComponent<Button>();
                challenge.interactable=true;challenge.onClick.AddListener(RenderLeaderboardChallenge);
            }
            var region=RequirePath(root.transform,"ChallengeList/ChallengeBar/RegionButton").GetComponent<Button>();
            region.interactable=true;region.onClick.AddListener(()=>RenderLeaderboard(false));
            var rules=RequirePath(root.transform,"IntroduceButton").GetComponent<Button>();
            rules.interactable=true;rules.onClick.AddListener(ShowLeaderboardRules);
        }

        internal void BindLeaderboardData(RecoveredLeaderboardPayload area,RecoveredLeaderboardPayload world)
        {
            leaderboardArea=area;leaderboardWorld=world;RenderLeaderboard(false);
        }

        internal void RenderLeaderboard(bool global)
        {
            if(!IsOpen||root.name!="RankPanel")return;
            leaderboardGlobal=global;
            RequirePath(root.transform,"RegionList").gameObject.SetActive(true);
            RequirePath(root.transform,"ChallengeList").gameObject.SetActive(false);
            RequirePath(root.transform,"SettlementPanel").gameObject.SetActive(false);
            RequirePath(root.transform,"OpenByGame").gameObject.SetActive(false);
            var board=global?leaderboardWorld:leaderboardArea;
            RequirePath(root.transform,"RegionList/AreaChildBar").gameObject.SetActive(!global);
            RequirePath(root.transform,"RegionList/GlobalChildBar").gameObject.SetActive(global);
            BindLeaderboardCountdown(board?.expiration??0,board!=null?host.Services.CurrentServerTimestamp:null);
            foreach(var bar in new[]{"AreaChildBar","GlobalChildBar"})
                RequirePath(root.transform,"RegionList/"+bar+"/AreaButton/Text").GetComponent<Text>().text=leaderboardArea?.title??"Area";
            // Windows layout adaptation: keep the recovered list below the selected child bar.
            var list=(RectTransform)RequirePath(root.transform,"RegionList/RScrollView");
            var selectedBar=(RectTransform)RequirePath(root.transform,"RegionList/"+(global?"GlobalChildBar":"AreaChildBar"));
            list.offsetMax=new Vector2(list.offsetMax.x,selectedBar.offsetMin.y);
            var parent=(RectTransform)RequirePath(root.transform,"RegionList/RScrollView/Viewport/Content");
            ClearChildren(parent);
            var layout=parent.GetComponent<LayoutGroup>();if(layout!=null)layout.enabled=false;
            var fitter=parent.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=false;
            var resource=Resources.Load<TextAsset>("RecoveredLeaderboardCells606/dialogs");
            if(resource==null)throw new InvalidOperationException("Missing original leaderboard cells");
            var data=JsonUtility.FromJson<RecoveredUiData>(resource.text);
            var scene=Array.Find(data.scenes,s=>s.name=="NormalCellView");
            if(scene==null)throw new InvalidOperationException("Missing original NormalCellView");
            int index=0;
            foreach(var item in board?.items??Array.Empty<RecoveredLeaderboardItem>())
            {
                if(item==null)continue;
                var tree=RecoveredUiBuilder.Build(data,scene,parent,host.PortraitCamera);
                RecoveredUiBuilder.UseAuthoredLayoutRoot(tree,scene.name,parent);
                var row=(RectTransform)tree.Root.transform;
                row.localScale=Vector3.one;row.anchorMin=new Vector2(0,1);row.anchorMax=Vector2.one;row.pivot=new Vector2(.5f,1);
                row.sizeDelta=new Vector2(0,row.sizeDelta.y);
                row.anchoredPosition=new Vector2(0,-index*143);index++;
                BindLeaderboardRow(row,item,data,false,item.rank>0&&item.rank==board.me?.rank);
            }
            parent.anchorMin=new Vector2(0,1);parent.anchorMax=Vector2.one;parent.pivot=new Vector2(.5f,1);
            parent.sizeDelta=new Vector2(0,index*143);parent.anchoredPosition=Vector2.zero;
            var own=RequirePath(root.transform,"RegionList/DownImage");own.gameObject.SetActive(board?.me!=null);
            if(board?.me!=null)BindLeaderboardRow(RequirePath(own,"SelfCellView"),board.me,data,true);
            var scroller=RequirePath(root.transform,"RegionList/RScrollView").GetComponent<ScrollRect>();
            ConfigureLeaderboardScroll(scroller);
            scroller.StopMovement();scroller.verticalNormalizedPosition=1;
            Canvas.ForceUpdateCanvases();
        }

        private static void ConfigureLeaderboardScroll(ScrollRect scroller)
        {
            // Export keeps ScrollRect defaults (both axes/elastic); EnhancedScroller's
            // vertical controller is absent in Windows. Constrain this replacement list.
            scroller.horizontal=false;scroller.vertical=true;scroller.movementType=ScrollRect.MovementType.Clamped;
            var viewport=scroller.viewport;
            if(viewport.GetComponent<RectMask2D>()==null)viewport.gameObject.AddComponent<RectMask2D>();
            scroller.content.anchorMin=new Vector2(0,1);scroller.content.anchorMax=Vector2.one;
            scroller.content.sizeDelta=new Vector2(0,scroller.content.sizeDelta.y);
            scroller.content.anchoredPosition=new Vector2(0,scroller.content.anchoredPosition.y);
        }
        private void BindLeaderboardRow(Transform row,RecoveredLeaderboardItem item,RecoveredUiData data,bool self=false,bool highlight=false)
        {
            var trophy=RequirePath(row,"OrderImage").GetComponent<Image>();
            trophy.gameObject.SetActive(item.rank>=1&&item.rank<=3);
            if(trophy.gameObject.activeSelf)
            {
                var asset=Array.Find(data.assets,a=>a.name=="jb"+item.rank);
                trophy.sprite=RecoveredUiBuilder.Asset<Sprite>(data,asset.id);
            }
            bool percent=self&&item.rank==0;
            var order=RequirePath(row,"OrderText").GetComponent<Text>();order.gameObject.SetActive(percent||item.rank>3||item.rank<=0);
            order.text=percent?RecoveredLocalization.Text("top_display","Top _$$_ %").Replace("_$$_",Math.Min((int)(item.percent+1),99).ToString()):item.rank.ToString();
            RequirePath(row,"NameBoxComponent/NameParent/NameText").GetComponent<Text>().text=item.screen_name??"";
            RequirePath(row,"ScoreText").GetComponent<Text>().text=item.score.ToString(System.Globalization.CultureInfo.InvariantCulture);
            // Observed RefreshListUI 0x2859590 selects bgSprites[~isSelf & 1].
            // Serialized NormalCellView 8248234444858691274 in leaderboard-cells-0000d312...:
            // [64878057799197323, -2016879117657221535]. Windows uses the same pair for the pinned own row.
            // Adapter: a positive rank matching this board's me identifies the displayed own seat;
            // names and scores are never used as identity, and no other player's auid is retained.
            row.GetComponent<Image>().sprite=RecoveredUiBuilder.Asset<Sprite>(data,
                "CAB-cd0c7d14ff2899f26393089537f88d0b:"+(self||highlight?"64878057799197323":"-2016879117657221535"));
            var cache=host.GetComponent<RecoveredCoverCache>()??host.gameObject.AddComponent<RecoveredCoverCache>();
            var photo=RequirePath(row,"PhotoBgImage/PhotoRawImage").GetComponent<RawImage>();
            var avatarBinding=photo.GetComponent<RecoveredCoverBinding>()??photo.gameObject.AddComponent<RecoveredCoverBinding>();
            avatarBinding.BindLocalTexture(cache,item.avatar,photo,new Vector2(119,114),
                RecoveredUiBuilder.Asset<Texture2D>(data,"CAB-cd0c7d14ff2899f26393089537f88d0b:-9116772427873344128"),new Vector2(38,60));
            var box=RequirePath(row,"NameBoxComponent");
            var borderBinding=box.GetComponent<RecoveredCoverBinding>()??box.gameObject.AddComponent<RecoveredCoverBinding>();
            borderBinding.BindLocalSprite(cache,item.name_box?.fore_box,box.GetComponent<Image>());
            // Serialized NameBox -5203467528445161738: guildDisplayType=Inside (1).
            var inside=RequirePath(box,"NameParent/GuildParent");inside.gameObject.SetActive(true);
            var horizontal=RequirePath(inside,"Horizontal");
            horizontal.gameObject.SetActive(!string.IsNullOrEmpty(item.guild?.guild_name));
            RequirePath(horizontal,"GuildTextInside").GetComponent<Text>().text=item.guild?.guild_name??"";
            // 2026 reference: rival photo frames are hidden; the pinned own row retains one.
            RequirePath(row,"PhotoBgImage").gameObject.SetActive(self);
            if(!self)
            {
                var nameRect=(RectTransform)box;
                nameRect.anchoredPosition=new Vector2(155,nameRect.anchoredPosition.y);
                nameRect.sizeDelta=new Vector2(-394,nameRect.sizeDelta.y);
            }
            foreach(var path in new[]{"NameBoxComponent/FriendOrEmoji","NameBoxComponent/GuildParent","NameBoxComponent/rankingText"})
                RequirePath(row,path).gameObject.SetActive(false);
            // TODO: recover NameBox RefreshNameText color/draw_info conversion before applying it.
        }
    }
}
