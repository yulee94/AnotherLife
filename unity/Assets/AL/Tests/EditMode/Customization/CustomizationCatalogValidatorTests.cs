using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AL.ChampionMode.Customization;
using AL.ChampionMode.Customization.Contracts;
using NUnit.Framework;

namespace AL.Tests.EditMode.Customization
{
    public class CustomizationCatalogValidatorTests
    {
        [Test]
        public void ValidCatalogPublishesCanonicalImmutableSnapshot()
        {
            CustomizationOptionCandidate[] source =
                CustomizationPlannerTestFixtures.Options();
            CustomizationCatalogValidationResult result =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(options: source));
            source[0] = null;

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Snapshot.Options[0], Is.Not.Null);
            Assert.That(result.Snapshot.Fingerprint, Has.Length.EqualTo(64));
            Assert.Throws<NotSupportedException>(() =>
                ((IList)result.Snapshot.Options).Clear());
            Assert.Throws<NotSupportedException>(() =>
                ((IDictionary)result.Snapshot.Policy.PlaceholderOptionIds).Clear());
        }

        [Test]
        public void CandidateDefensivelyCopiesCollectionsAndNumericArrays()
        {
            float[] scale = { 1f, 1f, 1f };
            var body = new[]
            {
                new CustomizationBodyPresetCandidate(
                    "average", "customization.body.average.name",
                    "cap_body_average", 0, scale),
                CustomizationPlannerTestFixtures.BodyPresets()[1]
            };
            CustomizationCatalogCandidate candidate =
                CustomizationPlannerTestFixtures.Candidate(bodyPresets: body);

            scale[0] = 9f;
            body[0] = null;

            Assert.That(candidate.BodyPresets[0], Is.Not.Null);
            Assert.That(candidate.BodyPresets[0].Scale[0], Is.EqualTo(1f));
            Assert.Throws<NotSupportedException>(() =>
                ((IList)candidate.BodyPresets).Clear());
            Assert.Throws<NotSupportedException>(() =>
                ((IList)candidate.BodyPresets[0].Scale).Clear());
        }

        [Test]
        public void NumericCandidateIngressCopiesAtMostRequiredShapePlusOne()
        {
            float[] oversized = Enumerable.Repeat(1f, 10000).ToArray();
            var body = new CustomizationBodyPresetCandidate(
                "average",
                "customization.body.average.name",
                "cap_body_average",
                0,
                oversized);
            var color = new CustomizationColorCandidate(
                CustomizationFamilies.PrimaryColors,
                "blue",
                "customization.color.blue.name",
                "cap_primary",
                0,
                oversized);

            oversized[0] = 9f;

            Assert.That(body.Scale.Count,
                Is.EqualTo(CustomizationTechnicalLimits.RequiredVectorComponents + 1));
            Assert.That(color.Rgb.Count,
                Is.EqualTo(CustomizationTechnicalLimits.RequiredVectorComponents + 1));
            Assert.That(body.Scale[0], Is.EqualTo(1f));
            Assert.That(color.Rgb[0], Is.EqualTo(1f));
        }

        [Test]
        public void CatalogIngressStopsAtEachLimitPlusOneBeforeValidation()
        {
            int bodyReads = 0;
            int optionReads = 0;
            int colorReads = 0;
            int aliasReads = 0;
            int presetReads = 0;
            CustomizationCatalogCandidate candidate =
                CustomizationPlannerTestFixtures.Candidate(
                    bodyPresets: HostileOversized(
                        CustomizationPlannerTestFixtures.BodyPresets()[0],
                        CustomizationTechnicalLimits.MaximumOptions,
                        () => bodyReads++),
                    options: HostileOversized(
                        CustomizationPlannerTestFixtures.Options()[0],
                        CustomizationTechnicalLimits.MaximumOptions,
                        () => optionReads++),
                    colors: HostileOversized(
                        CustomizationPlannerTestFixtures.Colors()[0],
                        CustomizationTechnicalLimits.MaximumOptions,
                        () => colorReads++),
                    aliases: HostileOversized(
                        new CustomizationAliasCandidate(
                            CustomizationFamilies.HairStyles,
                            "legacy_hair",
                            "short",
                            "v2",
                            false),
                        CustomizationTechnicalLimits.MaximumAliases,
                        () => aliasReads++),
                    presets: HostileOversized(
                        CustomizationPlannerTestFixtures.Presets()[0],
                        CustomizationTechnicalLimits.MaximumPresets,
                        () => presetReads++));

            CustomizationCatalogValidationResult result =
                CustomizationCatalogValidator.Validate(candidate);

            Assert.That(bodyReads,
                Is.EqualTo(CustomizationTechnicalLimits.MaximumOptions + 1));
            Assert.That(optionReads,
                Is.EqualTo(CustomizationTechnicalLimits.MaximumOptions + 1));
            Assert.That(colorReads,
                Is.EqualTo(CustomizationTechnicalLimits.MaximumOptions + 1));
            Assert.That(aliasReads,
                Is.EqualTo(CustomizationTechnicalLimits.MaximumAliases + 1));
            Assert.That(presetReads,
                Is.EqualTo(CustomizationTechnicalLimits.MaximumPresets + 1));
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics.Count(item =>
                item.Code == "AL-CUS-COLLECTION-INVALID"), Is.EqualTo(5));
        }

        [Test]
        public void PresetCapabilityIngressStopsAtLimitPlusOneBeforeValidation()
        {
            int reads = 0;
            var preset = new CustomizationPresetCandidate(
                "oversized",
                "customization.preset.oversized.name",
                CustomizationField.HairStyle,
                CustomizationPlannerTestFixtures.Values(hair: "long"),
                HostileOversized(
                    "cap_hair_long",
                    CustomizationTechnicalLimits.MaximumCapabilities,
                    () => reads++));

            CustomizationCatalogValidationResult result =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(
                        presets: new[] { preset }));

            Assert.That(reads,
                Is.EqualTo(CustomizationTechnicalLimits.MaximumCapabilities + 1));
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-PRESET-CAPABILITY"));
        }

        [Test]
        public void PlaceholderIngressStopsAtLimitPlusOneBeforeValidation()
        {
            int reads = 0;
            var policy = new CustomizationPolicyCandidate(
                CustomizationPlannerTestFixtures.Values(),
                new CustomizationScale(0.5f, 0.5f, 0.5f),
                new CustomizationScale(2f, 2f, 2f),
                new HostilePlaceholderDictionary(() => reads++),
                true);

            CustomizationCatalogValidationResult result =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(policy: policy));

            Assert.That(reads, Is.EqualTo(
                CustomizationTechnicalLimits.MaximumPlaceholderOptions + 1));
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-POLICY-PLACEHOLDERS"));
        }

        [Test]
        public void UnexpectedPlaceholderFamilyRejectsAtomically()
        {
            Dictionary<string, string> placeholders =
                CustomizationPlannerTestFixtures.Placeholders().ToDictionary(
                    item => item.Key,
                    item => item.Value,
                    StringComparer.Ordinal);
            placeholders.Remove(CustomizationFamilies.OffhandStyles);
            placeholders.Add("unexpected_family", "shield");
            var policy = new CustomizationPolicyCandidate(
                CustomizationPlannerTestFixtures.Values(),
                new CustomizationScale(0.5f, 0.5f, 0.5f),
                new CustomizationScale(2f, 2f, 2f),
                placeholders,
                true);

            CustomizationCatalogValidationResult result =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(policy: policy));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-POLICY-PLACEHOLDER-UNEXPECTED"));
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-POLICY-PLACEHOLDER-MISSING"));
        }

        [Test]
        public void InputPermutationProducesSameCanonicalFingerprint()
        {
            CustomizationCatalogSnapshot first =
                CustomizationPlannerTestFixtures.Catalog();
            CustomizationCatalogCandidate reversed =
                CustomizationPlannerTestFixtures.Candidate(
                    bodyPresets: CustomizationPlannerTestFixtures.BodyPresets()
                        .Reverse().ToArray(),
                    options: CustomizationPlannerTestFixtures.Options()
                        .Reverse().ToArray(),
                    colors: CustomizationPlannerTestFixtures.Colors()
                        .Reverse().ToArray(),
                    presets: CustomizationPlannerTestFixtures.Presets()
                        .Reverse().ToArray());
            CustomizationCatalogSnapshot second =
                CustomizationPlannerTestFixtures.Catalog(reversed);

            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
        }

        [TestCase("Character_Customization")]
        [TestCase("character-customization")]
        [TestCase("character_customization\n")]
        public void WrongCatalogIdentityRejects(string catalogId)
        {
            CustomizationCatalogValidationResult result =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(
                        identity: CustomizationPlannerTestFixtures.Identity(catalogId)));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-IDENTITY-CATALOG"));
        }

        [Test]
        public void BadHashAndPackagingPathReject()
        {
            var identity = new CustomizationCatalogIdentity(
                "another_life", "catalog_set_test",
                CustomizationTechnicalLimits.ExpectedCatalogId,
                CustomizationTechnicalLimits.ExpectedFamilyId,
                "schema_v1", "content_v1", "source_v1", "ABC", "../escape.json");
            CustomizationCatalogValidationResult result =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(identity: identity));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Is.EquivalentTo(new[]
                {
                    "AL-CUS-IDENTITY-HASH",
                    "AL-CUS-IDENTITY-PATH"
                }));
        }

        [Test]
        public void NullDuplicateAndOrderCollisionRejectAtomically()
        {
            CustomizationOptionCandidate[] options =
                CustomizationPlannerTestFixtures.Options();
            options[1] = options[0];
            options[2] = null;

            CustomizationCatalogValidationResult result =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(options: options));

            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-OPTION-DUPLICATE"));
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-OPTION-ORDER-DUPLICATE"));
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-OPTION-NULL"));
        }

        [TestCase(null)]
        [TestCase(new float[0])]
        [TestCase(new[] { 0.1f })]
        [TestCase(new[] { 0.1f, 0.2f })]
        [TestCase(new[] { 0.1f, 0.2f, 0.3f, 0.4f })]
        public void ColorRequiresExactlyThreeChannels(float[] rgb)
        {
            CustomizationColorCandidate[] colors =
                CustomizationPlannerTestFixtures.Colors();
            colors[0] = new CustomizationColorCandidate(
                CustomizationFamilies.PrimaryColors, "crown_blue",
                "customization.primary.crown_blue.name", "cap_primary", 0, rgb);

            CustomizationCatalogValidationResult result =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(colors: colors));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-COLOR-SHAPE"));
        }

        [TestCase(float.NaN, 0.2f, 0.3f)]
        [TestCase(float.PositiveInfinity, 0.2f, 0.3f)]
        [TestCase(float.NegativeInfinity, 0.2f, 0.3f)]
        [TestCase(-0.01f, 0.2f, 0.3f)]
        [TestCase(1.01f, 0.2f, 0.3f)]
        public void ColorRejectsNonFiniteAndOutOfRangeWithoutClamping(
            float red,
            float green,
            float blue)
        {
            CustomizationColorCandidate[] colors =
                CustomizationPlannerTestFixtures.Colors();
            colors[0] = new CustomizationColorCandidate(
                CustomizationFamilies.PrimaryColors, "crown_blue",
                "customization.primary.crown_blue.name", "cap_primary", 0,
                new[] { red, green, blue });

            CustomizationCatalogValidationResult result =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(colors: colors));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-COLOR-NUMERIC"));
        }

        [Test]
        public void BodyScaleRejectsInvalidShapeNumericAndInjectedBounds()
        {
            CustomizationBodyPresetCandidate[] badShape =
                CustomizationPlannerTestFixtures.BodyPresets();
            badShape[0] = new CustomizationBodyPresetCandidate(
                "average", "customization.body.average.name", "cap_body_average", 0,
                new[] { 1f, 1f });
            CustomizationBodyPresetCandidate[] badNumeric =
                CustomizationPlannerTestFixtures.BodyPresets();
            badNumeric[0] = new CustomizationBodyPresetCandidate(
                "average", "customization.body.average.name", "cap_body_average", 0,
                new[] { 0f, float.NaN, 3f });
            CustomizationBodyPresetCandidate[] outsideBounds =
                CustomizationPlannerTestFixtures.BodyPresets();
            outsideBounds[0] = new CustomizationBodyPresetCandidate(
                "average", "customization.body.average.name", "cap_body_average", 0,
                new[] { 2.01f, 1f, 1f });

            CustomizationCatalogValidationResult shape =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(bodyPresets: badShape));
            CustomizationCatalogValidationResult numeric =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(bodyPresets: badNumeric));
            CustomizationCatalogValidationResult bounds =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(
                        bodyPresets: outsideBounds));

            Assert.That(shape.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-SCALE-SHAPE"));
            Assert.That(numeric.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-SCALE-NUMERIC"));
            Assert.That(bounds.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-SCALE-BOUNDS"));
        }

        [Test]
        public void MissingFamilyDefaultAndPlaceholderReject()
        {
            CustomizationOptionCandidate[] options =
                CustomizationPlannerTestFixtures.Options()
                    .Where(item => item.FamilyId != CustomizationFamilies.OffhandStyles)
                    .ToArray();
            var placeholders = CustomizationPlannerTestFixtures.Placeholders()
                .Where(item => item.Key != CustomizationFamilies.WeaponStyles)
                .ToDictionary(item => item.Key, item => item.Value);
            var policy = new CustomizationPolicyCandidate(
                CustomizationPlannerTestFixtures.Values(offhand: "unknown"),
                new CustomizationScale(0.5f, 0.5f, 0.5f),
                new CustomizationScale(2f, 2f, 2f),
                placeholders,
                true);

            CustomizationCatalogValidationResult result =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(
                        options: options,
                        policy: policy));

            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-FAMILY-MISSING"));
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-POLICY-DEFAULT-MISSING"));
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-POLICY-PLACEHOLDER-MISSING"));
        }

        [Test]
        public void AliasMustResolveExactlyAndCannotShadowCurrentOption()
        {
            var aliases = new[]
            {
                new CustomizationAliasCandidate(
                    CustomizationFamilies.HairStyles, "short", "long", "v2", false),
                new CustomizationAliasCandidate(
                    CustomizationFamilies.HairStyles, "legacy", "future", "v2", false)
            };

            CustomizationCatalogValidationResult result =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(aliases: aliases));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics.Count(item =>
                item.Code == "AL-CUS-ALIAS-COLLISION"), Is.EqualTo(2));
        }

        [Test]
        public void AliasChainsFlattenToCurrentIdentityAndCyclesReject()
        {
            var chain = new[]
            {
                new CustomizationAliasCandidate(
                    CustomizationFamilies.HairStyles,
                    "very_old_hair", "old_hair", "v2", false),
                new CustomizationAliasCandidate(
                    CustomizationFamilies.HairStyles,
                    "old_hair", "short", "v3", true)
            };
            var cycle = new[]
            {
                new CustomizationAliasCandidate(
                    CustomizationFamilies.HairStyles,
                    "cycle_a", "cycle_b", "v2", false),
                new CustomizationAliasCandidate(
                    CustomizationFamilies.HairStyles,
                    "cycle_b", "cycle_a", "v2", false)
            };

            CustomizationCatalogValidationResult valid =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(aliases: chain));
            CustomizationCatalogValidationResult invalid =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(aliases: cycle));

            Assert.That(valid.IsValid, Is.True);
            Assert.That(valid.Snapshot.TryGetAlias(
                CustomizationFamilies.HairStyles,
                "very_old_hair",
                out CustomizationAliasDefinition flattened), Is.True);
            Assert.That(flattened.NewId, Is.EqualTo("short"));
            Assert.That(flattened.RequiresUserConfirmation, Is.True);
            Assert.That(invalid.IsValid, Is.False);
            Assert.That(invalid.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-ALIAS-CYCLE"));
        }

        [Test]
        public void EmptyAliasSetIsValidButInvalidPresetReferenceRejects()
        {
            CustomizationCatalogValidationResult noAliases =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(
                        aliases: Array.Empty<CustomizationAliasCandidate>()));
            CustomizationPresetCandidate[] presets =
                CustomizationPlannerTestFixtures.Presets();
            presets[0] = new CustomizationPresetCandidate(
                "vanguard", "customization.preset.vanguard.name",
                CustomizationField.WeaponStyle,
                CustomizationPlannerTestFixtures.Values(weapon: "unknown_weapon"),
                Array.Empty<string>());
            CustomizationCatalogValidationResult badPreset =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(presets: presets));

            Assert.That(noAliases.IsValid, Is.True);
            Assert.That(badPreset.IsValid, Is.False);
            Assert.That(badPreset.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-PRESET-REFERENCE"));
        }

        [Test]
        public void PalettePresetRemainsValidWhenArbitraryExactColorsAreDisabled()
        {
            var policy = new CustomizationPolicyCandidate(
                CustomizationPlannerTestFixtures.Values(),
                new CustomizationScale(0.5f, 0.5f, 0.5f),
                new CustomizationScale(2f, 2f, 2f),
                CustomizationPlannerTestFixtures.Placeholders(),
                false);
            var palettePreset = new CustomizationPresetCandidate(
                "palette_only", "customization.preset.palette_only.name",
                CustomizationField.PrimaryColor,
                CustomizationPlannerTestFixtures.Values(
                    primary: CustomizationPlannerTestFixtures.Blue),
                new[] { "cap_primary" });
            var arbitraryPreset = new CustomizationPresetCandidate(
                "arbitrary", "customization.preset.arbitrary.name",
                CustomizationField.PrimaryColor,
                CustomizationPlannerTestFixtures.Values(
                    primary: new CustomizationColor(0.123f, 0.456f, 0.789f)),
                new[] { "cap_primary" });

            CustomizationCatalogValidationResult allowed =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(
                        presets: new[] { palettePreset }, policy: policy));
            CustomizationCatalogValidationResult rejected =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(
                        presets: new[] { arbitraryPreset }, policy: policy));

            Assert.That(allowed.IsValid, Is.True);
            Assert.That(rejected.IsValid, Is.False);
            Assert.That(rejected.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-PRESET-EXACT-COLOR-DISALLOWED"));
        }

        [Test]
        public void PresetMustDeclareCapabilitiesRequiredBySelectedOptions()
        {
            var preset = new CustomizationPresetCandidate(
                "missing_capability",
                "customization.preset.missing_capability.name",
                CustomizationField.HairStyle,
                CustomizationPlannerTestFixtures.Values(hair: "long"),
                Array.Empty<string>());

            CustomizationCatalogValidationResult result =
                CustomizationCatalogValidator.Validate(
                    CustomizationPlannerTestFixtures.Candidate(
                        presets: new[] { preset }));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Contains.Item("AL-CUS-PRESET-CAPABILITY-REFERENCE"));
        }

        [TestCase("")]
        [TestCase("Upper")]
        [TestCase("bad__id")]
        [TestCase("bad-id")]
        [TestCase("bad_id\n")]
        public void TechnicalIdGrammarRejectsAmbiguousValues(string id)
        {
            Assert.That(CustomizationCatalogValidator.IsTechnicalId(id), Is.False);
        }

        private static IEnumerable<T> HostileOversized<T>(
            T value,
            int maximum,
            Action onRead)
        {
            for (int index = 0; index <= maximum; index++)
            {
                onRead();
                yield return value;
            }

            throw new InvalidOperationException(
                "The bounded copy enumerated beyond limit plus one.");
        }

        private sealed class HostilePlaceholderDictionary :
            IReadOnlyDictionary<string, string>
        {
            private readonly Action _onRead;

            internal HostilePlaceholderDictionary(Action onRead)
            {
                _onRead = onRead;
            }

            public int Count => throw new NotSupportedException();
            public IEnumerable<string> Keys => throw new NotSupportedException();
            public IEnumerable<string> Values => throw new NotSupportedException();
            public string this[string key] => throw new NotSupportedException();

            public bool ContainsKey(string key)
            {
                throw new NotSupportedException();
            }

            public bool TryGetValue(string key, out string value)
            {
                throw new NotSupportedException();
            }

            public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
            {
                return Enumerate().GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private IEnumerable<KeyValuePair<string, string>> Enumerate()
            {
                for (int index = 0;
                     index <= CustomizationTechnicalLimits.MaximumPlaceholderOptions;
                     index++)
                {
                    _onRead();
                    yield return new KeyValuePair<string, string>(
                        "unexpected_" + index,
                        "unknown");
                }

                throw new InvalidOperationException(
                    "The bounded copy enumerated beyond limit plus one.");
            }
        }
    }
}
