using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.RealmSelection;
using AL.RealmWar.Territories.Contracts;

namespace AL.RealmWar.Territories
{
    public sealed class TerritoryCaptureAcceptedCommandResult
    {
        public TerritoryCaptureAcceptedCommandResult(
            string operationId,
            string territoryId,
            RealmId expectedPreviousOwner,
            long expectedPreviousRevision,
            TerritoryCaptureAuthorization authorization,
            long evaluatedAtUtcTicks)
        {
            OperationId = operationId ?? string.Empty;
            TerritoryId = territoryId ?? string.Empty;
            ExpectedPreviousOwner = expectedPreviousOwner;
            ExpectedPreviousRevision = expectedPreviousRevision;
            Authorization = authorization;
            EvaluatedAtUtcTicks = evaluatedAtUtcTicks;
        }

        public string OperationId { get; }
        public string TerritoryId { get; }
        public RealmId ExpectedPreviousOwner { get; }
        public long ExpectedPreviousRevision { get; }
        public TerritoryCaptureAuthorization Authorization { get; }
        public long EvaluatedAtUtcTicks { get; }
    }

    public static class TerritoryCaptureCaller
    {
        public static TerritoryCaptureApplicationResult ApplyAcceptedResult(
            ITerritoryService territoryService,
            ISaveGameService saveGameService,
            TerritoryCaptureAcceptedCommandResult acceptedResult)
        {
            if (territoryService == null || saveGameService == null)
            {
                return Reject(
                    acceptedResult?.TerritoryId,
                    "CaptureServiceUnavailable",
                    "Territory capture services are unavailable.");
            }

            if (acceptedResult == null ||
                acceptedResult.Authorization == null ||
                acceptedResult.Authorization.Source !=
                    TerritoryCaptureAuthorizationSource.CommandResult)
            {
                return Reject(
                    acceptedResult?.TerritoryId,
                    "AcceptedCommandResultUnavailable",
                    "Territory capture requires an externally supplied accepted command result.");
            }

            SaveGameData save = saveGameService.CurrentSave;
            if (!TryGetCommittedRealm(save, out RealmId committedRealm))
            {
                return Reject(
                    acceptedResult.TerritoryId,
                    "CommittedRealmUnavailable",
                    "Territory capture requires a committed profile realm.");
            }

            TerritoryPhaseBPlanner planner =
                TerritoryPhaseBPlanner.CreateCurrentBaseline();
            string profileSessionId = string.IsNullOrWhiteSpace(
                save.TerritoryCaptureLedger?.ProfileSessionId)
                ? TerritoryCaptureTransactionService.LocalProfileSessionId
                : save.TerritoryCaptureLedger.ProfileSessionId;
            TerritoryQueryResult query = planner.BuildQuery(
                TerritoryCaptureTransactionService.ReadStates(save, planner.Catalog),
                committedRealm,
                profileSessionId);
            var request = new TerritoryCaptureTransactionRequest(
                new TerritoryCaptureRequest(
                    acceptedResult.OperationId,
                    acceptedResult.TerritoryId,
                    committedRealm,
                    acceptedResult.Authorization.CapturerRealm,
                    acceptedResult.ExpectedPreviousOwner,
                    acceptedResult.ExpectedPreviousRevision,
                    acceptedResult.Authorization),
                planner.Catalog.Identity,
                query.StateRevisionHash,
                profileSessionId,
                acceptedResult.EvaluatedAtUtcTicks);
            return territoryService.ApplyCaptureTransaction(request) ?? Reject(
                acceptedResult.TerritoryId,
                "CaptureResultUnavailable",
                "Territory capture returned no application result.");
        }

        public static TerritoryCaptureApplicationResult RejectMissingAcceptedResult() =>
            Reject(
                string.Empty,
                "AcceptedCommandResultUnavailable",
                "Territory capture requires an externally supplied accepted command result.");

        private static bool TryGetCommittedRealm(
            SaveGameData save,
            out RealmId realm)
        {
            realm = RealmId.None;
            RealmSelectionAuthorityState receipt = save?.RealmSelection;
            if (save == null ||
                !RealmSelectionAuthority.IsDefinedPlayable(save.SelectedRealm) ||
                receipt == null ||
                !receipt.Committed ||
                receipt.SelectedRealm != (int)save.SelectedRealm ||
                !string.Equals(receipt.ProfileId, save.ProfileId, StringComparison.Ordinal) ||
                !string.Equals(
                    receipt.ReceiptFingerprint,
                    RealmSelectionAuthority.ComputeReceiptFingerprint(
                        receipt.ProfileId,
                        save.SelectedRealm,
                        receipt.TransactionId,
                        receipt.CorrelationId,
                        receipt.OperationId,
                        receipt.EventId,
                        receipt.Provenance,
                        receipt.Revision),
                    StringComparison.Ordinal))
            {
                return false;
            }

            realm = save.SelectedRealm;
            return true;
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

    public static class TerritoryCapturePresentation
    {
        public static string Describe(TerritoryCaptureApplicationResult result)
        {
            if (result == null)
            {
                return "Capture unavailable: no application result was returned.";
            }

            switch (result.Disposition)
            {
                case TerritoryApplyDisposition.Committed:
                    return "Territory secured. Ownership, rewards, and capture receipt were saved.";
                case TerritoryApplyDisposition.Replayed:
                    return "Capture already committed. No duplicate ownership or rewards were applied.";
                case TerritoryApplyDisposition.NoChange:
                    return "No territory change: the committed realm already owns this territory.";
                case TerritoryApplyDisposition.RolledBack:
                    return "Capture rolled back. The previous save remains authoritative and nothing was published.";
                case TerritoryApplyDisposition.CommitUncertain:
                    return "Capture save is uncertain. Reconcile from disk before retrying; no success was published.";
                default:
                    TerritoryDiagnostic diagnostic =
                        result.Diagnostics?.FirstOrDefault(item =>
                            item != null &&
                            !string.IsNullOrWhiteSpace(item.Message));
                    return diagnostic != null
                        ? "Capture rejected: " + diagnostic.Message
                        : "Capture rejected. No ownership or rewards were applied.";
            }
        }
    }
}
