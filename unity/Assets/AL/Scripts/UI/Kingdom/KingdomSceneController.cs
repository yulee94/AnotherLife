using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.Kingdom;
using AL.Kingdom.Visuals;
using AL.Narrative.Nvs01;
using AL.Narrative.Nvs01.Contracts;
using AL.Services.Local;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AL.Input;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace AL.UI.Kingdom
{
    public class KingdomSceneController : MonoBehaviour
    {
        private Text _realmText;
        private Text _resourceText;
        private Text _buildingText;
        private Text _troopText;
        private Text _researchText;
        private Text _questText;
        private Text _territoryText;
        private Text _battleText;
        private Text _messageHeaderText;
        private Text _messageMetaText;
        private Text _messageStatusText;
        private Text _messageText;
        private Text _boardHintText;
        private Image _messagePanelImage;
        private Image _messageAccent;
        private Image _messageTopRule;
        private Image _messageBottomRule;
        private Image _messageWash;
        private Image _messageStatusPlate;
        private Image _messageStatusRule;
        private GameObject _dashboardRoot;
        private Text _dashboardToggleText;
        private Text _commandDeckAuthorityStatus;
        private Text _privateKingdomStatusText;
        private Text _privateKingdomTimerText;
        private Text _privateKingdomMapText;
        private GameObject _constructionDockRoot;
        private GameObject _privateMapRoot;
        private KingdomVisualizer _kingdomVisualizer;
        private Nvs01KingdomPresenter _nvs01Presenter;
        private Nvs01KingdomView _nvs01View;
        private Transform _nvs01ActionRoot;
        private readonly List<Button> _nvs01ActionButtons = new List<Button>();
        private bool _nvs01CatalogLoading;
        private KingdomGreyboxDuelHost _greyboxDuelHost;
        private Color _messageAccentBaseColor = new Color(0.42f, 0.62f, 0.78f, 0.92f);
        private Color _messagePanelBaseColor = new Color(0.020f, 0.027f, 0.037f, 0.92f);
        private Color _messageWashBaseColor = new Color(0.28f, 0.56f, 0.78f, 0.05f);
        private Color _messageSignalBaseColor = new Color(0.42f, 0.62f, 0.78f, 0.30f);
        private float _messagePulseTimer;
        private bool _dashboardVisible = true;
        private bool _profileReady;
        private bool _runtimeInitialized;
        private bool _profileMutationPresentationCaptured;
        private ProfileMutationPresentationState _profileMutationPresentation;
        private long _lastLiveRefreshTimestamp;
        private readonly List<Image> _messageSignalBars = new List<Image>();
        private readonly Text[] _readinessChipTexts = new Text[4];
        private readonly Image[] _readinessChipPanels = new Image[4];
        private readonly Image[] _readinessChipRails = new Image[4];
        private readonly Image[] _readinessChipGlows = new Image[4];
        private readonly Color[] _readinessChipAccents = new Color[4];
        private readonly float[] _readinessChipUrgencies = new float[4];
        private readonly Text[] _resourceChipTexts = new Text[8];
        private readonly Image[] _resourceChipPanels = new Image[8];
        private readonly Image[] _resourceChipRails = new Image[8];
        private readonly Image[] _resourceChipGlows = new Image[8];
        private readonly Color[] _resourceChipAccents = new Color[8];
        private readonly float[] _resourceChipWeights = new float[8];

        private readonly string[] _buildingIds =
        {
            "TownHall", "Farm", "LumberMill", "Quarry", "GoldMine", "ManaShrine", "Mine", "Barracks"
        };

        private void OnEnable()
        {
            CityLayoutEngine.OnBuildingSelected += HandleBuildingSelected;
            KingdomVisualizer.OnTerritorySelected += HandleTerritorySelected;
        }

        private void OnDisable()
        {
            CityLayoutEngine.OnBuildingSelected -= HandleBuildingSelected;
            KingdomVisualizer.OnTerritorySelected -= HandleTerritorySelected;
        }

        private void Start()
        {
            // #178 containment: the controller no longer bootstraps the service stack or loads a
            // profile itself. The committed Kingdom scene carries its own Bootloader owner root that
            // performs the single marker-validated load (#241). This controller only consumes the
            // already-ready profile; a controller-owned InitializeIfMissing()/Load() would be a second
            // load that re-applies offline progress.
            _profileReady = TryConsumeReadyProfile();

            BuildRuntimeWorld();
            BuildRuntimeUi();
            Refresh();
            _runtimeInitialized = true;
            StartCoroutine(InitializeNvs01QuestPresentation());
            _lastLiveRefreshTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        // Reads the merged #241 offline-stack marker to decide whether a ready profile exists. It never
        // triggers a load: if the scene's Bootloader owner has not completed the single load, this
        // returns false and the read-only UI renders an honest unavailable state instead.
        private static bool TryConsumeReadyProfile()
        {
            if (!ServiceLocator.TryGet<IOfflineServiceStackMarker>(out var marker))
            {
                return false;
            }

            if (marker.LoadState != OfflineStackLoadState.Succeeded)
            {
                return false;
            }

            return marker.TryGetExpected<ISaveGameService>(out var saveGameService) &&
                   saveGameService.CurrentSave != null;
        }

        private void Update()
        {
            // Start can fail before the runtime-generated UI exists (for example while recovering a
            // presentation camera during a scene transition). Never turn one initialization failure
            // into a per-frame null-reference loop against fields that were not constructed.
            if (!_runtimeInitialized)
            {
                return;
            }

            UpdateCommandMessagePulse();
            UpdateStrategicReadinessPulse();
            UpdateResourceTickerPulse();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                keyboard.bKey.wasPressedThisFrame &&
                string.Equals(gameObject.scene.name, "Kingdom", StringComparison.Ordinal))
            {
                ToggleConstructionDock();
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (_profileReady && now > _lastLiveRefreshTimestamp)
            {
                _lastLiveRefreshTimestamp = now;
                Refresh();
            }
        }

        private void BuildRuntimeWorld()
        {
            Scene controllerScene = gameObject.scene;
            ConfigureKingdomCamera(controllerScene);
            ConfigureKingdomLighting(controllerScene);

            var visualizerObject = new GameObject("Kingdom_2_5D_Board");
            _kingdomVisualizer = visualizerObject.AddComponent<KingdomVisualizer>();
            _kingdomVisualizer.InitializeKingdom();
        }

        private static void ConfigureKingdomCamera(Scene controllerScene)
        {
            // Cache Camera.main once and use Unity's overloaded null comparison explicitly. A camera
            // from the scene being unloaded can be a CLR-non-null Unity "fake null"; `??` would retain
            // that destroyed component and throw MissingComponentException when configured.
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            GameObject cameraObject = camera != null &&
                                      camera.gameObject.scene == controllerScene
                ? camera.gameObject
                : FindMainCameraObject(controllerScene);

            if (cameraObject == null)
            {
                cameraObject = new GameObject("Main Camera");
            }

            cameraObject.tag = "MainCamera";
            if (camera == null || camera.gameObject != cameraObject)
            {
                camera = cameraObject.GetComponent<UnityEngine.Camera>();
            }

            if (camera == null)
            {
                camera = cameraObject.AddComponent<UnityEngine.Camera>();
            }

            camera.orthographic = true;
            camera.orthographicSize = 7.5f;
            camera.transform.position = new Vector3(0f, 10.4f, -7.3f);
            camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.020f, 0.026f, 0.034f);

            if (cameraObject.GetComponent<AudioListener>() == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }

            KingdomBoardCameraController controls =
                cameraObject.GetComponent<KingdomBoardCameraController>();
            if (controls == null)
            {
                controls = cameraObject.AddComponent<KingdomBoardCameraController>();
            }

            controls.Configure(camera);
        }

        private static GameObject FindMainCameraObject(Scene controllerScene)
        {
            GameObject[] candidates = GameObject.FindGameObjectsWithTag("MainCamera");
            for (int i = 0; i < candidates.Length; i++)
            {
                GameObject candidate = candidates[i];
                if (candidate != null && candidate.scene == controllerScene)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void ConfigureKingdomLighting(Scene controllerScene)
        {
            RenderSettings.ambientLight = new Color(0.20f, 0.22f, 0.24f);
            GameObject lightObject = GameObject.Find("Kingdom_KeyLight");
            if (lightObject == null || lightObject.scene != controllerScene)
            {
                lightObject = new GameObject("Kingdom_KeyLight");
            }

            Light light = lightObject.GetComponent<Light>();
            if (light == null)
            {
                light = lightObject.AddComponent<Light>();
            }

            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.92f, 0.78f);
            lightObject.transform.rotation = Quaternion.Euler(48f, -36f, 0f);
        }

        private void BuildRuntimeUi()
        {
            BuildPrivateKingdomRuntimeUi();
            return;

#pragma warning disable CS0162
            CaptureProfileMutationPresentationOnce();

            var canvas = CreateCanvas("KingdomCanvas");
            var font = GetDefaultFont();

            var background = new GameObject("Kingdom_Backdrop");
            background.transform.SetParent(canvas.transform, false);
            var bg = background.AddComponent<Image>();
            bg.color = new Color(0.012f, 0.016f, 0.022f, 0.26f);
            Stretch(background.GetComponent<RectTransform>());

            _dashboardRoot = new GameObject("KingdomDashboardRoot");
            _dashboardRoot.transform.SetParent(canvas.transform, false);
            Stretch(_dashboardRoot.AddComponent<RectTransform>());

            var topBar = CreatePanel(_dashboardRoot.transform, "CommandTopBar", new Vector2(32f, -24f), new Vector2(1180f, 106f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.030f, 0.040f, 0.052f, 0.86f));
            CreatePanel(topBar.transform, "TopBarAccent", new Vector2(0f, 0f), new Vector2(6f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Color(0.86f, 0.62f, 0.30f, 0.86f));
            CreatePanel(topBar.transform, "TopBarRule", new Vector2(0f, -1f), new Vector2(-36f, 2f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Color(1f, 0.88f, 0.58f, 0.20f));
            _realmText = CreateText(topBar.transform, "RealmText", font, 28, TextAnchor.UpperLeft, new Vector2(20f, -14f), new Vector2(1080f, 34f));
            _realmText.color = new Color(1f, 0.92f, 0.76f);
            _resourceText = CreateText(topBar.transform, "ResourceText", font, 11, TextAnchor.UpperLeft, new Vector2(20f, -47f), new Vector2(676f, 14f));
            _resourceText.text = "TREASURY";
            _resourceText.color = new Color(0.58f, 0.68f, 0.78f);
            CreateResourceTicker(topBar.transform, font);
            CreateStrategicReadinessConsole(topBar.transform, font);

            _buildingText = CreatePanelText(_dashboardRoot.transform, "DistrictPanel", "BuildingText", font, 18, TextAnchor.UpperLeft, new Vector2(32f, -150f), new Vector2(520f, 292f));
            _troopText = CreatePanelText(_dashboardRoot.transform, "ForcesPanel", "TroopText", font, 18, TextAnchor.UpperLeft, new Vector2(32f, -460f), new Vector2(520f, 214f));
            _researchText = CreatePanelText(_dashboardRoot.transform, "ResearchPanel", "ResearchText", font, 18, TextAnchor.UpperLeft, new Vector2(584f, -150f), new Vector2(454f, 174f));
            _questText = CreatePanelText(_dashboardRoot.transform, "QuestPanel", "QuestText", font, 17, TextAnchor.UpperLeft, new Vector2(584f, -342f), new Vector2(454f, 166f));
            ConfigureNvs01QuestPanel();
            _territoryText = CreatePanelText(_dashboardRoot.transform, "TerritoryPanel", "TerritoryText", font, 17, TextAnchor.UpperLeft, new Vector2(584f, -526f), new Vector2(454f, 194f));
            _battleText = CreatePanelText(_dashboardRoot.transform, "BattlePanel", "BattleText", font, 17, TextAnchor.UpperLeft, new Vector2(584f, -738f), new Vector2(454f, 170f));

            var messagePanel = CreatePanel(_dashboardRoot.transform, "CommandMessagePanel", new Vector2(32f, 32f), new Vector2(1008f, 118f), Vector2.zero, Vector2.zero, Vector2.zero, _messagePanelBaseColor);
            _messagePanelImage = messagePanel.GetComponent<Image>();
            _messageWash = CreatePanel(messagePanel.transform, "CommandMessageWash", new Vector2(6f, -26f), new Vector2(996f, 70f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), _messageWashBaseColor).GetComponent<Image>();
            _messageAccent = CreatePanel(messagePanel.transform, "CommandMessageAccent", new Vector2(0f, 0f), new Vector2(6f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Color(0.88f, 0.62f, 0.30f, 0.92f)).GetComponent<Image>();
            _messageTopRule = CreatePanel(messagePanel.transform, "CommandMessageTopRule", new Vector2(0f, -1f), new Vector2(-34f, 2f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Color(1f, 0.86f, 0.54f, 0.20f)).GetComponent<Image>();
            _messageBottomRule = CreatePanel(messagePanel.transform, "CommandMessageBottomRule", new Vector2(0f, 1f), new Vector2(-34f, 2f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Color(0.28f, 0.56f, 0.78f, 0.18f)).GetComponent<Image>();
            _messageHeaderText = CreateText(messagePanel.transform, "MessageHeaderText", font, 13, TextAnchor.UpperLeft, new Vector2(18f, -10f), new Vector2(380f, 20f));
            _messageHeaderText.text = "COMMAND DOSSIER";
            _messageHeaderText.color = new Color(0.78f, 0.86f, 0.94f);
            _messageMetaText = CreateText(messagePanel.transform, "MessageMetaText", font, 13, TextAnchor.UpperRight, new Vector2(648f, -10f), new Vector2(336f, 20f));
            _messageMetaText.color = new Color(0.54f, 0.66f, 0.76f);
            _messageStatusPlate = CreatePanel(messagePanel.transform, "CommandStatusPlate", new Vector2(18f, -42f), new Vector2(124f, 32f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.10f, 0.14f, 0.18f, 0.86f)).GetComponent<Image>();
            _messageStatusRule = CreatePanel(_messageStatusPlate.transform, "CommandStatusPlateRule", new Vector2(0f, 0f), new Vector2(3f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Color(0.88f, 0.62f, 0.30f, 0.62f)).GetComponent<Image>();
            _messageStatusText = CreateText(_messageStatusPlate.transform, "MessageStatusText", font, 13, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(116f, 28f));
            _messageStatusText.text = "ONLINE";
            _messageStatusText.color = new Color(1f, 0.88f, 0.62f);
            var statusRect = _messageStatusText.GetComponent<RectTransform>();
            statusRect.anchorMin = Vector2.zero;
            statusRect.anchorMax = Vector2.one;
            statusRect.pivot = new Vector2(0.5f, 0.5f);
            statusRect.offsetMin = new Vector2(6f, 0f);
            statusRect.offsetMax = new Vector2(-6f, 0f);
            CreateCommandSignalBars(messagePanel.transform);
            _messageText = CreateText(messagePanel.transform, "MessageText", font, 19, TextAnchor.UpperLeft, new Vector2(158f, -36f), new Vector2(826f, 66f));
            _messageText.color = new Color(0.92f, 0.96f, 1f);
            _messageText.verticalOverflow = VerticalWrapMode.Truncate;

            var commandDeck = CreatePanel(_dashboardRoot.transform, "CommandDeck", new Vector2(-28f, -78f), new Vector2(430f, 936f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Color(0.026f, 0.033f, 0.044f, 0.92f));
            CreatePanel(commandDeck.transform, "CommandDeckAccent", new Vector2(0f, 0f), new Vector2(6f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Color(0.86f, 0.62f, 0.30f, 0.88f));
            CreatePanel(commandDeck.transform, "CommandDeckTopRule", new Vector2(0f, -1f), new Vector2(-30f, 2f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Color(1f, 0.86f, 0.54f, 0.18f));
            var commandTitle = CreateText(commandDeck.transform, "CommandDeckTitle", font, 23, TextAnchor.UpperLeft, new Vector2(18f, -16f), new Vector2(380f, 34f));
            commandTitle.text = "COMMAND DECK";
            commandTitle.color = new Color(1f, 0.88f, 0.62f);
            _commandDeckAuthorityStatus = CreateText(
                commandDeck.transform,
                "CommandDeckAuthorityStatus",
                font,
                9,
                TextAnchor.UpperRight,
                new Vector2(202f, -13f),
                new Vector2(194f, 36f));
            _commandDeckAuthorityStatus.text =
                _profileMutationPresentation.DisplayText;
            _commandDeckAuthorityStatus.color =
                _profileMutationPresentation.IsReadOnly
                    ? new Color(1f, 0.74f, 0.38f, 1f)
                    : new Color(0.54f, 0.88f, 0.66f, 1f);
            _commandDeckAuthorityStatus.resizeTextForBestFit = true;
            _commandDeckAuthorityStatus.resizeTextMinSize = 6;
            _commandDeckAuthorityStatus.resizeTextMaxSize = 9;
            _commandDeckAuthorityStatus.verticalOverflow =
                VerticalWrapMode.Truncate;

            CreateCommandDeck(commandDeck.transform, font);

            var toggle = CreateButton(canvas.transform, font, "Board View", new Vector2(-24f, -24f), ToggleDashboard, new Vector2(170f, 42f), new Color(0.075f, 0.095f, 0.122f, 0.96f));
            _dashboardToggleText = toggle.GetComponentInChildren<Text>();
            _boardHintText = CreateBoardHintText(canvas.transform, font);
            SetMessage("Command board online. Select a district or border outpost to inspect yield, readiness, and next order.");
            RefreshBoardHintVisibility();
#pragma warning restore CS0162
        }

        private void BuildPrivateKingdomRuntimeUi()
        {
            CaptureProfileMutationPresentationOnce();
            Canvas canvas = CreateCanvas("KingdomCanvas");
            Font font = AL.UI.Presentation.PresentationChrome.ResolveFont(18);
            Color ink = AL.UI.Presentation.PresentationChrome.Ink;
            Color muted = AL.UI.Presentation.PresentationChrome.InkMuted;
            Color plate = new Color(0.025f, 0.030f, 0.038f, 0.86f);
            Color accent = GetCurrentRealmAccent();

            _dashboardRoot = new GameObject("PrivateKingdomHud");
            _dashboardRoot.transform.SetParent(canvas.transform, false);
            Stretch(_dashboardRoot.AddComponent<RectTransform>());

            GameObject top = CreatePanel(
                _dashboardRoot.transform,
                "PrivateKingdomTopBar",
                new Vector2(28f, -24f),
                new Vector2(1220f, 96f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                plate);
            CreatePanel(
                top.transform,
                "RealmHeraldryRail",
                Vector2.zero,
                new Vector2(7f, 0f),
                Vector2.zero,
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                accent);
            _realmText = CreateText(
                top.transform,
                "RealmText",
                font,
                20,
                TextAnchor.UpperLeft,
                new Vector2(22f, -10f),
                new Vector2(500f, 30f));
            _realmText.color = ink;
            _resourceText = CreateText(
                top.transform,
                "ResourceText",
                font,
                11,
                TextAnchor.UpperLeft,
                new Vector2(22f, -38f),
                new Vector2(420f, 18f));
            _resourceText.color = muted;
            CreateResourceTicker(top.transform, font);

            GameObject timerStrip = CreatePanel(
                top.transform,
                "PrivateKingdomTimerStrip",
                new Vector2(692f, -18f),
                new Vector2(168f, 54f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.014f, 0.020f, 0.030f, 0.92f));
            CreatePanel(
                timerStrip.transform,
                "TimerRail",
                Vector2.zero,
                new Vector2(4f, 0f),
                Vector2.zero,
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                accent);
            _privateKingdomTimerText = CreateText(
                timerStrip.transform,
                "PrivateKingdomTimerText",
                font,
                12,
                TextAnchor.MiddleLeft,
                new Vector2(12f, 0f),
                new Vector2(146f, 50f));
            _privateKingdomTimerText.color = ink;
            _privateKingdomTimerText.text = PrivateKingdomHudTimer.Format(
                Array.Empty<BuildingState>(),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            _privateKingdomStatusText = CreateText(
                top.transform,
                "PrivateKingdomStatus",
                font,
                15,
                TextAnchor.MiddleRight,
                new Vector2(878f, -18f),
                new Vector2(316f, 54f));
            _privateKingdomStatusText.color = Color.Lerp(accent, Color.white, 0.38f);
            _privateKingdomStatusText.text = ResolvePrivateKingdomStatus(false);

            _buildingText = CreatePanelText(
                _dashboardRoot.transform,
                "CastleSummary",
                "BuildingText",
                font,
                17,
                TextAnchor.UpperLeft,
                new Vector2(28f, -140f),
                new Vector2(350f, 238f));
            _questText = CreatePanelText(
                _dashboardRoot.transform,
                "QuestPanel",
                "QuestText",
                font,
                16,
                TextAnchor.UpperLeft,
                new Vector2(28f, -396f),
                new Vector2(350f, 226f));
            ConfigureNvs01QuestPanel();

            _messageText = CreatePanelText(
                _dashboardRoot.transform,
                "KingdomNotice",
                "MessageText",
                font,
                16,
                TextAnchor.UpperLeft,
                new Vector2(28f, 92f),
                new Vector2(820f, 82f));
            RectTransform noticeRect = _messageText.transform.parent.GetComponent<RectTransform>();
            noticeRect.anchorMin = Vector2.zero;
            noticeRect.anchorMax = Vector2.zero;
            noticeRect.pivot = Vector2.zero;
            noticeRect.anchoredPosition = new Vector2(28f, 92f);
            _messageText.color = ink;

            CreatePrivateKingdomDock(_dashboardRoot.transform, font, accent);
            CreateConstructionDock(canvas.transform, font, accent);
            CreatePrivateMapPreview(canvas.transform, font, accent);
            SetMessage("Your private castle is ready. Press B to inspect the construction dock.");
        }

        private void CreatePrivateKingdomDock(Transform parent, Font font, Color accent)
        {
            GameObject dock = CreatePanel(
                parent,
                "PrivateKingdomDock",
                new Vector2(-28f, 34f),
                new Vector2(720f, 68f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Color(0.025f, 0.030f, 0.038f, 0.92f));
            CreateDockButton(dock.transform, font, "CITY", 0, () => SetMessage("Private castle view centered on your Town Hall."), accent, true);
            CreateDockButton(dock.transform, font, "CONSTRUCT  [B]", 1, ToggleConstructionDock, accent, true);
            CreateDockButton(dock.transform, font, "RESEARCH", 2, () => SetMessage("Research is locked until the kingdom research contract is approved."), accent, false);
            CreateDockButton(dock.transform, font, "TROOPS", 3, () => SetMessage("Troop management is locked until the force roster is approved."), accent, false);
            CreateDockButton(dock.transform, font, "ADVISORS", 4, () => SetMessage("Advisors are locked until the court roster is approved."), accent, false);
            CreateDockButton(dock.transform, font, "MAP", 5, TogglePrivateMap, accent, true);
            CreateDockButton(dock.transform, font, "SHARED MENU", 6, OpenSharedMenu, accent, true);
        }

        private static void CreateDockButton(
            Transform parent,
            Font font,
            string label,
            int index,
            UnityEngine.Events.UnityAction action,
            Color accent,
            bool interactable)
        {
            Button button = AL.UI.Presentation.PresentationChrome.CreateHit(
                parent,
                label.Replace(" ", string.Empty),
                interactable
                    ? Color.Lerp(new Color(0.055f, 0.065f, 0.078f, 0.98f), accent, 0.14f)
                    : new Color(0.040f, 0.044f, 0.050f, 0.82f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(10f + index * 101f, 0f),
                new Vector2(94f, 48f));
            button.interactable = interactable;
            button.onClick.AddListener(action);
            Text text = AL.UI.Presentation.PresentationChrome.CreateLabel(
                button.transform,
                "Label",
                font,
                interactable ? label : label + "\nLOCKED",
                interactable ? 12 : 10,
                interactable
                    ? AL.UI.Presentation.PresentationChrome.Ink
                    : AL.UI.Presentation.PresentationChrome.InkFaint,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 8;
        }

        private void CreateConstructionDock(Transform parent, Font font, Color accent)
        {
            _constructionDockRoot = CreatePanel(
                parent,
                "ConstructionDock",
                new Vector2(0f, 116f),
                new Vector2(560f, 210f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Color(0.025f, 0.030f, 0.038f, 0.97f));
            CreatePanel(
                _constructionDockRoot.transform,
                "ConstructionRail",
                Vector2.zero,
                new Vector2(7f, 0f),
                Vector2.zero,
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                accent);
            Text heading = CreateText(
                _constructionDockRoot.transform,
                "ConstructionHeading",
                font,
                22,
                TextAnchor.UpperLeft,
                new Vector2(24f, -18f),
                new Vector2(500f, 34f));
            heading.text = "TOWN HALL CONSTRUCTION";
            heading.color = AL.UI.Presentation.PresentationChrome.Ink;
            Text body = CreateText(
                _constructionDockRoot.transform,
                "ConstructionBody",
                font,
                15,
                TextAnchor.UpperLeft,
                new Vector2(24f, -58f),
                new Vector2(508f, 72f));
            body.text = "One approved build is available. The order writes through the save authority and changes the central castle model.";
            body.color = AL.UI.Presentation.PresentationChrome.InkMuted;
            Button build = AL.UI.Presentation.PresentationChrome.CreateHit(
                _constructionDockRoot.transform,
                "ConstructTownHall",
                Color.Lerp(new Color(0.09f, 0.10f, 0.12f, 1f), accent, 0.28f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(24f, 20f),
                new Vector2(236f, 52f));
            build.onClick.AddListener(ConstructTownHall);
            AL.UI.Presentation.PresentationChrome.CreateLabel(
                build.transform,
                "Label",
                font,
                "CONSTRUCT TOWN HALL",
                16,
                AL.UI.Presentation.PresentationChrome.Ink,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            _constructionDockRoot.SetActive(false);
        }

        private void CreatePrivateMapPreview(Transform parent, Font font, Color accent)
        {
            _privateMapRoot = CreatePanel(
                parent,
                "PrivateKingdomMapPreview",
                new Vector2(-28f, -276f),
                new Vector2(410f, 260f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Color(0.025f, 0.030f, 0.038f, 0.96f));
            CreatePanel(
                _privateMapRoot.transform,
                "MapRail",
                Vector2.zero,
                new Vector2(7f, 0f),
                Vector2.zero,
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                accent);
            _privateKingdomMapText = CreateText(
                _privateMapRoot.transform,
                "PrivateMapText",
                font,
                16,
                TextAnchor.UpperLeft,
                new Vector2(24f, -20f),
                new Vector2(356f, 216f));
            _privateKingdomMapText.color = AL.UI.Presentation.PresentationChrome.Ink;
            _privateMapRoot.SetActive(false);
        }

        // Read-only refresh. The controller's own direct panel reads here are non-seeding: they never
        // create domain entities as a side effect of rendering (#178 defect 2). Panels whose only data
        // source is a state-seeding getter render a technical, layout-stable UNAVAILABLE state (D8)
        // rather than calling that getter from read-only UI; DISTRICTS/RESEARCH still show their real
        // (frozen) state through the non-seeding GetAll* reads (D7).
        //
        // GetResourceCount/GetCredits (resource ticker + WAR chip) run wallet/credit normalization
        // that is a strict no-op on a canonical loaded save and never calls Save(); the visualizer and
        // district panel both consume immutable building snapshots and seed no domain entities.
        private void Refresh()
        {
            RefreshPrivateKingdomHud();
            return;

#pragma warning disable CS0162
            _kingdomVisualizer?.RefreshVisuals();

            if (!_profileReady)
            {
                RenderProfileUnavailable();
                return;
            }

            var realm = ServiceLocator.Get<IRealmService>().CurrentRealm;
            _realmText.text = realm == null
                ? "ANOTHERLIFE COMMAND"
                : $"{realm.RealmName.ToUpperInvariant()} COMMAND";

            var resources = ServiceLocator.Get<IResourceService>();
            var selectedRealmId = ServiceLocator.Get<IRealmService>().CurrentRealmId;
            ResourceType rareResourceType = ResourceRules.GetRareResourceForRealm(selectedRealmId);
            int warzoneCredits = ServiceLocator.Get<IWarzoneCreditService>().GetCredits();
            _resourceText.text = "TREASURY / " + FormatResourceLabel(rareResourceType) + " ROUTE";
            RefreshResourceTicker(resources, rareResourceType, warzoneCredits);
            RefreshStrategicReadiness();

            RefreshDistrictsPanel();
            RefreshResearchPanel();

            // FORCES/WAR ZONE still depend on state-seeding getters. OBJECTIVES is now owned by the
            // packet-backed NVS-01 presenter and never reads the legacy state-seeding quest service.
            _troopText.text = BuildUnavailablePanel("FORCES");
            RefreshNvs01QuestPanel();
            _territoryText.text = BuildUnavailablePanel("WAR ZONE");
#pragma warning restore CS0162
        }

        private void RefreshPrivateKingdomHud()
        {
            _kingdomVisualizer?.RefreshVisuals();
            if (!_profileReady)
            {
                RenderProfileUnavailable();
                SetPanelText(
                    _privateKingdomTimerText,
                    "BUILD TIMER\nUNAVAILABLE");
                SetPanelText(
                    _privateKingdomStatusText,
                    "PRIVATE CASTLE\nPROFILE UNAVAILABLE");
                return;
            }

            RealmId realmId = ServiceLocator.Get<IRealmService>().CurrentRealmId;
            var realm = ServiceLocator.Get<IRealmService>().CurrentRealm;
            string realmName = realm == null ? realmId.ToString() : realm.RealmName;
            _realmText.text = realmName.ToUpperInvariant() + " PRIVATE KINGDOM";
            _resourceText.text = "TREASURY / LIVE PROFILE";

            IResourceService resources = ServiceLocator.Get<IResourceService>();
            ResourceType rare = ResourceRules.GetRareResourceForRealm(realmId);
            SetResourceChip(0, "FOOD", resources.GetResourceCount(ResourceType.Food), new Color(0.56f, 0.86f, 0.48f, 1f), 0.36f);
            SetResourceChip(1, "WOOD", resources.GetResourceCount(ResourceType.Wood), new Color(0.74f, 0.58f, 0.36f, 1f), 0.34f);
            SetResourceChip(2, "STONE", resources.GetResourceCount(ResourceType.Stone), new Color(0.62f, 0.72f, 0.80f, 1f), 0.32f);
            SetResourceChip(3, "GOLD", resources.GetResourceCount(ResourceType.Gold), new Color(0.96f, 0.76f, 0.34f, 1f), 0.42f);
            SetResourceChip(4, "MANA", resources.GetResourceCount(ResourceType.ManaStone), new Color(0.48f, 0.78f, 1f, 1f), 0.48f);
            SetResourceChip(5, "ORE", resources.GetResourceCount(ResourceType.Ore), new Color(0.52f, 0.60f, 0.70f, 1f), 0.34f);
            SetResourceChip(6, FormatResourceLabel(rare), resources.GetResourceCount(rare), GetCurrentRealmAccent(), 0.60f);

            BuildingState[] buildingStates = ServiceLocator.Get<IBuildingService>()
                .GetAllBuildingStates()
                .Where(state => state != null)
                .ToArray();
            KingdomBuildingPresentation townHall =
                KingdomBuildingPresentationResolver.Resolve(
                        realmId,
                        buildingStates)
                    .FirstOrDefault(item => item != null && item.BuildingId == "TownHall");
            bool constructed = townHall != null &&
                               townHall.Status == KingdomBuildingPresentationStatus.Built &&
                               townHall.ConfirmedLevel > 0;
            SetResourceChip(
                7,
                "HALL",
                constructed ? townHall.ConfirmedLevel : 0,
                constructed ? GetCurrentRealmAccent() : new Color(0.56f, 0.58f, 0.60f, 1f),
                constructed ? 0.72f : 0.30f);
            SetPanelText(
                _privateKingdomTimerText,
                PrivateKingdomHudTimer.Format(
                    buildingStates,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

            _buildingText.text =
                "CASTLE DOMAIN\n\n" +
                (constructed
                    ? $"Town Hall  Lv {townHall.ConfirmedLevel}  /  BUILT\n"
                    : "Town Hall  /  CONSTRUCTION READY\n") +
                "Architecture  /  REALM-SPECIALIZED\n" +
                "Districts  /  POPULATED SILHOUETTES\n\n" +
                "Press B to open construction.";
            SetPanelText(
                _privateKingdomStatusText,
                ResolvePrivateKingdomStatus(constructed));
            RefreshNvs01QuestPanel();

            if (_privateMapRoot != null && _privateMapRoot.activeSelf)
            {
                RefreshPrivateMapText(realmId);
            }
        }

        private void ToggleConstructionDock()
        {
            if (_constructionDockRoot == null)
            {
                return;
            }

            bool open = !_constructionDockRoot.activeSelf;
            _constructionDockRoot.SetActive(open);
            if (open && _privateMapRoot != null)
            {
                _privateMapRoot.SetActive(false);
            }
        }

        private string ResolvePrivateKingdomStatus(bool constructed)
        {
            if (_profileMutationPresentation.IsReadOnly)
            {
                return "CASTLE READ-ONLY\n" +
                       (_profileMutationPresentation.DisplayText ?? string.Empty)
                           .Replace("COMMAND DECK", "CASTLE");
            }

            return constructed
                ? "CASTLE ESTABLISHED\nSAVE VERIFIED"
                : "CASTLE CLAIMED\nONE BUILD AVAILABLE";
        }

        private void ConstructTownHall()
        {
            ServiceLocator.TryGet<ISaveGameService>(out ISaveGameService save);
            ServiceLocator.TryGet<IGameDataService>(out IGameDataService gameData);
            KingdomOneBuildResult result = KingdomOneBuildCommand.TryExecute(save, gameData);
            SetMessage(result.Message);
            _constructionDockRoot?.SetActive(false);
            RefreshPrivateKingdomHud();
        }

        private void TogglePrivateMap()
        {
            if (_privateMapRoot == null)
            {
                return;
            }

            bool open = !_privateMapRoot.activeSelf;
            _privateMapRoot.SetActive(open);
            if (!open)
            {
                return;
            }

            _constructionDockRoot?.SetActive(false);
            RealmId realm = RealmId.None;
            if (ServiceLocator.TryGet<IRealmService>(out IRealmService realmService))
            {
                realm = realmService.CurrentRealmId;
            }
            RefreshPrivateMapText(realm);
        }

        private void RefreshPrivateMapText(RealmId realm)
        {
            IReadOnlyList<string> destinations =
                AL.UI.SharedMenu.PrivateKingdomInnerDestinations
                    .EnumerateCastleAndAreas(realm);
            if (AL.UI.SharedMenu.PrivateKingdomInnerDestinations.ContainsForbidden(destinations))
            {
                _privateKingdomMapText.text =
                    "PRIVATE KINGDOM MAP\n\nDESTINATIONS UNAVAILABLE\nFail-closed destination policy.";
                return;
            }

            string realmName = realm == RealmId.None ? "Inner Realm" : realm.ToString();
            if (ServiceLocator.TryGet<IRealmService>(out IRealmService realmService) &&
                realmService.CurrentRealm != null &&
                realmService.CurrentRealmId == realm)
            {
                realmName = realmService.CurrentRealm.RealmName;
            }

            _privateKingdomMapText.text =
                "PRIVATE KINGDOM MAP\n\n" +
                "CASTLE  /  " + realmName + " Castle\n" +
                "AREA I  /  " + realmName + " Area I\n" +
                "AREA II /  " + realmName + " Area II\n\n" +
                "Inner-realm destinations only.";
        }

        private void OpenSharedMenu()
        {
            AL.UI.SharedMenu.SharedMenuModeSwitchHost host =
                AL.UI.SharedMenu.SharedMenuModeSwitchHost.EnsureForScene(gameObject.scene);
            if (host == null)
            {
                SetMessage("Shared Menu is unavailable in this scene.");
                return;
            }

            host.Open();
        }

        private void RefreshDistrictsPanel()
        {
            var buildings = ServiceLocator.Get<IBuildingService>();
            RealmId realmId = ServiceLocator.Get<IRealmService>().CurrentRealmId;
            Dictionary<string, KingdomBuildingPresentation> snapshot =
                KingdomBuildingPresentationResolver
                    .Resolve(realmId, buildings.GetAllBuildingStates())
                    .ToDictionary(item => item.BuildingId, StringComparer.Ordinal);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var builder = new StringBuilder();
            builder.AppendLine("DISTRICTS");
            foreach (var buildingId in _buildingIds)
            {
                if (snapshot.TryGetValue(buildingId, out var state) && state != null)
                {
                    if (state.Status == KingdomBuildingPresentationStatus.InvalidState)
                    {
                        builder.AppendLine(
                            $"{FormatBuildingName(buildingId),-12}  --  DATA UNAVAILABLE");
                        continue;
                    }

                    string timer = state.IsUpgrading
                        ? $"UPGRADING {Math.Max(0, state.UpgradeCompleteTimestamp - now)}s"
                        : state.ConfirmedLevel == 0
                            ? "UNBUILT"
                            : "READY";
                    builder.AppendLine(
                        $"{FormatBuildingName(buildingId),-12}  Lv {state.ConfirmedLevel}  {timer}");
                }
                else
                {
                    builder.AppendLine($"{FormatBuildingName(buildingId),-12}  --  UNAVAILABLE");
                }
            }

            _buildingText.text = builder.ToString();
        }

        private void RefreshResearchPanel()
        {
            var research = ServiceLocator.Get<IResearchService>();
            Dictionary<string, ResearchState> snapshot = BuildResearchSnapshot(research);
            snapshot.TryGetValue("Steel Forging", out var steel);
            snapshot.TryGetValue("Plate Armor", out var armor);

            // The derived Attack/Defense stat-bonus lines are dropped: the only API for them
            // (IResearchService.GetStatBonus) internally seeds research state, and the bonus formula is
            // domain-owned (#165/#183), not the controller's to reproduce. Levels and the real frozen
            // research timer remain visible (D7).
            _researchText.text =
                "RESEARCH\n" +
                FormatResearch("Steel Forging", steel) + "\n" +
                FormatResearch("Plate Armor", armor);
        }

        private static Dictionary<string, ResearchState> BuildResearchSnapshot(IResearchService research)
        {
            var snapshot = new Dictionary<string, ResearchState>(StringComparer.Ordinal);
            foreach (var state in research.GetAllResearchStates())
            {
                if (state != null && !string.IsNullOrEmpty(state.ResearchId) && !snapshot.ContainsKey(state.ResearchId))
                {
                    snapshot[state.ResearchId] = state;
                }
            }

            return snapshot;
        }

        private void RenderProfileUnavailable()
        {
            if (_realmText != null)
            {
                _realmText.text = "ANOTHERLIFE COMMAND";
            }

            if (_resourceText != null)
            {
                _resourceText.text = "TREASURY";
            }

            SetPanelText(_buildingText, BuildUnavailablePanel("DISTRICTS"));
            SetPanelText(_troopText, BuildUnavailablePanel("FORCES"));
            SetPanelText(_researchText, BuildUnavailablePanel("RESEARCH"));
            SetPanelText(_questText, BuildUnavailablePanel("OBJECTIVES"));
            SetPanelText(_territoryText, BuildUnavailablePanel("WAR ZONE"));
            ClearNvs01ActionButtons();
        }

        private static void SetPanelText(Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static string BuildUnavailablePanel(string header)
        {
            return header + "\n\nTEMPORARILY UNAVAILABLE\nLive data pending domain contract.";
        }

        private void ConfigureNvs01QuestPanel()
        {
            if (_questText == null)
            {
                return;
            }

            var textRect = _questText.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(textRect.sizeDelta.x, 82f);

            var actionRoot = new GameObject("Nvs01QuestActions");
            actionRoot.transform.SetParent(_questText.transform.parent, false);
            Stretch(actionRoot.AddComponent<RectTransform>());
            _nvs01ActionRoot = actionRoot.transform;
            _questText.text = BuildNvs01LoadingPanel();
        }

        private IEnumerator InitializeNvs01QuestPresentation()
        {
            if (!_profileReady)
            {
                _nvs01CatalogLoading = false;
                _nvs01Presenter = null;
                _nvs01View = null;
                SetPanelText(_questText, BuildUnavailablePanel("OBJECTIVES"));
                ClearNvs01ActionButtons();
                yield break;
            }

            _nvs01CatalogLoading = true;
            RefreshNvs01QuestPanel();

            Nvs01CatalogLoadResult result = null;
            yield return Nvs01CatalogLoader.Shared.LoadOnce(value => result = value);

            _nvs01CatalogLoading = false;
            if (result != null && result.IsSuccess)
            {
                InitializeNvs01Presenter(result.VerifiedCatalog);
                yield break;
            }

            RenderNvs01CatalogUnavailable(result?.Diagnostics.FirstOrDefault());
        }

        private void InitializeNvs01Presenter(Nvs01VerifiedCatalog verifiedCatalog)
        {
            if (verifiedCatalog == null)
            {
                RenderNvs01CatalogUnavailable(null);
                return;
            }

            if (!ServiceLocator.TryGet<ISaveGameService>(out var saveGameService) ||
                saveGameService.CurrentSave == null ||
                !(saveGameService is ISaveGameCandidateStore candidateStore))
            {
                _nvs01Presenter = null;
                RenderNvs01View(
                    Nvs01KingdomView.PersistenceUnavailable(null),
                    true);
                return;
            }

            if (!Nvs01ProgressCodec.TryDecode(
                    saveGameService.CurrentSave.Nvs01Progress,
                    verifiedCatalog,
                    out Nvs01QuestSnapshot initialSnapshot,
                    out Nvs01RuntimeDiagnostic persistenceDiagnostic))
            {
                _nvs01Presenter = null;
                RenderNvs01View(
                    Nvs01KingdomView.PersistenceUnavailable(
                        persistenceDiagnostic?.Code),
                    true);
                return;
            }

            var runtime = new Nvs01QuestRuntime(
                verifiedCatalog,
                initialSnapshot,
                new Nvs01SaveGameMutationCommitter(
                    candidateStore,
                    verifiedCatalog),
                () => Guid.NewGuid().ToString("D"));
            _nvs01Presenter = new Nvs01KingdomPresenter(
                runtime,
                ResolveNvs01RealmContext,
                () => BuildNvs01CapabilitySnapshot(verifiedCatalog),
                () => Guid.NewGuid().ToString("D"),
                () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            if (!_profileReady)
            {
                _nvs01View = null;
                SetPanelText(_questText, BuildUnavailablePanel("OBJECTIVES"));
                ClearNvs01ActionButtons();
                return;
            }

            RenderNvs01View(_nvs01Presenter.Present(), true);
        }

        private static Nvs01RealmContext ResolveNvs01RealmContext()
        {
            try
            {
                if (!ServiceLocator.TryGet<IRealmService>(out var realmService) || realmService == null)
                {
                    return Nvs01RealmContext.Unavailable();
                }

                return Nvs01RealmContextAdapter.FromCommittedIdentity(realmService.Identity);
            }
            catch (Exception)
            {
                return Nvs01RealmContext.Unavailable();
            }
        }

        private static Nvs01CapabilitySnapshot BuildNvs01CapabilitySnapshot(
            Nvs01VerifiedCatalog verifiedCatalog) =>
            // No CH1 consumer is approved or mounted. A future consumer must
            // register its exact typed capability and current packet identity;
            // scene/catalog ID coincidence never grants capability authority.
            Nvs01MountedConsumerRegistry.Empty.Capture(verifiedCatalog);

        private void RenderNvs01CatalogUnavailable(Nvs01CatalogDiagnostic diagnostic)
        {
            _nvs01Presenter = null;
            RenderNvs01View(Nvs01KingdomView.CatalogUnavailable(diagnostic), true);
        }

        private void RefreshNvs01QuestPanel()
        {
            if (_questText == null)
            {
                return;
            }

            if (!_profileReady)
            {
                _questText.text = BuildUnavailablePanel("OBJECTIVES");
                ClearNvs01ActionButtons();
                return;
            }

            if (_nvs01CatalogLoading || (_nvs01Presenter == null && _nvs01View == null))
            {
                _questText.text = BuildNvs01LoadingPanel();
                return;
            }

            if (_nvs01View != null)
            {
                _questText.text = BuildNvs01Summary(_nvs01View);
            }
        }

        private void RenderNvs01View(Nvs01KingdomView view, bool announce)
        {
            _nvs01View = view;
            if (_questText != null)
            {
                _questText.text = BuildNvs01Summary(view);
            }

            RebuildNvs01ActionButtons(view);
            if (announce && view != null)
            {
                string announcement = BuildNvs01Announcement(view);
                if (!string.IsNullOrWhiteSpace(announcement))
                {
                    SetMessage(announcement);
                }
            }
        }

        private static string BuildNvs01LoadingPanel()
        {
            return "OBJECTIVES\n\n" +
                   Nvs01CatalogContract.QuestId +
                   "\nVerifying approved quest data...";
        }

        private static string BuildNvs01Summary(Nvs01KingdomView view)
        {
            if (view == null)
            {
                return BuildNvs01LoadingPanel();
            }

            var builder = new StringBuilder("OBJECTIVES");
            if (!string.IsNullOrWhiteSpace(view.Title))
            {
                builder.Append('\n').Append(view.Title);
            }

            if (!string.IsNullOrWhiteSpace(view.ObjectiveText))
            {
                builder.Append('\n').Append(view.ObjectiveText);
            }

            if (!string.IsNullOrWhiteSpace(view.PlayerMessage))
            {
                builder.Append('\n').Append(view.PlayerMessage);
            }

            return builder.ToString();
        }

        private static string BuildNvs01Announcement(Nvs01KingdomView view)
        {
            if (!string.IsNullOrWhiteSpace(view.PlayerMessage))
            {
                return view.PlayerMessage;
            }

            if (!string.IsNullOrWhiteSpace(view.DialogueText))
            {
                var builder = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(view.SpeakerName))
                {
                    builder.Append(view.SpeakerName);
                    if (!string.IsNullOrWhiteSpace(view.SpeakerRole))
                    {
                        builder.Append(" — ").Append(view.SpeakerRole);
                    }

                    builder.Append('\n');
                }

                builder.Append(view.DialogueText);
                return builder.ToString();
            }

            if (!string.IsNullOrWhiteSpace(view.Description))
            {
                return view.Title + "\n" + view.Description;
            }

            return view.Title;
        }

        private void RebuildNvs01ActionButtons(Nvs01KingdomView view)
        {
            ClearNvs01ActionButtons();
            if (_nvs01ActionRoot == null ||
                view == null ||
                view.Status == Nvs01KingdomViewStatus.Unavailable)
            {
                return;
            }

            foreach (Nvs01KingdomChoice choice in view.Choices)
            {
                string choiceKey = choice.Key;
                CreateNvs01ActionButton(
                    choice.Label,
                    () => HandleNvs01ActionResult(_nvs01Presenter?.SelectChoice(choiceKey)));
            }

            if (view.PrimaryAction != Nvs01KingdomActionKind.None &&
                !string.IsNullOrWhiteSpace(view.PrimaryActionLabel))
            {
                Nvs01KingdomActionKind action = view.PrimaryAction;
                CreateNvs01ActionButton(
                    view.PrimaryActionLabel,
                    () => InvokeNvs01PrimaryAction(action));
            }
        }

        private void CreateNvs01ActionButton(string label, UnityEngine.Events.UnityAction action)
        {
            if (_nvs01ActionRoot == null || _nvs01ActionButtons.Count >= 3)
            {
                return;
            }

            int index = _nvs01ActionButtons.Count;
            var buttonObject = new GameObject("Nvs01Action_" + index);
            buttonObject.transform.SetParent(_nvs01ActionRoot, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.085f, 0.125f, 0.155f, 0.98f);

            var button = buttonObject.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = image.color,
                highlightedColor = new Color(0.16f, 0.26f, 0.32f, 1f),
                pressedColor = new Color(0.045f, 0.075f, 0.095f, 1f),
                selectedColor = new Color(0.12f, 0.20f, 0.25f, 1f),
                disabledColor = new Color(0.08f, 0.09f, 0.10f, 0.55f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            button.onClick.AddListener(action);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(18f + index * 136f, 14f);
            rect.sizeDelta = new Vector2(126f, 34f);

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(buttonObject.transform, false);
            var text = labelObject.AddComponent<Text>();
            text.font = GetDefaultFont();
            text.fontSize = 13;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 9;
            text.resizeTextMaxSize = 13;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = new Color(0.92f, 0.96f, 1f);
            text.text = label;
            var labelRect = labelObject.GetComponent<RectTransform>();
            Stretch(labelRect);
            labelRect.offsetMin = new Vector2(5f, 2f);
            labelRect.offsetMax = new Vector2(-5f, -2f);

            _nvs01ActionButtons.Add(button);
        }

        private void InvokeNvs01PrimaryAction(Nvs01KingdomActionKind action)
        {
            if (_nvs01Presenter == null)
            {
                return;
            }

            Nvs01KingdomActionResult result;
            switch (action)
            {
                case Nvs01KingdomActionKind.SelectValerius:
                    result = _nvs01Presenter.SelectValerius();
                    break;
                case Nvs01KingdomActionKind.InvokeSemanticAction:
                case Nvs01KingdomActionKind.ResumeEncounter:
                    result = _nvs01Presenter.InvokePrimaryAction();
                    break;
                default:
                    return;
            }

            HandleNvs01ActionResult(result);
        }

        private void HandleNvs01ActionResult(Nvs01KingdomActionResult result)
        {
            if (result?.View == null)
            {
                return;
            }

            RenderNvs01View(result.View, true);
        }

        private void ClearNvs01ActionButtons()
        {
            foreach (Button button in _nvs01ActionButtons)
            {
                if (button == null)
                {
                    continue;
                }

                button.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(button.gameObject);
                }
                else
                {
                    DestroyImmediate(button.gameObject);
                }
            }

            _nvs01ActionButtons.Clear();
        }

        private void RefreshResourceTicker(IResourceService resources, ResourceType rareResourceType, int warzoneCredits)
        {
            if (_resourceChipTexts[0] == null)
            {
                return;
            }

            SetResourceChip(0, "FOOD", resources.GetResourceCount(ResourceType.Food), new Color(0.56f, 0.86f, 0.48f, 1f), 0.36f);
            SetResourceChip(1, "WOOD", resources.GetResourceCount(ResourceType.Wood), new Color(0.74f, 0.58f, 0.36f, 1f), 0.34f);
            SetResourceChip(2, "STONE", resources.GetResourceCount(ResourceType.Stone), new Color(0.62f, 0.72f, 0.80f, 1f), 0.32f);
            SetResourceChip(3, "GOLD", resources.GetResourceCount(ResourceType.Gold), new Color(0.96f, 0.76f, 0.34f, 1f), 0.42f);
            SetResourceChip(4, "MANA", resources.GetResourceCount(ResourceType.ManaStone), new Color(0.48f, 0.78f, 1f, 1f), 0.48f);
            SetResourceChip(5, "ORE", resources.GetResourceCount(ResourceType.Ore), new Color(0.52f, 0.60f, 0.70f, 1f), 0.34f);
            SetResourceChip(6, FormatResourceLabel(rareResourceType), resources.GetResourceCount(rareResourceType), GetCurrentRealmAccent(), 0.60f);
            SetResourceChip(7, "WAR", warzoneCredits, warzoneCredits > 0 ? new Color(0.96f, 0.72f, 0.32f, 1f) : new Color(0.84f, 0.36f, 0.32f, 1f), warzoneCredits > 0 ? 0.62f : 0.42f);
        }

        private void SetResourceChip(int index, string label, long value, Color accent, float weight)
        {
            if (index < 0 || index >= _resourceChipTexts.Length || _resourceChipTexts[index] == null)
            {
                return;
            }

            weight = Mathf.Clamp01(weight);
            _resourceChipAccents[index] = accent;
            _resourceChipWeights[index] = weight;
            _resourceChipTexts[index].text = label + "\n" + FormatCompactNumber(value);
            _resourceChipTexts[index].color = Color.Lerp(new Color(0.84f, 0.90f, 0.96f, 1f), accent, 0.18f + weight * 0.10f);
            SetImageColor(_resourceChipPanels[index], WithAlpha(Color.Lerp(new Color(0.014f, 0.020f, 0.030f, 1f), accent, 0.10f + weight * 0.06f), 0.90f));
            SetImageColor(_resourceChipRails[index], WithAlpha(accent, 0.48f + weight * 0.26f));
            SetImageColor(_resourceChipGlows[index], WithAlpha(accent, 0.04f + weight * 0.08f));
        }

        private void RefreshStrategicReadiness()
        {
            if (_readinessChipTexts[0] == null)
            {
                return;
            }

            var buildings = ServiceLocator.Get<IBuildingService>();
            RealmId realmId = ServiceLocator.Get<IRealmService>().CurrentRealmId;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int upgradingCount = 0;
            int totalBuildingLevels = 0;
            foreach (KingdomBuildingPresentation state in
                     KingdomBuildingPresentationResolver.Resolve(
                         realmId,
                         buildings.GetAllBuildingStates()))
            {
                if (state == null ||
                    state.Status == KingdomBuildingPresentationStatus.InvalidState)
                {
                    continue;
                }

                totalBuildingLevels += state.ConfirmedLevel;
                if (state.IsUpgrading && state.UpgradeCompleteTimestamp > now)
                {
                    upgradingCount++;
                }
            }

            SetReadinessChip(
                0,
                "BUILD",
                upgradingCount > 0 ? $"{upgradingCount} UP" : $"LV {totalBuildingLevels}",
                upgradingCount > 0 ? new Color(0.92f, 0.62f, 0.28f, 1f) : new Color(0.62f, 0.86f, 0.56f, 1f),
                upgradingCount > 0 ? 0.88f : 0.34f);

            // FORCE: troop counts are only available through the state-seeding GetTroopCount getter, so
            // the read-only readiness chip renders a neutral unavailable value (D8) instead.
            SetReadinessChip(
                1,
                "FORCE",
                "N/A",
                new Color(0.52f, 0.57f, 0.64f, 1f),
                0.30f);

            var research = ServiceLocator.Get<IResearchService>();
            int activeResearch = 0;
            int researchLevels = 0;
            foreach (var state in research.GetAllResearchStates())
            {
                if (state == null)
                {
                    continue;
                }

                researchLevels += Math.Max(0, state.Level);
                if (state.IsResearching)
                {
                    activeResearch++;
                }
            }

            SetReadinessChip(
                2,
                "LAB",
                activeResearch > 0 ? $"{activeResearch} RUN" : $"L{researchLevels}",
                activeResearch > 0 ? new Color(0.54f, 0.76f, 1f, 1f) : new Color(0.72f, 0.60f, 1f, 1f),
                activeResearch > 0 ? 0.82f : 0.42f);

            int warzoneCredits = ServiceLocator.Get<IWarzoneCreditService>().GetCredits();
            SetReadinessChip(
                3,
                "WAR",
                FormatCompactNumber(warzoneCredits),
                warzoneCredits > 0 ? new Color(0.96f, 0.76f, 0.34f, 1f) : new Color(0.88f, 0.42f, 0.34f, 1f),
                warzoneCredits > 0 ? 0.52f : 0.42f);
        }

        private void SetReadinessChip(int index, string label, string value, Color accent, float urgency)
        {
            if (index < 0 || index >= _readinessChipTexts.Length || _readinessChipTexts[index] == null)
            {
                return;
            }

            urgency = Mathf.Clamp01(urgency);
            _readinessChipAccents[index] = accent;
            _readinessChipUrgencies[index] = urgency;
            _readinessChipTexts[index].text = label + "\n" + value;
            _readinessChipTexts[index].color = Color.Lerp(new Color(0.84f, 0.90f, 0.96f, 1f), accent, 0.24f);
            SetImageColor(_readinessChipPanels[index], WithAlpha(Color.Lerp(new Color(0.018f, 0.026f, 0.036f, 1f), accent, 0.12f + urgency * 0.06f), 0.94f));
            SetImageColor(_readinessChipRails[index], WithAlpha(accent, 0.56f + urgency * 0.30f));
            SetImageColor(_readinessChipGlows[index], WithAlpha(accent, 0.07f + urgency * 0.10f));
        }

        private void SetMessage(string message)
        {
            string cleanMessage = string.IsNullOrWhiteSpace(message)
                ? "Command board online. Select a district or border outpost to inspect yield, readiness, and next order."
                : message;

            CommandMessageProfile profile = GetMessageProfile(cleanMessage);
            ApplyMessageProfile(profile);

            if (_messageHeaderText != null)
            {
                _messageHeaderText.text = profile.Header;
                _messageHeaderText.color = Color.Lerp(profile.Accent, Color.white, 0.28f);
            }

            if (_messageMetaText != null)
            {
                _messageMetaText.text = DateTime.Now.ToString("HH:mm:ss") + " / " + profile.Meta;
            }

            if (_messageStatusText != null)
            {
                _messageStatusText.text = profile.Status;
                _messageStatusText.color = Color.Lerp(profile.Accent, Color.white, 0.34f);
            }

            if (_messageText != null)
            {
                _messageText.text = cleanMessage;
                _messageText.color = profile.MessageColor;
            }

            if (_boardHintText != null)
            {
                _boardHintText.text = cleanMessage;
            }
        }

        private void ApplyMessageProfile(CommandMessageProfile profile)
        {
            _messageAccentBaseColor = profile.Accent;
            _messagePanelBaseColor = WithAlpha(Color.Lerp(new Color(0.020f, 0.027f, 0.037f, 1f), profile.Accent, 0.07f), 0.92f);
            _messageWashBaseColor = WithAlpha(profile.Accent, 0.065f);
            _messageSignalBaseColor = WithAlpha(profile.Accent, 0.30f);
            _messagePulseTimer = profile.PulseSeconds;

            SetImageColor(_messagePanelImage, _messagePanelBaseColor);
            SetImageColor(_messageAccent, profile.Accent);
            SetImageColor(_messageTopRule, WithAlpha(Color.Lerp(profile.Accent, Color.white, 0.35f), 0.24f));
            SetImageColor(_messageBottomRule, WithAlpha(profile.Accent, 0.20f));
            SetImageColor(_messageWash, _messageWashBaseColor);
            SetImageColor(_messageStatusPlate, WithAlpha(Color.Lerp(new Color(0.055f, 0.070f, 0.090f, 1f), profile.Accent, 0.20f), 0.88f));
            SetImageColor(_messageStatusRule, WithAlpha(profile.Accent, 0.62f));
            SetSignalBarsIdle();
        }

        private void HandleBuildingSelected(string message)
        {
            SetMessage(message);
        }

        private void HandleTerritorySelected(string message)
        {
            SetMessage(message);
        }

        private void ToggleDashboard()
        {
            _dashboardVisible = !_dashboardVisible;
            if (_dashboardRoot != null)
            {
                _dashboardRoot.SetActive(_dashboardVisible);
            }

            if (_dashboardToggleText != null)
            {
                _dashboardToggleText.text = _dashboardVisible ? "Board View" : "Show UI";
            }

            RefreshBoardHintVisibility();
        }

        private void RefreshBoardHintVisibility()
        {
            if (_boardHintText != null)
            {
                _boardHintText.gameObject.SetActive(!_dashboardVisible);
            }
        }

        private void UpdateCommandMessagePulse()
        {
            if (_messagePulseTimer <= 0f)
            {
                return;
            }

            _messagePulseTimer -= Time.deltaTime;
            if (_messagePulseTimer <= 0f)
            {
                ResetMessagePulseVisuals();
                return;
            }

            float pulse = Mathf.PingPong(Time.time * 5.5f, 1f);
            SetImageColor(_messageAccent, Color.Lerp(_messageAccentBaseColor, Color.white, pulse * 0.20f));
            SetImageColor(_messageWash, Color.Lerp(_messageWashBaseColor, WithAlpha(_messageAccentBaseColor, 0.13f), pulse));
            SetImageColor(_messageStatusPlate, Color.Lerp(WithAlpha(Color.Lerp(new Color(0.055f, 0.070f, 0.090f, 1f), _messageAccentBaseColor, 0.20f), 0.88f), WithAlpha(_messageAccentBaseColor, 0.38f), pulse * 0.45f));

            for (int i = 0; i < _messageSignalBars.Count; i++)
            {
                float barPulse = Mathf.PingPong(Time.time * 7.2f + i * 0.24f, 1f);
                Color signalColor = Color.Lerp(_messageSignalBaseColor, Color.Lerp(_messageAccentBaseColor, Color.white, 0.30f), barPulse);
                signalColor.a = 0.22f + barPulse * 0.44f;
                _messageSignalBars[i].color = signalColor;
                _messageSignalBars[i].transform.localScale = new Vector3(1f, 0.82f + barPulse * 0.42f, 1f);
            }
        }

        private void UpdateStrategicReadinessPulse()
        {
            if (_readinessChipTexts[0] == null)
            {
                return;
            }

            for (int i = 0; i < _readinessChipTexts.Length; i++)
            {
                float urgency = Mathf.Clamp01(_readinessChipUrgencies[i]);
                Color accent = _readinessChipAccents[i];
                if (accent.a <= 0f)
                {
                    accent = new Color(0.42f, 0.62f, 0.78f, 1f);
                }

                float pulse = (Mathf.Sin(Time.unscaledTime * (2.4f + i * 0.18f) + i * 0.74f) + 1f) * 0.5f;
                SetImageColor(_readinessChipRails[i], WithAlpha(Color.Lerp(accent, Color.white, pulse * 0.18f), 0.46f + urgency * 0.34f + pulse * 0.12f));
                SetImageColor(_readinessChipGlows[i], WithAlpha(accent, 0.045f + pulse * (0.060f + urgency * 0.080f)));

                if (_readinessChipPanels[i] != null)
                {
                    _readinessChipPanels[i].rectTransform.localScale = Vector3.one * (1f + urgency * pulse * 0.010f);
                }
            }
        }

        private void UpdateResourceTickerPulse()
        {
            if (_resourceChipTexts[0] == null)
            {
                return;
            }

            for (int i = 0; i < _resourceChipTexts.Length; i++)
            {
                float weight = Mathf.Clamp01(_resourceChipWeights[i]);
                Color accent = _resourceChipAccents[i];
                if (accent.a <= 0f)
                {
                    accent = new Color(0.42f, 0.62f, 0.78f, 1f);
                }

                float pulse = (Mathf.Sin(Time.unscaledTime * (1.8f + i * 0.11f) + i * 0.41f) + 1f) * 0.5f;
                SetImageColor(_resourceChipRails[i], WithAlpha(Color.Lerp(accent, Color.white, pulse * 0.14f), 0.38f + weight * 0.30f + pulse * 0.08f));
                SetImageColor(_resourceChipGlows[i], WithAlpha(accent, 0.025f + pulse * (0.040f + weight * 0.060f)));
            }
        }

        private void ResetMessagePulseVisuals()
        {
            SetImageColor(_messageAccent, _messageAccentBaseColor);
            SetImageColor(_messageWash, _messageWashBaseColor);
            SetImageColor(_messageStatusPlate, WithAlpha(Color.Lerp(new Color(0.055f, 0.070f, 0.090f, 1f), _messageAccentBaseColor, 0.20f), 0.88f));
            SetSignalBarsIdle();
        }

        private static Canvas CreateCanvas(string name)
        {
            var canvasObject = new GameObject(name);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Color color)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var image = panel.AddComponent<Image>();
            image.color = color;

            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return panel;
        }

        private static Text CreatePanelText(Transform parent, string panelName, string textName, Font font, int size, TextAnchor alignment, Vector2 anchoredPosition, Vector2 panelSize)
        {
            Color accent = GetStatusPanelAccent(panelName);
            var panel = CreatePanel(parent, panelName, anchoredPosition, panelSize, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), WithAlpha(Color.Lerp(new Color(0.024f, 0.032f, 0.043f, 1f), accent, 0.05f), 0.86f));
            CreatePanel(panel.transform, panelName + "_Wash", new Vector2(8f, -8f), new Vector2(-16f, -18f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), WithAlpha(accent, 0.035f));
            CreatePanel(panel.transform, panelName + "_Accent", new Vector2(0f, 0f), new Vector2(4f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), WithAlpha(accent, 0.50f));
            CreatePanel(panel.transform, panelName + "_TopRule", new Vector2(0f, -1f), new Vector2(-30f, 2f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), WithAlpha(Color.Lerp(accent, Color.white, 0.30f), 0.16f));
            CreatePanel(panel.transform, panelName + "_BottomRule", new Vector2(0f, 6f), new Vector2(-34f, 2f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), WithAlpha(accent, 0.13f));
            CreateStatusPanelCorners(panel.transform, panelName, accent);
            CreateStatusPanelPips(panel.transform, panelName, accent);

            var text = CreateText(panel.transform, textName, font, size, alignment, new Vector2(18f, -18f), new Vector2(panelSize.x - 36f, panelSize.y - 44f));
            text.color = new Color(0.86f, 0.91f, 0.96f);
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void CreateStatusPanelCorners(Transform parent, string panelName, Color accent)
        {
            Color cornerColor = WithAlpha(Color.Lerp(accent, Color.white, 0.24f), 0.22f);
            CreatePanel(parent, panelName + "_CornerTL_H", new Vector2(10f, -10f), new Vector2(30f, 2f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), cornerColor);
            CreatePanel(parent, panelName + "_CornerTL_V", new Vector2(10f, -10f), new Vector2(2f, 18f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), cornerColor);
            CreatePanel(parent, panelName + "_CornerTR_H", new Vector2(-10f, -10f), new Vector2(30f, 2f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), cornerColor);
            CreatePanel(parent, panelName + "_CornerTR_V", new Vector2(-10f, -10f), new Vector2(2f, 18f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), cornerColor);
        }

        private static void CreateStatusPanelPips(Transform parent, string panelName, Color accent)
        {
            for (int i = 0; i < 4; i++)
            {
                CreatePanel(parent, panelName + "_StatusPip_" + (i + 1), new Vector2(-24f - i * 13f, 13f), new Vector2(7f, 7f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), WithAlpha(accent, 0.12f + i * 0.055f));
            }
        }

        private static Text CreateText(Transform parent, string name, Font font, int size, TextAnchor alignment, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return text;
        }

        private static Text CreateBoardHintText(Transform parent, Font font)
        {
            var text = CreateText(parent, "BoardHintText", font, 19, TextAnchor.LowerLeft, new Vector2(40f, 34f), new Vector2(760f, 64f));
            text.color = new Color(0.90f, 0.94f, 1f);
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            return text;
        }

        private void CreateResourceTicker(Transform parent, Font font)
        {
            for (int i = 0; i < _resourceChipTexts.Length; i++)
            {
                CreateResourceChip(parent, font, i, new Vector2(20f + i * 83f, -64f));
            }
        }

        private void CreateResourceChip(Transform parent, Font font, int index, Vector2 anchoredPosition)
        {
            var chip = CreatePanel(
                parent,
                "ResourceChip_" + (index + 1),
                anchoredPosition,
                new Vector2(76f, 30f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.014f, 0.020f, 0.030f, 0.90f));
            _resourceChipPanels[index] = chip.GetComponent<Image>();
            _resourceChipRails[index] = CreatePanel(chip.transform, "ResourceRail", new Vector2(0f, 0f), new Vector2(3f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Color(0.42f, 0.62f, 0.78f, 0.50f)).GetComponent<Image>();
            _resourceChipGlows[index] = CreatePanel(chip.transform, "ResourceGlow", new Vector2(0f, 0f), new Vector2(-8f, 2f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Color(0.42f, 0.62f, 0.78f, 0.06f)).GetComponent<Image>();

            var text = CreateText(chip.transform, "ResourceText", font, 10, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(70f, 28f));
            text.text = "--\n0";
            text.color = new Color(0.84f, 0.90f, 0.96f);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 8;
            text.resizeTextMaxSize = 10;
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(5f, 1f);
            rect.offsetMax = new Vector2(-4f, -1f);
            _resourceChipTexts[index] = text;
        }

        private void CreateStrategicReadinessConsole(Transform parent, Font font)
        {
            var console = CreatePanel(
                parent,
                "StrategicReadinessConsole",
                new Vector2(712f, -14f),
                new Vector2(448f, 78f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.010f, 0.015f, 0.022f, 0.58f));
            CreatePanel(console.transform, "StrategicReadinessRule", new Vector2(0f, -1f), new Vector2(-20f, 2f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Color(1f, 0.86f, 0.54f, 0.14f));

            var title = CreateText(console.transform, "StrategicReadinessTitle", font, 11, TextAnchor.UpperLeft, new Vector2(12f, -7f), new Vector2(300f, 16f));
            title.text = "STRATEGIC READINESS";
            title.color = new Color(0.58f, 0.68f, 0.78f);

            string[] labels = { "BUILD", "FORCE", "LAB", "WAR" };
            for (int i = 0; i < labels.Length; i++)
            {
                CreateReadinessChip(console.transform, font, labels[i], i, new Vector2(12f + i * 106f, -29f));
            }
        }

        private void CreateReadinessChip(Transform parent, Font font, string label, int index, Vector2 anchoredPosition)
        {
            var chip = CreatePanel(
                parent,
                "ReadinessChip_" + label,
                anchoredPosition,
                new Vector2(96f, 39f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.018f, 0.026f, 0.036f, 0.94f));
            _readinessChipPanels[index] = chip.GetComponent<Image>();
            _readinessChipRails[index] = CreatePanel(chip.transform, label + "_Rail", new Vector2(0f, 0f), new Vector2(3f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Color(0.42f, 0.62f, 0.78f, 0.56f)).GetComponent<Image>();
            _readinessChipGlows[index] = CreatePanel(chip.transform, label + "_Glow", new Vector2(0f, 0f), new Vector2(-10f, 3f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Color(0.42f, 0.62f, 0.78f, 0.08f)).GetComponent<Image>();

            var text = CreateText(chip.transform, label + "_Text", font, 11, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(90f, 35f));
            text.text = label + "\n--";
            text.color = new Color(0.84f, 0.90f, 0.96f);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 9;
            text.resizeTextMaxSize = 11;
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(6f, 1f);
            rect.offsetMax = new Vector2(-6f, -1f);
            _readinessChipTexts[index] = text;
        }

        private void CreateCommandSignalBars(Transform parent)
        {
            _messageSignalBars.Clear();
            float[] heights = { 4f, 7f, 10f, 7f, 4f };
            for (int i = 0; i < heights.Length; i++)
            {
                var bar = CreatePanel(
                    parent,
                    "CommandSignalBar_" + (i + 1),
                    new Vector2(24f + i * 22f, -86f),
                    new Vector2(14f, heights[i]),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    WithAlpha(_messageSignalBaseColor, 0.24f + i * 0.035f)).GetComponent<Image>();
                _messageSignalBars.Add(bar);
            }
        }

        private void CreateCommandDeck(Transform parent, Font font)
        {
            var context = CreateCommandContext();
            IReadOnlyList<KingdomCommandDescriptor> descriptors = KingdomCommandPolicy.CreateDeckDescriptors(context);

            CreateCommandSection(parent, font, descriptors, KingdomCommandCategory.Build, "BUILD", new Vector2(18f, -64f), -104f);
            CreateCommandSection(parent, font, descriptors, KingdomCommandCategory.Forces, "FORCES", new Vector2(18f, -302f), -342f);
            CreateCommandSection(parent, font, descriptors, KingdomCommandCategory.Progression, "PROGRESSION", new Vector2(18f, -444f), -484f);
            CreateCommandSection(parent, font, descriptors, KingdomCommandCategory.RealmOps, "REALM OPS", new Vector2(18f, -586f), -626f);
        }

        private void CaptureProfileMutationPresentationOnce()
        {
            if (_profileMutationPresentationCaptured)
            {
                return;
            }

            _profileMutationPresentationCaptured = true;
            IProfileWriteAuthorityProvider provider = null;
            try
            {
                if (ServiceLocator.TryGet<ISaveGameService>(
                        out var saveGameService))
                {
                    provider = saveGameService as
                        IProfileWriteAuthorityProvider;
                }
            }
            catch (Exception)
            {
                provider = null;
            }

            _profileMutationPresentation =
                ProfileMutationPresentationPolicy.Capture(provider);
        }

        private KingdomCommandContext CreateCommandContext()
        {
            bool hasCommittedRealm = false;
            bool buildingConstructionAvailable = false;
            try
            {
                hasCommittedRealm = ServiceLocator.Get<IRealmService>().CurrentRealmId != RealmId.None;
                // Full castle-grid upgrades stay capability-gated. The one Town Hall
                // construct is unlocked by KingdomCommandPolicy itself.
                buildingConstructionAvailable = false;
            }
            catch (Exception)
            {
                hasCommittedRealm = false;
                buildingConstructionAvailable = false;
            }

            return new KingdomCommandContext(
                hasCommittedRealm,
                new KingdomCommandCapabilities(
                    buildingUpgrade: buildingConstructionAvailable));
        }

        private void CreateCommandSection(
            Transform parent,
            Font font,
            IReadOnlyList<KingdomCommandDescriptor> descriptors,
            KingdomCommandCategory category,
            string label,
            Vector2 headerPosition,
            float firstButtonY)
        {
            CreateSectionHeader(parent, font, label, headerPosition);

            int index = 0;
            foreach (KingdomCommandDescriptor descriptor in descriptors)
            {
                if (descriptor.Category != category)
                {
                    continue;
                }

                float x = index % 2 == 0 ? -222f : -18f;
                float y = firstButtonY - (index / 2) * 48f;
                CreateDeckButton(parent, font, descriptor, new Vector2(x, y));
                index++;
            }
        }

        private Button CreateDeckButton(Transform parent, Font font, KingdomCommandDescriptor descriptor, Vector2 anchoredPosition)
        {
            bool blockedByProfileAuthority =
                descriptor.IsInteractable &&
                KingdomCommandPolicy.TryGetBuildingId(
                    descriptor.Id,
                    out string buildingId) &&
                !KingdomOneBuildCommand.IsOneBuild(buildingId) &&
                !_profileMutationPresentation
                    .OrdinaryMutationCommandsEnabled;
            bool isInteractable =
                descriptor.IsInteractable &&
                !blockedByProfileAuthority;
            Color fill = isInteractable
                ? new Color(0.105f, 0.138f, 0.178f, 1f)
                : new Color(0.062f, 0.073f, 0.088f, 0.94f);
            var button = CreateButton(parent, font, descriptor.Label, anchoredPosition, () => HandleCommandSelected(descriptor), new Vector2(190f, 40f), fill);
            button.name = descriptor.Id;
            button.interactable = isInteractable;

            if (!isInteractable)
            {
                var image = button.GetComponent<Image>();
                if (image != null)
                {
                    image.color = fill;
                }

                var text = button.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.color = new Color(0.58f, 0.66f, 0.74f, 0.92f);
                }

                var plate = CreatePanel(button.transform, "UnavailableStatus", new Vector2(-18f, -8f), new Vector2(46f, 13f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Color(0.18f, 0.13f, 0.08f, 0.88f));
                var status = CreateText(plate.transform, "UnavailableStatusText", font, 8, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(46f, 13f));
                status.text = blockedByProfileAuthority
                    ? "READ-ONLY"
                    : "LOCKED";
                status.color = new Color(1f, 0.78f, 0.42f, 0.92f);
            }

            return button;
        }

        private void HandleCommandSelected(KingdomCommandDescriptor descriptor)
        {
            if (!descriptor.IsInteractable)
            {
                SetMessage(CreateUnavailableCommandMessage(descriptor));
                return;
            }

            if (descriptor.Id == KingdomCommandPolicy.BoardView)
            {
                ToggleDashboard();
                return;
            }

            if (descriptor.Id == KingdomCommandPolicy.GreyboxDuel)
            {
                StartGreyboxDuel();
                return;
            }

            if (KingdomOneBuildCommand.IsOneBuildCommand(descriptor.Id))
            {
                ServiceLocator.TryGet<ISaveGameService>(out ISaveGameService save);
                ServiceLocator.TryGet<IGameDataService>(out IGameDataService gameData);
                KingdomOneBuildResult oneBuild = KingdomOneBuildCommand.TryExecute(
                    save,
                    gameData);
                SetMessage(oneBuild.Message);
                if (_runtimeInitialized)
                {
                    Refresh();
                }
                return;
            }

            if (KingdomCommandPolicy.TryGetBuildingId(
                    descriptor.Id,
                    out string buildingId))
            {
                if (!_profileMutationPresentation
                        .OrdinaryMutationCommandsEnabled)
                {
                    SetMessage(string.IsNullOrWhiteSpace(
                            _profileMutationPresentation.DisplayText)
                        ? "COMMAND DECK READ-ONLY — PROFILE AUTHORITY UNAVAILABLE"
                        : _profileMutationPresentation.DisplayText);
                    return;
                }

                BuildingConstructionResult result =
                    ServiceLocator.Get<IBuildingService>().TryStartConstruction(
                        buildingId,
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                SetMessage(FormatConstructionResult(result));
                Refresh();
                return;
            }

            SetMessage(CreateUnavailableCommandMessage(descriptor));
        }

        private void StartGreyboxDuel()
        {
            if (_greyboxDuelHost == null)
            {
                _greyboxDuelHost = gameObject.GetComponent<KingdomGreyboxDuelHost>();
                if (_greyboxDuelHost == null)
                {
                    _greyboxDuelHost = gameObject.AddComponent<KingdomGreyboxDuelHost>();
                }

                _greyboxDuelHost.Bind(SetMessage);
            }

            _greyboxDuelHost.StartDuel();
        }

        private static string FormatConstructionResult(
            BuildingConstructionResult result)
        {
            if (result == null)
            {
                return "CONSTRUCTION UNAVAILABLE: no authoritative result.";
            }

            BuildingConstructionQuote quote = result.Quote;
            string label = quote == null
                ? "BUILDING"
                : FormatBuildingName(quote.BuildingId).ToUpperInvariant();
            switch (result.Status)
            {
                case BuildingConstructionStatus.Started:
                    return
                        $"{label} ORDER ACCEPTED: Lv {quote.ConfirmedLevel} → {quote.TargetLevel}; " +
                        $"{FormatDuration(quote.DurationSeconds)}; {FormatConstructionCosts(quote.Costs)}.";
                case BuildingConstructionStatus.AlreadyInProgress:
                    long remaining = Math.Max(
                        0,
                        quote.CompleteTimestamp -
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    return
                        $"{label} ORDER ACTIVE: Lv {quote.ConfirmedLevel} → {quote.TargetLevel}; " +
                        $"{FormatDuration(remaining)} remaining.";
                case BuildingConstructionStatus.MaxLevel:
                    return $"{label} COMPLETE: Level {quote.ConfirmedLevel} is the capstone.";
                case BuildingConstructionStatus.RejectedInsufficientResources:
                    return
                        $"{label} ORDER REJECTED: requires {FormatConstructionCosts(quote.Costs)}.";
                case BuildingConstructionStatus.SaveFailedRolledBack:
                    return
                        $"{label} ORDER NOT COMMITTED: resources and building state were restored.";
                case BuildingConstructionStatus.CommitUncertain:
                    return
                        $"{label} ORDER UNRESOLVED: persistence reconciliation is required before another order.";
                case BuildingConstructionStatus.RejectedUnsupportedBuilding:
                case BuildingConstructionStatus.RejectedInvalidDefinition:
                    return $"{label} UNAVAILABLE: construction definition is not approved.";
                case BuildingConstructionStatus.RejectedMalformedState:
                    return $"{label} UNAVAILABLE: saved building state requires recovery.";
                case BuildingConstructionStatus.RejectedNoCurrentSave:
                    return $"{label} UNAVAILABLE: no writable kingdom profile.";
                case BuildingConstructionStatus.RejectedEconomyUnavailable:
                    return $"{label} UNAVAILABLE: economy transaction could not be authorized.";
                default:
                    return $"{label} ORDER UNCHANGED: {result.DiagnosticCode}.";
            }
        }

        private static string FormatConstructionCosts(
            IReadOnlyList<BuildingConstructionCost> costs)
        {
            if (costs == null || costs.Count == 0)
            {
                return "no resource cost";
            }

            return string.Join(
                " / ",
                costs.Select(cost =>
                    $"{FormatResourceLabel(cost.ResourceType)} {FormatCompactNumber(cost.Amount)}"));
        }

        private static string FormatDuration(long seconds)
        {
            seconds = Math.Max(0L, seconds);
            if (seconds < 60L)
            {
                return seconds + "s";
            }

            if (seconds < 3600L)
            {
                return (seconds / 60L) + "m";
            }

            return (seconds / 3600L) + "h";
        }

        private static string CreateUnavailableCommandMessage(KingdomCommandDescriptor descriptor)
        {
            string issues = descriptor.BlockingIssueIds.Count == 0
                ? "pending contract"
                : "#" + string.Join("/#", descriptor.BlockingIssueIds);
            string code = string.IsNullOrWhiteSpace(descriptor.TechnicalCode)
                ? "capability-disabled"
                : descriptor.TechnicalCode;
            return $"{descriptor.Label.ToUpperInvariant()} UNAVAILABLE: {code}; waiting on {issues}.";
        }

        private static void CreateSectionHeader(Transform parent, Font font, string label, Vector2 anchoredPosition)
        {
            Color accent = GetCommandSectionAccent(label);
            var band = CreatePanel(parent, label + "_SectionBand", new Vector2(anchoredPosition.x - 6f, anchoredPosition.y + 7f), new Vector2(392f, 30f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), WithAlpha(Color.Lerp(new Color(0.016f, 0.023f, 0.034f, 1f), accent, 0.12f), 0.58f));
            CreatePanel(band.transform, label + "_SectionAccent", new Vector2(0f, 0f), new Vector2(4f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), WithAlpha(accent, 0.58f));
            CreatePanel(band.transform, label + "_SectionTopRule", new Vector2(0f, -1f), new Vector2(-22f, 1.4f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), WithAlpha(Color.Lerp(accent, Color.white, 0.22f), 0.20f));

            for (int i = 0; i < 3; i++)
            {
                CreatePanel(band.transform, label + "_SectionPip_" + (i + 1), new Vector2(336f + i * 16f, -10f), new Vector2(8f, 8f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), WithAlpha(accent, 0.20f + i * 0.12f));
            }

            var header = CreateText(band.transform, label + "_Header", font, 15, TextAnchor.MiddleLeft, new Vector2(14f, -4f), new Vector2(292f, 22f));
            header.text = label;
            header.color = Color.Lerp(new Color(0.64f, 0.74f, 0.84f, 1f), accent, 0.16f);
        }

        private static Button CreateDeckButton(Transform parent, Font font, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action, Color? fillColor = null)
        {
            return CreateButton(parent, font, label, anchoredPosition, action, new Vector2(190f, 40f), fillColor ?? new Color(0.105f, 0.138f, 0.178f, 1f));
        }

        private static Button CreateButton(Transform parent, Font font, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action, Vector2? sizeDelta = null, Color? fillColor = null)
        {
            var buttonObject = new GameObject(label);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            Color baseColor = fillColor ?? new Color(0.20f, 0.28f, 0.36f, 1f);
            image.color = baseColor;

            var button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(action);
            button.transition = Selectable.Transition.None;
            button.colors = new ColorBlock
            {
                normalColor = baseColor,
                highlightedColor = Color.Lerp(baseColor, Color.white, 0.16f),
                pressedColor = Color.Lerp(baseColor, Color.black, 0.12f),
                selectedColor = Color.Lerp(baseColor, Color.white, 0.10f),
                disabledColor = new Color(0.12f, 0.13f, 0.15f, 0.60f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta ?? new Vector2(240, 48);

            Color iconColor = GetCommandIconColor(label, baseColor);
            CreatePanel(buttonObject.transform, "ButtonInnerWash", new Vector2(4f, -4f), new Vector2(-8f, -8f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), WithAlpha(iconColor, 0.035f));
            var accent = CreatePanel(buttonObject.transform, "ButtonAccent", new Vector2(0f, 0f), new Vector2(3f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), WithAlpha(iconColor, 0.54f)).GetComponent<Image>();
            var topTrace = CreatePanel(buttonObject.transform, "ButtonTopTrace", new Vector2(0f, -1f), new Vector2(-18f, 1.5f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Color(1f, 0.90f, 0.66f, 0.13f)).GetComponent<Image>();
            CreatePanel(buttonObject.transform, "ButtonBottomTrace", new Vector2(0f, 1f), new Vector2(-22f, 1.2f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), WithAlpha(iconColor, 0.10f));
            var actionNotch = CreatePanel(buttonObject.transform, "ButtonActionNotch", new Vector2(-9f, -11f), new Vector2(14f, 18f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), WithAlpha(iconColor, 0.18f)).GetComponent<Image>();
            CreatePanel(actionNotch.transform, "ButtonActionNotchCore", new Vector2(-4f, -4f), new Vector2(5f, 10f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), WithAlpha(Color.Lerp(iconColor, Color.white, 0.32f), 0.28f));
            var iconFrame = CreateCommandButtonIcon(buttonObject.transform, label, iconColor);

            int fontSize = rect.sizeDelta.x <= 190f ? 17 : 20;
            var text = CreateText(buttonObject.transform, label + "_Text", font, fontSize, TextAnchor.MiddleLeft, Vector2.zero, rect.sizeDelta);
            text.text = label;
            text.color = new Color(0.92f, 0.96f, 1f);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(rect.sizeDelta.x <= 190f ? 42f : 46f, 0f);
            textRect.offsetMax = new Vector2(-22f, 0f);
            buttonObject.AddComponent<KingdomCommandButtonFeedback>().Configure(image, text, accent, topTrace, iconFrame, actionNotch, iconColor);
            return button;
        }

        private static Image CreateCommandButtonIcon(Transform parent, string label, Color iconColor)
        {
            Color frameColor = new Color(0.006f, 0.010f, 0.016f, 0.82f);
            Color coreColor = new Color(iconColor.r, iconColor.g, iconColor.b, 0.24f);
            var frame = CreatePanel(parent, "ButtonIconFrame", new Vector2(9f, -7f), new Vector2(26f, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), frameColor);
            CreatePanel(frame.transform, "IconCore", Vector2.zero, new Vector2(16f, 16f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), coreColor);

            switch (GetCommandIconKind(label))
            {
                case CommandIconKind.Build:
                    CreateIconStroke(frame.transform, "BuildBase", new Vector2(0f, -5f), new Vector2(16f, 3f), 0f, iconColor);
                    CreateIconStroke(frame.transform, "BuildTowerL", new Vector2(-5f, 1f), new Vector2(4f, 13f), 0f, iconColor);
                    CreateIconStroke(frame.transform, "BuildTowerR", new Vector2(5f, 1f), new Vector2(4f, 13f), 0f, iconColor);
                    CreateIconStroke(frame.transform, "BuildCrown", new Vector2(0f, 7f), new Vector2(15f, 3f), 0f, Color.Lerp(iconColor, Color.white, 0.20f));
                    break;
                case CommandIconKind.Forces:
                    CreateIconStroke(frame.transform, "ForceChevronA", new Vector2(-4f, 0f), new Vector2(14f, 3f), 35f, iconColor);
                    CreateIconStroke(frame.transform, "ForceChevronB", new Vector2(4f, 0f), new Vector2(14f, 3f), -35f, iconColor);
                    CreateIconStroke(frame.transform, "ForceSpear", new Vector2(0f, 0f), new Vector2(3f, 17f), 0f, Color.Lerp(iconColor, Color.white, 0.16f));
                    break;
                case CommandIconKind.Gem:
                    CreateIconStroke(frame.transform, "GemTop", new Vector2(0f, 5f), new Vector2(12f, 3f), 0f, iconColor);
                    CreateIconStroke(frame.transform, "GemLeft", new Vector2(-4f, -1f), new Vector2(12f, 3f), -45f, iconColor);
                    CreateIconStroke(frame.transform, "GemRight", new Vector2(4f, -1f), new Vector2(12f, 3f), 45f, iconColor);
                    CreateIconStroke(frame.transform, "GemCore", new Vector2(0f, 0f), new Vector2(5f, 5f), 45f, Color.Lerp(iconColor, Color.white, 0.24f));
                    break;
                case CommandIconKind.Board:
                    CreateIconStroke(frame.transform, "BoardVertical", new Vector2(0f, 0f), new Vector2(3f, 18f), 0f, iconColor);
                    CreateIconStroke(frame.transform, "BoardHorizontal", new Vector2(0f, 0f), new Vector2(18f, 3f), 0f, iconColor);
                    CreateIconStroke(frame.transform, "BoardDot", new Vector2(5f, -5f), new Vector2(5f, 5f), 0f, Color.Lerp(iconColor, Color.white, 0.24f));
                    break;
                case CommandIconKind.Danger:
                    CreateIconStroke(frame.transform, "DangerSlashA", new Vector2(0f, 0f), new Vector2(18f, 3f), 45f, iconColor);
                    CreateIconStroke(frame.transform, "DangerSlashB", new Vector2(0f, 0f), new Vector2(18f, 3f), -45f, iconColor);
                    break;
                default:
                    CreateIconStroke(frame.transform, "ProgressRingA", new Vector2(0f, 0f), new Vector2(16f, 3f), 0f, iconColor);
                    CreateIconStroke(frame.transform, "ProgressRingB", new Vector2(0f, 0f), new Vector2(16f, 3f), 90f, iconColor);
                    CreateIconStroke(frame.transform, "ProgressCore", new Vector2(0f, 0f), new Vector2(6f, 6f), 45f, Color.Lerp(iconColor, Color.white, 0.20f));
                    break;
            }

            return frame.GetComponent<Image>();
        }

        private static void CreateIconStroke(Transform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta, float rotationDegrees, Color color)
        {
            var stroke = CreatePanel(parent, name, anchoredPosition, sizeDelta, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), color);
            stroke.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
        }

        private static Color GetCommandIconColor(string label, Color baseColor)
        {
            return GetCommandIconKind(label) switch
            {
                CommandIconKind.Build => new Color(0.88f, 0.62f, 0.28f, 0.92f),
                CommandIconKind.Forces => new Color(0.40f, 0.72f, 1f, 0.92f),
                CommandIconKind.Gem => new Color(0.66f, 0.92f, 1f, 0.92f),
                CommandIconKind.Board => new Color(0.78f, 0.88f, 1f, 0.92f),
                CommandIconKind.Danger => new Color(0.94f, 0.28f, 0.20f, 0.92f),
                _ => Color.Lerp(baseColor, new Color(1f, 0.88f, 0.54f, 0.92f), 0.44f)
            };
        }

        private static Color GetCommandSectionAccent(string label)
        {
            string lower = label?.ToLowerInvariant() ?? string.Empty;
            if (lower.Contains("build"))
            {
                return new Color(0.88f, 0.62f, 0.28f, 1f);
            }

            if (lower.Contains("force"))
            {
                return new Color(0.40f, 0.72f, 1f, 1f);
            }

            if (lower.Contains("progress"))
            {
                return new Color(0.72f, 0.60f, 1f, 1f);
            }

            if (lower.Contains("realm"))
            {
                return new Color(0.72f, 0.88f, 0.42f, 1f);
            }

            return new Color(0.42f, 0.62f, 0.78f, 1f);
        }

        private static Color GetStatusPanelAccent(string panelName)
        {
            string lower = panelName?.ToLowerInvariant() ?? string.Empty;
            if (lower.Contains("district"))
            {
                return new Color(0.88f, 0.62f, 0.28f, 1f);
            }

            if (lower.Contains("force"))
            {
                return new Color(0.40f, 0.72f, 1f, 1f);
            }

            if (lower.Contains("research"))
            {
                return new Color(0.72f, 0.60f, 1f, 1f);
            }

            if (lower.Contains("quest"))
            {
                return new Color(0.74f, 0.88f, 0.54f, 1f);
            }

            if (lower.Contains("territory"))
            {
                return new Color(0.72f, 0.88f, 0.42f, 1f);
            }

            if (lower.Contains("battle"))
            {
                return new Color(0.92f, 0.38f, 0.28f, 1f);
            }

            return new Color(0.42f, 0.62f, 0.78f, 1f);
        }

        private static CommandIconKind GetCommandIconKind(string label)
        {
            string lower = label?.ToLowerInvariant() ?? string.Empty;
            if (lower.Contains("reset"))
            {
                return CommandIconKind.Danger;
            }

            if (lower.Contains("town") || lower.Contains("farm") || lower.Contains("lumber") || lower.Contains("quarry") || lower.Contains("gold") || lower.Contains("mana") || lower == "mine")
            {
                return CommandIconKind.Build;
            }

            if (lower.Contains("infantry") || lower.Contains("ranged") || lower.Contains("capture") || lower.Contains("drill") || lower.Contains("champion"))
            {
                return CommandIconKind.Forces;
            }

            if (lower.Contains("gem") || lower.Contains("wish"))
            {
                return CommandIconKind.Gem;
            }

            if (lower.Contains("board"))
            {
                return CommandIconKind.Board;
            }

            return CommandIconKind.Progress;
        }

        private enum CommandIconKind
        {
            Progress,
            Build,
            Forces,
            Gem,
            Board,
            Danger
        }

        private struct CommandMessageProfile
        {
            public string Header;
            public string Status;
            public string Meta;
            public Color Accent;
            public Color MessageColor;
            public float PulseSeconds;
        }

        private static Font GetDefaultFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                   Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static string FormatCompactNumber(long value)
        {
            long absoluteValue = Math.Abs(value);
            if (absoluteValue >= 1000000)
            {
                return (value / 1000000f).ToString("0.#") + "M";
            }

            if (absoluteValue >= 1000)
            {
                return (value / 1000f).ToString("0.#") + "K";
            }

            return value.ToString();
        }

        private static string FormatResourceLabel(ResourceType type)
        {
            return type switch
            {
                ResourceType.Food => "FOOD",
                ResourceType.Wood => "WOOD",
                ResourceType.Stone => "STONE",
                ResourceType.Gold => "GOLD",
                ResourceType.ManaStone => "MANA",
                ResourceType.Ore => "ORE",
                ResourceType.DeepOre => "D.ORE",
                ResourceType.WorldSap => "SAP",
                ResourceType.RoyalSigil => "SIGIL",
                ResourceType.DarkCrystal => "CRYS",
                _ => type.ToString().ToUpperInvariant()
            };
        }

        private static string FormatResearch(string label, ResearchState state)
        {
            if (state == null)
            {
                return $"{label}: Level 0";
            }

            if (!state.IsResearching)
            {
                return $"{label}: Level {state.Level}, ready";
            }

            long remaining = Math.Max(0, state.CompleteTimestamp - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            return $"{label}: Level {state.Level}, researching {remaining}s";
        }

        private static string FormatLosses(IEnumerable<TroopStack> losses)
        {
            var builder = new StringBuilder();
            foreach (var loss in losses)
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(loss.Type).Append(" ").Append(loss.Count);
            }

            return builder.Length == 0 ? "none" : builder.ToString();
        }

        private static CommandMessageProfile GetMessageProfile(string message)
        {
            string lower = message?.ToLowerInvariant() ?? string.Empty;
            if (lower.Contains("champion duel"))
            {
                if (lower.Contains("victory"))
                {
                    return CreateMessageProfile("CHAMPION DUEL", "VICTORY", "RETURNED", new Color(0.72f, 0.88f, 0.42f, 0.95f), 0.78f);
                }

                if (lower.Contains("defeat"))
                {
                    return CreateMessageProfile("CHAMPION DUEL", "DEFEAT", "RETURNED", new Color(0.86f, 0.34f, 0.22f, 0.95f), 0.92f);
                }

                if (lower.Contains("concluded"))
                {
                    return CreateMessageProfile("CHAMPION DUEL", "RETURNED", "KINGDOM", new Color(0.66f, 0.92f, 1f, 0.95f), 0.78f);
                }

                return CreateMessageProfile("CHAMPION DUEL", "ENGAGED", "GREYBOX ARENA", new Color(0.92f, 0.66f, 0.30f, 0.95f), 0.78f);
            }

            if (lower.Contains("war drill"))
            {
                return lower.Contains("victory")
                    ? CreateMessageProfile("WAR DRILL", "VICTORY", "COMBAT SIM", new Color(0.72f, 0.88f, 0.42f, 0.95f), 0.78f)
                    : CreateMessageProfile("WAR DRILL", "REVIEW", "COMBAT SIM", new Color(0.86f, 0.34f, 0.22f, 0.95f), 0.92f);
            }

            if (lower.Contains("defeat") || lower.Contains("need ") || lower.Contains("could not") || lower.Contains("denied") || lower.Contains("no completed"))
            {
                return CreateMessageProfile("RISK NOTICE", "BLOCKED", "VERIFY ORDER", new Color(0.86f, 0.34f, 0.22f, 0.95f), 0.92f);
            }

            if (lower.Contains("save reset") || lower.Contains("reset"))
            {
                return CreateMessageProfile("SYSTEM NOTICE", "RESET", "BOOT FLOW", new Color(0.92f, 0.45f, 0.30f, 0.95f), 0.90f);
            }

            if (lower.Contains("build order"))
            {
                return CreateMessageProfile("BUILD ORDER", "QUEUED", "DISTRICT OPS", new Color(0.88f, 0.62f, 0.30f, 0.95f), 0.70f);
            }

            if (lower.Contains("research order"))
            {
                return CreateMessageProfile("RESEARCH ORDER", "FILED", "LAB TIMER", new Color(0.54f, 0.76f, 1f, 0.95f), 0.70f);
            }

            if (lower.Contains("muster order"))
            {
                return CreateMessageProfile("MUSTER ORDER", "TRAINING", "FORCE GROWTH", new Color(0.44f, 0.78f, 1f, 0.95f), 0.72f);
            }

            if (lower.Contains("warzone payout"))
            {
                return CreateMessageProfile("WARZONE PAYOUT", "FUNDED", "WAR CHEST", new Color(0.76f, 0.88f, 0.48f, 0.95f), 0.78f);
            }

            if (lower.Contains("warmaster") || lower.Contains("purchased"))
            {
                return CreateMessageProfile("WARMASTER LOG", lower.Contains("complete") ? "COMPLETE" : "ARMING", "SET PROGRESS", new Color(0.92f, 0.66f, 0.30f, 0.95f), 0.78f);
            }

            if (lower.Contains("realm gem"))
            {
                return CreateMessageProfile("REALM GEM", lower.Contains("secured") ? "CARRIER" : "CHECK", "RELIC OPS", new Color(0.66f, 0.92f, 1f, 0.95f), 0.78f);
            }

            if (lower.Contains("wishgate"))
            {
                return CreateMessageProfile("WISHGATE", lower.Contains("selected") ? "REWARD" : "READY", "REALM OBJECTIVE", new Color(0.72f, 0.88f, 1f, 0.95f), 0.78f);
            }

            if (lower.Contains("realm ops") || lower.Contains("captured") || lower.Contains("territory") || lower.Contains("border"))
            {
                return CreateMessageProfile("REALM OPS", lower.Contains("captured") ? "SECURED" : "SCOUTING", "WAR ZONE", new Color(0.72f, 0.88f, 0.42f, 0.95f), 0.76f);
            }

            if (lower.Contains("claimed") || lower.Contains("quest reward") || lower.Contains("objective"))
            {
                return CreateMessageProfile("OBJECTIVE CLAIM", lower.Contains("claimed") ? "CLAIMED" : "STANDBY", "REWARD OPS", new Color(0.74f, 0.88f, 0.54f, 0.95f), 0.72f);
            }

            if (lower.Contains("selected") || lower.Contains("district") || lower.Contains("outpost") || lower.Contains("yield") || lower.Contains("owner"))
            {
                return CreateMessageProfile("FIELD INSPECTION", "SELECTED", "BOARD SCAN", new Color(0.92f, 0.66f, 0.30f, 0.95f), 0.66f);
            }

            if (lower.Contains("command board online"))
            {
                return CreateMessageProfile("COMMAND DOSSIER", "ONLINE", "LIVE OPS", new Color(0.42f, 0.62f, 0.78f, 0.92f), 0.62f);
            }

            return CreateMessageProfile("COMMAND DOSSIER", "LIVE", "COMMAND LOG", new Color(0.42f, 0.62f, 0.78f, 0.92f), 0.62f);
        }

        private static CommandMessageProfile CreateMessageProfile(string header, string status, string meta, Color accent, float pulseSeconds)
        {
            return new CommandMessageProfile
            {
                Header = header,
                Status = status,
                Meta = meta,
                Accent = accent,
                MessageColor = Color.Lerp(new Color(0.92f, 0.96f, 1f, 1f), accent, 0.08f),
                PulseSeconds = pulseSeconds
            };
        }

        private void SetSignalBarsIdle()
        {
            for (int i = 0; i < _messageSignalBars.Count; i++)
            {
                Color idleColor = _messageSignalBaseColor;
                idleColor.a = Mathf.Clamp01(0.20f + i * 0.035f);
                _messageSignalBars[i].color = idleColor;
                _messageSignalBars[i].transform.localScale = Vector3.one;
            }
        }

        private static void SetImageColor(Image image, Color color)
        {
            if (image != null)
            {
                image.color = color;
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private Color GetCurrentRealmAccent()
        {
            RealmId realmId = ServiceLocator.Get<IRealmService>().CurrentRealmId;
            return realmId switch
            {
                RealmId.Stonehold => new Color(0.72f, 0.58f, 0.40f, 1f),
                RealmId.Eldergrove => new Color(0.28f, 0.78f, 0.44f, 1f),
                RealmId.Crownlands => new Color(0.34f, 0.58f, 1f, 1f),
                RealmId.Umbral => new Color(0.68f, 0.26f, 0.92f, 1f),
                _ => new Color(0.42f, 0.68f, 1f, 1f)
            };
        }

        private static string FormatBuildingName(string buildingId)
        {
            if (string.IsNullOrWhiteSpace(buildingId))
            {
                return "District";
            }

            return buildingId
                .Replace("TownHall", "Town Hall")
                .Replace("LumberMill", "Lumber")
                .Replace("GoldMine", "Gold Mine")
                .Replace("ManaShrine", "Mana")
                .Replace("Barracks", "Barracks");
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    internal sealed class KingdomCommandButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform _rectTransform;
        private Image _background;
        private Image _accent;
        private Image _topTrace;
        private Image _iconFrame;
        private Image _actionNotch;
        private Text _label;
        private Selectable _selectable;
        private Color _baseColor;
        private Color _accentColor;
        private Color _accentBaseColor;
        private Color _topTraceBaseColor;
        private Color _iconFrameBaseColor;
        private Color _actionNotchBaseColor;
        private float _hoverAmount;
        private float _pressAmount;
        private float _impactAmount;
        private bool _hovered;
        private bool _pressed;

        public void Configure(Image background, Text label, Image accent, Image topTrace, Image iconFrame, Image actionNotch, Color accentColor)
        {
            _rectTransform = GetComponent<RectTransform>();
            _selectable = GetComponent<Selectable>();
            _background = background;
            _label = label;
            _accent = accent;
            _topTrace = topTrace;
            _iconFrame = iconFrame;
            _actionNotch = actionNotch;
            _accentColor = accentColor;
            _baseColor = background != null ? background.color : new Color(0.105f, 0.138f, 0.178f, 1f);
            _accentBaseColor = accent != null ? accent.color : WithAlpha(accentColor, 0.54f);
            _topTraceBaseColor = topTrace != null ? topTrace.color : new Color(1f, 0.90f, 0.66f, 0.13f);
            _iconFrameBaseColor = iconFrame != null ? iconFrame.color : new Color(0.006f, 0.010f, 0.016f, 0.82f);
            _actionNotchBaseColor = actionNotch != null ? actionNotch.color : WithAlpha(accentColor, 0.18f);
        }

        private void Update()
        {
            if (!CanAnimate())
            {
                ResetDisabledVisuals();
                return;
            }

            float delta = Time.unscaledDeltaTime;
            _hoverAmount = Mathf.MoveTowards(_hoverAmount, _hovered ? 1f : 0f, delta * 9f);
            _pressAmount = Mathf.MoveTowards(_pressAmount, _pressed ? 1f : 0f, delta * 16f);
            _impactAmount = Mathf.MoveTowards(_impactAmount, 0f, delta * 5.8f);
            float pulse = (Mathf.Sin(Time.unscaledTime * 8.6f) + 1f) * 0.5f;
            float state = Mathf.Clamp01(_hoverAmount + _pressAmount * 0.65f + _impactAmount * 0.35f);

            if (_background != null)
            {
                Color hoverColor = Color.Lerp(_baseColor, _accentColor, 0.20f + pulse * 0.06f);
                Color pressedColor = Color.Lerp(_baseColor, Color.black, 0.16f);
                _background.color = Color.Lerp(Color.Lerp(_baseColor, hoverColor, _hoverAmount), pressedColor, _pressAmount * 0.72f);
            }

            if (_accent != null)
            {
                Color color = Color.Lerp(_accentBaseColor, _accentColor, 0.56f + pulse * 0.18f);
                _accent.color = WithAlpha(color, Mathf.Lerp(0.54f, 0.94f, state));
                _accent.rectTransform.localScale = new Vector3(1f, 1f + state * 0.055f, 1f);
            }

            if (_topTrace != null)
            {
                Color color = Color.Lerp(_topTraceBaseColor, Color.Lerp(_accentColor, Color.white, 0.26f), 0.36f + pulse * 0.20f);
                _topTrace.color = WithAlpha(color, Mathf.Lerp(0.13f, 0.72f, state));
            }

            if (_iconFrame != null)
            {
                _iconFrame.color = Color.Lerp(_iconFrameBaseColor, WithAlpha(_accentColor, 0.38f), 0.38f + state * 0.32f);
                _iconFrame.rectTransform.localScale = Vector3.one * (1f + state * 0.055f + _impactAmount * 0.040f);
            }

            if (_actionNotch != null)
            {
                _actionNotch.color = Color.Lerp(_actionNotchBaseColor, WithAlpha(Color.Lerp(_accentColor, Color.white, 0.22f), 0.54f), state * 0.78f + pulse * 0.10f);
                _actionNotch.rectTransform.localScale = new Vector3(1f + state * 0.12f, 1f + state * 0.05f, 1f);
            }

            if (_label != null)
            {
                _label.color = Color.Lerp(new Color(0.92f, 0.96f, 1f), Color.Lerp(_accentColor, Color.white, 0.50f), state * 0.55f);
            }

            if (_rectTransform != null)
            {
                float scale = 1f + _hoverAmount * 0.014f - _pressAmount * 0.026f + _impactAmount * 0.012f;
                _rectTransform.localScale = Vector3.one * scale;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanAnimate())
            {
                ResetDisabledVisuals();
                return;
            }

            _hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!CanAnimate())
            {
                ResetDisabledVisuals();
                return;
            }

            _hovered = false;
            _pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanAnimate())
            {
                ResetDisabledVisuals();
                return;
            }

            _pressed = true;
            _impactAmount = 0.75f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!CanAnimate())
            {
                ResetDisabledVisuals();
                return;
            }

            _pressed = false;
            _impactAmount = Mathf.Max(_impactAmount, 0.58f);
        }

        private bool CanAnimate() =>
            _selectable != null &&
            _selectable.isActiveAndEnabled &&
            _selectable.interactable;

        private void ResetDisabledVisuals()
        {
            _hovered = false;
            _pressed = false;
            _hoverAmount = 0f;
            _pressAmount = 0f;
            _impactAmount = 0f;

            if (_background != null)
            {
                _background.color = _baseColor;
            }

            if (_accent != null)
            {
                _accent.color = _accentBaseColor;
                _accent.rectTransform.localScale = Vector3.one;
            }

            if (_topTrace != null)
            {
                _topTrace.color = _topTraceBaseColor;
            }

            if (_iconFrame != null)
            {
                _iconFrame.color = _iconFrameBaseColor;
                _iconFrame.rectTransform.localScale = Vector3.one;
            }

            if (_actionNotch != null)
            {
                _actionNotch.color = _actionNotchBaseColor;
                _actionNotch.rectTransform.localScale = Vector3.one;
            }

            if (_rectTransform != null)
            {
                _rectTransform.localScale = Vector3.one;
            }

            // The disabled-state owner intentionally paints the muted label.
            // Do not replace it with the active feedback palette here.
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }

    public class KingdomBoardCameraController : MonoBehaviour
    {
        [SerializeField] private float _minZoom = 5.2f;
        [SerializeField] private float _maxZoom = 12.5f;
        [SerializeField] private float _mousePanSpeed = 0.012f;
        [SerializeField] private float _touchPanSpeed = 0.010f;
        [SerializeField] private Vector2 _panLimit = new Vector2(5.8f, 5.2f);

        private UnityEngine.Camera _camera;
        private Vector3 _lastPointerPosition;
        private float _lastPinchDistance;

        public void Configure(UnityEngine.Camera camera)
        {
            _camera = camera;
        }

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = GetComponent<UnityEngine.Camera>();
            }
        }

        private void Update()
        {
            if (_camera == null || !_camera.orthographic)
            {
                return;
            }

            HandleMouse();
            HandleTouch();
        }

        private void HandleMouse()
        {
            if (GameInput.TouchCount > 0)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (!IsPointerOverUi() && Mathf.Abs(mouse.scroll.y.ReadValue()) > 0.01f)
            {
                Zoom(-mouse.scroll.y.ReadValue() * 0.65f);
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                _lastPointerPosition = (Vector3)mouse.position.ReadValue();
            }

            if (mouse.rightButton.isPressed && !IsPointerOverUi())
            {
                Vector3 delta = (Vector3)mouse.position.ReadValue() - _lastPointerPosition;
                Pan(delta, _mousePanSpeed);
                _lastPointerPosition = (Vector3)mouse.position.ReadValue();
            }
        }

        private void HandleTouch()
        {
            if (GameInput.TouchCount == 1)
            {
                EnhancedTouch touch = GameInput.GetTouch(0);
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    _lastPointerPosition = touch.screenPosition;
                }
                else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved && !IsPointerOverUi(touch.touchId))
                {
                    Vector3 delta = (Vector3)touch.screenPosition - _lastPointerPosition;
                    Pan(delta, _touchPanSpeed);
                    _lastPointerPosition = touch.screenPosition;
                }
            }
            else if (GameInput.TouchCount >= 2)
            {
                EnhancedTouch a = GameInput.GetTouch(0);
                EnhancedTouch b = GameInput.GetTouch(1);
                float distance = Vector2.Distance(a.screenPosition, b.screenPosition);
                if (a.phase == UnityEngine.InputSystem.TouchPhase.Began || b.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    _lastPinchDistance = distance;
                    return;
                }

                float delta = distance - _lastPinchDistance;
                Zoom(-delta * 0.012f);
                _lastPinchDistance = distance;
            }
        }

        private void Pan(Vector3 screenDelta, float speed)
        {
            float zoomScale = _camera.orthographicSize / 8.6f;
            Vector3 right = transform.right;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 movement = (-right * screenDelta.x - forward * screenDelta.y) * speed * zoomScale;
            Vector3 next = transform.position + movement;
            next.x = Mathf.Clamp(next.x, -_panLimit.x, _panLimit.x);
            next.z = Mathf.Clamp(next.z, -10.8f - _panLimit.y, -10.8f + _panLimit.y);
            transform.position = next;
        }

        private void Zoom(float delta)
        {
            _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize + delta, _minZoom, _maxZoom);
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private static bool IsPointerOverUi(int fingerId)
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId);
        }
    }
}
