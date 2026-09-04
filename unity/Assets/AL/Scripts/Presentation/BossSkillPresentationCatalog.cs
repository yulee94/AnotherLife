using System;
using System.Collections.Generic;
using System.IO;
using AL.Services.Local;
using UnityEngine;

namespace AL.Presentation
{
    [Serializable]
    public sealed class PresentationAuthority
    {
        public string catalogOwner;
        public string finalCreativeOwner;
        public string status;
        public string ownerDecisionRef;
        public bool gameplayAuthority;
        public bool runtimeSpawn;
    }

    [Serializable]
    public sealed class PresentationQualityTier
    {
        public string id;
        public string lodId;
        public int maxParticles;
        public int maxLights;
        public int maxTrails;
        public bool cameraImpulse;
        public bool flashAllowed;
    }

    [Serializable]
    public sealed class PresentationDistanceContext
    {
        public string id;
        public string lodId;
        public float cosmeticScale;
        public bool telegraphProtected;
    }

    [Serializable]
    public sealed class PresentationAccessibility
    {
        public string reducedMotionVariantId;
        public bool reducedFlash;
        public string[] nonColorCues;
    }

    [Serializable]
    public sealed class PresentationPooling
    {
        public int maxActive;
        public int maxPooled;
        public bool resetOnRelease;
    }

    [Serializable]
    public sealed class PresentationPhases
    {
        public string anticipation;
        public string cast;
        public string channel;
        public string release;
        public string recovery;
    }

    [Serializable]
    public sealed class PresentationEffects
    {
        public string telegraph;
        public string active;
        public string impact;
        public string cleanup;
        public string accessibility;
    }

    [Serializable]
    public sealed class BossPresentationProfile
    {
        public string id;
        public string kind;
        public string realmId;
        public string sourceProfileId;
        public string modelId;
        public string qualificationId;
        public string sourceVersion;
        public string sourceSha256;
        public string source2dPath;
        public string source2dSha256;
        public string assetState;
        public string prefabRef;
        public string rigId;
        public string materialId;
        public string[] motionKeys;
        public string[] protectedIdentityCues;
        public PresentationQualityTier[] qualityTiers;
        public PresentationDistanceContext[] distanceContexts;
        public PresentationAccessibility accessibility;
        public PresentationPooling pooling;
        public string unavailableFallback;
    }

    [Serializable]
    public sealed class SkillPresentationProfile
    {
        public string id;
        public string skillId;
        public string vfxId;
        public string actorFamily;
        public bool presentationIndependentOfPower;
        public string telegraphChannel;
        public string cosmeticChannel;
        public PresentationPhases phases;
        public PresentationEffects effects;
        public PresentationQualityTier[] qualityTiers;
        public PresentationDistanceContext[] distanceContexts;
        public PresentationAccessibility accessibility;
        public PresentationPooling pooling;
        public string unavailableFallback;
    }

    [Serializable]
    public sealed class BossSkillPresentationFile
    {
        public string gameId;
        public string catalogId;
        public int schemaVersion;
        public string contentVersion;
        public PresentationAuthority authority;
        public BossPresentationProfile[] bossProfiles;
        public SkillPresentationProfile[] skillProfiles;
    }

    public sealed class FrozenGameplaySnapshot
    {
        public FrozenGameplaySnapshot(string skillId)
        {
            SkillId = skillId ?? string.Empty;
            Source = "skill_weather_cast_binding";
            PresentationCannotMutate = true;
        }

        public string SkillId { get; }
        public string Source { get; }
        public bool PresentationCannotMutate { get; }
    }

    public sealed class ResolvedPresentation
    {
        public ResolvedPresentation(
            string modelId,
            string quality,
            string distance,
            FrozenGameplaySnapshot gameplay,
            string bossLod,
            float cosmeticScale,
            int maxActive)
        {
            ModelId = modelId;
            Quality = quality;
            Distance = distance;
            Gameplay = gameplay;
            BossLod = bossLod;
            CosmeticScale = cosmeticScale;
            MaxActive = maxActive;
            ProtectedCuesPreserved = true;
        }

        public string ModelId { get; }
        public string Quality { get; }
        public string Distance { get; }
        public FrozenGameplaySnapshot Gameplay { get; }
        public string BossLod { get; }
        public float CosmeticScale { get; }
        public int MaxActive { get; }
        public bool ProtectedCuesPreserved { get; }
    }

    public sealed class PresentationPool
    {
        private readonly List<int> free = new List<int>();
        private int nextId = 1;

        public PresentationPool(int maxActive, int maxPooled)
        {
            MaxActive = maxActive;
            MaxPooled = maxPooled;
        }

        public int MaxActive { get; }
        public int MaxPooled { get; }
        public int Created { get; private set; }
        public int Active { get; private set; }

        public int Acquire()
        {
            if (Active >= MaxActive)
            {
                return 0;
            }

            int instanceId;
            if (free.Count > 0)
            {
                instanceId = free[free.Count - 1];
                free.RemoveAt(free.Count - 1);
            }
            else
            {
                instanceId = nextId++;
                Created++;
            }

            Active++;
            return instanceId;
        }

        public void Release(int instanceId)
        {
            if (instanceId <= 0 || Active <= 0)
            {
                return;
            }

            Active--;
            if (free.Count < MaxPooled)
            {
                free.Add(instanceId);
            }
        }
    }

    public sealed class BossSkillPresentationSnapshot
    {
        internal BossSkillPresentationSnapshot(
            BossPresentationProfile[] bosses,
            SkillPresentationProfile[] skills,
            BossPresentationProfile boss,
            SkillPresentationProfile skill)
        {
            Bosses = bosses;
            Skills = skills;
            Boss = boss;
            Skill = skill;
        }

        public BossPresentationProfile[] Bosses { get; }
        public SkillPresentationProfile[] Skills { get; }
        public BossPresentationProfile Boss { get; }
        public SkillPresentationProfile Skill { get; }
    }

    public static class BossSkillPresentationCatalog
    {
        public const string CatalogFileName = "al_boss_skill_presentation_catalog.v1.json";
        public const string ExpectedBossId = "boss_presentation_stonehold_fault_crowned_colossus_v001";
        public const string ExpectedModelId = "boss_stonehold_fault_crowned_colossus";
        public const string ExpectedSkillId = "boss_faultline_slam";
        public const string ExpectedSkillProfileId = "skill_presentation_boss_faultline_slam_v001";

        private static readonly string[] ForbiddenKeys =
        {
            "\"slot\":",
            "\"ItemGrade\":",
            "\"itemGrade\":",
            "\"item_grade\":",
            "\"power\":",
            "\"damage\":",
            "\"cooldown_seconds\":",
            "\"mana_cost\":",
            "\"threat\":",
            "\"loot\":",
            "\"spawn\":"
        };

        private static BossSkillPresentationSnapshot cached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cached = null;
        }

        public static bool TryLoad(out BossSkillPresentationSnapshot snapshot)
        {
            snapshot = cached;
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

                cached = snapshot;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[BossSkillPresentationCatalog] Could not load presentation catalog. " +
                    exception.Message);
                return false;
            }
        }

        public static bool TryParse(string json, out BossSkillPresentationSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(json) || ContainsForbiddenKey(json))
            {
                return false;
            }

            BossSkillPresentationFile file;
            try
            {
                file = JsonUtility.FromJson<BossSkillPresentationFile>(json);
            }
            catch (Exception)
            {
                return false;
            }

            if (file == null ||
                file.schemaVersion != 1 ||
                file.authority == null ||
                file.authority.gameplayAuthority ||
                file.authority.runtimeSpawn ||
                file.bossProfiles == null ||
                file.bossProfiles.Length == 0 ||
                file.skillProfiles == null ||
                file.skillProfiles.Length == 0 ||
                !HasUniqueProfileIds(file.bossProfiles) ||
                !HasUniqueProfileIds(file.skillProfiles))
            {
                return false;
            }

            BossPresentationProfile boss = null;
            for (int index = 0; index < file.bossProfiles.Length; index++)
            {
                BossPresentationProfile candidate = file.bossProfiles[index];
                if (!IsValidBoss(candidate))
                {
                    return false;
                }

                if (candidate.id == ExpectedBossId)
                {
                    boss = candidate;
                }
            }

            SkillPresentationProfile skill = null;
            for (int index = 0; index < file.skillProfiles.Length; index++)
            {
                SkillPresentationProfile candidate = file.skillProfiles[index];
                if (!IsValidSkill(candidate))
                {
                    return false;
                }

                if (candidate.id == ExpectedSkillProfileId)
                {
                    skill = candidate;
                }
            }

            if (boss == null || skill == null)
            {
                return false;
            }

            snapshot = new BossSkillPresentationSnapshot(
                file.bossProfiles,
                file.skillProfiles,
                boss,
                skill);
            return true;
        }

        public static bool TryResolve(
            BossSkillPresentationSnapshot snapshot,
            string quality,
            string distance,
            out ResolvedPresentation resolved)
        {
            return TryResolve(
                snapshot,
                ExpectedModelId,
                ExpectedSkillId,
                quality,
                distance,
                out resolved);
        }

        public static bool TryResolve(
            BossSkillPresentationSnapshot snapshot,
            string modelId,
            string skillId,
            string quality,
            string distance,
            out ResolvedPresentation resolved)
        {
            resolved = null;
            if (snapshot == null ||
                string.IsNullOrWhiteSpace(modelId) ||
                string.IsNullOrWhiteSpace(skillId) ||
                string.IsNullOrWhiteSpace(quality) ||
                string.IsNullOrWhiteSpace(distance))
            {
                return false;
            }

            BossPresentationProfile boss = FindBoss(snapshot.Bosses, modelId);
            SkillPresentationProfile skill = FindSkill(snapshot.Skills, skillId);
            if (boss == null || skill == null || boss.assetState != "source_qualified")
            {
                return false;
            }

            PresentationQualityTier qualityTier = FindQuality(boss.qualityTiers, quality);
            PresentationDistanceContext distanceContext = FindDistance(skill.distanceContexts, distance);
            if (qualityTier == null || distanceContext == null || !distanceContext.telegraphProtected)
            {
                return false;
            }

            resolved = new ResolvedPresentation(
                boss.modelId,
                quality,
                distance,
                new FrozenGameplaySnapshot(skill.skillId),
                qualityTier.lodId,
                distanceContext.cosmeticScale,
                skill.pooling.maxActive);
            return true;
        }

        private static bool ContainsForbiddenKey(string json)
        {
            for (int index = 0; index < ForbiddenKeys.Length; index++)
            {
                if (json.IndexOf(ForbiddenKeys[index], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsValidBoss(BossPresentationProfile boss)
        {
            return boss != null &&
                   !string.IsNullOrWhiteSpace(boss.id) &&
                   !string.IsNullOrWhiteSpace(boss.modelId) &&
                   !string.IsNullOrWhiteSpace(boss.sourceProfileId) &&
                   IsValidAssetState(boss) &&
                   HasTiers(boss.qualityTiers) &&
                   HasDistances(boss.distanceContexts) &&
                   IsValidPooling(boss.pooling) &&
                   boss.accessibility != null &&
                   boss.accessibility.reducedFlash;
        }

        private static bool IsValidSkill(SkillPresentationProfile skill)
        {
            return skill != null &&
                   !string.IsNullOrWhiteSpace(skill.id) &&
                   !string.IsNullOrWhiteSpace(skill.skillId) &&
                   skill.id == "skill_presentation_" + skill.skillId + "_v001" &&
                   skill.presentationIndependentOfPower &&
                   skill.phases != null &&
                   !string.IsNullOrWhiteSpace(skill.phases.anticipation) &&
                   !string.IsNullOrWhiteSpace(skill.phases.cast) &&
                   !string.IsNullOrWhiteSpace(skill.phases.channel) &&
                   !string.IsNullOrWhiteSpace(skill.phases.release) &&
                   !string.IsNullOrWhiteSpace(skill.phases.recovery) &&
                   skill.effects != null &&
                   !string.IsNullOrWhiteSpace(skill.effects.telegraph) &&
                   !string.IsNullOrWhiteSpace(skill.effects.active) &&
                   !string.IsNullOrWhiteSpace(skill.effects.impact) &&
                   !string.IsNullOrWhiteSpace(skill.effects.cleanup) &&
                   !string.IsNullOrWhiteSpace(skill.effects.accessibility) &&
                   HasTiers(skill.qualityTiers) &&
                   HasDistances(skill.distanceContexts) &&
                   IsValidPooling(skill.pooling) &&
                   skill.accessibility != null &&
                   skill.accessibility.reducedFlash;
        }

        private static bool IsValidAssetState(BossPresentationProfile boss)
        {
            if (boss.assetState == "source_qualified")
            {
                return !string.IsNullOrWhiteSpace(boss.qualificationId) &&
                       !string.IsNullOrWhiteSpace(boss.sourceVersion) &&
                       !string.IsNullOrWhiteSpace(boss.sourceSha256) &&
                       !string.IsNullOrWhiteSpace(boss.prefabRef) &&
                       !string.IsNullOrWhiteSpace(boss.rigId) &&
                       !string.IsNullOrWhiteSpace(boss.materialId) &&
                       HasMotion(boss.motionKeys);
            }

            return boss.assetState == "source_only" &&
                   boss.qualificationId == "explicit_unavailable" &&
                   boss.prefabRef == "explicit_unavailable" &&
                   boss.rigId == "explicit_unavailable" &&
                   boss.materialId == "explicit_unavailable" &&
                   boss.motionKeys != null &&
                   boss.motionKeys.Length == 0;
        }

        private static bool HasUniqueProfileIds(BossPresentationProfile[] profiles)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var modelIds = new HashSet<string>(StringComparer.Ordinal);
            var sourceIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < profiles.Length; index++)
            {
                BossPresentationProfile profile = profiles[index];
                if (profile == null ||
                    !ids.Add(profile.id) ||
                    !modelIds.Add(profile.modelId) ||
                    !sourceIds.Add(profile.sourceProfileId))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasUniqueProfileIds(SkillPresentationProfile[] profiles)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var skillIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < profiles.Length; index++)
            {
                SkillPresentationProfile profile = profiles[index];
                if (profile == null || !ids.Add(profile.id) || !skillIds.Add(profile.skillId))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasMotion(string[] keys)
        {
            return Contains(keys, "locomotion.walk") &&
                   Contains(keys, "locomotion.run") &&
                   Contains(keys, "attack.basic") &&
                   Contains(keys, "attack.special") &&
                   Contains(keys, "skill.anticipation");
        }

        private static bool HasTiers(PresentationQualityTier[] tiers)
        {
            return tiers != null &&
                   tiers.Length == 3 &&
                   tiers[0] != null && tiers[0].id == "low" &&
                   tiers[1] != null && tiers[1].id == "balanced" &&
                   tiers[2] != null && tiers[2].id == "high";
        }

        private static bool HasDistances(PresentationDistanceContext[] contexts)
        {
            return contexts != null &&
                   contexts.Length == 3 &&
                   contexts[0] != null && contexts[0].id == "hero" && contexts[0].telegraphProtected &&
                   contexts[1] != null && contexts[1].id == "nearby" && contexts[1].telegraphProtected &&
                   contexts[2] != null && contexts[2].id == "distant" && contexts[2].telegraphProtected;
        }

        private static bool IsValidPooling(PresentationPooling pooling)
        {
            return pooling != null && pooling.maxActive >= 1 && pooling.maxPooled >= 1 && pooling.resetOnRelease;
        }

        private static bool Contains(string[] values, string expected)
        {
            if (values == null)
            {
                return false;
            }

            for (int index = 0; index < values.Length; index++)
            {
                if (string.Equals(values[index], expected, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static PresentationQualityTier FindQuality(PresentationQualityTier[] tiers, string id)
        {
            if (tiers == null)
            {
                return null;
            }

            for (int index = 0; index < tiers.Length; index++)
            {
                if (tiers[index] != null && string.Equals(tiers[index].id, id, StringComparison.Ordinal))
                {
                    return tiers[index];
                }
            }

            return null;
        }

        private static BossPresentationProfile FindBoss(BossPresentationProfile[] profiles, string modelId)
        {
            if (profiles == null)
            {
                return null;
            }

            for (int index = 0; index < profiles.Length; index++)
            {
                if (profiles[index] != null &&
                    string.Equals(profiles[index].modelId, modelId, StringComparison.Ordinal))
                {
                    return profiles[index];
                }
            }

            return null;
        }

        private static SkillPresentationProfile FindSkill(SkillPresentationProfile[] profiles, string skillId)
        {
            if (profiles == null)
            {
                return null;
            }

            for (int index = 0; index < profiles.Length; index++)
            {
                if (profiles[index] != null &&
                    string.Equals(profiles[index].skillId, skillId, StringComparison.Ordinal))
                {
                    return profiles[index];
                }
            }

            return null;
        }

        private static PresentationDistanceContext FindDistance(PresentationDistanceContext[] contexts, string id)
        {
            if (contexts == null)
            {
                return null;
            }

            for (int index = 0; index < contexts.Length; index++)
            {
                if (contexts[index] != null && string.Equals(contexts[index].id, id, StringComparison.Ordinal))
                {
                    return contexts[index];
                }
            }

            return null;
        }

        private static string BuildCatalogPath()
        {
            if (SixFamilyRuntimeCatalog.TryResolveGameDataDirectory(out string gameDataDirectory))
            {
                return Path.Combine(gameDataDirectory, CatalogFileName);
            }

            return Application.streamingAssetsPath.TrimEnd('/', '\\') +
                   "/GameData/" + CatalogFileName;
        }
    }
}
