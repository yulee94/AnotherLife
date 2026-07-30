using System;
using AL.RealmWar.Territories.Runtime;
using UnityEngine;

namespace AL.Terrestrials.Slagfall
{
    [DisallowMultipleComponent]
    public sealed class SlagwhistlePresentation : MonoBehaviour
    {
        [SerializeField] private GameObject _fullDetail;
        [SerializeField] private GameObject _mediumDetail;
        [SerializeField] private GameObject _lowDetail;
        [SerializeField] private GameObject _impostor;
        [SerializeField] private Animator[] _animators = Array.Empty<Animator>();
        [SerializeField] private Transform[] _reducedMotionTransforms =
            Array.Empty<Transform>();

        private Quaternion[] _authoredRotations = Array.Empty<Quaternion>();
        private bool _reducedMotion;

        public TerritoryRenderTier CurrentTier { get; private set; } =
            TerritoryRenderTier.FullDetail;
        public bool ReducedMotion => _reducedMotion;
        public bool IsRepresented =>
            CurrentTier != TerritoryRenderTier.Culled &&
            ActiveRepresentationCount == 1;
        public int ActiveRepresentationCount =>
            CountActive(_fullDetail) +
            CountActive(_mediumDetail) +
            CountActive(_lowDetail) +
            CountActive(_impostor);

        public bool ValidateRequiredRepresentations(out string diagnostic)
        {
            if (_fullDetail == null)
            {
                diagnostic = "missing_slagwhistle_full_detail";
                return false;
            }

            if (_mediumDetail == null)
            {
                diagnostic = "missing_slagwhistle_medium_detail";
                return false;
            }

            if (_lowDetail == null)
            {
                diagnostic = "missing_slagwhistle_low_detail";
                return false;
            }

            if (_impostor == null)
            {
                diagnostic = "missing_slagwhistle_impostor";
                return false;
            }

            diagnostic = "ok";
            return true;
        }

        private void Awake()
        {
            CaptureAuthoredState();
        }

        public void Configure(
            GameObject fullDetail,
            GameObject mediumDetail,
            GameObject lowDetail,
            GameObject impostor,
            Animator[] animators,
            Transform[] reducedMotionTransforms)
        {
            _fullDetail = fullDetail;
            _mediumDetail = mediumDetail;
            _lowDetail = lowDetail;
            _impostor = impostor;
            _animators = animators ?? Array.Empty<Animator>();
            _reducedMotionTransforms =
                reducedMotionTransforms ?? Array.Empty<Transform>();
            CaptureAuthoredState();
            ApplyTier(TerritoryRenderTier.FullDetail);
        }

        public void ApplyLoad(TerritoryLoadLevel level)
        {
            switch (level)
            {
                case TerritoryLoadLevel.Normal:
                    ApplyTier(TerritoryRenderTier.FullDetail);
                    break;
                case TerritoryLoadLevel.Elevated:
                    ApplyTier(TerritoryRenderTier.MediumDetail);
                    break;
                case TerritoryLoadLevel.Heavy:
                    ApplyTier(TerritoryRenderTier.LowDetail);
                    break;
                case TerritoryLoadLevel.Critical:
                    ApplyTier(TerritoryRenderTier.Impostor);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level));
            }
        }

        public void CancelOptionalTiers()
        {
            ApplyTier(TerritoryRenderTier.LowDetail);
        }

        public void SetReducedMotion(bool enabled)
        {
            EnsureAuthoredState();
            _reducedMotion = enabled;
            for (int index = 0; index < _animators.Length; index++)
            {
                Animator animator = _animators[index];
                if (animator != null)
                {
                    bool animatedTier =
                        CurrentTier == TerritoryRenderTier.FullDetail ||
                        CurrentTier == TerritoryRenderTier.MediumDetail;
                    animator.enabled = animatedTier && !enabled;
                }
            }

            if (!enabled)
            {
                return;
            }

            for (int index = 0;
                index < _reducedMotionTransforms.Length;
                index++)
            {
                Transform target = _reducedMotionTransforms[index];
                if (target != null)
                {
                    target.localRotation = _authoredRotations[index];
                }
            }
        }

        public void ApplyTier(TerritoryRenderTier tier)
        {
            if (tier == TerritoryRenderTier.Culled)
            {
                tier = TerritoryRenderTier.Impostor;
            }

            SetActive(_fullDetail, tier == TerritoryRenderTier.FullDetail);
            SetActive(_mediumDetail, tier == TerritoryRenderTier.MediumDetail);
            SetActive(_lowDetail, tier == TerritoryRenderTier.LowDetail);
            SetActive(_impostor, tier == TerritoryRenderTier.Impostor);
            CurrentTier = tier;
            SetReducedMotion(_reducedMotion);
        }

        private void CaptureAuthoredState()
        {
            _reducedMotionTransforms ??= Array.Empty<Transform>();
            _authoredRotations =
                new Quaternion[_reducedMotionTransforms.Length];
            for (int index = 0;
                index < _reducedMotionTransforms.Length;
                index++)
            {
                Transform target = _reducedMotionTransforms[index];
                _authoredRotations[index] =
                    target != null
                        ? target.localRotation
                        : Quaternion.identity;
            }
        }

        private void EnsureAuthoredState()
        {
            if (_authoredRotations == null ||
                _authoredRotations.Length !=
                    (_reducedMotionTransforms?.Length ?? 0))
            {
                CaptureAuthoredState();
            }
        }

        private static int CountActive(GameObject target)
        {
            return target != null && target.activeSelf ? 1 : 0;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
