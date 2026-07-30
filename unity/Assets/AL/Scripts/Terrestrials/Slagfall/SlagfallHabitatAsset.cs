using UnityEngine;

namespace AL.Terrestrials.Slagfall
{
    [DisallowMultipleComponent]
    public sealed class SlagfallHabitatAsset : MonoBehaviour
    {
        [SerializeField] private string _familyId;
        [SerializeField, Min(0)] private int _variantIndex;
        [SerializeField] private LODGroup _lodGroup;

        public string FamilyId => _familyId;
        public int VariantIndex => _variantIndex;
        public LODGroup LodGroup => _lodGroup;

        public void Configure(
            string familyId,
            int variantIndex,
            LODGroup lodGroup)
        {
            _familyId = familyId;
            _variantIndex = variantIndex;
            _lodGroup = lodGroup;
        }
    }
}
