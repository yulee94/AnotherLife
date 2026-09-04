using System;
using System.IO;
using AL.Narrative.Nvs01;
using AL.Narrative.Nvs01.Contracts;
using UnityEngine;

namespace AL.Narrative.MainQuestLine
{
    public sealed class MainQuestLineExecutionResult
    {
        internal MainQuestLineExecutionResult(
            bool succeeded,
            MainQuestLineDiagnostic diagnostic,
            MainQuestLineCatalog catalog,
            string progressedStateId,
            string resumedStateId)
        {
            Succeeded = succeeded;
            Diagnostic = diagnostic;
            Catalog = catalog;
            ProgressedStateId = progressedStateId ?? string.Empty;
            ResumedStateId = resumedStateId ?? string.Empty;
        }

        public bool Succeeded { get; }
        public MainQuestLineDiagnostic Diagnostic { get; }
        public MainQuestLineCatalog Catalog { get; }
        public string ProgressedStateId { get; }
        public string ResumedStateId { get; }
    }

    public static class MainQuestLineRuntime
    {
        public static MainQuestLineExecutionResult ExecuteRepresentativePath()
        {
            MainQuestLineCatalog catalog;
            MainQuestLineDiagnostic diagnostic;
            if (!MainQuestLineCatalogLoader.TryLoadCanonical(out catalog, out diagnostic))
            {
                return Fail(diagnostic, catalog);
            }

            return ExecuteRepresentativePath(catalog);
        }

        public static MainQuestLineExecutionResult ExecuteRepresentativePath(MainQuestLineCatalog catalog)
        {
            if (catalog == null)
            {
                return Fail(
                    new MainQuestLineDiagnostic(
                        MainQuestLineContract.DiagnosticPrefix + "CATALOG-MISSING",
                        "Representative path requires a verified runtime catalog.",
                        MainQuestLineContract.CatalogId,
                        "null"),
                    null);
            }

            string nvsPath = MainQuestLineCatalogLoader.ResolveNvs01CatalogPath();
            if (!File.Exists(nvsPath))
            {
                return Fail(
                    new MainQuestLineDiagnostic(
                        MainQuestLineContract.DiagnosticPrefix + "DEPENDENCY-MISSING",
                        "OMEN_1 packaged catalog is missing.",
                        Nvs01CatalogContract.StreamingAssetsRelativePath,
                        nvsPath),
                    catalog);
            }

            Nvs01CatalogValidationResult validation;
            try
            {
                validation = Nvs01CatalogValidator.ValidateCanonicalArtifact(File.ReadAllBytes(nvsPath));
            }
            catch (Exception exception)
            {
                return Fail(
                    new MainQuestLineDiagnostic(
                        MainQuestLineContract.DiagnosticPrefix + "DEPENDENCY-MISSING",
                        "OMEN_1 catalog validation threw.",
                        "accepted catalog",
                        exception.GetType().Name),
                    catalog);
            }

            if (validation == null || !validation.IsAccepted || validation.VerifiedCatalog == null)
            {
                return Fail(
                    new MainQuestLineDiagnostic(
                        MainQuestLineContract.DiagnosticPrefix + "DEPENDENCY-MISSING",
                        "OMEN_1 catalog was rejected.",
                        Nvs01CatalogContract.CanonicalSha256,
                        "rejected"),
                    catalog);
            }

            var operationIds = new[]
            {
                "00000000-0000-4000-8000-000000000001",
                "00000000-0000-4000-8000-000000000002"
            };
            int operationIndex = 0;
            var runtime = new Nvs01QuestRuntime(
                validation.VerifiedCatalog,
                null,
                new Nvs01InMemoryMutationCommitter(),
                () => Guid.NewGuid().ToString("D"));

            var offerCommand = new Nvs01CommandEnvelope(
                Nvs01RuntimeContract.ContractVersion,
                operationIds[operationIndex++],
                runtime.Snapshot.QuestId,
                runtime.Snapshot.StateId,
                runtime.Snapshot.Revision,
                runtime.Catalog.Speaker.Id,
                runtime.Catalog.Placement.ContextId,
                0);
            Nvs01CommandDisposition offer = runtime.SelectValerius(
                offerCommand,
                Nvs01InteractionKind.Offer,
                new Nvs01RealmContext(Nvs01RealmContextStatus.CommittedValid, "crownlands"));
            if (offer == null || !offer.IsCommitted)
            {
                return Fail(
                    new MainQuestLineDiagnostic(
                        MainQuestLineContract.DiagnosticPrefix + "PROGRESS-FAILED",
                        "OMEN_1 offer could not be selected.",
                        "OFFERED",
                        offer?.Snapshot.StateId ?? "null"),
                    catalog);
            }

            var acceptCommand = new Nvs01CommandEnvelope(
                Nvs01RuntimeContract.ContractVersion,
                operationIds[operationIndex],
                runtime.Snapshot.QuestId,
                runtime.Snapshot.StateId,
                runtime.Snapshot.Revision,
                "PLAYER",
                runtime.Snapshot.CurrentDialogueNodeId,
                0);
            Nvs01CommandDisposition accepted = runtime.SelectDialogueChoice(
                acceptCommand,
                catalog.AcceptChoiceKey);
            if (accepted == null ||
                !accepted.IsCommitted ||
                !string.Equals(
                    accepted.Snapshot.StateId,
                    MainQuestLineContract.ProgressedStateId,
                    StringComparison.Ordinal))
            {
                return Fail(
                    new MainQuestLineDiagnostic(
                        MainQuestLineContract.DiagnosticPrefix + "PROGRESS-FAILED",
                        "Representative QUEST_ACCEPTED branch did not reach the expected state.",
                        MainQuestLineContract.ProgressedStateId,
                        accepted?.Snapshot.StateId ?? "null"),
                    catalog);
            }

            var encoded = Nvs01ProgressCodec.Encode(accepted.Snapshot);
            Nvs01QuestSnapshot resumed;
            Nvs01RuntimeDiagnostic resumeDiagnostic;
            if (!Nvs01ProgressCodec.TryDecode(
                    encoded,
                    validation.VerifiedCatalog,
                    out resumed,
                    out resumeDiagnostic) ||
                resumed == null ||
                !string.Equals(
                    resumed.StateId,
                    accepted.Snapshot.StateId,
                    StringComparison.Ordinal))
            {
                return Fail(
                    new MainQuestLineDiagnostic(
                        MainQuestLineContract.DiagnosticPrefix + "RESUME-FAILED",
                        "Representative progress did not round-trip through save encoding.",
                        accepted.Snapshot.StateId,
                        resumeDiagnostic != null ? resumeDiagnostic.Code : resumed?.StateId ?? "null"),
                    catalog);
            }

            Debug.Log(
                MainQuestLineContract.ProgressMarker +
                " quest=" + accepted.Snapshot.QuestId +
                " state=" + accepted.Snapshot.StateId);
            Debug.Log(
                MainQuestLineContract.ResumedMarker +
                " quest=" + resumed.QuestId +
                " state=" + resumed.StateId);
            return new MainQuestLineExecutionResult(
                true,
                null,
                catalog,
                accepted.Snapshot.StateId,
                resumed.StateId);
        }

        private static MainQuestLineExecutionResult Fail(
            MainQuestLineDiagnostic diagnostic,
            MainQuestLineCatalog catalog)
        {
            string marker = diagnostic != null &&
                            diagnostic.Code.IndexOf("CATALOG-MISSING", StringComparison.Ordinal) >= 0
                ? MainQuestLineContract.MissingMarker
                : MainQuestLineContract.FailedMarker;
            Debug.LogError(marker + " " + (diagnostic != null ? diagnostic.ToString() : "unknown"));
            return new MainQuestLineExecutionResult(false, diagnostic, catalog, string.Empty, string.Empty);
        }
    }
}
