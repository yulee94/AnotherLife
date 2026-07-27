using System;
using System.Collections.Generic;
using AL.Data.Definitions;
using AL.Data.Runtime;

namespace AL.Core.Interfaces
{
    public interface IBossLootService
    {
        BossLootResult RollLoot(BossLootRequest request);
        IEnumerable<OwnedEquipmentState> GetOwnedEquipment();
    }

    public enum BossLootApplicationStatus
    {
        None = 0,
        Committed = 1,
        AlreadyCommitted = 2,
        RejectedInvalidRequest = 3,
        RejectedInvalidDefinition = 4,
        RejectedNoCurrentSave = 5,
        RejectedMalformedState = 6,
        RejectedCreditMutation = 7,
        SaveFailedRolledBack = 8,
        CommitUncertain = 9
    }

    public enum BossLootApplicationValidationStatus
    {
        None = 0,
        Valid = 1,
        InvalidEncounterId = 2,
        InvalidRewardResultId = 3,
        InvalidBossId = 4
    }

    [Serializable]
    public sealed class BossLootApplicationIdentity
    {
        public string EncounterId;
        public string RewardResultId;
        public string BossId;
    }

    public sealed class BossLootApplicationValidationResult
    {
        public BossLootApplicationValidationResult(
            BossLootApplicationValidationStatus status,
            string diagnosticCode)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public BossLootApplicationValidationStatus Status { get; }
        public string DiagnosticCode { get; }
        public bool IsValid => Status == BossLootApplicationValidationStatus.Valid;
    }

    public static class BossLootApplicationIdentityValidator
    {
        public static BossLootApplicationValidationResult Validate(
            BossLootApplicationIdentity identity)
        {
            if (identity == null || string.IsNullOrWhiteSpace(identity.EncounterId))
            {
                return new BossLootApplicationValidationResult(
                    BossLootApplicationValidationStatus.InvalidEncounterId,
                    "AL-BOSS-LOOT-ENCOUNTER-ID-INVALID");
            }

            if (string.IsNullOrWhiteSpace(identity.RewardResultId))
            {
                return new BossLootApplicationValidationResult(
                    BossLootApplicationValidationStatus.InvalidRewardResultId,
                    "AL-BOSS-LOOT-RESULT-ID-INVALID");
            }

            if (string.IsNullOrWhiteSpace(identity.BossId))
            {
                return new BossLootApplicationValidationResult(
                    BossLootApplicationValidationStatus.InvalidBossId,
                    "AL-BOSS-LOOT-BOSS-ID-INVALID");
            }

            return new BossLootApplicationValidationResult(
                BossLootApplicationValidationStatus.Valid,
                string.Empty);
        }
    }

    [Serializable]
    public class BossLootRequest
    {
        public string EncounterId;
        public string RewardResultId;
        public string BossId;
        public string BossName;
        public string PlayerDisplayName = "Anonymous player";
        public int WarzoneCreditReward = 500;
        public int RandomSeed;
        public List<EquipmentDefinition> LootTable = new List<EquipmentDefinition>();
    }

    [Serializable]
    public class BossLootResult
    {
        public BossLootApplicationStatus ApplicationStatus;
        public string DiagnosticCode;
        public string EncounterId;
        public string RewardResultId;
        public string BossId;
        public string BossName;
        public int WarzoneCreditsAwarded;
        public List<BossLootDrop> Drops = new List<BossLootDrop>();
        public bool IsCommitted =>
            ApplicationStatus == BossLootApplicationStatus.Committed ||
            ApplicationStatus == BossLootApplicationStatus.AlreadyCommitted;
    }

    [Serializable]
    public class BossLootDrop
    {
        public string EquipmentId;
        public string DisplayName;
        public AL.Core.EquipmentSlot Slot;
        public int AttackBonus;
        public int DefenseBonus;
        public int HealthBonus;
        public bool AnnounceWorldDrop;
        public int Quantity = 1;
    }
}
