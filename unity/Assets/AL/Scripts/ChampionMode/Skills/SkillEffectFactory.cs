using AL.Core;
using UnityEngine;

namespace AL.ChampionMode.Skills
{
    public static class SkillEffectFactory
    {
        private const int MaxActiveBursts = 24;
        private const int MaxPooledBurstsPerKey = 8;
        private const int MaxActiveRings = 18;
        private const int MaxPooledRingsPerKey = 8;
        private const int MaxActiveTrails = 12;
        private const int MaxPooledTrails = 6;

        public static GameObject SpawnForgeBurst(Vector3 position)
        {
            return SpawnBurst("VFX_Runtime_ForgeBurst", position, new Color(1f, 0.42f, 0.08f), new Color(0.55f, 0.50f, 0.45f), 0.8f);
        }

        public static GameObject SpawnHealingBloom(Vector3 position)
        {
            return SpawnBurst("VFX_Runtime_HealingBloom", position, new Color(0.35f, 1f, 0.45f), new Color(0.95f, 0.86f, 0.35f), 1.1f);
        }

        public static GameObject SpawnCurseMark(Vector3 position)
        {
            return SpawnBurst("VFX_Runtime_CurseMark", position, new Color(0.42f, 0f, 0.65f), new Color(0.9f, 0.02f, 0.08f), 0.9f);
        }

        public static GameObject SpawnRoyalStrike(Vector3 position)
        {
            var burst = SpawnBurst("VFX_Runtime_RoyalStrike", position, new Color(0.2f, 0.42f, 1f), new Color(1f, 0.78f, 0.18f), 0.85f);
            SpawnGroundRing("VFX_Runtime_RoyalCrest", position, new Color(1f, 0.78f, 0.18f, 0.45f), 1.2f, 0.65f);
            return burst;
        }

        public static GameObject SpawnRealmImpact(Vector3 position, RealmId realmId)
        {
            return realmId switch
            {
                RealmId.Stonehold => SpawnForgeBurst(position),
                RealmId.Eldergrove => SpawnHealingBloom(position),
                RealmId.Crownlands => SpawnRoyalStrike(position),
                RealmId.Umbral => SpawnCurseMark(position),
                _ => SpawnForgeBurst(position)
            };
        }

        public static GameObject SpawnDodgeTrail(Vector3 position, Vector3 forward, RealmId realmId)
        {
            Color color = realmId switch
            {
                RealmId.Stonehold => new Color(0.95f, 0.62f, 0.22f, 0.42f),
                RealmId.Eldergrove => new Color(0.35f, 1f, 0.55f, 0.42f),
                RealmId.Crownlands => new Color(0.25f, 0.52f, 1f, 0.42f),
                RealmId.Umbral => new Color(0.75f, 0.08f, 0.95f, 0.42f),
                _ => new Color(0.8f, 0.9f, 1f, 0.35f)
            };

            string key = "trail:realm-dodge";
            if (!RuntimeVfxPool.TryGet(key, MaxActiveTrails, CreateTrailObject, out var trail))
            {
                return null;
            }

            trail.name = "VFX_Runtime_DodgeTrail";
            trail.transform.position = position - forward.normalized * 0.75f;
            trail.transform.rotation = Quaternion.LookRotation(forward.sqrMagnitude > 0.01f ? forward : Vector3.forward);
            trail.transform.localScale = new Vector3(0.35f, 0.08f, 1.5f);
            trail.GetComponent<Renderer>().material.color = color;
            RuntimeVfxPool.ReleaseAfter(key, trail, 0.35f, MaxPooledTrails);
            return trail;
        }

        public static GameObject SpawnBossTelegraph(Vector3 position, float radius, float lifetime)
        {
            return SpawnGroundRing("VFX_Runtime_BossTelegraph", position, new Color(1f, 0.08f, 0.02f, 0.45f), radius, lifetime);
        }

        private static GameObject SpawnBurst(string name, Vector3 position, Color startColor, Color endColor, float size)
        {
            string key = "burst:" + name;
            if (!RuntimeVfxPool.TryGet(key, MaxActiveBursts, () => CreateBurstObject(name), out var effect))
            {
                return null;
            }

            effect.name = name;
            effect.transform.position = position;

            var particles = effect.GetComponent<ParticleSystem>();
            var main = particles.main;
            main.duration = 0.75f;
            main.loop = false;
            main.startLifetime = 0.55f;
            main.startSpeed = 3.2f;
            main.startSize = size;
            main.startColor = new ParticleSystem.MinMaxGradient(startColor, endColor);
            main.maxParticles = 80;

            var emission = particles.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 38) });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            particles.Clear(true);
            particles.Play(true);
            RuntimeVfxPool.ReleaseAfter(key, effect, 1.25f, MaxPooledBurstsPerKey);
            return effect;
        }

        private static GameObject SpawnGroundRing(string name, Vector3 position, Color color, float radius, float lifetime)
        {
            string key = "ring:" + name;
            if (!RuntimeVfxPool.TryGet(key, MaxActiveRings, CreateGroundRingObject, out var ring))
            {
                return null;
            }

            ring.name = name;
            ring.transform.position = position + Vector3.up * 0.03f;
            ring.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            ring.GetComponent<Renderer>().material.color = color;
            RuntimeVfxPool.ReleaseAfter(key, ring, lifetime, MaxPooledRingsPerKey);
            return ring;
        }

        private static GameObject CreateBurstObject(string name)
        {
            var effect = new GameObject(name);
            effect.AddComponent<ParticleSystem>();
            return effect;
        }

        private static GameObject CreateGroundRingObject()
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var collider = ring.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            return ring;
        }

        private static GameObject CreateTrailObject()
        {
            var trail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var collider = trail.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            return trail;
        }
    }
}
