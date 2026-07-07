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
        public List<TroopStack> AttackerLosses;
        public List<TroopStack> DefenderLosses;
        public string Summary;
    }
}
