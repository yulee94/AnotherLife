using System;
using System.IO;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.Narrative.Nvs01;
using AL.Narrative.Nvs01.Contracts;
using AL.RealmSelection;
using AL.ChampionMode.Death;

namespace AL.Services.Local
{
    /// <summary>
    /// Approval-only save authority. LocalSaveGameService remains the sole typed
    /// mutation policy, while this wrapper makes one registry-envelope commit per
    /// complete service/candidate operation.
    /// </summary>
    internal sealed class MvpApprovalTransactionalSaveGameService :
        ISaveGameService,
        ISaveLoadDispositionProvider,
        ISaveOperationDispositionProvider,
        ISaveGameCandidateStore,
        ILegacyRealmSelectionCandidateStore,
        IProfileBoundRealmSelectionCandidateStore,
        IProfileBoundDeathPenaltyCandidateStore,
        IProfileBoundWishgateCandidateStore,
        ILegacyMvpLoopCandidateStore,
        ILegacyFirstWorldProgressCandidateStore,
        ILegacyKingdomTeachingCandidateStore,
        INvs01LegacyCandidateStore,
        IProfileWriteAuthorityProvider
    {
        private readonly MvpApprovalVirtualStore _store;
        private readonly LocalSaveGameService _inner;
        private volatile bool _commitUncertain;
        private string _commitUncertainMessage = string.Empty;

        internal MvpApprovalTransactionalSaveGameService(
            MvpApprovalVirtualStore store,
            LocalSaveGameService inner)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        internal LocalSaveGameService InnerService => _inner;
        internal bool PersistenceFrozen => _commitUncertain;

        public SaveGameData CurrentSave => _inner.CurrentSave;
        public SaveLoadStatus LastLoadStatus => _inner.LastLoadStatus;
        public string LastLoadMessage => _inner.LastLoadMessage;
        public SaveOperationStatus LastSaveStatus =>
            _commitUncertain
                ? SaveOperationStatus.CommitUncertain
                : _inner.LastSaveStatus;
        public string LastSaveMessage =>
            _commitUncertain
                ? _commitUncertainMessage
                : _inner.LastSaveMessage;
        public SaveLoadDisposition LastLoadDisposition =>
            ((ISaveLoadDispositionProvider)_inner).LastLoadDisposition;
        public SaveGameData ReadOnlyCandidateSnapshot =>
            ((ISaveLoadDispositionProvider)_inner).ReadOnlyCandidateSnapshot;
        public SaveOperationDisposition LastSaveDisposition =>
            _commitUncertain
                ? new SaveOperationDisposition(
                    SaveOperationStatus.CommitUncertain,
                    mayHaveMutated: true,
                    candidatePrimaryVerified: false,
                    requiredBackupVerified: false,
                    previousAuthorityVerified: false,
                    cleanupVerified: false,
                    rollbackAttempted: false,
                    rollbackVerified: false,
                    diagnosticCodes: new[]
                    {
                        "AL-MVP-APPROVAL-REGISTRY-COMMIT-UNCERTAIN"
                    })
                : ((ISaveOperationDispositionProvider)_inner).LastSaveDisposition;

        public void Save() => Execute(() => _inner.Save(), ResolveSaveMutation);

        internal void PersistLifecycleCheckpoint() =>
            Execute(() => _inner.PersistLifecycleCheckpoint(), ResolveSaveMutation);

        public void Load() => Execute(() => _inner.Load(), ResolveLoad);

        public bool HasSave() => Execute(
            () => _inner.HasSave(),
            _ => InnerCommitUncertain
                ? TransactionResolution.RollbackAndFreeze
                : TransactionResolution.Rollback);

        public void CreateNewSave(AL.Core.RealmId realmId) =>
            Execute(
                () => _inner.CreateNewSave(realmId),
                () => ResolveCreateNewSave(realmId));

        public void DeleteSave() => Execute(
            () => _inner.DeleteSave(),
            ResolveDeleteSave);

        internal T ExecuteReset<T>(Func<LocalSaveGameService, T> operation) =>
            Execute(
                () => operation(_inner),
                ResolveReset);

        SaveCandidateCommitResult ISaveGameCandidateStore.TryCommitCandidate(
            Func<SaveGameData, SaveCandidateMutationPreparation> prepareCandidate) =>
            Execute(
                () => ((ISaveGameCandidateStore)_inner)
                    .TryCommitCandidate(prepareCandidate),
                ResolveCandidateCommit);

        RealmSelectionResult
            ILegacyRealmSelectionCandidateStore.TryCommitLegacyRealmSelection(
                RealmSelectionRequest request) =>
            Execute(
                () => ((ILegacyRealmSelectionCandidateStore)_inner)
                    .TryCommitLegacyRealmSelection(request),
                ResolveRealmSelection);

        RealmSelectionResult
            IProfileBoundRealmSelectionCandidateStore
            .TryCommitProfileBoundRealmSelection(
                RealmSelectionRequest request) =>
            Execute(
                () => ((IProfileBoundRealmSelectionCandidateStore)_inner)
                    .TryCommitProfileBoundRealmSelection(request),
                ResolveRealmSelection);

        DeathPenaltyCommitResult
            IProfileBoundDeathPenaltyCandidateStore
            .TryCommitProfileBoundDeathPenalty(
                DeathPenaltyCommitRequest request) =>
            Execute(
                () => ((IProfileBoundDeathPenaltyCandidateStore)_inner)
                    .TryCommitProfileBoundDeathPenalty(request),
                ResolveDeathPenalty);

        WishgateCommitResult
            IProfileBoundWishgateCandidateStore
            .TryCommitProfileBoundWishgate(
                WishgateCommitRequest request,
                WishgateDurableDependencies dependencies) =>
            Execute(
                () => ((IProfileBoundWishgateCandidateStore)_inner)
                    .TryCommitProfileBoundWishgate(request, dependencies),
                ResolveWishgate);

        SaveCandidateCommitResult
            ILegacyMvpLoopCandidateStore.TryCommitLegacyMvpLoop(
                MvpLoopCommitRequest request) =>
            Execute(
                () => ((ILegacyMvpLoopCandidateStore)_inner)
                    .TryCommitLegacyMvpLoop(request),
                ResolveCandidateCommit);

        SaveCandidateCommitResult
            ILegacyFirstWorldProgressCandidateStore
                .TryCommitLegacyFirstWorldProgress(
                    FirstWorldProgressCommitRequest request) =>
            Execute(
                () => ((ILegacyFirstWorldProgressCandidateStore)_inner)
                    .TryCommitLegacyFirstWorldProgress(request),
                ResolveCandidateCommit);

        SaveCandidateCommitResult
            ILegacyKingdomTeachingCandidateStore.TryCommitLegacyKingdomTeaching(
                KingdomTeachingCommitRequest request) =>
            Execute(
                () => ((ILegacyKingdomTeachingCandidateStore)_inner)
                    .TryCommitLegacyKingdomTeaching(request),
                ResolveCandidateCommit);

        SaveCandidateCommitResult
            INvs01LegacyCandidateStore.TryCommitNvs01LegacyCandidate(
                Nvs01MutationPlan plan,
                Nvs01VerifiedCatalog verifiedCatalog) =>
            Execute(
                () => ((INvs01LegacyCandidateStore)_inner)
                    .TryCommitNvs01LegacyCandidate(plan, verifiedCatalog),
                ResolveCandidateCommit);

        ProfileWriteAuthoritySnapshot
            IProfileWriteAuthorityProvider.GetCurrentAuthority() =>
            Execute(
                () => ((IProfileWriteAuthorityProvider)_inner).GetCurrentAuthority(),
                _ => InnerCommitUncertain
                    ? TransactionResolution.RollbackAndFreeze
                    : TransactionResolution.Rollback);

        private void Execute(
            Action operation,
            Func<TransactionResolution> resolve)
        {
            Execute(
                () =>
                {
                    operation();
                    return true;
                },
                _ => resolve());
        }

        private T Execute<T>(
            Func<T> operation,
            Func<T, TransactionResolution> resolve)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }
            if (resolve == null)
            {
                throw new ArgumentNullException(nameof(resolve));
            }

            if (_commitUncertain)
            {
                throw new InvalidOperationException(
                    "Approval persistence is frozen after an uncertain registry commit.");
            }

            if (!_store.TryBeginTransaction(
                    out MvpApprovalVirtualStore.ApprovalTransaction transaction,
                    out string failure))
            {
                throw new IOException(failure);
            }

            using (transaction)
            {
                if (_commitUncertain)
                {
                    throw new InvalidOperationException(
                        "Approval persistence is frozen after an uncertain registry commit.");
                }

                T result;
                try
                {
                    result = operation();
                }
                catch (Exception operationException)
                {
                    try
                    {
                        transaction.Rollback();
                        _inner.Load();
                        if (!RollbackReconstructionSucceeded())
                        {
                            throw new IOException(
                                "Approval rollback reload did not restore writable live save authority.");
                        }
                    }
                    catch (Exception rollbackException)
                    {
                        _commitUncertainMessage =
                            "AL-MVP-APPROVAL-ROLLBACK-RESTORE-FAILED: " +
                            rollbackException.GetType().Name;
                        _commitUncertain = true;
                        throw new InvalidOperationException(
                            "Approval operation rollback could not restore live save state.",
                            new AggregateException(operationException, rollbackException));
                    }

                    throw;
                }

                TransactionResolution resolution = resolve(result);
                if (resolution != TransactionResolution.Commit)
                {
                    transaction.Rollback();
                    if (resolution == TransactionResolution.RollbackAndFreeze ||
                        resolution == TransactionResolution.RollbackFreezeAndThrow)
                    {
                        FreezeFromInnerDisposition();
                    }

                    if (resolution == TransactionResolution.RollbackAndThrow ||
                        resolution == TransactionResolution.RollbackFreezeAndThrow)
                    {
                        throw new InvalidOperationException(
                            "Approval transaction did not reach a verified commit outcome.");
                    }

                    return result;
                }

                try
                {
                    transaction.Commit();
                }
                catch (Exception exception)
                {
                    _commitUncertainMessage =
                        "AL-MVP-APPROVAL-REGISTRY-COMMIT-UNCERTAIN: " +
                        exception.GetType().Name;
                    _commitUncertain = true;
                    throw;
                }

                return result;
            }
        }

        private TransactionResolution ResolveSaveMutation()
        {
            if (InnerCommitUncertain)
            {
                return TransactionResolution.RollbackAndFreeze;
            }

            if (_inner.LastSaveStatus != SaveOperationStatus.SavedPrimary)
            {
                return TransactionResolution.Rollback;
            }

            return SaveCommitVerified
                ? TransactionResolution.Commit
                : TransactionResolution.RollbackAndFreeze;
        }

        private TransactionResolution ResolveLoad()
        {
            if (InnerCommitUncertain)
            {
                return TransactionResolution.RollbackAndFreeze;
            }

            return LoadIsVerifiedWritable
                ? TransactionResolution.Commit
                : TransactionResolution.Rollback;
        }

        private TransactionResolution ResolveCreateNewSave(AL.Core.RealmId realmId)
        {
            if (InnerCommitUncertain)
            {
                return TransactionResolution.RollbackAndFreeze;
            }

            if (_inner.CurrentSave == null ||
                _inner.CurrentSave.SelectedRealm != realmId ||
                realmId == AL.Core.RealmId.None)
            {
                return TransactionResolution.Rollback;
            }

            return ResolveSaveMutation();
        }

        private TransactionResolution ResolveDeleteSave()
        {
            if (InnerCommitUncertain)
            {
                return TransactionResolution.RollbackAndFreeze;
            }

            return _inner.CurrentSave == null &&
                   _inner.LastSaveStatus == SaveOperationStatus.None &&
                   !_inner.HasSave()
                ? TransactionResolution.Commit
                : TransactionResolution.Rollback;
        }

        private TransactionResolution ResolveCandidateCommit(
            SaveCandidateCommitResult result)
        {
            if (InnerCommitUncertain ||
                result.Outcome == SaveCandidateCommitOutcome.CommitUncertain)
            {
                return TransactionResolution.RollbackAndFreeze;
            }

            if (result.Outcome != SaveCandidateCommitOutcome.Committed)
            {
                return TransactionResolution.Rollback;
            }

            return SaveCommitVerified
                ? TransactionResolution.Commit
                : TransactionResolution.RollbackAndFreeze;
        }

        private TransactionResolution ResolveRealmSelection(RealmSelectionResult result)
        {
            if (InnerCommitUncertain ||
                result.Status == RealmSelectionStatus.CommitUncertain)
            {
                return TransactionResolution.RollbackAndFreeze;
            }

            if (result.Status == RealmSelectionStatus.AlreadyCommittedSameRealm)
            {
                return result.Persisted && result.MutationOccurred && SaveCommitVerified
                    ? TransactionResolution.Commit
                    : TransactionResolution.Rollback;
            }

            if (result.Status != RealmSelectionStatus.Committed)
            {
                return TransactionResolution.Rollback;
            }

            return result.Persisted && result.MutationOccurred && SaveCommitVerified
                ? TransactionResolution.Commit
                : TransactionResolution.RollbackAndFreeze;
        }

        private TransactionResolution ResolveDeathPenalty(DeathPenaltyCommitResult result)
        {
            if (InnerCommitUncertain ||
                result.Status == DeathPenaltyCommitStatus.RejectedSaveUncertain)
            {
                return TransactionResolution.RollbackAndFreeze;
            }

            if (result.Status == DeathPenaltyCommitStatus.ReplayedBelowMax ||
                result.Status == DeathPenaltyCommitStatus.ReplayedOathmarkPaymentRequired)
            {
                return result.Persisted && result.MutationOccurred && SaveCommitVerified
                    ? TransactionResolution.Commit
                    : TransactionResolution.Rollback;
            }

            if (result.Status != DeathPenaltyCommitStatus.CommittedBelowMax &&
                result.Status != DeathPenaltyCommitStatus.OathmarkPaymentRequired)
            {
                return TransactionResolution.Rollback;
            }

            return result.Persisted && result.MutationOccurred && SaveCommitVerified
                ? TransactionResolution.Commit
                : TransactionResolution.RollbackAndFreeze;
        }

        private TransactionResolution ResolveWishgate(WishgateCommitResult result)
        {
            if (InnerCommitUncertain ||
                result.Status == WishgateCommitStatus.RejectedSaveUncertain ||
                result.Status == WishgateCommitStatus.RecoveryRequired)
            {
                return TransactionResolution.RollbackAndFreeze;
            }

            if (result.Status == WishgateCommitStatus.Replayed ||
                result.Status == WishgateCommitStatus.NoChange)
            {
                return result.Persisted && result.MutationOccurred && SaveCommitVerified
                    ? TransactionResolution.Commit
                    : TransactionResolution.Rollback;
            }

            if (result.Status != WishgateCommitStatus.Committed)
            {
                return TransactionResolution.Rollback;
            }

            return result.Persisted && result.MutationOccurred && SaveCommitVerified
                ? TransactionResolution.Commit
                : TransactionResolution.RollbackAndFreeze;
        }

        private TransactionResolution ResolveReset<T>(T result)
        {
            if (!(result is MvpApprovalStartNewDisposition disposition))
            {
                return TransactionResolution.RollbackAndThrow;
            }

            if (disposition == MvpApprovalStartNewDisposition.Failed)
            {
                return TransactionResolution.Rollback;
            }

            if (InnerCommitUncertain)
            {
                return TransactionResolution.RollbackFreezeAndThrow;
            }

            if (disposition == MvpApprovalStartNewDisposition.ReloadBootRequired)
            {
                return _inner.CurrentSave == null && !_inner.HasSave()
                    ? TransactionResolution.Commit
                    : TransactionResolution.RollbackAndThrow;
            }

            return _inner.LastLoadStatus == SaveLoadStatus.CreatedNew &&
                   LoadIsVerifiedWritable
                ? TransactionResolution.Commit
                : TransactionResolution.RollbackAndThrow;
        }

        private bool LoadIsVerifiedWritable
        {
            get
            {
                SaveLoadDisposition disposition =
                    ((ISaveLoadDispositionProvider)_inner).LastLoadDisposition;
                if (_inner.CurrentSave == null ||
                    disposition == null ||
                    !disposition.IsRuntimeUsable ||
                    !disposition.IsWritable)
                {
                    return false;
                }

                switch (_inner.LastLoadStatus)
                {
                    case SaveLoadStatus.LoadedPrimary:
                    case SaveLoadStatus.RecoveredFromBackup:
                    case SaveLoadStatus.CreatedNew:
                    case SaveLoadStatus.LoadedPrimaryNormalized:
                    case SaveLoadStatus.LoadedPrimaryWithPreservedUnknown:
                    case SaveLoadStatus.MigratedSchemaOne:
                        return true;
                    default:
                        return false;
                }
            }
        }

        private bool SaveCommitVerified
        {
            get
            {
                SaveOperationDisposition disposition =
                    ((ISaveOperationDispositionProvider)_inner).LastSaveDisposition;
                return disposition != null &&
                       disposition.Status == SaveOperationStatus.SavedPrimary &&
                       disposition.CandidatePrimaryVerified &&
                       disposition.RequiredBackupVerified &&
                       disposition.CleanupVerified;
            }
        }

        private bool InnerCommitUncertain
        {
            get
            {
                SaveOperationDisposition disposition =
                    ((ISaveOperationDispositionProvider)_inner).LastSaveDisposition;
                return _inner.LastSaveStatus == SaveOperationStatus.CommitUncertain ||
                       disposition?.Status == SaveOperationStatus.CommitUncertain;
            }
        }

        private void FreezeFromInnerDisposition()
        {
            _commitUncertainMessage = string.IsNullOrWhiteSpace(_inner.LastSaveMessage)
                ? "AL-MVP-APPROVAL-INNER-COMMIT-UNCERTAIN"
                : _inner.LastSaveMessage;
            _commitUncertain = true;
        }

        private enum TransactionResolution
        {
            Commit = 0,
            Rollback = 1,
            RollbackAndFreeze = 2,
            RollbackAndThrow = 3,
            RollbackFreezeAndThrow = 4
        }

        private bool RollbackReconstructionSucceeded()
        {
            SaveLoadDisposition disposition =
                ((ISaveLoadDispositionProvider)_inner).LastLoadDisposition;
            if (_inner.CurrentSave == null ||
                disposition == null ||
                !disposition.IsRuntimeUsable ||
                !disposition.IsWritable)
            {
                return false;
            }

            switch (_inner.LastLoadStatus)
            {
                case SaveLoadStatus.LoadedPrimary:
                case SaveLoadStatus.RecoveredFromBackup:
                case SaveLoadStatus.CreatedNew:
                case SaveLoadStatus.LoadedPrimaryNormalized:
                case SaveLoadStatus.LoadedPrimaryWithPreservedUnknown:
                case SaveLoadStatus.MigratedSchemaOne:
                    break;
                default:
                    return false;
            }

            ProfileWriteAuthoritySnapshot authority =
                ((IProfileWriteAuthorityProvider)_inner).GetCurrentAuthority();
            return authority != null &&
                   (authority.Status == ProfileWriteAuthorityStatus.Writable ||
                    authority.Status == ProfileWriteAuthorityStatus.MigrationRequired);
        }
    }
}
