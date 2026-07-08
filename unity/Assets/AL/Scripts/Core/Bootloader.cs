using UnityEngine;
using AL.Core.Interfaces;
using AL.Services.Local;
using System;

namespace AL.Core
{
    public class Bootloader : MonoBehaviour
    {
        [SerializeField] private bool _autoLoadOnStart = true;

        private void Awake()
        {
            InitializeIfMissing();

            if (_autoLoadOnStart)
            {
                ServiceLocator.Get<ISaveGameService>().Load();
            }
        }

        public static void InitializeIfMissing()
        {
            try
            {
                ServiceLocator.Get<IResourceService>();
                Debug.Log("[Bootloader] Services already registered.");
            }
            catch (Exception)
            {
                Debug.Log("[Bootloader] Services missing. Initializing Offline Stack...");

                // 1. Data & Save Services
                var gameData = new LocalGameDataService();
                var saveGame = new LocalSaveGameService();

                ServiceLocator.Register<IGameDataService>(gameData);
                ServiceLocator.Register<ISaveGameService>(saveGame);

                // 2. Domain & Kingdom Services
                var realmService = new LocalRealmService(saveGame, gameData);
                var resourceService = new LocalResourceService(saveGame);
                var researchService = new LocalResearchService(saveGame, resourceService);
                var buildingService = new LocalBuildingService(saveGame, resourceService, gameData);
                var trainingService = new LocalTrainingService(saveGame, resourceService);

                ServiceLocator.Register<IRealmService>(realmService);
                ServiceLocator.Register<IResourceService>(resourceService);
                ServiceLocator.Register<IResearchService>(researchService);
                ServiceLocator.Register<IBuildingService>(buildingService);
                ServiceLocator.Register<ITrainingService>(trainingService);

                // 3. Battle & Economy Services
                var battleSim = new AL.Battle.Simulator.DeterministicBattleSimulator();
                var warzoneCredits = new LocalWarzoneCreditService(saveGame);
                var warmaster = new LocalWarmasterService(saveGame, warzoneCredits);
                var territoryService = new AL.RealmWar.Warzone.WarzoneService(saveGame);
                var questService = new LocalQuestService(saveGame, resourceService, warzoneCredits);
                var storyService = new LocalStoryService(saveGame, gameData);
                var reputationService = new ReputationService(saveGame);
                var factionService = new FactionService(saveGame);
                var personaService = new PersonaService(saveGame);
                var notificationService = new LocalNotificationService();
                var realmGemService = new LocalRealmGemService(saveGame);
                var worldStateService = new WorldStateService(saveGame, notificationService);
                var worldAtlasService = new AL.RealmWar.World.LocalWorldAtlasService(storyService);

                ServiceLocator.Register<IBattleSimulator>(battleSim);
                ServiceLocator.Register<IWarzoneCreditService>(warzoneCredits);
                ServiceLocator.Register<IWarmasterService>(warmaster);
                ServiceLocator.Register<ITerritoryService>(territoryService);
                ServiceLocator.Register<IWorldStateService>(worldStateService);
                ServiceLocator.Register<IReputationService>(reputationService);
                ServiceLocator.Register<IFactionService>(factionService);
                ServiceLocator.Register<IPersonaService>(personaService);
                ServiceLocator.Register<IQuestService>(questService);
                ServiceLocator.Register<IStoryService>(storyService);
                ServiceLocator.Register<INotificationService>(notificationService);
                ServiceLocator.Register<IRealmGemService>(realmGemService);
                ServiceLocator.Register<IWorldAtlasService>(worldAtlasService);

                Debug.Log("<color=cyan>[Bootloader] Offline Services Initialized Successfully.</color>");
            }
        }

        private void Update()
        {
            // Update resource production
            var resourceService = ServiceLocator.Get<IResourceService>();
            if (resourceService != null)
            {
                resourceService.TickProduction(Time.deltaTime);
            }
        }

        private void OnApplicationQuit()
        {
            // Auto-save on exit
            ServiceLocator.Get<ISaveGameService>().Save();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                ServiceLocator.Get<ISaveGameService>().Save();
            }
        }
    }
}
