using System.Collections.Generic;
using AL.Data.Runtime;

namespace AL.Core.Interfaces
{
    public interface IBuildingService
    {
        BuildingState GetBuildingState(string buildingId);
        IEnumerable<BuildingState> GetAllBuildingStates();
        void StartUpgrade(string buildingId);
        void CompleteUpgrade(string buildingId);
    }
}
