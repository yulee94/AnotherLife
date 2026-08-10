using System;
using System.Collections.Generic;

namespace AL.Core.SaveAuthority
{
    public static class ProfileIdentityMigrationPlanner
    {
        public static ProfileIdentityMigrationPlan Plan(
            ProfileIdentityMigrationRequest request)
        {
            if (request == null)
            {
                return Rejected(
                    ProfileIdentityMigrationDiagnosticCodes.RequestMissing,
                    0);
            }

            if (!TryClassifyEligibleEvidence(
                    request.Evidence,
                    out ProfileIdentityMigrationMode mode,
                    out string evidenceDiagnostic))
            {
                return Rejected(evidenceDiagnostic, 0);
            }

            var reserved = new HashSet<string>(StringComparer.Ordinal);
            int identityCount = 0;
            if (!TryCaptureIdentities(
                    request.Evidence.RecognizedProfileIds,
                    reserved,
                    ref identityCount))
            {
                return Rejected(
                    ProfileIdentityMigrationDiagnosticCodes.IdentitySetInvalid,
                    0);
            }

            if (identityCount != 0)
            {
                return Rejected(
                    ProfileIdentityMigrationDiagnosticCodes.EvidenceInvalid,
                    0);
            }

            if (!TryCaptureIdentities(
                    request.RetainedSessionProfileIds,
                    reserved,
                    ref identityCount))
            {
                return Rejected(
                    ProfileIdentityMigrationDiagnosticCodes.IdentitySetInvalid,
                    0);
            }

            if (request.IdentityCandidateSource == null)
            {
                return Rejected(
                    ProfileIdentityMigrationDiagnosticCodes
                        .IdentitySourceMissing,
                    0);
            }

            if (request.ProjectionSource == null)
            {
                return Rejected(
                    ProfileIdentityMigrationDiagnosticCodes
                        .ProjectionSourceMissing,
                    0);
            }

            string selectedProfileId = string.Empty;
            int attemptCount = 0;
            for (int attempt = 1;
                 attempt <= ProfileIdentityMigrationTechnicalLimits
                     .MaximumIdentityAttempts;
                 attempt++)
            {
                attemptCount = attempt;
                string candidate;
                try
                {
                    candidate = request.IdentityCandidateSource.GetCandidate(
                        attempt);
                }
                catch
                {
                    return Rejected(
                        ProfileIdentityMigrationDiagnosticCodes
                            .IdentitySourceThrew,
                        attemptCount);
                }

                if (!SaveAuthorityValidation.IsCanonicalProfileId(candidate))
                {
                    return Rejected(
                        ProfileIdentityMigrationDiagnosticCodes
                            .IdentityCandidateInvalid,
                        attemptCount);
                }

                if (reserved.Contains(candidate))
                    continue;

                selectedProfileId = candidate;
                break;
            }

            if (string.IsNullOrEmpty(selectedProfileId))
            {
                return Rejected(
                    ProfileIdentityMigrationDiagnosticCodes.IdentityExhausted,
                    attemptCount);
            }

            ProfileIdentityMigrationProjection projection;
            try
            {
                projection = request.ProjectionSource.CreateProjection(
                    mode,
                    selectedProfileId,
                    request.Evidence);
            }
            catch
            {
                return Rejected(
                    ProfileIdentityMigrationDiagnosticCodes
                        .ProjectionSourceThrew,
                    attemptCount);
            }

            if (!IsProjectionValid(
                    mode,
                    selectedProfileId,
                    request.Evidence,
                    projection))
            {
                return Rejected(
                    ProfileIdentityMigrationDiagnosticCodes.ProjectionInvalid,
                    attemptCount);
            }

            return new ProfileIdentityMigrationPlan(
                mode == ProfileIdentityMigrationMode.CreateNewProfile
                    ? ProfileIdentityMigrationPlanStatus.CreationReady
                    : ProfileIdentityMigrationPlanStatus.MigrationReady,
                mode,
                selectedProfileId,
                attemptCount,
                projection.Predecessor,
                projection.Candidate,
                projection.Witness,
                Array.Empty<string>());
        }

        private static bool TryClassifyEligibleEvidence(
            ProfileIdentityMigrationEvidence evidence,
            out ProfileIdentityMigrationMode mode,
            out string diagnostic)
        {
            mode = ProfileIdentityMigrationMode.None;
            diagnostic =
                ProfileIdentityMigrationDiagnosticCodes.EvidenceInvalid;
            if (evidence == null ||
                !Enum.IsDefined(
                    typeof(ProfileIdentityMigrationEvidenceState),
                    evidence.State))
            {
                return false;
            }

            switch (evidence.State)
            {
                case ProfileIdentityMigrationEvidenceState.AllMissing:
                    if (evidence.HasSelectedSourceGeneration ||
                        evidence.SelectedSourceGeneration !=
                            ProfileAuthoritySourceGeneration.None ||
                        !IsMissingGeneration(evidence.SelectedPredecessor))
                    {
                        return false;
                    }

                    mode = ProfileIdentityMigrationMode.CreateNewProfile;
                    return true;

                case ProfileIdentityMigrationEvidenceState.CoherentSchemaOne:
                    if (!evidence.HasSelectedSourceGeneration ||
                        !IsSupportedSource(
                            evidence.SelectedSourceGeneration) ||
                        !IsLegacyPredecessor(
                            evidence.SelectedPredecessor,
                            evidence.SelectedSourceGeneration))
                    {
                        return false;
                    }

                    mode = ProfileIdentityMigrationMode.MigrateSchemaOne;
                    return true;

                default:
                    diagnostic = ProfileIdentityMigrationDiagnosticCodes
                        .EvidenceUnsupported;
                    return false;
            }
        }

        private static bool TryCaptureIdentities(
            IEnumerable<string> identities,
            HashSet<string> reserved,
            ref int count)
        {
            if (identities == null || reserved == null)
                return false;

            try
            {
                foreach (string identity in identities)
                {
                    count++;
                    if (count > ProfileIdentityMigrationTechnicalLimits
                            .MaximumRetainedIdentityCount ||
                        !SaveAuthorityValidation.IsCanonicalProfileId(
                            identity) ||
                        !reserved.Add(identity))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsProjectionValid(
            ProfileIdentityMigrationMode mode,
            string selectedProfileId,
            ProfileIdentityMigrationEvidence evidence,
            ProfileIdentityMigrationProjection projection)
        {
            if (projection == null ||
                !IsCurrentCandidate(
                    projection.Candidate,
                    selectedProfileId))
            {
                return false;
            }

            switch (mode)
            {
                case ProfileIdentityMigrationMode.CreateNewProfile:
                    return IsMissingGeneration(projection.Predecessor) &&
                           IsMissingWitness(projection.Witness);

                case ProfileIdentityMigrationMode.MigrateSchemaOne:
                    return IsSameGeneration(
                               evidence.SelectedPredecessor,
                               projection.Predecessor) &&
                           IsMigrationWitness(
                               projection.Witness,
                               evidence.SelectedSourceGeneration,
                               projection.Predecessor,
                               projection.Candidate,
                               selectedProfileId);

                default:
                    return false;
            }
        }

        private static bool IsCurrentCandidate(
            ProfileIdentityGenerationExpectation candidate,
            string expectedProfileId) =>
            IsExactGeneration(candidate) &&
            candidate.SourceGeneration ==
                ProfileAuthoritySourceGeneration.Primary &&
            string.Equals(
                candidate.ProfileId,
                expectedProfileId,
                StringComparison.Ordinal) &&
            candidate.SaveSchemaVersion ==
                SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion &&
            candidate.ProfileInitializationVersion ==
                SaveAuthorityTechnicalLimits
                    .IdentityAwareProfileInitializationVersion;

        private static bool IsLegacyPredecessor(
            ProfileIdentityGenerationExpectation predecessor,
            ProfileAuthoritySourceGeneration expectedSource) =>
            IsExactGeneration(predecessor) &&
            predecessor.SourceGeneration == expectedSource &&
            string.Equals(
                predecessor.ProfileId,
                string.Empty,
                StringComparison.Ordinal) &&
            predecessor.SaveSchemaVersion ==
                SaveAuthorityTechnicalLimits.LegacySaveSchemaVersion &&
            predecessor.ProfileInitializationVersion ==
                SaveAuthorityTechnicalLimits
                    .LegacyProfileInitializationVersion;

        private static bool IsExactGeneration(
            ProfileIdentityGenerationExpectation generation) =>
            generation != null &&
            generation.IsPresent &&
            IsSupportedSource(generation.SourceGeneration) &&
            generation.ByteCount > 0 &&
            generation.ByteCount <=
                ProfileIdentityMigrationTechnicalLimits
                    .MaximumArtifactByteCount &&
            SaveAuthorityValidation.IsCanonicalSha256(generation.Sha256) &&
            generation.ProfileId != null &&
            generation.SaveSchemaVersion > 0 &&
            generation.ProfileInitializationVersion > 0;

        private static bool IsMissingGeneration(
            ProfileIdentityGenerationExpectation generation) =>
            generation != null &&
            !generation.IsPresent &&
            generation.SourceGeneration ==
                ProfileAuthoritySourceGeneration.None &&
            generation.ByteCount == 0 &&
            string.Equals(
                generation.Sha256,
                string.Empty,
                StringComparison.Ordinal) &&
            string.Equals(
                generation.ProfileId,
                string.Empty,
                StringComparison.Ordinal) &&
            generation.SaveSchemaVersion == 0 &&
            generation.ProfileInitializationVersion == 0;

        private static bool IsMigrationWitness(
            ProfileIdentityMigrationWitnessExpectation witness,
            ProfileAuthoritySourceGeneration expectedSource,
            ProfileIdentityGenerationExpectation predecessor,
            ProfileIdentityGenerationExpectation candidate,
            string expectedProfileId) =>
            witness != null &&
            witness.IsPresent &&
            string.Equals(
                witness.ContractVersion,
                ProfileIdentityMigrationTechnicalLimits.ContractVersion,
                StringComparison.Ordinal) &&
            SaveAuthorityValidation.IsBoundedOpaqueIdentity(
                witness.OperationId) &&
            witness.SelectedLegacySourceGeneration == expectedSource &&
            witness.PredecessorByteCount == predecessor.ByteCount &&
            string.Equals(
                witness.PredecessorSha256,
                predecessor.Sha256,
                StringComparison.Ordinal) &&
            string.Equals(
                witness.ProfileId,
                expectedProfileId,
                StringComparison.Ordinal) &&
            witness.TargetSaveSchemaVersion ==
                SaveAuthorityTechnicalLimits.IdentityAwareSaveSchemaVersion &&
            witness.TargetProfileInitializationVersion ==
                SaveAuthorityTechnicalLimits
                    .IdentityAwareProfileInitializationVersion &&
            witness.CandidateByteCount == candidate.ByteCount &&
            string.Equals(
                witness.CandidateSha256,
                candidate.Sha256,
                StringComparison.Ordinal) &&
            witness.ByteCount > 0 &&
            witness.ByteCount <=
                ProfileIdentityMigrationTechnicalLimits
                    .MaximumArtifactByteCount &&
            SaveAuthorityValidation.IsCanonicalSha256(witness.Sha256);

        private static bool IsMissingWitness(
            ProfileIdentityMigrationWitnessExpectation witness) =>
            witness != null &&
            !witness.IsPresent &&
            string.Equals(
                witness.ContractVersion,
                string.Empty,
                StringComparison.Ordinal) &&
            string.Equals(
                witness.OperationId,
                string.Empty,
                StringComparison.Ordinal) &&
            witness.SelectedLegacySourceGeneration ==
                ProfileAuthoritySourceGeneration.None &&
            witness.PredecessorByteCount == 0 &&
            string.Equals(
                witness.PredecessorSha256,
                string.Empty,
                StringComparison.Ordinal) &&
            string.Equals(
                witness.ProfileId,
                string.Empty,
                StringComparison.Ordinal) &&
            witness.TargetSaveSchemaVersion == 0 &&
            witness.TargetProfileInitializationVersion == 0 &&
            witness.CandidateByteCount == 0 &&
            string.Equals(
                witness.CandidateSha256,
                string.Empty,
                StringComparison.Ordinal) &&
            witness.ByteCount == 0 &&
            string.Equals(
                witness.Sha256,
                string.Empty,
                StringComparison.Ordinal);

        private static bool IsSameGeneration(
            ProfileIdentityGenerationExpectation expected,
            ProfileIdentityGenerationExpectation actual) =>
            expected != null &&
            actual != null &&
            expected.IsPresent == actual.IsPresent &&
            expected.SourceGeneration == actual.SourceGeneration &&
            expected.ByteCount == actual.ByteCount &&
            string.Equals(
                expected.Sha256,
                actual.Sha256,
                StringComparison.Ordinal) &&
            string.Equals(
                expected.ProfileId,
                actual.ProfileId,
                StringComparison.Ordinal) &&
            expected.SaveSchemaVersion == actual.SaveSchemaVersion &&
            expected.ProfileInitializationVersion ==
                actual.ProfileInitializationVersion;

        private static bool IsSupportedSource(
            ProfileAuthoritySourceGeneration source) =>
            source == ProfileAuthoritySourceGeneration.Primary ||
            source == ProfileAuthoritySourceGeneration.Backup ||
            source == ProfileAuthoritySourceGeneration.Previous ||
            source == ProfileAuthoritySourceGeneration.Temp;

        private static ProfileIdentityMigrationPlan Rejected(
            string diagnosticCode,
            int identityAttemptCount) =>
            new ProfileIdentityMigrationPlan(
                ProfileIdentityMigrationPlanStatus.Rejected,
                ProfileIdentityMigrationMode.None,
                string.Empty,
                identityAttemptCount,
                ProfileIdentityGenerationExpectation.Missing(),
                ProfileIdentityGenerationExpectation.Missing(),
                ProfileIdentityMigrationWitnessExpectation.Missing(),
                new[] { diagnosticCode });
    }
}
