using AL.ChampionMode.AI;
using AL.ChampionMode.Camera;
using AL.ChampionMode.Control;
using AL.ChampionMode.Customization;
using AL.ChampionMode.Skills;
using AL.ChampionMode.UI;
using AL.Core;
using AL.Core.Interfaces;
using AL.RealmWar.World;
using AL.RealmWar.Warzone;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AL.ChampionMode
{
    public class ChampionArenaSceneController : MonoBehaviour
    {
        [SerializeField] private int _dummyCount = 16;
        [SerializeField] private int _botChampionCount = 40;
        [SerializeField] private string _kingdomSceneName = "Kingdom";

        private ChampionController _playerController;
        private ChampionCustomizationController _playerCustomization;
        private AutoCombatController _autoCombatController;
        private ChampionCombat _playerCombat;
        private SkillCaster _playerSkillCaster;
        private CameraFollow _cameraFollow;
        private RvrBotSpawner _rvrBotSpawner;
        private BossDummyAI _boss;
        private Transform _bossTransform;
        private UnityEngine.Camera _arenaCamera;
        private RectTransform _hudCanvasRect;
        private Text _healthText;
        private Text _manaText;
        private Text _skillText;
        private Text _bossText;
        private Text _combatFeedText;
        private Text _encounterTimerText;
        private Text _combatGoalsText;
        private Text _encounterResultText;
        private Image _healthFill;
        private Image _manaFill;
        private Image _bossHealthFill;
        private Image _bossBreakFill;
        private Image _bossStateStrip;
        private Image _damageFlashImage;
        private GameObject _targetLockRoot;
        private RectTransform _targetLockRect;
        private Text _targetLockText;
        private Text _targetLockMetaText;
        private Image _targetLockGlow;
        private Image _targetLockCore;
        private Image _targetLockHealthFill;
        private Image _targetLockBreakFill;
        private readonly Image[] _lowHealthEdges = new Image[4];
        private readonly Image[] _targetLockMarks = new Image[8];
        private readonly Image[] _targetLockTicks = new Image[6];
        private GameObject _defeatPanelObject;
        private Text _defeatSummaryText;
        private Text _defeatDetailText;
        private Text _defeatActionText;
        private GameObject _clearPanelObject;
        private Text _clearTitleText;
        private Text _clearSummaryText;
        private Text _clearDetailText;
        private GameObject _introPanelObject;
        private Image _introTopLetterbox;
        private Image _introBottomLetterbox;
        private Text _introTitleText;
        private Text _introSubtitleText;
        private Text _introCountdownText;
        private Text _appearanceInspectButtonText;
        private Text _appearanceSummaryText;
        private readonly Image[] _appearanceSwatches = new Image[5];
        private readonly Text[] _skillButtonTexts = new Text[4];
        private readonly Text[] _skillCooldownTexts = new Text[4];
        private readonly Image[] _skillCooldownFills = new Image[4];
        private readonly Image[] _skillReadyGlows = new Image[4];
        private readonly Image[] _skillManaPips = new Image[4];
        private Text _castChannelText;
        private Image _castChannelFill;
        private Image _castChannelGlow;
        private float _skillHudTimer;
        private float _warzoneCreditTimer;
        private float _encounterStartTime;
        private float _appearanceFeedTimer;
        private float _lastHealthRatio = 1f;
        private Coroutine _damageFlashRoutine;
        private bool _guardBreakObserved;
        private bool _enrageObserved;
        private bool _encounterClearShown;
        private bool _encounterFailed;
        private bool _encounterIntroRunning;
        private bool _appearanceInspectionMode;
        private GameObject _inspectionShowcaseRoot;
        private GameObject _introStageCueRoot;
        private RuntimePlatformQualityController _qualityController;

        private void Start()
        {
            Bootloader.InitializeIfMissing();
            ApplyRuntimeQuality();
            BuildArena();
            BuildHud();
            StartCoroutine(EncounterIntroRoutine());
        }

        private void Update()
        {
            if (_playerController == null)
            {
                return;
            }

            RefreshLowHealthFeedback();
            UpdateTargetLockIndicator();
            RefreshCastChannel();
            if (_appearanceFeedTimer > 0f)
            {
                _appearanceFeedTimer -= Time.deltaTime;
            }

            _skillHudTimer += Time.deltaTime;
            if (_skillHudTimer >= 0.25f)
            {
                _skillHudTimer = 0f;
                RefreshSkillText();
                RefreshBossText();
                RefreshAppearanceText();
                RefreshEncounterText();
            }

            if (_encounterFailed)
            {
                return;
            }

            if (_encounterIntroRunning)
            {
                return;
            }

            if (_bossTransform == null)
            {
                return;
            }

            _warzoneCreditTimer += Time.deltaTime;
            if (_warzoneCreditTimer < 5f)
            {
                return;
            }

            _warzoneCreditTimer = 0f;
            float distance = Vector3.Distance(_playerController.transform.position, _bossTransform.position);
            if (distance <= 12f)
            {
                ServiceLocator.Get<AL.Core.Interfaces.IWarzoneCreditService>().AddCredits(1);
            }
        }

        private void ApplyRuntimeQuality()
        {
            var qualityObject = new GameObject("RuntimePlatformQuality");
            _qualityController = qualityObject.AddComponent<RuntimePlatformQualityController>();
            _qualityController.Apply();
            _dummyCount = _qualityController.GetDummyBudget(_dummyCount);
            _botChampionCount = _qualityController.GetBotChampionBudget(_botChampionCount);
        }

        private void BuildArena()
        {
            ConfigureArenaLighting();
            BuildArenaEnvironment();
            Color realmAccent = GetRealmAccentColor(GetCurrentRealmId());

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player_Champion";
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 1.1f, -7.4f);
            ApplyMaterial(player, new Color(0.16f, 0.34f, 0.78f), 0.15f, 0.55f);
            _playerCombat = player.AddComponent<ChampionCombat>();
            _playerSkillCaster = player.AddComponent<SkillCaster>();
            _playerController = player.AddComponent<ChampionController>();
            ProceduralChampionModelBuilder.EnsureModel(player);
            _playerCustomization = player.AddComponent<ChampionCustomizationController>();
            _inspectionShowcaseRoot = CreateInspectionShowcase(player.transform, realmAccent);
            _autoCombatController = player.AddComponent<AutoCombatController>();

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            _arenaCamera = camera;
            camera.transform.position = new Vector3(0f, 7.2f, -13.4f);
            camera.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
            camera.fieldOfView = 42f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.03f, 0.04f);
            cameraObject.AddComponent<AudioListener>();
            _cameraFollow = cameraObject.AddComponent<CameraFollow>();
            _cameraFollow.Configure(player.transform, 8.6f, 2.65f, 25f, 0f);

            var boss = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            boss.name = "BossDummy";
            boss.transform.position = new Vector3(0f, 1.8f, 8.6f);
            boss.transform.localScale = new Vector3(1.55f, 1.8f, 1.55f);
            ApplyMaterial(boss, new Color(0.20f, 0.03f, 0.05f), 0.2f, 0.42f);
            DressBossVisual(boss);
            _boss = boss.AddComponent<BossDummyAI>();
            _bossTransform = boss.transform;
            CreateIntroCinematicCues(player.transform, boss.transform, realmAccent);
            _encounterStartTime = Time.time;
            _guardBreakObserved = false;
            _enrageObserved = false;
            _encounterClearShown = false;
            _encounterFailed = false;
            _encounterIntroRunning = false;
            _appearanceInspectionMode = false;

            SpawnBotChampions();

            for (int i = 0; i < _dummyCount; i++)
            {
                var dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dummy.name = "Dummy_" + i;
                float angle = i * Mathf.PI * 2f / _dummyCount;
                float radius = i % 2 == 0 ? 9.8f : 7.6f;
                dummy.transform.position = new Vector3(Mathf.Cos(angle) * radius, 0.58f, Mathf.Sin(angle) * radius);
                dummy.transform.localScale = new Vector3(0.72f, 1.08f, 0.72f);
                ApplyMaterial(dummy, Color.Lerp(new Color(0.48f, 0.05f, 0.12f), new Color(0.20f, 0.08f, 0.34f), i / (float)Mathf.Max(1, _dummyCount - 1)), 0.05f, 0.36f);
                DressTrainingShade(dummy, angle);
            }

            CreateWeather();
            CreateWorldObjectiveMarkers();
        }

        private void ConfigureArenaLighting()
        {
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.035f, 0.04f, 0.055f);
            RenderSettings.fogDensity = 0.018f;

            var lightObject = FindObjectOfType<Light>()?.gameObject ?? new GameObject("Key Light - Moonforge");
            var light = lightObject.GetComponent<Light>() ?? lightObject.AddComponent<Light>();
            light.name = "Key Light - Moonforge";
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            light.color = new Color(0.74f, 0.82f, 1f);
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            CreatePointLight("Boss Rift Light", new Vector3(0f, 3.2f, 8.4f), new Color(1f, 0.18f, 0.08f), 4.2f, 12f);
            CreatePointLight("Player Rim Light", new Vector3(0f, 3.4f, -6.8f), new Color(0.34f, 0.65f, 1f), 2.3f, 8f);
            CreatePointLight("Arena Cold Fill", new Vector3(0f, 5f, 0f), new Color(0.24f, 0.36f, 0.58f), 1.6f, 18f);
        }

        private void BuildArenaEnvironment()
        {
            var environment = new GameObject("ChampionArena_ObsidianCitadel").transform;
            var atmospherePulse = environment.gameObject.AddComponent<ArenaAtmospherePulse>();
            Color realmAccent = GetRealmAccentColor(GetCurrentRealmId());
            Color riftRed = new Color(1f, 0.18f, 0.08f);
            Color coldBlue = new Color(0.24f, 0.56f, 1f);

            CreateArenaPrimitive(environment, "Arena_Foundation", PrimitiveType.Cylinder, new Vector3(0f, -0.16f, 0f), new Vector3(12.8f, 0.18f, 12.8f), Vector3.zero, new Color(0.055f, 0.062f, 0.074f), false, 0.08f, 0.38f);
            CreateArenaPrimitive(environment, "Arena_CombatStone", PrimitiveType.Cylinder, new Vector3(0f, -0.04f, 0f), new Vector3(10.2f, 0.08f, 10.2f), Vector3.zero, new Color(0.10f, 0.112f, 0.128f), false, 0.06f, 0.44f);
            var bossDais = CreateArenaPrimitive(environment, "Boss_Dais", PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 8.6f), new Vector3(3.4f, 0.16f, 3.4f), Vector3.zero, new Color(0.15f, 0.105f, 0.105f), false, 0.1f, 0.5f);
            var playerSigil = CreateArenaPrimitive(environment, "Player_StartSigil", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, -7.4f), new Vector3(1.8f, 0.025f, 1.8f), Vector3.zero, new Color(0.08f, 0.22f, 0.38f), true, 0f, 0.7f);
            atmospherePulse.RegisterRenderer(bossDais, riftRed, 0.3f, 0.36f);
            atmospherePulse.RegisterRenderer(playerSigil, realmAccent, 1.2f, 0.42f);
            CreateArenaGroundDetails(environment, atmospherePulse, realmAccent, riftRed, coldBlue);

            for (int i = 0; i < 18; i++)
            {
                float angle = i * Mathf.PI * 2f / 18f;
                float yaw = -angle * Mathf.Rad2Deg;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 11.4f, 0.72f, Mathf.Sin(angle) * 11.4f);
                CreateArenaPrimitive(environment, "OuterWall_" + i, PrimitiveType.Cube, position, new Vector3(1.7f, 1.4f, 0.36f), new Vector3(0f, yaw, 0f), new Color(0.072f, 0.08f, 0.095f), true, 0.04f, 0.28f);
            }

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f + Mathf.PI / 8f;
                Vector3 basePosition = new Vector3(Mathf.Cos(angle) * 10.4f, 1.1f, Mathf.Sin(angle) * 10.4f);
                var pillar = CreateArenaPrimitive(environment, "RunedPillar_" + i, PrimitiveType.Cylinder, basePosition, new Vector3(0.38f, 1.8f, 0.38f), Vector3.zero, new Color(0.12f, 0.13f, 0.15f), true, 0.08f, 0.36f);
                Color emberColor = i % 2 == 0 ? new Color(0.95f, 0.28f, 0.08f) : new Color(0.20f, 0.58f, 1f);
                var ember = CreateArenaPrimitive(pillar.transform, "PillarEmber", PrimitiveType.Sphere, new Vector3(0f, 0.62f, 0f), new Vector3(0.42f, 0.10f, 0.42f), Vector3.zero, emberColor, true, 0f, 0.82f);
                var pillarLight = CreatePointLight("Pillar Light " + i, basePosition + Vector3.up * 1.3f, emberColor, 1.1f, 5f);
                atmospherePulse.RegisterRenderer(ember, emberColor, i * 0.63f, 0.62f);
                atmospherePulse.RegisterLight(pillarLight, i * 0.47f, 0.16f);
            }

            for (int i = -2; i <= 2; i++)
            {
                var lane = CreateArenaPrimitive(environment, "CombatLane_" + (i + 2), PrimitiveType.Cube, new Vector3(i * 1.15f, 0.035f, 0.4f), new Vector3(0.045f, 0.035f, 15.8f), Vector3.zero, new Color(0.12f, 0.25f, 0.36f), true, 0f, 0.72f);
                atmospherePulse.RegisterRenderer(lane, i == 0 ? realmAccent : coldBlue, i * 0.38f, 0.28f);
            }

            CreateArenaBoundaryDetails(environment, atmospherePulse, realmAccent, riftRed, coldBlue);
            CreateArenaBraziers(environment, atmospherePulse, realmAccent, riftRed);
        }

        private void CreateArenaGroundDetails(Transform environment, ArenaAtmospherePulse atmospherePulse, Color realmAccent, Color riftRed, Color coldBlue)
        {
            for (int i = 0; i < 28; i++)
            {
                float angle = i * Mathf.PI * 2f / 28f;
                float radius = i % 2 == 0 ? 4.85f : 5.28f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0.042f, Mathf.Sin(angle) * radius);
                Vector3 euler = new Vector3(0f, -angle * Mathf.Rad2Deg, 0f);
                Color color = i % 3 == 0 ? realmAccent : Color.Lerp(coldBlue, riftRed, i / 27f);
                var rune = CreateArenaPrimitive(environment, "Floor_RuneStroke_" + i, PrimitiveType.Cube, position, new Vector3(0.72f, 0.022f, 0.040f), euler, color, true, 0f, 0.78f);
                atmospherePulse.RegisterRenderer(rune, color, i * 0.24f, 0.34f);
            }

            for (int i = 0; i < 10; i++)
            {
                float x = -4.5f + i;
                Color color = Color.Lerp(coldBlue, realmAccent, i / 9f);
                var crossLine = CreateArenaPrimitive(environment, "Tactical_Crossline_" + i, PrimitiveType.Cube, new Vector3(x, 0.038f, -0.25f), new Vector3(0.028f, 0.020f, 9.2f), Vector3.zero, color, true, 0f, 0.70f);
                atmospherePulse.RegisterRenderer(crossLine, color, i * 0.19f, 0.18f);
            }
        }

        private void CreateArenaBoundaryDetails(Transform environment, ArenaAtmospherePulse atmospherePulse, Color realmAccent, Color riftRed, Color coldBlue)
        {
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2f / 24f;
                float yaw = -angle * Mathf.Rad2Deg;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 12.35f, 1.58f, Mathf.Sin(angle) * 12.35f);
                Color color = i % 2 == 0 ? new Color(0.10f, 0.11f, 0.13f) : new Color(0.075f, 0.085f, 0.105f);
                CreateArenaPrimitive(environment, "Obsidian_Spine_" + i, PrimitiveType.Cube, position, new Vector3(0.22f, 1.85f, 0.22f), new Vector3(0f, yaw, i % 2 == 0 ? 12f : -12f), color, true, 0.10f, 0.34f);
            }

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f + Mathf.PI / 8f;
                float yaw = -angle * Mathf.Rad2Deg;
                Vector3 basePosition = new Vector3(Mathf.Cos(angle) * 12.1f, 0f, Mathf.Sin(angle) * 12.1f);
                Color bannerColor = i % 3 == 0 ? realmAccent : i % 3 == 1 ? riftRed : coldBlue;
                CreateArenaPrimitive(environment, "Citadel_BannerPole_" + i, PrimitiveType.Cube, basePosition + Vector3.up * 1.88f, new Vector3(0.09f, 3.1f, 0.09f), new Vector3(0f, yaw, 0f), new Color(0.16f, 0.13f, 0.10f), true, 0.30f, 0.48f);
                var banner = CreateArenaPrimitive(environment, "Citadel_WarBanner_" + i, PrimitiveType.Cube, basePosition + new Vector3(Mathf.Cos(angle) * -0.28f, 2.34f, Mathf.Sin(angle) * -0.28f), new Vector3(0.70f, 0.88f, 0.045f), new Vector3(0f, yaw, 0f), bannerColor, true, 0.02f, 0.42f);
                var bannerMark = CreateArenaPrimitive(environment, "Citadel_BannerMark_" + i, PrimitiveType.Cube, basePosition + new Vector3(Mathf.Cos(angle) * -0.31f, 2.34f, Mathf.Sin(angle) * -0.31f), new Vector3(0.46f, 0.055f, 0.052f), new Vector3(0f, yaw, 0f), Color.Lerp(bannerColor, Color.white, 0.36f), true, 0f, 0.72f);
                atmospherePulse.RegisterRenderer(banner, bannerColor, i * 0.36f, 0.16f);
                atmospherePulse.RegisterRenderer(bannerMark, Color.Lerp(bannerColor, Color.white, 0.25f), i * 0.51f, 0.22f);
            }
        }

        private void CreateArenaBraziers(Transform environment, ArenaAtmospherePulse atmospherePulse, Color realmAccent, Color riftRed)
        {
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI * 2f / 6f + Mathf.PI / 6f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 7.6f, 0f, Mathf.Sin(angle) * 7.6f);
                Color flameColor = i % 2 == 0 ? riftRed : realmAccent;
                CreateArenaPrimitive(environment, "WarBrazier_Base_" + i, PrimitiveType.Cylinder, position + Vector3.up * 0.22f, new Vector3(0.46f, 0.24f, 0.46f), Vector3.zero, new Color(0.12f, 0.10f, 0.085f), true, 0.28f, 0.52f);
                CreateArenaPrimitive(environment, "WarBrazier_Crown_" + i, PrimitiveType.Cylinder, position + Vector3.up * 0.58f, new Vector3(0.58f, 0.12f, 0.58f), Vector3.zero, new Color(0.18f, 0.14f, 0.10f), true, 0.34f, 0.58f);
                var flame = CreateArenaPrimitive(environment, "WarBrazier_Flame_" + i, PrimitiveType.Sphere, position + Vector3.up * 0.82f, new Vector3(0.28f, 0.42f, 0.28f), Vector3.zero, flameColor, true, 0f, 0.88f);
                var light = CreatePointLight("WarBrazier Light " + i, position + Vector3.up * 1.18f, flameColor, 1.18f, 5.4f);
                atmospherePulse.RegisterRenderer(flame, flameColor, i * 0.58f, 0.72f);
                atmospherePulse.RegisterLight(light, i * 0.41f, 0.18f);
            }
        }

        private void DressBossVisual(GameObject boss)
        {
            var rootRenderer = boss.GetComponent<Renderer>();
            if (rootRenderer != null)
            {
                rootRenderer.enabled = false;
            }

            var obsidian = new Color(0.055f, 0.045f, 0.052f);
            var bloodPlate = new Color(0.26f, 0.025f, 0.038f);
            var hotCore = new Color(1f, 0.13f, 0.055f);
            var brass = new Color(0.70f, 0.56f, 0.24f);
            var coldEdge = new Color(0.24f, 0.42f, 0.62f);

            CreateArenaPrimitive(boss.transform, "Boss_AuraRing", PrimitiveType.Cylinder, new Vector3(0f, -0.98f, 0f), new Vector3(1.24f, 0.015f, 1.24f), Vector3.zero, new Color(0.95f, 0.05f, 0.025f), true, 0f, 0.82f);
            CreateArenaPrimitive(boss.transform, "Boss_LowerMantle", PrimitiveType.Cylinder, new Vector3(0f, -0.35f, 0f), new Vector3(0.70f, 0.36f, 0.70f), Vector3.zero, obsidian, true, 0.16f, 0.36f);
            CreateArenaPrimitive(boss.transform, "Boss_Torso", PrimitiveType.Cylinder, new Vector3(0f, 0.16f, 0f), new Vector3(0.54f, 0.64f, 0.50f), Vector3.zero, bloodPlate, true, 0.28f, 0.46f);
            CreateArenaPrimitive(boss.transform, "Boss_RibPlate", PrimitiveType.Cube, new Vector3(0f, 0.20f, 0.38f), new Vector3(0.58f, 0.54f, 0.09f), Vector3.zero, Color.Lerp(bloodPlate, Color.black, 0.18f), true, 0.30f, 0.52f);
            CreateArenaPrimitive(boss.transform, "Boss_ChestCore", PrimitiveType.Sphere, new Vector3(0f, 0.24f, 0.50f), new Vector3(0.22f, 0.22f, 0.09f), Vector3.zero, hotCore, true, 0f, 0.92f);
            CreateArenaPrimitive(boss.transform, "Boss_CoreClamp_H", PrimitiveType.Cube, new Vector3(0f, 0.24f, 0.57f), new Vector3(0.48f, 0.045f, 0.04f), Vector3.zero, brass, true, 0.24f, 0.64f);
            CreateArenaPrimitive(boss.transform, "Boss_CoreClamp_V", PrimitiveType.Cube, new Vector3(0f, 0.24f, 0.58f), new Vector3(0.050f, 0.46f, 0.04f), Vector3.zero, brass, true, 0.24f, 0.64f);

            CreateArenaPrimitive(boss.transform, "Boss_Head", PrimitiveType.Sphere, new Vector3(0f, 0.78f, 0.04f), new Vector3(0.34f, 0.30f, 0.30f), Vector3.zero, obsidian, true, 0.18f, 0.44f);
            CreateArenaPrimitive(boss.transform, "Boss_Faceplate", PrimitiveType.Cube, new Vector3(0f, 0.76f, 0.32f), new Vector3(0.33f, 0.22f, 0.055f), Vector3.zero, Color.Lerp(obsidian, coldEdge, 0.24f), true, 0.38f, 0.58f);
            CreateArenaPrimitive(boss.transform, "Boss_Eye_L", PrimitiveType.Sphere, new Vector3(-0.09f, 0.80f, 0.37f), new Vector3(0.055f, 0.030f, 0.030f), Vector3.zero, hotCore, true, 0f, 0.9f);
            CreateArenaPrimitive(boss.transform, "Boss_Eye_R", PrimitiveType.Sphere, new Vector3(0.09f, 0.80f, 0.37f), new Vector3(0.055f, 0.030f, 0.030f), Vector3.zero, hotCore, true, 0f, 0.9f);
            CreateArenaPrimitive(boss.transform, "Boss_Crown", PrimitiveType.Cube, new Vector3(0f, 0.98f, 0f), new Vector3(0.54f, 0.08f, 0.48f), new Vector3(0f, 45f, 0f), brass, true, 0.26f, 0.62f);
            CreateArenaPrimitive(boss.transform, "Boss_CrownSpire", PrimitiveType.Cube, new Vector3(0f, 1.13f, -0.02f), new Vector3(0.12f, 0.28f, 0.10f), new Vector3(0f, 45f, 0f), brass, true, 0.26f, 0.62f);
            CreateArenaPrimitive(boss.transform, "Boss_Horn_L", PrimitiveType.Cube, new Vector3(-0.28f, 1.00f, 0.02f), new Vector3(0.34f, 0.08f, 0.08f), new Vector3(0f, 0f, 24f), brass, true, 0.24f, 0.58f);
            CreateArenaPrimitive(boss.transform, "Boss_Horn_R", PrimitiveType.Cube, new Vector3(0.28f, 1.00f, 0.02f), new Vector3(0.34f, 0.08f, 0.08f), new Vector3(0f, 0f, -24f), brass, true, 0.24f, 0.58f);

            CreateArenaPrimitive(boss.transform, "Boss_LeftShoulder", PrimitiveType.Sphere, new Vector3(-0.58f, 0.31f, 0f), new Vector3(0.27f, 0.20f, 0.27f), Vector3.zero, bloodPlate, true, 0.18f, 0.44f);
            CreateArenaPrimitive(boss.transform, "Boss_RightShoulder", PrimitiveType.Sphere, new Vector3(0.58f, 0.31f, 0f), new Vector3(0.27f, 0.20f, 0.27f), Vector3.zero, bloodPlate, true, 0.18f, 0.44f);
            CreateArenaPrimitive(boss.transform, "Boss_LeftPauldronEdge", PrimitiveType.Cube, new Vector3(-0.76f, 0.36f, 0.01f), new Vector3(0.28f, 0.08f, 0.08f), new Vector3(0f, 0f, -18f), brass, true, 0.26f, 0.62f);
            CreateArenaPrimitive(boss.transform, "Boss_RightPauldronEdge", PrimitiveType.Cube, new Vector3(0.76f, 0.36f, 0.01f), new Vector3(0.28f, 0.08f, 0.08f), new Vector3(0f, 0f, 18f), brass, true, 0.26f, 0.62f);
            CreateArenaPrimitive(boss.transform, "Boss_Arm_L", PrimitiveType.Cube, new Vector3(-0.66f, -0.05f, 0.06f), new Vector3(0.16f, 0.56f, 0.16f), new Vector3(0f, 0f, -10f), obsidian, true, 0.18f, 0.42f);
            CreateArenaPrimitive(boss.transform, "Boss_Arm_R", PrimitiveType.Cube, new Vector3(0.66f, -0.05f, 0.06f), new Vector3(0.16f, 0.56f, 0.16f), new Vector3(0f, 0f, 10f), obsidian, true, 0.18f, 0.42f);
            CreateArenaPrimitive(boss.transform, "Boss_Claw_L", PrimitiveType.Cube, new Vector3(-0.76f, -0.44f, 0.13f), new Vector3(0.20f, 0.06f, 0.18f), new Vector3(0f, 0f, -14f), brass, true, 0.26f, 0.62f);
            CreateArenaPrimitive(boss.transform, "Boss_Claw_R", PrimitiveType.Cube, new Vector3(0.76f, -0.44f, 0.13f), new Vector3(0.20f, 0.06f, 0.18f), new Vector3(0f, 0f, 14f), brass, true, 0.26f, 0.62f);

            CreateArenaPrimitive(boss.transform, "Boss_BackBlade", PrimitiveType.Cube, new Vector3(0f, 0.12f, -0.62f), new Vector3(0.10f, 0.92f, 0.10f), new Vector3(0f, 0f, 22f), brass, true, 0.18f, 0.62f);
            CreateArenaPrimitive(boss.transform, "Boss_BackShard_L", PrimitiveType.Cube, new Vector3(-0.30f, 0.18f, -0.60f), new Vector3(0.08f, 0.70f, 0.08f), new Vector3(0f, 0f, -18f), coldEdge, true, 0.12f, 0.74f);
            CreateArenaPrimitive(boss.transform, "Boss_BackShard_R", PrimitiveType.Cube, new Vector3(0.30f, 0.18f, -0.60f), new Vector3(0.08f, 0.70f, 0.08f), new Vector3(0f, 0f, 18f), coldEdge, true, 0.12f, 0.74f);

            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI * 2f / 6f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 0.72f, 0.58f + Mathf.Sin(i * 1.7f) * 0.08f, Mathf.Sin(angle) * 0.72f);
                CreateArenaPrimitive(boss.transform, "Boss_OrbitShard_" + i, PrimitiveType.Cube, position, new Vector3(0.06f, 0.28f, 0.06f), new Vector3(0f, -angle * Mathf.Rad2Deg, 18f), i % 2 == 0 ? hotCore : coldEdge, true, 0.04f, 0.78f);
            }

            var coreLight = CreatePointLight("Boss Core Glow", boss.transform.position + new Vector3(0f, 1.8f, 0.65f), hotCore, 2.1f, 4.8f);
            coreLight.transform.SetParent(boss.transform, true);
            var crownLight = CreatePointLight("Boss Crown Edge Glow", boss.transform.position + new Vector3(0f, 2.55f, -0.15f), coldEdge, 1.15f, 4.2f);
            crownLight.transform.SetParent(boss.transform, true);
        }

        private void DressTrainingShade(GameObject dummy, float angle)
        {
            CreateArenaPrimitive(dummy.transform, "Shade_Crest", PrimitiveType.Cube, new Vector3(0f, 0.58f, 0.03f), new Vector3(0.30f, 0.12f, 0.08f), new Vector3(0f, angle * Mathf.Rad2Deg, 0f), new Color(0.85f, 0.16f, 0.24f), true, 0f, 0.7f);
        }

        private GameObject CreateArenaPrimitive(Transform parent, string name, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles, Color color, bool removeCollider, float metallic, float smoothness)
        {
            var obj = GameObject.CreatePrimitive(primitive);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localRotation = Quaternion.Euler(localEulerAngles);
            obj.transform.localScale = localScale;
            if (removeCollider)
            {
                var collider = obj.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }
            }

            ApplyMaterial(obj, color, metallic, smoothness);
            return obj;
        }

        private static void ApplyMaterial(GameObject obj, Color color, float metallic, float smoothness)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            var shader = Shader.Find("Standard");
            var material = shader != null ? new Material(shader) : new Material(renderer.material);
            material.color = color;
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            if (smoothness > 0.68f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.75f);
            }

            renderer.material = material;
        }

        private static Light CreatePointLight(string name, Vector3 position, Color color, float intensity, float range)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.position = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            return light;
        }

        private void SpawnBotChampions()
        {
            var spawnerObject = new GameObject("RvrBotSpawner");
            _rvrBotSpawner = spawnerObject.AddComponent<RvrBotSpawner>();
            _rvrBotSpawner.Configure(_playerController.transform, _bossTransform, GetCurrentRealmId(), _botChampionCount);
        }

        private void CreateWeather()
        {
            var weatherObject = new GameObject("Warzone_BattleFog_Weather");
            weatherObject.transform.position = new Vector3(0f, 6f, 0f);
            var weather = weatherObject.AddComponent<RuntimeWeatherController>();
            weather.ConfigureForRealm(GetCurrentRealmId());
            if (_qualityController != null)
            {
                weather.ApplyParticleBudgetMultiplier(_qualityController.GetWeatherParticleMultiplier());
            }
        }

        private GameObject CreateInspectionShowcase(Transform player, Color realmAccent)
        {
            var showcase = new GameObject("ChampionAppearanceInspectionShowcase");
            showcase.transform.SetParent(player, false);
            showcase.transform.localPosition = Vector3.zero;
            showcase.transform.localRotation = Quaternion.identity;

            Color darkGlass = new Color(0.035f, 0.052f, 0.072f, 0.70f);
            Color blackSteel = new Color(0.028f, 0.030f, 0.036f);
            Color warmEdge = Color.Lerp(realmAccent, new Color(1f, 0.82f, 0.44f), 0.22f);
            Color coldEdge = Color.Lerp(realmAccent, new Color(0.24f, 0.56f, 1f), 0.46f);

            CreateArenaPrimitive(showcase.transform, "Inspection_StageBase", PrimitiveType.Cylinder, new Vector3(0f, -1.12f, 0f), new Vector3(1.46f, 0.035f, 1.46f), Vector3.zero, blackSteel, true, 0.22f, 0.68f);
            CreateArenaPrimitive(showcase.transform, "Inspection_StageGlow", PrimitiveType.Cylinder, new Vector3(0f, -1.07f, 0f), new Vector3(1.18f, 0.018f, 1.18f), Vector3.zero, realmAccent, true, 0.04f, 0.86f);
            CreateArenaPrimitive(showcase.transform, "Inspection_InnerGlassDisk", PrimitiveType.Cylinder, new Vector3(0f, -1.035f, 0f), new Vector3(0.92f, 0.012f, 0.92f), Vector3.zero, darkGlass, true, 0.04f, 0.92f);
            CreateArenaPrimitive(showcase.transform, "Inspection_FootRimFront", PrimitiveType.Cube, new Vector3(0f, -0.96f, 0.88f), new Vector3(1.34f, 0.030f, 0.045f), Vector3.zero, warmEdge, true, 0.08f, 0.78f);
            CreateArenaPrimitive(showcase.transform, "Inspection_FootRimBack", PrimitiveType.Cube, new Vector3(0f, -0.96f, -0.88f), new Vector3(1.34f, 0.030f, 0.045f), Vector3.zero, warmEdge, true, 0.08f, 0.78f);
            CreateArenaPrimitive(showcase.transform, "Inspection_FootRimLeft", PrimitiveType.Cube, new Vector3(-0.88f, -0.96f, 0f), new Vector3(0.045f, 0.030f, 1.34f), Vector3.zero, warmEdge, true, 0.08f, 0.78f);
            CreateArenaPrimitive(showcase.transform, "Inspection_FootRimRight", PrimitiveType.Cube, new Vector3(0.88f, -0.96f, 0f), new Vector3(0.045f, 0.030f, 1.34f), Vector3.zero, warmEdge, true, 0.08f, 0.78f);

            var orbit = new GameObject("Inspection_TurntableOrbit");
            orbit.transform.SetParent(showcase.transform, false);
            orbit.transform.localPosition = new Vector3(0f, -1.005f, 0f);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI * 2f / 12f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 1.04f, 0f, Mathf.Sin(angle) * 1.04f);
                Vector3 euler = new Vector3(0f, -angle * Mathf.Rad2Deg, 0f);
                CreateArenaPrimitive(orbit.transform, "Inspection_OrbitNotch_" + i, PrimitiveType.Cube, position, new Vector3(0.30f, 0.016f, 0.036f), euler, i % 2 == 0 ? warmEdge : coldEdge, true, 0.02f, 0.86f);
            }

            CreateArenaPrimitive(showcase.transform, "Inspection_MirrorLeft", PrimitiveType.Cube, new Vector3(-1.08f, -0.12f, -0.10f), new Vector3(0.040f, 1.42f, 0.62f), new Vector3(0f, 18f, 0f), darkGlass, true, 0.02f, 0.90f);
            CreateArenaPrimitive(showcase.transform, "Inspection_MirrorRight", PrimitiveType.Cube, new Vector3(1.08f, -0.12f, -0.10f), new Vector3(0.040f, 1.42f, 0.62f), new Vector3(0f, -18f, 0f), darkGlass, true, 0.02f, 0.90f);
            CreateArenaPrimitive(showcase.transform, "Inspection_MirrorFrameLeft", PrimitiveType.Cube, new Vector3(-1.105f, -0.12f, -0.10f), new Vector3(0.028f, 1.58f, 0.72f), new Vector3(0f, 18f, 0f), coldEdge, true, 0.08f, 0.82f);
            CreateArenaPrimitive(showcase.transform, "Inspection_MirrorFrameRight", PrimitiveType.Cube, new Vector3(1.105f, -0.12f, -0.10f), new Vector3(0.028f, 1.58f, 0.72f), new Vector3(0f, -18f, 0f), coldEdge, true, 0.08f, 0.82f);
            CreateArenaPrimitive(showcase.transform, "Inspection_BackLightSpine", PrimitiveType.Cube, new Vector3(0f, 0.10f, -0.96f), new Vector3(0.080f, 1.54f, 0.065f), Vector3.zero, realmAccent, true, 0.08f, 0.84f);
            CreateArenaPrimitive(showcase.transform, "Inspection_BackCrossbar", PrimitiveType.Cube, new Vector3(0f, 0.72f, -0.96f), new Vector3(0.94f, 0.050f, 0.055f), Vector3.zero, warmEdge, true, 0.10f, 0.80f);
            CreateArenaPrimitive(showcase.transform, "Inspection_BackGlowPanel_L", PrimitiveType.Cube, new Vector3(-0.34f, 0.06f, -1.02f), new Vector3(0.040f, 1.18f, 0.052f), new Vector3(0f, 0f, -10f), coldEdge, true, 0.02f, 0.82f);
            CreateArenaPrimitive(showcase.transform, "Inspection_BackGlowPanel_R", PrimitiveType.Cube, new Vector3(0.34f, 0.06f, -1.02f), new Vector3(0.040f, 1.18f, 0.052f), new Vector3(0f, 0f, 10f), coldEdge, true, 0.02f, 0.82f);

            for (int i = 0; i < 5; i++)
            {
                float offset = -0.48f + i * 0.24f;
                CreateArenaPrimitive(showcase.transform, "Inspection_FloorTrace_" + i, PrimitiveType.Cube, new Vector3(offset, -0.985f, 0.42f), new Vector3(0.045f, 0.014f, 0.48f), new Vector3(0f, i % 2 == 0 ? 18f : -18f, 0f), i % 2 == 0 ? realmAccent : warmEdge, true, 0.02f, 0.82f);
            }

            var keyLight = CreatePointLight("Inspection Key Light", player.position + new Vector3(0.0f, 2.35f, 1.55f), Color.Lerp(realmAccent, Color.white, 0.28f), 1.95f, 4.8f);
            keyLight.transform.SetParent(showcase.transform, true);
            var rimLight = CreatePointLight("Inspection Rim Light", player.position + new Vector3(0.0f, 1.85f, -1.35f), Color.Lerp(realmAccent, new Color(0.24f, 0.56f, 1f), 0.35f), 1.35f, 4.2f);
            rimLight.transform.SetParent(showcase.transform, true);
            var footLight = CreatePointLight("Inspection Foot Light", player.position + new Vector3(0f, 0.35f, 0.35f), warmEdge, 0.82f, 2.4f);
            footLight.transform.SetParent(showcase.transform, true);

            CreateInspectionMotes(showcase.transform, realmAccent, warmEdge);

            var pulse = showcase.AddComponent<ChampionInspectionShowcase>();
            pulse.Configure(realmAccent, warmEdge);
            showcase.SetActive(false);
            return showcase;
        }

        private static void CreateInspectionMotes(Transform parent, Color accent, Color edge)
        {
            var motesObject = new GameObject("Inspection_DetailMotes");
            motesObject.transform.SetParent(parent, false);
            motesObject.transform.localPosition = new Vector3(0f, 0.20f, 0f);
            var particles = motesObject.AddComponent<ParticleSystem>();

            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 4.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.10f, 0.28f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.046f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(accent.r, accent.g, accent.b, 0.46f), new Color(edge.r, edge.g, edge.b, 0.24f));
            main.maxParticles = 72;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = 18f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2.6f, 1.7f, 2.2f);

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.02f, 0.18f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.14f;
            noise.frequency = 0.10f;
            noise.scrollSpeed = 0.22f;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortMode = ParticleSystemSortMode.Distance;
                renderer.sortingOrder = 2;
            }

            particles.Play();
        }

        private void CreateWorldObjectiveMarkers()
        {
            var markerObject = new GameObject("WorldObjectiveMarkers");
            markerObject.transform.position = Vector3.zero;
            int markerBudget = _qualityController != null ? _qualityController.GetWorldMarkerBudget(8) : 8;
            markerObject.AddComponent<WorldObjectiveMarkerSpawner>().Configure(GetCurrentRealmId(), markerBudget);
        }

        private void CreateIntroCinematicCues(Transform player, Transform boss, Color realmAccent)
        {
            if (player == null || boss == null)
            {
                return;
            }

            var root = new GameObject("ChampionIntroCinematicCues");
            _introStageCueRoot = root;

            Color threatRed = new Color(1f, 0.20f, 0.08f);
            Color coldEdge = new Color(0.30f, 0.62f, 1f);
            Vector3 playerGround = new Vector3(player.position.x, 0.07f, player.position.z);
            Vector3 bossGround = new Vector3(boss.position.x, 0.09f, boss.position.z);
            Vector3 laneCenter = Vector3.Lerp(playerGround, bossGround, 0.5f);
            float laneLength = Vector3.Distance(playerGround, bossGround);

            var heroHalo = CreateArenaPrimitive(root.transform, "Intro_Hero_CommandHalo", PrimitiveType.Cylinder, playerGround, new Vector3(2.30f, 0.018f, 2.30f), Vector3.zero, realmAccent, true, 0f, 0.86f);
            var heroInner = CreateArenaPrimitive(root.transform, "Intro_Hero_InnerTrace", PrimitiveType.Cylinder, playerGround + Vector3.up * 0.018f, new Vector3(1.24f, 0.012f, 1.24f), Vector3.zero, coldEdge, true, 0f, 0.74f);
            var bossHalo = CreateArenaPrimitive(root.transform, "Intro_Boss_ThreatHalo", PrimitiveType.Cylinder, bossGround, new Vector3(3.30f, 0.020f, 3.30f), Vector3.zero, threatRed, true, 0f, 0.90f);
            var bossInner = CreateArenaPrimitive(root.transform, "Intro_Boss_BreakRing", PrimitiveType.Cylinder, bossGround + Vector3.up * 0.020f, new Vector3(2.08f, 0.014f, 2.08f), Vector3.zero, new Color(1f, 0.72f, 0.32f), true, 0f, 0.82f);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f;
                Vector3 position = playerGround + new Vector3(Mathf.Cos(angle) * 1.18f, 0.038f, Mathf.Sin(angle) * 1.18f);
                CreateArenaPrimitive(root.transform, "Intro_Hero_Notch_" + i, PrimitiveType.Cube, position, new Vector3(0.34f, 0.018f, 0.055f), new Vector3(0f, -angle * Mathf.Rad2Deg, 0f), Color.Lerp(realmAccent, Color.white, 0.18f), true, 0f, 0.76f);
            }

            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI * 2f / 6f;
                Vector3 position = bossGround + new Vector3(Mathf.Cos(angle) * 1.72f, 0.042f, Mathf.Sin(angle) * 1.72f);
                CreateArenaPrimitive(root.transform, "Intro_Boss_Notch_" + i, PrimitiveType.Cube, position, new Vector3(0.42f, 0.020f, 0.065f), new Vector3(0f, -angle * Mathf.Rad2Deg, 0f), i % 2 == 0 ? threatRed : new Color(1f, 0.72f, 0.32f), true, 0f, 0.80f);
            }

            CreateArenaPrimitive(root.transform, "Intro_PressureLane_Core", PrimitiveType.Cube, laneCenter + Vector3.up * 0.018f, new Vector3(0.060f, 0.018f, laneLength), Vector3.zero, Color.Lerp(realmAccent, Color.white, 0.18f), true, 0f, 0.72f);
            CreateArenaPrimitive(root.transform, "Intro_PressureLane_Left", PrimitiveType.Cube, laneCenter + new Vector3(-0.62f, 0.016f, 0f), new Vector3(0.035f, 0.016f, laneLength * 0.82f), Vector3.zero, coldEdge, true, 0f, 0.62f);
            CreateArenaPrimitive(root.transform, "Intro_PressureLane_Right", PrimitiveType.Cube, laneCenter + new Vector3(0.62f, 0.016f, 0f), new Vector3(0.035f, 0.016f, laneLength * 0.82f), Vector3.zero, threatRed, true, 0f, 0.62f);

            var heroLight = CreatePointLight("Intro Champion Key Light", player.position + new Vector3(-1.1f, 2.8f, -1.8f), Color.Lerp(realmAccent, Color.white, 0.32f), 2.2f, 6.4f);
            heroLight.transform.SetParent(root.transform, true);
            var bossLight = CreatePointLight("Intro Boss Threat Light", boss.position + new Vector3(0.6f, 2.6f, -1.0f), threatRed, 2.9f, 7.2f);
            bossLight.transform.SetParent(root.transform, true);

            var cue = root.AddComponent<ChampionIntroCinematicCue>();
            cue.Configure(heroHalo.transform, heroInner.transform, bossHalo.transform, bossInner.transform);
            root.SetActive(false);
        }

        private RealmId GetCurrentRealmId()
        {
            try
            {
                var realmId = ServiceLocator.Get<IRealmService>().CurrentRealmId;
                return realmId == RealmId.None ? RealmId.Crownlands : realmId;
            }
            catch (System.Exception)
            {
                return RealmId.Crownlands;
            }
        }

        private static Color GetRealmAccentColor(RealmId realmId)
        {
            switch (realmId)
            {
                case RealmId.Stonehold:
                    return new Color(0.84f, 0.68f, 0.42f);
                case RealmId.Eldergrove:
                    return new Color(0.34f, 1f, 0.56f);
                case RealmId.Crownlands:
                    return new Color(0.32f, 0.56f, 1f);
                case RealmId.Umbral:
                    return new Color(0.82f, 0.22f, 1f);
                default:
                    return new Color(0.72f, 0.78f, 0.84f);
            }
        }

        private void BuildHud()
        {
            var canvasObject = new GameObject("ChampionMode_HUD");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _hudCanvasRect = canvasObject.GetComponent<RectTransform>();
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                       Resources.GetBuiltinResource<Font>("Arial.ttf");

            CreateDamageFeedbackLayer(canvasObject.transform);

            var playerPanel = CreateHudPanel(canvasObject.transform, "PlayerFrame", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(430f, 154f), new Color(0.035f, 0.045f, 0.060f, 0.84f));
            CreateText(playerPanel.transform, font, "CHAMPION STATUS", 18, new Vector2(18f, -16f), new Vector2(250f, 24f), TextAnchor.UpperLeft, new Color(0.78f, 0.86f, 1f));
            _healthText = CreateText(playerPanel.transform, font, "HP 1000 / 1000", 18, new Vector2(18f, -48f), new Vector2(220f, 24f), TextAnchor.UpperLeft);
            _manaText = CreateText(playerPanel.transform, font, "MP 100 / 100", 18, new Vector2(18f, -93f), new Vector2(220f, 24f), TextAnchor.UpperLeft);
            _healthFill = CreateStatusBar(playerPanel.transform, new Vector2(176f, -50f), new Vector2(226f, 18f), new Color(0.80f, 0.12f, 0.10f));
            _manaFill = CreateStatusBar(playerPanel.transform, new Vector2(176f, -95f), new Vector2(226f, 18f), new Color(0.20f, 0.48f, 1f));

            var goalsPanel = CreateHudPanel(canvasObject.transform, "CombatGoals", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -190f), new Vector2(430f, 94f), new Color(0.026f, 0.034f, 0.045f, 0.80f));
            CreateText(goalsPanel.transform, font, "COMBAT GOALS", 15, new Vector2(16f, -12f), new Vector2(160f, 20f), TextAnchor.UpperLeft, new Color(0.78f, 0.86f, 1f));
            _encounterTimerText = CreateText(goalsPanel.transform, font, "TIME 00:00", 14, new Vector2(292f, -12f), new Vector2(116f, 20f), TextAnchor.UpperRight, new Color(1f, 0.78f, 0.38f));
            _combatGoalsText = CreateText(goalsPanel.transform, font, "Break Guard\nDefeat Boss", 13, new Vector2(16f, -38f), new Vector2(200f, 42f), TextAnchor.UpperLeft, new Color(0.84f, 0.88f, 0.92f));
            _encounterResultText = CreateText(goalsPanel.transform, font, "Grade pending", 13, new Vector2(214f, -38f), new Vector2(194f, 42f), TextAnchor.UpperRight, new Color(0.84f, 0.88f, 0.92f));

            var bossPanel = CreateHudPanel(canvasObject.transform, "BossFrame", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(820f, 124f), new Color(0.045f, 0.035f, 0.042f, 0.86f));
            _bossStateStrip = CreateHudPanel(bossPanel.transform, "BossStateStrip", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -12f), new Vector2(6f, 96f), new Color(1f, 0.36f, 0.12f, 0.82f));
            CreateText(bossPanel.transform, font, "OBSIDIAN GATE ENCOUNTER", 16, new Vector2(22f, -14f), new Vector2(300f, 24f), TextAnchor.UpperLeft, new Color(1f, 0.74f, 0.45f));
            _bossText = CreateText(bossPanel.transform, font, "Boss: acquiring target", 20, new Vector2(22f, -40f), new Vector2(500f, 50f), TextAnchor.UpperLeft);
            _bossHealthFill = CreateStatusBar(bossPanel.transform, new Vector2(380f, -43f), new Vector2(400f, 20f), new Color(0.88f, 0.10f, 0.08f));
            _bossBreakFill = CreateStatusBar(bossPanel.transform, new Vector2(380f, -76f), new Vector2(400f, 14f), new Color(0.25f, 0.95f, 1f));
            CreateText(bossPanel.transform, font, "HP", 13, new Vector2(346f, -46f), new Vector2(28f, 18f), TextAnchor.UpperLeft, new Color(0.86f, 0.82f, 0.78f));
            CreateText(bossPanel.transform, font, "BREAK", 13, new Vector2(326f, -79f), new Vector2(50f, 18f), TextAnchor.UpperLeft, new Color(0.86f, 0.82f, 0.78f));

            var skillPanel = CreateHudPanel(canvasObject.transform, "CombatHotbar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(748f, 120f), new Color(0.035f, 0.042f, 0.052f, 0.88f));
            _skillText = CreateText(skillPanel.transform, font, "Skill loadout ready", 15, new Vector2(24f, -12f), new Vector2(360f, 22f), TextAnchor.UpperLeft, new Color(0.78f, 0.86f, 1f));
            _castChannelGlow = CreateUiImage(skillPanel.transform, "CastChannelGlow", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(420f, -11f), new Vector2(306f, 24f), new Color(0.25f, 0.62f, 1f, 0.05f));
            _castChannelFill = CreateStatusBar(skillPanel.transform, new Vector2(424f, -15f), new Vector2(298f, 16f), new Color(0.32f, 0.66f, 1f, 0.82f));
            _castChannelText = CreateText(skillPanel.transform, font, "CHANNEL READY", 11, new Vector2(424f, -13f), new Vector2(298f, 18f), TextAnchor.MiddleCenter, new Color(0.72f, 0.82f, 0.92f));
            SetFillAmount(_castChannelFill, 0f);
            for (int i = 0; i < 4; i++)
            {
                CreateSkillButton(skillPanel.transform, font, i, new Vector2(24f + i * 176f, -42f));
            }

            var actionPanel = CreateHudPanel(canvasObject.transform, "CombatActions", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 28f), new Vector2(168f, 310f), new Color(0.035f, 0.042f, 0.052f, 0.82f));
            CreateText(actionPanel.transform, font, "ACTIONS", 15, new Vector2(16f, -16f), new Vector2(136f, 20f), TextAnchor.MiddleCenter, new Color(0.78f, 0.86f, 1f));
            CreateHudButton(actionPanel.transform, font, "Attack", new Vector2(18f, -48f), new Vector2(132f, 42f), () => _playerController.RequestBasicAttack(), 16, new Color(0.24f, 0.08f, 0.08f, 0.95f));
            CreateHudButton(actionPanel.transform, font, "Dodge", new Vector2(18f, -96f), new Vector2(132f, 42f), () => _playerController.RequestDodge(), 16, new Color(0.09f, 0.16f, 0.24f, 0.95f));
            CreateHudButton(actionPanel.transform, font, "Manual", new Vector2(18f, -162f), new Vector2(132f, 34f), () => _autoCombatController.SetMode(AutoMode.Manual), 14);
            CreateHudButton(actionPanel.transform, font, "Assist", new Vector2(18f, -202f), new Vector2(132f, 34f), () => _autoCombatController.SetMode(AutoMode.SemiAuto), 14);
            CreateHudButton(actionPanel.transform, font, "Auto", new Vector2(18f, -242f), new Vector2(132f, 34f), () => _autoCombatController.SetMode(AutoMode.FullAuto), 14);

            var appearancePanel = CreateHudPanel(canvasObject.transform, "AppearanceRack", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(402f, 506f), new Color(0.026f, 0.033f, 0.044f, 0.88f));
            CreateUiImage(appearancePanel.transform, "ForgeTopAccent", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -1f), new Vector2(-24f, 4f), new Color(1f, 0.68f, 0.28f, 0.76f));
            CreateUiImage(appearancePanel.transform, "ForgeSideAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -8f), new Vector2(5f, 486f), new Color(0.24f, 0.56f, 1f, 0.48f));
            CreateText(appearancePanel.transform, font, "CHAMPION FORGE", 16, new Vector2(18f, -14f), new Vector2(178f, 22f), TextAnchor.UpperLeft, new Color(1f, 0.80f, 0.48f));
            CreateText(appearancePanel.transform, font, "COLORS", 12, new Vector2(244f, -16f), new Vector2(64f, 18f), TextAnchor.UpperLeft, new Color(0.78f, 0.86f, 1f));
            for (int i = 0; i < _appearanceSwatches.Length; i++)
            {
                _appearanceSwatches[i] = CreateHudPanel(appearancePanel.transform, "ColorSwatch_" + i, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(244f + i * 27f, -38f), new Vector2(22f, 22f), Color.white);
            }

            CreateUiImage(appearancePanel.transform, "ForgeDividerA", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(-32f, 1f), new Color(0.38f, 0.48f, 0.62f, 0.32f));
            CreateText(appearancePanel.transform, font, "STYLE", 11, new Vector2(18f, -61f), new Vector2(80f, 18f), TextAnchor.UpperLeft, new Color(0.58f, 0.70f, 0.84f));
            CreateText(appearancePanel.transform, font, "LOADOUT", 11, new Vector2(18f, -181f), new Vector2(92f, 18f), TextAnchor.UpperLeft, new Color(0.58f, 0.70f, 0.84f));
            CreateText(appearancePanel.transform, font, "PRESETS", 11, new Vector2(18f, -239f), new Vector2(92f, 18f), TextAnchor.UpperLeft, new Color(0.58f, 0.70f, 0.84f));

            Color colorButton = new Color(0.075f, 0.105f, 0.135f, 0.95f);
            Color styleButton = new Color(0.070f, 0.090f, 0.118f, 0.96f);
            Color gearButton = new Color(0.095f, 0.085f, 0.070f, 0.96f);
            Color vanguardButton = new Color(0.15f, 0.12f, 0.08f, 0.96f);
            Color arcanistButton = new Color(0.06f, 0.12f, 0.18f, 0.96f);
            Color nightbladeButton = new Color(0.14f, 0.055f, 0.065f, 0.96f);
            Color dreadknightButton = new Color(0.15f, 0.035f, 0.030f, 0.96f);
            Color oracleButton = new Color(0.08f, 0.16f, 0.13f, 0.96f);
            Color duelistButton = new Color(0.16f, 0.11f, 0.06f, 0.96f);
            CreateHudButton(appearancePanel.transform, font, "Primary", new Vector2(18f, -84f), new Vector2(112f, 32f), () => { _playerCustomization.CyclePrimaryColor(); RefreshAppearanceText(); }, 13, colorButton);
            CreateHudButton(appearancePanel.transform, font, "Hair", new Vector2(144f, -84f), new Vector2(112f, 32f), () => { _playerCustomization.CycleHairColor(); RefreshAppearanceText(); }, 13, colorButton);
            CreateHudButton(appearancePanel.transform, font, "Skin", new Vector2(270f, -84f), new Vector2(112f, 32f), () => { _playerCustomization.CycleSkinColor(); RefreshAppearanceText(); }, 13, colorButton);
            CreateHudButton(appearancePanel.transform, font, "Hair Style", new Vector2(18f, -122f), new Vector2(112f, 32f), () => { _playerCustomization.CycleHairStyle(); RefreshAppearanceText(); }, 13, styleButton);
            CreateHudButton(appearancePanel.transform, font, "Body", new Vector2(144f, -122f), new Vector2(112f, 32f), () => { _playerCustomization.CycleBodyPreset(); RefreshAppearanceText(); }, 13, styleButton);
            CreateHudButton(appearancePanel.transform, font, "Armor", new Vector2(270f, -122f), new Vector2(112f, 32f), () => { _playerCustomization.CycleArmorStyle(); RefreshAppearanceText(); }, 13, styleButton);
            CreateHudButton(appearancePanel.transform, font, "Eyes", new Vector2(18f, -160f), new Vector2(112f, 32f), () => { _playerCustomization.CycleEyeColor(); RefreshAppearanceText(); }, 13, colorButton);
            CreateHudButton(appearancePanel.transform, font, "Accent", new Vector2(144f, -160f), new Vector2(112f, 32f), () => { _playerCustomization.CycleAccentColor(); RefreshAppearanceText(); }, 13, colorButton);
            CreateHudButton(appearancePanel.transform, font, "Face", new Vector2(270f, -160f), new Vector2(112f, 32f), () => { _playerCustomization.CycleFaceMark(); RefreshAppearanceText(); }, 13, styleButton);
            CreateHudButton(appearancePanel.transform, font, "Weapon", new Vector2(18f, -204f), new Vector2(112f, 32f), () => { _playerCustomization.CycleWeaponStyle(); RefreshAppearanceText(); }, 13, gearButton);
            CreateHudButton(appearancePanel.transform, font, "Offhand", new Vector2(144f, -204f), new Vector2(112f, 32f), () => { _playerCustomization.CycleOffhandStyle(); RefreshAppearanceText(); }, 13, gearButton);
            CreateHudButton(appearancePanel.transform, font, "Cape", new Vector2(270f, -204f), new Vector2(112f, 32f), () => { _playerCustomization.ToggleCape(); RefreshAppearanceText(); }, 13, gearButton);
            CreateHudButton(appearancePanel.transform, font, "Vanguard", new Vector2(18f, -262f), new Vector2(112f, 32f), () => ApplyChampionPreset("vanguard"), 12, vanguardButton);
            CreateHudButton(appearancePanel.transform, font, "Arcanist", new Vector2(144f, -262f), new Vector2(112f, 32f), () => ApplyChampionPreset("arcanist"), 12, arcanistButton);
            CreateHudButton(appearancePanel.transform, font, "Nightblade", new Vector2(270f, -262f), new Vector2(112f, 32f), () => ApplyChampionPreset("nightblade"), 12, nightbladeButton);
            CreateHudButton(appearancePanel.transform, font, "Dread", new Vector2(18f, -300f), new Vector2(112f, 32f), () => ApplyChampionPreset("dreadknight"), 12, dreadknightButton);
            CreateHudButton(appearancePanel.transform, font, "Oracle", new Vector2(144f, -300f), new Vector2(112f, 32f), () => ApplyChampionPreset("oracle"), 12, oracleButton);
            CreateHudButton(appearancePanel.transform, font, "Duelist", new Vector2(270f, -300f), new Vector2(112f, 32f), () => ApplyChampionPreset("duelist"), 12, duelistButton);
            CreateHudButton(appearancePanel.transform, font, "Random", new Vector2(18f, -344f), new Vector2(112f, 30f), () => { _playerCustomization.RandomizeAppearance(); RefreshAppearanceText(); }, 13, new Color(0.16f, 0.13f, 0.08f, 0.95f));
            CreateHudButton(appearancePanel.transform, font, "Reset", new Vector2(144f, -344f), new Vector2(112f, 30f), () => { _playerCustomization.ResetAppearance(); RefreshAppearanceText(); }, 13, new Color(0.10f, 0.11f, 0.13f, 0.95f));
            CreateHudButton(appearancePanel.transform, font, "Helmet", new Vector2(270f, -344f), new Vector2(112f, 30f), () => { _playerCustomization.ToggleHelmet(); RefreshAppearanceText(); }, 13, gearButton);
            var inspectButton = CreateHudButton(appearancePanel.transform, font, "Inspect", new Vector2(270f, -382f), new Vector2(112f, 30f), ToggleAppearanceInspection, 13, new Color(0.10f, 0.14f, 0.19f, 0.95f));
            _appearanceInspectButtonText = inspectButton.GetComponentInChildren<Text>();
            _appearanceSummaryText = CreateText(appearancePanel.transform, font, "Loading appearance", 12, new Vector2(18f, -388f), new Vector2(238f, 62f), TextAnchor.UpperLeft, new Color(0.84f, 0.88f, 0.92f));

            var navPanel = CreateHudPanel(canvasObject.transform, "NavigationPad", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 28f), new Vector2(236f, 188f), new Color(0.035f, 0.042f, 0.052f, 0.80f));
            CreateText(navPanel.transform, font, "MOVE", 15, new Vector2(18f, -14f), new Vector2(88f, 20f), TextAnchor.UpperLeft, new Color(0.78f, 0.86f, 1f));
            CreateMoveButton(navPanel.transform, font, "^", new Vector2(90f, -42f), new Vector2(0, 1));
            CreateMoveButton(navPanel.transform, font, "<", new Vector2(34f, -92f), new Vector2(-1, 0));
            CreateMoveButton(navPanel.transform, font, ">", new Vector2(146f, -92f), new Vector2(1, 0));
            CreateMoveButton(navPanel.transform, font, "v", new Vector2(90f, -142f), new Vector2(0, -1));

            var combatFeedPanel = CreateHudPanel(canvasObject.transform, "CombatFeed", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -296f), new Vector2(560f, 62f), new Color(0.020f, 0.026f, 0.034f, 0.76f));
            _combatFeedText = CreateText(combatFeedPanel.transform, font, "Enter the arena. Break the boss guard before the enrage window.", 16, new Vector2(16f, -10f), new Vector2(526f, 44f), TextAnchor.UpperLeft, new Color(0.84f, 0.88f, 0.92f));
            CreateHudButton(canvasObject.transform, font, "Kingdom", new Vector2(-28f, -268f), new Vector2(132f, 40f), () => SceneManager.LoadScene(_kingdomSceneName), 14, new Color(0.12f, 0.11f, 0.08f, 0.92f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            CreateTargetLockIndicator(canvasObject.transform, font);
            CreateDefeatPanel(canvasObject.transform, font);
            CreateClearPanel(canvasObject.transform, font);
            CreateIntroPanel(canvasObject.transform, font);
            if (_playerCombat != null)
            {
                _playerCombat.OnHealthChanged += UpdateHealthText;
                _playerCombat.OnManaChanged += UpdateManaText;
                _playerCombat.OnDeath += HandlePlayerDeath;
            }

            RefreshSkillText();
            RefreshBossText();
            RefreshAppearanceText();
            RefreshEncounterText();
        }

        private void OnDestroy()
        {
            if (_playerCombat == null)
            {
                return;
            }

            _playerCombat.OnHealthChanged -= UpdateHealthText;
            _playerCombat.OnManaChanged -= UpdateManaText;
            _playerCombat.OnDeath -= HandlePlayerDeath;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private void UpdateHealthText(float current, float max)
        {
            float healthRatio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (_healthText != null)
            {
                _healthText.text = $"HP {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }

            if (healthRatio < _lastHealthRatio - 0.002f)
            {
                PlayDamageFlash(_lastHealthRatio - healthRatio);
            }

            _lastHealthRatio = healthRatio;
            SetFillAmount(_healthFill, healthRatio);
        }

        private void UpdateManaText(float current, float max)
        {
            if (_manaText != null)
            {
                _manaText.text = $"MP {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }

            SetFillAmount(_manaFill, max > 0f ? current / max : 0f);
        }

        private void RefreshSkillText()
        {
            if (_skillText == null || _playerSkillCaster == null)
            {
                return;
            }

            _skillText.text =
                "Loadout: " + _playerSkillCaster.GetSkillName(0) + " / " +
                _playerSkillCaster.GetSkillName(1) + " / " +
                _playerSkillCaster.GetSkillName(2) + " / " +
                _playerSkillCaster.GetSkillName(3);
            RefreshSkillButtonLabels();
        }

        private void RefreshCastChannel()
        {
            if (_castChannelText == null || _castChannelFill == null)
            {
                return;
            }

            if (_playerSkillCaster == null || !_playerSkillCaster.IsCasting)
            {
                SetFillAmount(_castChannelFill, 0f);
                SetImageColor(_castChannelFill, new Color(0.32f, 0.66f, 1f, 0.16f));
                SetImageColor(_castChannelGlow, new Color(0.25f, 0.62f, 1f, 0.05f));
                _castChannelText.text = "CHANNEL READY";
                _castChannelText.color = new Color(0.72f, 0.82f, 0.92f, 0.88f);
                return;
            }

            int activeSlot = Mathf.Clamp(_playerSkillCaster.ActiveSlot, 0, 3);
            float progress = _playerSkillCaster.ActiveCastProgress;
            Color slotColor = GetSkillSlotColor(activeSlot);
            float pulse = (Mathf.Sin(Time.unscaledTime * 8.4f) + 1f) * 0.5f;
            SetFillAmount(_castChannelFill, progress);
            SetImageColor(_castChannelFill, Color.Lerp(slotColor, Color.white, progress * 0.16f));
            SetImageColor(_castChannelGlow, WithAlpha(slotColor, 0.12f + pulse * 0.14f));
            string skillName = GetCompactSkillName(_playerSkillCaster.ActiveSkillName).ToUpperInvariant();
            _castChannelText.text = $"CASTING {skillName}  {Mathf.CeilToInt(progress * 100f)}%";
            _castChannelText.color = Color.Lerp(slotColor, Color.white, 0.26f);
        }

        private void RefreshBossText()
        {
            if (_bossText == null)
            {
                return;
            }

            if (_encounterFailed)
            {
                _bossText.color = new Color(1f, 0.38f, 0.28f);
                _bossText.text = "Champion fallen\nEncounter failed";
                if (_bossStateStrip != null)
                {
                    _bossStateStrip.color = new Color(1f, 0.18f, 0.08f, 0.92f);
                }

                return;
            }

            if (_boss == null || _boss.IsDead)
            {
                _bossText.color = new Color(0.80f, 1f, 0.62f);
                _bossText.text = "Boss defeated\nLoot roll complete";
                if (_bossStateStrip != null)
                {
                    _bossStateStrip.color = new Color(0.42f, 1f, 0.48f, 0.90f);
                }

                SetFillAmount(_bossHealthFill, 0f);
                SetFillAmount(_bossBreakFill, 1f);
                if (_combatFeedText != null)
                {
                    _combatFeedText.text = "Boss defeated. Loot roll complete. Return to Kingdom or keep testing your build.";
                }
                return;
            }

            float healthPercent = _boss.MaxHealth > 0f ? Mathf.Clamp01(_boss.CurrentHealth / _boss.MaxHealth) : 0f;
            float breakPercent = _boss.MaxBreak > 0f ? Mathf.Clamp01(_boss.CurrentBreak / _boss.MaxBreak) : 0f;
            string breakState = _boss.IsBroken ? "BROKEN - damage window" : $"Guard {Mathf.CeilToInt(breakPercent * 100f)}%";
            string enrageState = _boss.IsEnraged ? "ENRAGED" : "Controlled";

            _bossText.color = _boss.IsEnraged
                ? new Color(1f, 0.48f, 0.28f)
                : _boss.IsBroken
                    ? new Color(0.44f, 1f, 0.92f)
                    : Color.white;
            if (_bossStateStrip != null)
            {
                _bossStateStrip.color = _boss.IsEnraged
                    ? new Color(1f, 0.24f, 0.08f, 0.92f)
                    : _boss.IsBroken
                        ? new Color(0.24f, 0.95f, 1f, 0.90f)
                        : new Color(1f, 0.62f, 0.20f, 0.78f);
            }

            _bossText.text =
                $"{_boss.BossName}  {Mathf.CeilToInt(healthPercent * 100f)}%  {enrageState}\n" +
                breakState;
            SetFillAmount(_bossHealthFill, healthPercent);
            SetFillAmount(_bossBreakFill, breakPercent);
            if (_combatFeedText != null && _appearanceFeedTimer <= 0f)
            {
                _combatFeedText.text = _boss.IsTelegraphing
                    ? "Slam windup active. Leave the marked zone, then punish the recovery."
                    : _boss.IsBroken
                        ? "Guard broken. Commit burst skills before the boss recovers."
                        : _boss.IsEnraged
                            ? "Enrage active. Dodge first, punish after the telegraph."
                            : "Pressure the guard bar, hold mana for the break window.";
            }
        }

        private void UpdateTargetLockIndicator()
        {
            if (_targetLockRoot == null)
            {
                return;
            }

            bool shouldShow = _boss != null &&
                              _bossTransform != null &&
                              !_boss.IsDead &&
                              !_encounterFailed &&
                              !_encounterIntroRunning &&
                              !_appearanceInspectionMode;
            if (!shouldShow)
            {
                if (_targetLockRoot.activeSelf)
                {
                    _targetLockRoot.SetActive(false);
                }

                return;
            }

            UnityEngine.Camera camera = _arenaCamera != null ? _arenaCamera : UnityEngine.Camera.main;
            if (camera == null)
            {
                _targetLockRoot.SetActive(false);
                return;
            }

            Vector3 screenPoint = camera.WorldToScreenPoint(_bossTransform.position + Vector3.up * 2.42f + _bossTransform.forward * 0.10f);
            if (screenPoint.z <= 0.05f)
            {
                _targetLockRoot.SetActive(false);
                return;
            }

            screenPoint.x = Mathf.Clamp(screenPoint.x, 168f, Screen.width - 168f);
            screenPoint.y = Mathf.Clamp(screenPoint.y, 132f, Screen.height - 168f);
            if (!_targetLockRoot.activeSelf)
            {
                _targetLockRoot.SetActive(true);
            }

            if (_hudCanvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(_hudCanvasRect, screenPoint, null, out Vector2 localPoint))
            {
                _targetLockRect.anchoredPosition = localPoint;
            }
            else
            {
                _targetLockRect.position = screenPoint;
            }

            float healthRatio = _boss.MaxHealth > 0f ? Mathf.Clamp01(_boss.CurrentHealth / _boss.MaxHealth) : 0f;
            float guardRatio = _boss.MaxBreak > 0f ? Mathf.Clamp01(_boss.CurrentBreak / _boss.MaxBreak) : 0f;
            float distance = _playerController != null ? Vector3.Distance(_playerController.transform.position, _bossTransform.position) : 8f;
            bool isTelegraphing = _boss.IsTelegraphing;
            float pulse = (Mathf.Sin(Time.unscaledTime * (isTelegraphing ? 10.2f : _boss.IsEnraged ? 7.8f : 4.8f)) + 1f) * 0.5f;
            float scale = Mathf.Clamp(1.09f - distance * 0.018f, 0.78f, 1.08f);
            if (isTelegraphing)
            {
                scale += 0.035f + pulse * 0.075f;
            }
            else if (_boss.IsEnraged)
            {
                scale += pulse * 0.055f;
            }
            else if (_boss.IsBroken)
            {
                scale += pulse * 0.035f;
            }

            _targetLockRect.localScale = Vector3.one * scale;
            Color accent = GetTargetLockAccent();
            SetImageColor(_targetLockGlow, WithAlpha(accent, isTelegraphing ? 0.24f + pulse * 0.16f : _boss.IsEnraged ? 0.18f + pulse * 0.12f : 0.09f + pulse * 0.06f));
            SetImageColor(_targetLockCore, Color.Lerp(accent, Color.white, 0.20f + pulse * 0.20f));
            SetImageColor(_targetLockHealthFill, new Color(0.92f, 0.12f, 0.08f, 0.88f));
            SetImageColor(_targetLockBreakFill, _boss.IsBroken ? new Color(0.50f, 1f, 0.92f, 0.95f) : new Color(0.28f, 0.90f, 1f, 0.82f));
            SetFillAmount(_targetLockHealthFill, healthRatio);
            SetFillAmount(_targetLockBreakFill, guardRatio);

            for (int i = 0; i < _targetLockMarks.Length; i++)
            {
                if (_targetLockMarks[i] == null)
                {
                    continue;
                }

                float markPulse = 0.76f + pulse * 0.20f;
                SetImageColor(_targetLockMarks[i], WithAlpha(Color.Lerp(accent, Color.white, i % 2 == 0 ? 0.14f : 0.04f), markPulse));
                _targetLockMarks[i].rectTransform.localScale = Vector3.one * (_boss.IsEnraged ? 1f + pulse * 0.07f : 1f + pulse * 0.025f);
            }

            for (int i = 0; i < _targetLockTicks.Length; i++)
            {
                if (_targetLockTicks[i] == null)
                {
                    continue;
                }

                float tickPulse = Mathf.PingPong(Time.unscaledTime * 2.8f + i * 0.18f, 1f);
                SetImageColor(_targetLockTicks[i], WithAlpha(Color.Lerp(accent, Color.white, 0.22f), 0.20f + tickPulse * 0.42f));
                _targetLockTicks[i].rectTransform.localScale = new Vector3(1f + tickPulse * 0.22f, 1f, 1f);
            }

            if (_targetLockText != null)
            {
                _targetLockText.text = isTelegraphing ? "DODGE SLAM" : _boss.IsEnraged ? "ENRAGE LOCK" : _boss.IsBroken ? "BREAK WINDOW" : "TARGET LOCK";
                _targetLockText.color = Color.Lerp(accent, Color.white, 0.24f);
            }

            if (_targetLockMetaText != null)
            {
                _targetLockMetaText.text = isTelegraphing
                    ? $"SLAM {Mathf.CeilToInt(_boss.TelegraphProgress * 100f)} / EVADE"
                    : $"HP {Mathf.CeilToInt(healthRatio * 100f)} / GUARD {Mathf.CeilToInt(guardRatio * 100f)}";
                _targetLockMetaText.color = new Color(0.86f, 0.92f, 0.98f, 0.88f);
            }
        }

        private void ApplyChampionPreset(string presetId)
        {
            if (_playerCustomization == null || !_playerCustomization.ApplyAppearancePreset(presetId))
            {
                return;
            }

            RefreshAppearanceText();
            if (!_encounterIntroRunning)
            {
                SetAppearanceInspection(true);
            }

            if (_combatFeedText != null)
            {
                _combatFeedText.text = GetChampionPresetMessage(presetId);
                _appearanceFeedTimer = 3.5f;
            }
        }

        private void RefreshAppearanceText()
        {
            if (_playerCustomization == null)
            {
                return;
            }

            if (_appearanceSummaryText != null)
            {
                _appearanceSummaryText.text = _playerCustomization.GetAppearanceSummary();
            }

            SetSwatchColor(0, _playerCustomization.GetPrimaryColor());
            SetSwatchColor(1, _playerCustomization.GetHairColor());
            SetSwatchColor(2, _playerCustomization.GetSkinColor());
            SetSwatchColor(3, _playerCustomization.GetEyeColor());
            SetSwatchColor(4, _playerCustomization.GetAccentColor());
        }

        private void SetSwatchColor(int index, Color color)
        {
            if (index < 0 || index >= _appearanceSwatches.Length || _appearanceSwatches[index] == null)
            {
                return;
            }

            _appearanceSwatches[index].color = new Color(color.r, color.g, color.b, 0.95f);
        }

        private static string GetChampionPresetMessage(string presetId)
        {
            return presetId switch
            {
                "vanguard" => "Forge preset loaded: Vanguard. Heavy plate, shield discipline, and a front-line silhouette.",
                "arcanist" => "Forge preset loaded: Arcanist. Robes, staff focus, and high-contrast arcane accents.",
                "nightblade" => "Forge preset loaded: Nightblade. Lean armor, bow pressure, dagger offhand, and a darker profile.",
                "dreadknight" => "Forge preset loaded: Dreadknight. Massive plate, hammer pressure, ash mask, and a heavier adult silhouette.",
                "oracle" => "Forge preset loaded: Oracle. Tall ritual profile, staff and orb focus, and pale luminous accents.",
                "duelist" => "Forge preset loaded: Duelist. Lean precision frame, sword and dagger, and close-read scar detail.",
                _ => "Forge preset loaded. Fine tune colors, gear, and face marks before entering combat."
            };
        }

        private void RefreshEncounterText()
        {
            float elapsed = Mathf.Max(0f, Time.time - _encounterStartTime);
            bool bossDefeated = _boss == null || _boss.IsDead;
            if (_boss != null)
            {
                _guardBreakObserved |= _boss.IsBroken;
                _enrageObserved |= _boss.IsEnraged;
            }

            if (_encounterTimerText != null)
            {
                _encounterTimerText.text = "TIME " + FormatEncounterTime(elapsed);
            }

            if (_combatGoalsText != null)
            {
                _combatGoalsText.text =
                    $"{GoalMark(_guardBreakObserved)} Break Guard\n" +
                    $"{GoalMark(bossDefeated)} Defeat Boss";
            }

            if (_encounterResultText == null)
            {
                return;
            }

            if (_encounterFailed)
            {
                _encounterResultText.color = new Color(1f, 0.34f, 0.24f);
                _encounterResultText.text = $"FALLEN\n{FormatEncounterTime(elapsed)}";
                return;
            }

            if (!bossDefeated)
            {
                _encounterResultText.color = _enrageObserved ? new Color(1f, 0.58f, 0.32f) : new Color(0.84f, 0.88f, 0.92f);
                _encounterResultText.text = _enrageObserved ? "Enrage survived\nfinish clean" : "Grade pending\nhold pressure";
                return;
            }

            string grade = GetEncounterGrade(elapsed);
            _encounterResultText.color = grade == "S"
                ? new Color(1f, 0.86f, 0.36f)
                : grade == "A"
                    ? new Color(0.58f, 1f, 0.72f)
                    : new Color(0.78f, 0.86f, 1f);
            _encounterResultText.text = $"CLEAR {grade}\n{FormatEncounterTime(elapsed)}";

            if (!_encounterClearShown && _playerController != null)
            {
                _encounterClearShown = true;
                ShowClearPanel(grade, elapsed);
                SkillEffectFactory.SpawnFloatingCombatText(_playerController.transform.position + Vector3.up * 2.6f, "CLEAR " + grade, _encounterResultText.color, 0.36f, 1.4f);
                RuntimeCombatAudio.PlayClear();
            }
        }

        private void ShowClearPanel(string grade, float elapsed)
        {
            SetAppearanceInspection(false);
            _autoCombatController?.SetMode(AutoMode.Manual);
            _playerController?.SetControlLocked(true);

            if (_clearPanelObject != null)
            {
                _clearPanelObject.SetActive(true);
            }

            Color gradeColor = grade == "S"
                ? new Color(1f, 0.86f, 0.36f)
                : grade == "A"
                    ? new Color(0.58f, 1f, 0.72f)
                    : new Color(0.78f, 0.86f, 1f);

            if (_clearTitleText != null)
            {
                _clearTitleText.text = "ENCOUNTER CLEAR " + grade;
                _clearTitleText.color = gradeColor;
            }

            if (_clearSummaryText != null)
            {
                _clearSummaryText.text = $"Time {FormatEncounterTime(elapsed)}   Guard {(_guardBreakObserved ? "broken" : "unbroken")}   Enrage {(_enrageObserved ? "survived" : "avoided")}";
            }

            if (_clearDetailText != null)
            {
                _clearDetailText.text = GetClearRecapLine(grade);
            }

            if (_combatFeedText != null)
            {
                _combatFeedText.text = "Encounter cleared. Review the result, inspect your build, retry, or return to Kingdom.";
            }
        }

        private string GetClearRecapLine(string grade)
        {
            switch (grade)
            {
                case "S":
                    return "Clean pressure window. This build is ready for harder Champion encounters.";
                case "A":
                    return "Strong clear. Tighten break timing or avoid enrage for an elite result.";
                case "B":
                    return "Solid clear. Improve burst timing and dodge discipline before scaling difficulty.";
                default:
                    return "Clear secured. Upgrade the build, practice telegraphs, then retry for a better grade.";
            }
        }

        private void HandlePlayerDeath()
        {
            if (_encounterFailed)
            {
                return;
            }

            _encounterFailed = true;
            SetAppearanceInspection(false);
            _playerController?.SetControlLocked(true);
            _autoCombatController?.SetMode(AutoMode.Manual);
            if (_boss != null)
            {
                _guardBreakObserved |= _boss.IsBroken;
                _enrageObserved |= _boss.IsEnraged;
            }

            float elapsed = Mathf.Max(0f, Time.time - _encounterStartTime);
            UpdateDefeatPanel(elapsed);

            if (_defeatPanelObject != null)
            {
                _defeatPanelObject.SetActive(true);
            }

            if (_combatFeedText != null)
            {
                _combatFeedText.text = "Champion down. Retry the encounter, refine your build, or return to Kingdom.";
            }

            if (_playerController != null)
            {
                SkillEffectFactory.SpawnFloatingCombatText(_playerController.transform.position + Vector3.up * 2.6f, "FALLEN", new Color(1f, 0.28f, 0.18f), 0.38f, 1.35f);
                SkillEffectFactory.ShakeCamera(0.20f, 0.22f);
            }

            RuntimeCombatAudio.PlayWarning();
            RefreshBossText();
            RefreshEncounterText();
        }

        private void UpdateDefeatPanel(float elapsed)
        {
            string bossHealth = GetBossHealthPercentRemaining();

            if (_defeatSummaryText != null)
            {
                _defeatSummaryText.text = $"Time {FormatEncounterTime(elapsed)}   Boss {bossHealth}   Guard {(_guardBreakObserved ? "broken" : "held")}   Enrage {(_enrageObserved ? "triggered" : "avoided")}";
            }

            if (_defeatDetailText != null)
            {
                _defeatDetailText.text = GetDefeatRecapLine(elapsed);
            }

            if (_defeatActionText != null)
            {
                _defeatActionText.text = "Next: retry for execution, inspect your champion, or return to Kingdom upgrades.";
            }
        }

        private string GetDefeatRecapLine(float elapsed)
        {
            if (!_guardBreakObserved)
            {
                return "Guard held. Build pressure until the break bar collapses, then spend burst skills inside that window.";
            }

            if (GetBossHealthRatioRemaining() <= 0.18f)
            {
                return "Boss was nearly finished. Save a defensive response for enrage, dodge first, then punish the recovery.";
            }

            if (_enrageObserved)
            {
                return "Enrage overwhelmed the run. Respect the marked slam, then return to close range for controlled damage.";
            }

            if (elapsed <= 45f)
            {
                return "Early fall. Slow the opener, keep mana for the guard break, and dodge before committing.";
            }

            return "The run reached a clearable pace. Tighten dodge timing and hold burst for the next break window.";
        }

        private float GetBossHealthRatioRemaining()
        {
            return _boss != null && _boss.MaxHealth > 0f ? Mathf.Clamp01(_boss.CurrentHealth / _boss.MaxHealth) : 1f;
        }

        private string GetBossHealthPercentRemaining()
        {
            if (_boss == null || _boss.MaxHealth <= 0f)
            {
                return "unknown";
            }

            return $"{Mathf.CeilToInt(GetBossHealthRatioRemaining() * 100f)}%";
        }

        private IEnumerator EncounterIntroRoutine()
        {
            _encounterIntroRunning = true;
            _playerController?.SetControlLocked(true);
            _autoCombatController?.SetMode(AutoMode.Manual);
            _encounterStartTime = Time.time;

            SetIntroPresentationActive(true);

            if (_combatFeedText != null)
            {
                _combatFeedText.text = "Encounter initializing. Read the boss posture, then commit burst skills.";
            }

            Vector3 playerLook = GetPlayerIntroLookPoint();
            Vector3 bossLook = GetBossIntroLookPoint();
            Vector3 arenaLook = Vector3.Lerp(playerLook, bossLook, 0.54f) + Vector3.up * 0.25f;

            _cameraFollow?.SetCinematicShot(playerLook + new Vector3(-3.2f, 1.15f, 2.85f), playerLook, 34f, 0.10f);
            SetIntroText("CHAMPION READY", "Forge identity locked. Read the arena before committing.", "3");
            RuntimeCombatAudio.PlayWarning();
            yield return new WaitForSecondsRealtime(0.72f);

            _cameraFollow?.SetCinematicShot(bossLook + new Vector3(3.6f, 1.05f, -3.35f), bossLook, 33f, 0.12f);
            SetIntroText("BOSS TARGET ACQUIRED", "Break the guard, dodge the slam, punish the recovery.", "2");
            RuntimeCombatAudio.PlayWarning();
            yield return new WaitForSecondsRealtime(0.74f);

            _cameraFollow?.SetCinematicShot(new Vector3(0f, 8.2f, -12.4f), arenaLook, 45f, 0.14f);
            SetIntroText("TACTICAL WINDOW", "Manual control ready. Hold mana for the break window.", "1");
            RuntimeCombatAudio.PlayWarning();
            yield return new WaitForSecondsRealtime(0.70f);

            _cameraFollow?.SetCinematicShot(new Vector3(0f, 6.8f, -10.8f), arenaLook, 42f, 0.10f);
            SetIntroText("ENGAGE", "Pressure the boss guard now.", "GO");
            RuntimeCombatAudio.PlayClear();
            yield return new WaitForSecondsRealtime(0.42f);

            SetIntroPresentationActive(false);
            _cameraFollow?.ClearCinematicShot();

            _encounterStartTime = Time.time;
            _encounterIntroRunning = false;
            if (!_encounterFailed && !_appearanceInspectionMode && _playerCombat != null && !_playerCombat.IsDead)
            {
                _playerController?.SetControlLocked(false);
            }

            if (_combatFeedText != null)
            {
                _combatFeedText.text = "Pressure the guard bar, hold mana for the break window.";
            }

            RefreshEncounterText();
        }

        private void SetIntroText(string title, string subtitle, string countdown)
        {
            if (_introTitleText != null)
            {
                _introTitleText.text = title;
            }

            if (_introSubtitleText != null)
            {
                _introSubtitleText.text = subtitle;
            }

            if (_introCountdownText != null)
            {
                _introCountdownText.text = countdown;
            }
        }

        private void SetIntroPresentationActive(bool isActive)
        {
            if (_introPanelObject != null)
            {
                _introPanelObject.SetActive(isActive);
            }

            if (_introTopLetterbox != null)
            {
                _introTopLetterbox.gameObject.SetActive(isActive);
            }

            if (_introBottomLetterbox != null)
            {
                _introBottomLetterbox.gameObject.SetActive(isActive);
            }

            if (_introStageCueRoot != null)
            {
                _introStageCueRoot.SetActive(isActive);
            }
        }

        private Vector3 GetPlayerIntroLookPoint()
        {
            if (_playerController == null)
            {
                return new Vector3(0f, 1.75f, -7.4f);
            }

            return _playerController.transform.position + new Vector3(0f, 0.80f, 0.12f);
        }

        private Vector3 GetBossIntroLookPoint()
        {
            if (_bossTransform == null)
            {
                return new Vector3(0f, 2.55f, 8.6f);
            }

            return _bossTransform.position + new Vector3(0f, 0.88f, 0.10f);
        }

        private void ToggleAppearanceInspection()
        {
            if (_encounterFailed || _encounterIntroRunning)
            {
                return;
            }

            SetAppearanceInspection(!_appearanceInspectionMode);
        }

        private void SetAppearanceInspection(bool enabled)
        {
            _appearanceInspectionMode = enabled;
            _cameraFollow?.SetInspectionMode(enabled);
            if (_inspectionShowcaseRoot != null)
            {
                _inspectionShowcaseRoot.SetActive(enabled);
            }

            if (_appearanceInspectButtonText != null)
            {
                _appearanceInspectButtonText.text = enabled ? "Resume" : "Inspect";
            }

            if (enabled)
            {
                _autoCombatController?.SetMode(AutoMode.Manual);
                _playerController?.SetControlLocked(true);
                if (_combatFeedText != null)
                {
                    _combatFeedText.text = "Inspection mode active. Adjust appearance, orbit with right drag or touch, then resume combat.";
                }
            }
            else if (!_encounterFailed && !_encounterIntroRunning && _playerCombat != null && !_playerCombat.IsDead)
            {
                _playerController?.SetControlLocked(false);
                if (_combatFeedText != null)
                {
                    _combatFeedText.text = "Inspection closed. Pressure the guard bar, hold mana for the break window.";
                }
            }
        }

        private static void RetryEncounter()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private string GetEncounterGrade(float elapsed)
        {
            if (_guardBreakObserved && !_enrageObserved && elapsed <= 60f)
            {
                return "S";
            }

            if (_guardBreakObserved && elapsed <= 90f)
            {
                return "A";
            }

            return elapsed <= 130f ? "B" : "C";
        }

        private static string GoalMark(bool isDone)
        {
            return isDone ? "[x]" : "[ ]";
        }

        private static string FormatEncounterTime(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int minutes = totalSeconds / 60;
            int remainder = totalSeconds % 60;
            return $"{minutes:00}:{remainder:00}";
        }

        private string FormatSkillStatus(int slotIndex)
        {
            float remaining = _playerSkillCaster.GetCooldownRemaining(slotIndex);
            string state = remaining <= 0.05f ? $"ready ({_playerSkillCaster.GetManaCost(slotIndex):0} MP)" : $"{remaining:0.0}s";
            return $"{slotIndex + 1}. {_playerSkillCaster.GetSkillName(slotIndex)}: {state}";
        }

        private void CreateSkillButton(Transform parent, Font font, int slotIndex, Vector2 anchoredPosition)
        {
            Color slotColor = GetSkillSlotColor(slotIndex);
            var button = CreateHudButton(parent, font, BuildSkillButtonLabel(slotIndex), anchoredPosition, new Vector2(154f, 58f), () => _playerController.RequestSkill(slotIndex), 14, new Color(0.06f, 0.09f, 0.13f, 0.96f));
            CreateHudPanel(button.transform, "SkillAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(154f, 5f), slotColor);
            _skillReadyGlows[slotIndex] = CreateUiImage(button.transform, "SkillReadyGlow", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(25f, -30f), new Vector2(42f, 42f), new Color(slotColor.r, slotColor.g, slotColor.b, 0.16f));
            CreateSkillIconTile(button.transform, slotIndex, slotColor);
            _skillCooldownFills[slotIndex] = CreateCooldownOverlay(button.transform);
            _skillManaPips[slotIndex] = CreateUiImage(button.transform, "SkillManaPip", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(48f, 7f), new Vector2(92f, 3f), new Color(slotColor.r, slotColor.g, slotColor.b, 0.72f));
            _skillButtonTexts[slotIndex] = button.GetComponentInChildren<Text>();
            if (_skillButtonTexts[slotIndex] != null)
            {
                _skillButtonTexts[slotIndex].alignment = TextAnchor.MiddleLeft;
                var labelRect = _skillButtonTexts[slotIndex].GetComponent<RectTransform>();
                labelRect.offsetMin = new Vector2(48f, 20f);
                labelRect.offsetMax = new Vector2(-8f, -6f);
            }

            _skillCooldownTexts[slotIndex] = CreateText(button.transform, font, "Ready", 12, new Vector2(48f, -37f), new Vector2(98f, 18f), TextAnchor.MiddleLeft, new Color(0.78f, 0.86f, 1f));
        }

        private static void CreateSkillIconTile(Transform parent, int slotIndex, Color slotColor)
        {
            Color frameColor = new Color(0.015f, 0.020f, 0.028f, 0.96f);
            Color innerColor = Color.Lerp(slotColor, Color.white, 0.10f);
            var frame = CreateHudPanel(parent, "SkillIconFrame", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -13f), new Vector2(34f, 34f), frameColor);
            CreateHudPanel(frame.transform, "SkillIconCore", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f), new Color(slotColor.r, slotColor.g, slotColor.b, 0.26f));

            switch (slotIndex)
            {
                case 0:
                    CreateSkillIconStroke(frame.transform, "SlashMain", new Vector2(17f, -17f), new Vector2(25f, 4f), -34f, innerColor);
                    CreateSkillIconStroke(frame.transform, "SlashEdge", new Vector2(17f, -17f), new Vector2(15f, 2f), -34f, Color.Lerp(innerColor, Color.white, 0.42f));
                    break;
                case 1:
                    CreateSkillIconStroke(frame.transform, "GuardTop", new Vector2(17f, -11f), new Vector2(18f, 4f), 0f, innerColor);
                    CreateSkillIconStroke(frame.transform, "GuardCenter", new Vector2(17f, -19f), new Vector2(12f, 12f), 0f, Color.Lerp(innerColor, Color.white, 0.22f));
                    break;
                case 2:
                    CreateSkillIconStroke(frame.transform, "BurstHorizontal", new Vector2(17f, -17f), new Vector2(24f, 4f), 0f, innerColor);
                    CreateSkillIconStroke(frame.transform, "BurstVertical", new Vector2(17f, -17f), new Vector2(4f, 24f), 0f, Color.Lerp(innerColor, Color.white, 0.34f));
                    break;
                default:
                    CreateSkillIconStroke(frame.transform, "BreakerLeft", new Vector2(13f, -17f), new Vector2(22f, 4f), -54f, innerColor);
                    CreateSkillIconStroke(frame.transform, "BreakerRight", new Vector2(21f, -17f), new Vector2(22f, 4f), 54f, Color.Lerp(innerColor, Color.white, 0.28f));
                    break;
            }
        }

        private static void CreateSkillIconStroke(Transform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta, float rotationDegrees, Color color)
        {
            var stroke = CreateHudPanel(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), anchoredPosition, sizeDelta, color);
            stroke.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
        }

        private void RefreshSkillButtonLabels()
        {
            for (int i = 0; i < _skillButtonTexts.Length; i++)
            {
                if (_skillButtonTexts[i] != null)
                {
                    _skillButtonTexts[i].text = BuildSkillButtonLabel(i);
                }

                if (_skillCooldownTexts[i] != null && _playerSkillCaster != null)
                {
                    float remaining = _playerSkillCaster.GetCooldownRemaining(i);
                    string state = remaining <= 0.05f ? $"{_playerSkillCaster.GetManaCost(i):0} MP" : $"{remaining:0.0}s";
                    _skillCooldownTexts[i].text = state;
                    _skillCooldownTexts[i].color = remaining <= 0.05f ? new Color(0.70f, 1f, 0.78f) : new Color(1f, 0.68f, 0.40f);
                    if (_skillCooldownFills[i] != null)
                    {
                        float duration = Mathf.Max(0.01f, _playerSkillCaster.GetCooldownDuration(i));
                        _skillCooldownFills[i].fillAmount = remaining <= 0.05f ? 0f : Mathf.Clamp01(remaining / duration);
                        _skillCooldownFills[i].color = remaining <= 0.05f ? new Color(0f, 0f, 0f, 0f) : new Color(0f, 0f, 0f, 0.56f);
                    }

                    UpdateSkillReadinessVisual(i, remaining <= 0.05f);
                }
            }
        }

        private void UpdateSkillReadinessVisual(int slotIndex, bool isReady)
        {
            Color slotColor = GetSkillSlotColor(slotIndex);
            float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 5.4f + slotIndex * 0.72f) * 0.5f;
            if (_skillReadyGlows[slotIndex] != null)
            {
                float alpha = isReady ? 0.18f + pulse * 0.18f : 0.035f;
                _skillReadyGlows[slotIndex].color = new Color(slotColor.r, slotColor.g, slotColor.b, alpha);
                _skillReadyGlows[slotIndex].rectTransform.localScale = Vector3.one * (isReady ? 1.0f + pulse * 0.08f : 0.92f);
            }

            if (_skillManaPips[slotIndex] != null)
            {
                _skillManaPips[slotIndex].color = isReady
                    ? new Color(slotColor.r, slotColor.g, slotColor.b, 0.78f + pulse * 0.16f)
                    : new Color(0.32f, 0.38f, 0.44f, 0.42f);
            }
        }

        private static Image CreateCooldownOverlay(Transform parent)
        {
            var overlayObject = new GameObject("CooldownOverlay");
            overlayObject.transform.SetParent(parent, false);
            var overlay = overlayObject.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0f);
            overlay.type = Image.Type.Filled;
            overlay.fillMethod = Image.FillMethod.Radial360;
            overlay.fillOrigin = (int)Image.Origin360.Top;
            overlay.fillClockwise = false;
            overlay.fillAmount = 0f;
            var rect = overlayObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return overlay;
        }

        private static Color GetSkillSlotColor(int slotIndex)
        {
            switch (slotIndex)
            {
                case 0:
                    return new Color(0.30f, 0.62f, 1f, 0.92f);
                case 1:
                    return new Color(0.42f, 1f, 0.54f, 0.92f);
                case 2:
                    return new Color(1f, 0.52f, 0.16f, 0.92f);
                default:
                    return new Color(0.95f, 0.22f, 0.16f, 0.92f);
            }
        }

        private Color GetTargetLockAccent()
        {
            if (_boss == null)
            {
                return new Color(1f, 0.58f, 0.18f, 0.82f);
            }

            if (_boss.IsTelegraphing)
            {
                return new Color(1f, 0.14f, 0.04f, 0.96f);
            }

            if (_boss.IsEnraged)
            {
                return new Color(1f, 0.22f, 0.08f, 0.92f);
            }

            if (_boss.IsBroken)
            {
                return new Color(0.38f, 1f, 0.92f, 0.92f);
            }

            return new Color(1f, 0.58f, 0.18f, 0.82f);
        }

        private string BuildSkillButtonLabel(int slotIndex)
        {
            string compactName = GetCompactSkillName(_playerSkillCaster != null ? _playerSkillCaster.GetSkillName(slotIndex) : string.Empty);
            return $"{slotIndex + 1}. {compactName}";
        }

        private static string GetCompactSkillName(string skillName)
        {
            if (string.IsNullOrWhiteSpace(skillName))
            {
                return "Skill";
            }

            string[] words = skillName.Split(' ');
            return words.Length == 0 ? skillName : words[words.Length - 1];
        }

        private void CreateMoveButton(Transform parent, Font font, string label, Vector2 anchoredPosition, Vector2 moveInput)
        {
            var button = CreateHudButton(parent, font, label, anchoredPosition, new Vector2(56f, 42f), null, 13);
            button.gameObject.AddComponent<ChampionMoveButton>().Setup(_playerController, moveInput);
        }

        private void CreateTargetLockIndicator(Transform parent, Font font)
        {
            _targetLockRoot = new GameObject("BossTargetLock");
            _targetLockRoot.transform.SetParent(parent, false);
            _targetLockRect = _targetLockRoot.AddComponent<RectTransform>();
            SetRect(_targetLockRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210f, 188f));

            Color accent = new Color(1f, 0.58f, 0.18f, 0.78f);
            _targetLockGlow = CreateUiImage(_targetLockRoot.transform, "TargetLockGlow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(158f, 70f), WithAlpha(accent, 0.10f));
            _targetLockCore = CreateUiImage(_targetLockRoot.transform, "TargetLockCore", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f), Color.Lerp(accent, Color.white, 0.18f));

            int mark = 0;
            _targetLockMarks[mark++] = CreateTargetLockMark(_targetLockRoot.transform, "LockMark_TopLeft_H", new Vector2(-72f, 34f), new Vector2(40f, 3f), 0f, accent);
            _targetLockMarks[mark++] = CreateTargetLockMark(_targetLockRoot.transform, "LockMark_TopLeft_V", new Vector2(-92f, 16f), new Vector2(3f, 36f), 0f, accent);
            _targetLockMarks[mark++] = CreateTargetLockMark(_targetLockRoot.transform, "LockMark_TopRight_H", new Vector2(72f, 34f), new Vector2(40f, 3f), 0f, accent);
            _targetLockMarks[mark++] = CreateTargetLockMark(_targetLockRoot.transform, "LockMark_TopRight_V", new Vector2(92f, 16f), new Vector2(3f, 36f), 0f, accent);
            _targetLockMarks[mark++] = CreateTargetLockMark(_targetLockRoot.transform, "LockMark_BottomLeft_H", new Vector2(-72f, -34f), new Vector2(40f, 3f), 0f, accent);
            _targetLockMarks[mark++] = CreateTargetLockMark(_targetLockRoot.transform, "LockMark_BottomLeft_V", new Vector2(-92f, -16f), new Vector2(3f, 36f), 0f, accent);
            _targetLockMarks[mark++] = CreateTargetLockMark(_targetLockRoot.transform, "LockMark_BottomRight_H", new Vector2(72f, -34f), new Vector2(40f, 3f), 0f, accent);
            _targetLockMarks[mark] = CreateTargetLockMark(_targetLockRoot.transform, "LockMark_BottomRight_V", new Vector2(92f, -16f), new Vector2(3f, 36f), 0f, accent);

            for (int i = 0; i < _targetLockTicks.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / _targetLockTicks.Length;
                Vector2 position = new Vector2(Mathf.Cos(angle) * 66f, Mathf.Sin(angle) * 28f);
                _targetLockTicks[i] = CreateTargetLockMark(_targetLockRoot.transform, "LockTick_" + i, position, new Vector2(18f, 2.5f), -angle * Mathf.Rad2Deg, WithAlpha(accent, 0.32f));
            }

            _targetLockText = CreateTargetLockText(_targetLockRoot.transform, font, "TARGET LOCK", 12, new Vector2(0f, -47f), new Vector2(172f, 18f), TextAnchor.MiddleCenter, Color.Lerp(accent, Color.white, 0.20f));
            _targetLockMetaText = CreateTargetLockText(_targetLockRoot.transform, font, "HP 100 / GUARD 100", 10, new Vector2(0f, -62f), new Vector2(172f, 16f), TextAnchor.MiddleCenter, new Color(0.84f, 0.90f, 0.96f, 0.84f));
            _targetLockHealthFill = CreateTargetLockBar(_targetLockRoot.transform, "TargetLockHealth", new Vector2(0f, -78f), new Vector2(118f, 5f), new Color(0.92f, 0.12f, 0.08f, 0.88f));
            _targetLockBreakFill = CreateTargetLockBar(_targetLockRoot.transform, "TargetLockBreak", new Vector2(0f, -86f), new Vector2(118f, 4f), new Color(0.28f, 0.90f, 1f, 0.82f));
            _targetLockRoot.SetActive(false);
        }

        private static Image CreateTargetLockMark(Transform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta, float rotationDegrees, Color color)
        {
            var mark = CreateUiImage(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, sizeDelta, color);
            mark.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            return mark;
        }

        private static Text CreateTargetLockText(Transform parent, Font font, string value, int size, Vector2 anchoredPosition, Vector2 sizeDelta, TextAnchor alignment, Color color)
        {
            var text = CreateText(parent, font, value, size, Vector2.zero, sizeDelta, alignment, color);
            SetRect(text.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, sizeDelta);
            return text;
        }

        private static Image CreateTargetLockBar(Transform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta, Color fillColor)
        {
            var frame = CreateUiImage(parent, name + "_Frame", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, sizeDelta, new Color(0.005f, 0.008f, 0.012f, 0.68f));
            var fillObject = new GameObject(name + "_Fill");
            fillObject.transform.SetParent(frame.transform, false);
            var fill = fillObject.AddComponent<Image>();
            fill.raycastTarget = false;
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            var rect = fillObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(1f, 1f);
            rect.offsetMax = new Vector2(-1f, -1f);
            return fill;
        }

        private void CreateDamageFeedbackLayer(Transform parent)
        {
            _damageFlashImage = CreateUiImage(parent, "DamageFlash", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(1f, 0.04f, 0.02f, 0f));
            _lowHealthEdges[0] = CreateUiImage(parent, "LowHealthTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 28f), new Color(1f, 0.04f, 0.02f, 0f));
            _lowHealthEdges[1] = CreateUiImage(parent, "LowHealthBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 34f), new Color(1f, 0.04f, 0.02f, 0f));
            _lowHealthEdges[2] = CreateUiImage(parent, "LowHealthLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(28f, 0f), new Color(1f, 0.04f, 0.02f, 0f));
            _lowHealthEdges[3] = CreateUiImage(parent, "LowHealthRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(28f, 0f), new Color(1f, 0.04f, 0.02f, 0f));
        }

        private void PlayDamageFlash(float healthDelta)
        {
            if (_damageFlashImage == null)
            {
                return;
            }

            if (_damageFlashRoutine != null)
            {
                StopCoroutine(_damageFlashRoutine);
            }

            float peakAlpha = Mathf.Clamp(0.16f + healthDelta * 0.9f, 0.16f, 0.42f);
            _damageFlashRoutine = StartCoroutine(DamageFlashRoutine(peakAlpha));
        }

        private IEnumerator DamageFlashRoutine(float peakAlpha)
        {
            float elapsed = 0f;
            const float duration = 0.32f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                SetImageAlpha(_damageFlashImage, peakAlpha * Mathf.Pow(1f - t, 2f));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            SetImageAlpha(_damageFlashImage, 0f);
            _damageFlashRoutine = null;
        }

        private void RefreshLowHealthFeedback()
        {
            if (_lowHealthEdges[0] == null)
            {
                return;
            }

            if (_encounterFailed || _playerCombat == null || _playerCombat.MaxHealth <= 0f)
            {
                SetLowHealthEdgeAlpha(0f);
                return;
            }

            float ratio = Mathf.Clamp01(_playerCombat.CurrentHealth / _playerCombat.MaxHealth);
            const float threshold = 0.30f;
            if (ratio > threshold)
            {
                SetLowHealthEdgeAlpha(0f);
                return;
            }

            float danger = 1f - ratio / threshold;
            float pulse = (Mathf.Sin(Time.unscaledTime * 6.2f) + 1f) * 0.5f;
            SetLowHealthEdgeAlpha(Mathf.Lerp(0.08f, 0.24f, pulse) * danger);
        }

        private void SetLowHealthEdgeAlpha(float alpha)
        {
            foreach (var edge in _lowHealthEdges)
            {
                SetImageAlpha(edge, alpha);
            }
        }

        private void CreateDefeatPanel(Transform parent, Font font)
        {
            var panel = CreateHudPanel(parent, "DefeatRetryPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 292f), new Color(0.035f, 0.018f, 0.018f, 0.93f));
            _defeatPanelObject = panel.gameObject;

            CreateHudPanel(_defeatPanelObject.transform, "DefeatTopAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(660f, 6f), new Color(1f, 0.22f, 0.10f, 0.90f));
            CreateHudPanel(_defeatPanelObject.transform, "DefeatSideAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -6f), new Vector2(6f, 286f), new Color(0.86f, 0.08f, 0.06f, 0.72f));
            CreateText(_defeatPanelObject.transform, font, "BATTLE REPORT", 13, new Vector2(36f, -22f), new Vector2(150f, 22f), TextAnchor.UpperLeft, new Color(0.74f, 0.62f, 0.58f));
            CreateText(_defeatPanelObject.transform, font, "CHAMPION FALLEN", 30, new Vector2(0f, -42f), new Vector2(660f, 42f), TextAnchor.MiddleCenter, new Color(1f, 0.42f, 0.28f));
            _defeatSummaryText = CreateText(_defeatPanelObject.transform, font, "Time 00:00   Boss 100%   Guard held   Enrage avoided", 15, new Vector2(50f, -94f), new Vector2(560f, 28f), TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.88f));
            _defeatDetailText = CreateText(_defeatPanelObject.transform, font, "Review the battle report, adjust timing, then choose the next attempt.", 15, new Vector2(70f, -134f), new Vector2(520f, 58f), TextAnchor.MiddleCenter, new Color(0.88f, 0.90f, 0.94f));
            _defeatActionText = CreateText(_defeatPanelObject.transform, font, "Next: retry for execution, inspect your champion, or return to Kingdom upgrades.", 13, new Vector2(84f, -184f), new Vector2(492f, 30f), TextAnchor.MiddleCenter, new Color(0.72f, 0.78f, 0.84f));
            CreateHudButton(_defeatPanelObject.transform, font, "Retry", new Vector2(70f, -232f), new Vector2(140f, 42f), RetryEncounter, 16, new Color(0.34f, 0.08f, 0.05f, 0.96f));
            CreateHudButton(_defeatPanelObject.transform, font, "Inspect", new Vector2(260f, -232f), new Vector2(140f, 42f), () =>
            {
                _defeatPanelObject.SetActive(false);
                SetAppearanceInspection(true);
            }, 16, new Color(0.10f, 0.14f, 0.19f, 0.96f));
            CreateHudButton(_defeatPanelObject.transform, font, "Kingdom", new Vector2(450f, -232f), new Vector2(140f, 42f), () => SceneManager.LoadScene(_kingdomSceneName), 16, new Color(0.11f, 0.12f, 0.14f, 0.96f));
            _defeatPanelObject.SetActive(false);
        }

        private void CreateClearPanel(Transform parent, Font font)
        {
            var panel = CreateHudPanel(parent, "EncounterClearPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 250f), new Color(0.018f, 0.034f, 0.026f, 0.93f));
            _clearPanelObject = panel.gameObject;

            CreateHudPanel(_clearPanelObject.transform, "ClearAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(620f, 6f), new Color(0.62f, 1f, 0.40f, 0.90f));
            _clearTitleText = CreateText(_clearPanelObject.transform, font, "ENCOUNTER CLEAR", 28, new Vector2(0f, -28f), new Vector2(620f, 40f), TextAnchor.MiddleCenter, new Color(0.72f, 1f, 0.54f));
            _clearSummaryText = CreateText(_clearPanelObject.transform, font, "Time 00:00   Guard broken   Enrage avoided", 16, new Vector2(48f, -78f), new Vector2(524f, 28f), TextAnchor.MiddleCenter, new Color(0.90f, 0.94f, 0.92f));
            _clearDetailText = CreateText(_clearPanelObject.transform, font, "Review the result, inspect your build, or retry for a better grade.", 15, new Vector2(54f, -112f), new Vector2(512f, 48f), TextAnchor.MiddleCenter, new Color(0.84f, 0.88f, 0.86f));
            CreateHudButton(_clearPanelObject.transform, font, "Retry", new Vector2(70f, -184f), new Vector2(140f, 42f), RetryEncounter, 16, new Color(0.12f, 0.20f, 0.13f, 0.96f));
            CreateHudButton(_clearPanelObject.transform, font, "Inspect", new Vector2(240f, -184f), new Vector2(140f, 42f), () =>
            {
                _clearPanelObject.SetActive(false);
                SetAppearanceInspection(true);
            }, 16, new Color(0.10f, 0.14f, 0.19f, 0.96f));
            CreateHudButton(_clearPanelObject.transform, font, "Kingdom", new Vector2(410f, -184f), new Vector2(140f, 42f), () => SceneManager.LoadScene(_kingdomSceneName), 16, new Color(0.13f, 0.12f, 0.08f, 0.96f));
            _clearPanelObject.SetActive(false);
        }

        private void CreateIntroPanel(Transform parent, Font font)
        {
            _introTopLetterbox = CreateUiImage(parent, "IntroLetterboxTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 92f), new Color(0.002f, 0.004f, 0.007f, 0.78f));
            _introBottomLetterbox = CreateUiImage(parent, "IntroLetterboxBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 110f), new Color(0.002f, 0.004f, 0.007f, 0.80f));
            CreateUiImage(_introTopLetterbox.transform, "IntroTopGoldTrace", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 2f), new Color(1f, 0.68f, 0.28f, 0.64f));
            CreateUiImage(_introBottomLetterbox.transform, "IntroBottomBlueTrace", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 2f), new Color(0.28f, 0.58f, 1f, 0.48f));
            _introTopLetterbox.gameObject.SetActive(false);
            _introBottomLetterbox.gameObject.SetActive(false);

            var panel = CreateHudPanel(parent, "EncounterIntroPanel", new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 226f), new Color(0.012f, 0.018f, 0.026f, 0.88f));
            _introPanelObject = panel.gameObject;
            CreateHudPanel(_introPanelObject.transform, "IntroTopAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(720f, 6f), new Color(1f, 0.64f, 0.22f, 0.88f));
            CreateUiImage(_introPanelObject.transform, "IntroLeftTrace", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -8f), new Vector2(5f, 202f), new Color(0.28f, 0.58f, 1f, 0.54f));
            CreateUiImage(_introPanelObject.transform, "IntroThreatTrace", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -8f), new Vector2(5f, 202f), new Color(1f, 0.28f, 0.12f, 0.58f));
            _introTitleText = CreateText(_introPanelObject.transform, font, "CHAMPION READY", 30, new Vector2(36f, -30f), new Vector2(456f, 42f), TextAnchor.UpperLeft, new Color(1f, 0.76f, 0.42f));
            _introSubtitleText = CreateText(_introPanelObject.transform, font, "Forge identity locked. Read the arena before committing.", 16, new Vector2(38f, -84f), new Vector2(500f, 58f), TextAnchor.UpperLeft, new Color(0.86f, 0.90f, 0.95f));
            _introCountdownText = CreateText(_introPanelObject.transform, font, "3", 72, new Vector2(552f, -36f), new Vector2(132f, 120f), TextAnchor.MiddleCenter, new Color(1f, 0.34f, 0.18f));
            CreateText(_introPanelObject.transform, font, "CHAMPION MODE", 13, new Vector2(40f, -162f), new Vector2(180f, 24f), TextAnchor.UpperLeft, new Color(0.54f, 0.68f, 0.84f));
            CreateText(_introPanelObject.transform, font, "MANUAL ENGAGEMENT", 13, new Vector2(498f, -162f), new Vector2(180f, 24f), TextAnchor.UpperRight, new Color(1f, 0.70f, 0.40f));
            _introPanelObject.SetActive(false);
        }

        private static Image CreateHudPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            var panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);
            var image = panelObject.AddComponent<Image>();
            image.color = color;
            var outline = panelObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.34f, 0.44f, 0.45f);
            outline.effectDistance = new Vector2(1.3f, -1.3f);
            SetRect(panelObject.GetComponent<RectTransform>(), anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
            return image;
        }

        private static Image CreateUiImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            var imageObject = new GameObject(name);
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            SetRect(imageObject.GetComponent<RectTransform>(), anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
            return image;
        }

        private static Image CreateStatusBar(Transform parent, Vector2 anchoredPosition, Vector2 sizeDelta, Color fillColor)
        {
            var frame = CreateHudPanel(parent, "BarFrame", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPosition, sizeDelta, new Color(0.005f, 0.007f, 0.010f, 0.92f));
            var fillObject = new GameObject("BarFill");
            fillObject.transform.SetParent(frame.transform, false);
            var fill = fillObject.AddComponent<Image>();
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            var rect = fillObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);
            return fill;
        }

        private static Button CreateHudButton(
            Transform parent,
            Font font,
            string label,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            UnityEngine.Events.UnityAction action,
            int fontSize,
            Color? color = null,
            Vector2? anchor = null,
            Vector2? pivot = null)
        {
            var buttonObject = new GameObject(label);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = color ?? new Color(0.095f, 0.125f, 0.158f, 0.94f);
            var outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.34f, 0.48f, 0.62f, 0.36f);
            outline.effectDistance = new Vector2(1f, -1f);
            var button = buttonObject.AddComponent<Button>();
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(image.color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(image.color, Color.black, 0.25f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var rect = buttonObject.GetComponent<RectTransform>();
            Vector2 resolvedAnchor = anchor ?? new Vector2(0f, 1f);
            Vector2 resolvedPivot = pivot ?? new Vector2(0f, 1f);
            SetRect(rect, resolvedAnchor, resolvedAnchor, resolvedPivot, anchoredPosition, sizeDelta);

            var text = CreateText(buttonObject.transform, font, label, fontSize, Vector2.zero, sizeDelta, TextAnchor.MiddleCenter);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }

        private static void SetFillAmount(Image image, float amount)
        {
            if (image != null)
            {
                image.fillAmount = Mathf.Clamp01(amount);
            }
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            var color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
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
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static Text CreateText(Transform parent, Font font, string value, int size, Vector2 anchoredPosition, Vector2 sizeDelta, TextAnchor alignment)
        {
            return CreateText(parent, font, value, size, anchoredPosition, sizeDelta, alignment, Color.white);
        }

        private static Text CreateText(Transform parent, Font font, string value, int size, Vector2 anchoredPosition, Vector2 sizeDelta, TextAnchor alignment, Color color)
        {
            var textObject = new GameObject("Text");
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.62f);
            shadow.effectDistance = new Vector2(1.2f, -1.2f);

            var rect = textObject.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPosition, sizeDelta);
            return text;
        }

    }

    internal sealed class ChampionInspectionShowcase : MonoBehaviour
    {
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Light> _lights = new List<Light>();
        private readonly List<float> _lightBaseIntensities = new List<float>();
        private readonly List<ParticleSystem> _particles = new List<ParticleSystem>();
        private Color _accent;
        private Color _edge;
        private Vector3 _baseLocalPosition;
        private Transform _turntableOrbit;

        public void Configure(Color accent, Color edge)
        {
            _accent = accent;
            _edge = edge;
            _baseLocalPosition = transform.localPosition;
            CollectTargets();
        }

        private void OnEnable()
        {
            CollectTargets();
        }

        private void CollectTargets()
        {
            _materials.Clear();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.material == null)
                {
                    continue;
                }

                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.EnableKeyword("_EMISSION");
                    _materials.Add(renderer.material);
                }
            }

            _lights.Clear();
            _lightBaseIntensities.Clear();
            foreach (var light in GetComponentsInChildren<Light>(true))
            {
                if (light != null)
                {
                    _lights.Add(light);
                    _lightBaseIntensities.Add(light.intensity);
                }
            }

            _particles.Clear();
            foreach (var particles in GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particles != null)
                {
                    _particles.Add(particles);
                }
            }

            _turntableOrbit = transform.Find("Inspection_TurntableOrbit");
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            float pulse = 0.62f + Mathf.Sin(time * 1.45f) * 0.18f;
            float slowBreath = 0.50f + Mathf.Sin(time * 0.70f) * 0.50f;
            transform.localPosition = _baseLocalPosition + Vector3.up * (Mathf.Sin(time * 0.92f) * 0.018f);

            if (_turntableOrbit != null)
            {
                _turntableOrbit.localRotation = Quaternion.Euler(0f, time * 18f, 0f);
            }

            for (int i = 0; i < _materials.Count; i++)
            {
                Material material = _materials[i];
                if (material == null)
                {
                    continue;
                }

                Color color = i % 2 == 0 ? _accent : _edge;
                material.SetColor("_EmissionColor", color * Mathf.Max(0f, pulse));
            }

            for (int i = 0; i < _lights.Count; i++)
            {
                Light light = _lights[i];
                if (light == null)
                {
                    continue;
                }

                float baseIntensity = i < _lightBaseIntensities.Count ? _lightBaseIntensities[i] : light.intensity;
                light.intensity = Mathf.Max(0f, baseIntensity * (0.90f + Mathf.Sin(time * 1.12f + i) * 0.08f + slowBreath * 0.08f));
            }

            for (int i = 0; i < _particles.Count; i++)
            {
                var particles = _particles[i];
                if (particles == null)
                {
                    continue;
                }

                var emission = particles.emission;
                emission.rateOverTimeMultiplier = Mathf.Lerp(0.78f, 1.20f, slowBreath);
            }
        }
    }

    internal sealed class ChampionIntroCinematicCue : MonoBehaviour
    {
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Color> _emissionColors = new List<Color>();
        private readonly List<Light> _lights = new List<Light>();
        private readonly List<float> _lightIntensities = new List<float>();
        private Transform _heroHalo;
        private Transform _heroInner;
        private Transform _bossHalo;
        private Transform _bossInner;
        private float _time;

        public void Configure(Transform heroHalo, Transform heroInner, Transform bossHalo, Transform bossInner)
        {
            _heroHalo = heroHalo;
            _heroInner = heroInner;
            _bossHalo = bossHalo;
            _bossInner = bossInner;
            CollectTargets();
        }

        private void OnEnable()
        {
            _time = 0f;
            if (_materials.Count == 0)
            {
                CollectTargets();
            }
        }

        private void CollectTargets()
        {
            _materials.Clear();
            _emissionColors.Clear();
            _lights.Clear();
            _lightIntensities.Clear();

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.material == null || !renderer.material.HasProperty("_EmissionColor"))
                {
                    continue;
                }

                renderer.material.EnableKeyword("_EMISSION");
                _materials.Add(renderer.material);
                _emissionColors.Add(renderer.material.color);
            }

            foreach (var light in GetComponentsInChildren<Light>(true))
            {
                if (light == null)
                {
                    continue;
                }

                _lights.Add(light);
                _lightIntensities.Add(light.intensity);
            }
        }

        private void Update()
        {
            _time += Time.unscaledDeltaTime;
            RotateCue(_heroHalo, 18f);
            RotateCue(_heroInner, -28f);
            RotateCue(_bossHalo, -14f);
            RotateCue(_bossInner, 34f);

            for (int i = 0; i < _materials.Count; i++)
            {
                var material = _materials[i];
                if (material == null)
                {
                    continue;
                }

                float pulse = 0.52f + Mathf.Sin(_time * 2.4f + i * 0.62f) * 0.34f;
                material.SetColor("_EmissionColor", _emissionColors[i] * Mathf.Max(0f, pulse));
            }

            for (int i = 0; i < _lights.Count; i++)
            {
                var light = _lights[i];
                if (light == null)
                {
                    continue;
                }

                float pulse = 1f + Mathf.Sin(_time * 2.0f + i * 0.9f) * 0.18f;
                light.intensity = _lightIntensities[i] * pulse;
            }
        }

        private void RotateCue(Transform cue, float degreesPerSecond)
        {
            if (cue == null)
            {
                return;
            }

            cue.Rotate(Vector3.up, degreesPerSecond * Time.unscaledDeltaTime, Space.World);
        }
    }

    internal sealed class ArenaAtmospherePulse : MonoBehaviour
    {
        private struct PulsedMaterial
        {
            public Material Material;
            public Color Color;
            public float Phase;
            public float Intensity;
        }

        private struct PulsedLight
        {
            public Light Light;
            public float BaseIntensity;
            public float Phase;
            public float Amplitude;
        }

        private readonly List<PulsedMaterial> _materials = new List<PulsedMaterial>();
        private readonly List<PulsedLight> _lights = new List<PulsedLight>();

        public void RegisterRenderer(GameObject target, Color color, float phase, float intensity)
        {
            if (target == null)
            {
                return;
            }

            var renderer = target.GetComponent<Renderer>();
            if (renderer == null || renderer.material == null || !renderer.material.HasProperty("_EmissionColor"))
            {
                return;
            }

            renderer.material.EnableKeyword("_EMISSION");
            _materials.Add(new PulsedMaterial
            {
                Material = renderer.material,
                Color = color,
                Phase = phase,
                Intensity = Mathf.Max(0f, intensity)
            });
        }

        public void RegisterLight(Light light, float phase, float amplitude)
        {
            if (light == null)
            {
                return;
            }

            _lights.Add(new PulsedLight
            {
                Light = light,
                BaseIntensity = light.intensity,
                Phase = phase,
                Amplitude = Mathf.Clamp(amplitude, 0f, 0.45f)
            });
        }

        private void Update()
        {
            float time = Time.time * 1.18f;

            for (int i = 0; i < _materials.Count; i++)
            {
                var entry = _materials[i];
                if (entry.Material == null)
                {
                    continue;
                }

                float pulse = 0.70f + Mathf.Sin(time + entry.Phase) * 0.30f;
                entry.Material.SetColor("_EmissionColor", entry.Color * entry.Intensity * Mathf.Max(0f, pulse));
            }

            for (int i = 0; i < _lights.Count; i++)
            {
                var entry = _lights[i];
                if (entry.Light == null)
                {
                    continue;
                }

                float pulse = Mathf.Sin(time * 0.82f + entry.Phase);
                entry.Light.intensity = Mathf.Max(0f, entry.BaseIntensity * (1f + pulse * entry.Amplitude));
            }
        }
    }
}
