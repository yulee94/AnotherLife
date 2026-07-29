using System;
using System.Collections.Generic;
using System.Linq;
using AL.Data.Runtime;
using AL.Narrative.Nvs01.Contracts;
using AL.Services.Local;

namespace AL.Narrative.Nvs01
{
    internal static class Nvs01ProgressCodec
    {
        private const string PersistenceUnavailable = "SAVE-PROGRESS-UNAVAILABLE";

        internal static Nvs01ProgressData Encode(Nvs01QuestSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var data = new Nvs01ProgressData
            {
                Version = Nvs01ProgressData.CurrentVersion,
                PacketVersion = snapshot.PacketVersion,
                PacketSha256 = snapshot.PacketSha256,
                QuestId = snapshot.QuestId,
                Revision = snapshot.Revision,
                StateId = snapshot.StateId,
                CurrentDialogueNodeId = snapshot.CurrentDialogueNodeId,
                PendingChoice = snapshot.PendingChoice,
                PendingSemanticActionId = snapshot.PendingSemanticActionId,
                CommittedRealmId = snapshot.CommittedRealmId,
                EncounterStatus = (int)snapshot.EncounterStatus,
                HasCurrentEncounter = snapshot.CurrentEncounter != null,
                CurrentEncounter = Encode(snapshot.CurrentEncounter),
                LastEncounterCorrelationId = snapshot.LastEncounterCorrelationId,
                HasLastEncounterOutcome = snapshot.LastEncounterOutcome.HasValue,
                LastEncounterOutcome = snapshot.LastEncounterOutcome.HasValue
                    ? (int)snapshot.LastEncounterOutcome.Value
                    : 0,
                LastEncounterEventId = snapshot.LastEncounterEventId,
                LastEncounterSnapshotVersion = snapshot.LastEncounterSnapshotVersion,
                LastEncounterSnapshotReference = snapshot.LastEncounterSnapshotReference,
                HasLastOperation = snapshot.LastOperation != null,
                LastOperation = Encode(snapshot.LastOperation),
                ConsequenceIntentIds = snapshot.ConsequenceIntentIds.ToList(),
                AcquiredArtifactIds = new List<string>(),
                AppliedEffectKeys = new List<string>(),
                UnlockedChapterId = string.Empty
            };

            data.Objectives = snapshot.Objectives
                .Select(objective => new Nvs01ObjectiveProgressData
                {
                    ObjectiveId = objective.ObjectiveId,
                    Status = (int)objective.Status
                })
                .ToList();
            return data;
        }

        internal static bool TryDecode(
            Nvs01ProgressData data,
            Nvs01VerifiedCatalog verifiedCatalog,
            out Nvs01QuestSnapshot snapshot,
            out Nvs01RuntimeDiagnostic diagnostic)
        {
            snapshot = null;
            diagnostic = null;
            if (verifiedCatalog == null)
            {
                diagnostic = Diagnostic(PersistenceUnavailable, "verified catalog", "missing");
                return false;
            }

            if (data != null && data.Version == 0)
            {
                if (!IsNeutral(data))
                {
                    diagnostic = Diagnostic(PersistenceUnavailable, "neutral progress", "malformed");
                    return false;
                }

                snapshot = Nvs01QuestRuntime.CreateInitialSnapshot(verifiedCatalog);
                return true;
            }

            if (!TryBuildSnapshot(data, out snapshot, out string error))
            {
                diagnostic = Diagnostic(PersistenceUnavailable, "valid persisted progress", error);
                return false;
            }

            try
            {
                var validator = new Nvs01QuestRuntime(
                    verifiedCatalog,
                    snapshot,
                    new Nvs01InMemoryMutationCommitter(),
                    () => Guid.NewGuid().ToString("D"));
                snapshot = validator.Snapshot;
                return true;
            }
            catch (Exception exception)
            {
                snapshot = null;
                diagnostic = Diagnostic(
                    PersistenceUnavailable,
                    "catalog-consistent persisted progress",
                    exception.GetType().Name);
                return false;
            }
        }

        internal static bool TryValidateStoredData(
            Nvs01ProgressData data,
            out string error)
        {
            if (data == null)
            {
                error = "NVS-01 progress is null.";
                return false;
            }

            if (data.Version == 0)
            {
                if (!IsNeutral(data))
                {
                    error = "NVS-01 neutral progress contains authored state.";
                    return false;
                }

                error = string.Empty;
                return true;
            }

            return TryBuildSnapshot(data, out _, out error);
        }

        internal static bool Equivalent(
            Nvs01QuestSnapshot left,
            Nvs01QuestSnapshot right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null) return false;
            if (!string.Equals(left.PacketVersion, right.PacketVersion, StringComparison.Ordinal) ||
                !string.Equals(left.PacketSha256, right.PacketSha256, StringComparison.Ordinal) ||
                !string.Equals(left.QuestId, right.QuestId, StringComparison.Ordinal) ||
                left.Revision != right.Revision ||
                !string.Equals(left.StateId, right.StateId, StringComparison.Ordinal) ||
                !string.Equals(left.CurrentDialogueNodeId, right.CurrentDialogueNodeId, StringComparison.Ordinal) ||
                left.PendingChoice != right.PendingChoice ||
                !string.Equals(left.PendingSemanticActionId, right.PendingSemanticActionId, StringComparison.Ordinal) ||
                !string.Equals(left.CommittedRealmId, right.CommittedRealmId, StringComparison.Ordinal) ||
                left.EncounterStatus != right.EncounterStatus ||
                !Equivalent(left.CurrentEncounter, right.CurrentEncounter) ||
                !string.Equals(left.LastEncounterCorrelationId, right.LastEncounterCorrelationId, StringComparison.Ordinal) ||
                left.LastEncounterOutcome != right.LastEncounterOutcome ||
                !string.Equals(left.LastEncounterEventId, right.LastEncounterEventId, StringComparison.Ordinal) ||
                !string.Equals(left.LastEncounterSnapshotVersion, right.LastEncounterSnapshotVersion, StringComparison.Ordinal) ||
                !string.Equals(left.LastEncounterSnapshotReference, right.LastEncounterSnapshotReference, StringComparison.Ordinal) ||
                !Equivalent(left.LastOperation, right.LastOperation) ||
                left.Objectives.Count != right.Objectives.Count ||
                left.ConsequenceIntentIds.Count != right.ConsequenceIntentIds.Count)
            {
                return false;
            }

            for (var index = 0; index < left.Objectives.Count; index++)
            {
                if (!string.Equals(
                        left.Objectives[index].ObjectiveId,
                        right.Objectives[index].ObjectiveId,
                        StringComparison.Ordinal) ||
                    left.Objectives[index].Status != right.Objectives[index].Status)
                {
                    return false;
                }
            }

            for (var index = 0; index < left.ConsequenceIntentIds.Count; index++)
            {
                if (!string.Equals(
                        left.ConsequenceIntentIds[index],
                        right.ConsequenceIntentIds[index],
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryBuildSnapshot(
            Nvs01ProgressData data,
            out Nvs01QuestSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            if (data == null)
            {
                error = "missing";
                return false;
            }

            if (data.Version != Nvs01ProgressData.CurrentVersion)
            {
                error = data.Version > Nvs01ProgressData.CurrentVersion
                    ? "forward version"
                    : "unsupported version";
                return false;
            }

            if (!string.Equals(data.PacketVersion, Nvs01RuntimeContract.PacketVersion, StringComparison.Ordinal) ||
                !string.Equals(data.PacketSha256, Nvs01RuntimeContract.PacketSha256, StringComparison.Ordinal) ||
                !string.Equals(data.QuestId, Nvs01RuntimeContract.QuestId, StringComparison.Ordinal))
            {
                error = "packet identity mismatch";
                return false;
            }

            if (data.Objectives == null ||
                data.Objectives.Count > Nvs01RuntimeContract.MaximumObjectiveCount ||
                data.ConsequenceIntentIds == null ||
                data.ConsequenceIntentIds.Count > Nvs01RuntimeContract.MaximumConsequenceIntentCount ||
                data.AcquiredArtifactIds == null ||
                data.AppliedEffectKeys == null)
            {
                error = "collection bound or nullability failure";
                return false;
            }

            if (data.AcquiredArtifactIds.Count != 0 ||
                data.AppliedEffectKeys.Count != 0 ||
                !string.IsNullOrEmpty(data.UnlockedChapterId))
            {
                error = "unsupported consequence state";
                return false;
            }

            try
            {
                var objectives = data.Objectives
                    .Select(item =>
                    {
                        if (item == null) throw new ArgumentException("null objective");
                        return new Nvs01ObjectiveSnapshot(
                            item.ObjectiveId,
                            (Nvs01ObjectiveStatus)item.Status);
                    })
                    .ToArray();
                NvsEncounterRequest encounter = data.HasCurrentEncounter
                    ? Decode(data.CurrentEncounter)
                    : null;
                if (!data.HasCurrentEncounter && !IsNeutral(data.CurrentEncounter))
                {
                    throw new ArgumentException("inactive encounter payload is not neutral");
                }

                NvsEncounterOutcome? outcome = data.HasLastEncounterOutcome
                    ? (NvsEncounterOutcome?)data.LastEncounterOutcome
                    : null;
                if (!data.HasLastEncounterOutcome && data.LastEncounterOutcome != 0)
                {
                    throw new ArgumentException("inactive encounter outcome is not neutral");
                }

                Nvs01OperationReceipt operation = data.HasLastOperation
                    ? Decode(data.LastOperation)
                    : null;
                if (!data.HasLastOperation && !IsNeutral(data.LastOperation))
                {
                    throw new ArgumentException("inactive operation payload is not neutral");
                }

                snapshot = new Nvs01QuestSnapshot(
                    data.PacketVersion,
                    data.PacketSha256,
                    data.QuestId,
                    data.Revision,
                    data.StateId,
                    objectives,
                    data.CurrentDialogueNodeId,
                    data.PendingChoice,
                    data.PendingSemanticActionId,
                    data.CommittedRealmId,
                    (Nvs01EncounterStatus)data.EncounterStatus,
                    encounter,
                    data.LastEncounterCorrelationId,
                    outcome,
                    data.LastEncounterEventId,
                    data.LastEncounterSnapshotVersion,
                    data.LastEncounterSnapshotReference,
                    operation,
                    data.ConsequenceIntentIds);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                snapshot = null;
                error = exception.GetType().Name;
                return false;
            }
        }

        private static Nvs01EncounterRequestData Encode(NvsEncounterRequest request)
        {
            if (request == null) return new Nvs01EncounterRequestData();
            return new Nvs01EncounterRequestData
            {
                ContractVersion = request.ContractVersion,
                RequestId = request.RequestId,
                CorrelationId = request.CorrelationId,
                QuestId = request.QuestId,
                StateId = request.StateId,
                ObjectiveId = request.ObjectiveId,
                HookId = request.HookId,
                LocationId = request.LocationId,
                RealmId = request.RealmId,
                SuccessEventId = request.SuccessEventId,
                FailureEventId = request.FailureEventId,
                CancelledEventId = request.CancelledEventId,
                UnavailableEventId = request.UnavailableEventId,
                ReturnScene = request.ReturnScene
            };
        }

        private static NvsEncounterRequest Decode(Nvs01EncounterRequestData request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return new NvsEncounterRequest(
                request.ContractVersion,
                request.RequestId,
                request.CorrelationId,
                request.QuestId,
                request.StateId,
                request.ObjectiveId,
                request.HookId,
                request.LocationId,
                request.RealmId,
                request.SuccessEventId,
                request.FailureEventId,
                request.CancelledEventId,
                request.UnavailableEventId,
                request.ReturnScene);
        }

        private static Nvs01OperationReceiptData Encode(Nvs01OperationReceipt receipt)
        {
            if (receipt == null) return new Nvs01OperationReceiptData();
            return new Nvs01OperationReceiptData
            {
                OperationId = receipt.OperationId,
                PayloadFingerprint = receipt.PayloadFingerprint,
                Status = (int)receipt.Status,
                Revision = receipt.Revision,
                StateId = receipt.StateId,
                EventId = receipt.EventId,
                CorrelationId = receipt.CorrelationId
            };
        }

        private static Nvs01OperationReceipt Decode(Nvs01OperationReceiptData receipt)
        {
            if (receipt == null) throw new ArgumentNullException(nameof(receipt));
            return new Nvs01OperationReceipt(
                receipt.OperationId,
                receipt.PayloadFingerprint,
                (Nvs01CommandStatus)receipt.Status,
                receipt.Revision,
                receipt.StateId,
                receipt.EventId,
                receipt.CorrelationId);
        }

        private static bool Equivalent(
            NvsEncounterRequest left,
            NvsEncounterRequest right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null) return false;
            return left.ContractVersion == right.ContractVersion &&
                   string.Equals(left.RequestId, right.RequestId, StringComparison.Ordinal) &&
                   string.Equals(left.CorrelationId, right.CorrelationId, StringComparison.Ordinal) &&
                   string.Equals(left.QuestId, right.QuestId, StringComparison.Ordinal) &&
                   string.Equals(left.StateId, right.StateId, StringComparison.Ordinal) &&
                   string.Equals(left.ObjectiveId, right.ObjectiveId, StringComparison.Ordinal) &&
                   string.Equals(left.HookId, right.HookId, StringComparison.Ordinal) &&
                   string.Equals(left.LocationId, right.LocationId, StringComparison.Ordinal) &&
                   string.Equals(left.RealmId, right.RealmId, StringComparison.Ordinal) &&
                   string.Equals(left.SuccessEventId, right.SuccessEventId, StringComparison.Ordinal) &&
                   string.Equals(left.FailureEventId, right.FailureEventId, StringComparison.Ordinal) &&
                   string.Equals(left.CancelledEventId, right.CancelledEventId, StringComparison.Ordinal) &&
                   string.Equals(left.UnavailableEventId, right.UnavailableEventId, StringComparison.Ordinal) &&
                   string.Equals(left.ReturnScene, right.ReturnScene, StringComparison.Ordinal);
        }

        private static bool Equivalent(
            Nvs01OperationReceipt left,
            Nvs01OperationReceipt right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null) return false;
            return string.Equals(left.OperationId, right.OperationId, StringComparison.Ordinal) &&
                   string.Equals(left.PayloadFingerprint, right.PayloadFingerprint, StringComparison.Ordinal) &&
                   left.Status == right.Status &&
                   left.Revision == right.Revision &&
                   string.Equals(left.StateId, right.StateId, StringComparison.Ordinal) &&
                   string.Equals(left.EventId, right.EventId, StringComparison.Ordinal) &&
                   string.Equals(left.CorrelationId, right.CorrelationId, StringComparison.Ordinal);
        }

        private static bool IsNeutral(Nvs01ProgressData data) =>
            data != null &&
            data.Version == 0 &&
            string.IsNullOrEmpty(data.PacketVersion) &&
            string.IsNullOrEmpty(data.PacketSha256) &&
            string.IsNullOrEmpty(data.QuestId) &&
            data.Revision == 0 &&
            string.IsNullOrEmpty(data.StateId) &&
            data.Objectives != null &&
            data.Objectives.Count == 0 &&
            string.IsNullOrEmpty(data.CurrentDialogueNodeId) &&
            !data.PendingChoice &&
            string.IsNullOrEmpty(data.PendingSemanticActionId) &&
            string.IsNullOrEmpty(data.CommittedRealmId) &&
            data.EncounterStatus == 0 &&
            !data.HasCurrentEncounter &&
            IsNeutral(data.CurrentEncounter) &&
            string.IsNullOrEmpty(data.LastEncounterCorrelationId) &&
            !data.HasLastEncounterOutcome &&
            data.LastEncounterOutcome == 0 &&
            string.IsNullOrEmpty(data.LastEncounterEventId) &&
            string.IsNullOrEmpty(data.LastEncounterSnapshotVersion) &&
            string.IsNullOrEmpty(data.LastEncounterSnapshotReference) &&
            !data.HasLastOperation &&
            IsNeutral(data.LastOperation) &&
            data.ConsequenceIntentIds != null &&
            data.ConsequenceIntentIds.Count == 0 &&
            data.AcquiredArtifactIds != null &&
            data.AcquiredArtifactIds.Count == 0 &&
            data.AppliedEffectKeys != null &&
            data.AppliedEffectKeys.Count == 0 &&
            string.IsNullOrEmpty(data.UnlockedChapterId);

        private static bool IsNeutral(Nvs01EncounterRequestData data) =>
            data != null &&
            data.ContractVersion == 0 &&
            string.IsNullOrEmpty(data.RequestId) &&
            string.IsNullOrEmpty(data.CorrelationId) &&
            string.IsNullOrEmpty(data.QuestId) &&
            string.IsNullOrEmpty(data.StateId) &&
            string.IsNullOrEmpty(data.ObjectiveId) &&
            string.IsNullOrEmpty(data.HookId) &&
            string.IsNullOrEmpty(data.LocationId) &&
            string.IsNullOrEmpty(data.RealmId) &&
            string.IsNullOrEmpty(data.SuccessEventId) &&
            string.IsNullOrEmpty(data.FailureEventId) &&
            string.IsNullOrEmpty(data.CancelledEventId) &&
            string.IsNullOrEmpty(data.UnavailableEventId) &&
            string.IsNullOrEmpty(data.ReturnScene);

        private static bool IsNeutral(Nvs01OperationReceiptData data) =>
            data != null &&
            string.IsNullOrEmpty(data.OperationId) &&
            string.IsNullOrEmpty(data.PayloadFingerprint) &&
            data.Status == 0 &&
            data.Revision == 0 &&
            string.IsNullOrEmpty(data.StateId) &&
            string.IsNullOrEmpty(data.EventId) &&
            string.IsNullOrEmpty(data.CorrelationId);

        private static Nvs01RuntimeDiagnostic Diagnostic(
            string code,
            string expected,
            string actual) =>
            new Nvs01RuntimeDiagnostic(
                code,
                "NVS-01 persisted progress",
                expected,
                actual,
                string.Empty,
                string.Empty,
                string.Empty);
    }

    internal sealed class Nvs01SaveGameMutationCommitter : INvs01MutationCommitter
    {
        private readonly ISaveGameCandidateStore _store;
        private readonly Nvs01VerifiedCatalog _verifiedCatalog;

        internal Nvs01SaveGameMutationCommitter(
            ISaveGameCandidateStore store,
            Nvs01VerifiedCatalog verifiedCatalog)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _verifiedCatalog = verifiedCatalog ?? throw new ArgumentNullException(nameof(verifiedCatalog));
        }

        public bool TryCommit(
            Nvs01MutationPlan plan,
            out Nvs01QuestSnapshot committed,
            out Nvs01RuntimeDiagnostic diagnostic)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            SaveCandidateCommitResult result = _store.TryCommitCandidate(candidateSave =>
            {
                if (candidateSave == null)
                {
                    return SaveCandidateMutationPreparation.Rejected(
                        Nvs01CatalogContract.DiagnosticCodePrefix +
                        "SAVE-PROGRESS-UNAVAILABLE");
                }

                if (!Nvs01ProgressCodec.TryDecode(
                        candidateSave.Nvs01Progress,
                        _verifiedCatalog,
                        out Nvs01QuestSnapshot durable,
                        out Nvs01RuntimeDiagnostic decodeDiagnostic))
                {
                    return SaveCandidateMutationPreparation.Rejected(
                        decodeDiagnostic?.Code ??
                        Nvs01CatalogContract.DiagnosticCodePrefix + "SAVE-PROGRESS-UNAVAILABLE");
                }

                if (Nvs01ProgressCodec.Equivalent(durable, plan.Candidate))
                {
                    return SaveCandidateMutationPreparation.Duplicate();
                }

                if (!Nvs01ProgressCodec.Equivalent(durable, plan.Expected))
                {
                    return SaveCandidateMutationPreparation.Rejected(
                        Nvs01CatalogContract.DiagnosticCodePrefix + "SAVE-CONFLICT");
                }

                candidateSave.Nvs01Progress = Nvs01ProgressCodec.Encode(plan.Candidate);
                return SaveCandidateMutationPreparation.Prepared();
            });

            if (result.IsCommitted &&
                result.PublishedSave != null &&
                Nvs01ProgressCodec.TryDecode(
                    result.PublishedSave.Nvs01Progress,
                    _verifiedCatalog,
                    out Nvs01QuestSnapshot persisted,
                    out _) &&
                Nvs01ProgressCodec.Equivalent(persisted, plan.Candidate))
            {
                committed = plan.Candidate;
                diagnostic = null;
                return true;
            }

            committed = plan.Expected;
            string code;
            switch (result.Outcome)
            {
                case SaveCandidateCommitOutcome.Committed:
                case SaveCandidateCommitOutcome.Duplicate:
                case SaveCandidateCommitOutcome.CommitUncertain:
                    code = "COMMIT-UNCERTAIN";
                    break;
                case SaveCandidateCommitOutcome.Rejected:
                    code = result.Message.IndexOf(
                               "SAVE-PROGRESS-UNAVAILABLE",
                               StringComparison.Ordinal) >= 0
                        ? "SAVE-PROGRESS-UNAVAILABLE"
                        : "SAVE-CONFLICT";
                    break;
                case SaveCandidateCommitOutcome.ReadOnly:
                    code = "SAVE-READ-ONLY";
                    break;
                default:
                    code = "SAVE-FAILED";
                    break;
            }

            diagnostic = new Nvs01RuntimeDiagnostic(
                code,
                "save-backed NVS-01 commit",
                "verified candidate",
                result.Message,
                plan.Expected.StateId,
                plan.TriggerEventId,
                plan.Expected.CurrentEncounter?.CorrelationId ?? string.Empty);
            return false;
        }
    }
}
