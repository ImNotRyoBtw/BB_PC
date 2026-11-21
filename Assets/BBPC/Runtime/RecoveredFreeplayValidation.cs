using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    // Runs inside the Windows Player. Pointer events are synthetic and raycast-checked, never claimed as physical input.
    public static class RecoveredFreeplayValidation
    {
        [Serializable] public sealed class Sample {public int width,height,pool;public float offset,header,viewportTop,filterBottom;public string capture;}
        [Serializable] public sealed class Result {public bool success,nightmareStyleFixture,nativeVariantTitles,marqueeMotion,marqueeLoop;public int nightmareCatalogRecords;public int checks,captures,pointerClicks;public string revision,error;public List<string> wheel=new List<string>();public List<Sample> scroll=new List<Sample>();}
        private static void Check(Result r,bool ok,string message){r.checks++;if(!ok)throw new InvalidOperationException(message);}
        private static void Click(RecoveredFrontend host,Button button,Result r)
        {
            Canvas.ForceUpdateCanvases();var corners=new Vector3[4];button.targetGraphic.rectTransform.GetWorldCorners(corners);
            var center=RectTransformUtility.WorldToScreenPoint(host.PortraitCamera,(corners[0]+corners[2])*.5f);
            var pointer=new PointerEventData(EventSystem.current){position=center,button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Check(r,hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"Freeplay pointer obstructed: "+button.name+" top="+(hits.Count==0?"none":hits[0].gameObject.name));
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);r.pointerClicks++;
        }
        private static void Capture(RecoveredFrontend host,string output,string name,Result r)
        {host.CaptureCurrentView(Path.Combine(output,name+".png"),Screen.width,Screen.height);r.captures++;}
        public static IEnumerator Run(RecoveredFrontend host,string output,Result r)
        {
            Directory.CreateDirectory(output);r.revision=RecoveredFrontend.Revision;var steps=Steps(host,output,r);
            while(true)
            {
                bool next;try{next=steps.MoveNext();}catch(Exception e){r.error=e.ToString();Debug.LogException(e);break;}
                if(!next){r.success=true;break;}yield return steps.Current;
            }
            host.GetComponent<RecoveredChoiceLevelDialog>()?.Close();
            File.WriteAllText(Path.Combine(output,"freeplay-results.json"),JsonUtility.ToJson(r,true));
        }
        private static IEnumerator Steps(RecoveredFrontend host,string output,Result r)
        {
            var character=host.Navigation.Pages[MainPage.Character];
            var activeBefore=character.Objects.Values.ToDictionary(go=>go,go=>go.activeSelf);
            var textsBefore=character.Root.GetComponentsInChildren<Text>(true).ToDictionary(text=>text,text=>new[]{text.text,text.supportRichText?"1":"0"});
            RecoveredFrontend.ApplyProfileName(character,"FIXTURE <b>literal</b>");
            var nameText=character.Root.GetComponentsInChildren<Text>(true).First(t=>t.name=="NameText");
            Check(r,nameText.text=="FIXTURE <b>literal</b>"&&!nameText.supportRichText,"Verified name must bind original NameText without markup interpretation");
            Check(r,character.Objects.Values.Where(go=>go.name=="GuildParent").All(go=>!go.activeSelf),"Authoring guild sample exposed as a real profile");
            foreach(var pair in textsBefore){pair.Key.text=pair.Value[0];pair.Key.supportRichText=pair.Value[1]=="1";}
            foreach(var pair in activeBefore)pair.Key.SetActive(pair.Value);
            foreach(var iconName in new[]{"progressG","progressO"}){var icon=Resources.Load<Texture2D>("RoleStages606/"+iconName);Check(r,icon!=null&&icon.width==100&&icon.height==100&&icon.mipmapCount==1&&icon.wrapMode==TextureWrapMode.Clamp,"Original progress icon import: "+iconName);}
            host.catalogOrigin=CatalogOrigin.PublicOffline606;host.catalogSnapshot=Resources.Load<TextAsset>("Catalog606/public-freeplay");host.ShowMainRoom();host.ShowCatalog();
            var view=host.FullCatalog;var list=host.MainRoom.Find("SonglistScrollView").GetComponent<ScrollRect>();
            Check(r,view.Index.MainEntries.Length==1197&&view.Index.Entries.Length==3252,"Full catalog + verified tutorial counts");
            var area=Screen.mainWindowDisplayInfo.workArea;int h=Math.Min(960,area.height-64);
            foreach(var size in new[]{new[]{h*9/16,h},new[]{Math.Min(960,area.width-32),Math.Min(960,area.width-32)*9/16}})
            {
                Screen.SetResolution(size[0],size[1],FullScreenMode.Windowed);yield return null;yield return null;
                view.Filter("");yield return null;
                float end=Mathf.Max(0,list.content.rect.height-list.viewport.rect.height);
                foreach(float offset in new[]{0f,120f,450f,2000f,end,450f,0f})
                {
                    list.StopMovement();list.content.anchoredPosition=new Vector2(0,offset);list.onValueChanged.Invoke(Vector2.zero);yield return null;
                    Canvas.ForceUpdateCanvases();var vp=new Vector3[4];var filter=new Vector3[4];list.viewport.GetWorldCorners(vp);host.MainRoom.Find("FilterContainer").GetComponent<RectTransform>().GetWorldCorners(filter);
                    Check(r,vp[2].y<=filter[0].y+.001f,"Rows viewport overlaps original filter/header");
                    Check(r,view.PoolCount<=16&&view.HeaderVisibleHeight>=99,"Pool bound or sticky filter lost");
                    if(offset==0)Check(r,Math.Abs(view.HeaderVisibleHeight-550)<.1,"Header failed to restore");
                    string name="scroll-"+size[0]+"x"+size[1]+"-"+r.scroll.Count;
                    r.scroll.Add(new Sample{width=Screen.width,height=Screen.height,offset=list.content.anchoredPosition.y,header=view.HeaderVisibleHeight,pool=view.PoolCount,viewportTop=vp[2].y,filterBottom=filter[0].y,capture=name+".png"});Capture(host,output,name,r);
                }
            }
            Screen.SetResolution(h*9/16,h,FullScreenMode.Windowed);yield return null;yield return null;
            // Exercise filter UI and original selector through EventSystem raycasts.
            view.Filter("");yield return null;
            Click(host,host.MainRoom.Objects["level3:74"].GetComponent<Button>(),r);yield return null;
            Check(r,view.DifficultyTree!=null,"Original DifficultySelector did not open");
            Check(r,view.DifficultyTree.Find("Content").transform.childCount==7,"Omega must not acquire invented selector art");
            var cells=view.DifficultyTree.Find("Content").GetComponentsInChildren<Button>();
            var ultimate=cells.Single(b=>b.GetComponentInParent<Transform>().parent.GetComponentsInChildren<Text>().Any(t=>t.name=="RankNameText"&&t.text==RecoveredDifficultyStyles.Label(6)));
            Click(host,ultimate,r);Capture(host,output,"difficulty-selector-ultimate",r);
            Click(host,view.DifficultyTree.Find("SaveButton").GetComponent<Button>(),r);yield return null;
            Check(r,view.SelectedDifficulty==6&&view.MatchCount==135,"Ultimate main cards / legacy split");
            Check(r,host.MainRoom.Find("DiffSelected").activeInHierarchy&&Math.Abs(view.HeaderVisibleHeight-650)<.1,"Selected difficulty strip missing or overlaps rows");Capture(host,output,"ultimate-main-cards",r);
            Click(host,host.MainRoom.Objects["level3:78"].GetComponent<Button>(),r);yield return null;Check(r,view.SelectedDifficulty==0,"Original DiffSelected close did not clear filter");
            Click(host,host.MainRoom.Objects["level3:74"].GetComponent<Button>(),r);yield return null;
            Click(host,view.DifficultyTree.Find("ClearButton").GetComponent<Button>(),r);
            Click(host,view.DifficultyTree.Find("SaveButton").GetComponent<Button>(),r);yield return null;
            Check(r,view.SelectedDifficulty==0&&view.MatchCount==1197,"Difficulty Clear lost grouped catalog");
            Check(r,!host.MainRoom.Find("DiffSelected").activeSelf,"Clear did not hide selected difficulty strip");
            var newButton=host.MainRoom.Find("FilterEnhancedScroller").GetComponentsInChildren<Button>().Single(b=>b.GetComponentsInChildren<Text>().Any(t=>t.text=="Nueva"));
            Click(host,newButton,r);yield return null;Check(r,view.MatchCount==15,"NEW id13/type2/tag1");Capture(host,output,"new-main-cards",r);
            view.Filter(""); // Filter text does not reset the category; reopen returns to ALL.
            host.ShowCatalog();view=host.FullCatalog;yield return null;
            var parent=view.Index.MainEntries.First(i=>i.more!=null&&i.more.Length>0&&i.stage.title=="Meltdown");
            view.Select(parent,false);yield return null;
            var dialog=host.GetComponent<RecoveredChoiceLevelDialog>();Check(r,dialog.IsOpen&&dialog.VariantCount==parent.more.Length+1,"Original variant dialog grouping");
            Check(r,dialog.Tree.Find("Content").GetComponentsInChildren<Text>().Any(t=>t.name=="VersionText"),"Original ChoiceLevelItem was not used");
            Capture(host,output,"meltdown-original-dialog",r);host.CaptureCurrentView(Path.Combine(output,"meltdown-original-dialog-16x9.png"),960,540);r.captures++;
            var cover=dialog.Tree.Find("Content").GetComponentsInChildren<RecoveredCoverBinding>().First();
            var coverImage=cover.GetComponentInChildren<RawImage>();
            Check(r,coverImage!=null&&coverImage.enabled&&coverImage.texture!=null,"Public HTTP200 cover sample not connected offline");
            Check(r,view.Covers.NetworkAttempts==0,"Offline Player unexpectedly contacted CDN");
            var song=parent.stage;bool favorite=view.IsFavorite(song);var buttons=dialog.Tree.Find("Content").GetComponentsInChildren<Button>();
            var favoriteButton=buttons.First(b=>b.name=="FavoritesButton");Click(host,favoriteButton,r);yield return null;
            Check(r,view.IsFavorite(song)!=favorite,"Variant favorite did not toggle");Capture(host,output,"favorite-toggled",r);
            dialog.Close();host.ShowCatalog();view=host.FullCatalog;yield return null;
            Check(r,view.IsFavorite(song)!=favorite,"Favorite did not persist across reconstruction");view.ToggleFavorite(song);Check(r,view.IsFavorite(song)==favorite,"Test failed to restore favorite state");host.DismissNotice();
            Check(r,host.Services.ReadAttempts.Count==0,"Offline favorite unexpectedly contacted a service");
            var neo=view.Index.MainEntries.Single(i=>i.stage.sid=="9kZX"&&i.stage.aid=="MzmB");view.Select(neo,false);yield return null;
            dialog=host.GetComponent<RecoveredChoiceLevelDialog>();var neoTitles=dialog.Tree.Find("Content").GetComponentsInChildren<Text>().Where(t=>t.name=="VersionText").Select(t=>t.GetComponent<RecoveredMarqueeText>()?.OriginalText??t.text).ToArray();
            Check(r,neoTitles.SequenceEqual(new[]{neo.stage.title}.Concat(neo.more.Select(i=>i.stage.title))),"ChoiceLevelItem must bind native Title +0x30, not version_title");r.nativeVariantTitles=true;
            var neoCovers=dialog.Tree.Find("Content").GetComponentsInChildren<RecoveredCoverBinding>();
            Check(r,neoCovers.Length==3&&neoCovers.All(c=>c.GetComponentInChildren<RawImage>()?.texture!=null),"Authentic Neo covers did not load offline");
            var originalTextures=neoCovers.Select(c=>c.GetComponentInChildren<RawImage>().texture).ToArray();
            dialog.Tree.Root.SetActive(false);yield return null;dialog.Tree.Root.SetActive(true);yield return null;yield return null;
            Check(r,neoCovers.Select((c,i)=>c.GetComponentInChildren<RawImage>()?.texture==originalTextures[i]).All(ok=>ok),"Covers lost after disable/enable without row-index change");
            Capture(host,output,"meltdown-neo-original-dialog",r);host.CaptureCurrentView(Path.Combine(output,"meltdown-neo-original-dialog-16x9.png"),960,540);r.captures++;
            var marquee=dialog.Tree.Find("Content").GetComponentsInChildren<RecoveredMarqueeText>().First(m=>m.CycleWidth>0);
            var moving=marquee.GetComponent<RectTransform>();float previousX=moving.localPosition.x;yield return new WaitForSeconds(2.5f);
            Check(r,moving.localPosition.x<previousX-10&&moving.sizeDelta==new Vector2(1500,84),"Original marquee did not move with recovered speed/size");r.marqueeMotion=true;
            Capture(host,output,"meltdown-neo-marquee-motion",r);float phaseX=moving.localPosition.x;marquee.Advance(marquee.CycleWidth/50f);Check(r,Math.Abs(moving.localPosition.x-phaseX)<.1,"Marquee linear restart differs");r.marqueeLoop=true;
            var chooseNeo=dialog.Tree.Find("Content").GetComponentsInChildren<Button>().First(b=>b.name=="VersionText");Click(host,chooseNeo,r);yield return null;
            Check(r,host.Selection.Song.Key==neo.stage.Key&&host.Phase==FrontendPhase.MainRoom,"Unverified Neo selection entered gameplay or lost SID+AID");
            Check(r,dialog.Tree.Find("Content").GetComponentsInChildren<Button>().Where(b=>b.name=="PlayButton").All(b=>!b.interactable),"Unverified Neo variant Play enabled");
            Click(host,dialog.Tree.Find("CloseButton").GetComponent<Button>(),r);yield return null;
            // Check every recovered row style, including recycled rows. Original levels 1..7 have null controllers.
            for(int level=1;level<=6;level++)
            {
                var sample=view.Index.MainEntries.First(i=>i.stage.CurrentDifficulty==level);view.Filter(sample.stage.title,0,level);yield return null;
                var active=list.content.GetComponentsInChildren<Text>().Where(t=>t.name=="DifficultyText"&&t.enabled).ToArray();
                Check(r,active.Length>0&&active.All(t=>t.text==RecoveredDifficultyStyles.Label(level)),"Difficulty label leakage after recycle: "+level);
                RecoveredDifficultyStyle style;Check(r,RecoveredDifficultyStyles.TryGet(level,out style),"Missing original style");
                Check(r,list.content.GetComponentsInChildren<Image>().Where(i=>i.name=="BorderImage"&&i.enabled).All(i=>i.sprite==RecoveredUiBuilder.Asset<Sprite>(RecoveredDifficultyStyles.Data,style.resourceId)),"Wrong difficulty sprite: "+level);
                Capture(host,output,"row-difficulty-"+level,r);
            }
            r.nightmareCatalogRecords=view.Index.Entries.Count(i=>i.stage.CurrentDifficulty==7);
            Check(r,r.nightmareCatalogRecords==0,"Expected snapshot domain changed; revisit Nightmare fixture");
            view.Filter("bbpc-style-fixture-no-catalog-match");yield return null;
            host.MainRoom.Find("NoSearchResultCellView")?.SetActive(false);host.MainRoom.Find("NoFavorite")?.SetActive(false);
            foreach(var name in new[]{"NotFound","TryNewSongs"})foreach(var go in host.MainRoom.Objects.Values)if(go.name==name)go.SetActive(false);
            var fixture=RecoveredUiBuilder.Build(host.Data,host.Data.row,list.content);var fixtureRect=fixture.Objects[host.Data.row.objects[0].id].GetComponent<RectTransform>();fixtureRect.anchorMin=new Vector2(0,1);fixtureRect.anchorMax=new Vector2(1,1);fixtureRect.pivot=new Vector2(.5f,1);fixtureRect.sizeDelta=new Vector2(0,236);fixtureRect.anchoredPosition=Vector2.zero;
            fixture.Find("SongText").GetComponent<Text>().text="PRUEBA DE ESTILO: NIGHTMARE";fixture.Find("ArtistText").GetComponent<Text>().text="Sin registros Nightmare en este snapshot";
            foreach(var button in fixture.Root.GetComponentsInChildren<Button>())button.interactable=false;
            foreach(var name in new[]{"NewTag","HotTag","Stars","ADButton"})fixture.Find(name)?.SetActive(false);
            RecoveredDifficultyStyles.Apply(host.Data,fixture,new SongMetadata{difficulty_v2=7});yield return null;
            Check(r,fixture.Find("BorderImage").GetComponent<Image>().sprite==RecoveredUiBuilder.Asset<Sprite>(RecoveredDifficultyStyles.Data,"resources.assets:1640"),"Native Nightmare sprite missing");
            Capture(host,output,"row-difficulty-7-style-fixture",r);r.nightmareStyleFixture=true;fixture.Root.SetActive(false);UnityEngine.Object.Destroy(fixture.Root);
            RecoveredDifficultyStyle deferred;Check(r,!RecoveredDifficultyStyles.TryGet(8,out deferred),"Omega visual invented");
            foreach(var size in new[]{new[]{537,956},new[]{960,540}})
            {
                Screen.SetResolution(size[0],size[1],FullScreenMode.Windowed);yield return null;yield return null;
                for(int cycle=0;cycle<3;cycle++)
                {
                    host.ShowCatalog();yield return null;yield return null;
                    Click(host,host.MainRoom.Find("StoryModeButton").GetComponent<Button>(),r);yield return null;yield return null;
                    Check(r,host.Stories.Catalog!=null&&host.MainRoom.Find("FreePlayButton").activeInHierarchy,"Story did not expose original Freeplay return button");
                    Check(r,!host.MainRoom.Find("FiltersContainer").activeInHierarchy,"Story retained Freeplay filters");
                    host.ApplyCatalogArrival(view.Index.Catalog);yield return null;
                    Check(r,host.Stories.Catalog!=null&&host.MainRoom.Find("FreePlayButton").activeInHierarchy,"Late catalog completion navigated away from Story");
                    Capture(host,output,"story-before-wheel-"+size[0]+"-"+cycle,r);Wheel(host,list,-1600,r);yield return null;yield return null;
                    Capture(host,output,"story-after-wheel-"+size[0]+"-"+cycle,r);Check(r,list.content.anchoredPosition.y>0&&host.Stories.HeaderVisibleHeight<550,"Story header does not move with wheel; offset="+list.content.anchoredPosition.y+" header="+host.Stories.HeaderVisibleHeight);
                    var up=host.MainRoom.Find("PageUpButton").GetComponent<Button>();Check(r,up.gameObject.activeInHierarchy&&up.interactable,"Story cannot return to top");
                    Capture(host,output,"story-scroll-"+size[0]+"-"+cycle,r);Click(host,up,r);yield return null;yield return null;
                    Check(r,Math.Abs(host.Stories.HeaderVisibleHeight-550)<.1,"Story baseline not restored");
                    Click(host,host.MainRoom.Find("FreePlayButton").GetComponent<Button>(),r);yield return null;yield return null;
                    Check(r,host.Stories.Catalog==null&&host.MainRoom.Find("FiltersContainer").activeInHierarchy&&!host.MainRoom.Find("InputField").activeInHierarchy&&host.MainRoom.Find("StoryModeButton").activeInHierarchy,"Story to Freeplay transition broken or search overlays categories");
                    Check(r,Math.Abs(host.FullCatalog.HeaderVisibleHeight-550)<.1,"Header baseline drift after Story/Freeplay");
                    var categories=host.MainRoom.Find("FilterEnhancedScroller").GetComponentsInChildren<Button>();
                    Click(host,categories.Single(b=>b.GetComponentsInChildren<Text>().Any(t=>t.text=="Nueva")),r);yield return null;
                    Check(r,host.FullCatalog.MatchCount==15,"NEW filter unusable after Story return");
                    Click(host,categories[0],r);yield return null;Check(r,host.FullCatalog.MatchCount==1197,"ALL filter unusable after Story return");
                    Wheel(host,list,-1600,r);yield return null;yield return null;Check(r,host.FullCatalog.HeaderVisibleHeight<=100.1,"Freeplay wheel did not collapse header");
                    Click(host,host.MainRoom.Find("PageUpButton").GetComponent<Button>(),r);yield return null;yield return null;
                    Capture(host,output,"freeplay-return-"+size[0]+"-"+cycle,r);
                }
            }
            host.FullCatalog.Filter("Elastic Destruction");yield return null;Capture(host,output,"elastic-search",r);
            host.ShowCatalog();
        }
        private static void Wheel(RecoveredFrontend host,ScrollRect scroll,float delta,Result r)
        {
            Canvas.ForceUpdateCanvases();var corners=new Vector3[4];scroll.viewport.GetWorldCorners(corners);var position=RectTransformUtility.WorldToScreenPoint(host.PortraitCamera,(corners[0]+corners[2])*.5f);
            var pointer=new PointerEventData(EventSystem.current){position=position,scrollDelta=new Vector2(0,delta)};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            var handler=hits.Count>0?ExecuteEvents.GetEventHandler<IScrollHandler>(hits[0].gameObject):null;
            r.wheel.Add("top="+(hits.Count>0?hits[0].gameObject.name:"none")+" handler="+(handler!=null?handler.name:"none")+" before="+scroll.content.anchoredPosition.y);
            Check(r,handler==scroll.gameObject||handler!=null&&handler.GetComponent<RecoveredNestedScrollRect>()!=null,"Wheel intercepted by a different scroll area");
            ExecuteEvents.Execute(handler,pointer,ExecuteEvents.scrollHandler);r.wheel.Add("after="+scroll.content.anchoredPosition.y+" content="+scroll.content.rect.height+" viewport="+scroll.viewport.rect.height);
        }
    }
}
