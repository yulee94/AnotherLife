using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalBuildingService : IBuildingService
    {
        private const int MaximumSupportedLevel = 10;

        private const string NoCurrentSaveCode = "AL-BLD-NO-CURRENT-SAVE";
        private const string UnsupportedBuildingCode = "AL-BLD-UNSUPPORTED-BUILDING";
        private const string InvalidDefinitionCode = "AL-BLD-DEFINITION-INVALID";
        private const string MalformedStateCode = "AL-BLD-STATE-MALFORMED";
        private const string InvalidTimestampCode = "AL-BLD-TIMESTAMP-INVALID";
        private const string MaxLevelCode = "AL-BLD-MAX-LEVEL";
        private const string InProgressCode = "AL-BLD-IN-PROGRESS";
        private const string NotReadyCode = "AL-BLD-NOT-READY";
        private const string InsufficientResourcesCode = "AL-BLD-INSUFFICIENT-RESOURCES";
        private const string EconomyUnavailableCode = "AL-BLD-ECONOMY-UNAVAILABLE";
        private const string SaveFailedCode = "AL-BLD-SAVE-FAILED-ROLLED-BACK";
        private const string CommitUncertainCode = "AL-BLD-COMMIT-UNCERTAIN";
        private const string StartedCode = "AL-BLD-STARTED";
        private const string CompletedCode = "AL-BLD-COMPLETED";

        private static readonly ConditionalWeakTable<ISaveGameService, object> TransactionGates =
            new ConditionalWeakTable<ISaveGameService, object>();

        private readonly ISaveGameService _saveGameService;
        private readonly IResourceService _resourceService;
        private readonly IGameDataService _gameDataService;
        private readonly object _transactionGate;
        private long _lastReconcileTimestamp;

        public LocalBuildingService(
            ISaveGameService saveGameService,
            IResourceService resourceService,
            IGameDataService gameDataService)
        {
            _saveGameService =
                saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
            _resourceService =
                resourceService ?? throw new ArgumentNullException(nameof(resourceService));
            _gameDataService =
                gameDataService ?? throw new ArgumentNullException(nameof(gameDataService));
            _transactionGate = TransactionGates.GetValue(
                _saveGameService,
                _ => new object());
        }

        private List<BuildingState> Buildings => _saveGameService.CurrentSave?.Buildings;

        public BuildingState GetBuildingState(string buildingId)
        {
            lock (_transactionGate)
            {
                return TryResolveState(buildingId, out BuildingState state, out _, out _)
                    ? CloneState(state)
                    : null;
            }
        }

        public IEnumerable<BuildingState> GetAllBuildingStates()
        {
            lock (_transactionGate)
            {
                if (Buildings == null)
                {
                    return Array.Empty<BuildingState>();
                }

                return Buildings
                    .Where(state => state != null)
                    .Select(CloneState)
                    .ToArray();
            }
        }

        public BuildingConstructionQuote GetConstructionQuote(string buildingId)
        {
            lock (_transactionGate)
            {
                return BuildQuote(buildingId);
            }
        }

        public BuildingConstructionResult TryStartConstruction(
            string buildingId,
            long requestedAtTimestamp)
        {
            lock (_transactionGate)
            {
                if (requestedAtTimestamp <= 0)
                {
                    return Result(
                        BuildingConstructionStatus.RejectedMalformedState,
                        BuildFailureQuote(
                            BuildingConstructionStatus.RejectedMalformedState,
                            buildingId,
                            InvalidTimestampCode),
                        false,
                        false,
                        InvalidTimestampCode);
                }

                if (_saveGameService.LastSaveStatus == SaveOperationStatus.CommitUncertain)
                {
                    return Result(
                        BuildingConstructionStatus.CommitUncertain,
                        BuildFailureQuote(
                            BuildingConstructionStatus.CommitUncertain,
                            buildingId,
                            CommitUncertainCode),
                        false,
                        false,
                        CommitUncertainCode);
                }

                BuildingConstructionQuote quote = BuildQuote(buildingId);
                if (!quote.CanStart)
                {
                    BuildingConstructionStatus status =
                        quote.Status == BuildingConstructionStatus.AlreadyInProgress
                            ? BuildingConstructionStatus.AlreadyInProgress
                            : quote.Status;
                    return Result(
                        status,
                        quote,
                        false,
                        false,
                        quote.DiagnosticCode);
                }

                if (!(_resourceService is IResourceIntegrityService economy))
                {
                    return Result(
                        BuildingConstructionStatus.RejectedEconomyUnavailable,
                        quote,
                        false,
                        false,
                        EconomyUnavailableCode);
                }

                long completeTimestamp;
                try
                {
                    completeTimestamp = checked(requestedAtTimestamp + quote.DurationSeconds);
                }
                catch (OverflowException)
                {
                    return Result(
                        BuildingConstructionStatus.RejectedMalformedState,
                        quote,
                        false,
                        false,
                        InvalidTimestampCode);
                }

                var appliedResources = new List<ResourceRollback>(quote.Costs.Count);
                foreach (BuildingConstructionCost cost in quote.Costs)
                {
                    EconomyMutationResult mutation =
                        economy.TryConsumeResource(cost.ResourceType, cost.Amount);
                    if (!mutation.Changed)
                    {
                        RollbackResources(economy, appliedResources);
                        BuildingConstructionStatus status =
                            mutation.Status == EconomyMutationStatus.RejectedInsufficientBalance
                                ? BuildingConstructionStatus.RejectedInsufficientResources
                                : BuildingConstructionStatus.RejectedEconomyUnavailable;
                        return Result(
                            status,
                            quote,
                            false,
                            false,
                            status == BuildingConstructionStatus.RejectedInsufficientResources
                                ? InsufficientResourcesCode
                                : EconomyUnavailableCode);
                    }

                    appliedResources.Add(
                        new ResourceRollback(
                            cost.ResourceType,
                            cost.Amount,
                            mutation.PreviousBalance ?? 0L));
                }

                bool addedState = false;
                BuildingState state;
                if (!TryResolveState(
                        buildingId,
                        out state,
                        out bool stateExists,
                        out string stateDiagnostic))
                {
                    RollbackResources(economy, appliedResources);
                    return Result(
                        BuildingConstructionStatus.RejectedMalformedState,
                        quote,
                        false,
                        false,
                        stateDiagnostic);
                }

                if (!stateExists)
                {
                    state = new BuildingState
                    {
                        BuildingId = buildingId,
                        Level = 0
                    };
                    Buildings.Add(state);
                    addedState = true;
                }

                BuildingState previousState = CloneState(state);
                state.IsUpgrading = true;
                state.UpgradeCompleteTimestamp = completeTimestamp;

                var startedQuote = new BuildingConstructionQuote(
                    BuildingConstructionStatus.Started,
                    quote.BuildingId,
                    quote.ConfirmedLevel,
                    quote.TargetLevel,
                    quote.DurationSeconds,
                    completeTimestamp,
                    quote.Costs,
                    StartedCode);

                try
                {
                    _saveGameService.Save();
                    if (_saveGameService.LastSaveStatus == SaveOperationStatus.CommitUncertain)
                    {
                        return Result(
                            BuildingConstructionStatus.CommitUncertain,
                            startedQuote,
                            true,
                            false,
                            CommitUncertainCode);
                    }

                    if (_saveGameService.LastSaveStatus != SaveOperationStatus.SavedPrimary)
                    {
                        RestoreState(state, previousState, addedState);
                        RollbackResources(economy, appliedResources);
                        return Result(
                            BuildingConstructionStatus.SaveFailedRolledBack,
                            quote,
                            false,
                            false,
                            SaveFailedCode);
                    }
                }
                catch (Exception)
                {
                    if (_saveGameService.LastSaveStatus == SaveOperationStatus.CommitUncertain)
                    {
                        return Result(
                            BuildingConstructionStatus.CommitUncertain,
                            startedQuote,
                            true,
                            false,
                            CommitUncertainCode);
                    }

                    RestoreState(state, previousState, addedState);
                    RollbackResources(economy, appliedResources);
                    return Result(
                        BuildingConstructionStatus.SaveFailedRolledBack,
                        quote,
                        false,
                        false,
                        SaveFailedCode);
                }

                Debug.Log(
                    $"Construction started for {buildingId}: Level {quote.ConfirmedLevel} to {quote.TargetLevel}.");
                return Result(
                    BuildingConstructionStatus.Started,
                    startedQuote,
                    true,
                    true,
                    StartedCode);
            }
        }

        public BuildingConstructionResult TryCompleteConstruction(
            string buildingId,
            long observedAtTimestamp)
        {
            lock (_transactionGate)
            {
                return CompleteOne(buildingId, observedAtTimestamp);
            }
        }

        public BuildingConstructionReconcileResult ReconcileCompletedConstructions(
            long observedAtTimestamp)
        {
            lock (_transactionGate)
            {
                if (observedAtTimestamp <= 0 ||
                    observedAtTimestamp <= _lastReconcileTimestamp)
                {
                    return ReconcileResult(
                        BuildingConstructionStatus.NotReady,
                        Array.Empty<string>(),
                        false,
                        NotReadyCode);
                }

                _lastReconcileTimestamp = observedAtTimestamp;
                if (_saveGameService.LastSaveStatus == SaveOperationStatus.CommitUncertain)
                {
                    return ReconcileResult(
                        BuildingConstructionStatus.CommitUncertain,
                        Array.Empty<string>(),
                        false,
                        CommitUncertainCode);
                }

                List<BuildingState> buildings = Buildings;
                if (buildings == null)
                {
                    return ReconcileResult(
                        BuildingConstructionStatus.RejectedNoCurrentSave,
                        Array.Empty<string>(),
                        false,
                        NoCurrentSaveCode);
                }

                var completedIds = new List<string>();
                var previousStates = new List<StateRollback>();
                foreach (BuildingState state in buildings.ToArray())
                {
                    if (state == null ||
                        !state.IsUpgrading ||
                        state.UpgradeCompleteTimestamp > observedAtTimestamp)
                    {
                        continue;
                    }

                    BuildingConstructionQuote quote = BuildQuote(state.BuildingId);
                    if (quote.Status != BuildingConstructionStatus.AlreadyInProgress ||
                        quote.TargetLevel != state.Level + 1)
                    {
                        Debug.LogWarning(
                            $"{MalformedStateCode}: Active construction '{state.BuildingId}' could not be reconciled.");
                        continue;
                    }

                    previousStates.Add(new StateRollback(state, CloneState(state)));
                    state.Level = quote.TargetLevel;
                    state.IsUpgrading = false;
                    state.UpgradeCompleteTimestamp = 0;
                    completedIds.Add(state.BuildingId);
                }

                if (completedIds.Count == 0)
                {
                    return ReconcileResult(
                        BuildingConstructionStatus.NotReady,
                        Array.Empty<string>(),
                        false,
                        NotReadyCode);
                }

                try
                {
                    _saveGameService.Save();
                    if (_saveGameService.LastSaveStatus == SaveOperationStatus.CommitUncertain)
                    {
                        return ReconcileResult(
                            BuildingConstructionStatus.CommitUncertain,
                            completedIds,
                            false,
                            CommitUncertainCode);
                    }

                    if (_saveGameService.LastSaveStatus != SaveOperationStatus.SavedPrimary)
                    {
                        RestoreStates(previousStates);
                        return ReconcileResult(
                            BuildingConstructionStatus.SaveFailedRolledBack,
                            Array.Empty<string>(),
                            false,
                            SaveFailedCode);
                    }
                }
                catch (Exception)
                {
                    if (_saveGameService.LastSaveStatus == SaveOperationStatus.CommitUncertain)
                    {
                        return ReconcileResult(
                            BuildingConstructionStatus.CommitUncertain,
                            completedIds,
                            false,
                            CommitUncertainCode);
                    }

                    RestoreStates(previousStates);
                    return ReconcileResult(
                        BuildingConstructionStatus.SaveFailedRolledBack,
                        Array.Empty<string>(),
                        false,
                        SaveFailedCode);
                }

                foreach (string completedId in completedIds)
                {
                    Debug.Log($"Construction completed for {completedId}.");
                }

                return ReconcileResult(
                    BuildingConstructionStatus.Completed,
                    completedIds,
                    true,
                    CompletedCode);
            }
        }

        public void StartUpgrade(string buildingId)
        {
            BuildingConstructionResult result = TryStartConstruction(
                buildingId,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (result.Status != BuildingConstructionStatus.Started &&
                result.Status != BuildingConstructionStatus.AlreadyInProgress)
            {
                Debug.LogWarning(result.DiagnosticCode);
            }
        }

        public void CompleteUpgrade(string buildingId)
        {
            BuildingConstructionResult result = TryCompleteConstruction(
                buildingId,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (result.Status != BuildingConstructionStatus.Completed &&
                result.Status != BuildingConstructionStatus.NotReady)
            {
                Debug.LogWarning(result.DiagnosticCode);
            }
        }

        private BuildingConstructionResult CompleteOne(
            string buildingId,
            long observedAtTimestamp)
        {
            if (observedAtTimestamp <= 0)
            {
                return Result(
                    BuildingConstructionStatus.RejectedMalformedState,
                    BuildFailureQuote(
                        BuildingConstructionStatus.RejectedMalformedState,
                        buildingId,
                        InvalidTimestampCode),
                    false,
                    false,
                    InvalidTimestampCode);
            }

            if (_saveGameService.LastSaveStatus == SaveOperationStatus.CommitUncertain)
            {
                return Result(
                    BuildingConstructionStatus.CommitUncertain,
                    BuildFailureQuote(
                        BuildingConstructionStatus.CommitUncertain,
                        buildingId,
                        CommitUncertainCode),
                    false,
                    false,
                    CommitUncertainCode);
            }

            BuildingConstructionQuote quote = BuildQuote(buildingId);
            if (quote.Status != BuildingConstructionStatus.AlreadyInProgress)
            {
                return Result(
                    quote.Status == BuildingConstructionStatus.Available
                        ? BuildingConstructionStatus.NotReady
                        : quote.Status,
                    quote,
                    false,
                    false,
                    quote.Status == BuildingConstructionStatus.Available
                        ? NotReadyCode
                        : quote.DiagnosticCode);
            }

            if (quote.CompleteTimestamp > observedAtTimestamp)
            {
                return Result(
                    BuildingConstructionStatus.NotReady,
                    quote,
                    false,
                    false,
                    NotReadyCode);
            }

            if (!TryResolveState(
                    buildingId,
                    out BuildingState state,
                    out bool stateExists,
                    out string diagnosticCode) ||
                !stateExists)
            {
                return Result(
                    BuildingConstructionStatus.RejectedMalformedState,
                    quote,
                    false,
                    false,
                    diagnosticCode);
            }

            BuildingState previousState = CloneState(state);
            state.Level = quote.TargetLevel;
            state.IsUpgrading = false;
            state.UpgradeCompleteTimestamp = 0;

            var completedQuote = new BuildingConstructionQuote(
                BuildingConstructionStatus.Completed,
                buildingId,
                state.Level,
                state.Level,
                0,
                0,
                Array.Empty<BuildingConstructionCost>(),
                CompletedCode);
            try
            {
                _saveGameService.Save();
                if (_saveGameService.LastSaveStatus == SaveOperationStatus.CommitUncertain)
                {
                    return Result(
                        BuildingConstructionStatus.CommitUncertain,
                        completedQuote,
                        true,
                        false,
                        CommitUncertainCode);
                }

                if (_saveGameService.LastSaveStatus != SaveOperationStatus.SavedPrimary)
                {
                    RestoreState(state, previousState, false);
                    return Result(
                        BuildingConstructionStatus.SaveFailedRolledBack,
                        quote,
                        false,
                        false,
                        SaveFailedCode);
                }
            }
            catch (Exception)
            {
                if (_saveGameService.LastSaveStatus == SaveOperationStatus.CommitUncertain)
                {
                    return Result(
                        BuildingConstructionStatus.CommitUncertain,
                        completedQuote,
                        true,
                        false,
                        CommitUncertainCode);
                }

                RestoreState(state, previousState, false);
                return Result(
                    BuildingConstructionStatus.SaveFailedRolledBack,
                    quote,
                    false,
                    false,
                    SaveFailedCode);
            }

            Debug.Log($"Construction completed for {buildingId} at Level {state.Level}.");
            return Result(
                BuildingConstructionStatus.Completed,
                completedQuote,
                true,
                true,
                CompletedCode);
        }

        private BuildingConstructionQuote BuildQuote(string buildingId)
        {
            if (string.IsNullOrWhiteSpace(buildingId))
            {
                return BuildFailureQuote(
                    BuildingConstructionStatus.RejectedUnsupportedBuilding,
                    buildingId,
                    UnsupportedBuildingCode);
            }

            if (Buildings == null)
            {
                return BuildFailureQuote(
                    BuildingConstructionStatus.RejectedNoCurrentSave,
                    buildingId,
                    NoCurrentSaveCode);
            }

            BuildingDefinition definition = _gameDataService.GetBuilding(buildingId);
            if (definition == null ||
                !string.Equals(definition.Id, buildingId, StringComparison.Ordinal))
            {
                return BuildFailureQuote(
                    BuildingConstructionStatus.RejectedUnsupportedBuilding,
                    buildingId,
                    UnsupportedBuildingCode);
            }

            if (definition.MaxLevel <= 0 ||
                definition.MaxLevel > MaximumSupportedLevel ||
                definition.ConstructionLevels == null)
            {
                return BuildFailureQuote(
                    BuildingConstructionStatus.RejectedInvalidDefinition,
                    buildingId,
                    InvalidDefinitionCode);
            }

            if (!TryResolveState(
                    buildingId,
                    out BuildingState state,
                    out bool stateExists,
                    out string stateDiagnostic))
            {
                return BuildFailureQuote(
                    BuildingConstructionStatus.RejectedMalformedState,
                    buildingId,
                    stateDiagnostic);
            }

            int confirmedLevel = stateExists ? state.Level : 0;
            if (confirmedLevel < 0 ||
                confirmedLevel > definition.MaxLevel ||
                confirmedLevel > MaximumSupportedLevel)
            {
                return new BuildingConstructionQuote(
                    BuildingConstructionStatus.RejectedMalformedState,
                    buildingId,
                    confirmedLevel,
                    confirmedLevel,
                    0,
                    0,
                    Array.Empty<BuildingConstructionCost>(),
                    MalformedStateCode);
            }

            if (stateExists && state.IsUpgrading)
            {
                if (state.UpgradeCompleteTimestamp <= 0 ||
                    confirmedLevel >= definition.MaxLevel)
                {
                    return new BuildingConstructionQuote(
                        BuildingConstructionStatus.RejectedMalformedState,
                        buildingId,
                        confirmedLevel,
                        confirmedLevel,
                        0,
                        state.UpgradeCompleteTimestamp,
                        Array.Empty<BuildingConstructionCost>(),
                        MalformedStateCode);
                }

                if (!TryGetLevelDefinition(
                        definition,
                        confirmedLevel + 1,
                        out BuildingConstructionLevelDefinition activeLevel,
                        out IReadOnlyList<BuildingConstructionCost> activeCosts))
                {
                    return new BuildingConstructionQuote(
                        BuildingConstructionStatus.RejectedInvalidDefinition,
                        buildingId,
                        confirmedLevel,
                        confirmedLevel + 1,
                        0,
                        state.UpgradeCompleteTimestamp,
                        Array.Empty<BuildingConstructionCost>(),
                        InvalidDefinitionCode);
                }

                return new BuildingConstructionQuote(
                    BuildingConstructionStatus.AlreadyInProgress,
                    buildingId,
                    confirmedLevel,
                    confirmedLevel + 1,
                    activeLevel.DurationSeconds,
                    state.UpgradeCompleteTimestamp,
                    activeCosts,
                    InProgressCode);
            }

            if (confirmedLevel >= definition.MaxLevel)
            {
                return new BuildingConstructionQuote(
                    BuildingConstructionStatus.MaxLevel,
                    buildingId,
                    confirmedLevel,
                    confirmedLevel,
                    0,
                    0,
                    Array.Empty<BuildingConstructionCost>(),
                    MaxLevelCode);
            }

            int targetLevel = confirmedLevel + 1;
            if (!TryGetLevelDefinition(
                    definition,
                    targetLevel,
                    out BuildingConstructionLevelDefinition level,
                    out IReadOnlyList<BuildingConstructionCost> costs))
            {
                return new BuildingConstructionQuote(
                    BuildingConstructionStatus.RejectedInvalidDefinition,
                    buildingId,
                    confirmedLevel,
                    targetLevel,
                    0,
                    0,
                    Array.Empty<BuildingConstructionCost>(),
                    InvalidDefinitionCode);
            }

            return new BuildingConstructionQuote(
                BuildingConstructionStatus.Available,
                buildingId,
                confirmedLevel,
                targetLevel,
                level.DurationSeconds,
                0,
                costs,
                string.Empty);
        }

        private static bool TryGetLevelDefinition(
            BuildingDefinition definition,
            int targetLevel,
            out BuildingConstructionLevelDefinition level,
            out IReadOnlyList<BuildingConstructionCost> costs)
        {
            level = null;
            costs = Array.Empty<BuildingConstructionCost>();
            if (definition?.ConstructionLevels == null)
            {
                return false;
            }

            BuildingConstructionLevelDefinition[] matches = definition.ConstructionLevels
                .Where(candidate => candidate != null && candidate.TargetLevel == targetLevel)
                .ToArray();
            if (matches.Length != 1 ||
                matches[0].DurationSeconds <= 0 ||
                matches[0].Costs == null ||
                matches[0].Costs.Count == 0)
            {
                return false;
            }

            var seen = new HashSet<ResourceType>();
            var resolvedCosts =
                new List<BuildingConstructionCost>(matches[0].Costs.Count);
            foreach (BuildingConstructionCostDefinition cost in matches[0].Costs)
            {
                if (cost == null ||
                    !ResourceRules.IsSupportedWalletResource(cost.ResourceType) ||
                    cost.Amount <= 0 ||
                    !seen.Add(cost.ResourceType))
                {
                    return false;
                }

                resolvedCosts.Add(
                    new BuildingConstructionCost(cost.ResourceType, cost.Amount));
            }

            level = matches[0];
            costs = resolvedCosts.AsReadOnly();
            return true;
        }

        private bool TryResolveState(
            string buildingId,
            out BuildingState state,
            out bool stateExists,
            out string diagnosticCode)
        {
            state = null;
            stateExists = false;
            diagnosticCode = string.Empty;
            List<BuildingState> buildings = Buildings;
            if (buildings == null)
            {
                diagnosticCode = NoCurrentSaveCode;
                return false;
            }

            if (buildings.Any(candidate => candidate == null))
            {
                diagnosticCode = MalformedStateCode;
                return false;
            }

            BuildingState[] matches = buildings
                .Where(candidate => string.Equals(
                    candidate.BuildingId,
                    buildingId,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length > 1)
            {
                diagnosticCode = MalformedStateCode;
                return false;
            }

            if (matches.Length == 1)
            {
                state = matches[0];
                stateExists = true;
            }

            return true;
        }

        private void RestoreState(
            BuildingState state,
            BuildingState previous,
            bool removeAddedState)
        {
            if (removeAddedState)
            {
                Buildings?.Remove(state);
                return;
            }

            if (state != null && previous != null)
            {
                state.BuildingId = previous.BuildingId;
                state.Level = previous.Level;
                state.IsUpgrading = previous.IsUpgrading;
                state.UpgradeCompleteTimestamp = previous.UpgradeCompleteTimestamp;
            }
        }

        private static void RestoreStates(IEnumerable<StateRollback> rollbacks)
        {
            foreach (StateRollback rollback in rollbacks)
            {
                BuildingState state = rollback.State;
                BuildingState previous = rollback.Previous;
                state.BuildingId = previous.BuildingId;
                state.Level = previous.Level;
                state.IsUpgrading = previous.IsUpgrading;
                state.UpgradeCompleteTimestamp = previous.UpgradeCompleteTimestamp;
            }
        }

        private void RollbackResources(
            IResourceIntegrityService economy,
            IReadOnlyList<ResourceRollback> appliedResources)
        {
            for (int index = appliedResources.Count - 1; index >= 0; index--)
            {
                ResourceRollback rollback = appliedResources[index];
                EconomyMutationResult result =
                    economy.TryAddResource(rollback.ResourceType, rollback.Amount);
                if (result.Status == EconomyMutationStatus.Applied &&
                    result.CurrentBalance == rollback.PreviousBalance)
                {
                    continue;
                }

                ResourceData entry = _saveGameService.CurrentSave?.Resources?
                    .SingleOrDefault(candidate =>
                        candidate != null &&
                        candidate.Type == rollback.ResourceType);
                if (entry != null)
                {
                    entry.Amount = rollback.PreviousBalance;
                }
            }
        }

        private static BuildingState CloneState(BuildingState state)
        {
            return state == null
                ? null
                : new BuildingState
                {
                    BuildingId = state.BuildingId,
                    Level = state.Level,
                    IsUpgrading = state.IsUpgrading,
                    UpgradeCompleteTimestamp = state.UpgradeCompleteTimestamp
                };
        }

        private static BuildingConstructionQuote BuildFailureQuote(
            BuildingConstructionStatus status,
            string buildingId,
            string diagnosticCode)
        {
            return new BuildingConstructionQuote(
                status,
                buildingId,
                0,
                0,
                0,
                0,
                Array.Empty<BuildingConstructionCost>(),
                diagnosticCode);
        }

        private static BuildingConstructionResult Result(
            BuildingConstructionStatus status,
            BuildingConstructionQuote quote,
            bool changed,
            bool persisted,
            string diagnosticCode)
        {
            return new BuildingConstructionResult(
                status,
                quote,
                changed,
                persisted,
                diagnosticCode);
        }

        private static BuildingConstructionReconcileResult ReconcileResult(
            BuildingConstructionStatus status,
            IEnumerable<string> completedIds,
            bool persisted,
            string diagnosticCode)
        {
            return new BuildingConstructionReconcileResult(
                status,
                completedIds,
                persisted,
                diagnosticCode);
        }

        private readonly struct ResourceRollback
        {
            public ResourceRollback(
                ResourceType resourceType,
                long amount,
                long previousBalance)
            {
                ResourceType = resourceType;
                Amount = amount;
                PreviousBalance = previousBalance;
            }

            public ResourceType ResourceType { get; }
            public long Amount { get; }
            public long PreviousBalance { get; }
        }

        private readonly struct StateRollback
        {
            public StateRollback(BuildingState state, BuildingState previous)
            {
                State = state;
                Previous = previous;
            }

            public BuildingState State { get; }
            public BuildingState Previous { get; }
        }
    }
}
