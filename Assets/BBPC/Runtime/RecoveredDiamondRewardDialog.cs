using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    // 6.0.6 GetDiamDialog hierarchy/sprites: map_GetDiamDialog.txt.
    // RectTransforms/colors recovered from the 6.0.6 GetDiamDialog bundle using Unity 2022.3.62f3.
    public sealed class RecoveredDiamondRewardDialog : MonoBehaviour
    {
        private GameObject popup;
        private Transform lighting;
        private float elapsed;
        public Text Count { get; private set; }
        public bool IsOpen => popup != null && popup.activeSelf;

        internal static Sprite LoadSprite(string name)
        {
            var sprite = Resources.Load<Sprite>("RecoveredGetDiamDialog606/Sprites/" + name);
            if (sprite == null) throw new InvalidOperationException("Missing GetDiamDialog sprite: " + name);
            return sprite;
        }

        public void Show(Transform parent, Camera camera, int gain)
        {
            Dismiss();
            // Load before creating UI so a missing import cannot leave a blocking partial dialog.
            var glow = LoadSprite("beiguang_0");
            var diamond = LoadSprite("zsd");
            var amountIcon = LoadSprite("zuanshicg");
            var continueSprite = LoadSprite("UISprite");
            // map_GetDiamDialog.txt: NumText :: Text[Font:CLASH FONT_CN_REGULAR FINAL]. That is resources.assets:914.
            // Do NOT proxy ResultDifficultyText.font — it is FORTE (913) and renders the amount thick/slanted.
            var font = Resources.Load<Font>("FreeplayUi606/Fonts/resources.assets_914") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            popup = new GameObject("GetDiamDialog", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            popup.transform.SetParent(parent, false);
            var canvas = popup.GetComponent<Canvas>();
            canvas.renderMode = camera == null ? RenderMode.ScreenSpaceOverlay : RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 100;
            canvas.overrideSorting = true; canvas.sortingOrder = 960;
            var scaler = popup.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(828, 1792); scaler.matchWidthOrHeight = 1;
            Stretch((RectTransform)popup.transform);

            var mask = ImageNode("MaskImage", popup.transform, null, Vector2.zero, Vector2.zero);
            Stretch(mask.rectTransform);
            mask.color = new Color(0, 0, 0, .6f); mask.raycastTarget = true;
            DismissButton(mask);
            var light = ImageNode("Lighting", mask.transform, glow, new Vector2(0, 200), new Vector2(450, 450));
            light.color = new Color(1, 1, 1, 230f / 255f);
            lighting = light.transform;
            lighting.localScale = new Vector3(1.5f, 1.5f, 1);
            light.gameObject.AddComponent<Animator>();
            ImageNode("RewardImage", mask.transform, diamond, new Vector2(0, 200), new Vector2(344, 266));
            var amount = ImageNode("Image", mask.transform, amountIcon, new Vector2(-67.9f, -100), new Vector2(89, 70));
            Count = TextNode("NumText", amount.transform, font, "+" + gain.ToString(CultureInfo.InvariantCulture), 50,
                new Vector2(162.5f, 0), new Vector2(140, 70));
            Count.rectTransform.anchorMin = new Vector2(0, .5f);
            Count.rectTransform.anchorMax = new Vector2(1, .5f);
            Count.alignment = TextAnchor.MiddleLeft;
            Count.GetComponent<Outline>().effectDistance = new Vector2(2, -2);
            var next = ImageNode("Continue", mask.transform, continueSprite, new Vector2(0, -368), new Vector2(960, 120));
            // The original UISprite is transparent: it provides the button hit area, not a visible panel.
            next.type = Image.Type.Sliced; next.color = new Color(1, 1, 1, 0);
            next.rectTransform.localScale = Vector3.one * .736f;
            next.raycastTarget = true;
            DismissButton(next);
            var label = TextNode("Text", next.transform, font, "CONTINUAR", 60, Vector2.zero, Vector2.zero);
            Stretch(label.rectTransform);
            label.GetComponent<Outline>().effectDistance = new Vector2(.75f, -.75f);
            var fitter = label.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var underline = ImageNode("UnderLine", label.transform, null, Vector2.zero, new Vector2(0, 2.5f));
            underline.rectTransform.anchorMin = Vector2.zero;
            underline.rectTransform.anchorMax = new Vector2(1, 0);
            underline.gameObject.AddComponent<Shadow>();
            elapsed = 0; ShowAnimation();
        }

        private void DismissButton(Image image)
        {
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image; button.transition = Selectable.Transition.None;
            button.onClick.AddListener(Dismiss);
        }
        private static Image ImageNode(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }
        private static Text TextNode(string name, Transform parent, Font font, string value, int size, Vector2 position, Vector2 dimensions)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.GetComponent<Outline>().effectColor = Color.black;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = dimensions;
            var text = go.GetComponent<Text>();
            text.font = font; text.text = value; text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter; text.color = Color.white;
            text.raycastTarget = false; text.supportRichText = false;
            return text;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero; rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }
        private void ShowAnimation()
        {
            // Windows glow rotation adapter. TODO: recover GetDiamDialog's own Animator clip/timing.
            lighting.localRotation = Quaternion.Euler(0, 0, -144f * (elapsed % 2.5f));
        }
        private void Update()
        {
            if (!IsOpen) return;
            elapsed += Time.unscaledDeltaTime; ShowAnimation();
        }
        public void Dismiss()
        {
            if (popup == null) return;
            popup.SetActive(false);
            if (Application.isPlaying) Destroy(popup); else DestroyImmediate(popup);
            popup = null; Count = null; lighting = null;
        }
        private void OnDisable() { Dismiss(); }
    }
}
