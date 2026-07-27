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
        private static Mesh _beveledCube;
        private static Mesh _cypressMesh;

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
                new Color(0.11f, 0.14f, 0.145f),
                new Color(0.28f, 0.31f, 0.24f),
                0.04f,
                0.18f);
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
                rock.transform.localPosition = new Vector3(x, HeightAt(x, z) + 0.35f, z);
                rock.transform.localRotation = Quaternion.Euler(i % 5 * 2.8f, -angle * Mathf.Rad2Deg, (i % 7 - 3) * 2.2f);
                float heightScale = i % 7 == 0 ? 1.85f : 0.72f + i % 5 * 0.18f;
                rock.transform.localScale = new Vector3(0.82f + i % 4 * 0.20f, heightScale, 0.78f + i % 3 * 0.18f);
                rock.AddComponent<MeshFilter>().sharedMesh = CreateRock(i);
                var rockRenderer = rock.AddComponent<MeshRenderer>();
                rockRenderer.sharedMaterial = renderer.sharedMaterial;
                rockRenderer.shadowCastingMode = ShadowCastingMode.On;
            }

            BuildWindcarvedGrove(root, reducedQuality);
            BuildDistantCitadel(
                root,
                reducedQuality,
                CreateSurfaceMaterial(
                    new Color(0.085f, 0.105f, 0.135f),
                    new Color(0.22f, 0.25f, 0.29f),
                    0.16f,
                    0.30f),
                CreateEmissiveMaterial(new Color(0.18f, 0.50f, 1f), 1.6f));
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

        public static Mesh GetBeveledCubeMesh()
        {
            if (_beveledCube != null)
            {
                return _beveledCube;
            }

            const float outer = 0.5f;
            const float inner = 0.41f;
            var vertices = new List<Vector3>(160);
            var uv = new List<Vector2>(160);
            var triangles = new List<int>(320);

            for (int axis = 0; axis < 3; axis++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var perimeter = new Vector3[8];
                    var plane = new[]
                    {
                        new Vector2(-inner, -outer),
                        new Vector2(inner, -outer),
                        new Vector2(outer, -inner),
                        new Vector2(outer, inner),
                        new Vector2(inner, outer),
                        new Vector2(-inner, outer),
                        new Vector2(-outer, inner),
                        new Vector2(-outer, -inner)
                    };
                    for (int i = 0; i < perimeter.Length; i++)
                    {
                        perimeter[i] = Compose(axis, sign * outer, plane[i].x, plane[i].y);
                    }

                    AppendPolygon(vertices, uv, triangles, AxisVector(axis) * sign, perimeter);
                }
            }

            int[,] axisPairs = { { 0, 1 }, { 0, 2 }, { 1, 2 } };
            for (int pair = 0; pair < axisPairs.GetLength(0); pair++)
            {
                int firstAxis = axisPairs[pair, 0];
                int secondAxis = axisPairs[pair, 1];
                int remainingAxis = 3 - firstAxis - secondAxis;
                for (int firstSign = -1; firstSign <= 1; firstSign += 2)
                {
                    for (int secondSign = -1; secondSign <= 1; secondSign += 2)
                    {
                        var edge = new Vector3[4];
                        edge[0] = ComposeAxes(firstAxis, firstSign * outer, secondAxis, secondSign * inner, remainingAxis, -inner);
                        edge[1] = ComposeAxes(firstAxis, firstSign * inner, secondAxis, secondSign * outer, remainingAxis, -inner);
                        edge[2] = ComposeAxes(firstAxis, firstSign * inner, secondAxis, secondSign * outer, remainingAxis, inner);
                        edge[3] = ComposeAxes(firstAxis, firstSign * outer, secondAxis, secondSign * inner, remainingAxis, inner);
                        Vector3 normal = (AxisVector(firstAxis) * firstSign + AxisVector(secondAxis) * secondSign).normalized;
                        AppendPolygon(vertices, uv, triangles, normal, edge);
                    }
                }
            }

            for (int xSign = -1; xSign <= 1; xSign += 2)
            {
                for (int ySign = -1; ySign <= 1; ySign += 2)
                {
                    for (int zSign = -1; zSign <= 1; zSign += 2)
                    {
                        AppendPolygon(
                            vertices,
                            uv,
                            triangles,
                            new Vector3(xSign, ySign, zSign).normalized,
                            new Vector3(xSign * outer, ySign * inner, zSign * inner),
                            new Vector3(xSign * inner, ySign * outer, zSign * inner),
                            new Vector3(xSign * inner, ySign * inner, zSign * outer));
                    }
                }
            }

            _beveledCube = new Mesh
            {
                name = "AL_BeveledCube",
                vertices = vertices.ToArray(),
                uv = uv.ToArray(),
                triangles = triangles.ToArray()
            };
            _beveledCube.RecalculateNormals();
            _beveledCube.RecalculateBounds();
            return _beveledCube;
        }

        private static Material CreateSurfaceMaterial(Color color, Color variation, float metallic, float smoothness)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            var material = new Material(shader)
            {
                color = Color.white,
                mainTexture = GetSurfaceTexture(color, variation)
            };
            material.mainTextureScale = new Vector2(7f, 7f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        private static Material CreateEmissiveMaterial(Color color, float intensity)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * Mathf.Max(0f, intensity));
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.72f);
            }

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
            const int rings = 3;
            var vertices = new Vector3[sides * rings + 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new List<int>(sides * 18);
            vertices[0] = new Vector3(0f, -0.5f, 0f);
            uv[0] = new Vector2(0.5f, 0f);
            vertices[vertices.Length - 1] = new Vector3(0f, 0.62f, 0f);
            uv[uv.Length - 1] = new Vector2(0.5f, 1f);
            for (int ring = 0; ring < rings; ring++)
            {
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2f / sides;
                    float ringRadius = ring == 1 ? 1f : ring == 0 ? 0.76f : 0.68f;
                    float noise = ringRadius *
                                  (0.84f + Mathf.PerlinNoise(seed * 0.31f + i, ring * 2.7f) * 0.30f);
                    float offsetX = ring == 1 ? (seed % 3 - 1) * 0.045f : 0f;
                    float offsetZ = ring == 2 ? (seed % 5 - 2) * 0.028f : 0f;
                    float y = ring == 0 ? -0.32f : ring == 1 ? 0.02f : 0.34f;
                    int index = 1 + ring * sides + i;
                    vertices[index] = new Vector3(
                        Mathf.Cos(angle) * noise + offsetX,
                        y,
                        Mathf.Sin(angle) * noise + offsetZ);
                    uv[index] = new Vector2(i / (float)sides, (ring + 1f) / (rings + 1f));
                }
            }

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                triangles.Add(0);
                triangles.Add(1 + next);
                triangles.Add(1 + i);
                for (int ring = 0; ring < rings - 1; ring++)
                {
                    int lower = 1 + ring * sides + i;
                    int lowerNext = 1 + ring * sides + next;
                    int upper = lower + sides;
                    int upperNext = lowerNext + sides;
                    triangles.Add(lower);
                    triangles.Add(lowerNext);
                    triangles.Add(upperNext);
                    triangles.Add(lower);
                    triangles.Add(upperNext);
                    triangles.Add(upper);
                }

                int topRing = 1 + (rings - 1) * sides;
                triangles.Add(vertices.Length - 1);
                triangles.Add(topRing + i);
                triangles.Add(topRing + next);
            }

            var mesh = new Mesh
            {
                name = "AL_WeatheredRock_" + seed,
                vertices = vertices,
                uv = uv,
                triangles = triangles.ToArray()
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void BuildWindcarvedGrove(Transform parent, bool reducedQuality)
        {
            int count = reducedQuality ? 14 : 28;
            Mesh mesh = GetCypressMesh();
            Material material = CreateSurfaceMaterial(
                new Color(0.035f, 0.095f, 0.080f),
                new Color(0.12f, 0.24f, 0.15f),
                0f,
                0.12f);

            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.39996323f + 0.72f;
                float radius = 19f + Mathf.Repeat(i * 5.17f, 19f);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                if (Mathf.Abs(x) < 8f && z > 0f)
                {
                    x += x >= 0f ? 8.5f : -8.5f;
                }

                var tree = new GameObject("WindcarvedCypress_" + i);
                tree.transform.SetParent(parent, false);
                tree.transform.localPosition = new Vector3(x, HeightAt(x, z), z);
                tree.transform.localRotation = Quaternion.Euler(
                    (i % 3 - 1) * 1.5f,
                    i * 37f,
                    (i % 5 - 2) * 1.4f);
                float scale = 0.82f + i % 6 * 0.10f;
                tree.transform.localScale = new Vector3(scale, scale * (1.05f + i % 3 * 0.08f), scale);
                tree.AddComponent<MeshFilter>().sharedMesh = mesh;
                var treeRenderer = tree.AddComponent<MeshRenderer>();
                treeRenderer.sharedMaterial = material;
                treeRenderer.shadowCastingMode = reducedQuality ? ShadowCastingMode.Off : ShadowCastingMode.On;
                treeRenderer.receiveShadows = true;
            }
        }

        private static Mesh GetCypressMesh()
        {
            if (_cypressMesh != null)
            {
                return _cypressMesh;
            }

            var vertices = new List<Vector3>(96);
            var uv = new List<Vector2>(96);
            var triangles = new List<int>(192);
            AppendFrustum(vertices, uv, triangles, Vector3.zero, 7, 0f, 1.35f, 0.13f, 0.10f, 0f);
            AppendFrustum(vertices, uv, triangles, Vector3.zero, 7, 0.55f, 2.25f, 0.78f, 0.025f, 0.18f);
            AppendFrustum(vertices, uv, triangles, Vector3.zero, 7, 1.18f, 2.95f, 0.66f, 0.020f, -0.10f);
            AppendFrustum(vertices, uv, triangles, Vector3.zero, 7, 1.88f, 3.55f, 0.52f, 0.015f, 0.12f);
            _cypressMesh = new Mesh
            {
                name = "AL_WindcarvedCypress",
                vertices = vertices.ToArray(),
                uv = uv.ToArray(),
                triangles = triangles.ToArray()
            };
            _cypressMesh.RecalculateNormals();
            _cypressMesh.RecalculateBounds();
            return _cypressMesh;
        }

        private static Vector3 Compose(int fixedAxis, float fixedValue, float firstPlaneValue, float secondPlaneValue)
        {
            switch (fixedAxis)
            {
                case 0: return new Vector3(fixedValue, firstPlaneValue, secondPlaneValue);
                case 1: return new Vector3(firstPlaneValue, fixedValue, secondPlaneValue);
                default: return new Vector3(firstPlaneValue, secondPlaneValue, fixedValue);
            }
        }

        private static Vector3 ComposeAxes(
            int firstAxis,
            float firstValue,
            int secondAxis,
            float secondValue,
            int thirdAxis,
            float thirdValue)
        {
            var value = Vector3.zero;
            value[firstAxis] = firstValue;
            value[secondAxis] = secondValue;
            value[thirdAxis] = thirdValue;
            return value;
        }

        private static Vector3 AxisVector(int axis)
        {
            return axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
        }

        private static void AppendPolygon(
            List<Vector3> vertices,
            List<Vector2> uv,
            List<int> triangles,
            Vector3 outwardNormal,
            params Vector3[] perimeter)
        {
            int centerIndex = vertices.Count;
            Vector3 center = Vector3.zero;
            for (int i = 0; i < perimeter.Length; i++)
            {
                center += perimeter[i];
            }
            center /= perimeter.Length;
            vertices.Add(center);
            uv.Add(ProjectUv(center, outwardNormal));

            int perimeterStart = vertices.Count;
            for (int i = 0; i < perimeter.Length; i++)
            {
                vertices.Add(perimeter[i]);
                uv.Add(ProjectUv(perimeter[i], outwardNormal));
            }

            for (int i = 0; i < perimeter.Length; i++)
            {
                int current = perimeterStart + i;
                int next = perimeterStart + (i + 1) % perimeter.Length;
                bool forward = Vector3.Dot(
                    Vector3.Cross(vertices[current] - center, vertices[next] - center),
                    outwardNormal) >= 0f;
                triangles.Add(centerIndex);
                triangles.Add(forward ? current : next);
                triangles.Add(forward ? next : current);
            }
        }

        private static Vector2 ProjectUv(Vector3 point, Vector3 normal)
        {
            Vector3 absolute = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            if (absolute.x >= absolute.y && absolute.x >= absolute.z)
            {
                return new Vector2(point.z + 0.5f, point.y + 0.5f);
            }

            if (absolute.y >= absolute.z)
            {
                return new Vector2(point.x + 0.5f, point.z + 0.5f);
            }

            return new Vector2(point.x + 0.5f, point.y + 0.5f);
        }

        private static void BuildDistantCitadel(
            Transform parent,
            bool reducedQuality,
            Material stoneMaterial,
            Material windowMaterial)
        {
            int count = reducedQuality ? 7 : 11;
            Vector3 previousPosition = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.Lerp(-0.82f, 0.82f, i / Mathf.Max(1f, count - 1f));
                float x = Mathf.Sin(angle) * 31f;
                float z = 31f + Mathf.Abs(x) * 0.18f;
                var towerPosition = new Vector3(x, HeightAt(x, z) - 0.1f, z);
                var tower = new GameObject("DistantCitadelTower_" + i);
                tower.name = "DistantCitadelTower_" + i;
                tower.transform.SetParent(parent, false);
                tower.transform.localPosition = towerPosition;
                tower.transform.localRotation = Quaternion.Euler(0f, (i % 3 - 1) * 7f, 0f);
                tower.transform.localScale = new Vector3(
                    1.25f + i % 2 * 0.28f,
                    1.22f + i % 4 * 0.16f,
                    1.25f + i % 2 * 0.28f);

                tower.AddComponent<MeshFilter>().sharedMesh = CreateCitadelTower(i);
                var towerRenderer = tower.AddComponent<MeshRenderer>();
                towerRenderer.sharedMaterial = stoneMaterial;
                towerRenderer.shadowCastingMode = ShadowCastingMode.On;
                towerRenderer.receiveShadows = true;

                CreateTowerWindow(
                    tower.transform,
                    "CitadelWindow_Lower_" + i,
                    new Vector3(0f, 1.48f, -0.91f),
                    new Vector3(0.16f, 0.40f, 0.035f),
                    windowMaterial);

                if (!reducedQuality)
                {
                    CreateTowerWindow(
                        tower.transform,
                        "CitadelWindow_Upper_" + i,
                        new Vector3(0f, 2.35f, -0.86f),
                        new Vector3(0.11f, 0.24f, 0.035f),
                        windowMaterial);
                }

                if (i > 0)
                {
                    Vector3 span = towerPosition - previousPosition;
                    var wall = new GameObject("CitadelCurtainWall_" + (i - 1));
                    wall.transform.SetParent(parent, false);
                    wall.transform.localPosition = Vector3.Lerp(previousPosition, towerPosition, 0.5f) + Vector3.up * 1.05f;
                    wall.transform.localRotation = Quaternion.Euler(
                        0f,
                        -Mathf.Atan2(span.z, span.x) * Mathf.Rad2Deg,
                        0f);
                    wall.transform.localScale = new Vector3(
                        Mathf.Sqrt(span.x * span.x + span.z * span.z),
                        2.1f,
                        0.58f);
                    wall.AddComponent<MeshFilter>().sharedMesh = GetBeveledCubeMesh();
                    var wallRenderer = wall.AddComponent<MeshRenderer>();
                    wallRenderer.sharedMaterial = stoneMaterial;
                    wallRenderer.shadowCastingMode = ShadowCastingMode.On;
                    wallRenderer.receiveShadows = true;
                }

                previousPosition = towerPosition;
            }
        }

        private static Mesh CreateCitadelTower(int seed)
        {
            const int sides = 8;
            var vertices = new List<Vector3>(128);
            var uv = new List<Vector2>(128);
            var triangles = new List<int>(256);
            float twist = seed % 2 == 0 ? 0f : Mathf.PI / sides;

            AppendFrustum(vertices, uv, triangles, Vector3.zero, sides, 0f, 3.05f, 0.82f, 0.74f, twist);
            AppendFrustum(vertices, uv, triangles, Vector3.zero, sides, 1.05f, 1.28f, 0.96f, 0.96f, twist);
            AppendFrustum(vertices, uv, triangles, Vector3.zero, sides, 3.05f, 3.34f, 1.02f, 1.02f, twist);
            AppendFrustum(vertices, uv, triangles, Vector3.zero, sides, 3.34f, 5.05f, 0.82f, 0.045f, twist);

            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f + twist;
                var center = new Vector3(Mathf.Cos(angle) * 0.80f, 0f, Mathf.Sin(angle) * 0.80f);
                AppendFrustum(vertices, uv, triangles, center, 5, 3.18f, 4.18f, 0.24f, 0.025f, angle);
            }

            var mesh = new Mesh
            {
                name = "AL_CitadelSpire_" + seed,
                vertices = vertices.ToArray(),
                uv = uv.ToArray(),
                triangles = triangles.ToArray()
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendFrustum(
            List<Vector3> vertices,
            List<Vector2> uv,
            List<int> triangles,
            Vector3 center,
            int sides,
            float bottomY,
            float topY,
            float bottomRadius,
            float topRadius,
            float rotation)
        {
            int start = vertices.Count;
            for (int ring = 0; ring < 2; ring++)
            {
                float radius = ring == 0 ? bottomRadius : topRadius;
                float y = ring == 0 ? bottomY : topY;
                for (int i = 0; i < sides; i++)
                {
                    float angle = rotation + i * Mathf.PI * 2f / sides;
                    vertices.Add(center + new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius));
                    uv.Add(new Vector2(i / (float)sides, ring));
                }
            }

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int lower = start + i;
                int lowerNext = start + next;
                int upper = start + sides + i;
                int upperNext = start + sides + next;
                triangles.Add(lower);
                triangles.Add(lowerNext);
                triangles.Add(upperNext);
                triangles.Add(lower);
                triangles.Add(upperNext);
                triangles.Add(upper);
            }
        }

        private static void CreateTowerWindow(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            var window = GameObject.CreatePrimitive(PrimitiveType.Cube);
            window.name = name;
            window.transform.SetParent(parent, false);
            window.transform.localPosition = localPosition;
            window.transform.localScale = localScale;
            RemoveCollider(window);
            window.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void RemoveCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static void ConfigureSky()
        {
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return;
            var sky = new Material(shader);
            sky.SetColor("_SkyTint", new Color(0.28f, 0.39f, 0.58f));
            sky.SetColor("_GroundColor", new Color(0.055f, 0.070f, 0.090f));
            sky.SetFloat("_AtmosphereThickness", 0.92f);
            sky.SetFloat("_Exposure", 0.92f);
            if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.035f);
            if (sky.HasProperty("_SunSizeConvergence")) sky.SetFloat("_SunSizeConvergence", 4.2f);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.38f, 0.52f);
            RenderSettings.ambientEquatorColor = new Color(0.16f, 0.20f, 0.27f);
            RenderSettings.ambientGroundColor = new Color(0.060f, 0.070f, 0.090f);
            RenderSettings.ambientIntensity = 1.08f;
            RenderSettings.reflectionIntensity = 0.72f;
        }
    }
}
