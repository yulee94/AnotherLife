using AL.Core;
using UnityEngine;

namespace AL.ChampionMode.Skills
{
    public static class SkillCastPresentationSettings
    {
        public static int DensityTier { get; set; }
        public static bool ReducedMotion { get; set; }
        public static bool PhotosensitivitySafe { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            DensityTier = 0;
            ReducedMotion = false;
            PhotosensitivitySafe = false;
        }
    }

    public static class SkillCastEffectRouter
    {
        public static GameObject PlayTelegraph(
            SkillCastBinding binding,
            Vector3 casterPosition,
            Vector3 targetPosition,
            Vector3 forward,
            RealmId realmId,
            float radius,
            float lifetime)
        {
            if (binding == null)
            {
                return null;
            }

            SkillEffectModule module = ResolveModule(binding.TelegraphModuleId);
            if (module == null)
            {
                return null;
            }

            if (string.Equals(module.ModuleKind, "telegraph", System.StringComparison.Ordinal) &&
                string.Equals(module.ShapeId, "disk", System.StringComparison.Ordinal))
            {
                return SkillEffectFactory.SpawnBossSlamTelegraph(
                    targetPosition,
                    casterPosition,
                    radius,
                    lifetime,
                    false);
            }

            SkillEffectFactory.SpawnSkillCastRing(casterPosition, realmId, radius, lifetime);
            if (!SuppressSecondary(module) &&
                !string.Equals(binding.Role, "self_heal_guard", System.StringComparison.Ordinal))
            {
                SkillEffectFactory.SpawnSkillTargetPreview(
                    casterPosition,
                    targetPosition,
                    forward,
                    realmId,
                    radius,
                    lifetime);
            }

            PlayAccessibilityMarker(binding, targetPosition, realmId);
            return null;
        }

        public static GameObject PlayActive(
            SkillCastBinding binding,
            Vector3 groundCenter,
            Vector3 forward,
            RealmId realmId,
            float radius)
        {
            if (binding == null)
            {
                return null;
            }

            SkillEffectModule module = ResolveModule(binding.ActiveEffectModuleId);
            if (module == null)
            {
                return null;
            }

            switch (module.ModuleKind)
            {
                case "trail":
                    return SkillEffectFactory.SpawnRealmSlash(groundCenter, forward, realmId);
                case "shield":
                    return SkillEffectFactory.SpawnRenewingGuard(groundCenter, realmId);
                case "area":
                    return SkillEffectFactory.SpawnWarzoneShockwave(groundCenter, realmId, radius);
                case "impact" when string.Equals(module.ShapeId, "burst", System.StringComparison.Ordinal):
                    return SkillEffectFactory.SpawnWarmasterBreaker(groundCenter, realmId, radius);
                case "projectile":
                case "beam":
                    SkillEffectFactory.SpawnSkillTargetPreview(
                        groundCenter - forward * Mathf.Max(1.5f, radius),
                        groundCenter,
                        forward,
                        realmId,
                        radius,
                        0.35f);
                    return SkillEffectFactory.SpawnRealmImpact(groundCenter, realmId);
                case "summon":
                    return SkillEffectFactory.SpawnSkillCastRing(groundCenter, realmId, radius, 0.45f);
                case "buff":
                    return SkillEffectFactory.SpawnRenewingGuard(groundCenter, realmId);
                case "debuff":
                    return SkillEffectFactory.SpawnWarzoneShockwave(groundCenter, realmId, radius);
                case "environment":
                    return SkillEffectFactory.SpawnSkillCastRing(groundCenter, realmId, radius, 0.20f);
                default:
                    return SkillEffectFactory.SpawnRealmImpact(groundCenter, realmId);
            }
        }

        public static GameObject PlayImpact(
            SkillCastBinding binding,
            Vector3 position,
            RealmId realmId)
        {
            if (binding == null)
            {
                return null;
            }

            SkillEffectModule module = ResolveModule(binding.ImpactModuleId);
            if (module == null)
            {
                return SkillEffectFactory.SpawnRealmImpact(position, realmId);
            }

            if (string.Equals(module.ModuleKind, "heal", System.StringComparison.Ordinal))
            {
                return SkillEffectFactory.SpawnHealingBloom(position);
            }

            return SkillEffectFactory.SpawnRealmImpact(position, realmId);
        }

        public static void PlayCleanup(SkillCastBinding binding, Vector3 position, RealmId realmId)
        {
            if (binding == null || SuppressSecondary(ResolveModule(binding.CleanupModuleId)))
            {
                return;
            }

            SkillEffectFactory.SpawnSkillCastRing(position, realmId, 0.85f, 0.12f);
        }

        public static bool AllowsCameraImpulse()
        {
            return !SkillCastPresentationSettings.ReducedMotion &&
                   !SkillCastPresentationSettings.PhotosensitivitySafe;
        }

        public static bool IsProtectedCue(string cue)
        {
            if (!SkillCastPresentationCatalog.TryLoad(out SkillCastPresentationSnapshot snapshot))
            {
                return cue == "timing" ||
                       cue == "target" ||
                       cue == "danger" ||
                       cue == "objective" ||
                       cue == "ownership" ||
                       cue == "accessibility";
            }

            for (int index = 0; index < snapshot.Policy.NeverHide.Count; index++)
            {
                if (string.Equals(snapshot.Policy.NeverHide[index], cue, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SuppressSecondary(SkillEffectModule module)
        {
            if (SkillCastPresentationSettings.DensityTier <= 0 &&
                !SkillCastPresentationSettings.ReducedMotion &&
                !SkillCastPresentationSettings.PhotosensitivitySafe)
            {
                return false;
            }

            if (module == null)
            {
                return true;
            }

            for (int index = 0; index < module.ProtectedCues.Count; index++)
            {
                if (IsProtectedCue(module.ProtectedCues[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static void PlayAccessibilityMarker(
            SkillCastBinding binding,
            Vector3 position,
            RealmId realmId)
        {
            SkillEffectModule variant = ResolveModule(binding.AccessibilityVariantId);
            if (variant == null)
            {
                return;
            }

            SkillEffectFactory.SpawnSkillCastRing(position, realmId, 0.55f, 0.20f);
        }

        private static SkillEffectModule ResolveModule(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId) ||
                !SkillCastPresentationCatalog.TryLoad(out SkillCastPresentationSnapshot snapshot) ||
                !snapshot.Modules.TryGetValue(moduleId, out SkillEffectModule module))
            {
                return null;
            }

            if ((SkillCastPresentationSettings.ReducedMotion ||
                 SkillCastPresentationSettings.PhotosensitivitySafe) &&
                snapshot.Modules.TryGetValue(module.ReducedMotionVariantId, out SkillEffectModule reduced))
            {
                return reduced;
            }

            return module;
        }
    }
}
