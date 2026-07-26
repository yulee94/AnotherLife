using System;
using System.Collections.Generic;
using System.Linq;

namespace AL.ChampionMode.C1
{
    public static class ChampionCombatProfileValidator
    {
        public static CombatValidationResult Validate(
            ChampionCombatProfile profile,
            CombatContractReferenceCatalog references,
            string expectedRawSha256 = null)
        {
            if (references == null) throw new ArgumentNullException(nameof(references));
            var diagnostics = new List<CombatDiagnostic>();
            if (profile == null)
            {
                diagnostics.Add(CombatValidatorDiagnostic.Error(
                    "AL-CHAMPION-PROFILE-MISSING",
                    CombatDiagnosticDomain.ChampionProfile,
                    "$",
                    "Champion combat profile is required.",
                    CombatBlockScope.Construction | CombatBlockScope.Encounter));
                return new CombatValidationResult(diagnostics);
            }

            string safeId = CombatPrimitiveValidation.IsStableId(profile.Id) ? profile.Id : string.Empty;
            CombatValidatorDiagnostic.RequireStableId(
                diagnostics,
                "AL-CHAMPION-PROFILE-ID",
                CombatDiagnosticDomain.ChampionProfile,
                profile.Id,
                "$.id",
                safeId);
            CombatValidatorDiagnostic.RequireSupportedVersions(
                diagnostics,
                "AL-CHAMPION-PROFILE",
                CombatDiagnosticDomain.ChampionProfile,
                profile.SchemaVersion,
                profile.ContentVersion,
                references,
                safeId);

            if (!StringComparer.Ordinal.Equals(profile.CatalogSetId, references.CatalogSetId))
            {
                diagnostics.Add(CombatValidatorDiagnostic.Error(
                    "AL-CHAMPION-PROFILE-CATALOG-SET",
                    CombatDiagnosticDomain.ChampionProfile,
                    "$.catalogSetId",
                    "Champion profile catalog-set identity does not match the validation snapshot.",
                    CombatBlockScope.Construction | CombatBlockScope.Encounter,
                    safeId,
                    profile.SchemaVersion,
                    profile.ContentVersion));
            }

            CombatValidatorDiagnostic.RequireMicros(
                diagnostics,
                "AL-CHAMPION-PROFILE-MAX-HEALTH",
                CombatDiagnosticDomain.ChampionProfile,
                profile.MaxHealthMicros,
                CombatScalarKind.Health,
                true,
                "$.maxHealthMicros",
                safeId);
            CombatValidatorDiagnostic.RequireMicros(
                diagnostics,
                "AL-CHAMPION-PROFILE-MAX-MANA",
                CombatDiagnosticDomain.ChampionProfile,
                profile.MaxManaMicros,
                CombatScalarKind.Mana,
                true,
                "$.maxManaMicros",
                safeId);
            CombatValidatorDiagnostic.RequireMicros(
                diagnostics,
                "AL-CHAMPION-PROFILE-MANA-REGEN",
                CombatDiagnosticDomain.ChampionProfile,
                profile.ManaRegenPerSecondMicros,
                CombatScalarKind.RegenerationRate,
                false,
                "$.manaRegenPerSecondMicros",
                safeId);
            CombatValidatorDiagnostic.RequireMicros(
                diagnostics,
                "AL-CHAMPION-PROFILE-ATTACK-POWER",
                CombatDiagnosticDomain.ChampionProfile,
                profile.BasicAttackPowerMicros,
                CombatScalarKind.AttackPower,
                false,
                "$.basicAttackPowerMicros",
                safeId);

            RequireReference(
                diagnostics,
                profile.BasicAttackBehaviorProfileId,
                references.TryGetBehavior(profile.BasicAttackBehaviorProfileId, out _),
                "$.basicAttackBehaviorProfileId",
                safeId);
            RequireReference(
                diagnostics,
                profile.MovementProfileId,
                references.ContainsMovementProfile(profile.MovementProfileId),
                "$.movementProfileId",
                safeId);
            RequireReference(
                diagnostics,
                profile.DodgeProfileId,
                references.ContainsDodgeProfile(profile.DodgeProfileId),
                "$.dodgeProfileId",
                safeId);
            RequireReference(
                diagnostics,
                profile.TargetingProfileId,
                references.TryGetTargeting(profile.TargetingProfileId, out _),
                "$.targetingProfileId",
                safeId);
            CombatValidatorDiagnostic.RequireVersion(
                diagnostics,
                "AL-CHAMPION-PROFILE-SOURCE-REVISION",
                CombatDiagnosticDomain.ChampionProfile,
                profile.SourceRevision,
                "$.sourceRevision",
                safeId);
            CombatValidatorDiagnostic.RequireHash(
                diagnostics,
                "AL-CHAMPION-PROFILE-HASH",
                CombatDiagnosticDomain.ChampionProfile,
                profile.RawSha256,
                expectedRawSha256,
                "$.rawSha256",
                safeId);

            return new CombatValidationResult(diagnostics);
        }

        private static void RequireReference(
            ICollection<CombatDiagnostic> diagnostics,
            string value,
            bool resolved,
            string fieldPath,
            string sourceId)
        {
            if (!CombatPrimitiveValidation.IsStableId(value) || !resolved)
            {
                diagnostics.Add(CombatValidatorDiagnostic.Error(
                    "AL-CHAMPION-PROFILE-REFERENCE",
                    CombatDiagnosticDomain.ChampionProfile,
                    fieldPath,
                    "A required Champion profile reference is invalid or unresolved.",
                    CombatBlockScope.Construction | CombatBlockScope.Encounter,
                    sourceId));
            }
        }
    }

    public static class CombatSkillDefinitionValidator
    {
        public static CombatValidationResult Validate(
            CombatSkillDefinition skill,
            CombatContractReferenceCatalog references,
            string expectedRawSha256 = null)
        {
            if (references == null) throw new ArgumentNullException(nameof(references));
            var diagnostics = new List<CombatDiagnostic>();
            if (skill == null)
            {
                diagnostics.Add(CombatValidatorDiagnostic.Error(
                    "AL-SKILL-CATALOG-DEFINITION-MISSING",
                    CombatDiagnosticDomain.SkillCatalog,
                    "$",
                    "Combat skill definition is required.",
                    CombatBlockScope.Construction |
                    CombatBlockScope.Action |
                    CombatBlockScope.Encounter));
                return new CombatValidationResult(diagnostics);
            }

            string safeId = CombatPrimitiveValidation.IsStableId(skill.Id) ? skill.Id : string.Empty;
            CombatValidatorDiagnostic.RequireStableId(
                diagnostics,
                "AL-SKILL-CATALOG-ID",
                CombatDiagnosticDomain.SkillCatalog,
                skill.Id,
                "$.id",
                safeId);
            CombatValidatorDiagnostic.RequireSupportedVersions(
                diagnostics,
                "AL-SKILL-CATALOG",
                CombatDiagnosticDomain.SkillCatalog,
                skill.SchemaVersion,
                skill.ContentVersion,
                references,
                safeId);

            bool behaviorResolved = references.TryGetBehavior(
                skill.BehaviorProfileId,
                out CombatSkillBehaviorReference behavior);
            RequireReference(
                diagnostics,
                skill.BehaviorProfileId,
                behaviorResolved,
                "$.behaviorProfileId",
                safeId);
            bool targetingResolved = references.TryGetTargeting(
                skill.TargetingProfileId,
                out CombatTargetingReference targeting);
            RequireReference(
                diagnostics,
                skill.TargetingProfileId,
                targetingResolved,
                "$.targetingProfileId",
                safeId);
            RequireReference(
                diagnostics,
                skill.ResourcePolicyId,
                references.ContainsResourcePolicy(skill.ResourcePolicyId),
                "$.resourcePolicyId",
                safeId);
            RequireReference(
                diagnostics,
                skill.CooldownPolicyId,
                references.ContainsCooldownPolicy(skill.CooldownPolicyId),
                "$.cooldownPolicyId",
                safeId);
            RequireReference(
                diagnostics,
                skill.PresentationProfileId,
                references.ContainsPresentationProfile(skill.PresentationProfileId),
                "$.presentationProfileId",
                safeId);

            CombatValidatorDiagnostic.RequireMicros(
                diagnostics,
                "AL-SKILL-CATALOG-MANA-COST",
                CombatDiagnosticDomain.SkillCatalog,
                skill.ManaCostMicros,
                CombatScalarKind.Mana,
                false,
                "$.manaCostMicros",
                safeId);
            CombatValidatorDiagnostic.RequireMicros(
                diagnostics,
                "AL-SKILL-CATALOG-CAST-DURATION",
                CombatDiagnosticDomain.SkillCatalog,
                skill.CastDurationMicros,
                CombatScalarKind.Duration,
                false,
                "$.castDurationMicros",
                safeId);
            CombatValidatorDiagnostic.RequireMicros(
                diagnostics,
                "AL-SKILL-CATALOG-COOLDOWN-DURATION",
                CombatDiagnosticDomain.SkillCatalog,
                skill.CooldownDurationMicros,
                CombatScalarKind.Duration,
                false,
                "$.cooldownDurationMicros",
                safeId);
            CombatValidatorDiagnostic.RequireMicros(
                diagnostics,
                "AL-SKILL-CATALOG-RANGE",
                CombatDiagnosticDomain.SkillCatalog,
                skill.RangeMicros,
                CombatScalarKind.WorldDistance,
                false,
                "$.rangeMicros",
                safeId);
            CombatValidatorDiagnostic.RequireMicros(
                diagnostics,
                "AL-SKILL-CATALOG-POWER",
                CombatDiagnosticDomain.SkillCatalog,
                skill.PowerMicros,
                CombatScalarKind.Damage,
                false,
                "$.powerMicros",
                safeId);
            CombatValidatorDiagnostic.RequireMicros(
                diagnostics,
                "AL-SKILL-CATALOG-BOT-MULTIPLIER",
                CombatDiagnosticDomain.SkillCatalog,
                skill.BotPowerMultiplierMicros,
                CombatScalarKind.Multiplier,
                false,
                "$.botPowerMultiplierMicros",
                safeId);

            if (behaviorResolved && targetingResolved &&
                !IsCompatible(
                    behavior.Kind,
                    targeting.Disposition,
                    targeting.AllowedIntentKind,
                    skill.RangeMicros,
                    skill.PowerMicros))
            {
                diagnostics.Add(CombatValidatorDiagnostic.Error(
                    "AL-SKILL-CATALOG-BEHAVIOR-TARGET-CONFLICT",
                    CombatDiagnosticDomain.SkillCatalog,
                    "$",
                    "Skill behavior, target disposition, range, and power are contradictory.",
                    CombatBlockScope.Construction |
                    CombatBlockScope.Action |
                    CombatBlockScope.Encounter,
                    safeId));
            }

            CombatValidatorDiagnostic.RequireVersion(
                diagnostics,
                "AL-SKILL-CATALOG-SOURCE-REVISION",
                CombatDiagnosticDomain.SkillCatalog,
                skill.SourceRevision,
                "$.sourceRevision",
                safeId);
            CombatValidatorDiagnostic.RequireHash(
                diagnostics,
                "AL-SKILL-CATALOG-HASH",
                CombatDiagnosticDomain.SkillCatalog,
                skill.RawSha256,
                expectedRawSha256,
                "$.rawSha256",
                safeId);

            return new CombatValidationResult(diagnostics);
        }

        private static bool IsCompatible(
            CombatSkillBehaviorKind behavior,
            CombatTargetDisposition target,
            CombatTargetIntentKind intent,
            long rangeMicros,
            long powerMicros)
        {
            if (CombatTargetIntentCompatibility.RequiresZeroRange(
                    intent) &&
                rangeMicros != 0L)
            {
                return false;
            }

            switch (behavior)
            {
                case CombatSkillBehaviorKind.Healing:
                    return (target == CombatTargetDisposition.Self ||
                            target == CombatTargetDisposition.Friendly) &&
                           powerMicros > 0L;
                case CombatSkillBehaviorKind.Damage:
                case CombatSkillBehaviorKind.BreakDamage:
                    return target != CombatTargetDisposition.Self &&
                           target != CombatTargetDisposition.Friendly &&
                           rangeMicros > 0L &&
                           powerMicros > 0L;
                case CombatSkillBehaviorKind.Utility:
                    return true;
                default:
                    return false;
            }
        }

        private static void RequireReference(
            ICollection<CombatDiagnostic> diagnostics,
            string value,
            bool resolved,
            string fieldPath,
            string sourceId)
        {
            if (!CombatPrimitiveValidation.IsStableId(value) || !resolved)
            {
                diagnostics.Add(CombatValidatorDiagnostic.Error(
                    "AL-SKILL-CATALOG-REFERENCE",
                    CombatDiagnosticDomain.SkillCatalog,
                    fieldPath,
                    "A required skill reference is invalid or unresolved.",
                    CombatBlockScope.Construction |
                    CombatBlockScope.Action |
                    CombatBlockScope.Encounter,
                    sourceId));
            }
        }
    }

    public static class CombatSkillLoadoutValidator
    {
        public static CombatSkillLoadoutValidationResult Validate(
            CombatSkillLoadout loadout,
            IReadOnlyDictionary<string, CombatSkillDefinition> skillsById,
            IReadOnlyDictionary<string, string> expectedSkillRawSha256ById,
            CombatContractReferenceCatalog references,
            string expectedRawSha256)
        {
            if (skillsById == null) throw new ArgumentNullException(nameof(skillsById));
            if (expectedSkillRawSha256ById == null)
            {
                throw new ArgumentNullException(
                    nameof(expectedSkillRawSha256ById));
            }
            if (references == null) throw new ArgumentNullException(nameof(references));
            var diagnostics = new List<CombatDiagnostic>();
            if (loadout == null)
            {
                diagnostics.Add(CombatValidatorDiagnostic.Error(
                    "AL-SKILL-LOADOUT-MISSING",
                    CombatDiagnosticDomain.SkillLoadout,
                    "$",
                    "Combat skill loadout is required.",
                    CombatBlockScope.Construction |
                    CombatBlockScope.Action |
                    CombatBlockScope.Encounter));
                return Result(diagnostics, null, null);
            }

            if (!CombatPrimitiveValidation.IsSha256(expectedRawSha256))
            {
                diagnostics.Add(CombatValidatorDiagnostic.Error(
                    "AL-SKILL-LOADOUT-EXPECTED-HASH",
                    CombatDiagnosticDomain.SkillLoadout,
                    "$.rawSha256",
                    "Trusted loadout source-hash authority is missing or invalid.",
                    CombatBlockScope.Construction |
                    CombatBlockScope.Action |
                    CombatBlockScope.Encounter,
                    loadout.Id));
            }

            string safeId = CombatPrimitiveValidation.IsStableId(loadout.Id) ? loadout.Id : string.Empty;
            CombatValidatorDiagnostic.RequireStableId(
                diagnostics,
                "AL-SKILL-LOADOUT-ID",
                CombatDiagnosticDomain.SkillLoadout,
                loadout.Id,
                "$.id",
                safeId);
            CombatValidatorDiagnostic.RequireSupportedVersions(
                diagnostics,
                "AL-SKILL-LOADOUT",
                CombatDiagnosticDomain.SkillLoadout,
                loadout.SchemaVersion,
                loadout.ContentVersion,
                references,
                safeId);

            if (!CombatPrimitiveValidation.IsStableId(loadout.ChampionOrClassProfileId) ||
                !references.ContainsChampionOrClassProfile(loadout.ChampionOrClassProfileId))
            {
                diagnostics.Add(CombatValidatorDiagnostic.Error(
                    "AL-SKILL-LOADOUT-CHAMPION-REFERENCE",
                    CombatDiagnosticDomain.SkillLoadout,
                    "$.championOrClassProfileId",
                    "Champion/class profile reference is invalid or unresolved.",
                    CombatBlockScope.Construction |
                    CombatBlockScope.Action |
                    CombatBlockScope.Encounter,
                    safeId));
            }

            if (skillsById.Count > CombatTechnicalLimits.MaximumReferenceEntries)
            {
                diagnostics.Add(CombatValidatorDiagnostic.Error(
                    "AL-SKILL-LOADOUT-SKILL-CATALOG-COUNT",
                    CombatDiagnosticDomain.SkillLoadout,
                    "$.skills",
                    "Skill lookup exceeds the bounded validation ceiling.",
                    CombatBlockScope.Construction |
                    CombatBlockScope.Action |
                    CombatBlockScope.Encounter,
                    safeId));
                return Result(diagnostics, null, null);
            }

            if (expectedSkillRawSha256ById.Count >
                CombatTechnicalLimits.MaximumReferenceEntries)
            {
                diagnostics.Add(CombatValidatorDiagnostic.Error(
                    "AL-SKILL-LOADOUT-SKILL-PROVENANCE-COUNT",
                    CombatDiagnosticDomain.SkillLoadout,
                    "$.expectedSkillRawSha256ById",
                    "Trusted skill provenance lookup exceeds the bounded validation ceiling.",
                    CombatBlockScope.Construction |
                    CombatBlockScope.Action |
                    CombatBlockScope.Encounter,
                    safeId));
                return Result(diagnostics, null, null);
            }

            if (loadout.Slots.Count != CombatSkillLoadout.RequiredSlotCount)
            {
                diagnostics.Add(CombatValidatorDiagnostic.Error(
                    "AL-SKILL-LOADOUT-SLOT-COUNT",
                    CombatDiagnosticDomain.SkillLoadout,
                    "$.slots",
                    "The initial Champion loadout requires exactly four slot bindings.",
                    CombatBlockScope.Construction |
                    CombatBlockScope.Action |
                    CombatBlockScope.Encounter,
                    safeId));
            }

            var seenSlots = new HashSet<int>();
            var seenSkills = new HashSet<string>(StringComparer.Ordinal);
            var seenInputBindings = new HashSet<string>(StringComparer.Ordinal);
            var capturedSkillsBySlot =
                new CombatSkillDefinition[
                    CombatSkillLoadout.RequiredSlotCount];
            var capturedSkillHashesBySlot =
                new string[CombatSkillLoadout.RequiredSlotCount];
            for (int index = 0; index < loadout.Slots.Count; index++)
            {
                CombatSkillSlotBinding binding = loadout.Slots[index];
                string path = "$.slots[" + index + "]";
                if (binding == null)
                {
                    diagnostics.Add(CombatValidatorDiagnostic.Error(
                        "AL-SKILL-LOADOUT-NULL-SLOT",
                        CombatDiagnosticDomain.SkillLoadout,
                        path,
                        "Slot binding cannot be null.",
                        CombatBlockScope.Construction |
                        CombatBlockScope.Action |
                        CombatBlockScope.Encounter,
                        safeId));
                    continue;
                }

                if (binding.SlotIndex < 0 ||
                    binding.SlotIndex >= CombatSkillLoadout.RequiredSlotCount)
                {
                    diagnostics.Add(CombatValidatorDiagnostic.Error(
                        "AL-SKILL-LOADOUT-SLOT-RANGE",
                        CombatDiagnosticDomain.SkillLoadout,
                        path + ".slotIndex",
                        "Slot index is outside the required range 0..3.",
                        CombatBlockScope.Construction |
                        CombatBlockScope.Action |
                        CombatBlockScope.Encounter,
                        safeId));
                }
                else if (!seenSlots.Add(binding.SlotIndex))
                {
                    diagnostics.Add(CombatValidatorDiagnostic.Error(
                        "AL-SKILL-LOADOUT-DUPLICATE-SLOT",
                        CombatDiagnosticDomain.SkillLoadout,
                        path + ".slotIndex",
                        "Slot index is duplicated.",
                        CombatBlockScope.Construction |
                        CombatBlockScope.Action |
                        CombatBlockScope.Encounter,
                        safeId));
                }

                if (!CombatPrimitiveValidation.IsStableId(binding.SkillDefinitionId))
                {
                    diagnostics.Add(CombatValidatorDiagnostic.Error(
                        "AL-SKILL-LOADOUT-SKILL-ID",
                        CombatDiagnosticDomain.SkillLoadout,
                        path + ".skillDefinitionId",
                        "Skill definition ID is invalid.",
                        CombatBlockScope.Construction |
                        CombatBlockScope.Action |
                        CombatBlockScope.Encounter,
                        safeId));
                    continue;
                }

                if (!seenSkills.Add(binding.SkillDefinitionId))
                {
                    diagnostics.Add(CombatValidatorDiagnostic.Error(
                        "AL-SKILL-LOADOUT-DUPLICATE-SKILL",
                        CombatDiagnosticDomain.SkillLoadout,
                        path + ".skillDefinitionId",
                        "Skill definition ID is duplicated in the initial loadout.",
                        CombatBlockScope.Construction |
                        CombatBlockScope.Action |
                        CombatBlockScope.Encounter,
                        safeId));
                }

                if (!CombatPrimitiveValidation.IsVersion(binding.SkillContentVersion))
                {
                    diagnostics.Add(CombatValidatorDiagnostic.Error(
                        "AL-SKILL-LOADOUT-SKILL-VERSION",
                        CombatDiagnosticDomain.SkillLoadout,
                        path + ".skillContentVersion",
                        "Skill content version is invalid.",
                        CombatBlockScope.Construction |
                        CombatBlockScope.Action |
                        CombatBlockScope.Encounter,
                        safeId));
                }

                if (!string.IsNullOrEmpty(binding.InputBindingId))
                {
                    if (!CombatPrimitiveValidation.IsStableId(binding.InputBindingId) ||
                        !seenInputBindings.Add(binding.InputBindingId))
                    {
                        diagnostics.Add(CombatValidatorDiagnostic.Error(
                            "AL-SKILL-LOADOUT-INPUT-BINDING",
                            CombatDiagnosticDomain.SkillLoadout,
                            path + ".inputBindingId",
                            "Optional input binding ID is invalid or duplicated.",
                            CombatBlockScope.Construction |
                            CombatBlockScope.Action |
                            CombatBlockScope.Encounter,
                            safeId));
                    }
                }

                if (!string.IsNullOrEmpty(binding.AvailabilityProfileId) &&
                    (!CombatPrimitiveValidation.IsStableId(binding.AvailabilityProfileId) ||
                     !references.ContainsAvailabilityProfile(binding.AvailabilityProfileId)))
                {
                    diagnostics.Add(CombatValidatorDiagnostic.Error(
                        "AL-SKILL-LOADOUT-AVAILABILITY",
                        CombatDiagnosticDomain.SkillLoadout,
                        path + ".availabilityProfileId",
                        "Optional availability profile ID is invalid or unresolved.",
                        CombatBlockScope.Construction |
                        CombatBlockScope.Action |
                        CombatBlockScope.Encounter,
                        safeId));
                }

                if (!TryGetExactSkill(
                        skillsById,
                        binding.SkillDefinitionId,
                        out CombatSkillDefinition skill) ||
                    skill == null)
                {
                    diagnostics.Add(CombatValidatorDiagnostic.Error(
                        "AL-SKILL-LOADOUT-SKILL-REFERENCE",
                        CombatDiagnosticDomain.SkillLoadout,
                        path + ".skillDefinitionId",
                        "Skill definition reference is unresolved.",
                        CombatBlockScope.Construction |
                        CombatBlockScope.Action |
                        CombatBlockScope.Encounter,
                        safeId));
                    continue;
                }

                if (!StringComparer.Ordinal.Equals(binding.SkillDefinitionId, skill.Id))
                {
                    diagnostics.Add(CombatValidatorDiagnostic.Error(
                        "AL-SKILL-LOADOUT-SKILL-IDENTITY-MISMATCH",
                        CombatDiagnosticDomain.SkillLoadout,
                        path + ".skillDefinitionId",
                        "Resolved dictionary key and skill definition identity do not match.",
                        CombatBlockScope.Construction |
                        CombatBlockScope.Action |
                        CombatBlockScope.Encounter,
                        safeId));
                    continue;
                }

                if (!StringComparer.Ordinal.Equals(
                    binding.SkillContentVersion,
                    skill.ContentVersion))
                {
                    diagnostics.Add(CombatValidatorDiagnostic.Error(
                        "AL-SKILL-LOADOUT-SKILL-VERSION-MISMATCH",
                        CombatDiagnosticDomain.SkillLoadout,
                        path + ".skillContentVersion",
                        "Slot skill content version does not match the resolved definition.",
                        CombatBlockScope.Construction |
                        CombatBlockScope.Action |
                        CombatBlockScope.Encounter,
                        safeId));
                }

                if (!TryGetExactHash(
                        expectedSkillRawSha256ById,
                        binding.SkillDefinitionId,
                        out string expectedSkillRawSha256))
                {
                    diagnostics.Add(CombatValidatorDiagnostic.Error(
                        "AL-SKILL-LOADOUT-SKILL-PROVENANCE",
                        CombatDiagnosticDomain.SkillLoadout,
                        path + ".skillDefinitionId",
                        "Referenced skill has no exact trusted source-hash authority.",
                        CombatBlockScope.Construction |
                        CombatBlockScope.Action |
                        CombatBlockScope.Encounter,
                        safeId));
                    continue;
                }

                if (binding.SlotIndex >= 0 &&
                    binding.SlotIndex <
                        CombatSkillLoadout.RequiredSlotCount)
                {
                    capturedSkillsBySlot[binding.SlotIndex] = skill;
                    capturedSkillHashesBySlot[binding.SlotIndex] =
                        expectedSkillRawSha256;
                }

                diagnostics.AddRange(
                    CombatSkillDefinitionValidator.Validate(
                        skill,
                        references,
                        expectedSkillRawSha256).Diagnostics);
            }

            for (int slot = 0; slot < CombatSkillLoadout.RequiredSlotCount; slot++)
            {
                if (!seenSlots.Contains(slot))
                {
                    diagnostics.Add(CombatValidatorDiagnostic.Error(
                        "AL-SKILL-LOADOUT-MISSING-SLOT",
                        CombatDiagnosticDomain.SkillLoadout,
                        "$.slots[" + slot + "]",
                        "A required slot binding is missing.",
                        CombatBlockScope.Construction |
                        CombatBlockScope.Action |
                        CombatBlockScope.Encounter,
                        safeId));
                }
            }

            CombatValidatorDiagnostic.RequireVersion(
                diagnostics,
                "AL-SKILL-LOADOUT-SOURCE-REVISION",
                CombatDiagnosticDomain.SkillLoadout,
                loadout.SourceRevision,
                "$.sourceRevision",
                safeId);
            CombatValidatorDiagnostic.RequireHash(
                diagnostics,
                "AL-SKILL-LOADOUT-HASH",
                CombatDiagnosticDomain.SkillLoadout,
                loadout.RawSha256,
                expectedRawSha256,
                "$.rawSha256",
                safeId);
            var validation = new CombatValidationResult(diagnostics);
            if (!validation.IsValid)
            {
                return new CombatSkillLoadoutValidationResult(
                    validation,
                    null);
            }

            return new CombatSkillLoadoutValidationResult(
                validation,
                new ValidatedCombatSkillLoadoutSnapshot(
                    loadout,
                    capturedSkillsBySlot,
                    references,
                    expectedRawSha256,
                    capturedSkillHashesBySlot));
        }

        private static bool TryGetExactSkill(
            IReadOnlyDictionary<string, CombatSkillDefinition> skillsById,
            string skillId,
            out CombatSkillDefinition skill)
        {
            skill = null;
            bool found = false;
            foreach (KeyValuePair<string, CombatSkillDefinition> entry in skillsById)
            {
                if (!StringComparer.Ordinal.Equals(entry.Key, skillId))
                {
                    continue;
                }

                if (found)
                {
                    skill = null;
                    return false;
                }

                found = true;
                skill = entry.Value;
            }

            return found;
        }

        private static bool TryGetExactHash(
            IReadOnlyDictionary<string, string> expectedHashesById,
            string skillId,
            out string expectedHash)
        {
            expectedHash = null;
            bool found = false;
            foreach (KeyValuePair<string, string> entry in expectedHashesById)
            {
                if (!StringComparer.Ordinal.Equals(entry.Key, skillId))
                {
                    continue;
                }

                if (found)
                {
                    expectedHash = null;
                    return false;
                }

                found = true;
                expectedHash = entry.Value;
            }

            return found;
        }

        private static CombatSkillLoadoutValidationResult Result(
            IEnumerable<CombatDiagnostic> diagnostics,
            CombatSkillLoadout loadout,
            IList<CombatSkillDefinition> skillsInSlotOrder)
        {
            var validation = new CombatValidationResult(diagnostics);
            return new CombatSkillLoadoutValidationResult(
                validation,
                null);
        }
    }

    internal static class CombatValidatorDiagnostic
    {
        private const CombatBlockScope ContractBlocks =
            CombatBlockScope.Construction |
            CombatBlockScope.Action |
            CombatBlockScope.Encounter;

        internal static CombatDiagnostic Error(
            string code,
            CombatDiagnosticDomain domain,
            string fieldPath,
            string message,
            CombatBlockScope blockScope,
            string sourceId = "",
            string schemaVersion = "",
            string contentVersion = "")
        {
            return new CombatDiagnostic(
                code,
                CombatDiagnosticSeverity.Error,
                domain,
                fieldPath,
                message,
                blockScope,
                sourceDefinitionId: sourceId,
                schemaVersion: schemaVersion,
                contentVersion: contentVersion);
        }

        internal static void RequireStableId(
            ICollection<CombatDiagnostic> diagnostics,
            string code,
            CombatDiagnosticDomain domain,
            string value,
            string fieldPath,
            string sourceId)
        {
            if (!CombatPrimitiveValidation.IsStableId(value))
            {
                diagnostics.Add(Error(
                    code,
                    domain,
                    fieldPath,
                    "A bounded, nonblank, case-sensitive stable technical ID is required.",
                    ContractBlocks,
                    sourceId));
            }
        }

        internal static void RequireVersion(
            ICollection<CombatDiagnostic> diagnostics,
            string code,
            CombatDiagnosticDomain domain,
            string value,
            string fieldPath,
            string sourceId)
        {
            if (!CombatPrimitiveValidation.IsVersion(value))
            {
                diagnostics.Add(Error(
                    code,
                    domain,
                    fieldPath,
                    "A bounded technical version is required.",
                    ContractBlocks,
                    sourceId));
            }
        }

        internal static void RequireSupportedVersions(
            ICollection<CombatDiagnostic> diagnostics,
            string codePrefix,
            CombatDiagnosticDomain domain,
            string schemaVersion,
            string contentVersion,
            CombatContractReferenceCatalog references,
            string sourceId)
        {
            if (!CombatPrimitiveValidation.IsVersion(schemaVersion) ||
                !StringComparer.Ordinal.Equals(schemaVersion, references.SchemaVersion))
            {
                diagnostics.Add(Error(
                    codePrefix + "-SCHEMA-VERSION",
                    domain,
                    "$.schemaVersion",
                    "Schema version is invalid or unsupported.",
                    ContractBlocks,
                    sourceId,
                    schemaVersion,
                    contentVersion));
            }

            if (!CombatPrimitiveValidation.IsVersion(contentVersion) ||
                !references.SupportsContentVersion(contentVersion))
            {
                diagnostics.Add(Error(
                    codePrefix + "-CONTENT-VERSION",
                    domain,
                    "$.contentVersion",
                    "Content version is invalid or unsupported.",
                    ContractBlocks,
                    sourceId,
                    schemaVersion,
                    contentVersion));
            }
        }

        internal static void RequireMicros(
            ICollection<CombatDiagnostic> diagnostics,
            string code,
            CombatDiagnosticDomain domain,
            long value,
            CombatScalarKind kind,
            bool requirePositive,
            string fieldPath,
            string sourceId)
        {
            if (!CombatPrimitiveValidation.IsMicrosInRange(value, kind, requirePositive))
            {
                diagnostics.Add(Error(
                    code,
                    domain,
                    fieldPath,
                    "Fixed-point combat value is outside its technical range.",
                    ContractBlocks,
                    sourceId));
            }
        }

        internal static void RequireHash(
            ICollection<CombatDiagnostic> diagnostics,
            string code,
            CombatDiagnosticDomain domain,
            string actual,
            string expected,
            string fieldPath,
            string sourceId)
        {
            if (!CombatPrimitiveValidation.IsSha256(actual) ||
                (expected != null && !StringComparer.Ordinal.Equals(actual, expected)))
            {
                diagnostics.Add(Error(
                    code,
                    domain,
                    fieldPath,
                    "Raw SHA-256 is malformed or does not match exact source provenance.",
                    ContractBlocks,
                    sourceId));
            }
        }
    }
}
