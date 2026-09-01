using System;
using UnityEngine;

namespace AL.Motion
{
    public enum MotionSubjectKind
    {
        Champion = 0,
        Npc = 1,
        Beast = 2,
        Monster = 3
    }

    public enum MotionRigClassification
    {
        Humanoid = 0,
        Generic = 1
    }

    public enum MotionRetargetMode
    {
        UnityHumanoid = 0,
        GenericExactSignature = 1,
        GenericSemanticChain = 2
    }

    [Serializable]
    public sealed class MotionProfileLayer
    {
        [SerializeField] private string layerId;
        [SerializeField] private bool additive;
        [SerializeField] private AvatarMask mask;
        [SerializeField] private int priority;

        public string LayerId => layerId;
        public bool Additive => additive;
        public AvatarMask Mask => mask;
        public int Priority => priority;
    }
}
