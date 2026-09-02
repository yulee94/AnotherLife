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
        private readonly Dictionary<string, WorldZoneData> _zones =
            new Dictionary<string, WorldZoneData>(StringComparer.Ordinal);
        private readonly List<WorldZoneData> _orderedZones = new List<WorldZoneData>();
        private IReadOnlyList<WorldZoneData> _zoneSnapshot;

        public LocalWorldAtlasService(IStoryService storyService)
        {
            _storyService = storyService;
            BuildFallbackAtlas();
            _zoneSnapshot = Array.AsReadOnly(_orderedZones.ToArray());
        }

        public WorldAtlasServiceQueryResult<IReadOnlyList<WorldZoneData>> GetAllZones()
        {
            return Available(_zoneSnapshot);
        }

        public WorldAtlasServiceQueryResult<IReadOnlyList<WorldZoneData>> GetZonesForRealm(RealmId realmId)
        {
            if (!IsValidViewer(realmId))
            {
                return InvalidViewer<IReadOnlyList<WorldZoneData>>();
            }

            return Available(BuildZonesForRealm(realmId));
        }

        public WorldAtlasServiceQueryResult<WorldZoneData> GetZone(string zoneId)
        {
            if (!IsValidId(zoneId))
            {
                return Failure<WorldZoneData>(
                    WorldAtlasServiceQueryStatus.InvalidId,
                    "AL-ATLAS-ID-INVALID",
                    "A lowercase snake-case zone ID is required.");
            }

            return _zones.TryGetValue(zoneId, out WorldZoneData zone)
                ? Available(zone)
                : Failure<WorldZoneData>(
                    WorldAtlasServiceQueryStatus.UnknownId,
                    "AL-ATLAS-ID-UNKNOWN",
                    "The requested zone ID is not present in the active atlas.");
        }

        public WorldAtlasServiceQueryResult<IReadOnlyList<WorldObjectiveData>> GetObjectivesForRealm(RealmId viewerRealm)
        {
            if (!IsValidViewer(viewerRealm))
            {
                return InvalidViewer<IReadOnlyList<WorldObjectiveData>>();
            }

            return Available(BuildObjectivesForRealm(viewerRealm));
        }

        public WorldAtlasServiceQueryResult<WorldNarrationSnapshot> GetNarrationSnapshot(RealmId viewerRealm)
        {
            if (!IsValidViewer(viewerRealm))
            {
                return InvalidViewer<WorldNarrationSnapshot>();
            }

            var diagnostics = new List<WorldAtlasServiceDiagnostic>();
            IReadOnlyList<string> conflictHints = GetConflictHints(viewerRealm, diagnostics);
            var snapshot = new WorldNarrationSnapshot(
                viewerRealm,
                BuildZonesForRealm(viewerRealm),
                BuildObjectivesForRealm(viewerRealm),
                conflictHints);
            WorldAtlasServiceQueryStatus status = diagnostics.Count == 0
                ? WorldAtlasServiceQueryStatus.Available
                : WorldAtlasServiceQueryStatus.AvailableWithDiagnostics;
            return new WorldAtlasServiceQueryResult<WorldNarrationSnapshot>(status, snapshot, diagnostics);
        }

        private IReadOnlyList<WorldZoneData> BuildZonesForRealm(RealmId realmId)
        {
            var result = new List<WorldZoneData>();
            foreach (WorldZoneData zone in _orderedZones)
            {
                if (zone.HomeRealm == realmId || zone.HomeRealm == RealmId.None)
                {
                    result.Add(zone);
                }
            }

            return Array.AsReadOnly(result.ToArray());
        }

        private IReadOnlyList<WorldObjectiveData> BuildObjectivesForRealm(RealmId viewerRealm)
        {
            var result = new List<WorldObjectiveData>();
            foreach (WorldZoneData zone in _orderedZones)
            {
                foreach (WorldObjectiveData objective in zone.Objectives)
                {
                    if (objective.IsWarzoneObjective || objective.OwnerRealm != viewerRealm)
                    {
                        result.Add(objective);
                    }
                }
            }

            return Array.AsReadOnly(result.ToArray());
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
                "A contested central region where every realm can test pressure without breaching an inner city.");

            AddWarzone(
                "iron_pass",
                "Iron Pass Gatefront",
                "mountain pass and siege approach",
                "hint_crownlands_trade",
                "GreatGate",
                RealmId.Stonehold,
                ResourceType.DeepOre,
                "The trade artery between Stonehold and Crownlands, now hardened into a gate war objective.");

            AddWarzone(
                "worldroot_border",
                "Worldroot Border Grove",
                "forest border and sacred roots",
                "hint_eldergrove_blight",
                "RareResource",
                RealmId.Eldergrove,
                ResourceType.WorldSap,
                "A living grove where enemy construction pressure threatens Eldergrove's rare WorldSap flow.");

            AddWarzone(
                "sovereign_road",
                "Sovereign Trade Road",
                "royal road, caravans, farmland",
                "hint_stonehold_war",
                "TradeRoute",
                RealmId.Crownlands,
                ResourceType.RoyalSigil,
                "The human realm's flexible economy depends on keeping this road open through border pressure.");

            AddWarzone(
                "ashen_rift",
                "Ashen Rift",
                "volcanic canyon and shadow vents",
                "hint_umbral_revenge",
                "BossApproach",
                RealmId.Umbral,
                ResourceType.DarkCrystal,
                "A dangerous rift where Umbral scouts draw enemies toward curses, ambushes, and DarkCrystal veins.");
        }

        private void AddRealmHomeland(RealmId realmId, string zoneId, string displayName, string safetyLayer, string terrain, string chapterKey, string capitalGemId, string bossGemId)
        {
            AddZone(new WorldZoneData(
                zoneId,
                displayName,
                realmId,
                safetyLayer,
                terrain,
                chapterKey,
                new[]
                {
                    CreateObjective(capitalGemId, displayName + " Heart Gem", "RealmGem", realmId, ResourceRules.GetRareResourceForRealm(realmId), chapterKey, "Capital gem objective inside the protected inner realm.", false),
                    CreateObjective(bossGemId, displayName + " Boss Gem", "RealmBossGem", realmId, ResourceRules.GetRareResourceForRealm(realmId), chapterKey, "Second realm gem guarded near the inner boss approach.", false)
                }));
        }

        private void AddWarzone(string zoneId, string displayName, string terrain, string narrativeKey, string objectiveType, RealmId ownerRealm, ResourceType reward, string description)
        {
            AddZone(new WorldZoneData(
                zoneId,
                displayName,
                ownerRealm,
                "WarZone",
                terrain,
                narrativeKey,
                new[]
                {
                    CreateObjective(zoneId + "_objective", displayName, objectiveType, ownerRealm, reward, narrativeKey, description, true)
                }));
        }

        private static WorldObjectiveData CreateObjective(string id, string displayName, string objectiveType, RealmId ownerRealm, ResourceType reward, string narrativeKey, string description, bool isWarzoneObjective)
        {
            return new WorldObjectiveData(
                id,
                displayName,
                objectiveType,
                ownerRealm,
                reward,
                narrativeKey,
                description,
                isWarzoneObjective);
        }

        private void AddZone(WorldZoneData zone)
        {
            _zones.Add(zone.Id, zone);
            _orderedZones.Add(zone);
        }

        private IReadOnlyList<string> GetConflictHints(
            RealmId viewerRealm,
            ICollection<WorldAtlasServiceDiagnostic> diagnostics)
        {
            if (_storyService == null)
            {
                diagnostics.Add(new WorldAtlasServiceDiagnostic(
                    "AL-ATLAS-STORY-UNAVAILABLE",
                    "Conflict hints are unavailable because no story service was registered."));
                return Array.Empty<string>();
            }

            try
            {
                var result = new List<string>();
                IEnumerable<DialogueNode> hints = _storyService.GetConflictHints(viewerRealm);
                if (hints == null)
                {
                    diagnostics.Add(new WorldAtlasServiceDiagnostic(
                        "AL-ATLAS-STORY-INVALID",
                        "The story service returned no conflict-hint collection."));
                    return Array.Empty<string>();
                }

                foreach (DialogueNode hint in hints)
                {
                    if (hint == null)
                    {
                        diagnostics.Add(new WorldAtlasServiceDiagnostic(
                            "AL-ATLAS-STORY-INVALID",
                            "The story service returned an invalid conflict hint."));
                        return Array.Empty<string>();
                    }

                    result.Add($"{hint.CharacterName}: {hint.Text}");
                }

                return Array.AsReadOnly(result.ToArray());
            }
            catch (Exception)
            {
                diagnostics.Add(new WorldAtlasServiceDiagnostic(
                    "AL-ATLAS-STORY-FAILED",
                    "The story service failed while reading conflict hints."));
                return Array.Empty<string>();
            }
        }

        private static bool IsValidViewer(RealmId realmId)
        {
            return realmId != RealmId.None && Enum.IsDefined(typeof(RealmId), realmId);
        }

        private static bool IsValidId(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }

            bool previousUnderscore = false;
            for (int index = 1; index < value.Length; index++)
            {
                char character = value[index];
                bool valid =
                    (character >= 'a' && character <= 'z') ||
                    (character >= '0' && character <= '9') ||
                    character == '_';
                if (!valid || character == '_' && previousUnderscore)
                {
                    return false;
                }

                previousUnderscore = character == '_';
            }

            return !previousUnderscore;
        }

        private static WorldAtlasServiceQueryResult<T> Available<T>(T value)
        {
            return new WorldAtlasServiceQueryResult<T>(
                WorldAtlasServiceQueryStatus.Available,
                value,
                Array.Empty<WorldAtlasServiceDiagnostic>());
        }

        private static WorldAtlasServiceQueryResult<T> InvalidViewer<T>()
        {
            return Failure<T>(
                WorldAtlasServiceQueryStatus.InvalidViewer,
                "AL-ATLAS-VIEWER-INVALID",
                "A committed playable realm is required for this atlas query.");
        }

        private static WorldAtlasServiceQueryResult<T> Failure<T>(
            WorldAtlasServiceQueryStatus status,
            string code,
            string message)
        {
            return new WorldAtlasServiceQueryResult<T>(
                status,
                default,
                new[] { new WorldAtlasServiceDiagnostic(code, message) });
        }
    }
}
