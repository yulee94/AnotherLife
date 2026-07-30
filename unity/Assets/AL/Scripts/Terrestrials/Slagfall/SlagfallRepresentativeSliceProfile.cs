using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AL.Terrestrials.Slagfall
{
    public static class SlagfallSourceAuthority
    {
        public const string SourceVersion = "tdf-eco-slagfall-2026-07-30-v002";
        public const string HabitatSourceId =
            "tdf_asset_habitat_stonehold_slagfall_quarry_master_v002";
        public const string HabitatSourceSha256 =
            "600a76d983f0cb63abf1169b7a9cdf34477b60ebf2e10ca9f74883efd899d195";
        public const string SlagwhistleIdentitySourceId =
            "tdf_asset_fauna_stonehold_slagwhistle_burrower_identity_v002";
        public const string SlagwhistleIdentitySha256 =
            "1a08581ef2a49d56f3e3b5a9925a88ee7eebcb6df2895de61691f74b820eaa05";
        public const string SlagwhistleMotionSourceId =
            "tdf_asset_fauna_stonehold_slagwhistle_burrower_motion_contact_v002";
        public const string SlagwhistleMotionSha256 =
            "1099937075dba7012545afb7636e100c592c561c30c2fe68ce7a434ca4ff2d92";

        public static readonly string[] HabitatFamilyIds =
        {
            "slagfall.irregular_fracture_raft",
            "slagfall.broken_fracture_raft",
            "slagfall.undercut_extraction_ledge",
            "slagfall.talus_apron",
            "slagfall.collapsed_gallery_mouth",
            "slagfall.diagonal_fault_slab",
            "slagfall.braided_runoff_pool",
            "slagfall.iron_soil_wedge"
        };

        public static readonly string[] ProtectedSlagwhistleFeatures =
        {
            "WedgeSkull",
            "SlitNostrils",
            "VentFoldLeft",
            "VentFoldRight",
            "ShovelPalmLeft",
            "ShovelPalmRight",
            "StabilizerLeftA",
            "StabilizerLeftB",
            "StabilizerRightA",
            "StabilizerRightB",
            "FlattenedBraceTail"
        };
    }

    [Serializable]
    public sealed class SlagfallHabitatFamilyEntry
    {
        [SerializeField] private string _familyId;
        [SerializeField] private GameObject[] _variants = Array.Empty<GameObject>();

        public string FamilyId => _familyId;
        public IReadOnlyList<GameObject> Variants => _variants;

        public void Configure(string familyId, GameObject[] variants)
        {
            _familyId = familyId;
            _variants = variants ?? Array.Empty<GameObject>();
        }

        public bool Validate(out string diagnostic)
        {
            if (string.IsNullOrWhiteSpace(_familyId))
            {
                diagnostic = "missing_habitat_family_id";
                return false;
            }

            if (_variants == null || _variants.Length == 0)
            {
                diagnostic = $"missing_habitat_variant:{_familyId}";
                return false;
            }

            if (_variants.Any(item => item == null))
            {
                diagnostic = $"null_habitat_variant:{_familyId}";
                return false;
            }

            diagnostic = "ok";
            return true;
        }
    }

    [CreateAssetMenu(
        fileName = "SlagfallRepresentativeSliceProfile",
        menuName = "Another Life/Terrestrials/Slagfall Representative Slice")]
    public sealed class SlagfallRepresentativeSliceProfile : ScriptableObject
    {
        [Header("Approved source")]
        [SerializeField] private string _sourceVersion;
        [SerializeField] private string _habitatSourceId;
        [SerializeField] private string _habitatSourceSha256;
        [SerializeField] private string _slagwhistleIdentitySourceId;
        [SerializeField] private string _slagwhistleIdentitySha256;
        [SerializeField] private string _slagwhistleMotionSourceId;
        [SerializeField] private string _slagwhistleMotionSha256;

        [Header("Representative slice")]
        [SerializeField] private Vector2 _cellSizeMeters;
        [SerializeField] private SlagfallHabitatFamilyEntry[] _habitatFamilies =
            Array.Empty<SlagfallHabitatFamilyEntry>();
        [SerializeField] private GameObject _slagwhistlePrefab;
        [SerializeField] private GameObject _representativeSlicePrefab;
        [SerializeField] private AnimationClip[] _slagwhistleClips =
            Array.Empty<AnimationClip>();
        [SerializeField] private Texture2D[] _habitatTextureSet =
            Array.Empty<Texture2D>();
        [SerializeField] private Texture2D[] _slagwhistleTextureSet =
            Array.Empty<Texture2D>();

        [Header("Measured authored budgets")]
        [SerializeField] private int _slagwhistleLod0Triangles;
        [SerializeField] private int _slagwhistleLod1Triangles;
        [SerializeField] private int _slagwhistleLod2Triangles;
        [SerializeField] private int _slagwhistleImpostorTriangles;
        [SerializeField] private int _slagwhistleBoneCount;
        [SerializeField] private int _slagwhistleMaterialSlots;
        [SerializeField] private long _habitatSourceBytes;
        [SerializeField] private long _slagwhistleSourceBytes;

        public string SourceVersion => _sourceVersion;
        public string HabitatSourceId => _habitatSourceId;
        public string HabitatSourceSha256 => _habitatSourceSha256;
        public string SlagwhistleIdentitySourceId => _slagwhistleIdentitySourceId;
        public string SlagwhistleIdentitySha256 => _slagwhistleIdentitySha256;
        public string SlagwhistleMotionSourceId => _slagwhistleMotionSourceId;
        public string SlagwhistleMotionSha256 => _slagwhistleMotionSha256;
        public Vector2 CellSizeMeters => _cellSizeMeters;
        public IReadOnlyList<SlagfallHabitatFamilyEntry> HabitatFamilies =>
            _habitatFamilies;
        public GameObject SlagwhistlePrefab => _slagwhistlePrefab;
        public GameObject RepresentativeSlicePrefab => _representativeSlicePrefab;
        public IReadOnlyList<AnimationClip> SlagwhistleClips => _slagwhistleClips;
        public IReadOnlyList<Texture2D> HabitatTextureSet => _habitatTextureSet;
        public IReadOnlyList<Texture2D> SlagwhistleTextureSet =>
            _slagwhistleTextureSet;
        public int SlagwhistleLod0Triangles => _slagwhistleLod0Triangles;
        public int SlagwhistleLod1Triangles => _slagwhistleLod1Triangles;
        public int SlagwhistleLod2Triangles => _slagwhistleLod2Triangles;
        public int SlagwhistleImpostorTriangles => _slagwhistleImpostorTriangles;
        public int SlagwhistleBoneCount => _slagwhistleBoneCount;
        public int SlagwhistleMaterialSlots => _slagwhistleMaterialSlots;
        public long HabitatSourceBytes => _habitatSourceBytes;
        public long SlagwhistleSourceBytes => _slagwhistleSourceBytes;

        public void Configure(
            SlagfallHabitatFamilyEntry[] habitatFamilies,
            GameObject slagwhistlePrefab,
            GameObject representativeSlicePrefab,
            AnimationClip[] slagwhistleClips,
            Texture2D[] habitatTextureSet,
            Texture2D[] slagwhistleTextureSet,
            int slagwhistleLod0Triangles,
            int slagwhistleLod1Triangles,
            int slagwhistleLod2Triangles,
            int slagwhistleImpostorTriangles,
            int slagwhistleBoneCount,
            int slagwhistleMaterialSlots,
            long habitatSourceBytes,
            long slagwhistleSourceBytes)
        {
            _sourceVersion = SlagfallSourceAuthority.SourceVersion;
            _habitatSourceId = SlagfallSourceAuthority.HabitatSourceId;
            _habitatSourceSha256 =
                SlagfallSourceAuthority.HabitatSourceSha256;
            _slagwhistleIdentitySourceId =
                SlagfallSourceAuthority.SlagwhistleIdentitySourceId;
            _slagwhistleIdentitySha256 =
                SlagfallSourceAuthority.SlagwhistleIdentitySha256;
            _slagwhistleMotionSourceId =
                SlagfallSourceAuthority.SlagwhistleMotionSourceId;
            _slagwhistleMotionSha256 =
                SlagfallSourceAuthority.SlagwhistleMotionSha256;
            _cellSizeMeters = new Vector2(128f, 128f);
            _habitatFamilies =
                habitatFamilies ?? Array.Empty<SlagfallHabitatFamilyEntry>();
            _slagwhistlePrefab = slagwhistlePrefab;
            _representativeSlicePrefab = representativeSlicePrefab;
            _slagwhistleClips =
                slagwhistleClips ?? Array.Empty<AnimationClip>();
            _habitatTextureSet =
                habitatTextureSet ?? Array.Empty<Texture2D>();
            _slagwhistleTextureSet =
                slagwhistleTextureSet ?? Array.Empty<Texture2D>();
            _slagwhistleLod0Triangles = slagwhistleLod0Triangles;
            _slagwhistleLod1Triangles = slagwhistleLod1Triangles;
            _slagwhistleLod2Triangles = slagwhistleLod2Triangles;
            _slagwhistleImpostorTriangles = slagwhistleImpostorTriangles;
            _slagwhistleBoneCount = slagwhistleBoneCount;
            _slagwhistleMaterialSlots = slagwhistleMaterialSlots;
            _habitatSourceBytes = habitatSourceBytes;
            _slagwhistleSourceBytes = slagwhistleSourceBytes;
        }

        public bool Validate(out string diagnostic)
        {
            if (_sourceVersion != SlagfallSourceAuthority.SourceVersion ||
                _habitatSourceId != SlagfallSourceAuthority.HabitatSourceId ||
                _habitatSourceSha256 !=
                    SlagfallSourceAuthority.HabitatSourceSha256 ||
                _slagwhistleIdentitySourceId !=
                    SlagfallSourceAuthority.SlagwhistleIdentitySourceId ||
                _slagwhistleIdentitySha256 !=
                    SlagfallSourceAuthority.SlagwhistleIdentitySha256 ||
                _slagwhistleMotionSourceId !=
                    SlagfallSourceAuthority.SlagwhistleMotionSourceId ||
                _slagwhistleMotionSha256 !=
                    SlagfallSourceAuthority.SlagwhistleMotionSha256)
            {
                diagnostic = "approved_source_mismatch";
                return false;
            }

            if (_cellSizeMeters != new Vector2(128f, 128f))
            {
                diagnostic = "invalid_review_cell_size";
                return false;
            }

            if (_habitatFamilies == null ||
                _habitatFamilies.Length !=
                    SlagfallSourceAuthority.HabitatFamilyIds.Length)
            {
                diagnostic = "invalid_habitat_family_count";
                return false;
            }

            string[] familyIds = _habitatFamilies
                .Where(item => item != null)
                .Select(item => item.FamilyId)
                .ToArray();
            if (familyIds.Length != _habitatFamilies.Length ||
                familyIds.Distinct(StringComparer.Ordinal).Count() !=
                    familyIds.Length ||
                !SlagfallSourceAuthority.HabitatFamilyIds.All(
                    required => familyIds.Contains(
                        required,
                        StringComparer.Ordinal)))
            {
                diagnostic = "invalid_habitat_family_ids";
                return false;
            }

            foreach (SlagfallHabitatFamilyEntry family in _habitatFamilies)
            {
                if (!family.Validate(out diagnostic))
                {
                    return false;
                }
            }

            if (_slagwhistlePrefab == null ||
                _representativeSlicePrefab == null)
            {
                diagnostic = "missing_required_prefab";
                return false;
            }

            if (_slagwhistleClips == null ||
                _slagwhistleClips.Length == 0 ||
                _slagwhistleClips.Length > 6 ||
                _slagwhistleClips.Any(item => item == null))
            {
                diagnostic = "invalid_slagwhistle_clip_set";
                return false;
            }

            if (_habitatTextureSet == null ||
                _habitatTextureSet.Length != 3 ||
                _habitatTextureSet.Any(item => item == null) ||
                _slagwhistleTextureSet == null ||
                _slagwhistleTextureSet.Length != 3 ||
                _slagwhistleTextureSet.Any(item => item == null))
            {
                diagnostic = "invalid_texture_set";
                return false;
            }

            if (_slagwhistleLod0Triangles < 8000 ||
                _slagwhistleLod0Triangles > 10000 ||
                !InRatio(_slagwhistleLod1Triangles,
                    _slagwhistleLod0Triangles, 0.55f, 0.60f) ||
                !InRatio(_slagwhistleLod2Triangles,
                    _slagwhistleLod0Triangles, 0.20f, 0.25f) ||
                !InRatio(_slagwhistleImpostorTriangles,
                    _slagwhistleLod0Triangles, 0.06f, 0.08f))
            {
                diagnostic = "invalid_slagwhistle_lod_budget";
                return false;
            }

            if (_slagwhistleBoneCount < 34 ||
                _slagwhistleBoneCount > 42 ||
                _slagwhistleMaterialSlots < 1 ||
                _slagwhistleMaterialSlots > 2)
            {
                diagnostic = "invalid_slagwhistle_rig_or_material_budget";
                return false;
            }

            if (_habitatSourceBytes > 12L * 1024L * 1024L ||
                _slagwhistleSourceBytes > 7L * 1024L * 1024L)
            {
                diagnostic = "compressed_content_ceiling_exceeded";
                return false;
            }

            diagnostic = "ok";
            return true;
        }

        private static bool InRatio(
            int value,
            int reference,
            float minimum,
            float maximum)
        {
            if (reference <= 0)
            {
                return false;
            }

            float ratio = value / (float)reference;
            return ratio >= minimum && ratio <= maximum;
        }
    }

}
