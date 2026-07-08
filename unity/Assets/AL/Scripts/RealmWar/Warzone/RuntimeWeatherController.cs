using AL.Core;
using UnityEngine;

namespace AL.RealmWar.Warzone
{
    public class RuntimeWeatherController : MonoBehaviour
    {
        [SerializeField] private Color _particleColor = new Color(0.45f, 0.42f, 0.38f, 0.35f);
        [SerializeField] private int _maxParticles = 140;
        [SerializeField] private float _radius = 24f;
        [SerializeField] private float _fallSpeed = 1.1f;

        private void Awake()
        {
            if (GetComponent<ParticleSystem>() == null)
            {
                BuildParticleWeather();
            }
        }

        public void Configure(Color particleColor, int maxParticles, float radius, float fallSpeed)
        {
            _particleColor = particleColor;
            _maxParticles = maxParticles;
            _radius = radius;
            _fallSpeed = fallSpeed;

            ConfigureParticleSystem(GetOrCreateParticleSystem());
        }

        public void ConfigureForRealm(RealmId realmId)
        {
            switch (realmId)
            {
                case RealmId.Stonehold:
                    Configure(new Color(0.82f, 0.92f, 1.0f, 0.42f), 180, 24f, 2.2f);
                    break;
                case RealmId.Eldergrove:
                    Configure(new Color(0.45f, 0.95f, 0.68f, 0.34f), 150, 22f, 1.35f);
                    break;
                case RealmId.Crownlands:
                    Configure(new Color(0.55f, 0.62f, 0.82f, 0.30f), 120, 22f, 1.55f);
                    break;
                case RealmId.Umbral:
                    Configure(new Color(0.24f, 0.18f, 0.20f, 0.52f), 220, 20f, 0.95f);
                    break;
                default:
                    Configure(new Color(0.45f, 0.42f, 0.38f, 0.35f), 120, 22f, 1.1f);
                    break;
            }
        }

        private void BuildParticleWeather()
        {
            ConfigureParticleSystem(GetOrCreateParticleSystem());
        }

        private ParticleSystem GetOrCreateParticleSystem()
        {
            return GetComponent<ParticleSystem>() ?? gameObject.AddComponent<ParticleSystem>();
        }

        private void ConfigureParticleSystem(ParticleSystem particles)
        {
            var main = particles.main;
            main.loop = true;
            main.startLifetime = 6f;
            main.startSpeed = _fallSpeed;
            main.startSize = 0.15f;
            main.startColor = _particleColor;
            main.maxParticles = _maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = Mathf.Max(4, _maxParticles / 8f);

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(_radius, 4f, _radius);

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            velocity.y = -_fallSpeed;
        }
    }
}
