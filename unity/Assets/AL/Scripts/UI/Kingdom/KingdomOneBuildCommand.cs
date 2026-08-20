using System;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.Data.Runtime;
using AL.Services.Local;
using AL.UI.FirstUserIdentity;

namespace AL.UI.Kingdom
{
    public readonly struct KingdomOneBuildResult
    {
        internal KingdomOneBuildResult(
            bool accepted,
            bool persisted,
            string buildingId,
            string catalogBuildingId,
            int level,
            string message)
        {
            Accepted = accepted;
            Persisted = persisted;
            BuildingId = buildingId ?? string.Empty;
            CatalogBuildingId = catalogBuildingId ?? string.Empty;
            Level = level;
            Message = message ?? string.Empty;
        }

        public bool Accepted { get; }
        public bool Persisted { get; }
        public string BuildingId { get; }
        public string CatalogBuildingId { get; }
        public int Level { get; }
        public string Message { get; }
    }

    /// <summary>
    /// The one production Kingdom construct after the greybox duel. Uses catalog
    /// building <c>town_hall</c> (runtime/save id <c>TownHall</c>) and persists
    /// through <see cref="ISaveGameService"/> via the existing MVP-loop adapter.
    /// Does not invent buildings, costs, or a parallel greybox save file.
    /// </summary>
    public static class KingdomOneBuildCommand
    {
        public const string CatalogBuildingId = "town_hall";
        public const string BuildingId = MvpLoopSaveCodec.DefaultOneBuildId;
        public const int CompletedLevel = 1;

        public static bool IsOneBuild(string buildingId)
        {
            return string.Equals(buildingId, BuildingId, StringComparison.Ordinal);
        }

        public static bool IsOneBuildCommand(string commandId)
        {
            return string.Equals(
                commandId,
                KingdomCommandPolicy.TownHallUpgrade,
                StringComparison.Ordinal);
        }

        public static KingdomOneBuildResult TryExecute(
            ISaveGameService saveGameService,
            IGameDataService gameDataService)
        {
            if (saveGameService?.CurrentSave == null)
            {
                return Fail("TOWN HALL UNAVAILABLE: no writable kingdom profile.");
            }

            if (gameDataService == null || !CatalogDefinesTownHall(gameDataService))
            {
                return Fail("TOWN HALL UNAVAILABLE: construction definition is not approved.");
            }

            MvpLoopSnapshot snapshot = MvpLoopSaveCodec.Read(saveGameService.CurrentSave);
            if (IsOneBuild(snapshot.LastBuildId) && snapshot.LastBuildLevel >= CompletedLevel)
            {
                return new KingdomOneBuildResult(
                    true,
                    false,
                    BuildingId,
                    CatalogBuildingId,
                    snapshot.LastBuildLevel,
                    "TOWN HALL ALREADY BUILT: catalog " +
                    CatalogBuildingId +
                    " remains Lv " +
                    snapshot.LastBuildLevel +
                    ".");
            }

            if (!snapshot.ClassFamily.HasValue ||
                !FirstUserIdentityDerivation.IsSupportedRealm(snapshot.Realm) ||
                !FirstUserIdentityDerivation.IsSupportedClassFamily(snapshot.ClassFamily.Value))
            {
                return Fail("TOWN HALL UNAVAILABLE: champion identity is required.");
            }

            MvpLoopCommitResult commit = MvpLoopSaveAuthority.TryCommit(
                saveGameService,
                new MvpLoopCommitRequest(
                    Guid.NewGuid().ToString("N"),
                    snapshot.Realm,
                    snapshot.ClassFamily.Value,
                    snapshot.IdentityConfirmed,
                    snapshot.LastResultId,
                    BuildingId,
                    CompletedLevel));
            if (commit == null || !commit.Accepted)
            {
                return Fail(
                    "TOWN HALL ORDER NOT COMMITTED: " +
                    (commit == null || string.IsNullOrWhiteSpace(commit.Message)
                        ? "profile authority rejected the construct."
                        : commit.Message));
            }

            int level = MvpLoopSaveCodec.Read(saveGameService.CurrentSave).LastBuildLevel;
            return new KingdomOneBuildResult(
                true,
                commit.Persisted,
                BuildingId,
                CatalogBuildingId,
                level,
                commit.Persisted
                    ? "TOWN HALL CONSTRUCTED: catalog " +
                      CatalogBuildingId +
                      " is now Lv " +
                      level +
                      " and saved."
                    : "TOWN HALL ALREADY BUILT: catalog " +
                      CatalogBuildingId +
                      " remains Lv " +
                      level +
                      ".");
        }

        private static bool CatalogDefinesTownHall(IGameDataService gameDataService)
        {
            BuildingDefinition byLegacy = gameDataService.GetBuilding(BuildingId);
            if (byLegacy != null)
            {
                return true;
            }

            return gameDataService.GetBuilding(CatalogBuildingId) != null;
        }

        private static KingdomOneBuildResult Fail(string message)
        {
            return new KingdomOneBuildResult(
                false,
                false,
                BuildingId,
                CatalogBuildingId,
                0,
                message);
        }
    }
}
