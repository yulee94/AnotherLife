namespace AL.ChampionMode.Encounter
{
    public enum ChampionEncounterMode
    {
        Practice = 0,
        DevelopmentDemo = 1,
        AuthoritativeBoss = 2,
        AuthoritativeQuest = 3
    }

    public enum ChampionEncounterConsequenceOutcome
    {
        ChampionVictory = 0,
        ChampionDefeat = 1,
        Cancelled = 2,
        ValidationFailure = 3,
        RuntimeFailure = 4
    }

    public enum ChampionEncounterConsequenceStatus
    {
        Applied = 0,
        DuplicateExact = 1,
        PracticeSuppressed = 2,
        ModeRejected = 3,
        InvalidInput = 4,
        InvalidDependency = 5,
        ProfileWriteUnavailable = 6,
        RewardAuthorityUnavailable = 7,
        CorrelationConflict = 8,
        ApplicationRejected = 9
    }

    public enum ChampionEncounterBossRewardStatus
    {
        Issued = 0,
        ExplicitNoReward = 1,
        DuplicateExact = 2,
        Unavailable = 3,
        Invalid = 4,
        CorrelationConflict = 5
    }

    public sealed class ChampionEncounterConsequenceRequest
    {
        public ChampionEncounterConsequenceRequest(
            string encounterResultId,
            string encounterId,
            string encounterAttemptId,
            ChampionEncounterMode mode,
            ChampionEncounterConsequenceOutcome outcome,
            string realmId,
            string nvsCorrelationId,
            string nvsQuestId,
            string rewardOperationId,
            string sourceFingerprint,
            string profileId,
            bool labeledPractice)
        {
            EncounterResultId = encounterResultId ?? string.Empty;
            EncounterId = encounterId ?? string.Empty;
            EncounterAttemptId = encounterAttemptId ?? string.Empty;
            Mode = mode;
            Outcome = outcome;
            RealmId = realmId ?? string.Empty;
            NvsCorrelationId = nvsCorrelationId ?? string.Empty;
            NvsQuestId = nvsQuestId ?? string.Empty;
            RewardOperationId = rewardOperationId ?? string.Empty;
            SourceFingerprint = sourceFingerprint ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            LabeledPractice = labeledPractice;
        }

        public string EncounterResultId { get; }
        public string EncounterId { get; }
        public string EncounterAttemptId { get; }
        public ChampionEncounterMode Mode { get; }
        public ChampionEncounterConsequenceOutcome Outcome { get; }
        public string RealmId { get; }
        public string NvsCorrelationId { get; }
        public string NvsQuestId { get; }
        public string RewardOperationId { get; }
        public string SourceFingerprint { get; }
        public string ProfileId { get; }
        public bool LabeledPractice { get; }
    }

    public sealed class ChampionEncounterBossRewardPlan
    {
        public ChampionEncounterBossRewardPlan(
            ChampionEncounterBossRewardStatus status,
            string rewardResultId,
            string diagnosticCode)
        {
            Status = status;
            RewardResultId = rewardResultId ?? string.Empty;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public ChampionEncounterBossRewardStatus Status { get; }
        public string RewardResultId { get; }
        public string DiagnosticCode { get; }
    }

    public sealed class ChampionEncounterConsequenceCandidate
    {
        internal ChampionEncounterConsequenceCandidate(
            ChampionEncounterConsequenceRequest request,
            ChampionEncounterBossRewardPlan rewardPlan,
            string consequenceFingerprint)
        {
            Request = request;
            RewardPlan = rewardPlan;
            ConsequenceFingerprint = consequenceFingerprint ?? string.Empty;
        }

        public ChampionEncounterConsequenceRequest Request { get; }
        public ChampionEncounterBossRewardPlan RewardPlan { get; }
        public string ConsequenceFingerprint { get; }
    }

    public sealed class ChampionEncounterConsequenceReceipt
    {
        internal ChampionEncounterConsequenceReceipt(
            string encounterResultId,
            string encounterId,
            ChampionEncounterMode mode,
            ChampionEncounterConsequenceOutcome outcome,
            string realmId,
            string nvsCorrelationId,
            string nvsQuestId,
            string rewardResultId,
            string consequenceFingerprint,
            string profileId)
        {
            EncounterResultId = encounterResultId;
            EncounterId = encounterId;
            Mode = mode;
            Outcome = outcome;
            RealmId = realmId;
            NvsCorrelationId = nvsCorrelationId;
            NvsQuestId = nvsQuestId;
            RewardResultId = rewardResultId;
            ConsequenceFingerprint = consequenceFingerprint;
            ProfileId = profileId;
        }

        public string EncounterResultId { get; }
        public string EncounterId { get; }
        public ChampionEncounterMode Mode { get; }
        public ChampionEncounterConsequenceOutcome Outcome { get; }
        public string RealmId { get; }
        public string NvsCorrelationId { get; }
        public string NvsQuestId { get; }
        public string RewardResultId { get; }
        public string ConsequenceFingerprint { get; }
        public string ProfileId { get; }
    }

    public sealed class ChampionEncounterConsequencePlan
    {
        internal ChampionEncounterConsequencePlan(
            ChampionEncounterConsequenceStatus status,
            string diagnosticCode,
            ChampionEncounterConsequenceReceipt receipt)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Receipt = receipt;
        }

        public ChampionEncounterConsequenceStatus Status { get; }
        public string DiagnosticCode { get; }
        public ChampionEncounterConsequenceReceipt Receipt { get; }
    }

    /// <summary>
    /// Typed #168 boss/reward planning surface. C4 orchestrates this receipt
    /// and never computes credits, items, or seeds itself.
    /// </summary>
    public interface IChampionEncounterBossRewardAuthority
    {
        ChampionEncounterBossRewardPlan Plan(ChampionEncounterConsequenceRequest request);
    }

    /// <summary>
    /// Typed #137 durable commit surface. C4 never writes saves directly.
    /// </summary>
    public interface IChampionEncounterProfileCommit
    {
        bool TryCommit(ChampionEncounterConsequenceCandidate candidate);
    }
}
