using System;
using System.Collections.Generic;
using AL.Core.Interfaces.WorldState;
using AL.Data.Runtime;

namespace AL.Services.WorldState
{
    public sealed class InMemoryWorldStateCandidatePersistence : IWorldStateCandidatePersistence
    {
        private WorldStatePersistentState _published;

        public int AttemptCount { get; private set; }

        public bool FailNext { get; set; }

        public WorldStatePersistenceResult PersistAndVerify(WorldStatePersistentState candidate)
        {
            AttemptCount++;
            if (candidate == null)
            {
                return new WorldStatePersistenceResult(
                    WorldStatePersistenceStatus.Failed,
                    WorldStatePersistentMapper.Clone(_published),
                    "Candidate is missing.");
            }

            if (FailNext)
            {
                FailNext = false;
                return new WorldStatePersistenceResult(
                    WorldStatePersistenceStatus.Failed,
                    WorldStatePersistentMapper.Clone(_published),
                    "Injected persistence fault.");
            }

            WorldStatePersistentState stored = WorldStatePersistentMapper.Clone(candidate);
            WorldStatePersistentState verified = WorldStatePersistentMapper.Clone(stored);
            if (!WorldStatePersistentMapper.SameJson(stored, verified))
            {
                return new WorldStatePersistenceResult(
                    WorldStatePersistenceStatus.Failed,
                    WorldStatePersistentMapper.Clone(_published),
                    "Persisted candidate failed verification.");
            }

            _published = stored;
            return new WorldStatePersistenceResult(
                WorldStatePersistenceStatus.Verified,
                verified,
                string.Empty);
        }

        public WorldStatePersistentState LoadPublished()
        {
            return _published == null
                ? null
                : WorldStatePersistentMapper.Clone(_published);
        }
    }

    public sealed class RecordingWorldStateCommitEventSink : IWorldStateCommitEventSink
    {
        private readonly List<WorldStateTransitionEvent> _published =
            new List<WorldStateTransitionEvent>();

        public List<Action<WorldStateTransitionEvent>> Subscribers { get; } =
            new List<Action<WorldStateTransitionEvent>>();

        public IReadOnlyList<WorldStateTransitionEvent> Published => _published;

        public IReadOnlyList<WorldStateDiagnostic> Publish(WorldStateTransitionEvent change)
        {
            if (change != null)
            {
                _published.Add(change);
            }

            var diagnostics = new List<WorldStateDiagnostic>();
            for (int i = 0; i < Subscribers.Count; i++)
            {
                try
                {
                    Subscribers[i]?.Invoke(change);
                }
                catch (Exception exception)
                {
                    diagnostics.Add(
                        new WorldStateDiagnostic(
                            WorldStateDiagnosticSeverity.Warning,
                            "AL-WST-EVENT-HANDLER",
                            change?.InstanceId ?? string.Empty,
                            exception.Message));
                }
            }

            return diagnostics;
        }
    }

    public sealed class RecordingWorldStateNotificationOutbox : IWorldStateNotificationOutbox
    {
        public List<WorldStateNotificationIntent> Enqueued { get; } =
            new List<WorldStateNotificationIntent>();

        public bool FailNext { get; set; }

        public bool TryEnqueue(WorldStateNotificationIntent intent, out string diagnostic)
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

    public sealed class WorldStateDurableService
    {
        private readonly WorldStateLifecyclePlanner _planner;
        private readonly WorldEffectConsumerRegistry _consumers;
        private readonly IWorldStateCandidatePersistence _persistence;
        private readonly IWorldStateCommitEventSink _events;
        private readonly IWorldStateNotificationOutbox _notifications;
        private WorldStatePersistentState _published;

        public WorldStateDurableService(
            IWorldStateDefinitionResolver definitions,
            IWorldStateClock clock,
            WorldEffectConsumerRegistry consumers,
            IWorldStateCandidatePersistence persistence,
            IWorldStateCommitEventSink events,
            IWorldStateNotificationOutbox notifications,
            WorldStatePersistentState initialPublished)
        {
            _consumers = consumers ??
                         new WorldEffectConsumerRegistry(
                             new[] { WorldStateAuthoredCatalog.CreatePresentationConsumer() });
            _planner = new WorldStateLifecyclePlanner(
                definitions ?? WorldStateAuthoredCatalog.CreateResolver(),
                clock,
                _consumers);
            _persistence = persistence ??
                           throw new ArgumentNullException(nameof(persistence));
            _events = events ?? new RecordingWorldStateCommitEventSink();
            _notifications = notifications;
            _published = persistence.LoadPublished();
            if (_published == null || _published.Version == 0)
            {
                _published = initialPublished != null
                    ? WorldStatePersistentMapper.Clone(initialPublished)
                    : WorldStatePersistentMapper.Empty();
            }
        }

        public WorldStateSnapshot Snapshot()
        {
            return WorldStatePersistentMapper.ToSnapshot(_published);
        }

        public void Reload()
        {
            WorldStatePersistentState loaded = _persistence.LoadPublished();
            if (loaded != null && loaded.Version != 0)
            {
                _published = WorldStatePersistentMapper.Clone(loaded);
            }
        }

        public WorldStateStandaloneCommitResult CommitStart(WorldStateStartRequest request)
        {
            return Commit(_planner.PlanStart(request, Snapshot()));
        }

        public WorldStateStandaloneCommitResult CommitEnd(WorldStateEndRequest request)
        {
            return Commit(_planner.PlanEnd(request, Snapshot()));
        }

        private WorldStateStandaloneCommitResult Commit(WorldStatePlanningResult planned)
        {
            WorldStateSnapshot before = Snapshot();
            if (planned == null)
            {
                return Reject(
                    WorldStateStandaloneCommitStatus.RejectedValidation,
                    null,
                    before,
                    0,
                    "AL-WST-PLAN",
                    "Planning returned no result.");
            }

            if (planned.Status == WorldStatePlanningStatus.AlreadyCommitted)
            {
                return new WorldStateStandaloneCommitResult(
                    WorldStateStandaloneCommitStatus.AlreadyCommitted,
                    null,
                    before,
                    before,
                    null,
                    0,
                    planned.Diagnostics);
            }

            if (planned.Status == WorldStatePlanningStatus.NoChangeAlreadyInState)
            {
                return new WorldStateStandaloneCommitResult(
                    WorldStateStandaloneCommitStatus.NoChange,
                    null,
                    before,
                    before,
                    null,
                    0,
                    planned.Diagnostics);
            }

            if (planned.Status == WorldStatePlanningStatus.RejectedStaleSnapshot)
            {
                return new WorldStateStandaloneCommitResult(
                    WorldStateStandaloneCommitStatus.RejectedStale,
                    null,
                    before,
                    before,
                    null,
                    0,
                    planned.Diagnostics);
            }

            if (planned.Status != WorldStatePlanningStatus.Prepared ||
                planned.Plan == null)
            {
                return new WorldStateStandaloneCommitResult(
                    WorldStateStandaloneCommitStatus.RejectedValidation,
                    planned.Plan,
                    before,
                    before,
                    null,
                    0,
                    planned.Diagnostics);
            }

            WorldStatePersistentState candidate = WorldStatePersistentMapper.Clone(_published);
            var target = new WorldStateMutationTarget(candidate);
            WorldStateEffectExecutionResult executed =
                WorldStateEffectPlanExecutor.ApplyToIsolatedCandidate(
                    planned.Plan,
                    _consumers,
                    target);
            if (executed.Status == WorldEffectApplyStatus.RejectedStaleTarget)
            {
                return new WorldStateStandaloneCommitResult(
                    WorldStateStandaloneCommitStatus.RejectedStale,
                    planned.Plan,
                    before,
                    before,
                    null,
                    0,
                    executed.Diagnostics);
            }

            if (executed.Status != WorldEffectApplyStatus.Applied)
            {
                return new WorldStateStandaloneCommitResult(
                    WorldStateStandaloneCommitStatus.RejectedValidation,
                    planned.Plan,
                    before,
                    before,
                    null,
                    0,
                    executed.Diagnostics);
            }

            WorldStatePersistentMapper.ApplyTransition(candidate, planned.Plan);
            int attemptsBefore = _persistence.AttemptCount;
            WorldStatePersistenceResult persisted = _persistence.PersistAndVerify(candidate);
            int attemptCount = _persistence.AttemptCount - attemptsBefore;
            if (!persisted.IsVerified)
            {
                var persistDiagnostics = new List<WorldStateDiagnostic>(executed.Diagnostics)
                {
                    new WorldStateDiagnostic(
                        WorldStateDiagnosticSeverity.Error,
                        "AL-WST-PERSIST",
                        planned.Plan.InstanceAfter.InstanceId,
                        persisted.Diagnostic)
                };
                return new WorldStateStandaloneCommitResult(
                    WorldStateStandaloneCommitStatus.PersistenceFailedPreviousPreserved,
                    planned.Plan,
                    before,
                    before,
                    null,
                    attemptCount,
                    persistDiagnostics);
            }

            _published = WorldStatePersistentMapper.Clone(persisted.Persisted);
            WorldStateSnapshot after = Snapshot();
            var diagnostics = new List<WorldStateDiagnostic>(executed.Diagnostics);
            diagnostics.AddRange(_events.Publish(planned.Plan.PostCommitEvent));

            WorldStateStandaloneCommitStatus status =
                WorldStateStandaloneCommitStatus.AppliedCommitted;
            if (_notifications != null)
            {
                for (int i = 0; i < planned.Plan.NotificationIntents.Count; i++)
                {
                    if (!_notifications.TryEnqueue(
                            planned.Plan.NotificationIntents[i],
                            out string notificationDiagnostic))
                    {
                        status = WorldStateStandaloneCommitStatus.NotificationFailedAfterCommit;
                        diagnostics.Add(
                            new WorldStateDiagnostic(
                                WorldStateDiagnosticSeverity.Warning,
                                "AL-WST-NOTIFY",
                                planned.Plan.InstanceAfter.InstanceId,
                                notificationDiagnostic));
                    }
                }
            }

            return new WorldStateStandaloneCommitResult(
                status,
                planned.Plan,
                before,
                after,
                planned.Plan.PostCommitEvent,
                attemptCount,
                diagnostics);
        }

        private static WorldStateStandaloneCommitResult Reject(
            WorldStateStandaloneCommitStatus status,
            WorldStateTransitionPlan plan,
            WorldStateSnapshot snapshot,
            int persistAttemptCount,
            string code,
            string action)
        {
            return new WorldStateStandaloneCommitResult(
                status,
                plan,
                snapshot,
                snapshot,
                null,
                persistAttemptCount,
                new[]
                {
                    new WorldStateDiagnostic(
                        WorldStateDiagnosticSeverity.Error,
                        code,
                        plan?.InstanceAfter?.InstanceId ?? string.Empty,
                        action)
                });
        }

        private sealed class WorldStateMutationTarget : IWorldStateMutationTarget
        {
            public WorldStateMutationTarget(WorldStatePersistentState state)
            {
                State = state;
            }

            public WorldStatePersistentState State { get; }

            public long WorldStateRevision => State.SnapshotRevision;

            public long EffectRevision => State.EffectRevision;
        }
    }
}
