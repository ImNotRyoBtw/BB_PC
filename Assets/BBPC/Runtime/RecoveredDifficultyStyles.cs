using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    public struct RecoveredDifficultyStyle
    {
        public readonly int level;
        public readonly string resourceId,key;
        public readonly Color textColor,borderColor;
        public RecoveredDifficultyStyle(int level,string resourceId,string key,Color textColor,Color borderColor)
        {this.level=level;this.resourceId=resourceId;this.key=key;this.textColor=textColor;this.borderColor=borderColor;}
    }
    // Independent serialized tables: DifficultyStyleConfig and SelectorCell.spriteDict (6.0.6).
    public static class RecoveredDifficultyStyles
    {
        [Serializable] public sealed class Entry {public int level;public string key,sprite,controller;public float[] textColor,borderColor;}
        [Serializable] public sealed class Tables {public Entry[] styles,selector;}
        private static Tables tables;
        private static RecoveredUiData data,dialogData;
        public static RecoveredUiData DialogData=>dialogData??(dialogData=JsonUtility.FromJson<RecoveredUiData>(Resources.Load<TextAsset>("FreeplayUi606/dialog").text));
        public static RecoveredUiData Data => data??(data=JsonUtility.FromJson<RecoveredUiData>(Resources.Load<TextAsset>("FreeplayUi606/ui").text));
        public static Tables Config => tables??(tables=JsonUtility.FromJson<Tables>(Resources.Load<TextAsset>("FreeplayUi606/styles").text));
        private static Color ColorOf(float[] a)=>new Color(a[0],a[1],a[2],a[3]);
        public static bool TryGet(int difficulty,out RecoveredDifficultyStyle style)=>TryGet(difficulty,false,out style);
        public static bool TryGet(int difficulty,bool selector,out RecoveredDifficultyStyle style)
        {
            style=default(RecoveredDifficultyStyle);
            // Omega exists in the model; its animated presentation is explicitly deferred.
            if(difficulty<1||difficulty>(selector?8:7))return false;
            var source=Config.styles.Single(x=>x.level==difficulty);
            var entry=selector?Config.selector.Single(x=>x.level==difficulty):source;
            style=new RecoveredDifficultyStyle(difficulty,entry.sprite,source.key,ColorOf(entry.textColor),ColorOf(entry.borderColor));return true;
        }
        public static string Label(int difficulty)
        {
            if(difficulty==8)return RecoveredLocalization.Text("difficulty_omega","Omega");
            RecoveredDifficultyStyle s;return TryGet(difficulty,out s)?RecoveredLocalization.Text(s.key,s.key):"";
        }
        public static void Apply(RecoveredUiData source,RecoveredUiTree row,SongMetadata song)
        {
            if(song==null)return;
            RecoveredDifficultyStyle style;bool available=TryGet(song.CurrentDifficulty,out style);
            var border=row.Find("BorderImage")?.GetComponent<Image>();
            if(border!=null){border.enabled=available;if(available)border.sprite=RecoveredUiBuilder.Asset<Sprite>(Data,style.resourceId);}
            var text=row.Find("DifficultyText")?.GetComponent<Text>();
            if(text!=null)ApplyLabel(text,song.CurrentDifficulty);
        }
        public static void ApplySelector(RecoveredUiTree row,int level,bool selected)
        {
            RecoveredDifficultyStyle style;if(!TryGet(level,true,out style))throw new ArgumentOutOfRangeException(nameof(level));
            var image=row.Find("SelectorCell").GetComponent<Image>();image.sprite=RecoveredUiBuilder.Asset<Sprite>(Data,style.resourceId);image.color=Color.white;
            var text=row.Find("RankNameText").GetComponent<Text>();text.text=Label(level);text.color=style.textColor;ApplyShadow(text,style.borderColor);
            row.Find("GetImage").SetActive(selected);
        }
        public static void ApplyLabel(Text text,int difficulty)
        {
            RecoveredDifficultyStyle style;bool available=TryGet(difficulty,out style);text.text=Label(difficulty);text.enabled=available;
            if(available){text.color=style.textColor;ApplyShadow(text,style.borderColor);}
            // Keep the authored rectangle; fit long localized names on one line.
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.resizeTextForBestFit=true;
            text.resizeTextMinSize=10;text.resizeTextMaxSize=text.fontSize;
        }
        private static void ApplyShadow(Text text,Color color)
        {foreach(var shadow in text.GetComponents<RecoveredUiShadow>())shadow.effectColor=color;}
    }
}
