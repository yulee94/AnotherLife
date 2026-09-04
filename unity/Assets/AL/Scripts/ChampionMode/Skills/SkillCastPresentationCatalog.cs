using System;
using System.Collections.Generic;
using System.IO;
using AL.Data.Catalogs;
using AL.Services.Local;
using UnityEngine;

namespace AL.ChampionMode.Skills
{
    public sealed class SkillCastBinding
    {
        public SkillCastBinding(
            string id,
            string skillId,
            string actorFamily,
            string role,
            string motionAnticipationId,
            string motionCastId,
            string motionChannelId,
            string motionReleaseId,
            string motionRecoveryId,
            string telegraphModuleId,
            string activeEffectModuleId,
            string impactModuleId,
            string cleanupModuleId,
            string accessibilityVariantId)
        {
            Id = id ?? string.Empty;
            SkillId = skillId ?? string.Empty;
            ActorFamily = actorFamily ?? string.Empty;
            Role = role ?? string.Empty;
            MotionAnticipationId = motionAnticipationId ?? string.Empty;
            MotionCastId = motionCastId ?? string.Empty;
            MotionChannelId = motionChannelId ?? string.Empty;
            MotionReleaseId = motionReleaseId ?? string.Empty;
            MotionRecoveryId = motionRecoveryId ?? string.Empty;
            TelegraphModuleId = telegraphModuleId ?? string.Empty;
            ActiveEffectModuleId = activeEffectModuleId ?? string.Empty;
            ImpactModuleId = impactModuleId ?? string.Empty;
            CleanupModuleId = cleanupModuleId ?? string.Empty;
            AccessibilityVariantId = accessibilityVariantId ?? string.Empty;
        }

        public string Id { get; }
        public string SkillId { get; }
        public string ActorFamily { get; }
        public string Role { get; }
        public string MotionAnticipationId { get; }
        public string MotionCastId { get; }
        public string MotionChannelId { get; }
        public string MotionReleaseId { get; }
        public string MotionRecoveryId { get; }
        public string TelegraphModuleId { get; }
        public string ActiveEffectModuleId { get; }
        public string ImpactModuleId { get; }
        public string CleanupModuleId { get; }
        public string AccessibilityVariantId { get; }
    }

    public sealed class SkillEffectModule
    {
        public SkillEffectModule(
            string id,
            string moduleKind,
            string shapeId,
            string prefabId,
            string[] protectedCues,
            string reducedMotionVariantId,
            bool ownershipReadable,
            string seedPolicy)
        {
            Id = id ?? string.Empty;
            ModuleKind = moduleKind ?? string.Empty;
            ShapeId = shapeId ?? string.Empty;
            PrefabId = prefabId ?? string.Empty;
            ProtectedCues = protectedCues ?? Array.Empty<string>();
            ReducedMotionVariantId = reducedMotionVariantId ?? string.Empty;
            OwnershipReadable = ownershipReadable;
            SeedPolicy = seedPolicy ?? string.Empty;
        }

        public string Id { get; }
        public string ModuleKind { get; }
        public string ShapeId { get; }
        public string PrefabId { get; }
        public IReadOnlyList<string> ProtectedCues { get; }
        public string ReducedMotionVariantId { get; }
        public bool OwnershipReadable { get; }
        public string SeedPolicy { get; }
    }

    public sealed class ProtectedCuePolicy
    {
        public ProtectedCuePolicy(
            string[] neverHide,
            string[] scalable,
            bool reducedMotion,
            bool photosensitivity)
        {
            NeverHide = neverHide ?? Array.Empty<string>();
            Scalable = scalable ?? Array.Empty<string>();
            ReducedMotion = reducedMotion;
            Photosensitivity = photosensitivity;
        }

        public IReadOnlyList<string> NeverHide { get; }
        public IReadOnlyList<string> Scalable { get; }
        public bool ReducedMotion { get; }
        public bool Photosensitivity { get; }
    }

    public sealed class SkillCastPresentationSnapshot
    {
        internal SkillCastPresentationSnapshot(
            IReadOnlyList<SkillCastBinding> bindings,
            IReadOnlyDictionary<string, SkillEffectModule> modules,
            ProtectedCuePolicy policy)
        {
            Bindings = bindings;
            Modules = modules;
            Policy = policy;
        }

        public IReadOnlyList<SkillCastBinding> Bindings { get; }
        public IReadOnlyDictionary<string, SkillEffectModule> Modules { get; }
        public ProtectedCuePolicy Policy { get; }

        public bool TryGetBinding(string skillId, out SkillCastBinding binding)
        {
            binding = null;
            if (string.IsNullOrWhiteSpace(skillId))
            {
                return false;
            }

            for (int index = 0; index < Bindings.Count; index++)
            {
                SkillCastBinding candidate = Bindings[index];
                if (string.Equals(candidate.SkillId, skillId, StringComparison.Ordinal) ||
                    string.Equals(candidate.Id, skillId, StringComparison.Ordinal))
                {
                    binding = candidate;
                    return true;
                }
            }

            return false;
        }
    }

    public static class SkillCastPresentationCatalog
    {
        private static SkillCastPresentationSnapshot _cached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _cached = null;
        }

        public static bool TryLoad(out SkillCastPresentationSnapshot snapshot)
        {
            snapshot = _cached;
            if (snapshot != null)
            {
                return true;
            }

            string path = BuildCatalogPath();
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                if (!TryParse(File.ReadAllText(path), out snapshot))
                {
                    return false;
                }

                _cached = snapshot;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[SkillCastPresentationCatalog] Could not load skill-cast presentation. " +
                    exception.Message);
                return false;
            }
        }

        public static bool TryParse(string json, out SkillCastPresentationSnapshot snapshot)
        {
            snapshot = null;
            if (!WireFamilyCatalogLoader.TryLoad(
                    "skill_weather",
                    json,
                    out GameDataFamilyCatalogSnapshot family,
                    out _))
            {
                return false;
            }

            var modules = new Dictionary<string, SkillEffectModule>(StringComparer.Ordinal);
            var moduleRecords = WireFamilyCatalogLoader.RecordsOfKind(family, "effect_module");
            for (int index = 0; index < moduleRecords.Count; index++)
            {
                if (!TryReadModule(moduleRecords[index], out SkillEffectModule module) ||
                    modules.ContainsKey(module.Id))
                {
                    return false;
                }

                modules.Add(module.Id, module);
            }

            var bindings = new List<SkillCastBinding>();
            var loadouts = WireFamilyCatalogLoader.RecordsOfKind(family, "skill_loadout");
            for (int index = 0; index < loadouts.Count; index++)
            {
                if (!TryReadBinding(loadouts[index], loadouts[index].Id, "champion", out SkillCastBinding binding))
                {
                    return false;
                }

                bindings.Add(binding);
            }

            var extras = WireFamilyCatalogLoader.RecordsOfKind(family, "skill_cast_binding");
            for (int index = 0; index < extras.Count; index++)
            {
                GameDataCatalogRecord record = extras[index];
                if (!WireFamilyCatalogLoader.TryGetString(record, "skill_id", out string skillId) ||
                    !WireFamilyCatalogLoader.TryGetString(record, "actor_family", out string actorFamily) ||
                    !TryReadBinding(record, skillId, actorFamily, out SkillCastBinding binding))
                {
                    return false;
                }

                bindings.Add(binding);
            }

            var policies = WireFamilyCatalogLoader.RecordsOfKind(family, "protected_cue_policy");
            if (policies.Count != 1 || !TryReadPolicy(policies[0], out ProtectedCuePolicy policy))
            {
                return false;
            }

            if (bindings.Count == 0 || modules.Count == 0)
            {
                return false;
            }

            snapshot = new SkillCastPresentationSnapshot(bindings, modules, policy);
            return true;
        }

        private static bool TryReadBinding(
            GameDataCatalogRecord record,
            string skillId,
            string actorFamily,
            out SkillCastBinding binding)
        {
            binding = null;
            string role;
            string anticipation;
            string cast;
            string channel;
            string release;
            string recovery;
            string telegraph;
            string active;
            string impact;
            string cleanup;
            string accessibility;
            if (!WireFamilyCatalogLoader.TryGetString(record, "role", out role) ||
                !WireFamilyCatalogLoader.TryGetString(record, "motion_anticipation_id", out anticipation) ||
                !WireFamilyCatalogLoader.TryGetString(record, "motion_cast_id", out cast) ||
                !WireFamilyCatalogLoader.TryGetString(record, "motion_channel_id", out channel) ||
                !WireFamilyCatalogLoader.TryGetString(record, "motion_release_id", out release) ||
                !WireFamilyCatalogLoader.TryGetString(record, "motion_recovery_id", out recovery) ||
                !WireFamilyCatalogLoader.TryGetString(record, "telegraph_module_id", out telegraph) ||
                !WireFamilyCatalogLoader.TryGetString(record, "active_effect_module_id", out active) ||
                !WireFamilyCatalogLoader.TryGetString(record, "impact_module_id", out impact) ||
                !WireFamilyCatalogLoader.TryGetString(record, "cleanup_module_id", out cleanup) ||
                !WireFamilyCatalogLoader.TryGetString(record, "accessibility_variant_id", out accessibility) ||
                string.IsNullOrWhiteSpace(skillId) ||
                string.IsNullOrWhiteSpace(actorFamily))
            {
                return false;
            }

            binding = new SkillCastBinding(
                record.Id,
                skillId,
                actorFamily,
                role,
                anticipation,
                cast,
                channel,
                release,
                recovery,
                telegraph,
                active,
                impact,
                cleanup,
                accessibility);
            return true;
        }

        private static bool TryReadModule(GameDataCatalogRecord record, out SkillEffectModule module)
        {
            module = null;
            if (!WireFamilyCatalogLoader.TryGetString(record, "module_kind", out string moduleKind) ||
                !WireFamilyCatalogLoader.TryGetString(record, "shape_id", out string shapeId) ||
                !WireFamilyCatalogLoader.TryGetString(record, "prefab_id", out string prefabId) ||
                !WireFamilyCatalogLoader.TryGetString(record, "reduced_motion_variant_id", out string reduced) ||
                !WireFamilyCatalogLoader.TryGetString(record, "seed_policy", out string seedPolicy) ||
                !WireFamilyCatalogLoader.TryGetBool(record, "ownership_readable", out bool ownership) ||
                !WireFamilyCatalogLoader.TryGetStringArray(record, "protected_cues", out string[] cues) ||
                cues.Length == 0)
            {
                return false;
            }

            module = new SkillEffectModule(
                record.Id,
                moduleKind,
                shapeId,
                prefabId,
                cues,
                reduced,
                ownership,
                seedPolicy);
            return true;
        }

        private static bool TryReadPolicy(GameDataCatalogRecord record, out ProtectedCuePolicy policy)
        {
            policy = null;
            if (!WireFamilyCatalogLoader.TryGetStringArray(record, "never_hide", out string[] neverHide) ||
                !WireFamilyCatalogLoader.TryGetStringArray(record, "scalable", out string[] scalable) ||
                !WireFamilyCatalogLoader.TryGetBool(record, "reduced_motion", out bool reducedMotion) ||
                !WireFamilyCatalogLoader.TryGetBool(record, "photosensitivity", out bool photosensitivity) ||
                neverHide.Length == 0)
            {
                return false;
            }

            policy = new ProtectedCuePolicy(neverHide, scalable, reducedMotion, photosensitivity);
            return true;
        }

        private static string BuildCatalogPath()
        {
            if (SixFamilyRuntimeCatalog.TryResolveGameDataDirectory(out string gameDataDirectory))
            {
                return Path.Combine(gameDataDirectory, "skill_weather.v1.json");
            }

            return Application.streamingAssetsPath.TrimEnd('/', '\\') +
                   "/GameData/skill_weather.v1.json";
        }
    }
}
