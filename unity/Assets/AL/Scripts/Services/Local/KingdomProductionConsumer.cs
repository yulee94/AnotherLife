using System;
using System.Collections.Generic;
using System.IO;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public static class KingdomProductionConsumer
    {
        public static KingdomProductionContributionProvider CreateLive(ISaveGameService saveGameService)
        {
            if (saveGameService == null)
            {
                throw new ArgumentNullException(nameof(saveGameService));
            }

            return new KingdomProductionContributionProvider(
                saveGameService,
                LoadLiveProfileOrIneligible(),
                new BuildingServiceProductionLevelSnapshotSource(),
                FailClosedTerritoryIncomeSnapshotSource.Instance,
                saveGameService as IProfileWriteAuthorityProvider);
        }

        public static void BindBuildingService(
            IEconomyProductionContributionProvider provider,
            IBuildingService buildingService)
        {
            if (provider is KingdomProductionContributionProvider live)
            {
                live.BindBuildingService(buildingService);
            }
        }

        private static KingdomProductionProfileSnapshot LoadLiveProfileOrIneligible()
        {
            try
            {
                string path = Path.GetFullPath(
                    Path.Combine(
                        Application.dataPath,
                        "..",
                        KingdomProductionProfileCatalog.LiveLedgerRelativePath));
                if (File.Exists(path))
                {
                    KingdomProductionProfileLoadResult result =
                        KingdomProductionProfileCatalog.TryBindAuthorityLedger(File.ReadAllBytes(path));
                    if (result.Snapshot != null)
                    {
                        return result.Snapshot;
                    }
                }
            }
            catch (Exception)
            {
            }

            return new KingdomProductionProfileSnapshot(
                KingdomProductionProfileCatalog.CatalogId,
                "missing-live-ledger",
                string.Empty,
                KingdomProductionProfileCatalog.AuthorityLedgerId,
                false,
                0,
                Array.Empty<KingdomProductionContributionRule>());
        }
    }

    public sealed class BuildingServiceProductionLevelSnapshotSource : IEconomyBuildingLevelSnapshotSource
    {
        private IBuildingService _buildingService;

        public void Bind(IBuildingService buildingService)
        {
            _buildingService = buildingService;
        }

        public bool TryCaptureBuildingLevels(
            out IReadOnlyDictionary<string, int> buildingLevels,
            out EconomyDiagnostic diagnostic)
        {
            buildingLevels = null;
            diagnostic = default;
            if (_buildingService == null)
            {
                diagnostic = new EconomyDiagnostic(
                    EconomyDiagnosticCodes.ProductionCatalog,
                    "Production.Buildings");
                return false;
            }

            IEnumerable<BuildingState> states;
            try
            {
                states = _buildingService.GetAllBuildingStates();
            }
            catch (Exception)
            {
                diagnostic = new EconomyDiagnostic(
                    EconomyDiagnosticCodes.ProductionCatalog,
                    "Production.Buildings");
                return false;
            }

            return KingdomProductionContributionProvider.TryCaptureBuildingLevelSnapshot(
                states,
                out buildingLevels,
                out diagnostic);
        }
    }

    public sealed class SaveBuildingLevelSnapshotSource : IEconomyBuildingLevelSnapshotSource
    {
        private readonly ISaveGameService _saveGameService;

        public SaveBuildingLevelSnapshotSource(ISaveGameService saveGameService)
        {
            _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
        }

        public bool TryCaptureBuildingLevels(
            out IReadOnlyDictionary<string, int> buildingLevels,
            out EconomyDiagnostic diagnostic)
        {
            buildingLevels = null;
            diagnostic = default;
            SaveGameData save;
            try
            {
                save = _saveGameService.CurrentSave;
            }
            catch (Exception)
            {
                diagnostic = new EconomyDiagnostic(
                    EconomyDiagnosticCodes.NoCurrentSave,
                    "Production.Save");
                return false;
            }

            return KingdomProductionContributionProvider.TryCaptureBuildingLevelSnapshot(
                save?.Buildings,
                out buildingLevels,
                out diagnostic);
        }
    }

    public sealed class AvailableEmptyTerritoryIncomeSnapshotSource : IEconomyTerritoryIncomeSnapshotSource
    {
        public static readonly AvailableEmptyTerritoryIncomeSnapshotSource Instance =
            new AvailableEmptyTerritoryIncomeSnapshotSource();

        private AvailableEmptyTerritoryIncomeSnapshotSource()
        {
        }

        public bool TryCaptureTerritoryIncome(
            RealmId realmId,
            double deltaSeconds,
            out IReadOnlyList<EconomyProductionContribution> contributions,
            out EconomyDiagnostic diagnostic)
        {
            contributions = Array.Empty<EconomyProductionContribution>();
            diagnostic = default;
            return true;
        }
    }

    public sealed class FailClosedTerritoryIncomeSnapshotSource : IEconomyTerritoryIncomeSnapshotSource
    {
        public static readonly FailClosedTerritoryIncomeSnapshotSource Instance =
            new FailClosedTerritoryIncomeSnapshotSource();

        private FailClosedTerritoryIncomeSnapshotSource()
        {
        }

        public bool TryCaptureTerritoryIncome(
            RealmId realmId,
            double deltaSeconds,
            out IReadOnlyList<EconomyProductionContribution> contributions,
            out EconomyDiagnostic diagnostic)
        {
            contributions = Array.Empty<EconomyProductionContribution>();
            diagnostic = new EconomyDiagnostic(
                EconomyDiagnosticCodes.ProductionCatalog,
                "Production.Territory");
            return false;
        }
    }
}
