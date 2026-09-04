using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.RealmWar.Territories.Contracts;

namespace AL.RealmWar.Territories
{
    internal sealed class TerritoryCaptureTransactionService
    {
        public const string LocalProfileSessionId = "local-territory-session-v1";

        private readonly ISaveGameService _saveGameService;
        private readonly TerritoryPhaseBPlanner _planner;
        private readonly AL.Services.Local.EconomyWriteAuthorityGate
            _writeAuthorityGate;
        private readonly bool _allowWritesWithoutGate;

        public TerritoryCaptureTransactionService(ISaveGameService saveGameService)
            : this(
                saveGameService,
                TerritoryPhaseBPlanner.CreateCurrentBaseline(),
                AL.Services.Local.EconomyWriteAuthorityGate.FromSaveService(
                    saveGameService),
                false)
        {
        }

        private TerritoryCaptureTransactionService(
            ISaveGameService saveGameService,
            TerritoryPhaseBPlanner planner,
            AL.Services.Local.EconomyWriteAuthorityGate writeAuthorityGate,
            bool allowWritesWithoutGate)
        {
            _saveGameService = saveGameService ??
                throw new ArgumentNullException(nameof(saveGameService));
            _planner = planner ??
                throw new ArgumentNullException(nameof(planner));
            _writeAuthorityGate = writeAuthorityGate;
            _allowWritesWithoutGate = allowWritesWithoutGate;
        }

        internal static TerritoryCaptureTransactionService CreateForTests(
            ISaveGameService saveGameService)
        {
            return new TerritoryCaptureTransactionService(
                saveGameService,
                TerritoryPhaseBPlanner.CreateCurrentBaseline(),
                null,
                true);
        }

        public TerritoryPhaseBPlanner Planner => _planner;

        public TerritoryCaptureApplicationResult ApplyCapture(
            TerritoryCaptureTransactionRequest request)
        {
            string territoryId = request?.CaptureRequest?.TerritoryId ?? string.Empty;
            if (!_allowWritesWithoutGate &&
                _saveGameService is
                    AL.Services.Local.IProfileBoundTerritoryCaptureCandidateStore
                        profileBoundStore)
            {
                return profileBoundStore.TryCommitProfileBoundTerritoryCapture(
                    request,
                    _planner);
            }

            if (!TryGetAuthorizedSave(out SaveGameData save))
            {
                return Reject(
                    territoryId,
                    "ProfileReadOnly",
                    "Territory capture rejected before any profile mutation.");
            }

            if (save == null)
            {
                return Reject(
                    territoryId,
                    "NoCurrentSave",
                    "Territory capture requires a current save.");
            }

            if (request?.CaptureRequest == null)
            {
                return Reject(
                    territoryId,
                    "MissingCaptureRequest",
                    "Territory capture requires a typed authorization result.");
            }

            TerritoryCaptureAuthorization authorization =
                request.CaptureRequest.Authorization;
            if (authorization == null ||
                authorization.Source != TerritoryCaptureAuthorizationSource.CommandResult)
            {
                return Reject(
                    territoryId,
                    "AuthorizationSourceUnavailable",
                    "Production territory capture requires a typed command authorization result.");
            }

            IReadOnlyList<TerritoryStateRecord> states = ReadStates(save, _planner.Catalog);
            string profileSessionId = ResolveProfileSessionId(save);
            TerritoryQueryResult query = _planner.BuildQuery(
                states,
                request.CaptureRequest.CommittedProfileRealm,
                profileSessionId);
            IReadOnlyList<TerritoryCaptureReceipt> receipts = ReadReceipts(save);
            TerritoryCaptureTransactionPlan plan = _planner.PlanCaptureTransaction(
                query,
                request,
                receipts);
            var candidate = new SaveBackedTerritoryCandidate(
                save,
                _saveGameService,
                _planner.Catalog);
            var economy = new SaveBackedTerritoryEconomy(save);
            var quest = new SaveBackedTerritoryQuest(save);
            return _planner.ApplyCapture(plan, candidate, economy, quest);
        }

        internal static IReadOnlyList<TerritoryStateRecord> ReadStates(
            SaveGameData save,
            TerritoryPhaseBCatalog catalog)
        {
            var states = new List<TerritoryStateRecord>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            List<TerritoryData> territories = save?.Territories ?? new List<TerritoryData>();
            TerritoryCaptureLedgerData ledger = save?.TerritoryCaptureLedger;
            foreach (TerritoryData territory in territories)
            {
                if (territory == null || string.IsNullOrWhiteSpace(territory.Id))
                {
                    continue;
                }

                seen.Add(territory.Id);
                states.Add(new TerritoryStateRecord(
                    territory.Id,
                    territory.OwnerRealm,
                    ReadRevision(ledger, territory.Id)));
            }

            if (catalog?.Definitions == null)
            {
                return states;
            }

            foreach (TerritoryDefinition definition in catalog.Definitions)
            {
                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.Id) ||
                    seen.Contains(definition.Id))
                {
                    continue;
                }

                states.Add(new TerritoryStateRecord(
                    definition.Id,
                    definition.InitialOwner,
                    0));
            }

            return states;
        }

        internal static IReadOnlyList<TerritoryCaptureReceipt> ReadReceipts(SaveGameData save)
        {
            List<TerritoryCaptureReceiptRecord> rows =
                save?.TerritoryCaptureLedger?.Receipts;
            if (rows == null || rows.Count == 0)
            {
                return Array.Empty<TerritoryCaptureReceipt>();
            }

            var receipts = new List<TerritoryCaptureReceipt>(rows.Count);
            foreach (TerritoryCaptureReceiptRecord row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                receipts.Add(row.ToReceipt());
            }

            return receipts;
        }

        internal static long ReadRevision(TerritoryCaptureLedgerData ledger, string territoryId)
        {
            if (ledger?.Revisions == null)
            {
                return 0;
            }

            TerritoryOwnershipRevisionData row = ledger.Revisions.FirstOrDefault(item =>
                item != null &&
                string.Equals(item.TerritoryId, territoryId, StringComparison.Ordinal));
            return row?.Revision ?? 0;
        }

        private static string ResolveProfileSessionId(SaveGameData save)
        {
            string existing = save?.TerritoryCaptureLedger?.ProfileSessionId;
            return string.IsNullOrWhiteSpace(existing)
                ? LocalProfileSessionId
                : existing;
        }

        private bool TryGetAuthorizedSave(out SaveGameData save)
        {
            if (_allowWritesWithoutGate)
            {
                save = _saveGameService.CurrentSave;
                return save != null;
            }

            return _writeAuthorityGate.TryGetWritableSave(out save);
        }

        private static TerritoryCaptureApplicationResult Reject(
            string territoryId,
            string code,
            string message)
        {
            return new TerritoryCaptureApplicationResult(
                TerritoryApplyDisposition.Rejected,
                null,
                null,
                null,
                new[]
                {
                    new TerritoryDiagnostic(
                        TerritoryDiagnosticSeverity.Error,
                        code,
                        territoryId ?? string.Empty,
                        message)
                });
        }
    }
}
