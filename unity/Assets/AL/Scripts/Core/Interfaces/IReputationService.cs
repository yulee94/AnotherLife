using System.Collections.Generic;

namespace AL.Core.Interfaces
{
    public interface IReputationService
    {
        float GetAffinity(string npcId);
        void ChangeAffinity(string npcId, float delta);
        string GetAffinityRank(string npcId);
    }
}
