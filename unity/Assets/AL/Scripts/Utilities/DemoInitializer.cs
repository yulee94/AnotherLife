using UnityEngine;
using UnityEngine.UI;
using AL.Core;
using AL.Core.Interfaces;
using AL.ChampionMode.AI;
using AL.ChampionMode.Customization;
using AL.Data.Runtime;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace AL.Utilities
{
    public class DemoInitializer : MonoBehaviour
    {
        private Text _statusText;

        private void Start()
        {
            // 0. Ensure Services are initialized (Plug-and-Play)
            Bootloader.InitializeIfMissing();
            EnsureSaveLoaded();

            SetupDemoScene();
            Debug.Log("<color=green><b>Welcome to Another Life!</b></color>");
            Debug.Log("Press <b>Play</b> in the Unity Editor to start your journey as a Realm Lord.");
        }

        private void SetupDemoScene()
        {
            EnsureEventSystem();
            EnsureFloor();

            // 0. Build Kingdom Visuals
            gameObject.AddComponent<AL.Kingdom.Visuals.KingdomVisualizer>();

            // 1. Ensure Player exists
            GameObject player = GameObject.Find("Player_Champion");
            if (player == null)
            {
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "Player_Champion";
                player.transform.position = new Vector3(0, 1, 0);

                // Add basic components for the 3D mode
                player.AddComponent<AL.ChampionMode.Control.ChampionController>();
                player.AddComponent<AL.ChampionMode.Control.ChampionCombat>();
                player.AddComponent<AutoCombatController>();
                ProceduralChampionModelBuilder.EnsureModel(player);
                player.AddComponent<ChampionCustomizationController>();

                // Add a material color to player
                player.GetComponent<Renderer>().material.color = Color.blue;

                Debug.Log("Created Player Champion (Capsule) for 3D Arena.");
            }

            player.tag = "Player";
            if (player.GetComponent<ChampionCustomizationController>() == null)
            {
                ProceduralChampionModelBuilder.EnsureModel(player);
                player.AddComponent<ChampionCustomizationController>();
            }
            else
            {
                ProceduralChampionModelBuilder.EnsureModel(player);
            }
            if (player.GetComponent<AutoCombatController>() == null)
            {
                player.AddComponent<AutoCombatController>();
            }

            // 2. Setup Camera
            Camera mainCam = Camera.main;
            if (mainCam != null && mainCam.GetComponent<AL.ChampionMode.Camera.CameraFollow>() == null)
            {
                var follow = mainCam.gameObject.AddComponent<AL.ChampionMode.Camera.CameraFollow>();
                Debug.Log("Attached CameraFollow to Main Camera.");
            }

            // 3. Create a simple Debug UI for Resources
            CreateDebugUI();
            SpawnArenaTargets();
        }

        private void CreateDebugUI()
        {
            GameObject canvasObj = new GameObject("DebugUI_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject textObj = new GameObject("ResourceText");
            textObj.transform.SetParent(canvasObj.transform);
            Text text = textObj.AddComponent<Text>();
            // Fallback font handling for modern Unity versions
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            text.fontSize = 24;
            text.color = Color.yellow;
            text.text = "Initializing Kingdom State...";

            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(20, -20);
            rect.sizeDelta = new Vector2(400, 100);

            _statusText = CreateText(canvasObj.transform, "StatusText", new Vector2(20, 360), new Vector2(620, 90), 20, Color.white);
            _statusText.text = "Prototype controls ready.";

            CreateButton(canvasObj.transform, "Select Crownlands", new Vector2(20, 295), () =>
            {
                ServiceLocator.Get<IRealmService>().SelectRealm(RealmId.Crownlands);
                SetStatus("Selected Crownlands Humans.");
            });

            CreateButton(canvasObj.transform, "Add Resources", new Vector2(20, 240), () =>
            {
                var resources = ServiceLocator.Get<IResourceService>();
                foreach (ResourceType resourceType in ResourceRules.WalletResources)
                {
                    long amount = ResourceRules.IsRareResource(resourceType) ? 100 : 1000;
                    resources.AddResource(resourceType, amount);
                }

                SetStatus("Added debug resources, including ManaStone, Ore, and rare realm materials.");
            });

            CreateButton(canvasObj.transform, "Upgrade Farm", new Vector2(20, 185), () =>
            {
                ServiceLocator.Get<IBuildingService>().StartUpgrade("Farm");
                SetStatus("Started Farm upgrade if enough Stone is available.");
            });

            CreateButton(canvasObj.transform, "Train Infantry", new Vector2(20, 130), () =>
            {
                ServiceLocator.Get<ITrainingService>().StartTraining(TroopType.Infantry, 25);
                SetStatus($"Infantry trained. Total: {ServiceLocator.Get<ITrainingService>().GetTroopCount(TroopType.Infantry)}");
            });

            CreateButton(canvasObj.transform, "Earn Warzone", new Vector2(20, 75), () =>
            {
                ServiceLocator.Get<IWarzoneCreditService>().AddCredits(250);
                SetStatus($"Warzone Credits: {ServiceLocator.Get<IWarzoneCreditService>().GetCredits()}");
            });

            CreateButton(canvasObj.transform, "Hero Color", new Vector2(20, 20), () =>
            {
                FindObjectOfType<ChampionCustomizationController>()?.CyclePrimaryColor();
                SetStatus("Cycled Champion primary color.");
            });

            CreateButton(canvasObj.transform, "Hero Hair", new Vector2(220, 295), () =>
            {
                FindObjectOfType<ChampionCustomizationController>()?.CycleHairColor();
                SetStatus("Cycled Champion hair color.");
            });

            CreateButton(canvasObj.transform, "Toggle Cape", new Vector2(220, 240), () =>
            {
                FindObjectOfType<ChampionCustomizationController>()?.ToggleCape();
                SetStatus("Toggled Champion cape.");
            });

            CreateButton(canvasObj.transform, "Hero Body", new Vector2(420, 295), () =>
            {
                FindObjectOfType<ChampionCustomizationController>()?.CycleBodyPreset();
                SetStatus("Cycled Champion body preset.");
            });

            CreateButton(canvasObj.transform, "Hair Style", new Vector2(420, 240), () =>
            {
                FindObjectOfType<ChampionCustomizationController>()?.CycleHairStyle();
                SetStatus("Cycled Champion hair style.");
            });

            CreateButton(canvasObj.transform, "Armor Style", new Vector2(420, 185), () =>
            {
                FindObjectOfType<ChampionCustomizationController>()?.CycleArmorStyle();
                SetStatus("Cycled Champion armor style.");
            });

            CreateButton(canvasObj.transform, "Toggle Helmet", new Vector2(420, 130), () =>
            {
                FindObjectOfType<ChampionCustomizationController>()?.ToggleHelmet();
                SetStatus("Toggled Champion helmet.");
            });

            CreateButton(canvasObj.transform, "Assist Mode", new Vector2(220, 185), () =>
            {
                FindObjectOfType<AutoCombatController>()?.SetMode(AutoMode.SemiAuto);
                SetStatus("Assist mode enabled. Manual input interrupts it.");
            });

            CreateButton(canvasObj.transform, "Auto Mode", new Vector2(220, 130), () =>
            {
                FindObjectOfType<AutoCombatController>()?.SetMode(AutoMode.FullAuto);
                SetStatus("Auto mode enabled. Manual input interrupts it.");
            });

            CreateButton(canvasObj.transform, "Capture Border", new Vector2(220, 75), () =>
            {
                var realm = ServiceLocator.Get<IRealmService>().CurrentRealmId;
                if (realm == RealmId.None)
                {
                    realm = RealmId.Crownlands;
                    ServiceLocator.Get<IRealmService>().SelectRealm(realm);
                }

                ServiceLocator.Get<ITerritoryService>().CaptureTerritory("T5", realm);
                SetStatus($"Captured Neutral Borderlands for {realm}.");
            });

            CreateButton(canvasObj.transform, "Test Battle", new Vector2(220, 20), RunTestBattle);
            CreateButton(canvasObj.transform, "Spawn Targets", new Vector2(420, 20), SpawnArenaTargets);

            // Update text in a simple loop
            StartCoroutine(UpdateResourceText(text));
        }

        private void EnsureSaveLoaded()
        {
            var save = ServiceLocator.Get<ISaveGameService>();
            if (save.CurrentSave == null)
            {
                save.Load();
            }
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private void EnsureFloor()
        {
            if (GameObject.Find("Demo_Floor") != null)
            {
                return;
            }

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Demo_Floor";
            floor.transform.localScale = new Vector3(8f, 1f, 8f);
            floor.GetComponent<Renderer>().material.color = new Color(0.14f, 0.18f, 0.16f);
        }

        private void SpawnArenaTargets()
        {
            for (int i = 0; i < 12; i++)
            {
                string name = "Dummy_" + i;
                if (GameObject.Find(name) != null)
                {
                    continue;
                }

                var dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dummy.name = name;
                float angle = i * Mathf.PI * 2f / 12f;
                dummy.transform.position = new Vector3(Mathf.Cos(angle) * 7f, 0.5f, Mathf.Sin(angle) * 7f);
                dummy.GetComponent<Renderer>().material.color = Color.red;
            }

            if (GameObject.Find("BossDummy") == null)
            {
                var boss = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                boss.name = "BossDummy";
                boss.transform.position = new Vector3(0f, 1.5f, 10f);
                boss.transform.localScale = new Vector3(2.2f, 2.2f, 2.2f);
                boss.GetComponent<Renderer>().material.color = new Color(0.7f, 0.05f, 0.05f);
                boss.AddComponent<BossDummyAI>();
            }

            for (int i = 0; i < 6; i++)
            {
                string botName = "BotChampion_" + i;
                if (GameObject.Find(botName) != null)
                {
                    continue;
                }

                var bot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                bot.name = botName;
                bot.transform.position = new Vector3(-10f + i * 2f, 1.1f, 9f);
                bot.GetComponent<Renderer>().material.color = new Color(0.55f, 0.1f, 0.65f);
                bot.AddComponent<BotChampionAI>();
            }

            SetStatus("Arena targets spawned. Use WASD, mouse click, and Space to fight.");
        }

        private void RunTestBattle()
        {
            var request = new BattleRequest
            {
                Type = BattleType.PvE,
                AttackerTroops = new List<TroopStack>
                {
                    new TroopStack { Type = TroopType.Infantry, Count = 60 },
                    new TroopStack { Type = TroopType.Ranged, Count = 40 }
                },
                DefenderTroops = new List<TroopStack>
                {
                    new TroopStack { Type = TroopType.Infantry, Count = 45 },
                    new TroopStack { Type = TroopType.Cavalry, Count = 20 }
                }
            };

            var report = ServiceLocator.Get<IBattleSimulator>().Simulate(request);
            SetStatus(report.Summary);
        }

        private Text CreateText(Transform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta, int fontSize, Color color)
        {
            var textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);
            var text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return text;
        }

        private void CreateButton(Transform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
        {
            var buttonObj = new GameObject(label);
            buttonObj.transform.SetParent(parent, false);

            var image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.12f, 0.22f, 0.32f, 0.92f);

            var button = buttonObj.AddComponent<Button>();
            button.onClick.AddListener(action);

            var rect = buttonObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(190, 44);

            var labelText = CreateText(buttonObj.transform, label + "_Text", Vector2.zero, rect.sizeDelta, 18, Color.white);
            labelText.text = label;
            labelText.alignment = TextAnchor.MiddleCenter;
            var labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }

            Debug.Log(message);
        }

        private System.Collections.IEnumerator UpdateResourceText(Text text)
        {
            while (true)
            {
                var resources = ServiceLocator.Get<IResourceService>();
                if (resources != null)
                {
                    var realmId = ServiceLocator.Get<IRealmService>().CurrentRealmId;
                    ResourceType rareResourceType = ResourceRules.GetRareResourceForRealm(realmId);
                    text.text = $"<b>RESOURCES</b>\n" +
                               $"Food: {resources.GetResourceCount(ResourceType.Food)}\n" +
                               $"Wood: {resources.GetResourceCount(ResourceType.Wood)}\n" +
                               $"Stone: {resources.GetResourceCount(ResourceType.Stone)}\n" +
                               $"Gold: {resources.GetResourceCount(ResourceType.Gold)}\n" +
                               $"ManaStone: {resources.GetResourceCount(ResourceType.ManaStone)}\n" +
                               $"Ore: {resources.GetResourceCount(ResourceType.Ore)}\n" +
                               $"{rareResourceType}: {resources.GetResourceCount(rareResourceType)}";
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
