using System;
using System.Collections.Generic;
using System.IO;
using AL.Core;
using AL.Data.Catalogs;
using AL.Data.Definitions;
using AL.VerticalSlice;
using UnityEngine;

namespace AL.Services.Local
{
    /// <summary>
    /// Loads the packaged six-family slice catalogs and projects them onto the
    /// runtime definition types. Missing or invalid catalogs fail closed.
    /// </summary>
    public static class SixFamilyRuntimeCatalog
    {
        public const string DefaultChampionId = "champion_stonehold_vanguard";
        public const string RealmsFileName = "realms.json";
        public const string BuildingsFileName = "buildings.json";
        public const string ChampionsFileName = "champions.json";
        public const string SkillsFileName = "skills.json";
        public const string ChampionRuntimeFileName = "champion_runtime.json";
        public const string DefendMitigationReadyCode =
            "AL-GDC-DEFEND-MITIGATION-READY";
        public const string DefendMitigationMissingCode =
            "AL-GDC-DEFEND-MITIGATION-MISSING";
        public const string DefendMitigationAmbiguousCode =
            "AL-GDC-DEFEND-MITIGATION-AMBIGUOUS";
        public const string DefendMitigationInvalidCode =
            "AL-GDC-DEFEND-MITIGATION-INVALID";

        public static bool TryResolveGameDataDirectory(out string directory)
        {
            var candidates = new[]
            {
                Path.Combine(Application.dataPath, "AL", "StreamingAssets", "GameData"),
                Path.Combine(
                    (Application.streamingAssetsPath ?? string.Empty).TrimEnd('/', '\\'),
                    "GameData")
            };

            for (var index = 0; index < candidates.Length; index++)
            {
                var candidate = candidates[index];
                if (!string.IsNullOrEmpty(candidate) &&
                    File.Exists(Path.Combine(candidate, ChampionsFileName)) &&
                    File.Exists(Path.Combine(candidate, BuildingsFileName)) &&
                    File.Exists(Path.Combine(candidate, RealmsFileName)) &&
                    File.Exists(Path.Combine(candidate, SkillsFileName)) &&
                    File.Exists(Path.Combine(candidate, ChampionRuntimeFileName)))
                {
                    directory = candidate;
                    return true;
                }
            }

            directory = null;
            return false;
        }

        public static bool TryLoad(out SixFamilyRuntimeSnapshot snapshot, out string diagnosticCode)
        {
            snapshot = null;
            diagnosticCode = "AL-GDC-MISSING";
            string directory;
            if (!TryResolveGameDataDirectory(out directory))
            {
                return false;
            }

            return TryLoadFromDirectory(directory, out snapshot, out diagnosticCode);
        }

        public static SixFamilyRuntimeSnapshot LoadOrThrow()
        {
            SixFamilyRuntimeSnapshot snapshot;
            string code;
            if (!TryLoad(out snapshot, out code))
            {
                throw new InvalidOperationException(
                    "AL-GDC-SIX-FAMILY-MISSING: packaged six-family catalogs failed (" +
                    code +
                    ").");
            }

            return snapshot;
        }

        public static bool TryLoadFromDirectory(
            string directory,
            out SixFamilyRuntimeSnapshot snapshot,
            out string diagnosticCode)
        {
            snapshot = null;
            diagnosticCode = "AL-GDC-MISSING";
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return false;
            }

            GameDataFamilyCatalogSnapshot realms;
            GameDataFamilyCatalogSnapshot buildings;
            GameDataFamilyCatalogSnapshot champions;
            IReadOnlyDictionary<string, GameDataFamilyCatalogSnapshot> families;
            if (!TryLoadFamilySet(directory, out families, out diagnosticCode) ||
                !families.TryGetValue("realms", out realms) ||
                !families.TryGetValue("buildings", out buildings) ||
                !families.TryGetValue("champions", out champions))
            {
                return false;
            }

            ChampionRuntimeFile runtime;
            if (!TryLoadRuntime(Path.Combine(directory, ChampionRuntimeFileName), out runtime, out diagnosticCode))
            {
                return false;
            }

            try
            {
                snapshot = Project(realms, buildings, champions, runtime);
            }
            catch (Exception exception)
            {
                diagnosticCode = "AL-GDC-PROJECT: " + exception.Message;
                snapshot = null;
                return false;
            }

            diagnosticCode = "AL-GDC-READY";
            return true;
        }

        public static bool TryGetDefaultChampion(out SliceChampionProfile profile, out string diagnosticCode)
        {
            profile = null;
            SixFamilyRuntimeSnapshot snapshot;
            if (!TryLoad(out snapshot, out diagnosticCode))
            {
                return false;
            }

            return snapshot.TryCreateSliceProfile(snapshot.DefaultChampionId, out profile, out diagnosticCode);
        }

        /// <summary>
        /// Resolves the one catalog champion that owns a realm's defend tuning.
        /// Runtime combat never substitutes a code default when this authority is
        /// missing, ambiguous, or invalid.
        /// </summary>
        public static bool TryResolveDefendMitigation(
            RealmId realmId,
            out float mitigation,
            out string diagnosticCode)
        {
            mitigation = 0f;
            SixFamilyRuntimeSnapshot snapshot;
            if (!TryLoad(out snapshot, out diagnosticCode))
            {
                diagnosticCode = DefendMitigationMissingCode + ":" + diagnosticCode;
                return false;
            }

            return snapshot.TryResolveDefendMitigation(
                realmId,
                out mitigation,
                out diagnosticCode);
        }

        private static bool TryLoadFamilySet(
            string directory,
            out IReadOnlyDictionary<string, GameDataFamilyCatalogSnapshot> families,
            out string diagnosticCode)
        {
            families = null;
            diagnosticCode = "AL-GDC-MISSING";
            var required = new[]
            {
                new KeyValuePair<string, string>("realms", RealmsFileName),
                new KeyValuePair<string, string>("buildings", BuildingsFileName),
                new KeyValuePair<string, string>("champions", ChampionsFileName),
                new KeyValuePair<string, string>("skills", SkillsFileName)
            };

            var payloads = new List<KeyValuePair<string, string>>(required.Length);
            for (var index = 0; index < required.Length; index++)
            {
                var path = Path.Combine(directory, required[index].Value);
                if (!File.Exists(path))
                {
                    diagnosticCode = "AL-GDC-MISSING:" + required[index].Value;
                    return false;
                }

                try
                {
                    payloads.Add(
                        new KeyValuePair<string, string>(
                            required[index].Key,
                            File.ReadAllText(path)));
                }
                catch (Exception exception)
                {
                    diagnosticCode = "AL-GDC-READ:" + required[index].Value + ":" + exception.GetType().Name;
                    return false;
                }
            }

            return SixFamilyCatalogLoader.TryLoadSet(payloads, out families, out diagnosticCode);
        }

        private static bool TryLoadRuntime(
            string path,
            out ChampionRuntimeFile runtime,
            out string diagnosticCode)
        {
            runtime = null;
            diagnosticCode = "AL-GDC-MISSING:" + ChampionRuntimeFileName;
            if (!File.Exists(path))
            {
                return false;
            }

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception exception)
            {
                diagnosticCode = "AL-GDC-READ:" + ChampionRuntimeFileName + ":" + exception.GetType().Name;
                return false;
            }

            runtime = JsonUtility.FromJson<ChampionRuntimeFile>(json);
            if (runtime == null || runtime.records == null || runtime.records.Length == 0)
            {
                diagnosticCode = "AL-GDC-INVALID:" + ChampionRuntimeFileName;
                runtime = null;
                return false;
            }

            diagnosticCode = "AL-GDC-READY";
            return true;
        }

        private static SixFamilyRuntimeSnapshot Project(
            GameDataFamilyCatalogSnapshot realmsFamily,
            GameDataFamilyCatalogSnapshot buildingsFamily,
            GameDataFamilyCatalogSnapshot championsFamily,
            ChampionRuntimeFile runtime)
        {
            var realms = new List<RealmDefinition>(realmsFamily.Records.Count);
            var realmsById = new Dictionary<RealmId, RealmDefinition>();
            for (var index = 0; index < realmsFamily.Records.Count; index++)
            {
                var record = realmsFamily.Records[index];
                var realmId = ParseRealmId(record.Id);
                string nameRef;
                string descriptionRef;
                if (!WireFamilyCatalogLoader.TryGetString(record, "name_ref", out nameRef) ||
                    !WireFamilyCatalogLoader.TryGetString(record, "description_ref", out descriptionRef))
                {
                    throw new InvalidOperationException("Realm record " + record.Id + " is missing content refs.");
                }

                var definition = ScriptableObject.CreateInstance<RealmDefinition>();
                definition.Id = realmId;
                definition.RealmName = ResolveRealmName(record.Id, nameRef);
                definition.Description = ResolveRealmDescription(record.Id, descriptionRef);
                realms.Add(definition);
                realmsById[realmId] = definition;
            }

            if (realms.Count != 4)
            {
                throw new InvalidOperationException("Realm catalog must contain exactly four records.");
            }

            var buildings = new Dictionary<string, BuildingDefinition>(StringComparer.Ordinal);
            for (var index = 0; index < buildingsFamily.Records.Count; index++)
            {
                var record = buildingsFamily.Records[index];
                GameDataBuildingProgressionReference reference;
                if (!GameDataBuildingProgressionRegistry.TryGetByStableId(record.Id, out reference))
                {
                    throw new InvalidOperationException("Building " + record.Id + " is not in the progression registry.");
                }

                var definition = ScriptableObject.CreateInstance<BuildingDefinition>();
                definition.Id = reference.LegacyBuildingId;
                definition.DisplayName = TitleCaseStableId(record.Id);
                definition.MaxLevel = reference.MaximumLevel;
                definition.ConstructionLevels = ProjectConstructionLevels(reference);
                buildings[reference.LegacyBuildingId] = definition;
                buildings[record.Id] = definition;
            }

            if (buildings.Count < GameDataBuildingProgressionRegistry.BuildingCount)
            {
                throw new InvalidOperationException("Building catalog is missing required identities.");
            }

            var runtimeById = new Dictionary<string, ChampionRuntimeRecord>(StringComparer.Ordinal);
            for (var index = 0; index < runtime.records.Length; index++)
            {
                var row = runtime.records[index];
                if (row == null || string.IsNullOrEmpty(row.id))
                {
                    throw new InvalidOperationException("Champion runtime record is missing an id.");
                }

                runtimeById[row.id] = row;
            }

            var champions = new List<ChampionDefinition>(championsFamily.Records.Count);
            var championsById = new Dictionary<string, ChampionDefinition>(StringComparer.Ordinal);
            for (var index = 0; index < championsFamily.Records.Count; index++)
            {
                var record = championsFamily.Records[index];
                ChampionRuntimeRecord runtimeRecord;
                if (!runtimeById.TryGetValue(record.Id, out runtimeRecord))
                {
                    throw new InvalidOperationException(
                        "Champion " + record.Id + " has no champion_runtime.json stats.");
                }

                var definition = ProjectChampion(record, runtimeRecord);
                champions.Add(definition);
                championsById[definition.Id] = definition;
            }

            if (champions.Count == 0)
            {
                throw new InvalidOperationException("Champion catalog is empty.");
            }

            var defaultId = string.IsNullOrEmpty(runtime.default_champion_id)
                ? DefaultChampionId
                : runtime.default_champion_id;
            if (!championsById.ContainsKey(defaultId))
            {
                throw new InvalidOperationException("Default champion " + defaultId + " is not in the catalog.");
            }

            return new SixFamilyRuntimeSnapshot(
                realms,
                realmsById,
                buildings,
                champions,
                championsById,
                runtimeById,
                defaultId);
        }

        private static ChampionDefinition ProjectChampion(
            GameDataCatalogRecord record,
            ChampionRuntimeRecord runtime)
        {
            var definition = ScriptableObject.CreateInstance<ChampionDefinition>();
            definition.Id = record.Id;
            definition.DisplayName = runtime.display_name;
            definition.Realm = ParseRealmId(runtime.realm_id);
            definition.Family = ParseClassFamily(runtime.class_family_id);
            definition.Subclass = ParseSubclass(runtime.subclass_id);
            definition.WeaponStyleId = runtime.weapon_style_id;
            definition.OffhandStyleId = runtime.offhand_style_id;
            definition.BaseStats = new ChampionBaseStats
            {
                MaxHealth = runtime.max_health,
                MaxMana = runtime.max_mana,
                Attack = runtime.attack,
                Defense = runtime.defense,
                Speed = runtime.speed,
                CritRate = runtime.crit_rate
            };

            var skills = runtime.skills ?? Array.Empty<ChampionRuntimeSkill>();
            definition.BaseSkills = new SkillDefinition[skills.Length];
            for (var index = 0; index < skills.Length; index++)
            {
                var skillRow = skills[index];
                var skill = ScriptableObject.CreateInstance<SkillDefinition>();
                skill.Id = skillRow.id;
                skill.DisplayName = skillRow.display_name;
                skill.TargetType = ParseTargetType(skillRow.target_type);
                skill.Cooldown = skillRow.cooldown_seconds;
                skill.Power = skillRow.power;
                definition.BaseSkills[index] = skill;
            }

            return definition;
        }

        private static List<BuildingConstructionLevelDefinition> ProjectConstructionLevels(
            GameDataBuildingProgressionReference reference)
        {
            GameDataBuildingCostProfile costProfile;
            if (!GameDataBuildingProgressionRegistry.TryGetCostProfileByStableId(
                    reference.CostProfileStableId,
                    out costProfile))
            {
                throw new InvalidOperationException(
                    "Missing cost profile " + reference.CostProfileStableId + ".");
            }

            var durationProfile = GameDataBuildingProgressionRegistry.DurationProfile;
            var levels = new List<BuildingConstructionLevelDefinition>(
                GameDataBuildingProgressionRegistry.TargetLevelCount);
            for (var target = 1; target <= GameDataBuildingProgressionRegistry.TargetLevelCount; target++)
            {
                GameDataBuildingCostLevel costLevel;
                GameDataBuildingDurationLevel durationLevel;
                if (!costProfile.TryGetLevel(target, out costLevel) ||
                    !durationProfile.TryGetLevel(target, out durationLevel))
                {
                    throw new InvalidOperationException(
                        "Building " + reference.StableId + " is missing level " + target + ".");
                }

                var costs = new List<BuildingConstructionCostDefinition>(costLevel.Costs.Count);
                for (var index = 0; index < costLevel.Costs.Count; index++)
                {
                    var amount = costLevel.Costs[index];
                    costs.Add(
                        new BuildingConstructionCostDefinition
                        {
                            ResourceType = MapResource(amount.ResourceStableId),
                            Amount = amount.Amount
                        });
                }

                levels.Add(
                    new BuildingConstructionLevelDefinition
                    {
                        TargetLevel = target,
                        DurationSeconds = durationLevel.DurationSeconds,
                        Costs = costs
                    });
            }

            return levels;
        }

        private static ResourceType MapResource(string stableId)
        {
            GameDataWalletResourceReference resource;
            if (!GameDataWalletResourceReferences.TryGetByStableId(stableId, out resource))
            {
                throw new InvalidOperationException("Unknown resource " + stableId + ".");
            }

            return (ResourceType)Enum.Parse(typeof(ResourceType), resource.LegacyEnumName);
        }

        private static RealmId ParseRealmId(string stableId)
        {
            GameDataRealmReference reference;
            if (GameDataRealmReferences.TryGetByStableId(stableId, out reference))
            {
                return (RealmId)reference.LegacyRealmValue;
            }

            throw new InvalidOperationException("Unknown realm " + stableId + ".");
        }

        private static ClassFamily ParseClassFamily(string value)
        {
            if (string.Equals(value, "warrior", StringComparison.Ordinal)) return ClassFamily.Warrior;
            if (string.Equals(value, "mage", StringComparison.Ordinal)) return ClassFamily.Mage;
            if (string.Equals(value, "ranger", StringComparison.Ordinal)) return ClassFamily.Ranger;
            if (string.Equals(value, "assassin", StringComparison.Ordinal)) return ClassFamily.Assassin;
            throw new InvalidOperationException("Unknown class family " + value + ".");
        }

        private static SubclassId ParseSubclass(string value)
        {
            if (string.Equals(value, "vanguard", StringComparison.Ordinal)) return SubclassId.Vanguard;
            if (string.Equals(value, "archmage", StringComparison.Ordinal)) return SubclassId.Archmage;
            if (string.Equals(value, "sharpshooter", StringComparison.Ordinal)) return SubclassId.Sharpshooter;
            if (string.Equals(value, "shadowblade", StringComparison.Ordinal)) return SubclassId.Shadowblade;
            throw new InvalidOperationException("Unknown subclass " + value + ".");
        }

        private static SkillTargetType ParseTargetType(string value)
        {
            if (string.Equals(value, "single", StringComparison.Ordinal)) return SkillTargetType.Single;
            if (string.Equals(value, "aoe", StringComparison.Ordinal)) return SkillTargetType.AoE;
            if (string.Equals(value, "self", StringComparison.Ordinal)) return SkillTargetType.Self;
            if (string.Equals(value, "ally", StringComparison.Ordinal)) return SkillTargetType.Ally;
            if (string.Equals(value, "enemy", StringComparison.Ordinal)) return SkillTargetType.Enemy;
            throw new InvalidOperationException("Unknown target type " + value + ".");
        }

        private static string TitleCaseStableId(string stableId)
        {
            var parts = stableId.Split('_');
            for (var index = 0; index < parts.Length; index++)
            {
                if (parts[index].Length == 0)
                {
                    continue;
                }

                parts[index] = char.ToUpperInvariant(parts[index][0]) + parts[index].Substring(1);
            }

            return string.Join(" ", parts);
        }

        private static string ResolveRealmName(string realmId, string nameRef)
        {
            switch (realmId)
            {
                case "stonehold": return "Stonehold Dwarves";
                case "eldergrove": return "Eldergrove Elves";
                case "crownlands": return "Crownlands Humans";
                case "umbral": return "Umbral Dark Elves";
                default: return nameRef;
            }
        }

        private static string ResolveRealmDescription(string realmId, string descriptionRef)
        {
            switch (realmId)
            {
                case "stonehold":
                    return "Mountain kings and master smiths.\n\nPerks:\n+20% Stone\n+10% Def\n\nPerks: Resilience";
                case "eldergrove":
                    return "Forest guardians and peerless mages.\n\nPerks:\n+20% Wood\n+15% Magic\n\nPerks: Harmony";
                case "crownlands":
                    return "Adaptive leaders of the central plains.\n\nPerks:\n+15% Gold\n+10% All Atk\n\nPerks: Ambition";
                case "umbral":
                    return "Masters of shadow and volcanic power.\n\nPerks:\n+20% Crit\n+15% Speed\n\nPerks: Cunning";
                default:
                    return descriptionRef;
            }
        }

        [Serializable]
        private sealed class ChampionRuntimeFile
        {
            public int schemaVersion;
            public string contentVersion;
            public string sourceRevision;
            public string default_champion_id;
            public ChampionRuntimeRecord[] records;
        }

        [Serializable]
        internal sealed class ChampionRuntimeRecord
        {
            public string id;
            public string display_name;
            public string realm_id;
            public string class_family_id;
            public string subclass_id;
            public string weapon_style_id;
            public string offhand_style_id;
            public int max_health;
            public int max_mana;
            public int attack;
            public int defense;
            public int speed;
            public int crit_rate;
            public int special_power;
            public float defend_mitigation;
            public ChampionRuntimeSkill[] skills;
        }

        [Serializable]
        internal sealed class ChampionRuntimeSkill
        {
            public string id;
            public string display_name;
            public string target_type;
            public float cooldown_seconds;
            public float power;
        }
    }

    public sealed class SixFamilyRuntimeSnapshot
    {
        private readonly IReadOnlyList<RealmDefinition> realms;
        private readonly IReadOnlyDictionary<RealmId, RealmDefinition> realmsById;
        private readonly IReadOnlyDictionary<string, BuildingDefinition> buildings;
        private readonly IReadOnlyList<ChampionDefinition> champions;
        private readonly IReadOnlyDictionary<string, ChampionDefinition> championsById;
        private readonly IReadOnlyDictionary<string, SixFamilyRuntimeCatalog.ChampionRuntimeRecord> runtimeById;

        internal SixFamilyRuntimeSnapshot(
            IReadOnlyList<RealmDefinition> realms,
            IReadOnlyDictionary<RealmId, RealmDefinition> realmsById,
            IReadOnlyDictionary<string, BuildingDefinition> buildings,
            IReadOnlyList<ChampionDefinition> champions,
            IReadOnlyDictionary<string, ChampionDefinition> championsById,
            IReadOnlyDictionary<string, SixFamilyRuntimeCatalog.ChampionRuntimeRecord> runtimeById,
            string defaultChampionId)
        {
            this.realms = realms;
            this.realmsById = realmsById;
            this.buildings = buildings;
            this.champions = champions;
            this.championsById = championsById;
            this.runtimeById = runtimeById;
            DefaultChampionId = defaultChampionId;
        }

        public string DefaultChampionId { get; }

        public RealmDefinition GetRealm(RealmId id)
        {
            RealmDefinition realm;
            return realmsById.TryGetValue(id, out realm) ? realm : null;
        }

        public IEnumerable<RealmDefinition> GetAllRealms()
        {
            return realms;
        }

        public BuildingDefinition GetBuilding(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            BuildingDefinition building;
            return buildings.TryGetValue(id, out building) ? building : null;
        }

        public ChampionDefinition GetChampion(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            ChampionDefinition champion;
            return championsById.TryGetValue(id, out champion) ? champion : null;
        }

        public IEnumerable<ChampionDefinition> GetAllChampions()
        {
            return champions;
        }

        public bool TryResolveDefendMitigation(
            RealmId realmId,
            out float mitigation,
            out string diagnosticCode)
        {
            mitigation = 0f;
            diagnosticCode = SixFamilyRuntimeCatalog.DefendMitigationInvalidCode;
            if (realmId == RealmId.None ||
                !Enum.IsDefined(typeof(RealmId), realmId))
            {
                return false;
            }

            ChampionDefinition realmChampion = null;
            var matchCount = 0;
            for (var index = 0; index < champions.Count; index++)
            {
                var candidate = champions[index];
                if (candidate == null || candidate.Realm != realmId)
                {
                    continue;
                }

                matchCount++;
                realmChampion = candidate;
            }

            if (matchCount == 0 || realmChampion == null)
            {
                diagnosticCode = SixFamilyRuntimeCatalog.DefendMitigationMissingCode;
                return false;
            }

            if (matchCount != 1)
            {
                diagnosticCode = SixFamilyRuntimeCatalog.DefendMitigationAmbiguousCode;
                return false;
            }

            SixFamilyRuntimeCatalog.ChampionRuntimeRecord runtime;
            if (string.IsNullOrEmpty(realmChampion.Id) ||
                !runtimeById.TryGetValue(realmChampion.Id, out runtime) ||
                runtime == null)
            {
                diagnosticCode = SixFamilyRuntimeCatalog.DefendMitigationMissingCode;
                return false;
            }

            var candidateMitigation = runtime.defend_mitigation;
            if (float.IsNaN(candidateMitigation) ||
                float.IsInfinity(candidateMitigation) ||
                candidateMitigation < 0f ||
                candidateMitigation > 1f)
            {
                diagnosticCode = SixFamilyRuntimeCatalog.DefendMitigationInvalidCode;
                return false;
            }

            mitigation = candidateMitigation;
            diagnosticCode = SixFamilyRuntimeCatalog.DefendMitigationReadyCode;
            return true;
        }

        public bool TryCreateSliceProfile(
            string championId,
            out SliceChampionProfile profile,
            out string diagnosticCode)
        {
            profile = null;
            diagnosticCode = "AL-GDC-CHAMPION-MISSING";
            var resolvedId = string.IsNullOrEmpty(championId) ? DefaultChampionId : championId;
            ChampionDefinition definition;
            SixFamilyRuntimeCatalog.ChampionRuntimeRecord runtime;
            if (!championsById.TryGetValue(resolvedId, out definition) ||
                !runtimeById.TryGetValue(resolvedId, out runtime))
            {
                return false;
            }

            profile = new SliceChampionProfile(
                definition.Id,
                definition.DisplayName,
                definition.Subclass.ToString(),
                runtime.max_health,
                runtime.max_mana,
                runtime.attack,
                runtime.special_power,
                runtime.defend_mitigation);
            diagnosticCode = "AL-GDC-READY";
            return true;
        }
    }
}
