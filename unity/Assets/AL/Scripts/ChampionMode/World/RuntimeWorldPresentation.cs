using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AL.ChampionMode.World
{
    /// <summary>
    /// Deterministic, bounded visual depth for the playable arena. This layer owns no gameplay state.
    /// </summary>
    public static class RuntimeWorldPresentation
    {
        private const float TerrainSize = 92f;
        private static readonly Dictionary<int, Texture2D> Textures = new Dictionary<int, Texture2D>();

        public static void BuildArenaBackdrop(Transform parent, bool reducedQuality)
        {
            if (parent == null || parent.Find("WorldPresentation_Backdrop") != null)
            {
                return;
            }

            var root = new GameObject("WorldPresentation_Backdrop").transform;
            root.SetParent(parent, false);
            int resolution = reducedQuality ? 25 : 49;

            var terrain = new GameObject("CitadelBasin_Terrain");
            terrain.transform.SetParent(root, false);
            var filter = terrain.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateTerrain(resolution);
            var renderer = terrain.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateSurfaceMaterial(
                new Color(0.075f, 0.082f, 0.090f),
                new Color(0.18f, 0.21f, 0.20f),
                0.12f,
                0.22f);
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            terrain.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;

            int rockCount = reducedQuality ? 16 : 34;
            for (int i = 0; i < rockCount; i++)
            {
                float angle = i * 2.39996323f;
                float radius = 18f + Mathf.Repeat(i * 7.13f, 21f);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                var rock = new GameObject("WeatheredMonolith_" + i);
                rock.transform.SetParent(root, false);
                rock.transform.localPosition = new Vector3(x, HeightAt(x, z) + 0.6f, z);
                rock.transform.localRotation = Quaternion.Euler(i % 5 * 2.8f, -angle * Mathf.Rad2Deg, (i % 7 - 3) * 2.2f);
                rock.transform.localScale = new Vector3(0.75f + i % 4 * 0.22f, 1.1f + i % 6 * 0.35f, 0.72f + i % 3 * 0.18f);
                rock.AddComponent<MeshFilter>().sharedMesh = CreateRock(i);
                var rockRenderer = rock.AddComponent<MeshRenderer>();
                rockRenderer.sharedMaterial = renderer.sharedMaterial;
                rockRenderer.shadowCastingMode = ShadowCastingMode.On;
            }

            BuildDistantCitadel(root, reducedQuality, renderer.sharedMaterial);
            ConfigureSky();
        }

        public static Texture2D GetSurfaceTexture(Color baseColor, Color variationColor)
        {
            int key = ColorUtility.ToHtmlStringRGB(baseColor).GetHashCode() ^
                      ColorUtility.ToHtmlStringRGB(variationColor).GetHashCode();
            if (Textures.TryGetValue(key, out var existing) && existing != null)
            {
                return existing;
            }

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, true)
            {
                name = "AL_ProceduralSurface_" + key,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 2
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float broad = Mathf.PerlinNoise(x * 0.055f + 11.2f, y * 0.055f + 31.8f);
                    float fine = Mathf.PerlinNoise(x * 0.19f + 57.3f, y * 0.19f + 3.6f);
                    texture.SetPixel(x, y, Color.Lerp(baseColor * 0.72f, variationColor * 1.12f, broad * 0.74f + fine * 0.26f));
                }
            }

            texture.Apply(true, false);
            Textures[key] = texture;
            return texture;
        }

        private static Material CreateSurfaceMaterial(Color color, Color variation, float metallic, float smoothness)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            var material = new Material(shader) { color = color, mainTexture = GetSurfaceTexture(color, variation) };
            material.mainTextureScale = new Vector2(7f, 7f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        private static Mesh CreateTerrain(int resolution)
        {
            var vertices = new Vector3[resolution * resolution];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(resolution - 1) * (resolution - 1) * 6];
            float step = TerrainSize / (resolution - 1);
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float worldX = -TerrainSize * 0.5f + x * step;
                    float worldZ = -TerrainSize * 0.5f + z * step;
                    int index = z * resolution + x;
                    vertices[index] = new Vector3(worldX, HeightAt(worldX, worldZ), worldZ);
                    uv[index] = new Vector2(x / (resolution - 1f), z / (resolution - 1f));
                }
            }

            int t = 0;
            for (int z = 0; z < resolution - 1; z++)
            {
                for (int x = 0; x < resolution - 1; x++)
                {
                    int i = z * resolution + x;
                    triangles[t++] = i; triangles[t++] = i + resolution; triangles[t++] = i + 1;
                    triangles[t++] = i + 1; triangles[t++] = i + resolution; triangles[t++] = i + resolution + 1;
                }
            }

            var mesh = new Mesh { name = "AL_CitadelBasin_" + resolution, vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float HeightAt(float x, float z)
        {
            float radius = Mathf.Sqrt(x * x + z * z);
            float basin = Mathf.SmoothStep(-0.7f, 8.5f, Mathf.InverseLerp(11f, 44f, radius));
            float broad = Mathf.PerlinNoise(x * 0.035f + 13.7f, z * 0.035f + 41.3f) * 5.2f;
            float detail = Mathf.PerlinNoise(x * 0.12f + 73.1f, z * 0.12f + 7.9f) * 1.2f;
            return basin + broad + detail + Mathf.Abs(Mathf.Sin(x * 0.075f + z * 0.046f)) * 2.1f - 3.4f;
        }

        private static Mesh CreateRock(int seed)
        {
            const int sides = 8;
            var vertices = new Vector3[sides * 2 + 2];
            var triangles = new List<int>(sides * 12);
            vertices[0] = new Vector3(0f, -0.5f, 0f);
            vertices[vertices.Length - 1] = new Vector3(0f, 0.62f, 0f);
            for (int ring = 0; ring < 2; ring++)
            {
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2f / sides;
                    float noise = 0.78f + Mathf.PerlinNoise(seed * 0.31f + i, ring * 2.7f) * 0.34f;
                    vertices[1 + ring * sides + i] = new Vector3(Mathf.Cos(angle) * noise, ring == 0 ? -0.34f : 0.30f, Mathf.Sin(angle) * noise);
                }
            }
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int lower = 1 + i;
                int lowerNext = 1 + next;
                int upper = 1 + sides + i;
                int upperNext = 1 + sides + next;
                triangles.Add(0); triangles.Add(lowerNext); triangles.Add(lower);
                triangles.Add(lower); triangles.Add(lowerNext); triangles.Add(upperNext);
                triangles.Add(lower); triangles.Add(upperNext); triangles.Add(upper);
                triangles.Add(vertices.Length - 1); triangles.Add(upper); triangles.Add(upperNext);
            }
            var mesh = new Mesh { name = "AL_WeatheredRock_" + seed, vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static void BuildDistantCitadel(Transform parent, bool reducedQuality, Material material)
        {
            int count = reducedQuality ? 7 : 11;
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.Lerp(-0.82f, 0.82f, i / Mathf.Max(1f, count - 1f));
                float x = Mathf.Sin(angle) * 31f;
                float z = 31f + Mathf.Abs(x) * 0.18f;
                var tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tower.name = "DistantCitadelTower_" + i;
                tower.transform.SetParent(parent, false);
                tower.transform.localPosition = new Vector3(x, HeightAt(x, z) + 3.5f + i % 3, z);
                tower.transform.localScale = new Vector3(1.4f + i % 2 * 0.45f, 4.6f + i % 4 * 1.2f, 1.4f + i % 2 * 0.45f);
                Object.Destroy(tower.GetComponent<Collider>());
                tower.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        private static void ConfigureSky()
        {
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return;
            var sky = new Material(shader);
            sky.SetColor("_SkyTint", new Color(0.20f, 0.27f, 0.38f));
            sky.SetColor("_GroundColor", new Color(0.035f, 0.040f, 0.048f));
            sky.SetFloat("_AtmosphereThickness", 0.78f);
            sky.SetFloat("_Exposure", 0.68f);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.22f, 0.28f, 0.38f);
            RenderSettings.ambientEquatorColor = new Color(0.10f, 0.12f, 0.16f);
            RenderSettings.ambientGroundColor = new Color(0.035f, 0.038f, 0.045f);
        }
    }
}
