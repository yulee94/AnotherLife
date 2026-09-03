using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AL.EditMode.Tests")]

namespace AL.Warmaster.Planning
{
    public enum WarmasterCatalogStatus
    {
        Ready,
        Unavailable,
        UnsupportedVersion,
        Malformed,
        Incomplete,
        ApprovalMissing
    }

    public enum WarmasterStateStatus
    {
        Valid,
        MigrationRequired,
        UnsupportedReadOnly,
        Unavailable,
        Malformed,
        CommitUncertain
    }

    public enum WarmasterWalletStatus
    {
        Available,
        Unavailable,
        Malformed,
        CommitUncertain
    }

    public enum WarmasterOperation
    {
        PurchasePiece,
        UnlockSet,
        EquipSet
    }

    public enum WarmasterAuthorizationStatus
    {
        Allowed,
        Denied,
        Unavailable
    }

    public enum WarmasterPieceAvailability
    {
        Available,
        Unavailable
    }

    public enum WarmasterProgressionMode
    {
        NoChange,
        AddDeltas
    }

    public enum WarmasterCompletionPolicy
    {
        AllMembers
    }

    public enum WarmasterUnlockPolicy
    {
        ManualAfterCompletion,
        AutomaticOnCompletion
    }

    public enum WarmasterEquipPolicy
    {
        ManualOnly,
        AutomaticOnUnlock
    }

    public enum WarmasterPlanStatus
    {
        Prepared,
        AlreadyOwned,
        AlreadyCommitted,
        NoChange,
        InvalidRequest,
        Unauthorized,
        Ineligible,
        InsufficientFunds,
        StaleState,
        StaleEconomy,
        StaleCatalog,
        UnknownDefinition,
        ApprovalMissing,
        MigrationRequired,
        Unsupported,
        Unavailable,
        Malformed,
        Conflict,
        Overflow,
        CommitUncertain
    }

    public sealed class WarmasterDiagnostic
    {
        public WarmasterDiagnostic(string code, string subjectId, string message)
        {
            Code = code ?? string.Empty;
            SubjectId = subjectId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string SubjectId { get; }
        public string Message { get; }
    }

    public sealed class WarmasterCatalogBinding
    {
        public WarmasterCatalogBinding(
            int schemaVersion,
            string contentVersion,
            string sourceRevision,
            string catalogHash,
            string approvalRevision,
            string currencyId)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion ?? string.Empty;
            SourceRevision = sourceRevision ?? string.Empty;
            CatalogHash = catalogHash ?? string.Empty;
            ApprovalRevision = approvalRevision ?? string.Empty;
            CurrencyId = currencyId ?? string.Empty;
        }

        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public string SourceRevision { get; }
        public string CatalogHash { get; }
        public string ApprovalRevision { get; }
        public string CurrencyId { get; }
    }

    public sealed class WarmasterProgressionRule
    {
        public WarmasterProgressionRule(
            WarmasterProgressionMode mode,
            int levelDelta,
            int experienceDelta,
            bool isApproved)
        {
            Mode = mode;
            LevelDelta = levelDelta;
            ExperienceDelta = experienceDelta;
            IsApproved = isApproved;
        }

        public WarmasterProgressionMode Mode { get; }
        public int LevelDelta { get; }
        public int ExperienceDelta { get; }
        public bool IsApproved { get; }
    }

    public sealed class WarmasterPieceDefinition
    {
        public WarmasterPieceDefinition(
            string pieceId,
            string setId,
            long priceAmount,
            WarmasterPieceAvailability availability,
            WarmasterProgressionRule progression,
            bool isApproved)
        {
            PieceId = pieceId ?? string.Empty;
            SetId = setId ?? string.Empty;
            PriceAmount = priceAmount;
            Availability = availability;
            Progression = progression;
            IsApproved = isApproved;
        }

        public string PieceId { get; }
        public string SetId { get; }
        public long PriceAmount { get; }
        public WarmasterPieceAvailability Availability { get; }
        public WarmasterProgressionRule Progression { get; }
        public bool IsApproved { get; }
    }

    public sealed class WarmasterSetDefinition
    {
        public WarmasterSetDefinition(
            string setId,
            IEnumerable<string> memberPieceIds,
            WarmasterCompletionPolicy completionPolicy,
            WarmasterUnlockPolicy unlockPolicy,
            WarmasterEquipPolicy equipPolicy,
            bool isApproved)
        {
            SetId = setId ?? string.Empty;
            MemberPieceIds = memberPieceIds == null
                ? null
                : Array.AsReadOnly(memberPieceIds.ToArray());
            CompletionPolicy = completionPolicy;
            UnlockPolicy = unlockPolicy;
            EquipPolicy = equipPolicy;
            IsApproved = isApproved;
        }

        public string SetId { get; }
        public IReadOnlyList<string> MemberPieceIds { get; }
        public WarmasterCompletionPolicy CompletionPolicy { get; }
        public WarmasterUnlockPolicy UnlockPolicy { get; }
        public WarmasterEquipPolicy EquipPolicy { get; }
        public bool IsApproved { get; }
    }

    public sealed class WarmasterCatalogSnapshot
    {
        public WarmasterCatalogSnapshot(
            WarmasterCatalogStatus status,
            WarmasterCatalogBinding binding,
            IEnumerable<WarmasterSetDefinition> sets,
            IEnumerable<WarmasterPieceDefinition> pieces,
            bool isComplete)
        {
            Status = status;
            Binding = binding;
            Sets = sets == null ? null : Array.AsReadOnly(sets.ToArray());
            Pieces = pieces == null ? null : Array.AsReadOnly(pieces.ToArray());
            IsComplete = isComplete;
        }

        public WarmasterCatalogStatus Status { get; }
        public WarmasterCatalogBinding Binding { get; }
        public IReadOnlyList<WarmasterSetDefinition> Sets { get; }
        public IReadOnlyList<WarmasterPieceDefinition> Pieces { get; }
        public bool IsComplete { get; }
    }

    public sealed class WarmasterOwnedPieceRecord
    {
        public WarmasterOwnedPieceRecord(string pieceId, bool isSupported)
        {
            PieceId = pieceId ?? string.Empty;
            IsSupported = isSupported;
        }

        public string PieceId { get; }
        public bool IsSupported { get; }
    }

    public sealed class WarmasterUnlockedSetRecord
    {
        public WarmasterUnlockedSetRecord(string setId, bool isSupported)
        {
            SetId = setId ?? string.Empty;
            IsSupported = isSupported;
        }

        public string SetId { get; }
        public bool IsSupported { get; }
    }

    public sealed class WarmasterWalletSnapshot
    {
        public WarmasterWalletSnapshot(
            WarmasterWalletStatus status,
            string currencyId,
            long balance,
            long revision,
            bool isComplete)
        {
            Status = status;
            CurrencyId = currencyId ?? string.Empty;
            Balance = balance;
            Revision = revision;
            IsComplete = isComplete;
        }

        public WarmasterWalletStatus Status { get; }
        public string CurrencyId { get; }
        public long Balance { get; }
        public long Revision { get; }
        public bool IsComplete { get; }
    }

    public sealed class WarmasterTransactionRecord
    {
        public WarmasterTransactionRecord(
            string operationId,
            string eventId,
            string correlationId,
            string profileId,
            WarmasterOperation operation,
            string requestFingerprint,
            WarmasterCatalogBinding catalogBinding,
            string setId,
            string pieceId,
            string currencyId,
            long debitAmount,
            string definitionFingerprint,
            long resultingStateRevision,
            long resultingEconomyRevision,
            string resultingStateHash,
            string planHash,
            string postCommitNotificationCorrelationId,
            bool isSupported)
        {
            OperationId = operationId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            Operation = operation;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            CatalogBinding = catalogBinding;
            SetId = setId ?? string.Empty;
            PieceId = pieceId ?? string.Empty;
            CurrencyId = currencyId ?? string.Empty;
            DebitAmount = debitAmount;
            DefinitionFingerprint = definitionFingerprint ?? string.Empty;
            ResultingStateRevision = resultingStateRevision;
            ResultingEconomyRevision = resultingEconomyRevision;
            ResultingStateHash = resultingStateHash ?? string.Empty;
            PlanHash = planHash ?? string.Empty;
            PostCommitNotificationCorrelationId =
                postCommitNotificationCorrelationId ?? string.Empty;
            IsSupported = isSupported;
        }

        public string OperationId { get; }
        public string EventId { get; }
        public string CorrelationId { get; }
        public string ProfileId { get; }
        public WarmasterOperation Operation { get; }
        public string RequestFingerprint { get; }
        public WarmasterCatalogBinding CatalogBinding { get; }
        public string SetId { get; }
        public string PieceId { get; }
        public string CurrencyId { get; }
        public long DebitAmount { get; }
        public string DefinitionFingerprint { get; }
        public long ResultingStateRevision { get; }
        public long ResultingEconomyRevision { get; }
        public string ResultingStateHash { get; }
        public string PlanHash { get; }
        public string PostCommitNotificationCorrelationId { get; }
        public bool IsSupported { get; }
    }

    public sealed class WarmasterStateSnapshot
    {
        public WarmasterStateSnapshot(
            WarmasterStateStatus status,
            string profileId,
            long revision,
            WarmasterCatalogBinding catalogBinding,
            IEnumerable<WarmasterOwnedPieceRecord> purchasedPieces,
            IEnumerable<WarmasterUnlockedSetRecord> unlockedSets,
            string equippedSetId,
            bool legacyTrueWarmasterFlag,
            int level,
            int experience,
            IEnumerable<WarmasterTransactionRecord> transactionRecords,
            bool isComplete)
        {
            Status = status;
            ProfileId = profileId ?? string.Empty;
            Revision = revision;
            CatalogBinding = catalogBinding;
            PurchasedPieces = purchasedPieces == null
                ? null
                : Array.AsReadOnly(purchasedPieces.ToArray());
            UnlockedSets = unlockedSets == null
                ? null
                : Array.AsReadOnly(unlockedSets.ToArray());
            EquippedSetId = equippedSetId ?? string.Empty;
            LegacyTrueWarmasterFlag = legacyTrueWarmasterFlag;
            Level = level;
            Experience = experience;
            TransactionRecords = transactionRecords == null
                ? null
                : Array.AsReadOnly(transactionRecords.ToArray());
            IsComplete = isComplete;
        }

        public WarmasterStateStatus Status { get; }
        public string ProfileId { get; }
        public long Revision { get; }
        public WarmasterCatalogBinding CatalogBinding { get; }
        public IReadOnlyList<WarmasterOwnedPieceRecord> PurchasedPieces { get; }
        public IReadOnlyList<WarmasterUnlockedSetRecord> UnlockedSets { get; }
        public string EquippedSetId { get; }
        public bool LegacyTrueWarmasterFlag { get; }
        public int Level { get; }
        public int Experience { get; }
        public IReadOnlyList<WarmasterTransactionRecord> TransactionRecords { get; }
        public bool IsComplete { get; }
    }

    public sealed class WarmasterTransactionRequest
    {
        public WarmasterTransactionRequest(
            WarmasterOperation operation,
            string profileId,
            string actorId,
            string operationId,
            string eventId,
            string correlationId,
            string setId,
            string pieceId,
            long expectedStateRevision,
            long expectedEconomyRevision,
            WarmasterCatalogBinding expectedCatalogBinding,
            WarmasterVerifiedReceipt priorReceipt = null)
        {
            Operation = operation;
            ProfileId = profileId ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            SetId = setId ?? string.Empty;
            PieceId = pieceId ?? string.Empty;
            ExpectedStateRevision = expectedStateRevision;
            ExpectedEconomyRevision = expectedEconomyRevision;
            ExpectedCatalogBinding = expectedCatalogBinding;
            PriorReceipt = priorReceipt;
        }

        public WarmasterOperation Operation { get; }
        public string ProfileId { get; }
        public string ActorId { get; }
        public string OperationId { get; }
        public string EventId { get; }
        public string CorrelationId { get; }
        public string SetId { get; }
        public string PieceId { get; }
        public long ExpectedStateRevision { get; }
        public long ExpectedEconomyRevision { get; }
        public WarmasterCatalogBinding ExpectedCatalogBinding { get; }
        public WarmasterVerifiedReceipt PriorReceipt { get; }
    }

    public sealed class WarmasterEconomyDebitIntent
    {
        internal WarmasterEconomyDebitIntent(
            string currencyId,
            long amount,
            long expectedRevision,
            long candidateRevision,
            long candidateBalance,
            string idempotencyKey)
        {
            CurrencyId = currencyId;
            Amount = amount;
            ExpectedRevision = expectedRevision;
            CandidateRevision = candidateRevision;
            CandidateBalance = candidateBalance;
            IdempotencyKey = idempotencyKey;
        }

        public string CurrencyId { get; }
        public long Amount { get; }
        public long ExpectedRevision { get; }
        public long CandidateRevision { get; }
        public long CandidateBalance { get; }
        public string IdempotencyKey { get; }
    }

    public sealed class WarmasterTransactionPlan
    {
        internal WarmasterTransactionPlan(
            WarmasterOperation operation,
            string requestFingerprint,
            WarmasterCatalogBinding expectedCatalogBinding,
            WarmasterStateSnapshot expectedState,
            WarmasterStateSnapshot candidateState,
            WarmasterTransactionRecord transactionRecord,
            WarmasterEconomyDebitIntent economyDebit,
            string planHash)
        {
            Operation = operation;
            RequestFingerprint = requestFingerprint;
            ExpectedCatalogBinding = expectedCatalogBinding;
            ExpectedState = expectedState;
            CandidateState = candidateState;
            TransactionRecord = transactionRecord;
            EconomyDebit = economyDebit;
            PlanHash = planHash;
        }

        public WarmasterOperation Operation { get; }
        public string RequestFingerprint { get; }
        public WarmasterCatalogBinding ExpectedCatalogBinding { get; }
        public WarmasterStateSnapshot ExpectedState { get; }
        public WarmasterStateSnapshot CandidateState { get; }
        public WarmasterTransactionRecord TransactionRecord { get; }
        public WarmasterEconomyDebitIntent EconomyDebit { get; }
        public string PlanHash { get; }
        public bool RequiresEconomyDebit => EconomyDebit != null;
        public string PostCommitNotificationCorrelationId =>
            TransactionRecord?.PostCommitNotificationCorrelationId ?? string.Empty;
    }

    public sealed class WarmasterVerifiedReceipt
    {
        internal WarmasterVerifiedReceipt(
            WarmasterTransactionRecord transactionRecord,
            string verifiedGenerationFingerprint,
            long verifiedStateRevision,
            long verifiedEconomyRevision,
            string receiptHash)
        {
            TransactionRecord = transactionRecord;
            VerifiedGenerationFingerprint = verifiedGenerationFingerprint ?? string.Empty;
            VerifiedStateRevision = verifiedStateRevision;
            VerifiedEconomyRevision = verifiedEconomyRevision;
            ReceiptHash = receiptHash ?? string.Empty;
        }

        public WarmasterTransactionRecord TransactionRecord { get; }
        public string VerifiedGenerationFingerprint { get; }
        public long VerifiedStateRevision { get; }
        public long VerifiedEconomyRevision { get; }
        public string ReceiptHash { get; }
        public string PostCommitNotificationCorrelationId =>
            TransactionRecord?.PostCommitNotificationCorrelationId ?? string.Empty;
    }

    public sealed class WarmasterPlanningResult
    {
        internal WarmasterPlanningResult(
            WarmasterPlanStatus status,
            WarmasterTransactionPlan plan,
            WarmasterTransactionRecord existingRecord,
            WarmasterVerifiedReceipt existingReceipt,
            IEnumerable<WarmasterDiagnostic> diagnostics)
        {
            Status = status;
            Plan = plan;
            ExistingRecord = existingRecord;
            ExistingReceipt = existingReceipt;
            Diagnostics = Array.AsReadOnly((diagnostics ?? Array.Empty<WarmasterDiagnostic>())
                .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.SubjectId, StringComparer.Ordinal)
                .ToArray());
        }

        public WarmasterPlanStatus Status { get; }
        public WarmasterTransactionPlan Plan { get; }
        public WarmasterTransactionRecord ExistingRecord { get; }
        public WarmasterVerifiedReceipt ExistingReceipt { get; }
        public IReadOnlyList<WarmasterDiagnostic> Diagnostics { get; }
        public bool IsPrepared => Status == WarmasterPlanStatus.Prepared && Plan != null;
    }

    public interface IWarmasterTransactionAuthority
    {
        WarmasterAuthorizationStatus Authorize(
            WarmasterTransactionRequest request,
            WarmasterStateSnapshot currentState);
    }
}
