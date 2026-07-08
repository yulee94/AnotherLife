using System;
using System.Collections.Generic;

namespace AL.Data.Runtime
{
    [Serializable]
    public class WarmasterState
    {
        public string EquippedSetId;
        public List<string> UnlockedSetIds = new List<string>();
        public List<string> PurchasedPieceIds = new List<string>();
        public bool IsTrueWarmaster;
        public int Level;
        public int Experience;
    }
}
