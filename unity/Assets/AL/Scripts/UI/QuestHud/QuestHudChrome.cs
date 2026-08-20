using UnityEngine;
using UnityEngine.UI;

namespace AL.UI.QuestHud
{
    /// <summary>
    /// Quest HUD tokens match PresentationChrome (t_04d03944): stone plate,
    /// metal edge, ink, 1920x1080 scaler, OS fonts. Not LegacyRuntime.
    /// </summary>
    public static class QuestHudChrome
    {
        public const int TitleSize = 18;
        public const int BodySize = 14;
        public const int CaptionSize = 13;
        public const int ActionSize = 15;
        public const float MinHit = 56f;
        public const float PlateWidth = 380f;
        public const float PlateHeight = 196f;

        public static readonly Color StonePlate = new Color(0.078f, 0.082f, 0.090f, 0.98f);
        public static readonly Color StoneInset = new Color(0.031f, 0.033f, 0.037f, 0.96f);
        public static readonly Color MetalEdge = new Color(0.78f, 0.75f, 0.68f, 0.92f);
        public static readonly Color Ink = new Color(0.93f, 0.91f, 0.85f, 1f);
        public static readonly Color InkMuted = new Color(0.72f, 0.70f, 0.65f, 1f);
        public static readonly Color InkFaint = new Color(0.58f, 0.57f, 0.53f, 1f);

        public static readonly string[] FontFamilies =
        {
            "Segoe UI",
            "Noto Sans",
            "Noto Sans CJK KR",
            "Malgun Gothic",
            "Arial"
        };

        public static Font ResolveFont(int size = BodySize)
        {
            Font font = Font.CreateDynamicFontFromOSFont(FontFamilies, size);
            if (font != null)
            {
                return font;
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        public static bool IsLegacyRuntime(Font font)
        {
            return font != null &&
                   font.name.IndexOf("LegacyRuntime", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void ApplyScaler(CanvasScaler scaler)
        {
            if (scaler == null)
            {
                return;
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        public static Image CreatePlate(
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            bool raycastTarget = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return image;
        }

        public static Text CreateLabel(
            Transform parent,
            string name,
            Font font,
            string value,
            int size,
            Color color,
            TextAnchor alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.text = value ?? string.Empty;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return text;
        }

        public static Button CreateHit(
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            Image image = CreatePlate(
                parent,
                name,
                color,
                anchorMin,
                anchorMax,
                pivot,
                anchoredPosition,
                sizeDelta,
                raycastTarget: true);
            var button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.04f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.80f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
            Vector2 size = image.rectTransform.sizeDelta;
            if (size.x > 0f && size.x < MinHit)
            {
                size.x = MinHit;
            }

            if (size.y > 0f && size.y < MinHit)
            {
                size.y = MinHit;
            }

            image.rectTransform.sizeDelta = size;
            return button;
        }
    }
}
