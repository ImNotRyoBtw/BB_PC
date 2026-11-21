using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    // Windows input adapter, not a recovered native algorithm. Preserve the chapter scroller,
    // but let the outer Story list receive gestures when the inner list fits or is at its edge.
    public sealed class RecoveredNestedScrollRect : ScrollRect
    {
        private ScrollRect routedDrag;
        private ScrollRect Outer()
        {
            for(var parent=transform.parent;parent!=null;parent=parent.parent)
            {var candidate=parent.GetComponent<ScrollRect>();if(candidate!=null&&candidate.isActiveAndEnabled)return candidate;}
            return null;
        }
        private bool CanMove(float direction)
        {
            if(!vertical||content==null||viewport==null||content.rect.height<=viewport.rect.height+.1f)return false;
            float position=verticalNormalizedPosition;
            return direction>0?position>.001f:direction<0&&position<.999f;
        }
        public override void OnScroll(PointerEventData eventData)
        {
            var outer=Outer();if(outer!=null&&!CanMove(-eventData.scrollDelta.y))outer.OnScroll(eventData);else base.OnScroll(eventData);
        }
        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {base.OnInitializePotentialDrag(eventData);Outer()?.OnInitializePotentialDrag(eventData);}
        public override void OnBeginDrag(PointerEventData eventData)
        {
            routedDrag=CanMove(eventData.delta.y)?null:Outer();
            if(routedDrag!=null)routedDrag.OnBeginDrag(eventData);else base.OnBeginDrag(eventData);
        }
        public override void OnDrag(PointerEventData eventData)
        {if(routedDrag!=null)routedDrag.OnDrag(eventData);else base.OnDrag(eventData);}
        public override void OnEndDrag(PointerEventData eventData)
        {if(routedDrag!=null)routedDrag.OnEndDrag(eventData);else base.OnEndDrag(eventData);routedDrag=null;}
    }
}
