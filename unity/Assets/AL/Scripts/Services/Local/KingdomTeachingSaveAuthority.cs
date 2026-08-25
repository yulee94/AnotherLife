using System;
using AL.ChampionMode.Quests;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.UI.Kingdom;

namespace AL.Services.Local
{
    public sealed class KingdomTeachingCommitResult
    {
        internal KingdomTeachingCommitResult(
            bool accepted,
            bool persisted,
            string message)
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
    /// Typed schema-v1 write boundary for the post-lordship private-kingdom
    /// teaching quest. The catalog chooses the current stable step and event;
    /// the save root accepts only a one-step ordered transition.
    /// </summary>
    public static class KingdomTeachingSaveAuthority
    {
        public static KingdomTeachingCommitResult TryAdvance(
            ISaveGameService saveGameService,
            KingdomTeachingCatalog catalog,
            string observedCompletionEvent)
        {
            if (saveGameService?.CurrentSave == null ||
                catalog == null ||
                !(saveGameService is ILegacyKingdomTeachingCandidateStore store))
            {
                return Rejected("AL-KINGDOM-TEACHING-PROFILE-READ-ONLY");
            }

            SaveGameData save = saveGameService.CurrentSave;
            KingdomTeachingState state = KingdomTeachingQuestline.Evaluate(save, catalog);
            if (!state.IsAvailable || state.IsComplete || state.CurrentStep == null)
            {
                return Rejected("AL-KINGDOM-TEACHING-STATE-UNAVAILABLE");
            }

            if (!string.Equals(
                    state.CurrentStep.CompletionEvent,
                    observedCompletionEvent,
                    StringComparison.Ordinal))
            {
                return Rejected("AL-KINGDOM-TEACHING-ORDER-CONFLICT");
            }

            SaveCandidateCommitResult commit = store.TryCommitLegacyKingdomTeaching(
                new KingdomTeachingCommitRequest(
                    Guid.NewGuid().ToString("N"),
                    save.SelectedRealm,
                    catalog.QuestId,
                    state.CurrentStep.Id,
                    state.CurrentStep.CompletionEvent,
                    state.ProgressValue,
                    state.ProgressValue + 1,
                    catalog.Steps.Count));
            if (commit == null)
            {
                return Rejected("AL-KINGDOM-TEACHING-PROFILE-READ-ONLY");
            }

            bool accepted = commit.Outcome == SaveCandidateCommitOutcome.Committed ||
                            commit.Outcome == SaveCandidateCommitOutcome.Duplicate;
            return new KingdomTeachingCommitResult(
                accepted,
                commit.Outcome == SaveCandidateCommitOutcome.Committed,
                commit.Message);
        }

        private static KingdomTeachingCommitResult Rejected(string message)
        {
            return new KingdomTeachingCommitResult(false, false, message);
        }
    }
}
