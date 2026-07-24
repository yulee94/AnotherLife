using System;
using UnityEngine;

namespace AL.Kingdom.Visuals.Architecture
{
    public enum ArchitectureConstructionState
    {
        SitePrepared = 0,
        BaseStructureEstablished = 1,
        SignatureStructureEstablished = 2,
        UpperStructureEstablished = 3,
        FitoutCompleted = 4,
        Operational = 5
    }

    [Serializable]
    public struct ArchitectureConstructionStageMotion
    {
        [SerializeField] private Vector3 entryOffset;
        [SerializeField] private Vector3 perPartOffset;
        [SerializeField] private Vector3 entryEuler;
        [SerializeField] private Vector3 alternatingEuler;
        [SerializeField] private Vector3 entryScaleMultiplier;

        public Vector3 EntryOffset => entryOffset;
        public Vector3 PerPartOffset => perPartOffset;
        public Vector3 EntryEuler => entryEuler;
        public Vector3 AlternatingEuler => alternatingEuler;
        public Vector3 EntryScaleMultiplier => entryScaleMultiplier;

        public static ArchitectureConstructionStageMotion Create(
            Vector3 offset,
            Vector3 partOffset,
            Vector3 rotation,
            Vector3 alternatingRotation,
            Vector3 scaleMultiplier)
        {
            return new ArchitectureConstructionStageMotion
            {
                entryOffset = offset,
                perPartOffset = partOffset,
                entryEuler = rotation,
                alternatingEuler = alternatingRotation,
                entryScaleMultiplier = scaleMultiplier
            };
        }
    }

    /// <summary>
    /// Data-only realm motion profile consumed by the shared architecture
    /// construction-state controller.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ArchitectureConstructionAnimationProfile",
        menuName = "Another Life/Architecture/Construction Animation Profile")]
    public sealed class ArchitectureConstructionAnimationProfile : ScriptableObject
    {
        public const int PersistentStageCount = 5;
        public const int ConstructionStateCount = 6;

        [Header("Identity")]
        [SerializeField] private string profileId = string.Empty;
        [SerializeField] private string realmId = string.Empty;
        [SerializeField] private string buildingArchetype = string.Empty;

        [Header("Shared lifecycle timing")]
        [SerializeField, Min(0.1f)] private float presentationDuration = 16f;
        [SerializeField, Min(0.01f)] private float stageDuration = 1.55f;
        [SerializeField, Min(0f)] private float operationalActivityStart = 9.1f;

        [Header("Cutaway")]
        [SerializeField, Range(0, PersistentStageCount - 1)]
        private int cutawayStageIndex = 3;
        [SerializeField] private Vector2 previewCutawayWindow =
            new Vector2(13.35f, 14.8f);

        [Header("Realm-specific construction motion")]
        [SerializeField] private ArchitectureConstructionStageMotion[] stageMotions =
            Array.Empty<ArchitectureConstructionStageMotion>();

        public string ProfileId => profileId;
        public string RealmId => realmId;
        public string BuildingArchetype => buildingArchetype;
        public float PresentationDuration => presentationDuration;
        public float StageDuration => stageDuration;
        public float OperationalActivityStart => operationalActivityStart;
        public int CutawayStageIndex => cutawayStageIndex;
        public Vector2 PreviewCutawayWindow => previewCutawayWindow;
        public int StageMotionCount => stageMotions?.Length ?? 0;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(profileId) &&
            !string.IsNullOrWhiteSpace(realmId) &&
            presentationDuration > 0f &&
            stageDuration > 0f &&
            stageMotions != null &&
            stageMotions.Length == PersistentStageCount;

        public void Configure(
            string stableProfileId,
            string stableRealmId,
            string archetype,
            float duration,
            float perStageDuration,
            float activityStart,
            int occlusionStageIndex,
            Vector2 cutawayWindow,
            ArchitectureConstructionStageMotion[] motions)
        {
            profileId = stableProfileId ?? string.Empty;
            realmId = stableRealmId ?? string.Empty;
            buildingArchetype = archetype ?? string.Empty;
            presentationDuration = Mathf.Max(0.1f, duration);
            stageDuration = Mathf.Max(0.01f, perStageDuration);
            operationalActivityStart = Mathf.Clamp(
                activityStart,
                0f,
                presentationDuration);
            cutawayStageIndex = Mathf.Clamp(
                occlusionStageIndex,
                0,
                PersistentStageCount - 1);
            previewCutawayWindow = new Vector2(
                Mathf.Clamp(cutawayWindow.x, 0f, presentationDuration),
                Mathf.Clamp(cutawayWindow.y, 0f, presentationDuration));
            stageMotions = motions == null
                ? Array.Empty<ArchitectureConstructionStageMotion>()
                : (ArchitectureConstructionStageMotion[])motions.Clone();
        }

        public ArchitectureConstructionStageMotion GetStageMotion(int stageIndex)
        {
            if (stageMotions == null ||
                stageIndex < 0 ||
                stageIndex >= stageMotions.Length)
            {
                return ArchitectureConstructionStageMotion.Create(
                    Vector3.zero,
                    Vector3.zero,
                    Vector3.zero,
                    Vector3.zero,
                    Vector3.one);
            }

            return stageMotions[stageIndex];
        }

        public float GetPreviewTime(ArchitectureConstructionState state)
        {
            if (state == ArchitectureConstructionState.Operational)
            {
                return Mathf.Max(0f, operationalActivityStart - 0.15f);
            }

            return Mathf.Min(
                presentationDuration,
                ((int)state + 1) * stageDuration - 0.01f);
        }

        public ArchitectureConstructionState ResolveState(float time)
        {
            int stateIndex = Mathf.Clamp(
                Mathf.FloorToInt(Mathf.Max(0f, time) / stageDuration),
                0,
                (int)ArchitectureConstructionState.Operational);
            return (ArchitectureConstructionState)stateIndex;
        }
    }
}
