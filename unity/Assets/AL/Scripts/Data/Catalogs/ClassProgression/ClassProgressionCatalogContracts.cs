using System.Collections.Generic;

namespace AL.Data.Catalogs
{
    public enum ClassProgressionCatalogValidationStatus
    {
        AcceptedIdentitySpine = 0,
        CatalogUnavailable = 1,
        CatalogInvalid = 2,
        UnsupportedVersion = 3
    }

    public enum ClassProgressionProductionReadiness
    {
        IdentitySpineOnly = 0
    }

    /// <summary>
    /// Immutable view over a semantically accepted class-identity catalog set.
    /// Acceptance means the source identity spine is internally consistent; it never
    /// means that skill behavior, balance, saves, runtime wiring, or Warmaster
    /// transactions are production ready.
    /// </summary>
    public sealed class ClassProgressionIdentitySnapshot
    {
        private static readonly IReadOnlyList<string> productionBlockers =
            ImmutableCollections.Freeze(new[]
            {
                "complete_skill_node_catalog",
                "combat_skill_definitions",
                "balance_and_point_policy",
                "character_save_and_migration",
                "runtime_catalog_authority",
                "warmaster_transaction_authority"
            });

        internal ClassProgressionIdentitySnapshot(GameDataCatalogSetSnapshot catalogSet)
        {
            CatalogSet = catalogSet;
        }

        public GameDataCatalogSetSnapshot CatalogSet { get; }
        public string PacketId => GameDataClassProgressionSchemas.PacketId;
        public string PacketVersion => GameDataClassProgressionSchemas.PacketVersion;
        public string PacketSha256 => GameDataClassProgressionSchemas.PacketSha256;
        public string ValidatedSourceRevision => GameDataClassProgressionSchemas.ValidatedRevision;
        public ClassProgressionProductionReadiness ProductionReadiness =>
            ClassProgressionProductionReadiness.IdentitySpineOnly;
        public bool IsProductionReady => false;
        public IReadOnlyList<string> ProductionBlockerIds => productionBlockers;

        public IReadOnlyList<GameDataCatalogRecord> Families =>
            Records("class_families");

        public IReadOnlyList<GameDataCatalogRecord> Classes =>
            Records("playable_classes");

        public IReadOnlyList<GameDataCatalogRecord> Resources =>
            Records("class_resources");

        public IReadOnlyList<GameDataCatalogRecord> Trees =>
            Records("class_skill_trees");

        public IReadOnlyList<GameDataCatalogRecord> Branches =>
            Records("class_skill_branches");

        public IReadOnlyList<GameDataCatalogRecord> MilestoneSkills =>
            Records("class_milestone_skills");

        public IReadOnlyList<GameDataCatalogRecord> MasteryTrials =>
            Records("class_mastery_trials");

        public IReadOnlyList<GameDataCatalogRecord> WarmasterIdentities =>
            Records("class_warmaster_identities");

        public GameDataCatalogQueryResult QueryFamily(string id)
        {
            return CatalogSet.QueryRecord("class_families", id);
        }

        public GameDataCatalogQueryResult QueryClass(string id)
        {
            return CatalogSet.QueryRecord("playable_classes", id);
        }

        public GameDataCatalogQueryResult QueryResource(string id)
        {
            return CatalogSet.QueryRecord("class_resources", id);
        }

        public GameDataCatalogQueryResult QueryTree(string id)
        {
            return CatalogSet.QueryRecord("class_skill_trees", id);
        }

        public GameDataCatalogQueryResult QueryBranch(string id)
        {
            return CatalogSet.QueryRecord("class_skill_branches", id);
        }

        public GameDataCatalogQueryResult QueryMilestoneSkill(string id)
        {
            return CatalogSet.QueryRecord("class_milestone_skills", id);
        }

        public GameDataCatalogQueryResult QueryMasteryTrial(string id)
        {
            return CatalogSet.QueryRecord("class_mastery_trials", id);
        }

        public GameDataCatalogQueryResult QueryWarmasterIdentity(string setId)
        {
            return CatalogSet.QueryRecord("class_warmaster_identities", setId);
        }

        private IReadOnlyList<GameDataCatalogRecord> Records(string family)
        {
            return CatalogSet.FamiliesById[family].Records;
        }
    }

    public sealed class ClassProgressionCatalogValidationResult
    {
        internal ClassProgressionCatalogValidationResult(
            ClassProgressionCatalogValidationStatus status,
            ClassProgressionIdentitySnapshot snapshot,
            IEnumerable<GameDataCatalogDiagnostic> diagnostics)
        {
            Status = status;
            Snapshot = snapshot;
            Diagnostics = ImmutableCollections.Freeze(diagnostics);
        }

        public ClassProgressionCatalogValidationStatus Status { get; }
        public ClassProgressionIdentitySnapshot Snapshot { get; }
        public IReadOnlyList<GameDataCatalogDiagnostic> Diagnostics { get; }
        public bool IsAccepted =>
            Status == ClassProgressionCatalogValidationStatus.AcceptedIdentitySpine &&
            Snapshot != null &&
            Diagnostics.Count == 0;
        public bool IsProductionReady => false;
    }
}
