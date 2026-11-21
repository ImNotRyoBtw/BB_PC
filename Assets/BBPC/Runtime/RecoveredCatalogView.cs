using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    // Reuse SongCellView and ScrollRect; a bounded pool replaces EnhancedScroller on Windows.
    public sealed class RecoveredCatalogView : MonoBehaviour
    {
        private RecoveredCoverCache covers;
        public RecoveredCoverCache Covers=>covers;
        private RecoveredFrontend frontend;private ScrollRect scroll;private RecoveredCatalogIndex index;private SongItem[] visible;
        private readonly List<RecoveredUiTree> rows=new List<RecoveredUiTree>();private float height,header;private int first=-1;
        private RecoveredCatalogLayout layout;private float visibleHeader;private RecoveredUiTree selectedChip;private GameObject difficultyMenu;private RecoveredUiTree difficultyTree;
        public RecoveredUiTree DifficultyTree=>difficultyTree;
        public event Action FavoritesChanged;
        public bool IsFavorite(SongMetadata song)=>favorites.Contains(song.sid);
        public float HeaderVisibleHeight=>visibleHeader;
        public int SelectedDifficulty=>difficulty;
        private string query="";private int genre,difficulty;private bool favoritesOnly,newOnly;private readonly HashSet<string> favorites=new HashSet<string>();
        private readonly List<Action> filterHighlights=new List<Action>();private int selectedFilter;
        public int MatchCount=>visible?.Length??0;public int PoolCount=>rows.Count;
        public RecoveredCatalogIndex Index=>index;
        public void Close()
        {
            if(scroll!=null)scroll.onValueChanged.RemoveListener(Scrolled);
            GetComponent<RecoveredChoiceLevelDialog>()?.Close();CloseDifficultyMenu();
            frontend?.MainRoom?.Find("DiffSelected")?.SetActive(false);frontend?.MainRoom?.Find("PageUpButton")?.SetActive(false);
            layout?.Restore();layout=null;
            foreach(var row in rows)if(row.Root!=null)row.Root.SetActive(false);rows.Clear();visible=null;first=-1;
        }
        private void CloseDifficultyMenu(){if(difficultyMenu==null)return;difficultyMenu.SetActive(false);if(Application.isPlaying)Destroy(difficultyMenu);else DestroyImmediate(difficultyMenu);difficultyMenu=null;difficultyTree=null;}
        private static string FavoriteKey(SongMetadata song)=>"BBPC.Windows.Favorite606."+Uri.EscapeDataString(song.sid);
        private static string LegacyFavoriteKey(SongMetadata song)=>"BBPC.Windows.Favorite606."+Uri.EscapeDataString(song.Key);
        public void Open(RecoveredFrontend host,SongCatalog catalog)
        {
            Close();frontend=host;covers=host.GetComponent<RecoveredCoverCache>()??host.gameObject.AddComponent<RecoveredCoverCache>();covers.AllowRemote=Application.isPlaying&&Array.IndexOf(Environment.GetCommandLineArgs(),"-bbpc-access-check")<0&&Array.IndexOf(Environment.GetCommandLineArgs(),"-bbpc-cover-check")<0&&Array.IndexOf(Environment.GetCommandLineArgs(),"-bbpc-validate")<0&&Array.IndexOf(Environment.GetCommandLineArgs(),"-bbpc-online-check")<0&&Array.IndexOf(Environment.GetCommandLineArgs(),"-bbpc-account-probe")<0&&Array.IndexOf(Environment.GetCommandLineArgs(),"-bbpc-account-wealth-probe")<0&&Array.IndexOf(Environment.GetCommandLineArgs(),"-bbpc-stage-check")<0;index=new RecoveredCatalogIndex(catalog);scroll=host.MainRoom.Find("SonglistScrollView").GetComponent<ScrollRect>();
            height=Math.Abs(host.Data.row.objects[0].rect[7]);
            layout=new RecoveredCatalogLayout(host,host.MainRoom.Find("FilterContainer").GetComponent<RectTransform>().rect.height);header=layout.HeaderHeight;
            ApplyHeader(0);
            if(height<=0)throw new InvalidOperationException("Original SongCellView height invalid");
            for(int i=scroll.content.childCount-1;i>=0;i--){var child=scroll.content.GetChild(i);child.gameObject.SetActive(false);if(Application.isPlaying)Destroy(child.gameObject);else DestroyImmediate(child.gameObject);}rows.Clear();
            favorites.Clear();bool migrated=false;
            foreach(var group in index.Entries.GroupBy(i=>i.stage.sid))
            {
                var song=group.First().stage;int stored=RecoveredPreferences.GetInt(FavoriteKey(song),-1);
                if(stored<0&&group.Any(i=>RecoveredPreferences.GetInt(LegacyFavoriteKey(i.stage),0)==1)){stored=1;RecoveredPreferences.SetInt(FavoriteKey(song),1);migrated=true;}
                if(stored==1)favorites.Add(song.sid);
            }
            if(migrated)RecoveredPreferences.Save();
            scroll.onValueChanged.RemoveListener(Scrolled);scroll.onValueChanged.AddListener(Scrolled);
            BindSearch();BindFilters();host.MainRoom.Find("DiffSelected")?.SetActive(false);host.MainRoom.Find("LoadingIndicator")?.SetActive(false);
            newOnly=false;Filter("",0,0,false);
            // The server list wins over the local mirror when it was already read for this identity.
            if(host.Services!=null&&host.Services.FavoritesVerified)ApplyServerFavorites(host.Services.ServerFavorites);
        }
        public void Filter(string text,int selectedGenre=0,int selectedDifficulty=0,bool onlyFavorites=false)
        {
            query=text??"";genre=selectedGenre;difficulty=selectedDifficulty;favoritesOnly=onlyFavorites;
            RefreshSelectedDifficulty();
            visible=index.Query(query,genre,difficulty,favoritesOnly?favorites:null,newOnly)
                .OrderByDescending(i=>IsVerifiedLocal(i.stage)||(i.more!=null&&i.more.Any(v=>v?.stage!=null&&IsVerifiedLocal(v.stage)))).ToArray();
            scroll.StopMovement();scroll.content.anchoredPosition=Vector2.zero;scroll.content.sizeDelta=new Vector2(scroll.content.sizeDelta.x,header+height*visible.Length);ApplyHeader(0);
            first=-1;Refresh();frontend.MainRoom.Find("NoSearchResultCellView")?.SetActive(visible.Length==0);
            frontend.MainRoom.Find("NoFavorite")?.SetActive(favoritesOnly&&visible.Length==0);
        }
        private void RefreshSelectedDifficulty()
        {
            var selected=frontend.MainRoom.Find("DiffSelected");bool show=RecoveredDifficultyStyles.TryGet(difficulty,true,out _);selected.SetActive(show);
            float strip=show?Math.Abs(selected.GetComponent<RectTransform>().sizeDelta.y):0;layout.SetExtraStrip(strip);header=layout.HeaderHeight;
            if(!show)return;
            if(selectedChip==null)
            {
                var data=RecoveredDifficultyStyles.Data;selectedChip=RecoveredUiBuilder.Build(data,Array.Find(data.scenes,s=>s.name=="SelectorCell"),frontend.MainRoom.Objects["level3:76"].transform);
                RecoveredUiBuilder.UseAuthoredLayoutRoot(selectedChip,"SelectorCell",frontend.MainRoom.Objects["level3:76"].transform);
            }
            RecoveredDifficultyStyles.ApplySelector(selectedChip,difficulty,false);
            var clear=frontend.MainRoom.Objects["level3:78"].GetComponent<Button>();clear.interactable=true;clear.onClick.RemoveAllListeners();clear.onClick.AddListener(()=>Filter(query,genre,0,favoritesOnly));
        }
        private void Scrolled(Vector2 value){ApplyHeader(scroll==null?0:scroll.content.anchoredPosition.y);Refresh();}
        private void LateUpdate(){if(visible!=null&&frontend!=null&&frontend.MainRoom.Find("HomePage").activeInHierarchy){ApplyHeader(scroll==null?0:scroll.content.anchoredPosition.y);Refresh();}}
        private void ApplyHeader(float contentOffset)
        {
            if(layout==null)return;layout.Refresh(visible?.Length??0,height);visibleHeader=layout.VisibleHeight;
        }

        private void Refresh()
        {
            if(visible==null)return;
            float viewportHeight=scroll.viewport!=null?scroll.viewport.rect.height:1792;
            int count=Math.Min(visible.Length,Math.Max(2,Mathf.CeilToInt(viewportHeight/height)+3));
            bool resized=rows.Count<count;
            while(rows.Count<count)rows.Add(RecoveredUiBuilder.Build(frontend.Data,frontend.Data.row,scroll.content));
            int start=Math.Max(0,Math.Min(Math.Max(0,visible.Length-count),Mathf.FloorToInt((scroll.content.anchoredPosition.y-header)/height)));
            bool populate=start!=first||resized;first=start;
            for(int i=0;i<rows.Count;i++)
            {
                var row=rows[i];bool shown=i<count;row.Root.SetActive(shown);if(!shown)continue;
                var item=visible[start+i];var rt=row.Objects[frontend.Data.row.objects[0].id].GetComponent<RectTransform>();
                rt.anchorMin=new Vector2(0,1);rt.anchorMax=new Vector2(1,1);rt.pivot=new Vector2(.5f,1);rt.sizeDelta=new Vector2(0,height);rt.anchoredPosition=new Vector2(0,-header-height*(start+i)+visibleHeader);rt.gameObject.SetActive(true);
                if(populate)Populate(row,item);
            }
        }
        private static void Text(RecoveredUiTree row,string name,string value){var t=row.Find(name)?.GetComponent<Text>();if(t!=null){var marquee=t.GetComponent<RecoveredMarqueeText>();if(marquee!=null)marquee.SetText(value);else t.text=value??"";}}
        internal void Populate(RecoveredUiTree row,SongItem item,bool variant=false)
        {
            var song=item.stage;
            Text(row,variant?"VersionText":"SongText",song.title);Text(row,"ArtistText",song.artist);
            foreach(var go in row.Objects.Values)if(go.TryGetComponent<Button>(out var b)){b.onClick.RemoveAllListeners();b.interactable=false;}
            foreach(var name in new[]{"Stars","HotTag","CoverIcon"})row.Find(name)?.SetActive(false);
            row.Find("DefaultCoverImage")?.SetActive(true); // Binding hides the placeholder only after a validated image is ready.
            var newTag=row.Find("NewTag"); if(newTag!=null)newTag.SetActive(song.sub_mark!=null&&song.sub_mark.tag==1);
            RecoveredDifficultyStyles.Apply(frontend.Data,row,song);
            bool local=IsVerifiedLocal(song);var play=row.Find("PlayButton").GetComponent<Button>();
            bool variants=!variant&&item.more!=null&&item.more.Length>0;
            bool adGated=!variants&&IsAdGated(item);
            bool ready=!adGated&&(local||(frontend.catalogOrigin==CatalogOrigin.Verified606Snapshot&&frontend.Services.Catalog!=null&&frontend.Services.HasPersistedPair&&frontend.Services.TimeSynced));
            play.interactable=!variant||ready;
            var playText=play.GetComponentInChildren<Text>(true);if(playText!=null)playText.text=variants?"VERSIONES":"JUGAR";
            play.onClick.AddListener(()=>{if(variant)SelectLeaf(item,true);else Select(item,true);});
            var title=row.Find(variant?"VersionText":"SongText").GetComponent<Text>();title.raycastTarget=true;var choose=title.GetComponent<Button>()??title.gameObject.AddComponent<Button>();choose.targetGraphic=title;choose.interactable=true;choose.onClick.RemoveAllListeners();choose.onClick.AddListener(()=>{if(variant)SelectLeaf(item,false);else Select(item,false);});
            var favorite=row.Find("FavoritesButton")?.GetComponent<Button>();if(favorite!=null){favorite.interactable=true;favorite.onClick.AddListener(()=>ToggleFavorite(song));}
            var favoriteImage=row.Find("FavoriteImageImage")?.GetComponent<Image>();
            if(favoriteImage!=null){favoriteImage.sprite=RecoveredUiBuilder.Asset<Sprite>(frontend.Data,favorites.Contains(song.sid)?"resources.assets:1618":"resources.assets:1511");favoriteImage.enabled=true;favoriteImage.gameObject.SetActive(true);}
            var cover=row.Root.GetComponent<RecoveredCoverBinding>()??row.Root.AddComponent<RecoveredCoverBinding>();
            cover.Bind(covers,song.cover,row.Find("CoverIcon")?.GetComponent<RawImage>(),row.Find("DefaultCoverImage"));
            // Unverified leaf content never enables Play or enters Loading.
            // SongCellView.RefreshUI (0x286ca48) toggles playButton vs adButton: only locked ad-gated leaves (unlock_type==1, no unlock_status)
            // show the AD button — authored ad badge = Sprites/resources.assets_1683 ("ad", 72x72) + text key "play" -> "Jugar". Free/unlocked show JUGAR.
            play.gameObject.name="PlayButton";
            var adNode=row.Find("ADButton");
            play.gameObject.SetActive(!adGated);
            if(adNode!=null){adNode.SetActive(adGated);if(adGated){var ad=adNode.GetComponent<Button>();if(ad!=null){ad.interactable=true;ad.onClick.RemoveAllListeners();ad.onClick.AddListener(()=>{if(item.restriction!=null)item.restriction.unlock_status=true;Populate(row,item);});}}}
        }
        public static bool IsVerifiedLocal(SongMetadata song)=>SongSelection.HasVerifiedLocalContent(song);
        // SongCellView.Refresh (0x286C93C): playButton "JUGAR" iff isUnLocked == Restriction.unlock_status; RefreshUI toggles playButton(0xA0) vs adButton(0xA8).
        // StageModel.IsLocked (0x2CB90BC): locked == unlock_status==false && unlock_type==1 (ad-gated, not yet obtained).
        // So free songs (unlock_type==0) and server-unlocked songs (unlock_status==true, server-authoritative) show JUGAR; only locked ad-gated songs show OBTENER.
        internal static bool IsUnlocked(SongItem item)=>item?.restriction!=null&&(item.restriction.unlock_type==0||item.restriction.unlock_status==true);
        // SongCellView.RefreshUI (0x286ca48): locked ad-gated (unlock_type==1, no unlock_status) toggles
        // the adButton; the port marks it unlocked locally instead of playing the ad. Local, not remote.
        internal static bool IsAdGated(SongItem item)=>item?.restriction!=null&&item.restriction.unlock_type==1&&item.restriction.unlock_status!=true;
        public void Select(SongItem item,bool prepare)
        {
            if(item==null||item.stage==null)return;
            if(item.more!=null&&item.more.Length>0){OpenVariantDialog(item);return;}
            SelectLeaf(item,prepare);
        }
        public void SelectStage(SongMetadata song,bool prepare)
        { if(song!=null)SelectLeaf(new SongItem{stage=song},prepare); }
        private void SelectLeaf(SongItem item,bool prepare)
        {
            bool local=IsVerifiedLocal(item.stage);frontend.Selection.Select(item.stage,local?CatalogOrigin.ValidatedLocal606:frontend.catalogOrigin);
            if(prepare&&local){GetComponent<RecoveredChoiceLevelDialog>()?.Close();frontend.TryPrepareSelectedSong();return;}
            if(!local&&frontend.catalogOrigin==CatalogOrigin.Verified606Snapshot)
            {
                var current=GetComponent<RecoveredChoiceLevelDialog>();
                Action<string> report=current!=null&&current.IsOpen?current.StatusFor(item.stage.Key):(Action<string>)(message=>{if(frontend.Selection.Song?.Key==item.stage.Key)frontend.ShowUnavailable(message);});
                frontend.ReadSelectedStageInfo(item.stage,report,prepare);return;
            }
            string restriction="No se ha verificado el acceso de esta instalación a este contenido.";
            var dialog=GetComponent<RecoveredChoiceLevelDialog>();if(dialog!=null&&dialog.IsOpen){dialog.ShowBlocker(local?"Audio/chart local verificado.":"Contenido pendiente: recursos y acceso de esta instalación sin verificar.");return;}
            frontend.ShowUnavailable(item.stage.title+" · "+item.stage.version_title+"\n"+(local?"Audio/chart local verificado.":restriction+" "+frontend.Selection.PreparationBlocker()));
        }
        private void OpenVariantDialog(SongItem item)
        {
            var dialog=GetComponent<RecoveredChoiceLevelDialog>()??gameObject.AddComponent<RecoveredChoiceLevelDialog>();dialog.Open(frontend,this,item);
        }
        // OBSERVED (user LogF.txt): the server owns the favorite list; the Windows mirror is replaced by it.
        public void ApplyServerFavorites(string[] sids)
        {
            if(sids==null)return;
            favorites.Clear();
            foreach(var sid in sids)if(!string.IsNullOrEmpty(sid))favorites.Add(sid);
            if(index!=null)
            {
                foreach(var group in index.Entries.GroupBy(i=>i.stage.sid))
                {
                    var song=group.First().stage;
                    RecoveredPreferences.SetInt(FavoriteKey(song),favorites.Contains(song.sid)?1:0);
                }
                RecoveredPreferences.Save();
                if(favoritesOnly)Filter(query,genre,difficulty,true);else{first=-1;Refresh();}
            }
            FavoritesChanged?.Invoke();
        }
        public void ToggleFavorite(SongMetadata song)
        {
            bool add=!favorites.Contains(song.sid);
            if(add)favorites.Add(song.sid);else favorites.Remove(song.sid);
            RecoveredPreferences.SetInt(FavoriteKey(song),add?1:0);RecoveredPreferences.Save();
            if(favoritesOnly)Filter(query,genre,difficulty,true);else{first=-1;Refresh();}FavoritesChanged?.Invoke();
            var services=frontend?.Services;
            if(services!=null&&services.FavoritesVerified)
            {
                // Best-effort remote sync. A failed write never withdraws the local favorite.
                frontend.StartCoroutine(services.SetFavorite(song.sid,add,ok=>
                {
                    if(ok)frontend.ShowUnavailable("Favorito sincronizado con el servidor.");
                    else frontend.ShowUnavailable("Favorito guardado solo en Windows; el servidor no confirmo el cambio.");
                }));
                return;
            }
            var dialog=GetComponent<RecoveredChoiceLevelDialog>();if(dialog!=null&&dialog.IsOpen)dialog.ShowBlocker("Favorito local de Windows; sin sincronizacion remota.");else frontend.ShowUnavailable("Favorito local de Windows; sin sincronizacion remota.");
        }
        private void BindSearch()
        {
            var tree=frontend.MainRoom;var go=tree.Find("InputField");if(go==null)return;
            var input=go.GetComponent<InputField>()??go.AddComponent<InputField>();input.targetGraphic=go.GetComponent<Graphic>();
            input.textComponent=go.GetComponentsInChildren<Text>(true).First(t=>t.name!="Placeholder");input.placeholder=go.GetComponentsInChildren<Text>(true).FirstOrDefault(t=>t.name=="Placeholder");input.textComponent.supportRichText=false;
            input.SetTextWithoutNotify("");go.SetActive(false);tree.Objects["level3:56"].SetActive(false);
            input.onValueChanged.RemoveAllListeners();input.onValueChanged.AddListener(value=>Filter(value,genre,difficulty,favoritesOnly));
            Action<string,Action> bind=(id,action)=>{var b=tree.Objects[id].GetComponent<Button>();b.onClick.RemoveAllListeners();b.interactable=true;b.onClick.AddListener(()=>action());};
            bind("level3:51",()=>{tree.Find("FiltersContainer").SetActive(false);go.SetActive(true);tree.Objects["level3:56"].SetActive(true);input.ActivateInputField();});
            bind("level3:56",()=>{input.text="";go.SetActive(false);tree.Find("FiltersContainer").SetActive(true);tree.Objects["level3:56"].SetActive(false);});
            bind("level3:84",()=>input.text="");tree.Objects["level3:84"].SetActive(true);
        }
        private void BindFilters()
        {
            var tree=frontend.MainRoom;var data=JsonUtility.FromJson<RecoveredUiData>(Resources.Load<TextAsset>("CatalogUi606/story-ui").text);
            var filterScroll=tree.Find("FilterEnhancedScroller").GetComponent<ScrollRect>();var content=filterScroll.content;
            for(int i=content.childCount-1;i>=0;i--){var child=content.GetChild(i);child.gameObject.SetActive(false);if(Application.isPlaying)Destroy(child.gameObject);else DestroyImmediate(child.gameObject);}
            filterHighlights.Clear();selectedFilter=0;float left=0;int position=0;
            Action<string,Action> add=(label,action)=>
            {
                int id=position++;var cell=RecoveredUiBuilder.Build(data,data.scenes[0],content);var root=cell.Find("FilterButton").GetComponent<RectTransform>();var text=cell.Find("Text").GetComponent<Text>();text.text=label;
                float width=Mathf.Max(176,text.preferredWidth+44);root.anchorMin=root.anchorMax=new Vector2(0,.5f);root.pivot=new Vector2(0,.5f);root.sizeDelta=new Vector2(width,80);root.anchoredPosition=new Vector2(left,0);left+=width;
                var image=cell.Find("Image").GetComponent<Image>();image.rectTransform.sizeDelta=new Vector2(width,80);
                var button=cell.Find("FilterButton").GetComponent<Button>();button.interactable=true;button.onClick.AddListener(()=>{selectedFilter=id;action();foreach(var highlight in filterHighlights)highlight();});
                filterHighlights.Add(()=>image.sprite=RecoveredUiBuilder.Asset<Sprite>(data,selectedFilter==id?data.filterStyle.upChoiceSprite:data.filterStyle.upNormalSprite));
            };
            add(RecoveredLocalization.Text("ALL_filter","All"),()=>{newOnly=false;Filter(query,0,difficulty);});
            var newFilter=Array.Find(index.Catalog.filters??new SongFilter[0],f=>f.type==2&&f.tag==1);
            if(newFilter!=null)add(newFilter.title,()=>{newOnly=true;Filter(query,0,difficulty);});
            add(RecoveredLocalization.Text("favorites","Favorites"),()=>{newOnly=false;Filter(query,0,difficulty,true);});
            foreach(var filter in index.Catalog.filters??new SongFilter[0])
            {
                var f=filter;
                if(f.type==1)add(f.title,()=>{newOnly=false;Filter(query,f.genre,difficulty);});
                else if(f.type==2&&f.tag==1)continue;
                else add(f.title,()=>frontend.ShowUnavailable("El snapshot no contiene la pertenencia completa al filtro "+f.title+"."));
            }
            content.anchorMin=content.anchorMax=new Vector2(0,.5f);content.pivot=new Vector2(0,.5f);content.sizeDelta=new Vector2(left,84);content.anchoredPosition=Vector2.zero;
            foreach(var highlight in filterHighlights)highlight();tree.Find("FiltersContainer").SetActive(true);
            var difficultyButton=tree.Objects["level3:74"].GetComponent<Button>();difficultyButton.interactable=true;difficultyButton.onClick.RemoveAllListeners();difficultyButton.onClick.AddListener(ShowDifficultyMenu);
            var up=tree.Find("PageUpButton").GetComponent<Button>();up.interactable=true;up.onClick.RemoveAllListeners();up.onClick.AddListener(()=>{scroll.StopMovement();scroll.verticalNormalizedPosition=1;Refresh();});up.gameObject.SetActive(true);
        }

        public void ShowDifficultyMenu()
        {
            if(difficultyMenu!=null){CloseDifficultyMenu();return;}
            var data=RecoveredDifficultyStyles.DialogData;
            var dialog=RecoveredUiBuilder.Build(data,Array.Find(data.scenes,s=>s.name=="DifficultySelector"),frontend.transform,frontend.PortraitCamera);
            difficultyTree=dialog;difficultyMenu=dialog.Root;var canvas=dialog.Find("DifficultySelector").GetComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=901;
            var content=dialog.Find("Content").GetComponent<RectTransform>();var cells=new List<Action>();int pending=difficulty;
            var cellData=RecoveredDifficultyStyles.Data;var template=Array.Find(cellData.scenes,s=>s.name=="SelectorCell");
            for(int d=1;d<=7;d++)
            {
                int level=d;var cell=RecoveredUiBuilder.Build(cellData,template,content);
                RecoveredUiBuilder.UseAuthoredLayoutRoot(cell,"SelectorCell",content);
                cells.Add(()=>RecoveredDifficultyStyles.ApplySelector(cell,level,pending==level));
                var button=cell.Find("ChoiceButton").GetComponent<Button>();button.interactable=true;button.onClick.AddListener(()=>{pending=pending==level?0:level;foreach(var refresh in cells)refresh();});
            }
            foreach(var refresh in cells)refresh();
            var clear=dialog.Find("ClearButton").GetComponent<Button>();clear.interactable=true;clear.onClick.AddListener(()=>{pending=0;foreach(var refresh in cells)refresh();});
            var save=dialog.Find("SaveButton").GetComponent<Button>();save.interactable=true;save.onClick.AddListener(()=>{Filter(query,genre,pending,favoritesOnly);CloseDifficultyMenu();});
            var mask=dialog.Find("MaskImage");var cancel=mask.AddComponent<Button>();cancel.targetGraphic=mask.GetComponent<Graphic>();cancel.onClick.AddListener(CloseDifficultyMenu);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
    }
}
