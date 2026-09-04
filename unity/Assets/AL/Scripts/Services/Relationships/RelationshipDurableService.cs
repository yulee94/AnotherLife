using System;
using System.Collections.Generic;
using AL.Core.Interfaces.Relationships;

namespace AL.Services.Relationships
{
    public sealed class InMemoryRelationshipCandidatePersistence : IRelationshipCandidatePersistence
    {
        private RelationshipRawState _published;

        public int AttemptCount { get; private set; }

        public bool FailNext { get; set; }

        public RelationshipPersistenceResult PersistAndVerify(RelationshipRawState candidate)
        {
            AttemptCount++;
            if (candidate == null)
            {
                return new RelationshipPersistenceResult(
                    RelationshipPersistenceStatus.Failed,
                    _published?.Clone(),
                    "Candidate is missing.");
            }

            if (FailNext)
            {
                FailNext = false;
                return new RelationshipPersistenceResult(
                    RelationshipPersistenceStatus.Failed,
                    _published?.Clone(),
                    "Injected persistence fault.");
            }

            RelationshipRawState stored = candidate.Clone();
            RelationshipRawState verified = stored.Clone();
            if (!SameCandidate(stored, verified))
            {
                return new RelationshipPersistenceResult(
                    RelationshipPersistenceStatus.Failed,
                    _published?.Clone(),
                    "Persisted candidate failed verification.");
            }

            _published = stored;
            return new RelationshipPersistenceResult(
                RelationshipPersistenceStatus.Verified,
                verified,
                string.Empty);
        }

        public RelationshipRawState LoadPublished()
        {
            return _published?.Clone();
        }

        private static bool SameCandidate(
            RelationshipRawState left,
            RelationshipRawState right)
        {
            return left != null &&
                   right != null &&
                   left.HasCurrentSave == right.HasCurrentSave &&
                   left.ProfileWritable == right.ProfileWritable &&
                   left.NpcAffinityRows.Count == right.NpcAffinityRows.Count &&
                   left.FactionRows.Count == right.FactionRows.Count;
        }
    }

    public sealed class RecordingRelationshipCommitEventSink : IRelationshipCommitEventSink
    {
        private readonly List<RelationshipCommittedChange> _published =
            new List<RelationshipCommittedChange>();

        public List<Action<RelationshipCommittedChange>> Subscribers { get; } =
            new List<Action<RelationshipCommittedChange>>();

        public IReadOnlyList<RelationshipCommittedChange> Published => _published;

        public IReadOnlyList<RelationshipDiagnostic> Publish(RelationshipCommittedChange change)
        {
            if (change != null)
            {
                _published.Add(change);
            }

            var diagnostics = new List<RelationshipDiagnostic>();
            for (int i = 0; i < Subscribers.Count; i++)
            {
                try
                {
                    Subscribers[i]?.Invoke(change);
                }
                catch (Exception exception)
                {
                    diagnostics.Add(
                        new RelationshipDiagnostic(
                            RelationshipDiagnosticSeverity.Warning,
                            RelationshipDiagnosticCodes.EventHandler,
                            change?.Domain,
                            string.Empty,
                            change?.CanonicalTargetId ?? string.Empty,
                            string.Empty,
                            string.Empty,
                            exception.Message,
                            false));
                }
            }

            return diagnostics;
        }
    }

    public sealed class RelationshipDurableService
    {
        private readonly IRelationshipIdentityResolver _identities;
        private readonly IRelationshipPolicyResolver _policies;
        private readonly RelationshipMutationPlanner _planner;
        private readonly IRelationshipCandidatePersistence _persistence;
        private readonly IRelationshipOperationLedger _ledger;
        private readonly IRelationshipCommitEventSink _events;
        private readonly IRelationshipNotificationOutbox _notifications;
        private RelationshipRawState _published;

        public RelationshipDurableService(
            IRelationshipIdentityResolver identities,
            IRelationshipPolicyResolver policies,
            IRelationshipCandidatePersistence persistence,
            IRelationshipOperationLedger ledger,
            IRelationshipCommitEventSink events,
            IRelationshipNotificationOutbox notifications,
            RelationshipRawState initialPublished)
        {
            _identities = identities;
            _policies = policies;
            _planner = new RelationshipMutationPlanner(identities, policies);
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _ledger = ledger ?? new InMemoryRelationshipOperationLedger();
            _events = events ?? new RecordingRelationshipCommitEventSink();
            _notifications = notifications;
            _published = persistence.LoadPublished() ??
                         (initialPublished ?? RelationshipRawState.EmptyWritable()).Clone();
        }

        public RelationshipSnapshot Snapshot()
        {
            return RelationshipSnapshotBuilder.Build(_published.Clone(), _identities, _policies);
        }

        public RelationshipQueryResult QueryNpcAffinity(string npcId)
        {
            return RelationshipSnapshotBuilder.QueryNpcAffinity(Snapshot(), _identities, npcId);
        }

        public RelationshipQueryResult QueryFactionReputation(string factionId)
        {
            return RelationshipSnapshotBuilder.QueryFactionReputation(
                Snapshot(),
                _identities,
                factionId);
        }

        public RelationshipClassificationQueryResult ClassifyNpcAffinity(string npcId)
        {
            return RelationshipSnapshotBuilder.ClassifyNpcAffinity(
                Snapshot(),
                _policies,
                _identities,
                npcId);
        }

        public RelationshipClassificationQueryResult ClassifyFactionReputation(string factionId)
        {
            return RelationshipSnapshotBuilder.ClassifyFactionReputation(
                Snapshot(),
                _policies,
                _identities,
                factionId);
        }

        public PersonaClassificationResult ClassifyPersona()
        {
            return RelationshipSnapshotBuilder.ClassifyPersona(Snapshot());
        }

        public RelationshipPlanningResult Plan(RelationshipMutationRequest request)
        {
            return _planner.Plan(request, Snapshot());
        }

        public void Reload()
        {
            RelationshipRawState loaded = _persistence.LoadPublished();
            if (loaded != null)
            {
                _published = loaded.Clone();
            }
        }

        public RelationshipStandaloneCommitResult Commit(RelationshipMutationRequest request)
        {
            RelationshipSnapshot before = Snapshot();
            if (_ledger.TryGet(request?.OperationId ?? string.Empty, out RelationshipPreparedPlan existing))
            {
                if (SamePayload(existing, request))
                {
                    return new RelationshipStandaloneCommitResult(
                        RelationshipStandaloneCommitStatus.AppliedCommitted,
                        existing,
                        before,
                        before,
                        null,
                        0,
                        Array.Empty<RelationshipDiagnostic>());
                }

                return Reject(
                    RelationshipStandaloneCommitStatus.RejectedValidation,
                    existing,
                    before,
                    0,
                    RelationshipDiagnosticCodes.Apply,
                    "Operation payload collides with a prior commit.");
            }

            if (_ledger.TryFindCorrelation(request?.CorrelationId ?? string.Empty, out string other) &&
                !string.Equals(other, request?.OperationId, StringComparison.Ordinal))
            {
                return Reject(
                    RelationshipStandaloneCommitStatus.RejectedValidation,
                    null,
                    before,
                    0,
                    RelationshipDiagnosticCodes.Correlation,
                    "Correlation is already bound to another operation.");
            }

            RelationshipPlanningResult planned = _planner.Plan(request, before);
            if (planned == null ||
                planned.Plan == null ||
                planned.Status == RelationshipPreparationStatus.RejectedNoCurrentSave ||
                planned.Status == RelationshipPreparationStatus.RejectedReadOnlyProfile ||
                planned.Status == RelationshipPreparationStatus.RejectedUnknownId ||
                planned.Status == RelationshipPreparationStatus.RejectedInvalidTrait ||
                planned.Status == RelationshipPreparationStatus.RejectedMalformedDomain ||
                planned.Status == RelationshipPreparationStatus.RejectedInvalidDelta ||
                planned.Status == RelationshipPreparationStatus.RejectedOverflow ||
                planned.Status == RelationshipPreparationStatus.RejectedPolicyUnavailable ||
                planned.Status == RelationshipPreparationStatus.RejectedCorrelationRequired ||
                planned.Status == RelationshipPreparationStatus.UnsupportedVersion)
            {
                return new RelationshipStandaloneCommitResult(
                    RelationshipStandaloneCommitStatus.RejectedValidation,
                    planned?.Plan,
                    before,
                    before,
                    null,
                    0,
                    planned?.Diagnostics);
            }

            if (planned.Status == RelationshipPreparationStatus.NoChange)
            {
                return new RelationshipStandaloneCommitResult(
                    RelationshipStandaloneCommitStatus.NoChange,
                    planned.Plan,
                    before,
                    before,
                    null,
                    0,
                    planned.Diagnostics);
            }

            var clone = new InMemoryRelationshipMutationTarget(_published.Clone());
            RelationshipApplyResult applied = clone.Apply(
                planned.Plan,
                _identities,
                _policies,
                null);
            if (applied.Status == RelationshipApplyStatus.RejectedStalePlan)
            {
                return new RelationshipStandaloneCommitResult(
                    RelationshipStandaloneCommitStatus.RejectedStale,
                    planned.Plan,
                    before,
                    before,
                    null,
                    0,
                    applied.Diagnostics);
            }

            if (applied.Status != RelationshipApplyStatus.Applied)
            {
                return new RelationshipStandaloneCommitResult(
                    RelationshipStandaloneCommitStatus.RejectedValidation,
                    planned.Plan,
                    before,
                    before,
                    null,
                    0,
                    applied.Diagnostics);
            }

            int attemptsBefore = _persistence.AttemptCount;
            RelationshipPersistenceResult persisted = _persistence.PersistAndVerify(
                clone.CurrentRawState.Clone());
            int attemptCount = _persistence.AttemptCount - attemptsBefore;
            if (!persisted.IsVerified)
            {
                var persistDiagnostics = new List<RelationshipDiagnostic>(applied.Diagnostics)
                {
                    Diagnostic(
                        RelationshipDiagnosticSeverity.Error,
                        RelationshipDiagnosticCodes.Persistence,
                        planned.Plan,
                        persisted.Diagnostic)
                };
                return new RelationshipStandaloneCommitResult(
                    RelationshipStandaloneCommitStatus.PersistenceFailedPreviousPreserved,
                    planned.Plan,
                    before,
                    before,
                    null,
                    attemptCount,
                    persistDiagnostics);
            }

            _published = persisted.Persisted.Clone();
            _ledger.TryRecord(planned.Plan, out _);
            RelationshipSnapshot after = Snapshot();
            var committed = new RelationshipCommittedChange(
                planned.Plan.Domain,
                planned.Plan.CanonicalTargetId,
                planned.Plan.PersonaTrait,
                planned.Plan.PreviousValue,
                planned.Plan.NewValue,
                planned.Plan.AppliedDelta,
                planned.Plan.WasClamped,
                planned.Plan.OperationId,
                planned.Plan.CorrelationId,
                planned.Plan.SourceSystemId,
                after.SnapshotRevision,
                request.OccurredAtUtc.Kind == DateTimeKind.Utc
                    ? request.OccurredAtUtc
                    : DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc));

            var diagnostics = new List<RelationshipDiagnostic>(applied.Diagnostics);
            diagnostics.AddRange(_events.Publish(committed));

            RelationshipStandaloneCommitStatus status =
                RelationshipStandaloneCommitStatus.AppliedCommitted;
            if (_notifications != null &&
                !_notifications.TryEnqueue(committed, out string notificationDiagnostic))
            {
                status = RelationshipStandaloneCommitStatus.NotificationFailedAfterCommit;
                diagnostics.Add(
                    Diagnostic(
                        RelationshipDiagnosticSeverity.Warning,
                        RelationshipDiagnosticCodes.Notification,
                        planned.Plan,
                        notificationDiagnostic));
            }

            return new RelationshipStandaloneCommitResult(
                status,
                planned.Plan,
                before,
                after,
                committed,
                attemptCount,
                diagnostics);
        }

        private static bool SamePayload(
            RelationshipPreparedPlan existing,
            RelationshipMutationRequest request)
        {
            if (existing == null || request == null)
            {
                return false;
            }

            return existing.Domain == request.Domain &&
                   existing.RequestedDelta == request.Delta &&
                   existing.PersonaTrait == request.PersonaTrait &&
                   string.Equals(
                       existing.OperationId,
                       request.OperationId,
                       StringComparison.Ordinal);
        }

        private static RelationshipStandaloneCommitResult Reject(
            RelationshipStandaloneCommitStatus status,
            RelationshipPreparedPlan plan,
            RelationshipSnapshot snapshot,
            int persistAttemptCount,
            string code,
            string action)
        {
            return new RelationshipStandaloneCommitResult(
                status,
                plan,
                snapshot,
                snapshot,
                null,
                persistAttemptCount,
                new[]
                {
                    Diagnostic(RelationshipDiagnosticSeverity.Error, code, plan, action)
                });
        }

        private static RelationshipDiagnostic Diagnostic(
            RelationshipDiagnosticSeverity severity,
            string code,
            RelationshipPreparedPlan plan,
            string action)
        {
            return new RelationshipDiagnostic(
                severity,
                code,
                plan?.Domain,
                string.Empty,
                plan?.CanonicalTargetId ?? string.Empty,
                string.Empty,
                string.Empty,
                action ?? string.Empty,
                severity == RelationshipDiagnosticSeverity.Error);
        }
    }
}
