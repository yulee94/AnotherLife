using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Core.Relationships;
using AL.Data.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Relationships
{
    public sealed class RelationshipPersistenceRecoveryTests
    {
        private const string CatalogRelativePath =
            "AL/StreamingAssets/GameData/al_relationship_authority_content_catalog.json";

        [Test]
        public void LegacyLabelsMigrateAndCommittedCompositionReloadsAsOneState()
        {
            var harness = NewHarness(new SaveGameData
            {
                Reputation = new List<NpcAffinityData>
                {
                    new NpcAffinityData { NpcId = "Captain Valerius", Affinity = 2 },
                    new NpcAffinityData { NpcId = "npc_future", Affinity = 77 }
                },
                FactionReputations = new List<FactionRepData>
                {
                    new FactionRepData { FactionId = "FACTION_VEIL_WATCH", Reputation = 4 }
                }
            });
            RelationshipPersistentSnapshot before = harness.Coordinator.Reload();
            RelationshipTransaction transaction = Compose(harness.Resolver, before.State, "legacy", 5, 6);

            RelationshipPersistenceResult committed = harness.Coordinator.Commit(transaction);
            RelationshipPersistentSnapshot reloaded = harness.Coordinator.Reload();

            Assert.AreEqual(RelationshipPersistenceStatus.Committed, committed.Status);
            Assert.AreEqual(7d, reloaded.State.Affinity.Values["npc_valerius"]);
            Assert.AreEqual(10d, reloaded.State.Faction.Values["faction_veil_watch"]);
            Assert.AreEqual(1, reloaded.State.Receipts.Count);
            Assert.AreEqual("npc_valerius", reloaded.Save.Reputation[0].NpcId);
            Assert.AreEqual(77f, reloaded.Save.Reputation.Single(row => row.NpcId == "npc_future").Affinity);
        }

        [Test]
        public void CurrentStableIdSaveReloadsWithoutIdentityDrift()
        {
            var harness = NewHarness(new SaveGameData
            {
                Reputation = new List<NpcAffinityData>
                {
                    new NpcAffinityData { NpcId = "npc_valerius", Affinity = 9 }
                },
                FactionReputations = new List<FactionRepData>
                {
                    new FactionRepData { FactionId = "faction_veil_watch", Reputation = -3 }
                }
            });

            RelationshipPersistentSnapshot reloaded = harness.Coordinator.Reload();

            Assert.AreEqual(9d, reloaded.State.Affinity.Values["npc_valerius"]);
            Assert.AreEqual(-3d, reloaded.State.Faction.Values["faction_veil_watch"]);
            Assert.AreEqual(0, reloaded.Migration.MigratedCount);
        }

        [Test]
        public void AtomicFileStoreSurvivesCoordinatorRestartWithReceiptIntact()
        {
            string directory = Path.Combine(
                Application.temporaryCachePath,
                "relationship-persistence-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "relationships.json");
            try
            {
                byte[] sourceBytes =
                    File.ReadAllBytes(Path.Combine(Application.dataPath, CatalogRelativePath));
                var resolver = new RelationshipCatalogResolver(sourceBytes);
                var migrator = new RelationshipLegacyIdentityMigrator(sourceBytes);
                var store = new FileRelationshipDocumentStore(path);
                store.WriteAtomically(Array.Empty<byte>(), RelationshipPersistenceCodec.SerializeLegacy(StableSave(2, 3)));
                var first = new RelationshipPersistenceCoordinator(resolver, migrator, store);
                RelationshipPersistentSnapshot before = first.Reload();
                RelationshipTransaction transaction = Compose(
                    resolver, before.State, "disk-restart", 5, 6);

                Assert.AreEqual(
                    RelationshipPersistenceStatus.Committed,
                    first.Commit(transaction).Status);

                var restarted = new RelationshipPersistenceCoordinator(
                    resolver,
                    new RelationshipLegacyIdentityMigrator(sourceBytes),
                    new FileRelationshipDocumentStore(path));
                RelationshipPersistentSnapshot reloaded = restarted.Reload();
                AssertComposition(reloaded, 7, 9, 1);
                Assert.AreEqual(
                    RelationshipPersistenceStatus.AlreadyCommitted,
                    restarted.Commit(transaction).Status);
                AssertComposition(restarted.Reload(), 7, 9, 1);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [TestCase(RelationshipPersistenceFaultPoint.BeforeValidation)]
        [TestCase(RelationshipPersistenceFaultPoint.DuringPersistence)]
        public void FaultBeforeDurablePublicationReloadsCompletePriorState(
            RelationshipPersistenceFaultPoint point)
        {
            using Harness harness = NewFileHarness(StableSave(1, 2));
            RelationshipPersistentSnapshot before = harness.Coordinator.Reload();
            harness.Faults.ThrowOnceAt = point;

            Assert.Throws<RelationshipPersistenceFaultException>(() =>
                harness.Coordinator.Commit(Compose(harness.Resolver, before.State, "fault-prior", 5, 6)));

            RelationshipPersistentSnapshot recovered = harness.Coordinator.Reload();
            AssertComposition(recovered, 1, 2, 0);
        }

        [Test]
        public void FaultAfterDurableWriteReloadsCompleteCommitAndRetryIsExactlyOnce()
        {
            using Harness harness = NewFileHarness(StableSave(1, 2));
            RelationshipPersistentSnapshot before = harness.Coordinator.Reload();
            RelationshipTransaction transaction = Compose(
                harness.Resolver, before.State, "fault-ack", 5, 6);
            harness.Faults.ThrowOnceAt =
                RelationshipPersistenceFaultPoint.AfterDurableWriteBeforeAcknowledgement;

            Assert.Throws<RelationshipPersistenceFaultException>(() =>
                harness.Coordinator.Commit(transaction));

            RelationshipPersistentSnapshot recovered = harness.Coordinator.Reload();
            AssertComposition(recovered, 6, 8, 1);
            RelationshipPersistenceResult retry = harness.Coordinator.Commit(transaction);
            Assert.AreEqual(RelationshipPersistenceStatus.AlreadyCommitted, retry.Status);
            AssertComposition(harness.Coordinator.Reload(), 6, 8, 1);
        }

        [Test]
        public void FaultDuringReloadPublishesNothingAndNextReloadIsComplete()
        {
            using Harness harness = NewFileHarness(StableSave(3, 4));
            harness.Faults.ThrowOnceAt = RelationshipPersistenceFaultPoint.DuringReload;

            Assert.Throws<RelationshipPersistenceFaultException>(() => harness.Coordinator.Reload());

            AssertComposition(harness.Coordinator.Reload(), 3, 4, 0);
        }

        [Test]
        public void UnsupportedEnvelopeFailsClosedWithoutReplacingBytes()
        {
            byte[] unsupported = System.Text.Encoding.UTF8.GetBytes(
                "{\"Version\":2,\"FuturePayload\":\"opaque\"}");
            byte[] sourceBytes =
                File.ReadAllBytes(Path.Combine(Application.dataPath, CatalogRelativePath));
            var store = new MemoryRelationshipDocumentStore(unsupported);
            var coordinator = new RelationshipPersistenceCoordinator(
                new RelationshipCatalogResolver(sourceBytes),
                new RelationshipLegacyIdentityMigrator(sourceBytes),
                store);

            Assert.Throws<InvalidDataException>(() => coordinator.Reload());
            CollectionAssert.AreEqual(unsupported, store.Read());
        }

        [Test]
        public void StoreCompareAndSwapRejectsStaleGeneration()
        {
            byte[] prior = RelationshipPersistenceCodec.SerializeLegacy(StableSave(1, 2));
            byte[] current = RelationshipPersistenceCodec.SerializeLegacy(StableSave(3, 4));
            byte[] staleCandidate = RelationshipPersistenceCodec.SerializeLegacy(StableSave(9, 9));
            var store = new MemoryRelationshipDocumentStore(prior);
            store.WriteAtomically(prior, current);

            Assert.Throws<InvalidOperationException>(() =>
                store.WriteAtomically(prior, staleCandidate));

            CollectionAssert.AreEqual(current, store.Read());
        }

        private static void AssertComposition(
            RelationshipPersistentSnapshot snapshot,
            double affinity,
            double faction,
            int receipts)
        {
            Assert.AreEqual(affinity, snapshot.State.Affinity.Values["npc_valerius"]);
            Assert.AreEqual(faction, snapshot.State.Faction.Values["faction_veil_watch"]);
            Assert.AreEqual(receipts, snapshot.State.Receipts.Count);
        }

        private static SaveGameData StableSave(float affinity, int faction) => new SaveGameData
        {
            Reputation = new List<NpcAffinityData>
            {
                new NpcAffinityData { NpcId = "npc_valerius", Affinity = affinity }
            },
            FactionReputations = new List<FactionRepData>
            {
                new FactionRepData { FactionId = "faction_veil_watch", Reputation = faction }
            }
        };

        private static RelationshipTransaction Compose(
            RelationshipCatalogResolver resolver,
            RelationshipTransactionState state,
            string id,
            double affinityDelta,
            int factionDelta)
        {
            const string correlation = "relationship-persistence-proof";
            return new RelationshipTransaction(id, correlation, new[]
            {
                RelationshipPlanner.PlanAffinity(
                    resolver, state.Affinity, "npc_valerius", affinityDelta,
                    id + ":affinity", correlation),
                RelationshipPlanner.PlanFaction(
                    resolver, state.Faction, "faction_veil_watch", factionDelta,
                    id + ":faction", correlation)
            });
        }

        private static Harness NewHarness(SaveGameData initial)
        {
            byte[] sourceBytes =
                File.ReadAllBytes(Path.Combine(Application.dataPath, CatalogRelativePath));
            var resolver = new RelationshipCatalogResolver(sourceBytes);
            var migrator = new RelationshipLegacyIdentityMigrator(sourceBytes);
            var store = new MemoryRelationshipDocumentStore(
                RelationshipPersistenceCodec.SerializeLegacy(initial));
            var faults = new ThrowOnceFaultInjector();
            return new Harness(
                resolver,
                faults,
                new RelationshipPersistenceCoordinator(resolver, migrator, store, faults));
        }

        private static Harness NewFileHarness(SaveGameData initial)
        {
            string directory = Path.Combine(
                Application.temporaryCachePath,
                "relationship-faults-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "relationships.json");
            byte[] sourceBytes =
                File.ReadAllBytes(Path.Combine(Application.dataPath, CatalogRelativePath));
            var resolver = new RelationshipCatalogResolver(sourceBytes);
            var migrator = new RelationshipLegacyIdentityMigrator(sourceBytes);
            var store = new FileRelationshipDocumentStore(path);
            store.WriteAtomically(
                Array.Empty<byte>(),
                RelationshipPersistenceCodec.SerializeLegacy(initial));
            var faults = new ThrowOnceFaultInjector();
            return new Harness(
                resolver,
                faults,
                new RelationshipPersistenceCoordinator(resolver, migrator, store, faults),
                directory);
        }

        private sealed class Harness : IDisposable
        {
            public Harness(
                RelationshipCatalogResolver resolver,
                ThrowOnceFaultInjector faults,
                RelationshipPersistenceCoordinator coordinator,
                string cleanupDirectory = null)
            {
                Resolver = resolver;
                Faults = faults;
                Coordinator = coordinator;
                CleanupDirectory = cleanupDirectory;
            }

            public RelationshipCatalogResolver Resolver { get; }
            public ThrowOnceFaultInjector Faults { get; }
            public RelationshipPersistenceCoordinator Coordinator { get; }
            private string CleanupDirectory { get; }

            public void Dispose()
            {
                if (!string.IsNullOrEmpty(CleanupDirectory) && Directory.Exists(CleanupDirectory))
                    Directory.Delete(CleanupDirectory, true);
            }
        }

        private sealed class ThrowOnceFaultInjector : IRelationshipPersistenceFaultInjector
        {
            public RelationshipPersistenceFaultPoint? ThrowOnceAt { get; set; }

            public void Hit(RelationshipPersistenceFaultPoint point)
            {
                if (ThrowOnceAt != point) return;
                ThrowOnceAt = null;
                throw new RelationshipPersistenceFaultException(point);
            }
        }
    }
}
