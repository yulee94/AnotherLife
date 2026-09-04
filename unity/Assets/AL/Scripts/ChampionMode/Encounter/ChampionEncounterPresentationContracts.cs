namespace AL.ChampionMode.Encounter
{
    public enum ChampionEncounterPresentationStatus
    {
        Pending = 0,
        Practice = 1,
        Clear = 2,
        Defeat = 3,
        Unavailable = 4,
        Failure = 5
    }

    public sealed class ChampionEncounterPresentationPlan
    {
        internal ChampionEncounterPresentationPlan(
            ChampionEncounterPresentationStatus status,
            string diagnosticCode,
            ChampionEncounterConsequenceReceipt receipt,
            bool visiblyPractice,
            bool showsCommittedReward,
            bool showsCommittedProgression)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Receipt = receipt;
            VisiblyPractice = visiblyPractice;
            ShowsCommittedReward = showsCommittedReward;
            ShowsCommittedProgression = showsCommittedProgression;
            RealmId = receipt != null ? receipt.RealmId ?? string.Empty : string.Empty;
            NvsCorrelationId = receipt != null
                ? receipt.NvsCorrelationId ?? string.Empty
                : string.Empty;
            NvsQuestId = receipt != null ? receipt.NvsQuestId ?? string.Empty : string.Empty;
            RewardResultId = receipt != null
                ? receipt.RewardResultId ?? string.Empty
                : string.Empty;
        }

        public ChampionEncounterPresentationStatus Status { get; }

        public string DiagnosticCode { get; }

        public ChampionEncounterConsequenceReceipt Receipt { get; }

        public bool VisiblyPractice { get; }

        public bool ShowsCommittedReward { get; }

        public bool ShowsCommittedProgression { get; }

        public string RealmId { get; }

        public string NvsCorrelationId { get; }

        public string NvsQuestId { get; }

        public string RewardResultId { get; }
    }
}
