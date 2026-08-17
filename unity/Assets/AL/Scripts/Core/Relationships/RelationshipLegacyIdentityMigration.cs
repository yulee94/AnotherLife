using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AL.Data.Runtime;

namespace AL.Core.Relationships
{
    public enum RelationshipIdentityMigrationStatus
    {
        Canonical,
        AliasMigrated,
        LegacyLabelMigrated,
        UnknownPreserved,
        AmbiguousPreserved,
        ObsoletePreserved,
        CatalogUnavailable,
        Invalid
    }

    public sealed class RelationshipIdentityMigrationResult
    {
        internal RelationshipIdentityMigrationResult(
            RelationshipIdentityMigrationStatus status,
            string originalValue,
            string canonicalId)
        {
            Status = status;
            OriginalValue = originalValue;
            CanonicalId = canonicalId ?? string.Empty;
            PreservedValue = IsResolved ? string.Empty : originalValue ?? string.Empty;
        }

        public RelationshipIdentityMigrationStatus Status { get; }
        public string OriginalValue { get; }
        public string CanonicalId { get; }
        public string PreservedValue { get; }
        public bool IsResolved =>
            Status == RelationshipIdentityMigrationStatus.Canonical ||
            Status == RelationshipIdentityMigrationStatus.AliasMigrated ||
            Status == RelationshipIdentityMigrationStatus.LegacyLabelMigrated;
    }

    /// Compatibility-only adapter for persisted relationship identities. Exact source
    /// aliases and display labels resolve to stable IDs. Unknown, ambiguous, and obsolete
    /// values are classified and preserved; no trimming or fuzzy matching is performed.
    public sealed class RelationshipLegacyIdentityMigrator
    {
        private static readonly IReadOnlyDictionary<string, string> LegacyNpcLabels =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Captain Valerius", "npc_valerius" },
                { "Valerius", "npc_valerius" },
                { "Master Gruff", "npc_gruff" },
                { "Molly", "npc_molly" },
                { "Xerath", "npc_xerath" },
                { "Vaeloryn", "npc_vaeloryn" },
                { "Edras Veyr", "npc_edras_veyr" }
            };

        private static readonly IReadOnlyDictionary<string, string> LegacyFactionLabels =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Radiant Council", "faction_crownlands_radiant_council" },
                { "The Radiant Council", "faction_crownlands_radiant_council" },
                { "Stonehold Assembly", "faction_stonehold_assembly" },
                { "Eldergrove Wardens", "faction_eldergrove_wardens" },
                { "Umbral Cabal", "faction_umbral_cabal" },
                { "The Umbral Cabal", "faction_umbral_cabal" },
                { "Veil Watch", "faction_veil_watch" }
            };

        private static readonly ISet<string> AmbiguousLabels =
            new HashSet<string>(new[] { "Council", "Assembly", "Wardens" }, StringComparer.Ordinal);

        private static readonly ISet<string> ObsoleteLabels =
            new HashSet<string>(new[]
            {
                "Human Council", "Dwarven Forge", "Elven Glade", "Dark Elf Rift"
            }, StringComparer.Ordinal);

        private readonly RelationshipCatalogResolver resolver;

        public RelationshipLegacyIdentityMigrator(byte[] sourceBytes)
        {
            resolver = new RelationshipCatalogResolver(sourceBytes);
        }

        public RelationshipIdentityMigrationResult Migrate(
            RelationshipDomain domain,
            string persistedValue)
        {
            if (string.IsNullOrEmpty(persistedValue) || domain == RelationshipDomain.PersonaTrait)
            {
                return Result(RelationshipIdentityMigrationStatus.Invalid, persistedValue, null);
            }

            if (!string.Equals(
                    resolver.PolicyRevision,
                    RelationshipCatalogResolver.ExpectedSourceSha256,
                    StringComparison.Ordinal))
            {
                return Result(RelationshipIdentityMigrationStatus.CatalogUnavailable,
                    persistedValue, null);
            }

            RelationshipIdentityResolution resolution = Resolve(domain, persistedValue);
            if (resolution.Status == RelationshipResolutionStatus.Found)
            {
                return Result(RelationshipIdentityMigrationStatus.Canonical, persistedValue,
                    resolution.Identity.CanonicalId);
            }
            if (resolution.Status == RelationshipResolutionStatus.AliasResolved)
            {
                return Result(RelationshipIdentityMigrationStatus.AliasMigrated, persistedValue,
                    resolution.Identity.CanonicalId);
            }

            string legacyId;
            IReadOnlyDictionary<string, string> legacy = domain == RelationshipDomain.NpcAffinity
                ? LegacyNpcLabels
                : LegacyFactionLabels;
            if (legacy.TryGetValue(persistedValue, out legacyId))
            {
                return Result(RelationshipIdentityMigrationStatus.LegacyLabelMigrated,
                    persistedValue, legacyId);
            }
            if (AmbiguousLabels.Contains(persistedValue))
            {
                return Result(RelationshipIdentityMigrationStatus.AmbiguousPreserved,
                    persistedValue, null);
            }
            if (ObsoleteLabels.Contains(persistedValue))
            {
                return Result(RelationshipIdentityMigrationStatus.ObsoletePreserved,
                    persistedValue, null);
            }

            return Result(RelationshipIdentityMigrationStatus.UnknownPreserved,
                persistedValue, null);
        }

        public string GetDisplayLabel(RelationshipDomain domain, string identityValue)
        {
            RelationshipIdentityMigrationResult migration = Migrate(domain, identityValue);
            if (!migration.IsResolved) return string.Empty;
            RelationshipIdentityResolution resolution = Resolve(domain, migration.CanonicalId);
            return resolution.Identity == null ? string.Empty : resolution.Identity.DisplayLabel;
        }

        private RelationshipIdentityResolution Resolve(RelationshipDomain domain, string value)
        {
            return domain == RelationshipDomain.NpcAffinity
                ? resolver.ResolveNpc(value)
                : resolver.ResolveFaction(value);
        }

        private static RelationshipIdentityMigrationResult Result(
            RelationshipIdentityMigrationStatus status,
            string original,
            string canonical)
        {
            return new RelationshipIdentityMigrationResult(status, original, canonical);
        }
    }

    public sealed class RelationshipPersistenceMigrationReport
    {
        internal RelationshipPersistenceMigrationReport(
            int migratedCount,
            IEnumerable<RelationshipIdentityMigrationResult> unresolved,
            bool canPersist)
        {
            MigratedCount = migratedCount;
            Unresolved = new ReadOnlyCollection<RelationshipIdentityMigrationResult>(
                new List<RelationshipIdentityMigrationResult>(
                    unresolved ?? Array.Empty<RelationshipIdentityMigrationResult>()));
            CanPersist = canPersist;
        }

        public int MigratedCount { get; }
        public IReadOnlyList<RelationshipIdentityMigrationResult> Unresolved { get; }
        public bool CanPersist { get; }
    }

    /// <summary>
    /// Persistence-bound compatibility pass. Resolved legacy identities are rewritten to
    /// canonical IDs. Unknown, ambiguous, and obsolete rows remain byte-for-byte intact.
    /// Canonical collisions fail closed and leave the affected row unchanged.
    /// </summary>
    public static class RelationshipPersistenceIdentityMigration
    {
        public static RelationshipPersistenceMigrationReport Apply(
            RelationshipLegacyIdentityMigrator migrator,
            SaveGameData save)
        {
            if (migrator == null) throw new ArgumentNullException(nameof(migrator));
            if (save == null)
            {
                return new RelationshipPersistenceMigrationReport(
                    0,
                    new[]
                    {
                        new RelationshipIdentityMigrationResult(
                            RelationshipIdentityMigrationStatus.Invalid,
                            string.Empty,
                            null)
                    },
                    false);
            }

            var unresolved = new List<RelationshipIdentityMigrationResult>();
            int migrated = MigrateNpcRows(migrator, save.Reputation, unresolved);
            migrated += MigrateFactionRows(migrator, save.FactionReputations, unresolved);
            bool canPersist = unresolved.TrueForAll(result =>
                result.Status != RelationshipIdentityMigrationStatus.CatalogUnavailable &&
                result.Status != RelationshipIdentityMigrationStatus.Invalid &&
                result.Status != RelationshipIdentityMigrationStatus.AmbiguousPreserved);
            return new RelationshipPersistenceMigrationReport(migrated, unresolved, canPersist);
        }

        private static int MigrateNpcRows(
            RelationshipLegacyIdentityMigrator migrator,
            IList<NpcAffinityData> rows,
            ICollection<RelationshipIdentityMigrationResult> unresolved)
        {
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            foreach (NpcAffinityData row in rows ?? Array.Empty<NpcAffinityData>())
            {
                if (row != null && !string.IsNullOrEmpty(row.NpcId)) occupied.Add(row.NpcId);
            }

            int migrated = 0;
            foreach (NpcAffinityData row in rows ?? Array.Empty<NpcAffinityData>())
            {
                if (row == null)
                {
                    unresolved.Add(new RelationshipIdentityMigrationResult(
                        RelationshipIdentityMigrationStatus.Invalid, string.Empty, null));
                    continue;
                }

                RelationshipIdentityMigrationResult result =
                    migrator.Migrate(RelationshipDomain.NpcAffinity, row.NpcId);
                if (TryApply(row.NpcId, result, occupied, out string canonical))
                {
                    occupied.Remove(row.NpcId);
                    row.NpcId = canonical;
                    occupied.Add(canonical);
                    if (result.Status != RelationshipIdentityMigrationStatus.Canonical) migrated++;
                }
                else if (!result.IsResolved || !string.Equals(row.NpcId, canonical, StringComparison.Ordinal))
                {
                    unresolved.Add(CollisionOrOriginal(result));
                }
            }
            return migrated;
        }

        private static int MigrateFactionRows(
            RelationshipLegacyIdentityMigrator migrator,
            IList<FactionRepData> rows,
            ICollection<RelationshipIdentityMigrationResult> unresolved)
        {
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            foreach (FactionRepData row in rows ?? Array.Empty<FactionRepData>())
            {
                if (row != null && !string.IsNullOrEmpty(row.FactionId)) occupied.Add(row.FactionId);
            }

            int migrated = 0;
            foreach (FactionRepData row in rows ?? Array.Empty<FactionRepData>())
            {
                if (row == null)
                {
                    unresolved.Add(new RelationshipIdentityMigrationResult(
                        RelationshipIdentityMigrationStatus.Invalid, string.Empty, null));
                    continue;
                }

                RelationshipIdentityMigrationResult result =
                    migrator.Migrate(RelationshipDomain.FactionReputation, row.FactionId);
                if (TryApply(row.FactionId, result, occupied, out string canonical))
                {
                    occupied.Remove(row.FactionId);
                    row.FactionId = canonical;
                    occupied.Add(canonical);
                    if (result.Status != RelationshipIdentityMigrationStatus.Canonical) migrated++;
                }
                else if (!result.IsResolved || !string.Equals(row.FactionId, canonical, StringComparison.Ordinal))
                {
                    unresolved.Add(CollisionOrOriginal(result));
                }
            }
            return migrated;
        }

        private static bool TryApply(
            string current,
            RelationshipIdentityMigrationResult result,
            ISet<string> occupied,
            out string canonical)
        {
            canonical = result.CanonicalId;
            if (!result.IsResolved) return false;
            return string.Equals(current, canonical, StringComparison.Ordinal) ||
                   !occupied.Contains(canonical);
        }

        private static RelationshipIdentityMigrationResult CollisionOrOriginal(
            RelationshipIdentityMigrationResult result)
        {
            return result.IsResolved
                ? new RelationshipIdentityMigrationResult(
                    RelationshipIdentityMigrationStatus.AmbiguousPreserved,
                    result.OriginalValue,
                    null)
                : result;
        }
    }
}
