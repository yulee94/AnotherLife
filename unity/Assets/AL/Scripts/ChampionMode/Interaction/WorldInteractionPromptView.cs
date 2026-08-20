using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AL.ChampionMode.Interaction
{
    /// <summary>
    /// Screen-space BDO-readable prompt. Never uses OnGUI.
    /// </summary>
    public sealed class WorldInteractionPromptView : MonoBehaviour
    {
        public const string CanvasName = "WorldInteractionPrompt";
        public const string PlateName = "PromptPlate";
        public const string GlyphName = "PromptGlyph";
        public const string LabelName = "PromptLabel";

        private GameObject _root;
        private Text _label;
        private Button _button;
        private UnityAction _onConfirm;

        public bool IsVisible => _root != null && _root.activeSelf;
        public string CurrentCopy => _label != null ? _label.text : string.Empty;

        public static WorldInteractionPromptView Create(Transform parent, UnityAction onConfirm)
        {
            var host = new GameObject(CanvasName);
            if (parent != null)
            {
                host.transform.SetParent(parent, false);
            }

            var view = host.AddComponent<WorldInteractionPromptView>();
            view._onConfirm = onConfirm;
            view.Build();
            view.Hide();
            return view;
        }

        public void Show(string copy)
        {
            if (_root == null)
            {
                return;
            }

            if (_label != null)
            {
                _label.text = copy ?? string.Empty;
            }

            _root.SetActive(!string.IsNullOrEmpty(copy));
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }

            if (_label != null)
            {
                _label.text = string.Empty;
            }
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                        Resources.GetBuiltinResource<Font>("Arial.ttf");

            _root = new GameObject(PlateName);
            _root.transform.SetParent(transform, false);
            var plate = _root.AddComponent<Image>();
            plate.color = new Color(0.012f, 0.016f, 0.022f, 0.92f);
            var outline = _root.AddComponent<Outline>();
            outline.effectColor = new Color(0.82f, 0.70f, 0.38f, 0.72f);
            outline.effectDistance = new Vector2(1.6f, -1.6f);
            var plateRect = _root.GetComponent<RectTransform>();
            plateRect.anchorMin = new Vector2(0.5f, 0f);
            plateRect.anchorMax = new Vector2(0.5f, 0f);
            plateRect.pivot = new Vector2(0.5f, 0f);
            plateRect.anchoredPosition = new Vector2(0f, 168f);
            plateRect.sizeDelta = new Vector2(640f, 64f);

            var glyph = new GameObject(GlyphName);
            glyph.transform.SetParent(_root.transform, false);
            var glyphImage = glyph.AddComponent<Image>();
            glyphImage.color = new Color(0.78f, 0.64f, 0.30f, 0.96f);
            var glyphRect = glyph.GetComponent<RectTransform>();
            glyphRect.anchorMin = new Vector2(0f, 0.5f);
            glyphRect.anchorMax = new Vector2(0f, 0.5f);
            glyphRect.pivot = new Vector2(0f, 0.5f);
            glyphRect.anchoredPosition = new Vector2(14f, 0f);
            glyphRect.sizeDelta = new Vector2(42f, 42f);
            var glyphTextObject = new GameObject("GlyphText");
            glyphTextObject.transform.SetParent(glyph.transform, false);
            var glyphText = glyphTextObject.AddComponent<Text>();
            glyphText.font = font;
            glyphText.fontSize = 26;
            glyphText.fontStyle = FontStyle.Bold;
            glyphText.alignment = TextAnchor.MiddleCenter;
            glyphText.color = new Color(0.08f, 0.06f, 0.03f, 1f);
            glyphText.text = WorldInteractionPromptCopy.InteractGlyph;
            glyphText.raycastTarget = false;
            var glyphTextRect = glyphTextObject.GetComponent<RectTransform>();
            glyphTextRect.anchorMin = Vector2.zero;
            glyphTextRect.anchorMax = Vector2.one;
            glyphTextRect.offsetMin = Vector2.zero;
            glyphTextRect.offsetMax = Vector2.zero;

            var labelObject = new GameObject(LabelName);
            labelObject.transform.SetParent(_root.transform, false);
            _label = labelObject.AddComponent<Text>();
            _label.font = font;
            _label.fontSize = 28;
            _label.fontStyle = FontStyle.Bold;
            _label.alignment = TextAnchor.MiddleLeft;
            _label.color = new Color(0.93f, 0.88f, 0.74f, 1f);
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.raycastTarget = false;
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(70f, 6f);
            labelRect.offsetMax = new Vector2(-16f, -6f);

            _button = _root.AddComponent<Button>();
            _button.targetGraphic = plate;
            if (_onConfirm != null)
            {
                _button.onClick.AddListener(_onConfirm);
            }
        }
    }
}
