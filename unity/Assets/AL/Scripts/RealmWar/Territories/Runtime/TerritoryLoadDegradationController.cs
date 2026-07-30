using System;
using System.Collections.Generic;
using UnityEngine;

namespace AL.RealmWar.Territories.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TerritoryLoadDegradationController : MonoBehaviour
    {
        [Header("Sampling")]
        [SerializeField, Min(1f)] private float _targetFrameTimeMilliseconds = 33.333f;
        [SerializeField, Min(0.05f)] private float _sampleIntervalSeconds = 0.25f;
        [SerializeField, Min(0f)] private float _degradeDelaySeconds = 0.5f;
        [SerializeField, Min(0f)] private float _recoverDelaySeconds = 3f;

        [Header("Territory")]
        [SerializeField] private Transform _observer;
        [SerializeField] private TerritoryLoadVisualAdapter _visualAdapter;
        [SerializeField, Min(0)] private int _activeUserCount;
        [SerializeField] private List<TerritoryCrowdParticipant> _participants =
            new List<TerritoryCrowdParticipant>();

        private readonly List<TerritoryCrowdParticipant> _sortedParticipants =
            new List<TerritoryCrowdParticipant>(TerritoryLoadDegradationPlanner.SafeRepresentedUserCapacity);
        private readonly ParticipantDistanceComparer _distanceComparer = new ParticipantDistanceComparer();

        private TerritoryLoadStateMachine _stateMachine;
        private float _smoothedFrameTimeMilliseconds;
        private float _sampleElapsedSeconds;
        private bool _participantsDirty;

        public event Action<TerritoryLoadPlan> PlanApplied;

        public TerritoryLoadLevel CurrentLevel =>
            _stateMachine?.CurrentLevel ?? TerritoryLoadLevel.Normal;

        public TerritoryLoadPlan CurrentPlan { get; private set; }

        public int ActiveUserCount => _activeUserCount;

        public int PlanApplicationCount { get; private set; }

        private void Awake()
        {
            EnsureRuntimeState();
            ApplyCurrentPlan();
        }

        private void OnEnable()
        {
            EnsureRuntimeState();
            ApplyCurrentPlan();
        }

        private void OnDisable()
        {
            RestoreAuthoredState();
        }

        private void Update()
        {
            if (_participantsDirty)
            {
                ApplyCurrentPlan();
            }

            float elapsed = Time.unscaledDeltaTime;
            float frameTimeMilliseconds = elapsed * 1000f;
            _smoothedFrameTimeMilliseconds = _smoothedFrameTimeMilliseconds <= 0f
                ? frameTimeMilliseconds
                : Mathf.Lerp(_smoothedFrameTimeMilliseconds, frameTimeMilliseconds, 0.10f);
            _sampleElapsedSeconds += elapsed;

            if (_sampleElapsedSeconds < _sampleIntervalSeconds)
            {
                return;
            }

            ProcessSample(_activeUserCount, _smoothedFrameTimeMilliseconds, _sampleElapsedSeconds);
            _sampleElapsedSeconds = 0f;
        }

        public void Configure(
            Transform observer,
            TerritoryLoadVisualAdapter visualAdapter,
            float targetFrameTimeMilliseconds = 33.333f,
            float degradeDelaySeconds = 0.5f,
            float recoverDelaySeconds = 3f)
        {
            if (float.IsNaN(targetFrameTimeMilliseconds) ||
                float.IsInfinity(targetFrameTimeMilliseconds) ||
                targetFrameTimeMilliseconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(targetFrameTimeMilliseconds));
            }

            _observer = observer;
            _visualAdapter = visualAdapter;
            _targetFrameTimeMilliseconds = targetFrameTimeMilliseconds;
            _degradeDelaySeconds = degradeDelaySeconds;
            _recoverDelaySeconds = recoverDelaySeconds;
            _stateMachine = new TerritoryLoadStateMachine(degradeDelaySeconds, recoverDelaySeconds);
            _smoothedFrameTimeMilliseconds = 0f;
            _sampleElapsedSeconds = 0f;
            ApplyCurrentPlan();
        }

        public void SetActiveUserCount(int activeUserCount)
        {
            if (activeUserCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(activeUserCount));
            }

            _activeUserCount = activeUserCount;
            PromoteForUserCountIfRequired();
        }

        public bool ProcessSample(
            int activeUserCount,
            float averageFrameTimeMilliseconds,
            float elapsedSeconds)
        {
            EnsureRuntimeState();
            RemoveMissingParticipants();
            _activeUserCount = activeUserCount;
            int effectiveUserCount = Math.Max(_activeUserCount, _participants.Count);
            PromoteForUserCountIfRequired();
            TerritoryLoadLevel requiredLevel = TerritoryLoadDegradationPlanner.EvaluateRequiredLevel(
                effectiveUserCount,
                averageFrameTimeMilliseconds,
                _targetFrameTimeMilliseconds);
            bool changed = _stateMachine.Step(requiredLevel, elapsedSeconds);
            ApplyCurrentPlan();
            return changed;
        }

        public void Register(TerritoryCrowdParticipant participant)
        {
            if (!RegisterWithoutRefresh(participant))
            {
                return;
            }

            participant.ApplyTier(TerritoryRenderTier.Impostor);
            PromoteForUserCountIfRequired();
            _participantsDirty = true;
        }

        public void RegisterRange(IEnumerable<TerritoryCrowdParticipant> participants)
        {
            if (participants == null)
            {
                throw new ArgumentNullException(nameof(participants));
            }

            bool changed = false;
            foreach (TerritoryCrowdParticipant participant in participants)
            {
                if (!RegisterWithoutRefresh(participant))
                {
                    continue;
                }

                participant.ApplyTier(TerritoryRenderTier.Impostor);
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            PromoteForUserCountIfRequired();
            ApplyCurrentPlan();
        }

        public void Unregister(TerritoryCrowdParticipant participant)
        {
            if (participant == null || !_participants.Remove(participant))
            {
                return;
            }

            participant.RestoreAuthoredState();
            _participantsDirty = true;
        }

        public void Refresh()
        {
            EnsureRuntimeState();
            ApplyCurrentPlan();
        }

        private void EnsureRuntimeState()
        {
            _participants ??= new List<TerritoryCrowdParticipant>();
            _stateMachine ??= new TerritoryLoadStateMachine(_degradeDelaySeconds, _recoverDelaySeconds);
        }

        private void ApplyCurrentPlan()
        {
            EnsureRuntimeState();
            RemoveMissingParticipants();

            Vector3 sortOrigin = ResolveObserverPosition();
            _sortedParticipants.Clear();
            _sortedParticipants.AddRange(_participants);
            _distanceComparer.Origin = sortOrigin;
            _sortedParticipants.Sort(_distanceComparer);

            CurrentPlan = TerritoryLoadDegradationPlanner.CreatePlan(
                _sortedParticipants.Count,
                _stateMachine.CurrentLevel);

            int fullEnd = CurrentPlan.FullDetailCount;
            int mediumEnd = fullEnd + CurrentPlan.MediumDetailCount;
            int lowEnd = mediumEnd + CurrentPlan.LowDetailCount;
            int impostorEnd = lowEnd + CurrentPlan.ImpostorCount;

            for (int index = 0; index < _sortedParticipants.Count; index++)
            {
                TerritoryRenderTier tier = index < fullEnd
                    ? TerritoryRenderTier.FullDetail
                    : index < mediumEnd
                        ? TerritoryRenderTier.MediumDetail
                        : index < lowEnd
                            ? TerritoryRenderTier.LowDetail
                            : index < impostorEnd
                                ? TerritoryRenderTier.Impostor
                                : TerritoryRenderTier.Culled;
                _sortedParticipants[index].ApplyTier(tier);
            }

            _visualAdapter?.Apply(CurrentPlan.Budget);
            _participantsDirty = false;
            PlanApplicationCount++;
            PlanApplied?.Invoke(CurrentPlan);
        }

        private bool RegisterWithoutRefresh(TerritoryCrowdParticipant participant)
        {
            if (participant == null || _participants.Contains(participant))
            {
                return false;
            }

            participant.ValidateForRegistration();
            _participants.Add(participant);
            return true;
        }

        private void PromoteForUserCountIfRequired()
        {
            EnsureRuntimeState();
            int effectiveUserCount = Math.Max(_activeUserCount, _participants.Count);
            TerritoryLoadLevel userFloor =
                TerritoryLoadDegradationPlanner.EvaluateUserLevel(effectiveUserCount);
            if (userFloor > _stateMachine.CurrentLevel)
            {
                _stateMachine.Reset(userFloor);
                _participantsDirty = true;
            }
        }

        private Vector3 ResolveObserverPosition()
        {
            if (_observer != null)
            {
                return _observer.position;
            }

            Camera mainCamera = Camera.main;
            return mainCamera != null ? mainCamera.transform.position : transform.position;
        }

        private void RemoveMissingParticipants()
        {
            for (int index = _participants.Count - 1; index >= 0; index--)
            {
                if (_participants[index] == null)
                {
                    _participants.RemoveAt(index);
                }
            }
        }

        private void RestoreAuthoredState()
        {
            if (_participants == null)
            {
                _visualAdapter?.RestoreBaseline();
                return;
            }

            foreach (TerritoryCrowdParticipant participant in _participants)
            {
                participant?.RestoreAuthoredState();
            }

            _visualAdapter?.RestoreBaseline();
        }

        private sealed class ParticipantDistanceComparer : IComparer<TerritoryCrowdParticipant>
        {
            public Vector3 Origin { get; set; }

            public int Compare(TerritoryCrowdParticipant left, TerritoryCrowdParticipant right)
            {
                if (ReferenceEquals(left, right))
                {
                    return 0;
                }

                if (left == null)
                {
                    return 1;
                }

                if (right == null)
                {
                    return -1;
                }

                int distanceComparison = left.DistanceSquaredTo(Origin).CompareTo(right.DistanceSquaredTo(Origin));
                return distanceComparison != 0
                    ? distanceComparison
                    : left.GetInstanceID().CompareTo(right.GetInstanceID());
            }
        }
    }
}
