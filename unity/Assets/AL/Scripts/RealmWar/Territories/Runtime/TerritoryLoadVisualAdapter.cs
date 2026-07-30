using System;
using UnityEngine;

namespace AL.RealmWar.Territories.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TerritoryLoadVisualAdapter : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] _decorativeParticles = Array.Empty<ParticleSystem>();
        [SerializeField] private ParticleSystem[] _weatherParticles = Array.Empty<ParticleSystem>();
        [SerializeField] private Light[] _decorativeLights = Array.Empty<Light>();
        [SerializeField] private LODGroup[] _environmentLodGroups = Array.Empty<LODGroup>();

        private ParticleBaseline[] _decorativeParticleBaselines = Array.Empty<ParticleBaseline>();
        private ParticleBaseline[] _weatherParticleBaselines = Array.Empty<ParticleBaseline>();
        private bool[] _lightEnabledBaselines = Array.Empty<bool>();
        private LOD[][] _lodBaselines = Array.Empty<LOD[]>();
        private bool _baselineCaptured;
        private bool _hasApplied;

        public TerritoryLoadLevel LastAppliedLevel { get; private set; } = TerritoryLoadLevel.Normal;

        private void Awake()
        {
            CaptureBaseline();
        }

        private void OnDisable()
        {
            RestoreBaseline();
        }

        public void Configure(
            ParticleSystem[] decorativeParticles,
            ParticleSystem[] weatherParticles,
            Light[] decorativeLights,
            LODGroup[] environmentLodGroups)
        {
            _decorativeParticles = decorativeParticles ?? Array.Empty<ParticleSystem>();
            _weatherParticles = weatherParticles ?? Array.Empty<ParticleSystem>();
            _decorativeLights = decorativeLights ?? Array.Empty<Light>();
            _environmentLodGroups = environmentLodGroups ?? Array.Empty<LODGroup>();
            CaptureBaseline();
        }

        public void Apply(TerritoryLoadBudget budget)
        {
            EnsureBaseline();
            if (_hasApplied && LastAppliedLevel == budget.Level)
            {
                return;
            }

            ApplyParticleBudget(_decorativeParticles, _decorativeParticleBaselines, budget.DecorativeVfxMultiplier);
            ApplyParticleBudget(_weatherParticles, _weatherParticleBaselines, budget.WeatherMultiplier);
            ApplyLightBudget(budget.DecorativeLightsEnabled);
            ApplyLodBudget(budget.EnvironmentLodTransitionMultiplier);
            LastAppliedLevel = budget.Level;
            _hasApplied = true;
        }

        public void RestoreBaseline()
        {
            if (!_baselineCaptured)
            {
                return;
            }

            RestoreParticleBaseline(_decorativeParticles, _decorativeParticleBaselines);
            RestoreParticleBaseline(_weatherParticles, _weatherParticleBaselines);

            for (int index = 0; index < _decorativeLights.Length; index++)
            {
                Light light = _decorativeLights[index];
                if (light != null)
                {
                    light.enabled = _lightEnabledBaselines[index];
                }
            }

            for (int index = 0; index < _environmentLodGroups.Length; index++)
            {
                LODGroup lodGroup = _environmentLodGroups[index];
                if (lodGroup != null)
                {
                    lodGroup.SetLODs(_lodBaselines[index]);
                }
            }

            LastAppliedLevel = TerritoryLoadLevel.Normal;
            _hasApplied = false;
        }

        private void CaptureBaseline()
        {
            _decorativeParticles ??= Array.Empty<ParticleSystem>();
            _weatherParticles ??= Array.Empty<ParticleSystem>();
            _decorativeLights ??= Array.Empty<Light>();
            _environmentLodGroups ??= Array.Empty<LODGroup>();

            _decorativeParticleBaselines = CaptureParticleBaselines(_decorativeParticles);
            _weatherParticleBaselines = CaptureParticleBaselines(_weatherParticles);

            _lightEnabledBaselines = new bool[_decorativeLights.Length];
            for (int index = 0; index < _decorativeLights.Length; index++)
            {
                _lightEnabledBaselines[index] =
                    _decorativeLights[index] != null && _decorativeLights[index].enabled;
            }

            _lodBaselines = new LOD[_environmentLodGroups.Length][];
            for (int index = 0; index < _environmentLodGroups.Length; index++)
            {
                LODGroup lodGroup = _environmentLodGroups[index];
                _lodBaselines[index] = lodGroup != null ? lodGroup.GetLODs() : Array.Empty<LOD>();
            }

            _baselineCaptured = true;
            _hasApplied = false;
        }

        private void EnsureBaseline()
        {
            if (!_baselineCaptured)
            {
                CaptureBaseline();
            }
        }

        private static ParticleBaseline[] CaptureParticleBaselines(ParticleSystem[] particleSystems)
        {
            var baselines = new ParticleBaseline[particleSystems.Length];
            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystem particles = particleSystems[index];
                if (particles == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = particles.emission;
                baselines[index] = new ParticleBaseline(
                    emission.rateOverTimeMultiplier,
                    emission.rateOverDistanceMultiplier,
                    particles.isPlaying);
            }

            return baselines;
        }

        private static void ApplyParticleBudget(
            ParticleSystem[] particleSystems,
            ParticleBaseline[] baselines,
            float multiplier)
        {
            multiplier = Mathf.Clamp01(multiplier);
            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystem particles = particleSystems[index];
                if (particles == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTimeMultiplier = baselines[index].RateOverTime * multiplier;
                emission.rateOverDistanceMultiplier = baselines[index].RateOverDistance * multiplier;

                if (multiplier <= 0f && particles.isPlaying)
                {
                    particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                else if (multiplier > 0f &&
                         baselines[index].WasPlaying &&
                         !particles.isPlaying &&
                         particles.gameObject.activeInHierarchy)
                {
                    particles.Play(false);
                }
            }
        }

        private static void RestoreParticleBaseline(
            ParticleSystem[] particleSystems,
            ParticleBaseline[] baselines)
        {
            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystem particles = particleSystems[index];
                if (particles == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTimeMultiplier = baselines[index].RateOverTime;
                emission.rateOverDistanceMultiplier = baselines[index].RateOverDistance;
                if (baselines[index].WasPlaying &&
                    !particles.isPlaying &&
                    particles.gameObject.activeInHierarchy)
                {
                    particles.Play(false);
                }
            }
        }

        private void ApplyLightBudget(bool enabled)
        {
            for (int index = 0; index < _decorativeLights.Length; index++)
            {
                Light light = _decorativeLights[index];
                if (light != null)
                {
                    light.enabled = enabled && _lightEnabledBaselines[index];
                }
            }
        }

        private void ApplyLodBudget(float transitionMultiplier)
        {
            transitionMultiplier = Mathf.Max(1f, transitionMultiplier);
            for (int groupIndex = 0; groupIndex < _environmentLodGroups.Length; groupIndex++)
            {
                LODGroup lodGroup = _environmentLodGroups[groupIndex];
                if (lodGroup == null)
                {
                    continue;
                }

                LOD[] baseline = _lodBaselines[groupIndex];
                var adjusted = new LOD[baseline.Length];
                float previousHeight = 1.0001f;
                for (int lodIndex = 0; lodIndex < baseline.Length; lodIndex++)
                {
                    adjusted[lodIndex] = baseline[lodIndex];
                    float scaledHeight = Mathf.Clamp01(
                        baseline[lodIndex].screenRelativeTransitionHeight * transitionMultiplier);
                    adjusted[lodIndex].screenRelativeTransitionHeight =
                        Mathf.Max(0f, Mathf.Min(scaledHeight, previousHeight - 0.0001f));
                    previousHeight = adjusted[lodIndex].screenRelativeTransitionHeight;
                }

                lodGroup.SetLODs(adjusted);
            }
        }

        private readonly struct ParticleBaseline
        {
            public ParticleBaseline(float rateOverTime, float rateOverDistance, bool wasPlaying)
            {
                RateOverTime = rateOverTime;
                RateOverDistance = rateOverDistance;
                WasPlaying = wasPlaying;
            }

            public float RateOverTime { get; }
            public float RateOverDistance { get; }
            public bool WasPlaying { get; }
        }
    }
}
