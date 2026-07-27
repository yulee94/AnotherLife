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
        public string BossId;
        public string BossName;
        public string EncounterId;
        public string RewardResultId;
        public string PlayerDisplayName = "Anonymous player";
        public int WarzoneCreditReward = 500;
        public int RandomSeed;
        public List<EquipmentDefinition> LootTable = new List<EquipmentDefinition>();
    }

    public enum BossLootCommitStatus
    {
        None = 0,
        Committed = 1,
        Duplicate = 2,
        RejectedInvalidIdentity = 3,
        RejectedInvalidDefinition = 4,
        RejectedMalformedInventory = 5,
        RejectedEconomy = 6,
        SaveFailedRolledBack = 7,
        NoReward = 8
    }

    [Serializable]
    public class BossLootResult
    {
        public string BossId;
        public string BossName;
        public string EncounterId;
        public string RewardResultId;
        public string RewardDigest;
        public BossLootCommitStatus CommitStatus;
        public string DiagnosticCode;
        public int WarzoneCreditsAwarded;
        public List<BossLootDrop> Drops = new List<BossLootDrop>();
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
