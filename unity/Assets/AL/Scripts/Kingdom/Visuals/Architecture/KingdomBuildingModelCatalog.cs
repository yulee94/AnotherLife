using System;
using System.Collections.Generic;
using AL.Core;
using UnityEngine;

namespace AL.Kingdom.Visuals.Architecture
{
    [Serializable]
    public sealed class KingdomBuildingModelEntry
    {
        [SerializeField] private string modelId = string.Empty;
        [SerializeField] private RealmId realmId;
        [SerializeField] private string buildingId = string.Empty;
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0.01f)] private float strategicBoardScale = 0.12f;
        [SerializeField, Range(1, 10)] private int minimumLevel = 1;
        [SerializeField, Range(1, 10)] private int maximumLevel = 10;

        public KingdomBuildingModelEntry(
            string stableModelId,
            RealmId stableRealmId,
            string stableBuildingId,
            GameObject productionPrefab,
            float boardScale,
            int supportedMinimumLevel,
            int supportedMaximumLevel)
        {
            modelId = stableModelId ?? string.Empty;
            realmId = stableRealmId;
            buildingId = stableBuildingId ?? string.Empty;
            prefab = productionPrefab;
            strategicBoardScale = Mathf.Max(0.01f, boardScale);
            minimumLevel = Mathf.Clamp(supportedMinimumLevel, 1, 10);
            maximumLevel = Mathf.Clamp(
                supportedMaximumLevel,
                minimumLevel,
                10);
        }

        public string ModelId => modelId;
        public RealmId RealmId => realmId;
        public string BuildingId => buildingId;
        public GameObject Prefab => prefab;
        public float StrategicBoardScale => strategicBoardScale;
        public int MinimumLevel => minimumLevel;
        public int MaximumLevel => maximumLevel;

        public bool IsConfigured
        {
            get
            {
                KingdomBuildingLevelModel levelModel =
                    prefab == null
                        ? null
                        : prefab.GetComponent<
                            KingdomBuildingLevelModel>();
                return
                    !string.IsNullOrWhiteSpace(modelId) &&
                    realmId != RealmId.None &&
                    !string.IsNullOrWhiteSpace(buildingId) &&
                    levelModel != null &&
                    levelModel.IsConfigured &&
                    string.Equals(
                        modelId,
                        levelModel.ModelId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        buildingId,
                        levelModel.BuildingId,
                        StringComparison.Ordinal) &&
                    strategicBoardScale > 0f &&
                    minimumLevel >= 1 &&
                    maximumLevel <= levelModel.MaximumLevel &&
                    maximumLevel >= minimumLevel;
            }
        }

        public bool SupportsLevel(int confirmedLevel)
        {
            return confirmedLevel >= minimumLevel &&
                confirmedLevel <= maximumLevel;
        }
    }

    /// <summary>
    /// Packaged production-model manifest. It owns asset identity only and
    /// never owns building progress, placement, saves, or economy state.
    /// </summary>
    [CreateAssetMenu(
        fileName = DefaultResourceName,
        menuName = "Another Life/Kingdom/Building Model Catalog")]
    public sealed class KingdomBuildingModelCatalog : ScriptableObject
    {
        public const string DefaultResourceName =
            "KingdomBuildingModelCatalog";
        public const string DuplicateBindingDiagnostic =
            "KINGDOM_MODEL_BINDING_DUPLICATE";
        public const string InvalidBindingDiagnostic =
            "KINGDOM_MODEL_BINDING_INVALID";

        [SerializeField] private KingdomBuildingModelEntry[] entries =
            Array.Empty<KingdomBuildingModelEntry>();

        public KingdomBuildingModelEntry[] Entries =>
            entries ?? Array.Empty<KingdomBuildingModelEntry>();

        public void Configure(KingdomBuildingModelEntry[] modelEntries)
        {
            entries = modelEntries == null
                ? Array.Empty<KingdomBuildingModelEntry>()
                : (KingdomBuildingModelEntry[])modelEntries.Clone();
        }

        public bool TryGetEntry(
            RealmId realmId,
            string buildingId,
            out KingdomBuildingModelEntry entry)
        {
            entry = null;
            if (realmId == RealmId.None ||
                string.IsNullOrWhiteSpace(buildingId))
            {
                return false;
            }

            foreach (KingdomBuildingModelEntry candidate in Entries)
            {
                if (candidate != null &&
                    candidate.RealmId == realmId &&
                    string.Equals(
                        candidate.BuildingId,
                        buildingId,
                        StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool Validate(out string diagnosticCode)
        {
            diagnosticCode = string.Empty;
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (KingdomBuildingModelEntry entry in Entries)
            {
                if (entry == null || !entry.IsConfigured)
                {
                    diagnosticCode = InvalidBindingDiagnostic;
                    return false;
                }

                string identity =
                    $"{entry.RealmId}:{entry.BuildingId}";
                if (!identities.Add(identity))
                {
                    diagnosticCode = DuplicateBindingDiagnostic;
                    return false;
                }
            }

            return true;
        }

        public static KingdomBuildingModelCatalog LoadDefault()
        {
            return Resources.Load<KingdomBuildingModelCatalog>(
                DefaultResourceName);
        }
    }
}
