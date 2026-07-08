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
        private Text _messageText;
        private Text _boardHintText;
        private GameObject _dashboardRoot;
        private Text _dashboardToggleText;
        private KingdomVisualizer _kingdomVisualizer;
        private float _completionTimer;
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
            bg.color = new Color(0.02f, 0.026f, 0.036f, 0.58f);
            Stretch(background.GetComponent<RectTransform>());

            _dashboardRoot = new GameObject("KingdomDashboardRoot");
            _dashboardRoot.transform.SetParent(canvas.transform, false);
            Stretch(_dashboardRoot.AddComponent<RectTransform>());

            _realmText = CreateText(_dashboardRoot.transform, "RealmText", font, 30, TextAnchor.UpperLeft, new Vector2(40, -30), new Vector2(900, 90));
            _resourceText = CreateText(_dashboardRoot.transform, "ResourceText", font, 24, TextAnchor.UpperLeft, new Vector2(40, -120), new Vector2(900, 130));
            _buildingText = CreateText(_dashboardRoot.transform, "BuildingText", font, 22, TextAnchor.UpperLeft, new Vector2(40, -250), new Vector2(500, 300));
            _troopText = CreateText(_dashboardRoot.transform, "TroopText", font, 22, TextAnchor.UpperLeft, new Vector2(40, -570), new Vector2(500, 180));
            _researchText = CreateText(_dashboardRoot.transform, "ResearchText", font, 22, TextAnchor.UpperLeft, new Vector2(600, -250), new Vector2(520, 210));
            _questText = CreateText(_dashboardRoot.transform, "QuestText", font, 20, TextAnchor.UpperLeft, new Vector2(600, -480), new Vector2(560, 200));
            _territoryText = CreateText(_dashboardRoot.transform, "TerritoryText", font, 20, TextAnchor.UpperLeft, new Vector2(600, -690), new Vector2(560, 170));
            _battleText = CreateText(_dashboardRoot.transform, "BattleText", font, 20, TextAnchor.UpperLeft, new Vector2(600, -870), new Vector2(560, 190));
            _messageText = CreateText(_dashboardRoot.transform, "MessageText", font, 22, TextAnchor.LowerLeft, new Vector2(40, 40), new Vector2(900, 80));

            CreateButton(_dashboardRoot.transform, font, "Upgrade Town Hall", new Vector2(-260, -80), () => UpgradeBuilding("TownHall"));
            CreateButton(_dashboardRoot.transform, font, "Upgrade Farm", new Vector2(-260, -145), () => UpgradeBuilding("Farm"));
            CreateButton(_dashboardRoot.transform, font, "Upgrade Lumber", new Vector2(-260, -210), () => UpgradeBuilding("LumberMill"));
            CreateButton(_dashboardRoot.transform, font, "Upgrade Quarry", new Vector2(-260, -275), () => UpgradeBuilding("Quarry"));
            CreateButton(_dashboardRoot.transform, font, "Upgrade Gold Mine", new Vector2(-260, -340), () => UpgradeBuilding("GoldMine"));
            CreateButton(_dashboardRoot.transform, font, "Upgrade Mana Shrine", new Vector2(-260, -405), () => UpgradeBuilding("ManaShrine"));
            CreateButton(_dashboardRoot.transform, font, "Upgrade Mine", new Vector2(-260, -470), () => UpgradeBuilding("Mine"));
            CreateButton(_dashboardRoot.transform, font, "Train Infantry", new Vector2(-260, -535), () => TrainTroops(TroopType.Infantry));
            CreateButton(_dashboardRoot.transform, font, "Train Ranged", new Vector2(-260, -600), () => TrainTroops(TroopType.Ranged));
            CreateButton(_dashboardRoot.transform, font, "Claim Quests", new Vector2(-260, -665), ClaimCompletedQuests);

            CreateButton(_dashboardRoot.transform, font, "Research Steel", new Vector2(-20, -80), () => StartResearch("Steel Forging"));
            CreateButton(_dashboardRoot.transform, font, "Research Armor", new Vector2(-20, -145), () => StartResearch("Plate Armor"));
            CreateButton(_dashboardRoot.transform, font, "Earn Warzone", new Vector2(-20, -210), EarnWarzoneCredits);
            CreateButton(_dashboardRoot.transform, font, "Buy Warmaster Piece", new Vector2(-20, -275), UnlockWarmaster);
            CreateButton(_dashboardRoot.transform, font, "Capture Border", new Vector2(-20, -340), CaptureBorderlands);
            CreateButton(_dashboardRoot.transform, font, "Pick Gem", new Vector2(-20, -405), PickTestGem);
            CreateButton(_dashboardRoot.transform, font, "Earn Wishgate", new Vector2(-20, -470), EarnWishgate);
            CreateButton(_dashboardRoot.transform, font, "Wish Reward", new Vector2(-20, -535), ChooseWishReward);
            CreateButton(_dashboardRoot.transform, font, "Test Battle", new Vector2(-20, -600), RunTestBattle);
            CreateButton(_dashboardRoot.transform, font, "Champion Arena", new Vector2(-20, -665), () => SceneManager.LoadScene(_arenaSceneName));
            CreateButton(_dashboardRoot.transform, font, "Reset Save", new Vector2(-20, -730), ResetSave);

            var toggle = CreateButton(canvas.transform, font, "Board View", new Vector2(-20, -20), ToggleDashboard);
            _dashboardToggleText = toggle.GetComponentInChildren<Text>();
            _boardHintText = CreateBoardHintText(canvas.transform, font);
            _boardHintText.text = "Board View: drag, zoom, and select buildings or outposts for details.";
            RefreshBoardHintVisibility();
        }

        private void UpgradeBuilding(string buildingId)
        {
            ServiceLocator.Get<IBuildingService>().StartUpgrade(buildingId);
            SetMessage($"Started upgrade attempt: {buildingId}");
            Refresh();
        }

        private void StartResearch(string researchId)
        {
            ServiceLocator.Get<IResearchService>().StartResearch(researchId);
            SetMessage($"Started research attempt: {researchId}");
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
            SetMessage(report.IsWinner ? "Victory report generated." : "Defeat report generated.");
        }

        private void TrainTroops(TroopType type)
        {
            ServiceLocator.Get<ITrainingService>().StartTraining(type, 25);
            SetMessage($"Training request: 25 {type}");
            Refresh();
        }

        private void EarnWarzoneCredits()
        {
            ServiceLocator.Get<IWarzoneCreditService>().AddCredits(250);
            SetMessage("Earned 250 Warzone Credits from a test objective.");
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
            SetMessage($"Captured Neutral Borderlands for {realm}.");
            Refresh();
        }

        private void PickTestGem()
        {
            var gemService = ServiceLocator.Get<IRealmGemService>();
            bool pickedUp = gemService.PickUpGem("Stonehold_Gem_1", "offline_player");
            SetMessage(pickedUp ? "Picked up Stonehold Gem 1 as offline_player." : "Could not pick up the gem yet.");
            Refresh();
        }

        private void EarnWishgate()
        {
            ServiceLocator.Get<IRealmGemService>().MarkWishgateEarned("Offline realm objective test");
            SetMessage("Wishgate earned for offline testing.");
            Refresh();
        }

        private void ChooseWishReward()
        {
            ServiceLocator.Get<IRealmGemService>().ChooseWishReward("warmaster_credits");
            ServiceLocator.Get<IWarzoneCreditService>().AddCredits(300);
            SetMessage("Wish reward chosen: Warmaster Credits.");
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
                ? "Kingdom: No realm selected"
                : $"Kingdom: {realm.RealmName}";

            var resources = ServiceLocator.Get<IResourceService>();
            var selectedRealmId = ServiceLocator.Get<IRealmService>().CurrentRealmId;
            ResourceType rareResourceType = ResourceRules.GetRareResourceForRealm(selectedRealmId);
            _resourceText.text =
                $"Food {resources.GetResourceCount(ResourceType.Food)}    " +
                $"Wood {resources.GetResourceCount(ResourceType.Wood)}    " +
                $"Stone {resources.GetResourceCount(ResourceType.Stone)}    " +
                $"Gold {resources.GetResourceCount(ResourceType.Gold)}\n" +
                $"ManaStone {resources.GetResourceCount(ResourceType.ManaStone)}    " +
                $"Ore {resources.GetResourceCount(ResourceType.Ore)}    " +
                $"{rareResourceType} {resources.GetResourceCount(rareResourceType)}    " +
                $"Warzone {ServiceLocator.Get<IWarzoneCreditService>().GetCredits()}";

            var buildings = ServiceLocator.Get<IBuildingService>();
            var builder = new StringBuilder();
            builder.AppendLine("Buildings");
            foreach (var buildingId in _buildingIds)
            {
                BuildingState state = buildings.GetBuildingState(buildingId);
                string timer = state.IsUpgrading
                    ? $" upgrading, completes in {Math.Max(0, state.UpgradeCompleteTimestamp - DateTimeOffset.UtcNow.ToUnixTimeSeconds())}s"
                    : " ready";
                builder.AppendLine($"{buildingId}: Level {state.Level}, {timer}");
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
                "Troops\n" +
                $"Infantry: {training.GetTroopCount(TroopType.Infantry)}\n" +
                $"Cavalry: {training.GetTroopCount(TroopType.Cavalry)}\n" +
                $"Ranged: {training.GetTroopCount(TroopType.Ranged)}\n" +
                $"Siege: {training.GetTroopCount(TroopType.Siege)}\n" +
                $"Warmaster pieces: {warmaster.GetPurchasedPieceCount()}/{warmaster.GetRequiredPieceCount()}\n" +
                $"Warmaster set: {equippedWarmasterSet} ({warmasterRank})";

            var research = ServiceLocator.Get<IResearchService>();
            var steel = research.GetResearchState("Steel Forging");
            var armor = research.GetResearchState("Plate Armor");
            _researchText.text =
                "Research\n" +
                FormatResearch("Steel Forging", steel) + "\n" +
                FormatResearch("Plate Armor", armor) + "\n" +
                $"Attack bonus: {research.GetStatBonus(StatType.Attack):P0}\n" +
                $"Defense bonus: {research.GetStatBonus(StatType.Defense):P0}";

            var quests = new StringBuilder();
            quests.AppendLine("Quests");
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
            if (_messageText != null)
            {
                _messageText.text = message;
            }

            if (_boardHintText != null)
            {
                _boardHintText.text = message;
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

        private static Canvas CreateCanvas(string name)
        {
            var canvasObject = new GameObject(name);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
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

        private static Button CreateButton(Transform parent, Font font, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
        {
            var buttonObject = new GameObject(label);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.20f, 0.28f, 0.36f, 1f);

            var button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(action);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(240, 48);

            var text = CreateText(buttonObject.transform, label + "_Text", font, 20, TextAnchor.MiddleCenter, Vector2.zero, rect.sizeDelta);
            text.text = label;
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
