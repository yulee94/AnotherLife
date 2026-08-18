using UnityEngine;
using UnityEngine.UI;
using AL.Core;
using AL.Core.Interfaces;
using AL.ChampionMode.AI;
using AL.ChampionMode.Customization;
using AL.ChampionMode.Skills;
using AL.Data.Runtime;
using AL.Slice;
using AL.VerticalSlice;
using AL.VerticalSlice.Combat;
using AL.Kingdom.Greybox;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace AL.Utilities
{
    public class DemoInitializer : MonoBehaviour
    {
        private Text _statusText;
        private Text _modeText;
        private GreyboxCombatEncounter _championDuel;
        private readonly Dictionary<MaterialStyle, Material> _materials = new Dictionary<MaterialStyle, Material>();
        private readonly Color _gold = new Color(0.92f, 0.66f, 0.30f, 1f);
        private readonly Color _blue = new Color(0.36f, 0.58f, 0.82f, 1f);

        private readonly struct MaterialStyle : IEquatable<MaterialStyle>
        {
            private readonly Color _color;
            private readonly float _metallic;
            private readonly float _smoothness;
            private readonly Color _emission;
            private readonly bool _hasEmission;

            public MaterialStyle(Color color, float metallic, float smoothness, Color? emission)
            {
                _color = color;
                _metallic = metallic;
                _smoothness = smoothness;
                _emission = emission.GetValueOrDefault();
                _hasEmission = emission.HasValue;
            }

            public bool Equals(MaterialStyle other)
            {
                return _color.Equals(other._color)
                    && _metallic.Equals(other._metallic)
                    && _smoothness.Equals(other._smoothness)
                    && _emission.Equals(other._emission)
                    && _hasEmission == other._hasEmission;
            }

            public override bool Equals(object obj) => obj is MaterialStyle other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _color.GetHashCode();
                    hash = (hash * 397) ^ _metallic.GetHashCode();
                    hash = (hash * 397) ^ _smoothness.GetHashCode();
                    hash = (hash * 397) ^ _emission.GetHashCode();
                    return (hash * 397) ^ _hasEmission.GetHashCode();
                }
            }
        }

        private void Start()
        {
            // 0. Ensure Services are initialized (Plug-and-Play) so the arena debug UI below keeps working.
            Bootloader.InitializeIfMissing();
            EnsureSaveLoaded();

            // Greybox vertical-slice opening: realm selection -> character creation -> arena.
            // Realm selection and character-creation-entry use hardcoded LocalGameDataService data and
            // the process-local GreyboxRunState only; they do not touch catalog/save/determinism authority.
            BeginGreyboxSliceFlow();
        }

        private void BeginGreyboxSliceFlow()
        {
            GreyboxRunState.Reset();

            var realmSelection = gameObject.AddComponent<GreyboxRealmSelectionController>();
            realmSelection.OnRealmCommitted += OnRealmCommitted;
            realmSelection.Present();
        }

        private void OnRealmCommitted(RealmId realmId)
        {
            Debug.Log($"[GREYBOX-SLICE] Advancing from realm selection to character creation for realm {realmId}.");

            var characterCreation = gameObject.AddComponent<GreyboxCharacterCreationEntryController>();
            characterCreation.OnCharacterConfirmed += OnCharacterConfirmed;
            characterCreation.Present();
        }

        private void OnCharacterConfirmed()
        {
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
                player.AddComponent<AL.ChampionMode.Control.ChampionCombat>();
                player.AddComponent<SkillCaster>();
                player.AddComponent<AL.ChampionMode.Control.ChampionController>();
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
            if (player.GetComponent<SkillCaster>() == null)
            {
                player.AddComponent<SkillCaster>();
            }

            // 2. Setup Camera
            Camera mainCam = Camera.main;
            if (mainCam != null && mainCam.GetComponent<AL.ChampionMode.Camera.CameraFollow>() == null)
            {
                var follow = mainCam.gameObject.AddComponent<AL.ChampionMode.Camera.CameraFollow>();
                Debug.Log("Attached CameraFollow to Main Camera.");
            }

            CreateDebugUI();
            SpawnArenaTargets();
        }

        private void CreateDebugUI()
        {
            GameObject canvasObj = new GameObject("DebugUI_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            CreatePanel(canvasObj.transform, "CommandBackdrop", new Vector2(0f, 0f), new Vector2(688f, 0f), Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Color(0.006f, 0.010f, 0.016f, 0.70f));
            CreatePanel(canvasObj.transform, "CommandTopRule", new Vector2(24f, -18f), new Vector2(634f, 2f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(1f, 0.84f, 0.50f, 0.34f));
            CreatePanel(canvasObj.transform, "CommandAccent", new Vector2(24f, -28f), new Vector2(6f, 292f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.88f, 0.56f, 0.24f, 0.90f));

            var title = CreateText(canvasObj.transform, "PrototypeTitle", new Vector2(44f, -28f), new Vector2(520f, 34f), 25, new Color(1f, 0.88f, 0.62f));
            title.text = "ANOTHER LIFE // REALM WAR PROTOTYPE";
            var subtitle = CreateText(canvasObj.transform, "PrototypeSubtitle", new Vector2(44f, -62f), new Vector2(520f, 42f), 15, new Color(0.72f, 0.82f, 0.92f));
            subtitle.text = "2.5D kingdom command with 3D champion warzone staging.";

            GameObject textObj = new GameObject("ResourceText");
            textObj.transform.SetParent(canvasObj.transform);
            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            text.fontSize = 15;
            text.color = new Color(0.86f, 0.91f, 0.96f);
            text.text = "Initializing command state...";
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(44, -116);
            rect.sizeDelta = new Vector2(512, 160);

            _modeText = CreateText(canvasObj.transform, "ModeText", new Vector2(44f, -286f), new Vector2(512f, 42f), 15, new Color(0.56f, 0.70f, 0.84f));
            _modeText.text = "INNER REALM: command board active // OUTER WARZONE: champion staging live";

            _statusText = CreateText(canvasObj.transform, "StatusText", new Vector2(44, -336), new Vector2(512, 82), 17, Color.white);
            _statusText.text = "Command console ready. Use the board, inspect realm state, then fight in 3D.";

            CreateButton(canvasObj.transform, "JOIN CROWNLANDS", new Vector2(44, -446), () =>
            {
                ServiceLocator.Get<IRealmService>().SelectRealm(RealmId.Crownlands);
                SetStatus("Crownlands oath recorded. Kingdom board and warzone staging are now aligned.");
            });

            CreateButton(canvasObj.transform, "SUPPLY CACHE", new Vector2(44, -500), () =>
            {
                var resources = ServiceLocator.Get<IResourceService>();
                foreach (ResourceType resourceType in ResourceRules.WalletResources)
                {
                    long amount = ResourceRules.IsRareResource(resourceType) ? 100 : 1000;
                    resources.AddResource(resourceType, amount);
                }

                SetStatus("Prototype supply cache delivered for kingdom-system inspection.");
            });

            CreateButton(canvasObj.transform, "UPGRADE FARM", new Vector2(44, -554), () =>
            {
                ServiceLocator.Get<IBuildingService>().StartUpgrade("Farm");
                SetStatus("Farm upgrade order issued if Stone reserves are sufficient.");
            });

            CreateButton(canvasObj.transform, "MUSTER INFANTRY", new Vector2(44, -608), () =>
            {
                ServiceLocator.Get<ITrainingService>().StartTraining(TroopType.Infantry, 25);
                SetStatus($"Infantry muster order resolved. Total: {ServiceLocator.Get<ITrainingService>().GetTroopCount(TroopType.Infantry)}");
            });

            CreateButton(canvasObj.transform, "WARZONE STIPEND", new Vector2(44, -662), () =>
            {
                ServiceLocator.Get<IWarzoneCreditService>().AddCredits(250);
                SetStatus($"Prototype Warzone Credits: {ServiceLocator.Get<IWarzoneCreditService>().GetCredits()}");
            });

            CreateButton(canvasObj.transform, "ARMOR COLOR", new Vector2(44, -716), () =>
            {
                FindObjectOfType<ChampionCustomizationController>()?.CyclePrimaryColor();
                SetStatus("Champion primary armor color cycled.");
            });

            CreateButton(canvasObj.transform, "HAIR COLOR", new Vector2(252, -446), () =>
            {
                FindObjectOfType<ChampionCustomizationController>()?.CycleHairColor();
                SetStatus("Champion hair color cycled.");
            });

            CreateButton(canvasObj.transform, "CAPE", new Vector2(252, -500), () =>
            {
                FindObjectOfType<ChampionCustomizationController>()?.ToggleCape();
                SetStatus("Champion cape toggled.");
            });

            CreateButton(canvasObj.transform, "BODY PRESET", new Vector2(460, -446), () =>
            {
                FindObjectOfType<ChampionCustomizationController>()?.CycleBodyPreset();
                SetStatus("Champion body preset cycled.");
            });

            CreateButton(canvasObj.transform, "HAIR STYLE", new Vector2(460, -500), () =>
            {
                FindObjectOfType<ChampionCustomizationController>()?.CycleHairStyle();
                SetStatus("Champion hair style cycled.");
            });

            CreateButton(canvasObj.transform, "ARMOR STYLE", new Vector2(460, -554), () =>
            {
                FindObjectOfType<ChampionCustomizationController>()?.CycleArmorStyle();
                SetStatus("Champion armor style cycled.");
            });

            CreateButton(canvasObj.transform, "HELMET", new Vector2(460, -608), () =>
            {
                FindObjectOfType<ChampionCustomizationController>()?.ToggleHelmet();
                SetStatus("Champion helmet toggled.");
            });

            CreateButton(canvasObj.transform, "ASSIST MODE", new Vector2(252, -554), () =>
            {
                FindObjectOfType<AutoCombatController>()?.SetMode(AutoMode.SemiAuto);
                SetStatus("Assist mode enabled. Manual input interrupts it.");
            });

            CreateButton(canvasObj.transform, "AUTO MODE", new Vector2(252, -608), () =>
            {
                FindObjectOfType<AutoCombatController>()?.SetMode(AutoMode.FullAuto);
                SetStatus("Auto mode enabled. Manual input interrupts it.");
            });

            CreateButton(canvasObj.transform, "SECURE BORDER", new Vector2(252, -662), () =>
            {
                var realm = ServiceLocator.Get<IRealmService>().CurrentRealmId;
                if (realm == RealmId.None)
                {
                    realm = RealmId.Crownlands;
                    ServiceLocator.Get<IRealmService>().SelectRealm(realm);
                }

                ServiceLocator.Get<ITerritoryService>().CaptureTerritory("T5", realm);
                SetStatus($"Prototype border outpost secured for {realm}.");
            });

            CreateButton(canvasObj.transform, "BATTLE SIM", new Vector2(252, -716), RunTestBattle);
            CreateButton(canvasObj.transform, "RESET TARGETS", new Vector2(460, -716), SpawnArenaTargets);
            CreateButton(canvasObj.transform, "CHAMPION DUEL", new Vector2(44, -770), StartChampionDuel);

            // Post-combat kingdom build scene (greybox vertical slice). The build action
            // spends combat loot (a fixed slice budget) and writes to the local run state.
            CreateButton(canvasObj.transform, "KINGDOM BUILD", new Vector2(252, -770), () =>
            {
                GreyboxKingdomBuildController.Toggle();
                SetStatus("Kingdom build scene toggled. Construct or upgrade a structure with your combat loot.");
            });

            StartCoroutine(UpdateResourceText(text));
        }

        private void StartChampionDuel()
        {
            if (_championDuel == null)
            {
                var encounterObject = new GameObject("GreyboxCombatEncounter");
                _championDuel = encounterObject.AddComponent<GreyboxCombatEncounter>();
                _championDuel.Completed += OnChampionDuelCompleted;
                _championDuel.ReturnRequested += OnChampionDuelReturn;
            }

            _championDuel.BeginEncounter();
        }

        private void OnChampionDuelCompleted(SliceCombatResult result)
        {
            SetStatus($"Champion duel {result.Outcome.ToString().ToUpperInvariant()} — {result.ChampionDisplayName} vs {result.OpponentDisplayName} in {result.TurnsTaken} turn(s).");
        }

        private void OnChampionDuelReturn()
        {
            SetStatus("Champion duel concluded. Command board restored.");
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
            floor.transform.localScale = new Vector3(10f, 1f, 10f);
            ApplyMaterial(floor.GetComponent<Renderer>(), new Color(0.045f, 0.052f, 0.062f), 0.02f, 0.50f);

            RenderSettings.ambientLight = new Color(0.14f, 0.16f, 0.19f);
            CreateWarzoneFrame();
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

                var dummy = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                dummy.name = name;
                float angle = i * Mathf.PI * 2f / 12f;
                dummy.transform.position = new Vector3(Mathf.Cos(angle) * 7f, 0.5f, Mathf.Sin(angle) * 7f);
                dummy.transform.localScale = new Vector3(0.32f, 0.70f + (i % 3) * 0.12f, 0.32f);
                ApplyMaterial(dummy.GetComponent<Renderer>(), new Color(0.54f, 0.13f, 0.11f), 0.05f, 0.58f, new Color(0.24f, 0.02f, 0.02f));
            }

            if (GameObject.Find("BossDummy") == null)
            {
                var boss = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                boss.name = "BossDummy";
                boss.transform.position = new Vector3(0f, 1.5f, 10f);
                boss.transform.localScale = new Vector3(2.2f, 2.2f, 2.2f);
                ApplyMaterial(boss.GetComponent<Renderer>(), new Color(0.45f, 0.04f, 0.05f), 0.12f, 0.68f, new Color(0.35f, 0.02f, 0.02f));
                boss.AddComponent<BossDummyAI>();
                CreateBossCrown(boss.transform);
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
                ApplyMaterial(bot.GetComponent<Renderer>(), new Color(0.36f, 0.10f, 0.54f), 0.06f, 0.60f, new Color(0.10f, 0.02f, 0.16f));
                bot.AddComponent<BotChampionAI>();
            }

            SetStatus("Warzone targets staged. Use WASD, mouse click, and Space to fight.");
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
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
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
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
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

            if (_modeText != null)
            {
                _modeText.text = "LIVE TEST // realm command, kingdom board, and champion warzone staging";
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
                    text.text = "TREASURY\n" +
                               $"FOOD {resources.GetResourceCount(ResourceType.Food),8}   WOOD {resources.GetResourceCount(ResourceType.Wood),8}\n" +
                               $"STONE {resources.GetResourceCount(ResourceType.Stone),7}   GOLD {resources.GetResourceCount(ResourceType.Gold),8}\n" +
                               $"MANA {resources.GetResourceCount(ResourceType.ManaStone),8}   ORE {resources.GetResourceCount(ResourceType.Ore),9}\n" +
                               $"{rareResourceType.ToString().ToUpperInvariant()} {resources.GetResourceCount(rareResourceType),8}";
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void CreateWarzoneFrame()
        {
            var root = new GameObject("Demo_WarzoneStaging");
            for (int i = -1; i <= 1; i += 2)
            {
                CreatePrimitive(root.transform, "RealmGatePylon", PrimitiveType.Cube, new Vector3(i * 8.8f, 1.35f, 8.5f), new Vector3(0.42f, 2.7f, 0.42f), new Color(0.18f, 0.20f, 0.24f), new Color(0.14f, 0.05f, 0.02f));
                CreatePrimitive(root.transform, "CrossroadBeacon", PrimitiveType.Cylinder, new Vector3(i * 4.8f, 0.42f, -6.7f), new Vector3(0.28f, 0.84f, 0.28f), _blue, _blue * 0.18f);
            }

            CreatePrimitive(root.transform, "RealmGateLintel", PrimitiveType.Cube, new Vector3(0f, 2.85f, 8.5f), new Vector3(9.8f, 0.34f, 0.38f), new Color(0.20f, 0.18f, 0.18f), new Color(0.20f, 0.07f, 0.02f));
            CreatePrimitive(root.transform, "CentralCrossroad", PrimitiveType.Cylinder, new Vector3(0f, 0.07f, -3.2f), new Vector3(1.8f, 0.035f, 1.8f), new Color(0.10f, 0.12f, 0.15f), _gold * 0.08f);
            CreatePrimitive(root.transform, "DragonWishMarker", PrimitiveType.Cylinder, new Vector3(0f, 0.28f, 7.15f), new Vector3(0.44f, 0.56f, 0.44f), _gold, _gold * 0.24f);
        }

        private void CreateBossCrown(Transform boss)
        {
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI * 2f / 6f;
                var shard = CreatePrimitive(boss, "BossCrownShard", PrimitiveType.Cube, new Vector3(Mathf.Cos(angle) * 0.74f, 0.95f, Mathf.Sin(angle) * 0.74f), new Vector3(0.12f, 0.62f, 0.12f), new Color(0.20f, 0.02f, 0.03f), new Color(0.34f, 0.02f, 0.02f));
                shard.transform.localRotation = Quaternion.Euler(18f, -angle * Mathf.Rad2Deg, 22f);
            }
        }

        private GameObject CreatePrimitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color, Color? emission = null)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            ApplyMaterial(obj.GetComponent<Renderer>(), color, 0.04f, 0.54f, emission);
            return obj;
        }

        private void ApplyMaterial(Renderer renderer, Color color, float metallic = 0f, float smoothness = 0.35f, Color? emission = null)
        {
            if (renderer == null || renderer.sharedMaterial == null)
            {
                return;
            }

            var style = new MaterialStyle(color, metallic, smoothness, emission);
            if (!_materials.TryGetValue(style, out Material material))
            {
                material = new Material(renderer.sharedMaterial)
                {
                    name = "DemoStyleMaterial"
                };
                _materials.Add(style, material);
            }

            material.color = color;
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            if (emission.HasValue && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }

            renderer.sharedMaterial = material;
        }

        private void OnDestroy()
        {
            foreach (Material material in _materials.Values)
            {
                if (material == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(material);
                }
                else
                {
                    DestroyImmediate(material);
                }
            }

            _materials.Clear();
        }

        private Image CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Color color)
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
    }
}
