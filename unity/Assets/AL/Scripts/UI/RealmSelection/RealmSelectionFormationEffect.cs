using System;
using AL.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AL.UI.RealmSelection
{
    /// <summary>
    /// Realm-specific UI material flow. Particles travel inward and resolve into
    /// the authored Arcane Axis mark instead of applying a generic card shimmer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RealmSelectionFormationEffect : MonoBehaviour
    {
        public const string RootName = "RealmFormationEffect";
        public const string GlowName = "RealmGlyphGlow";
        public const string TextureName = "RealmElementalTexture";
        public const string FlowRootName = "RealmMaterialFlow";
        public const int ParticleCount = 12;

        private readonly FlowParticle[] _particles = new FlowParticle[ParticleCount];
        private Image _glow;
        private Image _texture;
        private RealmId _realm;
        private bool _hovered;
        private float _seed;

        public RealmId Realm => _realm;
        public string MaterialNoun => MaterialNounFor(_realm);

        public void Configure(RealmId realm, Sprite emblem)
        {
            _realm = realm;
            _seed = Mathf.Abs(realm.GetHashCode() * 0.6180339f);
            RectTransform root = EnsureRoot();
            Color accent = AccentFor(realm);

            _glow = EnsureImage(
                root,
                GlowName,
                emblem,
                new Color(accent.r, accent.g, accent.b, 0.20f),
                new Vector2(146f, 146f));
            _texture = EnsureImage(
                root,
                TextureName,
                emblem,
                new Color(accent.r, accent.g, accent.b, 0.36f),
                new Vector2(102f, 102f));

            Transform flowRoot = root.Find(FlowRootName);
            if (flowRoot == null)
            {
                flowRoot = new GameObject(FlowRootName, typeof(RectTransform)).transform;
                flowRoot.SetParent(root, false);
                RectTransform flowRect = (RectTransform)flowRoot;
                flowRect.anchorMin = Vector2.zero;
                flowRect.anchorMax = Vector2.one;
                flowRect.offsetMin = Vector2.zero;
                flowRect.offsetMax = Vector2.zero;
            }

            string noun = MaterialNounFor(realm);
            for (int index = 0; index < ParticleCount; index++)
            {
                float angle = (index / (float)ParticleCount) * Mathf.PI * 2f + _seed;
                float radius = 76f + (index % 4) * 16f;
                Vector2 start = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Vector2 size = ParticleSizeFor(realm, index);
                Image image = EnsureImage(
                    flowRoot,
                    noun + "_" + (index + 1).ToString("00"),
                    null,
                    ParticleColorFor(realm, index),
                    size);
                image.rectTransform.anchoredPosition = start;
                image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ParticleRotationFor(realm, index, angle));
                _particles[index] = new FlowParticle(image, start, (index * 0.137f + _seed) % 1f);
            }

            RealmSelectionFormationHoverRelay relay = GetComponent<RealmSelectionFormationHoverRelay>();
            if (relay == null)
            {
                relay = gameObject.AddComponent<RealmSelectionFormationHoverRelay>();
            }
            relay.Bind(this);
        }

        public void SetHovered(bool hovered)
        {
            _hovered = hovered;
        }

        private void Update()
        {
            if (_realm == RealmId.None)
            {
                return;
            }

            float time = Time.unscaledTime;
            float pulse = 0.5f + Mathf.Sin(time * 2.1f + _seed) * 0.5f;
            float hoverBoost = _hovered ? 1f : 0f;
            if (_glow != null)
            {
                Color color = _glow.color;
                color.a = Mathf.Lerp(0.16f, 0.34f + hoverBoost * 0.18f, pulse);
                _glow.color = color;
                _glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1.10f + hoverBoost * 0.04f, pulse);
            }

            if (_texture != null)
            {
                Color color = _texture.color;
                color.a = Mathf.Lerp(0.30f, 0.52f + hoverBoost * 0.12f, pulse);
                _texture.color = color;
            }

            float speed = _hovered ? 0.34f : 0.22f;
            for (int index = 0; index < _particles.Length; index++)
            {
                FlowParticle particle = _particles[index];
                if (particle.Image == null)
                {
                    continue;
                }

                float progress = Mathf.Repeat(time * speed + particle.Phase, 1f);
                float eased = progress * progress * (3f - 2f * progress);
                Vector2 curl = Vector2.Perpendicular(particle.Start.normalized) * Mathf.Sin(progress * Mathf.PI) * (9f + index % 3 * 3f);
                particle.Image.rectTransform.anchoredPosition = Vector2.Lerp(particle.Start, Vector2.zero, eased) + curl;
                Color color = particle.Image.color;
                color.a = Mathf.Sin(progress * Mathf.PI) * (_hovered ? 0.92f : 0.62f);
                particle.Image.color = color;
            }
        }

        private RectTransform EnsureRoot()
        {
            Transform existing = transform.Find(RootName);
            GameObject rootObject = existing != null
                ? existing.gameObject
                : new GameObject(RootName, typeof(RectTransform));
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.SetAsFirstSibling();
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(230f, 190f);
            return rect;
        }

        private static Image EnsureImage(
            Transform parent,
            string name,
            Sprite sprite,
            Color color,
            Vector2 size)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = sprite != null;
            image.color = color;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return image;
        }

        private static string MaterialNounFor(RealmId realm) => realm switch
        {
            RealmId.Stonehold => "RockFragment",
            RealmId.Eldergrove => "Leaf",
            RealmId.Umbral => "DarkMatter",
            RealmId.Crownlands => "GoldPowder",
            _ => "RealmMatter"
        };

        private static Color AccentFor(RealmId realm) => realm switch
        {
            RealmId.Stonehold => new Color(0.92f, 0.48f, 0.20f, 1f),
            RealmId.Eldergrove => new Color(0.36f, 0.88f, 0.42f, 1f),
            RealmId.Umbral => new Color(0.68f, 0.24f, 0.86f, 1f),
            RealmId.Crownlands => new Color(1.00f, 0.78f, 0.24f, 1f),
            _ => new Color(0.82f, 0.82f, 0.78f, 1f)
        };

        private static Color ParticleColorFor(RealmId realm, int index)
        {
            Color accent = AccentFor(realm);
            Color material = realm switch
            {
                RealmId.Stonehold => new Color(0.38f, 0.30f, 0.24f, 1f),
                RealmId.Eldergrove => new Color(0.18f, 0.48f, 0.20f, 1f),
                RealmId.Umbral => new Color(0.12f, 0.04f, 0.18f, 1f),
                RealmId.Crownlands => new Color(0.92f, 0.66f, 0.16f, 1f),
                _ => accent
            };
            Color color = Color.Lerp(material, accent, 0.28f + (index % 4) * 0.12f);
            color.a = 0.6f;
            return color;
        }

        private static Vector2 ParticleSizeFor(RealmId realm, int index) => realm switch
        {
            RealmId.Stonehold => new Vector2(8f + index % 3 * 3f, 6f + index % 2 * 3f),
            RealmId.Eldergrove => new Vector2(6f, 13f + index % 3 * 2f),
            RealmId.Umbral => Vector2.one * (8f + index % 4 * 3f),
            RealmId.Crownlands => Vector2.one * (3f + index % 3 * 2f),
            _ => Vector2.one * 6f
        };

        private static float ParticleRotationFor(RealmId realm, int index, float angle)
        {
            if (realm == RealmId.Crownlands)
            {
                return index * 30f + 45f;
            }
            if (realm == RealmId.Eldergrove)
            {
                return angle * Mathf.Rad2Deg + 90f;
            }
            return index * 37f;
        }

        private readonly struct FlowParticle
        {
            public FlowParticle(Image image, Vector2 start, float phase)
            {
                Image = image;
                Start = start;
                Phase = phase;
            }

            public Image Image { get; }
            public Vector2 Start { get; }
            public float Phase { get; }
        }
    }

    [DisallowMultipleComponent]
    public sealed class RealmSelectionFormationHoverRelay : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private RealmSelectionFormationEffect _effect;

        public void Bind(RealmSelectionFormationEffect effect)
        {
            _effect = effect;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _effect?.SetHovered(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _effect?.SetHovered(false);
        }
    }
}
