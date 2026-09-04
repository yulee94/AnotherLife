using System;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;

namespace AL.Services.Local
{
    public sealed class KingdomProductionContributionProvider : IEconomyProductionContributionProvider
    {
        private readonly ISaveGameService _saveGameService;
        private readonly KingdomProductionProfileSnapshot _profile;

        public KingdomProductionContributionProvider(
            ISaveGameService saveGameService,
            KingdomProductionProfileSnapshot profile)
        {
            _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
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

            if (!TryReadBuildingLevels(save, out Dictionary<string, int> buildingLevels, out EconomyDiagnostic buildingDiagnostic))
            {
                return Unavailable(buildingDiagnostic.Code, buildingDiagnostic.RecordPath);
            }

            var contributions = new List<EconomyProductionContribution>(_profile.Contributions.Count);
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

                double amount = rule.RatePerLevelPerSecond * level * deltaSeconds;
                if (rule.CapPerTick > 0d && amount > rule.CapPerTick)
                {
                    amount = rule.CapPerTick;
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

            return new EconomyProductionContributionSnapshot(
                EconomyProductionSourceStatus.Available,
                save.ProfileId,
                _profile.SourceSha256,
                contributions,
                Array.Empty<EconomyDiagnostic>());
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

        private static bool TryReadBuildingLevels(
            SaveGameData save,
            out Dictionary<string, int> buildingLevels,
            out EconomyDiagnostic diagnostic)
        {
            buildingLevels = new Dictionary<string, int>(StringComparer.Ordinal);
            diagnostic = default;
            IList<BuildingState> buildings = save.Buildings;
            if (buildings == null)
            {
                diagnostic = new EconomyDiagnostic(
                    EconomyDiagnosticCodes.ProductionCatalog,
                    "Production.Buildings");
                return false;
            }

            for (int index = 0; index < buildings.Count; index++)
            {
                BuildingState state = buildings[index];
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

                if (buildingLevels.ContainsKey(canonical))
                {
                    diagnostic = new EconomyDiagnostic(
                        EconomyDiagnosticCodes.ProductionCatalog,
                        path);
                    return false;
                }

                buildingLevels[canonical] = state.Level;
            }

            return true;
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
