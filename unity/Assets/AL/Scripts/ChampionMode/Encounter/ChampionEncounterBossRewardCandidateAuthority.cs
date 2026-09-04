using System;
using System.Collections.Generic;
using AL.Core.BossRewards;

namespace AL.ChampionMode.Encounter
{
    /// <summary>
    /// Production #168 Champion boss-reward consumer. Maps pinned
    /// BossRewardCandidateApplication receipts onto typed C4 statuses and
    /// never fabricates fallback credits or items. Save mutation stays blocked.
    /// </summary>
    public sealed class ChampionEncounterBossRewardCandidateAuthority :
        IChampionEncounterBossRewardAuthority
    {
        public const string InvalidRequestCode =
            "AL-CHAMPION-ENCOUNTER-BOSS-REWARD-CANDIDATE-INVALID";

        private readonly byte[] _sourceBytes;
        private readonly string _bossDefinitionId;
        private readonly bool _isSaveAvailable;
        private readonly string _saveRevision;
        private readonly string _economyRevision;
        private readonly string _inventoryRevision;
        private readonly string _ledgerRevision;
        private readonly BossRewardEconomySnapshot _economy;
        private readonly IEnumerable<OwnedEquipmentSnapshot> _inventory;
        private readonly BossRewardLedgerSnapshot _ledger;
        private readonly long _plannedUtcSeconds;

        public ChampionEncounterBossRewardCandidateAuthority(
            byte[] sourceBytes,
            string bossDefinitionId,
            bool isSaveAvailable,
            string saveRevision,
            string economyRevision,
            string inventoryRevision,
            string ledgerRevision,
            BossRewardEconomySnapshot economy,
            IEnumerable<OwnedEquipmentSnapshot> inventory,
            BossRewardLedgerSnapshot ledger,
            long plannedUtcSeconds)
        {
            _sourceBytes = sourceBytes;
            _bossDefinitionId = bossDefinitionId ?? string.Empty;
            _isSaveAvailable = isSaveAvailable;
            _saveRevision = saveRevision ?? string.Empty;
            _economyRevision = economyRevision ?? string.Empty;
            _inventoryRevision = inventoryRevision ?? string.Empty;
            _ledgerRevision = ledgerRevision ?? string.Empty;
            _economy = economy;
            _inventory = inventory;
            _ledger = ledger;
            _plannedUtcSeconds = plannedUtcSeconds;
        }

        public ChampionEncounterBossRewardPlan Plan(
            ChampionEncounterConsequenceRequest request)
        {
            if (request == null)
            {
                return new ChampionEncounterBossRewardPlan(
                    ChampionEncounterBossRewardStatus.Invalid,
                    string.Empty,
                    InvalidRequestCode);
            }

            BossRewardEncounterConsumerResult consumed =
                BossRewardEncounterConsumer.Consume(
                    new BossRewardCandidateApplicationRequest(
                        _sourceBytes,
                        request.ProfileId,
                        request.EncounterId,
                        request.EncounterAttemptId,
                        request.RewardOperationId,
                        _bossDefinitionId,
                        true,
                        _isSaveAvailable,
                        _saveRevision,
                        _economyRevision,
                        _inventoryRevision,
                        _ledgerRevision,
                        _economy,
                        _inventory,
                        _ledger,
                        null,
                        _plannedUtcSeconds));
            return Map(consumed);
        }

        private static ChampionEncounterBossRewardPlan Map(
            BossRewardEncounterConsumerResult consumed)
        {
            if (consumed == null)
            {
                return new ChampionEncounterBossRewardPlan(
                    ChampionEncounterBossRewardStatus.Invalid,
                    string.Empty,
                    InvalidRequestCode);
            }

            if (consumed.Status == BossRewardEncounterConsumerStatus.Prepared)
            {
                return new ChampionEncounterBossRewardPlan(
                    ChampionEncounterBossRewardStatus.Issued,
                    consumed.Receipt != null ? consumed.Receipt.RewardResultId : string.Empty,
                    string.Empty);
            }

            if (consumed.Status == BossRewardEncounterConsumerStatus.Duplicate)
            {
                return new ChampionEncounterBossRewardPlan(
                    ChampionEncounterBossRewardStatus.DuplicateExact,
                    consumed.Receipt != null ? consumed.Receipt.RewardResultId : string.Empty,
                    string.Empty);
            }

            if (consumed.Status == BossRewardEncounterConsumerStatus.NoReward)
            {
                return new ChampionEncounterBossRewardPlan(
                    ChampionEncounterBossRewardStatus.ExplicitNoReward,
                    consumed.Receipt != null ? consumed.Receipt.RewardResultId : string.Empty,
                    string.Empty);
            }

            if (consumed.Status == BossRewardEncounterConsumerStatus.Unavailable)
            {
                return new ChampionEncounterBossRewardPlan(
                    ChampionEncounterBossRewardStatus.Unavailable,
                    string.Empty,
                    consumed.DiagnosticCode);
            }

            if (consumed.CandidateStatus ==
                BossRewardCandidateApplicationStatus.CorrelationConflict)
            {
                return new ChampionEncounterBossRewardPlan(
                    ChampionEncounterBossRewardStatus.CorrelationConflict,
                    string.Empty,
                    consumed.DiagnosticCode);
            }

            return new ChampionEncounterBossRewardPlan(
                ChampionEncounterBossRewardStatus.Invalid,
                string.Empty,
                string.IsNullOrEmpty(consumed.DiagnosticCode)
                    ? InvalidRequestCode
                    : consumed.DiagnosticCode);
        }
    }
}
