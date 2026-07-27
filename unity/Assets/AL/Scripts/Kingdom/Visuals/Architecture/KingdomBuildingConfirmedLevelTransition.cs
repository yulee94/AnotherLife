using System;
using System.Collections.Generic;
using AL.Core;
using UnityEngine;

namespace AL.Kingdom.Visuals.Architecture
{
    /// <summary>
    /// Session-only detector for newly confirmed adjacent building levels.
    /// It never reads or writes saves and intentionally does not replay on the
    /// first observation after loading or streaming.
    /// </summary>
    public sealed class KingdomBuildingConfirmedLevelTracker
    {
        private readonly Dictionary<string, int> confirmedLevels =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public bool Observe(
            RealmId realmId,
            string slotId,
            int confirmedLevel,
            bool isUpgrading,
            bool isValid)
        {
            if (realmId == RealmId.None ||
                string.IsNullOrWhiteSpace(slotId))
            {
                return false;
            }

            string key = $"{(int)realmId}:{slotId}";
            if (!isValid)
            {
                confirmedLevels.Remove(key);
                return false;
            }

            int normalizedLevel = Mathf.Clamp(confirmedLevel, 0, 10);
            bool hasPrevious =
                confirmedLevels.TryGetValue(key, out int previousLevel);
            confirmedLevels[key] = normalizedLevel;

            return hasPrevious &&
                !isUpgrading &&
                normalizedLevel == previousLevel + 1;
        }
    }

    /// <summary>
    /// Applies one realm-profile rigid settle to only the newly confirmed
    /// production-model delta. The component is transient, sleeps after the
    /// short transition, and owns no gameplay or persistence state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KingdomBuildingConfirmedLevelTransition : MonoBehaviour
    {
        private const float MinimumDuration = 0.35f;
        private const float MaximumDuration = 1.25f;

        private TransitionPose[] poses = Array.Empty<TransitionPose>();
        private float elapsed;
        private float duration;
        private bool reducedMotion;

        public int ConfirmedLevel { get; private set; }
        public int AnimatedObjectCount => poses.Length;
        public bool ReducedMotion => reducedMotion;
        public bool IsAnimating => enabled && poses.Length > 0;

        private readonly struct TransitionPose
        {
            public TransitionPose(
                Transform part,
                ArchitectureConstructionStageMotion motion,
                int partIndex)
            {
                Part = part;
                SettledPosition = part.localPosition;
                SettledRotation = part.localRotation;
                SettledScale = part.localScale;
                Vector3 alternatingRotation =
                    partIndex % 2 == 0
                        ? motion.AlternatingEuler
                        : -motion.AlternatingEuler;
                EntryPosition =
                    SettledPosition +
                    motion.EntryOffset +
                    motion.PerPartOffset * partIndex;
                EntryRotation =
                    SettledRotation *
                    Quaternion.Euler(
                        motion.EntryEuler + alternatingRotation);
                EntryScale = Vector3.Scale(
                    SettledScale,
                    motion.EntryScaleMultiplier);
            }

            public Transform Part { get; }
            public Vector3 SettledPosition { get; }
            public Quaternion SettledRotation { get; }
            public Vector3 SettledScale { get; }
            public Vector3 EntryPosition { get; }
            public Quaternion EntryRotation { get; }
            public Vector3 EntryScale { get; }
        }

        public bool Configure(
            KingdomBuildingLevelModel model,
            ArchitectureConstructionAnimationProfile profile,
            int confirmedLevel,
            bool useReducedMotion)
        {
            RestoreSettledPoses();
            poses = Array.Empty<TransitionPose>();
            enabled = false;
            ConfirmedLevel = 0;

            if (model == null ||
                profile == null ||
                !profile.IsConfigured ||
                !model.IsConfigured ||
                confirmedLevel < 1 ||
                confirmedLevel > model.MaximumLevel)
            {
                return false;
            }

            KingdomBuildingLevelDelta delta = Array.Find(
                model.LevelDeltas,
                candidate =>
                    candidate != null &&
                    candidate.MinimumLevel == confirmedLevel);
            if (delta == null)
            {
                return false;
            }

            var targetObjects = new List<GameObject>(4);
            foreach (GameObject lodObject in delta.LodObjects)
            {
                if (lodObject != null && lodObject.activeSelf)
                {
                    targetObjects.Add(lodObject);
                }
            }

            if (targetObjects.Count == 0)
            {
                return false;
            }

            int stageIndex = Mathf.Clamp(
                (confirmedLevel - 1) / 2,
                0,
                ArchitectureConstructionAnimationProfile
                    .PersistentStageCount - 1);
            ArchitectureConstructionStageMotion motion =
                profile.GetStageMotion(stageIndex);
            poses = new TransitionPose[targetObjects.Count];
            for (int index = 0; index < targetObjects.Count; index++)
            {
                poses[index] = new TransitionPose(
                    targetObjects[index].transform,
                    motion,
                    index);
            }

            ConfirmedLevel = confirmedLevel;
            reducedMotion = useReducedMotion;
            elapsed = 0f;
            duration = Mathf.Clamp(
                profile.StageDuration,
                MinimumDuration,
                MaximumDuration);

            if (reducedMotion)
            {
                RestoreSettledPoses();
                return true;
            }

            ApplyProgress(0f);
            enabled = true;
            return true;
        }

        public void SetReducedMotion(bool value)
        {
            reducedMotion = value;
            if (reducedMotion)
            {
                RestoreSettledPoses();
                enabled = false;
            }
        }

        public void Evaluate(float normalizedProgress)
        {
            if (poses.Length == 0)
            {
                return;
            }

            if (reducedMotion)
            {
                RestoreSettledPoses();
                enabled = false;
                return;
            }

            ApplyProgress(Mathf.Clamp01(normalizedProgress));
            if (normalizedProgress >= 1f)
            {
                enabled = false;
            }
        }

        private void Update()
        {
            if (reducedMotion || poses.Length == 0)
            {
                RestoreSettledPoses();
                enabled = false;
                return;
            }

            elapsed += Time.deltaTime;
            Evaluate(duration <= 0f ? 1f : elapsed / duration);
        }

        private void OnDisable()
        {
            RestoreSettledPoses();
        }

        private void ApplyProgress(float progress)
        {
            float eased = EaseOutCubic(progress);
            foreach (TransitionPose pose in poses)
            {
                if (pose.Part == null)
                {
                    continue;
                }

                pose.Part.localPosition = Vector3.LerpUnclamped(
                    pose.EntryPosition,
                    pose.SettledPosition,
                    eased);
                pose.Part.localRotation = Quaternion.SlerpUnclamped(
                    pose.EntryRotation,
                    pose.SettledRotation,
                    eased);
                pose.Part.localScale = Vector3.LerpUnclamped(
                    pose.EntryScale,
                    pose.SettledScale,
                    eased);
            }
        }

        private void RestoreSettledPoses()
        {
            foreach (TransitionPose pose in poses)
            {
                if (pose.Part == null)
                {
                    continue;
                }

                pose.Part.localPosition = pose.SettledPosition;
                pose.Part.localRotation = pose.SettledRotation;
                pose.Part.localScale = pose.SettledScale;
            }
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }
    }
}
