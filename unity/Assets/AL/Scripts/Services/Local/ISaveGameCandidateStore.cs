using System;
using AL.Data.Runtime;

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

    internal interface ISaveGameCandidateStore
    {
        SaveCandidateCommitResult TryCommitCandidate(
            Func<SaveGameData, SaveCandidateMutationPreparation> prepareCandidate);
    }
}
