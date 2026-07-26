using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AL.ChampionMode.C1;
using NUnit.Framework;

namespace AL.Tests.EditMode.ChampionCombat.C1
{
    public sealed class ChampionCombatProfileValidationTests
    {
        [Test]
        public void RepresentativeObservedProfile_IsValidAtExactProvenance()
        {
            ChampionCombatProfile profile = CombatContractTestData.CreateProfile();

            CombatValidationResult result = ChampionCombatProfileValidator.Validate(
                profile,
                CombatContractTestData.CreateReferences(),
                CombatContractTestData.HashA);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.IsBlocked, Is.False);
            Assert.That(result.Diagnostics, Is.Empty);
        }

        [Test]
        public void MissingProfile_IsBlockedWithoutThrowing()
        {
            CombatValidationResult result = null;
            Assert.DoesNotThrow(() =>
                result = ChampionCombatProfileValidator.Validate(
                    null,
                    CombatContractTestData.CreateReferences()));

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.Diagnostics.Select(item => item.Code),
                Does.Contain("AL-CHAMPION-PROFILE-MISSING"));
        }

        [Test]
        public void ProfileNumericMatrix_RejectsZeroNegativeAndOverCeilingAsSpecified()
        {
            var invalidCases = new[]
            {
                new ProfileCase(
                    "max health zero",
                    CombatContractTestData.CreateProfile(maxHealthMicros: 0L),
                    "AL-CHAMPION-PROFILE-MAX-HEALTH"),
                new ProfileCase(
                    "max health negative",
                    CombatContractTestData.CreateProfile(maxHealthMicros: -1L),
                    "AL-CHAMPION-PROFILE-MAX-HEALTH"),
                new ProfileCase(
                    "max health over",
                    CombatContractTestData.CreateProfile(
                        maxHealthMicros:
                            CombatTechnicalLimits.HealthManaDamageHealingAttackPowerMaximumMicros + 1L),
                    "AL-CHAMPION-PROFILE-MAX-HEALTH"),
                new ProfileCase(
                    "max mana zero",
                    CombatContractTestData.CreateProfile(maxManaMicros: 0L),
                    "AL-CHAMPION-PROFILE-MAX-MANA"),
                new ProfileCase(
                    "max mana negative",
                    CombatContractTestData.CreateProfile(maxManaMicros: -1L),
                    "AL-CHAMPION-PROFILE-MAX-MANA"),
                new ProfileCase(
                    "max mana over",
                    CombatContractTestData.CreateProfile(
                        maxManaMicros:
                            CombatTechnicalLimits.HealthManaDamageHealingAttackPowerMaximumMicros + 1L),
                    "AL-CHAMPION-PROFILE-MAX-MANA"),
                new ProfileCase(
                    "regen negative",
                    CombatContractTestData.CreateProfile(manaRegenMicros: -1L),
                    "AL-CHAMPION-PROFILE-MANA-REGEN"),
                new ProfileCase(
                    "regen over",
                    CombatContractTestData.CreateProfile(
                        manaRegenMicros:
                            CombatTechnicalLimits.RegenerationRateMaximumMicros + 1L),
                    "AL-CHAMPION-PROFILE-MANA-REGEN"),
                new ProfileCase(
                    "attack negative",
                    CombatContractTestData.CreateProfile(attackPowerMicros: -1L),
                    "AL-CHAMPION-PROFILE-ATTACK-POWER"),
                new ProfileCase(
                    "attack over",
                    CombatContractTestData.CreateProfile(
                        attackPowerMicros:
                            CombatTechnicalLimits.HealthManaDamageHealingAttackPowerMaximumMicros + 1L),
                    "AL-CHAMPION-PROFILE-ATTACK-POWER")
            };

            foreach (ProfileCase invalidCase in invalidCases)
            {
                CombatValidationResult result = ChampionCombatProfileValidator.Validate(
                    invalidCase.Profile,
                    CombatContractTestData.CreateReferences());
                Assert.That(result.IsValid, Is.False, invalidCase.Name);
                Assert.That(
                    result.Diagnostics.Select(item => item.Code),
                    Does.Contain(invalidCase.Code),
                    invalidCase.Name);
            }

            ChampionCombatProfile zeroOptionalValues =
                CombatContractTestData.CreateProfile(
                    manaRegenMicros: 0L,
                    attackPowerMicros: 0L);
            Assert.That(
                ChampionCombatProfileValidator.Validate(
                    zeroOptionalValues,
                    CombatContractTestData.CreateReferences()).IsValid,
                Is.True);
        }

        [Test]
        public void ProfileIdentityVersionReferenceAndProvenanceMatrix_FailsClosed()
        {
            var invalidCases = new[]
            {
                new ProfileCase(
                    "blank id",
                    CombatContractTestData.CreateProfile(id: ""),
                    "AL-CHAMPION-PROFILE-ID"),
                new ProfileCase(
                    "unsupported schema",
                    CombatContractTestData.CreateProfile(schemaVersion: "2"),
                    "AL-CHAMPION-PROFILE-SCHEMA-VERSION"),
                new ProfileCase(
                    "unsupported content",
                    CombatContractTestData.CreateProfile(contentVersion: "combat-v2"),
                    "AL-CHAMPION-PROFILE-CONTENT-VERSION"),
                new ProfileCase(
                    "wrong catalog",
                    CombatContractTestData.CreateProfile(catalogSetId: "other.catalog"),
                    "AL-CHAMPION-PROFILE-CATALOG-SET"),
                new ProfileCase(
                    "behavior",
                    CombatContractTestData.CreateProfile(
                        behaviorProfileId: "combat.behavior.unknown"),
                    "AL-CHAMPION-PROFILE-REFERENCE"),
                new ProfileCase(
                    "movement",
                    CombatContractTestData.CreateProfile(
                        movementProfileId: "combat.movement.unknown"),
                    "AL-CHAMPION-PROFILE-REFERENCE"),
                new ProfileCase(
                    "dodge",
                    CombatContractTestData.CreateProfile(
                        dodgeProfileId: "combat.dodge.unknown"),
                    "AL-CHAMPION-PROFILE-REFERENCE"),
                new ProfileCase(
                    "target",
                    CombatContractTestData.CreateProfile(
                        targetingProfileId: "combat.target.unknown"),
                    "AL-CHAMPION-PROFILE-REFERENCE"),
                new ProfileCase(
                    "source",
                    CombatContractTestData.CreateProfile(sourceRevision: "bad revision"),
                    "AL-CHAMPION-PROFILE-SOURCE-REVISION"),
                new ProfileCase(
                    "hash shape",
                    CombatContractTestData.CreateProfile(rawSha256: ""),
                    "AL-CHAMPION-PROFILE-HASH")
            };

            foreach (ProfileCase invalidCase in invalidCases)
            {
                CombatValidationResult result = ChampionCombatProfileValidator.Validate(
                    invalidCase.Profile,
                    CombatContractTestData.CreateReferences());
                Assert.That(result.IsValid, Is.False, invalidCase.Name);
                Assert.That(
                    result.Diagnostics.Select(item => item.Code),
                    Does.Contain(invalidCase.Code),
                    invalidCase.Name);
            }

            CombatValidationResult mismatch = ChampionCombatProfileValidator.Validate(
                CombatContractTestData.CreateProfile(),
                CombatContractTestData.CreateReferences(),
                CombatContractTestData.HashB);
            Assert.That(mismatch.IsValid, Is.False);
            Assert.That(
                mismatch.Diagnostics.Select(item => item.Code),
                Does.Contain("AL-CHAMPION-PROFILE-HASH"));
        }

        [Test]
        public void ReferenceCatalog_RequiresSupportedSchemaAndBoundedVersions()
        {
            Assert.Throws<ArgumentException>(() => CreateReferenceCatalog(
                schemaVersion: "2"));
            Assert.Throws<ArgumentException>(() => CreateReferenceCatalog(
                contentVersions: new[]
                {
                    new string('v', CombatTechnicalLimits.MaximumVersionUtf8Bytes + 1)
                }));
            Assert.Throws<ArgumentException>(() => CreateReferenceCatalog(
                championIds: new string[CombatTechnicalLimits.MaximumReferenceEntries + 1]));
        }

        [Test]
        public void ReferenceCatalog_CopiesEveryInputCollection()
        {
            var versions = new[] { CombatContractTestData.ContentVersion };
            var championIds = new[] { CombatContractTestData.ChampionProfileId };
            var behaviors = new[]
            {
                new CombatSkillBehaviorReference(
                    "combat.behavior.damage",
                    CombatSkillBehaviorKind.Damage)
            };
            var targeting = new[]
            {
                new CombatTargetingReference(
                    "combat.target.hostile",
                    CombatTargetDisposition.Hostile,
                    CombatTargetIntentKind.AreaProfile,
                    "unit.meter",
                    "area.standard",
                    true,
                    "target.profile.standard")
            };
            var resource = new[] { "combat.resource.standard" };
            var cooldown = new[] { "combat.cooldown.standard" };
            var presentation = new[] { "combat.presentation.test" };
            var movement = new[] { "combat.movement.test" };
            var dodge = new[] { "combat.dodge.test" };
            var catalog = new CombatContractReferenceCatalog(
                CombatContractTestData.CatalogSetId,
                CombatContractTestData.SchemaVersion,
                versions,
                championIds,
                behaviors,
                targeting,
                resource,
                cooldown,
                presentation,
                movement,
                dodge);

            versions[0] = "changed";
            championIds[0] = "changed";
            behaviors[0] = new CombatSkillBehaviorReference(
                "changed",
                CombatSkillBehaviorKind.Utility);
            targeting[0] = new CombatTargetingReference(
                "changed",
                CombatTargetDisposition.Any,
                CombatTargetIntentKind.Point,
                "unit.meter",
                string.Empty,
                false);
            resource[0] = "changed";
            cooldown[0] = "changed";
            presentation[0] = "changed";
            movement[0] = "changed";
            dodge[0] = "changed";

            Assert.That(catalog.SupportsContentVersion(CombatContractTestData.ContentVersion), Is.True);
            Assert.That(catalog.ContainsChampionOrClassProfile(
                CombatContractTestData.ChampionProfileId), Is.True);
            Assert.That(catalog.TryGetBehavior("combat.behavior.damage", out _), Is.True);
            Assert.That(catalog.TryGetTargeting("combat.target.hostile", out _), Is.True);
            Assert.That(catalog.ContainsResourcePolicy("combat.resource.standard"), Is.True);
            Assert.That(catalog.ContainsCooldownPolicy("combat.cooldown.standard"), Is.True);
            Assert.That(catalog.ContainsPresentationProfile("combat.presentation.test"), Is.True);
            Assert.That(catalog.ContainsMovementProfile("combat.movement.test"), Is.True);
            Assert.That(catalog.ContainsDodgeProfile("combat.dodge.test"), Is.True);
            Assert.That(catalog.SupportsContentVersion("changed"), Is.False);
        }

        [Test]
        public void FoundationalContractRecords_AreSealedAndExposeNoMutableProperties()
        {
            Type[] contractTypes =
            {
                typeof(ChampionCombatProfile),
                typeof(CombatSkillDefinition),
                typeof(CombatSkillSlotBinding),
                typeof(CombatSkillLoadout),
                typeof(CombatSkillBehaviorReference),
                typeof(CombatTargetingReference),
                typeof(CombatContractReferenceCatalog),
                typeof(CombatDiagnostic),
                typeof(CombatValidationResult)
            };

            foreach (Type contractType in contractTypes)
            {
                Assert.That(contractType.IsSealed, Is.True, contractType.FullName);
                Assert.That(
                    contractType.GetFields(BindingFlags.Instance | BindingFlags.Public),
                    Is.Empty,
                    contractType.FullName);
                Assert.That(
                    contractType
                        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Where(property => property.SetMethod != null),
                    Is.Empty,
                    contractType.FullName);
            }
        }

        [Test]
        public void TargetingReferencesRequireExactShapeRangeAreaAndDisposition()
        {
            var invalid = new[]
            {
                new CombatTargetingReference(
                    "target.undefined-intent",
                    CombatTargetDisposition.Hostile,
                    (CombatTargetIntentKind)999,
                    "unit.meter",
                    string.Empty,
                    false),
                new CombatTargetingReference(
                    "target.blank-unit",
                    CombatTargetDisposition.Hostile,
                    CombatTargetIntentKind.ParticipantId,
                    string.Empty,
                    string.Empty,
                    false),
                new CombatTargetingReference(
                    "target.area-missing-shape",
                    CombatTargetDisposition.Hostile,
                    CombatTargetIntentKind.AreaProfile,
                    "unit.meter",
                    string.Empty,
                    false),
                new CombatTargetingReference(
                    "target.point-with-area",
                    CombatTargetDisposition.Any,
                    CombatTargetIntentKind.Point,
                    "unit.meter",
                    "area.unexpected",
                    false),
                new CombatTargetingReference(
                    "target.self-wrong-intent",
                    CombatTargetDisposition.Self,
                    CombatTargetIntentKind.ParticipantId,
                    "unit.meter",
                    string.Empty,
                    false),
                new CombatTargetingReference(
                    "target.self-wrong-disposition",
                    CombatTargetDisposition.Hostile,
                    CombatTargetIntentKind.Self,
                    "unit.meter",
                    string.Empty,
                    false)
            };

            foreach (CombatTargetingReference targeting in invalid)
            {
                Assert.Throws<ArgumentException>(
                    () => CreateReferenceCatalog(
                        targeting: new[] { targeting }),
                    targeting.Id);
            }
        }

        [Test]
        public void InvalidProfileDiagnostics_AreStableAcrossRepeatedValidation()
        {
            ChampionCombatProfile invalid = CombatContractTestData.CreateProfile(
                id: "",
                schemaVersion: "2",
                maxHealthMicros: -1L,
                behaviorProfileId: "missing",
                rawSha256: "bad");

            string[] first = ChampionCombatProfileValidator.Validate(
                    invalid,
                    CombatContractTestData.CreateReferences())
                .Diagnostics
                .Select(ToStableDiagnosticProjection)
                .ToArray();
            string[] second = ChampionCombatProfileValidator.Validate(
                    invalid,
                    CombatContractTestData.CreateReferences())
                .Diagnostics
                .Select(ToStableDiagnosticProjection)
                .ToArray();

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Is.Ordered.Using<string>(StringComparer.Ordinal));
        }

        private static string ToStableDiagnosticProjection(CombatDiagnostic diagnostic)
        {
            return diagnostic.Code + "|" + diagnostic.FieldPath + "|" + diagnostic.Message;
        }

        private static CombatContractReferenceCatalog CreateReferenceCatalog(
            string schemaVersion = CombatContractTestData.SchemaVersion,
            IList<string> contentVersions = null,
            IList<string> championIds = null,
            IList<CombatTargetingReference> targeting = null)
        {
            return new CombatContractReferenceCatalog(
                CombatContractTestData.CatalogSetId,
                schemaVersion,
                contentVersions ?? new[] { CombatContractTestData.ContentVersion },
                championIds ?? new[] { CombatContractTestData.ChampionProfileId },
                new[]
                {
                    new CombatSkillBehaviorReference(
                        "combat.behavior.damage",
                        CombatSkillBehaviorKind.Damage)
                },
                targeting ?? new[]
                {
                    new CombatTargetingReference(
                        "combat.target.hostile",
                        CombatTargetDisposition.Hostile,
                        CombatTargetIntentKind.AreaProfile,
                        "unit.meter",
                        "area.standard",
                        true,
                        "target.profile.standard")
                },
                new[] { "combat.resource.standard" },
                new[] { "combat.cooldown.standard" },
                new[] { "combat.presentation.test" },
                new[] { "combat.movement.test" },
                new[] { "combat.dodge.test" });
        }

        private sealed class ProfileCase
        {
            internal ProfileCase(
                string name,
                ChampionCombatProfile profile,
                string code)
            {
                Name = name;
                Profile = profile;
                Code = code;
            }

            internal string Name { get; }
            internal ChampionCombatProfile Profile { get; }
            internal string Code { get; }
        }
    }
}
