#if !UNITY_EDITOR
#error The isolated first-user Game Test adapter is Editor-only.
#endif

using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AL.Core;
using AL.Editor.Development.OnboardingAuthority;
using AL.UI.FirstUserIdentity;

namespace AL.Editor.Development.FirstUserGameTest
{
    internal enum FirstUserGameTestAdapterStatus
    {
        Invalid = 0,
        Rejected = 1,
        Blocked = 2,
        Eligible = 3
    }

    internal enum FirstUserGameTestAdapterFailure
    {
        None = 0,
        SelectionInvalid = 1,
        CustomizationInvalid = 2,
        DevelopmentHandleInvalid = 3,
        CommitmentUnavailable = 4,
        AuthorityRejected = 5,
        ReceiptVerificationFailed = 6,
        ProjectionRejected = 7,
        ProjectionVerificationFailed = 8,
        RouteAdmissionRejected = 9,
        DevelopmentCeilingViolated = 10,
        RetainedStateInvalid = 11
    }

    /// <summary>
    /// Session-only input for the Editor Game Test authority emulator. The handle is a
    /// transport-bounded development label, not a production username or uniqueness claim.
    /// </summary>
    internal sealed class FirstUserGameTestSelection
    {
        internal FirstUserGameTestSelection(
            string sessionId,
            FirstUserIdentityDraftSnapshot identity,
            string customizationId,
            string developmentHandle)
        {
            SessionId = sessionId ?? string.Empty;
            Identity = identity;
            CustomizationId = customizationId ?? string.Empty;
            DevelopmentHandle = developmentHandle ?? string.Empty;
        }

        internal string SessionId { get; }
        internal FirstUserIdentityDraftSnapshot Identity { get; }
        internal string CustomizationId { get; }
        internal string DevelopmentHandle { get; }
    }

    internal sealed class FirstUserGameTestAdapterResult
    {
        internal FirstUserGameTestAdapterResult(
            FirstUserGameTestAdapterStatus status,
            FirstUserGameTestAdapterFailure failure,
            DevelopmentAuthorityFailure authorityFailure,
            FirstUserRoutePlan routePlan,
            FirstUserGameTestSelection selection,
            VerifiedDevelopmentReceipt receipt,
            VerifiedDevelopmentProjection projection)
        {
            Status = status;
            Failure = failure;
            AuthorityFailure = authorityFailure;
            RoutePlan = routePlan;
            Selection = selection;
            Receipt = receipt;
            Projection = projection;
        }

        internal FirstUserGameTestAdapterStatus Status { get; }
        internal FirstUserGameTestAdapterFailure Failure { get; }
        internal DevelopmentAuthorityFailure AuthorityFailure { get; }
        internal FirstUserRoutePlan RoutePlan { get; }
        internal FirstUserGameTestSelection Selection { get; }
        internal VerifiedDevelopmentReceipt Receipt { get; }
        internal VerifiedDevelopmentProjection Projection { get; }
        internal bool CanEnterIsolatedCharacterGameTest =>
            Status == FirstUserGameTestAdapterStatus.Eligible &&
            Failure == FirstUserGameTestAdapterFailure.None &&
            AuthorityFailure == DevelopmentAuthorityFailure.None &&
            Receipt != null && Receipt.IsValid &&
            Projection != null && Projection.IsValid &&
            RoutePlan.AllowsIsolatedCharacterGameTest;
    }

    internal interface IFirstUserGameTestDevelopmentWritableVerifier
    {
        bool IsDevelopmentWritable(
            VerifiedDevelopmentReceipt receipt,
            VerifiedDevelopmentProjection projection);
    }

    /// <summary>
    /// Editor-only adapter joining the development authority emulator to the pure #494 route
    /// planner. It never exposes production evidence, saves, scenes, callbacks, or route authority.
    /// </summary>
    internal sealed class FirstUserGameTestAdapter
    {
        internal const string ContractVersion = "al.editor.first-user-game-test.v1";
        internal const int MaximumCustomizationIdLength = 64;
        internal const int MaximumHandleCodeUnits = 32;
        internal const int MaximumHandleUtf8Bytes = 64;

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly string _sessionId;
        private readonly string _authorityInstanceId;
        private readonly string _projectionInstanceId;
        private readonly DeterministicDevelopmentOnboardingAuthorityEmulator _authority;
        private readonly DeterministicDevelopmentLocalProjectionEmulator _projection;

        internal FirstUserGameTestAdapter(string sessionId)
            : this(
                sessionId,
                new DeterministicDevelopmentOnboardingAuthorityEmulator(
                    BuildInstanceId("al-game-test-auth-", sessionId)),
                new DeterministicDevelopmentLocalProjectionEmulator(
                    BuildInstanceId("al-game-test-proj-", sessionId)))
        {
        }

        private FirstUserGameTestAdapter(
            string sessionId,
            DeterministicDevelopmentOnboardingAuthorityEmulator authority,
            DeterministicDevelopmentLocalProjectionEmulator projection)
        {
            if (!IsCanonicalSessionId(sessionId))
            {
                throw new ArgumentException(
                    "The Game Test session ID must be exactly 32 lowercase hexadecimal characters.",
                    nameof(sessionId));
            }

            _sessionId = sessionId;
            _authorityInstanceId = BuildInstanceId("al-game-test-auth-", sessionId);
            _projectionInstanceId = BuildInstanceId("al-game-test-proj-", sessionId);
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
            _projection = projection ?? throw new ArgumentNullException(nameof(projection));
        }

        internal byte[] CaptureAuthorityState() => _authority.CaptureRetainedState();

        internal byte[] CaptureProjectionState() => _projection.CaptureRetainedState();

        internal static bool TryRestore(
            string sessionId,
            byte[] authorityState,
            byte[] projectionState,
            out FirstUserGameTestAdapter adapter,
            out FirstUserGameTestAdapterFailure failure)
        {
            adapter = null;
            failure = FirstUserGameTestAdapterFailure.RetainedStateInvalid;
            if (!IsCanonicalSessionId(sessionId) || authorityState == null || projectionState == null)
            {
                return false;
            }

            string authorityInstanceId = BuildInstanceId("al-game-test-auth-", sessionId);
            string projectionInstanceId = BuildInstanceId("al-game-test-proj-", sessionId);
            if (!DeterministicDevelopmentOnboardingAuthorityEmulator.TryRestore(
                    authorityInstanceId,
                    DevelopmentHandleAvailabilityFixtures.Empty,
                    authorityState,
                    out DeterministicDevelopmentOnboardingAuthorityEmulator authority,
                    out _) ||
                !DeterministicDevelopmentLocalProjectionEmulator.TryRestore(
                    projectionInstanceId,
                    projectionState,
                    out DeterministicDevelopmentLocalProjectionEmulator projection,
                    out _))
            {
                return false;
            }

            adapter = new FirstUserGameTestAdapter(sessionId, authority, projection);
            failure = FirstUserGameTestAdapterFailure.None;
            return true;
        }

        internal FirstUserGameTestAdapterResult CommitAndEvaluate(
            FirstUserGameTestSelection selection,
            bool hostReady,
            IFirstUserGameTestDevelopmentWritableVerifier writableVerifier)
        {
            if (!TryValidateSelection(selection, out FirstUserGameTestAdapterFailure failure))
            {
                return Reject(failure);
            }

            if (!TryBuildCommitRequest(
                    selection,
                    out DevelopmentOnboardingCommitRequest request,
                    out Commitment32 localProfileScope))
            {
                return Reject(FirstUserGameTestAdapterFailure.CommitmentUnavailable);
            }

            DevelopmentOnboardingCommitResult commit = _authority.TryCommit(request);
            if ((commit.State != DevelopmentOnboardingCommitState.Committed &&
                 commit.State != DevelopmentOnboardingCommitState.ReplayCommitted) ||
                commit.Failure != DevelopmentAuthorityFailure.None ||
                commit.Receipt == null)
            {
                return Reject(
                    FirstUserGameTestAdapterFailure.AuthorityRejected,
                    commit.Failure);
            }

            VerifiedDevelopmentReceipt receipt = _authority.Verify(
                commit.Receipt,
                request,
                commit.Receipt.Handle);
            if (receipt == null || !receipt.IsValid)
            {
                return Reject(
                    FirstUserGameTestAdapterFailure.ReceiptVerificationFailed,
                    receipt == null
                        ? DevelopmentAuthorityFailure.IntegrityFailure
                        : receipt.Failure);
            }

            DevelopmentProjectionResult projected = _projection.TryProject(
                localProfileScope,
                expectedLocalProjectionRevision: 0UL,
                receipt);
            if ((projected.State != DevelopmentProjectionState.Projected &&
                 projected.State != DevelopmentProjectionState.ReplayProjected) ||
                projected.Failure != DevelopmentAuthorityFailure.None ||
                projected.VerifiedProjection == null ||
                projected.VerifiedProjection.Marker == null)
            {
                return Reject(
                    FirstUserGameTestAdapterFailure.ProjectionRejected,
                    projected.Failure);
            }

            VerifiedDevelopmentProjection projection = _projection.Verify(
                projected.VerifiedProjection.Marker,
                localProfileScope,
                receipt.Handle,
                expectedLocalProjectionRevision: 0UL,
                projected.VerifiedProjection.Handle);
            if (projection == null || !projection.IsValid)
            {
                return Reject(
                    FirstUserGameTestAdapterFailure.ProjectionVerificationFailed,
                    projection == null
                        ? DevelopmentAuthorityFailure.IntegrityFailure
                        : projection.Failure);
            }

            bool writable;
            try
            {
                writable = writableVerifier != null &&
                           writableVerifier.IsDevelopmentWritable(receipt, projection);
            }
            catch
            {
                writable = false;
            }

            var completeSnapshot = new FirstUserRouteSnapshot(
                realmValidated: true,
                originRaceValidated: true,
                classSelectionValidated: true,
                customizationValidated: true,
                handleValidated: true,
                authoritativeReceiptVerified: receipt.IsValid,
                localProjectionVerified: projection.IsValid,
                hostReady: hostReady,
                writable: writable,
                evidenceOrigin: FirstUserRouteEvidenceOrigin.DevelopmentEmulatorV1,
                cursor: new FirstUserRouteCursorEvidence(
                    FirstUserRouteCursorState.Matching,
                    FirstUserJourneyStep.Complete));

            FirstUserRoutePlan isolated = FirstUserRouteAdmissionPlanner.Plan(
                FirstUserRouteIntent.RequestIsolatedCharacterGameTest,
                completeSnapshot);
            FirstUserRoutePlan production = FirstUserRouteAdmissionPlanner.Plan(
                FirstUserRouteIntent.RequestGameplay,
                completeSnapshot);
            FirstUserRoutePlan kingdom = FirstUserRouteAdmissionPlanner.Plan(
                FirstUserRouteIntent.RequestKingdom,
                completeSnapshot);

            if (production.Status != FirstUserRoutePlanStatus.Rejected ||
                production.Diagnostic != FirstUserRouteDiagnostic.DevelopmentEvidenceCeiling ||
                production.AllowsGameplay ||
                kingdom.Status != FirstUserRoutePlanStatus.Rejected ||
                kingdom.Diagnostic != FirstUserRouteDiagnostic.KingdomAuthorityUnavailable ||
                kingdom.Destination != FirstUserRouteDestination.None)
            {
                return Reject(
                    FirstUserGameTestAdapterFailure.DevelopmentCeilingViolated,
                    DevelopmentAuthorityFailure.IntegrityFailure,
                    isolated,
                    selection,
                    receipt,
                    projection);
            }

            if (!isolated.AllowsIsolatedCharacterGameTest)
            {
                return new FirstUserGameTestAdapterResult(
                    isolated.Status == FirstUserRoutePlanStatus.AdmissionBlocked
                        ? FirstUserGameTestAdapterStatus.Blocked
                        : FirstUserGameTestAdapterStatus.Rejected,
                    FirstUserGameTestAdapterFailure.RouteAdmissionRejected,
                    DevelopmentAuthorityFailure.None,
                    isolated,
                    selection,
                    receipt,
                    projection);
            }

            return new FirstUserGameTestAdapterResult(
                FirstUserGameTestAdapterStatus.Eligible,
                FirstUserGameTestAdapterFailure.None,
                DevelopmentAuthorityFailure.None,
                isolated,
                selection,
                receipt,
                projection);
        }

        internal static bool IsValidDevelopmentHandle(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaximumHandleCodeUnits ||
                char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[value.Length - 1]))
            {
                return false;
            }

            int byteCount = 0;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (char.IsControl(character) ||
                    character == '\u2028' || character == '\u2029')
                {
                    return false;
                }

                if (char.IsHighSurrogate(character))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                    {
                        return false;
                    }

                    index++;
                }
                else if (char.IsLowSurrogate(character))
                {
                    return false;
                }
            }

            try
            {
                byteCount = StrictUtf8.GetByteCount(value);
            }
            catch (EncoderFallbackException)
            {
                return false;
            }

            return byteCount > 0 && byteCount <= MaximumHandleUtf8Bytes;
        }

        private bool TryValidateSelection(
            FirstUserGameTestSelection selection,
            out FirstUserGameTestAdapterFailure failure)
        {
            failure = FirstUserGameTestAdapterFailure.SelectionInvalid;
            if (selection == null ||
                !string.Equals(selection.SessionId, _sessionId, StringComparison.Ordinal) ||
                selection.Identity == null ||
                !selection.Identity.IsCustomizationReady ||
                !selection.Identity.ClassFamily.HasValue ||
                !FirstUserIdentityDerivation.TryDeriveRace(
                    selection.Identity.Realm,
                    out FirstUserRace derivedRace) ||
                derivedRace != selection.Identity.Race ||
                !FirstUserIdentityDerivation.IsSupportedClassFamily(
                    selection.Identity.ClassFamily.Value))
            {
                return false;
            }

            if (!IsCanonicalCustomizationId(selection.CustomizationId))
            {
                failure = FirstUserGameTestAdapterFailure.CustomizationInvalid;
                return false;
            }

            if (!IsValidDevelopmentHandle(selection.DevelopmentHandle))
            {
                failure = FirstUserGameTestAdapterFailure.DevelopmentHandleInvalid;
                return false;
            }

            failure = FirstUserGameTestAdapterFailure.None;
            return true;
        }

        private bool TryBuildCommitRequest(
            FirstUserGameTestSelection selection,
            out DevelopmentOnboardingCommitRequest request,
            out Commitment32 localProfileScope)
        {
            request = default;
            localProfileScope = default;
            try
            {
                Commitment32 authorityScope = HashCommitment(
                    "authority-scope",
                    _sessionId);
                Commitment32 operation = HashCommitment(
                    "operation",
                    _sessionId);
                Commitment32 semantic = HashCommitment(
                    "semantic",
                    ((int)selection.Identity.Realm).ToString(CultureInfo.InvariantCulture),
                    ((int)selection.Identity.Race).ToString(CultureInfo.InvariantCulture),
                    ((int)selection.Identity.ClassFamily.Value).ToString(CultureInfo.InvariantCulture),
                    selection.CustomizationId,
                    selection.DevelopmentHandle);
                Commitment32 compiledCore = HashCommitment(
                    "compiled-core",
                    ContractVersion,
                    ((int)selection.Identity.Realm).ToString(CultureInfo.InvariantCulture),
                    ((int)selection.Identity.Race).ToString(CultureInfo.InvariantCulture),
                    ((int)selection.Identity.ClassFamily.Value).ToString(CultureInfo.InvariantCulture),
                    selection.CustomizationId);
                Commitment32 handle = HashCommitment(
                    "development-handle-exact",
                    selection.DevelopmentHandle);
                localProfileScope = HashCommitment(
                    "local-profile-scope",
                    _sessionId);

                request = new DevelopmentOnboardingCommitRequest(
                    authorityScope,
                    operation,
                    semantic,
                    compiledCore,
                    handle,
                    expectedGeneration: 0UL);
                return request.IsValid &&
                       localProfileScope.IsValid && !localProfileScope.IsZero;
            }
            catch (Exception exception) when (
                exception is CryptographicException ||
                exception is EncoderFallbackException ||
                exception is IOException ||
                exception is ArgumentException)
            {
                request = default;
                localProfileScope = default;
                return false;
            }
        }

        private static Commitment32 HashCommitment(string tag, params string[] fields)
        {
            using (var stream = new MemoryStream(512))
            {
                WriteField(stream, ContractVersion);
                WriteField(stream, tag);
                for (int index = 0; index < fields.Length; index++)
                {
                    WriteField(stream, fields[index]);
                }

                using (SHA256 sha = SHA256.Create())
                {
                    return new Commitment32(sha.ComputeHash(stream.ToArray()));
                }
            }
        }

        private static void WriteField(Stream stream, string value)
        {
            byte[] bytes = StrictUtf8.GetBytes(value ?? string.Empty);
            stream.WriteByte((byte)(bytes.Length >> 24));
            stream.WriteByte((byte)(bytes.Length >> 16));
            stream.WriteByte((byte)(bytes.Length >> 8));
            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        internal static bool IsCanonicalCustomizationId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaximumCustomizationIdLength ||
                value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }

            for (int index = 1; index < value.Length; index++)
            {
                char character = value[index];
                bool allowed = character >= 'a' && character <= 'z' ||
                               character >= '0' && character <= '9' ||
                               character == '_';
                if (!allowed || character == '_' && value[index - 1] == '_')
                {
                    return false;
                }
            }

            return value[value.Length - 1] != '_';
        }

        private static string BuildInstanceId(string prefix, string sessionId)
        {
            if (!IsCanonicalSessionId(sessionId))
            {
                throw new ArgumentException("Invalid Game Test session ID.", nameof(sessionId));
            }

            return prefix + sessionId.Substring(0, 16);
        }

        private static bool IsCanonicalSessionId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 32)
            {
                return false;
            }

            int nonzero = 0;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!(character >= '0' && character <= '9') &&
                    !(character >= 'a' && character <= 'f'))
                {
                    return false;
                }

                nonzero |= character == '0' ? 0 : 1;
            }

            return nonzero != 0;
        }

        private static FirstUserGameTestAdapterResult Reject(
            FirstUserGameTestAdapterFailure failure,
            DevelopmentAuthorityFailure authorityFailure = DevelopmentAuthorityFailure.None,
            FirstUserRoutePlan routePlan = default,
            FirstUserGameTestSelection selection = null,
            VerifiedDevelopmentReceipt receipt = null,
            VerifiedDevelopmentProjection projection = null)
        {
            return new FirstUserGameTestAdapterResult(
                FirstUserGameTestAdapterStatus.Rejected,
                failure,
                authorityFailure,
                routePlan,
                selection,
                receipt,
                projection);
        }
    }
}
