using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    // NavigationType values and prefab bindings are from 6.0.6, not the 6.0.2 screenshot.
    public enum MainPage { Home = 0, Shop = 1, Versus = 2, Character = 3, Adventure = 4 }
    public sealed class RecoveredNavigation
    {
        public MainPage Current { get; private set; }
        public readonly RecoveredUiData Data;
        public readonly RecoveredUiTree Bar;
        public readonly Dictionary<MainPage, RecoveredUiTree> Pages = new Dictionary<MainPage, RecoveredUiTree>();
        private readonly RecoveredUiTree main;
        private readonly Action<MainPage> changed;
        private readonly Dictionary<MainPage, Button> buttons = new Dictionary<MainPage, Button>();
        public RecoveredNavigation(RecoveredUiTree main, Camera camera, Action<MainPage> changed)
        {
            this.main = main; this.changed = changed;
            var source = Resources.Load<TextAsset>("Recovered606/navigation");
            if (source == null) throw new InvalidOperationException("Original navigation prefabs unavailable");
            Data = JsonUtility.FromJson<RecoveredUiData>(source.text);
            if (Data.sourceVersion != "6.0.6") throw new InvalidOperationException("Wrong navigation version");
            var container=(RectTransform)main.Find("NavigationBar").transform;
            // The 324-unit container starts at -200: only its top 124 units are visible.
            // Preserve that authored placement and the bar's 500-unit artwork below it;
            // stretching the artwork into 324 visible units distorts the bottom navigation.
            Bar = RecoveredUiBuilder.Build(Data, Data.scenes[0], container, camera);
            foreach (var page in new[] {MainPage.Adventure, MainPage.Versus, MainPage.Shop, MainPage.Character})
            {
                UiScene scene = Array.Find(Data.scenes, x => x.name == page.ToString());
                var tree = RecoveredUiBuilder.Build(Data, scene, main.Find("HomePage").transform.parent, camera);
                Pages.Add(page, tree);
                // Do not expose serialized authoring popups, sample profile, promotions or purchases as live data.
                foreach (var pair in tree.Objects)
                {
                    var original = Array.Find(scene.objects,x=>x.id==pair.Key);
                    bool requiresModel = pair.Value.name == "PopupWindow" || pair.Value.name == "DetailDisplayPanel" || pair.Value.name == "BannerCell" || pair.Value.name == "LoadingIndicator";
                    foreach(var script in original.scripts) if(script==".NameBoxComponent" || script==".FriendOrEmojiComponent" || script==".RankBar" || script==".SeasonPanelPresenter" || script==".LoadingIndicator") requiresModel=true;
                    if (requiresModel) pair.Value.SetActive(false);
                    if (pair.Value.TryGetComponent<Text>(out var text))
                    {
                        // Exact serialized authoring labels, never a replacement catalog/profile.
                        if (text.text == "This is a title" || text.text == "This is a subtitle" || text.text == "Taik to XXX" || text.text == "BF Name" || text.text == "Gf Name" || text.text == "Boss Name") text.text = "";
                        else if (double.TryParse(text.text.TrimEnd('%','+'), out var authoringNumber)) text.text = "—";
                    }
                    if (pair.Value.TryGetComponent<Button>(out var button)) button.interactable = false;
                }
                tree.Root.SetActive(false);
            }
            foreach (var page in new[] { MainPage.Adventure, MainPage.Versus, MainPage.Home, MainPage.Shop, MainPage.Character })
            {
                string name = page == MainPage.Character ? "CustomButton" : page + "Button";
                var button = Bar.Find(name).GetComponent<Button>(); buttons.Add(page, button);
                button.interactable = true; button.onClick.AddListener(() => Select(page));
            }
            Select(MainPage.Home);
        }
        public void Select(MainPage page)
        {
            if (!Enum.IsDefined(typeof(MainPage), page)) throw new ArgumentOutOfRangeException(nameof(page));
            main.Find("HomePage").SetActive(page == MainPage.Home);
            foreach (var pair in Pages) pair.Value.Root.SetActive(pair.Key == page);
            // Native MutualExclusionButton.SetSelecting: target GO active iff selecting == selectingNeedHiddenImage.
            // Every button in NormalNavigationBar serializes selectingNeedHiddenImage=false.
            foreach (var pair in buttons) pair.Value.targetGraphic.gameObject.SetActive(pair.Key != page);
            Current = page; changed?.Invoke(page);
        }
        public Button ButtonFor(MainPage page) => buttons[page];
    }
}
