using System;

namespace AL.ChampionMode.Death
{
    public enum DeathPenaltyCommitStatus
    {
        Unknown = 0,
        CommittedBelowMax = 1,
        ReplayedBelowMax = 2,
        OathmarkPaymentRequired = 3,
        ReplayedOathmarkPaymentRequired = 4,
        RejectedInvalidRequest = 5,
        RejectedProfileUnavailable = 6,
        RejectedStale = 7,
        RejectedCollision = 8,
        RejectedWrongProfile = 9,
        RejectedDegraded = 10,
        RejectedForward = 11,
        RejectedReadOnly = 12,
        RejectedSaveUncertain = 13,
        RejectedPlanner = 14
    }

    public sealed class DeathPenaltyCommitRequest
    {
        public DeathPenaltyCommitRequest(
            string operationId,
            string deathEventId,
            string combatSessionId,
            string encounterAttemptId,
            string instanceId,
            long deathOrdinal = 0L,
            string expectedProfileId = null,
            string expectedGenerationFingerprint = null)
        {
            OperationId = operationId ?? string.Empty;
            DeathEventId = deathEventId ?? string.Empty;
            CombatSessionId = combatSessionId ?? string.Empty;
            EncounterAttemptId = encounterAttemptId ?? string.Empty;
            InstanceId = instanceId ?? string.Empty;
            DeathOrdinal = deathOrdinal;
            ExpectedProfileId = expectedProfileId ?? string.Empty;
            ExpectedGenerationFingerprint = expectedGenerationFingerprint ?? string.Empty;
        }

        public string OperationId { get; }
        public string DeathEventId { get; }
        public string CombatSessionId { get; }
        public string EncounterAttemptId { get; }
        public string InstanceId { get; }
        public long DeathOrdinal { get; }
        public string ExpectedProfileId { get; }
        public string ExpectedGenerationFingerprint { get; }
    }

    public sealed class DeathPenaltyCommitResult
    {
        public DeathPenaltyCommitResult(
            DeathPenaltyCommitStatus status,
            bool mutationOccurred,
            bool persisted,
            bool allowsRevive,
            int afterLevel,
            long afterInLevelExperienceUnits,
            string technicalCode)
        {
            Status = status;
            MutationOccurred = mutationOccurred;
            Persisted = persisted;
            AllowsRevive = allowsRevive;
            AfterLevel = afterLevel;
            AfterInLevelExperienceUnits = afterInLevelExperienceUnits;
            TechnicalCode = technicalCode ?? string.Empty;
        }

        public DeathPenaltyCommitStatus Status { get; }
        public bool MutationOccurred { get; }
        public bool Persisted { get; }
        public bool AllowsRevive { get; }
        public int AfterLevel { get; }
        public long AfterInLevelExperienceUnits { get; }
        public string TechnicalCode { get; }
    }

    public static class DeathPenaltyCommitCodes
    {
        public const string Committed = "AL-DEATH-PENALTY-COMMITTED";
        public const string Replayed = "AL-DEATH-PENALTY-REPLAYED";
        public const string OathmarkPaymentRequired =
            "AL-DEATH-PENALTY-OATHMARK-PAYMENT-REQUIRED";
        public const string InvalidRequest = "AL-DEATH-PENALTY-REQUEST-INVALID";
        public const string ProfileNotSchemaTwo =
            "AL-DEATH-PENALTY-PROFILE-NOT-SCHEMA-TWO";
        public const string ProfileMismatch = "AL-DEATH-PENALTY-PROFILE-MISMATCH";
        public const string StaleBase = "AL-DEATH-PENALTY-STALE-BASE";
        public const string ReadOnly = "AL-DEATH-PENALTY-READ-ONLY";
        public const string CommitUncertain = "AL-DEATH-PENALTY-COMMIT-UNCERTAIN";
        public const string ForwardSchema = "AL-DEATH-PENALTY-FORWARD-SCHEMA";
        public const string Collision = "AL-DEATH-PENALTY-COLLISION";
        public const string Degraded = "AL-DEATH-PENALTY-DEGRADED";
        public const string NotWritable = "AL-DEATH-PENALTY-PROFILE-NOT-WRITABLE";
    }
}
