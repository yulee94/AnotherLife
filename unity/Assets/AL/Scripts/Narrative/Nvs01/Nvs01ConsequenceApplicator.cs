using System;
using System.Collections.Generic;
using AL.Core;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.Narrative.Nvs01.Contracts;
using UnityEngine;

namespace AL.Narrative.Nvs01
{
    internal enum Nvs01ConsequenceApplicationStatus
    {
        Applied = 0,
        AlreadyApplied = 1,
        NotificationFailedAfterCommit = 2,
        PersistenceFailedPreviousPreserved = 3,
        Rejected = 4
    }

    internal sealed class Nvs01ConsequenceNotificationIntent
    {
        internal Nvs01ConsequenceNotificationIntent(
            string notificationId,
            string operationId,
            string profileId,
            string technicalCurrencyId,
            IList<string> effectKeys)
        {
            NotificationId = notificationId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            TechnicalCurrencyId = technicalCurrencyId ?? string.Empty;
            EffectKeys = Nvs01ConsequenceImmutable.FreezeStrings(
                effectKeys,
                Nvs01ConsequenceContract.MaximumAppliedEffectCount + 1);
        }

        internal string NotificationId { get; }
        internal string OperationId { get; }
        internal string ProfileId { get; }
        internal string TechnicalCurrencyId { get; }
        internal IReadOnlyList<string> EffectKeys { get; }
    }

    internal interface INvs01ConsequenceNotificationOutbox
    {
        bool TryEnqueue(
            Nvs01ConsequenceNotificationIntent intent,
            out string diagnostic);
    }

    internal sealed class RecordingNvs01ConsequenceNotificationOutbox :
        INvs01ConsequenceNotificationOutbox
    {
        public List<Nvs01ConsequenceNotificationIntent> Enqueued { get; } =
            new List<Nvs01ConsequenceNotificationIntent>();

        public bool FailNext { get; set; }

        public bool TryEnqueue(
            Nvs01ConsequenceNotificationIntent intent,
            out string diagnostic)
        {
            if (FailNext)
            {
                FailNext = false;
                diagnostic = "Injected notification fault.";
                return false;
            }

            if (intent != null)
            {
                Enqueued.Add(intent);
            }

            diagnostic = string.Empty;
            return true;
        }
    }

    internal enum Nvs01ConsequencePersistStatus
    {
        Verified = 0,
        Failed = 1
    }

    internal sealed class Nvs01ConsequencePersistResult
    {
        internal Nvs01ConsequencePersistResult(
            Nvs01ConsequencePersistStatus status,
            SaveGameData persisted,
            string diagnostic)
        {
            Status = status;
            Persisted = persisted;
            Diagnostic = diagnostic ?? string.Empty;
        }

        internal Nvs01ConsequencePersistStatus Status { get; }
        internal SaveGameData Persisted { get; }
        internal string Diagnostic { get; }
        internal bool IsVerified =>
            Status == Nvs01ConsequencePersistStatus.Verified &&
            Persisted != null;
    }

    internal interface INvs01ConsequenceCandidatePersistence
    {
        int AttemptCount { get; }

        Nvs01ConsequencePersistResult PersistAndVerify(SaveGameData candidate);

        SaveGameData LoadPublished();
    }

    internal sealed class InMemoryNvs01ConsequenceCandidatePersistence :
        INvs01ConsequenceCandidatePersistence
    {
        private SaveGameData _published;

        internal InMemoryNvs01ConsequenceCandidatePersistence(
            SaveGameData initialPublished)
        {
            _published = CloneSave(initialPublished);
        }

        public int AttemptCount { get; private set; }

        public bool FailNext { get; set; }

        public Nvs01ConsequencePersistResult PersistAndVerify(
            SaveGameData candidate)
        {
            AttemptCount++;
            if (candidate == null)
            {
                return new Nvs01ConsequencePersistResult(
                    Nvs01ConsequencePersistStatus.Failed,
                    CloneSave(_published),
                    "Candidate is missing.");
            }

            if (FailNext)
            {
                FailNext = false;
                return new Nvs01ConsequencePersistResult(
                    Nvs01ConsequencePersistStatus.Failed,
                    CloneSave(_published),
                    "Injected persistence fault.");
            }

            SaveGameData stored = CloneSave(candidate);
            _published = stored;
            return new Nvs01ConsequencePersistResult(
                Nvs01ConsequencePersistStatus.Verified,
                CloneSave(stored),
                string.Empty);
        }

        public SaveGameData LoadPublished() => CloneSave(_published);

        internal static SaveGameData CloneSave(SaveGameData source)
        {
            if (source == null) return null;
            return JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(source));
        }
    }

    internal sealed class Nvs01ConsequenceFidelityEvidence
    {
        internal Nvs01ConsequenceFidelityEvidence(
            string packetVersion,
            string packetSha256,
            string profileId,
            string operationId,
            string planFingerprint,
            string technicalCurrencyId,
            string encounterSnapshotVersion,
            string encounterSnapshotReference,
            IList<string> effectKeys)
        {
            PacketVersion = packetVersion ?? string.Empty;
            PacketSha256 = packetSha256 ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            PlanFingerprint = planFingerprint ?? string.Empty;
            TechnicalCurrencyId = technicalCurrencyId ?? string.Empty;
            EncounterSnapshotVersion = encounterSnapshotVersion ?? string.Empty;
            EncounterSnapshotReference =
                encounterSnapshotReference ?? string.Empty;
            EffectKeys = Nvs01ConsequenceImmutable.FreezeStrings(
                effectKeys,
                Nvs01ConsequenceContract.MaximumAppliedEffectCount + 1);
        }

        internal string PacketVersion { get; }
        internal string PacketSha256 { get; }
        internal string ProfileId { get; }
        internal string OperationId { get; }
        internal string PlanFingerprint { get; }
        internal string TechnicalCurrencyId { get; }
        internal string EncounterSnapshotVersion { get; }
        internal string EncounterSnapshotReference { get; }
        internal IReadOnlyList<string> EffectKeys { get; }
    }

    internal sealed class Nvs01ConsequenceApplicationResult
    {
        internal Nvs01ConsequenceApplicationResult(
            Nvs01ConsequenceApplicationStatus status,
            string diagnosticCode,
            Nvs01ConsequencePlan plan,
            Nvs01ConsequenceApplicationReceipt receipt,
            Nvs01ConsequenceFidelityEvidence fidelity,
            int persistAttemptCount)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Plan = plan;
            Receipt = receipt;
            Fidelity = fidelity;
            PersistAttemptCount = persistAttemptCount;
        }

        internal Nvs01ConsequenceApplicationStatus Status { get; }
        internal string DiagnosticCode { get; }
        internal Nvs01ConsequencePlan Plan { get; }
        internal Nvs01ConsequenceApplicationReceipt Receipt { get; }
        internal Nvs01ConsequenceFidelityEvidence Fidelity { get; }
        internal int PersistAttemptCount { get; }
        internal bool IsApplied =>
            Status == Nvs01ConsequenceApplicationStatus.Applied ||
            Status == Nvs01ConsequenceApplicationStatus
                .NotificationFailedAfterCommit;
    }

    internal sealed class Nvs01ConsequenceApplicator
    {
        private readonly Nvs01ConsequencePlanner _planner;
        private readonly INvs01ConsequenceCandidatePersistence _persistence;
        private readonly INvs01ConsequenceNotificationOutbox _notifications;

        internal Nvs01ConsequenceApplicator(
            Nvs01ConsequencePlanner planner,
            INvs01ConsequenceCandidatePersistence persistence,
            INvs01ConsequenceNotificationOutbox notifications)
        {
            _planner = planner ??
                throw new ArgumentNullException(nameof(planner));
            _persistence = persistence ??
                throw new ArgumentNullException(nameof(persistence));
            _notifications = notifications ??
                throw new ArgumentNullException(nameof(notifications));
        }

        internal Nvs01ConsequenceApplicationResult Commit(
            Nvs01ConsequencePlanningContext context)
        {
            SaveGameData before = _persistence.LoadPublished();
            if (context == null ||
                context.Catalog == null ||
                context.QuestMutation == null ||
                context.Authority == null ||
                context.Dependencies == null)
            {
                return Reject(
                    Nvs01ConsequenceDiagnosticCodes.MissingInput,
                    0);
            }

            string mixed = DetectMixedAuthority(context, before);
            if (mixed.Length != 0)
            {
                return Reject(mixed, 0);
            }

            if (context.Authority.Status != ProfileWriteAuthorityStatus.Writable ||
                before == null ||
                before.SaveSchemaVersion !=
                    SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion)
            {
                return Reject(
                    Nvs01ConsequenceDiagnosticCodes.AuthorityUnavailable,
                    0);
            }

            Nvs01ConsequencePlanningResult planned = _planner.Plan(context);
            if (planned == null)
            {
                return Reject(
                    Nvs01ConsequenceDiagnosticCodes.MissingInput,
                    0);
            }

            if (planned.Status == Nvs01ConsequencePlanningStatus.AlreadyApplied)
            {
                Nvs01ConsequenceApplicationReceipt existing =
                    FindReceipt(
                        context.Domain,
                        context.QuestMutation.Candidate.LastOperation?
                            .EventId ==
                        Nvs01ConsequenceContract.ReportConclusionEventId
                            ? Nvs01ConsequenceContract.ReportOperationId
                            : Nvs01ConsequenceContract.ArenaOperationPrefix +
                              context.QuestMutation.Candidate
                                  .LastEncounterCorrelationId);
                if (existing == null || !existing.HasCanonicalFingerprint())
                {
                    return Reject(
                        Nvs01ConsequenceDiagnosticCodes
                            .ReplayFingerprintMismatch,
                        0);
                }

                return new Nvs01ConsequenceApplicationResult(
                    Nvs01ConsequenceApplicationStatus.AlreadyApplied,
                    string.Empty,
                    null,
                    existing,
                    ToFidelity(context, existing),
                    0);
            }

            if (!planned.IsReady)
            {
                string code = planned.DiagnosticCode;
                if (planned.Status ==
                    Nvs01ConsequencePlanningStatus.RejectedPartialApplication)
                {
                    code = Nvs01ConsequenceDiagnosticCodes.PartialApplication;
                }

                return Reject(
                    string.IsNullOrEmpty(code)
                        ? Nvs01ConsequenceDiagnosticCodes.ContractMismatch
                        : code,
                    0);
            }

            Nvs01ConsequencePlan plan = planned.Plan;
            if (!Nvs01ConsequenceContract.IsAuthoritativeOathmarkCurrency(
                    plan.ApplicationReceipt.TechnicalCurrencyId) ||
                Nvs01ConsequenceContract.IsForbiddenCurrencySubstitution(
                    plan.ApplicationReceipt.TechnicalCurrencyId) ||
                CreditsForbiddenCurrency(plan))
            {
                return Reject(
                    Nvs01ConsequenceDiagnosticCodes.DependencyMalformed,
                    0);
            }

            if (!plan.ApplicationReceipt.HasCanonicalFingerprint())
            {
                return Reject(
                    Nvs01ConsequenceDiagnosticCodes.ReplayFingerprintMismatch,
                    0);
            }

            SaveGameData candidate =
                InMemoryNvs01ConsequenceCandidatePersistence.CloneSave(before);
            if (!TryApplyPlan(candidate, context, plan, out string applyError))
            {
                return Reject(
                    string.IsNullOrEmpty(applyError)
                        ? Nvs01ConsequenceDiagnosticCodes.PartialApplication
                        : applyError,
                    0);
            }

            int attemptsBefore = _persistence.AttemptCount;
            Nvs01ConsequencePersistResult persisted =
                _persistence.PersistAndVerify(candidate);
            int attemptCount = _persistence.AttemptCount - attemptsBefore;
            if (!persisted.IsVerified)
            {
                return new Nvs01ConsequenceApplicationResult(
                    Nvs01ConsequenceApplicationStatus
                        .PersistenceFailedPreviousPreserved,
                    Nvs01ConsequenceDiagnosticCodes.PersistFailed,
                    plan,
                    plan.ApplicationReceipt,
                    ToFidelity(context, plan.ApplicationReceipt),
                    attemptCount);
            }

            Nvs01ConsequenceApplicationStatus status =
                Nvs01ConsequenceApplicationStatus.Applied;
            var intent = new Nvs01ConsequenceNotificationIntent(
                Nvs01ConsequenceDiagnosticCodes.EffectsCommittedNotificationId,
                plan.OperationId,
                plan.ProfileId,
                plan.ApplicationReceipt.TechnicalCurrencyId,
                new List<string>(plan.ApplicationReceipt.EffectKeys));
            if (!_notifications.TryEnqueue(intent, out _))
            {
                status = Nvs01ConsequenceApplicationStatus
                    .NotificationFailedAfterCommit;
            }

            return new Nvs01ConsequenceApplicationResult(
                status,
                status == Nvs01ConsequenceApplicationStatus
                    .NotificationFailedAfterCommit
                    ? Nvs01ConsequenceDiagnosticCodes.NotifyFailed
                    : string.Empty,
                plan,
                plan.ApplicationReceipt,
                ToFidelity(context, plan.ApplicationReceipt),
                attemptCount);
        }

        private static string DetectMixedAuthority(
            Nvs01ConsequencePlanningContext context,
            SaveGameData published)
        {
            if (published == null)
            {
                return Nvs01ConsequenceDiagnosticCodes.AuthorityUnavailable;
            }

            if (!string.Equals(
                    published.ProfileId ?? string.Empty,
                    context.Authority.ProfileId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    context.Catalog.CanonicalSha256,
                    Nvs01ConsequenceContract.PacketSha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    context.Catalog.Catalog.PacketVersion,
                    Nvs01ConsequenceContract.PacketVersion,
                    StringComparison.Ordinal))
            {
                return Nvs01ConsequenceDiagnosticCodes.MixedAuthority;
            }

            Nvs01QuestSnapshot snapshot = context.QuestMutation.Candidate;
            if (snapshot != null &&
                (!string.IsNullOrEmpty(snapshot.LastEncounterSnapshotVersion) ||
                 !string.IsNullOrEmpty(snapshot.LastEncounterSnapshotReference)) &&
                !Nvs01ConsequenceContract.MatchesCatalogBackedEncounterResult(
                    snapshot.LastEncounterSnapshotVersion,
                    snapshot.LastEncounterSnapshotReference))
            {
                return Nvs01ConsequenceDiagnosticCodes.MixedAuthority;
            }

            return string.Empty;
        }

        private static bool CreditsForbiddenCurrency(Nvs01ConsequencePlan plan)
        {
            for (int index = 0; index < plan.Operations.Count; index++)
            {
                Nvs01ConsequenceOperation operation = plan.Operations[index];
                if (operation.Kind != Nvs01ConsequenceMutationKind.CreditResource)
                {
                    continue;
                }

                if (Nvs01ConsequenceContract.IsForbiddenCurrencySubstitution(
                        operation.TargetId) ||
                    string.Equals(
                        operation.TargetId,
                        Nvs01ConsequenceContract.ForbiddenLegacyGoldResourceId,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        operation.TargetId,
                        Nvs01ConsequenceContract.ForbiddenKingdomResourceId,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        operation.TargetId,
                        Nvs01ConsequenceContract.CatalogGoldTargetId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryApplyPlan(
            SaveGameData candidate,
            Nvs01ConsequencePlanningContext context,
            Nvs01ConsequencePlan plan,
            out string error)
        {
            error = string.Empty;
            long goldBefore = ReadGoldAmount(candidate);
            if (!Nvs01ProgressCodec.TryDecode(
                    candidate.Nvs01Progress,
                    context.Catalog,
                    out Nvs01QuestSnapshot durable,
                    out _) ||
                !Nvs01ProgressCodec.Equivalent(
                    durable,
                    context.QuestMutation.Expected))
            {
                error = Nvs01ConsequenceDiagnosticCodes.QuestTransitionMismatch;
                return false;
            }

            Nvs01ProgressData progress = Nvs01ProgressCodec.Encode(
                context.QuestMutation.Candidate);
            Nvs01ProgressCodec.CopyConsequenceLedger(
                progress,
                candidate.Nvs01Progress);

            if (!TryAppendLedger(progress, plan, out error))
            {
                return false;
            }

            if (plan.Kind == Nvs01ConsequencePlanKind.ReportCompletion)
            {
                if (!TryAdjustValeriusAffinity(
                        candidate,
                        plan.ApplicationReceipt.PreviousValeriusAffinity,
                        plan.ResultingValeriusAffinity,
                        out error))
                {
                    return false;
                }

                progress.UnlockedChapterId =
                    plan.ApplicationReceipt.TargetChapterId;
                candidate.CurrentChapterId = plan.ResultingChapterId;
            }

            candidate.Nvs01Progress = progress;
            if (ReadGoldAmount(candidate) != goldBefore)
            {
                error = Nvs01ConsequenceDiagnosticCodes.DependencyMalformed;
                return false;
            }

            if (!Nvs01ProgressCodec.HasExactConsequenceLedger(progress))
            {
                error = Nvs01ConsequenceDiagnosticCodes.PartialApplication;
                return false;
            }

            return true;
        }

        private static bool TryAppendLedger(
            Nvs01ProgressData progress,
            Nvs01ConsequencePlan plan,
            out string error)
        {
            error = string.Empty;
            progress.AcquiredArtifactIds =
                progress.AcquiredArtifactIds ?? new List<string>();
            progress.AppliedEffectKeys =
                progress.AppliedEffectKeys ?? new List<string>();
            progress.AppliedOperationIds =
                progress.AppliedOperationIds ?? new List<string>();
            progress.ApplicationReceipts =
                progress.ApplicationReceipts ??
                new List<Nvs01ConsequenceApplicationReceiptData>();

            if (ContainsOrdinal(progress.AppliedOperationIds, plan.OperationId))
            {
                error = Nvs01ConsequenceDiagnosticCodes.PartialApplication;
                return false;
            }

            if (plan.Kind == Nvs01ConsequencePlanKind.ArenaSuccess)
            {
                if (progress.AcquiredArtifactIds.Count != 0 ||
                    progress.AppliedOperationIds.Count != 0 ||
                    progress.AppliedEffectKeys.Count != 0 ||
                    progress.ApplicationReceipts.Count != 0)
                {
                    error = Nvs01ConsequenceDiagnosticCodes.PartialApplication;
                    return false;
                }

                progress.AcquiredArtifactIds.Add(
                    Nvs01ConsequenceContract.TearArtifactId);
            }
            else if (
                progress.AcquiredArtifactIds.Count != 1 ||
                !ContainsOrdinal(
                    progress.AcquiredArtifactIds,
                    Nvs01ConsequenceContract.TearArtifactId) ||
                progress.ApplicationReceipts.Count != 1)
            {
                error = Nvs01ConsequenceDiagnosticCodes.PartialApplication;
                return false;
            }

            progress.AppliedOperationIds.Add(plan.OperationId);
            for (int index = 0; index < plan.ApplicationReceipt.EffectKeys.Count; index++)
            {
                string effect = plan.ApplicationReceipt.EffectKeys[index];
                if (ContainsOrdinal(progress.AppliedEffectKeys, effect))
                {
                    error = Nvs01ConsequenceDiagnosticCodes.PartialApplication;
                    return false;
                }

                progress.AppliedEffectKeys.Add(effect);
            }

            progress.ApplicationReceipts.Add(
                Nvs01ProgressCodec.EncodeReceipt(plan.ApplicationReceipt));
            return true;
        }

        private static bool TryAdjustValeriusAffinity(
            SaveGameData candidate,
            float expectedPrevious,
            float resulting,
            out string error)
        {
            error = string.Empty;
            candidate.Reputation = candidate.Reputation ??
                new List<NpcAffinityData>();
            NpcAffinityData row = null;
            for (int index = 0; index < candidate.Reputation.Count; index++)
            {
                NpcAffinityData current = candidate.Reputation[index];
                if (current != null &&
                    string.Equals(
                        current.NpcId,
                        Nvs01ConsequenceContract.ValeriusNpcId,
                        StringComparison.Ordinal))
                {
                    row = current;
                    break;
                }
            }

            float previous = row == null ? 0f : row.Affinity;
            if (Math.Abs(previous - expectedPrevious) > 0.0001f)
            {
                error = Nvs01ConsequenceDiagnosticCodes.PartialApplication;
                return false;
            }

            if (row == null)
            {
                row = new NpcAffinityData
                {
                    NpcId = Nvs01ConsequenceContract.ValeriusNpcId,
                    Affinity = resulting
                };
                candidate.Reputation.Add(row);
            }
            else
            {
                row.Affinity = resulting;
            }

            return true;
        }

        private static long ReadGoldAmount(SaveGameData save)
        {
            if (save?.Resources == null) return 0;
            long total = 0;
            for (int index = 0; index < save.Resources.Count; index++)
            {
                ResourceData resource = save.Resources[index];
                if (resource != null && resource.Type == ResourceType.Gold)
                {
                    total += resource.Amount;
                }
            }

            return total;
        }

        private static bool ContainsOrdinal(IList<string> values, string expected)
        {
            if (values == null) return false;
            for (int index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index], expected, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static Nvs01ConsequenceApplicationReceipt FindReceipt(
            Nvs01ConsequenceDomainSnapshot domain,
            string operationId)
        {
            if (domain?.ApplicationReceipts == null) return null;
            for (int index = 0; index < domain.ApplicationReceipts.Count; index++)
            {
                Nvs01ConsequenceApplicationReceipt receipt =
                    domain.ApplicationReceipts[index];
                if (receipt != null &&
                    string.Equals(
                        receipt.OperationId,
                        operationId,
                        StringComparison.Ordinal))
                {
                    return receipt;
                }
            }

            return null;
        }

        private static Nvs01ConsequenceFidelityEvidence ToFidelity(
            Nvs01ConsequencePlanningContext context,
            Nvs01ConsequenceApplicationReceipt receipt)
        {
            Nvs01QuestSnapshot snapshot = context.QuestMutation.Candidate;
            return new Nvs01ConsequenceFidelityEvidence(
                Nvs01ConsequenceContract.PacketVersion,
                Nvs01ConsequenceContract.PacketSha256,
                receipt.ProfileId,
                receipt.OperationId,
                receipt.PlanFingerprint,
                receipt.TechnicalCurrencyId,
                snapshot.LastEncounterSnapshotVersion,
                snapshot.LastEncounterSnapshotReference,
                new List<string>(receipt.EffectKeys));
        }

        private static Nvs01ConsequenceApplicationResult Reject(
            string diagnosticCode,
            int persistAttemptCount)
        {
            return new Nvs01ConsequenceApplicationResult(
                Nvs01ConsequenceApplicationStatus.Rejected,
                diagnosticCode,
                null,
                null,
                null,
                persistAttemptCount);
        }
    }
}
