using System;
using System.Collections.Generic;
using System.Globalization;
using AL.Core.Interfaces;
using AL.Core.Interfaces.Relationships;

namespace AL.Services.Relationships
{
    public sealed class RelationshipMutationPlanner
    {
        private readonly IRelationshipIdentityResolver _identities;
        private readonly IRelationshipPolicyResolver _policies;

        public RelationshipMutationPlanner(
            IRelationshipIdentityResolver identities,
            IRelationshipPolicyResolver policies)
        {
            _identities = identities;
            _policies = policies;
        }

        public RelationshipPlanningResult Plan(
            RelationshipMutationRequest request,
            RelationshipSnapshot snapshot)
        {
            if (request == null)
            {
                return Reject(
                    RelationshipPreparationStatus.RejectedInvalidDelta,
                    RelationshipDomain.NpcAffinity,
                    RelationshipDiagnosticCodes.Apply,
                    string.Empty,
                    "Mutation request is null.");
            }

            if (string.IsNullOrEmpty(request.CorrelationId) ||
                string.IsNullOrEmpty(request.OperationId) ||
                string.IsNullOrEmpty(request.SourceSystemId))
            {
                return Reject(
                    RelationshipPreparationStatus.RejectedCorrelationRequired,
                    request.Domain,
                    RelationshipDiagnosticCodes.Correlation,
                    request.TargetId,
                    "Correlation, operation, and source identities are required.");
            }

            if (snapshot == null ||
                snapshot.NpcAffinityDomain.Status ==
                RelationshipDomainValidationStatus.UnavailableNoCurrentSave)
            {
                return Reject(
                    RelationshipPreparationStatus.RejectedNoCurrentSave,
                    request.Domain,
                    RelationshipDiagnosticCodes.NoCurrentSave,
                    request.TargetId,
                    "Snapshot is missing.");
            }

            if (!snapshot.ProfileWritable)
            {
                return Reject(
                    RelationshipPreparationStatus.RejectedReadOnlyProfile,
                    request.Domain,
                    RelationshipDiagnosticCodes.ProfileReadOnly,
                    request.TargetId,
                    "Profile is read-only.");
            }

            if (_policies == null ||
                !_policies.PolicyValidation.IsValid ||
                _policies.Availability != RelationshipCatalogAvailability.Available)
            {
                RelationshipPreparationStatus status =
                    _identities != null &&
                    _identities.Availability == RelationshipCatalogAvailability.Pending
                        ? RelationshipPreparationStatus.RejectedPolicyUnavailable
                        : RelationshipPreparationStatus.RejectedPolicyUnavailable;
                if (_policies != null &&
                    _policies.ResolvePolicy() != null &&
                    _policies.ResolvePolicy().SchemaVersion !=
                    RelationshipTechnicalLimits.CurrentSchemaVersion)
                {
                    status = RelationshipPreparationStatus.UnsupportedVersion;
                }

                return Reject(
                    status,
                    request.Domain,
                    RelationshipDiagnosticCodes.Policy,
                    request.TargetId,
                    "Policy catalog is not available as production authority.");
            }

            switch (request.Domain)
            {
                case RelationshipDomain.NpcAffinity:
                    return PlanAffinity(request, snapshot);
                case RelationshipDomain.FactionReputation:
                    return PlanFaction(request, snapshot);
                case RelationshipDomain.PersonaTrait:
                    return PlanPersona(request, snapshot);
                default:
                    return Reject(
                        RelationshipPreparationStatus.RejectedInvalidDelta,
                        request.Domain,
                        RelationshipDiagnosticCodes.Apply,
                        request.TargetId,
                        "Unknown relationship domain.");
            }
        }

        private RelationshipPlanningResult PlanAffinity(
            RelationshipMutationRequest request,
            RelationshipSnapshot snapshot)
        {
            if (!snapshot.NpcAffinityDomain.IsMutationReady)
            {
                return Reject(
                    snapshot.NpcAffinityDomain.Status ==
                    RelationshipDomainValidationStatus.UnavailableNoCurrentSave
                        ? RelationshipPreparationStatus.RejectedNoCurrentSave
                        : RelationshipPreparationStatus.RejectedMalformedDomain,
                    request.Domain,
                    RelationshipDiagnosticCodes.Apply,
                    request.TargetId,
                    "Affinity domain is not mutation-ready.");
            }

            RelationshipIdentityResolution resolution = _identities.ResolveNpc(request.TargetId);
            if (!resolution.SupportsMutation)
            {
                return Reject(
                    MapIdentityRejection(resolution.Status),
                    request.Domain,
                    RelationshipDiagnosticCodes.UnknownId,
                    request.TargetId,
                    "NPC identity is unsupported for mutation.");
            }

            if (!string.IsNullOrEmpty(request.ExpectedSnapshotRevision) &&
                !string.Equals(
                    request.ExpectedSnapshotRevision,
                    snapshot.NpcAffinityDomain.Fingerprint,
                    StringComparison.Ordinal))
            {
                return Reject(
                    RelationshipPreparationStatus.RejectedStaleSnapshot,
                    request.Domain,
                    RelationshipDiagnosticCodes.StalePlan,
                    resolution.CanonicalId,
                    "Affinity plan is stale.");
            }

            float current = 0f;
            RelationshipRowOperation rowOperation = RelationshipRowOperation.Create;
            if (snapshot.NpcAffinityDomain.SupportedValuesByCanonicalNpcId.TryGetValue(
                    resolution.CanonicalId,
                    out float stored))
            {
                current = stored;
                rowOperation = RelationshipRowOperation.Update;
            }

            if (!IsFinite(current) ||
                current < RelationshipTechnicalLimits.AffinityMinimum ||
                current > RelationshipTechnicalLimits.AffinityMaximum)
            {
                return Reject(
                    RelationshipPreparationStatus.RejectedMalformedDomain,
                    request.Domain,
                    RelationshipDiagnosticCodes.OutOfRange,
                    resolution.CanonicalId,
                    "Current affinity is not a valid finite in-range value.");
            }

            if (!IsFinite(request.Delta))
            {
                return Reject(
                    RelationshipPreparationStatus.RejectedInvalidDelta,
                    request.Domain,
                    RelationshipDiagnosticCodes.NonFinite,
                    resolution.CanonicalId,
                    "Affinity delta is not finite.");
            }

            if (request.Delta == 0d)
            {
                return Prepared(
                    RelationshipPreparationStatus.NoChange,
                    request,
                    resolution.CanonicalId,
                    null,
                    0d,
                    current,
                    current,
                    0d,
                    false,
                    RelationshipRowOperation.None,
                    snapshot.NpcAffinityDomain.Fingerprint);
            }

            double raw = current + request.Delta;
            if (!IsFinite(raw))
            {
                return Reject(
                    RelationshipPreparationStatus.RejectedInvalidDelta,
                    request.Domain,
                    RelationshipDiagnosticCodes.NonFinite,
                    resolution.CanonicalId,
                    "Affinity addition is non-finite.");
            }

            double clamped = Math.Min(
                RelationshipTechnicalLimits.AffinityMaximum,
                Math.Max(RelationshipTechnicalLimits.AffinityMinimum, raw));
            float next = (float)clamped;
            if (!IsFinite(next))
            {
                return Reject(
                    RelationshipPreparationStatus.RejectedInvalidDelta,
                    request.Domain,
                    RelationshipDiagnosticCodes.NonFinite,
                    resolution.CanonicalId,
                    "Clamped affinity is non-finite.");
            }

            double applied = next - current;
            bool wasClamped = applied != request.Delta;
            return Prepared(
                wasClamped
                    ? RelationshipPreparationStatus.PreparedClamped
                    : RelationshipPreparationStatus.Prepared,
                request,
                resolution.CanonicalId,
                null,
                request.Delta,
                current,
                next,
                applied,
                wasClamped,
                rowOperation,
                snapshot.NpcAffinityDomain.Fingerprint);
        }

        private RelationshipPlanningResult PlanFaction(
            RelationshipMutationRequest request,
            RelationshipSnapshot snapshot)
        {
            if (!snapshot.FactionDomain.IsMutationReady)
            {
                return Reject(
                    snapshot.FactionDomain.Status ==
                    RelationshipDomainValidationStatus.UnavailableNoCurrentSave
                        ? RelationshipPreparationStatus.RejectedNoCurrentSave
                        : RelationshipPreparationStatus.RejectedMalformedDomain,
                    request.Domain,
                    RelationshipDiagnosticCodes.Apply,
                    request.TargetId,
                    "Faction domain is not mutation-ready.");
            }

            RelationshipIdentityResolution resolution =
                _identities.ResolveFaction(request.TargetId);
            if (!resolution.SupportsMutation)
            {
                return Reject(
                    MapIdentityRejection(resolution.Status),
                    request.Domain,
                    RelationshipDiagnosticCodes.UnknownId,
                    request.TargetId,
                    "Faction identity is unsupported for mutation.");
            }

            if (!string.IsNullOrEmpty(request.ExpectedSnapshotRevision) &&
                !string.Equals(
                    request.ExpectedSnapshotRevision,
                    snapshot.FactionDomain.Fingerprint,
                    StringComparison.Ordinal))
            {
                return Reject(
                    RelationshipPreparationStatus.RejectedStaleSnapshot,
                    request.Domain,
                    RelationshipDiagnosticCodes.StalePlan,
                    resolution.CanonicalId,
                    "Faction plan is stale.");
            }

            int current = 0;
            RelationshipRowOperation rowOperation = RelationshipRowOperation.Create;
            if (snapshot.FactionDomain.SupportedValuesByCanonicalFactionId.TryGetValue(
                    resolution.CanonicalId,
                    out int stored))
            {
                current = stored;
                rowOperation = RelationshipRowOperation.Update;
            }

            if (!IsWholeInteger(request.Delta))
            {
                return Reject(
                    RelationshipPreparationStatus.RejectedInvalidDelta,
                    request.Domain,
                    RelationshipDiagnosticCodes.NonFinite,
                    resolution.CanonicalId,
                    "Faction delta must be a signed integer.");
            }

            int delta = Convert.ToInt32(request.Delta, CultureInfo.InvariantCulture);
            if (delta == 0)
            {
                return Prepared(
                    RelationshipPreparationStatus.NoChange,
                    request,
                    resolution.CanonicalId,
                    null,
                    0d,
                    current,
                    current,
                    0d,
                    false,
                    RelationshipRowOperation.None,
                    snapshot.FactionDomain.Fingerprint);
            }

            try
            {
                int next = checked(current + delta);
                return Prepared(
                    RelationshipPreparationStatus.Prepared,
                    request,
                    resolution.CanonicalId,
                    null,
                    delta,
                    current,
                    next,
                    delta,
                    false,
                    rowOperation,
                    snapshot.FactionDomain.Fingerprint);
            }
            catch (OverflowException)
            {
                return Reject(
                    RelationshipPreparationStatus.RejectedOverflow,
                    request.Domain,
                    RelationshipDiagnosticCodes.Overflow,
                    resolution.CanonicalId,
                    "Faction addition overflows Int32.");
            }
        }

        private RelationshipPlanningResult PlanPersona(
            RelationshipMutationRequest request,
            RelationshipSnapshot snapshot)
        {
            if (!request.PersonaTrait.HasValue ||
                !Enum.IsDefined(typeof(PersonaTrait), request.PersonaTrait.Value))
            {
                return Reject(
                    RelationshipPreparationStatus.RejectedInvalidTrait,
                    request.Domain,
                    RelationshipDiagnosticCodes.Apply,
                    string.Empty,
                    "Persona trait is undefined.");
            }

            if (!snapshot.PersonaDomain.IsMutationReady ||
                !snapshot.PersonaDomain.Values.IsPresent)
            {
                return Reject(
                    snapshot.PersonaDomain.Status ==
                    RelationshipDomainValidationStatus.UnavailableNoCurrentSave
                        ? RelationshipPreparationStatus.RejectedNoCurrentSave
                        : snapshot.PersonaDomain.Status ==
                          RelationshipDomainValidationStatus.CompatibleNormalizedTopLevel
                            ? RelationshipPreparationStatus.RejectedMalformedDomain
                            : RelationshipPreparationStatus.RejectedMalformedDomain,
                    request.Domain,
                    RelationshipDiagnosticCodes.Apply,
                    request.PersonaTrait.Value.ToString(),
                    "Persona domain is not mutation-ready.");
            }

            if (!string.IsNullOrEmpty(request.ExpectedSnapshotRevision) &&
                !string.Equals(
                    request.ExpectedSnapshotRevision,
                    snapshot.PersonaDomain.Fingerprint,
                    StringComparison.Ordinal))
            {
                return Reject(
                    RelationshipPreparationStatus.RejectedStaleSnapshot,
                    request.Domain,
                    RelationshipDiagnosticCodes.StalePlan,
                    request.PersonaTrait.Value.ToString(),
                    "Persona plan is stale.");
            }

            int current = snapshot.PersonaDomain.Values.Get(request.PersonaTrait.Value);
            if (!IsWholeInteger(request.Delta))
            {
                return Reject(
                    RelationshipPreparationStatus.RejectedInvalidDelta,
                    request.Domain,
                    RelationshipDiagnosticCodes.NonFinite,
                    request.PersonaTrait.Value.ToString(),
                    "Persona delta must be a signed integer.");
            }

            int delta = Convert.ToInt32(request.Delta, CultureInfo.InvariantCulture);
            if (delta == 0)
            {
                return Prepared(
                    RelationshipPreparationStatus.NoChange,
                    request,
                    string.Empty,
                    request.PersonaTrait,
                    0d,
                    current,
                    current,
                    0d,
                    false,
                    RelationshipRowOperation.None,
                    snapshot.PersonaDomain.Fingerprint);
            }

            try
            {
                int next = checked(current + delta);
                return Prepared(
                    RelationshipPreparationStatus.Prepared,
                    request,
                    string.Empty,
                    request.PersonaTrait,
                    delta,
                    current,
                    next,
                    delta,
                    false,
                    RelationshipRowOperation.Update,
                    snapshot.PersonaDomain.Fingerprint);
            }
            catch (OverflowException)
            {
                return Reject(
                    RelationshipPreparationStatus.RejectedOverflow,
                    request.Domain,
                    RelationshipDiagnosticCodes.Overflow,
                    request.PersonaTrait.Value.ToString(),
                    "Persona addition overflows Int32.");
            }
        }

        private static RelationshipPreparationStatus MapIdentityRejection(
            RelationshipIdentityStatus status)
        {
            switch (status)
            {
                case RelationshipIdentityStatus.CatalogPending:
                case RelationshipIdentityStatus.CatalogUnavailable:
                case RelationshipIdentityStatus.InvalidRecord:
                    return RelationshipPreparationStatus.RejectedPolicyUnavailable;
                case RelationshipIdentityStatus.UnsupportedVersion:
                    return RelationshipPreparationStatus.UnsupportedVersion;
                default:
                    return RelationshipPreparationStatus.RejectedUnknownId;
            }
        }

        private RelationshipPlanningResult Prepared(
            RelationshipPreparationStatus status,
            RelationshipMutationRequest request,
            string canonicalId,
            PersonaTrait? trait,
            double requestedDelta,
            double previous,
            double next,
            double applied,
            bool wasClamped,
            RelationshipRowOperation rowOperation,
            string domainFingerprint)
        {
            var plan = new RelationshipPreparedPlan(
                RelationshipHash.Compute(
                    request.OperationId,
                    request.CorrelationId,
                    request.Domain.ToString(),
                    canonicalId,
                    trait?.ToString() ?? string.Empty,
                    requestedDelta.ToString("R", CultureInfo.InvariantCulture)),
                status,
                request.Domain,
                canonicalId,
                trait,
                requestedDelta,
                previous,
                next,
                applied,
                wasClamped,
                rowOperation,
                domainFingerprint,
                _policies.ResolvePolicy().PolicyRevision,
                request.CorrelationId,
                request.OperationId,
                request.SourceSystemId,
                Array.Empty<RelationshipDiagnostic>());
            return new RelationshipPlanningResult(
                status,
                plan,
                Array.Empty<RelationshipDiagnostic>());
        }

        private static RelationshipPlanningResult Reject(
            RelationshipPreparationStatus status,
            RelationshipDomain domain,
            string code,
            string targetId,
            string action)
        {
            var diagnostic = new RelationshipDiagnostic(
                RelationshipDiagnosticSeverity.Error,
                code,
                domain,
                string.Empty,
                targetId ?? string.Empty,
                string.Empty,
                string.Empty,
                action,
                true);
            return new RelationshipPlanningResult(
                status,
                null,
                new[] { diagnostic });
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsWholeInteger(double value)
        {
            if (!IsFinite(value))
            {
                return false;
            }

            if (value < int.MinValue || value > int.MaxValue)
            {
                return false;
            }

            return Math.Abs(value - Math.Truncate(value)) < double.Epsilon;
        }
    }
}
