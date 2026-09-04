using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
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
    /// Typed boundary for the 3D-first loop. Schema-v2 admits only first-session
    /// identity and earned lordship; general writes remain contained.
    /// </summary>
    public static class MvpLoopSaveAuthority
    {
        public static MvpLoopCommitResult TryCommit(
            ISaveGameService saveGameService,
            MvpLoopCommitRequest request)
        {
            SaveCandidateCommitResult commit;
            if (saveGameService?.CurrentSave?.SaveSchemaVersion ==
                    SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion &&
                saveGameService is IProfileBoundFirstSessionCandidateStore bound)
            {
                commit = bound.TryCommitFirstSessionIdentity(request);
            }
            else if (saveGameService?.CurrentSave?.SaveSchemaVersion ==
                         SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion &&
                     saveGameService is ILegacyMvpLoopCandidateStore legacy)
            {
                commit = legacy.TryCommitLegacyMvpLoop(request);
            }
            else
            {
                return new MvpLoopCommitResult(
                    false,
                    false,
                    "AL-MVP-LOOP-PROFILE-READ-ONLY");
            }

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
