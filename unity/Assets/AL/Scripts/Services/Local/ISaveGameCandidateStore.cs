using System;
using System.Runtime.CompilerServices;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.Narrative.Nvs01;
using AL.Narrative.Nvs01.Contracts;
using AL.RealmSelection;

[assembly: InternalsVisibleTo("AL.Nvs01.Persistence.Tests")]

namespace AL.Services.Local
{
    internal enum SaveCandidateMutationDisposition
    {
        Prepared = 0,
        Duplicate = 1,
        Rejected = 2
    }

    internal sealed class SaveCandidateMutationPreparation
    {
        private SaveCandidateMutationPreparation(
            SaveCandidateMutationDisposition disposition,
            string message)
        {
            Disposition = disposition;
            Message = message ?? string.Empty;
        }

        internal SaveCandidateMutationDisposition Disposition { get; }
        internal string Message { get; }

        internal static SaveCandidateMutationPreparation Prepared() =>
            new SaveCandidateMutationPreparation(
                SaveCandidateMutationDisposition.Prepared,
                string.Empty);

        internal static SaveCandidateMutationPreparation Duplicate() =>
            new SaveCandidateMutationPreparation(
                SaveCandidateMutationDisposition.Duplicate,
                string.Empty);

        internal static SaveCandidateMutationPreparation Rejected(string message) =>
            new SaveCandidateMutationPreparation(
                SaveCandidateMutationDisposition.Rejected,
                message);
    }

    internal enum SaveCandidateCommitOutcome
    {
        Committed = 0,
        Duplicate = 1,
        Rejected = 2,
        PreviousPreserved = 3,
        CommitUncertain = 4,
        ReadOnly = 5
    }

    internal sealed class SaveCandidateCommitResult
    {
        internal SaveCandidateCommitResult(
            SaveCandidateCommitOutcome outcome,
            SaveGameData publishedSave,
            string message)
        {
            Outcome = outcome;
            PublishedSave = publishedSave;
            Message = message ?? string.Empty;
        }

        internal SaveCandidateCommitOutcome Outcome { get; }
        internal SaveGameData PublishedSave { get; }
        internal string Message { get; }
        internal bool IsCommitted =>
            Outcome == SaveCandidateCommitOutcome.Committed ||
            Outcome == SaveCandidateCommitOutcome.Duplicate;
    }

    internal sealed class ProfileBoundSaveCandidateCommitResult
    {
        internal ProfileBoundSaveCandidateCommitResult(
            SaveCandidateCommitResult commitResult,
            ProfileMutationReceipt authorityReceipt)
        {
            CommitResult = commitResult;
            AuthorityReceipt = authorityReceipt;
        }

        internal SaveCandidateCommitResult CommitResult { get; }
        internal ProfileMutationReceipt AuthorityReceipt { get; }
    }

    internal interface ISaveGameCandidateStore
    {
        SaveCandidateCommitResult TryCommitCandidate(
            Func<SaveGameData, SaveCandidateMutationPreparation> prepareCandidate);
    }

    /// <summary>
    /// The only schema-v1 realm/bootstrap mutation entry point. It carries no
    /// caller-provided mutation callback.
    /// </summary>
    internal interface ILegacyRealmSelectionCandidateStore
    {
        RealmSelectionResult TryCommitLegacyRealmSelection(
            RealmSelectionRequest request);
    }

    /// <summary>
    /// Schema-v1 3D-first MVP loop mutation entry point. It carries no
    /// caller-provided mutation callback.
    /// </summary>
    internal interface ILegacyMvpLoopCandidateStore
    {
        SaveCandidateCommitResult TryCommitLegacyMvpLoop(MvpLoopCommitRequest request);
    }

    /// <summary>
    /// Schema-v1 private-kingdom teaching mutation entry point. The typed
    /// request can advance only one ordered catalog step in SaveGameData.Quests.
    /// </summary>
    internal interface ILegacyKingdomTeachingCandidateStore
    {
        SaveCandidateCommitResult TryCommitLegacyKingdomTeaching(
            KingdomTeachingCommitRequest request);
    }

    /// <summary>
    /// Schema-v1 first-world progression mutation entry point. The typed
    /// request can advance exactly one tutorial or Proof command; arbitrary
    /// save mutation remains unavailable to gameplay callers.
    /// </summary>
    internal interface ILegacyFirstWorldProgressCandidateStore
    {
        SaveCandidateCommitResult TryCommitLegacyFirstWorldProgress(
            FirstWorldProgressCommitRequest request);
    }

    /// <summary>
    /// The only schema-v1 narrative mutation entry point. The complete typed
    /// NVS-01 plan and verified catalog are interpreted inside the save root;
    /// callers cannot supply an arbitrary save mutation callback.
    /// </summary>
    internal interface INvs01LegacyCandidateStore
    {
        SaveCandidateCommitResult TryCommitNvs01LegacyCandidate(
            Nvs01MutationPlan plan,
            Nvs01VerifiedCatalog verifiedCatalog);
    }

    /// <summary>
    /// Post-migration candidate boundary. Implementations must recheck the
    /// complete expectation immediately before persistence, keep ProfileId
    /// unchanged before/after the callback and on duplicate replay, and only
    /// return a published candidate from that same profile. For an exact
    /// replay, AuthorityReceipt must be minted by the serialized authority
    /// boundary from trusted mutation-receipt/generation-history verification,
    /// never synthesized from the callback candidate.
    /// Schema-v1 stores intentionally do not implement this contract.
    /// </summary>
    internal interface IProfileBoundSaveGameCandidateStore :
        ISaveGameCandidateStore,
        IProfileWriteAuthorityProvider
    {
        ProfileBoundSaveCandidateCommitResult TryCommitCandidate(
            ProfileAuthorityExpectation expectation,
            string operationId,
            string resultId,
            Func<SaveGameData, SaveCandidateMutationPreparation>
                prepareCandidate);
    }
}
