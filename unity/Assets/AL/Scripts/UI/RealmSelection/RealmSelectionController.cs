using UnityEngine;
using UnityEngine.SceneManagement;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using System.Collections.Generic;
using UnityEngine.UI;

namespace AL.UI.RealmSelection
{
    public class RealmSelectionController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private RealmSelectionCard _cardPrefab;
        [SerializeField] private Transform _container;
        [SerializeField] private string _nextScene = "Kingdom";

        private void Start()
        {
            Bootloader.InitializeIfMissing();
            PopulateRealms();
        }

        private void PopulateRealms()
        {
            var dataService = ServiceLocator.Get<IGameDataService>();
            var realms = dataService.GetAllRealms();

            if (_cardPrefab == null || _container == null)
            {
                BuildFallbackRealmUi(realms);
                return;
            }

            foreach (var realm in realms)
            {
                var card = Instantiate(_cardPrefab, _container);
                card.Setup(realm, OnRealmSelected);
            }
        }

        private void OnRealmSelected(RealmId id)
        {
            Debug.Log($"Realm Selected in UI: {id}");
            var realmService = ServiceLocator.Get<IRealmService>();
            realmService.SelectRealm(id);

            SceneManager.LoadScene(_nextScene);
        }

        private void BuildFallbackRealmUi(IEnumerable<RealmDefinition> realms)
        {
            var canvasObject = new GameObject("RealmSelectionCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();

            var background = new GameObject("Background");
            background.transform.SetParent(canvasObject.transform, false);
            var backgroundImage = background.AddComponent<Image>();
            backgroundImage.color = new Color(0.07f, 0.08f, 0.10f, 1f);
            var backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                       Resources.GetBuiltinResource<Font>("Arial.ttf");

            CreateText(canvasObject.transform, "Title", font, "Choose Your Realm", 34, new Vector2(0, -40), new Vector2(900, 80));

            int index = 0;
            foreach (var realm in realms)
            {
                CreateRealmButton(canvasObject.transform, font, realm, index);
                index++;
            }
        }

        private void CreateRealmButton(Transform parent, Font font, RealmDefinition realm, int index)
        {
            var buttonObject = new GameObject(realm.RealmName);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = GetRealmColor(realm.Id);

            var button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(() => OnRealmSelected(realm.Id));

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -140 - index * 115);
            rect.sizeDelta = new Vector2(820, 96);

            var label = $"{realm.RealmName}\n{realm.Description}";
            var text = CreateText(buttonObject.transform, realm.RealmName + "_Text", font, label, 20, Vector2.zero, rect.sizeDelta);
            text.alignment = TextAnchor.MiddleCenter;
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16, 8);
            textRect.offsetMax = new Vector2(-16, -8);
        }

        private static Text CreateText(Transform parent, string name, Font font, string textValue, int fontSize, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.text = textValue;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return text;
        }

        private static Color GetRealmColor(RealmId id)
        {
            return id switch
            {
                RealmId.Stonehold => new Color(0.38f, 0.34f, 0.30f, 1f),
                RealmId.Eldergrove => new Color(0.18f, 0.44f, 0.28f, 1f),
                RealmId.Crownlands => new Color(0.18f, 0.28f, 0.58f, 1f),
                RealmId.Umbral => new Color(0.18f, 0.08f, 0.20f, 1f),
                _ => new Color(0.20f, 0.20f, 0.22f, 1f)
            };
        }
    }
}
