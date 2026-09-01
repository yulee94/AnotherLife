using AL.ChampionMode.Quests;
using AL.Core.Interfaces;
using AL.Data.Runtime;

namespace AL.Services.Local
{
    public sealed class FirstWorldProgressCommitResult
    {
        internal FirstWorldProgressCommitResult(
            bool accepted,
            bool persisted,
            FirstWorldProgressSnapshot snapshot,
            string message)
        {
            Accepted = accepted;
            Persisted = persisted;
            Snapshot = snapshot;
            Message = message ?? string.Empty;
        }

        public bool Accepted { get; }
        public bool Persisted { get; }
        public FirstWorldProgressSnapshot Snapshot { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Typed schema-v1 mutation boundary for first-world tutorial and Proof
    /// progression. Generic save mutation remains contained by the save root.
    /// </summary>
    public static class FirstWorldProgressSaveAuthority
    {
        public static bool CanCommit(ISaveGameService saveGameService) =>
            saveGameService?.CurrentSave != null &&
            saveGameService is ILegacyFirstWorldProgressCandidateStore;

        public static bool TryRead(
            ISaveGameService saveGameService,
            out FirstWorldProgressSnapshot snapshot,
            out string message)
        {
            if (saveGameService?.CurrentSave == null)
            {
                snapshot = null;
                message = "AL-FIRST-WORLD-PROFILE-READ-ONLY";
                return false;
            }

            return FirstWorldProgressSaveCodec.TryRead(
                saveGameService.CurrentSave,
                out snapshot,
                out message);
        }

        public static FirstWorldProgressCommitResult TryAdvanceTutorial(
            ISaveGameService saveGameService,
            FirstWorldProgressSnapshot expected,
            FirstWorldTutorialProgressCommand command,
            bool blockTaught = false)
        {
            return TryCommit(
                saveGameService,
                expected,
                command,
                blockTaught,
                ProofOfWorthCommand.Invalid);
        }

        public static FirstWorldProgressCommitResult TryAdvanceProof(
            ISaveGameService saveGameService,
            FirstWorldProgressSnapshot expected,
            ProofOfWorthCommand command)
        {
            if (expected?.Proof == null)
            {
                return Rejected(expected, "AL-FIRST-WORLD-PROOF-UNAVAILABLE");
            }

            ProofOfWorthTransition preview =
                ProofOfWorthPlanner.Apply(expected.Proof, command);
            if (!preview.Changed)
            {
                return preview.Status == ProofOfWorthStatus.DuplicateIgnored
                    ? new FirstWorldProgressCommitResult(
                        true,
                        false,
                        expected,
                        "AL-FIRST-WORLD-PROOF-DUPLICATE")
                    : Rejected(expected, "AL-FIRST-WORLD-PROOF-REJECTED");
            }

            return TryCommit(
                saveGameService,
                expected,
                FirstWorldTutorialProgressCommand.Invalid,
                blockTaught: false,
                proofCommand: command);
        }

        private static FirstWorldProgressCommitResult TryCommit(
            ISaveGameService saveGameService,
            FirstWorldProgressSnapshot expected,
            FirstWorldTutorialProgressCommand tutorialCommand,
            bool blockTaught,
            ProofOfWorthCommand proofCommand)
        {
            if (saveGameService?.CurrentSave == null ||
                expected == null ||
                !(saveGameService is ILegacyFirstWorldProgressCandidateStore store))
            {
                return Rejected(expected, "AL-FIRST-WORLD-PROFILE-READ-ONLY");
            }

            string operationId = FirstWorldProgressSaveCodec.BuildOperationId(
                expected,
                tutorialCommand,
                blockTaught,
                proofCommand);
            if (string.IsNullOrEmpty(operationId))
            {
                return Rejected(expected, "AL-FIRST-WORLD-OPERATION-INVALID");
            }

            var request = new FirstWorldProgressCommitRequest(
                operationId,
                operationId,
                expected,
                tutorialCommand,
                blockTaught,
                proofCommand);
            SaveCandidateCommitResult commit =
                store.TryCommitLegacyFirstWorldProgress(request);
            if (commit == null || !commit.IsCommitted)
            {
                return Rejected(
                    expected,
                    commit?.Message ?? "AL-FIRST-WORLD-PROFILE-READ-ONLY");
            }

            if (!FirstWorldProgressSaveCodec.TryRead(
                    commit.PublishedSave,
                    out FirstWorldProgressSnapshot published,
                    out string readMessage))
            {
                return Rejected(
                    expected,
                    string.IsNullOrWhiteSpace(readMessage)
                        ? "AL-FIRST-WORLD-PUBLISHED-STATE-INVALID"
                        : readMessage);
            }

            return new FirstWorldProgressCommitResult(
                true,
                commit.Outcome == SaveCandidateCommitOutcome.Committed,
                published,
                commit.Message);
        }

        private static FirstWorldProgressCommitResult Rejected(
            FirstWorldProgressSnapshot snapshot,
            string message)
        {
            return new FirstWorldProgressCommitResult(
                false,
                false,
                snapshot,
                message);
        }
    }
}
