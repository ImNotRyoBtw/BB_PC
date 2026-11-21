using System;
using UnityEngine;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    // Shared Windows adapter for the recovered HomePageTopView/ScrollRect geometry.
    // Story has no filter strip; it must not inherit Freeplay's reserved strip or scrolled baseline.
    public sealed class RecoveredCatalogLayout
    {
        private readonly RecoveredFrontend host;
        private readonly ScrollRect scroll;
        private readonly RectTransform header,viewport;
        private readonly Vector2 headerBase,viewportBase;
        private readonly float authoredHeader,minimum;
        private readonly Button pageUp;
        private float extra;
        public float HeaderHeight=>authoredHeader+extra;
        public float VisibleHeight{get;private set;}
        public RecoveredCatalogLayout(RecoveredFrontend host,float minimum)
        {
            this.host=host;this.minimum=minimum;scroll=host.MainRoom.Find("SonglistScrollView").GetComponent<ScrollRect>();
            scroll.StopMovement();header=host.MainRoom.Find("HomePageTopView").GetComponent<RectTransform>();viewport=scroll.viewport;
            headerBase=header.anchoredPosition;viewportBase=viewport.offsetMax;authoredHeader=Math.Abs(header.sizeDelta.y);
            if(authoredHeader<=0)throw new InvalidOperationException("Recovered header height missing");
            if(viewport.GetComponent<RectMask2D>()==null)viewport.gameObject.AddComponent<RectMask2D>();
            var page=host.MainRoom.Find("HomePage");if(page.GetComponent<RectMask2D>()==null)page.AddComponent<RectMask2D>();
            pageUp=host.MainRoom.Find("PageUpButton")?.GetComponent<Button>();if(pageUp!=null){pageUp.interactable=true;pageUp.onClick.AddListener(ScrollToTop);}
            VisibleHeight=HeaderHeight;
        }
        public void SetExtraStrip(float height){extra=height;}
        public void Refresh(int count,float rowHeight)
        {
            float offset=scroll.content.anchoredPosition.y;
            float collapse=Mathf.Clamp(offset,0,Mathf.Max(0,HeaderHeight-minimum-extra));VisibleHeight=HeaderHeight-collapse;
            header.anchoredPosition=headerBase+Vector2.up*collapse;header.gameObject.SetActive(true);
            var max=viewportBase;max.y-=VisibleHeight;viewport.offsetMax=max;
            scroll.content.sizeDelta=new Vector2(scroll.content.sizeDelta.x,HeaderHeight+rowHeight*count-VisibleHeight);
            host.MainRoom.Find("PageUpButton")?.SetActive(offset>HeaderHeight);
        }
        private void ScrollToTop(){scroll.StopMovement();scroll.verticalNormalizedPosition=1;}
        public void Restore()
        {if(pageUp!=null)pageUp.onClick.RemoveListener(ScrollToTop);scroll.StopMovement();header.anchoredPosition=headerBase;viewport.offsetMax=viewportBase;host.MainRoom.Find("PageUpButton")?.SetActive(false);}
    }
}
