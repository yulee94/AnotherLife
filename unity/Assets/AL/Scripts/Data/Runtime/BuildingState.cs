using System;

namespace AL.Data.Runtime
{
    [Serializable]
    public class BuildingState
    {
        public string BuildingId;
        public int Level;
        public bool IsUpgrading;
        public long UpgradeCompleteTimestamp;
    }
}
