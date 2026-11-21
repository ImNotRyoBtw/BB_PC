using System;
using UnityEngine;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    // Windows bindings for recovered countdown/death controls. No ad, IAP, rewards or remote revive.
    public sealed class RecoveredStageOverlays
    {
        public readonly RecoveredUiData Data;
        public readonly RecoveredUiTree CountdownView,DeathView;
        private readonly RecoveredUiTree play;
        private readonly Image countdownImage;
        private readonly AudioSource sound;
        private readonly Sprite[] sprites;
        private readonly Sprite[] deathSprites;
        private readonly AudioClip[] sounds;
        private readonly Button revive,abort;
        private readonly Image deathNumber;
        private float deathStarted;
        private bool showingDeath;
        public int DeathRemaining {get;private set;}
        public bool RevivePending {get;private set;}
        // The ad adapter must call back true only after verified completion.
        public Action<Action<bool>> RequestRevive;
        public Action Revived,Abort;
        private readonly GameObject pauseRoot;private readonly Text pauseLabel;
        public readonly Button PauseButton;
        public readonly Button StartButton;
        public readonly GameObject StartRoot;
        public Action Pause,Resume,Retry,Exit;
        public int CountdownSounds {get;private set;}
        public RecoveredStageOverlays(RecoveredUiTree view,RecoveredUiTree result,Camera camera,AudioSource effectSource)
        {
            play=view;sound=effectSource;Data=JsonUtility.FromJson<RecoveredUiData>(Resources.Load<TextAsset>("Recovered606/freeplay").text);
            // GameStageController.startButton is level2:70; its clickable child is level2:54.
            // Both are named StartButton, so a name lookup can select the wrong object.
            StartRoot=play.Objects["level2:70"];StartButton=play.Objects["level2:54"].GetComponent<Button>();
            StartButton.onClick=new Button.ButtonClickedEvent();StartButton.onClick.AddListener(()=>Resume?.Invoke());
            var parent=play.Find("Canvas").transform;
            CountdownView=RecoveredUiBuilder.Build(Data,Array.Find(Data.scenes,s=>s.name=="Countdown"),parent,camera);
            DeathView=RecoveredUiBuilder.Build(Data,Array.Find(Data.scenes,s=>s.name=="DeathOptions"),parent,camera);
            countdownImage=CountdownView.Find("SpriteImage").GetComponent<Image>();
            sprites=Array.ConvertAll(new[]{"resources.assets:1611","sharedassets2.assets:193","sharedassets2.assets:217","sharedassets2.assets:226"},id=>RecoveredUiBuilder.Asset<Sprite>(Data,id));
            sounds=Array.ConvertAll(new[]{"SE_Intro3","SE_Intro2","SE_Intro1","SE_IntroGo"},n=>Resources.Load<AudioClip>("Freeplay606/Sounds/"+n));
            foreach(var clip in sounds)if(clip==null)throw new InvalidOperationException("Original intro sound missing");
            foreach(var pair in DeathView.Objects)if(pair.Value.TryGetComponent<Button>(out var b))b.interactable=false;
            foreach(var n in new[]{"CountinueTimesButton","CountinueVipButton","FreeForVIPButton","MoreHealthButton"})DeathView.Find(n)?.SetActive(false);
            DeathView.Find("MoreButtonLayout")?.SetActive(true);
            DeathView.Find("CountdownAndClose")?.SetActive(true);
            revive=DeathView.Find("CountinuePlayButton").GetComponent<Button>();
            abort=DeathView.Find("Close").GetComponent<Button>();
            deathNumber=DeathView.Objects["level2:78"].GetComponent<Image>();
            deathSprites=Array.ConvertAll(new[]{"Count1","Count2","Count3"},n=>Resources.Load<Sprite>("Result606/"+n));
            foreach(var sprite in deathSprites)if(sprite==null)throw new InvalidOperationException("Missing original death countdown sprite");
            revive.onClick=new Button.ButtonClickedEvent();revive.onClick.AddListener(TryRevive);
            abort.onClick=new Button.ButtonClickedEvent();abort.onClick.AddListener(()=>{if(!showingDeath||RevivePending)return;Hide();Abort?.Invoke();});
            // Explicit Windows control until the native pause prefab/icon is recovered. Kept out of the inspection overlay.
            pauseRoot=UnityEngine.Object.Instantiate(result.Find("HomeButton"),parent,false);pauseRoot.name="WindowsPauseControls";
            var pr=pauseRoot.GetComponent<RectTransform>();pr.anchorMin=pr.anchorMax=new Vector2(.5f,1);pr.pivot=new Vector2(.5f,1);pr.anchoredPosition=new Vector2(0,-180);pr.sizeDelta=new Vector2(120,80);
            var image=pauseRoot.GetComponent<Image>();if(image!=null)image.enabled=false;
            foreach(var graphic in pauseRoot.GetComponentsInChildren<Graphic>())graphic.enabled=false;
            var label=UnityEngine.Object.Instantiate(play.Find("HPCountLabel"),pauseRoot.transform,false);label.name="WindowsPauseLabel";label.SetActive(true);var lt=label.GetComponent<Text>();pauseLabel=lt;lt.text="Ⅱ";lt.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");lt.fontSize=42;lt.resizeTextForBestFit=false;lt.raycastTarget=true;
            var lr=label.GetComponent<RectTransform>();lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.sizeDelta=Vector2.zero;lr.anchoredPosition=Vector2.zero;
            PauseButton=pauseRoot.GetComponent<Button>();PauseButton.interactable=true;PauseButton.targetGraphic=lt;PauseButton.onClick=new Button.ButtonClickedEvent();PauseButton.onClick.AddListener(()=>Pause?.Invoke());pauseRoot.SetActive(true);
            Hide();
        }
        public void ShowCountdown(int remaining)
        {
            StartRoot.SetActive(false);
            DeathView.Root.SetActive(false);CountdownView.Root.SetActive(true);CountdownView.Find("ContinueCountDownObj").SetActive(true);
            countdownImage.sprite=sprites[RecoveredCountdown.SpriteIndex(remaining)];countdownImage.color=Color.white;countdownImage.gameObject.SetActive(remaining>0);
            int soundIndex=remaining==4?0:remaining==3?1:remaining==2?2:remaining==1?3:-1;
            if(soundIndex>=0){sound.PlayOneShot(sounds[soundIndex]);CountdownSounds++;}
        }
        public void ShowDeath()
        {
            StartRoot.SetActive(false);showingDeath=true;RevivePending=false;deathStarted=Time.unscaledTime;
            CountdownView.Root.SetActive(false);DeathView.Root.SetActive(true);DeathView.Find("ReviveCountdownContainer").SetActive(true);pauseRoot.SetActive(false);
            revive.gameObject.SetActive(true);revive.interactable=RequestRevive!=null;abort.interactable=true;AdvanceDeath();
            var clip=Resources.Load<AudioClip>("Freeplay606/Sounds/SE_Loss_Sfx");if(clip!=null)sound.PlayOneShot(clip);
        }
        public void AdvanceDeath()
        {
            if(!showingDeath||RevivePending)return;
            DeathRemaining=RecoveredResult606.DeathRemaining(Time.unscaledTime-deathStarted);
            deathNumber.gameObject.SetActive(DeathRemaining>=1&&DeathRemaining<=3);
            if(DeathRemaining>=1&&DeathRemaining<=3)deathNumber.sprite=deathSprites[DeathRemaining-1];
            abort.gameObject.SetActive(DeathRemaining==0);
            float phase=Mathf.PingPong((Time.unscaledTime-deathStarted)/.375f,1);
            revive.transform.localScale=Vector3.one*Mathf.Lerp(1,1.1f,Mathf.SmoothStep(0,1,phase));
        }
        private void TryRevive()
        {
            if(!showingDeath||RevivePending||RequestRevive==null)return;
            RevivePending=true;revive.interactable=false;abort.interactable=false;
            var began=deathStarted;float suspended=Time.unscaledTime;bool answered=false;
            RequestRevive(ok=>{
                if(answered)return;answered=true;
                if(!showingDeath||deathStarted!=began)return;
                RevivePending=false;
                if(ok){Hide();Revived?.Invoke();}
                else{deathStarted+=Time.unscaledTime-suspended;revive.interactable=true;abort.interactable=true;AdvanceDeath();}
            });
        }
        public void ShowPaused(bool paused)
        {
            var mask=play.Find("Mask");mask.SetActive(paused);play.Find("tipObject")?.SetActive(false);
            StartRoot.SetActive(paused);StartButton.gameObject.SetActive(paused);StartButton.interactable=true;
            pauseRoot.SetActive(!paused);pauseLabel.text="Ⅱ";
            PauseButton.onClick.RemoveAllListeners();PauseButton.onClick.AddListener(()=>{if(paused)Resume?.Invoke();else Pause?.Invoke();});
        }
        public void ShowStart(){Hide();ShowPaused(true);}
        public void HideCountdown(){CountdownView.Root.SetActive(false);play.Find("Mask").SetActive(false);StartRoot.SetActive(false);}
        public void Hide(){showingDeath=false;RevivePending=false;CountdownView.Root.SetActive(false);DeathView.Root.SetActive(false);play.Find("Mask").SetActive(false);StartRoot.SetActive(false);sound.Stop();pauseRoot.SetActive(false);}
        public void Playing(){HideCountdown();pauseRoot.SetActive(true);ShowPaused(false);}
    }
}
