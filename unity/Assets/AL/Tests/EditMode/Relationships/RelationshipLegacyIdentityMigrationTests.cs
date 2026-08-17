using System;
using System.IO;
using System.Linq;
using AL.Core.Relationships;
using AL.Data.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Relationships
{
    public sealed class RelationshipLegacyIdentityMigrationTests
    {
        private const string CatalogPath =
            "AL/StreamingAssets/GameData/al_relationship_authority_content_catalog.json";

        [TestCase(RelationshipDomain.NpcAffinity, "npc_valerius", "npc_valerius")]
        [TestCase(RelationshipDomain.NpcAffinity, "NPC_VALERIUS", "npc_valerius")]
        [TestCase(RelationshipDomain.NpcAffinity, "Captain Valerius", "npc_valerius")]
        [TestCase(RelationshipDomain.FactionReputation, "FACT_HUMAN_COUNCIL", "faction_crownlands_radiant_council")]
        [TestCase(RelationshipDomain.FactionReputation, "The Radiant Council", "faction_crownlands_radiant_council")]
        public void LegacyValuesMapDeterministicallyToStableNarrativeIds(
            RelationshipDomain domain,
            string legacyValue,
            string expectedId)
        {
            RelationshipIdentityMigrationResult result = NewMigrator().Migrate(domain, legacyValue);

            Assert.AreEqual(expectedId, result.CanonicalId);
            Assert.IsTrue(result.IsResolved);
            Assert.AreEqual(legacyValue, result.OriginalValue);
            CollectionAssert.Contains(
                new[]
                {
                    RelationshipIdentityMigrationStatus.Canonical,
                    RelationshipIdentityMigrationStatus.AliasMigrated,
                    RelationshipIdentityMigrationStatus.LegacyLabelMigrated
                },
                result.Status);
        }

        [Test]
        public void UnknownAmbiguousAndObsoleteLabelsArePreservedAndExplicit()
        {
            RelationshipLegacyIdentityMigrator migrator = NewMigrator();

            RelationshipIdentityMigrationResult unknown =
                migrator.Migrate(RelationshipDomain.NpcAffinity, "Future Friend");
            RelationshipIdentityMigrationResult ambiguous =
                migrator.Migrate(RelationshipDomain.FactionReputation, "Council");
            RelationshipIdentityMigrationResult obsolete =
                migrator.Migrate(RelationshipDomain.FactionReputation, "Human Council");

            Assert.AreEqual(RelationshipIdentityMigrationStatus.UnknownPreserved, unknown.Status);
            Assert.AreEqual(RelationshipIdentityMigrationStatus.AmbiguousPreserved, ambiguous.Status);
            Assert.AreEqual(RelationshipIdentityMigrationStatus.ObsoletePreserved, obsolete.Status);
            Assert.AreEqual("Future Friend", unknown.PreservedValue);
            Assert.AreEqual("Council", ambiguous.PreservedValue);
            Assert.AreEqual("Human Council", obsolete.PreservedValue);
            Assert.IsFalse(unknown.IsResolved || ambiguous.IsResolved || obsolete.IsResolved);
        }

        [Test]
        public void BlankValuesAreRejectedRatherThanGuessedOrTrimmed()
        {
            RelationshipLegacyIdentityMigrator migrator = NewMigrator();

            Assert.AreEqual(
                RelationshipIdentityMigrationStatus.Invalid,
                migrator.Migrate(RelationshipDomain.NpcAffinity, "").Status);
            Assert.AreEqual(
                RelationshipIdentityMigrationStatus.UnknownPreserved,
                migrator.Migrate(RelationshipDomain.NpcAffinity, " Captain Valerius").Status);
        }

        [Test]
        public void InvalidCatalogFailsClosedWithoutApplyingCompatibilityMappings()
        {
            var unavailable = new RelationshipLegacyIdentityMigrator(null);
            var corrupt = new RelationshipLegacyIdentityMigrator(new byte[] { 1, 2, 3 });

            Assert.AreEqual(
                RelationshipIdentityMigrationStatus.CatalogUnavailable,
                unavailable.Migrate(RelationshipDomain.NpcAffinity, "Captain Valerius").Status);
            Assert.AreEqual(
                RelationshipIdentityMigrationStatus.CatalogUnavailable,
                corrupt.Migrate(RelationshipDomain.FactionReputation, "The Radiant Council").Status);
        }

        [Test]
        public void PersistenceAdapterMigratesResolvedRowsAndPreservesUnresolvedRows()
        {
            var save = new SaveGameData
            {
                Reputation = new System.Collections.Generic.List<NpcAffinityData>
                {
                    new NpcAffinityData { NpcId = "Captain Valerius", Affinity = 12 },
                    new NpcAffinityData { NpcId = "Future Friend", Affinity = 4 }
                },
                FactionReputations = new System.Collections.Generic.List<FactionRepData>
                {
                    new FactionRepData { FactionId = "FACT_HUMAN_COUNCIL", Reputation = 20 },
                    new FactionRepData { FactionId = "Council", Reputation = 3 },
                    new FactionRepData { FactionId = "Human Council", Reputation = -2 }
                }
            };

            RelationshipPersistenceMigrationReport report =
                RelationshipPersistenceIdentityMigration.Apply(NewMigrator(), save);

            Assert.AreEqual("npc_valerius", save.Reputation[0].NpcId);
            Assert.AreEqual("Future Friend", save.Reputation[1].NpcId);
            Assert.AreEqual("faction_crownlands_radiant_council", save.FactionReputations[0].FactionId);
            Assert.AreEqual("Council", save.FactionReputations[1].FactionId);
            Assert.AreEqual("Human Council", save.FactionReputations[2].FactionId);
            Assert.AreEqual(2, report.MigratedCount);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    RelationshipIdentityMigrationStatus.UnknownPreserved,
                    RelationshipIdentityMigrationStatus.AmbiguousPreserved,
                    RelationshipIdentityMigrationStatus.ObsoletePreserved
                },
                report.Unresolved.Select(item => item.Status));
        }

        [Test]
        public void PersistenceAdapterFailsClosedOnCanonicalCollision()
        {
            var save = new SaveGameData
            {
                Reputation = new System.Collections.Generic.List<NpcAffinityData>
                {
                    new NpcAffinityData { NpcId = "npc_valerius", Affinity = 1 },
                    new NpcAffinityData { NpcId = "NPC_VALERIUS", Affinity = 2 }
                }
            };

            RelationshipPersistenceMigrationReport report =
                RelationshipPersistenceIdentityMigration.Apply(NewMigrator(), save);

            Assert.IsFalse(report.CanPersist);
            Assert.AreEqual("npc_valerius", save.Reputation[0].NpcId);
            Assert.AreEqual("NPC_VALERIUS", save.Reputation[1].NpcId);
            Assert.AreEqual(RelationshipIdentityMigrationStatus.AmbiguousPreserved,
                report.Unresolved.Single().Status);
        }

        [Test]
        public void PresentationLabelsRemainByteForByteUnchanged()
        {
            RelationshipLegacyIdentityMigrator migrator = NewMigrator();

            Assert.AreEqual("Captain Valerius", migrator.GetDisplayLabel(RelationshipDomain.NpcAffinity, "npc_valerius"));
            Assert.AreEqual("Radiant Council", migrator.GetDisplayLabel(RelationshipDomain.FactionReputation, "faction_crownlands_radiant_council"));
            Assert.AreEqual("Master Gruff", migrator.GetDisplayLabel(RelationshipDomain.NpcAffinity, "ADVISOR_GRUFF"));
        }

        private static RelationshipLegacyIdentityMigrator NewMigrator()
        {
            return new RelationshipLegacyIdentityMigrator(
                File.ReadAllBytes(Path.Combine(Application.dataPath, CatalogPath)));
        }
    }
}
