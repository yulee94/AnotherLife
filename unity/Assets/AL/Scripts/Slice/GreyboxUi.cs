using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AL.Slice
{
    /// <summary>
    /// Minimal, dependency-free uGUI helpers for the greybox slice screens. Shares the same
    /// LegacyRuntime.ttf / Arial.ttf fallback and 1920x1080 reference resolution used by the legacy
    /// DemoInitializer runtime so the slice screens render consistently with the arena debug UI.
    /// </summary>
    internal static class GreyboxUi
    {
        public static Font LoadFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                   Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        public static GameObject CreateCanvas(string name, int sortingOrder)
        {
            var canvasObject = new GameObject(name);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
            return canvasObject;
        }

        public static Image CreateBackdrop(Transform parent, string name, Color color)
        {
            var backdropObject = new GameObject(name);
            backdropObject.transform.SetParent(parent, false);
            var image = backdropObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            var rect = backdropObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        public static Text CreateText(Transform parent, string name, Font font, int fontSize, Color color, TextAnchor alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Anchors a rect to the top-center of its parent at the given y offset from the top.</summary>
        public static void PlaceTopCentered(RectTransform rect, float yOffset, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -yOffset);
            rect.sizeDelta = new Vector2(width, height);
        }

        public static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Font font,
            float yOffset,
            float width,
            float height,
            UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.06f, 0.10f, 0.15f, 0.96f);

            var button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);

            var rect = buttonObject.GetComponent<RectTransform>();
            PlaceTopCentered(rect, yOffset, width, height);

            Text labelText = CreateText(buttonObject.transform, name + "_Label", font, 24, Color.white, TextAnchor.MiddleCenter);
            var labelRect = labelText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(24f, 4f);
            labelRect.offsetMax = new Vector2(-24f, -4f);
            labelText.text = label;
            labelText.raycastTarget = false;

            return button;
        }
    }
}
