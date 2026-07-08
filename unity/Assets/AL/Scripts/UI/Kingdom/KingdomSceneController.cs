using System;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Kingdom;
using AL.Kingdom.Visuals;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AL.UI.Kingdom
{
    public class KingdomSceneController : MonoBehaviour
    {
        [SerializeField] private string _arenaSceneName = "ChampionArena";

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
        private Text _messageText;
        private Text _boardHintText;
        private Image _messageAccent;
        private GameObject _dashboardRoot;
        private Text _dashboardToggleText;
        private KingdomVisualizer _kingdomVisualizer;
        private Color _messageAccentBaseColor = new Color(0.42f, 0.62f, 0.78f, 0.92f);
        private float _completionTimer;
        private float _messagePulseTimer;
        private bool _dashboardVisible = true;

        private readonly string[] _buildingIds =
        {
            "TownHall", "Farm", "LumberMill", "Quarry", "GoldMine", "ManaShrine", "Mine", "Barracks"
        };

        private const int WarmasterPieceCost = 100;

        private static readonly string[] WarmasterPieceIds =
        {
            "warmaster_weapon",
            "warmaster_helm",
            "warmaster_chest",
            "warmaster_gloves",
            "warmaster_boots",
            "warmaster_cape",
            "warmaster_ring",
            "warmaster_amulet",
            "warmaster_mount_armor",
            "warmaster_class_relic"
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
            Bootloader.InitializeIfMissing();

            var save = ServiceLocator.Get<ISaveGameService>();
            if (save.CurrentSave == null)
            {
                save.Load();
            }

            BuildRuntimeWorld();
            BuildRuntimeUi();
            Refresh();
        }

        private void Update()
        {
            UpdateCommandMessagePulse();

            _completionTimer += Time.deltaTime;
            if (_completionTimer < 1f)
            {
                return;
            }

            _completionTimer = 0f;
            var buildingService = ServiceLocator.Get<IBuildingService>();
            foreach (var buildingId in _buildingIds)
            {
                buildingService.CompleteUpgrade(buildingId);
            }

            var researchService = ServiceLocator.Get<IResearchService>();
            researchService.CompleteResearch("Steel Forging");
            researchService.CompleteResearch("Plate Armor");

            Refresh();
        }

        private void BuildRuntimeWorld()
        {
            ConfigureKingdomCamera();
            ConfigureKingdomLighting();

            var visualizerObject = new GameObject("Kingdom_2_5D_Board");
            _kingdomVisualizer = visualizerObject.AddComponent<KingdomVisualizer>();
            _kingdomVisualizer.InitializeKingdom();
        }

        private static void ConfigureKingdomCamera()
        {
            var cameraObject = UnityEngine.Camera.main != null
                ? UnityEngine.Camera.main.gameObject
                : new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<UnityEngine.Camera>() ?? cameraObject.AddComponent<UnityEngine.Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8.6f;
            camera.transform.position = new Vector3(0f, 10.4f, -10.8f);
            camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.020f, 0.026f, 0.034f);

            if (cameraObject.GetComponent<AudioListener>() == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }

            var controls = cameraObject.GetComponent<KingdomBoardCameraController>() ?? cameraObject.AddComponent<KingdomBoardCameraController>();
            controls.Configure(camera);
        }

        private static void ConfigureKingdomLighting()
        {
            RenderSettings.ambientLight = new Color(0.20f, 0.22f, 0.24f);
            var lightObject = GameObject.Find("Kingdom_KeyLight") ?? new GameObject("Kingdom_KeyLight");
            var light = lightObject.GetComponent<Light>() ?? lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.92f, 0.78f);
            lightObject.transform.rotation = Quaternion.Euler(48f, -36f, 0f);
        }

        private void BuildRuntimeUi()
        {
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
            _resourceText = CreateText(topBar.transform, "ResourceText", font, 19, TextAnchor.UpperLeft, new Vector2(20f, -56f), new Vector2(1136f, 42f));
            _resourceText.color = new Color(0.82f, 0.88f, 0.94f);

            _buildingText = CreatePanelText(_dashboardRoot.transform, "DistrictPanel", "BuildingText", font, 18, TextAnchor.UpperLeft, new Vector2(32f, -150f), new Vector2(520f, 292f));
            _troopText = CreatePanelText(_dashboardRoot.transform, "ForcesPanel", "TroopText", font, 18, TextAnchor.UpperLeft, new Vector2(32f, -460f), new Vector2(520f, 214f));
            _researchText = CreatePanelText(_dashboardRoot.transform, "ResearchPanel", "ResearchText", font, 18, TextAnchor.UpperLeft, new Vector2(584f, -150f), new Vector2(454f, 174f));
            _questText = CreatePanelText(_dashboardRoot.transform, "QuestPanel", "QuestText", font, 17, TextAnchor.UpperLeft, new Vector2(584f, -342f), new Vector2(454f, 166f));
            _territoryText = CreatePanelText(_dashboardRoot.transform, "TerritoryPanel", "TerritoryText", font, 17, TextAnchor.UpperLeft, new Vector2(584f, -526f), new Vector2(454f, 194f));
            _battleText = CreatePanelText(_dashboardRoot.transform, "BattlePanel", "BattleText", font, 17, TextAnchor.UpperLeft, new Vector2(584f, -738f), new Vector2(454f, 170f));

            var messagePanel = CreatePanel(_dashboardRoot.transform, "CommandMessagePanel", new Vector2(32f, 32f), new Vector2(1008f, 118f), Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.020f, 0.027f, 0.037f, 0.92f));
            _messageAccent = CreatePanel(messagePanel.transform, "CommandMessageAccent", new Vector2(0f, 0f), new Vector2(6f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Color(0.88f, 0.62f, 0.30f, 0.92f)).GetComponent<Image>();
            CreatePanel(messagePanel.transform, "CommandMessageTopRule", new Vector2(0f, -1f), new Vector2(-34f, 2f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Color(1f, 0.86f, 0.54f, 0.20f));
            CreatePanel(messagePanel.transform, "CommandMessageBottomRule", new Vector2(0f, 1f), new Vector2(-34f, 2f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Color(0.28f, 0.56f, 0.78f, 0.18f));
            _messageHeaderText = CreateText(messagePanel.transform, "MessageHeaderText", font, 13, TextAnchor.UpperLeft, new Vector2(18f, -10f), new Vector2(380f, 20f));
            _messageHeaderText.text = "COMMAND DOSSIER";
            _messageHeaderText.color = new Color(0.78f, 0.86f, 0.94f);
            _messageMetaText = CreateText(messagePanel.transform, "MessageMetaText", font, 13, TextAnchor.UpperRight, new Vector2(648f, -10f), new Vector2(336f, 20f));
            _messageMetaText.color = new Color(0.54f, 0.66f, 0.76f);
            _messageText = CreateText(messagePanel.transform, "MessageText", font, 20, TextAnchor.UpperLeft, new Vector2(18f, -36f), new Vector2(966f, 66f));
            _messageText.color = new Color(0.92f, 0.96f, 1f);
            _messageText.verticalOverflow = VerticalWrapMode.Truncate;

            var commandDeck = CreatePanel(_dashboardRoot.transform, "CommandDeck", new Vector2(-28f, -78f), new Vector2(430f, 936f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Color(0.026f, 0.033f, 0.044f, 0.92f));
            CreatePanel(commandDeck.transform, "CommandDeckAccent", new Vector2(0f, 0f), new Vector2(6f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Color(0.86f, 0.62f, 0.30f, 0.88f));
            CreatePanel(commandDeck.transform, "CommandDeckTopRule", new Vector2(0f, -1f), new Vector2(-30f, 2f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Color(1f, 0.86f, 0.54f, 0.18f));
            var commandTitle = CreateText(commandDeck.transform, "CommandDeckTitle", font, 23, TextAnchor.UpperLeft, new Vector2(18f, -16f), new Vector2(380f, 34f));
            commandTitle.text = "COMMAND DECK";
            commandTitle.color = new Color(1f, 0.88f, 0.62f);

            CreateSectionHeader(commandDeck.transform, font, "BUILD", new Vector2(18f, -64f));
            CreateDeckButton(commandDeck.transform, font, "Town Hall", new Vector2(-222f, -104f), () => UpgradeBuilding("TownHall"));
            CreateDeckButton(commandDeck.transform, font, "Farm", new Vector2(-18f, -104f), () => UpgradeBuilding("Farm"));
            CreateDeckButton(commandDeck.transform, font, "Lumber", new Vector2(-222f, -152f), () => UpgradeBuilding("LumberMill"));
            CreateDeckButton(commandDeck.transform, font, "Quarry", new Vector2(-18f, -152f), () => UpgradeBuilding("Quarry"));
            CreateDeckButton(commandDeck.transform, font, "Gold Mine", new Vector2(-222f, -200f), () => UpgradeBuilding("GoldMine"));
            CreateDeckButton(commandDeck.transform, font, "Mana Shrine", new Vector2(-18f, -200f), () => UpgradeBuilding("ManaShrine"));
            CreateDeckButton(commandDeck.transform, font, "Mine", new Vector2(-222f, -248f), () => UpgradeBuilding("Mine"));

            CreateSectionHeader(commandDeck.transform, font, "FORCES", new Vector2(18f, -302f));
            CreateDeckButton(commandDeck.transform, font, "Infantry", new Vector2(-222f, -342f), () => TrainTroops(TroopType.Infantry));
            CreateDeckButton(commandDeck.transform, font, "Ranged", new Vector2(-18f, -342f), () => TrainTroops(TroopType.Ranged));
            CreateDeckButton(commandDeck.transform, font, "Claim", new Vector2(-222f, -390f), ClaimCompletedQuests);

            CreateSectionHeader(commandDeck.transform, font, "PROGRESSION", new Vector2(18f, -444f));
            CreateDeckButton(commandDeck.transform, font, "Steel", new Vector2(-222f, -484f), () => StartResearch("Steel Forging"));
            CreateDeckButton(commandDeck.transform, font, "Armor", new Vector2(-18f, -484f), () => StartResearch("Plate Armor"));
            CreateDeckButton(commandDeck.transform, font, "Warzone", new Vector2(-222f, -532f), EarnWarzoneCredits);
            CreateDeckButton(commandDeck.transform, font, "Warmaster", new Vector2(-18f, -532f), UnlockWarmaster);

            CreateSectionHeader(commandDeck.transform, font, "REALM OPS", new Vector2(18f, -586f));
            CreateDeckButton(commandDeck.transform, font, "Capture", new Vector2(-222f, -626f), CaptureBorderlands);
            CreateDeckButton(commandDeck.transform, font, "Secure Gem", new Vector2(-18f, -626f), PickTestGem);
            CreateDeckButton(commandDeck.transform, font, "Wishgate", new Vector2(-222f, -674f), EarnWishgate);
            CreateDeckButton(commandDeck.transform, font, "Claim Wish", new Vector2(-18f, -674f), ChooseWishReward);
            CreateDeckButton(commandDeck.transform, font, "War Drill", new Vector2(-222f, -722f), RunTestBattle);
            CreateDeckButton(commandDeck.transform, font, "Champion", new Vector2(-18f, -722f), () => SceneManager.LoadScene(_arenaSceneName));
            CreateDeckButton(commandDeck.transform, font, "Reset Save", new Vector2(-18f, -812f), ResetSave, new Color(0.34f, 0.12f, 0.12f, 1f));

            var toggle = CreateButton(canvas.transform, font, "Board View", new Vector2(-24f, -24f), ToggleDashboard, new Vector2(170f, 42f), new Color(0.075f, 0.095f, 0.122f, 0.96f));
            _dashboardToggleText = toggle.GetComponentInChildren<Text>();
            _boardHintText = CreateBoardHintText(canvas.transform, font);
            SetMessage("Command board online. Select a district or border outpost to inspect yield, readiness, and next order.");
            RefreshBoardHintVisibility();
        }

        private void UpgradeBuilding(string buildingId)
        {
            ServiceLocator.Get<IBuildingService>().StartUpgrade(buildingId);
            SetMessage($"BUILD ORDER: {FormatBuildingName(buildingId)} upgrade queued. Watch the district timer before committing the next resource spend.");
            Refresh();
        }

        private void StartResearch(string researchId)
        {
            ServiceLocator.Get<IResearchService>().StartResearch(researchId);
            SetMessage($"RESEARCH ORDER: {researchId} filed. Combat bonuses update when the lab timer clears.");
            Refresh();
        }

        private void RunTestBattle()
        {
            RealmId attackerRealm = ServiceLocator.Get<IRealmService>().CurrentRealmId;
            if (attackerRealm == RealmId.None)
            {
                attackerRealm = RealmId.Crownlands;
            }

            var request = new BattleRequest
            {
                Type = BattleType.Warzone,
                RandomSeed = 20260708,
                AttackerRealm = attackerRealm,
                DefenderRealm = RealmId.Umbral,
                AttackerMorale = 1.08f,
                DefenderMorale = 0.96f,
                TerrainId = "border_forest_road",
                AttackerTroops = new List<TroopStack>
                {
                    new TroopStack { Type = TroopType.Infantry, Count = 80 },
                    new TroopStack { Type = TroopType.Ranged, Count = 45 },
                    new TroopStack { Type = TroopType.Cavalry, Count = 20 }
                },
                DefenderTroops = new List<TroopStack>
                {
                    new TroopStack { Type = TroopType.Infantry, Count = 70 },
                    new TroopStack { Type = TroopType.Ranged, Count = 35 }
                }
            };

            var report = ServiceLocator.Get<IBattleSimulator>().Simulate(request);
            _battleText.text =
                "Battle Report\n" +
                $"{report.Summary}\n" +
                $"Rounds: {report.Rounds}  Seed: {request.RandomSeed}\n" +
                $"Attacker losses: {FormatDetailedLosses(report.AttackerDetailedLosses)}\n" +
                $"Defender losses: {FormatDetailedLosses(report.DefenderDetailedLosses)}\n" +
                $"Loot: {FormatLoot(report.Loot)}";
            SetMessage(report.IsWinner ? "WAR DRILL: Victory profile confirmed. Scale troop production before pushing another border." : "WAR DRILL: Defeat profile logged. Reinforce troops or research before the next push.");
        }

        private void TrainTroops(TroopType type)
        {
            ServiceLocator.Get<ITrainingService>().StartTraining(type, 25);
            SetMessage($"MUSTER ORDER: 25 {type} added to the queue. Keep force growth aligned with border captures.");
            Refresh();
        }

        private void EarnWarzoneCredits()
        {
            ServiceLocator.Get<IWarzoneCreditService>().AddCredits(250);
            SetMessage("WARZONE PAYOUT: +250 Credits secured for Warmaster progression.");
            Refresh();
        }

        private void UnlockWarmaster()
        {
            var warmaster = ServiceLocator.Get<IWarmasterService>();
            if (warmaster.IsTrueWarmaster())
            {
                SetMessage("True Warmaster set is already complete and equipped.");
                return;
            }

            string nextPieceId = GetNextWarmasterPieceId(warmaster.GetState());
            if (string.IsNullOrWhiteSpace(nextPieceId))
            {
                SetMessage("Warmaster pieces are complete. The True Warmaster set is ready.");
                Refresh();
                return;
            }

            if (!warmaster.PurchasePiece(nextPieceId, WarmasterPieceCost))
            {
                SetMessage($"Need {WarmasterPieceCost} Warzone Credits to buy the next Warmaster piece.");
                Refresh();
                return;
            }

            if (warmaster.IsTrueWarmaster())
            {
                SetMessage("True Warmaster set completed and equipped.");
            }
            else
            {
                SetMessage($"Purchased {FormatWarmasterPieceName(nextPieceId)} ({warmaster.GetPurchasedPieceCount()}/{warmaster.GetRequiredPieceCount()}).");
            }

            Refresh();
        }

        private void ClaimCompletedQuests()
        {
            var questService = ServiceLocator.Get<IQuestService>();
            int claimed = 0;
            foreach (var quest in questService.GetActiveQuests())
            {
                if (!quest.IsCompleted || quest.IsClaimed)
                {
                    continue;
                }

                questService.ClaimReward(quest.QuestId);
                claimed++;
            }

            SetMessage(claimed > 0 ? $"Claimed {claimed} quest reward(s)." : "No completed quest rewards to claim.");
            Refresh();
        }

        private void CaptureBorderlands()
        {
            var realm = ServiceLocator.Get<IRealmService>().CurrentRealmId;
            if (realm == RealmId.None)
            {
                realm = RealmId.Crownlands;
            }

            ServiceLocator.Get<ITerritoryService>().CaptureTerritory("T5", realm);
            SetMessage($"REALM OPS: Neutral Borderlands captured for {realm}. Confirm the new yield in the war zone panel.");
            Refresh();
        }

        private void PickTestGem()
        {
            var gemService = ServiceLocator.Get<IRealmGemService>();
            bool pickedUp = gemService.PickUpGem("Stonehold_Gem_1", "offline_player");
            SetMessage(pickedUp ? "REALM GEM: Stonehold Gem secured by the active carrier." : "REALM GEM: Pickup denied. Confirm the gem is exposed before assigning a carrier.");
            Refresh();
        }

        private void EarnWishgate()
        {
            ServiceLocator.Get<IRealmGemService>().MarkWishgateEarned("Offline realm objective test");
            SetMessage("WISHGATE: Realm objective fulfilled. Choose a reward when the command window is stable.");
            Refresh();
        }

        private void ChooseWishReward()
        {
            ServiceLocator.Get<IRealmGemService>().ChooseWishReward("warmaster_credits");
            ServiceLocator.Get<IWarzoneCreditService>().AddCredits(300);
            SetMessage("WISHGATE: Warmaster Credits selected. +300 Credits added to the war chest.");
            Refresh();
        }

        private void ResetSave()
        {
            var save = ServiceLocator.Get<ISaveGameService>();
            save.DeleteSave();
            save.Load();
            SetMessage("Save reset. Choose a realm again from the boot flow.");
            SceneManager.LoadScene("Boot");
        }

        private void Refresh()
        {
            _kingdomVisualizer?.RefreshVisuals();

            var realm = ServiceLocator.Get<IRealmService>().CurrentRealm;
            _realmText.text = realm == null
                ? "ANOTHERLIFE COMMAND"
                : $"{realm.RealmName.ToUpperInvariant()} COMMAND";

            var resources = ServiceLocator.Get<IResourceService>();
            var selectedRealmId = ServiceLocator.Get<IRealmService>().CurrentRealmId;
            ResourceType rareResourceType = ResourceRules.GetRareResourceForRealm(selectedRealmId);
            _resourceText.text =
                $"Food {resources.GetResourceCount(ResourceType.Food)}   |   " +
                $"Wood {resources.GetResourceCount(ResourceType.Wood)}   |   " +
                $"Stone {resources.GetResourceCount(ResourceType.Stone)}   |   " +
                $"Gold {resources.GetResourceCount(ResourceType.Gold)}\n" +
                $"Mana {resources.GetResourceCount(ResourceType.ManaStone)}   |   " +
                $"Ore {resources.GetResourceCount(ResourceType.Ore)}   |   " +
                $"{rareResourceType} {resources.GetResourceCount(rareResourceType)}   |   " +
                $"Warzone {ServiceLocator.Get<IWarzoneCreditService>().GetCredits()}";

            var buildings = ServiceLocator.Get<IBuildingService>();
            var builder = new StringBuilder();
            builder.AppendLine("DISTRICTS");
            foreach (var buildingId in _buildingIds)
            {
                BuildingState state = buildings.GetBuildingState(buildingId);
                string timer = state.IsUpgrading
                    ? $"UPGRADING {Math.Max(0, state.UpgradeCompleteTimestamp - DateTimeOffset.UtcNow.ToUnixTimeSeconds())}s"
                    : "READY";
                builder.AppendLine($"{FormatBuildingName(buildingId),-12}  Lv {state.Level}  {timer}");
            }

            _buildingText.text = builder.ToString();

            var training = ServiceLocator.Get<ITrainingService>();
            var warmaster = ServiceLocator.Get<IWarmasterService>();
            var warmasterState = warmaster.GetState();
            string equippedWarmasterSet = string.IsNullOrWhiteSpace(warmasterState?.EquippedSetId)
                ? "none"
                : warmasterState.EquippedSetId;
            string warmasterRank = warmaster.IsTrueWarmaster() ? "True Warmaster" : "assembling";
            _troopText.text =
                "FORCES\n" +
                $"Infantry     {training.GetTroopCount(TroopType.Infantry)}\n" +
                $"Cavalry      {training.GetTroopCount(TroopType.Cavalry)}\n" +
                $"Ranged       {training.GetTroopCount(TroopType.Ranged)}\n" +
                $"Siege        {training.GetTroopCount(TroopType.Siege)}\n" +
                $"Warmaster    {warmaster.GetPurchasedPieceCount()}/{warmaster.GetRequiredPieceCount()}\n" +
                $"Set          {equippedWarmasterSet} ({warmasterRank})";

            var research = ServiceLocator.Get<IResearchService>();
            var steel = research.GetResearchState("Steel Forging");
            var armor = research.GetResearchState("Plate Armor");
            _researchText.text =
                "RESEARCH\n" +
                FormatResearch("Steel Forging", steel) + "\n" +
                FormatResearch("Plate Armor", armor) + "\n" +
                $"Attack bonus: {research.GetStatBonus(StatType.Attack):P0}\n" +
                $"Defense bonus: {research.GetStatBonus(StatType.Defense):P0}";

            var quests = new StringBuilder();
            quests.AppendLine("OBJECTIVES");
            foreach (var quest in ServiceLocator.Get<IQuestService>().GetActiveQuests())
            {
                string state = quest.IsCompleted ? "complete" : "active";
                quests.AppendLine($"{quest.QuestId}: {quest.CurrentValue} ({state})");
            }
            _questText.text = quests.ToString();

            var territories = new StringBuilder();
            territories.AppendLine("War Zone");
            foreach (var territory in ServiceLocator.Get<ITerritoryService>().GetTerritories())
            {
                territories.AppendLine($"{territory.Name}: {territory.OwnerRealm} (+{territory.BonusAmount} {territory.BonusType})");
            }
            var wishgate = ServiceLocator.Get<IRealmGemService>().GetWishgateState();
            territories.AppendLine(wishgate != null && wishgate.IsEarned ? "Wishgate: earned" : "Wishgate: dormant");
            foreach (var gem in ServiceLocator.Get<IRealmGemService>().GetRealmGems())
            {
                if (!gem.IsAtHome || gem.IsDropped)
                {
                    territories.AppendLine($"{gem.GemId}: carrier {gem.CarrierId ?? "dropped"}");
                }
            }
            _territoryText.text = territories.ToString();
        }

        private void SetMessage(string message)
        {
            string cleanMessage = string.IsNullOrWhiteSpace(message)
                ? "Command board online. Select a district or border outpost to inspect yield, readiness, and next order."
                : message;

            Color accent = GetMessageAccent(cleanMessage);
            _messageAccentBaseColor = accent;
            if (_messageAccent != null)
            {
                _messageAccent.color = accent;
                _messagePulseTimer = 0.62f;
            }

            if (_messageHeaderText != null)
            {
                _messageHeaderText.text = "COMMAND DOSSIER";
            }

            if (_messageMetaText != null)
            {
                _messageMetaText.text = DateTime.Now.ToString("HH:mm:ss") + " / LIVE OPS";
            }

            if (_messageText != null)
            {
                _messageText.text = cleanMessage;
            }

            if (_boardHintText != null)
            {
                _boardHintText.text = cleanMessage;
            }
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
            if (_messageAccent == null || _messagePulseTimer <= 0f)
            {
                return;
            }

            _messagePulseTimer -= Time.deltaTime;
            if (_messagePulseTimer <= 0f)
            {
                _messageAccent.color = _messageAccentBaseColor;
                return;
            }

            float pulse = Mathf.PingPong(Time.time * 5.5f, 1f);
            _messageAccent.color = Color.Lerp(_messageAccentBaseColor, Color.white, pulse * 0.16f);
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
            var panel = CreatePanel(parent, panelName, anchoredPosition, panelSize, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.026f, 0.034f, 0.045f, 0.84f));
            CreatePanel(panel.transform, panelName + "_Accent", new Vector2(0f, 0f), new Vector2(4f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Color(0.36f, 0.55f, 0.70f, 0.46f));
            CreatePanel(panel.transform, panelName + "_TopRule", new Vector2(0f, -1f), new Vector2(-30f, 2f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Color(1f, 0.86f, 0.54f, 0.13f));
            var text = CreateText(panel.transform, textName, font, size, alignment, new Vector2(18f, -16f), new Vector2(panelSize.x - 36f, panelSize.y - 30f));
            text.color = new Color(0.86f, 0.91f, 0.96f);
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
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

        private static void CreateSectionHeader(Transform parent, Font font, string label, Vector2 anchoredPosition)
        {
            var header = CreateText(parent, label + "_Header", font, 16, TextAnchor.UpperLeft, anchoredPosition, new Vector2(360f, 28f));
            header.text = label;
            header.color = new Color(0.58f, 0.68f, 0.78f);
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

            CreatePanel(buttonObject.transform, "ButtonAccent", new Vector2(0f, 0f), new Vector2(3f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Color(0.86f, 0.62f, 0.30f, 0.54f));
            CreatePanel(buttonObject.transform, "ButtonTopTrace", new Vector2(0f, -1f), new Vector2(-18f, 1.5f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Color(1f, 0.90f, 0.66f, 0.13f));

            int fontSize = rect.sizeDelta.x <= 190f ? 17 : 20;
            var text = CreateText(buttonObject.transform, label + "_Text", font, fontSize, TextAnchor.MiddleCenter, Vector2.zero, rect.sizeDelta);
            text.text = label;
            text.color = new Color(0.92f, 0.96f, 1f);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }

        private static Font GetDefaultFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                   Resources.GetBuiltinResource<Font>("Arial.ttf");
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

        private static string FormatDetailedLosses(IEnumerable<TroopLossReport> losses)
        {
            if (losses == null)
            {
                return "none";
            }

            var builder = new StringBuilder();
            foreach (var loss in losses)
            {
                if (builder.Length > 0)
                {
                    builder.Append("; ");
                }

                builder.Append(loss.Type)
                    .Append(" K").Append(loss.Killed)
                    .Append(" W").Append(loss.Wounded)
                    .Append(" S").Append(loss.Survived);
            }

            return builder.Length == 0 ? "none" : builder.ToString();
        }

        private static string FormatLoot(IEnumerable<ResourceData> loot)
        {
            if (loot == null)
            {
                return "none";
            }

            var builder = new StringBuilder();
            foreach (var item in loot)
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(item.Type).Append(" ").Append(item.Amount);
            }

            return builder.Length == 0 ? "none" : builder.ToString();
        }

        private static Color GetMessageAccent(string message)
        {
            string lower = message?.ToLowerInvariant() ?? string.Empty;
            if (lower.Contains("defeat") || lower.Contains("need ") || lower.Contains("could not") || lower.Contains("no completed"))
            {
                return new Color(0.86f, 0.34f, 0.22f, 0.95f);
            }

            if (lower.Contains("victory") || lower.Contains("captured") || lower.Contains("earned") || lower.Contains("completed") || lower.Contains("purchased"))
            {
                return new Color(0.72f, 0.88f, 0.42f, 0.95f);
            }

            if (lower.Contains("lock") || lower.Contains("selected"))
            {
                return new Color(0.92f, 0.66f, 0.30f, 0.95f);
            }

            return new Color(0.42f, 0.62f, 0.78f, 0.92f);
        }

        private static string GetNextWarmasterPieceId(WarmasterState state)
        {
            foreach (string pieceId in WarmasterPieceIds)
            {
                if (state?.PurchasedPieceIds == null || !state.PurchasedPieceIds.Contains(pieceId))
                {
                    return pieceId;
                }
            }

            return null;
        }

        private static string FormatWarmasterPieceName(string pieceId)
        {
            if (string.IsNullOrWhiteSpace(pieceId))
            {
                return "Warmaster piece";
            }

            string name = pieceId.Replace("warmaster_", string.Empty).Replace("_", " ");
            return "Warmaster " + name;
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
            if (Input.touchCount > 0)
            {
                return;
            }

            if (!IsPointerOverUi() && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
            {
                Zoom(-Input.mouseScrollDelta.y * 0.65f);
            }

            if (Input.GetMouseButtonDown(1))
            {
                _lastPointerPosition = Input.mousePosition;
            }

            if (Input.GetMouseButton(1) && !IsPointerOverUi())
            {
                Vector3 delta = Input.mousePosition - _lastPointerPosition;
                Pan(delta, _mousePanSpeed);
                _lastPointerPosition = Input.mousePosition;
            }
        }

        private void HandleTouch()
        {
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    _lastPointerPosition = touch.position;
                }
                else if (touch.phase == TouchPhase.Moved && !IsPointerOverUi(touch.fingerId))
                {
                    Vector3 delta = (Vector3)touch.position - _lastPointerPosition;
                    Pan(delta, _touchPanSpeed);
                    _lastPointerPosition = touch.position;
                }
            }
            else if (Input.touchCount >= 2)
            {
                Touch a = Input.GetTouch(0);
                Touch b = Input.GetTouch(1);
                float distance = Vector2.Distance(a.position, b.position);
                if (a.phase == TouchPhase.Began || b.phase == TouchPhase.Began)
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
