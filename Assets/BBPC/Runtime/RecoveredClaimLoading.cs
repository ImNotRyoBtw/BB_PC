using System;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    // 6.0.6 navigation-graphs.json: LoadingDialog/Mask/Loading, autoPlay,
    // rotateVector3=(0,0,-360), duration=1. Sorting: LOADING_SPINNER_NOTES.
    // Windows adapter: one refcount per host, only used around active claim POSTs.
    public sealed class RecoveredClaimLoading : MonoBehaviour
    {
        private GameObject root;
        private Transform spinner;
        private int references;
        internal GameObject Root => root;
        internal int References => references;

        public static IDisposable Begin(Transform host,Camera camera)
        {
            if(host==null)throw new ArgumentNullException(nameof(host));
            var loading=host.GetComponent<RecoveredClaimLoading>();
            if(loading==null)loading=host.gameObject.AddComponent<RecoveredClaimLoading>();
            if(loading.root==null)loading.Build(camera);
            if(loading.references++==0)
            {
                loading.spinner.localRotation=Quaternion.identity;
                loading.root.SetActive(true);
            }
            return new Scope(loading);
        }

        private void Build(Camera camera)
        {
            var resource=Resources.Load<TextAsset>("RecoveredLoading606/dialogs");
            if(resource==null)throw new InvalidOperationException("Missing recovered claim loading dialog");
            var data=JsonUtility.FromJson<RecoveredUiData>(resource.text);
            var scene=Array.Find(data.scenes,item=>item.name=="LoadingDialog");
            if(scene==null)throw new InvalidOperationException("Missing LoadingDialog projection");
            var tree=RecoveredUiBuilder.Build(data,scene,transform,camera);
            root=tree.Root;
            try
            {
                RecoveredUiBuilder.UseAuthoredLayoutRoot(tree,"LoadingDialog",transform);
                root=tree.Root;
                // Normalize hidden prefab root into the same portrait popup space as Home.
                var rect=(RectTransform)root.transform;
                rect.localScale=Vector3.one;
                rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
                rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=Vector2.zero;rect.anchoredPosition=Vector2.zero;
                var canvas=root.GetComponent<Canvas>();
                canvas.renderMode=camera==null?RenderMode.ScreenSpaceOverlay:RenderMode.ScreenSpaceCamera;
                canvas.worldCamera=camera;canvas.planeDistance=100;
                canvas.overrideSorting=true;canvas.sortingOrder=32699;
                var scaler=root.GetComponent<RecoveredCanvasScaler>();
                scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(828,1792);scaler.matchWidthOrHeight=1;
                spinner=root.transform.Find("Mask/Loading");
                if(spinner==null)throw new InvalidOperationException("Missing recovered loading indicator");
                // dialogs.json: Mask/Loading itself owns the Image (no image child); keep its authored
                // splash_progress spinner. The reward diamond icon was wrongly forced here and read as a spinning diamond.
                root.SetActive(false);
            }
            catch { Clear();throw; }
        }

        private void Update()
        {
            if(references>0&&spinner!=null)
                spinner.Rotate(0,0,-360f*Time.unscaledDeltaTime,Space.Self);
        }
        private void Release()
        {
            if(references==0)return;
            if(--references==0&&root!=null)root.SetActive(false);
        }
        private void OnDestroy() { Clear(); }
        private void Clear()
        {
            references=0;spinner=null;
            if(root!=null)
            {
                root.SetActive(false);
                if(Application.isPlaying)Destroy(root);else DestroyImmediate(root);
            }
            root=null;
        }
        private sealed class Scope : IDisposable
        {
            private RecoveredClaimLoading owner;
            internal Scope(RecoveredClaimLoading owner) { this.owner=owner; }
            public void Dispose()
            {
                var current=owner;owner=null;
                if(current!=null)current.Release();
            }
        }
    }
}
