using System;
using System.Collections.Generic;
using AL.Core;
using AL.Data.Runtime;

namespace AL.Core.Interfaces
{
    public enum BuildingConstructionStatus
    {
        Unknown = 0,
        Available = 1,
        Started = 2,
        AlreadyInProgress = 3,
        NotReady = 4,
        Completed = 5,
        MaxLevel = 6,
        RejectedNoCurrentSave = 7,
        RejectedUnsupportedBuilding = 8,
        RejectedInvalidDefinition = 9,
        RejectedMalformedState = 10,
        RejectedInsufficientResources = 11,
        RejectedEconomyUnavailable = 12,
        SaveFailedRolledBack = 13,
        CommitUncertain = 14
    }

    public readonly struct BuildingConstructionCost
    {
        public BuildingConstructionCost(ResourceType resourceType, long amount)
        {
            ResourceType = resourceType;
            Amount = amount;
        }

        public ResourceType ResourceType { get; }
        public long Amount { get; }
    }

    public sealed class BuildingConstructionQuote
    {
        private readonly IReadOnlyList<BuildingConstructionCost> _costs;

        public BuildingConstructionQuote(
            BuildingConstructionStatus status,
            string buildingId,
            int confirmedLevel,
            int targetLevel,
            int durationSeconds,
            long completeTimestamp,
            IEnumerable<BuildingConstructionCost> costs,
            string diagnosticCode)
        {
            Status = status;
            BuildingId = buildingId ?? string.Empty;
            ConfirmedLevel = confirmedLevel;
            TargetLevel = targetLevel;
            DurationSeconds = durationSeconds;
            CompleteTimestamp = completeTimestamp;
            _costs = Array.AsReadOnly(
                costs == null
                    ? Array.Empty<BuildingConstructionCost>()
                    : new List<BuildingConstructionCost>(costs).ToArray());
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public BuildingConstructionStatus Status { get; }
        public string BuildingId { get; }
        public int ConfirmedLevel { get; }
        public int TargetLevel { get; }
        public int DurationSeconds { get; }
        public long CompleteTimestamp { get; }
        public IReadOnlyList<BuildingConstructionCost> Costs => _costs;
        public string DiagnosticCode { get; }
        public bool CanStart => Status == BuildingConstructionStatus.Available;
    }

    public sealed class BuildingConstructionResult
    {
        public BuildingConstructionResult(
            BuildingConstructionStatus status,
            BuildingConstructionQuote quote,
            bool changed,
            bool persisted,
            string diagnosticCode)
        {
            Status = status;
            Quote = quote;
            Changed = changed;
            Persisted = persisted;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public BuildingConstructionStatus Status { get; }
        public BuildingConstructionQuote Quote { get; }
        public bool Changed { get; }
        public bool Persisted { get; }
        public string DiagnosticCode { get; }
    }

    public sealed class BuildingConstructionReconcileResult
    {
        private readonly IReadOnlyList<string> _completedBuildingIds;

        public BuildingConstructionReconcileResult(
            BuildingConstructionStatus status,
            IEnumerable<string> completedBuildingIds,
            bool persisted,
            string diagnosticCode)
        {
            Status = status;
            _completedBuildingIds = Array.AsReadOnly(
                completedBuildingIds == null
                    ? Array.Empty<string>()
                    : new List<string>(completedBuildingIds).ToArray());
            Persisted = persisted;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public BuildingConstructionStatus Status { get; }
        public IReadOnlyList<string> CompletedBuildingIds => _completedBuildingIds;
        public bool Changed => CompletedBuildingIds.Count > 0;
        public bool Persisted { get; }
        public string DiagnosticCode { get; }
    }

    public interface IBuildingService
    {
        BuildingState GetBuildingState(string buildingId);
        IEnumerable<BuildingState> GetAllBuildingStates();
        BuildingConstructionQuote GetConstructionQuote(string buildingId);
        BuildingConstructionResult TryStartConstruction(string buildingId, long requestedAtTimestamp);
        BuildingConstructionResult TryCompleteConstruction(string buildingId, long observedAtTimestamp);
        BuildingConstructionReconcileResult ReconcileCompletedConstructions(long observedAtTimestamp);

        // Compatibility wrappers for prototype callers. Production callers use
        // the typed construction transaction methods above.
        void StartUpgrade(string buildingId);
        void CompleteUpgrade(string buildingId);
    }
}
