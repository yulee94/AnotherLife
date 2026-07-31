using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AL.ChampionMode.Customization;
using AL.ChampionMode.Customization.Contracts;
using NUnit.Framework;

namespace AL.Tests.EditMode.Customization
{
    public class CustomizationModelCapabilityValidatorTests
    {
        [Test]
        public void ValidCandidatePublishesCanonicalImmutableSnapshot()
        {
            string[] source = { "cap_weapon_sword", "cap_hair_short" };
            ModelCapabilityValidationResult result =
                CustomizationModelCapabilityValidator.Validate(
                    new ModelCapabilityCandidate(
                        "model_capability_test",
                        1L,
                        "procedural_champion_fixture",
                        CustomizationField.HairStyle |
                        CustomizationField.WeaponStyle,
                        source));
            source[0] = "mutated";

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Snapshot.Capabilities,
                Is.EqualTo(new[] { "cap_hair_short", "cap_weapon_sword" }));
            Assert.That(result.Snapshot.Fingerprint, Has.Length.EqualTo(64));
            Assert.Throws<NotSupportedException>(() =>
                ((IList)result.Snapshot.Capabilities).Clear());
        }

        [Test]
        public void InputPermutationProducesSameFingerprint()
        {
            ModelCapabilityValidationResult first =
                CustomizationModelCapabilityValidator.Validate(
                    Candidate(new[] { "cap_weapon_sword", "cap_hair_short" }));
            ModelCapabilityValidationResult second =
                CustomizationModelCapabilityValidator.Validate(
                    Candidate(new[] { "cap_hair_short", "cap_weapon_sword" }));

            Assert.That(first.IsValid, Is.True);
            Assert.That(second.IsValid, Is.True);
            Assert.That(second.Snapshot.Fingerprint,
                Is.EqualTo(first.Snapshot.Fingerprint));
        }

        [Test]
        public void InvalidIdentityRevisionSourceFieldsAndCapabilitiesReject()
        {
            ModelCapabilityValidationResult result =
                CustomizationModelCapabilityValidator.Validate(
                    new ModelCapabilityCandidate(
                        "Bad-Model",
                        0L,
                        "Bad Source",
                        CustomizationField.All | (CustomizationField)(1 << 20),
                        new[] { "bad-capability", "valid_cap", "valid_cap" }));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Is.EquivalentTo(new[]
                {
                    "AL-CUS-MODEL-IDENTITY",
                    "AL-CUS-MODEL-REVISION",
                    "AL-CUS-MODEL-SOURCE",
                    "AL-CUS-MODEL-FIELDS",
                    "AL-CUS-MODEL-CAPABILITY-ID",
                    "AL-CUS-MODEL-CAPABILITY-DUPLICATE"
                }));
        }

        [Test]
        public void NullCandidateAndNullCapabilityCollectionReject()
        {
            ModelCapabilityValidationResult missing =
                CustomizationModelCapabilityValidator.Validate(null);
            ModelCapabilityValidationResult missingCollection =
                CustomizationModelCapabilityValidator.Validate(
                    Candidate(null));

            Assert.That(missing.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-MODEL-NULL"));
            Assert.That(missingCollection.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-MODEL-CAPABILITIES"));
        }

        [Test]
        public void CapabilityIngressStopsAtLimitPlusOneBeforeValidation()
        {
            int reads = 0;
            ModelCapabilityCandidate candidate = Candidate(
                HostileOversized("cap_hair_short", () => reads++));

            ModelCapabilityValidationResult result =
                CustomizationModelCapabilityValidator.Validate(candidate);

            Assert.That(reads,
                Is.EqualTo(CustomizationTechnicalLimits.MaximumCapabilities + 1));
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-MODEL-CAPABILITIES"));
        }

        private static ModelCapabilityCandidate Candidate(
            IEnumerable<string> capabilities)
        {
            return new ModelCapabilityCandidate(
                "model_capability_test",
                1L,
                "procedural_champion_fixture",
                CustomizationField.All,
                capabilities);
        }

        private static IEnumerable<string> HostileOversized(
            string value,
            Action onRead)
        {
            for (int index = 0;
                 index <= CustomizationTechnicalLimits.MaximumCapabilities;
                 index++)
            {
                onRead();
                yield return value;
            }

            throw new InvalidOperationException(
                "The bounded copy enumerated beyond limit plus one.");
        }
    }
}
