using System.Collections.Generic;
using AL.ChampionMode.C1;

namespace AL.Tests.EditMode.ChampionCombat.C1
{
    internal static class CombatContractTestData
    {
        internal const string SchemaVersion = "1";
        internal const string ContentVersion = "combat-v1";
        internal const string CatalogSetId = "combat.catalog.test";
        internal const string ChampionProfileId = "champion.profile.test";
        internal const string SourceRevision = "source-r1";
        internal static readonly string HashA = new string('a', 64);
        internal static readonly string HashB = new string('b', 64);

        internal static CombatContractReferenceCatalog CreateReferences()
        {
            return new CombatContractReferenceCatalog(
                CatalogSetId,
                SchemaVersion,
                new[] { ContentVersion },
                new[] { ChampionProfileId },
                new[]
                {
                    new CombatSkillBehaviorReference(
                        "combat.behavior.damage",
                        CombatSkillBehaviorKind.Damage),
                    new CombatSkillBehaviorReference(
                        "combat.behavior.heal",
                        CombatSkillBehaviorKind.Healing),
                    new CombatSkillBehaviorReference(
                        "combat.behavior.break",
                        CombatSkillBehaviorKind.BreakDamage),
                    new CombatSkillBehaviorReference(
                        "combat.behavior.utility",
                        CombatSkillBehaviorKind.Utility)
                },
                new[]
                {
                    new CombatTargetingReference(
                        "combat.target.hostile",
                        CombatTargetDisposition.Hostile,
                        CombatTargetIntentKind.AreaProfile,
                        "unit.meter",
                        "area.standard",
                        true,
                        "target.profile.standard"),
                    new CombatTargetingReference(
                        "combat.target.self",
                        CombatTargetDisposition.Self,
                        CombatTargetIntentKind.Self,
                        "unit.meter",
                        string.Empty,
                        false),
                    new CombatTargetingReference(
                        "combat.target.friendly",
                        CombatTargetDisposition.Friendly,
                        CombatTargetIntentKind.ParticipantId,
                        "unit.meter",
                        string.Empty,
                        true,
                        "target.profile.standard"),
                    new CombatTargetingReference(
                        "combat.target.any",
                        CombatTargetDisposition.Any,
                        CombatTargetIntentKind.Point,
                        "unit.meter",
                        string.Empty,
                        false)
                },
                new[] { "combat.resource.standard" },
                new[] { "combat.cooldown.standard" },
                new[] { "combat.presentation.test" },
                new[] { "combat.movement.test" },
                new[] { "combat.dodge.test" },
                new[] { "combat.availability.test" });
        }

        internal static ChampionCombatProfile CreateProfile(
            string id = ChampionProfileId,
            string schemaVersion = SchemaVersion,
            string contentVersion = ContentVersion,
            string catalogSetId = CatalogSetId,
            long maxHealthMicros = 1_000L * CombatTechnicalLimits.MicrosPerUnit,
            long maxManaMicros = 100L * CombatTechnicalLimits.MicrosPerUnit,
            long manaRegenMicros = 10L * CombatTechnicalLimits.MicrosPerUnit,
            long attackPowerMicros = 50L * CombatTechnicalLimits.MicrosPerUnit,
            string behaviorProfileId = "combat.behavior.damage",
            string movementProfileId = "combat.movement.test",
            string dodgeProfileId = "combat.dodge.test",
            string targetingProfileId = "combat.target.hostile",
            string sourceRevision = SourceRevision,
            string rawSha256 = null)
        {
            return new ChampionCombatProfile(
                id,
                schemaVersion,
                contentVersion,
                catalogSetId,
                maxHealthMicros,
                maxManaMicros,
                manaRegenMicros,
                attackPowerMicros,
                behaviorProfileId,
                movementProfileId,
                dodgeProfileId,
                targetingProfileId,
                sourceRevision,
                rawSha256 ?? HashA);
        }

        internal static CombatSkillDefinition CreateSkill(
            string id = "skill.one",
            string schemaVersion = SchemaVersion,
            string contentVersion = ContentVersion,
            string behaviorProfileId = "combat.behavior.damage",
            string targetingProfileId = "combat.target.hostile",
            string resourcePolicyId = "combat.resource.standard",
            string cooldownPolicyId = "combat.cooldown.standard",
            long manaCostMicros = 20L * CombatTechnicalLimits.MicrosPerUnit,
            long castDurationMicros = 50_000L,
            long cooldownDurationMicros = 4L * CombatTechnicalLimits.MicrosPerUnit,
            long rangeMicros = 3L * CombatTechnicalLimits.MicrosPerUnit,
            long powerMicros = 150L * CombatTechnicalLimits.MicrosPerUnit,
            long botPowerMultiplierMicros = 720_000L,
            string presentationProfileId = "combat.presentation.test",
            string sourceRevision = SourceRevision,
            string rawSha256 = null)
        {
            return new CombatSkillDefinition(
                id,
                schemaVersion,
                contentVersion,
                behaviorProfileId,
                targetingProfileId,
                resourcePolicyId,
                cooldownPolicyId,
                manaCostMicros,
                castDurationMicros,
                cooldownDurationMicros,
                rangeMicros,
                powerMicros,
                botPowerMultiplierMicros,
                presentationProfileId,
                sourceRevision,
                rawSha256 ?? HashA);
        }

        internal static IReadOnlyDictionary<string, CombatSkillDefinition> CreateSkills()
        {
            var result = new Dictionary<string, CombatSkillDefinition>(
                System.StringComparer.Ordinal)
            {
                ["skill.one"] = CreateSkill("skill.one"),
                ["skill.two"] = CreateSkill(
                    "skill.two",
                    behaviorProfileId: "combat.behavior.heal",
                    targetingProfileId: "combat.target.self",
                    rangeMicros: 0L),
                ["skill.three"] = CreateSkill("skill.three"),
                ["skill.four"] = CreateSkill(
                    "skill.four",
                    behaviorProfileId: "combat.behavior.break")
            };
            return result;
        }

        internal static IReadOnlyDictionary<string, string>
            CreateExpectedSkillHashes()
        {
            return new Dictionary<string, string>(
                System.StringComparer.Ordinal)
            {
                ["skill.one"] = HashA,
                ["skill.two"] = HashA,
                ["skill.three"] = HashA,
                ["skill.four"] = HashA
            };
        }

        internal static List<CombatSkillSlotBinding> CreateSlots()
        {
            return new List<CombatSkillSlotBinding>
            {
                new CombatSkillSlotBinding(0, "skill.one", ContentVersion, "input.skill.0"),
                new CombatSkillSlotBinding(1, "skill.two", ContentVersion, "input.skill.1"),
                new CombatSkillSlotBinding(2, "skill.three", ContentVersion, "input.skill.2"),
                new CombatSkillSlotBinding(3, "skill.four", ContentVersion, "input.skill.3")
            };
        }

        internal static CombatSkillLoadout CreateLoadout(
            IList<CombatSkillSlotBinding> slots = null,
            string id = "loadout.test",
            string schemaVersion = SchemaVersion,
            string contentVersion = ContentVersion,
            string championProfileId = ChampionProfileId,
            string sourceRevision = SourceRevision,
            string rawSha256 = null)
        {
            return new CombatSkillLoadout(
                id,
                schemaVersion,
                contentVersion,
                championProfileId,
                slots ?? CreateSlots(),
                sourceRevision,
                rawSha256 ?? HashA);
        }
    }
}
