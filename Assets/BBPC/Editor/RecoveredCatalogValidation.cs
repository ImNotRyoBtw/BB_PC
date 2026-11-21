using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using BBPC.UnityRuntime;
namespace BBPC.EditorTools
{
    public static class RecoveredCatalogValidation
    {
        [Serializable] private sealed class Receipt {public bool success;public int checks,main,variants,filters,stories,chapters,beginAnimations,endAnimations,maxPool;public string revision,error;}
        public static void Run(RecoveredFrontend frontend,string output)
        {
            Directory.CreateDirectory(output);var r=new Receipt{revision=RecoveredFrontend.Revision};Action<bool,string> check=(ok,message)=>{r.checks++;if(!ok)throw new InvalidOperationException(message);};
            try
            {
                var catalog=CatalogEnvelope.Parse(Resources.Load<TextAsset>("Catalog606/public-freeplay").text);var index=new RecoveredCatalogIndex(catalog);
                r.main=catalog.items.Length;r.variants=index.Entries.Length-r.main;r.filters=catalog.filters.Length;
                check(r.main==1196&&r.variants==2055&&r.filters==45,"Public Freeplay counts differ");
                check(index.Entries.Select(i=>i.stage.Key).Distinct().Count()==3251,"SID+AID uniqueness lost");
                var elastic=index.Entries.Single(i=>i.stage.sid=="gEYv"&&i.stage.aid=="9m6w");check(elastic.stage.game_config.asset.md5==RecoveredRoleAssets.Source.stage.bundleMd5,"Elastic exact bundle descriptor differs");
                check(index.Query("Elastic Destruction").Contains(elastic),"Keyword/title search misses exact Elastic");
                foreach(var filter in catalog.filters.Where(f=>f.type==1))check(index.Query(genre:filter.genre).All(i=>i.stage.genre==filter.genre||Array.IndexOf(i.stage.genres,filter.genre)>=0),"Genre filter leaked unrelated entry");
                for(int d=1;d<=8;d++)check(index.Query(difficulty:d).All(i=>i.stage.difficulty_v2==d),"Difficulty filter differs");
                check(index.Query("missing-content-fixture-606").Length==0,"Empty search fabricated a match");
                var favorites=new System.Collections.Generic.HashSet<string>{elastic.stage.Key};check(index.Query(favorites:favorites).Single()==elastic,"Favorites not keyed by SID+AID");
                var stories=StoryCatalog.ParseEnvelope(Resources.Load<TextAsset>("Catalog606/public-story").text);r.stories=stories.stories.Length;r.chapters=stories.stories.Sum(s=>s.chapters.Length);r.beginAnimations=stories.stories.Sum(s=>s.chapters.Count(c=>c.begin_animation!=null));r.endAnimations=stories.stories.Sum(s=>s.chapters.Count(c=>c.end_animation!=null));
                check(r.stories==42&&r.chapters==125&&r.beginAnimations==102&&r.endAnimations==9,"Story snapshot counts differ");
                var keys=new System.Collections.Generic.HashSet<string>(index.Entries.Select(i=>i.stage.Key));foreach(var chapter in stories.stories.SelectMany(s=>s.chapters))check(keys.Contains(chapter.stage_info.stage.Key),"Story chapter SID+AID missing from Freeplay");
                frontend.catalogSnapshot=Resources.Load<TextAsset>("Catalog606/public-freeplay");frontend.catalogOrigin=CatalogOrigin.PublicOffline606;frontend.ShowCatalog();var view=frontend.FullCatalog;
                check(frontend.CatalogCount==1197&&view.Index.Entries.Length==3252,"Snapshot not connected to actual frontend");
                foreach(var size in new[]{new[]{900,1600},new[]{1600,900},new[]{414,600}})
                {
                    frontend.ConfigureViewport(size[0],size[1]);Canvas.ForceUpdateCanvases();view.Filter("Elastic Destruction");check(view.MatchCount>0&&view.MatchCount<20,"UI search did not apply");
                    frontend.CaptureCurrentView(Path.Combine(output,"elastic-search-"+size[0]+"x"+size[1]+".png"),size[0],size[1]);
                }
                view.Filter("");r.maxPool=view.PoolCount;check(view.PoolCount<=16,"Full catalog instantiated an unbounded cell list");
                var scroll=frontend.MainRoom.Find("SonglistScrollView").GetComponent<ScrollRect>();scroll.verticalNormalizedPosition=0;scroll.onValueChanged.Invoke(Vector2.zero);check(view.PoolCount<=16,"Scrolling exceeded pool bound");
                var header=frontend.MainRoom.Find("HomePageTopView");check(header!=null,"Freeplay header missing");
                scroll.content.anchoredPosition=new Vector2(0,450);scroll.onValueChanged.Invoke(Vector2.zero);Canvas.ForceUpdateCanvases();
                check(header.activeSelf&&Math.Abs(view.HeaderVisibleHeight-100)<.1,"Original filter strip not retained");
                var vp=new Vector3[4];var filterCorners=new Vector3[4];scroll.viewport.GetWorldCorners(vp);frontend.MainRoom.Find("FilterContainer").GetComponent<RectTransform>().GetWorldCorners(filterCorners);
                check(vp[2].y<=filterCorners[0].y+.001f,"Rows mask extends over filter strip");frontend.CaptureCurrentView(Path.Combine(output,"catalog-scrolled-header.png"),900,1600);
                var parentWithVariants=Array.Find(catalog.items,i=>i!=null&&i.more!=null&&i.more.Length>0);check(parentWithVariants!=null,"Catalog has no variant parent");view.Select(parentWithVariants,false);var dialog=frontend.GetComponent<RecoveredChoiceLevelDialog>();check(dialog!=null&&dialog.IsOpen&&dialog.VariantCount>1,"ChoiceLevel selector not opened");frontend.CaptureCurrentView(Path.Combine(output,"choice-level-dialog.png"),900,1600);dialog.Close();
                frontend.CaptureCurrentView(Path.Combine(output,"catalog-last-page.png"),900,1600);view.Filter("");
                frontend.ShowStoryCatalog();check(frontend.Stories.Catalog.stories.Length==42&&frontend.Stories.PoolCount<=16,"Story original cell list not connected");
                var firstStory=frontend.Stories.Catalog.stories[0];frontend.Stories.Select(firstStory,1);
                check(frontend.Stories.SelectedChapter==firstStory.chapters[1]&&frontend.Selection.Song.Key==firstStory.chapters[1].stage_info.stage.Key&&frontend.Phase==FrontendPhase.MainRoom,"Story selection lost context or entered unprepared gameplay");frontend.DismissNotice();
                frontend.CaptureCurrentView(Path.Combine(output,"story-9x16.png"),900,1600);frontend.CaptureCurrentView(Path.Combine(output,"story-16x9.png"),1600,900);
                frontend.ShowCatalog();r.success=true;
            }
            catch(Exception e){r.error=e.ToString();throw;}
            finally{File.WriteAllText(Path.Combine(output,"unity-catalog-results.json"),JsonUtility.ToJson(r,true));}
        }
    }
}
