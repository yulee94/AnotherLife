using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core.Interfaces;
using AL.Core.Interfaces.Relationships;

namespace AL.Services.Relationships
{
    public sealed class InMemoryRelationshipOperationLedger : IRelationshipOperationLedger
    {
        private readonly Dictionary<string, RelationshipPreparedPlan> _byOperation =
            new Dictionary<string, RelationshipPreparedPlan>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _operationByCorrelation =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public bool TryGet(string operationId, out RelationshipPreparedPlan appliedPlan)
        {
            return _byOperation.TryGetValue(operationId ?? string.Empty, out appliedPlan);
        }

        public bool TryFindCorrelation(string correlationId, out string operationId)
        {
            return _operationByCorrelation.TryGetValue(
                correlationId ?? string.Empty,
                out operationId);
        }

        public bool TryRecord(RelationshipPreparedPlan plan, out string conflictCorrelationId)
        {
            conflictCorrelationId = null;
            if (plan == null || string.IsNullOrEmpty(plan.OperationId))
            {
                return false;
            }

            if (_operationByCorrelation.TryGetValue(plan.CorrelationId, out string existingOp) &&
                !string.Equals(existingOp, plan.OperationId, StringComparison.Ordinal))
            {
                conflictCorrelationId = plan.CorrelationId;
                return false;
            }

            _byOperation[plan.OperationId] = plan;
            if (!string.IsNullOrEmpty(plan.CorrelationId))
            {
                _operationByCorrelation[plan.CorrelationId] = plan.OperationId;
            }

            return true;
        }
    }

    public sealed class InMemoryRelationshipMutationTarget : IRelationshipMutationTarget
    {
        public InMemoryRelationshipMutationTarget(RelationshipRawState raw)
        {
            CurrentRawState = raw ?? RelationshipRawState.NoSave();
        }

        public RelationshipRawState CurrentRawState { get; private set; }

        public RelationshipSnapshot GetCurrentSnapshot(
            IRelationshipIdentityResolver identities,
            IRelationshipPolicyResolver policies)
        {
            return RelationshipSnapshotBuilder.Build(CurrentRawState, identities, policies);
        }

        public RelationshipApplyResult Apply(
            RelationshipPreparedPlan plan,
            IRelationshipIdentityResolver identities,
            IRelationshipPolicyResolver policies,
            IRelationshipOperationLedger ledger)
        {
            RelationshipSnapshot before = GetCurrentSnapshot(identities, policies);
            if (plan == null || !plan.CanApply)
            {
                return Fail(
                    RelationshipApplyStatus.RejectedTargetInvalid,
                    plan,
                    before,
                    RelationshipDiagnosticCodes.Apply,
                    "Plan is missing or not applicable.");
            }

            if (!CurrentRawState.HasCurrentSave)
            {
                return Fail(
                    RelationshipApplyStatus.RejectedTargetInvalid,
                    plan,
                    before,
                    RelationshipDiagnosticCodes.NoCurrentSave,
                    "Mutation target has no current save.");
            }

            if (!CurrentRawState.ProfileWritable)
            {
                return Fail(
                    RelationshipApplyStatus.RejectedTargetReadOnly,
                    plan,
                    before,
                    RelationshipDiagnosticCodes.ProfileReadOnly,
                    "Mutation target is read-only.");
            }

            if (policies == null ||
                !string.Equals(
                    policies.ResolvePolicy()?.PolicyRevision,
                    plan.PolicyRevision,
                    StringComparison.Ordinal))
            {
                return Fail(
                    RelationshipApplyStatus.RejectedStalePlan,
                    plan,
                    before,
                    RelationshipDiagnosticCodes.StalePlan,
                    "Policy revision changed.");
            }

            if (ledger != null &&
                ledger.TryGet(plan.OperationId, out RelationshipPreparedPlan existing))
            {
                bool samePayload =
                    existing.Domain == plan.Domain &&
                    string.Equals(
                        existing.CanonicalTargetId,
                        plan.CanonicalTargetId,
                        StringComparison.Ordinal) &&
                    existing.PersonaTrait == plan.PersonaTrait &&
                    existing.RequestedDelta == plan.RequestedDelta &&
                    existing.NewValue == plan.NewValue;
                if (samePayload)
                {
                    return new RelationshipApplyResult(
                        RelationshipApplyStatus.RejectedAlreadyApplied,
                        existing,
                        before,
                        Array.Empty<RelationshipDiagnostic>());
                }

                return Fail(
                    RelationshipApplyStatus.RejectedApplyFailure,
                    plan,
                    before,
                    RelationshipDiagnosticCodes.Apply,
                    "Operation payload collides with a prior apply.");
            }

            if (ledger != null &&
                ledger.TryFindCorrelation(plan.CorrelationId, out string otherOperation) &&
                !string.Equals(otherOperation, plan.OperationId, StringComparison.Ordinal))
            {
                return Fail(
                    RelationshipApplyStatus.RejectedCorrelationConflict,
                    plan,
                    before,
                    RelationshipDiagnosticCodes.Correlation,
                    "Correlation is already bound to another operation.");
            }

            string currentFingerprint = Fingerprint(plan.Domain, before);
            if (!string.Equals(
                    currentFingerprint,
                    plan.ExpectedSnapshotRevision,
                    StringComparison.Ordinal))
            {
                return Fail(
                    RelationshipApplyStatus.RejectedStalePlan,
                    plan,
                    before,
                    RelationshipDiagnosticCodes.StalePlan,
                    "Domain fingerprint does not match the plan.");
            }

            if (!CurrentValueMatches(plan, before))
            {
                return Fail(
                    RelationshipApplyStatus.RejectedStalePlan,
                    plan,
                    before,
                    RelationshipDiagnosticCodes.StalePlan,
                    "Current target value no longer matches the plan.");
            }

            if (plan.Status == RelationshipPreparationStatus.NoChange)
            {
                return new RelationshipApplyResult(
                    RelationshipApplyStatus.NoChange,
                    plan,
                    before,
                    Array.Empty<RelationshipDiagnostic>());
            }

            RelationshipRawState next = ApplyToRaw(plan, CurrentRawState, identities);
            if (next == null)
            {
                return Fail(
                    RelationshipApplyStatus.RejectedApplyFailure,
                    plan,
                    before,
                    RelationshipDiagnosticCodes.Apply,
                    "Apply could not mutate the isolated candidate.");
            }

            CurrentRawState = next;
            if (ledger != null &&
                !ledger.TryRecord(plan, out string conflict) &&
                !string.IsNullOrEmpty(conflict))
            {
                CurrentRawState = before == null
                    ? CurrentRawState
                    : ReconstructRaw(before, CurrentRawState.ProfileWritable);
                return Fail(
                    RelationshipApplyStatus.RejectedCorrelationConflict,
                    plan,
                    GetCurrentSnapshot(identities, policies),
                    RelationshipDiagnosticCodes.Correlation,
                    "Ledger rejected the correlation.");
            }

            return new RelationshipApplyResult(
                RelationshipApplyStatus.Applied,
                plan,
                GetCurrentSnapshot(identities, policies),
                Array.Empty<RelationshipDiagnostic>());
        }

        private static string Fingerprint(
            RelationshipDomain domain,
            RelationshipSnapshot snapshot)
        {
            switch (domain)
            {
                case RelationshipDomain.NpcAffinity:
                    return snapshot.NpcAffinityDomain.Fingerprint;
                case RelationshipDomain.FactionReputation:
                    return snapshot.FactionDomain.Fingerprint;
                default:
                    return snapshot.PersonaDomain.Fingerprint;
            }
        }

        private static bool CurrentValueMatches(
            RelationshipPreparedPlan plan,
            RelationshipSnapshot snapshot)
        {
            switch (plan.Domain)
            {
                case RelationshipDomain.NpcAffinity:
                    snapshot.NpcAffinityDomain.SupportedValuesByCanonicalNpcId.TryGetValue(
                        plan.CanonicalTargetId,
                        out float affinity);
                    return affinity == (float)plan.PreviousValue;
                case RelationshipDomain.FactionReputation:
                    snapshot.FactionDomain.SupportedValuesByCanonicalFactionId.TryGetValue(
                        plan.CanonicalTargetId,
                        out int reputation);
                    return reputation == (int)plan.PreviousValue;
                default:
                    if (!plan.PersonaTrait.HasValue || !snapshot.PersonaDomain.Values.IsPresent)
                    {
                        return false;
                    }

                    return snapshot.PersonaDomain.Values.Get(plan.PersonaTrait.Value) ==
                           (int)plan.PreviousValue;
            }
        }

        private static RelationshipRawState ApplyToRaw(
            RelationshipPreparedPlan plan,
            RelationshipRawState current,
            IRelationshipIdentityResolver identities)
        {
            switch (plan.Domain)
            {
                case RelationshipDomain.NpcAffinity:
                    var npcRows = current.NpcAffinityRows.ToList();
                    bool npcUpdated = false;
                    for (int i = 0; i < npcRows.Count; i++)
                    {
                        RelationshipNpcAffinityRow row = npcRows[i];
                        if (row == null || row.IsNullEntry)
                        {
                            continue;
                        }

                        RelationshipIdentityResolution resolved = identities.ResolveNpc(row.NpcId);
                        if (string.Equals(
                                resolved.CanonicalId,
                                plan.CanonicalTargetId,
                                StringComparison.Ordinal))
                        {
                            npcRows[i] = RelationshipNpcAffinityRow.Value(
                                row.NpcId,
                                (float)plan.NewValue);
                            npcUpdated = true;
                            break;
                        }
                    }

                    if (!npcUpdated)
                    {
                        if (plan.RowOperation != RelationshipRowOperation.Create)
                        {
                            return null;
                        }

                        npcRows.Add(
                            RelationshipNpcAffinityRow.Value(
                                plan.CanonicalTargetId,
                                (float)plan.NewValue));
                    }

                    return current.WithNpcRows(npcRows);

                case RelationshipDomain.FactionReputation:
                    var factionRows = current.FactionRows.ToList();
                    bool factionUpdated = false;
                    for (int i = 0; i < factionRows.Count; i++)
                    {
                        RelationshipFactionRow row = factionRows[i];
                        if (row == null || row.IsNullEntry)
                        {
                            continue;
                        }

                        RelationshipIdentityResolution resolved =
                            identities.ResolveFaction(row.FactionId);
                        if (string.Equals(
                                resolved.CanonicalId,
                                plan.CanonicalTargetId,
                                StringComparison.Ordinal))
                        {
                            factionRows[i] = RelationshipFactionRow.Value(
                                row.FactionId,
                                (int)plan.NewValue);
                            factionUpdated = true;
                            break;
                        }
                    }

                    if (!factionUpdated)
                    {
                        if (plan.RowOperation != RelationshipRowOperation.Create)
                        {
                            return null;
                        }

                        factionRows.Add(
                            RelationshipFactionRow.Value(
                                plan.CanonicalTargetId,
                                (int)plan.NewValue));
                    }

                    return current.WithFactionRows(factionRows);

                default:
                    if (!plan.PersonaTrait.HasValue || !current.Persona.IsPresent)
                    {
                        return null;
                    }

                    return current.WithPersona(
                        current.Persona.With(plan.PersonaTrait.Value, (int)plan.NewValue));
            }
        }

        private static RelationshipRawState ReconstructRaw(
            RelationshipSnapshot snapshot,
            bool writable)
        {
            return snapshot == null
                ? RelationshipRawState.NoSave()
                : new RelationshipRawState(
                    true,
                    writable,
                    false,
                    false,
                    !snapshot.PersonaDomain.Values.IsPresent,
                    snapshot.NpcAffinityDomain.SupportedValuesByCanonicalNpcId.Select(
                        pair => RelationshipNpcAffinityRow.Value(pair.Key, pair.Value)),
                    snapshot.FactionDomain.SupportedValuesByCanonicalFactionId.Select(
                        pair => RelationshipFactionRow.Value(pair.Key, pair.Value)),
                    snapshot.PersonaDomain.Values);
        }

        private static RelationshipApplyResult Fail(
            RelationshipApplyStatus status,
            RelationshipPreparedPlan plan,
            RelationshipSnapshot snapshot,
            string code,
            string action)
        {
            return new RelationshipApplyResult(
                status,
                plan,
                snapshot,
                new[]
                {
                    new RelationshipDiagnostic(
                        RelationshipDiagnosticSeverity.Error,
                        code,
                        plan?.Domain,
                        string.Empty,
                        plan?.CanonicalTargetId ?? string.Empty,
                        string.Empty,
                        string.Empty,
                        action,
                        true)
                });
        }
    }
}
