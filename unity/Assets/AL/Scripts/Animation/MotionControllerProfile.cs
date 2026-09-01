using System;
using System.Collections.Generic;
using UnityEngine;

namespace AL.Motion
{
    [CreateAssetMenu(
        fileName = "MotionControllerProfile",
        menuName = "Another Life/Motion/Controller Profile")]
    public sealed class MotionControllerProfile : ScriptableObject
    {
        [SerializeField] private string standardId = "rmc_standard_rig_motion_v001";
        [SerializeField] private string profileId;
        [SerializeField] private MotionSubjectKind subjectKind;
        [SerializeField] private MotionRigClassification rigClassification;
        [SerializeField] private string skeletonProfileId;
        [SerializeField] private string retargetProfileId;
        [SerializeField] private string bindPoseId;
        [SerializeField] private string layerProfileId;
        [SerializeField] private string safeMotionKey = "idle.neutral";
        [SerializeField] private TextAsset requiredMotionManifest;
        [SerializeField] private int maximumLayers;
        [SerializeField] private MotionProfileLayer[] layers = Array.Empty<MotionProfileLayer>();

        public string StandardId => standardId;
        public string ProfileId => profileId;
        public MotionSubjectKind SubjectKind => subjectKind;
        public MotionRigClassification RigClassification => rigClassification;
        public string SkeletonProfileId => skeletonProfileId;
        public string RetargetProfileId => retargetProfileId;
        public string BindPoseId => bindPoseId;
        public string LayerProfileId => layerProfileId;
        public string SafeMotionKey => safeMotionKey;
        public TextAsset RequiredMotionManifest => requiredMotionManifest;
        public int MaximumLayers => maximumLayers;
        public IReadOnlyList<MotionProfileLayer> Layers => Array.AsReadOnly(layers);

        public bool HasValidTechnicalIdentity()
        {
            return string.Equals(
                       standardId,
                       "rmc_standard_rig_motion_v001",
                       StringComparison.Ordinal) &&
                   !string.IsNullOrWhiteSpace(profileId) &&
                   !string.IsNullOrWhiteSpace(skeletonProfileId) &&
                   !string.IsNullOrWhiteSpace(retargetProfileId) &&
                   !string.IsNullOrWhiteSpace(bindPoseId) &&
                   !string.IsNullOrWhiteSpace(layerProfileId) &&
                   !string.IsNullOrWhiteSpace(safeMotionKey) &&
                   requiredMotionManifest != null &&
                   maximumLayers > 0 &&
                   layers != null && layers.Length <= maximumLayers;
        }
    }
}
