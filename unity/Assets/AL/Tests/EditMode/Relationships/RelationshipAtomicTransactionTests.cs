using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Core.Relationships;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Relationships
{
    public sealed class RelationshipAtomicTransactionTests
    {
        private const string CatalogRelativePath =
            "AL/StreamingAssets/GameData/al_relationship_authority_content_catalog.json";

        [Test]
        public void MultipleOrderedOperationsCommitAsOneImmutableState()
        {
            RelationshipCatalogResolver resolver = NewResolver();
            RelationshipTransactionState initial = NewState(resolver);
            RelationshipMutationPlan affinity = RelationshipPlanner.PlanAffinity(
                resolver, initial.Affinity, "NPC_VALERIUS", 5, "affinity-op", "report-correlation");
            RelationshipMutationPlan faction = RelationshipPlanner.PlanFaction(
                resolver, initial.Faction, "FACTION_VEIL_WATCH", 12, "faction-op", "report-correlation");
            var transaction = new RelationshipTransaction(
                "report-transaction", "report-correlation", new[] { affinity, faction });

            RelationshipTransactionResult result = RelationshipTransactionEngine.Commit(
                resolver, initial, transaction);

            Assert.AreEqual(RelationshipTransactionStatus.Committed, result.Status);
            Assert.AreEqual(0d, initial.Affinity.Values["npc_valerius"]);
            Assert.AreEqual(5d, result.State.Affinity.Values["npc_valerius"]);
            Assert.AreEqual(12d, result.State.Faction.Values["faction_veil_watch"]);
            CollectionAssert.AreEqual(
                new[] { "affinity-op", "faction-op" },
                result.Receipt.Changes.Select(change => change.OperationId));
            Assert.Throws<NotSupportedException>(() =>
                ((IDictionary<string, double>)result.State.Affinity.Values)["npc_valerius"] = 99);
        }

        [Test]
        public void SequentialPlansForOneTargetComposeInDeclaredOrder()
        {
            RelationshipCatalogResolver resolver = NewResolver();
            RelationshipTransactionState initial = NewState(resolver);
            RelationshipMutationPlan first = RelationshipPlanner.PlanAffinity(
                resolver, initial.Affinity, "npc_valerius", 5, "first", "correlation");
            RelationshipNumericSnapshot afterFirst = RelationshipSnapshotBuilder.BuildNpcAffinity(
                resolver,
                new[]
                {
                    new RelationshipNumericRow("npc_valerius", 5),
                    new RelationshipNumericRow("npc_gruff", 2)
                },
                true);
            RelationshipMutationPlan second = RelationshipPlanner.PlanAffinity(
                resolver, afterFirst, "npc_valerius", 7, "second", "correlation");

            RelationshipTransactionResult result = RelationshipTransactionEngine.Commit(
                resolver,
                initial,
                new RelationshipTransaction("ordered", "correlation", new[] { first, second }));

            Assert.AreEqual(RelationshipTransactionStatus.Committed, result.Status);
            Assert.AreEqual(12d, result.State.Affinity.Values["npc_valerius"]);
            CollectionAssert.AreEqual(
                new[] { 5d, 12d }, result.Receipt.Changes.Select(change => change.NewValue));
        }

        [Test]
        public void CommitPreservesUnsupportedStableRowsWithoutMutatingThem()
        {
            RelationshipCatalogResolver resolver = NewResolver();
            RelationshipNumericSnapshot affinity = RelationshipSnapshotBuilder.BuildNpcAffinity(
                resolver,
                new[]
                {
                    new RelationshipNumericRow("npc_valerius", 0),
                    new RelationshipNumericRow("npc_future", 77)
                },
                true);
            RelationshipNumericSnapshot faction = RelationshipSnapshotBuilder.BuildFactionReputation(
                resolver, Array.Empty<RelationshipNumericRow>(), true);
            RelationshipTransactionState initial = RelationshipTransactionState.Create(affinity, faction);
            RelationshipTransaction transaction = SingleAffinityTransaction(
                resolver, initial, "preserve-unknown", "correlation", 5);

            RelationshipTransactionResult result = RelationshipTransactionEngine.Commit(
                resolver, initial, transaction);

            Assert.AreEqual(RelationshipTransactionStatus.Committed, result.Status);
            CollectionAssert.AreEqual(
                new[] { "npc_future" }, result.State.Affinity.PreservedUnknownIds);
            Assert.AreEqual(5d, result.State.Affinity.Values["npc_valerius"]);
        }

        [Test]
        public void ValidationFailureRollsBackEveryOperationAndReturnsOriginalState()
        {
            RelationshipCatalogResolver resolver = NewResolver();
            RelationshipTransactionState initial = NewState(resolver);
            RelationshipMutationPlan valid = RelationshipPlanner.PlanAffinity(
                resolver, initial.Affinity, "npc_valerius", 5, "valid", "correlation");
            RelationshipMutationPlan invalid = RelationshipPlanner.PlanFaction(
                resolver, initial.Faction, "unknown_faction", 1, "invalid", "correlation");

            RelationshipTransactionResult result = RelationshipTransactionEngine.Commit(
                resolver,
                initial,
                new RelationshipTransaction("invalid", "correlation", new[] { valid, invalid }));

            Assert.AreEqual(RelationshipTransactionStatus.RejectedValidation, result.Status);
            Assert.AreSame(initial, result.State);
            Assert.AreEqual(0d, initial.Affinity.Values["npc_valerius"]);
            Assert.IsNull(result.Receipt);
        }

        [Test]
        public void ApplyFailureAfterEarlierStageRollsBackCompletely()
        {
            RelationshipCatalogResolver resolver = NewResolver();
            RelationshipTransactionState initial = NewState(resolver);
            RelationshipMutationPlan valid = RelationshipPlanner.PlanAffinity(
                resolver, initial.Affinity, "npc_valerius", 5, "valid", "correlation");
            RelationshipMutationPlan staleSecond = RelationshipPlanner.PlanAffinity(
                resolver, initial.Affinity, "npc_valerius", 7, "stale", "correlation");

            RelationshipTransactionResult result = RelationshipTransactionEngine.Commit(
                resolver,
                initial,
                new RelationshipTransaction("rollback", "correlation", new[] { valid, staleSecond }));

            Assert.AreEqual(RelationshipTransactionStatus.RejectedStale, result.Status);
            Assert.AreSame(initial, result.State);
            Assert.AreEqual(0d, result.State.Affinity.Values["npc_valerius"]);
        }

        [Test]
        public void DuplicateSubmissionAndRetryReturnPriorReceiptWithoutReapplying()
        {
            RelationshipCatalogResolver resolver = NewResolver();
            RelationshipTransactionState initial = NewState(resolver);
            var transaction = new RelationshipTransaction(
                "retryable",
                "correlation",
                new[]
                {
                    RelationshipPlanner.PlanAffinity(
                        resolver, initial.Affinity, "NPC_VALERIUS", 5, "approved-valerius-five", "correlation")
                });
            RelationshipTransactionResult committed = RelationshipTransactionEngine.Commit(
                resolver, initial, transaction);

            RelationshipTransactionResult retry = RelationshipTransactionEngine.Commit(
                resolver, committed.State, transaction);

            Assert.AreEqual(RelationshipTransactionStatus.AlreadyCommitted, retry.Status);
            Assert.AreSame(committed.State, retry.State);
            Assert.AreSame(committed.Receipt, retry.Receipt);
            Assert.AreEqual(5d, retry.State.Affinity.Values["npc_valerius"]);
        }

        [Test]
        public void RetryAfterReceiptReloadReturnsPriorOutcomeWithoutReapplying()
        {
            RelationshipCatalogResolver resolver = NewResolver();
            RelationshipTransactionState initial = NewState(resolver);
            RelationshipTransaction transaction = SingleAffinityTransaction(
                resolver, initial, "reload-retry", "reload-correlation", 5);
            RelationshipTransactionResult committed = RelationshipTransactionEngine.Commit(
                resolver, initial, transaction);
            RelationshipTransactionState reloaded = RelationshipTransactionState.Restore(
                committed.State.Affinity,
                committed.State.Faction,
                committed.State.Receipts.Values);

            RelationshipTransactionResult retry = RelationshipTransactionEngine.Commit(
                resolver, reloaded, transaction);

            Assert.AreEqual(RelationshipTransactionStatus.AlreadyCommitted, retry.Status);
            Assert.AreSame(reloaded, retry.State);
            Assert.AreSame(committed.Receipt, retry.Receipt);
            Assert.AreEqual(5d, retry.State.Affinity.Values["npc_valerius"]);
        }

        [Test]
        public void ReusedTransactionIdentityWithDifferentPayloadRejectsConflict()
        {
            RelationshipCatalogResolver resolver = NewResolver();
            RelationshipTransactionState initial = NewState(resolver);
            RelationshipTransaction first = SingleAffinityTransaction(
                resolver, initial, "same-id", "same-correlation", 5);
            RelationshipTransactionResult committed = RelationshipTransactionEngine.Commit(
                resolver, initial, first);
            RelationshipNumericSnapshot current = committed.State.Affinity;
            var conflicting = new RelationshipTransaction(
                "same-id",
                "same-correlation",
                new[]
                {
                    RelationshipPlanner.PlanAffinity(
                        resolver, current, "npc_valerius", 6, "affinity-op", "same-correlation")
                });

            RelationshipTransactionResult result = RelationshipTransactionEngine.Commit(
                resolver, committed.State, conflicting);

            Assert.AreEqual(RelationshipTransactionStatus.RejectedIdempotencyConflict, result.Status);
            Assert.AreSame(committed.State, result.State);
            Assert.AreEqual(5d, result.State.Affinity.Values["npc_valerius"]);
        }

        [Test]
        public void ConcurrentCommitRejectsTransactionPreparedFromStaleSnapshot()
        {
            RelationshipCatalogResolver resolver = NewResolver();
            RelationshipTransactionState initial = NewState(resolver);
            RelationshipTransaction first = SingleAffinityTransaction(
                resolver, initial, "first-tx", "first-correlation", 3);
            RelationshipTransaction stale = SingleAffinityTransaction(
                resolver, initial, "stale-tx", "stale-correlation", 9);
            RelationshipTransactionResult committed = RelationshipTransactionEngine.Commit(
                resolver, initial, first);

            RelationshipTransactionResult conflict = RelationshipTransactionEngine.Commit(
                resolver, committed.State, stale);

            Assert.AreEqual(RelationshipTransactionStatus.RejectedStale, conflict.Status);
            Assert.AreSame(committed.State, conflict.State);
            Assert.AreEqual(3d, conflict.State.Affinity.Values["npc_valerius"]);
        }

        [Test]
        public void OperationOrderIsPartOfIdempotencyIdentity()
        {
            RelationshipCatalogResolver resolver = NewResolver();
            RelationshipTransactionState initial = NewState(resolver);
            RelationshipMutationPlan affinity = RelationshipPlanner.PlanAffinity(
                resolver, initial.Affinity, "npc_valerius", 5, "affinity", "correlation");
            RelationshipMutationPlan faction = RelationshipPlanner.PlanFaction(
                resolver, initial.Faction, "faction_veil_watch", 1, "faction", "correlation");
            var first = new RelationshipTransaction(
                "order-sensitive", "correlation", new[] { affinity, faction });
            RelationshipTransactionResult committed = RelationshipTransactionEngine.Commit(
                resolver, initial, first);
            var reversed = new RelationshipTransaction(
                "order-sensitive", "correlation", new[] { faction, affinity });

            RelationshipTransactionResult result = RelationshipTransactionEngine.Commit(
                resolver, committed.State, reversed);

            Assert.AreEqual(RelationshipTransactionStatus.RejectedIdempotencyConflict, result.Status);
            Assert.AreSame(committed.State, result.State);
        }

        private static RelationshipTransaction SingleAffinityTransaction(
            RelationshipCatalogResolver resolver,
            RelationshipTransactionState state,
            string transactionId,
            string correlationId,
            double delta)
        {
            return new RelationshipTransaction(
                transactionId,
                correlationId,
                new[]
                {
                    RelationshipPlanner.PlanAffinity(
                        resolver, state.Affinity, "npc_valerius", delta, "affinity-op", correlationId)
                });
        }

        private static RelationshipTransactionState NewState(RelationshipCatalogResolver resolver)
        {
            RelationshipNumericSnapshot affinity = RelationshipSnapshotBuilder.BuildNpcAffinity(
                resolver,
                new[]
                {
                    new RelationshipNumericRow("npc_valerius", 0),
                    new RelationshipNumericRow("npc_gruff", 2)
                },
                true);
            RelationshipNumericSnapshot faction = RelationshipSnapshotBuilder.BuildFactionReputation(
                resolver,
                new[] { new RelationshipNumericRow("faction_veil_watch", 0) },
                true);
            return RelationshipTransactionState.Create(affinity, faction);
        }

        private static RelationshipCatalogResolver NewResolver()
        {
            return new RelationshipCatalogResolver(
                File.ReadAllBytes(Path.Combine(Application.dataPath, CatalogRelativePath)));
        }
    }
}
