using System;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using System.Collections.Generic;
using UnityEngine;
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
        private float _completionTimer;

        private readonly string[] _buildingIds =
        {
            "TownHall", "Farm", "LumberMill", "Quarry", "GoldMine", "Barracks"
        };

        private void Start()
        {
            Bootloader.InitializeIfMissing();

            var save = ServiceLocator.Get<ISaveGameService>();
            if (save.CurrentSave == null)
            {
                save.Load();
            }

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

        private void BuildRuntimeUi()
        {
            var canvas = CreateCanvas("KingdomCanvas");
            var font = GetDefaultFont();

            var background = new GameObject("Kingdom_Backdrop");
            background.transform.SetParent(canvas.transform, false);
            var bg = background.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.10f, 0.12f, 1f);
            Stretch(background.GetComponent<RectTransform>());

            _realmText = CreateText(canvas.transform, "RealmText", font, 30, TextAnchor.UpperLeft, new Vector2(40, -30), new Vector2(900, 90));
            _resourceText = CreateText(canvas.transform, "ResourceText", font, 24, TextAnchor.UpperLeft, new Vector2(40, -120), new Vector2(900, 130));
            _buildingText = CreateText(canvas.transform, "BuildingText", font, 22, TextAnchor.UpperLeft, new Vector2(40, -250), new Vector2(500, 300));
            _troopText = CreateText(canvas.transform, "TroopText", font, 22, TextAnchor.UpperLeft, new Vector2(40, -570), new Vector2(500, 180));
            _researchText = CreateText(canvas.transform, "ResearchText", font, 22, TextAnchor.UpperLeft, new Vector2(600, -250), new Vector2(520, 210));
            _questText = CreateText(canvas.transform, "QuestText", font, 20, TextAnchor.UpperLeft, new Vector2(600, -480), new Vector2(560, 200));
            _territoryText = CreateText(canvas.transform, "TerritoryText", font, 20, TextAnchor.UpperLeft, new Vector2(600, -690), new Vector2(560, 170));
            _battleText = CreateText(canvas.transform, "BattleText", font, 20, TextAnchor.UpperLeft, new Vector2(600, -870), new Vector2(560, 190));
            _messageText = CreateText(canvas.transform, "MessageText", font, 22, TextAnchor.LowerLeft, new Vector2(40, 40), new Vector2(900, 80));

            CreateButton(canvas.transform, font, "Upgrade Town Hall", new Vector2(-260, -80), () => UpgradeBuilding("TownHall"));
            CreateButton(canvas.transform, font, "Upgrade Farm", new Vector2(-260, -145), () => UpgradeBuilding("Farm"));
            CreateButton(canvas.transform, font, "Upgrade Lumber", new Vector2(-260, -210), () => UpgradeBuilding("LumberMill"));
            CreateButton(canvas.transform, font, "Upgrade Quarry", new Vector2(-260, -275), () => UpgradeBuilding("Quarry"));
            CreateButton(canvas.transform, font, "Upgrade Gold Mine", new Vector2(-260, -340), () => UpgradeBuilding("GoldMine"));
            CreateButton(canvas.transform, font, "Research Steel", new Vector2(-260, -420), () => StartResearch("Steel Forging"));
            CreateButton(canvas.transform, font, "Research Armor", new Vector2(-260, -485), () => StartResearch("Plate Armor"));
            CreateButton(canvas.transform, font, "Train Infantry", new Vector2(-260, -565), () => TrainTroops(TroopType.Infantry));
            CreateButton(canvas.transform, font, "Train Ranged", new Vector2(-260, -630), () => TrainTroops(TroopType.Ranged));
            CreateButton(canvas.transform, font, "Earn Warzone", new Vector2(-260, -695), EarnWarzoneCredits);
            CreateButton(canvas.transform, font, "Unlock Warmaster", new Vector2(-260, -760), UnlockWarmaster);
            CreateButton(canvas.transform, font, "Claim Quests", new Vector2(-260, -825), ClaimCompletedQuests);
            CreateButton(canvas.transform, font, "Capture Border", new Vector2(-260, -890), CaptureBorderlands);
            CreateButton(canvas.transform, font, "Test Battle", new Vector2(-260, -955), RunTestBattle);
            CreateButton(canvas.transform, font, "Champion Arena", new Vector2(-260, -1020), () => SceneManager.LoadScene(_arenaSceneName));
            CreateButton(canvas.transform, font, "Reset Save", new Vector2(-260, -1085), ResetSave);
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
            var request = new BattleRequest
            {
                Type = BattleType.PvE,
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
                $"Attacker losses: {FormatLosses(report.AttackerLosses)}\n" +
                $"Defender losses: {FormatLosses(report.DefenderLosses)}";
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
            var credits = ServiceLocator.Get<IWarzoneCreditService>();
            if (!credits.SpendCredits(100))
            {
                SetMessage("Need 100 Warzone Credits to unlock the prototype Warmaster set.");
                return;
            }

            var warmaster = ServiceLocator.Get<IWarmasterService>();
            warmaster.UnlockSet("prototype_true_warmaster");
            warmaster.EquipSet("prototype_true_warmaster");
            SetMessage("Prototype True Warmaster set unlocked.");
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
            var realm = ServiceLocator.Get<IRealmService>().CurrentRealm;
            _realmText.text = realm == null
                ? "Kingdom: No realm selected"
                : $"Kingdom: {realm.RealmName}";

            var resources = ServiceLocator.Get<IResourceService>();
            _resourceText.text =
                $"Food {resources.GetResourceCount(ResourceType.Food)}    " +
                $"Wood {resources.GetResourceCount(ResourceType.Wood)}    " +
                $"Stone {resources.GetResourceCount(ResourceType.Stone)}    " +
                $"Gold {resources.GetResourceCount(ResourceType.Gold)}    " +
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
            _troopText.text =
                "Troops\n" +
                $"Infantry: {training.GetTroopCount(TroopType.Infantry)}\n" +
                $"Cavalry: {training.GetTroopCount(TroopType.Cavalry)}\n" +
                $"Ranged: {training.GetTroopCount(TroopType.Ranged)}\n" +
                $"Siege: {training.GetTroopCount(TroopType.Siege)}\n" +
                $"Warmaster: {ServiceLocator.Get<IWarmasterService>().GetState()?.EquippedSetId ?? "none"}";

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
            _territoryText.text = territories.ToString();
        }

        private void SetMessage(string message)
        {
            _messageText.text = message;
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

        private static void CreateButton(Transform parent, Font font, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
