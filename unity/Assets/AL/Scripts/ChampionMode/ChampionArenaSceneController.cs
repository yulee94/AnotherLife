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
        private RvrBotSpawner _rvrBotSpawner;
        private BossDummyAI _boss;
        private Transform _bossTransform;
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
        private readonly Image[] _lowHealthEdges = new Image[4];
        private GameObject _defeatPanelObject;
        private Text _appearanceSummaryText;
        private readonly Image[] _appearanceSwatches = new Image[5];
        private readonly Text[] _skillButtonTexts = new Text[4];
        private readonly Text[] _skillCooldownTexts = new Text[4];
        private readonly Image[] _skillCooldownFills = new Image[4];
        private float _skillHudTimer;
        private float _warzoneCreditTimer;
        private float _encounterStartTime;
        private float _lastHealthRatio = 1f;
        private Coroutine _damageFlashRoutine;
        private bool _guardBreakObserved;
        private bool _enrageObserved;
        private bool _encounterClearShown;
        private bool _encounterFailed;
        private RuntimePlatformQualityController _qualityController;

        private void Start()
        {
            Bootloader.InitializeIfMissing();
            ApplyRuntimeQuality();
            BuildArena();
            BuildHud();
        }

        private void Update()
        {
            if (_playerController == null)
            {
                return;
            }

            RefreshLowHealthFeedback();

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
            _autoCombatController = player.AddComponent<AutoCombatController>();

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.transform.position = new Vector3(0f, 7.2f, -13.4f);
            camera.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
            camera.fieldOfView = 42f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.03f, 0.04f);
            cameraObject.AddComponent<AudioListener>();
            var cameraFollow = cameraObject.AddComponent<CameraFollow>();
            cameraFollow.Configure(player.transform, 8.6f, 2.65f, 25f, 0f);

            var boss = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            boss.name = "BossDummy";
            boss.transform.position = new Vector3(0f, 1.8f, 8.6f);
            boss.transform.localScale = new Vector3(1.55f, 1.8f, 1.55f);
            ApplyMaterial(boss, new Color(0.20f, 0.03f, 0.05f), 0.2f, 0.42f);
            DressBossVisual(boss);
            _boss = boss.AddComponent<BossDummyAI>();
            _bossTransform = boss.transform;
            _encounterStartTime = Time.time;
            _guardBreakObserved = false;
            _enrageObserved = false;
            _encounterClearShown = false;
            _encounterFailed = false;

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

            CreateArenaPrimitive(environment, "Arena_Foundation", PrimitiveType.Cylinder, new Vector3(0f, -0.16f, 0f), new Vector3(12.8f, 0.18f, 12.8f), Vector3.zero, new Color(0.055f, 0.062f, 0.074f), false, 0.08f, 0.38f);
            CreateArenaPrimitive(environment, "Arena_CombatStone", PrimitiveType.Cylinder, new Vector3(0f, -0.04f, 0f), new Vector3(10.2f, 0.08f, 10.2f), Vector3.zero, new Color(0.10f, 0.112f, 0.128f), false, 0.06f, 0.44f);
            CreateArenaPrimitive(environment, "Boss_Dais", PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 8.6f), new Vector3(3.4f, 0.16f, 3.4f), Vector3.zero, new Color(0.15f, 0.105f, 0.105f), false, 0.1f, 0.5f);
            CreateArenaPrimitive(environment, "Player_StartSigil", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, -7.4f), new Vector3(1.8f, 0.025f, 1.8f), Vector3.zero, new Color(0.08f, 0.22f, 0.38f), true, 0f, 0.7f);

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
                CreateArenaPrimitive(pillar.transform, "PillarEmber", PrimitiveType.Sphere, new Vector3(0f, 0.62f, 0f), new Vector3(0.42f, 0.10f, 0.42f), Vector3.zero, i % 2 == 0 ? new Color(0.95f, 0.28f, 0.08f) : new Color(0.20f, 0.58f, 1f), true, 0f, 0.82f);
                CreatePointLight("Pillar Light " + i, basePosition + Vector3.up * 1.3f, i % 2 == 0 ? new Color(1f, 0.22f, 0.08f) : new Color(0.2f, 0.55f, 1f), 1.1f, 5f);
            }

            for (int i = -2; i <= 2; i++)
            {
                CreateArenaPrimitive(environment, "CombatLane_" + (i + 2), PrimitiveType.Cube, new Vector3(i * 1.15f, 0.035f, 0.4f), new Vector3(0.045f, 0.035f, 15.8f), Vector3.zero, new Color(0.12f, 0.25f, 0.36f), true, 0f, 0.72f);
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

        private void CreateWorldObjectiveMarkers()
        {
            var markerObject = new GameObject("WorldObjectiveMarkers");
            markerObject.transform.position = Vector3.zero;
            int markerBudget = _qualityController != null ? _qualityController.GetWorldMarkerBudget(8) : 8;
            markerObject.AddComponent<WorldObjectiveMarkerSpawner>().Configure(GetCurrentRealmId(), markerBudget);
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

        private void BuildHud()
        {
            var canvasObject = new GameObject("ChampionMode_HUD");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
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

            var appearancePanel = CreateHudPanel(canvasObject.transform, "AppearanceRack", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(402f, 304f), new Color(0.035f, 0.042f, 0.052f, 0.84f));
            CreateText(appearancePanel.transform, font, "APPEARANCE", 15, new Vector2(18f, -14f), new Vector2(150f, 22f), TextAnchor.UpperLeft, new Color(0.78f, 0.86f, 1f));
            CreateText(appearancePanel.transform, font, "COLORS", 12, new Vector2(244f, -16f), new Vector2(64f, 18f), TextAnchor.UpperLeft, new Color(0.78f, 0.86f, 1f));
            for (int i = 0; i < _appearanceSwatches.Length; i++)
            {
                _appearanceSwatches[i] = CreateHudPanel(appearancePanel.transform, "ColorSwatch_" + i, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(244f + i * 27f, -38f), new Vector2(22f, 22f), Color.white);
            }

            CreateHudButton(appearancePanel.transform, font, "Primary", new Vector2(18f, -48f), new Vector2(112f, 34f), () => { _playerCustomization.CyclePrimaryColor(); RefreshAppearanceText(); }, 13);
            CreateHudButton(appearancePanel.transform, font, "Hair", new Vector2(144f, -48f), new Vector2(112f, 34f), () => { _playerCustomization.CycleHairColor(); RefreshAppearanceText(); }, 13);
            CreateHudButton(appearancePanel.transform, font, "Skin", new Vector2(270f, -48f), new Vector2(112f, 34f), () => { _playerCustomization.CycleSkinColor(); RefreshAppearanceText(); }, 13);
            CreateHudButton(appearancePanel.transform, font, "Hair Style", new Vector2(18f, -88f), new Vector2(112f, 34f), () => { _playerCustomization.CycleHairStyle(); RefreshAppearanceText(); }, 13);
            CreateHudButton(appearancePanel.transform, font, "Body", new Vector2(144f, -88f), new Vector2(112f, 34f), () => { _playerCustomization.CycleBodyPreset(); RefreshAppearanceText(); }, 13);
            CreateHudButton(appearancePanel.transform, font, "Armor", new Vector2(270f, -88f), new Vector2(112f, 34f), () => { _playerCustomization.CycleArmorStyle(); RefreshAppearanceText(); }, 13);
            CreateHudButton(appearancePanel.transform, font, "Eyes", new Vector2(18f, -128f), new Vector2(112f, 34f), () => { _playerCustomization.CycleEyeColor(); RefreshAppearanceText(); }, 13);
            CreateHudButton(appearancePanel.transform, font, "Accent", new Vector2(144f, -128f), new Vector2(112f, 34f), () => { _playerCustomization.CycleAccentColor(); RefreshAppearanceText(); }, 13);
            CreateHudButton(appearancePanel.transform, font, "Face", new Vector2(270f, -128f), new Vector2(112f, 34f), () => { _playerCustomization.CycleFaceMark(); RefreshAppearanceText(); }, 13);
            CreateHudButton(appearancePanel.transform, font, "Weapon", new Vector2(18f, -168f), new Vector2(112f, 34f), () => { _playerCustomization.CycleWeaponStyle(); RefreshAppearanceText(); }, 13);
            CreateHudButton(appearancePanel.transform, font, "Offhand", new Vector2(144f, -168f), new Vector2(112f, 34f), () => { _playerCustomization.CycleOffhandStyle(); RefreshAppearanceText(); }, 13);
            CreateHudButton(appearancePanel.transform, font, "Cape", new Vector2(270f, -168f), new Vector2(112f, 34f), () => { _playerCustomization.ToggleCape(); RefreshAppearanceText(); }, 13);
            CreateHudButton(appearancePanel.transform, font, "Random", new Vector2(18f, -208f), new Vector2(112f, 32f), () => { _playerCustomization.RandomizeAppearance(); RefreshAppearanceText(); }, 13, new Color(0.16f, 0.13f, 0.08f, 0.95f));
            CreateHudButton(appearancePanel.transform, font, "Reset", new Vector2(144f, -208f), new Vector2(112f, 32f), () => { _playerCustomization.ResetAppearance(); RefreshAppearanceText(); }, 13, new Color(0.10f, 0.11f, 0.13f, 0.95f));
            CreateHudButton(appearancePanel.transform, font, "Helmet", new Vector2(270f, -208f), new Vector2(112f, 32f), () => { _playerCustomization.ToggleHelmet(); RefreshAppearanceText(); }, 13);
            _appearanceSummaryText = CreateText(appearancePanel.transform, font, "Loading appearance", 13, new Vector2(18f, -244f), new Vector2(364f, 48f), TextAnchor.UpperLeft, new Color(0.84f, 0.88f, 0.92f));

            var navPanel = CreateHudPanel(canvasObject.transform, "NavigationPad", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 28f), new Vector2(236f, 188f), new Color(0.035f, 0.042f, 0.052f, 0.80f));
            CreateText(navPanel.transform, font, "MOVE", 15, new Vector2(18f, -14f), new Vector2(88f, 20f), TextAnchor.UpperLeft, new Color(0.78f, 0.86f, 1f));
            CreateMoveButton(navPanel.transform, font, "^", new Vector2(90f, -42f), new Vector2(0, 1));
            CreateMoveButton(navPanel.transform, font, "<", new Vector2(34f, -92f), new Vector2(-1, 0));
            CreateMoveButton(navPanel.transform, font, ">", new Vector2(146f, -92f), new Vector2(1, 0));
            CreateMoveButton(navPanel.transform, font, "v", new Vector2(90f, -142f), new Vector2(0, -1));

            var combatFeedPanel = CreateHudPanel(canvasObject.transform, "CombatFeed", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -296f), new Vector2(560f, 62f), new Color(0.020f, 0.026f, 0.034f, 0.76f));
            _combatFeedText = CreateText(combatFeedPanel.transform, font, "Enter the arena. Break the boss guard before the enrage window.", 16, new Vector2(16f, -10f), new Vector2(526f, 44f), TextAnchor.UpperLeft, new Color(0.84f, 0.88f, 0.92f));
            CreateHudButton(canvasObject.transform, font, "Kingdom", new Vector2(-28f, -268f), new Vector2(132f, 40f), () => SceneManager.LoadScene(_kingdomSceneName), 14, new Color(0.12f, 0.11f, 0.08f, 0.92f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            CreateDefeatPanel(canvasObject.transform, font);
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
            if (_combatFeedText != null)
            {
                _combatFeedText.text = _boss.IsBroken
                    ? "Guard broken. Commit burst skills before the boss recovers."
                    : _boss.IsEnraged
                        ? "Enrage active. Dodge first, punish after the telegraph."
                        : "Pressure the guard bar, hold mana for the break window.";
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
                SkillEffectFactory.SpawnFloatingCombatText(_playerController.transform.position + Vector3.up * 2.6f, "CLEAR " + grade, _encounterResultText.color, 0.36f, 1.4f);
                RuntimeCombatAudio.PlayClear();
            }
        }

        private void HandlePlayerDeath()
        {
            if (_encounterFailed)
            {
                return;
            }

            _encounterFailed = true;
            _playerController?.SetControlLocked(true);
            _autoCombatController?.SetMode(AutoMode.Manual);

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
            _skillCooldownFills[slotIndex] = CreateCooldownOverlay(button.transform);
            _skillButtonTexts[slotIndex] = button.GetComponentInChildren<Text>();
            _skillCooldownTexts[slotIndex] = CreateText(button.transform, font, "Ready", 12, new Vector2(8f, -37f), new Vector2(138f, 18f), TextAnchor.MiddleCenter, new Color(0.78f, 0.86f, 1f));
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
                }
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
            var panel = CreateHudPanel(parent, "DefeatRetryPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540f, 220f), new Color(0.035f, 0.018f, 0.018f, 0.92f));
            _defeatPanelObject = panel.gameObject;

            CreateHudPanel(_defeatPanelObject.transform, "DefeatAccent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(540f, 6f), new Color(1f, 0.22f, 0.10f, 0.90f));
            CreateText(_defeatPanelObject.transform, font, "CHAMPION FALLEN", 28, new Vector2(0f, -30f), new Vector2(540f, 40f), TextAnchor.MiddleCenter, new Color(1f, 0.42f, 0.28f));
            CreateText(_defeatPanelObject.transform, font, "The encounter is lost. Retry immediately or return to Kingdom after adjusting your build.", 15, new Vector2(54f, -78f), new Vector2(432f, 46f), TextAnchor.MiddleCenter, new Color(0.90f, 0.92f, 0.94f));
            CreateHudButton(_defeatPanelObject.transform, font, "Retry", new Vector2(96f, -148f), new Vector2(154f, 42f), RetryEncounter, 16, new Color(0.34f, 0.08f, 0.05f, 0.96f));
            CreateHudButton(_defeatPanelObject.transform, font, "Kingdom", new Vector2(290f, -148f), new Vector2(154f, 42f), () => SceneManager.LoadScene(_kingdomSceneName), 16, new Color(0.11f, 0.12f, 0.14f, 0.96f));
            _defeatPanelObject.SetActive(false);
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
}
