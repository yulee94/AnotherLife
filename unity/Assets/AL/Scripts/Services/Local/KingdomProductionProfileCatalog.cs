using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using UnityEngine;

namespace AL.Services.Local
{
    [Serializable]
    public sealed class KingdomProductionContributionRecord
    {
        public string id;
        public string resourceId;
        public string buildingId;
        public int minBuildingLevel;
        public double ratePerLevelPerSecond;
        public double capPerTick;
        public string[] realmIds;
    }

    [Serializable]
    public sealed class KingdomProductionProfileFile
    {
        public int schemaVersion;
        public string catalogId;
        public bool productionEligible;
        public string sourceRevision;
        public string authorityLedgerId;
        public long maxOfflineElapsedSeconds;
        public KingdomProductionContributionRecord[] contributions;
    }

    [Serializable]
    public sealed class SixFamilyAuthorityLedgerFile
    {
        public int schemaVersion;
        public string ledgerId;
        public bool productionEligible;
        public string sourceSetSha256;
        public string sourceSetVersion;
    }

    public sealed class KingdomProductionContributionRule
    {
        internal KingdomProductionContributionRule(
            string id,
            ResourceType resourceType,
            string buildingId,
            int minBuildingLevel,
            double ratePerLevelPerSecond,
            double capPerTick,
            IReadOnlyList<RealmId> realmIds)
        {
            Id = id;
            ResourceType = resourceType;
            BuildingId = buildingId;
            MinBuildingLevel = minBuildingLevel;
            RatePerLevelPerSecond = ratePerLevelPerSecond;
            CapPerTick = capPerTick;
            RealmIds = realmIds;
        }

        public string Id { get; }
        public ResourceType ResourceType { get; }
        public string BuildingId { get; }
        public int MinBuildingLevel { get; }
        public double RatePerLevelPerSecond { get; }
        public double CapPerTick { get; }
        public IReadOnlyList<RealmId> RealmIds { get; }
    }

    public sealed class KingdomProductionProfileSnapshot
    {
        internal KingdomProductionProfileSnapshot(
            string catalogId,
            string sourceRevision,
            string sourceSha256,
            string authorityLedgerId,
            bool productionEligible,
            long maxOfflineElapsedSeconds,
            IReadOnlyList<KingdomProductionContributionRule> contributions)
        {
            CatalogId = catalogId;
            SourceRevision = sourceRevision;
            SourceSha256 = sourceSha256;
            AuthorityLedgerId = authorityLedgerId;
            ProductionEligible = productionEligible;
            MaxOfflineElapsedSeconds = maxOfflineElapsedSeconds;
            Contributions = contributions;
        }

        public string CatalogId { get; }
        public string SourceRevision { get; }
        public string SourceSha256 { get; }
        public string AuthorityLedgerId { get; }
        public bool ProductionEligible { get; }
        public long MaxOfflineElapsedSeconds { get; }
        public IReadOnlyList<KingdomProductionContributionRule> Contributions { get; }
    }

    public readonly struct KingdomProductionProfileLoadResult
    {
        internal KingdomProductionProfileLoadResult(
            bool isReady,
            string diagnosticCode,
            string recordPath,
            KingdomProductionProfileSnapshot snapshot)
        {
            IsReady = isReady;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            RecordPath = recordPath ?? string.Empty;
            Snapshot = snapshot;
        }

        public bool IsReady { get; }
        public string DiagnosticCode { get; }
        public string RecordPath { get; }
        public KingdomProductionProfileSnapshot Snapshot { get; }

        public EconomyDiagnostic Diagnostic =>
            new EconomyDiagnostic(DiagnosticCode, RecordPath);
    }

    public static class KingdomProductionProfileCatalog
    {
        public const string CatalogId = "kingdom_production_profile_v1";
        public const string AuthorityLedgerId = "al_six_family_production_authority_v1";
        public const int SchemaVersion = 1;
        public const long MaximumOfflineElapsedSeconds = 2592000L;
        public const string LiveLedgerRelativePath =
            "Docs/GameDataCatalog/six-family-production-authority.v1.json";

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        private static readonly Dictionary<string, string> CanonicalBuildingIds =
            CreateBuildingAliases();

        public static string ComputeSha256(byte[] bytes)
        {
            if (bytes == null)
            {
                return string.Empty;
            }

            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        public static KingdomProductionProfileLoadResult TryLoadProfile(
            byte[] sourceBytes,
            string expectedSha256)
        {
            if (sourceBytes == null || sourceBytes.Length == 0)
            {
                return Reject(EconomyDiagnosticCodes.ProductionCatalog, "Production.Catalog");
            }

            string json;
            try
            {
                json = Utf8.GetString(sourceBytes);
            }
            catch (Exception)
            {
                return Reject(EconomyDiagnosticCodes.ProductionCatalog, "Production.Catalog");
            }

            if (string.IsNullOrWhiteSpace(json) || ContainsOathmarkToken(json))
            {
                return Reject(
                    ContainsOathmarkToken(json)
                        ? EconomyDiagnosticCodes.ProductionOathmark
                        : EconomyDiagnosticCodes.ProductionCatalog,
                    "Production.Catalog");
            }

            string actualSha256 = ComputeSha256(sourceBytes);
            if (!IsLowerSha256(expectedSha256) ||
                !string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
            {
                return Reject(EconomyDiagnosticCodes.ProductionDrift, "Production.Catalog.Hash");
            }

            KingdomProductionProfileFile file;
            try
            {
                file = JsonUtility.FromJson<KingdomProductionProfileFile>(json);
            }
            catch (Exception)
            {
                return Reject(EconomyDiagnosticCodes.ProductionCatalog, "Production.Catalog");
            }

            if (file == null ||
                file.schemaVersion != SchemaVersion ||
                !string.Equals(file.catalogId, CatalogId, StringComparison.Ordinal) ||
                !string.Equals(file.authorityLedgerId, AuthorityLedgerId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(file.sourceRevision) ||
                file.sourceRevision.Length > 128)
            {
                return Reject(EconomyDiagnosticCodes.ProductionCatalog, "Production.Catalog");
            }

            var rules = new List<KingdomProductionContributionRule>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            KingdomProductionContributionRecord[] records =
                file.contributions ?? Array.Empty<KingdomProductionContributionRecord>();
            if (file.productionEligible && records.Length == 0)
            {
                return Reject(EconomyDiagnosticCodes.ProductionCatalog, "Production.Contributions");
            }

            for (int index = 0; index < records.Length; index++)
            {
                KingdomProductionContributionRecord record = records[index];
                string path = $"Production.Contributions[{index}]";
                if (record == null)
                {
                    return Reject(EconomyDiagnosticCodes.ProductionCatalog, path);
                }

                if (ContainsOathmarkToken(record.resourceId) || ContainsOathmarkToken(record.id))
                {
                    return Reject(EconomyDiagnosticCodes.ProductionOathmark, path);
                }

                if (string.IsNullOrWhiteSpace(record.id) ||
                    !seenIds.Add(record.id) ||
                    !TryCanonicalBuildingId(record.buildingId, out string buildingId) ||
                    record.minBuildingLevel < 0 ||
                    double.IsNaN(record.ratePerLevelPerSecond) ||
                    double.IsInfinity(record.ratePerLevelPerSecond) ||
                    record.ratePerLevelPerSecond < 0d ||
                    double.IsNaN(record.capPerTick) ||
                    double.IsInfinity(record.capPerTick) ||
                    record.capPerTick < 0d ||
                    !ResourceRules.TryGetResourceTypeByStableId(record.resourceId, out ResourceType resourceType) ||
                    !TryParseRealms(record.realmIds, out IReadOnlyList<RealmId> realms))
                {
                    return Reject(EconomyDiagnosticCodes.ProductionCatalog, path);
                }

                rules.Add(
                    new KingdomProductionContributionRule(
                        record.id,
                        resourceType,
                        buildingId,
                        record.minBuildingLevel,
                        record.ratePerLevelPerSecond,
                        record.capPerTick,
                        realms));
            }

            long maxOfflineElapsedSeconds = 0;
            if (file.maxOfflineElapsedSeconds > 0 &&
                file.maxOfflineElapsedSeconds <= MaximumOfflineElapsedSeconds)
            {
                maxOfflineElapsedSeconds = file.maxOfflineElapsedSeconds;
            }

            var snapshot = new KingdomProductionProfileSnapshot(
                file.catalogId,
                file.sourceRevision,
                actualSha256,
                file.authorityLedgerId,
                file.productionEligible,
                maxOfflineElapsedSeconds,
                Array.AsReadOnly(rules.ToArray()));
            return new KingdomProductionProfileLoadResult(
                true,
                string.Empty,
                string.Empty,
                snapshot);
        }

        public static KingdomProductionProfileLoadResult TryBindAuthorityLedger(byte[] ledgerBytes)
        {
            if (ledgerBytes == null || ledgerBytes.Length == 0)
            {
                return Reject(EconomyDiagnosticCodes.ProductionCatalog, "Production.AuthorityLedger");
            }

            string json;
            try
            {
                json = Utf8.GetString(ledgerBytes);
            }
            catch (Exception)
            {
                return Reject(EconomyDiagnosticCodes.ProductionCatalog, "Production.AuthorityLedger");
            }

            SixFamilyAuthorityLedgerFile ledger;
            try
            {
                ledger = JsonUtility.FromJson<SixFamilyAuthorityLedgerFile>(json);
            }
            catch (Exception)
            {
                return Reject(EconomyDiagnosticCodes.ProductionCatalog, "Production.AuthorityLedger");
            }

            if (ledger == null ||
                ledger.schemaVersion != SchemaVersion ||
                !string.Equals(ledger.ledgerId, AuthorityLedgerId, StringComparison.Ordinal) ||
                !IsLowerSha256(ledger.sourceSetSha256) ||
                string.IsNullOrWhiteSpace(ledger.sourceSetVersion))
            {
                return Reject(EconomyDiagnosticCodes.ProductionCatalog, "Production.AuthorityLedger");
            }

            var snapshot = new KingdomProductionProfileSnapshot(
                CatalogId,
                ledger.sourceSetVersion,
                ledger.sourceSetSha256,
                ledger.ledgerId,
                ledger.productionEligible,
                0,
                Array.Empty<KingdomProductionContributionRule>());
            return new KingdomProductionProfileLoadResult(
                true,
                ledger.productionEligible
                    ? string.Empty
                    : EconomyDiagnosticCodes.ProductionCatalog,
                ledger.productionEligible ? string.Empty : "Production.AuthorityLedger.Eligible",
                snapshot);
        }

        internal static bool TryCanonicalBuildingId(string buildingId, out string canonical)
        {
            canonical = null;
            if (string.IsNullOrWhiteSpace(buildingId))
            {
                return false;
            }

            return CanonicalBuildingIds.TryGetValue(buildingId, out canonical);
        }

        private static bool TryParseRealms(string[] realmIds, out IReadOnlyList<RealmId> realms)
        {
            realms = Array.Empty<RealmId>();
            if (realmIds == null || realmIds.Length == 0)
            {
                return false;
            }

            var parsed = new List<RealmId>(realmIds.Length);
            var seen = new HashSet<RealmId>();
            for (int index = 0; index < realmIds.Length; index++)
            {
                if (!TryParseRealm(realmIds[index], out RealmId realmId) || !seen.Add(realmId))
                {
                    return false;
                }

                parsed.Add(realmId);
            }

            realms = Array.AsReadOnly(parsed.ToArray());
            return true;
        }

        private static bool TryParseRealm(string realmId, out RealmId realm)
        {
            realm = RealmId.None;
            if (string.IsNullOrWhiteSpace(realmId))
            {
                return false;
            }

            switch (realmId)
            {
                case "stonehold":
                    realm = RealmId.Stonehold;
                    return true;
                case "eldergrove":
                    realm = RealmId.Eldergrove;
                    return true;
                case "crownlands":
                    realm = RealmId.Crownlands;
                    return true;
                case "umbral":
                    realm = RealmId.Umbral;
                    return true;
                default:
                    return false;
            }
        }

        private static bool ContainsOathmarkToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.IndexOf("oathmark", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsLowerSha256(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool hex = (character >= '0' && character <= '9') ||
                           (character >= 'a' && character <= 'f');
                if (!hex)
                {
                    return false;
                }
            }

            return true;
        }

        private static KingdomProductionProfileLoadResult Reject(string code, string path)
        {
            return new KingdomProductionProfileLoadResult(false, code, path, null);
        }

        private static Dictionary<string, string> CreateBuildingAliases()
        {
            var aliases = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["town_hall"] = "town_hall",
                ["TownHall"] = "town_hall",
                ["farm"] = "farm",
                ["Farm"] = "farm",
                ["lumber_mill"] = "lumber_mill",
                ["LumberMill"] = "lumber_mill",
                ["quarry"] = "quarry",
                ["Quarry"] = "quarry",
                ["gold_mine"] = "gold_mine",
                ["GoldMine"] = "gold_mine",
                ["barracks"] = "barracks",
                ["Barracks"] = "barracks",
                ["academy"] = "academy",
                ["Academy"] = "academy",
                ["market"] = "market",
                ["Market"] = "market",
                ["storehouse"] = "storehouse",
                ["Storehouse"] = "storehouse",
                ["forge"] = "forge",
                ["Forge"] = "forge",
                ["stable"] = "stable",
                ["Stable"] = "stable",
                ["workshop"] = "workshop",
                ["Workshop"] = "workshop",
                ["embassy"] = "embassy",
                ["Embassy"] = "embassy",
                ["wall"] = "wall",
                ["Wall"] = "wall",
                ["watchtower"] = "watchtower",
                ["Watchtower"] = "watchtower"
            };
            return aliases;
        }
    }
}
