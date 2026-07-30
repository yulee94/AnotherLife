using System;
using System.Collections.Generic;
using AL.RealmWar.Territories.Runtime;
using UnityEngine;

namespace AL.Terrestrials.Slagfall
{
    [DisallowMultipleComponent]
    public sealed class SlagfallRepresentativeSlice : MonoBehaviour
    {
        [SerializeField] private SlagfallRepresentativeSliceProfile _profile;
        [SerializeField] private TerritoryLoadDegradationController _controller;
        [SerializeField] private TerritoryLoadVisualAdapter _visualAdapter;
        [SerializeField] private SlagwhistlePresentation _slagwhistle;
        [SerializeField] private TerritoryCrowdParticipant[] _syntheticCrowd =
            Array.Empty<TerritoryCrowdParticipant>();
        [SerializeField] private GameObject[] _decorativeObjects =
            Array.Empty<GameObject>();

        private bool _initialized;

        public SlagfallRepresentativeSliceProfile Profile => _profile;
        public TerritoryLoadDegradationController Controller => _controller;
        public SlagwhistlePresentation Slagwhistle => _slagwhistle;
        public IReadOnlyList<TerritoryCrowdParticipant> SyntheticCrowd =>
            _syntheticCrowd;
        public bool EffectsOff { get; private set; }
        public bool ReducedMotion { get; private set; }
        public int ActiveRepresentedSyntheticUserCount
        {
            get
            {
                int represented = 0;
                foreach (TerritoryCrowdParticipant participant in
                    _syntheticCrowd)
                {
                    if (participant != null &&
                        participant.gameObject.activeInHierarchy &&
                        participant.IsRepresented &&
                        participant.ActiveRepresentationCount == 1)
                    {
                        represented++;
                    }
                }

                return represented;
            }
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (_controller != null)
            {
                _controller.PlanApplied += HandlePlanApplied;
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.PlanApplied -= HandlePlanApplied;
            }
        }

        public void Configure(
            SlagfallRepresentativeSliceProfile profile,
            TerritoryLoadDegradationController controller,
            TerritoryLoadVisualAdapter visualAdapter,
            SlagwhistlePresentation slagwhistle,
            TerritoryCrowdParticipant[] syntheticCrowd,
            GameObject[] decorativeObjects)
        {
            _profile = profile;
            _controller = controller;
            _visualAdapter = visualAdapter;
            _slagwhistle = slagwhistle;
            _syntheticCrowd =
                syntheticCrowd ?? Array.Empty<TerritoryCrowdParticipant>();
            _decorativeObjects =
                decorativeObjects ?? Array.Empty<GameObject>();
            _initialized = false;
        }

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            if (_profile == null)
            {
                throw new InvalidOperationException(
                    "Slagfall representative slice profile is missing.");
            }

            if (!_profile.Validate(out string diagnostic))
            {
                throw new InvalidOperationException(
                    $"Slagfall representative slice profile is invalid: {diagnostic}");
            }

            if (_controller == null ||
                _visualAdapter == null ||
                _slagwhistle == null)
            {
                throw new InvalidOperationException(
                    "Slagfall representative slice is missing its controller, visual adapter, or Slagwhistle.");
            }

            if (!_slagwhistle.ValidateRequiredRepresentations(
                out string presentationDiagnostic))
            {
                throw new InvalidOperationException(
                    $"Slagwhistle presentation is invalid: {presentationDiagnostic}");
            }

            if (_syntheticCrowd == null ||
                _syntheticCrowd.Length !=
                    TerritoryLoadDegradationPlanner.SafeRepresentedUserCapacity)
            {
                throw new InvalidOperationException(
                    "Slagfall representative slice must contain exactly 100 synthetic users.");
            }

            _controller.RegisterRange(_syntheticCrowd);
            _controller.SetActiveUserCount(_syntheticCrowd.Length);
            _slagwhistle.ApplyLoad(_controller.CurrentLevel);
            _initialized = true;
        }

        public void ApplySyntheticPressure(
            float frameTimeMilliseconds,
            float elapsedSeconds)
        {
            Initialize();
            _controller.ProcessSample(
                _syntheticCrowd.Length,
                frameTimeMilliseconds,
                elapsedSeconds);
            _slagwhistle.ApplyLoad(_controller.CurrentLevel);
        }

        public void SetTargetFrameTimeMilliseconds(
            float targetFrameTimeMilliseconds)
        {
            Initialize();
            _controller.SetTargetFrameTimeMilliseconds(
                targetFrameTimeMilliseconds);
        }

        public void SetSyntheticCrowdActive(bool active)
        {
            Initialize();
            var crowdRoots = new HashSet<GameObject>();
            foreach (TerritoryCrowdParticipant participant in
                _syntheticCrowd)
            {
                if (participant == null)
                {
                    continue;
                }

                Transform parent = participant.transform.parent;
                if (parent != null)
                {
                    crowdRoots.Add(parent.gameObject);
                }
                else if (participant.gameObject.activeSelf != active)
                {
                    participant.gameObject.SetActive(active);
                }
            }

            foreach (GameObject crowdRoot in crowdRoots)
            {
                if (crowdRoot.activeSelf != active)
                {
                    crowdRoot.SetActive(active);
                }
            }

            if (active)
            {
                _controller.Refresh();
            }
        }

        public void SetAccessibility(
            bool effectsOff,
            bool reducedMotion)
        {
            EffectsOff = effectsOff;
            ReducedMotion = reducedMotion;
            foreach (GameObject decorativeObject in _decorativeObjects)
            {
                if (decorativeObject != null)
                {
                    decorativeObject.SetActive(!effectsOff);
                }
            }
            _slagwhistle?.SetReducedMotion(reducedMotion);
        }

        public void CancelOptionalPresentation()
        {
            Initialize();
            _slagwhistle.CancelOptionalTiers();
        }

        private void HandlePlanApplied(TerritoryLoadPlan plan)
        {
            _slagwhistle?.ApplyLoad(plan.Budget.Level);
        }
    }
}
