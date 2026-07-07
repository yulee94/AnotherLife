using UnityEngine;
using AL.Core.Interfaces;
using AL.Services.Local;

namespace AL.Core
{
    public class Bootloader : MonoBehaviour
    {
        [SerializeField] private bool _autoLoadOnStart = true;

        private void Awake()
        {
            InitializeServices();

            if (_autoLoadOnStart)
            {
                ServiceLocator.Get<ISaveGameService>().Load();
            }
        }

        private void InitializeServices()
        {
            // 1. Data & Save Services
            var gameData = new LocalGameDataService();
            var saveGame = new LocalSaveGameService();

            ServiceLocator.Register<IGameDataService>(gameData);
            ServiceLocator.Register<ISaveGameService>(saveGame);

            // 2. Domain & Kingdom Services
            var realmService = new LocalRealmService(saveGame, gameData);
            var resourceService = new LocalResourceService(saveGame);
            var researchService = new AL.Services.Local.LocalResearchService(saveGame, resourceService);
            var buildingService = new LocalBuildingService(saveGame, resourceService, gameData);
            var trainingService = new LocalTrainingService(saveGame, resourceService);

            ServiceLocator.Register<IRealmService>(realmService);
            ServiceLocator.Register<IResourceService>(resourceService);
            ServiceLocator.Register<IResearchService>(researchService);
            ServiceLocator.Register<IBuildingService>(buildingService);
            ServiceLocator.Register<ITrainingService>(trainingService);

            // 3. Battle & Economy Services
            var battleSim = new AL.Battle.Simulator.DeterministicBattleSimulator();
            var warzoneCredits = new LocalWarzoneCreditService();
            var warmaster = new LocalWarmasterService(saveGame);
            var territoryService = new AL.RealmWar.Warzone.WarzoneService();
            var questService = new AL.Services.Local.LocalQuestService(saveGame, resourceService, warzoneCredits);

            ServiceLocator.Register<IBattleSimulator>(battleSim);
            ServiceLocator.Register<IWarzoneCreditService>(warzoneCredits);
            ServiceLocator.Register<IWarmasterService>(warmaster);
            ServiceLocator.Register<ITerritoryService>(territoryService);
            ServiceLocator.Register<IQuestService>(questService);

            Debug.Log("Core Services Initialized.");
        }

        private void Update()
        {
            // Update resource production
            ServiceLocator.Get<IResourceService>().TickProduction(Time.deltaTime);
        }

        private void OnApplicationQuit()
        {
            // Auto-save on exit
            ServiceLocator.Get<ISaveGameService>().Save();
        }
    }
}
