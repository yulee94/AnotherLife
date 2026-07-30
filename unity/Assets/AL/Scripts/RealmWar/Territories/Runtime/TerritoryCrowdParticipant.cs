using System;
using UnityEngine;

namespace AL.RealmWar.Territories.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TerritoryCrowdParticipant : MonoBehaviour
    {
        [SerializeField] private GameObject _fullDetail;
        [SerializeField] private GameObject _mediumDetail;
        [SerializeField] private GameObject _lowDetail;
        [SerializeField] private GameObject _impostor;

        private Animator[] _animators = Array.Empty<Animator>();
        private bool[] _animatorEnabledBaselines = Array.Empty<bool>();
        private bool _fullDetailBaseline;
        private bool _mediumDetailBaseline;
        private bool _lowDetailBaseline;
        private bool _impostorBaseline;
        private bool _baselineCaptured;

        public TerritoryRenderTier CurrentTier { get; private set; } = TerritoryRenderTier.Culled;

        public bool IsRepresented => CurrentTier != TerritoryRenderTier.Culled;

        public bool HasCompleteRepresentationSet =>
            _fullDetail != null &&
            _mediumDetail != null &&
            _lowDetail != null &&
            _impostor != null;

        public int ActiveRepresentationCount
        {
            get
            {
                int count = 0;
                count += IsActive(_fullDetail) ? 1 : 0;
                count += IsActive(_mediumDetail) ? 1 : 0;
                count += IsActive(_lowDetail) ? 1 : 0;
                count += IsActive(_impostor) ? 1 : 0;
                return count;
            }
        }

        private void Awake()
        {
            CaptureBaseline();
        }

        public void Configure(
            GameObject fullDetail,
            GameObject mediumDetail,
            GameObject lowDetail,
            GameObject impostor)
        {
            ValidateRepresentation(fullDetail, nameof(fullDetail));
            ValidateRepresentation(mediumDetail, nameof(mediumDetail));
            ValidateRepresentation(lowDetail, nameof(lowDetail));
            ValidateRepresentation(impostor, nameof(impostor));
            ValidateDistinct(fullDetail, mediumDetail, lowDetail, impostor);

            _fullDetail = fullDetail;
            _mediumDetail = mediumDetail;
            _lowDetail = lowDetail;
            _impostor = impostor;
            CaptureBaseline();
        }

        public void ValidateForRegistration()
        {
            if (!HasCompleteRepresentationSet)
            {
                throw new InvalidOperationException(
                    $"{name} cannot enter the protected territory crowd without full, medium, low, and impostor representations.");
            }

            ValidateDistinct(_fullDetail, _mediumDetail, _lowDetail, _impostor);
        }

        public TerritoryRenderTier ApplyTier(TerritoryRenderTier requestedTier)
        {
            if (!Enum.IsDefined(typeof(TerritoryRenderTier), requestedTier))
            {
                throw new ArgumentOutOfRangeException(nameof(requestedTier));
            }

            EnsureBaseline();
            TerritoryRenderTier resolvedTier = ResolveAvailableTier(requestedTier);
            SetActiveIfSafe(_fullDetail, resolvedTier == TerritoryRenderTier.FullDetail);
            SetActiveIfSafe(_mediumDetail, resolvedTier == TerritoryRenderTier.MediumDetail);
            SetActiveIfSafe(_lowDetail, resolvedTier == TerritoryRenderTier.LowDetail);
            SetActiveIfSafe(_impostor, resolvedTier == TerritoryRenderTier.Impostor);

            bool animate = resolvedTier == TerritoryRenderTier.FullDetail ||
                           resolvedTier == TerritoryRenderTier.MediumDetail;
            for (int index = 0; index < _animators.Length; index++)
            {
                Animator animator = _animators[index];
                if (animator != null)
                {
                    animator.enabled = animate && _animatorEnabledBaselines[index];
                }
            }

            CurrentTier = resolvedTier;
            return resolvedTier;
        }

        public void RestoreAuthoredState()
        {
            if (!_baselineCaptured)
            {
                return;
            }

            SetActiveIfSafe(_fullDetail, _fullDetailBaseline);
            SetActiveIfSafe(_mediumDetail, _mediumDetailBaseline);
            SetActiveIfSafe(_lowDetail, _lowDetailBaseline);
            SetActiveIfSafe(_impostor, _impostorBaseline);

            for (int index = 0; index < _animators.Length; index++)
            {
                Animator animator = _animators[index];
                if (animator != null)
                {
                    animator.enabled = _animatorEnabledBaselines[index];
                }
            }
        }

        public float DistanceSquaredTo(Vector3 worldPosition)
        {
            return (transform.position - worldPosition).sqrMagnitude;
        }

        private void CaptureBaseline()
        {
            _fullDetailBaseline = IsActive(_fullDetail);
            _mediumDetailBaseline = IsActive(_mediumDetail);
            _lowDetailBaseline = IsActive(_lowDetail);
            _impostorBaseline = IsActive(_impostor);
            _animators = GetComponentsInChildren<Animator>(true);
            _animatorEnabledBaselines = new bool[_animators.Length];
            for (int index = 0; index < _animators.Length; index++)
            {
                _animatorEnabledBaselines[index] = _animators[index] != null && _animators[index].enabled;
            }

            _baselineCaptured = true;
        }

        private void EnsureBaseline()
        {
            if (!_baselineCaptured)
            {
                CaptureBaseline();
            }
        }

        private TerritoryRenderTier ResolveAvailableTier(TerritoryRenderTier requestedTier)
        {
            switch (requestedTier)
            {
                case TerritoryRenderTier.FullDetail:
                    if (_fullDetail != null)
                    {
                        return TerritoryRenderTier.FullDetail;
                    }

                    goto case TerritoryRenderTier.MediumDetail;
                case TerritoryRenderTier.MediumDetail:
                    if (_mediumDetail != null)
                    {
                        return TerritoryRenderTier.MediumDetail;
                    }

                    goto case TerritoryRenderTier.LowDetail;
                case TerritoryRenderTier.LowDetail:
                    if (_lowDetail != null)
                    {
                        return TerritoryRenderTier.LowDetail;
                    }

                    goto case TerritoryRenderTier.Impostor;
                case TerritoryRenderTier.Impostor:
                    return _impostor != null
                        ? TerritoryRenderTier.Impostor
                        : TerritoryRenderTier.Culled;
                default:
                    return TerritoryRenderTier.Culled;
            }
        }

        private void ValidateRepresentation(GameObject representation, string parameterName)
        {
            if (representation == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (representation == gameObject)
            {
                throw new ArgumentException(
                    "A participant representation cannot be the participant root because culling it would disable recovery.",
                    parameterName);
            }
        }

        private static void ValidateDistinct(params GameObject[] representations)
        {
            for (int left = 0; left < representations.Length; left++)
            {
                if (representations[left] == null)
                {
                    continue;
                }

                for (int right = left + 1; right < representations.Length; right++)
                {
                    if (representations[left] == representations[right])
                    {
                        throw new ArgumentException("Territory crowd representations must be distinct objects.");
                    }
                }
            }
        }

        private static bool IsActive(GameObject target)
        {
            return target != null && target.activeSelf;
        }

        private static void SetActiveIfSafe(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
