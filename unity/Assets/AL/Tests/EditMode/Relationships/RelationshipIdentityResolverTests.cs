using System.Collections;
using System.Linq;
using AL.Core.Interfaces.Relationships;
using AL.Services.Relationships;
using NUnit.Framework;

namespace AL.Tests.EditMode.Relationships
{
    public class RelationshipIdentityResolverTests
    {
        [Test]
        public void CanonicalNpcResolvesFoundWithoutRewritingAlias()
        {
            RelationshipIdentityResolution result =
                RelationshipTestFixtures.Identities().ResolveNpc(
                    RelationshipTestFixtures.Valerius);

            Assert.AreEqual(RelationshipIdentityStatus.Found, result.Status);
            Assert.AreEqual(RelationshipTestFixtures.Valerius, result.CanonicalId);
            Assert.IsTrue(result.RelationshipEnabled);
            Assert.IsTrue(result.SupportsMutation);
        }

        [TestCase(RelationshipTestFixtures.ValeriusAlias)]
        [TestCase(RelationshipTestFixtures.AdvisorValerius)]
        public void ExactLegacyAliasResolvesWithoutCaseFolding(string alias)
        {
            RelationshipIdentityResolution result =
                RelationshipTestFixtures.Identities().ResolveNpc(alias);

            Assert.AreEqual(RelationshipIdentityStatus.AliasResolved, result.Status);
            Assert.AreEqual(RelationshipTestFixtures.Valerius, result.CanonicalId);
            Assert.AreEqual(alias, result.RequestedId);
        }

        [Test]
        public void LowercasedLegacyAliasIsUnknown()
        {
            RelationshipIdentityResolution result =
                RelationshipTestFixtures.Identities().ResolveNpc("npc_valerius ".TrimEnd());
            RelationshipIdentityResolution folded =
                RelationshipTestFixtures.Identities().ResolveNpc("npc_VALERIUS");

            Assert.AreEqual(RelationshipIdentityStatus.Found, result.Status);
            Assert.AreEqual(RelationshipIdentityStatus.UnknownId, folded.Status);
        }

        [Test]
        public void BlankAndWhitespaceIdentitiesAreNotTrimmedIntoCanonicals()
        {
            InjectedRelationshipIdentityResolver resolver =
                RelationshipTestFixtures.Identities();

            Assert.AreEqual(
                RelationshipIdentityStatus.UnknownId,
                resolver.ResolveNpc(string.Empty).Status);
            Assert.AreEqual(
                RelationshipIdentityStatus.UnknownId,
                resolver.ResolveNpc(" ").Status);
            Assert.AreEqual(
                RelationshipIdentityStatus.UnknownId,
                resolver.ResolveFaction(null).Status);
        }

        [Test]
        public void DisabledCatalogNpcIsFoundButNotMutationSupported()
        {
            RelationshipIdentityResolution result =
                RelationshipTestFixtures.Identities().ResolveNpc("npc_vaeloryn");

            Assert.AreEqual(RelationshipIdentityStatus.Found, result.Status);
            Assert.IsFalse(result.SupportsMutation);
        }

        [Test]
        public void DuplicateCanonicalIsInvalidRecord()
        {
            var npcs = RelationshipTestFixtures.NpcRecords().ToList();
            npcs.Add(
                new RelationshipIdentityRecord(
                    RelationshipTestFixtures.Valerius,
                    new[] { "DUP" },
                    true,
                    "dup"));
            InjectedRelationshipIdentityResolver resolver =
                RelationshipTestFixtures.Identities(npcs: npcs);

            Assert.AreEqual(
                RelationshipIdentityCatalogValidationStatus.DuplicateId,
                resolver.CatalogValidation.Status);
            Assert.AreEqual(
                RelationshipIdentityStatus.InvalidRecord,
                resolver.ResolveNpc(RelationshipTestFixtures.Valerius).Status);
        }

        [Test]
        public void AliasCollisionAndShadowAndCycleAreRejected()
        {
            var collision = new[]
            {
                new RelationshipIdentityRecord(
                    "npc_a",
                    new[] { "SHARED" },
                    true,
                    "a"),
                new RelationshipIdentityRecord(
                    "npc_b",
                    new[] { "SHARED" },
                    true,
                    "b")
            };
            var shadow = new[]
            {
                new RelationshipIdentityRecord(
                    "npc_a",
                    new[] { "npc_b" },
                    true,
                    "a"),
                new RelationshipIdentityRecord(
                    "npc_b",
                    new string[0],
                    true,
                    "b")
            };
            var cycle = new[]
            {
                new RelationshipIdentityRecord(
                    "npc_a",
                    new[] { "npc_a" },
                    true,
                    "a")
            };

            Assert.AreEqual(
                RelationshipIdentityCatalogValidationStatus.AliasCollision,
                RelationshipTestFixtures.Identities(npcs: collision).CatalogValidation.Status);
            Assert.AreEqual(
                RelationshipIdentityCatalogValidationStatus.AliasShadow,
                RelationshipTestFixtures.Identities(npcs: shadow).CatalogValidation.Status);
            Assert.AreEqual(
                RelationshipIdentityCatalogValidationStatus.AliasCycle,
                RelationshipTestFixtures.Identities(npcs: cycle).CatalogValidation.Status);
        }

        [Test]
        public void FactionAliasResolvesExactLegacyAndroidId()
        {
            RelationshipIdentityResolution council =
                RelationshipTestFixtures.Identities().ResolveFaction(
                    RelationshipTestFixtures.HumanCouncilAlias);
            RelationshipIdentityResolution watch =
                RelationshipTestFixtures.Identities().ResolveFaction(
                    RelationshipTestFixtures.VeilWatchAlias);

            Assert.AreEqual(RelationshipIdentityStatus.AliasResolved, council.Status);
            Assert.AreEqual(
                RelationshipTestFixtures.CrownlandsCouncil,
                council.CanonicalId);
            Assert.AreEqual(RelationshipIdentityStatus.AliasResolved, watch.Status);
            Assert.AreEqual(RelationshipTestFixtures.VeilWatch, watch.CanonicalId);
        }

        [Test]
        public void PendingCatalogDoesNotInventAuthorityFromInjectedRecords()
        {
            InjectedRelationshipIdentityResolver resolver =
                RelationshipTestFixtures.Identities(
                    RelationshipCatalogAvailability.Pending);

            RelationshipIdentityResolution result =
                resolver.ResolveNpc(RelationshipTestFixtures.Valerius);

            Assert.AreEqual(
                RelationshipIdentityCatalogValidationStatus.CatalogPending,
                resolver.CatalogValidation.Status);
            Assert.AreEqual(RelationshipIdentityStatus.CatalogPending, result.Status);
            Assert.IsFalse(result.SupportsMutation);
            Assert.IsTrue(
                result.Diagnostics.Any(item =>
                    item.Code == RelationshipDiagnosticCodes.CatalogPending));
        }

        [Test]
        public void DiagnosticsAreImmutableAndContainNoPlayerFacingEnglishRank()
        {
            RelationshipIdentityResolution result =
                RelationshipTestFixtures.Identities().ResolveNpc("unknown_future");

            Assert.AreEqual(RelationshipIdentityStatus.UnknownId, result.Status);
            Assert.Throws<System.NotSupportedException>(() =>
                ((IList)result.Diagnostics).Clear());
            Assert.IsFalse(
                result.Diagnostics.Any(item =>
                    item.Action.Contains("Exalted") ||
                    item.Action.Contains("Nemesis") ||
                    item.Action.Contains("Ally")));
        }
    }
}
