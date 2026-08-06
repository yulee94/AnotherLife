using System;
using System.Collections.Generic;

namespace AL.ChampionMode.C1
{
    public enum CombatSkillLoadStatus
    {
        Uninitialized = 0,
        Loading = 1,
        Loaded = 2,
        MissingArtifact = 3,
        ReadFailure = 4,
        ParseFailure = 5,
        InvalidCatalogIdentity = 6,
        UnsupportedVersion = 7,
        InvalidLoadout = 8,
        InvalidSkill = 9,
        InvalidReference = 10,
        HashMismatch = 11,
        Cancelled = 12,
        Superseded = 13,
        DevelopmentFallbackLoaded = 14,
        Disposed = 15
    }

    public enum CombatSkillLoadSessionPlanStatus
    {
        Applied = 0,
        DuplicateExact = 1,
        CorrelationConflict = 2,
        RejectedInvalidState = 3,
        RejectedInvalidRequest = 4,
        RejectedInvalidCompletion = 5,
        RejectedInvalidCancellation = 6,
        RejectedInvalidDisposal = 7,
        RejectedWrongOwner = 8,
        RejectedWrongRequest = 9,
        RejectedWrongGeneration = 10,
        RejectedSuperseded = 11,
        RejectedCancelled = 12,
        RejectedDisposed = 13,
        RejectedTransition = 14,
        RejectedGenerationOverflow = 15,
        RejectedAuthoritativeFallback = 16,
        RejectedPublicationIdentity = 17
    }

    public enum CombatSkillLoadOperationKind
    {
        Begin = 0,
        Complete = 1,
        Cancel = 2,
        Dispose = 3
    }

    public sealed class CombatSkillLoadExpectedSkill
    {
        public CombatSkillLoadExpectedSkill(
            int slotIndex,
            CombatStableId skillDefinitionId,
            CombatContractVersion schemaVersion,
            CombatContractVersion contentVersion,
            CombatContractVersion sourceRevision,
            CombatSha256 trustedRawSha256)
        {
            SlotIndex = slotIndex;
            SkillDefinitionId = skillDefinitionId;
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            SourceRevision = sourceRevision;
            TrustedRawSha256 = trustedRawSha256;
        }

        public int SlotIndex { get; }
        public CombatStableId SkillDefinitionId { get; }
        public CombatContractVersion SchemaVersion { get; }
        public CombatContractVersion ContentVersion { get; }
        public CombatContractVersion SourceRevision { get; }
        public CombatSha256 TrustedRawSha256 { get; }
    }

    public sealed class CombatSkillLoadRequest
    {
        public CombatSkillLoadRequest(
            CombatStableId ownerId,
            CombatStableId requestId,
            long expectedPreviousGeneration,
            CombatEncounterMode mode,
            bool allowsDevelopmentFallback,
            CombatStableId expectedCatalogSetId,
            CombatStableId expectedLoadoutId,
            CombatStableId expectedChampionOrClassProfileId,
            CombatContractVersion expectedSchemaVersion,
            CombatContractVersion expectedContentVersion,
            CombatContractVersion expectedSourceRevision,
            CombatSha256 expectedLoadoutRawSha256,
            IList<CombatSkillLoadExpectedSkill> expectedSkillsInSlotOrder)
        {
            OwnerId = ownerId;
            RequestId = requestId;
            ExpectedPreviousGeneration = expectedPreviousGeneration;
            Mode = mode;
            AllowsDevelopmentFallback = allowsDevelopmentFallback;
            ExpectedCatalogSetId = expectedCatalogSetId;
            ExpectedLoadoutId = expectedLoadoutId;
            ExpectedChampionOrClassProfileId =
                expectedChampionOrClassProfileId;
            ExpectedSchemaVersion = expectedSchemaVersion;
            ExpectedContentVersion = expectedContentVersion;
            ExpectedSourceRevision = expectedSourceRevision;
            ExpectedLoadoutRawSha256 = expectedLoadoutRawSha256;
            ExpectedSkillsInSlotOrder = CombatImmutable.Freeze(
                expectedSkillsInSlotOrder ??
                    new CombatSkillLoadExpectedSkill[0],
                nameof(expectedSkillsInSlotOrder),
                CombatTechnicalLimits.MaximumLoadoutBindings);
        }

        public CombatStableId OwnerId { get; }
        public CombatStableId RequestId { get; }
        public long ExpectedPreviousGeneration { get; }
        public CombatEncounterMode Mode { get; }
        public bool AllowsDevelopmentFallback { get; }
        public CombatStableId ExpectedCatalogSetId { get; }
        public CombatStableId ExpectedLoadoutId { get; }
        public CombatStableId ExpectedChampionOrClassProfileId { get; }
        public CombatContractVersion ExpectedSchemaVersion { get; }
        public CombatContractVersion ExpectedContentVersion { get; }
        public CombatContractVersion ExpectedSourceRevision { get; }
        public CombatSha256 ExpectedLoadoutRawSha256 { get; }
        public IReadOnlyList<CombatSkillLoadExpectedSkill>
            ExpectedSkillsInSlotOrder { get; }
    }

    public sealed class CombatSkillLoadCompletion
    {
        private static readonly IReadOnlyList<CombatDiagnostic>
            EmptyDiagnostics = Array.AsReadOnly(new CombatDiagnostic[0]);

        public CombatSkillLoadCompletion(
            CombatStableId completionId,
            CombatStableId ownerId,
            CombatStableId requestId,
            long generation,
            CombatSkillLoadStatus outcome,
            CombatSkillLoadoutValidationResult loadoutValidation,
            CombatValidationResult failureValidation)
        {
            CompletionId = completionId;
            OwnerId = ownerId;
            RequestId = requestId;
            Generation = generation;
            Outcome = outcome;
            LoadoutValidation = loadoutValidation;
            FailureValidation = failureValidation;
        }

        public CombatStableId CompletionId { get; }
        public CombatStableId OwnerId { get; }
        public CombatStableId RequestId { get; }
        public long Generation { get; }
        public CombatSkillLoadStatus Outcome { get; }
        public CombatSkillLoadoutValidationResult LoadoutValidation { get; }
        public CombatValidationResult FailureValidation { get; }
        public IReadOnlyList<CombatDiagnostic> Diagnostics =>
            LoadoutValidation != null
                ? LoadoutValidation.Diagnostics
                : FailureValidation != null
                    ? FailureValidation.Diagnostics
                    : EmptyDiagnostics;
    }

    public sealed class CombatSkillLoadCancellation
    {
        public CombatSkillLoadCancellation(
            CombatStableId cancellationId,
            CombatStableId ownerId,
            CombatStableId requestId,
            long generation)
        {
            CancellationId = cancellationId;
            OwnerId = ownerId;
            RequestId = requestId;
            Generation = generation;
        }

        public CombatStableId CancellationId { get; }
        public CombatStableId OwnerId { get; }
        public CombatStableId RequestId { get; }
        public long Generation { get; }
    }

    public sealed class CombatSkillLoadDisposal
    {
        public CombatSkillLoadDisposal(
            CombatStableId disposalId,
            CombatStableId ownerId,
            long expectedGeneration)
        {
            DisposalId = disposalId;
            OwnerId = ownerId;
            ExpectedGeneration = expectedGeneration;
        }

        public CombatStableId DisposalId { get; }
        public CombatStableId OwnerId { get; }
        public long ExpectedGeneration { get; }
    }

    public sealed class CombatSkillLoadOperationReceipt
    {
        internal CombatSkillLoadOperationReceipt(
            CombatSkillLoadOperationKind operationKind,
            CombatStableId operationId,
            CombatStableId ownerId,
            CombatStableId requestId,
            long generation,
            CombatSkillLoadStatus beforeStatus,
            CombatSkillLoadStatus afterStatus,
            CombatSkillLoadRequest request,
            CombatSkillLoadCompletion completion,
            CombatSkillLoadCancellation cancellation,
            CombatSkillLoadDisposal disposal,
            CombatStableId supersededRequestId)
        {
            OperationKind = operationKind;
            OperationId = operationId;
            OwnerId = ownerId;
            RequestId = requestId;
            Generation = generation;
            BeforeStatus = beforeStatus;
            AfterStatus = afterStatus;
            Request = request;
            Completion = completion;
            Cancellation = cancellation;
            Disposal = disposal;
            SupersededRequestId = supersededRequestId;
        }

        public CombatSkillLoadOperationKind OperationKind { get; }
        public CombatStableId OperationId { get; }
        public CombatStableId OwnerId { get; }
        public CombatStableId RequestId { get; }
        public long Generation { get; }
        public CombatSkillLoadStatus BeforeStatus { get; }
        public CombatSkillLoadStatus AfterStatus { get; }
        public CombatSkillLoadRequest Request { get; }
        public CombatSkillLoadCompletion Completion { get; }
        public CombatSkillLoadCancellation Cancellation { get; }
        public CombatSkillLoadDisposal Disposal { get; }
        public CombatStableId SupersededRequestId { get; }
        public bool SupersededPreviousRequest =>
            !SupersededRequestId.IsDefault;
    }

    public sealed class CombatSkillLoadSessionSnapshot
    {
        internal CombatSkillLoadSessionSnapshot(
            CombatStableId ownerId,
            long generation,
            CombatSkillLoadStatus status,
            CombatSkillLoadRequest request,
            ValidatedCombatSkillLoadoutSnapshot publishedSnapshot,
            CombatValidationResult validation,
            CombatSkillLoadOperationReceipt latestReceipt)
        {
            OwnerId = ownerId;
            Generation = generation;
            Status = status;
            Request = request;
            PublishedSnapshot = publishedSnapshot;
            Validation = validation;
            LatestReceipt = latestReceipt;
        }

        public CombatStableId OwnerId { get; }
        public long Generation { get; }
        public CombatSkillLoadStatus Status { get; }
        public CombatSkillLoadRequest Request { get; }
        public ValidatedCombatSkillLoadoutSnapshot PublishedSnapshot { get; }
        public CombatValidationResult Validation { get; }
        public CombatSkillLoadOperationReceipt LatestReceipt { get; }
        public bool HasPublishedSnapshot => PublishedSnapshot != null;
        public bool IsAuthoritative => Status == CombatSkillLoadStatus.Loaded;
        public bool IsDevelopmentFallback =>
            Status == CombatSkillLoadStatus.DevelopmentFallbackLoaded;
        public bool IsDisposed => Status == CombatSkillLoadStatus.Disposed;
    }

    public sealed class CombatSkillLoadSessionPlan
    {
        internal CombatSkillLoadSessionPlan(
            CombatSkillLoadSessionPlanStatus status,
            CombatSkillLoadSessionSnapshot before,
            CombatSkillLoadSessionSnapshot after,
            CombatSkillLoadOperationReceipt receipt,
            CombatSkillLoadStatus observedLoadStatus)
        {
            Status = status;
            Before = before;
            After = after;
            Receipt = receipt;
            ObservedLoadStatus = observedLoadStatus;
        }

        public CombatSkillLoadSessionPlanStatus Status { get; }
        public CombatSkillLoadSessionSnapshot Before { get; }
        public CombatSkillLoadSessionSnapshot After { get; }
        public CombatSkillLoadOperationReceipt Receipt { get; }
        public CombatSkillLoadStatus ObservedLoadStatus { get; }
        public bool Applied =>
            Status == CombatSkillLoadSessionPlanStatus.Applied;
        public bool ExactReplay =>
            Status == CombatSkillLoadSessionPlanStatus.DuplicateExact;
    }

    public static class CombatSkillLoadSessionPlanner
    {
        private static readonly CombatValidationResult EmptyValidation =
            new CombatValidationResult(new CombatDiagnostic[0]);

        public static bool TryCreateInitial(
            CombatStableId ownerId,
            out CombatSkillLoadSessionSnapshot snapshot)
        {
            if (ownerId.IsDefault)
            {
                snapshot = null;
                return false;
            }

            snapshot = new CombatSkillLoadSessionSnapshot(
                ownerId,
                0L,
                CombatSkillLoadStatus.Uninitialized,
                null,
                null,
                EmptyValidation,
                null);
            return true;
        }

        public static CombatSkillLoadSessionPlan Begin(
            CombatSkillLoadSessionSnapshot current,
            CombatSkillLoadRequest request)
        {
            if (!IsCoherent(current))
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedInvalidState,
                    current);
            }
            if (current.IsDisposed)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedDisposed,
                    current);
            }
            if (!IsValidRequest(request))
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedInvalidRequest,
                    current);
            }
            if (request.OwnerId != current.OwnerId)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedWrongOwner,
                    current);
            }

            if (current.Request != null &&
                request.RequestId == current.Request.RequestId)
            {
                if (SameRequest(current.Request, request) &&
                    HasSameOperationId(
                        current,
                        CombatSkillLoadOperationKind.Begin,
                        request.RequestId) &&
                    current.Generation > 0L &&
                    request.ExpectedPreviousGeneration ==
                        current.Generation - 1L)
                {
                    return ExactReplay(current);
                }

                return Rejected(
                    CombatSkillLoadSessionPlanStatus.CorrelationConflict,
                    current);
            }

            if (current.LatestReceipt != null &&
                request.RequestId ==
                    current.LatestReceipt.OperationId)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.CorrelationConflict,
                    current);
            }

            if (request.ExpectedPreviousGeneration != current.Generation)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedWrongGeneration,
                    current);
            }
            if (current.Generation == long.MaxValue)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus
                        .RejectedGenerationOverflow,
                    current);
            }

            long nextGeneration = current.Generation + 1L;
            CombatStableId supersededRequestId =
                current.Status == CombatSkillLoadStatus.Loading &&
                current.Request != null
                    ? current.Request.RequestId
                    : default;
            var receipt = new CombatSkillLoadOperationReceipt(
                CombatSkillLoadOperationKind.Begin,
                request.RequestId,
                current.OwnerId,
                request.RequestId,
                nextGeneration,
                current.Status,
                CombatSkillLoadStatus.Loading,
                request,
                null,
                null,
                null,
                supersededRequestId);
            var after = new CombatSkillLoadSessionSnapshot(
                current.OwnerId,
                nextGeneration,
                CombatSkillLoadStatus.Loading,
                request,
                null,
                EmptyValidation,
                receipt);
            return Applied(current, after, receipt);
        }

        public static CombatSkillLoadSessionPlan Complete(
            CombatSkillLoadSessionSnapshot current,
            CombatSkillLoadCompletion completion)
        {
            if (!IsCoherent(current))
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedInvalidState,
                    current);
            }
            if (current.IsDisposed)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedDisposed,
                    current);
            }
            if (!IsValidCompletionEnvelope(completion))
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus
                        .RejectedInvalidCompletion,
                    current);
            }

            if (HasSameOperationId(
                    current,
                    CombatSkillLoadOperationKind.Complete,
                    completion.CompletionId))
            {
                return SameCompletion(
                        current.LatestReceipt.Completion,
                        completion)
                    ? ExactReplay(current)
                    : Rejected(
                        CombatSkillLoadSessionPlanStatus
                            .CorrelationConflict,
                        current);
            }
            if (UsesReservedOperationId(
                    current,
                    completion.CompletionId))
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.CorrelationConflict,
                    current);
            }
            if (!IsValidCompletionShape(completion))
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus
                        .RejectedInvalidCompletion,
                    current);
            }
            if (completion.OwnerId != current.OwnerId)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedWrongOwner,
                    current);
            }
            if (completion.Generation < current.Generation)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedSuperseded,
                    current,
                    CombatSkillLoadStatus.Superseded);
            }
            if (completion.Generation > current.Generation)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus
                        .RejectedWrongGeneration,
                    current);
            }
            if (current.Request == null ||
                completion.RequestId != current.Request.RequestId)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedWrongRequest,
                    current);
            }
            if (current.Status == CombatSkillLoadStatus.Cancelled)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedCancelled,
                    current,
                    CombatSkillLoadStatus.Cancelled);
            }
            if (current.Status != CombatSkillLoadStatus.Loading)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedTransition,
                    current);
            }

            bool fallback = completion.Outcome ==
                CombatSkillLoadStatus.DevelopmentFallbackLoaded;
            if (fallback &&
                (!current.Request.AllowsDevelopmentFallback ||
                 current.Request.Mode ==
                    CombatEncounterMode.AuthoritativeBoss ||
                 current.Request.Mode ==
                    CombatEncounterMode.AuthoritativeQuest))
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus
                        .RejectedAuthoritativeFallback,
                    current);
            }

            if (IsPublishedOutcome(completion.Outcome) &&
                !PublicationMatches(
                    current.Request,
                    completion.LoadoutValidation.Snapshot))
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus
                        .RejectedPublicationIdentity,
                    current);
            }

            ValidatedCombatSkillLoadoutSnapshot publishedSnapshot =
                IsPublishedOutcome(completion.Outcome)
                    ? completion.LoadoutValidation.Snapshot
                    : null;
            CombatValidationResult validation =
                IsPublishedOutcome(completion.Outcome)
                    ? completion.LoadoutValidation.Validation
                    : completion.FailureValidation;
            var receipt = new CombatSkillLoadOperationReceipt(
                CombatSkillLoadOperationKind.Complete,
                completion.CompletionId,
                current.OwnerId,
                current.Request.RequestId,
                current.Generation,
                current.Status,
                completion.Outcome,
                current.Request,
                completion,
                null,
                null,
                default);
            var after = new CombatSkillLoadSessionSnapshot(
                current.OwnerId,
                current.Generation,
                completion.Outcome,
                current.Request,
                publishedSnapshot,
                validation,
                receipt);
            return Applied(current, after, receipt);
        }

        public static CombatSkillLoadSessionPlan Cancel(
            CombatSkillLoadSessionSnapshot current,
            CombatSkillLoadCancellation cancellation)
        {
            if (!IsCoherent(current))
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedInvalidState,
                    current);
            }
            if (current.IsDisposed)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedDisposed,
                    current);
            }
            if (!IsValidCancellation(cancellation))
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus
                        .RejectedInvalidCancellation,
                    current);
            }

            if (HasSameOperationId(
                    current,
                    CombatSkillLoadOperationKind.Cancel,
                    cancellation.CancellationId))
            {
                return SameCancellation(
                        current.LatestReceipt.Cancellation,
                        cancellation)
                    ? ExactReplay(current)
                    : Rejected(
                        CombatSkillLoadSessionPlanStatus
                            .CorrelationConflict,
                        current);
            }
            if (UsesReservedOperationId(
                    current,
                    cancellation.CancellationId))
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.CorrelationConflict,
                    current);
            }
            if (cancellation.OwnerId != current.OwnerId)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedWrongOwner,
                    current);
            }
            if (cancellation.Generation < current.Generation)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedSuperseded,
                    current,
                    CombatSkillLoadStatus.Superseded);
            }
            if (cancellation.Generation > current.Generation)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus
                        .RejectedWrongGeneration,
                    current);
            }
            if (current.Request == null ||
                cancellation.RequestId != current.Request.RequestId)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedWrongRequest,
                    current);
            }
            if (current.Status == CombatSkillLoadStatus.Cancelled)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedCancelled,
                    current,
                    CombatSkillLoadStatus.Cancelled);
            }
            if (current.Status != CombatSkillLoadStatus.Loading)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedTransition,
                    current);
            }

            var receipt = new CombatSkillLoadOperationReceipt(
                CombatSkillLoadOperationKind.Cancel,
                cancellation.CancellationId,
                current.OwnerId,
                current.Request.RequestId,
                current.Generation,
                current.Status,
                CombatSkillLoadStatus.Cancelled,
                current.Request,
                null,
                cancellation,
                null,
                default);
            var after = new CombatSkillLoadSessionSnapshot(
                current.OwnerId,
                current.Generation,
                CombatSkillLoadStatus.Cancelled,
                current.Request,
                null,
                EmptyValidation,
                receipt);
            return Applied(current, after, receipt);
        }

        public static CombatSkillLoadSessionPlan Dispose(
            CombatSkillLoadSessionSnapshot current,
            CombatSkillLoadDisposal disposal)
        {
            if (!IsCoherent(current))
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedInvalidState,
                    current);
            }
            if (!IsValidDisposal(disposal))
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus
                        .RejectedInvalidDisposal,
                    current);
            }

            if (HasSameOperationId(
                    current,
                    CombatSkillLoadOperationKind.Dispose,
                    disposal.DisposalId))
            {
                return SameDisposal(
                        current.LatestReceipt.Disposal,
                        disposal)
                    ? ExactReplay(current)
                    : Rejected(
                        CombatSkillLoadSessionPlanStatus
                            .CorrelationConflict,
                        current);
            }
            if (UsesReservedOperationId(current, disposal.DisposalId))
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.CorrelationConflict,
                    current);
            }
            if (current.IsDisposed)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedDisposed,
                    current);
            }
            if (disposal.OwnerId != current.OwnerId)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus.RejectedWrongOwner,
                    current);
            }
            if (disposal.ExpectedGeneration != current.Generation)
            {
                return Rejected(
                    CombatSkillLoadSessionPlanStatus
                        .RejectedWrongGeneration,
                    current);
            }

            CombatStableId requestId = current.Request != null
                ? current.Request.RequestId
                : default;
            var receipt = new CombatSkillLoadOperationReceipt(
                CombatSkillLoadOperationKind.Dispose,
                disposal.DisposalId,
                current.OwnerId,
                requestId,
                current.Generation,
                current.Status,
                CombatSkillLoadStatus.Disposed,
                current.Request,
                null,
                null,
                disposal,
                default);
            var after = new CombatSkillLoadSessionSnapshot(
                current.OwnerId,
                current.Generation,
                CombatSkillLoadStatus.Disposed,
                current.Request,
                null,
                EmptyValidation,
                receipt);
            return Applied(current, after, receipt);
        }

        private static CombatSkillLoadSessionPlan Applied(
            CombatSkillLoadSessionSnapshot before,
            CombatSkillLoadSessionSnapshot after,
            CombatSkillLoadOperationReceipt receipt)
        {
            return new CombatSkillLoadSessionPlan(
                CombatSkillLoadSessionPlanStatus.Applied,
                before,
                after,
                receipt,
                after.Status);
        }

        private static CombatSkillLoadSessionPlan ExactReplay(
            CombatSkillLoadSessionSnapshot current)
        {
            return new CombatSkillLoadSessionPlan(
                CombatSkillLoadSessionPlanStatus.DuplicateExact,
                current,
                current,
                current.LatestReceipt,
                current.Status);
        }

        private static bool IsCoherent(
            CombatSkillLoadSessionSnapshot current)
        {
            if (current == null ||
                current.OwnerId.IsDefault ||
                current.Generation < 0L ||
                current.Validation == null ||
                !IsKnownLoadStatus(current.Status))
            {
                return false;
            }

            switch (current.Status)
            {
                case CombatSkillLoadStatus.Uninitialized:
                    return current.Generation == 0L &&
                        current.Request == null &&
                        current.PublishedSnapshot == null &&
                        current.Validation.IsValid;
                case CombatSkillLoadStatus.Loading:
                    return HasCurrentRequest(current) &&
                        current.PublishedSnapshot == null &&
                        current.Validation.IsValid;
                case CombatSkillLoadStatus.Loaded:
                    return HasCurrentRequest(current) &&
                        current.PublishedSnapshot != null &&
                        current.Validation.IsValid &&
                        PublicationMatches(
                            current.Request,
                            current.PublishedSnapshot);
                case CombatSkillLoadStatus.DevelopmentFallbackLoaded:
                    return HasCurrentRequest(current) &&
                        current.PublishedSnapshot != null &&
                        current.Validation.IsValid &&
                        current.Request.AllowsDevelopmentFallback &&
                        current.Request.Mode !=
                            CombatEncounterMode.AuthoritativeBoss &&
                        current.Request.Mode !=
                            CombatEncounterMode.AuthoritativeQuest &&
                        PublicationMatches(
                            current.Request,
                            current.PublishedSnapshot);
                case CombatSkillLoadStatus.MissingArtifact:
                case CombatSkillLoadStatus.ReadFailure:
                case CombatSkillLoadStatus.ParseFailure:
                case CombatSkillLoadStatus.InvalidCatalogIdentity:
                case CombatSkillLoadStatus.UnsupportedVersion:
                case CombatSkillLoadStatus.InvalidLoadout:
                case CombatSkillLoadStatus.InvalidSkill:
                case CombatSkillLoadStatus.InvalidReference:
                case CombatSkillLoadStatus.HashMismatch:
                    return HasCurrentRequest(current) &&
                        current.PublishedSnapshot == null &&
                        !current.Validation.IsValid;
                case CombatSkillLoadStatus.Cancelled:
                case CombatSkillLoadStatus.Superseded:
                    return HasCurrentRequest(current) &&
                        current.PublishedSnapshot == null;
                case CombatSkillLoadStatus.Disposed:
                    return current.PublishedSnapshot == null &&
                        (current.Request == null
                            ? current.Generation == 0L
                            : HasCurrentRequest(current));
                default:
                    return false;
            }
        }

        private static bool HasCurrentRequest(
            CombatSkillLoadSessionSnapshot current)
        {
            return current.Generation > 0L &&
                IsValidRequest(current.Request) &&
                current.Request.OwnerId == current.OwnerId &&
                current.Request.ExpectedPreviousGeneration ==
                    current.Generation - 1L;
        }

        private static bool IsValidRequest(CombatSkillLoadRequest request)
        {
            if (request == null ||
                request.OwnerId.IsDefault ||
                request.RequestId.IsDefault ||
                request.ExpectedPreviousGeneration < 0L ||
                !IsKnownMode(request.Mode) ||
                request.ExpectedCatalogSetId.IsDefault ||
                request.ExpectedLoadoutId.IsDefault ||
                request.ExpectedChampionOrClassProfileId.IsDefault ||
                request.ExpectedSchemaVersion.IsDefault ||
                request.ExpectedContentVersion.IsDefault ||
                request.ExpectedSourceRevision.IsDefault ||
                request.ExpectedLoadoutRawSha256.IsDefault ||
                request.ExpectedSkillsInSlotOrder == null ||
                request.ExpectedSkillsInSlotOrder.Count !=
                    CombatSkillLoadout.RequiredSlotCount)
            {
                return false;
            }

            for (int index = 0;
                index < request.ExpectedSkillsInSlotOrder.Count;
                index++)
            {
                CombatSkillLoadExpectedSkill expected =
                    request.ExpectedSkillsInSlotOrder[index];
                if (expected == null ||
                    expected.SlotIndex != index ||
                    expected.SkillDefinitionId.IsDefault ||
                    expected.SchemaVersion.IsDefault ||
                    expected.ContentVersion.IsDefault ||
                    expected.SourceRevision.IsDefault ||
                    expected.TrustedRawSha256.IsDefault)
                {
                    return false;
                }

                for (int earlier = 0; earlier < index; earlier++)
                {
                    if (request.ExpectedSkillsInSlotOrder[earlier]
                            .SkillDefinitionId ==
                        expected.SkillDefinitionId)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsValidCompletionEnvelope(
            CombatSkillLoadCompletion completion)
        {
            return completion != null &&
                !completion.CompletionId.IsDefault &&
                !completion.OwnerId.IsDefault &&
                !completion.RequestId.IsDefault &&
                completion.Generation > 0L &&
                IsKnownLoadStatus(completion.Outcome);
        }

        private static bool IsValidCompletionShape(
            CombatSkillLoadCompletion completion)
        {
            if (IsPublishedOutcome(completion.Outcome))
            {
                return completion.LoadoutValidation != null &&
                    completion.LoadoutValidation.IsValid &&
                    completion.LoadoutValidation.Snapshot != null &&
                    completion.FailureValidation == null;
            }

            if (IsFailureOutcome(completion.Outcome))
            {
                return completion.LoadoutValidation == null &&
                    completion.FailureValidation != null &&
                    !completion.FailureValidation.IsValid;
            }

            if (completion.Outcome == CombatSkillLoadStatus.Cancelled ||
                completion.Outcome == CombatSkillLoadStatus.Superseded)
            {
                return completion.LoadoutValidation == null &&
                    completion.FailureValidation != null;
            }

            return false;
        }

        private static bool IsValidCancellation(
            CombatSkillLoadCancellation cancellation)
        {
            return cancellation != null &&
                !cancellation.CancellationId.IsDefault &&
                !cancellation.OwnerId.IsDefault &&
                !cancellation.RequestId.IsDefault &&
                cancellation.Generation > 0L;
        }

        private static bool IsValidDisposal(
            CombatSkillLoadDisposal disposal)
        {
            return disposal != null &&
                !disposal.DisposalId.IsDefault &&
                !disposal.OwnerId.IsDefault &&
                disposal.ExpectedGeneration >= 0L;
        }

        private static bool HasSameOperationId(
            CombatSkillLoadSessionSnapshot current,
            CombatSkillLoadOperationKind kind,
            CombatStableId operationId)
        {
            return current.LatestReceipt != null &&
                current.LatestReceipt.OperationKind == kind &&
                current.LatestReceipt.OperationId == operationId;
        }

        private static bool UsesReservedOperationId(
            CombatSkillLoadSessionSnapshot current,
            CombatStableId operationId)
        {
            return current.Request != null &&
                    current.Request.RequestId == operationId ||
                current.LatestReceipt != null &&
                    current.LatestReceipt.OperationId == operationId;
        }

        private static bool SameRequest(
            CombatSkillLoadRequest left,
            CombatSkillLoadRequest right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }
            if (left == null || right == null ||
                left.OwnerId != right.OwnerId ||
                left.RequestId != right.RequestId ||
                left.ExpectedPreviousGeneration !=
                    right.ExpectedPreviousGeneration ||
                left.Mode != right.Mode ||
                left.AllowsDevelopmentFallback !=
                    right.AllowsDevelopmentFallback ||
                left.ExpectedCatalogSetId != right.ExpectedCatalogSetId ||
                left.ExpectedLoadoutId != right.ExpectedLoadoutId ||
                left.ExpectedChampionOrClassProfileId !=
                    right.ExpectedChampionOrClassProfileId ||
                !left.ExpectedSchemaVersion.Equals(
                    right.ExpectedSchemaVersion) ||
                !left.ExpectedContentVersion.Equals(
                    right.ExpectedContentVersion) ||
                !left.ExpectedSourceRevision.Equals(
                    right.ExpectedSourceRevision) ||
                !left.ExpectedLoadoutRawSha256.Equals(
                    right.ExpectedLoadoutRawSha256) ||
                left.ExpectedSkillsInSlotOrder.Count !=
                    right.ExpectedSkillsInSlotOrder.Count)
            {
                return false;
            }

            for (int index = 0;
                index < left.ExpectedSkillsInSlotOrder.Count;
                index++)
            {
                CombatSkillLoadExpectedSkill leftSkill =
                    left.ExpectedSkillsInSlotOrder[index];
                CombatSkillLoadExpectedSkill rightSkill =
                    right.ExpectedSkillsInSlotOrder[index];
                if (leftSkill.SlotIndex != rightSkill.SlotIndex ||
                    leftSkill.SkillDefinitionId !=
                        rightSkill.SkillDefinitionId ||
                    !leftSkill.SchemaVersion.Equals(
                        rightSkill.SchemaVersion) ||
                    !leftSkill.ContentVersion.Equals(
                        rightSkill.ContentVersion) ||
                    !leftSkill.SourceRevision.Equals(
                        rightSkill.SourceRevision) ||
                    !leftSkill.TrustedRawSha256.Equals(
                        rightSkill.TrustedRawSha256))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SameCompletion(
            CombatSkillLoadCompletion left,
            CombatSkillLoadCompletion right)
        {
            return ReferenceEquals(left, right) ||
                left != null && right != null &&
                left.CompletionId == right.CompletionId &&
                left.OwnerId == right.OwnerId &&
                left.RequestId == right.RequestId &&
                left.Generation == right.Generation &&
                left.Outcome == right.Outcome &&
                SameLoadoutValidation(
                    left.LoadoutValidation,
                    right.LoadoutValidation) &&
                SameValidation(
                    left.FailureValidation,
                    right.FailureValidation);
        }

        private static bool SameLoadoutValidation(
            CombatSkillLoadoutValidationResult left,
            CombatSkillLoadoutValidationResult right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }
            if (left == null || right == null ||
                !SameValidation(left.Validation, right.Validation))
            {
                return false;
            }
            if (ReferenceEquals(left.Snapshot, right.Snapshot))
            {
                return true;
            }
            if (left.Snapshot == null || right.Snapshot == null)
            {
                return false;
            }

            return SamePublishedIdentity(left.Snapshot, right.Snapshot);
        }

        private static bool SamePublishedIdentity(
            ValidatedCombatSkillLoadoutSnapshot left,
            ValidatedCombatSkillLoadoutSnapshot right)
        {
            if (left == null || right == null ||
                left.Loadout == null || right.Loadout == null ||
                left.SkillsInSlotOrder == null ||
                right.SkillsInSlotOrder == null ||
                left.TrustedSkillRawSha256InSlotOrder == null ||
                right.TrustedSkillRawSha256InSlotOrder == null ||
                left.SkillsInSlotOrder.Count !=
                    right.SkillsInSlotOrder.Count ||
                left.TrustedSkillRawSha256InSlotOrder.Count !=
                    right.TrustedSkillRawSha256InSlotOrder.Count ||
                left.Loadout.Slots.Count != right.Loadout.Slots.Count ||
                !OrdinalEquals(left.CatalogSetId, right.CatalogSetId) ||
                !OrdinalEquals(left.Loadout.Id, right.Loadout.Id) ||
                !OrdinalEquals(
                    left.Loadout.ChampionOrClassProfileId,
                    right.Loadout.ChampionOrClassProfileId) ||
                !OrdinalEquals(
                    left.Loadout.SchemaVersion,
                    right.Loadout.SchemaVersion) ||
                !OrdinalEquals(
                    left.Loadout.ContentVersion,
                    right.Loadout.ContentVersion) ||
                !OrdinalEquals(
                    left.Loadout.SourceRevision,
                    right.Loadout.SourceRevision) ||
                !OrdinalEquals(
                    left.Loadout.RawSha256,
                    right.Loadout.RawSha256) ||
                !OrdinalEquals(
                    left.TrustedLoadoutRawSha256,
                    right.TrustedLoadoutRawSha256))
            {
                return false;
            }

            for (int slotIndex = 0;
                slotIndex < left.SkillsInSlotOrder.Count;
                slotIndex++)
            {
                CombatSkillDefinition leftSkill =
                    left.SkillsInSlotOrder[slotIndex];
                CombatSkillDefinition rightSkill =
                    right.SkillsInSlotOrder[slotIndex];
                CombatSkillSlotBinding leftBinding =
                    FindBinding(left.Loadout, slotIndex);
                CombatSkillSlotBinding rightBinding =
                    FindBinding(right.Loadout, slotIndex);
                if (leftSkill == null || rightSkill == null ||
                    leftBinding == null || rightBinding == null ||
                    !SameSkillIdentity(leftSkill, rightSkill) ||
                    !OrdinalEquals(
                        leftBinding.SkillDefinitionId,
                        rightBinding.SkillDefinitionId) ||
                    !OrdinalEquals(
                        leftBinding.SkillContentVersion,
                        rightBinding.SkillContentVersion) ||
                    !OrdinalEquals(
                        left.TrustedSkillRawSha256InSlotOrder[slotIndex],
                        right.TrustedSkillRawSha256InSlotOrder[slotIndex]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SameSkillIdentity(
            CombatSkillDefinition left,
            CombatSkillDefinition right)
        {
            return OrdinalEquals(left.Id, right.Id) &&
                OrdinalEquals(left.SchemaVersion, right.SchemaVersion) &&
                OrdinalEquals(left.ContentVersion, right.ContentVersion) &&
                OrdinalEquals(left.SourceRevision, right.SourceRevision) &&
                OrdinalEquals(left.RawSha256, right.RawSha256);
        }

        private static bool SameValidation(
            CombatValidationResult left,
            CombatValidationResult right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }
            if (left == null || right == null ||
                left.Diagnostics == null || right.Diagnostics == null ||
                left.Diagnostics.Count != right.Diagnostics.Count)
            {
                return false;
            }

            for (int index = 0; index < left.Diagnostics.Count; index++)
            {
                if (!SameDiagnostic(
                    left.Diagnostics[index],
                    right.Diagnostics[index]))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool SameDiagnostic(
            CombatDiagnostic left,
            CombatDiagnostic right)
        {
            return ReferenceEquals(left, right) ||
                left != null && right != null &&
                OrdinalEquals(left.Code, right.Code) &&
                left.Severity == right.Severity &&
                left.Domain == right.Domain &&
                OrdinalEquals(
                    left.SourceDefinitionId,
                    right.SourceDefinitionId) &&
                OrdinalEquals(
                    left.EncounterSessionId,
                    right.EncounterSessionId) &&
                OrdinalEquals(
                    left.EncounterAttemptId,
                    right.EncounterAttemptId) &&
                OrdinalEquals(left.ActionId, right.ActionId) &&
                OrdinalEquals(left.ParticipantId, right.ParticipantId) &&
                OrdinalEquals(left.FieldPath, right.FieldPath) &&
                OrdinalEquals(left.SchemaVersion, right.SchemaVersion) &&
                OrdinalEquals(left.ContentVersion, right.ContentVersion) &&
                OrdinalEquals(left.PolicyVersion, right.PolicyVersion) &&
                left.BlockScope == right.BlockScope &&
                OrdinalEquals(left.Message, right.Message);
        }

        private static bool SameCancellation(
            CombatSkillLoadCancellation left,
            CombatSkillLoadCancellation right)
        {
            return ReferenceEquals(left, right) ||
                left != null && right != null &&
                left.CancellationId == right.CancellationId &&
                left.OwnerId == right.OwnerId &&
                left.RequestId == right.RequestId &&
                left.Generation == right.Generation;
        }

        private static bool SameDisposal(
            CombatSkillLoadDisposal left,
            CombatSkillLoadDisposal right)
        {
            return ReferenceEquals(left, right) ||
                left != null && right != null &&
                left.DisposalId == right.DisposalId &&
                left.OwnerId == right.OwnerId &&
                left.ExpectedGeneration == right.ExpectedGeneration;
        }

        private static bool PublicationMatches(
            CombatSkillLoadRequest request,
            ValidatedCombatSkillLoadoutSnapshot snapshot)
        {
            if (!IsValidRequest(request) ||
                snapshot == null ||
                snapshot.Loadout == null ||
                snapshot.References == null ||
                snapshot.SkillsInSlotOrder == null ||
                snapshot.TrustedSkillRawSha256InSlotOrder == null ||
                snapshot.SkillsInSlotOrder.Count !=
                    CombatSkillLoadout.RequiredSlotCount ||
                snapshot.TrustedSkillRawSha256InSlotOrder.Count !=
                    CombatSkillLoadout.RequiredSlotCount ||
                snapshot.Loadout.Slots == null ||
                snapshot.Loadout.Slots.Count !=
                    CombatSkillLoadout.RequiredSlotCount ||
                !OrdinalEquals(
                    request.ExpectedCatalogSetId.Value,
                    snapshot.CatalogSetId) ||
                !OrdinalEquals(
                    request.ExpectedLoadoutId.Value,
                    snapshot.Loadout.Id) ||
                !OrdinalEquals(
                    request.ExpectedChampionOrClassProfileId.Value,
                    snapshot.Loadout.ChampionOrClassProfileId) ||
                !OrdinalEquals(
                    request.ExpectedSchemaVersion.Value,
                    snapshot.Loadout.SchemaVersion) ||
                !OrdinalEquals(
                    request.ExpectedContentVersion.Value,
                    snapshot.Loadout.ContentVersion) ||
                !OrdinalEquals(
                    request.ExpectedSourceRevision.Value,
                    snapshot.Loadout.SourceRevision) ||
                !OrdinalEquals(
                    request.ExpectedLoadoutRawSha256.Value,
                    snapshot.TrustedLoadoutRawSha256) ||
                !OrdinalEquals(
                    request.ExpectedLoadoutRawSha256.Value,
                    snapshot.Loadout.RawSha256))
            {
                return false;
            }

            for (int slotIndex = 0;
                slotIndex < CombatSkillLoadout.RequiredSlotCount;
                slotIndex++)
            {
                CombatSkillLoadExpectedSkill expected =
                    request.ExpectedSkillsInSlotOrder[slotIndex];
                CombatSkillDefinition skill =
                    snapshot.SkillsInSlotOrder[slotIndex];
                CombatSkillSlotBinding binding = FindBinding(
                    snapshot.Loadout,
                    slotIndex);
                string trustedHash =
                    snapshot.TrustedSkillRawSha256InSlotOrder[slotIndex];
                if (expected == null ||
                    skill == null ||
                    binding == null ||
                    expected.SlotIndex != slotIndex ||
                    !OrdinalEquals(
                        expected.SkillDefinitionId.Value,
                        skill.Id) ||
                    !OrdinalEquals(
                        expected.SkillDefinitionId.Value,
                        binding.SkillDefinitionId) ||
                    !OrdinalEquals(
                        expected.SchemaVersion.Value,
                        skill.SchemaVersion) ||
                    !OrdinalEquals(
                        expected.ContentVersion.Value,
                        skill.ContentVersion) ||
                    !OrdinalEquals(
                        expected.ContentVersion.Value,
                        binding.SkillContentVersion) ||
                    !OrdinalEquals(
                        expected.SourceRevision.Value,
                        skill.SourceRevision) ||
                    !OrdinalEquals(
                        expected.TrustedRawSha256.Value,
                        trustedHash) ||
                    !OrdinalEquals(
                        expected.TrustedRawSha256.Value,
                        skill.RawSha256))
                {
                    return false;
                }
            }

            return true;
        }

        private static CombatSkillSlotBinding FindBinding(
            CombatSkillLoadout loadout,
            int slotIndex)
        {
            CombatSkillSlotBinding match = null;
            for (int index = 0; index < loadout.Slots.Count; index++)
            {
                CombatSkillSlotBinding candidate = loadout.Slots[index];
                if (candidate != null &&
                    candidate.SlotIndex == slotIndex)
                {
                    if (match != null)
                    {
                        return null;
                    }
                    match = candidate;
                }
            }
            return match;
        }

        private static bool IsKnownMode(CombatEncounterMode mode)
        {
            switch (mode)
            {
                case CombatEncounterMode.Practice:
                case CombatEncounterMode.DevelopmentDemo:
                case CombatEncounterMode.AuthoritativeBoss:
                case CombatEncounterMode.AuthoritativeQuest:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsKnownLoadStatus(CombatSkillLoadStatus status)
        {
            return status >= CombatSkillLoadStatus.Uninitialized &&
                status <= CombatSkillLoadStatus.Disposed;
        }

        private static bool IsPublishedOutcome(
            CombatSkillLoadStatus status)
        {
            return status == CombatSkillLoadStatus.Loaded ||
                status ==
                    CombatSkillLoadStatus.DevelopmentFallbackLoaded;
        }

        private static bool IsFailureOutcome(
            CombatSkillLoadStatus status)
        {
            return status >= CombatSkillLoadStatus.MissingArtifact &&
                status <= CombatSkillLoadStatus.HashMismatch;
        }

        private static bool OrdinalEquals(string left, string right)
        {
            return StringComparer.Ordinal.Equals(left, right);
        }

        private static CombatSkillLoadSessionPlan Rejected(
            CombatSkillLoadSessionPlanStatus status,
            CombatSkillLoadSessionSnapshot current)
        {
            return Rejected(
                status,
                current,
                current != null
                    ? current.Status
                    : CombatSkillLoadStatus.Uninitialized);
        }

        private static CombatSkillLoadSessionPlan Rejected(
            CombatSkillLoadSessionPlanStatus status,
            CombatSkillLoadSessionSnapshot current,
            CombatSkillLoadStatus observedLoadStatus)
        {
            return new CombatSkillLoadSessionPlan(
                status,
                current,
                current,
                null,
                observedLoadStatus);
        }
    }
}
