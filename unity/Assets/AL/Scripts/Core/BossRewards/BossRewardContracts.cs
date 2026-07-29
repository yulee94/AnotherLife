using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace AL.Core.BossRewards
{
    public static class BossRewardTechnicalLimits
    {
        public const int MaximumIdentifierUtf8Bytes = 128;
        public const int MaximumOpaqueIdentifierUtf8Bytes = 256;
        public const int MaximumRevisionUtf8Bytes = 256;
        public const int MaximumVersionUtf8Bytes = 128;
        public const int MaximumCatalogEntries = 4096;
        public const int MaximumRewardEntries = 256;
        public const int MaximumDiagnostics = 128;
        public const int MaximumInventoryRows = 4096;
        public const int MaximumLedgerRows = 4096;
        public const int MaximumWarzoneCredits = int.MaxValue;
        public const int MaximumOwnedQuantity = int.MaxValue;
        public const int MicrosPerUnit = 1_000_000;
        public const string SupportedRewardSchemaVersion = "boss_reward_schema_v1";
        public const string DeterminismVersionV1 = "boss_reward_sha256_v1";
        public const string ApplicationPolicyVersionV1 =
            "boss_reward_application_v1";
        public const string SupportedDeterminismVersion = DeterminismVersionV1;
        public const string SupportedApplicationPolicyVersion =
            ApplicationPolicyVersionV1;
        public const string SupportedInventorySchemaVersion = "owned_equipment_v1";

        public static bool IsReadableDeterminismVersion(string value)
        {
            // Append future readable versions; never replace retained readers.
            return string.Equals(
                value,
                DeterminismVersionV1,
                StringComparison.Ordinal);
        }

        public static bool IsReadableApplicationPolicyVersion(string value)
        {
            // Append future readable versions; never replace retained readers.
            return string.Equals(
                value,
                ApplicationPolicyVersionV1,
                StringComparison.Ordinal);
        }
    }

    public static class BossRewardStackPolicies
    {
        public const string StackQuantity = "stack_quantity";
        public const string UniqueInstance = "unique_instance";

        public static bool IsSupported(string value)
        {
            return string.Equals(value, StackQuantity, StringComparison.Ordinal) ||
                   string.Equals(value, UniqueInstance, StringComparison.Ordinal);
        }
    }

    public static class BossRewardAcquisitionSnapshotPolicies
    {
        public const string SnapshotV1 = "acquisition_snapshot_v1";

        public static bool IsSupported(string value)
        {
            return string.Equals(value, SnapshotV1, StringComparison.Ordinal);
        }
    }

    public enum BossRewardDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public enum BossRewardDiagnosticDomain
    {
        Request = 0,
        Catalog = 1,
        Determinism = 2,
        Inventory = 3,
        Ledger = 4,
        Transaction = 5,
        Notification = 6
    }

    public sealed class BossRewardDiagnostic : IComparable<BossRewardDiagnostic>
    {
        public BossRewardDiagnostic(
            string code,
            BossRewardDiagnosticSeverity severity,
            BossRewardDiagnosticDomain domain,
            string fieldPath,
            bool blocksOperation,
            string safeDeveloperMessage,
            string operationId = "",
            string recordId = "",
            string schemaVersion = "",
            string contentVersion = "")
        {
            if (string.IsNullOrEmpty(code))
                throw new ArgumentException("A diagnostic code is required.", nameof(code));
            if (!code.StartsWith("AL-BOSS-REWARD-", StringComparison.Ordinal))
                throw new ArgumentException("Diagnostic code is outside the boss-reward family.", nameof(code));
            if (!Enum.IsDefined(typeof(BossRewardDiagnosticSeverity), severity))
                throw new ArgumentOutOfRangeException(nameof(severity));
            if (!Enum.IsDefined(typeof(BossRewardDiagnosticDomain), domain))
                throw new ArgumentOutOfRangeException(nameof(domain));

            Code = BossRewardText.Sanitize(code, 96);
            Severity = severity;
            Domain = domain;
            FieldPath = BossRewardText.Sanitize(fieldPath, 256);
            BlocksOperation = blocksOperation;
            SafeDeveloperMessage = BossRewardText.Sanitize(safeDeveloperMessage, 512);
            OperationId = BossRewardText.Sanitize(operationId, 256);
            RecordId = BossRewardText.Sanitize(recordId, 256);
            SchemaVersion = BossRewardText.Sanitize(schemaVersion, 128);
            ContentVersion = BossRewardText.Sanitize(contentVersion, 128);
        }

        public string Code { get; }
        public BossRewardDiagnosticSeverity Severity { get; }
        public BossRewardDiagnosticDomain Domain { get; }
        public string FieldPath { get; }
        public bool BlocksOperation { get; }
        public string SafeDeveloperMessage { get; }
        public string OperationId { get; }
        public string RecordId { get; }
        public string SchemaVersion { get; }
        public string ContentVersion { get; }

        public int CompareTo(BossRewardDiagnostic other)
        {
            if (ReferenceEquals(other, null)) return -1;
            int comparison = Severity.CompareTo(other.Severity);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(Code, other.Code);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(RecordId, other.RecordId);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(FieldPath, other.FieldPath);
            if (comparison != 0) return comparison;
            comparison = Domain.CompareTo(other.Domain);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(OperationId, other.OperationId);
            if (comparison != 0) return comparison;
            comparison = BlocksOperation.CompareTo(other.BlocksOperation);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(
                SchemaVersion,
                other.SchemaVersion);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(
                ContentVersion,
                other.ContentVersion);
            if (comparison != 0) return comparison;
            return StringComparer.Ordinal.Compare(
                SafeDeveloperMessage,
                other.SafeDeveloperMessage);
        }
    }

    public sealed class BossRewardBinding
    {
        public BossRewardBinding(
            string bossDefinitionId,
            string bossDefinitionContentVersion,
            string rewardProfileId,
            string rewardProfileContentVersion)
        {
            BossDefinitionId = bossDefinitionId;
            BossDefinitionContentVersion = bossDefinitionContentVersion;
            RewardProfileId = rewardProfileId;
            RewardProfileContentVersion = rewardProfileContentVersion;
        }

        public string BossDefinitionId { get; }
        public string BossDefinitionContentVersion { get; }
        public string RewardProfileId { get; }
        public string RewardProfileContentVersion { get; }
    }

    public sealed class BossRewardEntry
    {
        public BossRewardEntry(
            string equipmentDefinitionId,
            int dropChanceMicros,
            int quantity,
            string acquisitionAnnouncementPolicyId)
        {
            EquipmentDefinitionId = equipmentDefinitionId;
            DropChanceMicros = dropChanceMicros;
            Quantity = quantity;
            AcquisitionAnnouncementPolicyId = acquisitionAnnouncementPolicyId;
        }

        public string EquipmentDefinitionId { get; }
        public int DropChanceMicros { get; }
        public int Quantity { get; }
        public string AcquisitionAnnouncementPolicyId { get; }
    }

    public sealed class BossRewardProfile
    {
        public BossRewardProfile(
            string gameId,
            string catalogSetId,
            string id,
            string schemaVersion,
            string contentVersion,
            int warzoneCredits,
            bool isExplicitNoReward,
            IEnumerable<BossRewardEntry> entries,
            string sourceRevision,
            string rawSha256)
        {
            GameId = gameId;
            CatalogSetId = catalogSetId;
            Id = id;
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            WarzoneCredits = warzoneCredits;
            IsExplicitNoReward = isExplicitNoReward;
            Entries = BossRewardImmutable.Freeze(entries, BossRewardTechnicalLimits.MaximumRewardEntries);
            SourceRevision = sourceRevision;
            RawSha256 = rawSha256;
        }

        public string GameId { get; }
        public string CatalogSetId { get; }
        public string Id { get; }
        public string SchemaVersion { get; }
        public string ContentVersion { get; }
        public int WarzoneCredits { get; }
        public bool IsExplicitNoReward { get; }
        public IReadOnlyList<BossRewardEntry> Entries { get; }
        public string SourceRevision { get; }
        public string RawSha256 { get; }
    }

    public sealed class BossEquipmentDefinitionSnapshot
    {
        public BossEquipmentDefinitionSnapshot(
            string equipmentDefinitionId,
            string schemaVersion,
            string contentVersion,
            string slotId,
            int attackBonus,
            int defenseBonus,
            int healthBonus,
            string stackPolicyId,
            string acquisitionSnapshotPolicyId,
            string presentationContentKey,
            string sourceRevision,
            string rawSha256)
        {
            EquipmentDefinitionId = equipmentDefinitionId;
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            SlotId = slotId;
            AttackBonus = attackBonus;
            DefenseBonus = defenseBonus;
            HealthBonus = healthBonus;
            StackPolicyId = stackPolicyId;
            AcquisitionSnapshotPolicyId = acquisitionSnapshotPolicyId;
            PresentationContentKey = presentationContentKey;
            SourceRevision = sourceRevision;
            RawSha256 = rawSha256;
        }

        public string EquipmentDefinitionId { get; }
        public string SchemaVersion { get; }
        public string ContentVersion { get; }
        public string SlotId { get; }
        public int AttackBonus { get; }
        public int DefenseBonus { get; }
        public int HealthBonus { get; }
        public string StackPolicyId { get; }
        public string AcquisitionSnapshotPolicyId { get; }
        public string PresentationContentKey { get; }
        public string SourceRevision { get; }
        public string RawSha256 { get; }
    }

    public sealed class BossRewardCatalogSnapshot
    {
        public BossRewardCatalogSnapshot(
            string gameId,
            string catalogSetId,
            string schemaVersion,
            string revision,
            IEnumerable<BossRewardBinding> bindings,
            IEnumerable<BossRewardProfile> profiles,
            IEnumerable<BossEquipmentDefinitionSnapshot> equipmentDefinitions,
            IEnumerable<string> announcementPolicyIds)
        {
            GameId = gameId;
            CatalogSetId = catalogSetId;
            SchemaVersion = schemaVersion;
            Revision = revision;
            Bindings = BossRewardImmutable.Freeze(bindings, BossRewardTechnicalLimits.MaximumCatalogEntries);
            Profiles = BossRewardImmutable.Freeze(profiles, BossRewardTechnicalLimits.MaximumCatalogEntries);
            EquipmentDefinitions = BossRewardImmutable.Freeze(
                equipmentDefinitions,
                BossRewardTechnicalLimits.MaximumCatalogEntries);
            AnnouncementPolicyIds = BossRewardImmutable.Freeze(
                announcementPolicyIds,
                BossRewardTechnicalLimits.MaximumCatalogEntries);
        }

        public string GameId { get; }
        public string CatalogSetId { get; }
        public string SchemaVersion { get; }
        public string Revision { get; }
        public IReadOnlyList<BossRewardBinding> Bindings { get; }
        public IReadOnlyList<BossRewardProfile> Profiles { get; }
        public IReadOnlyList<BossEquipmentDefinitionSnapshot> EquipmentDefinitions { get; }
        public IReadOnlyList<string> AnnouncementPolicyIds { get; }
    }

    public sealed class BossRewardComputationRequest
    {
        public BossRewardComputationRequest(
            string gameId,
            string catalogSetId,
            string profileId,
            string encounterId,
            string encounterCompletionId,
            string rewardResultId,
            string bossDefinitionId,
            string bossDefinitionContentVersion,
            string rewardProfileId,
            string rewardProfileContentVersion,
            string determinismVersion)
        {
            GameId = gameId;
            CatalogSetId = catalogSetId;
            ProfileId = profileId;
            EncounterId = encounterId;
            EncounterCompletionId = encounterCompletionId;
            RewardResultId = rewardResultId;
            BossDefinitionId = bossDefinitionId;
            BossDefinitionContentVersion = bossDefinitionContentVersion;
            RewardProfileId = rewardProfileId;
            RewardProfileContentVersion = rewardProfileContentVersion;
            DeterminismVersion = determinismVersion;
        }

        public string GameId { get; }
        public string CatalogSetId { get; }
        public string ProfileId { get; }
        public string EncounterId { get; }
        public string EncounterCompletionId { get; }
        public string RewardResultId { get; }
        public string BossDefinitionId { get; }
        public string BossDefinitionContentVersion { get; }
        public string RewardProfileId { get; }
        public string RewardProfileContentVersion { get; }
        public string DeterminismVersion { get; }
    }

    public enum BossRewardComputationStatus
    {
        Computed = 0,
        ExplicitNoReward = 1,
        InvalidRequest = 2,
        CatalogUnavailable = 3,
        UnsupportedVersion = 4,
        UnknownBoss = 5,
        UnknownRewardProfile = 6,
        BossRewardBindingMismatch = 7,
        InvalidRewardProfile = 8,
        InvalidEquipmentDefinition = 9,
        DeterminismFailure = 10
    }

    public sealed class BossRewardComputedDrop
    {
        public BossRewardComputedDrop(
            string equipmentDefinitionId,
            string equipmentDefinitionContentVersion,
            string acquisitionSnapshotFingerprint,
            string slotId,
            int attackBonus,
            int defenseBonus,
            int healthBonus,
            int quantity,
            string stackPolicyId,
            string acquisitionAnnouncementPolicyId)
        {
            EquipmentDefinitionId = equipmentDefinitionId;
            EquipmentDefinitionContentVersion = equipmentDefinitionContentVersion;
            AcquisitionSnapshotFingerprint = acquisitionSnapshotFingerprint;
            SlotId = slotId;
            AttackBonus = attackBonus;
            DefenseBonus = defenseBonus;
            HealthBonus = healthBonus;
            Quantity = quantity;
            StackPolicyId = stackPolicyId;
            AcquisitionAnnouncementPolicyId = acquisitionAnnouncementPolicyId;
        }

        public string EquipmentDefinitionId { get; }
        public string EquipmentDefinitionContentVersion { get; }
        public string AcquisitionSnapshotFingerprint { get; }
        public string SlotId { get; }
        public int AttackBonus { get; }
        public int DefenseBonus { get; }
        public int HealthBonus { get; }
        public int Quantity { get; }
        public string StackPolicyId { get; }
        public string AcquisitionAnnouncementPolicyId { get; }
    }

    public sealed class BossRewardComputedValue
    {
        public BossRewardComputedValue(
            string gameId,
            string catalogSetId,
            string profileId,
            string rewardResultId,
            string encounterId,
            string encounterCompletionId,
            string bossDefinitionId,
            string bossDefinitionContentVersion,
            string rewardProfileId,
            string rewardProfileContentVersion,
            string rewardProfileSha256,
            int warzoneCredits,
            bool isExplicitNoReward,
            IEnumerable<BossRewardComputedDrop> drops,
            string determinismVersion,
            string computationHash)
        {
            GameId = gameId;
            CatalogSetId = catalogSetId;
            ProfileId = profileId;
            RewardResultId = rewardResultId;
            EncounterId = encounterId;
            EncounterCompletionId = encounterCompletionId;
            BossDefinitionId = bossDefinitionId;
            BossDefinitionContentVersion = bossDefinitionContentVersion;
            RewardProfileId = rewardProfileId;
            RewardProfileContentVersion = rewardProfileContentVersion;
            RewardProfileSha256 = rewardProfileSha256;
            WarzoneCredits = warzoneCredits;
            IsExplicitNoReward = isExplicitNoReward;
            Drops = BossRewardImmutable.Freeze(drops, BossRewardTechnicalLimits.MaximumRewardEntries);
            DeterminismVersion = determinismVersion;
            ComputationHash = computationHash;
        }

        public string GameId { get; }
        public string CatalogSetId { get; }
        public string ProfileId { get; }
        public string RewardResultId { get; }
        public string EncounterId { get; }
        public string EncounterCompletionId { get; }
        public string BossDefinitionId { get; }
        public string BossDefinitionContentVersion { get; }
        public string RewardProfileId { get; }
        public string RewardProfileContentVersion { get; }
        public string RewardProfileSha256 { get; }
        public int WarzoneCredits { get; }
        public bool IsExplicitNoReward { get; }
        public IReadOnlyList<BossRewardComputedDrop> Drops { get; }
        public string DeterminismVersion { get; }
        public string ComputationHash { get; }
    }

    public sealed class BossRewardComputationResult
    {
        public BossRewardComputationResult(
            BossRewardComputationStatus status,
            BossRewardComputedValue value,
            IEnumerable<BossRewardDiagnostic> diagnostics)
        {
            Status = status;
            Value = value;
            Diagnostics = BossRewardDiagnosticOrdering.Order(diagnostics);
            if ((status == BossRewardComputationStatus.Computed ||
                 status == BossRewardComputationStatus.ExplicitNoReward) != (value != null))
            {
                throw new ArgumentException("Only successful computation statuses expose a value.");
            }
        }

        public BossRewardComputationStatus Status { get; }
        public BossRewardComputedValue Value { get; }
        public IReadOnlyList<BossRewardDiagnostic> Diagnostics { get; }
        public bool IsSuccess =>
            Status == BossRewardComputationStatus.Computed ||
            Status == BossRewardComputationStatus.ExplicitNoReward;
    }

    public static class BossRewardDiagnosticOrdering
    {
        private static readonly IReadOnlyList<BossRewardDiagnostic> Empty =
            Array.AsReadOnly(new BossRewardDiagnostic[0]);

        public static IReadOnlyList<BossRewardDiagnostic> Order(
            IEnumerable<BossRewardDiagnostic> diagnostics)
        {
            if (diagnostics == null) return Empty;
            if (diagnostics is BossRewardDiagnosticCollector collector)
                return collector.ToOrderedReadOnly();

            var bounded = new BossRewardDiagnosticCollector();
            foreach (BossRewardDiagnostic diagnostic in diagnostics)
                bounded.Add(diagnostic);
            return bounded.ToOrderedReadOnly();
        }
    }

    internal sealed class BossRewardDiagnosticCollector :
        IEnumerable<BossRewardDiagnostic>
    {
        private readonly List<BossRewardDiagnostic> selected =
            new List<BossRewardDiagnostic>(
                BossRewardTechnicalLimits.MaximumDiagnostics);
        private bool overflowed;
        private bool isMaxHeap;
        private bool hasDuplicateDiagnostic;
        private bool hasEquipmentDiagnostic;

        public int Count =>
            overflowed
                ? BossRewardTechnicalLimits.MaximumDiagnostics
                : selected.Count;

        public bool HasDuplicateDiagnostic => hasDuplicateDiagnostic;
        public bool HasEquipmentDiagnostic => hasEquipmentDiagnostic;

        public void Add(BossRewardDiagnostic diagnostic)
        {
            if (diagnostic == null)
                throw new ArgumentException(
                    "Diagnostic collection contains a null record.",
                    nameof(diagnostic));

            hasDuplicateDiagnostic |= diagnostic.Code.EndsWith(
                "DUPLICATE",
                StringComparison.Ordinal);
            hasEquipmentDiagnostic |= diagnostic.Code.StartsWith(
                "AL-BOSS-REWARD-CATALOG-EQUIPMENT",
                StringComparison.Ordinal);

            if (!overflowed)
            {
                if (selected.Count <
                    BossRewardTechnicalLimits.MaximumDiagnostics)
                {
                    selected.Add(diagnostic);
                    return;
                }

                overflowed = true;
                BuildMaxHeap();
                RetainIfSmaller(diagnostic);
                RemoveMaximum();
                return;
            }

            RetainIfSmaller(diagnostic);
        }

        public IReadOnlyList<BossRewardDiagnostic> ToOrderedReadOnly()
        {
            if (selected.Count == 0 && !overflowed)
                return Array.AsReadOnly(new BossRewardDiagnostic[0]);

            int outputCount = selected.Count + (overflowed ? 1 : 0);
            var copy = new BossRewardDiagnostic[outputCount];
            selected.CopyTo(copy, 0);
            if (overflowed)
            {
                copy[copy.Length - 1] = new BossRewardDiagnostic(
                    "AL-BOSS-REWARD-TRANSACTION-DIAGNOSTIC-LIMIT",
                    BossRewardDiagnosticSeverity.Error,
                    BossRewardDiagnosticDomain.Transaction,
                    "diagnostics",
                    true,
                    "Additional diagnostics were canonically truncated.");
            }
            Array.Sort(copy);
            return Array.AsReadOnly(copy);
        }

        public IEnumerator<BossRewardDiagnostic> GetEnumerator()
        {
            return ToOrderedReadOnly().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private void RetainIfSmaller(BossRewardDiagnostic diagnostic)
        {
            if (!isMaxHeap)
                BuildMaxHeap();
            if (selected.Count == 0 ||
                diagnostic.CompareTo(selected[0]) >= 0)
                return;
            selected[0] = diagnostic;
            SiftDown(0);
        }

        private void BuildMaxHeap()
        {
            for (int index = (selected.Count / 2) - 1; index >= 0; index--)
                SiftDown(index);
            isMaxHeap = true;
        }

        private void RemoveMaximum()
        {
            int last = selected.Count - 1;
            selected[0] = selected[last];
            selected.RemoveAt(last);
            if (selected.Count > 0)
                SiftDown(0);
        }

        private void SiftDown(int index)
        {
            while (true)
            {
                int left = (index * 2) + 1;
                if (left >= selected.Count) return;
                int right = left + 1;
                int largest =
                    right < selected.Count &&
                    selected[right].CompareTo(selected[left]) > 0
                        ? right
                        : left;
                if (selected[index].CompareTo(selected[largest]) >= 0)
                    return;
                BossRewardDiagnostic swap = selected[index];
                selected[index] = selected[largest];
                selected[largest] = swap;
                index = largest;
            }
        }
    }

    internal static class BossRewardImmutable
    {
        public static IReadOnlyList<T> Freeze<T>(IEnumerable<T> source, int maximum)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new List<T>();
            foreach (T item in source)
            {
                if (copy.Count >= maximum)
                    throw new ArgumentException(
                        "Collection exceeds the technical entry ceiling.");
                copy.Add(item);
            }
            return Array.AsReadOnly(copy.ToArray());
        }
    }

    internal static class BossRewardText
    {
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static bool IsBoundedTechnicalId(string value)
        {
            return IsBoundedTechnicalText(
                value,
                BossRewardTechnicalLimits.MaximumIdentifierUtf8Bytes);
        }

        public static bool IsBoundedOpaqueId(string value)
        {
            return IsBoundedTechnicalText(
                value,
                BossRewardTechnicalLimits.MaximumOpaqueIdentifierUtf8Bytes);
        }

        public static bool IsBoundedRevision(string value)
        {
            return IsBoundedTechnicalText(
                value,
                BossRewardTechnicalLimits.MaximumRevisionUtf8Bytes);
        }

        public static bool IsCanonicalTechnicalId(string value)
        {
            return IsBoundedTechnicalId(value) &&
                   IsCanonicalSegment(value, 0, value.Length);
        }

        public static bool IsBoundedContentKey(string value)
        {
            if (!IsBoundedTechnicalId(value)) return false;
            int segmentStart = 0;
            for (int index = 0; index <= value.Length; index++)
            {
                if (index != value.Length && value[index] != '.') continue;
                if (!IsCanonicalSegment(value, segmentStart, index))
                    return false;
                segmentStart = index + 1;
            }
            return true;
        }

        public static bool IsBoundedVersion(string value)
        {
            return IsBoundedTechnicalText(
                value,
                BossRewardTechnicalLimits.MaximumVersionUtf8Bytes);
        }

        public static bool IsLowerSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                    return false;
            }
            return true;
        }

        public static string Sanitize(string value, int maximumCharacters)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var output = new StringBuilder(Math.Min(value.Length, maximumCharacters));
            for (int index = 0; index < value.Length && output.Length < maximumCharacters; index++)
            {
                char character = value[index];
                output.Append(char.IsControl(character) ? ' ' : character);
            }
            return output.ToString();
        }

        private static bool IsBoundedTechnicalText(string value, int maximumUtf8Bytes)
        {
            if (string.IsNullOrEmpty(value) ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal))
                return false;
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index]) || char.IsWhiteSpace(value[index]))
                    return false;
                if (char.IsSurrogate(value[index]))
                {
                    if (!char.IsHighSurrogate(value[index]) ||
                        index + 1 >= value.Length ||
                        !char.IsLowSurrogate(value[index + 1]))
                        return false;
                    index++;
                }
            }
            return StrictUtf8.GetByteCount(value) <= maximumUtf8Bytes;
        }

        private static bool IsCanonicalSegment(
            string value,
            int startInclusive,
            int endExclusive)
        {
            if (startInclusive >= endExclusive ||
                value[startInclusive] < 'a' ||
                value[startInclusive] > 'z')
                return false;
            bool previousWasUnderscore = false;
            for (int index = startInclusive + 1; index < endExclusive; index++)
            {
                char character = value[index];
                if (character == '_')
                {
                    if (previousWasUnderscore || index + 1 == endExclusive)
                        return false;
                    previousWasUnderscore = true;
                    continue;
                }
                if (!((character >= 'a' && character <= 'z') ||
                      (character >= '0' && character <= '9')))
                    return false;
                previousWasUnderscore = false;
            }
            return true;
        }
    }
}
