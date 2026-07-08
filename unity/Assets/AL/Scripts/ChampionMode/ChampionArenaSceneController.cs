using AL.ChampionMode.AI;
using AL.ChampionMode.Control;
using AL.ChampionMode.Customization;
using AL.ChampionMode.UI;
using AL.Core;
using AL.RealmWar.Warzone;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AL.ChampionMode
{
    public class ChampionArenaSceneController : MonoBehaviour
    {
        [SerializeField] private int _dummyCount = 16;
        [SerializeField] private string _kingdomSceneName = "Kingdom";

        private ChampionController _playerController;
        private ChampionCustomizationController _playerCustomization;
        private AutoCombatController _autoCombatController;
        private ChampionCombat _playerCombat;
        private Transform _bossTransform;
        private Text _healthText;
        private float _warzoneCreditTimer;

        private void Start()
        {
            Bootloader.InitializeIfMissing();
            BuildArena();
            BuildHud();
        }

        private void Update()
        {
            if (_playerController == null || _bossTransform == null)
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

        private void BuildArena()
        {
            if (FindObjectOfType<Light>() == null)
            {
                var lightObject = new GameObject("Directional Light");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.15f;
                lightObject.transform.rotation = Quaternion.Euler(50, -30, 0);
            }

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "ChampionArena_Floor";
            floor.transform.localScale = new Vector3(7f, 1f, 7f);
            floor.GetComponent<Renderer>().material.color = new Color(0.16f, 0.18f, 0.18f);

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player_Champion";
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 1.1f, -8f);
            player.GetComponent<Renderer>().material.color = new Color(0.20f, 0.40f, 1.0f);
            _playerController = player.AddComponent<ChampionController>();
            _playerCombat = player.AddComponent<ChampionCombat>();
            ProceduralChampionModelBuilder.EnsureModel(player);
            _playerCustomization = player.AddComponent<ChampionCustomizationController>();
            _autoCombatController = player.AddComponent<AutoCombatController>();

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 8f, -15f);
            camera.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f);

            var boss = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            boss.name = "BossDummy";
            boss.transform.position = new Vector3(0f, 1.5f, 9f);
            boss.transform.localScale = new Vector3(2.4f, 2.4f, 2.4f);
            boss.GetComponent<Renderer>().material.color = new Color(0.75f, 0.08f, 0.08f);
            boss.AddComponent<BossDummyAI>();
            _bossTransform = boss.transform;

            SpawnBotChampions();

            for (int i = 0; i < _dummyCount; i++)
            {
                var dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dummy.name = "Dummy_" + i;
                float angle = i * Mathf.PI * 2f / _dummyCount;
                dummy.transform.position = new Vector3(Mathf.Cos(angle) * 9f, 0.5f, Mathf.Sin(angle) * 9f);
                dummy.GetComponent<Renderer>().material.color = Color.Lerp(Color.red, Color.magenta, i / (float)_dummyCount);
            }

            CreateWeather();
        }

        private void SpawnBotChampions()
        {
            for (int i = 0; i < 12; i++)
            {
                var bot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                bot.name = "BotChampion_" + i;
                float angle = i * Mathf.PI * 2f / 12f;
                bot.transform.position = new Vector3(Mathf.Cos(angle) * 14f, 1.1f, Mathf.Sin(angle) * 14f);
                bot.GetComponent<Renderer>().material.color = Color.Lerp(new Color(0.55f, 0.12f, 0.72f), new Color(0.92f, 0.18f, 0.18f), i / 11f);
                bot.AddComponent<BotChampionAI>();
            }
        }

        private void CreateWeather()
        {
            var weatherObject = new GameObject("Warzone_BattleFog_Weather");
            weatherObject.transform.position = new Vector3(0f, 6f, 0f);
            weatherObject.AddComponent<RuntimeWeatherController>()
                .Configure(new Color(0.45f, 0.42f, 0.38f, 0.35f), 120, 22f, 1.1f);
        }

        private void BuildHud()
        {
            var canvasObject = new GameObject("DebugUI_Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                       Resources.GetBuiltinResource<Font>("Arial.ttf");

            CreateText(canvasObject.transform, font, "Champion Arena\nWASD move  |  Mouse click attack  |  Space dodge\nDefeat the red dummies. Boss telegraphs when close.", 20, new Vector2(20, -20), new Vector2(780, 120), TextAnchor.UpperLeft);
            _healthText = CreateText(canvasObject.transform, font, "HP: 1000 / 1000", 22, new Vector2(20, -145), new Vector2(420, 45), TextAnchor.UpperLeft);
            if (_playerCombat != null)
            {
                _playerCombat.OnHealthChanged += UpdateHealthText;
            }

            CreateButton(canvasObject.transform, font, "Attack", new Vector2(-20, 310), () => _playerController.RequestBasicAttack());
            CreateButton(canvasObject.transform, font, "Dodge", new Vector2(-20, 250), () => _playerController.RequestDodge());
            CreateButton(canvasObject.transform, font, "Skill 1", new Vector2(-20, 190), () => _playerController.RequestSkill(0));
            CreateButton(canvasObject.transform, font, "Manual", new Vector2(-20, 130), () => _autoCombatController.SetMode(AutoMode.Manual));
            CreateButton(canvasObject.transform, font, "Assist", new Vector2(-20, 70), () => _autoCombatController.SetMode(AutoMode.SemiAuto));
            CreateButton(canvasObject.transform, font, "Auto", new Vector2(-20, 10), () => _autoCombatController.SetMode(AutoMode.FullAuto));

            CreateButton(canvasObject.transform, font, "Primary", new Vector2(-165, 310), () => _playerCustomization.CyclePrimaryColor());
            CreateButton(canvasObject.transform, font, "Hair Color", new Vector2(-165, 250), () => _playerCustomization.CycleHairColor());
            CreateButton(canvasObject.transform, font, "Hair Style", new Vector2(-165, 190), () => _playerCustomization.CycleHairStyle());
            CreateButton(canvasObject.transform, font, "Body", new Vector2(-165, 130), () => _playerCustomization.CycleBodyPreset());
            CreateButton(canvasObject.transform, font, "Armor", new Vector2(-165, 70), () => _playerCustomization.CycleArmorStyle());
            CreateButton(canvasObject.transform, font, "Helmet", new Vector2(-165, 10), () => _playerCustomization.ToggleHelmet());
            CreateButton(canvasObject.transform, font, "Cape", new Vector2(-310, 70), () => _playerCustomization.ToggleCape());
            CreateButton(canvasObject.transform, font, "Kingdom", new Vector2(-310, 10), () => SceneManager.LoadScene(_kingdomSceneName));

            CreateMoveButton(canvasObject.transform, font, "Up", new Vector2(95, 150), new Vector2(0, 1));
            CreateMoveButton(canvasObject.transform, font, "Left", new Vector2(30, 90), new Vector2(-1, 0));
            CreateMoveButton(canvasObject.transform, font, "Right", new Vector2(160, 90), new Vector2(1, 0));
            CreateMoveButton(canvasObject.transform, font, "Down", new Vector2(95, 30), new Vector2(0, -1));
        }

        private void UpdateHealthText(float current, float max)
        {
            if (_healthText != null)
            {
                _healthText.text = $"HP: {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }
        }

        private void CreateMoveButton(Transform parent, Font font, string label, Vector2 anchoredPosition, Vector2 moveInput)
        {
            var button = CreateButton(parent, font, label, anchoredPosition, null, false);
            button.gameObject.AddComponent<ChampionMoveButton>().Setup(_playerController, moveInput);
        }

        private static Button CreateButton(Transform parent, Font font, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action, bool anchorRight = true)
        {
            var buttonObject = new GameObject(label);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.24f, 0.30f, 0.92f);

            var button = buttonObject.AddComponent<Button>();
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            var rect = buttonObject.GetComponent<RectTransform>();
            Vector2 anchor = anchorRight ? new Vector2(1, 0) : Vector2.zero;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(130, 50);

            var text = CreateText(buttonObject.transform, font, label, 18, Vector2.zero, rect.sizeDelta, TextAnchor.MiddleCenter);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }

        private static Text CreateText(Transform parent, Font font, string value, int size, Vector2 anchoredPosition, Vector2 sizeDelta, TextAnchor alignment)
        {
            var textObject = new GameObject("Text");
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
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

    }
}
