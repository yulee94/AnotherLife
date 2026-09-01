using UnityEngine;

namespace AL.Motion
{
    [CreateAssetMenu(
        fileName = "MotionImportPreset",
        menuName = "Another Life/Motion/Import Preset")]
    public sealed class MotionImportPreset : ScriptableObject
    {
        [SerializeField] private string presetId;
        [SerializeField] private string skeletonProfileId;
        [SerializeField] private string retargetProfileId;
        [SerializeField] private MotionRigClassification rigClassification;
        [SerializeField] private MotionRetargetMode retargetMode;
        [SerializeField] private int sampleRateHz = 30;
        [SerializeField] private float globalScale = 1f;
        [SerializeField] private bool bakeAxisConversion = true;
        [SerializeField] private bool preserveHierarchy = true;
        [SerializeField] private bool importBlendShapes = true;
        [SerializeField] private bool optimizeGameObjects;
        [SerializeField] private float rotationError = 0.25f;
        [SerializeField] private float positionError = 0.5f;
        [SerializeField] private float scaleError = 0.5f;

        public string PresetId => presetId;
        public string SkeletonProfileId => skeletonProfileId;
        public string RetargetProfileId => retargetProfileId;
        public MotionRigClassification RigClassification => rigClassification;
        public MotionRetargetMode RetargetMode => retargetMode;
        public int SampleRateHz => sampleRateHz;
        public float GlobalScale => globalScale;
        public bool BakeAxisConversion => bakeAxisConversion;
        public bool PreserveHierarchy => preserveHierarchy;
        public bool ImportBlendShapes => importBlendShapes;
        public bool OptimizeGameObjects => optimizeGameObjects;
        public float RotationError => rotationError;
        public float PositionError => positionError;
        public float ScaleError => scaleError;

        public bool HasValidTechnicalIdentity()
        {
            return !string.IsNullOrWhiteSpace(presetId) &&
                   !string.IsNullOrWhiteSpace(skeletonProfileId) &&
                   !string.IsNullOrWhiteSpace(retargetProfileId) &&
                   sampleRateHz >= 30 && sampleRateHz <= 60 &&
                   Mathf.Abs(globalScale - 1f) <= 0.0001f &&
                   bakeAxisConversion && preserveHierarchy &&
                   rotationError >= 0f && positionError >= 0f && scaleError >= 0f;
        }
    }
}
