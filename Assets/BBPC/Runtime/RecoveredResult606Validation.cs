using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace BBPC.UnityRuntime
{
    public static partial class RecoveredResult606Validation
    {
        [Serializable] public sealed class Receipt { public bool success; public string error; public int checks,captures; }
        private static bool Check(Receipt r,Action action)
        {try{action();r.checks++;return true;}catch(Exception e){r.error=e.ToString();return false;}}
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        internal static void Click(RecoveredFrontend f,Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect=button.transform as RectTransform;
            var center=rect!=null?rect.TransformPoint(rect.rect.center):button.transform.position;
            var screen=RectTransformUtility.WorldToScreenPoint(f.PortraitCamera,center);
            if(button.targetGraphic!=null)
            {
                var visibleCorners=new Vector3[4];button.targetGraphic.rectTransform.GetWorldCorners(visibleCorners);
                var low=RectTransformUtility.WorldToScreenPoint(f.PortraitCamera,visibleCorners[0]);
                var high=RectTransformUtility.WorldToScreenPoint(f.PortraitCamera,visibleCorners[2]);
                float left=Mathf.Max(0,Mathf.Min(low.x,high.x)),right=Mathf.Min(Screen.width,Mathf.Max(low.x,high.x));
                float bottom=Mathf.Max(0,Mathf.Min(low.y,high.y)),top=Mathf.Min(Screen.height,Mathf.Max(low.y,high.y));
                if(right>left&&top>bottom)screen=new Vector2((left+right)*.5f,(bottom+top)*.5f);
            }
            var pointer=new PointerEventData(EventSystem.current){position=screen,button=PointerEventData.InputButton.Left};
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            string canvasInfo="none";var canvas=button.GetComponentInParent<Canvas>();if(canvas!=null)canvasInfo=canvas.name+" active="+canvas.gameObject.activeInHierarchy+" enabled="+canvas.enabled+" camera="+(canvas.worldCamera!=null?canvas.worldCamera.name:"none");
            string groups="";foreach(var group in button.GetComponentsInParent<CanvasGroup>(true))groups+=group.name+" active="+group.gameObject.activeInHierarchy+" alpha="+group.alpha+" blocks="+group.blocksRaycasts+";";
            string corners="none";if(rect!=null){var c=new Vector3[4];rect.GetWorldCorners(c);corners=string.Join("|",Array.ConvertAll(c,x=>x.ToString()));}
            string hitNames=string.Join(",",hits.ConvertAll(x=>x.gameObject.name+"/"+x.module));
            Require(button.interactable&&button.gameObject.activeInHierarchy,"Inactive control: "+button.name+" enabled="+button.enabled+" rect="+corners+" screen="+screen+" canvas="+canvasInfo+" groups="+groups);
            var handler=hits.Count>0?ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject):null;
            Require(hits.Count>0&&handler==button.gameObject,"Obstructed control: "+button.name+" hit="+(hits.Count>0?hits[0].gameObject.name:"none")+" hits="+hitNames+" rect="+corners+" screen="+screen+" canvas="+canvasInfo+" groups="+groups);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        }
        private static void Capture(RecoveredFrontend f,string output,string name,Receipt r)
        {f.CaptureCurrentView(Path.Combine(output,name+".png"),450,800);r.captures++;}
        public static IEnumerator Run(RecoveredFrontend f,string output,Receipt r)
        {
            Directory.CreateDirectory(output);
            yield return new WaitForEndOfFrame();
            if(!Check(r,()=>
            {
                f.ShowMainRoom();
                var navigation=(RectTransform)f.MainRoom.Find("NavigationBar").transform;
                Require(navigation.anchoredPosition.y==-200&&navigation.rect.height==324,"Navigation visible band differs from authored placement");
                Require(!f.ServerReportsRequested&&f.LocalReportsEnabled,"Diagnostic reports must default offline/local");
                Click(f,f.Navigation.ButtonFor(MainPage.Shop));
                Require(f.Navigation.Current==MainPage.Shop&&f.Navigation.Pages[MainPage.Shop].Root.activeInHierarchy,"Shop navigation did not open recovered original page");
                Require(!Array.Exists(f.MainRoom.Root.GetComponentsInChildren<Canvas>(),c=>c.name=="Windows store panel"||c.name=="Windows skins panel"),"Procedural shop overlay covers recovered page");
                Capture(f,output,"shop-original-route",r);
                Click(f,f.Navigation.ButtonFor(MainPage.Home));
                Click(f,f.Navigation.ButtonFor(MainPage.Character));
                Require(f.Navigation.Current==MainPage.Character&&f.Navigation.Pages[MainPage.Character].Root.activeInHierarchy,"Character navigation did not open recovered original page");
                Require(!Array.Exists(f.MainRoom.Root.GetComponentsInChildren<Canvas>(),c=>c.name=="Windows skins panel"),"Procedural skins overlay covers recovered page");
                Capture(f,output,"character-original-route",r);
                Click(f,f.Navigation.ButtonFor(MainPage.Home));
            }))yield break;
            if(!Check(r,()=>{
                bool rejected=false;
                try{RecoveredReadContracts.ReadReportResult("{\"status\":1,\"data\":{}}");}catch(FormatException){rejected=true;}
                Require(rejected,"Empty report falsely accepted as reward");
                rejected=false;
                try{RecoveredReadContracts.ReadRewardResult("{\"status\":1,\"data\":{}}");}catch(FormatException){rejected=true;}
                Require(rejected,"Empty claim falsely accepted");
                Require(RecoveredReadContracts.ReadReportResult("{\"status\":1,\"data\":{\"gain_diamond\":0}}").gain_diamond==0,"Explicit zero reward rejected");
            }))yield break;
            if(!Check(r,()=>{Require(RecoveredStorage.Isolated,"Isolation required");f.PlayRoom.Audio.PauseOnFocusLoss=false;f.PlayRoom.InputSource.ReadHardware=false;f.PrepareLocalTutorial("normal");}))yield break;
            float deadline=Time.realtimeSinceStartup+20;
            while(f.Phase==FrontendPhase.SongPreparation&&Time.realtimeSinceStartup<deadline)yield return null;
            var room=f.PlayRoom;string round=null;
            if(!Check(r,()=>{Require(room.State==RecoveredPlayState.Prepared,"Preparation failed");round=room.RoundId;Click(f,room.Overlays.StartButton);}))yield break;
            deadline=Time.realtimeSinceStartup+70;
            while(room.State!=RecoveredPlayState.Dead&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!Check(r,()=>{Require(room.State==RecoveredPlayState.Dead,"Death not reached");Require(!room.Audio.Playing,"Death audio still playing");Capture(f,output,"death-initial",r);}))yield break;
            deadline=Time.realtimeSinceStartup+4;
            while(room.Overlays.DeathRemaining>3&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!Check(r,()=>{var image=room.Overlays.DeathView.Objects["level2:78"].GetComponent<Image>();Require(image.gameObject.activeInHierarchy&&image.sprite==Resources.Load<Sprite>("Result606/Count3"),"Wrong death number");Capture(f,output,"death-three",r);Click(f,room.Overlays.DeathView.Find("CountinuePlayButton").GetComponent<Button>());Require(room.UsedLocalRevive&&!room.Session.Dead&&room.RoundId==round,"Revive replaced session identity or left dead");}))yield break;
            deadline=Time.realtimeSinceStartup+12;
            while(room.State==RecoveredPlayState.Countdown&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!Check(r,()=>{Require(room.State==RecoveredPlayState.Playing&&room.Audio.Playing,"Revive did not resume audio");Capture(f,output,"revived",r);}))yield break;
            deadline=Time.realtimeSinceStartup+70;
            while(room.State!=RecoveredPlayState.Dead&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!Check(r,()=>Require(room.State==RecoveredPlayState.Dead,"Second death not reached")))yield break;
            deadline=Time.realtimeSinceStartup+7;
            while(room.Overlays.DeathRemaining>0&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!Check(r,()=>
            {
                Capture(f,output,"death-close",r);
                Click(f,room.Overlays.DeathView.Find("Close").GetComponent<Button>());
                Require(f.Phase==FrontendPhase.ResultRoom&&room.Result.roundId==round,"Abort did not preserve RoundID into ResultRoom");
                Capture(f,output,"result",r);
                int lit=(int)((float)room.Result.progress/100f*3f);
                var ids=new[]{"sharedassets4.assets:139","sharedassets4.assets:128","sharedassets4.assets:125"};
                for(int i=0;i<ids.Length;i++)
                {
                    var raw=room.ResultHeader.Objects[ids[i]].GetComponent<RawImage>();
                    var expected=RecoveredUiBuilder.Asset<Texture2D>(room.GameplayData,i<lit?"sharedassets2.assets:63":"resources.assets:248");
                    Require(raw.gameObject.activeInHierarchy&&raw.texture==expected,"Original result progress indicator mismatch: "+ids[i]);
                }
                Require(room.ResultHeader.Find("ComboText").GetComponent<Text>().text==room.Result.maxCombo.ToString(),"Original combo field was replaced");
            }))yield break;
            if(!Check(r,()=>{var a=f.Services.BuildOwnReportBody(room.Result);var b=f.Services.BuildOwnReportBody(room.Result);Require(a==b&&f.Services.LastReportClientId==round,"Report serialization changed RoundID");Require(f.Services.ReadAttempts.Count==0,"Unexpected remote service attempt");}))yield break;
            int expectedIndex=-1,expectedGain=0;
            f.Services.OfflinePost=(route,body)=>{
                if(route==RecoveredReadContracts.ResultReport)
                    return "{\"status\":1,\"data\":{\"new_record\":false,\"gain_diamond\":12,\"point\":2,\"rewards\":[],\"multiple_factors\":[2,3,4,5,4,3,2]}}";
                if(route==RecoveredReadContracts.ResultReward){Require(body==RecoveredReadContracts.RewardBody(round,room.Result.sid,room.Result.aid,expectedIndex),"Claim changed RoundID or visible factor");return "{\"status\":1,\"data\":{\"gain_diamond\":"+expectedGain+"}}";}
                throw new InvalidOperationException("Unexpected fake route");
            };
            RecoveredReportResultData report=null;
            yield return f.Services.ReportOwnResult(room.Result,value=>report=value);
            if(!Check(r,()=>{Require(report!=null&&report.gain_diamond==12,"Synthetic report parse failed");f.Results.SetReportStatus(room.Result,"Offline fixture",report);}))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var button=room.ResultHeader.Find("GetMoreButton").transform;
                var background=button.Find("ADImage").GetComponent<Image>();
                Require(background.gameObject.activeInHierarchy&&background.enabled&&background.sprite!=null&&background.color.a>0,"Reward button background invisible");
                var label=button.Find("DiaImage/DiaText").GetComponent<Text>();
                Require(label.text=="+ "+(12*report.multiple_factors[f.Results.SelectedFactorIndex]).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture),"Reward amount differs from visible factor");
                Capture(f,output,"reward-moving",r);
            }))yield break;
            int previousFactor=f.Results.SelectedFactorIndex;
            deadline=Time.realtimeSinceStartup+3;
            while(f.Results.SelectedFactorIndex==previousFactor&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!Check(r,()=>
            {
                Require(f.Results.SelectedFactorIndex!=previousFactor,"Reward cursor did not change zone");
                var label=room.ResultHeader.Find("GetMoreButton").transform.Find("DiaImage/DiaText").GetComponent<Text>();
                Require(label.text=="+ "+(12*report.multiple_factors[f.Results.SelectedFactorIndex]).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture),"Reward label did not follow changed zone");
            }))yield break;
            if(!Check(r,()=>{expectedIndex=f.Results.SelectedFactorIndex;var cursor=room.ResultHeader.Find("GetDiamonds").transform.Find("SlidingRewardBar/Cursor");Require(RecoveredResult606.FactorIndex(cursor.localPosition.x)==expectedIndex,"Visible cursor differs from selected factor");expectedGain=(int)(12*report.multiple_factors[expectedIndex]);Click(f,room.ResultHeader.Find("GetMoreButton").GetComponent<Button>());}))yield break;
            deadline=Time.realtimeSinceStartup+5;
            while(!f.Results.ConfirmedRewardGain.HasValue&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!Check(r,()=>{Require(f.Results.ConfirmedRewardGain==expectedGain&&f.Services.OfflinePostCount==2,"Claim did not use fake transport");Require(f.Services.ReadAttempts.Count==0,"Remote request during synthetic claim");Require(!room.ResultHeader.Find("GetMoreButton").GetComponent<Button>().interactable,"Claim remained repeatable");Capture(f,output,"reward-claimed",r);}))yield break;
            if(!Check(r,()=>
            {
                var dialog=f.GetComponent<RecoveredDiamondRewardDialog>();
                Require(dialog!=null&&dialog.IsOpen&&dialog.Count.text=="+"+expectedGain.ToString(System.Globalization.CultureInfo.InvariantCulture),"Confirmed diamond dialog amount missing");
                Require(dialog.Count.font==Resources.Load<Font>("FreeplayUi606/Fonts/resources.assets_914"),"Reward amount must use CLASH FONT_CN_REGULAR FINAL (resources.assets:914)");
                Click(f,dialog.Count.transform.parent.parent.GetComponent<Button>());
                Require(!dialog.IsOpen,"Full-screen reward click did not dismiss");
                Require(f.Results.ConfirmedReport==report&&f.Services.OfflinePostCount==2,"Dialog changed report state or repeated request");
            }))yield break;
            f.Services.OfflinePost=null;
            if(!Check(r,()=>{
                var fixture=new RecoveredRecommendation();
                var catalog=CatalogEnvelope.Parse(Resources.Load<TextAsset>("Catalog606/public-freeplay").text);
                Require(catalog.items.Length>=2,"Recommendation fixture requires two songs");
                for(int i=0;i<2;i++){catalog.items[i].stage.cover=null;fixture.Stages.Add(catalog.items[i]);fixture.Rsids.Add(catalog.items[i].stage.sid);}
                fixture.HotSids.Add(fixture.Stages[0].stage.sid);
                f.Results.RefreshRecommendations(fixture);
                var parent=room.ResultView.Find("Content").transform.Find("Windows result recommendations");
                Require(parent.childCount==2,"Original recommendation rows missing");
                var title=parent.GetChild(0).GetComponentsInChildren<Text>(true);
                Require(Array.Exists(title,t=>t.name=="SongText"&&t.text==fixture.Stages[0].stage.title),"Original song title not bound");
                f.Results.RefreshRecommendations(fixture);Require(parent.childCount==2,"Repeated refresh duplicated cells");
                room.ResultView.Find("Content").GetComponent<RectTransform>().anchoredPosition=new Vector2(0,650);
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>{Capture(f,output,"recommendations-original",r);Require(f.Services.ReadAttempts.Count==0,"Remote attempt during recommendations");}))yield break;
            if(!Check(r,()=>{
                var parent=room.ResultView.Find("Content").transform.Find("Windows result recommendations");
                var first=parent.GetChild(0);var title=Array.Find(first.GetComponentsInChildren<Text>(true),t=>t.name=="SongText").text;
                var play=Array.Find(first.GetComponentsInChildren<Button>(true),b=>b.name=="PlayButton");
                Click(f,play);
                Require(f.Phase!=FrontendPhase.ResultRoom&&f.Selection.Song!=null&&f.Selection.Song.title==title,"Recommendation did not select its song");
                Require(f.Services.ReadAttempts.Count==0,"Remote attempt during recommendation selection");
                Capture(f,output,"recommendation-selected",r);
            }))yield break;
            var dunk=new RecoveredDunkValidation.Result();
            yield return RecoveredDunkValidation.Run(f,Path.Combine(output,"dunk-catalog"),dunk);
            File.WriteAllText(Path.Combine(output,"dunk-catalog-results.json"),JsonUtility.ToJson(dunk,true));
            if(!Check(r,()=>Require(dunk.success,"Dunk catalog/gameplay: "+dunk.error)))yield break;
            f.ShowCatalog();f.DismissNotice();
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                Click(f,f.MainRoom.Find("SettingButton").GetComponent<Button>());
                Require(f.HomeDialogs.IsOpen,"Settings original did not open from MainRoom");
                var dialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="SettingsDialog");
                Require(dialog!=null,"Original Settings canvas missing");
                Capture(f,output,"settings-original",r);
                foreach(var key in new[]{"SE","BGM"})
                {
                    var toggle=dialog.transform.Find("BgImage/MiniBg/List/"+key+"/Parent/CheckBG");
                    var mark=toggle.Find("Checkmark").GetComponent<Image>();
                    bool initial=mark.enabled;
                    Click(f,toggle.GetComponent<Button>());
                    Require(mark.enabled!=initial,"Settings toggle did not change: "+key);
                    Require((RecoveredPreferences.GetInt("BBPC.Windows.Settings."+key,-1)==1)==mark.enabled,"Settings preference differs from checkmark: "+key);
                }
                Click(f,dialog.transform.Find("BgImage/MiniBg/CloseButton").GetComponent<Button>());
                Require(!f.HomeDialogs.IsOpen,"Settings close failed");
            }))yield break;
            yield return null;
            if(!Check(r,()=>
            {
                Click(f,f.MainRoom.Find("SettingButton").GetComponent<Button>());
                Require(f.HomeDialogs.IsOpen,"Settings did not reopen");
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var dialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="SettingsDialog");
                Require(dialog!=null,"Reopened Settings canvas missing");
                foreach(var key in new[]{"SE","BGM"})
                {
                    var toggle=dialog.transform.Find("BgImage/MiniBg/List/"+key+"/Parent/CheckBG");
                    var mark=toggle.Find("Checkmark").GetComponent<Image>();
                    Require((RecoveredPreferences.GetInt("BBPC.Windows.Settings."+key,-1)==1)==mark.enabled,"Settings preference lost on reopen: "+key);
                    bool before=mark.enabled;
                    Click(f,toggle.GetComponent<Button>());
                    Require(mark.enabled!=before,"Settings listener repeated on reopen: "+key);
                }
                Click(f,dialog.transform.Find("BgImage/MiniBg/CloseButton").GetComponent<Button>());
            }))yield break;
            yield return null;
            if(!Check(r,()=>
            {
                Click(f,f.MainRoom.Find("ExtraHpButton").GetComponent<Button>());
                Require(f.HomeDialogs.IsOpen,"ExtraHP original did not open from MainRoom");
                var dialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="PermanentExtraHPDialog");
                Require(dialog!=null,"Original ExtraHP canvas missing");
                Capture(f,output,"extra-hp-original",r);
                Click(f,dialog.transform.Find("Background/CloseButton").GetComponent<Button>());
                Require(!f.HomeDialogs.IsOpen,"ExtraHP close failed");
            }))yield break;
            yield return null;yield return null;
            var pass=new RecoveredBattlePassData{point=25,level_tasks=new[]{
                new RecoveredBattlePassLevel{level=0,free=new RecoveredTaskPeriodItem{target=0},vip=new RecoveredTaskPeriodItem{target=0}},
                new RecoveredBattlePassLevel{level=1,free=new RecoveredTaskPeriodItem{target=100},vip=new RecoveredTaskPeriodItem{target=100}},
                new RecoveredBattlePassLevel{level=2,free=new RecoveredTaskPeriodItem{target=200},vip=new RecoveredTaskPeriodItem{target=200}}}};
            var bar=f.MainRoom.Find("BattlePassBar");
            if(!Check(r,()=>
            {
                Require(f.BattlePassBar!=null&&bar.activeInHierarchy,"Authored battle pass bar unavailable");
                RecoveredBattlePassBar.ResetMark();f.BattlePassBar.Apply(pass,false);
                var slider=bar.transform.Find("Content/Slider").GetComponent<Slider>();
                Require(slider.value==25&&slider.maxValue==100,"Battle pass cumulative points used as local progress");
                Require(bar.transform.Find("NumText").GetComponent<Text>().text=="1","Battle pass display level mismatch");
                Require(RecoveredBattlePassHelpers.GetLevelPoint(pass,RecoveredBattlePassProgress.From(pass),200)==0,"Final threshold helper mismatch");
                pass.point=150;f.BattlePassBar.Apply(pass,false);
            }))yield break;
            deadline=Time.realtimeSinceStartup+2.2f;
            while(Time.realtimeSinceStartup<deadline)yield return null;
            if(!Check(r,()=>
            {
                var slider=bar.transform.Find("Content/Slider").GetComponent<Slider>();
                Require(slider.value==50&&slider.maxValue==100,"Battle pass level transition endpoint mismatch");
                Require(bar.transform.Find("Content/Slider/Text").GetComponent<Text>().text=="50 / 100","Battle pass local progress label mismatch");
                Capture(f,output,"battle-pass-bar-original",r);
                f.BattlePassBar.Apply(pass,true);
                Require(!slider.gameObject.activeSelf&&bar.transform.Find("ClaimTip").gameObject.activeSelf,"Battle pass complete-task signal ignored");
                Require(bar.transform.Find("Content/RevText").GetComponent<Text>().fontSize==42,"Battle pass claim font mismatch");
                Require(f.Services.ReadAttempts.Count==0,"Battle pass timer attempted a remote read");
                RecoveredBattlePassBar.ResetMark();
            }))yield break;
            for(int cycle=0;cycle<2;cycle++)
            {
                yield return null;yield return null;
                if(!Check(r,()=>
                {
                    Click(f,bar.GetComponent<Button>());
                    Require(f.HomeDialogs.IsOpen,"BattlePass did not open from authored bar");
                    var dialogs=Array.FindAll(f.GetComponentsInChildren<Canvas>(),c=>c.name=="BattlePassDialog");
                    Require(dialogs.Length==1,"BattlePass click created duplicate dialogs");
                    var dialog=dialogs[0].transform;
                    Require(dialog.Find("PointProgress/PointProgressText").GetComponent<Text>().text=="—","Authored progress presented as account state");
                    f.HomeDialogs.BindBattlePassProgress(pass);
                    Require(RecoveredReadContracts.BattlePassRewardBody("00000000000000000000000000000001","fixture-pass","fixture-task",0)=="{\"client_id\":\"00000000000000000000000000000001\",\"bid\":\"fixture-pass\",\"tid\":\"fixture-task\",\"type\":0}","BattlePass claim body differs from native contract");
                    bool invalidType=false;try{RecoveredReadContracts.BattlePassRewardBody("00000000000000000000000000000001","fixture-pass","fixture-task",2);}catch(ArgumentOutOfRangeException){invalidType=true;}
                    Require(invalidType,"BattlePass accepted unknown receive type");
                    long[] remaining={0,1,59999,60000,5399999,5400000,86400000,172800000};
                    string[] clockText={null,"0 s","59 s","1 min","89 min","1 hora","24 horas","2 días"};
                    for(int i=0;i<remaining.Length;i++)Require(RecoveredBattlePassHelpers.RemainingTime(remaining[i])==clockText[i],"BattlePass clock boundary mismatch");
                    f.HomeDialogs.BindBattlePassCountdown(5401000,1000);
                    Require(dialog.Find("TopPanel/TopPanelRight/RemainDay/RemainDayText").GetComponent<Text>().text=="1 hora","BattlePass clock not bound to original label");
                    f.HomeDialogs.BindBattlePassCountdown(5401000,null);
                    Require(dialog.Find("TopPanel/TopPanelRight/RemainDay/RemainDayText").GetComponent<Text>().text=="","Unsynchronized BattlePass clock presented as server time");
                    pass.level_tasks[0].free.rewards=new[]{new RecoveredTaskReward{type=1,quantity=17}};
                    pass.level_tasks[0].vip.rewards=new[]{new RecoveredTaskReward{type=1,quantity=31}};
                    pass.level_tasks[0].vip.status=2;
                    // Synthetic image shared with the packaged cover; this is not cosmetic account data.
                    var cosmeticImage=new SongImage{urls=new[]{"http://cdn.makeawesomeapps.com/mtools/beatmusic/image/Garcello_song1__0.png"}};
                    pass.level_tasks[1].free.rewards=new[]{new RecoveredTaskReward{type=2,role=new RecoveredServerRole{title="Fixture GF",type=2,icon=cosmeticImage}}};
                    pass.level_tasks[1].vip.rewards=new[]{new RecoveredTaskReward{type=3,name_box=new RecoveredServerBox{box_title="Fixture frame",fore_box=cosmeticImage}}};
                    pass.level_tasks[2].free.rewards=new[]{new RecoveredTaskReward{type=5,decor=new RecoveredServerDecor{title="Fixture accessory",icon=cosmeticImage}}};
                    pass.bonus=new RecoveredTaskPeriodItem[23];
                    for(int i=0;i<pass.bonus.Length;i++)pass.bonus[i]=new RecoveredTaskPeriodItem{status=0,rewards=new[]{new RecoveredTaskReward{type=1,quantity=26+i}}};
                    f.HomeDialogs.BindBattlePassRewards(pass);
                    f.HomeDialogs.BindBattlePassRewards(pass);
                    var rewards=dialog.Find("Rewards/RewardScroll/Viewport/Content");
                    int activeCells=0;foreach(Transform cell in rewards)if(cell.gameObject.activeSelf)activeCells++;
                    Require(activeCells==pass.level_tasks.Length+1,"BattlePass ordinary rows include bonus or duplicate cells");
                    Require(!Array.Exists(rewards.GetComponentsInChildren<Text>(true),t=>t.transform.parent.name=="Rank"&&t.text=="26"),"BattlePass bonus presented as ordinary rank");
                    var cosmeticBindings=rewards.GetComponentsInChildren<RecoveredCoverBinding>();
                    Require(cosmeticBindings.Length==3,"Cosmetic reward bindings missing or duplicated");
                    foreach(var binding in cosmeticBindings)Require(binding.Ready,"Packaged cosmetic fixture image not ready");
                    foreach(var title in new[]{"Fixture GF","Fixture frame","Fixture accessory"})
                        Require(Array.Exists(rewards.GetComponentsInChildren<Text>(true),t=>t.name=="RoleName"&&t.text==title),"Cosmetic title missing: "+title);
                    int[] amounts={0,1,25,26,50,51,75,76,100,101,150,151,250,251,1500,1501,3000,3001,7000,7001,12000,12001};
                    int[] bands={0,0,0,1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,10};
                    for(int band=0;band<amounts.Length;band++)Require(RecoveredBattlePassCells.DiamondIndex(amounts[band])==bands[band],"Diamond quantity threshold mismatch");
                    var first=Array.Find(rewards.GetComponentsInChildren<Text>(true),t=>t.name=="DiamondNumber"&&t.text=="+17");
                    Require(first!=null,"BattlePass free diamond quantity not bound");
                    Require(Array.Exists(rewards.GetComponentsInChildren<Text>(true),t=>t.name=="DiamondNumber"&&t.text=="+31"),"BattlePass VIP quantity not bound");
                    foreach(var button in rewards.GetComponentsInChildren<Button>(true))Require(!button.interactable,"Unverified BattlePass claim enabled");
                    var slider=dialog.Find("PointProgress/PointSlider").GetComponent<Slider>();
                    Require(slider.value==50&&slider.maxValue==100,"BattlePass header progress mismatch");
                    Require(dialog.Find("PointProgress/PointProgressText").GetComponent<Text>().text=="50 / 100","BattlePass header label mismatch");
                    Require(dialog.Find("PointProgress/Point").GetComponent<Text>().text=="2","BattlePass header level mismatch");
                    foreach(var path in new[]{"TopPanel/FinalSBF","Rewards/NextByDiamond","Rewards/BattlePassKeyRewrad","TopPanel/TopPanelRight/VipPassBtn"})
                        Require(!dialog.Find(path).gameObject.activeSelf,"Unbound authored offer visible: "+path);
                }))yield break;
                yield return null;yield return null;
                if(!Check(r,()=>
                {
                    var dialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="BattlePassDialog");
                    Require(dialog.transform.Find("Rewards/RewardScroll/Viewport/Content").childCount==pass.level_tasks.Length+1,"Deferred BattlePass cells survived frame boundary");
                    Capture(f,output,"battle-pass-dialog-original-"+cycle,r);
                    Click(f,dialog.transform.Find("BottomPanel/CloseBtn").GetComponent<Button>());
                    Require(!f.HomeDialogs.IsOpen,"BattlePass close failed");
                    Require(f.Services.ReadAttempts.Count==0,"Remote attempt during BattlePass navigation");
                }))yield break;
                yield return null;
                if(!Check(r,()=>Require(!Array.Exists(f.GetComponentsInChildren<Canvas>(true),c=>c.name=="BattlePassDialog"),"BattlePass hierarchy survived close")))yield break;
            }
            yield return null;
            if(!Check(r,()=>
            {
                Click(f,f.MainRoom.Find("SignInButton").GetComponent<Button>());
                var dialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="PeriodicTaskDialog").transform;
                var fixture=new RecoveredTaskPeriodList{signin=new RecoveredSignInBlock{signin_task_type=1,tasks=new[]{
                    new RecoveredTaskPeriodItem{tid="fixture-only",target=1,status=0,rewards=new[]{new RecoveredTaskReward{type=1,quantity=23}}}}}};
                f.HomeDialogs.RenderPeriodicSignIn(fixture);
                var cell=dialog.Find("BgPanel/SignIn/Periodic/Viewport/Content/SignInCell");
                Require(cell.gameObject.activeInHierarchy,"Original check-in cell hidden");
                Require(cell.Find("RewardContainer/RewardText").GetComponent<Text>().text=="23","Check-in retained prefab reward");
                Require(!cell.Find("GetButton").gameObject.activeSelf&&cell.Find("MaskImage").gameObject.activeSelf,"Incomplete check-in must hide claim and show mask");
                Capture(f,output,"check-in-original",r);
            }))yield break;
            foreach(var tab in new[]{"Daily","Weekly","SignIn"})
            {
                yield return null;yield return null;
                if(!Check(r,()=>
                {
                    var dialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="PeriodicTaskDialog").transform;
                    Click(f,dialog.Find("BgPanel/Buttons/"+tab).GetComponent<Button>());
                }))yield break;
            }
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var dialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="PeriodicTaskDialog").transform;
                Require(dialog.Find("BgPanel/SignIn/Periodic/Viewport/Content/SignInCell").gameObject.activeInHierarchy,"Check-in lost after tab switch");
                Click(f,dialog.Find("BgPanel/CloseButton").GetComponent<Button>());
                Require(!f.HomeDialogs.IsOpen&&f.Services.ReadAttempts.Count==0,"Periodic dialog close or isolation failed");
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                Click(f,f.MainRoom.Find("SignInButton").GetComponent<Button>());
                var dialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="PeriodicTaskDialog").transform;
                var fixture=new RecoveredTaskPeriodList{
                    signin=new RecoveredSignInBlock{signin_task_type=1,tasks=new[]{new RecoveredTaskPeriodItem{tid="fixture-signin",status=0,rewards=new[]{new RecoveredTaskReward{type=1,quantity=23}}}}},
                    daily=new RecoveredTaskPeriodBlock{point=new[]{new RecoveredTaskPeriodItem{desc="Fixture diaria",finish=1,target=3}},reward=new[]{new RecoveredTaskPeriodItem{desc="Hito diario",finish=1,target=3}}},
                    weekly=new RecoveredTaskPeriodBlock{point=new[]{new RecoveredTaskPeriodItem{desc="Fixture semanal",finish=2,target=5}},reward=new[]{new RecoveredTaskPeriodItem{desc="Hito semanal",finish=2,target=5}}}
                };
                f.HomeDialogs.RenderPeriodicTasks(fixture);
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var dialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="PeriodicTaskDialog").transform;
                Click(f,dialog.Find("BgPanel/Buttons/Daily").GetComponent<Button>());
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var dialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="PeriodicTaskDialog").transform;
                var dailyContent=dialog.Find("BgPanel/DailyTask/Tasks/Scroll View/Viewport/Content");
                var weeklyContent=dialog.Find("BgPanel/WeeklyTask/Tasks/Scroll View/Viewport/Content");
                int dailyActive=0;foreach(Transform cell in dailyContent)if(cell.gameObject.activeSelf)dailyActive++;
                int weeklyActive=0;foreach(Transform cell in weeklyContent)if(cell.gameObject.activeSelf)weeklyActive++;
                Require(dailyActive==1,"Daily list must contain tasks only");
                Require(weeklyActive==1,"Weekly list must contain tasks only");
                Require(dialog.Find("BgPanel/DailyTask/Progress/Reward/Bar/Milestones").childCount>0,"Daily milestone missing from bar");
                var dailyLabels=dailyContent.GetComponentsInChildren<Text>(true);
                Require(Array.Exists(dailyLabels,t=>t.text.Contains("Fixture diaria")),"Daily task label not bound");
                Require(Array.Exists(dailyLabels,t=>t.text.Contains("1/3")),"Daily progress not bound");
                Require(!Array.Exists(dailyLabels,t=>t.text.Contains("Hito diario")),"Milestone leaked into daily task list");
                var weeklyLabels=weeklyContent.GetComponentsInChildren<Text>(true);
                Require(Array.Exists(weeklyLabels,t=>t.text.Contains("Fixture semanal")),"Weekly task label not bound");
                Require(Array.Exists(weeklyLabels,t=>t.text.Contains("2/5")),"Weekly progress not bound");
                Require(!Array.Exists(weeklyLabels,t=>t.text.Contains("Hito semanal")),"Milestone leaked into weekly task list");
                var viewport=dialog.Find("BgPanel/DailyTask/Tasks/Scroll View/Viewport").GetComponent<RectTransform>();
                var viewportCorners=new Vector3[4];viewport.GetWorldCorners(viewportCorners);
                foreach(Transform row in dailyContent)
                {
                    if(!row.gameObject.activeSelf)continue;
                    var rowRect=row.GetComponent<RectTransform>();
                    Require(rowRect.rect.height>0,"Daily row has zero height");
                    Require(rowRect.rect.width>0,"Daily row has zero width");
                    var rowCorners=new Vector3[4];rowRect.GetWorldCorners(rowCorners);
                    bool intersects=(rowCorners[0].y<viewportCorners[1].y&&rowCorners[1].y>viewportCorners[0].y);
                    Require(intersects,"Daily row "+row.name+" outside viewport");
                    var label=row.GetComponentInChildren<Text>();
                    Require(label!=null&&label.rectTransform.rect.height>0&&label.rectTransform.rect.width>0,"Daily row label has zero area");
                }
                Capture(f,output,"periodic-daily-layout",r);
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var dialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="PeriodicTaskDialog").transform;
                Click(f,dialog.Find("BgPanel/Buttons/Weekly").GetComponent<Button>());
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var dialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="PeriodicTaskDialog").transform;
                var weeklyContent=dialog.Find("BgPanel/WeeklyTask/Tasks/Scroll View/Viewport/Content");
                Require(weeklyContent.gameObject.activeInHierarchy,"Weekly tab not active");
                var weeklyLabels=weeklyContent.GetComponentsInChildren<Text>(true);
                Require(Array.Exists(weeklyLabels,t=>t.text.Contains("Fixture semanal")),"Weekly task lost after tab switch");
                var weeklyViewport=dialog.Find("BgPanel/WeeklyTask/Tasks/Scroll View/Viewport").GetComponent<RectTransform>();
                var weeklyViewportCorners=new Vector3[4];weeklyViewport.GetWorldCorners(weeklyViewportCorners);
                foreach(Transform row in weeklyContent)
                {
                    if(!row.gameObject.activeSelf)continue;
                    var rowRect=row.GetComponent<RectTransform>();
                    Require(rowRect.rect.height>0&&rowRect.rect.width>0,"Weekly row has zero area after tab switch");
                    var label=row.GetComponentInChildren<Text>();
                    Require(label!=null&&label.rectTransform.rect.height>0&&label.rectTransform.rect.width>0,"Weekly row label has zero area");
                    var rowCorners=new Vector3[4];rowRect.GetWorldCorners(rowCorners);
                    bool intersects=(rowCorners[0].y<weeklyViewportCorners[1].y&&rowCorners[1].y>weeklyViewportCorners[0].y);
                    Require(intersects,"Weekly row "+row.name+" outside viewport after tab switch");
                }
                Capture(f,output,"periodic-weekly",r);
                Click(f,dialog.Find("BgPanel/Buttons/Daily").GetComponent<Button>());
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var dialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.name=="PeriodicTaskDialog").transform;
                var dailyContent=dialog.Find("BgPanel/DailyTask/Tasks/Scroll View/Viewport/Content");
                Require(dailyContent.gameObject.activeInHierarchy,"Daily tab not active after return");
                int dailyActive=0;foreach(Transform cell in dailyContent)if(cell.gameObject.activeSelf)dailyActive++;
                Require(dailyActive==1,"Daily tasks duplicated on tab return");
                var dailyViewport=dialog.Find("BgPanel/DailyTask/Tasks/Scroll View/Viewport").GetComponent<RectTransform>();
                var dailyViewportCorners=new Vector3[4];dailyViewport.GetWorldCorners(dailyViewportCorners);
                foreach(Transform row in dailyContent)
                {
                    if(!row.gameObject.activeSelf)continue;
                    var rowRect=row.GetComponent<RectTransform>();
                    Require(rowRect.rect.height>0,"Daily row has zero height after tab return");
                    var rowCorners=new Vector3[4];rowRect.GetWorldCorners(rowCorners);
                    bool intersects=(rowCorners[0].y<dailyViewportCorners[1].y&&rowCorners[1].y>dailyViewportCorners[0].y);
                    Require(intersects,"Daily row "+row.name+" outside viewport after tab return");
                }
                Capture(f,output,"periodic-daily",r);
                Click(f,dialog.Find("BgPanel/CloseButton").GetComponent<Button>());
                Require(!f.HomeDialogs.IsOpen,"Periodic dialog close failed");
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                Click(f,f.MainRoom.Find("RecommendButton").GetComponent<Button>());
                Require(f.HomeDialogs.IsOpen,"Free Song did not open");
            }))yield break;
            yield return null;yield return null;
            if(!Check(r,()=>
            {
                var dialog=Array.Find(f.GetComponentsInChildren<Canvas>(),c=>c.transform.parent!=null&&c.transform.parent.name=="ProgressiveFreeSongDialog");
                Require(dialog!=null,"Original Free Song canvas missing");
                var current=dialog.transform.Find("RecommendPanel/ShowPanel/BorderImage/Current");
                var icon=current.Find("CoverIcon").GetComponent<RawImage>();
                var size=icon.rectTransform.sizeDelta;
                var fixture=new SongItem{stage=new SongMetadata{title="Headache",artist="Smoke 'Em Out",cover=new SongImage{urls=new[]{"http://cdn.makeawesomeapps.com/mtools/beatmusic/image/Garcello_song1__0.png"}}}};
                int attempts=(f.GetComponent<RecoveredCoverCache>()??f.gameObject.AddComponent<RecoveredCoverCache>()).NetworkAttempts;
                f.HomeDialogs.BindFreeStage(fixture.stage);
                f.HomeDialogs.BindFreeStage(fixture.stage);
                var binding=current.GetComponent<RecoveredCoverBinding>();
                Require(binding!=null&&binding.Ready,"Free Song still shows placeholder instead of cached cover");
                Require(current.GetComponents<RecoveredCoverBinding>().Length==1,"Free Song duplicate cover binding");
                var bounds=((RectTransform)current).rect.size;
                Require(icon.rectTransform.sizeDelta.x<=bounds.x+0.01f&&icon.rectTransform.sizeDelta.y<=bounds.y+0.01f,"Free Song cover overflows frame");
                Require(Mathf.Abs(icon.rectTransform.sizeDelta.x/icon.rectTransform.sizeDelta.y-(float)icon.texture.width/icon.texture.height)<0.001f,"Free Song cover distorted");
                Require(!current.Find("CoverBackground").gameObject.activeSelf,"Free Song placeholder overlaps cover");
                Require(f.GetComponent<RecoveredCoverCache>().NetworkAttempts==attempts,"Free Song fetched remote cover during isolated check");
                Capture(f,output,"free-song-original",r);
                var play=dialog.transform.Find("RecommendPanel/ShowPanel/Container/Button/GetAndPlayButton").GetComponent<Button>();
                Require(play.gameObject.activeInHierarchy&&play.interactable,"Authored Free Song play access is hidden");
                var panel=dialog.transform.Find("RecommendPanel/ShowPanel");
                Require(!panel.Find("Container/Progress").gameObject.activeSelf,"Free Song leaked authored green milestone");
                var playLabel=play.transform.Find("TextParent/Text").GetComponent<Text>();
                Require(playLabel.rectTransform.anchorMin==Vector2.zero&&playLabel.rectTransform.anchorMax==Vector2.one&&playLabel.alignment==TextAnchor.MiddleCenter,"Free Song Get&Play label not centered");
                Require(((RectTransform)panel.Find("FavoritesButton")).anchoredPosition.y>((RectTransform)panel.Find("DifficultyText")).anchoredPosition.y,"Free Song favorite must appear above difficulty");
                Click(f,play);
                Require(f.Services.ReadAttempts.Count==0,"Free Song selection attempted network offline");
                Require(f.GetComponent<RecoveredChoiceLevelDialog>()==null||!f.GetComponent<RecoveredChoiceLevelDialog>().IsOpen,"Free Song must not open variants");
                f.HomeDialogs.Close();
                Require(!f.HomeDialogs.IsOpen,"Free Song close failed");
            }))yield break;
            yield return CheckHomeClaims(f,output,r);
            if(!string.IsNullOrEmpty(r.error))yield break;
            yield return CheckLeaderboard(f,output,r);
            if(!string.IsNullOrEmpty(r.error))yield break;
            string[] claimTypes={"1","2","3"};
            foreach(var type in claimTypes)
            {
                RecoveredTaskRewardResultData claimResult=null;
                f.Services.OfflinePost=(route,body)=>
                {
                    Require(route==RecoveredReadContracts.TaskPeriodReward,"Wrong periodic claim route");
                    Require(body.Contains("\"type\":\""+type+"\"")&&body.Contains("\"tid\":\"fixture-claim\""),"Wrong claim body");
                    return "{\"status\":1,\"data\":{\"rewards\":[{\"type\":1,\"quantity\":23}]}}";
                };
                yield return f.Services.ClaimTaskReward("fixture-claim",type,value=>claimResult=value);
                if(!Check(r,()=>Require(claimResult?.rewards?.Length==1&&claimResult.rewards[0].quantity==23,"Periodic claim fake transport failed")))yield break;
            }
            RecoveredTaskRewardResultData passClaim=null;
            f.Services.OfflinePost=(route,body)=>
            {
                Require(route==RecoveredReadContracts.BattlePassReward,"Wrong battle claim route");
                Require(body.Contains("\"bid\":\"fixture-pass\"")&&body.Contains("\"tid\":\"fixture-bonus\"")&&body.Contains("\"type\":0"),"Wrong bonus claim body");
                return "{\"status\":1,\"data\":{\"type\":1,\"quantity\":100}}";
            };
            yield return f.Services.ClaimBattlePassReward("fixture-pass","fixture-bonus",0,value=>passClaim=value);
            if(!Check(r,()=>Require(passClaim?.rewards?[0].quantity==100&&f.Services.ReadAttempts.Count==0,"Bonus claim or isolation failed")))yield break;
            f.Services.OfflinePost=null;
            r.success=true;
        }
    }
}
