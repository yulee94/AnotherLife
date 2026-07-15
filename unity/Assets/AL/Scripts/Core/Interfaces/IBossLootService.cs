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

    [Serializable]
    public class BossLootRequest
    {
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
        public string BossId;
        public string BossName;
        public int WarzoneCreditsAwarded;
        public List<BossLootDrop> Drops = new List<BossLootDrop>();
    }

    [Serializable]
    public class BossLootDrop
    {
        public string EquipmentId;
        public string DisplayName;
        public AL.Core.EquipmentSlot Slot;
        public AL.Core.ItemGrade Grade;
        public AL.Core.RealmId VisualRealm;
        public string VisualEffectKey;
        public int AttackBonus;
        public int DefenseBonus;
        public int HealthBonus;
        public float AuraIntensity;
        public float RevealScale = 1f;
        public float PrimaryR;
        public float PrimaryG;
        public float PrimaryB;
        public float SecondaryR;
        public float SecondaryG;
        public float SecondaryB;
        public bool AnnounceWorldDrop;
        public int Quantity = 1;
    }
}
