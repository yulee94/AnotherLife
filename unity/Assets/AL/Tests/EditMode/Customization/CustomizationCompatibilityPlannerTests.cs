using System;
using System.Linq;
using AL.ChampionMode.Customization;
using AL.ChampionMode.Customization.Contracts;
using NUnit.Framework;

namespace AL.Tests.EditMode.Customization
{
    public class CustomizationCompatibilityPlannerTests
    {
        [Test]
        public void CurrentLegacyStateIsValidWithoutBeingMutatedOrUpgraded()
        {
            RawCustomizationSnapshot raw = CustomizationPlannerTestFixtures.Raw();
            string before = raw.Fingerprint;

            CustomizationCompatibilityResult result =
                CustomizationCompatibilityPlanner.Classify(
                    raw,
                    CustomizationCatalogAvailability.Ready,
                    CustomizationPlannerTestFixtures.Catalog());

            Assert.That(result.Status,
                Is.EqualTo(CustomizationDomainStatus.ValidLegacyNoMetadata));
            Assert.That(result.Raw, Is.SameAs(raw));
            Assert.That(result.Raw.Fingerprint, Is.EqualTo(before));
            Assert.That(result.Fields.Values.All(item =>
                item.Status == CustomizationFieldStatus.RawValidResolved), Is.True);
        }

        [Test]
        public void MetadataBearingCurrentStateIsValid()
        {
            CustomizationCompatibilityResult result =
                CustomizationCompatibilityPlanner.Classify(
                    CustomizationPlannerTestFixtures.Raw(metadata: true),
                    CustomizationCatalogAvailability.Ready,
                    CustomizationPlannerTestFixtures.Catalog());

            Assert.That(result.Status, Is.EqualTo(CustomizationDomainStatus.Valid));
        }

        [Test]
        public void PreservedUnknownRemainsExactWhilePresentationUsesPlaceholder()
        {
            CustomizationValues values =
                CustomizationPlannerTestFixtures.Values(hair: "future_hair_v2");
            RawCustomizationSnapshot raw =
                CustomizationPlannerTestFixtures.Raw(values);
            CustomizationCatalogSnapshot catalog =
                CustomizationPlannerTestFixtures.Catalog();

            CustomizationCompatibilityResult compatibility =
                CustomizationCompatibilityPlanner.Classify(
                    raw, CustomizationCatalogAvailability.Ready, catalog);
            CustomizationQueryResult query =
                CustomizationCompatibilityPlanner.Resolve(
                    raw, CustomizationCatalogAvailability.Ready, catalog,
                    CustomizationPlannerTestFixtures.Model());

            Assert.That(compatibility.Status,
                Is.EqualTo(CustomizationDomainStatus.PreservedUnknown));
            Assert.That(compatibility.Raw.Values.HairStyleId,
                Is.EqualTo("future_hair_v2"));
            Assert.That(query.EffectivePresentation.Values.HairStyleId,
                Is.EqualTo("short"));
            Assert.That(query.EffectivePresentation.FieldStatuses[
                    CustomizationField.HairStyle],
                Is.EqualTo(CustomizationFieldStatus.EffectivePlaceholder));
            Assert.That(query.RawCommitted.Values.HairStyleId,
                Is.EqualTo("future_hair_v2"));
        }

        [Test]
        public void AliasCanPreviewDestinationWithoutRewritingRawIdentity()
        {
            RawCustomizationSnapshot raw = CustomizationPlannerTestFixtures.Raw(
                CustomizationPlannerTestFixtures.Values(hair: "cropped"));
            CustomizationCatalogSnapshot catalog =
                CustomizationPlannerTestFixtures.Catalog();

            CustomizationQueryResult query =
                CustomizationCompatibilityPlanner.Resolve(
                    raw, CustomizationCatalogAvailability.Ready, catalog,
                    CustomizationPlannerTestFixtures.Model());

            Assert.That(query.Status,
                Is.EqualTo(CustomizationDomainStatus.NeedsAliasMigration));
            Assert.That(query.RawCommitted.Values.HairStyleId,
                Is.EqualTo("cropped"));
            Assert.That(query.EffectivePresentation.Values.HairStyleId,
                Is.EqualTo("short"));
            Assert.That(query.EffectivePresentation.FieldStatuses[
                    CustomizationField.HairStyle],
                Is.EqualTo(CustomizationFieldStatus.RawValidAliasAvailable));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-0.01f)]
        [TestCase(1.01f)]
        public void InvalidRawColorIsPreservedAndPresentedAsDefault(float red)
        {
            var invalid = new CustomizationColor(red, 0.4f, 0.5f);
            RawCustomizationSnapshot raw = CustomizationPlannerTestFixtures.Raw(
                CustomizationPlannerTestFixtures.Values(primary: invalid));

            CustomizationQueryResult query =
                CustomizationCompatibilityPlanner.Resolve(
                    raw,
                    CustomizationCatalogAvailability.Ready,
                    CustomizationPlannerTestFixtures.Catalog(),
                    CustomizationPlannerTestFixtures.Model());

            Assert.That(query.Status, Is.EqualTo(CustomizationDomainStatus.Malformed));
            Assert.That(float.IsNaN(red)
                    ? float.IsNaN(query.RawCommitted.Values.PrimaryColor.Red)
                    : query.RawCommitted.Values.PrimaryColor.Red.Equals(red),
                Is.True);
            Assert.That(query.EffectivePresentation.Values.PrimaryColor,
                Is.EqualTo(CustomizationPlannerTestFixtures.Blue));
            Assert.That(query.EffectivePresentation.FieldStatuses[
                    CustomizationField.PrimaryColor],
                Is.EqualTo(CustomizationFieldStatus.EffectivePlaceholder));
        }

        [Test]
        public void PendingCatalogDoesNotNormalizeAndCanRetainExactPriorPresentation()
        {
            RawCustomizationSnapshot raw = CustomizationPlannerTestFixtures.Raw();
            CustomizationCatalogSnapshot catalog =
                CustomizationPlannerTestFixtures.Catalog();
            ModelCapabilitySnapshot model = CustomizationPlannerTestFixtures.Model();
            EffectiveAppearanceSnapshot prior =
                CustomizationPlannerTestFixtures.Query(raw, catalog, model)
                    .EffectivePresentation;

            CustomizationQueryResult withoutPrior =
                CustomizationCompatibilityPlanner.Resolve(
                    raw, CustomizationCatalogAvailability.Pending, null, model);
            CustomizationQueryResult withPrior =
                CustomizationCompatibilityPlanner.Resolve(
                    raw, CustomizationCatalogAvailability.Pending, null, model, prior);

            Assert.That(withoutPrior.Status,
                Is.EqualTo(CustomizationDomainStatus.CatalogPending));
            Assert.That(withoutPrior.EffectivePresentation, Is.Null);
            Assert.That(withPrior.EffectivePresentation, Is.SameAs(prior));
            Assert.That(withPrior.RawCommitted.Fingerprint, Is.EqualTo(raw.Fingerprint));
        }

        [Test]
        public void MissingRequiredModelCapabilityIsVisibleAndUsesSafePlaceholder()
        {
            string[] capabilities = CustomizationPlannerTestFixtures.AllCapabilities()
                .Where(item => item != "cap_hair_long").ToArray();
            ModelCapabilitySnapshot model = CustomizationPlannerTestFixtures.Model(
                capabilities: capabilities);
            RawCustomizationSnapshot raw = CustomizationPlannerTestFixtures.Raw(
                CustomizationPlannerTestFixtures.Values(hair: "long"));

            CustomizationQueryResult result =
                CustomizationCompatibilityPlanner.Resolve(
                    raw,
                    CustomizationCatalogAvailability.Ready,
                    CustomizationPlannerTestFixtures.Catalog(),
                    model);

            Assert.That(result.Status,
                Is.EqualTo(CustomizationDomainStatus.ModelCapabilityUnavailable));
            Assert.That(result.RawCommitted.Values.HairStyleId, Is.EqualTo("long"));
            Assert.That(result.EffectivePresentation.Values.HairStyleId,
                Is.EqualTo("short"));
            Assert.That(result.EffectivePresentation.FieldStatuses[
                    CustomizationField.HairStyle],
                Is.EqualTo(CustomizationFieldStatus.UnavailableMissingCapability));
        }

        [Test]
        public void FutureSchemaFailsClosedAcrossEveryField()
        {
            CustomizationCompatibilityResult result =
                CustomizationCompatibilityPlanner.Classify(
                    CustomizationPlannerTestFixtures.Raw(schemaVersion: 2),
                    CustomizationCatalogAvailability.Ready,
                    CustomizationPlannerTestFixtures.Catalog());

            Assert.That(result.Status,
                Is.EqualTo(CustomizationDomainStatus.FutureSchemaUnsupported));
            Assert.That(result.Fields.Count,
                Is.EqualTo(CustomizationFieldMap.Enumerate(CustomizationField.All).Count));
            Assert.That(result.Fields.Values.All(item =>
                item.Status == CustomizationFieldStatus.RawUnsupportedFutureSchema),
                Is.True);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonpositiveRawSchemaIsMalformed(int schemaVersion)
        {
            CustomizationCompatibilityResult result =
                CustomizationCompatibilityPlanner.Classify(
                    CustomizationPlannerTestFixtures.Raw(
                        schemaVersion: schemaVersion),
                    CustomizationCatalogAvailability.Ready,
                    CustomizationPlannerTestFixtures.Catalog());

            Assert.That(result.Status,
                Is.EqualTo(CustomizationDomainStatus.Malformed));
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-RAW-SCHEMA-INVALID"));
        }

        [TestCase("")]
        [TestCase(" SHORT")]
        [TestCase("Short")]
        [TestCase("short\n")]
        public void BlankOrNoncanonicalRawOptionIsMalformed(string hairId)
        {
            CustomizationCompatibilityResult result =
                CustomizationCompatibilityPlanner.Classify(
                    CustomizationPlannerTestFixtures.Raw(
                        CustomizationPlannerTestFixtures.Values(hair: hairId)),
                    CustomizationCatalogAvailability.Ready,
                    CustomizationPlannerTestFixtures.Catalog());

            Assert.That(result.Status, Is.EqualTo(CustomizationDomainStatus.Malformed));
            Assert.That(result.Fields[CustomizationField.HairStyle].Status,
                Is.EqualTo(CustomizationFieldStatus.RawBlankInvalid));
        }
    }
}
