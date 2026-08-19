using System;
using System.Collections.Generic;
using System.IO;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Catalogs;
using AL.Data.Definitions;
using UnityEngine;

namespace AL.Services.Local
{
    /// <summary>
    /// Catalog-backed <see cref="IGameDataService"/>. Six-family records come from
    /// StreamingAssets/GameData; construction levels are projected from the
    /// catalog cost/duration profile IDs via <see cref="GameDataBuildingProgressionRegistry"/>.
    /// </summary>
    public class LocalGameDataService : IGameDataService
    {
        internal const string CatalogSetFileName = SixFamilyCatalogLoader.ManifestFileName;

        private static readonly object CacheGate = new object();
        private static string CachedRoot;
        private static GameDataCatalogSetSnapshot CachedSnapshot;

        private readonly Dictionary<RealmId, RealmDefinition> _realms =
            new Dictionary<RealmId, RealmDefinition>();
        private readonly Dictionary<string, BuildingDefinition> _buildings =
            new Dictionary<string, BuildingDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, TroopDefinition> _troops =
            new Dictionary<string, TroopDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, ChampionDefinition> _champions =
            new Dictionary<string, ChampionDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, SkillDefinition> _skills =
            new Dictionary<string, SkillDefinition>(StringComparer.Ordinal);
        private readonly List<string> _canonicalChampionIds = new List<string>();

        /// <summary>
        /// Test seam. When set, the parameterless constructor loads this directory
        /// instead of StreamingAssets/GameData.
        /// </summary>
        internal static string CatalogRootOverride { get; set; }

        public LocalGameDataService()
            : this(ResolveDefaultRoot())
        {
        }

        public LocalGameDataService(string catalogRootDirectory)
            : this(LoadOrGetCachedSnapshot(catalogRootDirectory))
        {
        }

        public LocalGameDataService(GameDataCatalogSetSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ProjectSnapshot(snapshot);
        }

        internal static void ClearSnapshotCache()
        {
            lock (CacheGate)
            {
                CachedRoot = null;
                CachedSnapshot = null;
            }
        }

        internal static string ResolveDefaultRoot()
        {
            if (!string.IsNullOrWhiteSpace(CatalogRootOverride))
            {
                return CatalogRootOverride;
            }

            try
            {
                if (!string.IsNullOrEmpty(Application.streamingAssetsPath))
                {
                    var packaged = Path.Combine(
                        Application.streamingAssetsPath,
                        SixFamilyCatalogLoader.PackagedRelativeRoot);
                    if (File.Exists(Path.Combine(packaged, CatalogSetFileName)))
                    {
                        return packaged;
                    }
                }

                if (!string.IsNullOrEmpty(Application.dataPath))
                {
                    var editor = Path.Combine(
                        Application.dataPath,
                        "StreamingAssets",
                        SixFamilyCatalogLoader.PackagedRelativeRoot);
                    if (File.Exists(Path.Combine(editor, CatalogSetFileName)))
                    {
                        return editor;
                    }
                }
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Six-family catalog root could not be resolved from Unity StreamingAssets.",
                    exception);
            }

            throw new InvalidOperationException(
                "Six-family catalog-set.json was not found under StreamingAssets/GameData. " +
                "Package the reviewed six-family catalog set before constructing IGameDataService.");
        }

        internal static GameDataCatalogSetSnapshot LoadOrGetCachedSnapshot(string catalogRootDirectory)
        {
            if (string.IsNullOrWhiteSpace(catalogRootDirectory))
            {
                throw new ArgumentException(
                    "A catalog root directory is required.",
                    nameof(catalogRootDirectory));
            }

            var root = Path.GetFullPath(catalogRootDirectory);
            lock (CacheGate)
            {
                if (CachedSnapshot != null &&
                    string.Equals(CachedRoot, root, StringComparison.OrdinalIgnoreCase))
                {
                    return CachedSnapshot;
                }

                var snapshot = SixFamilyCatalogLoader.LoadRequiredSnapshot(root);
                CachedRoot = root;
                CachedSnapshot = snapshot;
                return snapshot;
            }
        }

        public RealmDefinition GetRealm(RealmId id)
        {
            return _realms.TryGetValue(id, out var realm) ? realm : null;
        }

        public IEnumerable<RealmDefinition> GetAllRealms()
        {
            var ordered = new List<RealmDefinition>(_realms.Count);
            if (_realms.TryGetValue(RealmId.Stonehold, out var stonehold)) ordered.Add(stonehold);
            if (_realms.TryGetValue(RealmId.Eldergrove, out var eldergrove)) ordered.Add(eldergrove);
            if (_realms.TryGetValue(RealmId.Crownlands, out var crownlands)) ordered.Add(crownlands);
            if (_realms.TryGetValue(RealmId.Umbral, out var umbral)) ordered.Add(umbral);
            return ordered;
        }

        public BuildingDefinition GetBuilding(string id)
        {
            return id != null && _buildings.TryGetValue(id, out var building) ? building : null;
        }

        public TroopDefinition GetTroop(string id)
        {
            return id != null && _troops.TryGetValue(id, out var troop) ? troop : null;
        }

        public ChampionDefinition GetChampion(string id)
        {
            return id != null && _champions.TryGetValue(id, out var champion) ? champion : null;
        }

        public IEnumerable<ChampionDefinition> GetAllChampions()
        {
            var ordered = new List<ChampionDefinition>(_canonicalChampionIds.Count);
            for (var index = 0; index < _canonicalChampionIds.Count; index++)
            {
                ChampionDefinition champion;
                if (_champions.TryGetValue(_canonicalChampionIds[index], out champion))
                {
                    ordered.Add(champion);
                }
            }

            return ordered;
        }

        public SkillDefinition GetSkill(string id)
        {
            return id != null && _skills.TryGetValue(id, out var skill) ? skill : null;
        }

        private void ProjectSnapshot(GameDataCatalogSetSnapshot snapshot)
        {
            ProjectSkills(RequireFamily(snapshot, "skills"));
            ProjectTroops(RequireFamily(snapshot, "troops"));
            ProjectBuildings(RequireFamily(snapshot, "buildings"));
            ProjectRealms(RequireFamily(snapshot, "realms"));
            ProjectChampions(RequireFamily(snapshot, "champions"));
        }

        private static GameDataFamilyCatalogSnapshot RequireFamily(
            GameDataCatalogSetSnapshot snapshot,
            string family)
        {
            GameDataFamilyCatalogSnapshot catalog;
            if (!snapshot.FamiliesById.TryGetValue(family, out catalog) ||
                catalog.Records.Count == 0)
            {
                throw new InvalidOperationException(
                    "Six-family catalog is missing required family '" + family + "'.");
            }

            return catalog;
        }

        private void ProjectRealms(GameDataFamilyCatalogSnapshot family)
        {
            foreach (var record in family.Records)
            {
                var legacyId = ReadString(record, "legacy_realm_id");
                RealmId realmId;
                if (!Enum.TryParse(legacyId, false, out realmId) || realmId == RealmId.None)
                {
                    throw new InvalidOperationException(
                        "Realm record '" + record.Id + "' has an invalid legacy_realm_id.");
                }

                var definition = ScriptableObject.CreateInstance<RealmDefinition>();
                definition.Id = realmId;
                definition.RealmName = string.IsNullOrEmpty(legacyId) ? record.Id : legacyId;
                definition.Description = ReadString(record, "description_ref");
                _realms[realmId] = definition;
            }
        }

        private void ProjectBuildings(GameDataFamilyCatalogSnapshot family)
        {
            foreach (var record in family.Records)
            {
                var canonical = ProjectBuilding(record, record.Id);
                _buildings[record.Id] = canonical;
                foreach (var alias in family.Aliases)
                {
                    if (!string.Equals(alias.CanonicalId, record.Id, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    _buildings[alias.LegacyId] = ProjectBuilding(record, alias.LegacyId);
                }
            }
        }

        private static BuildingDefinition ProjectBuilding(GameDataCatalogRecord record, string exposedId)
        {
            var definition = ScriptableObject.CreateInstance<BuildingDefinition>();
            definition.Id = exposedId;
            var legacyId = ReadString(record, "legacy_building_id");
            definition.DisplayName = FormatBuildingDisplayName(legacyId ?? exposedId);
            definition.MaxLevel = ReadInt32(record, "max_level");
            definition.ConstructionLevels = ProjectConstructionLevels(
                ReadString(record, "cost_profile_id"),
                ReadString(record, "duration_profile_id"));
            return definition;
        }

        private static List<BuildingConstructionLevelDefinition> ProjectConstructionLevels(
            string costProfileId,
            string durationProfileId)
        {
            GameDataBuildingCostProfile costProfile;
            if (!GameDataBuildingProgressionRegistry.TryGetCostProfileByStableId(
                    costProfileId,
                    out costProfile))
            {
                throw new InvalidOperationException(
                    "Building cost profile '" + costProfileId + "' is not in the progression registry.");
            }

            var durationProfile = GameDataBuildingProgressionRegistry.DurationProfile;
            if (durationProfile == null ||
                !string.Equals(durationProfile.StableId, durationProfileId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Building duration profile '" + durationProfileId + "' is not the reviewed common profile.");
            }

            var levels = new List<BuildingConstructionLevelDefinition>(costProfile.Levels.Count);
            for (var index = 0; index < costProfile.Levels.Count; index++)
            {
                var costLevel = costProfile.Levels[index];
                GameDataBuildingDurationLevel durationLevel;
                if (!durationProfile.TryGetLevel(costLevel.TargetLevel, out durationLevel))
                {
                    throw new InvalidOperationException(
                        "Duration profile is missing target level " + costLevel.TargetLevel + ".");
                }

                var costs = new List<BuildingConstructionCostDefinition>(costLevel.Costs.Count);
                for (var costIndex = 0; costIndex < costLevel.Costs.Count; costIndex++)
                {
                    var cost = costLevel.Costs[costIndex];
                    costs.Add(new BuildingConstructionCostDefinition
                    {
                        ResourceType = MapResource(cost.ResourceStableId),
                        Amount = cost.Amount
                    });
                }

                levels.Add(new BuildingConstructionLevelDefinition
                {
                    TargetLevel = costLevel.TargetLevel,
                    DurationSeconds = durationLevel.DurationSeconds,
                    Costs = costs
                });
            }

            return levels;
        }

        private void ProjectTroops(GameDataFamilyCatalogSnapshot family)
        {
            foreach (var record in family.Records)
            {
                var definition = ProjectTroop(record, record.Id);
                _troops[record.Id] = definition;
                foreach (var alias in family.Aliases)
                {
                    if (string.Equals(alias.CanonicalId, record.Id, StringComparison.Ordinal))
                    {
                        _troops[alias.LegacyId] = ProjectTroop(record, alias.LegacyId);
                    }
                }
            }
        }

        private static TroopDefinition ProjectTroop(GameDataCatalogRecord record, string exposedId)
        {
            var definition = ScriptableObject.CreateInstance<TroopDefinition>();
            definition.Id = exposedId;
            var legacyType = ReadString(record, "legacy_troop_type");
            TroopType troopType;
            if (!Enum.TryParse(legacyType, false, out troopType))
            {
                throw new InvalidOperationException(
                    "Troop record '" + record.Id + "' has an invalid legacy_troop_type.");
            }

            definition.Type = troopType;
            definition.DisplayName = legacyType;
            definition.BaseAttack = ReadInt32(record, "base_attack");
            definition.BaseDefense = ReadInt32(record, "base_defense");
            return definition;
        }

        private void ProjectSkills(GameDataFamilyCatalogSnapshot family)
        {
            foreach (var record in family.Records)
            {
                var definition = ProjectSkill(record, record.Id);
                _skills[record.Id] = definition;
                foreach (var alias in family.Aliases)
                {
                    if (string.Equals(alias.CanonicalId, record.Id, StringComparison.Ordinal))
                    {
                        _skills[alias.LegacyId] = ProjectSkill(record, alias.LegacyId);
                    }
                }
            }
        }

        private static SkillDefinition ProjectSkill(GameDataCatalogRecord record, string exposedId)
        {
            var definition = ScriptableObject.CreateInstance<SkillDefinition>();
            definition.Id = exposedId;
            var nameRef = ReadString(record, "name_ref");
            definition.DisplayName = string.IsNullOrEmpty(nameRef) ? exposedId : nameRef;
            definition.TargetType = MapSkillTarget(ReadString(record, "target_type"));
            definition.Cooldown = ReadSingle(record, "cooldown_seconds");
            definition.Power = ReadSingle(record, "power");
            return definition;
        }

        private void ProjectChampions(GameDataFamilyCatalogSnapshot family)
        {
            foreach (var record in family.Records)
            {
                var definition = ProjectChampion(record, record.Id);
                _champions[record.Id] = definition;
                _canonicalChampionIds.Add(record.Id);
                foreach (var alias in family.Aliases)
                {
                    if (string.Equals(alias.CanonicalId, record.Id, StringComparison.Ordinal))
                    {
                        _champions[alias.LegacyId] = ProjectChampion(record, alias.LegacyId);
                    }
                }
            }
        }

        private ChampionDefinition ProjectChampion(GameDataCatalogRecord record, string exposedId)
        {
            var definition = ScriptableObject.CreateInstance<ChampionDefinition>();
            definition.Id = exposedId;
            var nameRef = ReadString(record, "name_ref");
            definition.DisplayName = string.IsNullOrEmpty(nameRef) ? exposedId : nameRef;

            var realmStableId = ReadString(record, "realm_id");
            RealmId realmId;
            if (!TryMapRealmStableId(realmStableId, out realmId))
            {
                throw new InvalidOperationException(
                    "Champion record '" + record.Id + "' has an invalid realm_id.");
            }

            definition.Realm = realmId;

            ClassFamily family;
            if (!Enum.TryParse(ReadString(record, "class_family_id"), true, out family))
            {
                throw new InvalidOperationException(
                    "Champion record '" + record.Id + "' has an invalid class_family_id.");
            }

            definition.Family = family;
            definition.BaseSkills = ResolveChampionSkills(record);

            GameDataChampionSliceRegistry.Overlay overlay;
            if (!GameDataChampionSliceRegistry.TryGet(record.Id, out overlay))
            {
                throw new InvalidOperationException(
                    "Champion record '" + record.Id + "' has no reviewed slice overlay.");
            }

            SubclassId subclass;
            if (!Enum.TryParse(overlay.SubclassId, false, out subclass) || subclass == SubclassId.None)
            {
                throw new InvalidOperationException(
                    "Champion record '" + record.Id + "' has an invalid overlay subclass.");
            }

            if (!string.IsNullOrEmpty(overlay.DisplayName))
            {
                definition.DisplayName = overlay.DisplayName;
            }

            definition.Subclass = subclass;
            definition.WeaponStyleId = overlay.WeaponStyleId;
            definition.OffhandStyleId = overlay.OffhandStyleId;
            definition.BaseStats = new ChampionBaseStats
            {
                MaxHealth = overlay.MaxHealth,
                MaxMana = overlay.MaxMana,
                Attack = overlay.Attack,
                Defense = overlay.Defense,
                Speed = overlay.Speed,
                CritRate = overlay.CritRate
            };
            return definition;
        }

        private SkillDefinition[] ResolveChampionSkills(GameDataCatalogRecord record)
        {
            GameDataValue value;
            if (!record.TryGetField("base_skill_ids", out value))
            {
                return Array.Empty<SkillDefinition>();
            }

            var array = value as GameDataArrayValue;
            if (array == null)
            {
                return Array.Empty<SkillDefinition>();
            }

            var skills = new List<SkillDefinition>(array.Items.Count);
            for (var index = 0; index < array.Items.Count; index++)
            {
                var item = array.Items[index] as GameDataStringValue;
                if (item == null)
                {
                    continue;
                }

                SkillDefinition skill;
                if (_skills.TryGetValue(item.Value, out skill))
                {
                    skills.Add(skill);
                }
            }

            return skills.ToArray();
        }

        private static bool TryMapRealmStableId(string stableId, out RealmId realmId)
        {
            if (string.Equals(stableId, "stonehold", StringComparison.Ordinal))
            {
                realmId = RealmId.Stonehold;
                return true;
            }

            if (string.Equals(stableId, "eldergrove", StringComparison.Ordinal))
            {
                realmId = RealmId.Eldergrove;
                return true;
            }

            if (string.Equals(stableId, "crownlands", StringComparison.Ordinal))
            {
                realmId = RealmId.Crownlands;
                return true;
            }

            if (string.Equals(stableId, "umbral", StringComparison.Ordinal))
            {
                realmId = RealmId.Umbral;
                return true;
            }

            realmId = RealmId.None;
            return false;
        }

        private static SkillTargetType MapSkillTarget(string targetType)
        {
            if (string.Equals(targetType, "enemy", StringComparison.Ordinal)) return SkillTargetType.Enemy;
            if (string.Equals(targetType, "self", StringComparison.Ordinal)) return SkillTargetType.Self;
            if (string.Equals(targetType, "aoe", StringComparison.Ordinal)) return SkillTargetType.AoE;
            if (string.Equals(targetType, "ally", StringComparison.Ordinal)) return SkillTargetType.Ally;
            if (string.Equals(targetType, "single", StringComparison.Ordinal)) return SkillTargetType.Single;
            throw new InvalidOperationException("Unknown skill target_type '" + targetType + "'.");
        }

        private static ResourceType MapResource(string resourceStableId)
        {
            if (string.Equals(resourceStableId, "wood", StringComparison.Ordinal)) return ResourceType.Wood;
            if (string.Equals(resourceStableId, "stone", StringComparison.Ordinal)) return ResourceType.Stone;
            if (string.Equals(resourceStableId, "gold", StringComparison.Ordinal)) return ResourceType.Gold;
            if (string.Equals(resourceStableId, "mana_stone", StringComparison.Ordinal)) return ResourceType.ManaStone;
            if (string.Equals(resourceStableId, "ore", StringComparison.Ordinal)) return ResourceType.Ore;
            if (string.Equals(resourceStableId, "food", StringComparison.Ordinal)) return ResourceType.Food;
            throw new InvalidOperationException("Unknown resource stable id '" + resourceStableId + "'.");
        }

        private static string FormatBuildingDisplayName(string legacyOrCanonical)
        {
            if (string.IsNullOrEmpty(legacyOrCanonical))
            {
                return legacyOrCanonical;
            }

            return legacyOrCanonical
                .Replace("Mill", " Mill")
                .Replace("Hall", " Hall")
                .Replace("Mine", " Mine");
        }

        private static string ReadString(GameDataCatalogRecord record, string fieldName)
        {
            GameDataValue value;
            if (!record.TryGetField(fieldName, out value))
            {
                return null;
            }

            var typed = value as GameDataStringValue;
            return typed != null ? typed.Value : null;
        }

        private static int ReadInt32(GameDataCatalogRecord record, string fieldName)
        {
            GameDataValue value;
            if (!record.TryGetField(fieldName, out value))
            {
                throw new InvalidOperationException(
                    "Record '" + record.Id + "' is missing integer field '" + fieldName + "'.");
            }

            var typed = value as GameDataNumberValue;
            long parsed;
            if (typed == null || !typed.TryGetInt64(out parsed) || parsed < int.MinValue || parsed > int.MaxValue)
            {
                throw new InvalidOperationException(
                    "Record '" + record.Id + "' field '" + fieldName + "' is not an int32.");
            }

            return (int)parsed;
        }

        private static float ReadSingle(GameDataCatalogRecord record, string fieldName)
        {
            GameDataValue value;
            if (!record.TryGetField(fieldName, out value))
            {
                throw new InvalidOperationException(
                    "Record '" + record.Id + "' is missing number field '" + fieldName + "'.");
            }

            var typed = value as GameDataNumberValue;
            if (typed == null)
            {
                throw new InvalidOperationException(
                    "Record '" + record.Id + "' field '" + fieldName + "' is not a number.");
            }

            return (float)typed.Value;
        }
    }
}
