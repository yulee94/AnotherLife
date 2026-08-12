using UnityEngine;
using UnityEngine.SceneManagement;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.RealmSelection;
using System.Collections;
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

        [Header("Realm Heraldry")]
        [SerializeField] private Sprite _stoneholdEmblem;
        [SerializeField] private Sprite _eldergroveEmblem;
        [SerializeField] private Sprite _crownlandsEmblem;
        [SerializeField] private Sprite _umbralEmblem;

        private bool _selectionInProgress;
        private GameObject _commitOverlayObject;
        private Image _commitBackdrop;
        private Image _commitAccentLine;
        private Image _commitEmblemGlow;
        private Image _commitEmblem;
        private Image _commitProgressFill;
        private Text _commitTitleText;
        private Text _commitRealmText;
        private Text _commitMetaText;

        private void Start()
        {
            EnsurePresentationCamera();
            Bootloader.InitializeIfMissing();
            PopulateRealms();
        }

        private static Camera EnsurePresentationCamera()
        {
            Camera main = Camera.main;
            if (IsDisplayPresentationCamera(main))
            {
                return main;
            }

            Camera[] activeCameras = Camera.allCameras;
            for (int i = 0; i < activeCameras.Length; i++)
            {
                if (IsDisplayPresentationCamera(activeCameras[i]))
                {
                    return activeCameras[i];
                }
            }

            var cameraObject = new GameObject("RealmSelectionCamera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.014f, 0.018f, 0.025f, 1f);
            camera.cullingMask = 0;
            camera.depth = -100f;
            camera.orthographic = true;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.allowDynamicResolution = false;
            camera.useOcclusionCulling = false;
            return camera;
        }

        private static bool IsDisplayPresentationCamera(Camera camera)
        {
            return camera != null &&
                   camera.isActiveAndEnabled &&
                   camera.targetTexture == null &&
                   camera.targetDisplay == 0;
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
            if (_selectionInProgress)
            {
                return;
            }

            Debug.Log($"Realm Selected in UI: {id}");
            StartCoroutine(RealmSelectionCommitRoutine(id));
        }

        private IEnumerator RealmSelectionCommitRoutine(RealmId id)
        {
            _selectionInProgress = true;
            float catalogWait = 0f;
            while (RealmCatalogRuntime.Status == RealmCatalogRuntimeStatus.Loading && catalogWait < 10f)
            {
                catalogWait += Time.unscaledDeltaTime;
                yield return null;
            }

            var realmService = ServiceLocator.Get<IRealmService>();
            RealmSelectionResult result = realmService.TrySelectRealm(
                new RealmSelectionRequest(System.Guid.NewGuid().ToString("N"), id));
            if (!result.AllowsNavigation)
            {
                _selectionInProgress = false;
                Debug.LogError(result.TechnicalCode);
                yield break;
            }

            ShowCommitOverlay(id);

            const float duration = 0.72f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                UpdateCommitOverlay(id, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            SceneManager.LoadScene(_nextScene);
        }

        private void BuildFallbackRealmUi(IEnumerable<RealmDefinition> realms)
        {
            var canvasObject = new GameObject("RealmSelectionCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var background = new GameObject("Background");
            background.transform.SetParent(canvasObject.transform, false);
            var backgroundImage = background.AddComponent<Image>();
            backgroundImage.color = new Color(0.014f, 0.018f, 0.025f, 1f);
            var backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            var atmosphere = canvasObject.AddComponent<RealmSelectionFallbackAtmosphere>();
            atmosphere.Bind(BuildAtmosphericBackdrop(canvasObject.transform));

            var safeAreaObject = new GameObject("SafeArea", typeof(RectTransform));
            safeAreaObject.transform.SetParent(canvasObject.transform, false);
            var safeAreaRect = safeAreaObject.GetComponent<RectTransform>();
            safeAreaRect.anchorMin = Vector2.zero;
            safeAreaRect.anchorMax = Vector2.one;
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                       Resources.GetBuiltinResource<Font>("Arial.ttf");

            var topRule = CreatePanel(safeAreaObject.transform, "TopRule", new Color(0.88f, 0.62f, 0.24f, 0.72f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(-96f, 4f));
            var bottomRule = CreatePanel(safeAreaObject.transform, "BottomRule", new Color(0.22f, 0.45f, 0.72f, 0.42f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 58f), new Vector2(-160f, 3f));
            atmosphere.AddPulseTarget(topRule, 0.72f, 0.96f, 0.52f);
            atmosphere.AddPulseTarget(bottomRule, 0.36f, 0.62f, 0.33f);

            var title = CreateText(safeAreaObject.transform, "Title", font, "ANOTHER LIFE", 42, new Vector2(0f, -18f), new Vector2(-48f, 52f));
            StretchAcrossTop(title);
            title.color = new Color(1f, 0.88f, 0.62f);
            var subtitle = CreateText(safeAreaObject.transform, "Subtitle", font, "Choose the realm that will define your command style.", 21, new Vector2(0f, -70f), new Vector2(-48f, 34f));
            StretchAcrossTop(subtitle);
            subtitle.color = new Color(0.78f, 0.86f, 0.94f);

            var cardsObject = new GameObject("RealmCards", typeof(RectTransform));
            cardsObject.transform.SetParent(safeAreaObject.transform, false);
            var cardsRect = cardsObject.GetComponent<RectTransform>();
            cardsRect.anchorMin = Vector2.zero;
            cardsRect.anchorMax = Vector2.one;
            cardsRect.offsetMin = new Vector2(24f, 88f);
            cardsRect.offsetMax = new Vector2(-24f, -148f);
            var grid = cardsObject.AddComponent<GridLayoutGroup>();
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;

            foreach (var realm in realms)
            {
                CreateRealmButton(cardsObject.transform, font, realm);
            }

            var safeAreaDriver = canvasObject.AddComponent<RealmSelectionSafeAreaDriver>();
            safeAreaDriver.Bind(safeAreaRect, cardsRect, grid);
        }

        private void CreateRealmButton(Transform parent, Font font, RealmDefinition realm)
        {
            var buttonObject = new GameObject(realm.RealmName);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            Color realmColor = GetRealmColor(realm.Id);
            image.color = new Color(0.030f, 0.039f, 0.052f, 0.92f);
            var outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = Color.Lerp(realmColor, Color.white, 0.18f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            var button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(() => OnRealmSelected(realm.Id));
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(image.color, realmColor, 0.22f);
            colors.pressedColor = Color.Lerp(image.color, Color.black, 0.28f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);

            CreateGradientPanel(buttonObject.transform, "CardDepth", new Color(0.055f, 0.067f, 0.086f, 0.88f), new Color(0.014f, 0.018f, 0.026f, 0.62f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            CreatePanel(buttonObject.transform, "CardTopTrace", new Color(1f, 0.90f, 0.66f, 0.18f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -1f), new Vector2(-34f, 1.8f));
            CreatePanel(buttonObject.transform, "CardBottomTrace", new Color(realmColor.r, realmColor.g, realmColor.b, 0.20f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(-34f, 1.5f));
            CreatePanel(buttonObject.transform, "RealmAccent", realmColor, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 0f));
            CreateRealmEmblem(buttonObject.transform, realm.Id, realmColor);

            var profile = CreateText(buttonObject.transform, realm.RealmName + "_Profile", font, GetRealmCommandProfile(realm.Id), 13, new Vector2(30f, -16f), new Vector2(260f, 18f));
            AnchorTopLeft(profile, new Vector2(30f, -16f), new Vector2(260f, 18f));
            profile.alignment = TextAnchor.UpperLeft;
            profile.color = new Color(1f, 0.84f, 0.52f);

            var realmName = CreateText(buttonObject.transform, realm.RealmName + "_Name", font, realm.RealmName.ToUpperInvariant(), 25, new Vector2(30f, -38f), new Vector2(500f, 32f));
            AnchorTopStretch(realmName, new Vector2(30f, -38f), 210f, 32f);
            realmName.alignment = TextAnchor.UpperLeft;
            realmName.color = Color.Lerp(realmColor, Color.white, 0.46f);

            var text = CreateText(buttonObject.transform, realm.RealmName + "_Text", font, realm.Description, 17, new Vector2(30f, -76f), new Vector2(585f, 70f));
            AnchorTopStretch(text, new Vector2(30f, -76f), 175f, 70f);
            text.alignment = TextAnchor.UpperLeft;
            text.color = new Color(0.84f, 0.88f, 0.92f);

            Image selectSurface = CreatePanel(
                buttonObject.transform,
                realm.RealmName + "_SelectButtonSurface",
                new Color(0.73f, 0.58f, 0.33f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 14f),
                new Vector2(-48f, 54f));
            var selectOutline = selectSurface.gameObject.AddComponent<Outline>();
            selectOutline.effectColor = new Color(0.93f, 0.90f, 0.82f, 0.70f);
            selectOutline.effectDistance = new Vector2(1.5f, -1.5f);

            Text selectText = CreateText(
                selectSurface.transform,
                realm.RealmName + "_Select",
                font,
                "SELECT " + realm.RealmName.ToUpperInvariant(),
                18,
                Vector2.zero,
                Vector2.zero);
            StretchToParent(selectText);
            selectText.fontStyle = FontStyle.Bold;
            selectText.color = new Color(0.027f, 0.045f, 0.063f, 1f);
        }

        private void ShowCommitOverlay(RealmId id)
        {
            EnsureCommitOverlay();
            _commitOverlayObject.SetActive(true);
            UpdateCommitOverlay(id, 0f);
        }

        private void EnsureCommitOverlay()
        {
            if (_commitOverlayObject != null)
            {
                return;
            }

            var canvasObject = new GameObject("RealmSelectionCommitCanvas");
            _commitOverlayObject = canvasObject;
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                       Resources.GetBuiltinResource<Font>("Arial.ttf");

            _commitBackdrop = CreatePanel(canvasObject.transform, "CommitBackdrop", new Color(0f, 0f, 0f, 0f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _commitBackdrop.raycastTarget = true;

            var panel = CreateGradientPanel(canvasObject.transform, "CommitPanel", new Color(0.052f, 0.064f, 0.084f, 0.98f), new Color(0.010f, 0.014f, 0.020f, 0.98f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 236f));
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.80f, 0.48f, 0.34f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            var shadow = panel.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.54f);
            shadow.effectDistance = new Vector2(0f, -6f);

            CreatePanel(panel.transform, "CommitTopTrace", new Color(1f, 0.86f, 0.56f, 0.32f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -1f), new Vector2(-42f, 2f));
            _commitAccentLine = CreatePanel(panel.transform, "CommitAccentLine", Color.white, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 0f));

            _commitEmblemGlow = CreatePanel(panel.transform, "CommitEmblemGlow", Color.clear, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(82f, -84f), new Vector2(88f, 88f));
            _commitEmblemGlow.preserveAspect = true;
            _commitEmblem = CreatePanel(panel.transform, "CommitEmblem", Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(82f, -84f), new Vector2(72f, 72f));
            _commitEmblem.preserveAspect = true;

            _commitTitleText = CreateText(panel.transform, "CommitTitle", font, "COMMAND ACCEPTED", 15, new Vector2(0f, -30f), new Vector2(640f, 24f));
            _commitTitleText.color = new Color(1f, 0.84f, 0.52f);
            _commitRealmText = CreateText(panel.transform, "CommitRealm", font, string.Empty, 34, new Vector2(0f, -66f), new Vector2(640f, 44f));
            _commitMetaText = CreateText(panel.transform, "CommitMeta", font, string.Empty, 16, new Vector2(0f, -116f), new Vector2(640f, 30f));
            _commitMetaText.color = new Color(0.80f, 0.88f, 0.94f);

            CreatePanel(panel.transform, "CommitProgressTrack", new Color(0.08f, 0.10f, 0.13f, 0.96f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 42f), new Vector2(620f, 6f));
            _commitProgressFill = CreatePanel(panel.transform, "CommitProgressFill", new Color(1f, 0.80f, 0.42f, 0.94f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(-310f, 42f), new Vector2(0f, 6f));

            _commitOverlayObject.SetActive(false);
        }

        private void UpdateCommitOverlay(RealmId id, float progress)
        {
            Color realmColor = GetRealmColor(id);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 7.0f) * 0.5f;

            if (_commitBackdrop != null)
            {
                _commitBackdrop.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.15f, 0.72f, eased));
            }

            if (_commitAccentLine != null)
            {
                _commitAccentLine.color = new Color(realmColor.r, realmColor.g, realmColor.b, Mathf.Lerp(0.68f, 1f, pulse));
            }

            Sprite emblem = GetRealmEmblem(id);
            if (_commitEmblemGlow != null)
            {
                _commitEmblemGlow.sprite = emblem;
                _commitEmblemGlow.color = new Color(realmColor.r, realmColor.g, realmColor.b, Mathf.Lerp(0.12f, 0.22f, pulse));
            }

            if (_commitEmblem != null)
            {
                _commitEmblem.sprite = emblem;
                _commitEmblem.color = Color.Lerp(realmColor, Color.white, 0.52f);
            }

            if (_commitProgressFill != null)
            {
                _commitProgressFill.color = Color.Lerp(realmColor, new Color(1f, 0.84f, 0.52f), 0.25f);
                _commitProgressFill.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(0f, 620f, eased), 6f);
            }

            if (_commitRealmText != null)
            {
                _commitRealmText.text = GetRealmDisplayName(id).ToUpperInvariant();
                _commitRealmText.color = Color.Lerp(realmColor, Color.white, 0.34f);
            }

            if (_commitMetaText != null)
            {
                _commitMetaText.text = GetRealmCommandProfile(id) + " // KINGDOM LINK ESTABLISHING";
            }
        }

        private static List<Image> BuildAtmosphericBackdrop(Transform parent)
        {
            var animated = new List<Image>(32);
            CreateGradientPanel(parent, "AtmosphereWash", new Color(0.030f, 0.044f, 0.064f, 1f), new Color(0.006f, 0.008f, 0.012f, 1f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            CreateGradientPanel(parent, "WarTableFalloff", new Color(0.016f, 0.026f, 0.038f, 0.18f), new Color(0.82f, 0.52f, 0.22f, 0.18f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(0f, 420f));
            CreatePanel(parent, "DistantRidge", new Color(0.020f, 0.026f, 0.032f, 0.92f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 134f), new Vector2(1560f, 92f));
            CreatePanel(parent, "ForwardRidge", new Color(0.010f, 0.014f, 0.018f, 0.96f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 78f), new Vector2(1920f, 110f));

            for (int i = 0; i < 9; i++)
            {
                float x = -720f + i * 180f;
                float height = 36f + (i % 3) * 22f;
                CreatePanel(parent, "CitadelSilhouette_" + i, new Color(0.008f, 0.011f, 0.015f, 0.96f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, 120f), new Vector2(64f, height));
            }

            for (int i = 0; i < 28; i++)
            {
                float x = -900f + (i * 137f) % 1800f;
                float y = -170f - (i * 83f) % 710f;
                float size = 2.5f + i % 4;
                float alpha = 0.10f + (i % 5) * 0.025f;
                var ember = CreatePanel(parent, "AtmosphereEmber_" + i, new Color(1f, 0.68f, 0.32f, alpha), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(size, size));
                animated.Add(ember);
            }

            return animated;
        }

        private void CreateRealmEmblem(Transform parent, RealmId id, Color realmColor)
        {
            Sprite emblem = GetRealmEmblem(id);
            if (emblem == null)
            {
                Debug.LogError($"Realm Selection is missing the approved emblem for {id}.");
                return;
            }

            var glow = CreatePanel(parent, "RealmEmblemGlow", new Color(realmColor.r, realmColor.g, realmColor.b, 0.18f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-74f, -50f), new Vector2(98f, 98f));
            glow.sprite = emblem;
            glow.preserveAspect = true;

            var image = CreatePanel(parent, "RealmEmblem", Color.Lerp(realmColor, Color.white, 0.52f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-74f, -50f), new Vector2(78f, 78f));
            image.sprite = emblem;
            image.preserveAspect = true;
        }

        private Sprite GetRealmEmblem(RealmId id)
        {
            return id switch
            {
                RealmId.Stonehold => _stoneholdEmblem,
                RealmId.Eldergrove => _eldergroveEmblem,
                RealmId.Crownlands => _crownlandsEmblem,
                RealmId.Umbral => _umbralEmblem,
                _ => null
            };
        }

        private static void AnchorTopLeft(Text text, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (text == null)
            {
                return;
            }

            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void AnchorTopStretch(Text text, Vector2 anchoredPosition, float rightInset, float height)
        {
            if (text == null)
            {
                return;
            }

            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(-anchoredPosition.x - rightInset, height);
        }

        private static void StretchToParent(Text text)
        {
            if (text == null)
            {
                return;
            }

            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void StretchAcrossTop(Text text)
        {
            if (text == null)
            {
                return;
            }

            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
        }

        private static Image CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);
            var image = panelObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return image;
        }

        private static Image CreateGradientPanel(Transform parent, string name, Color topColor, Color bottomColor, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var image = CreatePanel(parent, name, Color.white, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
            image.sprite = CreateVerticalGradientSprite(name + "_Sprite", topColor, bottomColor);
            return image;
        }

        private static Sprite CreateVerticalGradientSprite(string name, Color topColor, Color bottomColor)
        {
            const int width = 8;
            const int height = 64;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name + "_Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < height; y++)
            {
                float t = y / (height - 1f);
                Color color = Color.Lerp(bottomColor, topColor, t);
                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
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
            text.raycastTarget = false;

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

        private static string GetRealmCommandProfile(RealmId id)
        {
            return id switch
            {
                RealmId.Stonehold => "FORTRESS ECONOMY",
                RealmId.Eldergrove => "GROWTH ENGINE",
                RealmId.Crownlands => "ROYAL COMMAND",
                RealmId.Umbral => "SHADOW WARFARE",
                _ => "COMMAND PROFILE"
            };
        }

        private static string GetRealmDisplayName(RealmId id)
        {
            return id == RealmId.None ? "Unclaimed Realm" : id.ToString();
        }

        private sealed class RealmSelectionFallbackAtmosphere : MonoBehaviour
        {
            private readonly List<EmberTarget> _embers = new();
            private readonly List<PulseTarget> _pulseTargets = new();

            public void Bind(List<Image> embers)
            {
                _embers.Clear();
                if (embers == null)
                {
                    return;
                }

                for (int i = 0; i < embers.Count; i++)
                {
                    var ember = embers[i];
                    if (ember != null)
                    {
                        _embers.Add(new EmberTarget(ember, ember.rectTransform.anchoredPosition));
                    }
                }
            }

            public void AddPulseTarget(Image image, float minAlpha, float maxAlpha, float speed)
            {
                if (image == null)
                {
                    return;
                }

                _pulseTargets.Add(new PulseTarget(image, minAlpha, maxAlpha, speed));
            }

            private void Update()
            {
                float time = Time.unscaledTime;
                for (int i = 0; i < _embers.Count; i++)
                {
                    _embers[i].Apply(time, i);
                }

                for (int i = 0; i < _pulseTargets.Count; i++)
                {
                    _pulseTargets[i].Apply(time);
                }
            }

            private readonly struct EmberTarget
            {
                private readonly Image _image;
                private readonly Vector2 _basePosition;

                public EmberTarget(Image image, Vector2 basePosition)
                {
                    _image = image;
                    _basePosition = basePosition;
                }

                public void Apply(float time, int index)
                {
                    if (_image == null)
                    {
                        return;
                    }

                    float drift = Mathf.Sin(time * (0.24f + index * 0.011f) + index) * 11f;
                    float lift = Mathf.Repeat(time * (6f + index % 5) + index * 23f, 840f);
                    _image.rectTransform.anchoredPosition = new Vector2(_basePosition.x + drift, -900f + lift);

                    Color color = _image.color;
                    color.a = 0.06f + Mathf.PingPong(time * (0.09f + index * 0.006f) + index * 0.17f, 0.16f);
                    _image.color = color;
                }
            }

            private readonly struct PulseTarget
            {
                private readonly Image _image;
                private readonly float _minAlpha;
                private readonly float _maxAlpha;
                private readonly float _speed;

                public PulseTarget(Image image, float minAlpha, float maxAlpha, float speed)
                {
                    _image = image;
                    _minAlpha = minAlpha;
                    _maxAlpha = maxAlpha;
                    _speed = speed;
                }

                public void Apply(float time)
                {
                    if (_image == null)
                    {
                        return;
                    }

                    Color color = _image.color;
                    float pulse = 0.5f + Mathf.Sin(time * _speed) * 0.5f;
                    color.a = Mathf.Lerp(_minAlpha, _maxAlpha, pulse);
                    _image.color = color;
                }
            }
        }
    }
}
