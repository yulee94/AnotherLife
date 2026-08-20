using UnityEngine;
using UnityEngine.UI;

namespace AL.UI.Presentation
{
    /// <summary>
    /// Shared first-session chrome. Realm select, character create, and the 3D HUD
    /// read these tokens so the session is one product.
    /// </summary>
    public static class PresentationChrome
    {
        public const int DisplaySize = 40;
        public const int TitleSize = 26;
        public const int PeopleSize = 16;
        public const int BodySize = 15;
        public const int CaptionSize = 13;
        public const int ActionSize = 16;
        public const float SpaceXs = 8f;
        public const float SpaceSm = 16f;
        public const float SpaceMd = 24f;
        public const float SpaceLg = 40f;
        public const float SpaceXl = 64f;
        public const float MinHit = 56f;
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        public static readonly Color StoneVoid = new Color(0.043f, 0.047f, 0.055f, 1f);
        public static readonly Color StonePlate = new Color(0.078f, 0.082f, 0.090f, 0.98f);
        public static readonly Color StoneInset = new Color(0.031f, 0.033f, 0.037f, 0.96f);
        public static readonly Color MetalEdge = new Color(0.78f, 0.75f, 0.68f, 0.92f);
        public static readonly Color MetalDim = new Color(0.52f, 0.50f, 0.46f, 0.88f);
        public static readonly Color Ink = new Color(0.93f, 0.91f, 0.85f, 1f);
        public static readonly Color InkMuted = new Color(0.72f, 0.70f, 0.65f, 1f);
        public static readonly Color InkFaint = new Color(0.58f, 0.57f, 0.53f, 1f);
        public static readonly Color Veil = new Color(0.02f, 0.02f, 0.025f, 0.78f);

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

        public static void ApplyCanvasScaler(CanvasScaler scaler)
        {
            if (scaler == null)
            {
                return;
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        public static void BindFonts(Transform root, Font font)
        {
            if (root == null || font == null)
            {
                return;
            }

            Text[] labels = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null)
                {
                    labels[i].font = font;
                }
            }
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
            var rect = image.rectTransform;
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
            var rect = text.rectTransform;
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
            EnforceMinHit(image.rectTransform);
            return button;
        }

        public static void EnforceMinHit(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            Vector2 size = rect.sizeDelta;
            if (size.x > 0f && size.x < MinHit)
            {
                size.x = MinHit;
            }

            if (size.y > 0f && size.y < MinHit)
            {
                size.y = MinHit;
            }

            rect.sizeDelta = size;
        }
    }
}
