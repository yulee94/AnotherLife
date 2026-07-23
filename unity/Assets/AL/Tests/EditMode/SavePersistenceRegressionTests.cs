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
            Assert.AreSame(resource, ((IList)GetField(save, "Resources"))[0]);
            Assert.AreSame(building, ((IList)GetField(save, "Buildings"))[0]);
            Assert.AreSame(quest, ((IList)GetField(save, "Quests"))[0]);
            Assert.AreSame(equipment, ((IList)GetField(save, "OwnedEquipment"))[0]);
            Assert.AreEqual(4321L, GetField(resource, "Amount"));
            Assert.AreEqual(7, GetField(building, "Level"));
            Assert.AreEqual(4, GetField(quest, "CurrentValue"));
            Assert.AreEqual(2, GetField(equipment, "Quantity"));
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
        public void CorruptedPrimaryAndValidBackupArePreservedForExplicitRecovery()
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

                Assert.AreEqual("RecoveryRequired", GetProperty(recoveredService, "LastLoadStatus").ToString());
                Assert.That(
                    (string)GetProperty(recoveredService, "LastLoadMessage"),
                    Does.Contain("AL-SAVE-RECOVERY-REQUIRED"));
                Assert.Null(GetProperty(recoveredService, "CurrentSave"));
                object recoveredSave = GetProperty(recoveredService, "ReadOnlyCandidateSnapshot");
                Assert.NotNull(recoveredSave);
                Assert.AreEqual("C1_BACKUP", GetField(recoveredSave, "CurrentChapterId"));
                Assert.True(File.Exists(primaryPath));
                Assert.True(File.Exists(backupPath));
                Assert.False(File.Exists(tempPath));
                CollectionAssert.AreEqual(primaryBefore, File.ReadAllBytes(primaryPath));
                CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(backupPath));
                Assert.IsEmpty(Directory.GetFiles(root, "save.json.corrupt-*"));
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
        public void MissingPrimaryAndValidBackupArePreservedForExplicitRecovery()
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
                File.Copy(primaryPath, backupPath, true);
                File.Delete(primaryPath);
                byte[] backupBefore = File.ReadAllBytes(backupPath);

                object recoveredService = CreateSaveService(root);
                Invoke(recoveredService, "Load");

                Assert.AreEqual("RecoveryRequired", GetProperty(recoveredService, "LastLoadStatus").ToString());
                Assert.That(
                    (string)GetProperty(recoveredService, "LastLoadMessage"),
                    Does.Contain("AL-SAVE-RECOVERY-REQUIRED"));
                Assert.Null(GetProperty(recoveredService, "CurrentSave"));
                object recoveredSave = GetProperty(recoveredService, "ReadOnlyCandidateSnapshot");
                Assert.NotNull(recoveredSave);
                Assert.AreEqual("C1_BACKUP_ONLY", GetField(recoveredSave, "CurrentChapterId"));
                Assert.False(File.Exists(primaryPath));
                Assert.True(File.Exists(backupPath));
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
                    $"save.json.corrupt-20260101000000-{Guid.NewGuid():N}",
                    $"save.backup.json.corrupt-20260101000000-{Guid.NewGuid():N}"
                };

                foreach (string fileName in extraFiles)
                {
                    File.WriteAllText(Path.Combine(root, fileName), "artifact");
                }

                Invoke(service, "DeleteSave");

                Assert.False((bool)Invoke(service, "HasSave"));
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
        public void InvalidPrimaryAndValidBackupRequireRecoveryWithoutMoveAttempt()
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

            Assert.AreEqual("RecoveryRequired", GetProperty(recoveredService, "LastLoadStatus").ToString());
            Assert.Null(GetProperty(recoveredService, "CurrentSave"));
            Assert.NotNull(GetProperty(recoveredService, "ReadOnlyCandidateSnapshot"));
            Assert.AreEqual(primaryBefore, fileSystem.ReadAllText(primaryPath));
            Assert.AreEqual(backupBefore, fileSystem.ReadAllText(backupPath));
            Assert.AreEqual(moveCallsBeforeLoad, fileSystem.TotalMoveCount);
            Assert.AreEqual(0, fileSystem.GetMoveCount(primaryPath));
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

            string backupPath = Path.Combine(root, "save.backup.json");
            string previousPath = Path.Combine(root, "save.previous.json");
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
                "SaveFailedPreviousPreserved",
                GetProperty(service, "LastSaveStatus").ToString());
            Assert.True(fileSystem.FileExists(previousPath));
            Assert.True(fileSystem.FileExists(backupPath));
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
            string backupBefore = fileSystem.ReadAllText(backupPath);
            object currentSave = GetProperty(service, "CurrentSave");
            SetField(currentSave, "CurrentChapterId", "C1_NEW_VALIDATED");
            fileSystem.ReplaceObserver = (sourcePath, destinationPath, previousPath) =>
            {
                if (string.Equals(sourcePath, tempPath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(destinationPath, primaryPath, StringComparison.OrdinalIgnoreCase))
                {
                    fileSystem.Files[primaryPath] = "{ changed after validation";
                }
            };

            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("^AL-SAVE-BACKUP-STAGE-INVALID:"));
            Invoke(service, "Save");

            Assert.AreEqual(
                "SaveFailedPreviousPreserved",
                GetProperty(service, "LastSaveStatus").ToString());
            Assert.AreEqual(backupBefore, fileSystem.ReadAllText(backupPath));
            Assert.That(fileSystem.ReadAllText(primaryPath), Does.Contain("C1_NEW_VALIDATED"));
            Assert.That(fileSystem.ReadAllText(tempPath), Does.Contain("changed after validation"));
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
            public readonly HashSet<string> DeleteFailures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> MoveFailures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> BoundedReadIoFailures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> ChangedDuringReadPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> WriteFailuresBeforeMutation = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> WriteFailuresAfterMutation = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, int> BoundedReadCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, int> MoveCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public int TotalMoveCount { get; private set; }
            public Action<string, int> BoundedReadObserver { get; set; }
            public Action<string, string, bool> CopyObserver { get; set; }
            public Action<string, string, bool> AfterCopyObserver { get; set; }
            public Action<string, string, string> ReplaceObserver { get; set; }

            public object Invoke(MethodInfo targetMethod, object[] args)
            {
                switch (targetMethod.Name)
                {
                    case "FileExists":
                        return FileExists((string)args[0]);
                    case "CreateDirectory":
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
                if (WriteFailuresBeforeMutation.Contains(path))
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
                    return CreateWriteResult(
                        targetMethod.ReturnType,
                        false,
                        true,
                        "SAVE_FILE_WRITE_FAILED");
                }

                Files[path] = contents;
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
            }

            private void Move(string sourcePath, string destinationPath)
            {
                TotalMoveCount++;
                Increment(MoveCounts, sourcePath);
                if (MoveFailures.Contains(sourcePath))
                {
                    throw new IOException($"Move blocked for {sourcePath}");
                }

                if (!Files.TryGetValue(sourcePath, out string contents))
                {
                    throw new FileNotFoundException(sourcePath);
                }

                Files.Remove(sourcePath);
                Files[destinationPath] = contents;
            }

            private void Replace(string sourcePath, string destinationPath, string backupPath)
            {
                ReplaceObserver?.Invoke(sourcePath, destinationPath, backupPath);
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
            }

            private void Delete(string path)
            {
                if (DeleteFailures.Contains(path))
                {
                    throw new IOException($"Delete blocked for {path}");
                }

                Files.Remove(path);
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
