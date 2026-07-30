using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace AL.Core.SaveAuthority
{
    internal enum ProfileMutationStatus
    {
        Unavailable = 0,
        Committed = 1,
        AlreadyCommitted = 2,
        Busy = 3,
        NotWritable = 4,
        StaleAuthority = 5,
        PublicationBackpressure = 6,
        PublicationUnavailable = 7,
        EpochUnavailable = 8,
        PreparationRejected = 9,
        PreparationFailed = 10,
        CandidateInvalid = 11,
        PersistenceRejected = 12,
        VerifiedRollback = 13,
        CommitUncertain = 14
    }

    internal sealed class ProfileMutationResult
    {
        internal ProfileMutationResult(
            ProfileMutationStatus status,
            ProfileMutationReceipt receipt,
            string diagnosticCode)
        {
            Status = status;
            Receipt = receipt;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        internal ProfileMutationStatus Status { get; }
        internal ProfileMutationReceipt Receipt { get; }
        internal string DiagnosticCode { get; }
    }

    internal enum ProfileCandidatePreparationStatus
    {
        Unavailable = 0,
        Prepared = 1,
        Rejected = 2,
        ExactReplay = 3
    }

    internal sealed class ProfileCandidatePreparation
    {
        private ProfileCandidatePreparation(
            ProfileCandidatePreparationStatus status,
            string diagnosticCode)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        internal ProfileCandidatePreparationStatus Status { get; }
        internal string DiagnosticCode { get; }

        internal static ProfileCandidatePreparation Prepared() =>
            new ProfileCandidatePreparation(
                ProfileCandidatePreparationStatus.Prepared,
                string.Empty);

        internal static ProfileCandidatePreparation Rejected(
            string diagnosticCode) =>
            new ProfileCandidatePreparation(
                ProfileCandidatePreparationStatus.Rejected,
                diagnosticCode);

        internal static ProfileCandidatePreparation ExactReplay() =>
            new ProfileCandidatePreparation(
                ProfileCandidatePreparationStatus.ExactReplay,
                string.Empty);
    }

    internal sealed class ProfileMutationReplayVerification
    {
        private ProfileMutationReplayVerification(
            bool isVerified,
            string expectedGenerationFingerprint,
            string committedPayloadFingerprint,
            string diagnosticCode)
        {
            IsVerified = isVerified;
            ExpectedGenerationFingerprint =
                expectedGenerationFingerprint ?? string.Empty;
            CommittedPayloadFingerprint =
                committedPayloadFingerprint ?? string.Empty;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        internal bool IsVerified { get; }
        internal string ExpectedGenerationFingerprint { get; }
        internal string CommittedPayloadFingerprint { get; }
        internal string DiagnosticCode { get; }

        internal static ProfileMutationReplayVerification Verified(
            string expectedGenerationFingerprint,
            string committedPayloadFingerprint) =>
            new ProfileMutationReplayVerification(
                true,
                expectedGenerationFingerprint,
                committedPayloadFingerprint,
                string.Empty);

        internal static ProfileMutationReplayVerification Invalid(
            string diagnosticCode) =>
            new ProfileMutationReplayVerification(
                false,
                string.Empty,
                string.Empty,
                diagnosticCode);
    }

    internal sealed class ProfileCandidateValidationResult
    {
        private ProfileCandidateValidationResult(
            bool isValid,
            string diagnosticCode)
        {
            IsValid = isValid;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        internal bool IsValid { get; }
        internal string DiagnosticCode { get; }

        internal static ProfileCandidateValidationResult Valid() =>
            new ProfileCandidateValidationResult(true, string.Empty);

        internal static ProfileCandidateValidationResult Invalid(
            string diagnosticCode) =>
            new ProfileCandidateValidationResult(false, diagnosticCode);
    }

    internal interface IProfileMutationCandidateAdapter<TCandidate>
    {
        TCandidate Clone(TCandidate source);
        ProfileCandidateValidationResult ValidatePublished(
            TCandidate candidate,
            string expectedProfileId,
            string expectedGenerationFingerprint);
        ProfileCandidateValidationResult Validate(
            TCandidate candidate,
            string expectedProfileId);
        ProfileMutationReplayVerification VerifyReplay(
            TCandidate publishedCandidate,
            string expectedProfileId,
            string expectedGenerationFingerprint,
            string operationId,
            string resultId);
    }

    internal enum ProfilePersistenceAuthorityStatus
    {
        Unavailable = 0,
        Current = 1,
        Stale = 2
    }

    internal sealed class ProfilePersistenceAuthorityCheck
    {
        internal ProfilePersistenceAuthorityCheck(
            ProfilePersistenceAuthorityStatus status,
            string diagnosticCode)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        internal ProfilePersistenceAuthorityStatus Status { get; }
        internal string DiagnosticCode { get; }

        internal static ProfilePersistenceAuthorityCheck Current() =>
            new ProfilePersistenceAuthorityCheck(
                ProfilePersistenceAuthorityStatus.Current,
                string.Empty);

        internal static ProfilePersistenceAuthorityCheck Stale() =>
            new ProfilePersistenceAuthorityCheck(
                ProfilePersistenceAuthorityStatus.Stale,
                "AL-SAVE-AUTH-PERSISTENCE-STALE");

        internal static ProfilePersistenceAuthorityCheck Unavailable(
            string diagnosticCode) =>
            new ProfilePersistenceAuthorityCheck(
                ProfilePersistenceAuthorityStatus.Unavailable,
                diagnosticCode);
    }

    internal enum ProfileCandidatePersistenceStatus
    {
        Unavailable = 0,
        Committed = 1,
        RejectedBeforeMutation = 2,
        VerifiedRollback = 3,
        CommitUncertain = 4
    }

    internal sealed class ProfileCandidatePersistenceResult<TCandidate>
    {
        private ProfileCandidatePersistenceResult(
            ProfileCandidatePersistenceStatus status,
            TCandidate committedCandidate,
            string committedGenerationFingerprint,
            string committedPayloadFingerprint,
            ProfileAuthoritySourceGeneration committedSourceGeneration,
            string diagnosticCode)
        {
            Status = status;
            CommittedCandidate = committedCandidate;
            CommittedGenerationFingerprint =
                committedGenerationFingerprint ?? string.Empty;
            CommittedPayloadFingerprint =
                committedPayloadFingerprint ?? string.Empty;
            CommittedSourceGeneration = committedSourceGeneration;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        internal ProfileCandidatePersistenceStatus Status { get; }
        internal TCandidate CommittedCandidate { get; }
        internal string CommittedGenerationFingerprint { get; }
        internal string CommittedPayloadFingerprint { get; }
        internal ProfileAuthoritySourceGeneration CommittedSourceGeneration
        {
            get;
        }
        internal string DiagnosticCode { get; }

        internal static ProfileCandidatePersistenceResult<TCandidate>
            Committed(
                TCandidate committedCandidate,
                string committedGenerationFingerprint,
                string committedPayloadFingerprint,
                ProfileAuthoritySourceGeneration committedSourceGeneration) =>
            new ProfileCandidatePersistenceResult<TCandidate>(
                ProfileCandidatePersistenceStatus.Committed,
                committedCandidate,
                committedGenerationFingerprint,
                committedPayloadFingerprint,
                committedSourceGeneration,
                string.Empty);

        internal static ProfileCandidatePersistenceResult<TCandidate>
            Rejected(string diagnosticCode) =>
            new ProfileCandidatePersistenceResult<TCandidate>(
                ProfileCandidatePersistenceStatus.RejectedBeforeMutation,
                default(TCandidate),
                string.Empty,
                string.Empty,
                ProfileAuthoritySourceGeneration.None,
                diagnosticCode);

        internal static ProfileCandidatePersistenceResult<TCandidate>
            VerifiedRollback(string diagnosticCode) =>
            new ProfileCandidatePersistenceResult<TCandidate>(
                ProfileCandidatePersistenceStatus.VerifiedRollback,
                default(TCandidate),
                string.Empty,
                string.Empty,
                ProfileAuthoritySourceGeneration.None,
                diagnosticCode);

        internal static ProfileCandidatePersistenceResult<TCandidate>
            CommitUncertain(string diagnosticCode) =>
            new ProfileCandidatePersistenceResult<TCandidate>(
                ProfileCandidatePersistenceStatus.CommitUncertain,
                default(TCandidate),
                string.Empty,
                string.Empty,
                ProfileAuthoritySourceGeneration.None,
                diagnosticCode);
    }

    internal sealed class ProfileMutationReservation
    {
        internal ProfileMutationReservation(
            ulong publicationSequence,
            string reservedAuthorityEpoch)
        {
            PublicationSequence = publicationSequence;
            ReservedAuthorityEpoch = reservedAuthorityEpoch;
        }

        internal ulong PublicationSequence { get; }
        internal string ReservedAuthorityEpoch { get; }
    }

    internal sealed class ProfileMutationCommitContext
    {
        internal ProfileMutationCommitContext(
            string profileId,
            string expectedGenerationFingerprint)
        {
            ProfileId = profileId;
            ExpectedGenerationFingerprint =
                expectedGenerationFingerprint;
        }

        internal string ProfileId { get; }
        internal string ExpectedGenerationFingerprint { get; }
    }

    internal interface IProfileMutationPersistence<TCandidate>
    {
        ProfilePersistenceAuthorityCheck RecheckAuthority(
            ProfileMutationCommitContext context);

        ProfileCandidatePersistenceResult<TCandidate> PersistAndVerify(
            TCandidate candidate,
            ProfileMutationCommitContext context);
    }

    internal interface IProfileMutationReceiptSink
    {
        void Publish(ProfileMutationReceipt receipt);
    }

    internal interface IAuthorityReceiptContinuationScheduler
    {
        bool TrySchedule(Action continuation);
    }

    internal sealed class InlineAuthorityReceiptContinuationScheduler :
        IAuthorityReceiptContinuationScheduler
    {
        private readonly object _gate = new object();
        private bool _running;
        private Action _pending;

        public bool TrySchedule(Action continuation)
        {
            if (continuation == null)
                return false;

            lock (_gate)
            {
                if (_running)
                {
                    if (_pending != null)
                        return false;
                    _pending = continuation;
                    return true;
                }

                _running = true;
            }

            Action current = continuation;
            try
            {
                while (current != null)
                {
                    current();
                    lock (_gate)
                    {
                        current = _pending;
                        _pending = null;
                        if (current == null)
                            _running = false;
                    }
                }

                return true;
            }
            catch
            {
                lock (_gate)
                {
                    _pending = null;
                    _running = false;
                }

                throw;
            }
        }
    }

    internal enum AuthorityPublicationReservationStatus
    {
        Unavailable = 0,
        Reserved = 1,
        Backpressure = 2,
        SequenceExhausted = 3
    }

    internal enum AuthorityCoordinatorRetirementStatus
    {
        Unavailable = 0,
        Retired = 1,
        Busy = 2,
        PublicationPending = 3
    }

    internal sealed class ProcessAuthorityMutationCoordinator
    {
        private static readonly ProcessAuthorityMutationCoordinator Shared =
            new ProcessAuthorityMutationCoordinator();

        private readonly object _stateGate = new object();
        private readonly Queue<ReceiptDispatchItem> _receiptQueue =
            new Queue<ReceiptDispatchItem>(
                SaveAuthorityTechnicalLimits.ReceiptCapacity);
        private readonly SortedSet<string> _diagnosticCodes =
            new SortedSet<string>(StringComparer.Ordinal);
        private readonly IAuthorityReceiptContinuationScheduler _scheduler;
        private readonly Action _beforeDispatcherRelease;
        private readonly Action _afterSubscriberLease;

        private object _owner;
        private AuthorityEpochAllocator _epochAllocator;
        private int _mutationActive;
        private int _reservedReceiptSlots;
        private ulong _publicationSequence;
        private bool _dispatcherActive;
        private bool _dispatchRequested;
        private bool _continuationScheduled;
        private bool _subscriberActive;

        internal ProcessAuthorityMutationCoordinator(
            ulong initialPublicationSequence = 0,
            IAuthorityReceiptContinuationScheduler scheduler = null,
            Action beforeDispatcherRelease = null,
            Action afterSubscriberLease = null)
        {
            _publicationSequence = initialPublicationSequence;
            _scheduler = scheduler ??
                         new InlineAuthorityReceiptContinuationScheduler();
            _beforeDispatcherRelease = beforeDispatcherRelease;
            _afterSubscriberLease = afterSubscriberLease;
        }

        internal static ProcessAuthorityMutationCoordinator ProcessLocal =>
            Shared;

        internal int PendingReceiptCount
        {
            get
            {
                lock (_stateGate)
                {
                    return _receiptQueue.Count;
                }
            }
        }

        internal IReadOnlyList<string> DiagnosticCodes
        {
            get
            {
                lock (_stateGate)
                {
                    return new ReadOnlyCollection<string>(
                        _diagnosticCodes.ToArray());
                }
            }
        }

        internal bool TryRegister(
            object owner,
            AuthorityEpochAllocator epochAllocator)
        {
            if (owner == null || epochAllocator == null)
                return false;
            lock (_stateGate)
            {
                if (_owner != null ||
                    Volatile.Read(ref _mutationActive) != 0 ||
                    _reservedReceiptSlots != 0 ||
                    _receiptQueue.Count != 0 ||
                    _dispatcherActive ||
                    _continuationScheduled ||
                    _subscriberActive)
                {
                    return false;
                }

                if (_epochAllocator != null &&
                    !ReferenceEquals(_epochAllocator, epochAllocator))
                {
                    return false;
                }

                _epochAllocator = epochAllocator;
                _owner = owner;
                return true;
            }
        }

        internal bool TryEnter(object owner)
        {
            lock (_stateGate)
            {
                if (!ReferenceEquals(_owner, owner))
                    return false;
                if (Volatile.Read(ref _mutationActive) != 0 ||
                    _subscriberActive)
                {
                    return false;
                }

                Volatile.Write(ref _mutationActive, 1);
                return true;
            }
        }

        internal void Exit()
        {
            Interlocked.Exchange(ref _mutationActive, 0);

            bool schedule = false;
            lock (_stateGate)
            {
                if (_dispatchRequested &&
                    _receiptQueue.Count > 0 &&
                    !_dispatcherActive &&
                    !_continuationScheduled &&
                    !_subscriberActive)
                {
                    _continuationScheduled = true;
                    _dispatchRequested = false;
                    schedule = true;
                }
                else if (_dispatchRequested &&
                         _receiptQueue.Count == 0 &&
                         !_dispatcherActive &&
                         !_continuationScheduled &&
                         !_subscriberActive)
                {
                    _dispatchRequested = false;
                }
            }

            if (schedule)
                TryScheduleContinuation();
        }

        internal AuthorityPublicationReservationStatus TryReserve(
            object owner,
            out ulong sequence)
        {
            lock (_stateGate)
            {
                sequence = 0;
                if (!ReferenceEquals(_owner, owner))
                    return AuthorityPublicationReservationStatus.Unavailable;
                if (_receiptQueue.Count + _reservedReceiptSlots >=
                    SaveAuthorityTechnicalLimits.ReceiptCapacity)
                {
                    return AuthorityPublicationReservationStatus.Backpressure;
                }

                if (_publicationSequence == ulong.MaxValue)
                {
                    return AuthorityPublicationReservationStatus
                        .SequenceExhausted;
                }

                _reservedReceiptSlots++;
                _publicationSequence++;
                sequence = _publicationSequence;
                return AuthorityPublicationReservationStatus.Reserved;
            }
        }

        internal void ReleaseReservation(object owner)
        {
            lock (_stateGate)
            {
                if (ReferenceEquals(_owner, owner) &&
                    _reservedReceiptSlots > 0)
                {
                    _reservedReceiptSlots--;
                }
            }
        }

        internal bool PublishReserved(
            object owner,
            ProfileMutationReceipt receipt,
            IProfileMutationReceiptSink sink)
        {
            lock (_stateGate)
            {
                if (!ReferenceEquals(_owner, owner) ||
                    _reservedReceiptSlots <= 0 ||
                    receipt == null ||
                    _receiptQueue.Count >=
                    SaveAuthorityTechnicalLimits.ReceiptCapacity)
                {
                    return false;
                }

                _receiptQueue.Enqueue(
                    new ReceiptDispatchItem(receipt, sink));
                _reservedReceiptSlots--;
                return true;
            }
        }

        internal AuthorityCoordinatorRetirementStatus TryRetire(
            object owner)
        {
            lock (_stateGate)
            {
                if (!ReferenceEquals(_owner, owner))
                {
                    return AuthorityCoordinatorRetirementStatus.Unavailable;
                }

                if (_reservedReceiptSlots != 0 ||
                    _receiptQueue.Count != 0 ||
                    _dispatcherActive ||
                    _continuationScheduled ||
                    _subscriberActive)
                {
                    return AuthorityCoordinatorRetirementStatus
                        .PublicationPending;
                }

                _owner = null;
                return AuthorityCoordinatorRetirementStatus.Retired;
            }
        }

        internal bool HasPendingPublication(object owner)
        {
            lock (_stateGate)
            {
                return !ReferenceEquals(_owner, owner) ||
                       _reservedReceiptSlots != 0 ||
                       _receiptQueue.Count != 0 ||
                       _dispatcherActive ||
                       _continuationScheduled ||
                       _subscriberActive;
            }
        }

        internal void RequestDrain()
        {
            bool schedule = false;
            lock (_stateGate)
            {
                _dispatchRequested = true;
                if (Volatile.Read(ref _mutationActive) == 0 &&
                    _receiptQueue.Count > 0 &&
                    !_dispatcherActive &&
                    !_continuationScheduled &&
                    !_subscriberActive)
                {
                    _continuationScheduled = true;
                    _dispatchRequested = false;
                    schedule = true;
                }
                else if (Volatile.Read(ref _mutationActive) == 0 &&
                         _receiptQueue.Count == 0 &&
                         !_dispatcherActive &&
                         !_continuationScheduled &&
                         !_subscriberActive)
                {
                    _dispatchRequested = false;
                }
            }

            if (schedule)
                TryScheduleContinuation();
        }

        internal void RecordDiagnostic(string diagnostic)
        {
            if (!SaveAuthorityValidation.IsDiagnosticCode(diagnostic))
                return;
            lock (_stateGate)
            {
                if (_diagnosticCodes.Count <
                    SaveAuthorityTechnicalLimits.MaximumDiagnosticCodes)
                {
                    _diagnosticCodes.Add(diagnostic);
                }
            }
        }

        private void DrainOneBatch()
        {
            int batchCount;
            lock (_stateGate)
            {
                batchCount = Math.Min(
                    _receiptQueue.Count,
                    SaveAuthorityTechnicalLimits.ReceiptCapacity);
            }

            for (int delivered = 0; delivered < batchCount; delivered++)
            {
                ReceiptDispatchItem item;
                lock (_stateGate)
                {
                    if (Volatile.Read(ref _mutationActive) != 0 ||
                        _subscriberActive)
                    {
                        _dispatchRequested = _receiptQueue.Count > 0;
                        break;
                    }

                    if (_receiptQueue.Count == 0)
                        break;
                    item = _receiptQueue.Peek();
                    _subscriberActive = true;
                }

                try
                {
                    _afterSubscriberLease?.Invoke();
                    item.Sink?.Publish(item.Receipt);
                }
                catch
                {
                    RecordDiagnostic(
                        SaveAuthorityDiagnosticCodes.ReceiptSinkThrew);
                }
                finally
                {
                    lock (_stateGate)
                    {
                        _subscriberActive = false;
                        if (_receiptQueue.Count > 0 &&
                            ReferenceEquals(_receiptQueue.Peek(), item))
                        {
                            _receiptQueue.Dequeue();
                        }
                    }
                }
            }

            try
            {
                _beforeDispatcherRelease?.Invoke();
            }
            catch
            {
                RecordDiagnostic(
                    SaveAuthorityDiagnosticCodes.ReceiptSinkThrew);
            }

            bool scheduleContinuation = false;
            lock (_stateGate)
            {
                _dispatcherActive = false;
                if (_dispatchRequested &&
                    Volatile.Read(ref _mutationActive) == 0 &&
                    !_continuationScheduled &&
                    !_subscriberActive &&
                    _receiptQueue.Count > 0)
                {
                    _continuationScheduled = true;
                    _dispatchRequested = false;
                    scheduleContinuation = true;
                }
                else if (_receiptQueue.Count == 0 &&
                         Volatile.Read(ref _mutationActive) == 0)
                {
                    _dispatchRequested = false;
                }
            }

            if (scheduleContinuation)
                TryScheduleContinuation();
        }

        private void TryScheduleContinuation()
        {
            bool scheduled;
            try
            {
                scheduled = _scheduler.TrySchedule(ContinueDrain);
            }
            catch
            {
                scheduled = false;
            }

            if (scheduled)
                return;

            RecordDiagnostic(
                SaveAuthorityDiagnosticCodes.ReceiptSchedulerUnavailable);
            bool runRetryBatch = false;
            lock (_stateGate)
            {
                bool retryArrived = _dispatchRequested;
                _continuationScheduled = false;
                _dispatchRequested = _receiptQueue.Count > 0;
                if (retryArrived &&
                    Volatile.Read(ref _mutationActive) == 0 &&
                    _receiptQueue.Count > 0 &&
                    !_dispatcherActive &&
                    !_subscriberActive)
                {
                    _dispatcherActive = true;
                    _dispatchRequested = false;
                    runRetryBatch = true;
                }
            }

            if (runRetryBatch)
                DrainOneBatch();
        }

        private void ContinueDrain()
        {
            lock (_stateGate)
            {
                _continuationScheduled = false;
                if (_dispatcherActive ||
                    _subscriberActive ||
                    Volatile.Read(ref _mutationActive) != 0 ||
                    _receiptQueue.Count == 0)
                {
                    _dispatchRequested = _receiptQueue.Count > 0;
                    return;
                }

                _dispatcherActive = true;
                _dispatchRequested = false;
            }

            DrainOneBatch();
        }

        private sealed class ReceiptDispatchItem
        {
            internal ReceiptDispatchItem(
                ProfileMutationReceipt receipt,
                IProfileMutationReceiptSink sink)
            {
                Receipt = receipt;
                Sink = sink;
            }

            internal ProfileMutationReceipt Receipt { get; }
            internal IProfileMutationReceiptSink Sink { get; }
        }
    }

    internal enum ProfileAuthorityTransitionStatus
    {
        Unavailable = 0,
        Published = 1,
        Busy = 2,
        Rejected = 3,
        PublicationPending = 4
    }

    internal sealed class ProfileAuthorityTransitionResult
    {
        internal ProfileAuthorityTransitionResult(
            ProfileAuthorityTransitionStatus status,
            ProfileWriteAuthoritySnapshot snapshot)
        {
            Status = status;
            Snapshot = snapshot;
        }

        internal ProfileAuthorityTransitionStatus Status { get; }
        internal ProfileWriteAuthoritySnapshot Snapshot { get; }
    }

    internal sealed class SerializedAuthorityMutationBoundary<TCandidate> :
        IProfileWriteAuthorityProvider
    {
        private const string InvalidRequestDiagnostic =
            "AL-SAVE-AUTH-REQUEST-INVALID";
        private const string BusyDiagnostic =
            "AL-SAVE-AUTH-MUTATION-BUSY";
        private const string NotWritableDiagnostic =
            "AL-SAVE-AUTH-NOT-WRITABLE";
        private const string StaleDiagnostic =
            "AL-SAVE-AUTH-STALE";
        private const string BackpressureDiagnostic =
            "AL-SAVE-AUTH-PUBLICATION-BACKPRESSURE";
        private const string SequenceDiagnostic =
            "AL-SAVE-AUTH-PUBLICATION-SEQUENCE";
        private const string EpochDiagnostic =
            "AL-SAVE-AUTH-EPOCH-UNAVAILABLE";
        private const string PreparationDiagnostic =
            "AL-SAVE-AUTH-PREPARATION-FAILED";
        private const string CandidateDiagnostic =
            "AL-SAVE-AUTH-CANDIDATE-INVALID";
        private const string PersistenceDiagnostic =
            "AL-SAVE-AUTH-PERSISTENCE-UNAVAILABLE";

        private readonly object _stateGate = new object();
        private readonly IProfileMutationCandidateAdapter<TCandidate> _adapter;
        private readonly IProfileMutationPersistence<TCandidate> _persistence;
        private readonly AuthorityEpochAllocator _epochAllocator;
        private readonly IProfileMutationReceiptSink _receiptSink;
        private readonly Action<ulong> _beforeDrainRequest;
        private readonly ProcessAuthorityMutationCoordinator _coordinator;

        private ProfileWriteAuthoritySnapshot _authority;
        private TCandidate _publishedCandidate;
        private bool _registered;

        internal SerializedAuthorityMutationBoundary(
            ProfileWriteAuthoritySnapshot initialAuthority,
            TCandidate initialCandidate,
            IProfileMutationCandidateAdapter<TCandidate> adapter,
            IProfileMutationPersistence<TCandidate> persistence,
            IProfileMutationReceiptSink receiptSink)
            : this(
                initialAuthority,
                initialCandidate,
                adapter,
                persistence,
                AuthorityEpochAllocator.ProcessLocal,
                receiptSink,
                null,
                ProcessAuthorityMutationCoordinator.ProcessLocal)
        {
        }

        internal static SerializedAuthorityMutationBoundary<TCandidate>
            CreateForTesting(
                ProfileWriteAuthoritySnapshot initialAuthority,
                TCandidate initialCandidate,
                IProfileMutationCandidateAdapter<TCandidate> adapter,
                IProfileMutationPersistence<TCandidate> persistence,
                AuthorityEpochAllocator epochAllocator,
                IProfileMutationReceiptSink receiptSink,
                Action<ulong> beforeDrainRequest,
                ProcessAuthorityMutationCoordinator testingCoordinator)
        {
            if (testingCoordinator == null)
                throw new ArgumentNullException(nameof(testingCoordinator));
            return new SerializedAuthorityMutationBoundary<TCandidate>(
                initialAuthority,
                initialCandidate,
                adapter,
                persistence,
                epochAllocator,
                receiptSink,
                beforeDrainRequest,
                testingCoordinator);
        }

        private SerializedAuthorityMutationBoundary(
            ProfileWriteAuthoritySnapshot initialAuthority,
            TCandidate initialCandidate,
            IProfileMutationCandidateAdapter<TCandidate> adapter,
            IProfileMutationPersistence<TCandidate> persistence,
            AuthorityEpochAllocator epochAllocator,
            IProfileMutationReceiptSink receiptSink,
            Action<ulong> beforeDrainRequest,
            ProcessAuthorityMutationCoordinator coordinator)
        {
            _adapter = adapter;
            _persistence = persistence;
            _epochAllocator = epochAllocator;
            _receiptSink = receiptSink;
            _beforeDrainRequest = beforeDrainRequest;
            _coordinator = coordinator;
            _authority =
                ProfileWriteAuthorityProviderGuard.ValidateOrUnavailable(
                    initialAuthority);

            if (_adapter == null ||
                _persistence == null ||
                _epochAllocator == null ||
                _receiptSink == null)
            {
                _authority = ProfileWriteAuthoritySnapshotFactory.Unavailable(
                    SaveAuthorityDiagnosticCodes.ProviderMissing);
                _publishedCandidate = default(TCandidate);
                return;
            }

            if (_authority.Status == ProfileWriteAuthorityStatus.Writable)
            {
                try
                {
                    _publishedCandidate = _adapter.Clone(initialCandidate);
                    ProfileCandidateValidationResult validation =
                        _adapter.ValidatePublished(
                            _publishedCandidate,
                            _authority.ProfileId,
                            _authority.VerifiedGenerationFingerprint);
                    if (validation == null || !validation.IsValid)
                    {
                        _authority =
                            ProfileWriteAuthoritySnapshotFactory.Unavailable(
                                SaveAuthorityDiagnosticCodes
                                    .ProviderInvariants);
                        _publishedCandidate = default(TCandidate);
                        return;
                    }
                }
                catch
                {
                    _authority =
                        ProfileWriteAuthoritySnapshotFactory.Unavailable(
                            SaveAuthorityDiagnosticCodes.ProviderInvariants);
                    _publishedCandidate = default(TCandidate);
                    return;
                }
            }
            else
            {
                _publishedCandidate = default(TCandidate);
            }

            _registered = _coordinator.TryRegister(
                this,
                _epochAllocator);
            if (!_registered)
            {
                _authority = ProfileWriteAuthoritySnapshotFactory.Unavailable(
                    "AL-SAVE-AUTH-COORDINATOR-BUSY");
                _publishedCandidate = default(TCandidate);
                return;
            }

            bool constructionSucceeded = false;
            try
            {
                if (_authority.Status == ProfileWriteAuthorityStatus.Writable)
                {
                    AuthorityEpochAllocationResult constructionEpoch =
                        _epochAllocator.Allocate();
                    if (constructionEpoch.Status !=
                            AuthorityEpochAllocationStatus.Allocated ||
                        !AuthorityEpochAllocator.IsStrictSuccessor(
                            _authority.AuthorityEpoch,
                            constructionEpoch.AuthorityEpoch))
                    {
                        _authority =
                            ProfileWriteAuthoritySnapshotFactory.Unavailable(
                                EpochDiagnostic);
                        _publishedCandidate = default(TCandidate);
                        return;
                    }

                    _authority = ProfileWriteAuthoritySnapshotFactory.Writable(
                        _authority.ProfileId,
                        constructionEpoch.AuthorityEpoch,
                        _authority.VerifiedGenerationFingerprint,
                        _authority.SelectedSourceGeneration,
                        _authority.DiagnosticCodes);
                }

                constructionSucceeded = true;
            }
            finally
            {
                if (!constructionSucceeded &&
                    _coordinator.TryRetire(this) ==
                    AuthorityCoordinatorRetirementStatus.Retired)
                {
                    _registered = false;
                }
            }
        }

        public ProfileWriteAuthoritySnapshot GetCurrentAuthority()
        {
            lock (_stateGate)
            {
                return _authority;
            }
        }

        internal int PendingReceiptCount
        {
            get => _coordinator.PendingReceiptCount;
        }

        internal IReadOnlyList<string> DispatcherDiagnosticCodes
        {
            get => _coordinator.DiagnosticCodes;
        }

        internal ProfileMutationResult TryMutate(
            ProfileAuthorityExpectation expectation,
            string operationId,
            string resultId,
            Func<TCandidate, ProfileCandidatePreparation> prepare)
        {
            if (!SaveAuthorityValidation.IsBoundedOpaqueIdentity(operationId) ||
                !SaveAuthorityValidation.IsBoundedOpaqueIdentity(resultId) ||
                prepare == null)
            {
                return Result(
                    ProfileMutationStatus.Unavailable,
                    null,
                    InvalidRequestDiagnostic);
            }

            if (!_registered)
            {
                return Result(
                    ProfileMutationStatus.NotWritable,
                    null,
                    NotWritableDiagnostic);
            }

            if (!_coordinator.TryEnter(this))
            {
                return Result(
                    ProfileMutationStatus.Busy,
                    null,
                    BusyDiagnostic);
            }

            bool shouldDrain = false;
            ulong drainSequence = 0;
            ProfileMutationResult result;
            try
            {
                result = ExecuteMutation(
                    expectation,
                    operationId,
                    resultId,
                    prepare,
                    out shouldDrain,
                    out drainSequence);
            }
            finally
            {
                _coordinator.Exit();
            }

            if (shouldDrain)
            {
                try
                {
                    _beforeDrainRequest?.Invoke(drainSequence);
                }
                catch
                {
                    RecordDispatcherDiagnostic(
                        SaveAuthorityDiagnosticCodes.ReceiptSinkThrew);
                }

                RequestReceiptDrain();
            }

            return result;
        }

        internal ProfileAuthorityTransitionResult TryRevokeAuthority(
            ProfileWriteAuthoritySnapshot replacement)
        {
            if (!_registered || !_coordinator.TryEnter(this))
            {
                return new ProfileAuthorityTransitionResult(
                    ProfileAuthorityTransitionStatus.Busy,
                    GetCurrentAuthority());
            }

            try
            {
                ProfileWriteAuthoritySnapshot guarded =
                    ProfileWriteAuthorityProviderGuard.ValidateOrUnavailable(
                        replacement);
                if (guarded.Status == ProfileWriteAuthorityStatus.Writable)
                {
                    return new ProfileAuthorityTransitionResult(
                        ProfileAuthorityTransitionStatus.Rejected,
                        GetCurrentAuthority());
                }

                if ((guarded.Status ==
                         ProfileWriteAuthorityStatus.Deleted ||
                     guarded.Status ==
                         ProfileWriteAuthorityStatus.MissingProfile) &&
                    _coordinator.HasPendingPublication(this))
                {
                    return new ProfileAuthorityTransitionResult(
                        ProfileAuthorityTransitionStatus.PublicationPending,
                        GetCurrentAuthority());
                }

                lock (_stateGate)
                {
                    _authority = guarded;
                    if (guarded.Status ==
                            ProfileWriteAuthorityStatus.Deleted ||
                        guarded.Status ==
                            ProfileWriteAuthorityStatus.MissingProfile)
                    {
                        _publishedCandidate = default(TCandidate);
                    }
                }

                return new ProfileAuthorityTransitionResult(
                    ProfileAuthorityTransitionStatus.Published,
                    guarded);
            }
            finally
            {
                _coordinator.Exit();
            }
        }

        internal AuthorityCoordinatorRetirementStatus TryRetire()
        {
            if (!_registered || !_coordinator.TryEnter(this))
                return AuthorityCoordinatorRetirementStatus.Busy;
            try
            {
                AuthorityCoordinatorRetirementStatus result =
                    _coordinator.TryRetire(this);
                if (result == AuthorityCoordinatorRetirementStatus.Retired)
                {
                    _registered = false;
                    lock (_stateGate)
                    {
                        _authority =
                            ProfileWriteAuthoritySnapshotFactory.Unavailable(
                                "AL-SAVE-AUTH-SERVICE-RETIRED");
                        _publishedCandidate = default(TCandidate);
                    }
                }

                return result;
            }
            finally
            {
                _coordinator.Exit();
            }
        }

        internal void RequestReceiptDrain()
        {
            _coordinator.RequestDrain();
        }

        private ProfileMutationResult ExecuteMutation(
            ProfileAuthorityExpectation expectation,
            string operationId,
            string resultId,
            Func<TCandidate, ProfileCandidatePreparation> prepare,
            out bool shouldDrain,
            out ulong drainSequence)
        {
            shouldDrain = false;
            drainSequence = 0;
            ProfileWriteAuthoritySnapshot current;
            TCandidate published;
            lock (_stateGate)
            {
                current = _authority;
                published = _publishedCandidate;
            }

            if (current.Status != ProfileWriteAuthorityStatus.Writable)
            {
                return Result(
                    ProfileMutationStatus.NotWritable,
                    null,
                    NotWritableDiagnostic);
            }

            if (!IsValidExpectation(expectation))
            {
                return Result(
                    ProfileMutationStatus.Unavailable,
                    null,
                    InvalidRequestDiagnostic);
            }

            if (!Matches(current, expectation))
            {
                return Result(
                    ProfileMutationStatus.StaleAuthority,
                    null,
                    StaleDiagnostic);
            }

            ProfilePersistenceAuthorityCheck initialCheck =
                RecheckPersistence(expectation);
            ProfileMutationResult checkFailure =
                MapAuthorityCheck(initialCheck);
            if (checkFailure != null)
                return checkFailure;

            if (!TryReserveSequence(
                    out ulong sequence,
                    out ProfileMutationResult reservationFailure))
            {
                return reservationFailure;
            }

            bool ownsReservation = true;
            try
            {
                AuthorityEpochAllocationResult epoch =
                    _epochAllocator.Allocate();
                if (epoch.Status != AuthorityEpochAllocationStatus.Allocated ||
                    !AuthorityEpochAllocator.IsStrictSuccessor(
                        current.AuthorityEpoch,
                        epoch.AuthorityEpoch))
                {
                    RevokeUnavailable(EpochDiagnostic);
                    return Result(
                        ProfileMutationStatus.EpochUnavailable,
                        null,
                        EpochDiagnostic);
                }

                var reservation = new ProfileMutationReservation(
                    sequence,
                    epoch.AuthorityEpoch);

                TCandidate callbackCandidate;
                try
                {
                    callbackCandidate = _adapter.Clone(published);
                }
                catch
                {
                    return Result(
                        ProfileMutationStatus.PreparationFailed,
                        null,
                        PreparationDiagnostic);
                }

                ProfileCandidatePreparation preparation;
                try
                {
                    preparation = prepare(callbackCandidate);
                }
                catch
                {
                    return Result(
                        ProfileMutationStatus.PreparationFailed,
                        null,
                        PreparationDiagnostic);
                }

                if (preparation == null)
                {
                    return Result(
                        ProfileMutationStatus.PreparationFailed,
                        null,
                        PreparationDiagnostic);
                }

                if (preparation.Status ==
                    ProfileCandidatePreparationStatus.Rejected)
                {
                    return Result(
                        ProfileMutationStatus.PreparationRejected,
                        null,
                        SafeDiagnostic(
                            preparation.DiagnosticCode,
                            PreparationDiagnostic));
                }

                if (preparation.Status ==
                    ProfileCandidatePreparationStatus.ExactReplay)
                {
                    ProfileMutationReplayVerification replay;
                    try
                    {
                        TCandidate replayCandidate =
                            _adapter.Clone(published);
                        ProfileCandidateValidationResult binding =
                            _adapter.ValidatePublished(
                                replayCandidate,
                                current.ProfileId,
                                current.VerifiedGenerationFingerprint);
                        if (binding == null || !binding.IsValid)
                        {
                            return Result(
                                ProfileMutationStatus.PreparationRejected,
                                null,
                                PreparationDiagnostic);
                        }

                        replay = _adapter.VerifyReplay(
                            replayCandidate,
                            current.ProfileId,
                            current.VerifiedGenerationFingerprint,
                            operationId,
                            resultId);
                    }
                    catch
                    {
                        return Result(
                            ProfileMutationStatus.PreparationRejected,
                            null,
                            PreparationDiagnostic);
                    }

                    if (!IsValidReplay(replay))
                    {
                        return Result(
                            ProfileMutationStatus.PreparationRejected,
                            null,
                            SafeDiagnostic(
                                replay?.DiagnosticCode,
                                PreparationDiagnostic));
                    }

                    ProfilePersistenceAuthorityCheck replayFinalCheck =
                        RecheckPersistence(expectation);
                    ProfileMutationResult replayCheckFailure =
                        MapAuthorityCheck(replayFinalCheck);
                    if (replayCheckFailure != null)
                        return replayCheckFailure;

                    return Result(
                        ProfileMutationStatus.AlreadyCommitted,
                        CreateReplayReceipt(
                            replay,
                            current,
                            reservation,
                            operationId,
                            resultId),
                        string.Empty);
                }

                if (preparation.Status !=
                    ProfileCandidatePreparationStatus.Prepared)
                {
                    return Result(
                        ProfileMutationStatus.PreparationFailed,
                        null,
                        PreparationDiagnostic);
                }

                TCandidate frozenCandidate;
                ProfileCandidateValidationResult validation;
                try
                {
                    frozenCandidate = _adapter.Clone(callbackCandidate);
                    validation = _adapter.Validate(
                        frozenCandidate,
                        current.ProfileId);
                }
                catch
                {
                    return Result(
                        ProfileMutationStatus.CandidateInvalid,
                        null,
                        CandidateDiagnostic);
                }

                if (validation == null || !validation.IsValid)
                {
                    return Result(
                        ProfileMutationStatus.CandidateInvalid,
                        null,
                        SafeDiagnostic(
                            validation?.DiagnosticCode,
                            CandidateDiagnostic));
                }

                ProfilePersistenceAuthorityCheck finalCheck =
                    RecheckPersistence(expectation);
                checkFailure = MapAuthorityCheck(finalCheck);
                if (checkFailure != null)
                    return checkFailure;

                ProfileCandidatePersistenceResult<TCandidate> persisted;
                try
                {
                    persisted = _persistence.PersistAndVerify(
                        frozenCandidate,
                        new ProfileMutationCommitContext(
                            current.ProfileId,
                            current.VerifiedGenerationFingerprint));
                }
                catch
                {
                    ProfileMutationResult uncertain = PublishUncertain(
                        current,
                        reservation,
                        operationId,
                        resultId,
                        SaveAuthorityDiagnosticCodes.CommitUncertain,
                        out ProfileMutationReceipt uncertainReceipt);
                    ownsReservation = false;
                    shouldDrain = true;
                    drainSequence =
                        uncertainReceipt.PublicationSequence;
                    return uncertain;
                }

                if (persisted == null)
                {
                    ProfileMutationResult uncertain = PublishUncertain(
                        current,
                        reservation,
                        operationId,
                        resultId,
                        SaveAuthorityDiagnosticCodes.CommitUncertain,
                        out ProfileMutationReceipt uncertainReceipt);
                    ownsReservation = false;
                    shouldDrain = true;
                    drainSequence =
                        uncertainReceipt.PublicationSequence;
                    return uncertain;
                }

                switch (persisted.Status)
                {
                    case ProfileCandidatePersistenceStatus.Committed:
                    {
                        ProfileMutationResult committed = PublishCommitted(
                            current,
                            persisted,
                            reservation,
                            operationId,
                            resultId,
                            out ProfileMutationReceipt receipt);
                        ownsReservation = false;
                        shouldDrain = true;
                        drainSequence = receipt.PublicationSequence;
                        return committed;
                    }
                    case ProfileCandidatePersistenceStatus
                        .RejectedBeforeMutation:
                        return Result(
                            ProfileMutationStatus.PersistenceRejected,
                            null,
                            SafeDiagnostic(
                                persisted.DiagnosticCode,
                                PersistenceDiagnostic));
                    case ProfileCandidatePersistenceStatus.VerifiedRollback:
                    {
                        ProfileMutationResult rollback = PublishRollback(
                            current,
                            reservation,
                            operationId,
                            resultId,
                            persisted.DiagnosticCode,
                            out ProfileMutationReceipt receipt);
                        ownsReservation = false;
                        shouldDrain = true;
                        drainSequence = receipt.PublicationSequence;
                        return rollback;
                    }
                    case ProfileCandidatePersistenceStatus.CommitUncertain:
                    default:
                    {
                        ProfileMutationResult uncertain = PublishUncertain(
                            current,
                            reservation,
                            operationId,
                            resultId,
                            persisted.DiagnosticCode,
                            out ProfileMutationReceipt receipt);
                        ownsReservation = false;
                        shouldDrain = true;
                        drainSequence = receipt.PublicationSequence;
                        return uncertain;
                    }
                }
            }
            finally
            {
                if (ownsReservation)
                    ReleaseReservation();
            }
        }

        private ProfileMutationResult PublishCommitted(
            ProfileWriteAuthoritySnapshot current,
            ProfileCandidatePersistenceResult<TCandidate> persisted,
            ProfileMutationReservation reservation,
            string operationId,
            string resultId,
            out ProfileMutationReceipt receipt)
        {
            TCandidate committedCandidate;
            try
            {
                if (!SaveAuthorityValidation.IsCanonicalSha256(
                        persisted.CommittedGenerationFingerprint) ||
                    !SaveAuthorityValidation.IsCanonicalSha256(
                        persisted.CommittedPayloadFingerprint) ||
                    !SaveAuthorityValidation.IsSourceCoherent(
                        true,
                        persisted.CommittedSourceGeneration))
                {
                    return PublishUncertain(
                        current,
                        reservation,
                        operationId,
                        resultId,
                        SaveAuthorityDiagnosticCodes.CommitUncertain,
                        out receipt);
                }

                committedCandidate =
                    _adapter.Clone(persisted.CommittedCandidate);
                ProfileCandidateValidationResult validation =
                    _adapter.ValidatePublished(
                        committedCandidate,
                        current.ProfileId,
                        persisted.CommittedGenerationFingerprint);
                if (validation == null || !validation.IsValid)
                {
                    return PublishUncertain(
                        current,
                        reservation,
                        operationId,
                        resultId,
                        SaveAuthorityDiagnosticCodes.CommitUncertain,
                        out receipt);
                }
            }
            catch
            {
                return PublishUncertain(
                    current,
                    reservation,
                    operationId,
                    resultId,
                    SaveAuthorityDiagnosticCodes.CommitUncertain,
                    out receipt);
            }

            ProfileWriteAuthoritySnapshot committedAuthority =
                ProfileWriteAuthoritySnapshotFactory.Writable(
                    current.ProfileId,
                    reservation.ReservedAuthorityEpoch,
                    persisted.CommittedGenerationFingerprint,
                    persisted.CommittedSourceGeneration,
                    Array.Empty<string>());
            if (committedAuthority.Status !=
                ProfileWriteAuthorityStatus.Writable)
            {
                return PublishUncertain(
                    current,
                    reservation,
                    operationId,
                    resultId,
                    SaveAuthorityDiagnosticCodes.CommitUncertain,
                    out receipt);
            }

            receipt = new ProfileMutationReceipt(
                ProfileMutationReceiptStatus.Committed,
                reservation.PublicationSequence,
                current.ProfileId,
                current.VerifiedGenerationFingerprint,
                persisted.CommittedGenerationFingerprint,
                reservation.ReservedAuthorityEpoch,
                operationId,
                resultId,
                persisted.CommittedPayloadFingerprint,
                true,
                Array.Empty<string>());
            lock (_stateGate)
            {
                _publishedCandidate = committedCandidate;
                _authority = committedAuthority;
                PublishReservedReceipt(receipt);
            }

            return Result(
                ProfileMutationStatus.Committed,
                receipt,
                string.Empty);
        }

        private ProfileMutationResult PublishRollback(
            ProfileWriteAuthoritySnapshot current,
            ProfileMutationReservation reservation,
            string operationId,
            string resultId,
            string diagnostic,
            out ProfileMutationReceipt receipt)
        {
            receipt = new ProfileMutationReceipt(
                ProfileMutationReceiptStatus.VerifiedRollback,
                reservation.PublicationSequence,
                current.ProfileId,
                current.VerifiedGenerationFingerprint,
                string.Empty,
                string.Empty,
                operationId,
                resultId,
                string.Empty,
                true,
                new[]
                {
                    SafeDiagnostic(
                        diagnostic,
                        "AL-SAVE-AUTH-VERIFIED-ROLLBACK")
                });
            lock (_stateGate)
            {
                _authority = current;
                PublishReservedReceipt(receipt);
            }

            return Result(
                ProfileMutationStatus.VerifiedRollback,
                receipt,
                receipt.DiagnosticCodes[0]);
        }

        private ProfileMutationResult PublishUncertain(
            ProfileWriteAuthoritySnapshot current,
            ProfileMutationReservation reservation,
            string operationId,
            string resultId,
            string diagnostic,
            out ProfileMutationReceipt receipt)
        {
            string safeDiagnostic = SafeDiagnostic(
                diagnostic,
                SaveAuthorityDiagnosticCodes.CommitUncertain);
            ProfileWriteAuthoritySnapshot uncertain =
                ProfileWriteAuthoritySnapshotFactory.NonWritable(
                    ProfileWriteAuthorityStatus.CommitUncertain,
                    Math.Max(0, current.SaveSchemaVersion),
                    Math.Max(0, current.ProfileInitializationVersion),
                    false,
                    ProfileAuthoritySourceGeneration.None,
                    new[] { safeDiagnostic });
            receipt = new ProfileMutationReceipt(
                ProfileMutationReceiptStatus.CommitUncertain,
                reservation.PublicationSequence,
                current.ProfileId,
                current.VerifiedGenerationFingerprint,
                string.Empty,
                string.Empty,
                operationId,
                resultId,
                string.Empty,
                true,
                new[] { safeDiagnostic });
            lock (_stateGate)
            {
                _authority = uncertain;
                PublishReservedReceipt(receipt);
            }

            return Result(
                ProfileMutationStatus.CommitUncertain,
                receipt,
                safeDiagnostic);
        }

        private void PublishReservedReceipt(ProfileMutationReceipt receipt)
        {
            if (!_coordinator.PublishReserved(
                    this,
                    receipt,
                    _receiptSink))
            {
                throw new InvalidOperationException(
                    "The reserved authority receipt slot was lost.");
            }
        }

        private bool TryReserveSequence(
            out ulong sequence,
            out ProfileMutationResult failure)
        {
            sequence = 0;
            failure = null;
            AuthorityPublicationReservationStatus status =
                _coordinator.TryReserve(this, out sequence);
            switch (status)
            {
                case AuthorityPublicationReservationStatus.Reserved:
                    return true;
                case AuthorityPublicationReservationStatus.Backpressure:
                    failure = Result(
                        ProfileMutationStatus.PublicationBackpressure,
                        null,
                        BackpressureDiagnostic);
                    return false;
                case AuthorityPublicationReservationStatus.SequenceExhausted:
                    RevokeUnavailable(SequenceDiagnostic);
                    failure = Result(
                        ProfileMutationStatus.PublicationUnavailable,
                        null,
                        SequenceDiagnostic);
                    return false;
                default:
                    failure = Result(
                        ProfileMutationStatus.Unavailable,
                        null,
                        SequenceDiagnostic);
                    return false;
            }
        }

        private void ReleaseReservation()
        {
            _coordinator.ReleaseReservation(this);
        }

        private ProfilePersistenceAuthorityCheck RecheckPersistence(
            ProfileAuthorityExpectation expectation)
        {
            ProfileWriteAuthoritySnapshot current;
            lock (_stateGate)
            {
                current = _authority;
            }
            if (current.Status != ProfileWriteAuthorityStatus.Writable ||
                !IsValidExpectation(expectation) ||
                !Matches(current, expectation))
            {
                return ProfilePersistenceAuthorityCheck.Stale();
            }

            try
            {
                return _persistence.RecheckAuthority(
                    new ProfileMutationCommitContext(
                        expectation.ProfileId,
                        expectation.ExpectedGenerationFingerprint));
            }
            catch
            {
                return ProfilePersistenceAuthorityCheck.Unavailable(
                    PersistenceDiagnostic);
            }
        }

        private ProfileMutationResult MapAuthorityCheck(
            ProfilePersistenceAuthorityCheck check)
        {
            if (check == null)
            {
                RevokeUnavailable(PersistenceDiagnostic);
                return Result(
                    ProfileMutationStatus.Unavailable,
                    null,
                    PersistenceDiagnostic);
            }

            switch (check.Status)
            {
                case ProfilePersistenceAuthorityStatus.Current:
                    return null;
                case ProfilePersistenceAuthorityStatus.Stale:
                    RevokeUnavailable(StaleDiagnostic);
                    return Result(
                        ProfileMutationStatus.StaleAuthority,
                        null,
                        StaleDiagnostic);
                default:
                    string diagnostic = SafeDiagnostic(
                        check.DiagnosticCode,
                        PersistenceDiagnostic);
                    RevokeUnavailable(diagnostic);
                    return Result(
                        ProfileMutationStatus.Unavailable,
                        null,
                        diagnostic);
            }
        }

        private static bool IsValidExpectation(
            ProfileAuthorityExpectation expectation) =>
            expectation != null &&
            SaveAuthorityValidation.IsCanonicalProfileId(
                expectation.ProfileId) &&
            AuthorityEpochAllocator.IsCanonical(
                expectation.AuthorityEpoch) &&
            SaveAuthorityValidation.IsCanonicalSha256(
                expectation.ExpectedGenerationFingerprint);

        private static bool Matches(
            ProfileWriteAuthoritySnapshot current,
            ProfileAuthorityExpectation expectation) =>
            string.Equals(
                current.ProfileId,
                expectation.ProfileId,
                StringComparison.Ordinal) &&
            string.Equals(
                current.AuthorityEpoch,
                expectation.AuthorityEpoch,
                StringComparison.Ordinal) &&
            string.Equals(
                current.VerifiedGenerationFingerprint,
                expectation.ExpectedGenerationFingerprint,
                StringComparison.Ordinal);

        private static bool IsValidReplay(
            ProfileMutationReplayVerification verification) =>
            verification != null &&
            verification.IsVerified &&
            verification.DiagnosticCode.Length == 0 &&
            SaveAuthorityValidation.IsCanonicalSha256(
                verification.ExpectedGenerationFingerprint) &&
            SaveAuthorityValidation.IsCanonicalSha256(
                verification.CommittedPayloadFingerprint);

        private static ProfileMutationReceipt CreateReplayReceipt(
            ProfileMutationReplayVerification verification,
            ProfileWriteAuthoritySnapshot current,
            ProfileMutationReservation reservation,
            string operationId,
            string resultId) =>
            new ProfileMutationReceipt(
                ProfileMutationReceiptStatus.Committed,
                reservation.PublicationSequence,
                current.ProfileId,
                verification.ExpectedGenerationFingerprint,
                current.VerifiedGenerationFingerprint,
                current.AuthorityEpoch,
                operationId,
                resultId,
                verification.CommittedPayloadFingerprint,
                true,
                Array.Empty<string>());

        private void RevokeUnavailable(string diagnostic)
        {
            lock (_stateGate)
            {
                _authority = ProfileWriteAuthoritySnapshotFactory.Unavailable(
                    SafeDiagnostic(
                        diagnostic,
                        SaveAuthorityDiagnosticCodes.ProviderInvariants));
                _publishedCandidate = default(TCandidate);
            }
        }

        private static string SafeDiagnostic(
            string candidate,
            string fallback) =>
            SaveAuthorityValidation.IsDiagnosticCode(candidate)
                ? candidate
                : fallback;

        private void RecordDispatcherDiagnostic(string diagnostic)
        {
            _coordinator.RecordDiagnostic(diagnostic);
        }

        private static ProfileMutationResult Result(
            ProfileMutationStatus status,
            ProfileMutationReceipt receipt,
            string diagnostic) =>
            new ProfileMutationResult(status, receipt, diagnostic);
    }
}
