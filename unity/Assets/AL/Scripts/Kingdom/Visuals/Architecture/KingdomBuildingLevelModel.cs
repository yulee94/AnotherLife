using System;
using UnityEngine;

namespace AL.Kingdom.Visuals.Architecture
{
    [Serializable]
    public sealed class KingdomBuildingLevelDelta
    {
        [SerializeField, Range(1, 10)] private int minimumLevel = 1;
        [SerializeField] private GameObject[] lodObjects =
            Array.Empty<GameObject>();

        public KingdomBuildingLevelDelta(
            int requiredLevel,
            GameObject[] objectsByLod)
        {
            minimumLevel = Mathf.Clamp(requiredLevel, 1, 10);
            lodObjects = objectsByLod ?? Array.Empty<GameObject>();
        }

        public int MinimumLevel => minimumLevel;
        public GameObject[] LodObjects =>
            lodObjects ?? Array.Empty<GameObject>();
    }

    /// <summary>
    /// Applies confirmed gameplay level to one cumulative production model.
    /// The selected level is runtime presentation state and is never persisted.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KingdomBuildingLevelModel : MonoBehaviour
    {
        [Header("Stable production identity")]
        [SerializeField] private string modelId = string.Empty;
        [SerializeField] private string buildingId = string.Empty;
        [SerializeField, Range(1, 10)] private int maximumLevel = 10;

        [Header("Authoring envelope in meters")]
        [SerializeField] private Vector3 slotEnvelope =
            new Vector3(10f, 6.8f, 8f);
        [SerializeField] private Vector3 maximumArtBounds =
            new Vector3(9.2f, 6.8f, 7f);

        [Header("Cumulative modules")]
        [SerializeField] private KingdomBuildingLevelDelta[] levelDeltas =
            Array.Empty<KingdomBuildingLevelDelta>();
        [SerializeField, Range(1, 10)] private int previewLevel = 10;

        [Header("Runtime presentation")]
        [SerializeField] private LODGroup lodGroup;
        [SerializeField] private BoxCollider selectionCollider;
        [SerializeField] private BoxCollider navigationCollider;

        public string ModelId => modelId;
        public string BuildingId => buildingId;
        public int MaximumLevel => maximumLevel;
        public Vector3 SlotEnvelope => slotEnvelope;
        public Vector3 MaximumArtBounds => maximumArtBounds;
        public LODGroup LodGroup => lodGroup;
        public BoxCollider SelectionCollider => selectionCollider;
        public BoxCollider NavigationCollider => navigationCollider;
        public KingdomBuildingLevelDelta[] LevelDeltas =>
            levelDeltas ?? Array.Empty<KingdomBuildingLevelDelta>();
        public int AppliedLevel { get; private set; }

        public bool IsConfigured
        {
            get
            {
                if (string.IsNullOrWhiteSpace(modelId) ||
                    string.IsNullOrWhiteSpace(buildingId) ||
                    maximumLevel != 10 ||
                    lodGroup == null ||
                    selectionCollider == null ||
                    navigationCollider == null ||
                    levelDeltas == null ||
                    levelDeltas.Length != maximumLevel)
                {
                    return false;
                }

                for (int index = 0; index < levelDeltas.Length; index++)
                {
                    KingdomBuildingLevelDelta delta = levelDeltas[index];
                    if (delta == null ||
                        delta.MinimumLevel != index + 1 ||
                        delta.LodObjects.Length != 4 ||
                        delta.LodObjects[0] == null ||
                        delta.LodObjects[1] == null ||
                        delta.LodObjects[2] == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public void Configure(
            string stableModelId,
            string stableBuildingId,
            Vector3 stableSlotEnvelope,
            Vector3 stableMaximumArtBounds,
            KingdomBuildingLevelDelta[] cumulativeDeltas,
            LODGroup modelLodGroup,
            BoxCollider modelSelectionCollider,
            BoxCollider modelNavigationCollider)
        {
            modelId = stableModelId ?? string.Empty;
            buildingId = stableBuildingId ?? string.Empty;
            maximumLevel = 10;
            slotEnvelope = stableSlotEnvelope;
            maximumArtBounds = stableMaximumArtBounds;
            levelDeltas = cumulativeDeltas == null
                ? Array.Empty<KingdomBuildingLevelDelta>()
                : (KingdomBuildingLevelDelta[])cumulativeDeltas.Clone();
            lodGroup = modelLodGroup;
            selectionCollider = modelSelectionCollider;
            navigationCollider = modelNavigationCollider;
            previewLevel = maximumLevel;
            ApplyConfirmedLevel(previewLevel);
        }

        public bool ApplyConfirmedLevel(int confirmedLevel)
        {
            if (!IsConfigured ||
                confirmedLevel < 1 ||
                confirmedLevel > maximumLevel)
            {
                return false;
            }

            foreach (KingdomBuildingLevelDelta delta in levelDeltas)
            {
                if (delta == null)
                {
                    continue;
                }

                bool active = delta.MinimumLevel <= confirmedLevel;
                foreach (GameObject lodObject in delta.LodObjects)
                {
                    if (lodObject != null &&
                        lodObject.activeSelf != active)
                    {
                        lodObject.SetActive(active);
                    }
                }
            }

            AppliedLevel = confirmedLevel;
            return true;
        }

        public bool IsLevelDeltaActive(int minimumLevel)
        {
            foreach (KingdomBuildingLevelDelta delta in LevelDeltas)
            {
                if (delta == null || delta.MinimumLevel != minimumLevel)
                {
                    continue;
                }

                foreach (GameObject lodObject in delta.LodObjects)
                {
                    if (lodObject != null && !lodObject.activeSelf)
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        private void Awake()
        {
            ApplyConfirmedLevel(previewLevel);
        }
    }
}
