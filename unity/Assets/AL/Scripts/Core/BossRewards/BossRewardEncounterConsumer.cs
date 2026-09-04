using System;
using System.Collections.Generic;

namespace AL.Core.BossRewards
{
    public enum BossRewardEncounterConsumerStatus
    {
        Prepared = 0,
        Duplicate = 1,
        NoReward = 2,
        Unavailable = 3,
        Failed = 4
    }

    public sealed class BossRewardEncounterConsumerResult
    {
        internal BossRewardEncounterConsumerResult(
            BossRewardEncounterConsumerStatus status,
            string diagnosticCode,
            BossRewardCandidateReceipt receipt,
            IEnumerable<BossRewardNotificationIntent> outboxIntents,
            BossRewardCandidateApplicationStatus candidateStatus)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Receipt = receipt;
            OutboxIntents = BossRewardImmutable.Freeze(
                outboxIntents ?? Array.Empty<BossRewardNotificationIntent>(),
                BossRewardTechnicalLimits.MaximumRewardEntries + 1);
            CandidateStatus = candidateStatus;
        }

        public BossRewardEncounterConsumerStatus Status { get; }
        public string DiagnosticCode { get; }
        public BossRewardCandidateApplicationStatus CandidateStatus { get; }
        public BossRewardCandidateReceipt Receipt { get; }
        public IReadOnlyList<BossRewardNotificationIntent> OutboxIntents { get; }
        public bool AllowsMutation => false;
        public string MutationActivation => BossRewardSourceCatalog.MutationActivation;
    }

    public static class BossRewardEncounterConsumer
    {
        public const string InvalidRequestCode =
            "AL-BOSS-REWARD-ENCOUNTER-CONSUMER-REQUEST-INVALID";

        public static BossRewardEncounterConsumerResult Consume(
            BossRewardCandidateApplicationRequest request)
        {
            return Map(BossRewardCandidateApplication.Prepare(request));
        }

        private static BossRewardEncounterConsumerResult Map(
            BossRewardCandidateApplicationResult prepared)
        {
            if (prepared == null)
            {
                return Reject(
                    BossRewardEncounterConsumerStatus.Failed,
                    InvalidRequestCode,
                    BossRewardCandidateApplicationStatus.InvalidRequest);
            }

            if (prepared.Status == BossRewardCandidateApplicationStatus.CandidatePrepared)
            {
                return Accept(
                    BossRewardEncounterConsumerStatus.Prepared,
                    prepared);
            }

            if (prepared.Status ==
                BossRewardCandidateApplicationStatus.ExplicitNoRewardPrepared)
            {
                return Accept(
                    BossRewardEncounterConsumerStatus.NoReward,
                    prepared);
            }

            if (prepared.Status == BossRewardCandidateApplicationStatus.AlreadyCommitted)
            {
                return Accept(
                    BossRewardEncounterConsumerStatus.Duplicate,
                    prepared);
            }

            if (prepared.Status == BossRewardCandidateApplicationStatus.SourceUnavailable ||
                prepared.Status == BossRewardCandidateApplicationStatus.UnknownBoss)
            {
                return Reject(
                    BossRewardEncounterConsumerStatus.Unavailable,
                    prepared.DiagnosticCode,
                    prepared.Status);
            }

            return Reject(
                BossRewardEncounterConsumerStatus.Failed,
                string.IsNullOrEmpty(prepared.DiagnosticCode)
                    ? InvalidRequestCode
                    : prepared.DiagnosticCode,
                prepared.Status);
        }

        private static BossRewardEncounterConsumerResult Accept(
            BossRewardEncounterConsumerStatus status,
            BossRewardCandidateApplicationResult prepared)
        {
            return new BossRewardEncounterConsumerResult(
                status,
                string.Empty,
                prepared.Receipt,
                prepared.OutboxIntents,
                prepared.Status);
        }

        private static BossRewardEncounterConsumerResult Reject(
            BossRewardEncounterConsumerStatus status,
            string diagnosticCode,
            BossRewardCandidateApplicationStatus candidateStatus)
        {
            return new BossRewardEncounterConsumerResult(
                status,
                diagnosticCode,
                null,
                Array.Empty<BossRewardNotificationIntent>(),
                candidateStatus);
        }
    }
}
