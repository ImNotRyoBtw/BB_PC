using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BBPC.Gameplay;
namespace BBPC.UnityRuntime
{
    // PlayRoomResultNodes 6.0.6: UpdateResult/DealResultImageAction/DealComboActions.
    // DOTweenSettings resources.assets:5315: OutQuad (6), scaled Update; Sequence.Setup uses Linear (1).
    // Replay only these recovered property sequences; no Android controller or new tween dependency.
    public sealed class RecoveredJudgementFeedback
    {
        private readonly RecoveredUiTree view;
        private readonly RecoveredUiData data;
        private readonly RecoveredNoteBindings bindings;
        private readonly Image rating,combo;
        private readonly RectTransform layout;
        private readonly Image[] digits;
        private readonly Sprite empty;
        private float time,latest=-100;
        private enum Property { Alpha, Y, Scale }
        private sealed class Tween
        {public Image image;public Property property;public float start,duration,target,from;public bool initialized;}
        private readonly List<Tween> tweens=new List<Tween>();
        public int Notifications {get;private set;}
        public Image Rating => rating;
        public RectTransform NumberLayout => layout;
        public static float NumberLayoutX(int count) => (count-1)*-60f*.4375f+120f;
        public static float OutQuad(float t){t=Mathf.Clamp01(t);return t*(2-t);}
        public RecoveredJudgementFeedback(RecoveredUiTree view,RecoveredUiData data,RecoveredNoteBindings bindings)
        {
            this.view=view;this.data=data;this.bindings=bindings;
            rating=view.Find("ResultImage").GetComponent<Image>();combo=view.Find("Combo").GetComponent<Image>();
            layout=view.Find("NumberLayout").GetComponent<RectTransform>();
            digits=Array.ConvertAll(bindings.digitObjects,id=>view.Objects[id].GetComponent<Image>());
            empty=RecoveredUiBuilder.Asset<Sprite>(data,bindings.judgements[4]);Reset();
        }
        private static void Alpha(Image image,float value){var c=image.color;c.a=value;image.color=c;}
        public void Reset()
        {
            tweens.Clear();time=0;latest=-100;Notifications=0;
            view.Find("ResultNodes").SetActive(true);layout.gameObject.SetActive(true);layout.localPosition=new Vector3(0,-10,0);
            rating.rectTransform.localPosition=new Vector3(0,-100,0);rating.gameObject.SetActive(false);
            combo.sprite=empty;Alpha(combo,1);
            foreach(var image in digits){image.gameObject.SetActive(true);image.sprite=empty;Alpha(image,1);}
        }
        private void Kill(Image image){tweens.RemoveAll(t=>t.image==image);}
        private void Add(Image image,Property property,float target,float delay,float duration)
        {tweens.Add(new Tween{image=image,property=property,target=target,start=time+delay,duration=duration});}
        public void Show(HitQuality quality,int count)
        {
            if(quality==HitQuality.None)return;
            Notifications++;rating.gameObject.SetActive(true);Kill(rating);
            rating.sprite=RecoveredUiBuilder.Asset<Sprite>(data,bindings.judgements[5-(int)quality]);
            rating.rectTransform.localPosition=new Vector3(0,-100,0);rating.rectTransform.localScale=Vector3.one*.5f;Alpha(rating,1);
            Add(rating,Property.Scale,1,0,.2f);Add(rating,Property.Alpha,1,.2f,.5f);Add(rating,Property.Alpha,0,.7f,.4f);
            string text=count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            layout.localPosition=new Vector3(NumberLayoutX(text.Length),-10,0);
            float delay=time-latest<.1f?.1f:0;
            for(int i=0;i<digits.Length;i++)
            {
                var image=digits[i];
                if(!image.gameObject.activeInHierarchy)
                {Kill(image);Alpha(image,1);var p=image.rectTransform.localPosition;p.y=delay>0?30:0;image.rectTransform.localPosition=p;}
                image.sprite=i<text.Length?RecoveredUiBuilder.Asset<Sprite>(data,bindings.digits[text[i]-'0']):empty;
            }
            if(quality==HitQuality.Miss)
            {
                layout.gameObject.SetActive(false);combo.sprite=empty;
                foreach(var image in digits)image.sprite=empty;
            }
            else
            {
                combo.sprite=RecoveredUiBuilder.Asset<Sprite>(data,bindings.combo);layout.gameObject.SetActive(true);
                foreach(var image in digits)
                {
                    // Native fast repeat branch: move for .1, wait .1; ordinary branch: move for .2.
                    Add(image,Property.Y,60,0,delay>0?delay:.2f);
                    if(delay>0)Add(image,Property.Alpha,1,delay,.2f-delay);
                    Add(image,Property.Alpha,1,.2f,.5f);Add(image,Property.Alpha,0,.7f,.4f);
                }
                if(delay>0)Add(combo,Property.Alpha,1,0,.2f-delay);
                Add(combo,Property.Alpha,1,delay>0?.2f-delay:0,.7f);
                Add(combo,Property.Alpha,0,delay>0?.9f-delay:.7f,.4f);
            }
            latest=time;
        }
        public void Advance(float seconds)
        {
            if(seconds<0||float.IsNaN(seconds)||float.IsInfinity(seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
            time+=seconds;
            // Preserve sequence insertion order: rapid judgements can overlap on combo/digit properties.
            for(int i=0;i<tweens.Count;)
            {
                var t=tweens[i];if(time<t.start){i++;continue;}
                var r=t.image.rectTransform;
                if(!t.initialized){t.from=t.property==Property.Alpha?t.image.color.a:t.property==Property.Y?r.localPosition.y:r.localScale.x;t.initialized=true;}
                float value=Mathf.LerpUnclamped(t.from,t.target,OutQuad((time-t.start)/t.duration));
                if(t.property==Property.Alpha)Alpha(t.image,value);
                else if(t.property==Property.Y){var p=r.localPosition;p.y=value;r.localPosition=p;}
                else r.localScale=Vector3.one*value;
                if(time>=t.start+t.duration)tweens.RemoveAt(i);else i++;
            }
        }
    }
}
