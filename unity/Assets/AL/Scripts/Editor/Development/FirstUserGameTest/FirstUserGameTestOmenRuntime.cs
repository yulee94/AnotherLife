#if !UNITY_EDITOR
#error The isolated first-user OMEN interaction is Editor-only.
#endif

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AL.Core;
using AL.Narrative.Nvs01;
using AL.Narrative.Nvs01.Contracts;
using AL.UI.Kingdom;
using UnityEditor;
using UnityEngine;

namespace AL.Editor.Development.FirstUserGameTest
{
    internal static class FirstUserGameTestOmenContract
    {
        internal const string ContractVersion =
            "al.editor.first-user-game-test.omen.v1";
        internal const string CanonicalAssetPath =
            "Assets/StreamingAssets/AL/Narrative/OMEN_1.catalog.json";
        internal const string OfferedState = "OFFERED";
        internal const string TalkObjective = "OBJ_OMEN_1_TALK";
        internal const string ArenaObjective = "OBJ_OMEN_1_ARENA";
        internal const string ReportObjective = "OBJ_OMEN_1_REPORT";
        internal const string OfferDialogue = "DLG_OMEN_1_OFFER";
        internal const string SelectValeriusEvent = "SELECT_VALERIUS";
        internal const int PendingDialogueRevision = 1;
        internal const int MaximumRetainedEnvelopeCharacters = 1024;

        internal static bool TryGetRealmId(RealmId realm, out string realmId)
        {
            switch (realm)
            {
                case RealmId.Crownlands:
                    realmId = "crownlands";
                    return true;
                case RealmId.Stonehold:
                    realmId = "stonehold";
                    return true;
                case RealmId.Eldergrove:
                    realmId = "eldergrove";
                    return true;
                case RealmId.Umbral:
                    realmId = "umbral";
                    return true;
                default:
                    realmId = string.Empty;
                    return false;
            }
        }

        internal static bool IsCanonicalRealmId(string value)
        {
            return string.Equals(value, "crownlands", StringComparison.Ordinal) ||
                   string.Equals(value, "stonehold", StringComparison.Ordinal) ||
                   string.Equals(value, "eldergrove", StringComparison.Ordinal) ||
                   string.Equals(value, "umbral", StringComparison.Ordinal);
        }

        internal static bool IsCanonicalGuid(string value)
        {
            Guid parsed;
            return Guid.TryParseExact(value, "D", out parsed) &&
                   parsed != Guid.Empty &&
                   string.Equals(parsed.ToString("D"), value, StringComparison.Ordinal);
        }

        internal static bool IsCanonicalSha256(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal sealed class FirstUserGameTestOmenProjection
    {
        internal FirstUserGameTestOmenProjection(
            string sessionId,
            string generation,
            string realmId,
            string operationId,
            string payloadFingerprint,
            long revision,
            string stateId,
            string dialogueNodeId,
            bool pendingChoice,
            string eventId)
        {
            SessionId = sessionId ?? string.Empty;
            Generation = generation ?? string.Empty;
            RealmId = realmId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            PayloadFingerprint = payloadFingerprint ?? string.Empty;
            Revision = revision;
            StateId = stateId ?? string.Empty;
            DialogueNodeId = dialogueNodeId ?? string.Empty;
            PendingChoice = pendingChoice;
            EventId = eventId ?? string.Empty;
        }

        internal string SessionId { get; }
        internal string Generation { get; }
        internal string RealmId { get; }
        internal string OperationId { get; }
        internal string PayloadFingerprint { get; }
        internal long Revision { get; }
        internal string StateId { get; }
        internal string DialogueNodeId { get; }
        internal bool PendingChoice { get; }
        internal string EventId { get; }

        internal bool ValueEquals(FirstUserGameTestOmenProjection other)
        {
            return other != null &&
                   string.Equals(SessionId, other.SessionId, StringComparison.Ordinal) &&
                   string.Equals(Generation, other.Generation, StringComparison.Ordinal) &&
                   string.Equals(RealmId, other.RealmId, StringComparison.Ordinal) &&
                   string.Equals(OperationId, other.OperationId, StringComparison.Ordinal) &&
                   string.Equals(
                       PayloadFingerprint,
                       other.PayloadFingerprint,
                       StringComparison.Ordinal) &&
                   Revision == other.Revision &&
                   string.Equals(StateId, other.StateId, StringComparison.Ordinal) &&
                   string.Equals(
                       DialogueNodeId,
                       other.DialogueNodeId,
                       StringComparison.Ordinal) &&
                   PendingChoice == other.PendingChoice &&
                   string.Equals(EventId, other.EventId, StringComparison.Ordinal);
        }
    }

    internal static class FirstUserGameTestOmenProjectionCodec
    {
        private const char Separator = '\n';

        internal static bool TryEncode(
            FirstUserGameTestOmenProjection projection,
            out string payload)
        {
            payload = string.Empty;
            if (!IsValid(projection))
            {
                return false;
            }

            payload = string.Join(
                Separator.ToString(),
                FirstUserGameTestOmenContract.ContractVersion,
                projection.SessionId,
                projection.Generation,
                projection.RealmId,
                projection.OperationId,
                projection.PayloadFingerprint,
                projection.Revision.ToString(CultureInfo.InvariantCulture),
                projection.StateId,
                projection.DialogueNodeId,
                projection.PendingChoice ? "1" : "0",
                projection.EventId);
            return payload.Length <=
                   FirstUserGameTestOmenContract.MaximumRetainedEnvelopeCharacters;
        }

        internal static bool TryDecode(
            string payload,
            out FirstUserGameTestOmenProjection projection)
        {
            projection = null;
            if (string.IsNullOrEmpty(payload) ||
                payload.Length >
                FirstUserGameTestOmenContract.MaximumRetainedEnvelopeCharacters ||
                payload.IndexOf('\r') >= 0)
            {
                return false;
            }

            string[] fields = payload.Split(Separator);
            if (fields.Length != 11 ||
                !string.Equals(
                    fields[0],
                    FirstUserGameTestOmenContract.ContractVersion,
                    StringComparison.Ordinal) ||
                !long.TryParse(
                    fields[6],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long revision) ||
                !string.Equals(
                    fields[6],
                    revision.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal) ||
                !string.Equals(fields[9], "1", StringComparison.Ordinal))
            {
                return false;
            }

            var candidate = new FirstUserGameTestOmenProjection(
                fields[1],
                fields[2],
                fields[3],
                fields[4],
                fields[5],
                revision,
                fields[7],
                fields[8],
                true,
                fields[10]);
            if (!IsValid(candidate) ||
                !TryEncode(candidate, out string canonical) ||
                !string.Equals(payload, canonical, StringComparison.Ordinal))
            {
                return false;
            }

            projection = candidate;
            return true;
        }

        internal static bool IsValid(FirstUserGameTestOmenProjection projection)
        {
            return projection != null &&
                   FirstUserGameTestTutorialContract.IsCanonicalSessionId(
                       projection.SessionId) &&
                   FirstUserGameTestTutorialContract.IsCanonicalGeneration(
                       projection.Generation) &&
                   FirstUserGameTestOmenContract.IsCanonicalRealmId(
                       projection.RealmId) &&
                   FirstUserGameTestOmenContract.IsCanonicalGuid(
                       projection.OperationId) &&
                   FirstUserGameTestOmenContract.IsCanonicalSha256(
                       projection.PayloadFingerprint) &&
                   projection.Revision ==
                   FirstUserGameTestOmenContract.PendingDialogueRevision &&
                   string.Equals(
                       projection.StateId,
                       FirstUserGameTestOmenContract.OfferedState,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       projection.DialogueNodeId,
                       FirstUserGameTestOmenContract.OfferDialogue,
                       StringComparison.Ordinal) &&
                   projection.PendingChoice &&
                   string.Equals(
                       projection.EventId,
                       FirstUserGameTestOmenContract.SelectValeriusEvent,
                       StringComparison.Ordinal);
        }
    }

    internal sealed class FirstUserGameTestOmenSessionStore
    {
        private const string KeyPrefix = "AL.FirstUserGameTest.Omen.v1.";

        private readonly string _sessionId;
        private readonly string _generation;
        private readonly string _realmId;
        private readonly string _key;

        internal FirstUserGameTestOmenSessionStore(
            string sessionId,
            string generation,
            string realmId)
        {
            if (!FirstUserGameTestTutorialContract.IsCanonicalSessionId(sessionId) ||
                !FirstUserGameTestTutorialContract.IsCanonicalGeneration(generation) ||
                !FirstUserGameTestOmenContract.IsCanonicalRealmId(realmId))
            {
                throw new ArgumentException(
                    "OMEN storage requires the exact isolated session, generation, and realm.");
            }

            _sessionId = sessionId;
            _generation = generation;
            _realmId = realmId;
            _key = KeyPrefix + sessionId;
        }

        internal string SessionId => _sessionId;
        internal string Generation => _generation;
        internal string RealmId => _realmId;

        internal bool TryLoad(
            out FirstUserGameTestOmenProjection projection,
            out string technicalDiagnostic)
        {
            projection = null;
            technicalDiagnostic = string.Empty;
            string payload = SessionState.GetString(_key, string.Empty);
            if (string.IsNullOrEmpty(payload))
            {
                return true;
            }

            if (!FirstUserGameTestOmenProjectionCodec.TryDecode(
                    payload,
                    out projection) ||
                !string.Equals(
                    projection.SessionId,
                    _sessionId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    projection.Generation,
                    _generation,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    projection.RealmId,
                    _realmId,
                    StringComparison.Ordinal))
            {
                projection = null;
                technicalDiagnostic = "OMEN_SESSION_PROJECTION_INVALID";
                return false;
            }

            return true;
        }

        internal bool TryPersist(
            FirstUserGameTestOmenProjection projection,
            out string technicalDiagnostic)
        {
            technicalDiagnostic = string.Empty;
            if (!FirstUserGameTestOmenProjectionCodec.TryEncode(
                    projection,
                    out string payload) ||
                !string.Equals(
                    projection.SessionId,
                    _sessionId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    projection.Generation,
                    _generation,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    projection.RealmId,
                    _realmId,
                    StringComparison.Ordinal))
            {
                technicalDiagnostic = "OMEN_SESSION_PROJECTION_REJECTED";
                return false;
            }

            string existingPayload = SessionState.GetString(_key, string.Empty);
            if (!string.IsNullOrEmpty(existingPayload))
            {
                if (FirstUserGameTestOmenProjectionCodec.TryDecode(
                        existingPayload,
                        out FirstUserGameTestOmenProjection existing) &&
                    existing.ValueEquals(projection))
                {
                    return true;
                }

                technicalDiagnostic = "OMEN_SESSION_PROJECTION_CONFLICT";
                return false;
            }

            SessionState.SetString(_key, payload);
            string retained = SessionState.GetString(_key, string.Empty);
            if (!string.Equals(payload, retained, StringComparison.Ordinal) ||
                !FirstUserGameTestOmenProjectionCodec.TryDecode(
                    retained,
                    out FirstUserGameTestOmenProjection verified) ||
                !projection.ValueEquals(verified))
            {
                technicalDiagnostic = "OMEN_SESSION_PROJECTION_VERIFY_FAILED";
                return false;
            }

            return true;
        }

        internal static void EraseSession(string sessionId)
        {
            if (FirstUserGameTestTutorialContract.IsCanonicalSessionId(sessionId))
            {
                SessionState.EraseString(KeyPrefix + sessionId);
            }
        }

        internal static void SetRawForTests(string sessionId, string payload)
        {
            if (FirstUserGameTestTutorialContract.IsCanonicalSessionId(sessionId))
            {
                SessionState.SetString(KeyPrefix + sessionId, payload ?? string.Empty);
            }
        }
    }

    internal static class FirstUserGameTestOmenCatalogLoader
    {
        internal static bool TryLoad(
            out Nvs01VerifiedCatalog verifiedCatalog,
            out string technicalDiagnostic)
        {
            verifiedCatalog = null;
            technicalDiagnostic = string.Empty;
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                {
                    technicalDiagnostic = "OMEN_PROJECT_ROOT_UNAVAILABLE";
                    return false;
                }

                string relativePath =
                    FirstUserGameTestOmenContract.CanonicalAssetPath.Replace(
                        '/',
                        Path.DirectorySeparatorChar);
                string fullPath = Path.GetFullPath(
                    Path.Combine(projectRoot, relativePath));
                string expectedPath = Path.GetFullPath(
                    Path.Combine(
                        projectRoot,
                        FirstUserGameTestOmenContract.CanonicalAssetPath.Replace(
                            '/',
                            Path.DirectorySeparatorChar)));
                if (!string.Equals(
                        fullPath,
                        expectedPath,
                        StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(fullPath))
                {
                    technicalDiagnostic = "OMEN_CANONICAL_ASSET_MISSING";
                    return false;
                }

                string guid = AssetDatabase.AssetPathToGUID(
                    FirstUserGameTestOmenContract.CanonicalAssetPath);
                if (string.IsNullOrEmpty(guid) ||
                    !string.Equals(
                        AssetDatabase.GUIDToAssetPath(guid),
                        FirstUserGameTestOmenContract.CanonicalAssetPath,
                        StringComparison.Ordinal))
                {
                    technicalDiagnostic = "OMEN_CANONICAL_ASSET_IDENTITY_INVALID";
                    return false;
                }

                var info = new FileInfo(fullPath);
                if (info.Length != Nvs01CatalogContract.CanonicalByteLength ||
                    info.Length <= 0 ||
                    info.Length > Nvs01CatalogContract.MaximumByteLength)
                {
                    technicalDiagnostic = "OMEN_CANONICAL_ASSET_LENGTH_INVALID";
                    return false;
                }

                byte[] bytes = File.ReadAllBytes(fullPath);
                Nvs01CatalogValidationResult validation =
                    Nvs01CatalogValidator.ValidateCanonicalArtifact(bytes);
                if (!validation.IsAccepted || validation.VerifiedCatalog == null)
                {
                    technicalDiagnostic = validation.Diagnostics.Count > 0
                        ? validation.Diagnostics[0].Code
                        : "OMEN_CANONICAL_ASSET_REJECTED";
                    return false;
                }

                verifiedCatalog = validation.VerifiedCatalog;
                return true;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                technicalDiagnostic =
                    "OMEN_CANONICAL_ASSET_EXCEPTION:" +
                    exception.GetType().Name;
                return false;
            }
        }
    }

    internal static class FirstUserGameTestOmenSnapshotRules
    {
        internal static bool IsInitialSnapshotExact(Nvs01QuestSnapshot snapshot)
        {
            return HasExactIdentity(snapshot) &&
                   snapshot.Revision == 0 &&
                   string.Equals(
                       snapshot.StateId,
                       FirstUserGameTestOmenContract.OfferedState,
                       StringComparison.Ordinal) &&
                   snapshot.CurrentDialogueNodeId.Length == 0 &&
                   !snapshot.PendingChoice &&
                   snapshot.PendingSemanticActionId.Length == 0 &&
                   snapshot.CommittedRealmId.Length == 0 &&
                   snapshot.EncounterStatus == Nvs01EncounterStatus.None &&
                   snapshot.CurrentEncounter == null &&
                   snapshot.LastOperation == null &&
                   snapshot.ConsequenceIntentIds.Count == 0 &&
                   HasInitialObjectives(snapshot);
        }

        internal static bool TryCreateProjection(
            Nvs01QuestSnapshot snapshot,
            string sessionId,
            string generation,
            string realmId,
            out FirstUserGameTestOmenProjection projection,
            out string technicalDiagnostic)
        {
            projection = null;
            technicalDiagnostic = "OMEN_PENDING_DIALOGUE_SHAPE_INVALID";
            if (!HasExactIdentity(snapshot) ||
                snapshot.Revision !=
                FirstUserGameTestOmenContract.PendingDialogueRevision ||
                !string.Equals(
                    snapshot.StateId,
                    FirstUserGameTestOmenContract.OfferedState,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    snapshot.CurrentDialogueNodeId,
                    FirstUserGameTestOmenContract.OfferDialogue,
                    StringComparison.Ordinal) ||
                !snapshot.PendingChoice ||
                snapshot.PendingSemanticActionId.Length != 0 ||
                !string.Equals(
                    snapshot.CommittedRealmId,
                    realmId,
                    StringComparison.Ordinal) ||
                snapshot.EncounterStatus != Nvs01EncounterStatus.None ||
                snapshot.CurrentEncounter != null ||
                snapshot.LastEncounterCorrelationId.Length != 0 ||
                snapshot.LastEncounterOutcome.HasValue ||
                snapshot.LastEncounterEventId.Length != 0 ||
                snapshot.LastEncounterSnapshotVersion.Length != 0 ||
                snapshot.LastEncounterSnapshotReference.Length != 0 ||
                snapshot.ConsequenceIntentIds.Count != 0 ||
                !HasInitialObjectives(snapshot))
            {
                return false;
            }

            Nvs01OperationReceipt receipt = snapshot.LastOperation;
            if (receipt == null ||
                receipt.Status != Nvs01CommandStatus.Committed ||
                receipt.Revision != snapshot.Revision ||
                !string.Equals(
                    receipt.StateId,
                    FirstUserGameTestOmenContract.OfferedState,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    receipt.EventId,
                    FirstUserGameTestOmenContract.SelectValeriusEvent,
                    StringComparison.Ordinal) ||
                receipt.CorrelationId.Length != 0 ||
                receipt.ExpectedGenerationFingerprint.Length != 0)
            {
                return false;
            }

            var candidate = new FirstUserGameTestOmenProjection(
                sessionId,
                generation,
                realmId,
                receipt.OperationId,
                receipt.PayloadFingerprint,
                snapshot.Revision,
                snapshot.StateId,
                snapshot.CurrentDialogueNodeId,
                snapshot.PendingChoice,
                receipt.EventId);
            if (!FirstUserGameTestOmenProjectionCodec.IsValid(candidate))
            {
                return false;
            }

            projection = candidate;
            technicalDiagnostic = string.Empty;
            return true;
        }

        internal static bool TryBuildSnapshot(
            Nvs01VerifiedCatalog catalog,
            FirstUserGameTestOmenProjection projection,
            out Nvs01QuestSnapshot snapshot)
        {
            snapshot = null;
            if (catalog == null ||
                !FirstUserGameTestOmenProjectionCodec.IsValid(projection) ||
                !TryBuildInitialObjectives(catalog, out Nvs01ObjectiveSnapshot[] objectives))
            {
                return false;
            }

            try
            {
                snapshot = new Nvs01QuestSnapshot(
                    catalog.Catalog.PacketVersion,
                    catalog.CanonicalSha256,
                    catalog.Catalog.QuestId,
                    projection.Revision,
                    projection.StateId,
                    objectives,
                    projection.DialogueNodeId,
                    projection.PendingChoice,
                    string.Empty,
                    projection.RealmId,
                    Nvs01EncounterStatus.None,
                    null,
                    string.Empty,
                    null,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    new Nvs01OperationReceipt(
                        projection.OperationId,
                        projection.PayloadFingerprint,
                        Nvs01CommandStatus.Committed,
                        projection.Revision,
                        projection.StateId,
                        projection.EventId,
                        string.Empty),
                    Array.Empty<string>());
                return TryCreateProjection(
                    snapshot,
                    projection.SessionId,
                    projection.Generation,
                    projection.RealmId,
                    out FirstUserGameTestOmenProjection rebuilt,
                    out _) &&
                       rebuilt.ValueEquals(projection);
            }
            catch (ArgumentException)
            {
                snapshot = null;
                return false;
            }
        }

        private static bool HasExactIdentity(Nvs01QuestSnapshot snapshot)
        {
            return snapshot != null &&
                   string.Equals(
                       snapshot.PacketVersion,
                       Nvs01CatalogContract.PacketVersion,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       snapshot.PacketSha256,
                       Nvs01CatalogContract.CanonicalSha256,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       snapshot.QuestId,
                       Nvs01CatalogContract.QuestId,
                       StringComparison.Ordinal);
        }

        private static bool HasInitialObjectives(Nvs01QuestSnapshot snapshot)
        {
            if (snapshot.Objectives.Count != 3)
            {
                return false;
            }

            return snapshot.TryGetObjectiveStatus(
                       FirstUserGameTestOmenContract.TalkObjective,
                       out Nvs01ObjectiveStatus talk) &&
                   talk == Nvs01ObjectiveStatus.Active &&
                   snapshot.TryGetObjectiveStatus(
                       FirstUserGameTestOmenContract.ArenaObjective,
                       out Nvs01ObjectiveStatus arena) &&
                   arena == Nvs01ObjectiveStatus.Inactive &&
                   snapshot.TryGetObjectiveStatus(
                       FirstUserGameTestOmenContract.ReportObjective,
                       out Nvs01ObjectiveStatus report) &&
                   report == Nvs01ObjectiveStatus.Inactive;
        }

        private static bool TryBuildInitialObjectives(
            Nvs01VerifiedCatalog catalog,
            out Nvs01ObjectiveSnapshot[] objectives)
        {
            objectives = null;
            if (catalog.Catalog.Objectives.Count != 3)
            {
                return false;
            }

            var result = new Nvs01ObjectiveSnapshot[3];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < catalog.Catalog.Objectives.Count; index++)
            {
                Nvs01Objective definition = catalog.Catalog.Objectives[index];
                if (definition == null || !seen.Add(definition.Id))
                {
                    return false;
                }

                Nvs01ObjectiveStatus status;
                if (string.Equals(
                        definition.Id,
                        FirstUserGameTestOmenContract.TalkObjective,
                        StringComparison.Ordinal))
                {
                    status = Nvs01ObjectiveStatus.Active;
                }
                else if (string.Equals(
                             definition.Id,
                             FirstUserGameTestOmenContract.ArenaObjective,
                             StringComparison.Ordinal) ||
                         string.Equals(
                             definition.Id,
                             FirstUserGameTestOmenContract.ReportObjective,
                             StringComparison.Ordinal))
                {
                    status = Nvs01ObjectiveStatus.Inactive;
                }
                else
                {
                    return false;
                }

                result[index] = new Nvs01ObjectiveSnapshot(definition.Id, status);
            }

            objectives = result;
            return true;
        }
    }

    internal sealed class FirstUserGameTestOmenSessionCommitter :
        INvs01MutationCommitter
    {
        private readonly FirstUserGameTestOmenSessionStore _store;
        private readonly string _sessionId;
        private readonly string _generation;
        private readonly string _realmId;

        internal FirstUserGameTestOmenSessionCommitter(
            FirstUserGameTestOmenSessionStore store,
            string sessionId,
            string generation,
            string realmId)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _sessionId = sessionId;
            _generation = generation;
            _realmId = realmId;
        }

        internal int AttemptCount { get; private set; }

        public bool TryCommit(
            Nvs01MutationPlan plan,
            out Nvs01QuestSnapshot committed,
            out Nvs01RuntimeDiagnostic diagnostic)
        {
            AttemptCount++;
            committed = plan?.Expected;
            diagnostic = null;
            string projectionDiagnostic = string.Empty;
            if (plan == null ||
                !string.Equals(
                    plan.TriggerEventId,
                    FirstUserGameTestOmenContract.SelectValeriusEvent,
                    StringComparison.Ordinal) ||
                plan.ConsequenceIntentIds.Count != 0 ||
                !FirstUserGameTestOmenSnapshotRules.IsInitialSnapshotExact(
                    plan.Expected) ||
                !FirstUserGameTestOmenSnapshotRules.TryCreateProjection(
                    plan.Candidate,
                    _sessionId,
                    _generation,
                    _realmId,
                    out FirstUserGameTestOmenProjection projection,
                    out projectionDiagnostic))
            {
                diagnostic = Diagnostic(
                    plan?.Expected,
                    "SAVE-FAILED",
                    string.IsNullOrEmpty(projectionDiagnostic)
                        ? "OMEN_MUTATION_SCOPE_REJECTED"
                        : projectionDiagnostic);
                return false;
            }

            if (!_store.TryPersist(projection, out string storeDiagnostic))
            {
                diagnostic = Diagnostic(
                    plan.Expected,
                    "SAVE-FAILED",
                    storeDiagnostic);
                return false;
            }

            committed = plan.Candidate;
            return true;
        }

        private static Nvs01RuntimeDiagnostic Diagnostic(
            Nvs01QuestSnapshot snapshot,
            string code,
            string actual)
        {
            return new Nvs01RuntimeDiagnostic(
                code,
                "isolated SessionState commit",
                FirstUserGameTestOmenContract.SelectValeriusEvent,
                actual ?? string.Empty,
                snapshot?.StateId ?? string.Empty,
                FirstUserGameTestOmenContract.SelectValeriusEvent,
                string.Empty);
        }
    }

    internal sealed class FirstUserGameTestOmenInteraction
    {
        private readonly FirstUserGameTestOmenSessionStore _store;
        private readonly FirstUserGameTestOmenSessionCommitter _committer;
        private readonly Nvs01QuestRuntime _runtime;
        private readonly Nvs01KingdomPresenter _presenter;
        private bool _reportOpen;
        private bool _selectValeriusAttempted;
        private int _selectValeriusInvocationCount;

        private FirstUserGameTestOmenInteraction(
            FirstUserGameTestOmenSessionStore store,
            FirstUserGameTestOmenSessionCommitter committer,
            Nvs01QuestRuntime runtime,
            Nvs01KingdomPresenter presenter,
            bool reportOpen)
        {
            _store = store;
            _committer = committer;
            _runtime = runtime;
            _presenter = presenter;
            _reportOpen = reportOpen;
            View = presenter.Present();
        }

        internal Nvs01KingdomView View { get; private set; }
        internal Nvs01QuestSnapshot Snapshot => _runtime.Snapshot;
        internal bool IsReportOpen => _reportOpen;
        internal int SelectValeriusInvocationCount =>
            _selectValeriusInvocationCount;
        internal int CommitAttemptCount => _committer.AttemptCount;

        internal static bool TryCreate(
            string sessionId,
            string generation,
            RealmId realm,
            out FirstUserGameTestOmenInteraction interaction,
            out string friendlyMessage,
            out string technicalDiagnostic)
        {
            interaction = null;
            friendlyMessage = string.Empty;
            technicalDiagnostic = string.Empty;
            if (!FirstUserGameTestTutorialContract.IsCanonicalSessionId(sessionId) ||
                !FirstUserGameTestTutorialContract.IsCanonicalGeneration(generation) ||
                !FirstUserGameTestOmenContract.TryGetRealmId(
                    realm,
                    out string realmId))
            {
                technicalDiagnostic = "OMEN_SESSION_IDENTITY_INVALID";
                friendlyMessage =
                    "Valerius's report is unavailable in this isolated playtest.";
                return false;
            }

            if (!FirstUserGameTestOmenCatalogLoader.TryLoad(
                    out Nvs01VerifiedCatalog catalog,
                    out technicalDiagnostic))
            {
                friendlyMessage =
                    "Valerius's report is unavailable in this isolated playtest.";
                return false;
            }

            FirstUserGameTestOmenSessionStore store;
            try
            {
                store = new FirstUserGameTestOmenSessionStore(
                    sessionId,
                    generation,
                    realmId);
            }
            catch (ArgumentException)
            {
                technicalDiagnostic = "OMEN_SESSION_STORE_INVALID";
                friendlyMessage =
                    "Valerius's report is unavailable in this isolated playtest.";
                return false;
            }

            if (!store.TryLoad(
                    out FirstUserGameTestOmenProjection retained,
                    out technicalDiagnostic))
            {
                friendlyMessage =
                    "The retained quest report could not be verified. Exit the isolated playtest and review the Console.";
                return false;
            }

            Nvs01QuestSnapshot initialSnapshot = null;
            if (retained != null &&
                !FirstUserGameTestOmenSnapshotRules.TryBuildSnapshot(
                    catalog,
                    retained,
                    out initialSnapshot))
            {
                technicalDiagnostic = "OMEN_SESSION_SNAPSHOT_REBUILD_FAILED";
                friendlyMessage =
                    "The retained quest report could not be verified. Exit the isolated playtest and review the Console.";
                return false;
            }

            string operationId;
            if (!TryCreateOperationId(
                    sessionId,
                    generation,
                    realmId,
                    out operationId))
            {
                technicalDiagnostic = "OMEN_OPERATION_ID_UNAVAILABLE";
                friendlyMessage =
                    "Valerius's report is unavailable in this isolated playtest.";
                return false;
            }

            var committer = new FirstUserGameTestOmenSessionCommitter(
                store,
                sessionId,
                generation,
                realmId);
            Nvs01QuestRuntime runtime;
            try
            {
                runtime = new Nvs01QuestRuntime(
                    catalog,
                    initialSnapshot,
                    committer,
                    () => operationId);
            }
            catch (ArgumentException)
            {
                technicalDiagnostic = "OMEN_RUNTIME_REHYDRATION_REJECTED";
                friendlyMessage =
                    "The retained quest report could not be verified. Exit the isolated playtest and review the Console.";
                return false;
            }

            var presenter = new Nvs01KingdomPresenter(
                runtime,
                () => new Nvs01RealmContext(
                    Nvs01RealmContextStatus.CommittedValid,
                    realmId),
                () => new Nvs01CapabilitySnapshot(
                    new Dictionary<string, bool>(StringComparer.Ordinal)),
                () => operationId,
                () => 0L);
            var candidate = new FirstUserGameTestOmenInteraction(
                store,
                committer,
                runtime,
                presenter,
                retained != null);
            if (!candidate.TryValidateCurrentSurface(
                    retained != null,
                    out technicalDiagnostic))
            {
                friendlyMessage =
                    "The retained quest report could not be verified. Exit the isolated playtest and review the Console.";
                return false;
            }

            interaction = candidate;
            return true;
        }

        internal bool TryOpenReport(
            out bool changed,
            out string friendlyMessage,
            out string technicalDiagnostic)
        {
            changed = false;
            friendlyMessage = string.Empty;
            technicalDiagnostic = string.Empty;
            if (_reportOpen)
            {
                return true;
            }

            if (_selectValeriusAttempted)
            {
                technicalDiagnostic = "OMEN_SELECT_VALERIUS_ALREADY_ATTEMPTED";
                return false;
            }

            _selectValeriusAttempted = true;
            _selectValeriusInvocationCount++;
            Nvs01KingdomActionResult result = _presenter.SelectValerius();
            if (result == null ||
                result.Disposition == null ||
                result.Disposition.Status != Nvs01CommandStatus.Committed ||
                result.EncounterRequest != null ||
                !FirstUserGameTestOmenSnapshotRules.TryCreateProjection(
                    result.Disposition.Snapshot,
                    _store.SessionId,
                    _store.Generation,
                    _store.RealmId,
                    out FirstUserGameTestOmenProjection committedProjection,
                    out technicalDiagnostic) ||
                !_store.TryLoad(
                    out FirstUserGameTestOmenProjection retainedProjection,
                    out technicalDiagnostic) ||
                retainedProjection == null ||
                !committedProjection.ValueEquals(retainedProjection))
            {
                friendlyMessage =
                    "Valerius's report could not be opened. Exit the isolated playtest and review the Console.";
                if (string.IsNullOrEmpty(technicalDiagnostic))
                {
                    technicalDiagnostic = "OMEN_SELECT_VALERIUS_REJECTED";
                }

                return false;
            }

            View = result.View;
            _reportOpen = true;
            if (!TryValidateCurrentSurface(true, out technicalDiagnostic))
            {
                friendlyMessage =
                    "Valerius's report could not be verified. Exit the isolated playtest and review the Console.";
                return false;
            }

            changed = true;
            return true;
        }

        private bool TryValidateCurrentSurface(
            bool expectReportOpen,
            out string technicalDiagnostic)
        {
            technicalDiagnostic = string.Empty;
            if (View == null ||
                View.Status != Nvs01KingdomViewStatus.Ready ||
                View.HasDiagnostic ||
                !string.Equals(
                    View.StateId,
                    FirstUserGameTestOmenContract.OfferedState,
                    StringComparison.Ordinal) ||
                _runtime.Snapshot.ConsequenceIntentIds.Count != 0)
            {
                technicalDiagnostic = "OMEN_PRESENTATION_SURFACE_INVALID";
                return false;
            }

            if (!expectReportOpen)
            {
                if (_runtime.Snapshot.Revision != 0 ||
                    _runtime.Snapshot.CurrentDialogueNodeId.Length != 0 ||
                    _runtime.Snapshot.PendingChoice ||
                    View.HasDialogue ||
                    View.Choices.Count != 0 ||
                    !FirstUserGameTestOmenSnapshotRules.IsInitialSnapshotExact(
                        _runtime.Snapshot))
                {
                    technicalDiagnostic = "OMEN_INITIAL_SURFACE_INVALID";
                    return false;
                }

                return true;
            }

            if (!FirstUserGameTestOmenSnapshotRules.TryCreateProjection(
                    _runtime.Snapshot,
                    _store.SessionId,
                    _store.Generation,
                    _store.RealmId,
                    out _,
                    out technicalDiagnostic) ||
                !View.HasDialogue ||
                View.Choices.Count != 2)
            {
                if (string.IsNullOrEmpty(technicalDiagnostic))
                {
                    technicalDiagnostic = "OMEN_PENDING_DIALOGUE_SURFACE_INVALID";
                }

                return false;
            }

            return true;
        }

        private static bool TryCreateOperationId(
            string sessionId,
            string generation,
            string realmId,
            out string operationId)
        {
            operationId = string.Empty;
            try
            {
                byte[] payload = Encoding.UTF8.GetBytes(
                    FirstUserGameTestOmenContract.ContractVersion + "\n" +
                    sessionId + "\n" + generation + "\n" + realmId);
                byte[] digest;
                using (SHA256 sha = SHA256.Create())
                {
                    digest = sha.ComputeHash(payload);
                }

                const string hex = "0123456789abcdef";
                var raw = new char[32];
                for (int index = 0; index < 16; index++)
                {
                    raw[index * 2] = hex[digest[index] >> 4];
                    raw[index * 2 + 1] = hex[digest[index] & 0x0f];
                }

                string value = new string(raw);
                operationId = value.Substring(0, 8) + "-" +
                              value.Substring(8, 4) + "-" +
                              value.Substring(12, 4) + "-" +
                              value.Substring(16, 4) + "-" +
                              value.Substring(20, 12);
                return FirstUserGameTestOmenContract.IsCanonicalGuid(operationId);
            }
            catch (Exception exception) when (
                exception is EncoderFallbackException ||
                exception is CryptographicException)
            {
                operationId = string.Empty;
                return false;
            }
        }
    }
}
