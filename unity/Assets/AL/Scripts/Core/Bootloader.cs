using UnityEngine;
using AL.Core.Interfaces;
using AL.Services.Local;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AL.Core
{
    public class Bootloader : MonoBehaviour
    {
        private const int OfflineStackVersion = 1;

        [SerializeField] private bool _autoLoadOnStart = true;

        private BootloaderInitializationResult _initializationResult = BootloaderInitializationResult.NotStarted();
        private bool _runtimeActive;
        private bool _runtimeDriftReported;

        private void Awake()
        {
            _initializationResult = InitializeIfMissing();
            _runtimeActive = _initializationResult.Succeeded;

            if (!_runtimeActive)
            {
                enabled = false;
                return;
            }

            if (_autoLoadOnStart &&
                ServiceLocator.TryGet<IOfflineServiceStackMarker>(out var marker) &&
                marker.TryMarkLoaded() &&
                ServiceLocator.TryGet<ISaveGameService>(out var saveGameService))
            {
                saveGameService.Load();
            }
        }

        public static BootloaderInitializationResult InitializeIfMissing()
        {
            var markerResult = TryValidateExistingMarker();
            if (markerResult.State == BootloaderInitializationState.ReusedCompleteStack ||
                markerResult.State == BootloaderInitializationState.FailedInconsistentMarker)
            {
                LogInitializationResult(markerResult);
                return markerResult;
            }

            if (ServiceLocator.ContainsAny(OfflineServiceStack.RequiredServiceTypes))
            {
                var partialResult = BootloaderInitializationResult.FailedPartialRegistry(
                    GetPresentRequiredTypes(),
                    GetMissingRequiredTypes());
                LogInitializationResult(partialResult);
                return partialResult;
            }

            OfflineServiceStack stack;
            try
            {
                stack = OfflineServiceStack.Create(OfflineStackVersion);
            }
            catch (Exception ex)
            {
                var constructionResult = BootloaderInitializationResult.FailedConstruction(ex.Message);
                LogInitializationResult(constructionResult);
                return constructionResult;
            }

            var publicationResult = ServiceLocator.PublishBatch(stack.Registrations);
            if (!publicationResult.Succeeded)
            {
                var failedPublicationResult = BootloaderInitializationResult.FailedPublication(
                    publicationResult.ServiceType,
                    publicationResult.Message);
                LogInitializationResult(failedPublicationResult);
                return failedPublicationResult;
            }

            var installedResult = TryValidateExistingMarker();
            var result = installedResult.State == BootloaderInitializationState.ReusedCompleteStack
                ? BootloaderInitializationResult.CreatedCompleteStack(stack.Marker.RegistrationId)
                : installedResult;

            LogInitializationResult(result);
            return result;
        }

        private static BootloaderInitializationResult TryValidateExistingMarker()
        {
            if (!ServiceLocator.TryGet<IOfflineServiceStackMarker>(out var marker))
            {
                return BootloaderInitializationResult.NotStarted();
            }

            if (marker.StackVersion != OfflineStackVersion)
            {
                return BootloaderInitializationResult.FailedInconsistentMarker(
                    marker.RegistrationId,
                    Array.Empty<Type>(),
                    $"Unsupported offline stack marker version {marker.StackVersion}.");
            }

            var missing = new List<Type>();
            var mismatched = new List<Type>();

            foreach (var serviceType in OfflineServiceStack.RequiredServiceTypes)
            {
                if (!ServiceLocator.TryGet(serviceType, out var current))
                {
                    missing.Add(serviceType);
                    continue;
                }

                if (!marker.ExpectedInstances.TryGetValue(serviceType, out var expected) ||
                    expected == null ||
                    !ReferenceEquals(current, expected))
                {
                    mismatched.Add(serviceType);
                }
            }

            if (!ReferenceEquals(marker.SaveRoot, marker.ExpectedInstances[typeof(ISaveGameService)]) ||
                !ReferenceEquals(marker.GameDataRoot, marker.ExpectedInstances[typeof(IGameDataService)]))
            {
                mismatched.Add(typeof(IOfflineServiceStackMarker));
            }

            if (missing.Count > 0 || mismatched.Count > 0)
            {
                return BootloaderInitializationResult.FailedInconsistentMarker(
                    marker.RegistrationId,
                    missing.Concat(mismatched).Distinct().ToArray(),
                    "Offline service stack marker no longer matches registered services.");
            }

            return BootloaderInitializationResult.ReusedCompleteStack(marker.RegistrationId);
        }

        private void Update()
        {
            if (!_runtimeActive)
            {
                return;
            }

            var validation = TryValidateExistingMarker();
            if (!validation.Succeeded)
            {
                if (!_runtimeDriftReported)
                {
                    _runtimeDriftReported = true;
                    Debug.LogError(BootloaderInitializationResult.RuntimeDrift(validation.Message).Message);
                }

                _runtimeActive = false;
                enabled = false;
                return;
            }

            if (ServiceLocator.TryGet<IResourceService>(out var resourceService))
            {
                resourceService.TickProduction(Time.deltaTime);
            }
        }

        private void OnApplicationQuit()
        {
            SaveIfReady();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveIfReady();
            }
        }

        private void SaveIfReady()
        {
            if (!_runtimeActive || !ServiceLocator.TryGet<ISaveGameService>(out var saveGameService))
            {
                return;
            }

            try
            {
                saveGameService.Save();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BOOT_STACK_SAVE_FAILED] Bootloader save failed: {ex.Message}");
            }
        }

        private static Type[] GetPresentRequiredTypes()
        {
            return OfflineServiceStack.RequiredServiceTypes
                .Where(type => ServiceLocator.TryGet(type, out _))
                .ToArray();
        }

        private static Type[] GetMissingRequiredTypes()
        {
            return OfflineServiceStack.RequiredServiceTypes
                .Where(type => !ServiceLocator.TryGet(type, out _))
                .ToArray();
        }

        private static void LogInitializationResult(BootloaderInitializationResult result)
        {
            if (!result.Succeeded && result.State != BootloaderInitializationState.NotStarted)
            {
                Debug.LogError(result.Message);
            }
        }
    }

    public enum BootloaderInitializationState
    {
        NotStarted,
        ReusedCompleteStack,
        CreatedCompleteStack,
        FailedPartialRegistry,
        FailedInconsistentMarker,
        FailedConstruction,
        FailedPublication,
        RuntimeDrift
    }

    public sealed class BootloaderInitializationResult
    {
        private BootloaderInitializationResult(
            BootloaderInitializationState state,
            string code,
            string message,
            string registrationId = "",
            Type serviceType = null,
            IReadOnlyList<Type> presentTypes = null,
            IReadOnlyList<Type> missingTypes = null,
            IReadOnlyList<Type> mismatchedTypes = null)
        {
            State = state;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            RegistrationId = registrationId ?? string.Empty;
            ServiceType = serviceType;
            PresentTypes = presentTypes ?? Array.Empty<Type>();
            MissingTypes = missingTypes ?? Array.Empty<Type>();
            MismatchedTypes = mismatchedTypes ?? Array.Empty<Type>();
        }

        public BootloaderInitializationState State { get; }
        public string Code { get; }
        public string Message { get; }
        public string RegistrationId { get; }
        public Type ServiceType { get; }
        public IReadOnlyList<Type> PresentTypes { get; }
        public IReadOnlyList<Type> MissingTypes { get; }
        public IReadOnlyList<Type> MismatchedTypes { get; }
        public bool Succeeded => State == BootloaderInitializationState.ReusedCompleteStack ||
            State == BootloaderInitializationState.CreatedCompleteStack;

        public static BootloaderInitializationResult NotStarted()
        {
            return new BootloaderInitializationResult(
                BootloaderInitializationState.NotStarted,
                "BOOT_STACK_NOT_STARTED",
                "Bootloader service-stack initialization has not started.");
        }

        public static BootloaderInitializationResult ReusedCompleteStack(string registrationId)
        {
            return new BootloaderInitializationResult(
                BootloaderInitializationState.ReusedCompleteStack,
                "BOOT_STACK_REUSED",
                $"[BOOT_STACK_REUSED] Reused offline service stack {registrationId}.",
                registrationId);
        }

        public static BootloaderInitializationResult CreatedCompleteStack(string registrationId)
        {
            return new BootloaderInitializationResult(
                BootloaderInitializationState.CreatedCompleteStack,
                "BOOT_STACK_CREATED",
                $"[BOOT_STACK_CREATED] Created offline service stack {registrationId}.",
                registrationId);
        }

        public static BootloaderInitializationResult FailedPartialRegistry(Type[] presentTypes, Type[] missingTypes)
        {
            string present = string.Join(", ", presentTypes.Select(type => type.Name));
            string missing = string.Join(", ", missingTypes.Select(type => type.Name));
            return new BootloaderInitializationResult(
                BootloaderInitializationState.FailedPartialRegistry,
                "BOOT_STACK_PARTIAL_REGISTRY",
                $"[BOOT_STACK_PARTIAL_REGISTRY] Required services are partially registered. Present: {present}. Missing: {missing}.",
                presentTypes: presentTypes,
                missingTypes: missingTypes);
        }

        public static BootloaderInitializationResult FailedInconsistentMarker(string registrationId, Type[] mismatchedTypes, string message)
        {
            string mismatched = string.Join(", ", mismatchedTypes.Select(type => type.Name));
            return new BootloaderInitializationResult(
                BootloaderInitializationState.FailedInconsistentMarker,
                "BOOT_STACK_MARKER_INCONSISTENT",
                $"[BOOT_STACK_MARKER_INCONSISTENT] {message} Registration: {registrationId}. Types: {mismatched}.",
                registrationId,
                mismatchedTypes: mismatchedTypes);
        }

        public static BootloaderInitializationResult FailedConstruction(string message)
        {
            return new BootloaderInitializationResult(
                BootloaderInitializationState.FailedConstruction,
                "BOOT_STACK_CONSTRUCTION_FAILED",
                $"[BOOT_STACK_CONSTRUCTION_FAILED] Could not construct offline service stack: {message}");
        }

        public static BootloaderInitializationResult FailedPublication(Type serviceType, string message)
        {
            return new BootloaderInitializationResult(
                BootloaderInitializationState.FailedPublication,
                "BOOT_STACK_PUBLICATION_FAILED",
                $"[BOOT_STACK_PUBLICATION_FAILED] Could not publish offline service stack: {message}",
                serviceType: serviceType);
        }

        public static BootloaderInitializationResult RuntimeDrift(string message)
        {
            return new BootloaderInitializationResult(
                BootloaderInitializationState.RuntimeDrift,
                "BOOT_STACK_RUNTIME_DRIFT",
                $"[BOOT_STACK_RUNTIME_DRIFT] {message}");
        }
    }

    internal interface IOfflineServiceStackMarker
    {
        int StackVersion { get; }
        string RegistrationId { get; }
        IReadOnlyDictionary<Type, object> ExpectedInstances { get; }
        object SaveRoot { get; }
        object GameDataRoot { get; }
        bool TryMarkLoaded();
    }

    internal sealed class LocalOfflineServiceStackMarker : IOfflineServiceStackMarker
    {
        private bool _loadClaimed;

        public LocalOfflineServiceStackMarker(
            int stackVersion,
            string registrationId,
            IReadOnlyDictionary<Type, object> expectedInstances,
            object saveRoot,
            object gameDataRoot)
        {
            StackVersion = stackVersion;
            RegistrationId = registrationId;
            ExpectedInstances = expectedInstances;
            SaveRoot = saveRoot;
            GameDataRoot = gameDataRoot;
        }

        public int StackVersion { get; }
        public string RegistrationId { get; }
        public IReadOnlyDictionary<Type, object> ExpectedInstances { get; }
        public object SaveRoot { get; }
        public object GameDataRoot { get; }

        public bool TryMarkLoaded()
        {
            if (_loadClaimed)
            {
                return false;
            }

            _loadClaimed = true;
            return true;
        }
    }

    internal sealed class OfflineServiceStack
    {
        private OfflineServiceStack(
            IReadOnlyDictionary<Type, object> requiredInstances,
            LocalOfflineServiceStackMarker marker,
            IReadOnlyList<ServiceRegistrationEntry> registrations)
        {
            RequiredInstances = requiredInstances;
            Marker = marker;
            Registrations = registrations;
        }

        public static readonly Type[] RequiredServiceTypes =
        {
            typeof(IGameDataService),
            typeof(ISaveGameService),
            typeof(IRealmService),
            typeof(IResourceService),
            typeof(IResearchService),
            typeof(IBuildingService),
            typeof(ITrainingService),
            typeof(IBattleSimulator),
            typeof(IWarzoneCreditService),
            typeof(IWarmasterService),
            typeof(ITerritoryService),
            typeof(IWorldStateService),
            typeof(IReputationService),
            typeof(IFactionService),
            typeof(IPersonaService),
            typeof(IQuestService),
            typeof(IStoryService),
            typeof(INotificationService),
            typeof(IRealmGemService),
            typeof(IWorldAtlasService),
            typeof(IBossLootService)
        };

        public IReadOnlyDictionary<Type, object> RequiredInstances { get; }
        public LocalOfflineServiceStackMarker Marker { get; }
        public IReadOnlyList<ServiceRegistrationEntry> Registrations { get; }

        public static OfflineServiceStack Create(int stackVersion)
        {
            var gameData = new LocalGameDataService();
            var saveGame = new LocalSaveGameService();

            var realmService = new LocalRealmService(saveGame, gameData);
            var resourceService = new LocalResourceService(saveGame);
            var researchService = new LocalResearchService(saveGame, resourceService);
            var buildingService = new LocalBuildingService(saveGame, resourceService, gameData);
            var trainingService = new LocalTrainingService(saveGame, resourceService);

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
            var bossLootService = new LocalBossLootService(saveGame, warzoneCredits, notificationService);

            var requiredInstances = new Dictionary<Type, object>
            {
                [typeof(IGameDataService)] = gameData,
                [typeof(ISaveGameService)] = saveGame,
                [typeof(IRealmService)] = realmService,
                [typeof(IResourceService)] = resourceService,
                [typeof(IResearchService)] = researchService,
                [typeof(IBuildingService)] = buildingService,
                [typeof(ITrainingService)] = trainingService,
                [typeof(IBattleSimulator)] = battleSim,
                [typeof(IWarzoneCreditService)] = warzoneCredits,
                [typeof(IWarmasterService)] = warmaster,
                [typeof(ITerritoryService)] = territoryService,
                [typeof(IWorldStateService)] = worldStateService,
                [typeof(IReputationService)] = reputationService,
                [typeof(IFactionService)] = factionService,
                [typeof(IPersonaService)] = personaService,
                [typeof(IQuestService)] = questService,
                [typeof(IStoryService)] = storyService,
                [typeof(INotificationService)] = notificationService,
                [typeof(IRealmGemService)] = realmGemService,
                [typeof(IWorldAtlasService)] = worldAtlasService,
                [typeof(IBossLootService)] = bossLootService
            };

            if (RequiredServiceTypes.Any(type => !requiredInstances.ContainsKey(type) || requiredInstances[type] == null))
            {
                throw new InvalidOperationException("Offline service stack required-instance map is incomplete.");
            }

            var marker = new LocalOfflineServiceStackMarker(
                stackVersion,
                Guid.NewGuid().ToString("N"),
                requiredInstances,
                saveGame,
                gameData);

            var registrations = RequiredServiceTypes
                .Select(type => new ServiceRegistrationEntry(type, requiredInstances[type]))
                .ToList();
            registrations.Add(new ServiceRegistrationEntry(typeof(IOfflineServiceStackMarker), marker));

            return new OfflineServiceStack(requiredInstances, marker, registrations);
        }
    }
}
