using System;
using System.Collections.Generic;

namespace AL.Data.Runtime
{
    [Serializable]
    public class GemState
    {
        public bool Stonehold_Heart_Gem;
        public bool Stonehold_Fortress_Gem;
        public bool Eldergrove_Heart_Gem;
        public bool Eldergrove_Glade_Gem;
        public bool Crownlands_Heart_Gem;
        public bool Crownlands_Capital_Gem;
        public bool Umbral_Heart_Gem;
        public bool Umbral_Void_Gem;

        public int TotalGemsCollected =>
            (Stonehold_Heart_Gem ? 1 : 0) + (Stonehold_Fortress_Gem ? 1 : 0) +
            (Eldergrove_Heart_Gem ? 1 : 0) + (Eldergrove_Glade_Gem ? 1 : 0) +
            (Crownlands_Heart_Gem ? 1 : 0) + (Crownlands_Capital_Gem ? 1 : 0) +
            (Umbral_Heart_Gem ? 1 : 0) + (Umbral_Void_Gem ? 1 : 0);
    }
}
