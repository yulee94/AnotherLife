using AL.Core;

namespace AL.Core.Interfaces
{
    public interface ITrainingService
    {
        ResearchTroopCatalogQueryResult QueryTroop(TroopType type);
        ResearchTroopMutationResult TryStartTraining(TroopType type, int count);
        ResearchTroopMutationResult TryCompleteTraining(TroopType type);
        void StartTraining(TroopType type, int count);
        void CompleteTraining(TroopType type);
        int GetTroopCount(TroopType type);
    }
}
