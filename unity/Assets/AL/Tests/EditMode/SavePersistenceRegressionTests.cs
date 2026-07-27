using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AL.Data.Catalogs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode
{
    public class SavePersistenceRegressionTests
    {
        private const string CurrentSaveFormatId = "anotherlife.local-save";

        [Test]
        public void ExplicitNewProfileStampsAndReloadsCurrentSaveMetadata()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                object service = CreateSaveService(root);
                Type realmType = GetRuntimeType("AL.Core.RealmId");
                Invoke(service, "CreateNewSave", Enum.Parse(realmType, "Eldergrove"));

                object currentSave = GetProperty(service, "CurrentSave");
                Assert.AreEqual(CurrentSaveFormatId, GetField(currentSave, "SaveFormatId"));
                Assert.AreEqual(1, GetField(currentSave, "SaveSchemaVersion"));
                Assert.AreEqual(1, GetField(currentSave, "ProfileInitializationVersion"));

                string primaryPath = Path.Combine(root, "save.json");
                SaveSemanticCandidate candidate = SaveSemanticCandidateValidator.Validate(
                    File.ReadAllBytes(primaryPath),
                    SaveCandidateSourceGeneration.Primary,
                    CreateSemanticPolicy());
                Assert.AreEqual(SaveSemanticCandidateOutcome.Valid, candidate.Outcome);
                Assert.True(candidate.IsWritable);

                object reloadedService = CreateSaveService(root);
                Invoke(reloadedService, "Load");
                object reloadedSave = GetProperty(reloadedService, "CurrentSave");
                Assert.AreEqual(CurrentSaveFormatId, GetField(reloadedSave, "SaveFormatId"));
                Assert.AreEqual(1, GetField(reloadedSave, "SaveSchemaVersion"));
                Assert.AreEqual(1, GetField(reloadedSave, "ProfileInitializationVersion"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void LegacyProfileCannotBeRewrittenBeforeExplicitMigration()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                string primaryPath = Path.Combine(root, "save.json");
                long futureTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60;
                string historicalJson =
                    "{" +
                    "\"SelectedRealm\":1," +
                    "\"Resources\":[{\"Type\":0,\"Amount\":1000}]," +
                    "\"Buildings\":[],\"Troops\":[],\"Researches\":[]," +
                    "\"Quests\":[],\"Territories\":[],\"RealmGems\":[]," +
                    "\"Wishgate\":{},\"CurrentChapterId\":\"C1\"," +
                    "\"Warmaster\":{},\"ChampionCustomization\":{}," +
                    "\"WarzoneCredits\":0,\"LastSavedTimestamp\":" +
                    futureTimestamp + "}";
                File.WriteAllText(primaryPath, historicalJson);
                byte[] originalBytes = File.ReadAllBytes(primaryPath);

                object service = CreateSaveService(root);
                Invoke(service, "Load");
                Assert.Null(GetProperty(service, "CurrentSave"));
                object currentSave = GetProperty(service, "ReadOnlyCandidateSnapshot");
                Assert.That((string)GetField(currentSave, "SaveFormatId"), Is.Null.Or.Empty);
                Assert.AreEqual(0, GetField(currentSave, "SaveSchemaVersion"));
                Assert.AreEqual(0, GetField(currentSave, "ProfileInitializationVersion"));

                SetField(currentSave, "CurrentChapterId", "C2_MUST_NOT_PERSIST");
                LogAssert.Expect(
                    LogType.Error,
                    new System.Text.RegularExpressions.Regex(
                        "AL-SAVE-READ-ONLY-DISPOSITION"));
                Invoke(service, "Save");

                Assert.AreEqual(
                    "SaveFailedPreviousPreserved",
                    GetProperty(service, "LastSaveStatus").ToString());
                Assert.That(
                    (string)GetProperty(service, "LastSaveMessage"),
                    Does.Contain("AL-SAVE-READ-ONLY-DISPOSITION"));
                CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(primaryPath));
                Assert.False(File.Exists(Path.Combine(root, "save.tmp.json")));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void EnsureSaveDefaultsInitializesNarrativeCompatibilityFields()
        {
            Type saveType = GetRuntimeType("AL.Data.Runtime.SaveGameData");
            object save = Activator.CreateInstance(saveType);

            SetField(save, "Reputation", null);
            SetField(save, "FactionReputations", null);
            SetField(save, "LordPersona", null);

            InvokeEnsureSaveDefaults(save);

            IList reputation = (IList)GetField(save, "Reputation");
            IList factions = (IList)GetField(save, "FactionReputations");
            object persona = GetField(save, "LordPersona");
            Assert.IsEmpty(reputation);
            Assert.IsEmpty(factions);
            Assert.NotNull(persona);
            Assert.AreEqual(0, GetField(persona, "Warlord"));
            Assert.AreEqual(0, GetField(persona, "Diplomat"));
            Assert.AreEqual(0, GetField(persona, "Sage"));
            Assert.AreEqual(0, GetField(persona, "Rogue"));
        }

        [Test]
        public void HistoricalJsonWithoutNarrativeCompatibilityFieldsNormalizesWithoutLosingProgress()
        {
            Type saveType = GetRuntimeType("AL.Data.Runtime.SaveGameData");
            const string historicalJson =
                "{\n" +
                "  \"SelectedRealm\": 2,\n" +
                "  \"Resources\": [{ \"Type\": 3, \"Amount\": 4321 }],\n" +
                "  \"Buildings\": [{ \"BuildingId\": \"BLD_LEGACY_KEEP\", \"Level\": 7, \"IsUpgrading\": true, \"UpgradeCompleteTimestamp\": 1700000001 }],\n" +
                "  \"Troops\": [],\n" +
                "  \"Researches\": [],\n" +
                "  \"Quests\": [{ \"QuestId\": \"Q_LEGACY_PATH\", \"CurrentValue\": 4, \"IsCompleted\": false, \"IsClaimed\": false }],\n" +
                "  \"Territories\": [],\n" +
                "  \"RealmGems\": [],\n" +
                "  \"Wishgate\": {},\n" +
                "  \"CurrentChapterId\": \"C7_LEGACY\",\n" +
                "  \"Warmaster\": {},\n" +
                "  \"ChampionCustomization\": {},\n" +
                "  \"OwnedEquipment\": [{ \"EquipmentId\": \"EQ_LEGACY_BLADE\", \"DisplayName\": \"Legacy Blade\", \"Slot\": 3, \"AttackBonus\": 19, \"Quantity\": 2, \"SourceBossId\": \"BOSS_ARCHIVE\", \"FirstAcquiredTimestamp\": 1699999900, \"LastAcquiredTimestamp\": 1700000000 }],\n" +
                "  \"LastSavedTimestamp\": 1700000123\n" +
                "}";

            Assert.That(historicalJson, Does.Not.Contain("\"Reputation\""));
            Assert.That(historicalJson, Does.Not.Contain("\"FactionReputations\""));
            Assert.That(historicalJson, Does.Not.Contain("\"LordPersona\""));

            object save = JsonUtility.FromJson(historicalJson, saveType);

            Assert.NotNull(save);
            Assert.IsNull(GetField(save, "SaveFormatId"));
            Assert.AreEqual(0, GetField(save, "SaveSchemaVersion"));
            Assert.AreEqual(0, GetField(save, "ProfileInitializationVersion"));
            IList deserializedReputation = (IList)GetField(save, "Reputation");
            IList deserializedFactions = (IList)GetField(save, "FactionReputations");
            object deserializedPersona = GetField(save, "LordPersona");
            Assert.IsEmpty(deserializedReputation);
            Assert.IsEmpty(deserializedFactions);
            Assert.NotNull(deserializedPersona);
            Assert.AreEqual(0, GetField(deserializedPersona, "Warlord"));
            Assert.AreEqual(0, GetField(deserializedPersona, "Diplomat"));
            Assert.AreEqual(0, GetField(deserializedPersona, "Sage"));
            Assert.AreEqual(0, GetField(deserializedPersona, "Rogue"));

            InvokeEnsureSaveDefaults(save);

            Assert.IsNull(GetField(save, "SaveFormatId"));
            Assert.AreEqual(0, GetField(save, "SaveSchemaVersion"));
            Assert.AreEqual(0, GetField(save, "ProfileInitializationVersion"));
            IList reputation = (IList)GetField(save, "Reputation");
            IList factions = (IList)GetField(save, "FactionReputations");
            object persona = GetField(save, "LordPersona");
            IList resources = (IList)GetField(save, "Resources");
            IList buildings = (IList)GetField(save, "Buildings");
            IList quests = (IList)GetField(save, "Quests");
            IList ownedEquipment = (IList)GetField(save, "OwnedEquipment");
            IList appliedBossLootRewards = (IList)GetField(save, "AppliedBossLootRewards");

            Assert.AreSame(deserializedReputation, reputation);
            Assert.AreSame(deserializedFactions, factions);
            Assert.AreSame(deserializedPersona, persona);
            Assert.IsEmpty(reputation);
            Assert.IsEmpty(factions);
            Assert.AreEqual(0, GetField(persona, "Warlord"));
            Assert.AreEqual(0, GetField(persona, "Diplomat"));
            Assert.AreEqual(0, GetField(persona, "Sage"));
            Assert.AreEqual(0, GetField(persona, "Rogue"));

            Assert.AreEqual("Eldergrove", GetField(save, "SelectedRealm").ToString());
            Assert.AreEqual("C7_LEGACY", GetField(save, "CurrentChapterId"));
            Assert.AreEqual(1700000123L, GetField(save, "LastSavedTimestamp"));
            Assert.AreEqual("Gold", GetField(resources[0], "Type").ToString());
            Assert.AreEqual(4321L, GetField(resources[0], "Amount"));
            Assert.AreEqual("BLD_LEGACY_KEEP", GetField(buildings[0], "BuildingId"));
            Assert.AreEqual(7, GetField(buildings[0], "Level"));
            Assert.True((bool)GetField(buildings[0], "IsUpgrading"));
            Assert.AreEqual(1700000001L, GetField(buildings[0], "UpgradeCompleteTimestamp"));
            Assert.AreEqual("Q_LEGACY_PATH", GetField(quests[0], "QuestId"));
            Assert.AreEqual(4, GetField(quests[0], "CurrentValue"));
            Assert.False((bool)GetField(quests[0], "IsCompleted"));
            Assert.False((bool)GetField(quests[0], "IsClaimed"));
            Assert.AreEqual("EQ_LEGACY_BLADE", GetField(ownedEquipment[0], "EquipmentId"));
            Assert.AreEqual("Legacy Blade", GetField(ownedEquipment[0], "DisplayName"));
            Assert.AreEqual("MainHand", GetField(ownedEquipment[0], "Slot").ToString());
            Assert.AreEqual(19, GetField(ownedEquipment[0], "AttackBonus"));
            Assert.AreEqual(2, GetField(ownedEquipment[0], "Quantity"));
            Assert.AreEqual("BOSS_ARCHIVE", GetField(ownedEquipment[0], "SourceBossId"));
            Assert.IsEmpty(appliedBossLootRewards);

            object resource = resources[0];
            object building = buildings[0];
            object quest = quests[0];
            object equipment = ownedEquipment[0];

            InvokeEnsureSaveDefaults(save);

            Assert.AreSame(reputation, GetField(save, "Reputation"));
            Assert.AreSame(factions, GetField(save, "FactionReputations"));
            Assert.AreSame(persona, GetField(save, "LordPersona"));
            Assert.AreSame(resources, GetField(save, "Resources"));
            Assert.AreSame(buildings, GetField(save, "Buildings"));
            Assert.AreSame(quests, GetField(save, "Quests"));
            Assert.AreSame(ownedEquipment, GetField(save, "OwnedEquipment"));
            Assert.AreSame(appliedBossLootRewards, GetField(save, "AppliedBossLootRewards"));
            Assert.AreSame(resource, ((IList)GetField(save, "Resources"))[0]);
            Assert.AreSame(building, ((IList)GetField(save, "Buildings"))[0]);
            Assert.AreSame(quest, ((IList)GetField(save, "Quests"))[0]);
            Assert.AreSame(equipment, ((IList)GetField(save, "OwnedEquipment"))[0]);
            Assert.AreEqual(4321L, GetField(resource, "Amount"));
            Assert.AreEqual(7, GetField(building, "Level"));
            Assert.AreEqual(4, GetField(quest, "CurrentValue"));
            Assert.AreEqual(2, GetField(equipment, "Quantity"));
        }

        [Test]
        public void AppliedBossLootLedgerRoundTripsStableApplicationIdentity()
        {
            Type saveType = GetRuntimeType("AL.Data.Runtime.SaveGameData");
            Type rewardType = GetRuntimeType("AL.Data.Runtime.AppliedBossLootRewardState");
            object save = Activator.CreateInstance(saveType);
            object reward = Activator.CreateInstance(rewardType);
            SetField(reward, "EncounterId", "ENCOUNTER_001");
            SetField(reward, "RewardResultId", "REWARD_001");
            SetField(reward, "BossId", "BOSS_001");
            SetField(reward, "RewardDigest", "sha256:0123456789abcdef");
            SetField(reward, "CommittedTimestamp", 1800000200L);
            IList rewards = CreateRuntimeList(rewardType);
            rewards.Add(reward);
            SetField(save, "AppliedBossLootRewards", rewards);

            InvokeEnsureSaveDefaults(save);
            string json = JsonUtility.ToJson(save);
            object roundTripped = JsonUtility.FromJson(json, saveType);
            InvokeEnsureSaveDefaults(roundTripped);

            Assert.True(InvokeValidateSaveSemantics(roundTripped, out string error), error);
            IList reloaded = (IList)GetField(roundTripped, "AppliedBossLootRewards");
            Assert.AreEqual(1, reloaded.Count);
            Assert.AreEqual("ENCOUNTER_001", GetField(reloaded[0], "EncounterId"));
            Assert.AreEqual("REWARD_001", GetField(reloaded[0], "RewardResultId"));
            Assert.AreEqual("BOSS_001", GetField(reloaded[0], "BossId"));
            Assert.AreEqual("sha256:0123456789abcdef", GetField(reloaded[0], "RewardDigest"));
            Assert.AreEqual(1800000200L, GetField(reloaded[0], "CommittedTimestamp"));
        }

        [TestCase(null, "REWARD_001", "BOSS_001", "sha256:01", 1L)]
        [TestCase("ENCOUNTER_001", "", "BOSS_001", "sha256:01", 1L)]
        [TestCase("ENCOUNTER_001", "REWARD_001", " ", "sha256:01", 1L)]
        [TestCase("ENCOUNTER_001", "REWARD_001", "BOSS_001", "", 1L)]
        [TestCase("ENCOUNTER_001", "REWARD_001", "BOSS_001", "sha256:01", 0L)]
        public void AppliedBossLootLedgerRejectsIncompleteEntries(
            string encounterId,
            string rewardResultId,
            string bossId,
            string rewardDigest,
            long committedTimestamp)
        {
            object save = CreateSaveWithAppliedBossLootReward(
                encounterId,
                rewardResultId,
                bossId,
                rewardDigest,
                committedTimestamp);

            Assert.False(InvokeValidateSaveSemantics(save, out string error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void AppliedBossLootLedgerRejectsDuplicateAndConflictingIdentities()
        {
            Type rewardType = GetRuntimeType("AL.Data.Runtime.AppliedBossLootRewardState");
            object save = CreateSaveWithAppliedBossLootReward(
                "ENCOUNTER_001",
                "REWARD_001",
                "BOSS_001",
                "sha256:01",
                1L);
            IList rewards = (IList)GetField(save, "AppliedBossLootRewards");

            object duplicate = Activator.CreateInstance(rewardType);
            SetField(duplicate, "EncounterId", "ENCOUNTER_001");
            SetField(duplicate, "RewardResultId", "REWARD_002");
            SetField(duplicate, "BossId", "BOSS_001");
            SetField(duplicate, "RewardDigest", "sha256:02");
            SetField(duplicate, "CommittedTimestamp", 2L);
            rewards.Add(duplicate);

            Assert.False(InvokeValidateSaveSemantics(save, out string error));
            Assert.That(error, Does.Contain("conflicting"));
        }

        [Test]
        public void AppliedBossLootLedgerRejectsNullEntry()
        {
            Type saveType = GetRuntimeType("AL.Data.Runtime.SaveGameData");
            Type rewardType = GetRuntimeType("AL.Data.Runtime.AppliedBossLootRewardState");
            object save = Activator.CreateInstance(saveType);
            IList rewards = CreateRuntimeList(rewardType);
            rewards.Add(null);
            SetField(save, "AppliedBossLootRewards", rewards);
            InvokeEnsureSaveDefaults(save);

            Assert.False(InvokeValidateSaveSemantics(save, out string error));
            Assert.That(error, Does.Contain("null entry"));
        }

        private static SaveSemanticValidationPolicy CreateSemanticPolicy()
        {
            var authority = new SaveSemanticValidationAuthority(
                new[] { 0, 1, 2, 3, 4 },
                new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
                new[] { 0, 1, 2, 3 },
                new[] { 0, 1, 2, 3, 4, 5 },
                new[] { 0, 1, 2, 3 },
                new[] { 0, 1, 2, 3, 4, 5 },
                Array.Empty<SaveSemanticQuestRule>(),
                new[]
                {
                    new SaveSemanticStableIdRule(SaveSemanticStableIdKind.Chapter, "C1"),
                    new SaveSemanticStableIdRule(SaveSemanticStableIdKind.BodyPreset, "average"),
                    new SaveSemanticStableIdRule(SaveSemanticStableIdKind.HairStyle, "short"),
                    new SaveSemanticStableIdRule(SaveSemanticStableIdKind.ArmorStyle, "realm_basic"),
                    new SaveSemanticStableIdRule(SaveSemanticStableIdKind.FaceMark, "none"),
                    new SaveSemanticStableIdRule(SaveSemanticStableIdKind.WeaponStyle, "sword"),
                    new SaveSemanticStableIdRule(SaveSemanticStableIdKind.OffhandStyle, "shield")
                });
            return new SaveSemanticValidationPolicy(
                CurrentSaveFormatId,
                1,
                1,
                authority,
                maximumInputBytes: 1024 * 1024);
        }

        [Test]
        public void RepeatedEnsureSaveDefaultsPreservesExistingNarrativeCompatibilityData()
        {
            Type saveType = GetRuntimeType("AL.Data.Runtime.SaveGameData");
            Type affinityType = GetRuntimeType("AL.Data.Runtime.NpcAffinityData");
            Type factionType = GetRuntimeType("AL.Data.Runtime.FactionRepData");
            Type personaType = GetRuntimeType("AL.Data.Runtime.PersonaData");
            object save = Activator.CreateInstance(saveType);

            object affinity = Activator.CreateInstance(affinityType);
            SetField(affinity, "NpcId", "ADVISOR_VALERIUS");
            SetField(affinity, "Affinity", 17.5f);
            IList reputation = CreateRuntimeList(affinityType);
            reputation.Add(affinity);

            object faction = Activator.CreateInstance(factionType);
            SetField(faction, "FactionId", "FACTION_VEIL_WATCH");
            SetField(faction, "Reputation", 9);
            IList factions = CreateRuntimeList(factionType);
            factions.Add(faction);

            object persona = Activator.CreateInstance(personaType);
            SetField(persona, "Warlord", 3);
            SetField(persona, "Diplomat", 7);
            SetField(persona, "Sage", 11);
            SetField(persona, "Rogue", 2);

            SetField(save, "Reputation", reputation);
            SetField(save, "FactionReputations", factions);
            SetField(save, "LordPersona", persona);

            InvokeEnsureSaveDefaults(save);
            InvokeEnsureSaveDefaults(save);

            Assert.AreSame(reputation, GetField(save, "Reputation"));
            Assert.AreSame(factions, GetField(save, "FactionReputations"));
            Assert.AreSame(persona, GetField(save, "LordPersona"));
            Assert.AreEqual("ADVISOR_VALERIUS", GetField(reputation[0], "NpcId"));
            Assert.AreEqual(17.5f, GetField(reputation[0], "Affinity"));
            Assert.AreEqual("FACTION_VEIL_WATCH", GetField(factions[0], "FactionId"));
            Assert.AreEqual(9, GetField(factions[0], "Reputation"));
            Assert.AreEqual(7, GetField(persona, "Diplomat"));
            Assert.AreEqual(11, GetField(persona, "Sage"));
        }

        [Test]
        public void NarrativeCompatibilityAndUnrelatedProgressSurviveJsonRoundTripAndNormalization()
        {
            Type saveType = GetRuntimeType("AL.Data.Runtime.SaveGameData");
            Type affinityType = GetRuntimeType("AL.Data.Runtime.NpcAffinityData");
            Type factionType = GetRuntimeType("AL.Data.Runtime.FactionRepData");
            Type personaType = GetRuntimeType("AL.Data.Runtime.PersonaData");
            Type resourceType = GetRuntimeType("AL.Data.Runtime.ResourceData");
            Type buildingType = GetRuntimeType("AL.Data.Runtime.BuildingState");
            Type questType = GetRuntimeType("AL.Core.Interfaces.QuestState");
            Type equipmentType = GetRuntimeType("AL.Data.Runtime.OwnedEquipmentState");
            object save = Activator.CreateInstance(saveType);

            object affinity = Activator.CreateInstance(affinityType);
            SetField(affinity, "NpcId", "NPC_ROUND_TRIP");
            SetField(affinity, "Affinity", 23.75f);
            IList reputation = CreateRuntimeList(affinityType);
            reputation.Add(affinity);

            object faction = Activator.CreateInstance(factionType);
            SetField(faction, "FactionId", "FACTION_ROUND_TRIP");
            SetField(faction, "Reputation", -8);
            IList factions = CreateRuntimeList(factionType);
            factions.Add(faction);

            object persona = Activator.CreateInstance(personaType);
            SetField(persona, "Warlord", 5);
            SetField(persona, "Diplomat", 9);
            SetField(persona, "Sage", 13);
            SetField(persona, "Rogue", 4);

            object resource = Activator.CreateInstance(resourceType);
            SetField(resource, "Type", Enum.Parse(GetRuntimeType("AL.Core.ResourceType"), "DarkCrystal"));
            SetField(resource, "Amount", 87L);
            IList resources = CreateRuntimeList(resourceType);
            resources.Add(resource);

            object building = Activator.CreateInstance(buildingType);
            SetField(building, "BuildingId", "BLD_ROUND_TRIP");
            SetField(building, "Level", 6);
            SetField(building, "UpgradeCompleteTimestamp", 1800000001L);
            IList buildings = CreateRuntimeList(buildingType);
            buildings.Add(building);

            object quest = Activator.CreateInstance(questType);
            SetField(quest, "QuestId", "Q_ROUND_TRIP");
            SetField(quest, "CurrentValue", 12);
            SetField(quest, "IsCompleted", true);
            SetField(quest, "IsClaimed", false);
            IList quests = CreateRuntimeList(questType);
            quests.Add(quest);

            object equipment = Activator.CreateInstance(equipmentType);
            SetField(equipment, "EquipmentId", "EQ_ROUND_TRIP");
            SetField(equipment, "DisplayName", "Round Trip Sigil");
            SetField(equipment, "Slot", Enum.Parse(GetRuntimeType("AL.Core.EquipmentSlot"), "Trinket"));
            SetField(equipment, "HealthBonus", 31);
            SetField(equipment, "Quantity", 3);
            SetField(equipment, "SourceBossId", "BOSS_ROUND_TRIP");
            IList ownedEquipment = CreateRuntimeList(equipmentType);
            ownedEquipment.Add(equipment);

            SetField(save, "SelectedRealm", Enum.Parse(GetRuntimeType("AL.Core.RealmId"), "Umbral"));
            SetField(save, "CurrentChapterId", "C9_ROUND_TRIP");
            SetField(save, "LastSavedTimestamp", 1800000123L);
            SetField(save, "Reputation", reputation);
            SetField(save, "FactionReputations", factions);
            SetField(save, "LordPersona", persona);
            SetField(save, "Resources", resources);
            SetField(save, "Buildings", buildings);
            SetField(save, "Quests", quests);
            SetField(save, "OwnedEquipment", ownedEquipment);

            InvokeEnsureSaveDefaults(save);
            string json = JsonUtility.ToJson(save);
            object roundTripped = JsonUtility.FromJson(json, saveType);
            InvokeEnsureSaveDefaults(roundTripped);
            string normalizedRoundTripJson = JsonUtility.ToJson(roundTripped);

            Assert.AreEqual(json, normalizedRoundTripJson);
            IList reloadedReputation = (IList)GetField(roundTripped, "Reputation");
            IList reloadedFactions = (IList)GetField(roundTripped, "FactionReputations");
            object reloadedPersona = GetField(roundTripped, "LordPersona");
            IList reloadedResources = (IList)GetField(roundTripped, "Resources");
            IList reloadedBuildings = (IList)GetField(roundTripped, "Buildings");
            IList reloadedQuests = (IList)GetField(roundTripped, "Quests");
            IList reloadedEquipment = (IList)GetField(roundTripped, "OwnedEquipment");

            Assert.AreEqual("NPC_ROUND_TRIP", GetField(reloadedReputation[0], "NpcId"));
            Assert.AreEqual(23.75f, GetField(reloadedReputation[0], "Affinity"));
            Assert.AreEqual("FACTION_ROUND_TRIP", GetField(reloadedFactions[0], "FactionId"));
            Assert.AreEqual(-8, GetField(reloadedFactions[0], "Reputation"));
            Assert.AreEqual(5, GetField(reloadedPersona, "Warlord"));
            Assert.AreEqual(9, GetField(reloadedPersona, "Diplomat"));
            Assert.AreEqual(13, GetField(reloadedPersona, "Sage"));
            Assert.AreEqual(4, GetField(reloadedPersona, "Rogue"));

            Assert.AreEqual("Umbral", GetField(roundTripped, "SelectedRealm").ToString());
            Assert.AreEqual("C9_ROUND_TRIP", GetField(roundTripped, "CurrentChapterId"));
            Assert.AreEqual(1800000123L, GetField(roundTripped, "LastSavedTimestamp"));
            Assert.AreEqual("DarkCrystal", GetField(reloadedResources[0], "Type").ToString());
            Assert.AreEqual(87L, GetField(reloadedResources[0], "Amount"));
            Assert.AreEqual("BLD_ROUND_TRIP", GetField(reloadedBuildings[0], "BuildingId"));
            Assert.AreEqual(6, GetField(reloadedBuildings[0], "Level"));
            Assert.AreEqual(1800000001L, GetField(reloadedBuildings[0], "UpgradeCompleteTimestamp"));
            Assert.AreEqual("Q_ROUND_TRIP", GetField(reloadedQuests[0], "QuestId"));
            Assert.AreEqual(12, GetField(reloadedQuests[0], "CurrentValue"));
            Assert.True((bool)GetField(reloadedQuests[0], "IsCompleted"));
            Assert.False((bool)GetField(reloadedQuests[0], "IsClaimed"));
            Assert.AreEqual("EQ_ROUND_TRIP", GetField(reloadedEquipment[0], "EquipmentId"));
            Assert.AreEqual("Round Trip Sigil", GetField(reloadedEquipment[0], "DisplayName"));
            Assert.AreEqual("Trinket", GetField(reloadedEquipment[0], "Slot").ToString());
            Assert.AreEqual(31, GetField(reloadedEquipment[0], "HealthBonus"));
            Assert.AreEqual(3, GetField(reloadedEquipment[0], "Quantity"));
            Assert.AreEqual("BOSS_ROUND_TRIP", GetField(reloadedEquipment[0], "SourceBossId"));
        }

        [Test]
        public void NarrativeCompatibilityFieldsCanMutateAndReloadAfterNormalization()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                object service = CreateSaveService(root);
                Type realmType = GetRuntimeType("AL.Core.RealmId");
                object noRealm = Enum.Parse(realmType, "None");
                Invoke(service, "CreateNewSave", noRealm);

                object currentSave = GetProperty(service, "CurrentSave");
                SetField(currentSave, "Reputation", null);
                SetField(currentSave, "FactionReputations", null);
                SetField(currentSave, "LordPersona", null);
                InvokeEnsureSaveDefaults(currentSave);

                object reputationService = CreateRuntimeService(
                    "AL.Services.Local.ReputationService",
                    "AL.Core.Interfaces.ISaveGameService",
                    service);
                object factionService = CreateRuntimeService(
                    "AL.Services.Local.FactionService",
                    "AL.Core.Interfaces.ISaveGameService",
                    service);
                object personaService = CreateRuntimeService(
                    "AL.Services.Local.PersonaService",
                    "AL.Core.Interfaces.ISaveGameService",
                    service);

                Type personaTraitType = GetRuntimeType("AL.Core.Interfaces.PersonaTrait");
                object diplomat = Enum.Parse(personaTraitType, "Diplomat");

                Invoke(reputationService, "ChangeAffinity", "ADVISOR_VALERIUS", 5.5f);
                Invoke(factionService, "AdjustReputation", "FACTION_VEIL_WATCH", 12);
                Invoke(personaService, "AdjustTrait", diplomat, 3);

                object reloadedService = CreateSaveService(root);
                Invoke(reloadedService, "Load");
                Assert.AreEqual(
                    "LoadedPrimary",
                    GetProperty(reloadedService, "LastLoadStatus").ToString());
                Assert.True(
                    (bool)GetProperty(
                        GetProperty(reloadedService, "LastLoadDisposition"),
                        "IsWritable"));
                object reloadedReputation = CreateRuntimeService(
                    "AL.Services.Local.ReputationService",
                    "AL.Core.Interfaces.ISaveGameService",
                    reloadedService);
                object reloadedFaction = CreateRuntimeService(
                    "AL.Services.Local.FactionService",
                    "AL.Core.Interfaces.ISaveGameService",
                    reloadedService);
                object reloadedPersona = CreateRuntimeService(
                    "AL.Services.Local.PersonaService",
                    "AL.Core.Interfaces.ISaveGameService",
                    reloadedService);

                Assert.AreEqual(5.5f, Invoke(reloadedReputation, "GetAffinity", "ADVISOR_VALERIUS"));
                Assert.AreEqual(12, Invoke(reloadedFaction, "GetReputation", "FACTION_VEIL_WATCH"));
                Assert.AreEqual(3, Invoke(reloadedPersona, "GetTraitValue", diplomat));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void CorruptedPrimaryAndValidBackupRecoverThroughExactQuarantine()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                object service = CreateSaveService(root);
                Type realmType = GetRuntimeType("AL.Core.RealmId");
                object noRealm = Enum.Parse(realmType, "None");
                Invoke(service, "CreateNewSave", noRealm);

                object currentSave = GetProperty(service, "CurrentSave");
                SetField(currentSave, "CurrentChapterId", "C1_BACKUP");
                Invoke(service, "Save");

                SetField(currentSave, "CurrentChapterId", "C1_PRIMARY");
                Invoke(service, "Save");

                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                string tempPath = Path.Combine(root, "save.tmp.json");
                Assert.True(File.Exists(primaryPath));
                Assert.True(File.Exists(backupPath));

                File.WriteAllText(primaryPath, "{ this is not valid json");
                byte[] primaryBefore = File.ReadAllBytes(primaryPath);
                byte[] backupBefore = File.ReadAllBytes(backupPath);

                object recoveredService = CreateSaveService(root);
                Invoke(recoveredService, "Load");

                Assert.AreEqual("RecoveredFromBackup", GetProperty(recoveredService, "LastLoadStatus").ToString());
                Assert.That(
                    (string)GetProperty(recoveredService, "LastLoadMessage"),
                    Does.Contain("AL-SAVE-RECOVERED-INVALID-PRIMARY"));
                object recoveredSave = GetProperty(recoveredService, "CurrentSave");
                Assert.NotNull(recoveredSave);
                Assert.Null(GetProperty(recoveredService, "ReadOnlyCandidateSnapshot"));
                Assert.AreEqual("C1_BACKUP", GetField(recoveredSave, "CurrentChapterId"));
                Assert.True(File.Exists(primaryPath));
                Assert.True(File.Exists(backupPath));
                Assert.True(File.Exists(tempPath));
                CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(primaryPath));
                CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(backupPath));
                CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(tempPath));
                string[] quarantines = Directory.GetFiles(root, "save.json.corrupt-*");
                Assert.AreEqual(1, quarantines.Length);
                CollectionAssert.AreEqual(primaryBefore, File.ReadAllBytes(quarantines[0]));

                object disposition = GetProperty(recoveredService, "LastLoadDisposition");
                Assert.AreEqual("Backup", GetProperty(disposition, "SelectedSource").ToString());
                Assert.True((bool)GetProperty(disposition, "IsWritable"));
                Assert.True((bool)GetProperty(disposition, "IsRuntimeUsable"));
                Assert.False((bool)GetProperty(disposition, "OfflineProgressApplied"));
                Assert.True((bool)GetProperty(disposition, "DiskChanged"));
                Assert.True((bool)GetProperty(disposition, "RawEvidencePreserved"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void MissingPrimaryAndExactWritableBackupAreInstalledByteForByte()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                object service = CreateSaveService(root);
                Type realmType = GetRuntimeType("AL.Core.RealmId");
                object noRealm = Enum.Parse(realmType, "None");
                Invoke(service, "CreateNewSave", noRealm);

                object currentSave = GetProperty(service, "CurrentSave");
                SetField(currentSave, "CurrentChapterId", "C1_BACKUP_ONLY");
                Invoke(service, "Save");

                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                string tempPath = Path.Combine(root, "save.tmp.json");
                File.Copy(primaryPath, backupPath, true);
                File.Delete(primaryPath);
                byte[] backupBefore = File.ReadAllBytes(backupPath);

                object recoveredService = CreateSaveService(root);
                Invoke(recoveredService, "Load");

                Assert.AreEqual("RecoveredFromBackup", GetProperty(recoveredService, "LastLoadStatus").ToString());
                Assert.That(
                    (string)GetProperty(recoveredService, "LastLoadMessage"),
                    Does.Contain("AL-SAVE-RECOVERED-BACKUP"));
                object recoveredSave = GetProperty(recoveredService, "CurrentSave");
                Assert.NotNull(recoveredSave);
                Assert.Null(GetProperty(recoveredService, "ReadOnlyCandidateSnapshot"));
                Assert.AreEqual("C1_BACKUP_ONLY", GetField(recoveredSave, "CurrentChapterId"));
                Assert.True(File.Exists(primaryPath));
                Assert.True(File.Exists(backupPath));
                Assert.True(File.Exists(tempPath));
                CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(primaryPath));
                CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(backupPath));
                CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(tempPath));
                Assert.IsEmpty(Directory.GetFiles(root, "*.corrupt-*"));

                object disposition = GetProperty(recoveredService, "LastLoadDisposition");
                Assert.AreEqual("Backup", GetProperty(disposition, "SelectedSource").ToString());
                Assert.True((bool)GetProperty(disposition, "IsWritable"));
                Assert.True((bool)GetProperty(disposition, "IsRuntimeUsable"));
                Assert.False((bool)GetProperty(disposition, "OfflineProgressApplied"));
                Assert.True((bool)GetProperty(disposition, "DiskChanged"));
                Assert.True((bool)GetProperty(disposition, "RawEvidencePreserved"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void BothInvalidSaveFilesArePreservedForExplicitRecovery()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                File.WriteAllText(primaryPath, "{ invalid primary");
                File.WriteAllText(backupPath, "{ invalid backup");
                byte[] primaryBefore = File.ReadAllBytes(primaryPath);
                byte[] backupBefore = File.ReadAllBytes(backupPath);

                object service = CreateSaveService(root);
                Invoke(service, "Load");

                Assert.AreEqual("RecoveryRequired", GetProperty(service, "LastLoadStatus").ToString());
                Assert.That(
                    (string)GetProperty(service, "LastLoadMessage"),
                    Does.Contain("AL-SAVE-RECOVERY-REQUIRED"));
                Assert.Null(GetProperty(service, "CurrentSave"));
                Assert.True(File.Exists(primaryPath));
                Assert.True(File.Exists(backupPath));
                CollectionAssert.AreEqual(primaryBefore, File.ReadAllBytes(primaryPath));
                CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(backupPath));
                Assert.IsEmpty(Directory.GetFiles(root, "*.corrupt-*"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void ValidPrimaryRemainsActiveWhileStaleAuxiliaryArtifactsArePreserved()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                object service = CreateSaveService(root);
                Type realmType = GetRuntimeType("AL.Core.RealmId");
                object noRealm = Enum.Parse(realmType, "None");
                Invoke(service, "CreateNewSave", noRealm);

                object currentSave = GetProperty(service, "CurrentSave");
                SetField(currentSave, "CurrentChapterId", "C1_PRIMARY");
                Invoke(service, "Save");

                string tempPath = Path.Combine(root, "save.tmp.json");
                string previousPath = Path.Combine(root, "save.previous.json");
                File.WriteAllText(tempPath, "{ stale temp");
                File.WriteAllText(previousPath, "{ stale previous");
                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                byte[] primaryBefore = File.ReadAllBytes(primaryPath);
                byte[] backupBefore = File.ReadAllBytes(backupPath);
                byte[] tempBefore = File.ReadAllBytes(tempPath);
                byte[] previousBefore = File.ReadAllBytes(previousPath);

                object reloadedService = CreateSaveService(root);
                Invoke(reloadedService, "Load");

                Assert.AreEqual("LoadedPrimary", GetProperty(reloadedService, "LastLoadStatus").ToString());
                Assert.True(File.Exists(tempPath));
                Assert.True(File.Exists(previousPath));
                object reloadedSave = GetProperty(reloadedService, "CurrentSave");
                Assert.NotNull(reloadedSave);
                Assert.Null(GetProperty(reloadedService, "ReadOnlyCandidateSnapshot"));
                Assert.AreEqual("C1_PRIMARY", GetField(reloadedSave, "CurrentChapterId"));
                object disposition = GetProperty(reloadedService, "LastLoadDisposition");
                Assert.False((bool)GetProperty(disposition, "IsWritable"));
                Assert.True((bool)GetProperty(disposition, "IsRuntimeUsable"));
                LogAssert.Expect(
                    LogType.Error,
                    new System.Text.RegularExpressions.Regex(
                        "^AL-SAVE-READ-ONLY-DISPOSITION:"));
                Invoke(reloadedService, "Save");
                Assert.AreEqual(
                    "SaveFailedPreviousPreserved",
                    GetProperty(reloadedService, "LastSaveStatus").ToString());
                CollectionAssert.AreEqual(primaryBefore, File.ReadAllBytes(primaryPath));
                CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(backupPath));
                CollectionAssert.AreEqual(tempBefore, File.ReadAllBytes(tempPath));
                CollectionAssert.AreEqual(previousBefore, File.ReadAllBytes(previousPath));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void ValidPreviousFallbackIsPreservedForExplicitRecovery()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                object service = CreateSaveService(root);
                Type realmType = GetRuntimeType("AL.Core.RealmId");
                object noRealm = Enum.Parse(realmType, "None");
                Invoke(service, "CreateNewSave", noRealm);

                object currentSave = GetProperty(service, "CurrentSave");
                SetField(currentSave, "CurrentChapterId", "C1_PREVIOUS_SAFE");
                Invoke(service, "Save");

                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                string previousPath = Path.Combine(root, "save.previous.json");
                File.Copy(primaryPath, previousPath, true);
                File.WriteAllText(primaryPath, "{ corrupt primary");
                File.Delete(backupPath);
                byte[] primaryBefore = File.ReadAllBytes(primaryPath);
                byte[] previousBefore = File.ReadAllBytes(previousPath);

                object recoveredService = CreateSaveService(root);
                Invoke(recoveredService, "Load");

                Assert.AreEqual("RecoveryRequired", GetProperty(recoveredService, "LastLoadStatus").ToString());
                Assert.Null(GetProperty(recoveredService, "CurrentSave"));
                object recoveredSave = GetProperty(recoveredService, "ReadOnlyCandidateSnapshot");
                Assert.NotNull(recoveredSave);
                Assert.AreEqual("C1_PREVIOUS_SAFE", GetField(recoveredSave, "CurrentChapterId"));
                Assert.True(File.Exists(primaryPath));
                Assert.False(File.Exists(backupPath));
                Assert.True(File.Exists(previousPath));
                CollectionAssert.AreEqual(primaryBefore, File.ReadAllBytes(primaryPath));
                CollectionAssert.AreEqual(previousBefore, File.ReadAllBytes(previousPath));
                Assert.IsEmpty(Directory.GetFiles(root, "*.corrupt-*"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void HasSaveReportsPreviousFallbackCandidate()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                object service = CreateSaveService(root);
                Type realmType = GetRuntimeType("AL.Core.RealmId");
                object noRealm = Enum.Parse(realmType, "None");
                Invoke(service, "CreateNewSave", noRealm);

                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                string previousPath = Path.Combine(root, "save.previous.json");
                File.Copy(primaryPath, previousPath, true);
                File.Delete(primaryPath);
                File.Delete(backupPath);

                object fallbackOnlyService = CreateSaveService(root);

                Assert.True((bool)Invoke(fallbackOnlyService, "HasSave"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void InvalidPrimaryNeverRotatesIntoBackupDuringSave()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                object service = CreateSaveService(root);
                Type realmType = GetRuntimeType("AL.Core.RealmId");
                object noRealm = Enum.Parse(realmType, "None");
                Invoke(service, "CreateNewSave", noRealm);

                object currentSave = GetProperty(service, "CurrentSave");
                SetField(currentSave, "CurrentChapterId", "C1_BACKUP_SAFE");
                Invoke(service, "Save");

                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                File.Copy(primaryPath, backupPath, true);
                File.WriteAllText(primaryPath, "{ corrupt primary must not become backup");

                currentSave = GetProperty(service, "CurrentSave");
                SetField(currentSave, "CurrentChapterId", "C1_VALIDATED_CANDIDATE");
                Invoke(service, "Save");

                Assert.AreEqual("SavedPrimary", GetProperty(service, "LastSaveStatus").ToString());
                Assert.That(File.ReadAllText(backupPath), Does.Not.Contain("corrupt primary"));
                Assert.That(File.ReadAllText(primaryPath), Does.Contain("C1_VALIDATED_CANDIDATE"));
                Assert.AreEqual(1, Directory.GetFiles(root, "save.json.corrupt-*").Length);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void QuarantineRetentionIsBoundedPerSourceFile()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                for (int i = 0; i < 5; i++)
                {
                    File.WriteAllText(Path.Combine(root, $"save.json.corrupt-2026010100000{i}-{Guid.NewGuid():N}"), "old");
                }

                string primaryPath = Path.Combine(root, "save.json");
                File.WriteAllText(primaryPath, "{ invalid primary");

                object service = CreateSaveService(root);
                Type realmType = GetRuntimeType("AL.Core.RealmId");
                object noRealm = Enum.Parse(realmType, "None");
                Invoke(service, "CreateNewSave", noRealm);

                Assert.LessOrEqual(Directory.GetFiles(root, "save.json.corrupt-*").Length, 3);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void DeleteSaveRemovesPrimaryBackupTransientAndQuarantineArtifacts()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                object service = CreateSaveService(root);
                Type realmType = GetRuntimeType("AL.Core.RealmId");
                object noRealm = Enum.Parse(realmType, "None");
                Invoke(service, "CreateNewSave", noRealm);

                string[] extraFiles =
                {
                    "save.tmp.json",
                    "save.previous.json",
                    "save.json.previous",
                    "save.recovery.stage5",
                    $"save.json.corrupt-20260101000000-{Guid.NewGuid():N}",
                    $"save.backup.json.corrupt-20260101000000-{Guid.NewGuid():N}"
                };

                foreach (string fileName in extraFiles)
                {
                    File.WriteAllText(Path.Combine(root, fileName), "artifact");
                }

                Invoke(service, "DeleteSave");

                Assert.False((bool)Invoke(service, "HasSave"));
                Assert.False(
                    File.Exists(
                        Path.Combine(root, "save.recovery.stage5")));
                Assert.Null(GetProperty(service, "CurrentSave"));
                Assert.AreEqual("None", GetProperty(service, "LastLoadStatus").ToString());
                Assert.AreEqual("None", GetProperty(service, "LastSaveStatus").ToString());
                Assert.IsEmpty(Directory.GetFiles(root, "save*.json*"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void DeleteSaveDoesNotClaimSuccessWhenAnArtifactSurvives()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            object noRealm = Enum.Parse(realmType, "None");
            Invoke(service, "CreateNewSave", noRealm);

            string backupPath = Path.Combine(root, "save.backup.json");
            fileSystem.DeleteFailures.Add(backupPath);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("AL-SAVE-DELETE-FAILED: Could not delete save artifact"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("AL-SAVE-DELETE-FAILED: Local save reset could not remove every profile artifact"));
            Invoke(service, "DeleteSave");

            Assert.NotNull(GetProperty(service, "CurrentSave"));
            Assert.AreEqual("DeleteFailed", GetProperty(service, "LastSaveStatus").ToString());
            Assert.True(fileSystem.FileExists(backupPath));
        }

        [TestCase("quarantine")]
        [TestCase("archive")]
        public void OrphanedStageFiveEvidenceCountsAsASaveAndBlocksCreation(
            string evidenceKind)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string quarantinePath = Path.Combine(
                root,
                "save.json.corrupt-20260724000000-stage5-tAAAAAAAAAAAAAAAAAAAAAA");
            string evidencePath = evidenceKind == "archive"
                ? quarantinePath + ".txn"
                : quarantinePath;
            fileSystem.Files[evidencePath] = "{ orphaned stage5 evidence";
            object service = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));

            Assert.True((bool)Invoke(service, "HasSave"));
            Invoke(service, "Load");

            Assert.AreEqual(
                "RecoveryRequired",
                GetProperty(service, "LastLoadStatus").ToString());
            Assert.Null(GetProperty(service, "CurrentSave"));
            Assert.AreEqual(
                "{ orphaned stage5 evidence",
                fileSystem.ReadAllText(evidencePath));
            Assert.AreEqual(0, fileSystem.MutationLedger.Count);
        }

        [Test]
        public void LegacyPreviousEvidenceCountsAsASaveAndBlocksCreation()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string legacyPreviousPath = Path.Combine(root, "save.json.previous");
            fileSystem.Files[legacyPreviousPath] = "{ legacy previous fallback";
            object service = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));

            Assert.True((bool)Invoke(service, "HasSave"));
            Invoke(service, "Load");

            Assert.AreEqual(
                "RecoveryRequired",
                GetProperty(service, "LastLoadStatus").ToString());
            Assert.Null(GetProperty(service, "CurrentSave"));
            Assert.AreEqual(
                "{ legacy previous fallback",
                fileSystem.ReadAllText(legacyPreviousPath));
            Assert.AreEqual(0, fileSystem.MutationLedger.Count);
        }

        [Test]
        public void TempCleanupFailureStopsSaveBeforeActiveFilesChange()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            object noRealm = Enum.Parse(realmType, "None");
            Invoke(service, "CreateNewSave", noRealm);

            object currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_BEFORE_TEMP_FAILURE");
            Invoke(service, "Save");

            string primaryPath = Path.Combine(root, "save.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string primaryBefore = fileSystem.ReadAllText(primaryPath);
            fileSystem.Files[tempPath] = "{ stale temp";
            fileSystem.DeleteFailures.Add(tempPath);
            SetField(currentSave, "CurrentChapterId", "C1_AFTER_TEMP_FAILURE");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("AL-SAVE-DELETE-FAILED: Could not delete save artifact"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("AL-SAVE-TEMP-CLEANUP-FAILED"));
            Invoke(service, "Save");

            Assert.AreEqual("SaveFailedPreviousPreserved", GetProperty(service, "LastSaveStatus").ToString());
            Assert.AreEqual(primaryBefore, fileSystem.ReadAllText(primaryPath));
            Assert.AreEqual("{ stale temp", fileSystem.ReadAllText(tempPath));
        }

        [Test]
        public void InvalidPrimaryAndValidBackupUseOnlyTheTwoApprovedMoves()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            object noRealm = Enum.Parse(realmType, "None");
            Invoke(service, "CreateNewSave", noRealm);

            object currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_BACKUP_RECOVERY");
            Invoke(service, "Save");

            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string backupBefore = fileSystem.ReadAllText(backupPath);
            fileSystem.Files[primaryPath] = "{ invalid primary";
            string primaryBefore = fileSystem.ReadAllText(primaryPath);
            int moveCallsBeforeLoad = fileSystem.TotalMoveCount;

            object recoveredService = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Invoke(recoveredService, "Load");

            Assert.AreEqual("RecoveredFromBackup", GetProperty(recoveredService, "LastLoadStatus").ToString());
            Assert.NotNull(GetProperty(recoveredService, "CurrentSave"));
            Assert.Null(GetProperty(recoveredService, "ReadOnlyCandidateSnapshot"));
            Assert.AreEqual(backupBefore, fileSystem.ReadAllText(primaryPath));
            Assert.AreEqual(backupBefore, fileSystem.ReadAllText(backupPath));
            Assert.AreEqual(moveCallsBeforeLoad + 2, fileSystem.TotalMoveCount);
            Assert.AreEqual(1, fileSystem.GetMoveCount(primaryPath));
            string quarantinePath = fileSystem.Files.Keys.Single(path =>
                Path.GetFileName(path).StartsWith(
                    "save.json.corrupt-",
                    StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(primaryBefore, fileSystem.ReadAllText(quarantinePath));
        }

        [TestCase("{")]
        [TestCase("")]
        [TestCase("[]")]
        [TestCase("{}")]
        public void InvalidPrimaryKindsRecoverRichBackupByteForByteThroughVerifiedQuarantine(
            string invalidPrimary)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string backupBytes = ArrangeInvalidPrimaryAndExactBackup(
                root,
                fileSystem,
                invalidPrimary,
                "C1_STAGE5_INVALID_KINDS");
            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            int tempWritesBefore = fileSystem.GetDurableWriteCount(tempPath);
            int primaryWritesBefore = fileSystem.GetDurableWriteCount(primaryPath);
            int primaryMovesBefore = fileSystem.GetMoveCount(primaryPath);
            int previousMovesBefore = fileSystem.GetMoveCount(previousPath);

            object service = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            Invoke(service, "Load");

            Assert.AreEqual(
                "RecoveredFromBackup",
                GetProperty(service, "LastLoadStatus").ToString());
            Assert.AreEqual(
                "C1_STAGE5_INVALID_KINDS",
                GetField(GetProperty(service, "CurrentSave"), "CurrentChapterId"));
            AssertCanonicalLedger(
                fileSystem,
                root,
                backupBytes,
                backupBytes,
                backupBytes,
                null);
            string quarantinePath = FindSingleStageFiveQuarantine(fileSystem);
            Assert.That(
                Path.GetFileName(quarantinePath).Length,
                Is.LessThanOrEqualTo(72));
            Assert.AreEqual(invalidPrimary, fileSystem.ReadAllText(quarantinePath));
            Assert.AreEqual(
                tempWritesBefore + 1,
                fileSystem.GetDurableWriteCount(tempPath));
            Assert.AreEqual(
                primaryWritesBefore + 1,
                fileSystem.GetDurableWriteCount(primaryPath));
            Assert.AreEqual(
                primaryMovesBefore + 1,
                fileSystem.GetMoveCount(primaryPath));
            Assert.AreEqual(
                previousMovesBefore + 1,
                fileSystem.GetMoveCount(previousPath));
            Assert.AreEqual(backupBytes, fileSystem.ReadAllText(backupPath));

            object disposition = GetProperty(service, "LastLoadDisposition");
            Assert.AreEqual("Backup", GetProperty(disposition, "SelectedSource").ToString());
            Assert.True((bool)GetProperty(disposition, "IsWritable"));
            Assert.True((bool)GetProperty(disposition, "IsRuntimeUsable"));
            Assert.False((bool)GetProperty(disposition, "OfflineProgressApplied"));
            Assert.True((bool)GetProperty(disposition, "DiskChanged"));
            Assert.True((bool)GetProperty(disposition, "RawEvidencePreserved"));

            fileSystem.ClearMutationLedger();
            int tempWritesAfterRecovery =
                fileSystem.GetDurableWriteCount(tempPath);
            int primaryWritesAfterRecovery =
                fileSystem.GetDurableWriteCount(primaryPath);
            object reloaded = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            Invoke(reloaded, "Load");
            Assert.AreEqual(
                "RecoveredFromBackup",
                GetProperty(reloaded, "LastLoadStatus").ToString());
            Assert.AreEqual(0, fileSystem.MutationLedger.Count);
            Assert.AreEqual(
                tempWritesAfterRecovery,
                fileSystem.GetDurableWriteCount(tempPath));
            Assert.AreEqual(
                primaryWritesAfterRecovery,
                fileSystem.GetDurableWriteCount(primaryPath));
            Assert.AreEqual(invalidPrimary, fileSystem.ReadAllText(quarantinePath));
        }

        [TestCase("S1", 0, 1, 1, 1)]
        [TestCase("S2", 0, 0, 1, 1)]
        [TestCase("S3", 0, 0, 0, 1)]
        public void QuarantinedRecoveryResumesExactIntermediateLedgerWithoutRepeatingSteps(
            string startingState,
            int expectedTempWrites,
            int expectedPrimaryMoves,
            int expectedPrimaryWrites,
            int expectedPreviousMoves)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            const string invalidPrimary = "{ stage5 invalid";
            string backupBytes = ArrangeInvalidPrimaryAndExactBackup(
                root,
                fileSystem,
                invalidPrimary,
                "C1_STAGE5_RESUME");
            string primaryPath = Path.Combine(root, "save.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            string markerPath = Path.Combine(root, "save.recovery.stage5");
            if (startingState == "S1")
            {
                fileSystem.AddMutationFault(
                    "Move",
                    primaryPath,
                    previousPath,
                    ScriptedFaultTiming.BeforeMutation,
                    ScriptedFaultException.Io);
            }
            else if (startingState == "S2")
            {
                fileSystem.WriteFailuresBeforeMutation.Add(primaryPath);
            }
            else
            {
                bool registered = false;
                fileSystem.MutationObserver = (
                    operation,
                    sourcePath,
                    destinationPath,
                    timing) =>
                {
                    if (!registered &&
                        operation == "Move" &&
                        timing == ScriptedFaultTiming.BeforeMutation &&
                        string.Equals(
                            sourcePath,
                            previousPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        registered = true;
                        fileSystem.AddMutationFault(
                            operation,
                            sourcePath,
                            destinationPath,
                            ScriptedFaultTiming.BeforeMutation,
                            ScriptedFaultException.Io);
                    }
                };
            }

            object interrupted = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(interrupted, "Load");
            Assert.AreEqual(
                "RecoveryFailed",
                GetProperty(interrupted, "LastLoadStatus").ToString());
            fileSystem.WriteFailuresBeforeMutation.Remove(primaryPath);
            fileSystem.MutationObserver = null;
            int tempWritesBefore = fileSystem.GetDurableWriteCount(tempPath);
            int primaryWritesBefore = fileSystem.GetDurableWriteCount(primaryPath);
            int primaryMovesBefore = fileSystem.GetMoveCount(primaryPath);
            int previousMovesBefore = fileSystem.GetMoveCount(previousPath);
            fileSystem.ClearMutationLedger();

            object service = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            Invoke(service, "Load");

            Assert.AreEqual(
                "RecoveredFromBackup",
                GetProperty(service, "LastLoadStatus").ToString());
            AssertCanonicalLedger(
                fileSystem,
                root,
                backupBytes,
                backupBytes,
                backupBytes,
                null);
            Assert.AreEqual(
                expectedTempWrites,
                fileSystem.GetDurableWriteCount(tempPath) - tempWritesBefore);
            Assert.AreEqual(
                expectedPrimaryWrites,
                fileSystem.GetDurableWriteCount(primaryPath) - primaryWritesBefore);
            Assert.AreEqual(
                expectedPrimaryMoves,
                fileSystem.GetMoveCount(primaryPath) - primaryMovesBefore);
            Assert.AreEqual(
                expectedPreviousMoves,
                fileSystem.GetMoveCount(previousPath) - previousMovesBefore);
            Assert.AreEqual(
                invalidPrimary,
                fileSystem.ReadAllText(FindSingleStageFiveQuarantine(fileSystem)));
        }

        [TestCase("preserve", ScriptedFaultTiming.BeforeMutation, "S1", false)]
        [TestCase("preserve", ScriptedFaultTiming.AfterMutation, "S4", true)]
        [TestCase("quarantine", ScriptedFaultTiming.BeforeMutation, "S3", false)]
        [TestCase("quarantine", ScriptedFaultTiming.AfterMutation, "S4", true)]
        public void QuarantinedRecoveryMoveFaultLeavesOnlyAnExactResumableLedger(
            string transition,
            ScriptedFaultTiming timing,
            string expectedState,
            bool expectRecoveredOnFaultingLoad)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            const string invalidPrimary = "{ stage5 move fault";
            string backupBytes = ArrangeInvalidPrimaryAndExactBackup(
                root,
                fileSystem,
                invalidPrimary,
                "C1_STAGE5_MOVE_FAULT");
            string primaryPath = Path.Combine(root, "save.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            if (transition == "preserve")
            {
                fileSystem.AddMutationFault(
                    "Move",
                    primaryPath,
                    previousPath,
                    timing,
                    ScriptedFaultException.Io);
            }
            else
            {
                bool registered = false;
                fileSystem.MutationObserver = (
                    operation,
                    sourcePath,
                    destinationPath,
                    observedTiming) =>
                {
                    if (!registered &&
                        operation == "Move" &&
                        string.Equals(
                            sourcePath,
                            previousPath,
                            StringComparison.OrdinalIgnoreCase) &&
                        observedTiming == ScriptedFaultTiming.BeforeMutation)
                    {
                        registered = true;
                        fileSystem.AddMutationFault(
                            "Move",
                            sourcePath,
                            destinationPath,
                            timing,
                            ScriptedFaultException.Io);
                    }
                };
            }

            object service = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(service, "Load");

            Assert.AreEqual(
                expectRecoveredOnFaultingLoad
                    ? "RecoveredFromBackup"
                    : "RecoveryFailed",
                GetProperty(service, "LastLoadStatus").ToString());
            if (expectedState == "S1")
            {
                AssertCanonicalLedger(
                    fileSystem,
                    root,
                    invalidPrimary,
                    backupBytes,
                    backupBytes,
                    null);
            }
            else if (expectedState == "S2")
            {
                AssertCanonicalLedger(
                    fileSystem,
                    root,
                    null,
                    backupBytes,
                    backupBytes,
                    invalidPrimary);
            }
            else if (expectedState == "S3")
            {
                AssertCanonicalLedger(
                    fileSystem,
                    root,
                    backupBytes,
                    backupBytes,
                    backupBytes,
                    invalidPrimary);
            }
            else
            {
                AssertCanonicalLedger(
                    fileSystem,
                    root,
                    backupBytes,
                    backupBytes,
                    backupBytes,
                    null);
                Assert.AreEqual(
                    invalidPrimary,
                    fileSystem.ReadAllText(
                        FindSingleStageFiveQuarantine(fileSystem)));
            }

            if (expectRecoveredOnFaultingLoad)
            {
                return;
            }

            fileSystem.MutationObserver = null;
            object resumed = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            Invoke(resumed, "Load");
            Assert.AreEqual(
                "RecoveredFromBackup",
                GetProperty(resumed, "LastLoadStatus").ToString());
            Assert.AreEqual(
                invalidPrimary,
                fileSystem.ReadAllText(FindSingleStageFiveQuarantine(fileSystem)));
        }

        [TestCase("temp", "before", "S0", true)]
        [TestCase("temp", "partial", "ambiguous", false)]
        [TestCase("temp", "exact", "S1", true)]
        [TestCase("primary", "before", "S2", true)]
        [TestCase("primary", "partial", "ambiguous", false)]
        [TestCase("primary", "exact", "S3", true)]
        public void QuarantinedRecoveryWriteFaultResumesOnlyExactResidue(
            string pathKind,
            string faultKind,
            string expectedState,
            bool expectCleanReloadRecovery)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            const string invalidPrimary = "{ stage5 write fault";
            string backupBytes = ArrangeInvalidPrimaryAndExactBackup(
                root,
                fileSystem,
                invalidPrimary,
                "C1_STAGE5_WRITE_FAULT");
            string primaryPath = Path.Combine(root, "save.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            string markerPath = Path.Combine(root, "save.recovery.stage5");
            string faultPath = pathKind == "temp" ? tempPath : primaryPath;
            if (faultKind == "before")
            {
                fileSystem.WriteFailuresBeforeMutation.Add(faultPath);
            }
            else if (faultKind == "partial")
            {
                fileSystem.WriteFailuresAfterMutation.Add(faultPath);
            }
            else
            {
                fileSystem.WriteFailuresAfterExactMutation.Add(faultPath);
            }

            object service = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(service, "Load");

            if (faultKind == "exact")
            {
                Assert.AreEqual(
                    "RecoveredFromBackup",
                    GetProperty(service, "LastLoadStatus").ToString());
                Assert.AreEqual(
                    invalidPrimary,
                    fileSystem.ReadAllText(
                        FindSingleStageFiveQuarantine(fileSystem)));
                return;
            }

            Assert.AreEqual(
                "RecoveryFailed",
                GetProperty(service, "LastLoadStatus").ToString());
            Assert.Null(GetProperty(service, "CurrentSave"));
            if (expectedState == "S0")
            {
                AssertCanonicalLedger(
                    fileSystem,
                    root,
                    invalidPrimary,
                    backupBytes,
                    null,
                    null);
            }
            else if (expectedState == "S2")
            {
                AssertCanonicalLedger(
                    fileSystem,
                    root,
                    null,
                    backupBytes,
                    backupBytes,
                    invalidPrimary);
            }
            else
            {
                string partialBackup = backupBytes.Substring(
                    0,
                    Math.Min(backupBytes.Length, 16));
                AssertCanonicalLedger(
                    fileSystem,
                    root,
                    pathKind == "temp"
                        ? invalidPrimary
                        : partialBackup,
                    backupBytes,
                    pathKind == "temp"
                        ? partialBackup
                        : backupBytes,
                    pathKind == "temp"
                        ? null
                        : invalidPrimary);
                Assert.True(fileSystem.FileExists(markerPath));
                Assert.That(
                    fileSystem.ReadAllText(markerPath),
                    Does.StartWith("AL-STAGE5|1|"));
                Assert.IsEmpty(fileSystem.Files.Keys.Where(path =>
                    Path.GetFileName(path).IndexOf(
                        "-stage5-",
                        StringComparison.OrdinalIgnoreCase) >= 0));
            }

            fileSystem.WriteFailuresBeforeMutation.Remove(faultPath);
            fileSystem.WriteFailuresAfterMutation.Remove(faultPath);
            fileSystem.WriteFailuresAfterExactMutation.Remove(faultPath);
            var filesBeforeReload = new Dictionary<string, string>(
                fileSystem.Files,
                StringComparer.OrdinalIgnoreCase);
            var durableWritesBeforeReload = new Dictionary<string, int>(
                fileSystem.DurableWriteCounts,
                StringComparer.OrdinalIgnoreCase);
            fileSystem.ClearMutationLedger();
            object reloaded = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(reloaded, "Load");
            Assert.AreEqual(
                expectCleanReloadRecovery
                    ? "RecoveredFromBackup"
                    : "RecoveryRequired",
                GetProperty(reloaded, "LastLoadStatus").ToString());
            if (!expectCleanReloadRecovery)
            {
                Assert.AreEqual(0, fileSystem.MutationLedger.Count);
                CollectionAssert.AreEquivalent(
                    filesBeforeReload.Keys,
                    fileSystem.Files.Keys);
                foreach (KeyValuePair<string, string> file in
                         filesBeforeReload)
                {
                    Assert.AreEqual(
                        file.Value,
                        fileSystem.ReadAllText(file.Key),
                        file.Key);
                }

                CollectionAssert.AreEquivalent(
                    durableWritesBeforeReload,
                    fileSystem.DurableWriteCounts);
            }
        }

        [TestCase("before", true)]
        [TestCase("partial", false)]
        [TestCase("exact", true)]
        public void StageFiveTransactionMarkerFaultPublishesOnlyExactProvenance(
            string faultKind,
            bool expectCleanReloadRecovery)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            const string invalidPrimary = "{ stage5 marker fault";
            string backupBytes = ArrangeInvalidPrimaryAndExactBackup(
                root,
                fileSystem,
                invalidPrimary,
                "C1_STAGE5_MARKER_FAULT");
            string markerPath = Path.Combine(root, "save.recovery.stage5");
            if (faultKind == "before")
            {
                fileSystem.WriteFailuresBeforeMutation.Add(markerPath);
            }
            else if (faultKind == "partial")
            {
                fileSystem.WriteFailuresAfterMutation.Add(markerPath);
            }
            else
            {
                fileSystem.WriteFailuresAfterExactMutation.Add(markerPath);
            }

            object service = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(service, "Load");

            Assert.AreEqual(
                faultKind == "exact"
                    ? "RecoveredFromBackup"
                    : "RecoveryFailed",
                GetProperty(service, "LastLoadStatus").ToString());
            if (faultKind == "exact")
            {
                Assert.AreEqual(
                    invalidPrimary,
                    fileSystem.ReadAllText(
                        FindSingleStageFiveQuarantine(fileSystem)));
                return;
            }

            AssertCanonicalLedger(
                fileSystem,
                root,
                invalidPrimary,
                backupBytes,
                null,
                null);
            string markerBeforeReload = fileSystem.FileExists(markerPath)
                ? fileSystem.ReadAllText(markerPath)
                : null;
            if (faultKind == "partial")
            {
                Assert.NotNull(markerBeforeReload);
                Assert.AreEqual(16, markerBeforeReload.Length);
            }
            else
            {
                Assert.Null(markerBeforeReload);
            }

            fileSystem.WriteFailuresBeforeMutation.Remove(markerPath);
            fileSystem.WriteFailuresAfterMutation.Remove(markerPath);
            fileSystem.WriteFailuresAfterExactMutation.Remove(markerPath);
            fileSystem.ClearMutationLedger();
            object reloaded = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(reloaded, "Load");
            Assert.AreEqual(
                expectCleanReloadRecovery
                    ? "RecoveredFromBackup"
                    : "RecoveryRequired",
                GetProperty(reloaded, "LastLoadStatus").ToString());
            if (!expectCleanReloadRecovery)
            {
                Assert.AreEqual(0, fileSystem.MutationLedger.Count);
                Assert.AreEqual(
                    markerBeforeReload,
                    fileSystem.ReadAllText(markerPath));
            }
        }

        [Test]
        public void StageFiveDirectoryCreationFailurePreservesS0AndCanRetry()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            const string invalidPrimary = "{ stage5 directory failure";
            string backupBytes = ArrangeInvalidPrimaryAndExactBackup(
                root,
                fileSystem,
                invalidPrimary,
                "C1_STAGE5_DIRECTORY_FAILURE");
            fileSystem.CreateDirectoryFailures.Add(root);

            object service = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(service, "Load");

            Assert.AreEqual(
                "RecoveryFailed",
                GetProperty(service, "LastLoadStatus").ToString());
            AssertCanonicalLedger(
                fileSystem,
                root,
                invalidPrimary,
                backupBytes,
                null,
                null);
            Assert.False(
                fileSystem.FileExists(
                    Path.Combine(root, "save.recovery.stage5")));
            fileSystem.CreateDirectoryFailures.Remove(root);

            object reloaded = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            Invoke(reloaded, "Load");
            Assert.AreEqual(
                "RecoveredFromBackup",
                GetProperty(reloaded, "LastLoadStatus").ToString());
        }

        [Test]
        public void DegradedPrimaryNeverEntersStrictInvalidRecovery()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string backupBytes = ArrangeExactBackupOnly(
                root,
                fileSystem,
                "C1_STAGE5_DEGRADED_EXCLUDED");
            string primaryPath = Path.Combine(root, "save.json");
            string degradedPrimary = backupBytes.Replace(
                "\"Resources\": [",
                "\"Resources\": [null,");
            Assert.AreNotEqual(backupBytes, degradedPrimary);
            fileSystem.Files[primaryPath] = degradedPrimary;
            fileSystem.ClearMutationLedger();

            object service = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            Invoke(service, "Load");

            Assert.AreEqual(
                "RecoveryRequired",
                GetProperty(service, "LastLoadStatus").ToString());
            Assert.Null(GetProperty(service, "CurrentSave"));
            Assert.NotNull(GetProperty(service, "ReadOnlyCandidateSnapshot"));
            object primarySummary = ((IEnumerable)GetProperty(
                    GetProperty(service, "LastLoadDisposition"),
                    "CandidateSummaries"))
                .Cast<object>()
                .Single(summary =>
                    GetProperty(summary, "Source").ToString() == "Primary");
            Assert.AreEqual(
                "DegradedMalformed",
                GetProperty(primarySummary, "SemanticOutcome").ToString());
            Assert.AreEqual(degradedPrimary, fileSystem.ReadAllText(primaryPath));
            Assert.AreEqual(
                backupBytes,
                fileSystem.ReadAllText(
                    Path.Combine(root, "save.backup.json")));
            Assert.False(
                fileSystem.FileExists(
                    Path.Combine(root, "save.recovery.stage5")));
            Assert.AreEqual(0, fileSystem.MutationLedger.Count);
        }

        [TestCase("missing")]
        [TestCase("invalid")]
        public void TransactionMarkerBlocksWritablePrimaryWhenBackupCannotValidate(
            string backupState)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string primaryBytes = ArrangeExactBackupOnly(
                root,
                fileSystem,
                "C1_STAGE5_MARKER_BACKUP_CONFLICT");
            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string markerPath = Path.Combine(root, "save.recovery.stage5");
            fileSystem.Files[primaryPath] = primaryBytes;
            if (backupState == "missing")
            {
                fileSystem.Files.Remove(backupPath);
            }
            else
            {
                fileSystem.Files[backupPath] = "{ invalid backup";
            }

            fileSystem.Files[markerPath] = "malformed-stage5-marker";
            fileSystem.ClearMutationLedger();

            object service = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            Invoke(service, "Load");

            Assert.AreEqual(
                "RecoveryRequired",
                GetProperty(service, "LastLoadStatus").ToString());
            Assert.Null(GetProperty(service, "CurrentSave"));
            Assert.NotNull(GetProperty(service, "ReadOnlyCandidateSnapshot"));
            Assert.AreEqual(primaryBytes, fileSystem.ReadAllText(primaryPath));
            Assert.AreEqual(
                "malformed-stage5-marker",
                fileSystem.ReadAllText(markerPath));
            Assert.AreEqual(0, fileSystem.MutationLedger.Count);
        }

        [Test]
        public void QuarantineDestinationCollisionPreservesForeignFileAndOriginalWitness()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            const string invalidPrimary = "{ stage5 collision";
            const string foreignContents = "foreign-quarantine-collision";
            string backupBytes = ArrangeInvalidPrimaryAndExactBackup(
                root,
                fileSystem,
                invalidPrimary,
                "C1_STAGE5_COLLISION");
            string previousPath = Path.Combine(root, "save.previous.json");
            string collisionPath = null;
            fileSystem.MutationObserver = (
                operation,
                sourcePath,
                destinationPath,
                timing) =>
            {
                if (collisionPath == null &&
                    operation == "Move" &&
                    timing == ScriptedFaultTiming.BeforeMutation &&
                    string.Equals(
                        sourcePath,
                        previousPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    collisionPath = destinationPath;
                    fileSystem.Files[destinationPath] = foreignContents;
                }
            };

            object service = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(service, "Load");

            Assert.AreEqual(
                "RecoveryFailed",
                GetProperty(service, "LastLoadStatus").ToString());
            Assert.Null(GetProperty(service, "CurrentSave"));
            AssertCanonicalLedger(
                fileSystem,
                root,
                backupBytes,
                backupBytes,
                backupBytes,
                invalidPrimary);
            Assert.NotNull(collisionPath);
            Assert.AreEqual(foreignContents, fileSystem.ReadAllText(collisionPath));

            fileSystem.MutationObserver = null;
            fileSystem.ClearMutationLedger();
            object reloaded = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            Invoke(reloaded, "Load");
            Assert.AreEqual(
                "RecoveryRequired",
                GetProperty(reloaded, "LastLoadStatus").ToString());
            Assert.AreEqual(0, fileSystem.MutationLedger.Count);
            Assert.AreEqual(foreignContents, fileSystem.ReadAllText(collisionPath));
            Assert.AreEqual(invalidPrimary, fileSystem.ReadAllText(previousPath));
        }

        [Test]
        public void PreviousDestinationCollisionPreservesForeignAndPrimaryEvidence()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            const string invalidPrimary = "{ stage5 previous collision";
            const string foreignPrevious = "foreign-previous-collision";
            string backupBytes = ArrangeInvalidPrimaryAndExactBackup(
                root,
                fileSystem,
                invalidPrimary,
                "C1_STAGE5_PREVIOUS_COLLISION");
            string primaryPath = Path.Combine(root, "save.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            fileSystem.MutationObserver = (
                operation,
                sourcePath,
                destinationPath,
                timing) =>
            {
                if (operation == "Move" &&
                    timing == ScriptedFaultTiming.BeforeMutation &&
                    string.Equals(
                        sourcePath,
                        primaryPath,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        destinationPath,
                        previousPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    fileSystem.Files[destinationPath] = foreignPrevious;
                }
            };

            object service = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(service, "Load");

            Assert.AreEqual(
                "RecoveryFailed",
                GetProperty(service, "LastLoadStatus").ToString());
            Assert.AreEqual(invalidPrimary, fileSystem.ReadAllText(primaryPath));
            Assert.AreEqual(foreignPrevious, fileSystem.ReadAllText(previousPath));
            Assert.AreEqual(
                backupBytes,
                fileSystem.ReadAllText(
                    Path.Combine(root, "save.backup.json")));

            fileSystem.MutationObserver = null;
            fileSystem.ClearMutationLedger();
            object reloaded = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            Invoke(reloaded, "Load");
            Assert.AreEqual(
                "RecoveryRequired",
                GetProperty(reloaded, "LastLoadStatus").ToString());
            Assert.AreEqual(0, fileSystem.MutationLedger.Count);
        }

        [Test]
        public void QuarantineDriftBetweenFinalInventoriesNeverPublishes()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            const string invalidPrimary = "{ stage5 final drift";
            ArrangeInvalidPrimaryAndExactBackup(
                root,
                fileSystem,
                invalidPrimary,
                "C1_STAGE5_FINAL_DRIFT");
            string previousPath = Path.Combine(root, "save.previous.json");
            string quarantinePath = null;
            fileSystem.MutationObserver = (
                operation,
                sourcePath,
                destinationPath,
                timing) =>
            {
                if (operation == "Move" &&
                    timing == ScriptedFaultTiming.AfterMutation &&
                    string.Equals(
                        sourcePath,
                        previousPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    quarantinePath = destinationPath;
                    int driftRead =
                        fileSystem.GetBoundedReadCount(destinationPath) + 3;
                    fileSystem.BoundedReadObserver = (path, count) =>
                    {
                        if (count == driftRead &&
                            string.Equals(
                                path,
                                destinationPath,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            fileSystem.Files[path] =
                                "drifted-final-quarantine";
                        }
                    };
                }
            };

            object service = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(service, "Load");

            Assert.AreEqual(
                "RecoveryFailed",
                GetProperty(service, "LastLoadStatus").ToString());
            Assert.Null(GetProperty(service, "CurrentSave"));
            Assert.NotNull(quarantinePath);
            Assert.AreEqual(
                "drifted-final-quarantine",
                fileSystem.ReadAllText(quarantinePath));
            fileSystem.MutationObserver = null;
            fileSystem.BoundedReadObserver = null;
            fileSystem.ClearMutationLedger();
            object reloaded = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            Invoke(reloaded, "Load");
            Assert.AreEqual(
                "RecoveryRequired",
                GetProperty(reloaded, "LastLoadStatus").ToString());
            Assert.AreEqual(0, fileSystem.MutationLedger.Count);
        }

        [Test]
        public void RecoveredInvalidPrimarySaveConsumesWitnessAndQuarantineDriftFreezes()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            const string invalidPrimary = "{ stage5 save witness";
            string backupBytes = ArrangeInvalidPrimaryAndExactBackup(
                root,
                fileSystem,
                invalidPrimary,
                "C1_STAGE5_SAVE_WITNESS");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            object service = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            Invoke(service, "Load");
            string quarantinePath = FindSingleStageFiveQuarantine(fileSystem);
            string recoveredLoadStatus =
                GetProperty(service, "LastLoadStatus").ToString();

            object currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_STAGE5_AFTER_SAVE");
            fileSystem.ClearMutationLedger();
            Invoke(service, "Save");

            Assert.AreEqual(
                "SavedPrimary",
                GetProperty(service, "LastSaveStatus").ToString());
            Assert.AreEqual(
                recoveredLoadStatus,
                GetProperty(service, "LastLoadStatus").ToString());
            Assert.False(fileSystem.FileExists(tempPath));
            Assert.False(fileSystem.FileExists(previousPath));
            Assert.False(
                fileSystem.FileExists(
                    Path.Combine(root, "save.recovery.stage5")));
            Assert.True(fileSystem.FileExists(quarantinePath + ".txn"));
            Assert.AreEqual(backupBytes, fileSystem.ReadAllText(backupPath));
            Assert.AreEqual(invalidPrimary, fileSystem.ReadAllText(quarantinePath));

            object reloaded = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            Invoke(reloaded, "Load");
            Assert.AreEqual(
                "LoadedPrimary",
                GetProperty(reloaded, "LastLoadStatus").ToString());
            Assert.AreEqual(
                "C1_STAGE5_AFTER_SAVE",
                GetField(GetProperty(reloaded, "CurrentSave"), "CurrentChapterId"));

            const string secondInvalidPrimary =
                "{ stage5 repeated invalid primary";
            string primaryPath = Path.Combine(root, "save.json");
            fileSystem.Files[primaryPath] = secondInvalidPrimary;
            object repeatedRecovery = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            Invoke(repeatedRecovery, "Load");
            Assert.AreEqual(
                "RecoveredFromBackup",
                GetProperty(repeatedRecovery, "LastLoadStatus").ToString());
            string[] retainedQuarantines = fileSystem.Files.Keys
                .Where(path =>
                    Path.GetFileName(path).StartsWith(
                        "save.json.corrupt-",
                        StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(path).IndexOf(
                        "-stage5-",
                        StringComparison.OrdinalIgnoreCase) >= 0 &&
                    !path.EndsWith(
                        ".txn",
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.AreEqual(2, retainedQuarantines.Length);
            CollectionAssert.AreEquivalent(
                new[] { invalidPrimary, secondInvalidPrimary },
                retainedQuarantines
                    .Select(fileSystem.ReadAllText)
                    .ToArray());

            string driftRoot = root + "-drift";
            var driftFileSystem = new ScriptedSaveFileOperations();
            string driftBackupBytes = ArrangeInvalidPrimaryAndExactBackup(
                driftRoot,
                driftFileSystem,
                invalidPrimary,
                "C1_STAGE5_DRIFT");
            object driftService = CreateSaveService(
                driftRoot,
                CreateFileOperationsProxy(driftFileSystem));
            Invoke(driftService, "Load");
            Assert.AreEqual(
                "RecoveredFromBackup",
                GetProperty(driftService, "LastLoadStatus").ToString());
            string driftQuarantinePath =
                FindSingleStageFiveQuarantine(driftFileSystem);
            string driftPrimaryPath = Path.Combine(driftRoot, "save.json");
            string primaryBeforeDriftCheck =
                driftFileSystem.ReadAllText(driftPrimaryPath);
            driftFileSystem.Files[driftQuarantinePath] =
                "changed-quarantine";
            driftFileSystem.ClearMutationLedger();
            InvokeAllowingFailureLogs(driftService, "Save");
            Assert.AreEqual(
                "SaveFailedPreviousPreserved",
                GetProperty(driftService, "LastSaveStatus").ToString());
            Assert.Null(GetProperty(driftService, "CurrentSave"));
            Assert.AreEqual(0, driftFileSystem.MutationLedger.Count);
            Assert.AreEqual(
                primaryBeforeDriftCheck,
                driftFileSystem.ReadAllText(driftPrimaryPath));
            Assert.AreEqual(
                driftBackupBytes,
                driftFileSystem.ReadAllText(
                    Path.Combine(driftRoot, "save.backup.json")));
        }

        [TestCase(ScriptedFaultTiming.BeforeMutation, false)]
        [TestCase(ScriptedFaultTiming.AfterMutation, true)]
        public void RecoveryMarkerArchiveMoveReconcilesWithoutDeletingEvidence(
            ScriptedFaultTiming timing,
            bool expectSaved)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            const string invalidPrimary = "{ stage5 archive fault";
            ArrangeInvalidPrimaryAndExactBackup(
                root,
                fileSystem,
                invalidPrimary,
                "C1_STAGE5_ARCHIVE_FAULT");
            object service = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            Invoke(service, "Load");
            string markerPath = Path.Combine(root, "save.recovery.stage5");
            string quarantinePath = FindSingleStageFiveQuarantine(fileSystem);
            string archivePath = quarantinePath + ".txn";
            string exactMarker = fileSystem.ReadAllText(markerPath);
            fileSystem.AddMutationFault(
                "Move",
                markerPath,
                archivePath,
                timing,
                ScriptedFaultException.Io);

            InvokeAllowingFailureLogs(service, "Save");

            Assert.AreEqual(
                expectSaved,
                GetProperty(service, "LastSaveStatus").ToString() ==
                    "SavedPrimary");
            Assert.AreEqual(
                invalidPrimary,
                fileSystem.ReadAllText(quarantinePath));
            if (expectSaved)
            {
                Assert.False(fileSystem.FileExists(markerPath));
                Assert.AreEqual(exactMarker, fileSystem.ReadAllText(archivePath));
                Assert.NotNull(GetProperty(service, "CurrentSave"));
            }
            else
            {
                Assert.AreEqual(exactMarker, fileSystem.ReadAllText(markerPath));
                Assert.False(fileSystem.FileExists(archivePath));
                Assert.Null(GetProperty(service, "CurrentSave"));
            }
        }

        [Test]
        public void MarkerDriftAtArchiveBoundaryIsMovedButNeverDeletedOrPublished()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            const string invalidPrimary = "{ stage5 archive drift";
            ArrangeInvalidPrimaryAndExactBackup(
                root,
                fileSystem,
                invalidPrimary,
                "C1_STAGE5_ARCHIVE_DRIFT");
            object service = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            Invoke(service, "Load");
            string markerPath = Path.Combine(root, "save.recovery.stage5");
            string quarantinePath = FindSingleStageFiveQuarantine(fileSystem);
            string archivePath = quarantinePath + ".txn";
            const string driftedMarker = "foreign-marker-at-cleanup";
            fileSystem.MutationObserver = (
                operation,
                sourcePath,
                destinationPath,
                timing) =>
            {
                if (operation == "Move" &&
                    timing == ScriptedFaultTiming.BeforeMutation &&
                    string.Equals(
                        sourcePath,
                        markerPath,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        destinationPath,
                        archivePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    fileSystem.Files[sourcePath] = driftedMarker;
                }
            };

            InvokeAllowingFailureLogs(service, "Save");

            Assert.AreNotEqual(
                "SavedPrimary",
                GetProperty(service, "LastSaveStatus").ToString());
            Assert.Null(GetProperty(service, "CurrentSave"));
            Assert.False(fileSystem.FileExists(markerPath));
            Assert.AreEqual(driftedMarker, fileSystem.ReadAllText(archivePath));
            Assert.AreEqual(
                invalidPrimary,
                fileSystem.ReadAllText(quarantinePath));
        }

        [Test]
        public void FirstRecoveredSavePinsQuarantineAndMarkerWhilePruningOldArtifacts()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            const string invalidPrimary = "{ stage5 prune witness";
            ArrangeInvalidPrimaryAndExactBackup(
                root,
                fileSystem,
                invalidPrimary,
                "C1_STAGE5_PRUNE");
            for (int index = 0; index < 4; index++)
            {
                fileSystem.Files[Path.Combine(
                    root,
                    $"save.json.corrupt-2026010100000{index}-legacy")] =
                    $"old-{index}";
            }

            object service = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            Invoke(service, "Load");
            string quarantinePath = FindSingleStageFiveQuarantine(fileSystem);
            string markerPath = Path.Combine(root, "save.recovery.stage5");
            string exactMarker = fileSystem.ReadAllText(markerPath);

            Invoke(service, "Save");

            Assert.AreEqual(
                "SavedPrimary",
                GetProperty(service, "LastSaveStatus").ToString());
            string archivePath = quarantinePath + ".txn";
            Assert.AreEqual(invalidPrimary, fileSystem.ReadAllText(quarantinePath));
            Assert.AreEqual(exactMarker, fileSystem.ReadAllText(archivePath));
            string[] remaining = fileSystem.Files.Keys
                .Where(path =>
                    Path.GetFileName(path).StartsWith(
                        "save.json.corrupt-",
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.That(remaining.Length, Is.LessThanOrEqualTo(3));
            CollectionAssert.Contains(remaining, quarantinePath);
            CollectionAssert.Contains(remaining, archivePath);
        }

        [Test]
        public void ExactBackupRecoveryIsTwiceVerifiedAndCompletedReloadDoesNotMutate()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string backupBytes = ArrangeExactBackupOnly(
                root,
                fileSystem,
                "C1_STAGE4_RECOVERED");
            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            int tempWritesBefore = fileSystem.GetDurableWriteCount(tempPath);
            int primaryWritesBefore = fileSystem.GetDurableWriteCount(primaryPath);
            int movesBefore = fileSystem.TotalMoveCount;

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Invoke(service, "Load");

            Assert.AreEqual(
                "RecoveredFromBackup",
                GetProperty(service, "LastLoadStatus").ToString());
            Assert.AreEqual(
                "C1_STAGE4_RECOVERED",
                GetField(GetProperty(service, "CurrentSave"), "CurrentChapterId"));
            Assert.Null(GetProperty(service, "ReadOnlyCandidateSnapshot"));
            AssertCanonicalLedger(
                fileSystem,
                root,
                backupBytes,
                backupBytes,
                backupBytes,
                null);
            Assert.AreEqual(
                tempWritesBefore + 1,
                fileSystem.GetDurableWriteCount(tempPath));
            Assert.AreEqual(
                primaryWritesBefore + 1,
                fileSystem.GetDurableWriteCount(primaryPath));
            Assert.AreEqual(movesBefore, fileSystem.TotalMoveCount);
            Assert.AreEqual(
                0,
                fileSystem.GetMutationAttemptCount("Copy", backupPath, tempPath));

            object disposition = GetProperty(service, "LastLoadDisposition");
            Assert.AreEqual("Backup", GetProperty(disposition, "SelectedSource").ToString());
            Assert.AreEqual(
                "SAVE_SELECT_CLEANER_BACKUP",
                GetProperty(disposition, "SelectorReason"));
            Assert.True((bool)GetProperty(disposition, "IsWritable"));
            Assert.True((bool)GetProperty(disposition, "IsRuntimeUsable"));
            Assert.False((bool)GetProperty(disposition, "OfflineProgressApplied"));
            Assert.True((bool)GetProperty(disposition, "DiskChanged"));
            Assert.True((bool)GetProperty(disposition, "RawEvidencePreserved"));

            fileSystem.ClearMutationLedger();
            tempWritesBefore = fileSystem.GetDurableWriteCount(tempPath);
            primaryWritesBefore = fileSystem.GetDurableWriteCount(primaryPath);
            movesBefore = fileSystem.TotalMoveCount;
            Invoke(service, "Load");

            Assert.AreEqual("LoadedPrimary", GetProperty(service, "LastLoadStatus").ToString());
            Assert.True(
                (bool)GetProperty(
                    GetProperty(service, "LastLoadDisposition"),
                    "IsWritable"));
            Assert.AreEqual(0, fileSystem.MutationLedger.Count);
            Assert.AreEqual(tempWritesBefore, fileSystem.GetDurableWriteCount(tempPath));
            Assert.AreEqual(primaryWritesBefore, fileSystem.GetDurableWriteCount(primaryPath));
            Assert.AreEqual(movesBefore, fileSystem.TotalMoveCount);
            AssertCanonicalLedger(
                fileSystem,
                root,
                backupBytes,
                backupBytes,
                backupBytes,
                null);
        }

        [TestCase("save.tmp.json")]
        [TestCase("save.previous.json")]
        public void AuxiliaryEvidenceBlocksMissingPrimaryRecoveryWithoutMutation(
            string artifactFileName)
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string backupBytes = ArrangeExactBackupOnly(
                root,
                fileSystem,
                "C1_STAGE4_AUXILIARY");
            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            string artifactPath = Path.Combine(root, artifactFileName);
            const string artifact = "stage4-auxiliary-evidence";
            fileSystem.Files[artifactPath] = artifact;
            int writesBefore = fileSystem.GetDurableWriteCount(tempPath);
            int movesBefore = fileSystem.TotalMoveCount;

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Invoke(service, "Load");

            Assert.AreEqual("RecoveryRequired", GetProperty(service, "LastLoadStatus").ToString());
            Assert.Null(GetProperty(service, "CurrentSave"));
            Assert.NotNull(GetProperty(service, "ReadOnlyCandidateSnapshot"));
            AssertCanonicalLedger(
                fileSystem,
                root,
                null,
                backupBytes,
                artifactFileName == "save.tmp.json" ? artifact : null,
                artifactFileName == "save.previous.json" ? artifact : null);
            Assert.AreEqual(0, fileSystem.MutationLedger.Count);
            Assert.AreEqual(writesBefore, fileSystem.GetDurableWriteCount(tempPath));
            Assert.AreEqual(movesBefore, fileSystem.TotalMoveCount);
            Assert.False(
                (bool)GetProperty(
                    GetProperty(service, "LastLoadDisposition"),
                    "DiskChanged"));
            Assert.False(fileSystem.FileExists(primaryPath));
            Assert.AreEqual(backupBytes, fileSystem.ReadAllText(backupPath));
            Assert.False(
                artifactFileName == "save.tmp.json" &&
                fileSystem.FileExists(previousPath));
        }

        [Test]
        public void RecoveryStageWriteFailureBeforeMutationCanRetrySafely()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string backupBytes = ArrangeExactBackupOnly(
                root,
                fileSystem,
                "C1_STAGE4_WRITE_RETRY");
            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            fileSystem.WriteFailuresBeforeMutation.Add(tempPath);

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(service, "Load");

            Assert.AreEqual("RecoveryFailed", GetProperty(service, "LastLoadStatus").ToString());
            AssertCanonicalLedger(
                fileSystem,
                root,
                null,
                backupBytes,
                null,
                null);
            Assert.False(
                (bool)GetProperty(
                    GetProperty(service, "LastLoadDisposition"),
                    "DiskChanged"));

            fileSystem.WriteFailuresBeforeMutation.Remove(tempPath);
            Invoke(service, "Load");

            Assert.AreEqual(
                "RecoveredFromBackup",
                GetProperty(service, "LastLoadStatus").ToString());
            AssertCanonicalLedger(
                fileSystem,
                root,
                backupBytes,
                backupBytes,
                backupBytes,
                null);
            Assert.True(fileSystem.FileExists(primaryPath));
            Assert.AreEqual(backupBytes, fileSystem.ReadAllText(backupPath));
            Assert.False(fileSystem.FileExists(previousPath));
        }

        [Test]
        public void RecoveryStageWriteFailureAfterMutationPreservesResidueAndRepeatedLoadIsIdle()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string backupBytes = ArrangeExactBackupOnly(
                root,
                fileSystem,
                "C1_STAGE4_WRITE_RESIDUE");
            string tempPath = Path.Combine(root, "save.tmp.json");
            fileSystem.WriteFailuresAfterMutation.Add(tempPath);

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(service, "Load");

            Assert.AreEqual("RecoveryFailed", GetProperty(service, "LastLoadStatus").ToString());
            Assert.True(fileSystem.FileExists(tempPath));
            string stagedResidue = fileSystem.ReadAllText(tempPath);
            Assert.AreNotEqual(backupBytes, stagedResidue);
            Assert.True(
                (bool)GetProperty(
                    GetProperty(service, "LastLoadDisposition"),
                    "DiskChanged"));
            int writesAfterFailure = fileSystem.GetDurableWriteCount(tempPath);
            int movesAfterFailure = fileSystem.TotalMoveCount;
            fileSystem.ClearMutationLedger();

            Invoke(service, "Load");

            Assert.AreEqual("RecoveryRequired", GetProperty(service, "LastLoadStatus").ToString());
            Assert.AreEqual(stagedResidue, fileSystem.ReadAllText(tempPath));
            Assert.AreEqual(writesAfterFailure, fileSystem.GetDurableWriteCount(tempPath));
            Assert.AreEqual(movesAfterFailure, fileSystem.TotalMoveCount);
            Assert.AreEqual(0, fileSystem.MutationLedger.Count);
        }

        [TestCase(ScriptedReadFaultDisposition.IoFailure)]
        [TestCase(ScriptedReadFaultDisposition.ChangedDuringRead)]
        public void RecoveryStageReadFaultPreservesExactBackupAndStage(
            ScriptedReadFaultDisposition faultDisposition)
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string backupBytes = ArrangeExactBackupOnly(
                root,
                fileSystem,
                "C1_STAGE4_STAGE_READ");
            string tempPath = Path.Combine(root, "save.tmp.json");
            int movesBefore = fileSystem.TotalMoveCount;
            fileSystem.AddReadFault(
                tempPath,
                fileSystem.GetBoundedReadCount(tempPath) + 3,
                faultDisposition);

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(service, "Load");

            Assert.AreEqual("RecoveryFailed", GetProperty(service, "LastLoadStatus").ToString());
            AssertCanonicalLedger(
                fileSystem,
                root,
                null,
                backupBytes,
                backupBytes,
                null);
            Assert.AreEqual(movesBefore, fileSystem.TotalMoveCount);
            Assert.Null(GetProperty(service, "CurrentSave"));
        }

        [Test]
        public void BackupChangingAfterDurableStageIsNeverInstalled()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string backupBytes = ArrangeExactBackupOnly(
                root,
                fileSystem,
                "C1_STAGE4_SOURCE_B0");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string changedBackup = backupBytes.Replace(
                "C1_STAGE4_SOURCE_B0",
                "C1_STAGE4_SOURCE_CHANGED");
            Assert.AreNotEqual(backupBytes, changedBackup);
            int movesBefore = fileSystem.TotalMoveCount;
            int mutationRead = fileSystem.GetBoundedReadCount(backupPath) + 3;
            fileSystem.BoundedReadObserver = (path, count) =>
            {
                if (count == mutationRead &&
                    string.Equals(path, backupPath, StringComparison.OrdinalIgnoreCase))
                {
                    fileSystem.Files[backupPath] = changedBackup;
                }
            };

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(service, "Load");

            Assert.AreEqual("RecoveryFailed", GetProperty(service, "LastLoadStatus").ToString());
            AssertCanonicalLedger(
                fileSystem,
                root,
                null,
                changedBackup,
                backupBytes,
                null);
            Assert.AreEqual(movesBefore, fileSystem.TotalMoveCount);
            Assert.Null(GetProperty(service, "CurrentSave"));
            Assert.True(fileSystem.FileExists(tempPath));
        }

        [TestCase("before", false)]
        [TestCase("partial", false)]
        [TestCase("exact", true)]
        public void RecoveryPrimaryWriteFaultPublishesOnlyACompleteTwiceVerifiedTarget(
            string faultMode,
            bool expectRecovered)
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string backupBytes = ArrangeExactBackupOnly(
                root,
                fileSystem,
                "C1_STAGE4_PRIMARY_WRITE_WINDOW");
            string primaryPath = Path.Combine(root, "save.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            if (faultMode == "before")
            {
                fileSystem.WriteFailuresBeforeMutation.Add(primaryPath);
            }
            else if (faultMode == "partial")
            {
                fileSystem.WriteFailuresAfterMutation.Add(primaryPath);
            }
            else
            {
                fileSystem.WriteFailuresAfterExactMutation.Add(primaryPath);
            }

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(service, "Load");

            Assert.AreEqual(
                expectRecovered ? "RecoveredFromBackup" : "RecoveryFailed",
                GetProperty(service, "LastLoadStatus").ToString());
            string installedPrimary = fileSystem.FileExists(primaryPath)
                ? fileSystem.ReadAllText(primaryPath)
                : null;
            AssertCanonicalLedger(
                fileSystem,
                root,
                expectRecovered
                    ? backupBytes
                    : faultMode == "partial"
                        ? installedPrimary
                        : null,
                backupBytes,
                backupBytes,
                null);
            Assert.AreEqual(
                expectRecovered,
                GetProperty(service, "CurrentSave") != null);
            Assert.True(
                (bool)GetProperty(
                    GetProperty(service, "LastLoadDisposition"),
                    "DiskChanged"),
                "Durable staging changes disk before every primary write window.");

            int tempWritesAfterFailure = fileSystem.GetDurableWriteCount(tempPath);
            int primaryWritesAfterFailure = fileSystem.GetDurableWriteCount(primaryPath);
            fileSystem.ClearMutationLedger();
            Invoke(service, "Load");

            Assert.AreEqual(
                expectRecovered ? "LoadedPrimary" : "RecoveryRequired",
                GetProperty(service, "LastLoadStatus").ToString());
            Assert.AreEqual(
                expectRecovered,
                GetProperty(service, "CurrentSave") != null);
            Assert.AreEqual(
                tempWritesAfterFailure,
                fileSystem.GetDurableWriteCount(tempPath));
            Assert.AreEqual(
                primaryWritesAfterFailure,
                fileSystem.GetDurableWriteCount(primaryPath));
            Assert.AreEqual(0, fileSystem.MutationLedger.Count);
            AssertCanonicalLedger(
                fileSystem,
                root,
                expectRecovered
                    ? backupBytes
                    : faultMode == "partial"
                        ? installedPrimary
                        : null,
                backupBytes,
                backupBytes,
                null);
        }

        [Test]
        public void PrimaryAppearingBeforeRecoveryInstallIsPreservedAndNeverOverwritten()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string backupBytes = ArrangeExactBackupOnly(
                root,
                fileSystem,
                "C1_STAGE4_PRIMARY_RACE_B0");
            string primaryPath = Path.Combine(root, "save.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string foreignPrimary = backupBytes.Replace(
                "C1_STAGE4_PRIMARY_RACE_B0",
                "C1_STAGE4_PRIMARY_RACE_FOREIGN");
            fileSystem.DurableWriteObserver = (path, contents) =>
            {
                if (string.Equals(path, primaryPath, StringComparison.OrdinalIgnoreCase))
                {
                    fileSystem.Files[primaryPath] = foreignPrimary;
                }
            };

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(service, "Load");

            Assert.AreEqual("RecoveryFailed", GetProperty(service, "LastLoadStatus").ToString());
            AssertCanonicalLedger(
                fileSystem,
                root,
                foreignPrimary,
                backupBytes,
                backupBytes,
                null);
            Assert.Null(GetProperty(service, "CurrentSave"));
        }

        [Test]
        public void ValidButDifferentDurableStageIsNeverInstalled()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string backupBytes = ArrangeExactBackupOnly(
                root,
                fileSystem,
                "C1_STAGE4_EXACT_STAGE_B0");
            string primaryPath = Path.Combine(root, "save.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string differentStage = backupBytes.Replace(
                "C1_STAGE4_EXACT_STAGE_B0",
                "C1_STAGE4_EXACT_STAGE_OTHER");
            fileSystem.AfterDurableWriteObserver = (path, contents) =>
            {
                if (string.Equals(path, tempPath, StringComparison.OrdinalIgnoreCase))
                {
                    fileSystem.Files[tempPath] = differentStage;
                }
            };

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(service, "Load");

            Assert.AreEqual("RecoveryFailed", GetProperty(service, "LastLoadStatus").ToString());
            AssertCanonicalLedger(
                fileSystem,
                root,
                null,
                backupBytes,
                differentStage,
                null);
            Assert.AreEqual(0, fileSystem.GetDurableWriteCount(primaryPath));
            Assert.Null(GetProperty(service, "CurrentSave"));
        }

        [TestCase("save.json", 1, "AL-SAVE-BACKUP-RECOVERY-VERIFY-FAILED")]
        [TestCase("save.backup.json", 1, "AL-SAVE-BACKUP-RECOVERY-VERIFY-FAILED")]
        [TestCase("save.tmp.json", 1, "AL-SAVE-BACKUP-RECOVERY-VERIFY-FAILED")]
        [TestCase("save.previous.json", 1, "AL-SAVE-BACKUP-RECOVERY-VERIFY-FAILED")]
        [TestCase("save.json", 2, "AL-SAVE-BACKUP-RECOVERY-REVERIFY-FAILED")]
        [TestCase("save.backup.json", 2, "AL-SAVE-BACKUP-RECOVERY-REVERIFY-FAILED")]
        [TestCase("save.tmp.json", 2, "AL-SAVE-BACKUP-RECOVERY-REVERIFY-FAILED")]
        [TestCase("save.previous.json", 2, "AL-SAVE-BACKUP-RECOVERY-REVERIFY-FAILED")]
        public void RecoveryVerificationReadFaultNeverPublishesUnprovenRuntimeState(
            string faultFileName,
            int verificationOccurrence,
            string expectedDiagnostic)
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string backupBytes = ArrangeExactBackupOnly(
                root,
                fileSystem,
                "C1_STAGE4_FINAL_READ");
            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string faultPath = Path.Combine(root, faultFileName);
            fileSystem.AfterDurableWriteObserver = (path, contents) =>
            {
                if (string.Equals(path, primaryPath, StringComparison.OrdinalIgnoreCase))
                {
                    fileSystem.AddReadFault(
                        faultPath,
                        fileSystem.GetBoundedReadCount(faultPath) + verificationOccurrence,
                        ScriptedReadFaultDisposition.IoFailure);
                }
            };

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(service, "Load");

            Assert.AreEqual("RecoveryFailed", GetProperty(service, "LastLoadStatus").ToString());
            Assert.That(
                (string)GetProperty(service, "LastLoadMessage"),
                Does.StartWith(expectedDiagnostic + ":"));
            Assert.Null(GetProperty(service, "CurrentSave"));
            AssertCanonicalLedger(
                fileSystem,
                root,
                backupBytes,
                backupBytes,
                backupBytes,
                null);

            fileSystem.AfterDurableWriteObserver = null;
            fileSystem.ClearMutationLedger();
            int writesBeforeRetry = fileSystem.GetDurableWriteCount(tempPath);
            int primaryWritesBeforeRetry = fileSystem.GetDurableWriteCount(primaryPath);
            Invoke(service, "Load");

            Assert.AreEqual("LoadedPrimary", GetProperty(service, "LastLoadStatus").ToString());
            Assert.NotNull(GetProperty(service, "CurrentSave"));
            Assert.True(
                (bool)GetProperty(
                    GetProperty(service, "LastLoadDisposition"),
                    "IsWritable"));
            Assert.AreEqual(0, fileSystem.MutationLedger.Count);
            Assert.AreEqual(writesBeforeRetry, fileSystem.GetDurableWriteCount(tempPath));
            Assert.AreEqual(
                primaryWritesBeforeRetry,
                fileSystem.GetDurableWriteCount(primaryPath));
            Assert.AreEqual(backupBytes, fileSystem.ReadAllText(backupPath));
        }

        [TestCase("backup")]
        [TestCase("previous")]
        [TestCase("temp")]
        public void PostInstallAuthorityConflictRemainsBlockedAcrossReload(
            string conflictSource)
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string backupBytes = ArrangeExactBackupOnly(
                root,
                fileSystem,
                "C1_STAGE4_INSTALL_CONFLICT_B0");
            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            string conflictingBytes = backupBytes.Replace(
                "C1_STAGE4_INSTALL_CONFLICT_B0",
                "C1_STAGE4_INSTALL_CONFLICT_OTHER");
            fileSystem.AfterDurableWriteObserver = (path, contents) =>
            {
                if (!string.Equals(path, primaryPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (conflictSource == "backup")
                {
                    fileSystem.Files[backupPath] = conflictingBytes;
                }
                else if (conflictSource == "temp")
                {
                    fileSystem.Files[tempPath] = conflictingBytes;
                }
                else
                {
                    fileSystem.Files[previousPath] = conflictingBytes;
                }
            };

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            InvokeAllowingFailureLogs(service, "Load");

            Assert.AreEqual("RecoveryFailed", GetProperty(service, "LastLoadStatus").ToString());
            Assert.Null(GetProperty(service, "CurrentSave"));
            AssertCanonicalLedger(
                fileSystem,
                root,
                backupBytes,
                conflictSource == "backup" ? conflictingBytes : backupBytes,
                conflictSource == "temp" ? conflictingBytes : backupBytes,
                conflictSource == "previous" ? conflictingBytes : null);

            fileSystem.AfterDurableWriteObserver = null;
            int tempWritesBeforeReload = fileSystem.GetDurableWriteCount(tempPath);
            int primaryWritesBeforeReload = fileSystem.GetDurableWriteCount(primaryPath);
            fileSystem.ClearMutationLedger();
            Invoke(service, "Load");

            Assert.AreEqual("RecoveryRequired", GetProperty(service, "LastLoadStatus").ToString());
            Assert.Null(GetProperty(service, "CurrentSave"));
            Assert.NotNull(GetProperty(service, "ReadOnlyCandidateSnapshot"));
            Assert.AreEqual(
                tempWritesBeforeReload,
                fileSystem.GetDurableWriteCount(tempPath));
            Assert.AreEqual(
                primaryWritesBeforeReload,
                fileSystem.GetDurableWriteCount(primaryPath));
            Assert.AreEqual(0, fileSystem.MutationLedger.Count);
            AssertCanonicalLedger(
                fileSystem,
                root,
                backupBytes,
                conflictSource == "backup" ? conflictingBytes : backupBytes,
                conflictSource == "temp" ? conflictingBytes : backupBytes,
                conflictSource == "previous" ? conflictingBytes : null);
        }

        [Test]
        public void RecoveredLoadStatusAndDispositionSurviveSubsequentSave()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            ArrangeExactBackupOnly(root, fileSystem, "C1_STAGE4_STATUS");
            string primaryPath = Path.Combine(root, "save.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Invoke(service, "Load");
            object loadDisposition = GetProperty(service, "LastLoadDisposition");
            string loadMessage = (string)GetProperty(service, "LastLoadMessage");
            object currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_STAGE4_STATUS_SAVED");

            Invoke(service, "Save");

            Assert.AreEqual("SavedPrimary", GetProperty(service, "LastSaveStatus").ToString());
            Assert.AreEqual(
                "RecoveredFromBackup",
                GetProperty(service, "LastLoadStatus").ToString());
            Assert.AreEqual(loadMessage, GetProperty(service, "LastLoadMessage"));
            Assert.AreSame(loadDisposition, GetProperty(service, "LastLoadDisposition"));
            Assert.False(
                (bool)GetProperty(loadDisposition, "OfflineProgressApplied"));
            Assert.That(
                fileSystem.ReadAllText(primaryPath),
                Does.Contain("C1_STAGE4_STATUS_SAVED"));
            Assert.False(fileSystem.FileExists(tempPath));
            Assert.False(fileSystem.FileExists(previousPath));
        }

        [TestCase("temp")]
        [TestCase("previous")]
        public void RecoveryWitnessDriftBeforeSaveFreezesWithoutDeletingEvidence(
            string driftSource)
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string backupBytes = ArrangeExactBackupOnly(
                root,
                fileSystem,
                "C1_STAGE4_SAVE_PREFLIGHT_B0");
            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Invoke(service, "Load");
            string conflictingBytes = backupBytes.Replace(
                "C1_STAGE4_SAVE_PREFLIGHT_B0",
                "C1_STAGE4_SAVE_PREFLIGHT_OTHER");
            if (driftSource == "temp")
            {
                fileSystem.Files[tempPath] = conflictingBytes;
            }
            else
            {
                fileSystem.Files[previousPath] = conflictingBytes;
            }

            fileSystem.ClearMutationLedger();
            int tempWritesBeforeSave = fileSystem.GetDurableWriteCount(tempPath);
            object currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_STAGE4_MUST_NOT_SAVE");
            InvokeAllowingFailureLogs(service, "Save");

            Assert.AreEqual(
                "SaveFailedPreviousPreserved",
                GetProperty(service, "LastSaveStatus").ToString());
            Assert.That(
                (string)GetProperty(service, "LastSaveMessage"),
                Does.StartWith("AL-SAVE-RECOVERY-WITNESS-CHANGED:"));
            Assert.Null(GetProperty(service, "CurrentSave"));
            Assert.NotNull(GetProperty(service, "ReadOnlyCandidateSnapshot"));
            AssertCanonicalLedger(
                fileSystem,
                root,
                backupBytes,
                backupBytes,
                driftSource == "temp" ? conflictingBytes : backupBytes,
                driftSource == "previous" ? conflictingBytes : null);
            Assert.AreEqual(0, fileSystem.MutationLedger.Count);
            Assert.AreEqual(
                tempWritesBeforeSave,
                fileSystem.GetDurableWriteCount(tempPath));
            Assert.That(
                fileSystem.ReadAllText(primaryPath),
                Does.Not.Contain("C1_STAGE4_MUST_NOT_SAVE"));
            Assert.AreEqual(backupBytes, fileSystem.ReadAllText(backupPath));
        }

        [Test]
        public void RecoveryWitnessDriftAtConsumptionBoundaryIsPreserved()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string backupBytes = ArrangeExactBackupOnly(
                root,
                fileSystem,
                "C1_STAGE4_SAVE_BOUNDARY_B0");
            string primaryPath = Path.Combine(root, "save.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Invoke(service, "Load");
            string conflictingTemp = backupBytes.Replace(
                "C1_STAGE4_SAVE_BOUNDARY_B0",
                "C1_STAGE4_SAVE_BOUNDARY_OTHER");
            int boundaryRead = fileSystem.GetBoundedReadCount(primaryPath) + 4;
            fileSystem.BoundedReadObserver = (path, count) =>
            {
                if (count == boundaryRead &&
                    string.Equals(path, primaryPath, StringComparison.OrdinalIgnoreCase))
                {
                    fileSystem.Files[tempPath] = conflictingTemp;
                }
            };
            fileSystem.ClearMutationLedger();
            object currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_STAGE4_BOUNDARY_MUST_NOT_SAVE");

            InvokeAllowingFailureLogs(service, "Save");

            Assert.AreEqual(
                "SaveFailedPreviousPreserved",
                GetProperty(service, "LastSaveStatus").ToString());
            Assert.That(
                (string)GetProperty(service, "LastSaveMessage"),
                Does.StartWith("AL-SAVE-RECOVERY-WITNESS-CONSUME-BLOCKED:"));
            Assert.Null(GetProperty(service, "CurrentSave"));
            AssertCanonicalLedger(
                fileSystem,
                root,
                backupBytes,
                backupBytes,
                conflictingTemp,
                null);
            Assert.AreEqual(0, fileSystem.MutationLedger.Count);
        }

        [Test]
        public void InaccessiblePrimaryFailsClosedWithoutSelectingOrChangingBackup()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Invoke(service, "CreateNewSave", Enum.Parse(realmType, "None"));

            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string primaryBefore = fileSystem.ReadAllText(primaryPath);
            string backupBefore = fileSystem.ReadAllText(backupPath);
            object currentBefore = GetProperty(service, "CurrentSave");
            int movesBefore = fileSystem.TotalMoveCount;
            fileSystem.BoundedReadIoFailures.Add(primaryPath);

            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("AL-SAVE-PRIMARY-UNREADABLE"));
            Invoke(service, "Load");

            Assert.AreEqual("RecoveryFailed", GetProperty(service, "LastLoadStatus").ToString());
            Assert.Null(GetProperty(service, "CurrentSave"));
            Assert.NotNull(GetProperty(service, "ReadOnlyCandidateSnapshot"));
            Assert.AreNotSame(currentBefore, GetProperty(service, "ReadOnlyCandidateSnapshot"));
            Assert.AreEqual(primaryBefore, fileSystem.ReadAllText(primaryPath));
            Assert.AreEqual(backupBefore, fileSystem.ReadAllText(backupPath));
            Assert.AreEqual(movesBefore, fileSystem.TotalMoveCount);
        }

        [Test]
        public void PrimaryChangingDuringReadFailsClosedAndPreservesEveryGeneration()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Invoke(service, "CreateNewSave", Enum.Parse(realmType, "None"));

            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string primaryBefore = fileSystem.ReadAllText(primaryPath);
            string backupBefore = fileSystem.ReadAllText(backupPath);
            int movesBefore = fileSystem.TotalMoveCount;
            fileSystem.ChangedDuringReadPaths.Add(primaryPath);

            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("AL-SAVE-PRIMARY-UNREADABLE"));
            Invoke(service, "Load");

            Assert.AreEqual("RecoveryFailed", GetProperty(service, "LastLoadStatus").ToString());
            Assert.AreEqual(primaryBefore, fileSystem.ReadAllText(primaryPath));
            Assert.AreEqual(backupBefore, fileSystem.ReadAllText(backupPath));
            Assert.AreEqual(movesBefore, fileSystem.TotalMoveCount);
        }

        [Test]
        public void AllMissingLoadDoesNotReplaceBackupThatAppearsAfterInventory()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var seedFileSystem = new ScriptedSaveFileOperations();
            object seedService = CreateSaveService(
                root,
                CreateFileOperationsProxy(seedFileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Invoke(seedService, "CreateNewSave", Enum.Parse(realmType, "None"));
            string backupPath = Path.Combine(root, "save.backup.json");
            string validBackup = seedFileSystem.ReadAllText(backupPath);

            var fileSystem = new ScriptedSaveFileOperations();
            string tempPath = Path.Combine(root, "save.tmp.json");
            fileSystem.BoundedReadObserver = (path, count) =>
            {
                if (string.Equals(path, tempPath, StringComparison.OrdinalIgnoreCase) &&
                    count == 1)
                {
                    fileSystem.Files[backupPath] = validBackup;
                }
            };

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("^AL-SAVE-CREATE-FAILED:"));
            Invoke(service, "Load");

            Assert.AreEqual("RecoveryFailed", GetProperty(service, "LastLoadStatus").ToString());
            Assert.Null(GetProperty(service, "CurrentSave"));
            Assert.AreEqual(validBackup, fileSystem.ReadAllText(backupPath));
            Assert.False(fileSystem.FileExists(Path.Combine(root, "save.json")));
            Assert.False(fileSystem.FileExists(tempPath));
            Assert.False(fileSystem.FileExists(Path.Combine(root, "save.previous.json")));
            Assert.False(
                (bool)GetProperty(
                    GetProperty(service, "LastLoadDisposition"),
                    "DiskChanged"));
        }

        [Test]
        public void AllMissingLoadReportsDiskChangeWhenTempWritePrecedesFailure()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string tempPath = Path.Combine(root, "save.tmp.json");
            fileSystem.BoundedReadObserver = (path, count) =>
            {
                if (string.Equals(path, tempPath, StringComparison.OrdinalIgnoreCase) &&
                    count == 3)
                {
                    fileSystem.ChangedDuringReadPaths.Add(tempPath);
                }
            };

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("^AL-SAVE-CREATE-FAILED:"));
            Invoke(service, "Load");

            Assert.AreEqual("RecoveryFailed", GetProperty(service, "LastLoadStatus").ToString());
            Assert.Null(GetProperty(service, "CurrentSave"));
            Assert.True(fileSystem.FileExists(tempPath));
            Assert.False(fileSystem.FileExists(Path.Combine(root, "save.json")));
            Assert.False(fileSystem.FileExists(Path.Combine(root, "save.backup.json")));
            Assert.True(
                (bool)GetProperty(
                    GetProperty(service, "LastLoadDisposition"),
                    "DiskChanged"));
        }

        [Test]
        public void AllMissingLoadReportsNoDiskChangeWhenDurableWriteFailsBeforeCreate()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string tempPath = Path.Combine(root, "save.tmp.json");
            fileSystem.WriteFailuresBeforeMutation.Add(tempPath);

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("^AL-SAVE-CREATE-FAILED:"));
            Invoke(service, "Load");

            Assert.AreEqual("RecoveryFailed", GetProperty(service, "LastLoadStatus").ToString());
            Assert.False(fileSystem.FileExists(tempPath));
            Assert.False(
                (bool)GetProperty(
                    GetProperty(service, "LastLoadDisposition"),
                    "DiskChanged"));
        }

        [Test]
        public void AllMissingLoadReportsDiskChangeWhenDurableWriteFailsAfterCreate()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            string tempPath = Path.Combine(root, "save.tmp.json");
            fileSystem.WriteFailuresAfterMutation.Add(tempPath);

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("^AL-SAVE-CREATE-FAILED:"));
            Invoke(service, "Load");

            Assert.AreEqual("RecoveryFailed", GetProperty(service, "LastLoadStatus").ToString());
            Assert.True(fileSystem.FileExists(tempPath));
            Assert.True(
                (bool)GetProperty(
                    GetProperty(service, "LastLoadDisposition"),
                    "DiskChanged"));
        }

        [Test]
        public void AllMissingLoadDoesNotInstallPrimaryWhenBackupAppearsAtCommit()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var seedFileSystem = new ScriptedSaveFileOperations();
            object seedService = CreateSaveService(
                root,
                CreateFileOperationsProxy(seedFileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Invoke(seedService, "CreateNewSave", Enum.Parse(realmType, "None"));
            string backupPath = Path.Combine(root, "save.backup.json");
            string validBackup = seedFileSystem.ReadAllText(backupPath);

            var fileSystem = new ScriptedSaveFileOperations();
            string tempPath = Path.Combine(root, "save.tmp.json");
            fileSystem.CopyObserver = (sourcePath, destinationPath, overwrite) =>
            {
                if (!overwrite &&
                    string.Equals(destinationPath, backupPath, StringComparison.OrdinalIgnoreCase))
                {
                    fileSystem.Files[backupPath] = validBackup;
                }
            };

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("^AL-SAVE-CREATE-FAILED:"));
            Invoke(service, "Load");

            Assert.AreEqual("RecoveryFailed", GetProperty(service, "LastLoadStatus").ToString());
            Assert.Null(GetProperty(service, "CurrentSave"));
            Assert.False(fileSystem.FileExists(Path.Combine(root, "save.json")));
            Assert.AreEqual(validBackup, fileSystem.ReadAllText(backupPath));
            Assert.True(fileSystem.FileExists(tempPath));
            Assert.True(
                (bool)GetProperty(
                    GetProperty(service, "LastLoadDisposition"),
                    "DiskChanged"));
        }

        [Test]
        public void AllMissingLoadDoesNotInstallPrimaryWhenPreviousAppearsAfterBackupCopy()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var seedFileSystem = new ScriptedSaveFileOperations();
            object seedService = CreateSaveService(
                root,
                CreateFileOperationsProxy(seedFileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Invoke(seedService, "CreateNewSave", Enum.Parse(realmType, "None"));
            string seededPrimaryPath = Path.Combine(root, "save.json");
            string authenticPrevious = seedFileSystem.ReadAllText(seededPrimaryPath);

            var fileSystem = new ScriptedSaveFileOperations();
            string backupPath = Path.Combine(root, "save.backup.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            fileSystem.AfterCopyObserver = (sourcePath, destinationPath, overwrite) =>
            {
                if (!overwrite &&
                    string.Equals(destinationPath, backupPath, StringComparison.OrdinalIgnoreCase))
                {
                    fileSystem.Files[previousPath] = authenticPrevious;
                }
            };

            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("^AL-SAVE-CREATE-FAILED:"));
            Invoke(service, "Load");

            Assert.AreEqual("RecoveryFailed", GetProperty(service, "LastLoadStatus").ToString());
            Assert.False(fileSystem.FileExists(Path.Combine(root, "save.json")));
            Assert.False(fileSystem.FileExists(backupPath));
            Assert.AreEqual(authenticPrevious, fileSystem.ReadAllText(previousPath));
            Assert.True(fileSystem.FileExists(tempPath));
        }

        [Test]
        public void MissingBackupIsRecreatedFromExactPriorPrimary()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Invoke(service, "CreateNewSave", Enum.Parse(realmType, "None"));

            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            object currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_PRIOR_PRIMARY");
            Invoke(service, "Save");
            string priorPrimary = fileSystem.ReadAllText(primaryPath);
            fileSystem.Files.Remove(backupPath);

            currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_NEW_PRIMARY");
            Invoke(service, "Save");

            Assert.AreEqual("SavedPrimary", GetProperty(service, "LastSaveStatus").ToString());
            Assert.AreEqual(priorPrimary, fileSystem.ReadAllText(backupPath));
            Assert.That(fileSystem.ReadAllText(primaryPath), Does.Contain("C1_NEW_PRIMARY"));
            Assert.False(fileSystem.FileExists(Path.Combine(root, "save.tmp.json")));
            Assert.False(fileSystem.FileExists(Path.Combine(root, "save.previous.json")));
        }

        [Test]
        public void PostRotationCleanupFailurePreservesPriorBackupAndReportsFailure()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Invoke(service, "CreateNewSave", Enum.Parse(realmType, "None"));

            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            string priorPrimary = fileSystem.ReadAllText(primaryPath);
            string priorBackup = fileSystem.ReadAllText(backupPath);
            object currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_CLEANUP_FAILURE");
            fileSystem.ReplaceObserver = (sourcePath, destinationPath, rollbackPath) =>
            {
                if (string.Equals(destinationPath, backupPath, StringComparison.OrdinalIgnoreCase))
                {
                    fileSystem.DeleteFailures.Add(previousPath);
                }
            };

            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("AL-SAVE-DELETE-FAILED:.*save.previous.json"));
            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("^AL-SAVE-BACKUP-CLEANUP-FAILED:"));
            Invoke(service, "Save");

            Assert.AreEqual(
                "CommitUncertain",
                GetProperty(service, "LastSaveStatus").ToString());
            string candidatePrimary = fileSystem.ReadAllText(primaryPath);
            Assert.That(candidatePrimary, Does.Contain("C1_CLEANUP_FAILURE"));
            AssertCanonicalLedger(
                fileSystem,
                root,
                candidatePrimary,
                priorPrimary,
                null,
                priorBackup);
            AssertSaveDisposition(
                service,
                "CommitUncertain",
                true,
                candidatePrimaryVerified: true,
                requiredBackupVerified: true,
                cleanupVerified: false);
        }

        [Test]
        public void PrimaryChangedAfterValidationNeverRotatesIntoBackup()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Invoke(service, "CreateNewSave", Enum.Parse(realmType, "None"));

            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            string backupBefore = fileSystem.ReadAllText(backupPath);
            object currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_NEW_VALIDATED");
            fileSystem.ReplaceObserver = (sourcePath, destinationPath, rollbackPath) =>
            {
                if (string.Equals(sourcePath, tempPath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(destinationPath, primaryPath, StringComparison.OrdinalIgnoreCase))
                {
                    fileSystem.Files[primaryPath] = "{ changed after validation";
                }
            };

            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("^AL-SAVE-BACKUP-ROTATION-EVIDENCE-MISSING:"));
            Invoke(service, "Save");

            Assert.AreEqual(
                "CommitUncertain",
                GetProperty(service, "LastSaveStatus").ToString());
            Assert.AreEqual(backupBefore, fileSystem.ReadAllText(backupPath));
            Assert.That(fileSystem.ReadAllText(primaryPath), Does.Contain("C1_NEW_VALIDATED"));
            Assert.False(fileSystem.FileExists(tempPath));
            Assert.That(fileSystem.ReadAllText(previousPath), Does.Contain("changed after validation"));
            AssertSaveDisposition(
                service,
                "CommitUncertain",
                true,
                candidatePrimaryVerified: true,
                requiredBackupVerified: true,
                cleanupVerified: false);
        }

        [Test]
        public void StalePreviousCleanupFailureStopsInstallWithoutDeletingPrimary()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Invoke(service, "CreateNewSave", Enum.Parse(realmType, "None"));

            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            string primaryBefore = fileSystem.ReadAllText(primaryPath);
            string backupBefore = fileSystem.ReadAllText(backupPath);
            fileSystem.Files[previousPath] = "previous-sentinel";
            fileSystem.DeleteFailures.Add(previousPath);
            object currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_NEW_CANDIDATE");

            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("AL-SAVE-DELETE-FAILED:.*save.previous.json"));
            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("^AL-SAVE-PREVIOUS-CLEANUP-FAILED:"));
            Invoke(service, "Save");

            Assert.AreEqual(
                "SaveFailedPreviousPreserved",
                GetProperty(service, "LastSaveStatus").ToString());
            Assert.AreEqual(primaryBefore, fileSystem.ReadAllText(primaryPath));
            Assert.AreEqual(backupBefore, fileSystem.ReadAllText(backupPath));
            Assert.AreEqual("previous-sentinel", fileSystem.ReadAllText(previousPath));
        }

        [Test]
        public void SaveOperationStatusContractIsAppendOnlyAndSuccessfulSavePublishesDisposition()
        {
            Type statusType = GetRuntimeType("AL.Core.Interfaces.SaveOperationStatus");
            Assert.AreEqual(0, Convert.ToInt32(Enum.Parse(statusType, "None")));
            Assert.AreEqual(1, Convert.ToInt32(Enum.Parse(statusType, "SavedPrimary")));
            Assert.AreEqual(
                2,
                Convert.ToInt32(Enum.Parse(statusType, "SaveFailedPreviousPreserved")));
            Assert.AreEqual(3, Convert.ToInt32(Enum.Parse(statusType, "DeleteFailed")));
            Assert.AreEqual(4, Convert.ToInt32(Enum.Parse(statusType, "CommitUncertain")));

            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Type dispositionProviderType =
                GetRuntimeType("AL.Core.Interfaces.ISaveOperationDispositionProvider");
            Assert.True(
                dispositionProviderType.IsAssignableFrom(service.GetType()),
                "The save service must expose the typed Stage-3 save disposition.");

            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Invoke(service, "CreateNewSave", Enum.Parse(realmType, "None"));

            Assert.AreEqual(
                "SavedPrimary",
                GetProperty(service, "LastSaveStatus").ToString());
            AssertSaveDisposition(
                service,
                "SavedPrimary",
                true,
                candidatePrimaryVerified: true,
                requiredBackupVerified: true,
                cleanupVerified: true,
                rollbackAttempted: false,
                rollbackVerified: false);
        }

        [Test]
        public void AtomicReplaceAfterMutationWithUnverifiedRollbackIsCommitUncertain()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Invoke(service, "CreateNewSave", Enum.Parse(realmType, "None"));

            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            object currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_PRIOR_ATOMIC");
            Invoke(service, "Save");
            string priorPrimary = fileSystem.ReadAllText(primaryPath);
            string priorBackup = fileSystem.ReadAllText(backupPath);

            currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_ATOMIC_AFTER_MUTATION");
            string candidatePrimary = null;
            fileSystem.MutationObserver = (operation, sourcePath, destinationPath, timing) =>
            {
                if (operation == "Replace" &&
                    timing == ScriptedFaultTiming.BeforeMutation &&
                    string.Equals(sourcePath, tempPath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(destinationPath, primaryPath, StringComparison.OrdinalIgnoreCase))
                {
                    candidatePrimary = fileSystem.ReadAllText(tempPath);
                }
            };
            fileSystem.AddMutationFault(
                "Replace",
                tempPath,
                primaryPath,
                ScriptedFaultTiming.AfterMutation,
                ScriptedFaultException.Io);
            fileSystem.AddMutationFault(
                "Copy",
                previousPath,
                primaryPath,
                ScriptedFaultTiming.BeforeMutation,
                ScriptedFaultException.Io);
            fileSystem.ClearMutationLedger();

            InvokeAllowingFailureLogs(service, "Save");

            Assert.NotNull(candidatePrimary);
            Assert.AreEqual(
                "CommitUncertain",
                GetProperty(service, "LastSaveStatus").ToString());
            AssertCanonicalLedger(
                fileSystem,
                root,
                candidatePrimary,
                priorBackup,
                null,
                priorPrimary);
            AssertSaveDisposition(
                service,
                "CommitUncertain",
                true,
                candidatePrimaryVerified: true,
                requiredBackupVerified: false,
                cleanupVerified: false);
            Assert.AreEqual(
                1,
                fileSystem.GetMutationAttemptCount("Replace", tempPath, primaryPath));
        }

        [Test]
        public void MoveFallbackWindowClaimsPreviousPreservedOnlyAfterVerifiedRollback()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Invoke(service, "CreateNewSave", Enum.Parse(realmType, "None"));

            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            string priorPrimary = fileSystem.ReadAllText(primaryPath);
            string priorBackup = fileSystem.ReadAllText(backupPath);
            object currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_MOVE_FALLBACK_WINDOW");
            string candidatePrimary = null;
            fileSystem.MutationObserver = (operation, sourcePath, destinationPath, timing) =>
            {
                if (operation == "Move" &&
                    timing == ScriptedFaultTiming.BeforeMutation &&
                    string.Equals(sourcePath, tempPath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(destinationPath, primaryPath, StringComparison.OrdinalIgnoreCase))
                {
                    candidatePrimary = fileSystem.ReadAllText(tempPath);
                }
            };
            fileSystem.AddMutationFault(
                "Replace",
                tempPath,
                primaryPath,
                ScriptedFaultTiming.BeforeMutation,
                ScriptedFaultException.NotSupported);
            fileSystem.AddMutationFault(
                "Move",
                tempPath,
                primaryPath,
                ScriptedFaultTiming.BeforeMutation,
                ScriptedFaultException.Io);
            fileSystem.AddMutationFault(
                "Copy",
                previousPath,
                primaryPath,
                ScriptedFaultTiming.AfterMutation,
                ScriptedFaultException.Io);
            fileSystem.ClearMutationLedger();

            InvokeAllowingFailureLogs(service, "Save");

            Assert.NotNull(candidatePrimary);
            Assert.AreEqual(
                "SaveFailedPreviousPreserved",
                GetProperty(service, "LastSaveStatus").ToString());
            AssertCanonicalLedger(
                fileSystem,
                root,
                priorPrimary,
                priorBackup,
                candidatePrimary,
                priorPrimary);
            AssertSaveDisposition(
                service,
                "SaveFailedPreviousPreserved",
                true,
                candidatePrimaryVerified: false,
                previousAuthorityVerified: true,
                rollbackAttempted: true,
                rollbackVerified: true);
            Assert.AreEqual(
                "C1",
                GetField(GetProperty(service, "CurrentSave"), "CurrentChapterId"),
                "A proven previous-preserved result must republish exact P0 runtime state.");
            Assert.AreEqual(
                1,
                fileSystem.GetMutationAttemptCount("Move", primaryPath, previousPath));
            Assert.AreEqual(
                1,
                fileSystem.GetMutationAttemptCount("Move", tempPath, primaryPath));
            Assert.AreEqual(
                1,
                fileSystem.GetMutationAttemptCount("Copy", previousPath, primaryPath));
        }

        [Test]
        public void FirstGenerationMoveAfterMutationFreezesRepeatedSaveAsCommitUncertain()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            string candidatePrimary = null;
            fileSystem.MutationObserver = (operation, sourcePath, destinationPath, timing) =>
            {
                if (operation == "Move" &&
                    timing == ScriptedFaultTiming.BeforeMutation &&
                    string.Equals(sourcePath, tempPath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(destinationPath, primaryPath, StringComparison.OrdinalIgnoreCase))
                {
                    candidatePrimary = fileSystem.ReadAllText(tempPath);
                }
            };
            fileSystem.AddMutationFault(
                "Move",
                tempPath,
                primaryPath,
                ScriptedFaultTiming.AfterMutation,
                ScriptedFaultException.Io);

            Type realmType = GetRuntimeType("AL.Core.RealmId");
            InvokeAllowingFailureLogs(
                service,
                "CreateNewSave",
                Enum.Parse(realmType, "None"));

            Assert.NotNull(candidatePrimary);
            Assert.AreEqual(
                "CommitUncertain",
                GetProperty(service, "LastSaveStatus").ToString());
            AssertCanonicalLedger(
                fileSystem,
                root,
                candidatePrimary,
                null,
                null,
                null);
            AssertSaveDisposition(
                service,
                "CommitUncertain",
                true,
                candidatePrimaryVerified: true,
                requiredBackupVerified: false);
            Assert.Null(
                GetProperty(service, "CurrentSave"),
                "A first-generation uncertain candidate must not become public runtime authority.");

            int mutationCountAfterUncertainty = fileSystem.MutationLedger.Count;
            object firstDisposition = GetProperty(service, "LastSaveDisposition");
            IReadOnlyList<string> firstDiagnosticCodes =
                GetDiagnosticCodes(firstDisposition);

            InvokeAllowingFailureLogs(service, "Save");

            Assert.AreEqual(
                mutationCountAfterUncertainty,
                fileSystem.MutationLedger.Count,
                "Commit uncertainty must freeze repeated persistence attempts.");
            Assert.AreEqual(
                "CommitUncertain",
                GetProperty(service, "LastSaveStatus").ToString());
            CollectionAssert.AreEqual(
                firstDiagnosticCodes,
                GetDiagnosticCodes(
                    GetProperty(service, "LastSaveDisposition")));
            AssertCanonicalLedger(
                fileSystem,
                root,
                candidatePrimary,
                null,
                null,
                null);
        }

        [Test]
        public void SecondCommitVerificationReadFailurePublishesUncertaintyDiagnostic()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Invoke(service, "CreateNewSave", Enum.Parse(realmType, "None"));

            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            object currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_SECOND_VERIFY_P0");
            Invoke(service, "Save");
            string priorPrimary = fileSystem.ReadAllText(primaryPath);

            currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_SECOND_VERIFY_N");
            string candidatePrimary = null;
            int previousDeleteCompletions = 0;
            fileSystem.MutationObserver =
                (operation, sourcePath, destinationPath, timing) =>
                {
                    if (operation == "Replace" &&
                        timing == ScriptedFaultTiming.BeforeMutation &&
                        string.Equals(sourcePath, tempPath, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(destinationPath, primaryPath, StringComparison.OrdinalIgnoreCase))
                    {
                        candidatePrimary = fileSystem.ReadAllText(tempPath);
                    }

                    if (operation == "Delete" &&
                        timing == ScriptedFaultTiming.AfterMutation &&
                        string.Equals(sourcePath, previousPath, StringComparison.OrdinalIgnoreCase))
                    {
                        previousDeleteCompletions++;
                        if (previousDeleteCompletions == 3)
                        {
                            fileSystem.AddReadFault(
                                primaryPath,
                                fileSystem.GetBoundedReadCount(primaryPath) + 2,
                                ScriptedReadFaultDisposition.IoFailure);
                        }
                    }
                };

            InvokeAllowingFailureLogs(service, "Save");

            Assert.NotNull(candidatePrimary);
            Assert.AreEqual(
                "CommitUncertain",
                GetProperty(service, "LastSaveStatus").ToString());
            Assert.That(
                (string)GetProperty(service, "LastSaveMessage"),
                Does.StartWith("AL-SAVE-COMMIT-REVERIFY-FAILED:"));
            CollectionAssert.Contains(
                GetDiagnosticCodes(GetProperty(service, "LastSaveDisposition")),
                "AL-SAVE-COMMIT-REVERIFY-FAILED");
            AssertCanonicalLedger(
                fileSystem,
                root,
                candidatePrimary,
                priorPrimary,
                null,
                null);
            AssertSaveDisposition(
                service,
                "CommitUncertain",
                true,
                candidatePrimaryVerified: true,
                requiredBackupVerified: true,
                cleanupVerified: true);
            Assert.AreEqual(
                "C1_SECOND_VERIFY_P0",
                GetField(GetProperty(service, "CurrentSave"), "CurrentChapterId"));
        }

        [Test]
        public void BackupReplaceAfterMutationNeverRunsDestructiveMoveFallback()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Invoke(service, "CreateNewSave", Enum.Parse(realmType, "None"));

            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            object currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_BACKUP_REPLACE_P0");
            Invoke(service, "Save");
            string priorPrimary = fileSystem.ReadAllText(primaryPath);
            string priorBackup = fileSystem.ReadAllText(backupPath);

            currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_BACKUP_REPLACE_N");
            string candidatePrimary = null;
            fileSystem.MutationObserver =
                (operation, sourcePath, destinationPath, timing) =>
                {
                    if (operation == "Replace" &&
                        timing == ScriptedFaultTiming.BeforeMutation &&
                        string.Equals(sourcePath, tempPath, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(destinationPath, primaryPath, StringComparison.OrdinalIgnoreCase))
                    {
                        candidatePrimary = fileSystem.ReadAllText(tempPath);
                    }
                };
            fileSystem.AddMutationFault(
                "Replace",
                tempPath,
                backupPath,
                ScriptedFaultTiming.AfterMutation,
                ScriptedFaultException.NotSupported);
            fileSystem.ClearMutationLedger();

            InvokeAllowingFailureLogs(service, "Save");

            Assert.NotNull(candidatePrimary);
            Assert.AreEqual(
                "CommitUncertain",
                GetProperty(service, "LastSaveStatus").ToString());
            Assert.That(
                (string)GetProperty(service, "LastSaveMessage"),
                Does.StartWith("AL-SAVE-BACKUP-REPLACE-UNSUPPORTED-UNCERTAIN:"));
            AssertCanonicalLedger(
                fileSystem,
                root,
                candidatePrimary,
                priorPrimary,
                null,
                priorBackup);
            AssertSaveDisposition(
                service,
                "CommitUncertain",
                true,
                candidatePrimaryVerified: true,
                requiredBackupVerified: true,
                cleanupVerified: false);
            Assert.AreEqual(
                0,
                fileSystem.GetMutationAttemptCount(
                    "Move",
                    backupPath,
                    previousPath));
            Assert.AreEqual(
                "C1_BACKUP_REPLACE_P0",
                GetField(GetProperty(service, "CurrentSave"), "CurrentChapterId"));
        }

        [Test]
        public void ValidButDifferentFinalBackupRetainsCanonicalResidue()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Invoke(service, "CreateNewSave", Enum.Parse(realmType, "None"));

            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            object currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_FINAL_EXACT_P0");
            Invoke(service, "Save");
            string priorPrimary = fileSystem.ReadAllText(primaryPath);
            string priorBackup = fileSystem.ReadAllText(backupPath);

            currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_FINAL_EXACT_N");
            string candidatePrimary = null;
            fileSystem.MutationObserver =
                (operation, sourcePath, destinationPath, timing) =>
                {
                    if (operation != "Replace")
                    {
                        return;
                    }

                    if (timing == ScriptedFaultTiming.BeforeMutation &&
                        string.Equals(sourcePath, tempPath, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(destinationPath, primaryPath, StringComparison.OrdinalIgnoreCase))
                    {
                        candidatePrimary = fileSystem.ReadAllText(tempPath);
                    }

                    if (timing == ScriptedFaultTiming.AfterMutation &&
                        string.Equals(sourcePath, tempPath, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(destinationPath, backupPath, StringComparison.OrdinalIgnoreCase))
                    {
                        int finalBackupRead =
                            fileSystem.GetBoundedReadCount(backupPath) + 2;
                        fileSystem.BoundedReadObserver = (path, count) =>
                        {
                            if (count == finalBackupRead &&
                                string.Equals(path, backupPath, StringComparison.OrdinalIgnoreCase))
                            {
                                fileSystem.Files[backupPath] = priorBackup;
                            }
                        };
                    }
                };

            InvokeAllowingFailureLogs(service, "Save");

            Assert.NotNull(candidatePrimary);
            Assert.AreEqual(
                "CommitUncertain",
                GetProperty(service, "LastSaveStatus").ToString());
            Assert.That(
                (string)GetProperty(service, "LastSaveMessage"),
                Does.StartWith("AL-SAVE-FINAL-BACKUP-INVALID:"));
            AssertCanonicalLedger(
                fileSystem,
                root,
                candidatePrimary,
                priorBackup,
                null,
                priorBackup);
            AssertSaveDisposition(
                service,
                "CommitUncertain",
                true,
                candidatePrimaryVerified: true,
                requiredBackupVerified: false,
                cleanupVerified: false);
            Assert.AreEqual(
                "C1_FINAL_EXACT_P0",
                GetField(GetProperty(service, "CurrentSave"), "CurrentChapterId"));
        }

        [Test]
        public void ValidButDifferentBackupStageNeverDeletesExactPriorPrimary()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-SaveTests",
                Guid.NewGuid().ToString("N"));
            var fileSystem = new ScriptedSaveFileOperations();
            object service = CreateSaveService(root, CreateFileOperationsProxy(fileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Invoke(service, "CreateNewSave", Enum.Parse(realmType, "None"));

            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            object currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_EXACT_STAGE_P0");
            Invoke(service, "Save");
            string priorPrimary = fileSystem.ReadAllText(primaryPath);
            string priorBackup = fileSystem.ReadAllText(backupPath);

            currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_EXACT_STAGE_N");
            string candidatePrimary = null;
            fileSystem.MutationObserver =
                (operation, sourcePath, destinationPath, timing) =>
                {
                    if (operation == "Replace" &&
                        timing == ScriptedFaultTiming.BeforeMutation &&
                        string.Equals(sourcePath, tempPath, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(destinationPath, primaryPath, StringComparison.OrdinalIgnoreCase))
                    {
                        candidatePrimary = fileSystem.ReadAllText(tempPath);
                    }
                };
            fileSystem.AfterCopyObserver = (sourcePath, destinationPath, overwrite) =>
            {
                if (string.Equals(sourcePath, previousPath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(destinationPath, tempPath, StringComparison.OrdinalIgnoreCase))
                {
                    fileSystem.Files[tempPath] = priorBackup;
                }
            };

            InvokeAllowingFailureLogs(service, "Save");

            Assert.NotNull(candidatePrimary);
            Assert.AreEqual(
                "CommitUncertain",
                GetProperty(service, "LastSaveStatus").ToString());
            AssertCanonicalLedger(
                fileSystem,
                root,
                candidatePrimary,
                priorBackup,
                priorBackup,
                priorPrimary);
            AssertSaveDisposition(
                service,
                "CommitUncertain",
                true,
                candidatePrimaryVerified: true,
                requiredBackupVerified: false,
                cleanupVerified: false);
            Assert.AreEqual(
                "C1_EXACT_STAGE_P0",
                GetField(GetProperty(service, "CurrentSave"), "CurrentChapterId"));
        }

        private static void AssertSaveDisposition(
            object service,
            string status,
            bool mayHaveMutated,
            bool? candidatePrimaryVerified = null,
            bool? requiredBackupVerified = null,
            bool? previousAuthorityVerified = null,
            bool? cleanupVerified = null,
            bool? rollbackAttempted = null,
            bool? rollbackVerified = null)
        {
            object disposition = GetProperty(service, "LastSaveDisposition");
            Assert.NotNull(disposition, "Expected a typed save-operation disposition.");
            Assert.AreEqual(
                status,
                GetProperty(disposition, "Status").ToString());
            Assert.AreEqual(
                mayHaveMutated,
                (bool)GetProperty(disposition, "MayHaveMutated"));

            AssertOptionalBoolean(
                disposition,
                "CandidatePrimaryVerified",
                candidatePrimaryVerified);
            AssertOptionalBoolean(
                disposition,
                "RequiredBackupVerified",
                requiredBackupVerified);
            AssertOptionalBoolean(
                disposition,
                "PreviousAuthorityVerified",
                previousAuthorityVerified);
            AssertOptionalBoolean(disposition, "CleanupVerified", cleanupVerified);
            AssertOptionalBoolean(disposition, "RollbackAttempted", rollbackAttempted);
            AssertOptionalBoolean(disposition, "RollbackVerified", rollbackVerified);
            IReadOnlyList<string> diagnosticCodes = GetDiagnosticCodes(disposition);
            Assert.That(diagnosticCodes.Count, Is.LessThanOrEqualTo(16));
            foreach (string diagnosticCode in diagnosticCodes)
            {
                Assert.That(diagnosticCode, Has.Length.LessThanOrEqualTo(128));
            }
        }

        private static IReadOnlyList<string> GetDiagnosticCodes(object disposition)
        {
            var result = new List<string>();
            foreach (object value in (IEnumerable)GetProperty(disposition, "DiagnosticCodes"))
            {
                result.Add((string)value);
            }

            return result;
        }

        private static void AssertOptionalBoolean(
            object target,
            string propertyName,
            bool? expected)
        {
            if (expected.HasValue)
            {
                Assert.AreEqual(
                    expected.Value,
                    (bool)GetProperty(target, propertyName),
                    propertyName);
            }
        }

        private static void AssertCanonicalLedger(
            ScriptedSaveFileOperations fileSystem,
            string root,
            string primary,
            string backup,
            string temp,
            string previous)
        {
            AssertCanonicalFile(
                fileSystem,
                Path.Combine(root, "save.json"),
                primary);
            AssertCanonicalFile(
                fileSystem,
                Path.Combine(root, "save.backup.json"),
                backup);
            AssertCanonicalFile(
                fileSystem,
                Path.Combine(root, "save.tmp.json"),
                temp);
            AssertCanonicalFile(
                fileSystem,
                Path.Combine(root, "save.previous.json"),
                previous);
        }

        private static void AssertCanonicalFile(
            ScriptedSaveFileOperations fileSystem,
            string path,
            string expectedContents)
        {
            if (expectedContents == null)
            {
                Assert.False(
                    fileSystem.FileExists(path),
                    $"Expected canonical artifact to be missing: {Path.GetFileName(path)}");
                return;
            }

            Assert.True(
                fileSystem.FileExists(path),
                $"Expected canonical artifact to exist: {Path.GetFileName(path)}");
            Assert.AreEqual(expectedContents, fileSystem.ReadAllText(path));
        }

        private static object InvokeAllowingFailureLogs(
            object target,
            string methodName,
            params object[] args)
        {
            bool priorIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                return Invoke(target, methodName, args);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = priorIgnore;
            }
        }

        private static object CreateSaveService(string root)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.LocalSaveGameService");
            ConstructorInfo constructor = serviceType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.NotNull(constructor, "Expected the testable persistence-path constructor.");
            return constructor.Invoke(new object[] { root });
        }

        private static string ArrangeExactBackupOnly(
            string root,
            ScriptedSaveFileOperations fileSystem,
            string chapterId)
        {
            object seedService = CreateSaveService(
                root,
                CreateFileOperationsProxy(fileSystem));
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Invoke(seedService, "CreateNewSave", Enum.Parse(realmType, "None"));
            object currentSave = GetProperty(seedService, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", chapterId);
            Invoke(seedService, "Save");

            string primaryPath = Path.Combine(root, "save.json");
            string backupPath = Path.Combine(root, "save.backup.json");
            string tempPath = Path.Combine(root, "save.tmp.json");
            string previousPath = Path.Combine(root, "save.previous.json");
            string exactBackup = fileSystem.ReadAllText(primaryPath);
            fileSystem.Files[backupPath] = exactBackup;
            fileSystem.Files.Remove(primaryPath);
            fileSystem.Files.Remove(tempPath);
            fileSystem.Files.Remove(previousPath);
            fileSystem.ClearMutationLedger();
            return exactBackup;
        }

        private static string ArrangeInvalidPrimaryAndExactBackup(
            string root,
            ScriptedSaveFileOperations fileSystem,
            string invalidPrimary,
            string chapterId)
        {
            string backupBytes = ArrangeExactBackupOnly(
                root,
                fileSystem,
                chapterId);
            string primaryPath = Path.Combine(root, "save.json");
            fileSystem.Files[primaryPath] = invalidPrimary;
            fileSystem.ClearMutationLedger();
            return backupBytes;
        }

        private static string FindSingleStageFiveQuarantine(
            ScriptedSaveFileOperations fileSystem)
        {
            string[] matches = fileSystem.Files.Keys
                .Where(path =>
                    Path.GetFileName(path).StartsWith(
                        "save.json.corrupt-",
                        StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(path).IndexOf(
                        "-stage5-",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            Assert.AreEqual(
                1,
                matches.Length,
                "Expected exactly one hash-linked Stage 5 quarantine.");
            return matches[0];
        }

        private static object CreateSaveService(string root, object fileOperations)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.LocalSaveGameService");
            Type fileOperationsType = GetRuntimeType("AL.Services.Local.ISaveFileOperations");
            ConstructorInfo constructor = serviceType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), fileOperationsType },
                null);
            Assert.NotNull(constructor, "Expected the testable file-operations constructor.");
            return constructor.Invoke(new[] { root, fileOperations });
        }

        private static object CreateFileOperationsProxy(ScriptedSaveFileOperations fileSystem)
        {
            Type interfaceType = GetRuntimeType("AL.Services.Local.ISaveFileOperations");
            MethodInfo createMethod = typeof(DispatchProxy).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "Create" && method.GetGenericArguments().Length == 2);
            object proxy = createMethod
                .MakeGenericMethod(interfaceType, typeof(ScriptedSaveFileOperationsProxy))
                .Invoke(null, null);
            ((ScriptedSaveFileOperationsProxy)proxy).FileSystem = fileSystem;
            return proxy;
        }

        private static object CreateRuntimeService(string serviceTypeName, string constructorArgumentTypeName, object argument)
        {
            Type serviceType = GetRuntimeType(serviceTypeName);
            Type constructorArgumentType = GetRuntimeType(constructorArgumentTypeName);
            ConstructorInfo constructor = serviceType.GetConstructor(new[] { constructorArgumentType });
            Assert.NotNull(constructor, $"Expected constructor for {serviceTypeName}.");
            return constructor.Invoke(new[] { argument });
        }

        private static void InvokeEnsureSaveDefaults(object save)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.LocalSaveGameService");
            MethodInfo method = serviceType.GetMethod(
                "EnsureSaveDefaults",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(null, new[] { save });
        }

        private static bool InvokeValidateSaveSemantics(object save, out string error)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.LocalSaveGameService");
            MethodInfo method = serviceType.GetMethod(
                "ValidateSaveSemantics",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            object[] arguments = { save, null };
            bool result = (bool)method.Invoke(null, arguments);
            error = (string)arguments[1];
            return result;
        }

        private static object CreateSaveWithAppliedBossLootReward(
            string encounterId,
            string rewardResultId,
            string bossId,
            string rewardDigest,
            long committedTimestamp)
        {
            Type saveType = GetRuntimeType("AL.Data.Runtime.SaveGameData");
            Type rewardType = GetRuntimeType("AL.Data.Runtime.AppliedBossLootRewardState");
            object save = Activator.CreateInstance(saveType);
            object reward = Activator.CreateInstance(rewardType);
            SetField(reward, "EncounterId", encounterId);
            SetField(reward, "RewardResultId", rewardResultId);
            SetField(reward, "BossId", bossId);
            SetField(reward, "RewardDigest", rewardDigest);
            SetField(reward, "CommittedTimestamp", committedTimestamp);
            IList rewards = CreateRuntimeList(rewardType);
            rewards.Add(reward);
            SetField(save, "AppliedBossLootRewards", rewards);
            InvokeEnsureSaveDefaults(save);
            return save;
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name == methodName && candidate.GetParameters().Length == args.Length);
            Assert.NotNull(method, $"Expected method {methodName}.");
            return method.Invoke(target, args);
        }

        private static IList CreateRuntimeList(Type elementType)
        {
            Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(elementType);
            return (IList)Activator.CreateInstance(listType);
        }

        private static Type GetRuntimeType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "Assembly-CSharp")
                ?.GetType(typeName);
            Assert.NotNull(type, $"Expected runtime type {typeName} in Assembly-CSharp.");
            return type;
        }

        private static object GetProperty(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property, $"Expected property {name}.");
            return property.GetValue(target);
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Expected field {name}.");
            return field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Expected field {name}.");
            field.SetValue(target, value);
        }

        public enum ScriptedFaultTiming
        {
            BeforeMutation,
            AfterMutation
        }

        public enum ScriptedFaultException
        {
            Io,
            NotSupported,
            PlatformNotSupported
        }

        public enum ScriptedReadFaultDisposition
        {
            IoFailure,
            ChangedDuringRead
        }

        private sealed class ScriptedReadFault
        {
            public ScriptedReadFault(
                string path,
                int occurrence,
                ScriptedReadFaultDisposition disposition)
            {
                Path = path;
                Occurrence = occurrence;
                Disposition = disposition;
            }

            public string Path { get; }
            public int Occurrence { get; }
            public ScriptedReadFaultDisposition Disposition { get; }
            public bool Triggered { get; set; }
        }

        private sealed class ScriptedMutationFault
        {
            public ScriptedMutationFault(
                string operation,
                string sourcePath,
                string destinationPath,
                ScriptedFaultTiming timing,
                ScriptedFaultException exception)
            {
                Operation = operation;
                SourcePath = sourcePath;
                DestinationPath = destinationPath;
                Timing = timing;
                Exception = exception;
            }

            public string Operation { get; }
            public string SourcePath { get; }
            public string DestinationPath { get; }
            public ScriptedFaultTiming Timing { get; }
            public ScriptedFaultException Exception { get; }
            public bool Triggered { get; set; }
        }

        public class ScriptedSaveFileOperationsProxy : DispatchProxy
        {
            public ScriptedSaveFileOperations FileSystem { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args) =>
                FileSystem.Invoke(targetMethod, args);
        }

        public sealed class ScriptedSaveFileOperations
        {
            private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

            public readonly Dictionary<string, string> Files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> CreateDirectoryFailures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> DeleteFailures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> MoveFailures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> BoundedReadIoFailures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> ChangedDuringReadPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> WriteFailuresBeforeMutation = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> WriteFailuresAfterMutation = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> WriteFailuresAfterExactMutation = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, int> BoundedReadCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, int> MoveCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, int> DurableWriteCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly List<string> MutationLedger = new List<string>();
            private readonly List<ScriptedMutationFault> _mutationFaults =
                new List<ScriptedMutationFault>();
            private readonly List<ScriptedReadFault> _readFaults =
                new List<ScriptedReadFault>();
            public int TotalMoveCount { get; private set; }
            public Action<string, int> BoundedReadObserver { get; set; }
            public Action<string, string, bool> CopyObserver { get; set; }
            public Action<string, string, bool> AfterCopyObserver { get; set; }
            public Action<string, string, string> ReplaceObserver { get; set; }
            public Action<string, string, string, ScriptedFaultTiming> MutationObserver { get; set; }
            public Action<string, string> DurableWriteObserver { get; set; }
            public Action<string, string> AfterDurableWriteObserver { get; set; }

            public object Invoke(MethodInfo targetMethod, object[] args)
            {
                switch (targetMethod.Name)
                {
                    case "FileExists":
                        return FileExists((string)args[0]);
                    case "CreateDirectory":
                        CreateDirectory((string)args[0]);
                        return null;
                    case "ReadAllBytesBounded":
                        return ReadAllBytesBounded(
                            targetMethod,
                            (string)args[0],
                            (int)args[1]);
                    case "WriteAllTextDurable":
                        return WriteAllTextDurable(
                            targetMethod,
                            (string)args[0],
                            (string)args[1]);
                    case "Copy":
                        Copy((string)args[0], (string)args[1], (bool)args[2]);
                        return null;
                    case "Move":
                        Move((string)args[0], (string)args[1]);
                        return null;
                    case "Replace":
                        Replace((string)args[0], (string)args[1], (string)args[2]);
                        return null;
                    case "Delete":
                        Delete((string)args[0]);
                        return null;
                    case "EnumerateFiles":
                        return EnumerateFiles((string)args[0], (string)args[1]);
                    default:
                        throw new NotSupportedException(targetMethod.Name);
                }
            }

            public bool FileExists(string path) => Files.ContainsKey(path);

            private void CreateDirectory(string path)
            {
                if (CreateDirectoryFailures.Contains(path))
                {
                    throw new IOException(
                        $"Directory creation blocked for {path}");
                }
            }

            public string ReadAllText(string path)
            {
                if (!Files.TryGetValue(path, out string contents))
                {
                    throw new FileNotFoundException(path);
                }

                return contents;
            }

            public int GetBoundedReadCount(string path) =>
                BoundedReadCounts.TryGetValue(path, out int count) ? count : 0;

            public int GetMoveCount(string path) =>
                MoveCounts.TryGetValue(path, out int count) ? count : 0;

            public int GetDurableWriteCount(string path) =>
                DurableWriteCounts.TryGetValue(path, out int count) ? count : 0;

            public void AddMutationFault(
                string operation,
                string sourcePath,
                string destinationPath,
                ScriptedFaultTiming timing,
                ScriptedFaultException exception)
            {
                _mutationFaults.Add(
                    new ScriptedMutationFault(
                        operation,
                        sourcePath,
                        destinationPath,
                        timing,
                        exception));
            }

            public void AddReadFault(
                string path,
                int occurrence,
                ScriptedReadFaultDisposition disposition)
            {
                _readFaults.Add(
                    new ScriptedReadFault(path, occurrence, disposition));
            }

            public void ClearMutationLedger() => MutationLedger.Clear();

            public int GetMutationAttemptCount(
                string operation,
                string sourcePath,
                string destinationPath)
            {
                string expected = FormatMutationLedgerEntry(
                    operation,
                    sourcePath,
                    destinationPath,
                    ScriptedFaultTiming.BeforeMutation);
                return MutationLedger.Count(
                    entry => string.Equals(
                        entry,
                        expected,
                        StringComparison.OrdinalIgnoreCase));
            }

            private object ReadAllBytesBounded(
                MethodInfo targetMethod,
                string path,
                int maximumBytes)
            {
                if (maximumBytes <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maximumBytes));
                }

                Increment(BoundedReadCounts, path);
                BoundedReadObserver?.Invoke(path, GetBoundedReadCount(path));
                ScriptedReadFault readFault = _readFaults.FirstOrDefault(
                    candidate =>
                        !candidate.Triggered &&
                        candidate.Occurrence == GetBoundedReadCount(path) &&
                        string.Equals(
                            candidate.Path,
                            path,
                            StringComparison.OrdinalIgnoreCase));
                if (readFault != null)
                {
                    readFault.Triggered = true;
                    string disposition =
                        readFault.Disposition ==
                        ScriptedReadFaultDisposition.ChangedDuringRead
                            ? "ChangedDuringRead"
                            : "IoFailure";
                    return CreateReadResult(
                        targetMethod.ReturnType,
                        disposition,
                        null,
                        0,
                        disposition == "ChangedDuringRead"
                            ? "SAVE_FILE_CHANGED_DURING_READ"
                            : "SAVE_FILE_IO_FAILURE");
                }

                if (BoundedReadIoFailures.Contains(path))
                {
                    return CreateReadResult(
                        targetMethod.ReturnType,
                        "IoFailure",
                        null,
                        0,
                        "SAVE_FILE_IO_FAILURE");
                }

                if (!Files.TryGetValue(path, out string contents))
                {
                    return CreateReadResult(
                        targetMethod.ReturnType,
                        "Missing",
                        null,
                        0,
                        "SAVE_FILE_MISSING");
                }

                byte[] bytes = StrictUtf8.GetBytes(contents);
                if (ChangedDuringReadPaths.Contains(path))
                {
                    return CreateReadResult(
                        targetMethod.ReturnType,
                        "ChangedDuringRead",
                        null,
                        bytes.LongLength,
                        "SAVE_FILE_CHANGED_DURING_READ");
                }

                if (bytes.Length > maximumBytes)
                {
                    return CreateReadResult(
                        targetMethod.ReturnType,
                        "Oversize",
                        null,
                        bytes.LongLength,
                        "SAVE_FILE_OVERSIZE");
                }

                return CreateReadResult(
                    targetMethod.ReturnType,
                    "Read",
                    bytes,
                    bytes.LongLength,
                    string.Empty);
            }

            private static object CreateReadResult(
                Type resultType,
                string dispositionName,
                byte[] bytes,
                long observedByteCount,
                string diagnosticCode)
            {
                ConstructorInfo constructor = resultType
                    .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Single(candidate => candidate.GetParameters().Length == 4);
                Type dispositionType = constructor.GetParameters()[0].ParameterType;
                object disposition = Enum.Parse(dispositionType, dispositionName);
                return constructor.Invoke(
                    new object[]
                    {
                        disposition,
                        bytes,
                        observedByteCount,
                        diagnosticCode
                    });
            }

            private object WriteAllTextDurable(
                MethodInfo targetMethod,
                string path,
                string contents)
            {
                Increment(DurableWriteCounts, path);
                DurableWriteObserver?.Invoke(path, contents);
                if (WriteFailuresBeforeMutation.Contains(path))
                {
                    return CreateWriteResult(
                        targetMethod.ReturnType,
                        false,
                        false,
                        "SAVE_FILE_WRITE_FAILED");
                }

                if (Files.ContainsKey(path))
                {
                    return CreateWriteResult(
                        targetMethod.ReturnType,
                        false,
                        false,
                        "SAVE_FILE_WRITE_FAILED");
                }

                if (WriteFailuresAfterMutation.Contains(path))
                {
                    Files[path] = contents.Substring(0, Math.Min(contents.Length, 16));
                    AfterDurableWriteObserver?.Invoke(path, contents);
                    return CreateWriteResult(
                        targetMethod.ReturnType,
                        false,
                        true,
                        "SAVE_FILE_WRITE_FAILED");
                }

                if (WriteFailuresAfterExactMutation.Contains(path))
                {
                    Files[path] = contents;
                    AfterDurableWriteObserver?.Invoke(path, contents);
                    return CreateWriteResult(
                        targetMethod.ReturnType,
                        false,
                        true,
                        "SAVE_FILE_WRITE_FAILED");
                }

                Files[path] = contents;
                AfterDurableWriteObserver?.Invoke(path, contents);
                return CreateWriteResult(
                    targetMethod.ReturnType,
                    true,
                    true,
                    string.Empty);
            }

            private static object CreateWriteResult(
                Type resultType,
                bool succeeded,
                bool diskChanged,
                string diagnosticCode)
            {
                ConstructorInfo constructor = resultType
                    .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Single(candidate => candidate.GetParameters().Length == 3);
                return constructor.Invoke(
                    new object[]
                    {
                        succeeded,
                        diskChanged,
                        diagnosticCode
                    });
            }

            private static void Increment(
                IDictionary<string, int> counts,
                string path)
            {
                counts.TryGetValue(path, out int current);
                counts[path] = current + 1;
            }

            private void Copy(string sourcePath, string destinationPath, bool overwrite)
            {
                CopyObserver?.Invoke(sourcePath, destinationPath, overwrite);
                ReachMutationBoundary(
                    "Copy",
                    sourcePath,
                    destinationPath,
                    ScriptedFaultTiming.BeforeMutation);
                if (!Files.TryGetValue(sourcePath, out string contents))
                {
                    throw new FileNotFoundException(sourcePath);
                }

                if (!overwrite && Files.ContainsKey(destinationPath))
                {
                    throw new IOException($"File already exists: {destinationPath}");
                }

                Files[destinationPath] = contents;
                AfterCopyObserver?.Invoke(sourcePath, destinationPath, overwrite);
                ReachMutationBoundary(
                    "Copy",
                    sourcePath,
                    destinationPath,
                    ScriptedFaultTiming.AfterMutation);
            }

            private void Move(string sourcePath, string destinationPath)
            {
                TotalMoveCount++;
                Increment(MoveCounts, sourcePath);
                if (MoveFailures.Contains(sourcePath))
                {
                    throw new IOException($"Move blocked for {sourcePath}");
                }

                ReachMutationBoundary(
                    "Move",
                    sourcePath,
                    destinationPath,
                    ScriptedFaultTiming.BeforeMutation);
                if (!Files.TryGetValue(sourcePath, out string contents))
                {
                    throw new FileNotFoundException(sourcePath);
                }

                if (Files.ContainsKey(destinationPath))
                {
                    throw new IOException($"File already exists: {destinationPath}");
                }

                Files.Remove(sourcePath);
                Files[destinationPath] = contents;
                ReachMutationBoundary(
                    "Move",
                    sourcePath,
                    destinationPath,
                    ScriptedFaultTiming.AfterMutation);
            }

            private void Replace(string sourcePath, string destinationPath, string backupPath)
            {
                ReplaceObserver?.Invoke(sourcePath, destinationPath, backupPath);
                ReachMutationBoundary(
                    "Replace",
                    sourcePath,
                    destinationPath,
                    ScriptedFaultTiming.BeforeMutation);
                if (!Files.TryGetValue(sourcePath, out string sourceContents))
                {
                    throw new FileNotFoundException(sourcePath);
                }

                if (!Files.TryGetValue(destinationPath, out string destinationContents))
                {
                    throw new FileNotFoundException(destinationPath);
                }

                Files[backupPath] = destinationContents;
                Files[destinationPath] = sourceContents;
                Files.Remove(sourcePath);
                ReachMutationBoundary(
                    "Replace",
                    sourcePath,
                    destinationPath,
                    ScriptedFaultTiming.AfterMutation);
            }

            private void Delete(string path)
            {
                ReachMutationBoundary(
                    "Delete",
                    path,
                    null,
                    ScriptedFaultTiming.BeforeMutation);
                if (DeleteFailures.Contains(path))
                {
                    throw new IOException($"Delete blocked for {path}");
                }

                Files.Remove(path);
                ReachMutationBoundary(
                    "Delete",
                    path,
                    null,
                    ScriptedFaultTiming.AfterMutation);
            }

            private void ReachMutationBoundary(
                string operation,
                string sourcePath,
                string destinationPath,
                ScriptedFaultTiming timing)
            {
                MutationLedger.Add(
                    FormatMutationLedgerEntry(
                        operation,
                        sourcePath,
                        destinationPath,
                        timing));
                MutationObserver?.Invoke(
                    operation,
                    sourcePath,
                    destinationPath,
                    timing);

                ScriptedMutationFault fault = _mutationFaults.FirstOrDefault(
                    candidate =>
                        !candidate.Triggered &&
                        candidate.Timing == timing &&
                        string.Equals(
                            candidate.Operation,
                            operation,
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            candidate.SourcePath,
                            sourcePath,
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            candidate.DestinationPath,
                            destinationPath,
                            StringComparison.OrdinalIgnoreCase));
                if (fault == null)
                {
                    return;
                }

                fault.Triggered = true;
                string message =
                    $"{operation} scripted {timing} fault: {sourcePath} -> {destinationPath}";
                switch (fault.Exception)
                {
                    case ScriptedFaultException.NotSupported:
                        throw new NotSupportedException(message);
                    case ScriptedFaultException.PlatformNotSupported:
                        throw new PlatformNotSupportedException(message);
                    default:
                        throw new IOException(message);
                }
            }

            private static string FormatMutationLedgerEntry(
                string operation,
                string sourcePath,
                string destinationPath,
                ScriptedFaultTiming timing)
            {
                return
                    $"{operation}|{timing}|{sourcePath ?? "<null>"}|{destinationPath ?? "<null>"}";
            }

            private IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern)
            {
                string prefix = searchPattern.EndsWith("*", StringComparison.Ordinal)
                    ? searchPattern.Substring(0, searchPattern.Length - 1)
                    : searchPattern;

                return Files.Keys
                    .Where(path => string.Equals(Path.GetDirectoryName(path), directoryPath, StringComparison.OrdinalIgnoreCase))
                    .Where(path => Path.GetFileName(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }
    }
}
