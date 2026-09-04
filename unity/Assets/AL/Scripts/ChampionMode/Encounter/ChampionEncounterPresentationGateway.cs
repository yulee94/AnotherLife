namespace AL.ChampionMode.Encounter
{
    /// <summary>
    /// C5 presentation/route evidence. HUD/clear/defeat/reward bind to a
    /// committed C4 receipt or an explicit pending/failure plan. Boss object
    /// null, local booleans, and loot callbacks are not presentation authority.
    /// </summary>
    public static class ChampionEncounterPresentationGateway
    {
        public const string PendingCode =
            "AL-CHAMPION-ENCOUNTER-PRESENTATION-PENDING";
        public const string PracticeCode =
            "AL-CHAMPION-ENCOUNTER-PRESENTATION-PRACTICE";
        public const string UnavailableCode =
            "AL-CHAMPION-ENCOUNTER-PRESENTATION-UNAVAILABLE";
        public const string FailureCode =
            "AL-CHAMPION-ENCOUNTER-PRESENTATION-FAILURE";
        public const string ReceiptInvalidCode =
            "AL-CHAMPION-ENCOUNTER-PRESENTATION-RECEIPT-INVALID";

        public static ChampionEncounterPresentationPlan Present(
            ChampionEncounterConsequencePlan consequence)
        {
            if (consequence == null)
            {
                return Plan(
                    ChampionEncounterPresentationStatus.Pending,
                    PendingCode);
            }

            if (consequence.Status == ChampionEncounterConsequenceStatus.PracticeSuppressed)
            {
                return Plan(
                    ChampionEncounterPresentationStatus.Practice,
                    string.IsNullOrEmpty(consequence.DiagnosticCode)
                        ? PracticeCode
                        : consequence.DiagnosticCode,
                    visiblyPractice: true);
            }

            if (consequence.Status == ChampionEncounterConsequenceStatus.Applied ||
                consequence.Status == ChampionEncounterConsequenceStatus.DuplicateExact)
            {
                return PresentReceipt(consequence.Receipt);
            }

            if (consequence.Status == ChampionEncounterConsequenceStatus.CorrelationConflict ||
                consequence.Status == ChampionEncounterConsequenceStatus.ApplicationRejected)
            {
                return Plan(
                    ChampionEncounterPresentationStatus.Failure,
                    string.IsNullOrEmpty(consequence.DiagnosticCode)
                        ? FailureCode
                        : consequence.DiagnosticCode);
            }

            return Plan(
                ChampionEncounterPresentationStatus.Unavailable,
                string.IsNullOrEmpty(consequence.DiagnosticCode)
                    ? UnavailableCode
                    : consequence.DiagnosticCode);
        }

        private static ChampionEncounterPresentationPlan PresentReceipt(
            ChampionEncounterConsequenceReceipt receipt)
        {
            if (receipt == null ||
                receipt.Mode != ChampionEncounterMode.AuthoritativeQuest ||
                !ChampionEncounterLoadGateway.IsCommittedValidRealm(receipt.RealmId) ||
                !ChampionEncounterConsequenceGateway.StableText(receipt.NvsCorrelationId) ||
                !ChampionEncounterConsequenceGateway.StableText(receipt.NvsQuestId) ||
                !ChampionEncounterConsequenceGateway.StableText(receipt.EncounterResultId))
            {
                return Plan(
                    ChampionEncounterPresentationStatus.Unavailable,
                    ReceiptInvalidCode);
            }

            if (receipt.Outcome == ChampionEncounterConsequenceOutcome.ChampionVictory)
            {
                bool committedReward =
                    ChampionEncounterConsequenceGateway.StableText(receipt.RewardResultId);
                return new ChampionEncounterPresentationPlan(
                    ChampionEncounterPresentationStatus.Clear,
                    string.Empty,
                    receipt,
                    false,
                    committedReward,
                    true);
            }

            if (receipt.Outcome == ChampionEncounterConsequenceOutcome.ChampionDefeat)
            {
                return new ChampionEncounterPresentationPlan(
                    ChampionEncounterPresentationStatus.Defeat,
                    string.Empty,
                    receipt,
                    false,
                    false,
                    true);
            }

            return Plan(
                ChampionEncounterPresentationStatus.Failure,
                FailureCode);
        }

        private static ChampionEncounterPresentationPlan Plan(
            ChampionEncounterPresentationStatus status,
            string diagnosticCode,
            bool visiblyPractice = false)
        {
            return new ChampionEncounterPresentationPlan(
                status,
                diagnosticCode,
                null,
                visiblyPractice,
                false,
                false);
        }
    }
}
