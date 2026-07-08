using AL.Core;
using System.Collections.Generic;
using UnityEngine;
using ChampionCameraFollow = AL.ChampionMode.Camera.CameraFollow;

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
        private const int MaxActiveSkillShapes = 20;
        private const int MaxPooledSkillShapesPerKey = 8;
        private const int MaxActiveGuardShells = 8;
        private const int MaxPooledGuardShells = 4;
        private const int MaxActiveFloatingTexts = 24;

        private static int _activeFloatingTexts;

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

        public static GameObject SpawnSkillCastRing(Vector3 position, RealmId realmId, float radius, float lifetime)
        {
            Color color = GetRealmColor(realmId, 0.38f);
            return SpawnGroundRing("VFX_Runtime_SkillCastRing", position, color, Mathf.Max(0.9f, radius), Mathf.Max(0.15f, lifetime));
        }

        public static GameObject SpawnRealmSlash(Vector3 groundPosition, Vector3 forward, RealmId realmId)
        {
            Vector3 safeForward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Color color = GetRealmColor(realmId, 0.72f);
            Quaternion rotation = Quaternion.LookRotation(safeForward) * Quaternion.Euler(0f, 0f, 22f);
            var slash = SpawnPrimitiveEffect(
                "shape:realm-slash",
                "VFX_Runtime_RealmSlash",
                PrimitiveType.Cube,
                MaxActiveSkillShapes,
                MaxPooledSkillShapesPerKey,
                groundPosition + Vector3.up * 0.95f + safeForward * 0.15f,
                rotation,
                new Vector3(1.85f, 0.09f, 0.42f),
                color,
                0.32f);

            SpawnPrimitiveEffect(
                "shape:realm-slash-edge",
                "VFX_Runtime_RealmSlash_Edge",
                PrimitiveType.Cube,
                MaxActiveSkillShapes,
                MaxPooledSkillShapesPerKey,
                groundPosition + Vector3.up * 1.02f + safeForward * 0.24f,
                rotation,
                new Vector3(2.12f, 0.045f, 0.12f),
                Color.Lerp(color, Color.white, 0.55f),
                0.22f);
            SpawnGroundRing("VFX_Runtime_RealmSlash_Crest", groundPosition, GetRealmColor(realmId, 0.28f), 1.15f, 0.38f);
            return slash;
        }

        public static GameObject SpawnRenewingGuard(Vector3 groundPosition, RealmId realmId)
        {
            Color shellColor = Color.Lerp(GetRealmColor(realmId, 0.36f), new Color(0.48f, 1f, 0.62f, 0.36f), 0.45f);
            var shell = SpawnPrimitiveEffect(
                "shape:renewing-guard",
                "VFX_Runtime_RenewingGuard",
                PrimitiveType.Sphere,
                MaxActiveGuardShells,
                MaxPooledGuardShells,
                groundPosition + Vector3.up * 1.05f,
                Quaternion.identity,
                new Vector3(2.25f, 2.25f, 2.25f),
                shellColor,
                0.65f);

            SpawnGroundRing("VFX_Runtime_RenewingGuard_Ring", groundPosition, new Color(0.48f, 1f, 0.62f, 0.42f), 1.35f, 0.55f);
            SpawnHealingBloom(groundPosition + Vector3.up * 0.9f);
            return shell;
        }

        public static GameObject SpawnWarzoneShockwave(Vector3 groundPosition, RealmId realmId, float radius)
        {
            float safeRadius = Mathf.Max(1.25f, radius);
            Color color = GetRealmColor(realmId, 0.42f);
            SpawnBurst("VFX_Runtime_WarzoneBurst_Core", groundPosition + Vector3.up * 0.85f, Color.Lerp(color, Color.white, 0.25f), color, 1.25f);
            SpawnGroundRing("VFX_Runtime_WarzoneBurst_Inner", groundPosition, color, safeRadius * 0.45f, 0.42f);
            SpawnGroundRing("VFX_Runtime_WarzoneBurst_Outer", groundPosition, GetRealmColor(realmId, 0.20f), safeRadius * 1.18f, 0.68f);
            return SpawnGroundRing("VFX_Runtime_WarzoneBurst_Wave", groundPosition, color, safeRadius, 0.58f);
        }

        public static GameObject SpawnWarmasterBreaker(Vector3 groundPosition, RealmId realmId, float radius)
        {
            float safeRadius = Mathf.Max(1.6f, radius);
            Color coreColor = Color.Lerp(GetRealmColor(realmId, 0.62f), new Color(1f, 0.62f, 0.18f, 0.62f), 0.35f);
            var impact = SpawnWarzoneShockwave(groundPosition, realmId, safeRadius);
            SpawnBurst("VFX_Runtime_WarmasterBreaker_Core", groundPosition + Vector3.up, coreColor, new Color(1f, 0.92f, 0.62f, 0.7f), 1.65f);

            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f + 45f;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (safeRadius * 0.48f);
                SpawnPrimitiveEffect(
                    "shape:warmaster-breaker-pillar",
                    "VFX_Runtime_WarmasterBreaker_Pillar",
                    PrimitiveType.Cylinder,
                    MaxActiveSkillShapes,
                    MaxPooledSkillShapesPerKey,
                    groundPosition + offset + Vector3.up * 0.45f,
                    Quaternion.Euler(0f, angle, 8f),
                    new Vector3(0.22f, 0.9f, 0.22f),
                    coreColor,
                    0.7f);
            }

            return impact;
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
            SetRendererColor(trail.GetComponent<Renderer>(), color);
            RuntimeVfxPool.ReleaseAfter(key, trail, 0.35f, MaxPooledTrails);
            return trail;
        }

        public static GameObject SpawnBossTelegraph(Vector3 position, float radius, float lifetime)
        {
            return SpawnGroundRing("VFX_Runtime_BossTelegraph", position, new Color(1f, 0.08f, 0.02f, 0.45f), radius, lifetime);
        }

        public static GameObject SpawnBossSlamTelegraph(Vector3 impactCenter, Vector3 sourcePosition, float radius, float lifetime, bool isEnraged)
        {
            float safeRadius = Mathf.Max(1.4f, radius);
            Vector3 center = Grounded(impactCenter);
            Vector3 source = Grounded(sourcePosition);
            Vector3 toCenter = center - source;
            toCenter.y = 0f;
            Vector3 direction = toCenter.sqrMagnitude > 0.01f ? toCenter.normalized : Vector3.forward;
            float distance = Mathf.Max(1f, toCenter.magnitude);
            Color dangerColor = isEnraged
                ? new Color(1f, 0.16f, 0.04f, 0.52f)
                : new Color(1f, 0.08f, 0.02f, 0.44f);
            Color edgeColor = Color.Lerp(dangerColor, new Color(1f, 0.82f, 0.36f, dangerColor.a), 0.42f);

            var field = SpawnGroundRing("VFX_Runtime_BossSlam_Field", center, dangerColor, safeRadius, lifetime);
            SpawnGroundRing("VFX_Runtime_BossSlam_Edge", center, edgeColor, safeRadius * 1.03f, lifetime * 0.92f);
            SpawnGroundRing("VFX_Runtime_BossSlam_Core", center, new Color(1f, 0.48f, 0.16f, 0.32f), safeRadius * 0.28f, lifetime * 0.72f);

            SpawnPrimitiveEffect(
                "shape:boss-slam-direction-line",
                "VFX_Runtime_BossSlam_Direction",
                PrimitiveType.Cube,
                MaxActiveSkillShapes,
                MaxPooledSkillShapesPerKey,
                source + direction * (distance * 0.5f) + Vector3.up * 0.06f,
                Quaternion.LookRotation(direction),
                new Vector3(0.10f, 0.035f, distance),
                new Color(1f, 0.22f, 0.08f, 0.34f),
                lifetime * 0.82f);

            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f;
                Vector3 radial = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                SpawnPrimitiveEffect(
                    "shape:boss-slam-edge-tick",
                    "VFX_Runtime_BossSlam_EdgeTick",
                    PrimitiveType.Cube,
                    MaxActiveSkillShapes,
                    MaxPooledSkillShapesPerKey,
                    center + radial * safeRadius + Vector3.up * 0.08f,
                    Quaternion.LookRotation(radial),
                    new Vector3(0.12f, 0.04f, 0.62f),
                    edgeColor,
                    lifetime);
            }

            return field;
        }

        public static GameObject SpawnBossSlamImpact(Vector3 impactCenter, float radius, RealmId realmId)
        {
            float safeRadius = Mathf.Max(1.4f, radius);
            Vector3 center = Grounded(impactCenter);
            Color hotColor = new Color(1f, 0.22f, 0.08f, 0.72f);
            SpawnBurst("VFX_Runtime_BossSlam_ImpactCore", center + Vector3.up * 0.75f, hotColor, new Color(0.42f, 0.08f, 0.04f, 0.62f), 1.35f);
            SpawnGroundRing("VFX_Runtime_BossSlam_ImpactRing", center, hotColor, safeRadius * 0.72f, 0.44f);
            return SpawnWarzoneShockwave(center, realmId, safeRadius);
        }

        public static void ShakeCamera(float strength, float duration)
        {
            var cameraFollow = Object.FindObjectOfType<ChampionCameraFollow>();
            cameraFollow?.AddShake(strength, duration);
        }

        public static GameObject SpawnFloatingCombatText(Vector3 position, string text, Color color, float size = 0.24f, float lifetime = 0.95f)
        {
            if (string.IsNullOrWhiteSpace(text) || _activeFloatingTexts >= MaxActiveFloatingTexts)
            {
                return null;
            }

            var textObject = new GameObject("VFX_Runtime_FloatingCombatText");
            textObject.transform.position = position;
            var mesh = textObject.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 64;
            mesh.characterSize = Mathf.Max(0.05f, size);
            mesh.color = color;

            var feedback = textObject.AddComponent<FloatingCombatText>();
            feedback.Configure(color, Mathf.Max(0.15f, lifetime));
            _activeFloatingTexts++;
            return textObject;
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
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 38) });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.45f;
            noise.frequency = 0.35f;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.18f, 1.0f),
                new Keyframe(1f, 0.08f)));

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
            Vector3 endScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            Vector3 startScale = new Vector3(radius * 1.25f, 0.02f, radius * 1.25f);
            ring.transform.localScale = startScale;
            SetRendererColor(ring.GetComponent<Renderer>(), color);
            AnimatePrimitive(ring, color, lifetime, startScale, endScale);
            RuntimeVfxPool.ReleaseAfter(key, ring, lifetime, MaxPooledRingsPerKey);
            return ring;
        }

        private static GameObject SpawnPrimitiveEffect(
            string key,
            string name,
            PrimitiveType primitiveType,
            int maxActive,
            int maxPoolSize,
            Vector3 position,
            Quaternion rotation,
            Vector3 localScale,
            Color color,
            float lifetime)
        {
            if (!RuntimeVfxPool.TryGet(key, maxActive, () => CreatePrimitiveEffectObject(primitiveType), out var effect))
            {
                return null;
            }

            effect.name = name;
            effect.transform.position = position;
            effect.transform.rotation = rotation;
            effect.transform.localScale = localScale;
            SetRendererColor(effect.GetComponent<Renderer>(), color);
            AnimatePrimitive(effect, color, lifetime, localScale * 0.88f, localScale * 1.08f);
            RuntimeVfxPool.ReleaseAfter(key, effect, lifetime, maxPoolSize);
            return effect;
        }

        private static void AnimatePrimitive(GameObject effect, Color color, float lifetime, Vector3 startScale, Vector3 endScale)
        {
            if (effect == null)
            {
                return;
            }

            var animator = effect.GetComponent<PrimitiveVfxAnimator>() ?? effect.AddComponent<PrimitiveVfxAnimator>();
            animator.Configure(color, Mathf.Max(0.05f, lifetime), startScale, endScale);
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

        private static GameObject CreatePrimitiveEffectObject(PrimitiveType primitiveType)
        {
            var effect = GameObject.CreatePrimitive(primitiveType);
            var collider = effect.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            return effect;
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

        private static Color GetRealmColor(RealmId realmId, float alpha)
        {
            Color color = realmId switch
            {
                RealmId.Stonehold => new Color(0.95f, 0.48f, 0.16f, alpha),
                RealmId.Eldergrove => new Color(0.32f, 0.95f, 0.54f, alpha),
                RealmId.Crownlands => new Color(0.28f, 0.52f, 1f, alpha),
                RealmId.Umbral => new Color(0.72f, 0.12f, 0.94f, alpha),
                _ => new Color(0.85f, 0.92f, 1f, alpha)
            };
            color.a = alpha;
            return color;
        }

        private static Vector3 Grounded(Vector3 position)
        {
            return new Vector3(position.x, 0.05f, position.z);
        }

        private static void SetRendererColor(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            var material = renderer.material;
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (color.a < 0.99f)
            {
                ConfigureTransparentMaterial(material);
            }
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static void ReleaseFloatingCombatText()
        {
            _activeFloatingTexts = Mathf.Max(0, _activeFloatingTexts - 1);
        }

        private sealed class PrimitiveVfxAnimator : MonoBehaviour
        {
            private Color _baseColor;
            private Renderer _renderer;
            private Vector3 _startScale;
            private Vector3 _endScale;
            private float _elapsed;
            private float _lifetime;

            public void Configure(Color baseColor, float lifetime, Vector3 startScale, Vector3 endScale)
            {
                _baseColor = baseColor;
                _lifetime = lifetime;
                _startScale = startScale;
                _endScale = endScale;
                _elapsed = 0f;
                _renderer = GetComponent<Renderer>();
                transform.localScale = _startScale;
                enabled = true;
                SetRendererColor(_renderer, _baseColor);
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _lifetime));
                float eased = 1f - Mathf.Pow(1f - t, 2f);
                transform.localScale = Vector3.Lerp(_startScale, _endScale, eased);

                if (_renderer != null)
                {
                    Color faded = _baseColor;
                    faded.a = Mathf.Lerp(_baseColor.a, 0f, t);
                    SetRendererColor(_renderer, faded);
                }

                if (t >= 1f)
                {
                    enabled = false;
                }
            }
        }

        private sealed class FloatingCombatText : MonoBehaviour
        {
            private Color _baseColor;
            private TextMesh _text;
            private float _elapsed;
            private float _lifetime;
            private bool _released;

            public void Configure(Color baseColor, float lifetime)
            {
                _baseColor = baseColor;
                _lifetime = lifetime;
                _text = GetComponent<TextMesh>();
            }

            private void Update()
            {
                float deltaTime = Time.unscaledDeltaTime;
                _elapsed += deltaTime;
                transform.position += Vector3.up * (0.92f * deltaTime);

                var camera = UnityEngine.Camera.main;
                if (camera != null)
                {
                    transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position);
                }

                if (_text != null)
                {
                    float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _lifetime));
                    Color faded = _baseColor;
                    faded.a = Mathf.Lerp(_baseColor.a, 0f, t);
                    _text.color = faded;
                }

                if (_elapsed >= _lifetime)
                {
                    Release();
                    Destroy(gameObject);
                }
            }

            private void OnDestroy()
            {
                Release();
            }

            private void Release()
            {
                if (_released)
                {
                    return;
                }

                _released = true;
                ReleaseFloatingCombatText();
            }
        }
    }

    public static class RuntimeCombatAudio
    {
        private const int SampleRate = 22050;
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        private static AudioSource _source;

        public static void PlayBasicAttack()
        {
            PlayTone("basic_attack", 185f, 0.11f, 0.24f, 0.32f);
        }

        public static void PlayImpact()
        {
            PlayTone("impact", 92f, 0.16f, 0.30f, 0.70f);
        }

        public static void PlayDodge()
        {
            PlayTone("dodge", 320f, 0.13f, 0.18f, 0.18f);
        }

        public static void PlaySkillCast()
        {
            PlayTone("skill_cast", 420f, 0.22f, 0.22f, 0.36f);
        }

        public static void PlayHeavySkill()
        {
            PlayTone("heavy_skill", 128f, 0.28f, 0.32f, 0.74f);
        }

        public static void PlayHeal()
        {
            PlayTone("heal", 540f, 0.30f, 0.20f, 0.24f);
        }

        public static void PlayWarning()
        {
            PlayTone("warning", 260f, 0.24f, 0.22f, 0.50f);
        }

        public static void PlayBreak()
        {
            PlayTone("break", 156f, 0.34f, 0.34f, 0.82f);
        }

        public static void PlayClear()
        {
            PlayTone("clear", 660f, 0.42f, 0.28f, 0.30f);
        }

        private static void PlayTone(string key, float frequency, float duration, float volume, float brightness)
        {
            var source = GetOrCreateSource();
            if (source == null)
            {
                return;
            }

            if (!Clips.TryGetValue(key, out var clip) || clip == null)
            {
                clip = CreateToneClip(key, frequency, duration, brightness);
                Clips[key] = clip;
            }

            source.pitch = 1f + Random.Range(-0.025f, 0.025f);
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private static AudioSource GetOrCreateSource()
        {
            if (_source != null)
            {
                return _source;
            }

            var host = new GameObject("ChampionRuntimeCombatAudio");
            Object.DontDestroyOnLoad(host);
            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.priority = 80;
            return _source;
        }

        private static AudioClip CreateToneClip(string key, float frequency, float duration, float brightness)
        {
            int sampleCount = Mathf.Max(32, Mathf.CeilToInt(SampleRate * Mathf.Max(0.03f, duration)));
            var samples = new float[sampleCount];
            float safeBrightness = Mathf.Clamp01(brightness);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float normalized = i / (float)Mathf.Max(1, sampleCount - 1);
                float envelope = Mathf.Pow(1f - normalized, 2.2f);
                float sweep = Mathf.Lerp(1.18f, 0.72f, normalized);
                float fundamental = Mathf.Sin(Mathf.PI * 2f * frequency * sweep * t);
                float harmonic = Mathf.Sin(Mathf.PI * 2f * frequency * 2.01f * t) * safeBrightness * 0.35f;
                float noise = PseudoNoise(i, key.Length) * safeBrightness * 0.14f;
                samples[i] = Mathf.Clamp((fundamental + harmonic + noise) * envelope, -1f, 1f);
            }

            var clip = AudioClip.Create("AL_" + key, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float PseudoNoise(int sampleIndex, int seed)
        {
            int value = sampleIndex * 1103515245 + seed * 12345;
            value = (value >> 16) & 0x7fff;
            return value / 16384f - 1f;
        }
    }
}
