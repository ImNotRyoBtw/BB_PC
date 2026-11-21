using UnityEngine;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    // Equivalent of MarqueeForText.StartMarquee: native linear DOLocalMoveX, repeated text + eight spaces.
    // Native constants: sizeDelta(1500,84); cycle width = preferredWidth(original + separator); duration = width/speed.
    public sealed class RecoveredMarqueeText : MonoBehaviour
    {
        private Text label;private RectTransform rect;private Vector2 authoredSize;private UiMarquee config;private bool started,pending;
        private float elapsed,cycleWidth;
        public string OriginalText{get;private set;}
        public float CycleWidth=>cycleWidth;
        public void Configure(Text text,UiMarquee source){label=text;rect=text.rectTransform;authoredSize=rect.sizeDelta;config=source;OriginalText=text.text;pending=true;}
        public void SetText(string value)
        {
            value=value??"";if(value==OriginalText&&!pending)return;
            OriginalText=value;label.text=value;rect.sizeDelta=authoredSize;rect.localPosition=Vector3.zero;elapsed=0;cycleWidth=0;pending=true;
        }
        private void Start(){started=true;}
        private void Update(){if(!started||config==null||!config.autoStart)return;if(pending)Begin();Advance(Time.deltaTime);}
        private void Begin()
        {
            pending=false;elapsed=0;cycleWidth=0;label.text=OriginalText;rect.sizeDelta=authoredSize;rect.localPosition=Vector3.zero;
            if(rect.rect.width<=0){pending=true;return;}
            if(label.preferredWidth<rect.rect.width)return;
            rect.sizeDelta=new Vector2(1500,84);label.text=OriginalText+"        ";cycleWidth=label.preferredWidth;
            label.text=OriginalText+"        "+OriginalText;
        }
        public void Advance(float delta)
        {
            if(pending||cycleWidth<=0||config.speed<=0||delta<0)return;
            elapsed+=delta;float moveDuration=cycleWidth/config.speed;float cycle=moveDuration+config.delay;
            float phase=Mathf.Max(0,elapsed-config.delay)%cycle;
            var position=rect.localPosition;position.x=-Mathf.Min(cycleWidth,phase*config.speed);rect.localPosition=position;
        }
    }
}
