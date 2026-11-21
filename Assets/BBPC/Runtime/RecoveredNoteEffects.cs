using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    [Serializable] public sealed class RecoveredEffectKey { public float time; public float[] coeff; }
    [Serializable] public sealed class RecoveredEffectChannel
    {
        public string property; public RecoveredEffectKey[] keys;
        public float Evaluate(float time)
        {
            var key=keys[0];for(int i=1;i<keys.Length&&keys[i].time<=time;i++)key=keys[i];
            float t=Math.Max(0,time-key.time);var c=key.coeff;return ((c[0]*t+c[1])*t+c[2])*t+c[3];
        }
    }
    [Serializable] public sealed class RecoveredEffectClip { public string name,source,raw_sha256;public float duration;public bool loop;public string[] sprites;public RecoveredEffectChannel[] channels; }
    [Serializable] public sealed class RecoveredEffectsConfig { public RecoveredEffectClip[] clips; }
    // Original sharedassets2 clips: streamed cubic coefficients, PPtr sprites, sizes and pivots.
    // No AnimatorController from an Android bundle is loaded into the Windows Player.
    public sealed class RecoveredNoteEffects
    {
        public RecoveredUiData Data {get;private set;}
        public int MusicalEmissions {get;private set;}
        public int HitEmissions {get;private set;}
        public int ActiveMusicals => active.Count;
        public float FirstMusicalY => active.Count==0?0:active[0].image.rectTransform.anchoredPosition.y;
        private readonly RecoveredEffectClip musical;
        private readonly RecoveredEffectClip[] hits=new RecoveredEffectClip[4];
        private readonly RecoveredEffectClip[] loops=new RecoveredEffectClip[4];
        private readonly Image[] drums=new Image[4];private readonly float[] elapsed={-1,-1,-1,-1};
        private readonly RecoveredUiTree view;private readonly Transform container;
        private readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        private sealed class Flying {public RecoveredUiTree tree;public Image image;public float elapsed;}
        private readonly List<Flying> active=new List<Flying>();private readonly Queue<Flying> pool=new Queue<Flying>();
        public RecoveredNoteEffects(RecoveredUiTree playroom)
        {
            view=playroom;container=view.Find("TransientNode").transform;
            Data=JsonUtility.FromJson<RecoveredUiData>(Resources.Load<TextAsset>("Recovered606/note-effects-ui").text);
            var config=JsonUtility.FromJson<RecoveredEffectsConfig>(Resources.Load<TextAsset>("Recovered606/note-effects").text);
            musical=Array.Find(config.clips,c=>c.name=="MusicalNode");
            string[] names={"LeftKey","DownKey","UpKey","RightKey"};
            for(int i=0;i<4;i++){hits[i]=Array.Find(config.clips,c=>c.name==names[i]);drums[i]=view.Find("Drum"+(i+1)).GetComponent<Image>();}
            string[] holdNames={"leftHold","DownHold","UpHold","RightHold"};
            for(int i=0;i<4;i++){loops[i]=Array.Find(config.clips,c=>c.name==holdNames[i]);if(loops[i]==null||!loops[i].loop)throw new InvalidOperationException("Original HoldNode loop missing");}
            foreach(var asset in Data.assets)if(asset.type=="Sprite")sprites.Add(asset.id,RecoveredUiBuilder.Asset<Sprite>(Data,asset.id));
            Clear();
        }
        public void ComboIncreased(RecoveredStageNote note)
        {
            if(!note.Human)return;
            // One emission per accepted score increment, including hold checkpoints; duplicates never reach this callback.
            MusicalEmissions++;
            Flying flying;
            if(pool.Count>0)flying=pool.Dequeue();
            else{var tree=RecoveredUiBuilder.Build(Data,Data.scenes[0],container);flying=new Flying{tree=tree,image=tree.Find("MusicalNode").GetComponent<Image>()};}
            var root=flying.tree.Root.GetComponent<RectTransform>();root.position=view.Find("KeyBoard"+(note.Action.key+1)).transform.position;root.localPosition+=new Vector3(0,100,0);
            flying.elapsed=0;flying.tree.Root.SetActive(true);Sample(flying.image,musical,0);active.Add(flying);
        }
        // Key feedback is independent of judgement: empty-lane presses animate too.
        public void Press(int key)
        {
            if(key<0||key>=4)throw new ArgumentOutOfRangeException(nameof(key));
            HitEmissions++;elapsed[key]=0;Sample(drums[key],hits[key],0);
        }
        public void SampleHold(Image image,int key,float seconds)
        {
            var clip=loops[key];Sample(image,clip,Math.Max(0,seconds)%clip.duration);
        }
        public void Advance(float seconds)
        {
            if(seconds<0||float.IsNaN(seconds)||float.IsInfinity(seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
            for(int i=0;i<4;i++)if(elapsed[i]>=0){elapsed[i]+=seconds;Sample(drums[i],hits[i],Math.Min(elapsed[i],hits[i].duration));if(elapsed[i]>=hits[i].duration)elapsed[i]=-1;}
            for(int i=active.Count-1;i>=0;i--){var f=active[i];f.elapsed+=seconds;Sample(f.image,musical,Math.Min(f.elapsed,musical.duration));if(f.elapsed>=musical.duration){f.tree.Root.SetActive(false);pool.Enqueue(f);active.RemoveAt(i);}}
        }
        private void Sample(Image image,RecoveredEffectClip clip,float time)
        {
            var r=image.rectTransform;var size=r.sizeDelta;var pivot=r.pivot;var position=r.anchoredPosition;var scale=r.localScale;var color=image.color;
            foreach(var c in clip.channels)
            {
                float value=c.Evaluate(time);
                switch(c.property)
                {
                    case "m_SizeDelta.x":size.x=value;break;case "m_SizeDelta.y":size.y=value;break;
                    case "m_Pivot.x":pivot.x=value;break;case "m_Pivot.y":pivot.y=value;break;
                    case "m_AnchoredPosition.x":position.x=value;break;case "m_AnchoredPosition.y":position.y=value;break;
                    case "m_LocalScale.x":scale.x=value;break;case "m_LocalScale.y":scale.y=value;break;case "m_LocalScale.z":scale.z=value;break;
                    case "m_Color.r":color.r=value;break;case "m_Color.g":color.g=value;break;case "m_Color.b":color.b=value;break;case "m_Color.a":color.a=value;break;
                    case "m_Sprite":image.sprite=sprites[clip.sprites[(int)value]];break;
                    default:throw new NotSupportedException("Unmapped original effect channel: "+c.property);
                }
            }
            r.sizeDelta=size;r.pivot=pivot;r.anchoredPosition=position;r.localScale=scale;image.color=color;
        }
        public void Clear()
        {
            foreach(var f in active){f.tree.Root.SetActive(false);pool.Enqueue(f);}active.Clear();
            for(int i=0;i<4;i++){elapsed[i]=-1;var color=drums[i].color;color.a=0;drums[i].color=color;}
            MusicalEmissions=HitEmissions=0;
        }
    }
}
