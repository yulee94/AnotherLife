using System;
using System.Collections.Generic;
using AL.Core;

namespace AL.Data.Runtime
{
    public class BattleRequest
    {
        public BattleType Type;
        public List<TroopStack> AttackerTroops;
        public List<TroopStack> DefenderTroops;
        public string BossId;
        public int RandomSeed = 12345;
        public RealmId AttackerRealm = RealmId.Crownlands;
        public RealmId DefenderRealm = RealmId.None;
        public float AttackerMorale = 1.0f;
        public float DefenderMorale = 1.0f;
        public string TerrainId;
    }

    [Serializable]
    public class TroopStack
    {
        public TroopType Type;
        public int Count;
    }

    [Serializable]
    public class BattleReport
    {
        public bool IsWinner;
        public int Rounds;
        public int AttackerPower;
        public int DefenderPower;
        public List<TroopStack> AttackerLosses;
        public List<TroopStack> DefenderLosses;
        public List<BattleRoundReport> RoundReports;
        public List<TroopLossReport> AttackerDetailedLosses;
        public List<TroopLossReport> DefenderDetailedLosses;
        public int WarzoneCreditsEarned;
        public List<ResourceData> Loot;
        public int XpGained;
        public string ChampionContribution;
        public string RealmPerkContribution;
        public string TerrainContribution;
        public string Summary;
        // Populated by the deterministic fixed-point engine adapter; empty when
        // computation failed validation. Enables exact determinism verification.
        public string ComputationSha256;
    }

    [Serializable]
    public class BattleRoundReport
    {
        public int Round;
        public float AttackerDamage;
        public float DefenderDamage;
        public string Note;
    }

    [Serializable]
    public class TroopLossReport
    {
        public TroopType Type;
        public int Killed;
        public int Wounded;
        public int Survived;
        public float DamageTaken;
    }
}
