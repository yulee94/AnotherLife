using UnityEngine;

namespace AL.Utilities
{
    /// <summary>
    /// Assigns a packaged, Built-In Render Pipeline compatible material to
    /// runtime-created ParticleSystems. Unity's implicit default particle
    /// material is not a stable Player-build dependency and may render with the
    /// magenta error shader after stripping.
    /// </summary>
    public static class RuntimeParticleMaterialFactory
    {
        private const string ShaderResourceName = "ALRuntimeSoftParticle";

        public static bool EnsureSoftMaterial(ParticleSystem particles, string materialName)
        {
            if (particles == null)
            {
                return false;
            }

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer == null)
            {
                return false;
            }

            Shader shader = Resources.Load<Shader>(ShaderResourceName);
            if (shader == null || !shader.isSupported)
            {
                // A missing presentation dependency must never become a field of
                // bright magenta error quads in the playable build.
                renderer.enabled = false;
                return false;
            }

            var owner = particles.GetComponent<RuntimeParticleMaterialOwner>();
            if (owner == null)
            {
                owner = particles.gameObject.AddComponent<RuntimeParticleMaterialOwner>();
            }

            owner.Ensure(renderer, shader, materialName);
            renderer.enabled = true;
            return true;
        }

        internal static Texture2D CreateSoftParticleTexture()
        {
            const int textureSize = 32;
            var texture = new Texture2D(
                textureSize,
                textureSize,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "T_RuntimeSoftParticle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            var pixels = new Color32[textureSize * textureSize];
            float halfSize = textureSize * 0.5f;
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float centeredX = (x + 0.5f - halfSize) / halfSize;
                    float centeredY = (y + 0.5f - halfSize) / halfSize;
                    float radius = Mathf.Sqrt(centeredX * centeredX + centeredY * centeredY);
                    float alpha = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(radius));
                    alpha *= alpha;
                    pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            // The bounded 32x32 CPU copy lets regression tests verify the falloff
            // and is reused for the full lifetime of its pooled particle object.
            texture.Apply(false, false);
            return texture;
        }
    }

    internal sealed class RuntimeParticleMaterialOwner : MonoBehaviour
    {
        private Material _material;
        private Texture2D _texture;

        internal void Ensure(
            ParticleSystemRenderer renderer,
            Shader shader,
            string materialName)
        {
            if (_material != null && _texture != null && _material.shader == shader)
            {
                renderer.sharedMaterial = _material;
                return;
            }

            ReleaseOwnedResources();
            _texture = RuntimeParticleMaterialFactory.CreateSoftParticleTexture();
            _material = new Material(shader)
            {
                name = string.IsNullOrWhiteSpace(materialName)
                    ? "MAT_RuntimeSoftParticle"
                    : materialName,
                hideFlags = HideFlags.DontSave,
                mainTexture = _texture
            };
            _material.SetColor("_TintColor", Color.white);
            renderer.sharedMaterial = _material;
        }

        private void OnDestroy()
        {
            ReleaseOwnedResources();
        }

        private void ReleaseOwnedResources()
        {
            DestroyOwnedObject(_material);
            DestroyOwnedObject(_texture);
            _material = null;
            _texture = null;
        }

        private static void DestroyOwnedObject(Object ownedObject)
        {
            if (ownedObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(ownedObject);
            }
            else
            {
                Object.DestroyImmediate(ownedObject);
            }
        }
    }
}
