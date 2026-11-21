using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BBPC.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    // Windows presentation. Get More uses the evidenced direct reward endpoint; gains remain server-owned.
    public sealed class RecoveredResultPanel : MonoBehaviour
    {
        public RecoveredStageResult CurrentResult { get; private set; }
        public string ReportStatus { get; private set; }
        public RecoveredReportResultData ConfirmedReport { get; private set; }
        private RecoveredFrontend frontend;
        private GameObject root;
        private Text reward;
        private Font font;
        private bool claimingReward, rewardAttempted;
        private string reportClientId, rewardStatus;
        private Text factorStrip, getMoreLabel, baseDiamondLabel, claimedLabel;
        private RectTransform rewardRoot, rewardBar, rewardCursor;
        private GameObject claimedImage, rewardDiamondImage;
        private Button getMore;
        private float factorStarted;
        public int SelectedFactorIndex { get; private set; }
        public int? ConfirmedRewardGain { get; private set; }
        private readonly List<GameObject> rows = new List<GameObject>();
        private RecoveredRecommendation displayedRecommendation;
        private RecoveredCoverCache covers;
        private RecoveredDiamondRewardDialog diamondDialog;
        private GameObject obtained;
        private Text appendDiamondsTextObtained;

        public void Build(RecoveredFrontend owner)
        {
            if (root != null) return;
            frontend = owner;
            var room = owner.PlayRoom;
            font = room.ResultDifficultyText.font;
            var header = room.ResultHeader.Find("ResultHeadView").transform;
            reward = Label(header, "Windows confirmed reward", 700, 200, 20);
            reward.resizeTextForBestFit = true;
            reward.resizeTextMinSize = 16;
            reward.resizeTextMaxSize = 20;
            rewardRoot = room.ResultHeader.Find("GetDiamonds").GetComponent<RectTransform>();
            rewardBar = rewardRoot.Find("SlidingRewardBar").GetComponent<RectTransform>();
            rewardCursor = rewardBar.Find("Cursor").GetComponent<RectTransform>();
            baseDiamondLabel = rewardRoot.Find("DiaImage/DiaText").GetComponent<Text>();
            getMore = rewardRoot.Find("GetMoreButton").GetComponent<Button>();
            getMoreLabel = getMore.transform.Find("DiaImage/DiaText").GetComponent<Text>();
            rewardDiamondImage = getMore.transform.Find("DiaImage").gameObject;
            claimedImage = getMore.transform.Find("GetImage").gameObject;
            claimedLabel = claimedImage.transform.Find("Text")?.GetComponent<Text>();
            diamondDialog = gameObject.AddComponent<RecoveredDiamondRewardDialog>();
            obtained = room.ResultHeader.Find("Obtained");
            appendDiamondsTextObtained = room.ResultHeader.Find("appendDiamondsTextObtained")?.GetComponent<Text>();
            // TODO: recover ResultDiamondPart's serialized Obtained (0x48) and
            // appendDiamondsTextObtained (0x40) prefab references/layout (dump TDI 2271).
            // Neither node is in gameplay.json; retain the recovered GetImage/GetDiaImage state.
            getMore.transform.Find("ADImage").gameObject.SetActive(true);
            // Keep authored geometry of the original reward controls.
            factorStrip = Label(header, "Windows reward factors", 950, 60, 22);
            getMore.onClick.RemoveAllListeners(); getMore.onClick.AddListener(ClaimSelectedReward);
            rewardRoot.gameObject.SetActive(false);
            root = new GameObject("Windows result recommendations", typeof(RectTransform));
            root.transform.SetParent(room.ResultView.Find("Content").transform, false);
            var headerHeight=Math.Abs(header.GetComponent<RectTransform>().sizeDelta.y);
            Place(root.GetComponent<RectTransform>(), headerHeight, 0);
            root.GetComponent<RectTransform>().sizeDelta=new Vector2(Math.Abs(owner.Data.row.objects[0].rect[6]),0);
            covers=owner.GetComponent<RecoveredCoverCache>()??owner.gameObject.AddComponent<RecoveredCoverCache>();
            root.SetActive(false);
        }

        public void SetOpen(bool open)
        {
            if (root == null) return;
            root.SetActive(open);
            if (!open) diamondDialog.Dismiss();
            reward.gameObject.SetActive(false);
            factorStrip.gameObject.SetActive(false);
            rewardRoot.gameObject.SetActive(open && ConfirmedReport != null);
            if (open) Refresh();
        }

        public void BeginResult(RecoveredStageResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (root == null) throw new InvalidOperationException("Build the result panel before showing a result");
            diamondDialog.Dismiss();
            CurrentResult = result; ConfirmedReport = null;
            claimingReward=false;rewardAttempted=false;reportClientId=null;rewardStatus=null;
            ConfirmedRewardGain=null;SelectedFactorIndex=0;factorStarted=Time.unscaledTime;
            ReportStatus = "Reporte pendiente";
            SetOpen(true);
        }

        public bool IsCurrent(RecoveredStageResult result)
        {
            return result != null && ReferenceEquals(CurrentResult, result) &&
                ReferenceEquals(frontend.PlayRoom.Result, result) && frontend.PlayRoom.State == RecoveredPlayState.Results &&
                frontend.Phase == FrontendPhase.ResultRoom;
        }

        public void SetReportStatus(RecoveredStageResult result, string message, RecoveredReportResultData confirmed = null)
        {
            if (!IsCurrent(result)) return;
            if(ConfirmedReport==null&&confirmed!=null)
            {reportClientId=frontend.Services.LastReportClientId;factorStarted=Time.unscaledTime;}
            ReportStatus = message; ConfirmedReport = confirmed; Refresh();
        }

        public void Close()
        {
            CurrentResult = null; ConfirmedReport = null;
            SetOpen(false);
        }

        public void Refresh()
        {
            if (root == null || CurrentResult == null) return;
            reward.text = ReportStatus + "\nDiamantes ganados: " + (ConfirmedReport?.gain_diamond?.ToString(CultureInfo.InvariantCulture) ?? "—") +
                "\nMultiplicador: " + (ConfirmedReport?.multiple_factor?.ToString(CultureInfo.InvariantCulture) ?? "—");
            if(!string.IsNullOrEmpty(rewardStatus))reward.text += "\n"+rewardStatus;
            if(ConfirmedReport==null&&ReportStatus!="Reporte pendiente")frontend.ShowUnavailable(ReportStatus);
            RefreshFactors();
            RefreshRecommendations(frontend.Services.LastRecommendation);
        }

        internal void RefreshRecommendations(RecoveredRecommendation recommendation)
        {
            if(ReferenceEquals(displayedRecommendation,recommendation))return;
            displayedRecommendation=recommendation;
            foreach (var row in rows) { row.SetActive(false); Destroy(row); }
            rows.Clear();
            float y = 0;
            if(recommendation!=null)foreach(var item in recommendation.Stages)
            {
                if(item?.stage==null)continue;
                AddRow(item,recommendation.HotSids.Contains(item.stage.sid),ref y);
            }
            var heading=frontend.PlayRoom.ResultHeader.Find("TryNewSongText");
            // The recovered heading is under a disabled background; preserve its authored world placement.
            heading.transform.SetParent(frontend.PlayRoom.ResultHeader.Find("ResultHeadView").transform,true);
            heading.SetActive(rows.Count>0);
            var content = frontend.PlayRoom.ResultView.Find("Content").GetComponent<RectTransform>();
            content.sizeDelta = new Vector2(content.sizeDelta.x, -root.GetComponent<RectTransform>().anchoredPosition.y+y+100);
            root.GetComponent<RectTransform>().sizeDelta=new Vector2(root.GetComponent<RectTransform>().sizeDelta.x,y);
        }

        private bool CanClaim()
        {
            var service=frontend.Services;
            return IsCurrent(CurrentResult)&&ConfirmedReport!=null&&ConfirmedReport.multiple_factors!=null&&
                ConfirmedReport.multiple_factors.Length==7&&!rewardAttempted&&!claimingReward&&
                !frontend.PlayRoom.IsLocalSandbox&&!string.IsNullOrEmpty(reportClientId)&&
                reportClientId==service.LastReportClientId&&CurrentResult.sid==service.LastReportSid&&
                CurrentResult.aid==service.LastReportAid&&
                ((RecoveredStorage.Isolated&&service.OfflinePost!=null)||(!frontend.PlayRoom.UsedLocalRevive&&service.Identity.HasPair&&service.TimeSynced&&service.ProfileVerified));
        }
        private void Update()
        {
            if(root==null||!root.activeInHierarchy||!IsCurrent(CurrentResult))return;
            if(CanClaim())
            {
                float x=RecoveredResult606.CursorX(Time.unscaledTime-factorStarted);
                SelectedFactorIndex=RecoveredResult606.FactorIndex(x);
                var position=rewardCursor.localPosition;
                position.x=x;
                rewardCursor.localPosition=position;
            }
            RefreshFactors();
        }
        private void RefreshFactors()
        {
            var factors=ConfirmedReport?.multiple_factors;
            if(factors==null||factors.Length==0)factorStrip.text="Multiplicadores pendientes del reporte";
            else
            {
                var labels=new string[factors.Length];
                for(int i=0;i<factors.Length;i++)labels[i]=(i==SelectedFactorIndex?"[":" ")+"×"+factors[i].ToString(CultureInfo.InvariantCulture)+(i==SelectedFactorIndex?"]":" ");
                factorStrip.text=string.Join("   ",labels);
                var names=new[]{"x2 Text Left","x3 Text Left","x4 Text Left","x5 Text","x4 Text","x3 Text","x2 Text"};
                if(factors.Length==names.Length)for(int i=0;i<names.Length;i++)
                    rewardBar.Find(names[i]).GetComponent<Text>().text="X"+factors[i].ToString(CultureInfo.InvariantCulture);
            }
            rewardRoot.gameObject.SetActive(root.activeSelf && ConfirmedReport != null);
            baseDiamondLabel.text="+ "+(ConfirmedReport?.gain_diamond?.ToString(CultureInfo.InvariantCulture)??"—");
            getMore.transform.Find("ADImage").gameObject.SetActive(!ConfirmedRewardGain.HasValue);
            getMore.interactable=CanClaim();
            bool received=ConfirmedRewardGain.HasValue;
            claimedImage.SetActive(received); rewardDiamondImage.SetActive(!received);
            if (obtained != null) obtained.SetActive(received);
            if (appendDiamondsTextObtained != null)
            {
                appendDiamondsTextObtained.gameObject.SetActive(received);
                if (received) appendDiamondsTextObtained.text = "+ " + ConfirmedRewardGain.Value.ToString(CultureInfo.InvariantCulture);
            }
            if(claimedLabel!=null)claimedLabel.text="Reclamadas";
            if(received)
            {
                claimedImage.transform.Find("GetDiaImage").gameObject.SetActive(true);
                var receivedLabel=claimedImage.transform.Find("GetDiaImage/DiaText")?.GetComponent<Text>();
                if(receivedLabel!=null)receivedLabel.text="+ "+ConfirmedRewardGain.Value.ToString(CultureInfo.InvariantCulture);
            }
            if(claimingReward)getMoreLabel.text="Reclamando…";
            else if(rewardAttempted)getMoreLabel.text="Enviada";
            else if(factors!=null&&factors.Length>0&&ConfirmedReport.gain_diamond.HasValue)
                getMoreLabel.text="+ "+(ConfirmedReport.gain_diamond.Value*factors[SelectedFactorIndex]).ToString("0.##",CultureInfo.InvariantCulture);
            else getMoreLabel.text="—";
        }
        private void ClaimSelectedReward()
        {
            if(!CanClaim())return;
            // Capture the index already visible this frame; no speculative freeze or ad provider.
            int index=SelectedFactorIndex;var played=CurrentResult;var clientId=reportClientId;
            rewardAttempted=true;claimingReward=true;rewardStatus="Reclamando recompensa del servidor…";Refresh();
            StartCoroutine(ClaimSelectedRewardFlow(played,clientId,index));
        }
        private IEnumerator ClaimSelectedRewardFlow(RecoveredStageResult played,string clientId,int index)
        {
            // Compatibility request: singleplayer loading is user-observed; the native
            // LoadingDialog field is confirmed for VS, not the inspected non-VS methods.
            using (var loading = RecoveredClaimLoading.Begin(frontend.transform, frontend.PortraitCamera))
            {
            RecoveredRewardResultData answer=null;
            yield return frontend.Services.ClaimReward(index,value=>answer=value);
            if(!IsCurrent(played)||clientId!=reportClientId)yield break;
            claimingReward=false;
            if(answer==null)
            {rewardStatus="Recompensa sin confirmar. No se reintenta para evitar duplicados.";frontend.ShowUnavailable(rewardStatus+" "+frontend.Services.Status);Refresh();yield break;}
            ConfirmedRewardGain=answer.gain_diamond;
            rewardStatus="Recompensa confirmada: "+(ConfirmedRewardGain?.ToString(CultureInfo.InvariantCulture)??"—")+" diamantes";Refresh();
            if(RecoveredStorage.Isolated)
            {
                loading.Dispose();
                ShowConfirmedDiamondReward();
                yield break;
            }
            bool refreshed=false;
            yield return frontend.Services.RefreshWealth(WealthRefreshTrigger.Reward,value=>refreshed=value!=null);
            if(!IsCurrent(played)||clientId!=reportClientId)yield break;
            frontend.Services.LogResultEvent(RecoveredReadContracts.ResultReward,refreshed?"wealth-confirmed":"wealth-unconfirmed",ConfirmedRewardGain,refreshed?(int?)frontend.Account.Wealth.diamond:null);
            loading.Dispose();
            ShowConfirmedDiamondReward();
            }
        }

        private void ShowConfirmedDiamondReward()
        {
            if (ConfirmedRewardGain.HasValue)
                diamondDialog.Show(frontend.transform, frontend.PortraitCamera, ConfirmedRewardGain.Value);
        }

        private void AddRow(SongItem item,bool hot,ref float y)
        {
            var row=RecoveredUiBuilder.Build(frontend.Data,frontend.Data.row,root.transform);
            var authored=row.Objects[frontend.Data.row.objects[0].id].GetComponent<RectTransform>();
            float height=Math.Abs(frontend.Data.row.objects[0].rect[7]);
            authored.anchorMin=authored.anchorMax=new Vector2(.5f,1);authored.pivot=new Vector2(.5f,1);
            authored.anchoredPosition=new Vector2(0,-y);authored.gameObject.SetActive(true);
            foreach(var go in row.Objects.Values)if(go.TryGetComponent<Button>(out var control)){control.onClick.RemoveAllListeners();control.interactable=false;}
            row.Find("SongText").GetComponent<Text>().text=item.stage.title??"";
            row.Find("ArtistText").GetComponent<Text>().text=item.stage.artist??"";
            foreach(var name in new[]{"Stars","ADButton","CoverIcon","FavoritesButton"})row.Find(name)?.SetActive(false);
            row.Find("HotTag")?.SetActive(hot);
            row.Find("NewTag")?.SetActive(item.stage.sub_mark!=null&&item.stage.sub_mark.tag==1);
            RecoveredDifficultyStyles.Apply(frontend.Data,row,item.stage);
            row.Root.AddComponent<RecoveredCoverBinding>().Bind(covers,item.stage.cover,row.Find("CoverIcon")?.GetComponent<RawImage>(),row.Find("DefaultCoverImage"));
            var play=row.Find("PlayButton").GetComponent<Button>();play.interactable=true;
            play.onClick.AddListener(()=>{frontend.ShowCatalog();frontend.FullCatalog?.Select(item,true);});
            rows.Add(row.Root);y+=height;
        }
        private Text Label(Transform parent, string name, float y, float height, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), y, height);
            var text = go.GetComponent<Text>(); text.font = font; text.fontSize = size;
            text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.supportRichText = false;
            text.raycastTarget = false; return text;
        }
        private static void Place(RectTransform rt, float y, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1); rt.pivot = new Vector2(.5f, 1);
            rt.anchoredPosition = new Vector2(0, -y); rt.sizeDelta = new Vector2(780, height);
        }
    }
}
