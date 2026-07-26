using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AL.ChampionMode.C1;
using NUnit.Framework;

namespace AL.Tests.EditMode.ChampionCombat.C1
{
    public sealed class CombatSkillAndLoadoutValidationTests
    {
        [Test]
        public void RepresentativeDamageHealingAndBreakSkills_AreValid()
        {
            CombatContractReferenceCatalog references =
                CombatContractTestData.CreateReferences();
            IReadOnlyDictionary<string, CombatSkillDefinition> skills =
                CombatContractTestData.CreateSkills();

            foreach (CombatSkillDefinition skill in skills.Values)
            {
                CombatValidationResult result =
                    CombatSkillDefinitionValidator.Validate(
                        skill,
                        references,
                        CombatContractTestData.HashA);
                Assert.That(result.IsValid, Is.True, skill.Id);
                Assert.That(result.Diagnostics, Is.Empty, skill.Id);
            }
        }

        [Test]
        public void SkillNumericMatrix_RejectsNegativeAndOverCeilingValues()
        {
            var invalidCases = new[]
            {
                new SkillCase(
                    "mana negative",
                    CombatContractTestData.CreateSkill(manaCostMicros: -1L),
                    "AL-SKILL-CATALOG-MANA-COST"),
                new SkillCase(
                    "mana over",
                    CombatContractTestData.CreateSkill(
                        manaCostMicros:
                            CombatTechnicalLimits.HealthManaDamageHealingAttackPowerMaximumMicros + 1L),
                    "AL-SKILL-CATALOG-MANA-COST"),
                new SkillCase(
                    "cast negative",
                    CombatContractTestData.CreateSkill(castDurationMicros: -1L),
                    "AL-SKILL-CATALOG-CAST-DURATION"),
                new SkillCase(
                    "cast over",
                    CombatContractTestData.CreateSkill(
                        castDurationMicros:
                            CombatTechnicalLimits.DurationMaximumMicros + 1L),
                    "AL-SKILL-CATALOG-CAST-DURATION"),
                new SkillCase(
                    "cooldown negative",
                    CombatContractTestData.CreateSkill(cooldownDurationMicros: -1L),
                    "AL-SKILL-CATALOG-COOLDOWN-DURATION"),
                new SkillCase(
                    "cooldown over",
                    CombatContractTestData.CreateSkill(
                        cooldownDurationMicros:
                            CombatTechnicalLimits.DurationMaximumMicros + 1L),
                    "AL-SKILL-CATALOG-COOLDOWN-DURATION"),
                new SkillCase(
                    "range negative",
                    CombatContractTestData.CreateSkill(rangeMicros: -1L),
                    "AL-SKILL-CATALOG-RANGE"),
                new SkillCase(
                    "range over",
                    CombatContractTestData.CreateSkill(
                        rangeMicros:
                            CombatTechnicalLimits.WorldDistanceMaximumMicros + 1L),
                    "AL-SKILL-CATALOG-RANGE"),
                new SkillCase(
                    "power negative",
                    CombatContractTestData.CreateSkill(powerMicros: -1L),
                    "AL-SKILL-CATALOG-POWER"),
                new SkillCase(
                    "power over",
                    CombatContractTestData.CreateSkill(
                        powerMicros:
                            CombatTechnicalLimits.HealthManaDamageHealingAttackPowerMaximumMicros + 1L),
                    "AL-SKILL-CATALOG-POWER"),
                new SkillCase(
                    "multiplier negative",
                    CombatContractTestData.CreateSkill(botPowerMultiplierMicros: -1L),
                    "AL-SKILL-CATALOG-BOT-MULTIPLIER"),
                new SkillCase(
                    "multiplier over",
                    CombatContractTestData.CreateSkill(
                        botPowerMultiplierMicros:
                            CombatTechnicalLimits.MultiplierMaximumMicros + 1L),
                    "AL-SKILL-CATALOG-BOT-MULTIPLIER")
            };

            foreach (SkillCase invalidCase in invalidCases)
            {
                CombatValidationResult result =
                    CombatSkillDefinitionValidator.Validate(
                        invalidCase.Skill,
                        CombatContractTestData.CreateReferences());
                Assert.That(result.IsValid, Is.False, invalidCase.Name);
                Assert.That(
                    result.Diagnostics.Select(item => item.Code),
                    Does.Contain(invalidCase.Code),
                    invalidCase.Name);
            }
        }

        [Test]
        public void UtilitySkill_AllowsExplicitZeroNumericFields()
        {
            CombatSkillDefinition utility = CombatContractTestData.CreateSkill(
                behaviorProfileId: "combat.behavior.utility",
                targetingProfileId: "combat.target.any",
                manaCostMicros: 0L,
                castDurationMicros: 0L,
                cooldownDurationMicros: 0L,
                rangeMicros: 0L,
                powerMicros: 0L,
                botPowerMultiplierMicros: 0L);

            Assert.That(
                CombatSkillDefinitionValidator.Validate(
                    utility,
                    CombatContractTestData.CreateReferences()).IsValid,
                Is.True);
        }

        [Test]
        public void SkillIdentityVersionReferenceAndHashMatrix_FailsClosed()
        {
            var invalidCases = new[]
            {
                new SkillCase(
                    "id",
                    CombatContractTestData.CreateSkill(id: ""),
                    "AL-SKILL-CATALOG-ID"),
                new SkillCase(
                    "schema",
                    CombatContractTestData.CreateSkill(schemaVersion: "2"),
                    "AL-SKILL-CATALOG-SCHEMA-VERSION"),
                new SkillCase(
                    "content",
                    CombatContractTestData.CreateSkill(contentVersion: "combat-v2"),
                    "AL-SKILL-CATALOG-CONTENT-VERSION"),
                new SkillCase(
                    "behavior",
                    CombatContractTestData.CreateSkill(
                        behaviorProfileId: "combat.behavior.unknown"),
                    "AL-SKILL-CATALOG-REFERENCE"),
                new SkillCase(
                    "target",
                    CombatContractTestData.CreateSkill(
                        targetingProfileId: "combat.target.unknown"),
                    "AL-SKILL-CATALOG-REFERENCE"),
                new SkillCase(
                    "resource",
                    CombatContractTestData.CreateSkill(
                        resourcePolicyId: "combat.resource.unknown"),
                    "AL-SKILL-CATALOG-REFERENCE"),
                new SkillCase(
                    "cooldown",
                    CombatContractTestData.CreateSkill(
                        cooldownPolicyId: "combat.cooldown.unknown"),
                    "AL-SKILL-CATALOG-REFERENCE"),
                new SkillCase(
                    "presentation",
                    CombatContractTestData.CreateSkill(
                        presentationProfileId: "combat.presentation.unknown"),
                    "AL-SKILL-CATALOG-REFERENCE"),
                new SkillCase(
                    "source",
                    CombatContractTestData.CreateSkill(sourceRevision: "bad revision"),
                    "AL-SKILL-CATALOG-SOURCE-REVISION"),
                new SkillCase(
                    "hash",
                    CombatContractTestData.CreateSkill(rawSha256: "bad"),
                    "AL-SKILL-CATALOG-HASH")
            };

            foreach (SkillCase invalidCase in invalidCases)
            {
                CombatValidationResult result =
                    CombatSkillDefinitionValidator.Validate(
                        invalidCase.Skill,
                        CombatContractTestData.CreateReferences());
                Assert.That(result.IsValid, Is.False, invalidCase.Name);
                Assert.That(
                    result.Diagnostics.Select(item => item.Code),
                    Does.Contain(invalidCase.Code),
                    invalidCase.Name);
            }

            CombatValidationResult mismatch =
                CombatSkillDefinitionValidator.Validate(
                    CombatContractTestData.CreateSkill(),
                    CombatContractTestData.CreateReferences(),
                    CombatContractTestData.HashB);
            Assert.That(mismatch.IsValid, Is.False);
            Assert.That(
                mismatch.Diagnostics.Select(item => item.Code),
                Does.Contain("AL-SKILL-CATALOG-HASH"));
        }

        [Test]
        public void BehaviorTargetCompatibility_IsExplicitAndDoesNotTreatAnyAsFriendly()
        {
            CombatSkillDefinition[] invalid =
            {
                CombatContractTestData.CreateSkill(
                    behaviorProfileId: "combat.behavior.heal",
                    targetingProfileId: "combat.target.hostile",
                    rangeMicros: 0L),
                CombatContractTestData.CreateSkill(
                    behaviorProfileId: "combat.behavior.heal",
                    targetingProfileId: "combat.target.any",
                    rangeMicros: 0L),
                CombatContractTestData.CreateSkill(
                    behaviorProfileId: "combat.behavior.damage",
                    targetingProfileId: "combat.target.self"),
                CombatContractTestData.CreateSkill(
                    behaviorProfileId: "combat.behavior.damage",
                    targetingProfileId: "combat.target.friendly"),
                CombatContractTestData.CreateSkill(rangeMicros: 0L),
                CombatContractTestData.CreateSkill(powerMicros: 0L)
            };

            foreach (CombatSkillDefinition skill in invalid)
            {
                CombatValidationResult result =
                    CombatSkillDefinitionValidator.Validate(
                        skill,
                        CombatContractTestData.CreateReferences());
                Assert.That(result.IsValid, Is.False);
                Assert.That(
                    result.Diagnostics.Select(item => item.Code),
                    Does.Contain("AL-SKILL-CATALOG-BEHAVIOR-TARGET-CONFLICT"));
            }

            CombatSkillDefinition friendlyHeal = CombatContractTestData.CreateSkill(
                behaviorProfileId: "combat.behavior.heal",
                targetingProfileId: "combat.target.friendly",
                rangeMicros: 0L);
            Assert.That(
                CombatSkillDefinitionValidator.Validate(
                    friendlyHeal,
                    CombatContractTestData.CreateReferences()).IsValid,
                Is.True);
        }

        [Test]
        public void CompleteFourSlotSnapshot_IsValidAndSlotDoesNotOwnBehavior()
        {
            IReadOnlyDictionary<string, CombatSkillDefinition> skills =
                CombatContractTestData.CreateSkills();
            var movedSlots = new List<CombatSkillSlotBinding>
            {
                new CombatSkillSlotBinding(
                    0,
                    "skill.two",
                    CombatContractTestData.ContentVersion,
                    availabilityProfileId: "combat.availability.test"),
                new CombatSkillSlotBinding(1, "skill.one", CombatContractTestData.ContentVersion),
                new CombatSkillSlotBinding(2, "skill.three", CombatContractTestData.ContentVersion),
                new CombatSkillSlotBinding(3, "skill.four", CombatContractTestData.ContentVersion)
            };

            CombatSkillLoadoutValidationResult result =
                CombatSkillLoadoutValidator.Validate(
                CombatContractTestData.CreateLoadout(movedSlots),
                skills,
                CombatContractTestData.CreateExpectedSkillHashes(),
                CombatContractTestData.CreateReferences(),
                CombatContractTestData.HashA);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Snapshot, Is.Not.Null);
            Assert.That(
                result.Snapshot.CatalogSetId,
                Is.EqualTo(CombatContractTestData.CatalogSetId));
            Assert.That(
                result.Snapshot.TrustedLoadoutRawSha256,
                Is.EqualTo(CombatContractTestData.HashA));
            Assert.That(
                result.Snapshot.TrustedSkillRawSha256InSlotOrder,
                Is.EqualTo(Enumerable.Repeat(
                    CombatContractTestData.HashA,
                    CombatSkillLoadout.RequiredSlotCount)));
            Assert.That(
                result.Snapshot.SkillsInSlotOrder.Select(skill => skill.Id),
                Is.EqualTo(new[]
                {
                    "skill.two",
                    "skill.one",
                    "skill.three",
                    "skill.four"
                }));
            Assert.That(skills["skill.two"].BehaviorProfileId, Is.EqualTo("combat.behavior.heal"));
            Assert.That(skills["skill.one"].BehaviorProfileId, Is.EqualTo("combat.behavior.damage"));
        }

        [Test]
        public void LoadoutPublicationUsesTheExactSkillObjectsValidatedOnce()
        {
            IReadOnlyDictionary<string, CombatSkillDefinition> valid =
                CombatContractTestData.CreateSkills();
            CombatSkillDefinition substituted =
                CombatContractTestData.CreateSkill(
                    "skill.one",
                    rangeMicros:
                        CombatTechnicalLimits
                            .WorldDistanceMaximumMicros);
            var switching = new SwitchingSkillDictionary(
                valid,
                "skill.one",
                substituted);

            CombatSkillLoadoutValidationResult result =
                CombatSkillLoadoutValidator.Validate(
                    CombatContractTestData.CreateLoadout(),
                    switching,
                    CombatContractTestData
                        .CreateExpectedSkillHashes(),
                    CombatContractTestData.CreateReferences(),
                    CombatContractTestData.HashA);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Snapshot, Is.Not.Null);
            Assert.That(
                result.Snapshot.SkillsInSlotOrder[0],
                Is.SameAs(valid["skill.one"]));
            Assert.False(
                result.Snapshot.SkillsInSlotOrder.Any(
                    skill => ReferenceEquals(
                        skill,
                        substituted)));
            Assert.That(
                switching.EnumerationCount,
                Is.EqualTo(
                    CombatSkillLoadout.RequiredSlotCount));
        }

        [Test]
        public void LoadoutSlotMatrix_RejectsMissingDuplicateOutOfRangeNullAndExtraBindings()
        {
            var cases = new List<LoadoutCase>
            {
                new LoadoutCase(
                    "missing",
                    CombatContractTestData.CreateSlots().Take(3).ToList(),
                    "AL-SKILL-LOADOUT-SLOT-COUNT"),
                new LoadoutCase(
                    "duplicate slot",
                    new List<CombatSkillSlotBinding>
                    {
                        new CombatSkillSlotBinding(0, "skill.one", CombatContractTestData.ContentVersion),
                        new CombatSkillSlotBinding(0, "skill.two", CombatContractTestData.ContentVersion),
                        new CombatSkillSlotBinding(2, "skill.three", CombatContractTestData.ContentVersion),
                        new CombatSkillSlotBinding(3, "skill.four", CombatContractTestData.ContentVersion)
                    },
                    "AL-SKILL-LOADOUT-DUPLICATE-SLOT"),
                new LoadoutCase(
                    "out of range",
                    new List<CombatSkillSlotBinding>
                    {
                        new CombatSkillSlotBinding(0, "skill.one", CombatContractTestData.ContentVersion),
                        new CombatSkillSlotBinding(1, "skill.two", CombatContractTestData.ContentVersion),
                        new CombatSkillSlotBinding(2, "skill.three", CombatContractTestData.ContentVersion),
                        new CombatSkillSlotBinding(4, "skill.four", CombatContractTestData.ContentVersion)
                    },
                    "AL-SKILL-LOADOUT-SLOT-RANGE"),
                new LoadoutCase(
                    "null",
                    new List<CombatSkillSlotBinding>
                    {
                        new CombatSkillSlotBinding(0, "skill.one", CombatContractTestData.ContentVersion),
                        null,
                        new CombatSkillSlotBinding(2, "skill.three", CombatContractTestData.ContentVersion),
                        new CombatSkillSlotBinding(3, "skill.four", CombatContractTestData.ContentVersion)
                    },
                    "AL-SKILL-LOADOUT-NULL-SLOT"),
                new LoadoutCase(
                    "extra",
                    new List<CombatSkillSlotBinding>
                    {
                        new CombatSkillSlotBinding(0, "skill.one", CombatContractTestData.ContentVersion),
                        new CombatSkillSlotBinding(1, "skill.two", CombatContractTestData.ContentVersion),
                        new CombatSkillSlotBinding(2, "skill.three", CombatContractTestData.ContentVersion),
                        new CombatSkillSlotBinding(3, "skill.four", CombatContractTestData.ContentVersion),
                        new CombatSkillSlotBinding(4, "skill.extra", CombatContractTestData.ContentVersion)
                    },
                    "AL-SKILL-LOADOUT-SLOT-COUNT")
            };

            foreach (LoadoutCase invalidCase in cases)
            {
                CombatSkillLoadoutValidationResult result =
                    CombatSkillLoadoutValidator.Validate(
                    CombatContractTestData.CreateLoadout(invalidCase.Slots),
                    CombatContractTestData.CreateSkills(),
                    CombatContractTestData.CreateExpectedSkillHashes(),
                    CombatContractTestData.CreateReferences(),
                    CombatContractTestData.HashA);
                Assert.That(result.IsValid, Is.False, invalidCase.Name);
                Assert.That(
                    result.Diagnostics.Select(item => item.Code),
                    Does.Contain(invalidCase.Code),
                    invalidCase.Name);
            }
        }

        [Test]
        public void LoadoutReferenceMatrix_RejectsDuplicateUnknownVersionAndDictionaryIdentityDrift()
        {
            var duplicateSkill = CombatContractTestData.CreateSlots();
            duplicateSkill[1] = new CombatSkillSlotBinding(
                1,
                "skill.one",
                CombatContractTestData.ContentVersion);
            AssertCode(
                duplicateSkill,
                CombatContractTestData.CreateSkills(),
                "AL-SKILL-LOADOUT-DUPLICATE-SKILL");

            var unknown = CombatContractTestData.CreateSlots();
            unknown[0] = new CombatSkillSlotBinding(
                0,
                "skill.unknown",
                CombatContractTestData.ContentVersion);
            AssertCode(
                unknown,
                CombatContractTestData.CreateSkills(),
                "AL-SKILL-LOADOUT-SKILL-REFERENCE");

            var versionMismatch = CombatContractTestData.CreateSlots();
            versionMismatch[0] = new CombatSkillSlotBinding(
                0,
                "skill.one",
                "combat-v2");
            AssertCode(
                versionMismatch,
                CombatContractTestData.CreateSkills(),
                "AL-SKILL-LOADOUT-SKILL-VERSION-MISMATCH");

            var duplicateInput = CombatContractTestData.CreateSlots();
            duplicateInput[1] = new CombatSkillSlotBinding(
                1,
                "skill.two",
                CombatContractTestData.ContentVersion,
                "input.skill.0");
            AssertCode(
                duplicateInput,
                CombatContractTestData.CreateSkills(),
                "AL-SKILL-LOADOUT-INPUT-BINDING");

            var invalidAvailability = CombatContractTestData.CreateSlots();
            invalidAvailability[0] = new CombatSkillSlotBinding(
                0,
                "skill.one",
                CombatContractTestData.ContentVersion,
                availabilityProfileId: "combat.availability.unknown");
            AssertCode(
                invalidAvailability,
                CombatContractTestData.CreateSkills(),
                "AL-SKILL-LOADOUT-AVAILABILITY");

            var mismatchedDictionary =
                new Dictionary<string, CombatSkillDefinition>(
                    CombatContractTestData.CreateSkills(),
                    StringComparer.Ordinal)
                {
                    ["skill.one"] = CombatContractTestData.CreateSkill("different.id")
                };
            AssertCode(
                CombatContractTestData.CreateSlots(),
                mismatchedDictionary,
                "AL-SKILL-LOADOUT-SKILL-IDENTITY-MISMATCH");

            var caseInsensitiveKeyDrift =
                new Dictionary<string, CombatSkillDefinition>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["SKILL.ONE"] = CombatContractTestData.CreateSkill("skill.one"),
                    ["skill.two"] = CombatContractTestData.CreateSkill(
                        "skill.two",
                        behaviorProfileId: "combat.behavior.heal",
                        targetingProfileId: "combat.target.self",
                        rangeMicros: 0L),
                    ["skill.three"] = CombatContractTestData.CreateSkill("skill.three"),
                    ["skill.four"] = CombatContractTestData.CreateSkill(
                        "skill.four",
                        behaviorProfileId: "combat.behavior.break")
                };
            AssertCode(
                CombatContractTestData.CreateSlots(),
                caseInsensitiveKeyDrift,
                "AL-SKILL-LOADOUT-SKILL-REFERENCE");
        }

        [Test]
        public void LoadoutRejectsInvalidReferencedSkillAsOneAtomicSnapshot()
        {
            var skills = new Dictionary<string, CombatSkillDefinition>(
                CombatContractTestData.CreateSkills(),
                StringComparer.Ordinal)
            {
                ["skill.one"] = CombatContractTestData.CreateSkill(
                    "skill.one",
                    rawSha256: "bad")
            };

            CombatSkillLoadoutValidationResult result =
                CombatSkillLoadoutValidator.Validate(
                CombatContractTestData.CreateLoadout(),
                skills,
                CombatContractTestData.CreateExpectedSkillHashes(),
                CombatContractTestData.CreateReferences(),
                CombatContractTestData.HashA);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(
                result.Diagnostics.Select(item => item.Code),
                Does.Contain("AL-SKILL-CATALOG-HASH"));
        }

        [Test]
        public void LoadoutPublicationRequiresTrustedHashForEveryReferencedSkill()
        {
            var missing =
                new Dictionary<string, string>(
                    CombatContractTestData.CreateExpectedSkillHashes(),
                    StringComparer.Ordinal);
            missing.Remove("skill.two");
            CombatSkillLoadoutValidationResult missingResult =
                CombatSkillLoadoutValidator.Validate(
                    CombatContractTestData.CreateLoadout(),
                    CombatContractTestData.CreateSkills(),
                    missing,
                    CombatContractTestData.CreateReferences(),
                    CombatContractTestData.HashA);
            Assert.That(missingResult.IsValid, Is.False);
            Assert.That(missingResult.Snapshot, Is.Null);
            Assert.That(
                missingResult.Diagnostics.Select(item => item.Code),
                Does.Contain("AL-SKILL-LOADOUT-SKILL-PROVENANCE"));

            var mismatched =
                new Dictionary<string, string>(
                    CombatContractTestData.CreateExpectedSkillHashes(),
                    StringComparer.Ordinal)
                {
                    ["skill.two"] = CombatContractTestData.HashB
                };
            CombatSkillLoadoutValidationResult mismatchResult =
                CombatSkillLoadoutValidator.Validate(
                    CombatContractTestData.CreateLoadout(),
                    CombatContractTestData.CreateSkills(),
                    mismatched,
                    CombatContractTestData.CreateReferences(),
                    CombatContractTestData.HashA);
            Assert.That(mismatchResult.IsValid, Is.False);
            Assert.That(mismatchResult.Snapshot, Is.Null);
            Assert.That(
                mismatchResult.Diagnostics.Select(item => item.Code),
                Does.Contain("AL-SKILL-CATALOG-HASH"));

            Assert.Throws<ArgumentNullException>(() =>
                CombatSkillLoadoutValidator.Validate(
                    CombatContractTestData.CreateLoadout(),
                    CombatContractTestData.CreateSkills(),
                    null,
                    CombatContractTestData.CreateReferences(),
                    CombatContractTestData.HashA));

            CombatSkillLoadoutValidationResult missingLoadoutAuthority =
                CombatSkillLoadoutValidator.Validate(
                    CombatContractTestData.CreateLoadout(),
                    CombatContractTestData.CreateSkills(),
                    CombatContractTestData.CreateExpectedSkillHashes(),
                    CombatContractTestData.CreateReferences(),
                    null);
            Assert.That(missingLoadoutAuthority.IsValid, Is.False);
            Assert.That(missingLoadoutAuthority.Snapshot, Is.Null);
            Assert.That(
                missingLoadoutAuthority.Diagnostics.Select(item => item.Code),
                Does.Contain("AL-SKILL-LOADOUT-EXPECTED-HASH"));
        }

        [Test]
        public void LoadoutIdentityVersionProfileAndProvenanceMatrix_FailsClosed()
        {
            var cases = new[]
            {
                new LoadoutValidationCase(
                    "id",
                    CombatContractTestData.CreateLoadout(id: ""),
                    "AL-SKILL-LOADOUT-ID"),
                new LoadoutValidationCase(
                    "schema",
                    CombatContractTestData.CreateLoadout(schemaVersion: "2"),
                    "AL-SKILL-LOADOUT-SCHEMA-VERSION"),
                new LoadoutValidationCase(
                    "content",
                    CombatContractTestData.CreateLoadout(contentVersion: "combat-v2"),
                    "AL-SKILL-LOADOUT-CONTENT-VERSION"),
                new LoadoutValidationCase(
                    "champion",
                    CombatContractTestData.CreateLoadout(
                        championProfileId: "champion.profile.unknown"),
                    "AL-SKILL-LOADOUT-CHAMPION-REFERENCE"),
                new LoadoutValidationCase(
                    "source",
                    CombatContractTestData.CreateLoadout(sourceRevision: "bad revision"),
                    "AL-SKILL-LOADOUT-SOURCE-REVISION"),
                new LoadoutValidationCase(
                    "hash",
                    CombatContractTestData.CreateLoadout(rawSha256: "bad"),
                    "AL-SKILL-LOADOUT-HASH")
            };

            foreach (LoadoutValidationCase invalidCase in cases)
            {
                CombatSkillLoadoutValidationResult result =
                    CombatSkillLoadoutValidator.Validate(
                    invalidCase.Loadout,
                    CombatContractTestData.CreateSkills(),
                    CombatContractTestData.CreateExpectedSkillHashes(),
                    CombatContractTestData.CreateReferences(),
                    CombatContractTestData.HashA);
                Assert.That(result.IsValid, Is.False, invalidCase.Name);
                Assert.That(
                    result.Diagnostics.Select(item => item.Code),
                    Does.Contain(invalidCase.Code),
                    invalidCase.Name);
            }

            CombatSkillLoadoutValidationResult mismatch =
                CombatSkillLoadoutValidator.Validate(
                CombatContractTestData.CreateLoadout(),
                CombatContractTestData.CreateSkills(),
                CombatContractTestData.CreateExpectedSkillHashes(),
                CombatContractTestData.CreateReferences(),
                CombatContractTestData.HashB);
            Assert.That(
                mismatch.Diagnostics.Select(item => item.Code),
                Does.Contain("AL-SKILL-LOADOUT-HASH"));
        }

        [Test]
        public void LoadoutConstructor_CopiesAndReadOnlyWrapsInputSlots()
        {
            List<CombatSkillSlotBinding> input = CombatContractTestData.CreateSlots();
            CombatSkillLoadout loadout = CombatContractTestData.CreateLoadout(input);
            CombatSkillSlotBinding original = input[0];
            input[0] = new CombatSkillSlotBinding(
                0,
                "changed",
                CombatContractTestData.ContentVersion);

            Assert.That(loadout.Slots[0], Is.SameAs(original));
            Assert.That(loadout.Slots[0].SkillDefinitionId, Is.EqualTo("skill.one"));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<CombatSkillSlotBinding>)loadout.Slots)[0] = input[0]);
            Assert.Throws<ArgumentException>(() =>
                CombatContractTestData.CreateLoadout(
                    new CombatSkillSlotBinding[
                        CombatTechnicalLimits.MaximumLoadoutBindings + 1]));
        }

        [Test]
        public void LoadoutValidation_BoundsExternalSkillLookup()
        {
            var skills = new Dictionary<string, CombatSkillDefinition>(
                StringComparer.Ordinal);
            CombatSkillDefinition shared = CombatContractTestData.CreateSkill();
            for (int index = 0;
                 index < CombatTechnicalLimits.MaximumReferenceEntries + 1;
                 index++)
            {
                skills.Add("skill.bound." + index, shared);
            }

            CombatSkillLoadoutValidationResult result =
                CombatSkillLoadoutValidator.Validate(
                CombatContractTestData.CreateLoadout(),
                skills,
                CombatContractTestData.CreateExpectedSkillHashes(),
                CombatContractTestData.CreateReferences(),
                CombatContractTestData.HashA);

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.Diagnostics.Select(item => item.Code),
                Does.Contain("AL-SKILL-LOADOUT-SKILL-CATALOG-COUNT"));
        }

        [Test]
        public void LoadoutDiagnostics_AreDeterministicAcrossRepeatedValidation()
        {
            var slots = new List<CombatSkillSlotBinding>
            {
                new CombatSkillSlotBinding(0, "skill.one", "wrong"),
                new CombatSkillSlotBinding(0, "skill.one", "wrong"),
                null,
                new CombatSkillSlotBinding(7, "missing", "bad version")
            };
            CombatSkillLoadout loadout = CombatContractTestData.CreateLoadout(
                slots,
                id: "",
                schemaVersion: "2",
                rawSha256: "bad");

            string[] first = CombatSkillLoadoutValidator.Validate(
                    loadout,
                    CombatContractTestData.CreateSkills(),
                    CombatContractTestData.CreateExpectedSkillHashes(),
                    CombatContractTestData.CreateReferences(),
                    CombatContractTestData.HashA)
                .Diagnostics
                .Select(ToStableDiagnosticProjection)
                .ToArray();
            string[] second = CombatSkillLoadoutValidator.Validate(
                    loadout,
                    CombatContractTestData.CreateSkills(),
                    CombatContractTestData.CreateExpectedSkillHashes(),
                    CombatContractTestData.CreateReferences(),
                    CombatContractTestData.HashA)
                .Diagnostics
                .Select(ToStableDiagnosticProjection)
                .ToArray();

            Assert.That(second, Is.EqualTo(first));
        }

        private static void AssertCode(
            IList<CombatSkillSlotBinding> slots,
            IReadOnlyDictionary<string, CombatSkillDefinition> skills,
            string expectedCode)
        {
            CombatSkillLoadoutValidationResult result =
                CombatSkillLoadoutValidator.Validate(
                CombatContractTestData.CreateLoadout(slots),
                skills,
                CombatContractTestData.CreateExpectedSkillHashes(),
                CombatContractTestData.CreateReferences(),
                CombatContractTestData.HashA);
            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.Diagnostics.Select(item => item.Code),
                Does.Contain(expectedCode));
        }

        private static string ToStableDiagnosticProjection(CombatDiagnostic diagnostic)
        {
            return diagnostic.Code + "|" + diagnostic.FieldPath + "|" + diagnostic.Message;
        }

        private sealed class SkillCase
        {
            internal SkillCase(
                string name,
                CombatSkillDefinition skill,
                string code)
            {
                Name = name;
                Skill = skill;
                Code = code;
            }

            internal string Name { get; }
            internal CombatSkillDefinition Skill { get; }
            internal string Code { get; }
        }

        private sealed class LoadoutCase
        {
            internal LoadoutCase(
                string name,
                IList<CombatSkillSlotBinding> slots,
                string code)
            {
                Name = name;
                Slots = slots;
                Code = code;
            }

            internal string Name { get; }
            internal IList<CombatSkillSlotBinding> Slots { get; }
            internal string Code { get; }
        }

        private sealed class LoadoutValidationCase
        {
            internal LoadoutValidationCase(
                string name,
                CombatSkillLoadout loadout,
                string code)
            {
                Name = name;
                Loadout = loadout;
                Code = code;
            }

            internal string Name { get; }
            internal CombatSkillLoadout Loadout { get; }
            internal string Code { get; }
        }

        private sealed class SwitchingSkillDictionary :
            IReadOnlyDictionary<string, CombatSkillDefinition>
        {
            private readonly IReadOnlyDictionary<
                string,
                CombatSkillDefinition> stable;
            private readonly string switchedKey;
            private readonly CombatSkillDefinition switchedValue;

            internal SwitchingSkillDictionary(
                IReadOnlyDictionary<string, CombatSkillDefinition> stable,
                string switchedKey,
                CombatSkillDefinition switchedValue)
            {
                this.stable = stable;
                this.switchedKey = switchedKey;
                this.switchedValue = switchedValue;
            }

            internal int EnumerationCount { get; private set; }

            public int Count => stable.Count;
            public IEnumerable<string> Keys => stable.Keys;
            public IEnumerable<CombatSkillDefinition> Values =>
                stable.Values;
            public CombatSkillDefinition this[string key] => stable[key];

            public bool ContainsKey(string key)
            {
                return stable.ContainsKey(key);
            }

            public bool TryGetValue(
                string key,
                out CombatSkillDefinition value)
            {
                return stable.TryGetValue(key, out value);
            }

            public IEnumerator<KeyValuePair<
                string,
                CombatSkillDefinition>> GetEnumerator()
            {
                EnumerationCount++;
                bool substitute =
                    EnumerationCount >
                    CombatSkillLoadout.RequiredSlotCount;
                foreach (KeyValuePair<
                    string,
                    CombatSkillDefinition> entry in stable)
                {
                    yield return
                        substitute &&
                        StringComparer.Ordinal.Equals(
                            entry.Key,
                            switchedKey)
                            ? new KeyValuePair<
                                string,
                                CombatSkillDefinition>(
                                entry.Key,
                                switchedValue)
                            : entry;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
