using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    public sealed class RecoveredUiTree
    {
        public GameObject Root;
        public readonly Dictionary<string, GameObject> Objects = new Dictionary<string, GameObject>();
        public readonly Dictionary<string, Transform> Transforms = new Dictionary<string, Transform>();
        public GameObject Find(string name) { foreach (var pair in Objects) if (pair.Value != null && pair.Value.name == name) return pair.Value; return null; }
    }
    public static class RecoveredUiBuilder
    {
        public static bool HasScript(UiObject item, string name) => item.scripts != null && Array.IndexOf(item.scripts, name) >= 0;
        private static Vector2 V2(float[] a, int offset = 0) => new Vector2(a[offset], a[offset + 1]);
        private static Vector3 V3(float[] a) => new Vector3(a[0], a[1], a[2]);
        private static Color ColorOf(float[] a) => new Color(a[0], a[1], a[2], a[3]);
        public static T Asset<T>(RecoveredUiData data, string id) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(id)) return null;
            // Two search-field Text nodes reference an external Unity builtin, not an extractable font.
            // Temporary, explicit Windows adapter; original font identity remains pending.
            if (id == "unity default resources:10102" && typeof(T) == typeof(Font)) return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") as T;
            foreach (var a in data.assets) if (a.id == id)
            {
                // Runtime packs (converted CDN bundles, see RecoveredUiPacks): "file:<md5>/Sprites/x.png" instead of a Resources path.
                if(!string.IsNullOrEmpty(a.resource)&&a.resource.StartsWith("file:",StringComparison.Ordinal)&&typeof(T)==typeof(Sprite))return RecoveredUiPacks.LoadSprite(a) as T;
                var asset=Resources.Load<T>(a.resource);
                // These PNGs are standalone atlas crops. Repeat can sample the opposite opaque edge,
                // producing a straight seam outside bg_lives' transparent diagonal corner.
                if(asset is Sprite sprite)sprite.texture.wrapMode=TextureWrapMode.Clamp;
                return asset;
            }
            throw new InvalidOperationException("Unrecovered asset: " + id);
        }
        // LayoutGroup must control the authored prefab root, not an extra stretch wrapper.
        public static void UseAuthoredLayoutRoot(RecoveredUiTree tree,string name,Transform parent)
        {
            var wrapper=tree.Root;var authored=tree.Find(name);if(authored==null)throw new InvalidOperationException("Authored layout root missing: "+name);
            authored.transform.SetParent(parent,false);tree.Root=authored;wrapper.SetActive(false);
            if(Application.isPlaying)UnityEngine.Object.Destroy(wrapper);else UnityEngine.Object.DestroyImmediate(wrapper);
        }
        public static RecoveredUiTree Build(RecoveredUiData data, UiScene source, Transform parent, Camera viewportCamera = null)
        {
            var tree = new RecoveredUiTree { Root = parent is RectTransform ? new GameObject(source.name, typeof(RectTransform)) : new GameObject(source.name) };
            tree.Root.transform.SetParent(parent, false);
            if (tree.Root.transform is RectTransform wrapper) { wrapper.anchorMin = Vector2.zero; wrapper.anchorMax = Vector2.one; wrapper.sizeDelta = Vector2.zero; wrapper.anchoredPosition = Vector2.zero; }
            foreach (var item in source.objects)
            {
                var go = item.rect.Length == 10 ? new GameObject(item.name, typeof(RectTransform)) : new GameObject(item.name);
                go.SetActive(false); tree.Objects.Add(item.id, go); tree.Transforms.Add(item.transformId, go.transform);
            }
            var originalTransforms = new Dictionary<string, UiObject>(); foreach (var item in source.objects) originalTransforms.Add(item.transformId, item);
            foreach (var item in source.objects)
            {
                if (HasScript(item, "UnityEngine.UI.Image") && HasScript(item, "UnityEngine.UI.Text")) throw new InvalidOperationException("Conflicting original Graphic declarations: " + item.id);
                var go = tree.Objects[item.id]; var t = go.transform;
                t.SetParent(!string.IsNullOrEmpty(item.parent) && tree.Transforms.TryGetValue(item.parent, out var p) ? p : tree.Root.transform, false);
                t.localPosition = V3(item.position); t.localRotation = new Quaternion(item.rotation[0], item.rotation[1], item.rotation[2], item.rotation[3]); t.localScale = V3(item.scale);
                if (t is RectTransform rt) { rt.anchorMin = V2(item.rect); rt.anchorMax = V2(item.rect, 2); rt.pivot = V2(item.rect, 8); rt.sizeDelta = V2(item.rect, 6); rt.anchoredPosition = V2(item.rect, 4); }
                if (item.canvas)
                {
                    // Native modes/settings remain in DTO; Windows uses one portrait camera for all screens.
                    bool nestedCanvas = parent.GetComponentInParent<Canvas>() != null; string ancestor = item.parent;
                    while (!string.IsNullOrEmpty(ancestor) && originalTransforms.TryGetValue(ancestor, out var ancestorItem)) { if (ancestorItem.canvas) nestedCanvas = true; ancestor = ancestorItem.parent; }
                    var canvas = go.AddComponent<Canvas>();
                    if (!nestedCanvas) { canvas.renderMode = viewportCamera == null ? RenderMode.ScreenSpaceOverlay : RenderMode.ScreenSpaceCamera; canvas.worldCamera = viewportCamera; canvas.planeDistance = 100; canvas.targetDisplay = 0; t.localScale = Vector3.one; }
                    canvas.overrideSorting = item.canvasSettings.overrideSorting; canvas.sortingOrder = item.canvasSettings.sortingOrder; canvas.enabled = item.canvasSettings.enabled;
                    go.AddComponent<GraphicRaycaster>();
                    if (HasScript(item, "UnityEngine.UI.CanvasScaler")) { var s = go.AddComponent<RecoveredCanvasScaler>(); s.uiScaleMode = (CanvasScaler.ScaleMode)item.scaler.mode; s.referenceResolution = V2(item.scaler.resolution); s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; s.matchWidthOrHeight = item.scaler.match; s.referencePixelsPerUnit = item.scaler.ppu; }
                }
                if (HasScript(item, "UnityEngine.UI.RawImage") && item.rawImage != null)
                {
                    var f=item.rawImage;if(f.color==null||f.color.Length!=4||f.uv==null||f.uv.Length!=4)throw new InvalidOperationException("Missing original RawImage projection: "+item.id);var im=go.AddComponent<RawImage>();im.texture=Asset<Texture2D>(data,f.texture);im.color=ColorOf(f.color);im.raycastTarget=f.raycast;im.enabled=f.enabled&&!string.IsNullOrEmpty(f.texture)&&string.IsNullOrEmpty(f.pending);im.uvRect=new Rect(f.uv[0],f.uv[1],f.uv[2],f.uv[3]);
                }
                if (HasScript(item, "UnityEngine.UI.Image"))
                {
                    var f = item.image; var im = go.AddComponent<Image>(); if (im == null) throw new InvalidOperationException("Cannot add original Image: " + item.id); string sprite = f.sprite; if (f.locales != null) foreach(var variant in f.locales) if(variant.language == RecoveredLocalization.Language) sprite = variant.sprite; im.sprite = Asset<Sprite>(data, sprite); im.color = ColorOf(f.color); im.raycastTarget = f.raycast;
                    im.type = (Image.Type)f.type; im.preserveAspect = f.preserve; im.fillCenter = f.fillCenter; im.fillMethod = (Image.FillMethod)f.fillMethod; im.fillOrigin = f.fillOrigin; im.fillAmount = f.fill; im.fillClockwise = f.clockwise; im.useSpriteMesh = f.useSpriteMesh; im.pixelsPerUnitMultiplier = f.ppuMultiplier; im.enabled = f.enabled;
                }
                if (HasScript(item, "UnityEngine.UI.Text"))
                {
                    var f = item.text; var text = go.AddComponent<Text>(); if (text == null) throw new InvalidOperationException("Cannot add original Text: " + item.id); text.font = Asset<Font>(data, f.font);
                    text.text = RecoveredLocalization.Text(f.key, f.value); text.fontSize = f.size; text.fontStyle = (FontStyle)f.style; text.alignment = (TextAnchor)f.alignment; text.resizeTextForBestFit = f.bestFit; text.resizeTextMinSize = f.minSize; text.resizeTextMaxSize = f.maxSize; text.supportRichText = f.rich; text.lineSpacing = f.lineSpacing; text.horizontalOverflow = (HorizontalWrapMode)f.horizontal; text.verticalOverflow = (VerticalWrapMode)f.vertical; text.color = ColorOf(f.color); text.raycastTarget = f.raycast; text.enabled = f.enabled;
                }
                if(item.marquee!=null&&item.marquee.recovered){if(!HasScript(item,".MarqueeForText")||go.GetComponent<Text>()==null)throw new InvalidOperationException("Invalid recovered marquee declaration: "+item.id);go.AddComponent<RecoveredMarqueeText>().Configure(go.GetComponent<Text>(),item.marquee);}
                if (item.shadows != null) foreach(var original in item.shadows) { var shadow = go.AddComponent<RecoveredUiShadow>(); shadow.style = original.style; shadow.effectColor = ColorOf(original.color); shadow.effectDistance = V2(original.distance); shadow.useGraphicAlpha = original.useGraphicAlpha; shadow.enabled = original.enabled; }
                if (HasScript(item, "UnityEngine.UI.AspectRatioFitter")) { var a = go.AddComponent<AspectRatioFitter>(); a.aspectMode = (AspectRatioFitter.AspectMode)item.aspect.mode; a.aspectRatio = item.aspect.ratio; a.enabled = item.aspect.enabled; }
                if (item.mask) go.AddComponent<Mask>().showMaskGraphic = item.maskGraphic;
                if (item.button) { var b = go.AddComponent<Button>(); b.targetGraphic = go.GetComponent<Graphic>(); b.interactable = false; b.transition = Selectable.Transition.None; }
                if (HasScript(item, "UnityEngine.UI.ContentSizeFitter")) { var f = go.AddComponent<ContentSizeFitter>(); f.horizontalFit = (ContentSizeFitter.FitMode)item.fit[0]; f.verticalFit = (ContentSizeFitter.FitMode)item.fit[1]; }
            }
            foreach (var item in source.objects)
            {
                if (item.button && !string.IsNullOrEmpty(item.buttonTarget)) tree.Objects[item.id].GetComponent<Button>().targetGraphic = tree.Objects[item.buttonTarget].GetComponent<Graphic>();
                foreach (var child in item.children) if (tree.Transforms.TryGetValue(child, out var c)) c.SetAsLastSibling();
                if (HasScript(item,"UnityEngine.UI.LayoutElement"))
                {
                    var f=item.layoutElement; if(f==null||f.sizes==null) throw new InvalidOperationException("Missing LayoutElement projection: "+item.id); var element=tree.Objects[item.id].AddComponent<LayoutElement>(); element.ignoreLayout=f.ignore; element.layoutPriority=f.priority; element.enabled=f.enabled;
                    element.minWidth=f.sizes[0]; element.minHeight=f.sizes[1]; element.preferredWidth=f.sizes[2]; element.preferredHeight=f.sizes[3]; element.flexibleWidth=f.sizes[4]; element.flexibleHeight=f.sizes[5];
                }
                if (HasScript(item,"UnityEngine.UI.HorizontalLayoutGroup") || HasScript(item,"UnityEngine.UI.VerticalLayoutGroup") || HasScript(item,"UnityEngine.UI.GridLayoutGroup"))
                {
                    var f=item.layout; if(f==null||f.padding==null) throw new InvalidOperationException("Missing LayoutGroup projection: "+item.id); LayoutGroup group;
                    if(f.kind==2) { var grid=tree.Objects[item.id].AddComponent<GridLayoutGroup>(); grid.cellSize=V2(f.cellSize); grid.spacing=V2(f.spacing); grid.startCorner=(GridLayoutGroup.Corner)f.startCorner; grid.startAxis=(GridLayoutGroup.Axis)f.startAxis; grid.constraint=(GridLayoutGroup.Constraint)f.constraint; grid.constraintCount=f.constraintCount; group=grid; }
                    else { HorizontalOrVerticalLayoutGroup line=f.kind==0 ? (HorizontalOrVerticalLayoutGroup)tree.Objects[item.id].AddComponent<HorizontalLayoutGroup>() : tree.Objects[item.id].AddComponent<VerticalLayoutGroup>(); line.spacing=f.spacing[0]; line.childForceExpandWidth=f.flags[0]; line.childForceExpandHeight=f.flags[1]; line.childControlWidth=f.flags[2]; line.childControlHeight=f.flags[3]; line.childScaleWidth=f.flags[4]; line.childScaleHeight=f.flags[5]; line.reverseArrangement=f.flags[6]; group=line; }
                    group.padding=new RectOffset(f.padding[0],f.padding[1],f.padding[2],f.padding[3]); group.childAlignment=(TextAnchor)f.alignment; group.enabled=f.enabled;
                }
                if (HasScript(item, "UnityEngine.UI.Slider"))
                {
                    var f = item.slider; if (f == null || f.fillRect == null) throw new InvalidOperationException("Missing original Slider projection: " + item.id); var slider = tree.Objects[item.id].AddComponent<Slider>();
                    if (tree.Transforms.TryGetValue(f.fillRect, out var fill)) slider.fillRect = fill as RectTransform;
                    if (!string.IsNullOrEmpty(f.handleRect) && tree.Transforms.TryGetValue(f.handleRect, out var handle)) slider.handleRect = handle as RectTransform;
                    slider.minValue = f.min; slider.maxValue = f.max; slider.wholeNumbers = f.wholeNumbers;
                    slider.direction = (Slider.Direction)f.direction; slider.interactable = f.interactable; slider.value = f.value;
                }
                if (HasScript(item, "UnityEngine.UI.ScrollRect"))
                {
                    var f = item.scroll; ScrollRect s = item.name=="SongsScrollView" ? tree.Objects[item.id].AddComponent<RecoveredNestedScrollRect>() : tree.Objects[item.id].AddComponent<ScrollRect>();
                    if (!string.IsNullOrEmpty(f.content) && tree.Transforms.TryGetValue(f.content, out var content)) s.content = content as RectTransform;
                    if (!string.IsNullOrEmpty(f.viewport) && tree.Transforms.TryGetValue(f.viewport, out var viewport)) s.viewport = viewport as RectTransform;
                    s.horizontal = f.horizontal; s.vertical = f.vertical; s.movementType = (ScrollRect.MovementType)f.movement; s.elasticity = f.elasticity; s.inertia = f.inertia; s.decelerationRate = f.deceleration; s.scrollSensitivity = f.sensitivity;
                }
                tree.Objects[item.id].SetActive(item.active);
            }
            return tree;
        }
    }
}
