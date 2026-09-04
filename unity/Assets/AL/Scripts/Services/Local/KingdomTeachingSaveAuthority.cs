using System;
using AL.ChampionMode.Quests;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.UI.Kingdom;
using AL.Core.SaveAuthority;

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
    /// Typed write boundary for the post-lordship private-kingdom teaching quest.
    /// Schema-v2 uses the profile-bound save root; schema-v1 retains its legacy
    /// typed boundary. The catalog chooses the current stable step and event,
    /// and either save root accepts only a one-step ordered transition.
    /// </summary>
    public static class KingdomTeachingSaveAuthority
    {
        public static KingdomTeachingCommitResult TryAdvance(
            ISaveGameService saveGameService,
            KingdomTeachingCatalog catalog,
            string observedCompletionEvent)
        {
            if (saveGameService?.CurrentSave == null || catalog == null)
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

            var request = new KingdomTeachingCommitRequest(
                Guid.NewGuid().ToString("N"),
                save.SelectedRealm,
                catalog.QuestId,
                state.CurrentStep.Id,
                state.CurrentStep.CompletionEvent,
                state.ProgressValue,
                state.ProgressValue + 1,
                catalog.Steps.Count);
            SaveCandidateCommitResult commit;
            if (save.SaveSchemaVersion ==
                    SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion &&
                saveGameService is IProfileBoundKingdomTeachingCandidateStore bound)
            {
                commit = bound.TryCommitProfileBoundKingdomTeaching(request);
            }
            else if (save.SaveSchemaVersion ==
                         SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion &&
                     saveGameService is ILegacyKingdomTeachingCandidateStore legacy)
            {
                commit = legacy.TryCommitLegacyKingdomTeaching(request);
            }
            else
            {
                return Rejected("AL-KINGDOM-TEACHING-PROFILE-READ-ONLY");
            }

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
