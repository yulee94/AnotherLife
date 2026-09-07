using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AL.Data.Catalogs;

namespace AL.Strongholds
{
    public enum StrongholdMilestone { Standard, MajorGateMilestone, CapstoneMilestone }

    public sealed class StrongholdLevel
    {
        internal StrongholdLevel(StrictJsonObject value)
        {
            StrongholdCatalog.Keys(value, "level", "milestone", "commandNpcRequired", "mageGuardsRequired", "fullGateUpgrade",
                "visualProfileId", "gateProfileId", "guardRosterProfileId", "upgradeCostProfileId", "guardStatsProfileId",
                "survivorRegenerationProfileId", "reinforcementTimingProfileId", "balance");
            Level = StrongholdCatalog.Number(value, "level");
            Milestone = (StrongholdMilestone)Enum.Parse(typeof(StrongholdMilestone), StrongholdCatalog.Text(value, "milestone"));
            CommandNpcRequired = StrongholdCatalog.Flag(value, "commandNpcRequired");
            MageGuardsRequired = StrongholdCatalog.Flag(value, "mageGuardsRequired");
            FullGateUpgrade = StrongholdCatalog.Flag(value, "fullGateUpgrade");
            VisualProfileId = StrongholdCatalog.Text(value, "visualProfileId");
            GateProfileId = StrongholdCatalog.Text(value, "gateProfileId");
            GuardRosterProfileId = StrongholdCatalog.Text(value, "guardRosterProfileId");
            UpgradeCostProfileId = StrongholdCatalog.Text(value, "upgradeCostProfileId");
            GuardStatsProfileId = StrongholdCatalog.Text(value, "guardStatsProfileId");
            SurvivorRegenerationProfileId = StrongholdCatalog.Text(value, "survivorRegenerationProfileId");
            ReinforcementTimingProfileId = StrongholdCatalog.Text(value, "reinforcementTimingProfileId");
            string[] prefixes = { "stronghold_visual_", "stronghold_gate_", "stronghold_roster_", "stronghold_cost_",
                "stronghold_stats_", "stronghold_survivor_regeneration_", "stronghold_reinforcement_timing_" };
            StrongholdCatalog.Require(References.Zip(prefixes, (id, prefix) => id.StartsWith(prefix, StringComparison.Ordinal)).All(valid => valid));
            StrongholdCatalog.Require(StrongholdCatalog.Get(value, "balance") is StrictJsonNull);
        }
        public int Level { get; }
        public StrongholdMilestone Milestone { get; }
        public bool CommandNpcRequired { get; }
        public bool MageGuardsRequired { get; }
        public bool FullGateUpgrade { get; }
        public string VisualProfileId { get; }
        public string GateProfileId { get; }
        public string GuardRosterProfileId { get; }
        public string UpgradeCostProfileId { get; }
        public string GuardStatsProfileId { get; }
        public string SurvivorRegenerationProfileId { get; }
        public string ReinforcementTimingProfileId { get; }
        public bool BalanceApproved => false;
        internal IEnumerable<string> References => new[] { VisualProfileId, GateProfileId, GuardRosterProfileId,
            UpgradeCostProfileId, GuardStatsProfileId, SurvivorRegenerationProfileId, ReinforcementTimingProfileId };
    }

    public sealed class StrongholdDefinition
    {
        internal StrongholdDefinition(StrictJsonObject value)
        {
            StrongholdCatalog.Keys(value, "id", "kind", "wallRingIds", "continuousWallRequired", "gateIds", "statueIds",
                "commandNpcId", "upgradeNpcId", "upgradeNpcRole");
            Id = StrongholdCatalog.Text(value, "id");
            Kind = StrongholdCatalog.Text(value, "kind");
            WallRingId = Single(value, "wallRingIds");
            GateId = Single(value, "gateIds");
            StatueId = Single(value, "statueIds");
            CommandNpcId = StrongholdCatalog.Text(value, "commandNpcId");
            UpgradeNpcId = StrongholdCatalog.Text(value, "upgradeNpcId");
            StrongholdCatalog.Require(StrongholdCatalog.Flag(value, "continuousWallRequired") &&
                (Kind == "Fortress" || Kind == "Castle") && StrongholdCatalog.Text(value, "upgradeNpcRole") == "Unresolved");
            StrongholdCatalog.Require(Ids.All(StrongholdCatalog.IsId) && Ids.Distinct().Count() == Ids.Count());
        }
        public string Id { get; }
        public string Kind { get; }
        public string WallRingId { get; }
        public string GateId { get; }
        public string StatueId { get; }
        public string CommandNpcId { get; }
        public string UpgradeNpcId { get; }
        internal IEnumerable<string> Ids => new[] { Id, WallRingId, GateId, StatueId, CommandNpcId, UpgradeNpcId };
        private static string Single(StrictJsonObject value, string key)
        {
            var rows = StrongholdCatalog.Array(value, key);
            StrongholdCatalog.Require(rows.Count == 1 && rows[0] is StrictJsonString);
            return ((StrictJsonString)rows[0]).Value;
        }
    }

    /// <summary>Inert v1 source slots, not an asset/balance/server admission certificate.</summary>
    public sealed class StrongholdCatalog
    {
        private StrongholdCatalog(byte[] bytes)
        {
            var root = StrictJsonDocument.Parse(bytes, 65536) as StrictJsonObject;
            Keys(root, "catalogId", "schemaVersion", "contentVersion", "sourceRevision", "productionEligible", "scope",
                "mappingAuthority", "takeoverDurationMilliseconds", "ownerRareCostProfileId", "ownerRareResources",
                "territories", "strongholds", "levels");
            CatalogId = Text(root, "catalogId");
            SourceRevision = Text(root, "sourceRevision");
            ContentVersion = Number(root, "contentVersion");
            TakeoverDurationMilliseconds = Number(root, "takeoverDurationMilliseconds");
            OwnerRareCostProfileId = Text(root, "ownerRareCostProfileId");
            Require(CatalogId == "al_stronghold_catalog" && Number(root, "schemaVersion") == 1 && ContentVersion == 1 &&
                IsId(SourceRevision) && IsId(OwnerRareCostProfileId) && OwnerRareCostProfileId.EndsWith("_v1", StringComparison.Ordinal) &&
                !Flag(root, "productionEligible") && Text(root, "scope") == "engine_free_planner_only" &&
                Text(root, "mappingAuthority") == "legacy_flags_only_not_world_placement" && TakeoverDurationMilliseconds == 180000);
            var rare = Get(root, "ownerRareResources") as StrictJsonObject;
            Keys(rare, "stonehold", "eldergrove", "crownlands", "umbral");
            var resources = rare.Properties.ToDictionary(p => p.Name, p => Text(rare, p.Name), StringComparer.Ordinal);
            // v1 resource identity bridge, not quantities or balance. Unknown realms never fall back.
            Require(resources["stonehold"] == "DeepOre" && resources["eldergrove"] == "WorldSap" &&
                resources["crownlands"] == "RoyalSigil" && resources["umbral"] == "DarkCrystal");
            OwnerRareResources = new ReadOnlyDictionary<string, string>(resources);
            var definitions = Array(root, "strongholds").Select(x => new StrongholdDefinition(x as StrictJsonObject)).ToArray();
            Require(definitions.Length == 2 && definitions.SelectMany(d => d.Ids).Distinct().Count() == definitions.Length * 6);
            Strongholds = System.Array.AsReadOnly(definitions);
            var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var row in Array(root, "territories"))
            {
                var value = row as StrictJsonObject;
                Keys(value, "territoryId", "strongholdProfileId");
                string territory = Text(value, "territoryId");
                Require(IsId(territory));
                string profile = Get(value, "strongholdProfileId") is StrictJsonNull ? null : Text(value, "strongholdProfileId");
                Require(profile == null || definitions.Any(d => d.Id == profile));
                mapping.Add(territory, profile);
            }
            Require(mapping.Count == 5 && mapping.Values.Count(v => v != null) == definitions.Length &&
                mapping.Values.Where(v => v != null).Distinct().Count() == definitions.Length);
            // Compatibility fence, not new world placement: never convert the legacy non-fortress IDs.
            string[] legacyProfiles = { "stronghold_t1", null, null, "stronghold_t4", null };
            for (int i = 0; i < legacyProfiles.Length; i++)
                Require(mapping.TryGetValue("T" + (i + 1).ToString(CultureInfo.InvariantCulture), out var mapped) && mapped == legacyProfiles[i]);
            Territories = new ReadOnlyDictionary<string, string>(mapping);
            var levels = Array(root, "levels").Select(x => new StrongholdLevel(x as StrictJsonObject)).ToArray();
            Require(levels.Length == 10);
            for (int i = 0; i < levels.Length; i++)
            {
                var level = levels[i];
                Require(level.Level == i + 1 && level.CommandNpcRequired == (i >= 4) && level.MageGuardsRequired == (i >= 4) &&
                    level.FullGateUpgrade == (i >= 4) && level.Milestone == (i == 4 ? StrongholdMilestone.MajorGateMilestone :
                        i == 9 ? StrongholdMilestone.CapstoneMilestone : StrongholdMilestone.Standard));
                string suffix = "_l" + (i + 1).ToString("00", CultureInfo.InvariantCulture) + "_v1";
                Require(level.References.All(id => IsId(id) && id.EndsWith(suffix, StringComparison.Ordinal)) &&
                    level.References.Distinct().Count() == 7);
            }
            Levels = System.Array.AsReadOnly(levels);
            using (var sha = SHA256.Create())
                Hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        public string CatalogId { get; }
        public string SourceRevision { get; }
        public int ContentVersion { get; }
        public string Hash { get; }
        public int TakeoverDurationMilliseconds { get; }
        public string OwnerRareCostProfileId { get; }
        public bool ProductionEligible => false;
        public IReadOnlyDictionary<string, string> OwnerRareResources { get; }
        public IReadOnlyDictionary<string, string> Territories { get; }
        public IReadOnlyList<StrongholdDefinition> Strongholds { get; }
        public IReadOnlyList<StrongholdLevel> Levels { get; }
        public static bool TryLoad(byte[] bytes, out StrongholdCatalog catalog)
        {
            catalog = null;
            try { catalog = new StrongholdCatalog(bytes); return true; }
            catch (Exception error) when (error is StrictJsonException || error is ArgumentException || error is InvalidOperationException || error is OverflowException)
            { return false; }
        }
        internal static bool IsId(string value) => value != null && value.Length <= 128 && Regex.IsMatch(value, @"\A[A-Za-z][A-Za-z0-9_]*\z");
        internal static void Require(bool valid) { if (!valid) throw new ArgumentException("Invalid stronghold v1 contract"); }
        internal static void Keys(StrictJsonObject value, params string[] keys)
        {
            Require(value != null && value.Properties.Count == keys.Length && value.Properties.All(p => keys.Contains(p.Name)));
        }
        internal static StrictJsonValue Get(StrictJsonObject value, string key)
        {
            Require(value != null && value.TryGet(key, out _));
            value.TryGet(key, out var result); return result;
        }
        internal static string Text(StrictJsonObject value, string key)
        { var row = Get(value, key) as StrictJsonString; Require(row != null); return row.Value; }
        internal static int Number(StrictJsonObject value, string key)
        {
            var row = Get(value, key) as StrictJsonNumber;
            Require(row != null);
            Require(int.TryParse(row.RawValue, NumberStyles.None, CultureInfo.InvariantCulture, out int number)); return number;
        }
        internal static bool Flag(StrictJsonObject value, string key)
        { var row = Get(value, key) as StrictJsonBoolean; Require(row != null); return row.Value; }
        internal static IReadOnlyList<StrictJsonValue> Array(StrictJsonObject value, string key)
        { var row = Get(value, key) as StrictJsonArray; Require(row != null); return row.Items; }
    }
}
