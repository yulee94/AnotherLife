using AL.Core;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace AL.RealmWar.Warzone
{
    public class RuntimeWeatherController : MonoBehaviour
    {
        [SerializeField] private WeatherProfileData _profile = WeatherProfileData.CreateDefault();
        [SerializeField] private bool _applyRenderSettings = true;
        [SerializeField] private bool _enableLightningPulses = true;
        [SerializeField] private Light _directionalLight;

        private ParticleSystem _particles;
        private ParticleSystem _groundMistParticles;
        private ParticleSystem _horizonHazeParticles;
        private ParticleSystem _windStreakParticles;
        private WindZone _windZone;
        private Light _lightningLight;
        private Light _atmosphereLight;
        private Coroutine _lightningRoutine;
        private Coroutine _combatFlashRoutine;
        private float _combatSurgeTimer;
        private float _combatSurgeDuration = 0.6f;
        private float _combatSurgeStrength;
        private float _nextLightningTime;
        private float _baseDirectionalIntensity;
        private float _baseAtmosphereLightIntensity;
        private readonly Renderer[] _horizonVeilRenderers = new Renderer[6];
        private readonly Material[] _horizonVeilMaterials = new Material[6];
        private readonly Vector3[] _horizonVeilBaseScales = new Vector3[6];
        private readonly Color[] _horizonVeilBaseColors = new Color[6];
        private readonly Renderer[] _lightShaftRenderers = new Renderer[3];
        private readonly Material[] _lightShaftMaterials = new Material[3];
        private readonly Vector3[] _lightShaftBaseScales = new Vector3[3];
        private readonly Color[] _lightShaftBaseColors = new Color[3];

        public WeatherProfileData CurrentProfile => _profile;

        private void Awake()
        {
            ApplyProfile();
        }

        private void Update()
        {
            TickCombatSurge();
            AnimateWind();
            AnimateAtmosphere();
            TickLightning();
        }

        private void OnDestroy()
        {
            DestroyMaterials(_horizonVeilMaterials);
            DestroyMaterials(_lightShaftMaterials);
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

        public void TriggerCombatFlash(Color color, float intensity, float duration)
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            if (_combatFlashRoutine != null)
            {
                StopCoroutine(_combatFlashRoutine);
            }

            _combatFlashRoutine = StartCoroutine(CombatFlashRoutine(color, intensity, duration));
            TriggerAtmosphereSurge(color, intensity);
        }

        private void ApplyProfile()
        {
            _profile ??= WeatherProfileData.CreateDefault();
            ConfigureParticleSystem(GetOrCreateParticleSystem());
            ConfigureGroundMist(GetOrCreateChildParticleSystem("Weather_GroundMist", ref _groundMistParticles));
            ConfigureHorizonHaze(GetOrCreateChildParticleSystem("Weather_HorizonHaze", ref _horizonHazeParticles));
            ConfigureWindStreaks(GetOrCreateChildParticleSystem("Weather_ForegroundWindStreaks", ref _windStreakParticles));
            ConfigureHorizonVeils();
            ConfigureLightShafts();
            ConfigureAtmosphereLight();
            ConfigureWindZone(GetOrCreateWindZone());
            ApplyLightingProfile();
            ScheduleNextLightning();
        }

        private ParticleSystem GetOrCreateParticleSystem()
        {
            _particles = GetComponent<ParticleSystem>() ?? gameObject.AddComponent<ParticleSystem>();
            return _particles;
        }

        private ParticleSystem GetOrCreateChildParticleSystem(string name, ref ParticleSystem cachedParticles)
        {
            if (cachedParticles != null)
            {
                return cachedParticles;
            }

            var existing = transform.Find(name);
            var particleObject = existing != null ? existing.gameObject : new GameObject(name);
            particleObject.transform.SetParent(transform, false);
            cachedParticles = particleObject.GetComponent<ParticleSystem>() ?? particleObject.AddComponent<ParticleSystem>();
            return cachedParticles;
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

        private void ConfigureGroundMist(ParticleSystem particles)
        {
            particles.transform.localPosition = new Vector3(0f, -5.15f, 0f);

            Color mistStart = Color.Lerp(_profile.FogColor, _profile.ParticleStartColor, 0.35f);
            mistStart.a = Mathf.Clamp01(_profile.ParticleStartColor.a * 0.28f);
            Color mistEnd = _profile.FogColor;
            mistEnd.a = Mathf.Clamp01(mistStart.a * 0.35f);

            var main = particles.main;
            main.loop = true;
            main.startLifetime = Mathf.Max(3.0f, _profile.ParticleLifetime * 0.72f);
            main.startSpeed = Mathf.Max(0.05f, _profile.FallSpeed * 0.18f);
            main.startSize = Mathf.Max(1.4f, _profile.ParticleSize * 16f);
            main.startColor = new ParticleSystem.MinMaxGradient(mistStart, mistEnd);
            int maxMistParticles = Mathf.Clamp(_profile.MaxParticles / 3, 16, 90);
            main.maxParticles = maxMistParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = Mathf.Max(3f, maxMistParticles * 0.09f);

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(Mathf.Max(8f, _profile.Radius * 0.95f), 1.1f, Mathf.Max(8f, _profile.Radius * 0.95f));

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-_profile.HorizontalDrift * 0.35f, _profile.HorizontalDrift * 0.35f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.03f, 0.08f);
            velocity.z = new ParticleSystem.MinMaxCurve(-_profile.HorizontalDrift * 0.35f, _profile.HorizontalDrift * 0.35f);

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = Mathf.Max(0.05f, _profile.NoiseStrength * 0.75f);
            noise.frequency = Mathf.Max(0.01f, _profile.NoiseFrequency * 0.65f);

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = -3;
            }
        }

        private void ConfigureHorizonHaze(ParticleSystem particles)
        {
            particles.transform.localPosition = new Vector3(0f, -1.8f, 0f);

            Color hazeStart = Color.Lerp(_profile.FogColor, _profile.ParticleEndColor, 0.55f);
            hazeStart.a = Mathf.Clamp01(_profile.ParticleEndColor.a * 0.38f);
            Color hazeEnd = hazeStart;
            hazeEnd.a = Mathf.Clamp01(hazeStart.a * 0.22f);

            var main = particles.main;
            main.loop = true;
            main.startLifetime = Mathf.Max(4.5f, _profile.ParticleLifetime * 0.95f);
            main.startSpeed = Mathf.Max(0.04f, _profile.FallSpeed * 0.10f);
            main.startSize = Mathf.Max(2.2f, _profile.ParticleSize * 22f);
            main.startColor = new ParticleSystem.MinMaxGradient(hazeStart, hazeEnd);
            int maxHazeParticles = Mathf.Clamp(_profile.MaxParticles / 4, 12, 72);
            main.maxParticles = maxHazeParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = Mathf.Max(2f, maxHazeParticles * 0.07f);

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(Mathf.Max(10f, _profile.Radius * 1.15f), 3.2f, Mathf.Max(10f, _profile.Radius * 1.15f));

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-_profile.HorizontalDrift * 0.18f, _profile.HorizontalDrift * 0.18f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.02f, 0.05f);
            velocity.z = new ParticleSystem.MinMaxCurve(-_profile.HorizontalDrift * 0.18f, _profile.HorizontalDrift * 0.18f);

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = Mathf.Max(0.04f, _profile.NoiseStrength * 0.42f);
            noise.frequency = Mathf.Max(0.01f, _profile.NoiseFrequency * 0.45f);

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = -4;
            }
        }

        private void ConfigureWindStreaks(ParticleSystem particles)
        {
            particles.transform.localPosition = new Vector3(0f, -1.15f, -2.4f);

            Color streakStart = Color.Lerp(_profile.ParticleStartColor, _profile.DirectionalLightColor, 0.28f);
            streakStart.a = Mathf.Clamp01(Mathf.Max(_profile.ParticleStartColor.a, 0.18f) * 0.42f);
            Color streakEnd = Color.Lerp(_profile.ParticleEndColor, Color.white, 0.12f);
            streakEnd.a = Mathf.Clamp01(streakStart.a * 0.24f);

            var main = particles.main;
            main.loop = true;
            main.startLifetime = Mathf.Max(0.42f, _profile.ParticleLifetime * 0.18f);
            main.startSpeed = Mathf.Max(1.8f, _profile.FallSpeed + _profile.WindMain * 2.6f);
            main.startSize = Mathf.Max(0.025f, _profile.ParticleSize * 1.45f);
            main.startColor = new ParticleSystem.MinMaxGradient(streakStart, streakEnd);
            int maxStreakParticles = Mathf.Clamp(_profile.MaxParticles / 5, 22, 96);
            main.maxParticles = maxStreakParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = Mathf.Max(4f, maxStreakParticles * 0.18f);

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(Mathf.Max(9f, _profile.Radius * 0.72f), 5.2f, Mathf.Max(7f, _profile.Radius * 0.46f));

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            float windDirection = _profile.WindYawDegrees * Mathf.Deg2Rad;
            velocity.x = new ParticleSystem.MinMaxCurve(Mathf.Sin(windDirection) * _profile.WindMain * 1.4f - _profile.HorizontalDrift, Mathf.Sin(windDirection) * _profile.WindMain * 1.4f + _profile.HorizontalDrift);
            velocity.y = new ParticleSystem.MinMaxCurve(-Mathf.Max(0.45f, _profile.FallSpeed * 0.62f), -Mathf.Max(1.2f, _profile.FallSpeed * 1.18f));
            velocity.z = new ParticleSystem.MinMaxCurve(Mathf.Cos(windDirection) * _profile.WindMain * 1.4f - _profile.HorizontalDrift, Mathf.Cos(windDirection) * _profile.WindMain * 1.4f + _profile.HorizontalDrift);

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = Mathf.Max(0.06f, _profile.NoiseStrength * 0.78f);
            noise.frequency = Mathf.Max(0.01f, _profile.NoiseFrequency * 1.15f);

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = Mathf.Lerp(2.1f, 4.8f, Mathf.InverseLerp(0.9f, 3.0f, _profile.FallSpeed + _profile.WindMain));
                renderer.velocityScale = 0.55f;
                renderer.cameraVelocityScale = 0.05f;
                renderer.sortingOrder = -2;
            }
        }

        private void ConfigureHorizonVeils()
        {
            for (int i = 0; i < _horizonVeilRenderers.Length; i++)
            {
                Renderer renderer = GetOrCreateQuadRenderer("Weather_HorizonVeil_" + i);
                _horizonVeilRenderers[i] = renderer;

                float angle = 20f + i * (360f / _horizonVeilRenderers.Length);
                float radians = angle * Mathf.Deg2Rad;
                float radius = Mathf.Max(15f, _profile.Radius * (0.58f + (i % 2) * 0.12f));
                float width = Mathf.Max(7.5f, _profile.Radius * (0.38f + (i % 3) * 0.045f));
                float height = Mathf.Lerp(4.8f, 7.2f, (i % 4) / 3f);

                Transform veil = renderer.transform;
                veil.localPosition = new Vector3(Mathf.Sin(radians) * radius, -1.4f + (i % 3) * 0.34f, Mathf.Cos(radians) * radius);
                veil.localRotation = Quaternion.LookRotation(-new Vector3(veil.localPosition.x, 0f, veil.localPosition.z).normalized, Vector3.up) * Quaternion.Euler(i % 2 == 0 ? -4f : 5f, 0f, (i - 2) * 1.2f);
                veil.localScale = new Vector3(width, height, 1f);

                Color color = Color.Lerp(_profile.FogColor, i % 2 == 0 ? _profile.ParticleEndColor : _profile.ParticleStartColor, 0.48f);
                color = Color.Lerp(color, Color.black, 0.16f);
                color.a = Mathf.Clamp01(0.040f + _profile.FogDensity * 2.4f + _profile.ParticleEndColor.a * 0.18f + (i % 2) * 0.018f);
                _horizonVeilBaseColors[i] = color;
                _horizonVeilBaseScales[i] = veil.localScale;

                SetRendererMaterial(ref _horizonVeilMaterials[i], renderer, "Weather_HorizonVeil_Material_" + i, color);
            }
        }

        private void ConfigureLightShafts()
        {
            for (int i = 0; i < _lightShaftRenderers.Length; i++)
            {
                Renderer renderer = GetOrCreateQuadRenderer("Weather_LightShaft_" + i);
                _lightShaftRenderers[i] = renderer;

                float side = i - 1f;
                Transform shaft = renderer.transform;
                shaft.localPosition = new Vector3(side * 4.8f, -0.82f + i * 0.24f, 1.8f + i * 1.55f);
                shaft.localRotation = Quaternion.Euler(10f + i * 4f, -18f + side * 12f, 7f - side * 8f);
                shaft.localScale = new Vector3(1.05f + i * 0.20f, 7.2f + i * 0.75f, 1f);

                Color color = Color.Lerp(_profile.DirectionalLightColor, _profile.ParticleStartColor, 0.42f);
                color = Color.Lerp(color, Color.black, 0.10f);
                color.a = Mathf.Clamp01(0.026f + _profile.FogDensity * 1.35f + i * 0.006f);
                _lightShaftBaseColors[i] = color;
                _lightShaftBaseScales[i] = shaft.localScale;

                SetRendererMaterial(ref _lightShaftMaterials[i], renderer, "Weather_LightShaft_Material_" + i, color);
            }
        }

        private void ConfigureAtmosphereLight()
        {
            _atmosphereLight = GetOrCreateAtmosphereLight();
            Color lightColor = Color.Lerp(_profile.FogColor, _profile.ParticleStartColor, 0.64f);
            lightColor = Color.Lerp(lightColor, _profile.DirectionalLightColor, 0.18f);
            _baseAtmosphereLightIntensity = Mathf.Clamp(0.28f + _profile.FogDensity * 18f + _profile.ParticleStartColor.a * 0.42f, 0.36f, 1.28f);
            _atmosphereLight.color = lightColor;
            _atmosphereLight.intensity = _baseAtmosphereLightIntensity;
            _atmosphereLight.range = Mathf.Max(8f, _profile.Radius * 0.58f);
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
            ResolveDirectionalLight();
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
                _baseDirectionalIntensity = _directionalLight.intensity;
            }
        }

        private void ResolveDirectionalLight()
        {
            if (_directionalLight != null)
            {
                return;
            }

            var lights = FindObjectsOfType<Light>();
            foreach (var light in lights)
            {
                if (light != null && light.type == LightType.Directional)
                {
                    _directionalLight = light;
                    return;
                }
            }
        }

        private void AnimateWind()
        {
            if (_windZone == null || _profile == null)
            {
                return;
            }

            float pulse = Mathf.Sin(Time.time * Mathf.Max(0.01f, _profile.WindPulseFrequency)) * _profile.WindPulseAmplitude;
            float combatSurge = GetCombatSurge01();
            _windZone.windMain = Mathf.Max(0f, _profile.WindMain + pulse + combatSurge * 2.8f);
            _windZone.windTurbulence = Mathf.Max(0f, _profile.WindTurbulence + combatSurge * 1.6f);
        }

        private void AnimateAtmosphere()
        {
            if (_profile == null)
            {
                return;
            }

            float pulse = Mathf.Sin(Time.time * Mathf.Max(0.01f, _profile.WindPulseFrequency * 0.72f));
            float combatSurge = GetCombatSurge01();
            if (_groundMistParticles != null)
            {
                var emission = _groundMistParticles.emission;
                emission.rateOverTimeMultiplier = 1f + Mathf.Clamp(pulse * 0.10f, -0.08f, 0.12f) + combatSurge * 0.46f;
            }

            if (_horizonHazeParticles != null)
            {
                var emission = _horizonHazeParticles.emission;
                emission.rateOverTimeMultiplier = 1f + Mathf.Clamp(-pulse * 0.08f, -0.06f, 0.10f) + combatSurge * 0.24f;
            }

            if (_directionalLight != null && _baseDirectionalIntensity > 0f)
            {
                _directionalLight.intensity = Mathf.Max(0f, _baseDirectionalIntensity * (1f + pulse * 0.025f + combatSurge * 0.045f));
            }

            AnimateHorizonVeils(pulse, combatSurge);
            AnimateLightShafts(pulse, combatSurge);
            AnimateAtmosphereLight(pulse, combatSurge);
        }

        private void TickCombatSurge()
        {
            if (_combatSurgeTimer <= 0f)
            {
                _combatSurgeStrength = 0f;
                return;
            }

            _combatSurgeTimer -= Time.deltaTime;
            if (_combatSurgeTimer <= 0f)
            {
                _combatSurgeTimer = 0f;
                _combatSurgeStrength = 0f;
            }
        }

        private float GetCombatSurge01()
        {
            if (_combatSurgeTimer <= 0f || _combatSurgeDuration <= 0f)
            {
                return 0f;
            }

            float remaining = Mathf.Clamp01(_combatSurgeTimer / _combatSurgeDuration);
            return Mathf.Clamp01(_combatSurgeStrength) * Mathf.SmoothStep(0f, 1f, remaining);
        }

        private void TriggerAtmosphereSurge(Color color, float intensity)
        {
            float strength = Mathf.Clamp01(intensity / 4.2f);
            _combatSurgeStrength = Mathf.Max(_combatSurgeStrength, strength);
            _combatSurgeDuration = Mathf.Lerp(0.45f, 0.92f, strength);
            _combatSurgeTimer = _combatSurgeDuration;
            EmitCombatWeatherBurst(color, strength);
        }

        private void EmitCombatWeatherBurst(Color color, float strength)
        {
            if (_profile == null)
            {
                return;
            }

            Color surgeColor = Color.Lerp(_profile.ParticleStartColor, color, 0.52f);
            surgeColor.a = Mathf.Clamp01(Mathf.Max(surgeColor.a, 0.34f) * Mathf.Lerp(0.75f, 1.25f, strength));
            int mistCount = Mathf.Clamp(Mathf.RoundToInt(_profile.MaxParticles * Mathf.Lerp(0.035f, 0.13f, strength)), 6, 46);
            int streakCount = Mathf.Clamp(Mathf.RoundToInt(_profile.MaxParticles * Mathf.Lerp(0.020f, 0.08f, strength)), 4, 34);

            EmitWeatherParticles(_groundMistParticles, mistCount, surgeColor, Mathf.Max(0.9f, _profile.ParticleSize * 13f), Mathf.Lerp(0.85f, 1.45f, strength), 2.2f + strength * 2.4f, true);
            EmitWeatherParticles(_particles, streakCount, Color.Lerp(surgeColor, Color.white, 0.16f), Mathf.Max(0.04f, _profile.ParticleSize * 1.35f), Mathf.Lerp(0.72f, 1.20f, strength), 3.0f + strength * 3.0f, false);
        }

        private void EmitWeatherParticles(ParticleSystem particles, int count, Color color, float size, float lifetime, float speed, bool groundMist)
        {
            if (particles == null || count <= 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Vector2 disk = Random.insideUnitCircle * Mathf.Max(1f, _profile.Radius * (groundMist ? 0.24f : 0.36f));
                Vector3 velocity = new Vector3(
                    Random.Range(-1f, 1f),
                    groundMist ? Random.Range(0.02f, 0.22f) : Random.Range(-0.28f, -0.08f),
                    Random.Range(-1f, 1f)).normalized * speed;
                var emitParams = new ParticleSystem.EmitParams
                {
                    position = new Vector3(disk.x, groundMist ? 0f : Random.Range(-2.0f, 2.4f), disk.y),
                    velocity = velocity,
                    startColor = color,
                    startSize = size * Random.Range(0.72f, 1.18f),
                    startLifetime = lifetime * Random.Range(0.72f, 1.12f)
                };
                particles.Emit(emitParams, 1);
            }
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

        private IEnumerator CombatFlashRoutine(Color color, float intensity, float duration)
        {
            var flash = GetOrCreateLightningLight();
            float safeDuration = Mathf.Clamp(duration, 0.025f, 0.16f);
            float safeIntensity = Mathf.Clamp(intensity, 0.2f, 4.2f);
            Color previousColor = flash.color;

            flash.color = color;
            flash.intensity = safeIntensity;
            flash.enabled = true;

            if (_directionalLight != null)
            {
                _directionalLight.intensity = Mathf.Max(_directionalLight.intensity, _baseDirectionalIntensity + safeIntensity * 0.12f);
            }

            yield return new WaitForSeconds(safeDuration);

            if (flash != null && _lightningRoutine == null)
            {
                flash.intensity = 0f;
                flash.color = previousColor;
                flash.enabled = false;
            }

            _combatFlashRoutine = null;
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

        private Light GetOrCreateAtmosphereLight()
        {
            if (_atmosphereLight != null)
            {
                return _atmosphereLight;
            }

            var existing = transform.Find("Weather_RealmAtmosphereLight");
            var lightObject = existing != null ? existing.gameObject : new GameObject("Weather_RealmAtmosphereLight");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = new Vector3(0f, -1.35f, 3.8f);

            _atmosphereLight = lightObject.GetComponent<Light>() ?? lightObject.AddComponent<Light>();
            _atmosphereLight.type = LightType.Point;
            _atmosphereLight.shadows = LightShadows.None;
            return _atmosphereLight;
        }

        private Renderer GetOrCreateQuadRenderer(string name)
        {
            var existing = transform.Find(name);
            if (existing != null && existing.TryGetComponent(out Renderer existingRenderer))
            {
                return existingRenderer;
            }

            var quadObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadObject.name = name;
            quadObject.transform.SetParent(transform, false);
            var collider = quadObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = quadObject.GetComponent<Renderer>();
            renderer.receiveShadows = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.sortingOrder = -5;
            return renderer;
        }

        private static void SetRendererMaterial(ref Material material, Renderer renderer, string name, Color color)
        {
            if (material == null)
            {
                material = CreateTransparentWeatherMaterial(name, color);
                renderer.sharedMaterial = material;
                return;
            }

            ApplyMaterialColor(material, color);
            renderer.sharedMaterial = material;
        }

        private static Material CreateTransparentWeatherMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Standard");
            var material = new Material(shader)
            {
                name = name,
                color = color,
                renderQueue = (int)RenderQueue.Transparent
            };

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", 0);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", (int)CullMode.Off);
            }

            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            ApplyMaterialColor(material, color);
            return material;
        }

        private static void ApplyMaterialColor(Material material, Color color)
        {
            material.color = color;
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_TintColor"))
            {
                material.SetColor("_TintColor", color);
            }
        }

        private void AnimateHorizonVeils(float pulse, float combatSurge)
        {
            for (int i = 0; i < _horizonVeilRenderers.Length; i++)
            {
                Renderer renderer = _horizonVeilRenderers[i];
                Material material = _horizonVeilMaterials[i];
                if (renderer == null || material == null)
                {
                    continue;
                }

                float localPulse = Mathf.Sin(Time.time * (0.18f + i * 0.035f) + i * 1.37f);
                Color color = _horizonVeilBaseColors[i];
                color.a = Mathf.Clamp01(color.a * (0.82f + localPulse * 0.16f + pulse * 0.04f + combatSurge * 1.25f));
                ApplyMaterialColor(material, color);

                Transform veil = renderer.transform;
                Vector3 baseScale = _horizonVeilBaseScales[i];
                veil.localScale = new Vector3(baseScale.x * (1f + localPulse * 0.018f + combatSurge * 0.035f), baseScale.y * (1f + combatSurge * 0.055f), baseScale.z);
            }
        }

        private void AnimateLightShafts(float pulse, float combatSurge)
        {
            for (int i = 0; i < _lightShaftRenderers.Length; i++)
            {
                Renderer renderer = _lightShaftRenderers[i];
                Material material = _lightShaftMaterials[i];
                if (renderer == null || material == null)
                {
                    continue;
                }

                float localPulse = Mathf.Sin(Time.time * (0.28f + i * 0.05f) + i * 0.91f);
                Color color = _lightShaftBaseColors[i];
                color.a = Mathf.Clamp01(color.a * (0.72f + localPulse * 0.18f + pulse * 0.08f + combatSurge * 1.55f));
                ApplyMaterialColor(material, color);

                Transform shaft = renderer.transform;
                Vector3 baseScale = _lightShaftBaseScales[i];
                shaft.localScale = new Vector3(baseScale.x * (1f + combatSurge * 0.08f), baseScale.y * (1f + localPulse * 0.014f + combatSurge * 0.06f), baseScale.z);
            }
        }

        private void AnimateAtmosphereLight(float pulse, float combatSurge)
        {
            if (_atmosphereLight == null)
            {
                return;
            }

            _atmosphereLight.intensity = Mathf.Max(0f, _baseAtmosphereLightIntensity * (1f + pulse * 0.08f + combatSurge * 0.72f));
        }

        private static void DestroyMaterials(Material[] materials)
        {
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null)
                {
                    continue;
                }

                UnityEngine.Object.Destroy(materials[i]);
                materials[i] = null;
            }
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
