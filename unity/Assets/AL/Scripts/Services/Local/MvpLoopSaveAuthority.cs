using AL.Core.Interfaces;
using AL.Data.Runtime;

namespace AL.Services.Local
{
    public sealed class MvpLoopCommitResult
    {
        internal MvpLoopCommitResult(bool accepted, bool persisted, string message)
        {
            Accepted = accepted;
            Persisted = persisted;
            Message = message ?? string.Empty;
        }

        public bool Accepted { get; }
        public bool Persisted { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Production write boundary for the 3D-first MVP loop. Schema-v1 accepts
    /// this typed adapter only; generic Save() stays contained.
    /// </summary>
    public static class MvpLoopSaveAuthority
    {
        public static MvpLoopCommitResult TryCommit(
            ISaveGameService saveGameService,
            MvpLoopCommitRequest request)
        {
            if (!(saveGameService is ILegacyMvpLoopCandidateStore store))
            {
                return new MvpLoopCommitResult(
                    false,
                    false,
                    "AL-MVP-LOOP-PROFILE-READ-ONLY");
            }

            SaveCandidateCommitResult commit = store.TryCommitLegacyMvpLoop(request);
            if (commit == null)
            {
                return new MvpLoopCommitResult(
                    false,
                    false,
                    "AL-MVP-LOOP-PROFILE-READ-ONLY");
            }

            bool accepted = commit.Outcome == SaveCandidateCommitOutcome.Committed ||
                            commit.Outcome == SaveCandidateCommitOutcome.Duplicate;
            bool persisted = commit.Outcome == SaveCandidateCommitOutcome.Committed;
            return new MvpLoopCommitResult(accepted, persisted, commit.Message);
        }
    }
}
