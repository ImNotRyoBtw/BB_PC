using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    // Story keeps its own models/chapters and original StoryCellView; it is not a flattened song list.
    public sealed class RecoveredStoryView : MonoBehaviour
    {
        public StoryCatalog Catalog {get;private set;}public StoryModel SelectedStory {get;private set;}public ChapterModel SelectedChapter {get;private set;}
        private RecoveredFrontend frontend;private ScrollRect scroll;private RecoveredUiData data;private readonly List<RecoveredUiTree> rows=new List<RecoveredUiTree>();private int first=-1;private RecoveredCatalogLayout layout;private RecoveredCoverCache covers;
        public float HeaderVisibleHeight=>layout?.VisibleHeight??0;
        public int PoolCount=>rows.Count;
        public void Open(RecoveredFrontend host)
        {
            Close();frontend=host;Catalog=StoryCatalog.ParseEnvelope(Resources.Load<TextAsset>("Catalog606/public-story").text);data=JsonUtility.FromJson<RecoveredUiData>(Resources.Load<TextAsset>("CatalogUi606/story-ui").text);
            scroll=host.MainRoom.Find("SonglistScrollView").GetComponent<ScrollRect>();
            for(int i=scroll.content.childCount-1;i>=0;i--){var child=scroll.content.GetChild(i);child.gameObject.SetActive(false);if(Application.isPlaying)Destroy(child.gameObject);else DestroyImmediate(child.gameObject);}rows.Clear();
            covers=host.GetComponent<RecoveredCoverCache>()??host.gameObject.AddComponent<RecoveredCoverCache>();
            layout=new RecoveredCatalogLayout(host,0);scroll.content.anchoredPosition=Vector2.zero;
            scroll.onValueChanged.RemoveListener(Scrolled);scroll.onValueChanged.AddListener(Scrolled);first=-1;
            foreach(var name in new[]{"InputField","FiltersContainer","NoSearchResultCellView","NoFavorite","DiffSelected"})host.MainRoom.Find(name)?.SetActive(false);
            host.MainRoom.Objects["level3:56"].SetActive(false);
            Refresh();
        }
        public void Close()
        {
            if(scroll!=null)scroll.onValueChanged.RemoveListener(Scrolled);layout?.Restore();layout=null;
            foreach(var row in rows)if(row.Root!=null)row.Root.SetActive(false);Catalog=null;rows.Clear();
            if(frontend!=null){frontend.MainRoom.Find("InputField")?.SetActive(false);frontend.MainRoom.Find("FiltersContainer")?.SetActive(true);}
        }
        private void Scrolled(Vector2 value){Refresh();}
        private void LateUpdate(){if(Catalog!=null&&frontend.MainRoom.Find("HomePage").activeInHierarchy)Refresh();}

        private void Refresh()
        {
            if(Catalog==null)return;
            layout.Refresh(Catalog.stories.Length,236);
            int count=Math.Min(Catalog.stories.Length,Math.Max(2,Mathf.CeilToInt((scroll.viewport?.rect.height??1792)/236)+3));bool resized=rows.Count<count;
            while(rows.Count<count)rows.Add(RecoveredUiBuilder.Build(data,data.row,scroll.content));
            int start=Math.Max(0,Math.Min(Catalog.stories.Length-count,Mathf.FloorToInt((scroll.content.anchoredPosition.y-layout.HeaderHeight)/236)));bool populate=first!=start||resized;first=start;
            for(int i=0;i<rows.Count;i++)
            {
                var row=rows[i];row.Root.SetActive(i<count);if(i>=count)continue;var story=Catalog.stories[start+i];
                var root=row.Find("StoryCellView").GetComponent<RectTransform>();root.anchorMin=new Vector2(0,1);root.anchorMax=Vector2.one;root.pivot=new Vector2(.5f,1);root.sizeDelta=new Vector2(0,236);root.anchoredPosition=new Vector2(0,-layout.HeaderHeight-(start+i)*236+layout.VisibleHeight);root.gameObject.SetActive(true);if(!populate)continue;
                row.Find("WeekText").GetComponent<Text>().text=story.title;
                foreach(var name in new[]{"ClearImage","Stars","ADButton","CoverIcon","DifficultyText","TagImage"})row.Find(name)?.SetActive(false);
                foreach(var go in row.Objects.Values)if(go.TryGetComponent<Button>(out var b)){b.onClick.RemoveAllListeners();b.interactable=false;}
                var binding=row.Root.GetComponent<RecoveredCoverBinding>()??row.Root.AddComponent<RecoveredCoverBinding>();binding.Bind(covers,story.cover,row.Find("CoverIcon")?.GetComponent<RawImage>(),row.Find("DefaultCoverImage"));
                var play=row.Find("PlayButton").GetComponent<Button>();play.interactable=true;play.onClick.AddListener(()=>Select(story,0));
                for(int c=0;c<4;c++)
                {
                    var text=row.Find("SongNameText"+(c+1)).GetComponent<Text>();bool exists=c<story.chapters.Length;text.gameObject.SetActive(exists);if(!exists)continue;
                    int chapterIndex=c;text.text=story.chapters[c].stage_info.stage.title;text.raycastTarget=true;var button=text.GetComponent<Button>()??text.gameObject.AddComponent<Button>();button.targetGraphic=text;button.interactable=true;button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>Select(story,chapterIndex));
                }
            }
        }
        public void Select(StoryModel story,int chapter)
        {
            if(story==null||chapter<0||chapter>=story.chapters.Length)throw new ArgumentOutOfRangeException(nameof(chapter));SelectedStory=story;SelectedChapter=story.chapters[chapter];
            frontend.Selection.Select(SelectedChapter.stage_info.stage,CatalogOrigin.PublicOffline606);
            frontend.ShowUnavailable(story.title+" · "+SelectedChapter.stage_info.stage.title+"\nStory: faltan stage/info y recursos verificados"+(SelectedChapter.begin_animation!=null?", cinemática inicial .bpma":"")+(SelectedChapter.end_animation!=null?", cinemática final .bpma":"")+". Restricciones de cuenta pendientes.");
        }
    }
}
