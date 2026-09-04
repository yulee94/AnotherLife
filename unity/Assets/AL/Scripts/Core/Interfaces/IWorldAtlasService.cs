using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;

namespace AL.Core.Interfaces
{
    public enum WorldAtlasServiceQueryStatus
    {
        Available = 0,
        AvailableWithDiagnostics = 1,
        UnknownId = 2,
        InvalidId = 3,
        InvalidViewer = 4
    }

    public sealed class WorldAtlasServiceDiagnostic
    {
        public WorldAtlasServiceDiagnostic(string code, string message)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string Message { get; }
    }

    public sealed class WorldAtlasServiceQueryResult<T>
    {
        public WorldAtlasServiceQueryResult(
            WorldAtlasServiceQueryStatus status,
            T value,
            IEnumerable<WorldAtlasServiceDiagnostic> diagnostics)
        {
            Status = status;
            Value = value;
            Diagnostics = Array.AsReadOnly(
                (diagnostics ?? Array.Empty<WorldAtlasServiceDiagnostic>()).ToArray());
        }

        public WorldAtlasServiceQueryStatus Status { get; }
        public T Value { get; }
        public IReadOnlyList<WorldAtlasServiceDiagnostic> Diagnostics { get; }
        public bool IsAvailable =>
            Status == WorldAtlasServiceQueryStatus.Available ||
            Status == WorldAtlasServiceQueryStatus.AvailableWithDiagnostics;
    }

    public sealed class WorldZoneData
    {
        public WorldZoneData(
            string id,
            string displayName,
            RealmId homeRealm,
            string safetyLayer,
            string terrainTheme,
            string sceneHint,
            IEnumerable<WorldObjectiveData> objectives)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            HomeRealm = homeRealm;
            SafetyLayer = safetyLayer ?? string.Empty;
            TerrainTheme = terrainTheme ?? string.Empty;
            SceneHint = sceneHint ?? string.Empty;
            Objectives = Array.AsReadOnly((objectives ?? Array.Empty<WorldObjectiveData>()).ToArray());
        }

        public string Id { get; }
        public string DisplayName { get; }
        public RealmId HomeRealm { get; }
        public string SafetyLayer { get; }
        public string TerrainTheme { get; }
        public string SceneHint { get; }
        public IReadOnlyList<WorldObjectiveData> Objectives { get; }
    }

    public sealed class WorldObjectiveData
    {
        public WorldObjectiveData(
            string id,
            string displayName,
            string objectiveType,
            RealmId ownerRealm,
            ResourceType rareResourceReward,
            string narrativeKey,
            string description,
            bool isWarzoneObjective)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ObjectiveType = objectiveType ?? string.Empty;
            OwnerRealm = ownerRealm;
            RareResourceReward = rareResourceReward;
            NarrativeKey = narrativeKey ?? string.Empty;
            Description = description ?? string.Empty;
            IsWarzoneObjective = isWarzoneObjective;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string ObjectiveType { get; }
        public RealmId OwnerRealm { get; }
        public ResourceType RareResourceReward { get; }
        public string NarrativeKey { get; }
        public string Description { get; }
        public bool IsWarzoneObjective { get; }
    }

    public sealed class WorldNarrationSnapshot
    {
        public WorldNarrationSnapshot(
            RealmId viewerRealm,
            IEnumerable<WorldZoneData> visibleZones,
            IEnumerable<WorldObjectiveData> activeObjectives,
            IEnumerable<string> conflictHints)
        {
            ViewerRealm = viewerRealm;
            VisibleZones = Array.AsReadOnly((visibleZones ?? Array.Empty<WorldZoneData>()).ToArray());
            ActiveObjectives = Array.AsReadOnly((activeObjectives ?? Array.Empty<WorldObjectiveData>()).ToArray());
            ConflictHints = Array.AsReadOnly((conflictHints ?? Array.Empty<string>()).ToArray());
        }

        public RealmId ViewerRealm { get; }
        public IReadOnlyList<WorldZoneData> VisibleZones { get; }
        public IReadOnlyList<WorldObjectiveData> ActiveObjectives { get; }
        public IReadOnlyList<string> ConflictHints { get; }
    }

    public interface IWorldAtlasService
    {
        WorldAtlasServiceQueryResult<IReadOnlyList<WorldZoneData>> GetAllZones();
        WorldAtlasServiceQueryResult<IReadOnlyList<WorldZoneData>> GetZonesForRealm(RealmId realmId);
        WorldAtlasServiceQueryResult<WorldZoneData> GetZone(string zoneId);
        WorldAtlasServiceQueryResult<IReadOnlyList<WorldObjectiveData>> GetObjectivesForRealm(RealmId viewerRealm);
        WorldAtlasServiceQueryResult<WorldNarrationSnapshot> GetNarrationSnapshot(RealmId viewerRealm);
    }
}
