using AL.Core;

namespace AL.Core.Interfaces
{
    public interface ITrainingService
    {
        void StartTraining(TroopType type, int count);
        void CompleteTraining(TroopType type);
        int GetTroopCount(TroopType type);
    }
}
