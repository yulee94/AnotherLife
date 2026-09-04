using System;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;

namespace AL.Services.Local
{
    public sealed class KingdomProductionContributionProvider : IEconomyProductionContributionProvider
    {
        private readonly ISaveGameService _saveGameService;
        private readonly KingdomProductionProfileSnapshot _profile;
        private readonly IEconomyBuildingLevelSnapshotSource _buildingLevels;
        private readonly IEconomyTerritoryIncomeSnapshotSource _territoryIncome;
        private readonly IProfileWriteAuthorityProvider _writeAuthority;

        public KingdomProductionContributionProvider(
            ISaveGameService saveGameService,
            KingdomProductionProfileSnapshot profile)
            : this(
                saveGameService,
                profile,
                new SaveBuildingLevelSnapshotSource(saveGameService),
                AvailableEmptyTerritoryIncomeSnapshotSource.Instance,
                saveGameService as IProfileWriteAuthorityProvider)
        {
        }

        public KingdomProductionContributionProvider(
            ISaveGameService saveGameService,
            KingdomProductionProfileSnapshot profile,
            IEconomyBuildingLevelSnapshotSource buildingLevels,
            IEconomyTerritoryIncomeSnapshotSource territoryIncome,
            IProfileWriteAuthorityProvider writeAuthority)
        {
            _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _buildingLevels = buildingLevels ?? throw new ArgumentNullException(nameof(buildingLevels));
            _territoryIncome = territoryIncome ?? throw new ArgumentNullException(nameof(territoryIncome));
            _writeAuthority = writeAuthority;
        }

        internal void BindBuildingService(IBuildingService buildingService)
        {
            if (_buildingLevels is BuildingServiceProductionLevelSnapshotSource live)
            {
                live.Bind(buildingService);
            }
        }

        public EconomyProductionContributionSnapshot BuildContributions(double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds) ||
                double.IsInfinity(deltaSeconds) ||
                deltaSeconds <= 0d ||
                deltaSeconds > 1d)
            {
                return Unavailable(
                    EconomyDiagnosticCodes.ProductionElapsed,
                    "Production.DeltaSeconds");
            }

            return BuildElapsedContributions(deltaSeconds, capPerTickScale: 1d);
        }

        public EconomyProductionContributionSnapshot BuildCatchUpContributions(long elapsedSeconds)
        {
            if (elapsedSeconds <= 0L ||
                _profile.MaxOfflineElapsedSeconds <= 0L ||
                elapsedSeconds > _profile.MaxOfflineElapsedSeconds)
            {
                return Unavailable(
                    EconomyDiagnosticCodes.ProductionElapsed,
                    "Production.CatchUp.ElapsedSeconds");
            }

            return BuildElapsedContributions(elapsedSeconds, capPerTickScale: elapsedSeconds);
        }

        private EconomyProductionContributionSnapshot BuildElapsedContributions(
            double elapsedSeconds,
            double capPerTickScale)
        {
            SaveGameData save;
            try
            {
                save = _saveGameService.CurrentSave;
            }
            catch (Exception)
            {
                return Unavailable(
                    EconomyDiagnosticCodes.NoCurrentSave,
                    "Production.Save");
            }

            if (save == null)
            {
                return Unavailable(
                    EconomyDiagnosticCodes.NoCurrentSave,
                    "Production.Save");
            }

            if (IsUncertainOrDegraded(_saveGameService))
            {
                return Unavailable(
                    EconomyDiagnosticCodes.ProductionProfile,
                    "Production.Save.Authority");
            }

            if (save.SaveSchemaVersion != SaveGameData.CurrentSaveSchemaVersion ||
                string.IsNullOrWhiteSpace(save.ProfileId) ||
                !IsSafeProfileIdentity(save.ProfileId))
            {
                return Unavailable(
                    EconomyDiagnosticCodes.ProductionProfile,
                    "Production.Profile");
            }

            if (_writeAuthority != null &&
                !ProfileWriteAuthorityProviderGuard.IsCurrentWritable(_writeAuthority))
            {
                return Unavailable(
                    EconomyDiagnosticCodes.ProductionProfile,
                    "Production.Profile.Writable");
            }

            if (!ResourceRules.TryGetRareResourceForRealm(save.SelectedRealm, out _))
            {
                return Unavailable(
                    EconomyDiagnosticCodes.ProductionRealm,
                    "Production.Realm");
            }

            if (!_profile.ProductionEligible ||
                _profile.Contributions == null ||
                _profile.Contributions.Count == 0 ||
                string.IsNullOrWhiteSpace(_profile.SourceSha256))
            {
                return Unavailable(
                    EconomyDiagnosticCodes.ProductionCatalog,
                    "Production.Catalog");
            }

            IReadOnlyDictionary<string, int> buildingLevels;
            EconomyDiagnostic buildingDiagnostic;
            try
            {
                if (!_buildingLevels.TryCaptureBuildingLevels(out buildingLevels, out buildingDiagnostic) ||
                    buildingLevels == null)
                {
                    return Unavailable(buildingDiagnostic.Code, buildingDiagnostic.RecordPath);
                }
            }
            catch (Exception)
            {
                return Unavailable(
                    EconomyDiagnosticCodes.ProductionCatalog,
                    "Production.Buildings");
            }

            IReadOnlyList<EconomyProductionContribution> territoryContributions;
            EconomyDiagnostic territoryDiagnostic;
            try
            {
                if (!_territoryIncome.TryCaptureTerritoryIncome(
                        save.SelectedRealm,
                        elapsedSeconds,
                        out territoryContributions,
                        out territoryDiagnostic) ||
                    territoryContributions == null)
                {
                    return Unavailable(territoryDiagnostic.Code, territoryDiagnostic.RecordPath);
                }
            }
            catch (Exception)
            {
                return Unavailable(
                    EconomyDiagnosticCodes.ProductionCatalog,
                    "Production.Territory");
            }

            var contributions = new List<EconomyProductionContribution>(
                _profile.Contributions.Count + territoryContributions.Count);
            for (int index = 0; index < _profile.Contributions.Count; index++)
            {
                KingdomProductionContributionRule rule = _profile.Contributions[index];
                if (!RuleAppliesToRealm(rule, save.SelectedRealm))
                {
                    continue;
                }

                if (!buildingLevels.TryGetValue(rule.BuildingId, out int level) ||
                    level < rule.MinBuildingLevel)
                {
                    continue;
                }

                double amount = rule.RatePerLevelPerSecond * level * elapsedSeconds;
                if (rule.CapPerTick > 0d)
                {
                    double cap = rule.CapPerTick * capPerTickScale;
                    if (amount > cap)
                    {
                        amount = cap;
                    }
                }

                if (double.IsNaN(amount) || double.IsInfinity(amount) || amount < 0d)
                {
                    return Unavailable(
                        EconomyDiagnosticCodes.ProductionInvalidContribution,
                        $"Production.Contributions[{rule.Id}]");
                }

                if (amount == 0d)
                {
                    continue;
                }

                contributions.Add(new EconomyProductionContribution(rule.ResourceType, amount));
            }

            for (int index = 0; index < territoryContributions.Count; index++)
            {
                EconomyProductionContribution territory = territoryContributions[index];
                if (double.IsNaN(territory.Amount) ||
                    double.IsInfinity(territory.Amount) ||
                    territory.Amount < 0d)
                {
                    return Unavailable(
                        EconomyDiagnosticCodes.ProductionInvalidContribution,
                        $"Production.Territory[{index}]");
                }

                if (territory.Amount == 0d)
                {
                    continue;
                }

                contributions.Add(territory);
            }

            return new EconomyProductionContributionSnapshot(
                EconomyProductionSourceStatus.Available,
                save.ProfileId,
                _profile.SourceSha256,
                contributions,
                Array.Empty<EconomyDiagnostic>());
        }

        internal static bool TryCaptureBuildingLevelSnapshot(
            IEnumerable<BuildingState> buildings,
            out IReadOnlyDictionary<string, int> buildingLevels,
            out EconomyDiagnostic diagnostic)
        {
            var captured = new Dictionary<string, int>(StringComparer.Ordinal);
            buildingLevels = captured;
            diagnostic = default;
            if (buildings == null)
            {
                diagnostic = new EconomyDiagnostic(
                    EconomyDiagnosticCodes.ProductionCatalog,
                    "Production.Buildings");
                return false;
            }

            int index = 0;
            foreach (BuildingState state in buildings)
            {
                string path = $"Production.Buildings[{index}]";
                if (state == null ||
                    !KingdomProductionProfileCatalog.TryCanonicalBuildingId(state.BuildingId, out string canonical) ||
                    state.Level < 0)
                {
                    diagnostic = new EconomyDiagnostic(
                        EconomyDiagnosticCodes.ProductionCatalog,
                        path);
                    return false;
                }

                if (captured.ContainsKey(canonical))
                {
                    diagnostic = new EconomyDiagnostic(
                        EconomyDiagnosticCodes.ProductionCatalog,
                        path);
                    return false;
                }

                captured[canonical] = state.Level;
                index++;
            }

            return true;
        }

        private static bool IsUncertainOrDegraded(ISaveGameService saveGameService)
        {
            try
            {
                if (saveGameService.LastSaveStatus == SaveOperationStatus.CommitUncertain ||
                    saveGameService.LastSaveStatus == SaveOperationStatus.SaveFailedPreviousPreserved)
                {
                    return true;
                }

                SaveLoadStatus loadStatus = saveGameService.LastLoadStatus;
                return loadStatus == SaveLoadStatus.LoadedPrimaryDegraded ||
                       loadStatus == SaveLoadStatus.LoadedForwardSchemaReadOnly ||
                       loadStatus == SaveLoadStatus.RecoveryRequired ||
                       loadStatus == SaveLoadStatus.RecoveryFailed;
            }
            catch (Exception)
            {
                return true;
            }
        }

        private static bool RuleAppliesToRealm(
            KingdomProductionContributionRule rule,
            RealmId realmId)
        {
            for (int index = 0; index < rule.RealmIds.Count; index++)
            {
                if (rule.RealmIds[index] == realmId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSafeProfileIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!char.IsLetterOrDigit(character) &&
                    character != '-' &&
                    character != '_' &&
                    character != '.')
                {
                    return false;
                }
            }

            return true;
        }

        private static EconomyProductionContributionSnapshot Unavailable(string code, string path)
        {
            return new EconomyProductionContributionSnapshot(
                EconomyProductionSourceStatus.Unavailable,
                string.Empty,
                string.Empty,
                Array.Empty<EconomyProductionContribution>(),
                new[] { new EconomyDiagnostic(code, path) });
        }
    }
}
