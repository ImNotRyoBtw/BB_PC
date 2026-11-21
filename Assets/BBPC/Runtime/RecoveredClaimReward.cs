using System;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    public sealed partial class RecoveredHomeDialogs
    {
        private GameObject rewardRoot;
        private System.Collections.IEnumerator AnimateClaimLighting(GameObject popup,Transform lighting)
        {
            // 6.0.6 ResultLighting clip -1410213415492431638, controller 1887317873089241960.
            // Original root Euler Z keys: (0,0), (1.25,-180), (2.5,-360), linear, looping.
            float elapsed=0;
            while(popup!=null&&popup.activeSelf)
            {
                lighting.localRotation=Quaternion.Euler(0,0,-144f*(elapsed%2.5f));
                yield return null;
                elapsed+=Time.deltaTime;
            }
        }
        private void CloseClaimReward()
        {
            if(rewardRoot==null)return;
            rewardRoot.SetActive(false);
            if(Application.isPlaying)Destroy(rewardRoot);else DestroyImmediate(rewardRoot);
            rewardRoot=null;
        }
        private void ShowClaimRewards(RecoveredTaskReward[] rewards,int request,int index=0,bool periodPoints=false)
        {
            if(!Current(request))return;
            CloseClaimReward();
            while(index<(rewards?.Length??0)&&(rewards[index]==null||rewards[index].type==6||rewards[index].type==9||(periodPoints&&(rewards[index].type==13||rewards[index].type==14))))index++;
            if(index>=(rewards?.Length??0))return;
            var reward=rewards[index];
            var resource=Resources.Load<TextAsset>("RecoveredClaimReward606/dialogs");
            if(resource==null)throw new InvalidOperationException("Missing original claim reward dialog");
            var data=JsonUtility.FromJson<RecoveredUiData>(resource.text);
            var scene=Array.Find(data.scenes,s=>s.name=="GetRewardDialog");
            var tree=RecoveredUiBuilder.Build(data,scene,host.transform,host.PortraitCamera);
            RecoveredUiBuilder.UseAuthoredLayoutRoot(tree,scene.name,host.transform);
            rewardRoot=tree.Root;rewardRoot.transform.localScale=Vector3.one;
            var canvas=rewardRoot.GetComponentInChildren<Canvas>(true);
            canvas.overrideSorting=true;canvas.sortingOrder=960;
            var mask=RequirePath(rewardRoot.transform,"MaskImage");
            foreach(var name in new[]{"AwardDisplayCell1","AwardDisplayCell2","AwardDisplayCell3"})RequirePath(rewardRoot.transform,name).gameObject.SetActive(false);
            foreach(var name in new[]{"BFCover","DecorCover","NameBoxComponent","KeepStar","StarBooster","BFTicket"})RequirePath(mask,name).gameObject.SetActive(false);
            var label=RequirePath(mask,"NameText").GetComponent<Text>();
            label.text=reward.quantity.ToString();
            if(reward.type==1)
            {
                var cover=RequirePath(mask,"BFCover");cover.gameObject.SetActive(true);
                cover.GetComponent<Image>().enabled=false;
                foreach(var name in new[]{"RarityIcon","UpgradeIcon"})RequirePath(cover,name).gameObject.SetActive(false);
                var icon=RequirePath(cover,"BFIcon");icon.gameObject.SetActive(true);icon.GetComponent<RawImage>().enabled=false;
                var graphic=new GameObject("Diamond",typeof(RectTransform),typeof(Image));
                var rect=(RectTransform)graphic.transform;rect.SetParent(icon,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.sizeDelta=Vector2.zero;
                var image=graphic.GetComponent<Image>();image.sprite=RecoveredRewardSprites.Diamond(reward.quantity);image.preserveAspect=true;
            }
            else if(reward.type==12)
            {
                var ticket=RequirePath(mask,"BFTicket");ticket.gameObject.SetActive(true);
                RequirePath(ticket,"CardRawImage").GetComponent<Image>().sprite=RecoveredRewardSprites.Ticket(reward.quantity,true);
                RequirePath(ticket,"TitleText").GetComponent<Text>().text="BF TICKET";
                RequirePath(ticket,"ShowText").GetComponent<Text>().text="";
                RequirePath(ticket,"CountText").GetComponent<Text>().text="x"+reward.quantity;
                label.text="";
            }
            else if(reward.type==3)
            {
                label.text=reward.name_box?.box_title??"";
                var namebox=RequirePath(mask,"NameBoxComponent");
                namebox.gameObject.SetActive(reward.name_box!=null);
                RequirePath(namebox,"NameParent/NameText").gameObject.SetActive(false);
                foreach(var path in new[]{"rankingText","FriendOrEmoji","GuildParent","NameParent/GuildParent"})RequirePath(namebox,path).gameObject.SetActive(false);
                var cache=host.GetComponent<RecoveredCoverCache>()??host.gameObject.AddComponent<RecoveredCoverCache>();
                namebox.gameObject.AddComponent<RecoveredCoverBinding>().BindLocalSprite(cache,reward.name_box?.fore_box,namebox.GetComponent<Image>());
            }
            else if(reward.type==5)
            {
                label.text=reward.decor?.title??"";
                var decor=RequirePath(mask,"DecorCover");decor.gameObject.SetActive(true);
                decor.GetComponent<Image>().enabled=false;
                var location=RequirePath(decor,"IconImage").GetComponent<Image>();
                location.sprite=RecoveredRewardSprites.DecorLocation(reward.decor);
                location.gameObject.SetActive(true);location.enabled=location.sprite!=null;
                var cache=host.GetComponent<RecoveredCoverCache>()??host.gameObject.AddComponent<RecoveredCoverCache>();
                decor.gameObject.AddComponent<RecoveredCoverBinding>().BindLocalTexture(cache,reward.decor?.icon,RequirePath(decor,"DecorIcon").GetComponent<RawImage>(),new Vector2(400,520));
            }
            else
            {
                // TODO: bind remaining non-currency presenters using their native presenter methods.
                label.text="Recompensa "+reward.type+" x "+reward.quantity;
            }
            if(reward.cover!=null)
            {
                var cover=RequirePath(mask,"BFCover");cover.gameObject.SetActive(true);
                ((RectTransform)cover).sizeDelta=new Vector2(400,520);
                cover.GetComponent<Image>().enabled=false;
                foreach(var name in new[]{"RarityIcon","UpgradeIcon"})RequirePath(cover,name).gameObject.SetActive(false);
                var icon=RequirePath(cover,"BFIcon").GetComponent<RawImage>();
                var fallback=icon.transform.Find("Diamond");
                var cache=host.GetComponent<RecoveredCoverCache>()??host.gameObject.AddComponent<RecoveredCoverCache>();
                cover.gameObject.AddComponent<RecoveredCoverBinding>().Bind(cache,reward.cover,icon,fallback==null?null:fallback.gameObject,true,true);
            }
            var button=RequirePath(mask,"GetButton").GetComponent<Button>();
            RequirePath(button.transform,"Text").GetComponent<Text>().text="RECLAMAR";
            button.interactable=true;button.onClick.RemoveAllListeners();
            int next=index+1;
            button.onClick.AddListener(()=>ShowClaimRewards(rewards,request,next,periodPoints));
            rewardRoot.SetActive(true);Canvas.ForceUpdateCanvases();
            StartCoroutine(AnimateClaimLighting(rewardRoot,RequirePath(mask,"Lighting")));
        }
    }
}
