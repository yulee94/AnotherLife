using System;
using AL.Core;
using UnityEngine;

namespace AL.World
{
    [Serializable]
    public sealed class FirstSessionRealmVisualAsset
    {
        [SerializeField] private RealmId realm;
        [SerializeField] private GameObject landmarkPrefab;
        [SerializeField] private GameObject premiumLandmarkPrefab;
        [SerializeField] private Texture2D premiumBaseColor;
        [SerializeField] private Texture2D premiumNormal;
        [SerializeField] private Texture2D premiumMetallic;
        [SerializeField] private Texture2D premiumRoughness;
        [SerializeField] private Texture2D premiumEmission;
        [SerializeField] private Texture2D panoramicSky;

        public RealmId Realm => realm;
        public GameObject LandmarkPrefab => landmarkPrefab;
        public GameObject PremiumLandmarkPrefab => premiumLandmarkPrefab;
        public Texture2D PremiumBaseColor => premiumBaseColor;
        public Texture2D PremiumNormal => premiumNormal;
        public Texture2D PremiumMetallic => premiumMetallic;
        public Texture2D PremiumRoughness => premiumRoughness;
        public Texture2D PremiumEmission => premiumEmission;
        public Texture2D PanoramicSky => panoramicSky;
    }

    [Serializable]
    public sealed class FirstSessionChampionBaseVisualAsset
    {
        [SerializeField] private string bodyBaseId;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Texture2D baseColor;
        [SerializeField] private Texture2D normal;
        [SerializeField] private Texture2D metallic;
        [SerializeField] private Texture2D roughness;
        [SerializeField] private Texture2D emission;
        [SerializeField] private AnimationClip locomotionClip;

        public string BodyBaseId => bodyBaseId;
        public GameObject Prefab => prefab;
        public Texture2D BaseColor => baseColor;
        public Texture2D Normal => normal;
        public Texture2D Metallic => metallic;
        public Texture2D Roughness => roughness;
        public Texture2D Emission => emission;
        public AnimationClip LocomotionClip => locomotionClip;

        public bool IsComplete()
        {
            return !string.IsNullOrWhiteSpace(bodyBaseId) && prefab != null &&
                   baseColor != null && normal != null && metallic != null &&
                   roughness != null && emission != null && locomotionClip != null &&
                   locomotionClip.length > 0f;
        }
    }

    /// <summary>
    /// Typed runtime bridge to the admitted authored first-session art packet.
    /// The asset is generated in Editor so player builds never depend on AssetDatabase.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FirstSessionAuthoredAssetCatalog",
        menuName = "Another Life/First Session/Authored Asset Catalog")]
    public sealed class FirstSessionAuthoredAssetCatalog : ScriptableObject
    {
        public const string ResourcesPath = "FirstSessionAuthoredAssetCatalog";

        [Header("Neutral covenant hall")]
        [SerializeField] private GameObject covenantHallPrefab;
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material trimMaterial;
        [SerializeField] private Texture2D premiumFloorBaseColor;
        [SerializeField] private Texture2D premiumFloorNormal;
        [SerializeField] private Texture2D premiumFloorMetallic;
        [SerializeField] private Texture2D premiumFloorRoughness;

        [Header("Champion")]
        [SerializeField] private FirstSessionChampionBaseVisualAsset[] championBases =
            Array.Empty<FirstSessionChampionBaseVisualAsset>();
        [SerializeField] private GameObject championBodyPrefab;
        [SerializeField] private GameObject championArmorPrefab;
        [SerializeField] private GameObject championWeaponPrefab;

        [Header("Guardian")]
        [SerializeField] private GameObject guardianPrefab;
        [SerializeField] private Texture2D guardianBaseColor;
        [SerializeField] private Texture2D guardianNormal;
        [SerializeField] private Texture2D guardianMetallic;
        [SerializeField] private Texture2D guardianRoughness;
        [SerializeField] private Texture2D guardianEmission;
        [SerializeField] private AnimationClip guardianLocomotionClip;

        [Header("Realm structural identity")]
        [SerializeField] private FirstSessionRealmVisualAsset[] realmVisuals =
            Array.Empty<FirstSessionRealmVisualAsset>();

        public GameObject CovenantHallPrefab => covenantHallPrefab;
        public Material FloorMaterial => floorMaterial;
        public Material WallMaterial => wallMaterial;
        public Material TrimMaterial => trimMaterial;
        public Texture2D PremiumFloorBaseColor => premiumFloorBaseColor;
        public Texture2D PremiumFloorNormal => premiumFloorNormal;
        public Texture2D PremiumFloorMetallic => premiumFloorMetallic;
        public Texture2D PremiumFloorRoughness => premiumFloorRoughness;
        public GameObject ChampionBodyPrefab => championBodyPrefab;
        public GameObject ChampionArmorPrefab => championArmorPrefab;
        public GameObject ChampionWeaponPrefab => championWeaponPrefab;
        public GameObject GuardianPrefab => guardianPrefab;
        public Texture2D GuardianBaseColor => guardianBaseColor;
        public Texture2D GuardianNormal => guardianNormal;
        public Texture2D GuardianMetallic => guardianMetallic;
        public Texture2D GuardianRoughness => guardianRoughness;
        public Texture2D GuardianEmission => guardianEmission;
        public AnimationClip GuardianLocomotionClip => guardianLocomotionClip;

        public bool TryResolveChampionBase(
            string bodyBaseId,
            out GameObject prefab,
            out AnimationClip locomotion)
        {
            if (TryResolveChampionBaseVisual(bodyBaseId, out FirstSessionChampionBaseVisualAsset visual))
            {
                prefab = visual.Prefab;
                locomotion = visual.LocomotionClip;
                return true;
            }

            prefab = null;
            locomotion = null;
            return false;
        }

        public bool TryResolveChampionBaseVisual(
            string bodyBaseId,
            out FirstSessionChampionBaseVisualAsset visual)
        {
            string resolvedBodyBaseId = string.IsNullOrEmpty(bodyBaseId)
                ? "male"
                : bodyBaseId;
            for (int index = 0; index < championBases.Length; index++)
            {
                FirstSessionChampionBaseVisualAsset candidate = championBases[index];
                if (candidate != null && candidate.IsComplete() &&
                    string.Equals(
                        candidate.BodyBaseId,
                        resolvedBodyBaseId,
                        StringComparison.Ordinal))
                {
                    visual = candidate;
                    return true;
                }
            }

            visual = null;
            return false;
        }

        public bool TryResolveRealmVisual(
            RealmId realm,
            out FirstSessionRealmVisualAsset visual)
        {
            for (int index = 0; index < realmVisuals.Length; index++)
            {
                FirstSessionRealmVisualAsset candidate = realmVisuals[index];
                if (candidate != null && candidate.Realm == realm &&
                    candidate.LandmarkPrefab != null &&
                    candidate.PremiumLandmarkPrefab != null &&
                    candidate.PremiumBaseColor != null &&
                    candidate.PremiumNormal != null &&
                    candidate.PremiumMetallic != null &&
                    candidate.PremiumRoughness != null &&
                    candidate.PremiumEmission != null &&
                    candidate.PanoramicSky != null)
                {
                    visual = candidate;
                    return true;
                }
            }

            visual = null;
            return false;
        }

        public bool HasRequiredAssets()
        {
            if (covenantHallPrefab == null || floorMaterial == null || wallMaterial == null ||
                trimMaterial == null || premiumFloorBaseColor == null ||
                premiumFloorNormal == null || premiumFloorMetallic == null ||
                premiumFloorRoughness == null ||
                championBodyPrefab == null || championArmorPrefab == null ||
                championWeaponPrefab == null || guardianPrefab == null ||
                guardianBaseColor == null || guardianNormal == null ||
                guardianMetallic == null || guardianRoughness == null || guardianEmission == null)
            {
                return false;
            }

            if (guardianLocomotionClip == null || guardianLocomotionClip.length <= 0f)
            {
                return false;
            }

            if (!TryResolveChampionBaseVisual("male", out FirstSessionChampionBaseVisualAsset male) ||
                !TryResolveChampionBaseVisual("female", out FirstSessionChampionBaseVisualAsset female) ||
                male.Prefab == female.Prefab)
            {
                return false;
            }

            return TryResolveRealmVisual(RealmId.Stonehold, out _) &&
                   TryResolveRealmVisual(RealmId.Eldergrove, out _) &&
                   TryResolveRealmVisual(RealmId.Crownlands, out _) &&
                   TryResolveRealmVisual(RealmId.Umbral, out _);
        }
    }
}
