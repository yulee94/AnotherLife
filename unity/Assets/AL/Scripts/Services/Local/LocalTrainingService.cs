using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;

namespace AL.Services.Local
{
    public class LocalTrainingService : ITrainingService
    {
        private readonly ISaveGameService _saveGameService;
        private readonly IResourceService _resourceService;

        public LocalTrainingService(ISaveGameService saveGameService, IResourceService resourceService)
        {
            _saveGameService = saveGameService;
            _resourceService = resourceService;
        }

        public ResearchTroopCatalogQueryResult QueryTroop(TroopType type)
        {
            return ResearchTroopCatalogAuthority.QueryTroop(type);
        }

        public ResearchTroopMutationResult TryStartTraining(TroopType type, int count)
        {
            return ResearchTroopCatalogAuthority.RejectTroop(type);
        }

        public ResearchTroopMutationResult TryCompleteTraining(TroopType type)
        {
            return ResearchTroopCatalogAuthority.RejectTroop(type);
        }

        public void StartTraining(TroopType type, int count)
        {
            TryStartTraining(type, count);
        }

        public void CompleteTraining(TroopType type)
        {
            TryCompleteTraining(type);
        }

        public int GetTroopCount(TroopType type)
        {
            return FindTroopState(_saveGameService?.CurrentSave, type)?.Count ?? 0;
        }

        private static TroopInventoryData FindTroopState(
            SaveGameData save,
            TroopType type) =>
            save?.Troops?.FirstOrDefault(state =>
                state != null && state.Type == type);
    }
}
