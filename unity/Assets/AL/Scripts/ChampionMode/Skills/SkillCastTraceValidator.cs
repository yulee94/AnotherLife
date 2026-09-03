using System;
using System.Collections.Generic;

namespace AL.ChampionMode.Skills
{
    public sealed class SkillCastTraceResult
    {
        public SkillCastTraceResult(
            string skillId,
            string actorFamily,
            string role,
            bool passed,
            string failure)
        {
            SkillId = skillId ?? string.Empty;
            ActorFamily = actorFamily ?? string.Empty;
            Role = role ?? string.Empty;
            Passed = passed;
            Failure = failure ?? string.Empty;
        }

        public string SkillId { get; }
        public string ActorFamily { get; }
        public string Role { get; }
        public bool Passed { get; }
        public string Failure { get; }
    }

    public sealed class SkillCastTraceReport
    {
        public SkillCastTraceReport(
            bool passed,
            IReadOnlyList<SkillCastTraceResult> results)
        {
            Passed = passed;
            Results = results ?? Array.Empty<SkillCastTraceResult>();
        }

        public bool Passed { get; }
        public IReadOnlyList<SkillCastTraceResult> Results { get; }
    }

    public static class SkillCastTraceValidator
    {
        private static readonly string[] RequiredProtectedCues =
        {
            "timing",
            "target",
            "danger",
            "objective",
            "ownership",
            "accessibility"
        };

        public static bool TryValidate(string json, out SkillCastTraceReport report)
        {
            report = null;
            if (!SkillCastPresentationCatalog.TryParse(json, out SkillCastPresentationSnapshot snapshot))
            {
                report = new SkillCastTraceReport(
                    false,
                    new[]
                    {
                        new SkillCastTraceResult(
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            false,
                            "catalog_unreadable")
                    });
                return false;
            }

            var results = new List<SkillCastTraceResult>(snapshot.Bindings.Count);
            bool passed = true;
            for (int index = 0; index < snapshot.Bindings.Count; index++)
            {
                SkillCastTraceResult result = Trace(snapshot, snapshot.Bindings[index]);
                results.Add(result);
                passed &= result.Passed;
            }

            if (!ContainsAll(snapshot.Policy.NeverHide, RequiredProtectedCues))
            {
                results.Add(new SkillCastTraceResult(
                    "protected_cue_policy_runtime",
                    "policy",
                    "protected_cue",
                    false,
                    "missing_protected_cues"));
                passed = false;
            }

            report = new SkillCastTraceReport(passed, results);
            return passed;
        }

        private static SkillCastTraceResult Trace(
            SkillCastPresentationSnapshot snapshot,
            SkillCastBinding binding)
        {
            if (!HasMotion(snapshot, binding, out string motionFailure))
            {
                return Fail(binding, motionFailure);
            }

            if (!TryModule(snapshot, binding.TelegraphModuleId, "telegraph", out string telegraphFailure) &&
                !TryModule(snapshot, binding.TelegraphModuleId, null, out telegraphFailure))
            {
                return Fail(binding, telegraphFailure);
            }

            if (!TryModule(snapshot, binding.ActiveEffectModuleId, null, out string activeFailure))
            {
                return Fail(binding, activeFailure);
            }

            if (!TryModule(snapshot, binding.ImpactModuleId, null, out string impactFailure))
            {
                return Fail(binding, impactFailure);
            }

            if (!TryModule(snapshot, binding.CleanupModuleId, null, out string cleanupFailure))
            {
                return Fail(binding, cleanupFailure);
            }

            if (!TryModule(snapshot, binding.AccessibilityVariantId, null, out string a11yFailure))
            {
                return Fail(binding, a11yFailure);
            }

            return new SkillCastTraceResult(
                binding.SkillId,
                binding.ActorFamily,
                binding.Role,
                true,
                string.Empty);
        }

        private static bool HasMotion(
            SkillCastPresentationSnapshot snapshot,
            SkillCastBinding binding,
            out string failure)
        {
            failure = string.Empty;
            if (!TryModule(snapshot, binding.MotionAnticipationId, "motion", out failure) ||
                !TryModule(snapshot, binding.MotionCastId, "motion", out failure) ||
                !TryModule(snapshot, binding.MotionChannelId, "motion", out failure) ||
                !TryModule(snapshot, binding.MotionReleaseId, "motion", out failure) ||
                !TryModule(snapshot, binding.MotionRecoveryId, "motion", out failure))
            {
                return false;
            }

            return true;
        }

        private static bool TryModule(
            SkillCastPresentationSnapshot snapshot,
            string moduleId,
            string expectedKind,
            out string failure)
        {
            failure = string.Empty;
            if (string.IsNullOrWhiteSpace(moduleId) ||
                !snapshot.Modules.TryGetValue(moduleId, out SkillEffectModule module))
            {
                failure = "missing_module:" + moduleId;
                return false;
            }

            if (!string.IsNullOrEmpty(expectedKind) &&
                !string.Equals(module.ModuleKind, expectedKind, StringComparison.Ordinal))
            {
                failure = "wrong_module_kind:" + moduleId;
                return false;
            }

            if (string.IsNullOrWhiteSpace(module.ShapeId) ||
                string.IsNullOrWhiteSpace(module.PrefabId) ||
                module.ProtectedCues.Count == 0 ||
                !module.OwnershipReadable)
            {
                failure = "incomplete_module:" + moduleId;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(module.ReducedMotionVariantId) &&
                !snapshot.Modules.ContainsKey(module.ReducedMotionVariantId))
            {
                failure = "missing_reduced_motion:" + module.ReducedMotionVariantId;
                return false;
            }

            return true;
        }

        private static SkillCastTraceResult Fail(SkillCastBinding binding, string failure)
        {
            return new SkillCastTraceResult(
                binding.SkillId,
                binding.ActorFamily,
                binding.Role,
                false,
                failure);
        }

        private static bool ContainsAll(IReadOnlyList<string> haystack, string[] needles)
        {
            for (int needle = 0; needle < needles.Length; needle++)
            {
                bool found = false;
                for (int index = 0; index < haystack.Count; index++)
                {
                    if (string.Equals(haystack[index], needles[needle], StringComparison.Ordinal))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
