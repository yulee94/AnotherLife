using AL.Core;
using System.Collections;
using UnityEngine;

namespace AL.RealmWar.Warzone
{
    public class RuntimeWeatherController : MonoBehaviour
    {
        [SerializeField] private WeatherProfileData _profile = WeatherProfileData.CreateDefault();
        [SerializeField] private bool _applyRenderSettings = true;
        [SerializeField] private bool _enableLightningPulses = true;
        [SerializeField] private Light _directionalLight;

        private ParticleSystem _particles;
        private WindZone _windZone;
        private Light _lightningLight;
        private Coroutine _lightningRoutine;
        private float _nextLightningTime;

        public WeatherProfileData CurrentProfile => _profile;

        private void Awake()
        {
            ApplyProfile();
        }

        private void Update()
        {
            AnimateWind();
            TickLightning();
        }

        public void Configure(Color particleColor, int maxParticles, float radius, float fallSpeed)
        {
            _profile = WeatherProfileData.CreateDefault();
            _profile.Id = "custom_runtime_weather";
            _profile.DisplayName = "Custom Runtime Weather";
            _profile.ParticleStartColor = particleColor;
            _profile.ParticleEndColor = new Color(particleColor.r, particleColor.g, particleColor.b, Mathf.Clamp01(particleColor.a * 0.45f));
            _profile.MaxParticles = maxParticles;
            _profile.Radius = radius;
            _profile.FallSpeed = fallSpeed;

            ApplyProfile();
        }

        public void Configure(WeatherProfileData profile)
        {
            _profile = profile ?? WeatherProfileData.CreateDefault();
            ApplyProfile();
        }

        public void ConfigureForRealm(RealmId realmId)
        {
            Configure(WeatherProfileData.CreateForRealm(realmId));
        }

        public void ApplyParticleBudgetMultiplier(float multiplier)
        {
            _profile ??= WeatherProfileData.CreateDefault();
            float safeMultiplier = Mathf.Clamp(multiplier, 0.15f, 1.25f);
            _profile.MaxParticles = Mathf.Max(8, Mathf.RoundToInt(_profile.MaxParticles * safeMultiplier));
            _profile.EmissionRateMultiplier = Mathf.Max(0.02f, _profile.EmissionRateMultiplier * safeMultiplier);
            ApplyProfile();
        }

        private void ApplyProfile()
        {
            _profile ??= WeatherProfileData.CreateDefault();
            ConfigureParticleSystem(GetOrCreateParticleSystem());
            ConfigureWindZone(GetOrCreateWindZone());
            ApplyLightingProfile();
            ScheduleNextLightning();
        }

        private ParticleSystem GetOrCreateParticleSystem()
        {
            _particles = GetComponent<ParticleSystem>() ?? gameObject.AddComponent<ParticleSystem>();
            return _particles;
        }

        private WindZone GetOrCreateWindZone()
        {
            if (_windZone != null)
            {
                return _windZone;
            }

            _windZone = GetComponentInChildren<WindZone>();
            if (_windZone != null)
            {
                return _windZone;
            }

            var windObject = new GameObject("Weather_WindZone");
            windObject.transform.SetParent(transform, false);
            _windZone = windObject.AddComponent<WindZone>();
            return _windZone;
        }

        private void ConfigureParticleSystem(ParticleSystem particles)
        {
            var main = particles.main;
            main.loop = true;
            main.startLifetime = Mathf.Max(0.5f, _profile.ParticleLifetime);
            main.startSpeed = Mathf.Max(0.1f, _profile.FallSpeed);
            main.startSize = Mathf.Max(0.01f, _profile.ParticleSize);
            main.startColor = new ParticleSystem.MinMaxGradient(_profile.ParticleStartColor, _profile.ParticleEndColor);
            main.maxParticles = Mathf.Max(8, _profile.MaxParticles);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = Mathf.Max(4f, _profile.MaxParticles * Mathf.Max(0.02f, _profile.EmissionRateMultiplier));

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(Mathf.Max(1f, _profile.Radius), 4f, Mathf.Max(1f, _profile.Radius));

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-_profile.HorizontalDrift, _profile.HorizontalDrift);
            velocity.y = -Mathf.Abs(_profile.FallSpeed);

            var noise = particles.noise;
            noise.enabled = _profile.NoiseStrength > 0f;
            noise.strength = Mathf.Max(0f, _profile.NoiseStrength);
            noise.frequency = Mathf.Max(0.01f, _profile.NoiseFrequency);

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = -1;
            }
        }

        private void ConfigureWindZone(WindZone windZone)
        {
            windZone.mode = WindZoneMode.Directional;
            windZone.windMain = Mathf.Max(0f, _profile.WindMain);
            windZone.windTurbulence = Mathf.Max(0f, _profile.WindTurbulence);
            windZone.transform.localRotation = Quaternion.Euler(0f, _profile.WindYawDegrees, 0f);
        }

        private void ApplyLightingProfile()
        {
            if (_applyRenderSettings)
            {
                RenderSettings.fog = _profile.ApplyFog;
                if (_profile.ApplyFog)
                {
                    RenderSettings.fogMode = FogMode.ExponentialSquared;
                    RenderSettings.fogColor = _profile.FogColor;
                    RenderSettings.fogDensity = Mathf.Max(0f, _profile.FogDensity);
                }

                RenderSettings.ambientLight = _profile.AmbientColor;
            }

            if (_directionalLight != null)
            {
                _directionalLight.color = _profile.DirectionalLightColor;
                _directionalLight.intensity = Mathf.Max(0f, _profile.DirectionalLightIntensity);
            }
        }

        private void AnimateWind()
        {
            if (_windZone == null || _profile == null)
            {
                return;
            }

            float pulse = Mathf.Sin(Time.time * Mathf.Max(0.01f, _profile.WindPulseFrequency)) * _profile.WindPulseAmplitude;
            _windZone.windMain = Mathf.Max(0f, _profile.WindMain + pulse);
        }

        private void TickLightning()
        {
            if (!_enableLightningPulses || _profile == null || !_profile.EnableLightning || Time.time < _nextLightningTime)
            {
                return;
            }

            if (_lightningRoutine == null)
            {
                _lightningRoutine = StartCoroutine(FlashLightning());
            }
        }

        private IEnumerator FlashLightning()
        {
            var flash = GetOrCreateLightningLight();
            flash.color = _profile.LightningColor;
            flash.intensity = Mathf.Max(0f, _profile.LightningFlashIntensity);
            flash.enabled = true;

            yield return new WaitForSeconds(Mathf.Max(0.02f, _profile.LightningDuration));

            if (flash != null)
            {
                flash.intensity = 0f;
                flash.enabled = false;
            }

            _lightningRoutine = null;
            ScheduleNextLightning();
        }

        private Light GetOrCreateLightningLight()
        {
            if (_lightningLight != null)
            {
                return _lightningLight;
            }

            var existing = transform.Find("Weather_LightningFlash");
            var lightObject = existing != null ? existing.gameObject : new GameObject("Weather_LightningFlash");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(62f, -18f, 0f);

            _lightningLight = lightObject.GetComponent<Light>() ?? lightObject.AddComponent<Light>();
            _lightningLight.type = LightType.Directional;
            _lightningLight.enabled = false;
            return _lightningLight;
        }

        private void ScheduleNextLightning()
        {
            if (_profile == null || !_profile.EnableLightning)
            {
                _nextLightningTime = float.PositiveInfinity;
                return;
            }

            float minDelay = Mathf.Max(0.1f, _profile.LightningMinDelay);
            float maxDelay = Mathf.Max(minDelay, _profile.LightningMaxDelay);
            _nextLightningTime = Time.time + Random.Range(minDelay, maxDelay);
        }
    }
}
