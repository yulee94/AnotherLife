using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.RealmSelection;
using AL.RealmWar.Territories;
using AL.RealmWar.Territories.Contracts;

namespace AL.Services.Local
{
    internal static class TerritoryCaptureSaveAuthority
    {
        internal const string OperationId =
            "al.save.schema2.territory-capture.v1";

        internal static bool CanCommit(AL.Core.Interfaces.ISaveGameService save) =>
            save is IProfileBoundTerritoryCaptureCandidateStore &&
            save is IProfileWriteAuthorityProvider authority &&
            authority.GetCurrentAuthority().Status ==
                ProfileWriteAuthorityStatus.Writable &&
            save.CurrentSave != null;
    }

    public sealed partial class LocalSaveGameService
    {
        TerritoryCaptureApplicationResult
            IProfileBoundTerritoryCaptureCandidateStore
                .TryCommitProfileBoundTerritoryCapture(
                    TerritoryCaptureTransactionRequest request,
                    TerritoryPhaseBPlanner planner)
        {
            string territoryId = request?.CaptureRequest?.TerritoryId ?? string.Empty;
            if (planner == null || request?.CaptureRequest == null)
            {
                return RejectTerritoryCapture(
                    territoryId,
                    "MissingCaptureRequest",
                    "Territory capture requires a complete typed transaction request.");
            }

            ProfileWriteAuthoritySnapshot before = GetCurrentAuthority();
            if (before.Status != ProfileWriteAuthorityStatus.Writable ||
                _currentSave == null)
            {
                return RejectTerritoryCapture(
                    territoryId,
                    "ProfileReadOnly",
                    "Territory capture rejected before any profile mutation.");
            }

            if (!HasMatchingCommittedRealm(
                    _currentSave,
                    request.CaptureRequest.CommittedProfileRealm))
            {
                return RejectTerritoryCapture(
                    territoryId,
                    "CommittedRealmMismatch",
                    "The capture realm does not match the committed profile realm.");
            }

            TerritoryCaptureTransactionPlan initialPlan =
                PlanTerritoryCapture(_currentSave, request, planner);
            if (initialPlan.Status != TerritoryCaptureStatus.Planned)
            {
                return planner.ApplyCapture(initialPlan, null, null, null);
            }

            TerritoryCaptureApplicationResult candidateResult = null;
            ProfileBoundSaveCandidateCommitResult bound =
                ((IProfileBoundSaveGameCandidateStore)this).TryCommitCandidate(
                    ProfileAuthorityExpectation.From(before),
                    TerritoryCaptureSaveAuthority.OperationId,
                    initialPlan.ResultId,
                    candidate =>
                    {
                        TerritoryCaptureTransactionPlan currentPlan =
                            PlanTerritoryCapture(candidate, request, planner);
                        if (currentPlan.Status ==
                            TerritoryCaptureStatus.AlreadyCommittedReplay)
                        {
                            candidateResult = planner.ApplyCapture(
                                currentPlan,
                                null,
                                null,
                                null);
                            return SaveCandidateMutationPreparation.Duplicate();
                        }

                        if (currentPlan.Status != TerritoryCaptureStatus.Planned)
                        {
                            candidateResult = planner.ApplyCapture(
                                currentPlan,
                                null,
                                null,
                                null);
                            return SaveCandidateMutationPreparation.Rejected(
                                FirstDiagnosticCode(
                                    candidateResult,
                                    "CapturePlanRejected"));
                        }

                        var ownership = new SaveBackedTerritoryCandidate(
                            candidate,
                            planner.Catalog);
                        var economy = new SaveBackedTerritoryEconomy(candidate);
                        var quest = new SaveBackedTerritoryQuest(candidate);
                        candidateResult = planner.ApplyCapture(
                            currentPlan,
                            ownership,
                            economy,
                            quest);
                        return candidateResult.Disposition ==
                                TerritoryApplyDisposition.Committed
                            ? SaveCandidateMutationPreparation.Prepared()
                            : SaveCandidateMutationPreparation.Rejected(
                                FirstDiagnosticCode(
                                    candidateResult,
                                    "CaptureApplyRejected"));
                    });

            if (bound?.CommitResult == null)
            {
                return RejectTerritoryCapture(
                    territoryId,
                    "ProfileCommitUnavailable",
                    "The profile-bound territory commit returned no result.");
            }

            switch (bound.CommitResult.Outcome)
            {
                case SaveCandidateCommitOutcome.Committed:
                    return candidateResult ?? RejectTerritoryCapture(
                        territoryId,
                        "ProfileCommitResultMissing",
                        "The territory commit completed without an application result.");

                case SaveCandidateCommitOutcome.Duplicate:
                    return candidateResult?.Disposition ==
                            TerritoryApplyDisposition.Replayed
                        ? candidateResult
                        : ApplyPublishedReplay(
                            bound.CommitResult.PublishedSave,
                            request,
                            planner);

                case SaveCandidateCommitOutcome.PreviousPreserved:
                    return WithCommitDisposition(
                        candidateResult,
                        initialPlan,
                        TerritoryApplyDisposition.RolledBack,
                        null,
                        "CaptureRolledBack",
                        "Territory capture was rolled back; the previous save remains authoritative.");

                case SaveCandidateCommitOutcome.CommitUncertain:
                    return WithCommitDisposition(
                        candidateResult,
                        initialPlan,
                        TerritoryApplyDisposition.CommitUncertain,
                        ToUncertain(candidateResult?.Receipt),
                        "CaptureCommitUncertain",
                        "Territory capture durability is uncertain; reconcile from disk before retrying.");

                case SaveCandidateCommitOutcome.ReadOnly:
                    return RejectTerritoryCapture(
                        territoryId,
                        "ProfileReadOnly",
                        bound.CommitResult.Message);

                default:
                    return candidateResult ?? RejectTerritoryCapture(
                        territoryId,
                        "CaptureCommitRejected",
                        bound.CommitResult.Message);
            }
        }

        private static TerritoryCaptureTransactionPlan PlanTerritoryCapture(
            SaveGameData save,
            TerritoryCaptureTransactionRequest request,
            TerritoryPhaseBPlanner planner)
        {
            string profileSessionId = string.IsNullOrWhiteSpace(
                save?.TerritoryCaptureLedger?.ProfileSessionId)
                ? TerritoryCaptureTransactionService.LocalProfileSessionId
                : save.TerritoryCaptureLedger.ProfileSessionId;
            TerritoryQueryResult query = planner.BuildQuery(
                TerritoryCaptureTransactionService.ReadStates(save, planner.Catalog),
                request.CaptureRequest.CommittedProfileRealm,
                profileSessionId);
            return planner.PlanCaptureTransaction(
                query,
                request,
                TerritoryCaptureTransactionService.ReadReceipts(save));
        }

        private static TerritoryCaptureApplicationResult ApplyPublishedReplay(
            SaveGameData published,
            TerritoryCaptureTransactionRequest request,
            TerritoryPhaseBPlanner planner)
        {
            TerritoryCaptureTransactionPlan replayPlan =
                PlanTerritoryCapture(published, request, planner);
            return planner.ApplyCapture(replayPlan, null, null, null);
        }

        private static bool HasMatchingCommittedRealm(
            SaveGameData save,
            RealmId requestedRealm)
        {
            RealmSelectionAuthorityState receipt = save?.RealmSelection;
            return save != null &&
                   requestedRealm != RealmId.None &&
                   Enum.IsDefined(typeof(RealmId), requestedRealm) &&
                   save.SelectedRealm == requestedRealm &&
                   receipt != null &&
                   receipt.Committed &&
                   receipt.SelectedRealm == (int)requestedRealm &&
                   !string.IsNullOrWhiteSpace(receipt.TransactionId) &&
                   string.Equals(receipt.ProfileId, save.ProfileId, StringComparison.Ordinal) &&
                   string.Equals(
                       receipt.ReceiptFingerprint,
                       RealmSelectionAuthority.ComputeReceiptFingerprint(
                           receipt.ProfileId,
                           requestedRealm,
                           receipt.TransactionId,
                           receipt.CorrelationId,
                           receipt.OperationId,
                           receipt.EventId,
                           receipt.Provenance,
                           receipt.Revision),
                       StringComparison.Ordinal);
        }

        private static string FirstDiagnosticCode(
            TerritoryCaptureApplicationResult result,
            string fallback)
        {
            TerritoryDiagnostic diagnostic = result?.Diagnostics?.FirstOrDefault();
            return string.IsNullOrWhiteSpace(diagnostic?.Code)
                ? fallback
                : diagnostic.Code;
        }

        private static TerritoryCaptureApplicationResult WithCommitDisposition(
            TerritoryCaptureApplicationResult source,
            TerritoryCaptureTransactionPlan fallbackPlan,
            TerritoryApplyDisposition disposition,
            TerritoryCaptureReceipt receipt,
            string code,
            string message)
        {
            var diagnostics = new List<TerritoryDiagnostic>();
            if (source?.Diagnostics != null)
            {
                diagnostics.AddRange(source.Diagnostics);
            }

            diagnostics.Add(new TerritoryDiagnostic(
                TerritoryDiagnosticSeverity.Error,
                code,
                source?.Plan?.CapturePlan?.TerritoryId ??
                    fallbackPlan?.CapturePlan?.TerritoryId ?? string.Empty,
                message));
            return new TerritoryCaptureApplicationResult(
                disposition,
                source?.Plan ?? fallbackPlan,
                receipt,
                null,
                diagnostics);
        }

        private static TerritoryCaptureReceipt ToUncertain(
            TerritoryCaptureReceipt receipt)
        {
            if (receipt == null)
            {
                return null;
            }

            return new TerritoryCaptureReceipt(
                receipt.ReceiptId,
                receipt.OperationId,
                receipt.SemanticHash,
                TerritoryOperationDurability.CommitUncertain,
                receipt.ResultId,
                receipt.EventId,
                receipt.TerritoryId,
                receipt.PreviousOwner,
                receipt.NewOwner,
                receipt.PreviousRevision,
                receipt.NewRevision,
                receipt.WarzoneCreditsDelta,
                receipt.QuestProgressDelta,
                new TerritoryCatalogIdentity(
                    receipt.CatalogId,
                    receipt.CatalogSchemaVersion,
                    receipt.CatalogContentVersion,
                    receipt.CatalogSourceRevision,
                    receipt.CatalogRawSha256),
                receipt.StateRevisionHash,
                receipt.ProfileSessionId,
                receipt.AuthorizationId,
                receipt.AuthorizationSourceResultId,
                receipt.AuthorizationSourceResultHash);
        }

        private static TerritoryCaptureApplicationResult RejectTerritoryCapture(
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
                        message ?? string.Empty)
                });
        }
    }
}
