using System;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions.Narrative;

namespace AL.RealmWar.World
{
    public class LocalWorldAtlasService : IWorldAtlasService
    {
        private readonly IStoryService _storyService;
        private readonly Dictionary<string, WorldZoneData> _zones = new Dictionary<string, WorldZoneData>();

        public LocalWorldAtlasService(IStoryService storyService)
        {
            _storyService = storyService;
            BuildFallbackAtlas();
        }

        public IEnumerable<WorldZoneData> GetAllZones() => _zones.Values;

        public IEnumerable<WorldZoneData> GetZonesForRealm(RealmId realmId)
        {
            foreach (var zone in _zones.Values)
            {
                if (zone.HomeRealm == realmId || zone.HomeRealm == RealmId.None)
                {
                    yield return zone;
                }
            }
        }

        public WorldZoneData GetZone(string zoneId)
        {
            return !string.IsNullOrWhiteSpace(zoneId) && _zones.TryGetValue(zoneId, out var zone)
                ? zone
                : null;
        }

        public IEnumerable<WorldObjectiveData> GetObjectivesForRealm(RealmId viewerRealm)
        {
            foreach (var zone in _zones.Values)
            {
                foreach (var objective in zone.Objectives)
                {
                    if (objective.IsWarzoneObjective || objective.OwnerRealm != viewerRealm)
                    {
                        yield return objective;
                    }
                }
            }
        }

        public WorldNarrationSnapshot GetNarrationSnapshot(RealmId viewerRealm)
        {
            var snapshot = new WorldNarrationSnapshot
            {
                ViewerRealm = viewerRealm
            };

            snapshot.VisibleZones.AddRange(GetZonesForRealm(viewerRealm));
            snapshot.ActiveObjectives.AddRange(GetObjectivesForRealm(viewerRealm));
            AddConflictHints(snapshot, viewerRealm);
            return snapshot;
        }

        private void BuildFallbackAtlas()
        {
            AddRealmHomeland(
                RealmId.Stonehold,
                "stonehold_inner",
                "Stonehold Deep Forge",
                "InnerRealm",
                "mountains, caves, stone halls",
                "C3_SH",
                "Stonehold_Heart_Gem",
                "Stonehold_Fortress_Gem");

            AddRealmHomeland(
                RealmId.Eldergrove,
                "eldergrove_inner",
                "Eldergrove World Tree",
                "InnerRealm",
                "ancient forest, lakes, luminous roots",
                "C3_EG",
                "Eldergrove_Heart_Gem",
                "Eldergrove_Glade_Gem");

            AddRealmHomeland(
                RealmId.Crownlands,
                "crownlands_inner",
                "Crownlands Royal Capital",
                "InnerRealm",
                "castles, trade roads, fertile plains",
                "C3_CL",
                "Crownlands_Heart_Gem",
                "Crownlands_Capital_Gem");

            AddRealmHomeland(
                RealmId.Umbral,
                "umbral_inner",
                "Umbral Void Rift",
                "InnerRealm",
                "volcanic rifts, ash deserts, shadow stone",
                "C3_UM",
                "Umbral_Heart_Gem",
                "Umbral_Void_Gem");

            AddWarzone(
                "neutral_borderlands",
                "Neutral Borderlands",
                "mixed hills and broken roads",
                "T5",
                "Fortress",
                RealmId.None,
                ResourceType.Gold,
                "A contested central region where every realm can test pressure without breaching an inner city.",
                1.0f);

            AddWarzone(
                "iron_pass",
                "Iron Pass Gatefront",
                "mountain pass and siege approach",
                "hint_crownlands_trade",
                "GreatGate",
                RealmId.Stonehold,
                ResourceType.DeepOre,
                "The trade artery between Stonehold and Crownlands, now hardened into a gate war objective.",
                1.35f);

            AddWarzone(
                "worldroot_border",
                "Worldroot Border Grove",
                "forest border and sacred roots",
                "hint_eldergrove_blight",
                "RareResource",
                RealmId.Eldergrove,
                ResourceType.WorldSap,
                "A living grove where enemy construction pressure threatens Eldergrove's rare WorldSap flow.",
                1.2f);

            AddWarzone(
                "sovereign_road",
                "Sovereign Trade Road",
                "royal road, caravans, farmland",
                "hint_stonehold_war",
                "TradeRoute",
                RealmId.Crownlands,
                ResourceType.RoyalSigil,
                "The human realm's flexible economy depends on keeping this road open through border pressure.",
                1.1f);

            AddWarzone(
                "ashen_rift",
                "Ashen Rift",
                "volcanic canyon and shadow vents",
                "hint_umbral_revenge",
                "BossApproach",
                RealmId.Umbral,
                ResourceType.DarkCrystal,
                "A dangerous rift where Umbral scouts draw enemies toward curses, ambushes, and DarkCrystal veins.",
                1.45f);
        }

        private void AddRealmHomeland(RealmId realmId, string zoneId, string displayName, string safetyLayer, string terrain, string chapterKey, string capitalGemId, string bossGemId)
        {
            var zone = new WorldZoneData
            {
                Id = zoneId,
                DisplayName = displayName,
                HomeRealm = realmId,
                SafetyLayer = safetyLayer,
                TerrainTheme = terrain,
                SceneHint = chapterKey
            };

            zone.Objectives.Add(CreateObjective(capitalGemId, displayName + " Heart Gem", "RealmGem", realmId, ResourceRules.GetRareResourceForRealm(realmId), chapterKey, "Capital gem objective inside the protected inner realm.", false, 0f));
            zone.Objectives.Add(CreateObjective(bossGemId, displayName + " Boss Gem", "RealmBossGem", realmId, ResourceRules.GetRareResourceForRealm(realmId), chapterKey, "Second realm gem guarded near the inner boss approach.", false, 0f));
            _zones[zone.Id] = zone;
        }

        private void AddWarzone(string zoneId, string displayName, string terrain, string narrativeKey, string objectiveType, RealmId ownerRealm, ResourceType reward, string description, float passiveCreditWeight)
        {
            var zone = new WorldZoneData
            {
                Id = zoneId,
                DisplayName = displayName,
                HomeRealm = ownerRealm,
                SafetyLayer = "WarZone",
                TerrainTheme = terrain,
                SceneHint = narrativeKey
            };

            zone.Objectives.Add(CreateObjective(zoneId + "_objective", displayName, objectiveType, ownerRealm, reward, narrativeKey, description, true, passiveCreditWeight));
            _zones[zone.Id] = zone;
        }

        private static WorldObjectiveData CreateObjective(string id, string displayName, string objectiveType, RealmId ownerRealm, ResourceType reward, string narrativeKey, string description, bool isWarzoneObjective, float passiveCreditWeight)
        {
            return new WorldObjectiveData
            {
                Id = id,
                DisplayName = displayName,
                ObjectiveType = objectiveType,
                OwnerRealm = ownerRealm,
                RareResourceReward = reward,
                NarrativeKey = narrativeKey,
                Description = description,
                IsWarzoneObjective = isWarzoneObjective,
                PassiveCreditWeight = passiveCreditWeight
            };
        }

        private void AddConflictHints(WorldNarrationSnapshot snapshot, RealmId viewerRealm)
        {
            if (_storyService == null)
            {
                return;
            }

            try
            {
                foreach (DialogueNode hint in _storyService.GetConflictHints(viewerRealm))
                {
                    snapshot.ConflictHints.Add($"{hint.CharacterName}: {hint.Text}");
                }
            }
            catch (Exception)
            {
                // Story services are optional for standalone world-atlas tests.
            }
        }
    }
}
