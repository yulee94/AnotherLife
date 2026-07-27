using System;
using System.Collections.Generic;
using System.Linq;

namespace AL.Core.BossRewards
{
    public enum OwnedEquipmentQueryStatus
    {
        Valid = 0,
        Empty = 1,
        Unavailable = 2,
        MalformedNullCollection = 3,
        MalformedNullEntry = 4,
        MalformedBlankId = 5,
        MalformedDuplicateId = 6,
        MalformedUnknownRequiredDefinition = 7,
        PreservedUnknownFutureDefinition = 8,
        MalformedQuantity = 9,
        MalformedSnapshot = 10,
        MalformedTimestamp = 11,
        MalformedProvenance = 12,
        UnsupportedVersion = 13
    }

    public sealed class OwnedEquipmentSnapshot
    {
        public OwnedEquipmentSnapshot(
            string equipmentDefinitionId,
            string equipmentDefinitionContentVersion,
            string acquisitionSnapshotFingerprint,
            string slotId,
            int attackBonus,
            int defenseBonus,
            int healthBonus,
            string stackPolicyId,
            int quantity,
            long firstAcquiredUtcSeconds,
            long lastAcquiredUtcSeconds,
            string lastSourceBossDefinitionId,
            string lastSourceEncounterCompletionId,
            string lastAppliedRewardResultId,
            string schemaVersion,
            bool isSupportedDefinition)
        {
            EquipmentDefinitionId = equipmentDefinitionId;
            EquipmentDefinitionContentVersion = equipmentDefinitionContentVersion;
            AcquisitionSnapshotFingerprint = acquisitionSnapshotFingerprint;
            SlotId = slotId;
            AttackBonus = attackBonus;
            DefenseBonus = defenseBonus;
            HealthBonus = healthBonus;
            StackPolicyId = stackPolicyId;
            Quantity = quantity;
            FirstAcquiredUtcSeconds = firstAcquiredUtcSeconds;
            LastAcquiredUtcSeconds = lastAcquiredUtcSeconds;
            LastSourceBossDefinitionId = lastSourceBossDefinitionId;
            LastSourceEncounterCompletionId = lastSourceEncounterCompletionId;
            LastAppliedRewardResultId = lastAppliedRewardResultId;
            SchemaVersion = schemaVersion;
            IsSupportedDefinition = isSupportedDefinition;
        }

        public string EquipmentDefinitionId { get; }
        public string EquipmentDefinitionContentVersion { get; }
        public string AcquisitionSnapshotFingerprint { get; }
        public string SlotId { get; }
        public int AttackBonus { get; }
        public int DefenseBonus { get; }
        public int HealthBonus { get; }
        public string StackPolicyId { get; }
        public int Quantity { get; }
        public long FirstAcquiredUtcSeconds { get; }
        public long LastAcquiredUtcSeconds { get; }
        public string LastSourceBossDefinitionId { get; }
        public string LastSourceEncounterCompletionId { get; }
        public string LastAppliedRewardResultId { get; }
        public string SchemaVersion { get; }
        public bool IsSupportedDefinition { get; }
    }

    public sealed class OwnedEquipmentQueryResult
    {
        public OwnedEquipmentQueryResult(
            OwnedEquipmentQueryStatus status,
            IEnumerable<OwnedEquipmentSnapshot> items,
            IEnumerable<BossRewardDiagnostic> diagnostics,
            string inventoryRevision)
        {
            Status = status;
            Items = BossRewardImmutable.Freeze(
                items,
                BossRewardTechnicalLimits.MaximumInventoryRows);
            Diagnostics = BossRewardDiagnosticOrdering.Order(diagnostics);
            InventoryRevision = inventoryRevision ?? string.Empty;
        }

        public OwnedEquipmentQueryStatus Status { get; }
        public IReadOnlyList<OwnedEquipmentSnapshot> Items { get; }
        public IReadOnlyList<BossRewardDiagnostic> Diagnostics { get; }
        public string InventoryRevision { get; }
        public bool CanApplyRewards =>
            Status == OwnedEquipmentQueryStatus.Valid ||
            Status == OwnedEquipmentQueryStatus.Empty;
    }

    public static class BossRewardInventoryValidator
    {
        public static OwnedEquipmentQueryResult Validate(
            IEnumerable<OwnedEquipmentSnapshot> sourceRows,
            string inventoryRevision,
            BossRewardCatalogSnapshot catalog,
            string supportedInventorySchemaVersion,
            bool isAvailable = true)
        {
            if (!isAvailable || catalog == null)
                return Result(
                    OwnedEquipmentQueryStatus.Unavailable,
                    Array.Empty<OwnedEquipmentSnapshot>(),
                    inventoryRevision,
                    Error(
                        "AL-BOSS-REWARD-INVENTORY-UNAVAILABLE",
                        "inventory",
                        string.Empty,
                        "The inventory snapshot or immutable catalog is unavailable."));
            if (sourceRows == null)
                return Result(
                    OwnedEquipmentQueryStatus.MalformedNullCollection,
                    Array.Empty<OwnedEquipmentSnapshot>(),
                    inventoryRevision,
                    Error(
                        "AL-BOSS-REWARD-INVENTORY-COLLECTION-NULL",
                        "inventory",
                        string.Empty,
                        "The owned-equipment collection is null."));
            if (!BossRewardText.IsBoundedTechnicalId(inventoryRevision))
                return Result(
                    OwnedEquipmentQueryStatus.MalformedProvenance,
                    Array.Empty<OwnedEquipmentSnapshot>(),
                    inventoryRevision,
                    Error(
                        "AL-BOSS-REWARD-INVENTORY-REVISION-INVALID",
                        "inventory.revision",
                        inventoryRevision,
                        "The inventory revision is invalid."));
            if (!BossRewardText.IsBoundedVersion(supportedInventorySchemaVersion) ||
                !string.Equals(
                    supportedInventorySchemaVersion,
                    BossRewardTechnicalLimits.SupportedInventorySchemaVersion,
                    StringComparison.Ordinal))
                return Result(
                    OwnedEquipmentQueryStatus.UnsupportedVersion,
                    Array.Empty<OwnedEquipmentSnapshot>(),
                    inventoryRevision,
                    Error(
                        "AL-BOSS-REWARD-INVENTORY-SCHEMA-UNSUPPORTED",
                        "inventory.schemaVersion",
                        supportedInventorySchemaVersion,
                        "The inventory schema version is unsupported."));

            var boundedRows = new List<OwnedEquipmentSnapshot>();
            foreach (OwnedEquipmentSnapshot sourceRow in sourceRows)
            {
                if (boundedRows.Count >= BossRewardTechnicalLimits.MaximumInventoryRows)
                    return Result(
                        OwnedEquipmentQueryStatus.MalformedProvenance,
                        Array.Empty<OwnedEquipmentSnapshot>(),
                        inventoryRevision,
                        Error(
                            "AL-BOSS-REWARD-INVENTORY-COUNT-EXCEEDED",
                            "inventory",
                            inventoryRevision,
                            "The inventory exceeds the technical row ceiling."));
                boundedRows.Add(sourceRow);
            }
            OwnedEquipmentSnapshot[] rows = boundedRows.ToArray();
            var diagnostics = new List<BossRewardDiagnostic>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var knownDefinitions = new Dictionary<string, BossEquipmentDefinitionSnapshot>(
                StringComparer.Ordinal);
            if (!BossRewardText.IsBoundedTechnicalId(catalog.GameId) ||
                !BossRewardText.IsBoundedTechnicalId(catalog.CatalogSetId) ||
                !BossRewardText.IsBoundedVersion(catalog.SchemaVersion) ||
                !BossRewardText.IsBoundedTechnicalId(catalog.Revision))
                return Result(
                    OwnedEquipmentQueryStatus.Unavailable,
                    Array.Empty<OwnedEquipmentSnapshot>(),
                    inventoryRevision,
                    Error(
                        "AL-BOSS-REWARD-INVENTORY-CATALOG-INVALID",
                        "catalog",
                        inventoryRevision,
                        "The immutable equipment catalog identity is malformed."));
            for (int index = 0; index < catalog.EquipmentDefinitions.Count; index++)
            {
                BossEquipmentDefinitionSnapshot definition =
                    catalog.EquipmentDefinitions[index];
                if (!IsUsableEquipmentDefinition(
                        definition,
                        catalog.SchemaVersion) ||
                    knownDefinitions.ContainsKey(definition.EquipmentDefinitionId))
                    return Result(
                        OwnedEquipmentQueryStatus.Unavailable,
                        Array.Empty<OwnedEquipmentSnapshot>(),
                        inventoryRevision,
                        Error(
                            "AL-BOSS-REWARD-INVENTORY-CATALOG-INVALID",
                            "catalog.equipmentDefinitions[" + index + "]",
                            definition == null
                                ? string.Empty
                                : definition.EquipmentDefinitionId,
                            "The immutable equipment catalog contains a malformed or duplicate definition."));
                knownDefinitions.Add(definition.EquipmentDefinitionId, definition);
            }
            if (rows.Length == 0)
                return new OwnedEquipmentQueryResult(
                    OwnedEquipmentQueryStatus.Empty,
                    Array.Empty<OwnedEquipmentSnapshot>(),
                    Array.Empty<BossRewardDiagnostic>(),
                    inventoryRevision);

            OwnedEquipmentQueryStatus status = OwnedEquipmentQueryStatus.Valid;
            for (int index = 0; index < rows.Length; index++)
            {
                OwnedEquipmentSnapshot row = rows[index];
                string path = "inventory.items[" + index + "]";
                if (row == null)
                {
                    Add(
                        diagnostics,
                        "AL-BOSS-REWARD-INVENTORY-ROW-NULL",
                        path,
                        string.Empty,
                        "The inventory contains a null row.");
                    status = Prefer(status, OwnedEquipmentQueryStatus.MalformedNullEntry);
                    continue;
                }
                if (!BossRewardText.IsBoundedTechnicalId(row.EquipmentDefinitionId))
                {
                    Add(
                        diagnostics,
                        "AL-BOSS-REWARD-INVENTORY-ID-INVALID",
                        path + ".equipmentDefinitionId",
                        row.EquipmentDefinitionId,
                        "The inventory equipment identity is invalid.");
                    status = Prefer(status, OwnedEquipmentQueryStatus.MalformedBlankId);
                    continue;
                }
                if (!seen.Add(row.EquipmentDefinitionId))
                {
                    Add(
                        diagnostics,
                        "AL-BOSS-REWARD-INVENTORY-ID-DUPLICATE",
                        path + ".equipmentDefinitionId",
                        row.EquipmentDefinitionId,
                        "The inventory contains duplicate equipment identities.");
                    status = Prefer(status, OwnedEquipmentQueryStatus.MalformedDuplicateId);
                }
                if (!BossRewardText.IsBoundedVersion(row.SchemaVersion) ||
                    !string.Equals(
                        row.SchemaVersion,
                        supportedInventorySchemaVersion,
                        StringComparison.Ordinal))
                {
                    Add(
                        diagnostics,
                        "AL-BOSS-REWARD-INVENTORY-SCHEMA-UNSUPPORTED",
                        path + ".schemaVersion",
                        row.EquipmentDefinitionId,
                        "The inventory row schema version is unsupported.");
                    status = Prefer(status, OwnedEquipmentQueryStatus.UnsupportedVersion);
                }
                if (row.Quantity <= 0)
                {
                    Add(
                        diagnostics,
                        "AL-BOSS-REWARD-INVENTORY-QUANTITY-INVALID",
                        path + ".quantity",
                        row.EquipmentDefinitionId,
                        "The inventory quantity must be positive.");
                    status = Prefer(status, OwnedEquipmentQueryStatus.MalformedQuantity);
                }
                if (string.Equals(
                        row.StackPolicyId,
                        BossRewardStackPolicies.UniqueInstance,
                        StringComparison.Ordinal) &&
                    row.Quantity != 1)
                {
                    Add(
                        diagnostics,
                        "AL-BOSS-REWARD-INVENTORY-QUANTITY-INVALID",
                        path + ".quantity",
                        row.EquipmentDefinitionId,
                        "A unique-instance inventory row must have quantity one.");
                    status = Prefer(status, OwnedEquipmentQueryStatus.MalformedQuantity);
                }
                if (row.FirstAcquiredUtcSeconds < 0 ||
                    row.LastAcquiredUtcSeconds < row.FirstAcquiredUtcSeconds)
                {
                    Add(
                        diagnostics,
                        "AL-BOSS-REWARD-INVENTORY-TIMESTAMP-INVALID",
                        path + ".lastAcquiredUtcSeconds",
                        row.EquipmentDefinitionId,
                        "The inventory acquisition timestamps are invalid.");
                    status = Prefer(status, OwnedEquipmentQueryStatus.MalformedTimestamp);
                }
                if (!IsOptionalId(row.LastSourceBossDefinitionId) ||
                    !IsOptionalId(row.LastSourceEncounterCompletionId) ||
                    !IsOptionalId(row.LastAppliedRewardResultId))
                {
                    Add(
                        diagnostics,
                        "AL-BOSS-REWARD-INVENTORY-PROVENANCE-INVALID",
                        path,
                        row.EquipmentDefinitionId,
                        "The inventory acquisition provenance is invalid.");
                    status = Prefer(status, OwnedEquipmentQueryStatus.MalformedProvenance);
                }

                if (!knownDefinitions.TryGetValue(
                        row.EquipmentDefinitionId,
                        out BossEquipmentDefinitionSnapshot definition))
                {
                    if (!row.IsSupportedDefinition)
                    {
                        Add(
                            diagnostics,
                            "AL-BOSS-REWARD-INVENTORY-FUTURE-DEFINITION-PRESERVED",
                            path + ".equipmentDefinitionId",
                            row.EquipmentDefinitionId,
                            "An unknown future equipment row is preserved but unsupported.",
                            BossRewardDiagnosticSeverity.Warning);
                        status = Prefer(
                            status,
                            OwnedEquipmentQueryStatus.PreservedUnknownFutureDefinition);
                    }
                    else
                    {
                        Add(
                            diagnostics,
                            "AL-BOSS-REWARD-INVENTORY-DEFINITION-MISSING",
                            path + ".equipmentDefinitionId",
                            row.EquipmentDefinitionId,
                            "A required equipment definition is unavailable.");
                        status = Prefer(
                            status,
                            OwnedEquipmentQueryStatus.MalformedUnknownRequiredDefinition);
                    }
                    continue;
                }

                string expectedFingerprint;
                try
                {
                    expectedFingerprint =
                        BossRewardComputation.ComputeAcquisitionSnapshotFingerprint(
                            definition);
                }
                catch (Exception)
                {
                    Add(
                        diagnostics,
                        "AL-BOSS-REWARD-INVENTORY-SNAPSHOT-INVALID",
                        path + ".acquisitionSnapshotFingerprint",
                        row.EquipmentDefinitionId,
                        "The acquired technical snapshot could not be verified.");
                    status = Prefer(status, OwnedEquipmentQueryStatus.MalformedSnapshot);
                    continue;
                }
                if (!row.IsSupportedDefinition ||
                    !BossRewardText.IsBoundedVersion(row.EquipmentDefinitionContentVersion) ||
                    !string.Equals(
                        row.EquipmentDefinitionContentVersion,
                        definition.ContentVersion,
                        StringComparison.Ordinal) ||
                    !BossRewardText.IsLowerSha256(row.AcquisitionSnapshotFingerprint) ||
                    !BossRewardStackPolicies.IsSupported(row.StackPolicyId) ||
                    !string.Equals(
                        row.AcquisitionSnapshotFingerprint,
                        expectedFingerprint,
                        StringComparison.Ordinal) ||
                    !string.Equals(row.SlotId, definition.SlotId, StringComparison.Ordinal) ||
                    !string.Equals(
                        row.StackPolicyId,
                        definition.StackPolicyId,
                        StringComparison.Ordinal) ||
                    row.AttackBonus != definition.AttackBonus ||
                    row.DefenseBonus != definition.DefenseBonus ||
                    row.HealthBonus != definition.HealthBonus)
                {
                    Add(
                        diagnostics,
                        "AL-BOSS-REWARD-INVENTORY-SNAPSHOT-INVALID",
                        path + ".acquisitionSnapshotFingerprint",
                        row.EquipmentDefinitionId,
                        "The acquired technical snapshot conflicts with its definition.");
                    status = Prefer(status, OwnedEquipmentQueryStatus.MalformedSnapshot);
                }
            }

            OwnedEquipmentSnapshot[] ordered = rows
                .Where(row => row != null)
                .OrderBy(row => row.EquipmentDefinitionId, StringComparer.Ordinal)
                .ToArray();
            return new OwnedEquipmentQueryResult(
                status,
                ordered,
                diagnostics,
                inventoryRevision);
        }

        private static bool IsUsableEquipmentDefinition(
            BossEquipmentDefinitionSnapshot definition,
            string supportedSchemaVersion)
        {
            return definition != null &&
                   BossRewardText.IsBoundedTechnicalId(
                       definition.EquipmentDefinitionId) &&
                   BossRewardText.IsBoundedVersion(definition.SchemaVersion) &&
                   string.Equals(
                       definition.SchemaVersion,
                       supportedSchemaVersion,
                       StringComparison.Ordinal) &&
                   BossRewardText.IsBoundedVersion(definition.ContentVersion) &&
                   BossRewardText.IsBoundedTechnicalId(definition.SlotId) &&
                   BossRewardStackPolicies.IsSupported(definition.StackPolicyId) &&
                   BossRewardText.IsBoundedTechnicalId(
                       definition.AcquisitionSnapshotPolicyId) &&
                   BossRewardText.IsBoundedTechnicalId(
                       definition.PresentationContentKey) &&
                   BossRewardText.IsBoundedTechnicalId(definition.SourceRevision) &&
                   BossRewardText.IsLowerSha256(definition.RawSha256);
        }

        private static OwnedEquipmentQueryStatus Prefer(
            OwnedEquipmentQueryStatus current,
            OwnedEquipmentQueryStatus candidate)
        {
            if (current == OwnedEquipmentQueryStatus.Valid) return candidate;
            if (current == OwnedEquipmentQueryStatus.PreservedUnknownFutureDefinition &&
                candidate != OwnedEquipmentQueryStatus.Valid)
                return candidate;
            return current;
        }

        private static bool IsOptionalId(string value)
        {
            return string.IsNullOrEmpty(value) || BossRewardText.IsBoundedTechnicalId(value);
        }

        private static OwnedEquipmentQueryResult Result(
            OwnedEquipmentQueryStatus status,
            IEnumerable<OwnedEquipmentSnapshot> items,
            string revision,
            params BossRewardDiagnostic[] diagnostics)
        {
            return new OwnedEquipmentQueryResult(status, items, diagnostics, revision);
        }

        private static void Add(
            ICollection<BossRewardDiagnostic> diagnostics,
            string code,
            string fieldPath,
            string recordId,
            string message,
            BossRewardDiagnosticSeverity severity = BossRewardDiagnosticSeverity.Error)
        {
            diagnostics.Add(new BossRewardDiagnostic(
                code,
                severity,
                BossRewardDiagnosticDomain.Inventory,
                fieldPath,
                true,
                message,
                string.Empty,
                recordId));
        }

        private static BossRewardDiagnostic Error(
            string code,
            string fieldPath,
            string recordId,
            string message)
        {
            return new BossRewardDiagnostic(
                code,
                BossRewardDiagnosticSeverity.Error,
                BossRewardDiagnosticDomain.Inventory,
                fieldPath,
                true,
                message,
                string.Empty,
                recordId);
        }
    }
}
