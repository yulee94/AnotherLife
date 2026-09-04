using System;
using AL.ChampionMode.Quests;
using AL.Core;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.RealmSelection;
using AL.UI.CharacterCreation;

namespace AL.Services.Local
{
    public sealed partial class LocalSaveGameService : IProfileBoundFirstSessionCandidateStore
    {
        SaveCandidateCommitResult IProfileBoundFirstSessionCandidateStore
            .TryCommitFirstSessionProgress(FirstWorldProgressCommitRequest request)
        {
            if (request.Expected == null || string.IsNullOrEmpty(request.Expected.ProfileId))
            {
                return LegacyCandidateRejected("AL-FIRST-WORLD-PROFILE-READ-ONLY");
            }

            return TryCommitFirstSessionCandidate(
                request.Expected.Realm,
                request.Expected.ProfileId,
                LegacyFirstWorldProgressOperationId,
                request.TransactionId,
                candidate =>
                {
                    FirstWorldProgressPrepareDisposition disposition =
                        FirstWorldProgressSaveCodec.PrepareCandidate(
                            candidate, request, out _, out string message);
                    switch (disposition)
                    {
                        case FirstWorldProgressPrepareDisposition.Prepared:
                            return SaveCandidateMutationPreparation.Prepared();
                        case FirstWorldProgressPrepareDisposition.Duplicate:
                            return SaveCandidateMutationPreparation.Duplicate();
                        default:
                            return SaveCandidateMutationPreparation.Rejected(message);
                    }
                });
        }

        SaveCandidateCommitResult IProfileBoundFirstSessionCandidateStore
            .TryCommitFirstSessionIdentity(MvpLoopCommitRequest request)
        {
            return TryCommitFirstSessionCandidate(
                request.ExpectedRealm,
                _currentSave?.ProfileId,
                LegacyMvpLoopOperationId,
                request.TransactionId,
                candidate =>
                {
                    if (!AllowsFirstSessionIdentity(candidate, request))
                    {
                        return SaveCandidateMutationPreparation.Rejected(
                            "AL-FIRST-SESSION-IDENTITY-SCOPE-REJECTED");
                    }

                    MvpLoopPrepareDisposition disposition =
                        MvpLoopSaveCodec.PrepareCandidate(candidate, request, out string message);
                    switch (disposition)
                    {
                        case MvpLoopPrepareDisposition.Prepared:
                            return SaveCandidateMutationPreparation.Prepared();
                        case MvpLoopPrepareDisposition.Duplicate:
                            return SaveCandidateMutationPreparation.Duplicate();
                        default:
                            return SaveCandidateMutationPreparation.Rejected(message);
                    }
                });
        }

        private SaveCandidateCommitResult TryCommitFirstSessionCandidate(
            RealmId realm,
            string profileId,
            string operationId,
            string transactionId,
            Func<SaveGameData, SaveCandidateMutationPreparation> prepare)
        {
            if (!RealmSelectionAuthority.IsBoundedIdentity(transactionId) ||
                !TryEnterLegacyCandidateCoordinator(operationId))
            {
                return LegacyCandidateRejected("AL-FIRST-SESSION-TRANSACTION-INVALID-OR-BUSY");
            }

            try
            {
                ProfileWriteAuthoritySnapshot authority = GetCurrentAuthority();
                if (!HasExactSchemaTwoProfile(_currentSave) ||
                    authority == null || authority.Status != ProfileWriteAuthorityStatus.Writable ||
                    authority.SelectedSourceGeneration != ProfileAuthoritySourceGeneration.Primary ||
                    !string.Equals(authority.ProfileId, profileId, StringComparison.Ordinal) ||
                    realm == RealmId.None || _currentSave.SelectedRealm != realm)
                {
                    return LegacyCandidateRejected("AL-FIRST-SESSION-PROFILE-READ-ONLY");
                }

                ProfileBoundSaveCandidateCommitResult bound =
                    ((IProfileBoundSaveGameCandidateStore)this).TryCommitCandidate(
                        ProfileAuthorityExpectation.From(authority),
                        operationId,
                        transactionId,
                        candidate =>
                        {
                            if (!HasExactSchemaTwoMetadata(candidate) || candidate.SelectedRealm != realm)
                            {
                                return SaveCandidateMutationPreparation.Rejected(
                                    "AL-FIRST-SESSION-AUTHORITY-CONFLICT");
                            }

                            SaveCandidateMutationPreparation preparation = prepare(candidate);
                            return HasExactSchemaTwoMetadata(candidate) && candidate.SelectedRealm == realm
                                ? preparation
                                : SaveCandidateMutationPreparation.Rejected(
                                    "AL-FIRST-SESSION-AUTHORITY-CONFLICT");
                        });
                return bound?.CommitResult ??
                    LegacyCandidateRejected("AL-FIRST-SESSION-COMMIT-UNAVAILABLE");
            }
            finally
            {
                ExitLegacyCandidateCoordinator();
            }
        }

        private static bool AllowsFirstSessionIdentity(
            SaveGameData candidate, MvpLoopCommitRequest request)
        {
            MvpLoopSnapshot current = MvpLoopSaveCodec.Read(candidate);
            if (!string.IsNullOrEmpty(request.BuildingId))
            {
                // A caller may carry an existing building through, never create or upgrade it.
                if (candidate.Buildings == null || !candidate.Buildings.Exists(building =>
                        building != null && building.BuildingId == request.BuildingId &&
                        building.Level == request.BuildingLevel))
                {
                    return false;
                }
            }
            else if (request.BuildingLevel != 0)
            {
                return false;
            }

            if (!MvpLoopSaveCodec.TryNormalizeUsername(request.Username, out string username))
            {
                return false;
            }

            if (current.IdentityConfirmed)
            {
                if (!request.ConfirmIdentity || current.ClassFamily != request.ClassFamily ||
                    (!string.IsNullOrEmpty(username) && username != current.Username) ||
                    (request.Appearance != null &&
                     !CharacterCreationLook.Matches(candidate.ChampionCustomization, request.Appearance)))
                {
                    return false;
                }
            }
            else if (request.ConfirmIdentity && string.IsNullOrEmpty(username))
            {
                return false;
            }

            if (string.IsNullOrEmpty(request.LastResultId) || request.LastResultId == current.LastResultId)
            {
                return true;
            }

            return current.HasConfirmedChampion &&
                request.LastResultId == ProofOfWorthLordship.ResolveMarkId(candidate.SelectedRealm) &&
                FirstWorldProgressSaveCodec.TryRead(candidate, out FirstWorldProgressSnapshot progress, out _) &&
                progress.Proof?.Phase == ProofOfWorthPhase.C1AcceptMark;
        }
    }
}
