using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AL.Core;
using AL.Core.Interfaces;
using AL.RealmWar.Territories;
using AL.RealmWar.Territories.Contracts;
using UnityEngine;
using System;

[assembly: InternalsVisibleTo("AL.EditMode.Tests")]

namespace AL.RealmWar.Warzone
{
    public class WarzoneService : ITerritoryService
    {
        private readonly ISaveGameService _saveGameService;
        private readonly AL.Services.Local.EconomyWriteAuthorityGate
            _writeAuthorityGate;
        private readonly TerritoryCaptureTransactionService _captureTransactions;
        private readonly bool _allowWritesWithoutGate;

        public event Action<string, RealmId> OnTerritoryCaptured;

        public WarzoneService(ISaveGameService saveGameService)
            : this(
                saveGameService,
                AL.Services.Local.EconomyWriteAuthorityGate.FromSaveService(
                    saveGameService),
                false)
        {
        }

        private WarzoneService(
            ISaveGameService saveGameService,
            AL.Services.Local.EconomyWriteAuthorityGate writeAuthorityGate,
            bool allowWritesWithoutGate)
        {
            _saveGameService = saveGameService ??
                throw new ArgumentNullException(nameof(saveGameService));
            _writeAuthorityGate = writeAuthorityGate;
            _allowWritesWithoutGate = allowWritesWithoutGate;
            _captureTransactions = allowWritesWithoutGate
                ? TerritoryCaptureTransactionService.CreateForTests(saveGameService)
                : new TerritoryCaptureTransactionService(saveGameService);
        }

        internal static WarzoneService CreateForTests(ISaveGameService saveGameService)
        {
            return new WarzoneService(saveGameService, null, true);
        }

        private List<TerritoryData> Territories =>
            _saveGameService.CurrentSave?.Territories;

        public IEnumerable<TerritoryData> GetTerritories()
        {
            List<TerritoryData> territories = Territories;
            return territories == null
                ? Array.Empty<TerritoryData>()
                : territories
                    .Where(territory => territory != null)
                    .Select(CloneTerritory)
                    .ToArray();
        }

        public void CaptureTerritory(string territoryId, RealmId capturer)
        {
            Debug.LogWarning(
                "[AL-WARZONE-CAPTURE-AUTHORIZATION-REQUIRED] Legacy capture is unavailable until a typed producer supplies an authorization result.");
        }

        public TerritoryCaptureApplicationResult ApplyCaptureTransaction(
            TerritoryCaptureTransactionRequest request)
        {
            string territoryId = request?.CaptureRequest?.TerritoryId ?? string.Empty;
            if (!TryGetAuthorizedSave(out _))
            {
                Debug.LogWarning(
                    "[AL-WARZONE-PROFILE-READ-ONLY] Territory capture rejected before any profile mutation.");
                return new TerritoryCaptureApplicationResult(
                    TerritoryApplyDisposition.Rejected,
                    null,
                    null,
                    null,
                    new[]
                    {
                        new TerritoryDiagnostic(
                            TerritoryDiagnosticSeverity.Error,
                            "ProfileReadOnly",
                            territoryId ?? string.Empty,
                            "Territory capture rejected before any profile mutation.")
                    });
            }

            TerritoryCaptureApplicationResult result =
                _captureTransactions.ApplyCapture(request);
            if (result != null &&
                result.Disposition == TerritoryApplyDisposition.Committed &&
                result.Event != null)
            {
                OnTerritoryCaptured?.Invoke(result.Event.TerritoryId, result.Event.NewOwner);
                Debug.Log($"Territory {result.Event.TerritoryId} captured by {result.Event.NewOwner}");
            }

            return result;
        }

        public long CalculatePassiveIncome(ResourceType type)
        {
            var selectedRealm = _saveGameService.CurrentSave?.SelectedRealm ?? RealmId.None;
            List<TerritoryData> territories = Territories;
            if (territories == null)
            {
                return 0;
            }

            long total = 0;
            for (int index = 0; index < territories.Count; index++)
            {
                TerritoryData territory = territories[index];
                if (territory != null &&
                    territory.OwnerRealm == selectedRealm &&
                    territory.BonusType == type)
                {
                    total = checked(total + territory.BonusAmount);
                }
            }

            return total;
        }

        private bool TryGetAuthorizedSave(out AL.Data.Runtime.SaveGameData save)
        {
            if (_allowWritesWithoutGate)
            {
                save = _saveGameService.CurrentSave;
                return save != null;
            }

            return _writeAuthorityGate.TryGetWritableSave(out save);
        }

        private static TerritoryData CloneTerritory(TerritoryData territory) =>
            new TerritoryData
            {
                Id = territory.Id,
                Name = territory.Name,
                OwnerRealm = territory.OwnerRealm,
                BonusType = territory.BonusType,
                BonusAmount = territory.BonusAmount,
                IsFortress = territory.IsFortress
            };
    }
}
