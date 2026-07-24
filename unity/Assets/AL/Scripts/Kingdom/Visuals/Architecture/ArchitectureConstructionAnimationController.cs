using System;
using UnityEngine;

namespace AL.Kingdom.Visuals.Architecture
{
    public interface IArchitectureConstructionActivity
    {
        void EvaluateActivity(float presentationTime, bool reducedMotion);
    }

    [Serializable]
    public sealed class ArchitectureConstructionStageBinding
    {
        [SerializeField] private Transform[] parts = Array.Empty<Transform>();

        public Transform[] Parts => parts ?? Array.Empty<Transform>();

        public ArchitectureConstructionStageBinding(Transform[] stageParts)
        {
            parts = stageParts ?? Array.Empty<Transform>();
        }
    }

    /// <summary>
    /// Shared deterministic lifecycle for realm architecture. Realm identity is
    /// provided by a data profile and optional bounded activity components.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArchitectureConstructionAnimationController : MonoBehaviour
    {
        [Header("Reusable construction profile")]
        [SerializeField] private ArchitectureConstructionAnimationProfile profile;
        [SerializeField] private ArchitectureConstructionStageBinding[] stages =
            Array.Empty<ArchitectureConstructionStageBinding>();

        [Header("Optional realm activity components")]
        [SerializeField] private MonoBehaviour[] activityBehaviours =
            Array.Empty<MonoBehaviour>();

        [Header("Optional inspection cutaway groups")]
        [SerializeField] private Transform[] supplementalCutawayGroups =
            Array.Empty<Transform>();

        [Header("Playback")]
        [SerializeField] private bool autoPlay;
        [SerializeField] private bool loop;
        [SerializeField] private bool reducedMotion;

        private PartPose[][] settledPoses = Array.Empty<PartPose[]>();
        private IArchitectureConstructionActivity[] activities =
            Array.Empty<IArchitectureConstructionActivity>();
        private float elapsed;
        private bool cacheReady;
        private bool forceCutaway;

        public int StageCount =>
            ArchitectureConstructionAnimationProfile.ConstructionStateCount;
        public int PersistentStageCount =>
            ArchitectureConstructionAnimationProfile.PersistentStageCount;
        public bool SupportsReducedMotion => true;
        public bool IsAnimating => enabled;
        public bool ReducedMotion => reducedMotion;
        public float PresentationDuration =>
            profile == null ? 0f : profile.PresentationDuration;
        public string ProfileId => profile == null ? string.Empty : profile.ProfileId;
        public string RealmId => profile == null ? string.Empty : profile.RealmId;
        public ArchitectureConstructionState CurrentState { get; private set; }

        private readonly struct PartPose
        {
            public PartPose(
                Transform part,
                ArchitectureConstructionStageMotion motion,
                int partIndex)
            {
                LocalPosition = part.localPosition;
                LocalRotation = part.localRotation;
                LocalScale = part.localScale;
                Vector3 alternatingRotation =
                    partIndex % 2 == 0
                        ? motion.AlternatingEuler
                        : -motion.AlternatingEuler;
                EntryPosition =
                    LocalPosition +
                    motion.EntryOffset +
                    motion.PerPartOffset * partIndex;
                EntryRotation =
                    LocalRotation *
                    Quaternion.Euler(
                        motion.EntryEuler + alternatingRotation);
                EntryScale = Vector3.Scale(
                    LocalScale,
                    motion.EntryScaleMultiplier);
            }

            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public Vector3 LocalScale { get; }
            public Vector3 EntryPosition { get; }
            public Quaternion EntryRotation { get; }
            public Vector3 EntryScale { get; }
        }

        public void Configure(
            ArchitectureConstructionAnimationProfile animationProfile,
            Transform[][] persistentStageParts,
            MonoBehaviour[] realmActivityBehaviours)
        {
            profile = animationProfile;
            stages = CreateBindings(persistentStageParts);
            activityBehaviours = realmActivityBehaviours ?? Array.Empty<MonoBehaviour>();
            cacheReady = false;
            EnsureCache();
        }

        public void ConfigurePlayback(
            bool shouldAutoPlay,
            bool shouldLoop,
            bool useReducedMotion)
        {
            autoPlay = shouldAutoPlay;
            loop = shouldLoop;
            reducedMotion = useReducedMotion;
        }

        public void ConfigureCutawayGroups(Transform[] groups)
        {
            supplementalCutawayGroups =
                groups ?? Array.Empty<Transform>();
        }

        public void Play()
        {
            if (!EnsureConfigured())
            {
                return;
            }

            elapsed = 0f;
            enabled = true;
            SetPreviewTime(0f);
        }

        public void PlayOperationalActivity()
        {
            if (!EnsureConfigured())
            {
                return;
            }

            elapsed = profile.OperationalActivityStart;
            enabled = true;
            SetPreviewTime(elapsed);
        }

        public void SetReducedMotion(bool value)
        {
            reducedMotion = value;
            SetPreviewTime(elapsed);
        }

        public void SetCutaway(bool value)
        {
            forceCutaway = value;
            SetPreviewTime(elapsed);
        }

        public void SetConstructionState(ArchitectureConstructionState state)
        {
            if (!EnsureConfigured())
            {
                return;
            }

            elapsed = profile.GetPreviewTime(state);
            SetPreviewTime(elapsed);
            enabled = false;
        }

        /// <summary>
        /// Deterministically evaluates construction and realm activity without
        /// depending on frame history.
        /// </summary>
        public void SetPreviewTime(float seconds)
        {
            if (!EnsureConfigured())
            {
                return;
            }

            elapsed = Mathf.Clamp(seconds, 0f, profile.PresentationDuration);

            for (int stageIndex = 0; stageIndex < stages.Length; stageIndex++)
            {
                float stageStart = stageIndex * profile.StageDuration;
                float progress = Mathf.Clamp01(
                    (elapsed - stageStart) / profile.StageDuration);
                bool visible = elapsed >= stageStart;

                if (reducedMotion)
                {
                    progress = progress >= 0.5f ? 1f : 0f;
                    visible = progress >= 1f;
                }

                ApplyStage(stageIndex, visible, progress);
            }

            ApplySupplementalCutawayGroups();
            CurrentState = profile.ResolveState(elapsed);
            foreach (IArchitectureConstructionActivity activity in activities)
            {
                activity.EvaluateActivity(elapsed, reducedMotion);
            }
        }

        private void Awake()
        {
            EnsureCache();
        }

        private void Start()
        {
            if (!EnsureConfigured())
            {
                enabled = false;
                return;
            }

            if (autoPlay)
            {
                Play();
                return;
            }

            SetPreviewTime(profile.PresentationDuration);
            enabled = false;
        }

        private void Update()
        {
            if (!EnsureConfigured())
            {
                enabled = false;
                return;
            }

            elapsed += Time.deltaTime;

            if (elapsed < profile.PresentationDuration)
            {
                SetPreviewTime(elapsed);
                return;
            }

            if (loop)
            {
                elapsed = 0f;
                SetPreviewTime(0f);
                return;
            }

            SetPreviewTime(profile.PresentationDuration);
            enabled = false;
        }

        private bool EnsureConfigured()
        {
            EnsureCache();
            return profile != null &&
                profile.IsConfigured &&
                stages.Length ==
                    ArchitectureConstructionAnimationProfile.PersistentStageCount;
        }

        private void EnsureCache()
        {
            if (cacheReady)
            {
                return;
            }

            stages ??= Array.Empty<ArchitectureConstructionStageBinding>();
            settledPoses = new PartPose[stages.Length][];
            for (int stageIndex = 0; stageIndex < stages.Length; stageIndex++)
            {
                Transform[] parts = stages[stageIndex]?.Parts ??
                    Array.Empty<Transform>();
                settledPoses[stageIndex] = new PartPose[parts.Length];
                ArchitectureConstructionStageMotion motion =
                    profile == null
                        ? ArchitectureConstructionStageMotion.Create(
                            Vector3.zero,
                            Vector3.zero,
                            Vector3.zero,
                            Vector3.zero,
                            Vector3.one)
                        : profile.GetStageMotion(stageIndex);

                for (int partIndex = 0; partIndex < parts.Length; partIndex++)
                {
                    if (parts[partIndex] != null)
                    {
                        settledPoses[stageIndex][partIndex] =
                            new PartPose(
                                parts[partIndex],
                                motion,
                                partIndex);
                    }
                }
            }

            activities = ResolveActivities(activityBehaviours);
            cacheReady = true;
        }

        private void ApplyStage(int stageIndex, bool visible, float progress)
        {
            Transform[] parts = stages[stageIndex].Parts;
            bool cutaway =
                stageIndex == profile.CutawayStageIndex &&
                (forceCutaway ||
                    elapsed >= profile.PreviewCutawayWindow.x &&
                    elapsed <= profile.PreviewCutawayWindow.y);
            bool shouldBeVisible = visible && !cutaway;

            for (int partIndex = 0; partIndex < parts.Length; partIndex++)
            {
                Transform part = parts[partIndex];
                if (part == null)
                {
                    continue;
                }

                if (part.gameObject.activeSelf != shouldBeVisible)
                {
                    part.gameObject.SetActive(shouldBeVisible);
                }

                if (!shouldBeVisible)
                {
                    continue;
                }

                PartPose pose = settledPoses[stageIndex][partIndex];
                float eased = EaseOutCubic(progress);

                part.localPosition = Vector3.LerpUnclamped(
                    pose.EntryPosition,
                    pose.LocalPosition,
                    eased);
                part.localRotation = Quaternion.SlerpUnclamped(
                    pose.EntryRotation,
                    pose.LocalRotation,
                    eased);
                part.localScale = Vector3.LerpUnclamped(
                    pose.EntryScale,
                    pose.LocalScale,
                    eased);
            }
        }

        private void ApplySupplementalCutawayGroups()
        {
            supplementalCutawayGroups ??= Array.Empty<Transform>();
            bool cutaway =
                forceCutaway ||
                elapsed >= profile.PreviewCutawayWindow.x &&
                elapsed <= profile.PreviewCutawayWindow.y;

            foreach (Transform group in supplementalCutawayGroups)
            {
                if (group != null &&
                    group.gameObject.activeSelf == cutaway)
                {
                    group.gameObject.SetActive(!cutaway);
                }
            }
        }

        private static ArchitectureConstructionStageBinding[] CreateBindings(
            Transform[][] stageParts)
        {
            if (stageParts == null)
            {
                return Array.Empty<ArchitectureConstructionStageBinding>();
            }

            var bindings =
                new ArchitectureConstructionStageBinding[stageParts.Length];
            for (int index = 0; index < stageParts.Length; index++)
            {
                bindings[index] =
                    new ArchitectureConstructionStageBinding(stageParts[index]);
            }

            return bindings;
        }

        private static IArchitectureConstructionActivity[] ResolveActivities(
            MonoBehaviour[] behaviours)
        {
            if (behaviours == null || behaviours.Length == 0)
            {
                return Array.Empty<IArchitectureConstructionActivity>();
            }

            var resolved =
                new IArchitectureConstructionActivity[behaviours.Length];
            int count = 0;
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IArchitectureConstructionActivity activity)
                {
                    resolved[count++] = activity;
                }
            }

            if (count == resolved.Length)
            {
                return resolved;
            }

            var trimmed = new IArchitectureConstructionActivity[count];
            Array.Copy(resolved, trimmed, count);
            return trimmed;
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }
    }
}
