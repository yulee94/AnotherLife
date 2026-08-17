using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace AL.Core.Relationships
{
    public enum RelationshipTransactionStatus
    {
        Committed,
        AlreadyCommitted,
        RejectedValidation,
        RejectedStale,
        RejectedIdempotencyConflict
    }

    public sealed class RelationshipTransaction
    {
        public RelationshipTransaction(
            string transactionId,
            string correlationId,
            IEnumerable<RelationshipMutationPlan> operations)
        {
            TransactionId = transactionId;
            CorrelationId = correlationId;
            Operations = Array.AsReadOnly((operations ?? Enumerable.Empty<RelationshipMutationPlan>()).ToArray());
            SemanticFingerprint = ComputeSemanticFingerprint();
        }

        public string TransactionId { get; }
        public string CorrelationId { get; }
        public IReadOnlyList<RelationshipMutationPlan> Operations { get; }
        public string SemanticFingerprint { get; }

        private string ComputeSemanticFingerprint()
        {
            return RelationshipSnapshotBuilder.Fingerprint(
                "relationship-transaction-v1",
                new[] { Encode(TransactionId), Encode(CorrelationId) }
                    .Concat(Operations.Select(Describe)));
        }

        private static string Describe(RelationshipMutationPlan plan)
        {
            if (plan == null) return Encode(null);
            return string.Concat(new[]
            {
                Encode(plan.Status.ToString()),
                Encode(plan.Domain.ToString()),
                Encode(plan.CanonicalTargetId),
                Encode(plan.RequestedDelta.ToString("R", CultureInfo.InvariantCulture)),
                Encode(plan.PreviousValue.ToString("R", CultureInfo.InvariantCulture)),
                Encode(plan.NewValue.ToString("R", CultureInfo.InvariantCulture)),
                Encode(plan.AppliedDelta.ToString("R", CultureInfo.InvariantCulture)),
                Encode(plan.RowOperation.ToString()),
                Encode(plan.ExpectedSnapshotRevision),
                Encode(plan.PolicyRevision),
                Encode(plan.OperationId),
                Encode(plan.CorrelationId)
            });
        }

        private static string Encode(string value)
        {
            string normalized = value ?? string.Empty;
            return normalized.Length.ToString(CultureInfo.InvariantCulture) + ":" + normalized;
        }
    }

    public sealed class RelationshipCommittedChange
    {
        internal RelationshipCommittedChange(RelationshipMutationPlan plan)
        {
            Domain = plan.Domain;
            CanonicalTargetId = plan.CanonicalTargetId;
            PreviousValue = plan.PreviousValue;
            NewValue = plan.NewValue;
            AppliedDelta = plan.AppliedDelta;
            OperationId = plan.OperationId;
            CorrelationId = plan.CorrelationId;
        }

        public RelationshipDomain Domain { get; }
        public string CanonicalTargetId { get; }
        public double PreviousValue { get; }
        public double NewValue { get; }
        public double AppliedDelta { get; }
        public string OperationId { get; }
        public string CorrelationId { get; }
    }

    public sealed class RelationshipTransactionReceipt
    {
        internal RelationshipTransactionReceipt(
            string transactionId,
            string correlationId,
            string semanticFingerprint,
            IEnumerable<RelationshipCommittedChange> changes,
            string commitRevision)
        {
            TransactionId = transactionId;
            CorrelationId = correlationId;
            SemanticFingerprint = semanticFingerprint;
            Changes = Array.AsReadOnly(changes.ToArray());
            CommitRevision = commitRevision;
        }

        public string TransactionId { get; }
        public string CorrelationId { get; }
        public string SemanticFingerprint { get; }
        public IReadOnlyList<RelationshipCommittedChange> Changes { get; }
        public string CommitRevision { get; }
    }

    public sealed class RelationshipTransactionState
    {
        private readonly IReadOnlyDictionary<string, RelationshipTransactionReceipt> receipts;

        private RelationshipTransactionState(
            RelationshipNumericSnapshot affinity,
            RelationshipNumericSnapshot faction,
            IDictionary<string, RelationshipTransactionReceipt> receipts)
        {
            Affinity = affinity;
            Faction = faction;
            this.receipts = new ReadOnlyDictionary<string, RelationshipTransactionReceipt>(
                new Dictionary<string, RelationshipTransactionReceipt>(receipts, StringComparer.Ordinal));
        }

        public RelationshipNumericSnapshot Affinity { get; }
        public RelationshipNumericSnapshot Faction { get; }
        public IReadOnlyDictionary<string, RelationshipTransactionReceipt> Receipts => receipts;

        public static RelationshipTransactionState Create(
            RelationshipNumericSnapshot affinity,
            RelationshipNumericSnapshot faction)
        {
            return Restore(affinity, faction, null);
        }

        public static RelationshipTransactionState Restore(
            RelationshipNumericSnapshot affinity,
            RelationshipNumericSnapshot faction,
            IEnumerable<RelationshipTransactionReceipt> committedReceipts)
        {
            if (affinity == null) throw new ArgumentNullException(nameof(affinity));
            if (faction == null) throw new ArgumentNullException(nameof(faction));
            if (affinity.Domain != RelationshipDomain.NpcAffinity)
                throw new ArgumentException("Affinity snapshot has the wrong domain.", nameof(affinity));
            if (faction.Domain != RelationshipDomain.FactionReputation)
                throw new ArgumentException("Faction snapshot has the wrong domain.", nameof(faction));
            var receipts = new Dictionary<string, RelationshipTransactionReceipt>(StringComparer.Ordinal);
            foreach (RelationshipTransactionReceipt receipt in
                     committedReceipts ?? Enumerable.Empty<RelationshipTransactionReceipt>())
            {
                if (receipt == null || string.IsNullOrEmpty(receipt.TransactionId) ||
                    receipts.ContainsKey(receipt.TransactionId))
                {
                    throw new ArgumentException("Committed receipts must have unique transaction IDs.", nameof(committedReceipts));
                }
                receipts.Add(receipt.TransactionId, receipt);
            }
            return new RelationshipTransactionState(affinity, faction, receipts);
        }

        internal static RelationshipTransactionState CreateCommitted(
            RelationshipNumericSnapshot affinity,
            RelationshipNumericSnapshot faction,
            IDictionary<string, RelationshipTransactionReceipt> receipts)
        {
            return new RelationshipTransactionState(affinity, faction, receipts);
        }
    }

    public sealed class RelationshipTransactionResult
    {
        internal RelationshipTransactionResult(
            RelationshipTransactionStatus status,
            RelationshipTransactionState state,
            RelationshipTransactionReceipt receipt)
        {
            Status = status;
            State = state;
            Receipt = receipt;
        }

        public RelationshipTransactionStatus Status { get; }
        public RelationshipTransactionState State { get; }
        public RelationshipTransactionReceipt Receipt { get; }
    }

    /// <summary>
    /// Applies prepared relationship plans to isolated dictionaries and publishes a new
    /// immutable state only after every ordered operation validates and applies. The
    /// returned state and receipt are suitable for inclusion in a larger save candidate;
    /// this engine never persists or mutates the supplied state.
    /// </summary>
    public static class RelationshipTransactionEngine
    {
        public static RelationshipTransactionResult Commit(
            IRelationshipIdentityResolver resolver,
            RelationshipTransactionState current,
            RelationshipTransaction transaction)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));

            RelationshipTransactionReceipt prior;
            if (transaction != null && !string.IsNullOrEmpty(transaction.TransactionId) &&
                current.Receipts.TryGetValue(transaction.TransactionId, out prior))
            {
                RelationshipTransactionStatus duplicateStatus =
                    string.Equals(prior.SemanticFingerprint, transaction.SemanticFingerprint, StringComparison.Ordinal) &&
                    string.Equals(prior.CorrelationId, transaction.CorrelationId, StringComparison.Ordinal)
                        ? RelationshipTransactionStatus.AlreadyCommitted
                        : RelationshipTransactionStatus.RejectedIdempotencyConflict;
                return new RelationshipTransactionResult(
                    duplicateStatus,
                    current,
                    duplicateStatus == RelationshipTransactionStatus.AlreadyCommitted ? prior : null);
            }

            if (!IsStructurallyValid(resolver, current, transaction))
            {
                return Rejected(RelationshipTransactionStatus.RejectedValidation, current);
            }

            var affinity = new SortedDictionary<string, double>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, double> pair in current.Affinity.Values)
            {
                affinity.Add(pair.Key, pair.Value);
            }
            var faction = new SortedDictionary<string, double>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, double> pair in current.Faction.Values)
            {
                faction.Add(pair.Key, pair.Value);
            }
            RelationshipNumericSnapshot affinitySnapshot = current.Affinity;
            RelationshipNumericSnapshot factionSnapshot = current.Faction;
            var changes = new List<RelationshipCommittedChange>();

            foreach (RelationshipMutationPlan plan in transaction.Operations)
            {
                RelationshipNumericSnapshot domainSnapshot = plan.Domain == RelationshipDomain.NpcAffinity
                    ? affinitySnapshot
                    : factionSnapshot;
                if (!string.Equals(plan.ExpectedSnapshotRevision, domainSnapshot.SnapshotRevision, StringComparison.Ordinal) ||
                    !string.Equals(plan.PolicyRevision, resolver.PolicyRevision, StringComparison.Ordinal))
                {
                    return Rejected(RelationshipTransactionStatus.RejectedStale, current);
                }

                double actualPrevious;
                bool exists = domainSnapshot.Values.TryGetValue(plan.CanonicalTargetId, out actualPrevious);
                RelationshipRowOperation expectedOperation = exists
                    ? RelationshipRowOperation.Update
                    : RelationshipRowOperation.Create;
                if (actualPrevious != plan.PreviousValue ||
                    (plan.Status != RelationshipPlanStatus.NoChange && plan.RowOperation != expectedOperation))
                {
                    return Rejected(RelationshipTransactionStatus.RejectedStale, current);
                }

                if (plan.Status == RelationshipPlanStatus.NoChange)
                {
                    continue;
                }

                SortedDictionary<string, double> values = plan.Domain == RelationshipDomain.NpcAffinity
                    ? affinity
                    : faction;
                values[plan.CanonicalTargetId] = plan.NewValue;
                changes.Add(new RelationshipCommittedChange(plan));

                if (plan.Domain == RelationshipDomain.NpcAffinity)
                {
                    affinitySnapshot = Rebuild(resolver, RelationshipDomain.NpcAffinity, affinity, current.Affinity);
                    if (!RebuildPreserved(affinitySnapshot, affinity, current.Affinity))
                        return Rejected(RelationshipTransactionStatus.RejectedValidation, current);
                }
                else
                {
                    factionSnapshot = Rebuild(resolver, RelationshipDomain.FactionReputation, faction, current.Faction);
                    if (!RebuildPreserved(factionSnapshot, faction, current.Faction))
                        return Rejected(RelationshipTransactionStatus.RejectedValidation, current);
                }
            }

            string commitRevision = RelationshipSnapshotBuilder.Fingerprint(
                "relationship-commit-v1",
                new[]
                {
                    transaction.SemanticFingerprint,
                    affinitySnapshot.SnapshotRevision,
                    factionSnapshot.SnapshotRevision
                });
            var receipt = new RelationshipTransactionReceipt(
                transaction.TransactionId,
                transaction.CorrelationId,
                transaction.SemanticFingerprint,
                changes,
                commitRevision);
            var receipts = new Dictionary<string, RelationshipTransactionReceipt>(
                current.Receipts,
                StringComparer.Ordinal)
            {
                [transaction.TransactionId] = receipt
            };
            RelationshipTransactionState committed = RelationshipTransactionState.CreateCommitted(
                affinitySnapshot,
                factionSnapshot,
                receipts);
            return new RelationshipTransactionResult(
                RelationshipTransactionStatus.Committed,
                committed,
                receipt);
        }

        private static bool IsStructurallyValid(
            IRelationshipIdentityResolver resolver,
            RelationshipTransactionState current,
            RelationshipTransaction transaction)
        {
            if (resolver == null || string.IsNullOrEmpty(resolver.PolicyRevision) || transaction == null ||
                string.IsNullOrEmpty(transaction.TransactionId) ||
                string.IsNullOrEmpty(transaction.CorrelationId) ||
                transaction.Operations.Count == 0 ||
                !current.Affinity.CanPlan || !current.Faction.CanPlan ||
                !string.Equals(current.Affinity.PolicyRevision, resolver.PolicyRevision, StringComparison.Ordinal) ||
                !string.Equals(current.Faction.PolicyRevision, resolver.PolicyRevision, StringComparison.Ordinal))
            {
                return false;
            }

            var operationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RelationshipMutationPlan plan in transaction.Operations)
            {
                if (plan == null ||
                    (plan.Status != RelationshipPlanStatus.Prepared &&
                     plan.Status != RelationshipPlanStatus.PreparedClamped &&
                     plan.Status != RelationshipPlanStatus.NoChange) ||
                    (plan.Domain != RelationshipDomain.NpcAffinity &&
                     plan.Domain != RelationshipDomain.FactionReputation) ||
                    string.IsNullOrEmpty(plan.OperationId) ||
                    !operationIds.Add(plan.OperationId) ||
                    !string.Equals(plan.CorrelationId, transaction.CorrelationId, StringComparison.Ordinal) ||
                    !string.Equals(plan.PolicyRevision, resolver.PolicyRevision, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static RelationshipNumericSnapshot Rebuild(
            IRelationshipIdentityResolver resolver,
            RelationshipDomain domain,
            IEnumerable<KeyValuePair<string, double>> values,
            RelationshipNumericSnapshot previous)
        {
            IEnumerable<RelationshipNumericRow> rows = values
                .Select(pair => new RelationshipNumericRow(pair.Key, pair.Value))
                .Concat(previous.PreservedUnknownIds.Select(id => new RelationshipNumericRow(id, 0)));
            return domain == RelationshipDomain.NpcAffinity
                ? RelationshipSnapshotBuilder.BuildNpcAffinity(resolver, rows, previous.ProfileWritable)
                : RelationshipSnapshotBuilder.BuildFactionReputation(resolver, rows, previous.ProfileWritable);
        }

        private static bool RebuildPreserved(
            RelationshipNumericSnapshot rebuilt,
            IReadOnlyDictionary<string, double> expectedValues,
            RelationshipNumericSnapshot previous)
        {
            return rebuilt != null && rebuilt.CanPlan &&
                rebuilt.Values.Count == expectedValues.Count &&
                rebuilt.PreservedUnknownIds.SequenceEqual(previous.PreservedUnknownIds);
        }

        private static RelationshipTransactionResult Rejected(
            RelationshipTransactionStatus status,
            RelationshipTransactionState current)
        {
            return new RelationshipTransactionResult(status, current, null);
        }
    }
}
