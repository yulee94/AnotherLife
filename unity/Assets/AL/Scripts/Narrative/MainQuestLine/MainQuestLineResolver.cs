using System;
using AL.ChampionMode.Quests;
using AL.Data.Runtime;

namespace AL.Narrative.MainQuestLine
{
    public static class MainQuestLineResolver
    {
        public static bool TryResolve(
            MainQuestLineCatalog catalog,
            Nvs01ProgressData nvs01Progress,
            FirstWorldProgressData firstWorldProgress,
            out MainQuestLineProgress progress,
            out MainQuestLineDiagnostic diagnostic)
        {
            progress = null;
            diagnostic = null;
            if (catalog == null)
            {
                diagnostic = new MainQuestLineDiagnostic(
                    MainQuestLineContract.DiagnosticPrefix + "CATALOG-MISSING",
                    "Cannot resolve a chapter without a verified runtime catalog.",
                    MainQuestLineContract.CatalogId,
                    "null");
                return false;
            }

            string questId = MainQuestLineContract.EntryQuestId;
            string stateId = "OFFERED";
            if (IsOmenCompleted(nvs01Progress) || IsProofActive(firstWorldProgress))
            {
                questId = MainQuestLineContract.ProofQuestId;
                stateId = firstWorldProgress != null &&
                          !string.IsNullOrEmpty(firstWorldProgress.ProofQuestStateId)
                    ? firstWorldProgress.ProofQuestStateId
                    : "OFFERED";
            }
            else if (nvs01Progress != null &&
                     nvs01Progress.Version != 0 &&
                     !string.IsNullOrEmpty(nvs01Progress.StateId))
            {
                stateId = nvs01Progress.StateId;
            }

            MainQuestLineChapter chapter;
            if (!catalog.TryGetChapterByQuest(questId, out chapter))
            {
                diagnostic = new MainQuestLineDiagnostic(
                    MainQuestLineContract.DiagnosticPrefix + "DEPENDENCY-MISSING",
                    "Resolved quest is not present in the runtime catalog.",
                    questId,
                    "missing");
                return false;
            }

            progress = new MainQuestLineProgress(chapter, stateId);
            return true;
        }

        internal static bool IsOmenCompleted(Nvs01ProgressData progress)
        {
            return progress != null &&
                   string.Equals(
                       progress.QuestId,
                       MainQuestLineContract.EntryQuestId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       progress.StateId,
                       MainQuestLineContract.OmenCompletedStateId,
                       StringComparison.Ordinal);
        }

        internal static bool IsProofActive(FirstWorldProgressData progress)
        {
            return progress != null &&
                   string.Equals(
                       progress.ProofQuestId,
                       ProofOfWorthIds.MainQuestId,
                       StringComparison.Ordinal);
        }
    }
}
