using System.IO;
using AL.Core.Interfaces.Relationships;
using AL.Services.Relationships;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Relationships
{
    public class RelationshipAuthorityBoundaryTests
    {
        [Test]
        public void FixtureIdentitiesMatchApproved347CatalogWithoutBecomingProductionAuthority()
        {
            string json = RelationshipTestFixtures.ApprovedCatalogJson();

            Assert.IsTrue(json.Contains("\"id\": \"npc_valerius\""));
            Assert.IsTrue(json.Contains("\"NPC_VALERIUS\""));
            Assert.IsTrue(json.Contains("\"ADVISOR_VALERIUS\""));
            Assert.IsTrue(json.Contains("\"id\": \"faction_veil_watch\""));
            Assert.IsTrue(json.Contains("\"FACTION_VEIL_WATCH\""));
            Assert.IsTrue(json.Contains("\"FACT_HUMAN_COUNCIL\""));
            Assert.IsTrue(json.Contains("GRANT_VALERIUS_AFFINITY_5"));
            Assert.IsTrue(json.Contains("\"targetNpcId\": \"npc_valerius\""));
            Assert.IsTrue(json.Contains("\"delta\": 5"));
            Assert.IsTrue(
                json.Contains("runtime service implementation") ||
                json.Contains("source_ready_runtime_transaction_unimplemented"));

            RelationshipIdentityResolution valerius =
                RelationshipTestFixtures.Identities().ResolveNpc("npc_valerius");
            Assert.AreEqual(RelationshipIdentityStatus.Found, valerius.Status);
        }

        [Test]
        public void Issue183PendingCatalogRefusesMutationAndDoesNotInventAuthority()
        {
            InjectedRelationshipIdentityResolver identities =
                RelationshipTestFixtures.Identities(
                    RelationshipCatalogAvailability.Pending);
            InjectedRelationshipPolicyResolver policies =
                RelationshipTestFixtures.Policies(
                    RelationshipCatalogAvailability.Pending);
            RelationshipSnapshot snapshot = RelationshipSnapshotBuilder.Build(
                RelationshipRawState.EmptyWritable(),
                identities,
                policies);
            RelationshipPlanningResult planned = new RelationshipMutationPlanner(
                    identities,
                    policies)
                .Plan(
                    RelationshipMutationRequest.Affinity(
                        RelationshipTestFixtures.Valerius,
                        5f,
                        RelationshipTestFixtures.CorrelationId,
                        "op-pending",
                        RelationshipTestFixtures.SourceSystemId),
                    snapshot);

            Assert.AreEqual(
                RelationshipDomainValidationStatus.MalformedPolicyUnavailable,
                snapshot.NpcAffinityDomain.Status);
            Assert.AreEqual(
                RelationshipPreparationStatus.RejectedPolicyUnavailable,
                planned.Status);
            Assert.IsNull(planned.Plan);
        }

        [Test]
        public void PlannerIsUnregisteredInBootloaderAndGameDataService()
        {
            string bootloader = RelationshipTestFixtures.BootloaderSource();
            string gameData = RelationshipTestFixtures.GameDataServiceSource();

            Assert.IsFalse(bootloader.Contains("IRelationshipIdentityResolver"));
            Assert.IsFalse(bootloader.Contains("RelationshipMutationPlanner"));
            Assert.IsFalse(bootloader.Contains("InjectedRelationshipIdentityResolver"));
            Assert.IsFalse(gameData.Contains("IRelationshipIdentityResolver"));
            Assert.IsFalse(gameData.Contains("RelationshipMutationPlanner"));
        }

        [Test]
        public void ProductionRelationshipServicesRemainSaveBackedAndUnchangedByThisSlice()
        {
            string reputation = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "AL/Scripts/Kingdom/Narrative/ReputationService.cs"));
            string faction = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "AL/Scripts/Kingdom/Narrative/FactionService.cs"));
            string persona = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "AL/Scripts/Kingdom/Narrative/PersonaService.cs"));

            Assert.IsTrue(reputation.Contains("Mathf.Clamp"));
            Assert.IsTrue(reputation.Contains("Exalted"));
            Assert.IsTrue(faction.Contains("Ally"));
            Assert.IsTrue(persona.Contains("PersonaTrait.Sage"));
            Assert.IsFalse(reputation.Contains("RelationshipMutationPlanner"));
            Assert.IsFalse(faction.Contains("IRelationshipIdentityResolver"));
            Assert.IsFalse(persona.Contains("PersonaClassificationStatus"));
        }

        [Test]
        public void PolicyRejectsOverlappingAffinityBands()
        {
            RelationshipPolicySnapshot valid =
                InjectedRelationshipPolicyResolver.CreateLegacyFixturePolicy(
                    RelationshipTechnicalLimits.FixtureCatalogRevision);
            var overlapping = new RelationshipPolicySnapshot(
                valid.SchemaVersion,
                valid.ContentVersion,
                valid.SourceRevision,
                valid.IdentityCatalogRevision,
                valid.PolicyRevision,
                valid.AffinityMinimum,
                valid.AffinityMaximum,
                new[]
                {
                    new RelationshipClassificationBand(
                        "a", 80d, 100d, true, true, "a"),
                    new RelationshipClassificationBand(
                        "b", 50d, 90d, true, false, "b"),
                    new RelationshipClassificationBand(
                        "c", 0d, 50d, true, false, "c"),
                    new RelationshipClassificationBand(
                        "d", -50d, 0d, true, false, "d"),
                    new RelationshipClassificationBand(
                        "e", -100d, -50d, true, false, "e")
                },
                valid.FactionBands,
                valid.SupportedPersonaTraits,
                valid.PersonaUniqueContentReference,
                valid.PersonaTieContentReference,
                valid.PersonaAllZeroContentReference,
                valid.PersonaUnavailableContentReference,
                valid.PersonaMalformedContentReference);

            InjectedRelationshipPolicyResolver resolver =
                RelationshipTestFixtures.Policies(
                    RelationshipCatalogAvailability.Available,
                    overlapping);

            Assert.AreEqual(
                RelationshipPolicyValidationStatus.InvalidBandOverlapOrGap,
                resolver.PolicyValidation.Status);
        }
    }
}
