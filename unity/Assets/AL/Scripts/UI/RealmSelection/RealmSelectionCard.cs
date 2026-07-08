using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AL.Core;
using AL.Data.Definitions;
using System;
using UnityEngine.EventSystems;

namespace AL.UI.RealmSelection
{
    public class RealmSelectionCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Button _selectButton;

        private RealmDefinition _definition;
        private Action<RealmId> _onSelected;
        private Vector3 _baseScale = Vector3.one;
        private Image _backgroundImage;
        private Image _accentBar;
        private Image _topTrace;
        private Image _bottomTrace;
        private Image _sigilGlow;
        private Color _realmColor = Color.white;
        private bool _hovered;
        private bool _pressed;
        private float _pulseSeed;

        public void Setup(RealmDefinition definition, Action<RealmId> onSelected)
        {
            _definition = definition;
            _onSelected = onSelected;
            _realmColor = GetRealmColor(definition != null ? definition.Id : RealmId.None);
            _pulseSeed = Mathf.Abs((definition?.RealmName ?? name).GetHashCode() % 997) * 0.01f;
            _baseScale = transform.localScale;

            ApplyRuntimePolish();

            if (_nameText != null)
            {
                _nameText.text = definition != null ? definition.RealmName.ToUpperInvariant() : "UNKNOWN REALM";
                _nameText.color = Color.Lerp(_realmColor, Color.white, 0.42f);
                _nameText.fontStyle = FontStyles.UpperCase;
                _nameText.enableWordWrapping = false;
            }

            if (_descriptionText != null)
            {
                _descriptionText.text = definition != null ? definition.Description : string.Empty;
                _descriptionText.color = new Color(0.84f, 0.89f, 0.94f, 0.94f);
                _descriptionText.enableWordWrapping = true;
            }

            if (_iconImage != null)
            {
                _iconImage.color = Color.Lerp(_realmColor, Color.white, 0.18f);
                _iconImage.raycastTarget = false;
            }

            if (definition != null && definition.Icon != null && _iconImage != null)
            {
                _iconImage.sprite = definition.Icon;
            }

            BindSelectionButton();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
        }

        private void Update()
        {
            float targetScale = _pressed ? 0.985f : _hovered ? 1.018f : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, _baseScale * targetScale, Time.unscaledDeltaTime * 10f);

            float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 1.18f + _pulseSeed) * 0.5f;
            SetImageAlpha(_accentBar, Mathf.Lerp(_hovered ? 0.78f : 0.52f, _hovered ? 0.98f : 0.70f, pulse));
            SetImageAlpha(_topTrace, Mathf.Lerp(0.16f, _hovered ? 0.46f : 0.26f, pulse));
            SetImageAlpha(_bottomTrace, Mathf.Lerp(0.20f, _hovered ? 0.60f : 0.36f, pulse));
            SetImageAlpha(_sigilGlow, Mathf.Lerp(_hovered ? 0.22f : 0.12f, _hovered ? 0.42f : 0.24f, pulse));
        }

        private void ApplyRuntimePolish()
        {
            _backgroundImage = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            _backgroundImage.color = new Color(0.026f, 0.034f, 0.046f, 0.96f);
            _backgroundImage.raycastTarget = true;

            var outline = GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            outline.effectColor = Color.Lerp(_realmColor, Color.white, 0.20f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);

            var shadow = GetPlainShadow() ?? gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
            shadow.effectDistance = new Vector2(0f, -4f);

            _accentBar = EnsurePanel("RuntimeRealmAccent", _realmColor, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(7f, 0f));
            _topTrace = EnsurePanel("RuntimeTopTrace", new Color(1f, 0.88f, 0.62f, 0.24f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -1f), new Vector2(-26f, 2f));
            _bottomTrace = EnsurePanel("RuntimeBottomTrace", new Color(_realmColor.r, _realmColor.g, _realmColor.b, 0.32f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(-26f, 2f));
            _sigilGlow = EnsurePanel("RuntimeSigilGlow", new Color(_realmColor.r, _realmColor.g, _realmColor.b, 0.20f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-56f, -48f), new Vector2(80f, 80f));
            _sigilGlow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private void BindSelectionButton()
        {
            if (_selectButton == null)
            {
                _selectButton = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            }

            _selectButton.onClick.RemoveAllListeners();
            _selectButton.onClick.AddListener(HandleSelection);

            var colors = _selectButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.Lerp(Color.white, _realmColor, 0.12f);
            colors.pressedColor = Color.Lerp(Color.white, Color.black, 0.12f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.10f;
            _selectButton.colors = colors;
        }

        private void HandleSelection()
        {
            if (_definition == null)
            {
                return;
            }

            _onSelected?.Invoke(_definition.Id);
        }

        private Image EnsurePanel(string panelName, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            Transform existing = transform.Find(panelName);
            GameObject panelObject = existing != null ? existing.gameObject : new GameObject(panelName);
            panelObject.transform.SetParent(transform, false);
            panelObject.transform.SetAsFirstSibling();

            var image = panelObject.GetComponent<Image>() ?? panelObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return image;
        }

        private Shadow GetPlainShadow()
        {
            var shadows = GetComponents<Shadow>();
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null && shadows[i].GetType() == typeof(Shadow))
                {
                    return shadows[i];
                }
            }

            return null;
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            Color color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
        }

        private static Color GetRealmColor(RealmId id)
        {
            return id switch
            {
                RealmId.Stonehold => new Color(0.72f, 0.58f, 0.40f, 1f),
                RealmId.Eldergrove => new Color(0.28f, 0.78f, 0.44f, 1f),
                RealmId.Crownlands => new Color(0.34f, 0.58f, 1f, 1f),
                RealmId.Umbral => new Color(0.68f, 0.26f, 0.92f, 1f),
                _ => new Color(0.80f, 0.84f, 0.90f, 1f)
            };
        }
    }
}
