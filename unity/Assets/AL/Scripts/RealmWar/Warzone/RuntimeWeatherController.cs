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

            var particles = GetComponent<ParticleSystem>();
            if (particles != null)
            {
                Destroy(particles);
            }

            BuildParticleWeather();
        }

        private void BuildParticleWeather()
        {
            var particles = gameObject.AddComponent<ParticleSystem>();
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

