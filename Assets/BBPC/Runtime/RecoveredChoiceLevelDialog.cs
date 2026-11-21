using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    // Original bundled ChoiceLevelCanvasDialog + resources.assets ChoiceLevelItem.
    public sealed class RecoveredChoiceLevelDialog : MonoBehaviour
    {
        private RecoveredUiTree tree;private RecoveredCatalogView owner;
        private readonly List<RecoveredUiTree> rows=new List<RecoveredUiTree>();
        private readonly List<SongItem> options=new List<SongItem>();
        private Text statusText;private int generation;private string statusKey;
        public int VariantCount=>options.Count;
        public bool IsOpen=>tree!=null;
        public RecoveredUiTree Tree=>tree;
        public void Close()
        {
            generation++;statusKey=null;statusText=null;
            if(owner!=null)owner.FavoritesChanged-=Refresh;
            if(tree!=null){tree.Root.SetActive(false);if(Application.isPlaying)Destroy(tree.Root);else DestroyImmediate(tree.Root);}
            tree=null;owner=null;rows.Clear();options.Clear();
        }
        public void Open(RecoveredFrontend host,RecoveredCatalogView catalog,SongItem parent)
        {
            Close();owner=catalog;
            var data=RecoveredDifficultyStyles.DialogData;
            tree=RecoveredUiBuilder.Build(data,Array.Find(data.scenes,s=>s.name=="ChoiceLevelCanvasDialog"),host.transform,host.PortraitCamera);
            var canvas=tree.Find("ChoiceLevelCanvasDialog").GetComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=900;
            tree.Find("SongText").GetComponent<Text>().text=parent.stage.title??"";
            tree.Find("ArtistText").GetComponent<Text>().text=parent.stage.artist??"";
            foreach(var name in new[]{"SongText","ArtistText"})
            {
                var label=tree.Find(name).GetComponent<Text>();label.supportRichText=false;
                label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;
                label.resizeTextForBestFit=true;label.resizeTextMinSize=18;
            }
            var close=tree.Find("CloseButton").GetComponent<Button>();close.interactable=true;close.onClick.AddListener(Close);
            AddUnique(parent);if(parent.more!=null)foreach(var option in parent.more)AddUnique(option);
            var content=tree.Find("Content").GetComponent<RectTransform>();
            var rowData=RecoveredDifficultyStyles.Data;var template=Array.Find(rowData.scenes,s=>s.name=="ChoiceLevelItem");
            foreach(var option in options)
            {
                var row=RecoveredUiBuilder.Build(rowData,template,content);
                RecoveredUiBuilder.UseAuthoredLayoutRoot(row,"ChoiceLevelItem",content);
                rows.Add(row);
            }
            owner.FavoritesChanged+=Refresh;Refresh();LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
        public void ShowBlocker(string message)
        {
            if(tree==null)return;
            if(statusText==null)
            {
                // Windows status area, separate from recovered song/artist and original variant rows.
                var panel=new GameObject("WindowsAccessStatus",typeof(RectTransform),typeof(Image));panel.transform.SetParent(tree.Find("ChoiceLevelCanvasDialog").transform,false);
                var rect=panel.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,24);rect.sizeDelta=new Vector2(760,145);
                var image=panel.GetComponent<Image>();image.color=new Color(0,0,0,.9f);image.raycastTarget=false;
                var label=new GameObject("StatusText",typeof(RectTransform),typeof(Text));label.transform.SetParent(panel.transform,false);
                var textRect=label.GetComponent<RectTransform>();textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;textRect.offsetMin=new Vector2(16,12);textRect.offsetMax=new Vector2(-16,-12);
                statusText=label.GetComponent<Text>();statusText.font=tree.Find("ArtistText").GetComponent<Text>().font;statusText.fontSize=25;statusText.color=Color.white;statusText.alignment=TextAnchor.MiddleCenter;statusText.raycastTarget=false;statusText.supportRichText=false;
                statusText.horizontalOverflow=HorizontalWrapMode.Wrap;statusText.verticalOverflow=VerticalWrapMode.Truncate;
            }
            statusText.text=message??"";statusText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
        public Action<string> StatusFor(string selectedKey)
        {
            if(statusKey!=selectedKey){statusKey=selectedKey;generation++;}int current=generation;ShowBlocker("");
            return message=>{if(tree!=null&&generation==current&&statusKey==selectedKey)ShowBlocker(message);};
        }
        private void Refresh(){for(int i=0;i<rows.Count;i++)owner.Populate(rows[i],options[i],true);}
        private void AddUnique(SongItem item){if(item==null||item.stage==null||options.Exists(i=>i.stage.Key==item.stage.Key))return;options.Add(item);}
    }
}
