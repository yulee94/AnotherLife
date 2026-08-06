using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AL.Core;

namespace AL.RealmWar.Territories.Contracts
{
    public static class TerritoryTechnicalLimits
    {
        public const int MaximumDefinitions = 256;
        public const int MaximumRewardProfiles = 64;
        public const int MaximumAliases = 256;
        public const int MaximumAllowedOwners = 4;
        public const int MaximumReferenceIds = 64;
        public const int MaximumStateRows = 512;
        public const int MaximumMigrationRows = MaximumStateRows + MaximumDefinitions;
        public const int MaximumReceipts = 256;
        public const int MaximumDiagnostics = 128;
        public const int MaximumDiagnosticCandidates = MaximumDiagnostics * 32;
        public const int MaximumTechnicalIdUtf8Bytes = 128;
        public const int MaximumContentKeyUtf8Bytes = 256;
        public const int MaximumHashFrames = 4096;
        public const int MaximumHashFrameUtf8Bytes = 4096;
        public const long MaximumBonusAmountPerTerritory = long.MaxValue / MaximumDefinitions;
    }

    public enum TerritoryCatalogValidationStatus
    {
        Valid,
        Invalid
    }

    public enum TerritoryInitializationMode
    {
        NewProfile,
        Initialized,
        Legacy,
        FutureIntentionallyEmpty
    }

    public enum TerritoryMigrationStatus
    {
        Planned,
        AlreadyInitialized,
        AlreadyCommittedReplay,
        RequiresRicherCandidate,
        Rejected,
        RejectedStaleRevision,
        CommitUncertain,
        CorrelationConflict
    }

    public enum TerritoryMigrationActionKind
    {
        InitializeKnown,
        PreserveKnown,
        MigrateAlias,
        PreserveUnknown
    }

    public enum TerritoryOperationDurability
    {
        Committed,
        RolledBack,
        CommitUncertain
    }

    public enum TerritoryApplyStepStatus
    {
        Applied,
        Rejected,
        Unavailable
    }

    public enum TerritoryCommitStatus
    {
        Committed,
        Rejected,
        Uncertain
    }

    public enum TerritoryApplyDisposition
    {
        Committed,
        Replayed,
        NoChange,
        Rejected,
        RolledBack,
        CommitUncertain
    }

    public sealed class TerritoryCatalogIdentity
    {
        public TerritoryCatalogIdentity(
            string catalogId,
            int schemaVersion,
            int contentVersion,
            string sourceRevision,
            string rawSha256)
        {
            CatalogId = catalogId ?? string.Empty;
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            SourceRevision = sourceRevision ?? string.Empty;
            RawSha256 = rawSha256 ?? string.Empty;
        }

        public string CatalogId { get; }
        public int SchemaVersion { get; }
        public int ContentVersion { get; }
        public string SourceRevision { get; }
        public string RawSha256 { get; }
    }

    public sealed class TerritoryCaptureRewardProfile
    {
        public TerritoryCaptureRewardProfile(
            string rewardProfileId,
            int warzoneCredits,
            string questProgressType,
            int questProgressDelta)
        {
            RewardProfileId = rewardProfileId ?? string.Empty;
            WarzoneCredits = warzoneCredits;
            QuestProgressType = questProgressType ?? string.Empty;
            QuestProgressDelta = questProgressDelta;
        }

        public string RewardProfileId { get; }
        public int WarzoneCredits { get; }
        public string QuestProgressType { get; }
        public int QuestProgressDelta { get; }
    }

    public sealed class TerritoryAliasDefinition
    {
        public TerritoryAliasDefinition(string oldTerritoryId, string newTerritoryId, int introducedInVersion)
        {
            OldTerritoryId = oldTerritoryId ?? string.Empty;
            NewTerritoryId = newTerritoryId ?? string.Empty;
            IntroducedInVersion = introducedInVersion;
        }

        public string OldTerritoryId { get; }
        public string NewTerritoryId { get; }
        public int IntroducedInVersion { get; }
    }

    public sealed class TerritoryPhaseBCatalog
    {
        public TerritoryPhaseBCatalog(
            TerritoryCatalogIdentity identity,
            IEnumerable<TerritoryDefinition> definitions,
            IEnumerable<TerritoryCaptureRewardProfile> rewardProfiles,
            IEnumerable<TerritoryAliasDefinition> aliases)
        {
            Identity = identity;
            Definitions = TerritoryPhaseBCollections.FreezeBounded(
                definitions,
                TerritoryTechnicalLimits.MaximumDefinitions + 1);
            RewardProfiles = TerritoryPhaseBCollections.FreezeBounded(
                rewardProfiles,
                TerritoryTechnicalLimits.MaximumRewardProfiles + 1);
            Aliases = TerritoryPhaseBCollections.FreezeBounded(
                aliases,
                TerritoryTechnicalLimits.MaximumAliases + 1);
        }

        public TerritoryCatalogIdentity Identity { get; }
        public IReadOnlyList<TerritoryDefinition> Definitions { get; }
        public IReadOnlyList<TerritoryCaptureRewardProfile> RewardProfiles { get; }
        public IReadOnlyList<TerritoryAliasDefinition> Aliases { get; }
    }

    public sealed class TerritoryCatalogValidationResult
    {
        public TerritoryCatalogValidationResult(
            TerritoryCatalogValidationStatus status,
            string catalogSemanticHash,
            IEnumerable<TerritoryDiagnostic> diagnostics)
        {
            Status = status;
            CatalogSemanticHash = catalogSemanticHash ?? string.Empty;
            Diagnostics = TerritoryPhaseBCollections.FreezeDiagnostics(diagnostics);
        }

        public TerritoryCatalogValidationStatus Status { get; }
        public string CatalogSemanticHash { get; }
        public IReadOnlyList<TerritoryDiagnostic> Diagnostics { get; }
    }

    public sealed class TerritoryInitializationRequest
    {
        public TerritoryInitializationRequest(
            string operationId,
            TerritoryInitializationMode mode,
            string expectedCatalogId,
            string expectedStateRevisionHash,
            bool hasRicherBackup,
            bool authorizeBaselineInitialization)
            : this(
                operationId,
                mode,
                new TerritoryCatalogIdentity(expectedCatalogId, 0, 0, string.Empty, string.Empty),
                expectedStateRevisionHash,
                hasRicherBackup,
                authorizeBaselineInitialization)
        {
        }

        public TerritoryInitializationRequest(
            string operationId,
            TerritoryInitializationMode mode,
            TerritoryCatalogIdentity expectedCatalogIdentity,
            string expectedStateRevisionHash,
            bool hasRicherBackup,
            bool authorizeBaselineInitialization)
        {
            OperationId = operationId ?? string.Empty;
            Mode = mode;
            ExpectedCatalogIdentity = expectedCatalogIdentity;
            ExpectedCatalogId = expectedCatalogIdentity?.CatalogId ?? string.Empty;
            ExpectedStateRevisionHash = expectedStateRevisionHash ?? string.Empty;
            HasRicherBackup = hasRicherBackup;
            AuthorizeBaselineInitialization = authorizeBaselineInitialization;
        }

        public string OperationId { get; }
        public TerritoryInitializationMode Mode { get; }
        public TerritoryCatalogIdentity ExpectedCatalogIdentity { get; }
        public string ExpectedCatalogId { get; }
        public string ExpectedStateRevisionHash { get; }
        public bool HasRicherBackup { get; }
        public bool AuthorizeBaselineInitialization { get; }
    }

    public sealed class TerritoryMigrationAction
    {
        public TerritoryMigrationAction(TerritoryMigrationActionKind kind, TerritoryStateRecord state)
        {
            Kind = kind;
            State = state;
        }

        public TerritoryMigrationActionKind Kind { get; }
        public TerritoryStateRecord State { get; }
    }

    public sealed class TerritoryOperationReceipt
    {
        public TerritoryOperationReceipt(
            string operationId,
            string semanticHash,
            TerritoryOperationDurability durability,
            string resultId)
        {
            OperationId = operationId ?? string.Empty;
            SemanticHash = semanticHash ?? string.Empty;
            Durability = durability;
            ResultId = resultId ?? string.Empty;
        }

        public string OperationId { get; }
        public string SemanticHash { get; }
        public TerritoryOperationDurability Durability { get; }
        public string ResultId { get; }
    }

    public sealed class TerritoryMigrationPlan
    {
        public TerritoryMigrationPlan(
            TerritoryMigrationStatus status,
            string operationId,
            string semanticHash,
            string resultId,
            string stateRevisionHash,
            IEnumerable<TerritoryMigrationAction> actions,
            IEnumerable<TerritoryStateRecord> outputStates,
            IEnumerable<TerritoryStateRecord> preservedUnknownStates,
            TerritoryOperationReceipt existingReceipt,
            IEnumerable<TerritoryDiagnostic> diagnostics)
        {
            Status = status;
            OperationId = operationId ?? string.Empty;
            SemanticHash = semanticHash ?? string.Empty;
            ResultId = resultId ?? string.Empty;
            StateRevisionHash = stateRevisionHash ?? string.Empty;
            Actions = TerritoryPhaseBCollections.FreezeBounded(
                actions,
                TerritoryTechnicalLimits.MaximumMigrationRows + 1);
            OutputStates = TerritoryPhaseBCollections.FreezeBounded(
                outputStates,
                TerritoryTechnicalLimits.MaximumMigrationRows + 1);
            PreservedUnknownStates = TerritoryPhaseBCollections.FreezeBounded(
                preservedUnknownStates,
                TerritoryTechnicalLimits.MaximumStateRows + 1);
            ExistingReceipt = existingReceipt;
            Diagnostics = TerritoryPhaseBCollections.FreezeDiagnostics(diagnostics);
        }

        public TerritoryMigrationStatus Status { get; }
        public string OperationId { get; }
        public string SemanticHash { get; }
        public string ResultId { get; }
        public string StateRevisionHash { get; }
        public IReadOnlyList<TerritoryMigrationAction> Actions { get; }
        public IReadOnlyList<TerritoryStateRecord> OutputStates { get; }
        public IReadOnlyList<TerritoryStateRecord> PreservedUnknownStates { get; }
        public TerritoryOperationReceipt ExistingReceipt { get; }
        public IReadOnlyList<TerritoryDiagnostic> Diagnostics { get; }
    }

    public sealed class TerritoryCaptureTransactionRequest
    {
        public TerritoryCaptureTransactionRequest(
            TerritoryCaptureRequest captureRequest,
            string expectedCatalogId,
            string expectedStateRevisionHash,
            string profileSessionId)
            : this(
                captureRequest,
                new TerritoryCatalogIdentity(expectedCatalogId, 0, 0, string.Empty, string.Empty),
                expectedStateRevisionHash,
                profileSessionId,
                0)
        {
        }

        public TerritoryCaptureTransactionRequest(
            TerritoryCaptureRequest captureRequest,
            TerritoryCatalogIdentity expectedCatalogIdentity,
            string expectedStateRevisionHash,
            string profileSessionId,
            long authorizationEvaluationUtcTicks)
        {
            CaptureRequest = captureRequest;
            ExpectedCatalogIdentity = expectedCatalogIdentity;
            ExpectedCatalogId = expectedCatalogIdentity?.CatalogId ?? string.Empty;
            ExpectedStateRevisionHash = expectedStateRevisionHash ?? string.Empty;
            ProfileSessionId = profileSessionId ?? string.Empty;
            AuthorizationEvaluationUtcTicks = authorizationEvaluationUtcTicks;
        }

        public TerritoryCaptureRequest CaptureRequest { get; }
        public TerritoryCatalogIdentity ExpectedCatalogIdentity { get; }
        public string ExpectedCatalogId { get; }
        public string ExpectedStateRevisionHash { get; }
        public string ProfileSessionId { get; }
        public long AuthorizationEvaluationUtcTicks { get; }
    }

    public sealed class TerritoryEconomyCommand
    {
        public TerritoryEconomyCommand(
            string operationId,
            string rewardProfileId,
            int warzoneCreditsDelta)
        {
            OperationId = operationId ?? string.Empty;
            RewardProfileId = rewardProfileId ?? string.Empty;
            WarzoneCreditsDelta = warzoneCreditsDelta;
        }

        public string OperationId { get; }
        public string RewardProfileId { get; }
        public int WarzoneCreditsDelta { get; }
    }

    public sealed class TerritoryQuestCommand
    {
        public TerritoryQuestCommand(
            string operationId,
            string progressType,
            int progressDelta)
        {
            OperationId = operationId ?? string.Empty;
            ProgressType = progressType ?? string.Empty;
            ProgressDelta = progressDelta;
        }

        public string OperationId { get; }
        public string ProgressType { get; }
        public int ProgressDelta { get; }
    }

    public sealed class TerritoryCaptureCommittedEvent
    {
        public TerritoryCaptureCommittedEvent(
            string eventId,
            string captureOperationId,
            string territoryId,
            RealmId previousOwner,
            RealmId newOwner,
            long previousRevision,
            long newRevision,
            string catalogId,
            string stateRevisionHash,
            string receiptId)
            : this(
                eventId,
                captureOperationId,
                territoryId,
                previousOwner,
                newOwner,
                previousRevision,
                newRevision,
                new TerritoryCatalogIdentity(catalogId, 0, 0, string.Empty, string.Empty),
                stateRevisionHash,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                receiptId)
        {
        }

        public TerritoryCaptureCommittedEvent(
            string eventId,
            string captureOperationId,
            string territoryId,
            RealmId previousOwner,
            RealmId newOwner,
            long previousRevision,
            long newRevision,
            TerritoryCatalogIdentity catalogIdentity,
            string stateRevisionHash,
            string profileSessionId,
            string authorizationId,
            string authorizationSourceResultId,
            string authorizationSourceResultHash,
            string receiptId)
        {
            EventId = eventId ?? string.Empty;
            CaptureOperationId = captureOperationId ?? string.Empty;
            TerritoryId = territoryId ?? string.Empty;
            PreviousOwner = previousOwner;
            NewOwner = newOwner;
            PreviousRevision = previousRevision;
            NewRevision = newRevision;
            CatalogId = catalogIdentity?.CatalogId ?? string.Empty;
            CatalogSchemaVersion = catalogIdentity?.SchemaVersion ?? 0;
            CatalogContentVersion = catalogIdentity?.ContentVersion ?? 0;
            CatalogSourceRevision = catalogIdentity?.SourceRevision ?? string.Empty;
            CatalogRawSha256 = catalogIdentity?.RawSha256 ?? string.Empty;
            StateRevisionHash = stateRevisionHash ?? string.Empty;
            ProfileSessionId = profileSessionId ?? string.Empty;
            AuthorizationId = authorizationId ?? string.Empty;
            AuthorizationSourceResultId = authorizationSourceResultId ?? string.Empty;
            AuthorizationSourceResultHash = authorizationSourceResultHash ?? string.Empty;
            ReceiptId = receiptId ?? string.Empty;
        }

        public string EventId { get; }
        public string CaptureOperationId { get; }
        public string TerritoryId { get; }
        public RealmId PreviousOwner { get; }
        public RealmId NewOwner { get; }
        public long PreviousRevision { get; }
        public long NewRevision { get; }
        public string CatalogId { get; }
        public int CatalogSchemaVersion { get; }
        public int CatalogContentVersion { get; }
        public string CatalogSourceRevision { get; }
        public string CatalogRawSha256 { get; }
        public string StateRevisionHash { get; }
        public string ProfileSessionId { get; }
        public string AuthorizationId { get; }
        public string AuthorizationSourceResultId { get; }
        public string AuthorizationSourceResultHash { get; }
        public string ReceiptId { get; }
    }

    public sealed class TerritoryCaptureReceipt
    {
        public TerritoryCaptureReceipt(
            string receiptId,
            string operationId,
            string semanticHash,
            TerritoryOperationDurability durability,
            string resultId,
            string eventId,
            string territoryId,
            RealmId previousOwner,
            RealmId newOwner,
            long previousRevision,
            long newRevision,
            int warzoneCreditsDelta,
            int questProgressDelta)
            : this(
                receiptId,
                operationId,
                semanticHash,
                durability,
                resultId,
                eventId,
                territoryId,
                previousOwner,
                newOwner,
                previousRevision,
                newRevision,
                warzoneCreditsDelta,
                questProgressDelta,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty)
        {
        }

        public TerritoryCaptureReceipt(
            string receiptId,
            string operationId,
            string semanticHash,
            TerritoryOperationDurability durability,
            string resultId,
            string eventId,
            string territoryId,
            RealmId previousOwner,
            RealmId newOwner,
            long previousRevision,
            long newRevision,
            int warzoneCreditsDelta,
            int questProgressDelta,
            TerritoryCatalogIdentity catalogIdentity,
            string stateRevisionHash,
            string profileSessionId,
            string authorizationId,
            string authorizationSourceResultId,
            string authorizationSourceResultHash)
        {
            ReceiptId = receiptId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            SemanticHash = semanticHash ?? string.Empty;
            Durability = durability;
            ResultId = resultId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            TerritoryId = territoryId ?? string.Empty;
            PreviousOwner = previousOwner;
            NewOwner = newOwner;
            PreviousRevision = previousRevision;
            NewRevision = newRevision;
            WarzoneCreditsDelta = warzoneCreditsDelta;
            QuestProgressDelta = questProgressDelta;
            CatalogId = catalogIdentity?.CatalogId ?? string.Empty;
            CatalogSchemaVersion = catalogIdentity?.SchemaVersion ?? 0;
            CatalogContentVersion = catalogIdentity?.ContentVersion ?? 0;
            CatalogSourceRevision = catalogIdentity?.SourceRevision ?? string.Empty;
            CatalogRawSha256 = catalogIdentity?.RawSha256 ?? string.Empty;
            StateRevisionHash = stateRevisionHash ?? string.Empty;
            ProfileSessionId = profileSessionId ?? string.Empty;
            AuthorizationId = authorizationId ?? string.Empty;
            AuthorizationSourceResultId = authorizationSourceResultId ?? string.Empty;
            AuthorizationSourceResultHash = authorizationSourceResultHash ?? string.Empty;
        }

        public string ReceiptId { get; }
        public string OperationId { get; }
        public string SemanticHash { get; }
        public TerritoryOperationDurability Durability { get; }
        public string ResultId { get; }
        public string EventId { get; }
        public string TerritoryId { get; }
        public RealmId PreviousOwner { get; }
        public RealmId NewOwner { get; }
        public long PreviousRevision { get; }
        public long NewRevision { get; }
        public int WarzoneCreditsDelta { get; }
        public int QuestProgressDelta { get; }
        public string CatalogId { get; }
        public int CatalogSchemaVersion { get; }
        public int CatalogContentVersion { get; }
        public string CatalogSourceRevision { get; }
        public string CatalogRawSha256 { get; }
        public string StateRevisionHash { get; }
        public string ProfileSessionId { get; }
        public string AuthorizationId { get; }
        public string AuthorizationSourceResultId { get; }
        public string AuthorizationSourceResultHash { get; }
    }

    public sealed class TerritoryCaptureTransactionPlan
    {
        public TerritoryCaptureTransactionPlan(
            TerritoryCaptureStatus status,
            string semanticHash,
            string resultId,
            string receiptId,
            string rewardProfileId,
            TerritoryCapturePlan capturePlan,
            TerritoryEconomyCommand economyCommand,
            TerritoryQuestCommand questCommand,
            TerritoryCaptureCommittedEvent committedEvent,
            TerritoryCaptureReceipt existingReceipt,
            IEnumerable<TerritoryDiagnostic> diagnostics)
            : this(
                status,
                semanticHash,
                resultId,
                receiptId,
                rewardProfileId,
                capturePlan,
                economyCommand,
                questCommand,
                committedEvent,
                existingReceipt,
                diagnostics,
                null)
        {
        }

        internal TerritoryCaptureTransactionPlan(
            TerritoryCaptureStatus status,
            string semanticHash,
            string resultId,
            string receiptId,
            string rewardProfileId,
            TerritoryCapturePlan capturePlan,
            TerritoryEconomyCommand economyCommand,
            TerritoryQuestCommand questCommand,
            TerritoryCaptureCommittedEvent committedEvent,
            TerritoryCaptureReceipt existingReceipt,
            IEnumerable<TerritoryDiagnostic> diagnostics,
            object plannerProvenance)
        {
            Status = status;
            SemanticHash = semanticHash ?? string.Empty;
            ResultId = resultId ?? string.Empty;
            ReceiptId = receiptId ?? string.Empty;
            RewardProfileId = rewardProfileId ?? string.Empty;
            CapturePlan = capturePlan;
            EconomyCommand = economyCommand;
            QuestCommand = questCommand;
            Event = committedEvent;
            ExistingReceipt = existingReceipt;
            Diagnostics = TerritoryPhaseBCollections.FreezeDiagnostics(diagnostics);
            _plannerProvenance = plannerProvenance;
        }

        private readonly object _plannerProvenance;

        public TerritoryCaptureStatus Status { get; }
        public string SemanticHash { get; }
        public string ResultId { get; }
        public string ReceiptId { get; }
        public string RewardProfileId { get; }
        public TerritoryCapturePlan CapturePlan { get; }
        public TerritoryEconomyCommand EconomyCommand { get; }
        public TerritoryQuestCommand QuestCommand { get; }
        public TerritoryCaptureCommittedEvent Event { get; }
        public TerritoryCaptureReceipt ExistingReceipt { get; }
        public IReadOnlyList<TerritoryDiagnostic> Diagnostics { get; }

        internal bool HasPlannerProvenance(object expected)
        {
            return expected != null && ReferenceEquals(_plannerProvenance, expected);
        }
    }

    public sealed class TerritoryCaptureApplicationResult
    {
        public TerritoryCaptureApplicationResult(
            TerritoryApplyDisposition disposition,
            TerritoryCaptureTransactionPlan plan,
            TerritoryCaptureReceipt receipt,
            TerritoryCaptureCommittedEvent committedEvent,
            IEnumerable<TerritoryDiagnostic> diagnostics)
        {
            Disposition = disposition;
            Plan = plan;
            Receipt = receipt;
            Event = committedEvent;
            Diagnostics = TerritoryPhaseBCollections.FreezeDiagnostics(diagnostics);
        }

        public TerritoryApplyDisposition Disposition { get; }
        public TerritoryCaptureTransactionPlan Plan { get; }
        public TerritoryCaptureReceipt Receipt { get; }
        public TerritoryCaptureCommittedEvent Event { get; }
        public IReadOnlyList<TerritoryDiagnostic> Diagnostics { get; }
    }

    public interface ITerritoryCandidateApplyTarget
    {
        // Must atomically reject a stale previous owner/revision and an already-bound
        // operation/result before staging ownership. No reward target is invoked first.
        TerritoryApplyStepStatus ApplyOwnership(TerritoryCaptureTransactionPlan plan);
        // Stages the receipt in the same candidate transaction as ownership/outbox.
        TerritoryApplyStepStatus ApplyReceipt(TerritoryCaptureReceipt receipt);
        // Stages the committed event in the same candidate transaction.
        TerritoryApplyStepStatus ApplyOutbox(TerritoryCaptureCommittedEvent committedEvent);
        // Atomically commits staged ownership, receipt/ledger identity, and outbox.
        TerritoryCommitStatus Commit(TerritoryCaptureTransactionPlan plan);
        bool Rollback(TerritoryCaptureTransactionPlan plan);
    }

    public interface ITerritoryEconomyApplyTarget
    {
        TerritoryApplyStepStatus Apply(TerritoryEconomyCommand command);
        bool Rollback(TerritoryEconomyCommand command);
    }

    public interface ITerritoryQuestApplyTarget
    {
        TerritoryApplyStepStatus Apply(TerritoryQuestCommand command);
        bool Rollback(TerritoryQuestCommand command);
    }

    public static class TerritorySemanticHasher
    {
        public static string HashFrames(params string[] frames)
        {
            string[] safeFrames = frames ?? Array.Empty<string>();
            if (safeFrames.Length > TerritoryTechnicalLimits.MaximumHashFrames)
            {
                throw new ArgumentOutOfRangeException(nameof(frames));
            }

            using (SHA256 algorithm = SHA256.Create())
            using (var hashingStream = new CryptoStream(Stream.Null, algorithm, CryptoStreamMode.Write))
            {
                using (var writer = new BinaryWriter(hashingStream, Encoding.UTF8, true))
                {
                    writer.Write(safeFrames.Length);
                    foreach (string frame in safeFrames)
                    {
                        string safeFrame = frame ?? string.Empty;
                        int byteCount = Encoding.UTF8.GetByteCount(safeFrame);
                        if (byteCount > TerritoryTechnicalLimits.MaximumHashFrameUtf8Bytes)
                        {
                            writer.Write(-1);
                            writer.Write(byteCount);
                            byte[] digest = HashOversizedUtf8Frame(safeFrame);
                            writer.Write(digest.Length);
                            writer.Write(digest);
                            continue;
                        }

                        byte[] bytes = Encoding.UTF8.GetBytes(safeFrame);
                        writer.Write(bytes.Length);
                        writer.Write(bytes);
                    }

                    writer.Flush();
                }

                hashingStream.FlushFinalBlock();
                return ToLowerHex(algorithm.Hash);
            }
        }

        private static byte[] HashOversizedUtf8Frame(string value)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                Encoder encoder = Encoding.UTF8.GetEncoder();
                var characters = new char[1024];
                var bytes = new byte[Encoding.UTF8.GetMaxByteCount(characters.Length)];
                int offset = 0;
                while (offset < value.Length)
                {
                    int characterCount = Math.Min(characters.Length, value.Length - offset);
                    value.CopyTo(offset, characters, 0, characterCount);
                    offset += characterCount;
                    int consumed = 0;
                    bool flush = offset == value.Length;
                    while (consumed < characterCount)
                    {
                        encoder.Convert(
                            characters,
                            consumed,
                            characterCount - consumed,
                            bytes,
                            0,
                            bytes.Length,
                            flush,
                            out int charactersUsed,
                            out int bytesUsed,
                            out bool completed);
                        if (bytesUsed > 0)
                        {
                            algorithm.TransformBlock(bytes, 0, bytesUsed, bytes, 0);
                        }

                        consumed += charactersUsed;
                        if (completed)
                        {
                            break;
                        }
                    }
                }

                algorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return algorithm.Hash;
            }
        }

        public static string HashStates(IEnumerable<TerritoryStateRecord> states)
        {
            bool exceeded;
            List<TerritoryStateRecord> rows = TerritoryPhaseBCollections.TakeBounded(
                states,
                TerritoryTechnicalLimits.MaximumMigrationRows + 1,
                out exceeded);
            var frames = new List<string> { "territory-states-v1", exceeded ? "exceeded" : "bounded" };
            foreach (TerritoryStateRecord row in rows
                         .OrderBy(item => item == null ? string.Empty : item.Id, StringComparer.Ordinal)
                         .ThenBy(item => item == null ? int.MinValue : (int)item.Owner)
                         .ThenBy(item => item == null ? long.MinValue : item.Revision))
            {
                if (row == null)
                {
                    frames.Add("null");
                    continue;
                }

                frames.Add("row");
                frames.Add(row.Id);
                frames.Add(((int)row.Owner).ToString(CultureInfo.InvariantCulture));
                frames.Add(row.Revision.ToString(CultureInfo.InvariantCulture));
            }

            return HashFrames(frames.ToArray());
        }

        public static string HashQueryStates(IEnumerable<TerritorySnapshot> snapshots)
        {
            bool exceeded;
            List<TerritorySnapshot> rows = TerritoryPhaseBCollections.TakeBounded(
                snapshots,
                TerritoryTechnicalLimits.MaximumStateRows + 1,
                out exceeded);
            var frames = new List<string> { "territory-query-states-v1", exceeded ? "exceeded" : "bounded" };
            foreach (string digest in rows
                         .Select(snapshot =>
                         {
                             if (snapshot == null || snapshot.State == null)
                             {
                                 return HashFrames("query-row", "null");
                             }

                             return HashFrames(
                                 "query-row",
                                 snapshot.State.Id,
                                 ((int)snapshot.State.Owner).ToString(CultureInfo.InvariantCulture),
                                 snapshot.State.Revision.ToString(CultureInfo.InvariantCulture),
                                 snapshot.IsSupported ? "supported" : "unsupported");
                         })
                         .OrderBy(item => item, StringComparer.Ordinal))
            {
                frames.Add(digest);
            }

            return HashFrames(frames.ToArray());
        }

        public static string HashCatalogPayload(
            IEnumerable<TerritoryDefinition> definitions,
            IEnumerable<TerritoryCaptureRewardProfile> rewards,
            IEnumerable<TerritoryAliasDefinition> aliases)
        {
            bool definitionsExceeded;
            bool rewardsExceeded;
            bool aliasesExceeded;
            List<TerritoryDefinition> definitionRows = TerritoryPhaseBCollections.TakeBounded(
                definitions,
                TerritoryTechnicalLimits.MaximumDefinitions + 1,
                out definitionsExceeded);
            List<TerritoryCaptureRewardProfile> rewardRows = TerritoryPhaseBCollections.TakeBounded(
                rewards,
                TerritoryTechnicalLimits.MaximumRewardProfiles + 1,
                out rewardsExceeded);
            List<TerritoryAliasDefinition> aliasRows = TerritoryPhaseBCollections.TakeBounded(
                aliases,
                TerritoryTechnicalLimits.MaximumAliases + 1,
                out aliasesExceeded);
            var frames = new List<string>
            {
                "territory-catalog-v1",
                definitionsExceeded || definitionRows.Count > TerritoryTechnicalLimits.MaximumDefinitions
                    ? "definitions-exceeded"
                    : "definitions-bounded",
                rewardsExceeded || rewardRows.Count > TerritoryTechnicalLimits.MaximumRewardProfiles
                    ? "rewards-exceeded"
                    : "rewards-bounded",
                aliasesExceeded || aliasRows.Count > TerritoryTechnicalLimits.MaximumAliases
                    ? "aliases-exceeded"
                    : "aliases-bounded"
            };

            foreach (string digest in definitionRows
                         .Select(HashDefinitionRow)
                         .OrderBy(item => item, StringComparer.Ordinal))
            {
                frames.Add(digest);
            }

            foreach (string digest in rewardRows
                         .Select(HashRewardRow)
                         .OrderBy(item => item, StringComparer.Ordinal))
            {
                frames.Add(digest);
            }

            foreach (string digest in aliasRows
                         .Select(HashAliasRow)
                         .OrderBy(item => item, StringComparer.Ordinal))
            {
                frames.Add(digest);
            }

            return HashFrames(frames.ToArray());
        }

        public static bool IsLowerSha256(string value)
        {
            return value != null &&
                   value.Length == 64 &&
                   value.All(character =>
                       (character >= '0' && character <= '9') ||
                       (character >= 'a' && character <= 'f'));
        }

        private static void AddSorted(List<string> frames, IEnumerable<string> values)
        {
            string[] sorted = (values ?? Array.Empty<string>())
                .Select(item => item ?? string.Empty)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            frames.Add(sorted.Length.ToString(CultureInfo.InvariantCulture));
            frames.AddRange(sorted);
        }

        private static string HashDefinitionRow(TerritoryDefinition definition)
        {
            if (definition == null)
            {
                return HashFrames("definition", "null");
            }

            var frames = new List<string>
            {
                "definition",
                NormalizeCatalogFrame(definition.Id),
                NormalizeCatalogFrame(definition.ContentKey),
                ((int)definition.InitialOwner).ToString(CultureInfo.InvariantCulture),
                ((int)definition.BonusType).ToString(CultureInfo.InvariantCulture),
                definition.BonusAmount.ToString(CultureInfo.InvariantCulture),
                definition.IsFortress ? "1" : "0",
                definition.AllowsNeutralOwnership ? "1" : "0",
                NormalizeCatalogFrame(definition.CaptureRewardProfileId)
            };
            AddSorted(
                frames,
                definition.AllowedOwners.Select(item => ((int)item).ToString(CultureInfo.InvariantCulture)));
            AddSorted(frames, definition.PrerequisiteIds.Select(NormalizeCatalogFrame));
            AddSorted(frames, definition.RequiredCapabilityIds.Select(NormalizeCatalogFrame));
            return HashFrames(frames.ToArray());
        }

        private static string HashRewardRow(TerritoryCaptureRewardProfile reward)
        {
            return reward == null
                ? HashFrames("reward", "null")
                : HashFrames(
                    "reward",
                    NormalizeCatalogFrame(reward.RewardProfileId),
                    reward.WarzoneCredits.ToString(CultureInfo.InvariantCulture),
                    NormalizeCatalogFrame(reward.QuestProgressType),
                    reward.QuestProgressDelta.ToString(CultureInfo.InvariantCulture));
        }

        private static string HashAliasRow(TerritoryAliasDefinition alias)
        {
            return alias == null
                ? HashFrames("alias", "null")
                : HashFrames(
                    "alias",
                    NormalizeCatalogFrame(alias.OldTerritoryId),
                    NormalizeCatalogFrame(alias.NewTerritoryId),
                    alias.IntroducedInVersion.ToString(CultureInfo.InvariantCulture));
        }

        private static string NormalizeCatalogFrame(string value)
        {
            string safe = value ?? string.Empty;
            int utf8Bytes = Encoding.UTF8.GetByteCount(safe);
            return utf8Bytes <= TerritoryTechnicalLimits.MaximumHashFrameUtf8Bytes
                ? safe
                : "oversized-frame:" +
                  utf8Bytes.ToString(CultureInfo.InvariantCulture) +
                  ":" +
                  HashFrames(safe);
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }

    internal static class TerritoryPhaseBCollections
    {
        internal static ReadOnlyCollection<T> FreezeBounded<T>(IEnumerable<T> values, int maximumRetained)
        {
            bool ignored;
            return new ReadOnlyCollection<T>(TakeBounded(values, maximumRetained, out ignored));
        }

        internal static List<T> TakeBounded<T>(
            IEnumerable<T> values,
            int maximumRetained,
            out bool exceeded)
        {
            exceeded = false;
            var result = new List<T>(Math.Max(0, maximumRetained));
            if (values == null || maximumRetained <= 0)
            {
                return result;
            }

            using (IEnumerator<T> enumerator = values.GetEnumerator())
            {
                while (result.Count < maximumRetained && enumerator.MoveNext())
                {
                    result.Add(enumerator.Current);
                }

                exceeded = enumerator.MoveNext();
            }

            return result;
        }

        internal static ReadOnlyCollection<TerritoryDiagnostic> FreezeDiagnostics(
            IEnumerable<TerritoryDiagnostic> diagnostics)
        {
            bool candidatesExceeded;
            List<TerritoryDiagnostic> candidates = TakeBounded(
                diagnostics,
                TerritoryTechnicalLimits.MaximumDiagnosticCandidates + 1,
                out candidatesExceeded);
            if (candidatesExceeded ||
                candidates.Count > TerritoryTechnicalLimits.MaximumDiagnosticCandidates)
            {
                return new ReadOnlyCollection<TerritoryDiagnostic>(new List<TerritoryDiagnostic>
                {
                    new TerritoryDiagnostic(
                        TerritoryDiagnosticSeverity.Error,
                        "DiagnosticLimitExceeded",
                        string.Empty,
                        "Territory diagnostic candidates exceed the deterministic retention limit.")
                });
            }

            TerritoryDiagnostic[] canonical = candidates
                .Where(item => item != null)
                .GroupBy(
                    item => new
                    {
                        item.Severity,
                        item.Code,
                        item.TerritoryId,
                        item.Message
                    })
                .Select(group => group.First())
                .OrderBy(item => item.TerritoryId, StringComparer.Ordinal)
                .ThenBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.Severity)
                .ThenBy(item => item.Message, StringComparer.Ordinal)
                .ToArray();
            bool diagnosticsExceeded = canonical.Length > TerritoryTechnicalLimits.MaximumDiagnostics;
            if (!diagnosticsExceeded)
            {
                return new ReadOnlyCollection<TerritoryDiagnostic>(canonical.ToList());
            }

            var retained = canonical
                .Take(TerritoryTechnicalLimits.MaximumDiagnostics - 1)
                .ToList();
            retained.Add(new TerritoryDiagnostic(
                TerritoryDiagnosticSeverity.Error,
                "DiagnosticLimitExceeded",
                string.Empty,
                "Additional territory diagnostics were deterministically omitted."));
            return new ReadOnlyCollection<TerritoryDiagnostic>(retained
                .OrderBy(item => item.TerritoryId, StringComparer.Ordinal)
                .ThenBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.Severity)
                .ThenBy(item => item.Message, StringComparer.Ordinal)
                .ToList());
        }
    }
}
