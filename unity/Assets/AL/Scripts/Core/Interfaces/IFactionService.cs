using System.Collections.Generic;

namespace AL.Core.Interfaces
{
    public interface IFactionService
    {
        int GetReputation(string factionId);
        void AdjustReputation(string factionId, int delta);
        string GetFactionAffiliation(string factionId);
    }
}
