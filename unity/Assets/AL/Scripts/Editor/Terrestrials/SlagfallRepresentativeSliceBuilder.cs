using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.RealmWar.Territories.Runtime;
using AL.Terrestrials.Slagfall;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AL.Editor.Terrestrials
{
    public static class SlagfallRepresentativeSliceBuilder
    {
        public const string RootFolder =
            "Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry";
        public const string EnvironmentFolder = RootFolder + "/Environment";
        public const string EnvironmentMeshFolder =
            EnvironmentFolder + "/Meshes";
        public const string EnvironmentMaterialFolder =
            EnvironmentFolder + "/Materials";
        public const string EnvironmentTextureFolder =
            EnvironmentFolder + "/Textures";
        public const string EnvironmentPrefabFolder =
            EnvironmentFolder + "/Prefabs";
        public const string SlagwhistleFolder =
            RootFolder + "/Fauna/Slagwhistle";
        public const string SlagwhistleMeshFolder =
            SlagwhistleFolder + "/Meshes";
        public const string SlagwhistleMaterialFolder =
            SlagwhistleFolder + "/Materials";
        public const string SlagwhistleTextureFolder =
            SlagwhistleFolder + "/Textures";
        public const string SlagwhistleAnimationFolder =
            SlagwhistleFolder + "/Animations";
        public const string SlagwhistlePrefabFolder =
            SlagwhistleFolder + "/Prefabs";
        public const string ProfilePath =
            EnvironmentPrefabFolder +
            "/Slagfall_RepresentativeSlice_Profile.asset";
        public const string SlagwhistlePrefabPath =
            SlagwhistlePrefabFolder +
            "/Slagwhistle_StandardAdult_Production.prefab";
        public const string SyntheticCrowdPrefabPath =
            EnvironmentPrefabFolder +
            "/Slagfall_SyntheticCrowd_User.prefab";
        public const string SlicePrefabPath =
            EnvironmentPrefabFolder +
            "/Slagfall_RepresentativeSlice.prefab";
        public const string ScenePath =
            "Assets/AL/Scenes/Prototype/Terrestrials/" +
            "SlagfallQuarryRepresentativeSlice.unity";

        private const int EnvironmentTextureSize = 2048;
        private const int SlagwhistleTextureSize = 1024;
        private const int SlagwhistleBoneCount = 36;

        private static readonly int[] FamilyVariantCounts =
            { 3, 3, 3, 1, 1, 1, 1, 1 };

        private static readonly float[] RimAngles =
        {
            0.02f, 0.19f, 0.42f,
            0.88f, 1.05f, 1.31f, 1.52f,
            2.05f, 2.24f,
            2.70f, 2.92f, 3.18f,
            3.73f, 3.90f, 4.18f, 4.42f,
            5.02f, 5.19f, 5.45f,
            5.92f
        };

        private static readonly Vector3[] FamilyPositions =
        {
            new Vector3(-28f, -1.4f, -14f),
            new Vector3(18f, -1.2f, 9f),
            new Vector3(-12f, -0.4f, 30f),
            new Vector3(31f, -1.7f, -27f),
            new Vector3(-34f, -0.8f, 24f),
            new Vector3(5f, -0.6f, -33f),
            new Vector3(10f, -1.8f, 20f),
            new Vector3(-5f, -1.5f, -3f)
        };

        private static readonly Vector3[] FamilyScales =
        {
            new Vector3(2.4f, 1.25f, 1.8f),
            new Vector3(2.0f, 1.3f, 2.3f),
            new Vector3(2.4f, 1.5f, 1.6f),
            new Vector3(2.2f, 1.2f, 2.5f),
            new Vector3(2.2f, 2.0f, 1.8f),
            new Vector3(1.5f, 1.8f, 2.7f),
            new Vector3(2.7f, 0.5f, 1.2f),
            new Vector3(2.4f, 1.0f, 1.8f)
        };

        [MenuItem(
            "Another Life/Terrestrials/" +
            "Build Slagfall Representative Slice")]
        public static void Build()
        {
            EnsureFolders();

            TextureSet habitatTextures = CreateHabitatTextures();
            TextureSet slagwhistleTextures = CreateSlagwhistleTextures();
            MaterialSet materials = CreateMaterials(
                habitatTextures,
                slagwhistleTextures);

            SlagfallHabitatFamilyEntry[] habitatFamilies =
                CreateHabitatFamilies(materials);
            AnimationClip[] clips = CreateSlagwhistleAnimationClips();
            RuntimeAnimatorController controller =
                CreateSlagwhistleAnimatorController(clips);
            GameObject slagwhistlePrefab =
                CreateSlagwhistlePrefab(materials.Slagwhistle, controller);
            GameObject syntheticCrowdPrefab =
                CreateSyntheticCrowdPrefab(materials.Crowd);

            SlagfallRepresentativeSliceProfile profile =
                LoadOrCreateProfile();
            SlagwhistleMetrics metrics =
                MeasureSlagwhistle(slagwhistlePrefab);
            profile.Configure(
                habitatFamilies,
                slagwhistlePrefab,
                null,
                clips,
                habitatTextures.AsArray(),
                slagwhistleTextures.AsArray(),
                metrics.Lod0Triangles,
                metrics.Lod1Triangles,
                metrics.Lod2Triangles,
                metrics.ImpostorTriangles,
                metrics.BoneCount,
                metrics.MaterialSlots,
                MeasureFolderBytes(EnvironmentFolder),
                MeasureFolderBytes(SlagwhistleFolder));
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            GameObject slicePrefab = CreateRepresentativeSlicePrefab(
                profile,
                habitatFamilies,
                slagwhistlePrefab,
                syntheticCrowdPrefab,
                materials);

            profile.Configure(
                habitatFamilies,
                slagwhistlePrefab,
                slicePrefab,
                clips,
                habitatTextures.AsArray(),
                slagwhistleTextures.AsArray(),
                metrics.Lod0Triangles,
                metrics.Lod1Triangles,
                metrics.Lod2Triangles,
                metrics.ImpostorTriangles,
                metrics.BoneCount,
                metrics.MaterialSlots,
                MeasureFolderBytes(EnvironmentFolder),
                MeasureFolderBytes(SlagwhistleFolder));
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            CreatePreviewScene(slicePrefab);
            EnsureSceneExcludedFromBuild();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!profile.Validate(out string diagnostic))
            {
                throw new InvalidOperationException(
                    $"Generated Slagfall profile is invalid: {diagnostic}");
            }
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        public static void RenderFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = Object.FindObjectOfType<Camera>();
            if (camera == null)
            {
                throw new InvalidOperationException(
                    "Slagfall review camera is missing.");
            }

            WriteFrame(
                camera,
                1536,
                864,
                Path.Combine(
                    Application.dataPath,
                    "..",
                    "..",
                    ".omx",
                    "state",
                    "slagfall-representative-slice",
                    "render.png"));
        }

        public static void ReportMetricsFromCommandLine()
        {
            SlagfallRepresentativeSliceProfile profile =
                AssetDatabase.LoadAssetAtPath<
                    SlagfallRepresentativeSliceProfile>(ProfilePath);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    "Slagfall profile is unavailable.");
            }

            if (!profile.Validate(out string diagnostic))
            {
                throw new InvalidOperationException(
                    $"Slagfall profile is unavailable: {diagnostic}");
            }

            Debug.Log(
                "SLAGFALL_METRIC " +
                $"habitatFamilies={profile.HabitatFamilies.Count} " +
                $"lod0={profile.SlagwhistleLod0Triangles} " +
                $"lod1={profile.SlagwhistleLod1Triangles} " +
                $"lod2={profile.SlagwhistleLod2Triangles} " +
                $"impostor={profile.SlagwhistleImpostorTriangles} " +
                $"bones={profile.SlagwhistleBoneCount} " +
                $"materials={profile.SlagwhistleMaterialSlots} " +
                $"habitatBytes={profile.HabitatSourceBytes} " +
                $"slagwhistleBytes={profile.SlagwhistleSourceBytes}");
        }

        private static TextureSet CreateHabitatTextures()
        {
            return new TextureSet(
                CreateTexture(
                    EnvironmentTextureFolder +
                    "/T_Slagfall_Habitat_Color_2048.png",
                    EnvironmentTextureSize,
                    TextureRole.Color,
                    new Color32(60, 63, 66, 255),
                    147),
                CreateTexture(
                    EnvironmentTextureFolder +
                    "/T_Slagfall_Habitat_Normal_2048.png",
                    EnvironmentTextureSize,
                    TextureRole.Normal,
                    new Color32(128, 128, 255, 255),
                    193),
                CreateTexture(
                    EnvironmentTextureFolder +
                    "/T_Slagfall_Habitat_Packed_2048.png",
                    EnvironmentTextureSize,
                    TextureRole.Packed,
                    new Color32(30, 118, 0, 72),
                    227));
        }

        private static TextureSet CreateSlagwhistleTextures()
        {
            return new TextureSet(
                CreateTexture(
                    SlagwhistleTextureFolder +
                    "/T_Slagwhistle_Color_1024.png",
                    SlagwhistleTextureSize,
                    TextureRole.Color,
                    new Color32(76, 57, 43, 255),
                    311),
                CreateTexture(
                    SlagwhistleTextureFolder +
                    "/T_Slagwhistle_Normal_1024.png",
                    SlagwhistleTextureSize,
                    TextureRole.Normal,
                    new Color32(128, 128, 255, 255),
                    367),
                CreateTexture(
                    SlagwhistleTextureFolder +
                    "/T_Slagwhistle_Packed_1024.png",
                    SlagwhistleTextureSize,
                    TextureRole.Packed,
                    new Color32(18, 142, 0, 54),
                    419));
        }

        private static Texture2D CreateTexture(
            string path,
            int size,
            TextureRole role,
            Color32 baseColor,
            int seed)
        {
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                true,
                role == TextureRole.Color)
            {
                name = Path.GetFileNameWithoutExtension(path),
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int hash =
                        (x * 37 + y * 61 + seed * 101 +
                            ((x >> 4) * (y >> 5))) & 255;
                    int striation =
                        ((x + y * 3 + seed) % 113) < 5 ? 18 : 0;
                    int crack =
                        ((x * 2 - y + seed * 7) % 197 + 197) %
                        197 < 3
                            ? -26
                            : 0;

                    Color32 pixel;
                    switch (role)
                    {
                        case TextureRole.Normal:
                            pixel = new Color32(
                                ClampByte(128 + (hash % 13) - 6),
                                ClampByte(128 +
                                    ((hash / 7) % 13) - 6),
                                255,
                                255);
                            break;
                        case TextureRole.Packed:
                            pixel = new Color32(
                                ClampByte(baseColor.r + hash % 10),
                                ClampByte(baseColor.g +
                                    (hash % 21) - 10),
                                0,
                                ClampByte(baseColor.a +
                                    (hash % 17) - 8));
                            break;
                        default:
                            int variation = (hash % 25) - 12 +
                                striation + crack;
                            pixel = new Color32(
                                ClampByte(baseColor.r + variation),
                                ClampByte(baseColor.g + variation),
                                ClampByte(baseColor.b +
                                    variation / 2),
                                255);
                            break;
                    }

                    pixels[y * size + x] = pixel;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            File.WriteAllBytes(
                Path.GetFullPath(path),
                texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"Unity did not import {path}.");
            }

            importer.textureType = role == TextureRole.Normal
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            importer.sRGBTexture = role == TextureRole.Color;
            importer.alphaSource =
                role == TextureRole.Packed
                    ? TextureImporterAlphaSource.FromInput
                    : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.maxTextureSize = size;
            importer.textureCompression =
                TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static MaterialSet CreateMaterials(
            TextureSet habitat,
            TextureSet slagwhistle)
        {
            Material rock = CreateMaterial(
                EnvironmentMaterialFolder + "/MAT_Slagfall_Rock.mat",
                habitat,
                new Color(0.82f, 0.86f, 0.90f),
                0.12f,
                0.24f);
            Material soil = CreateMaterial(
                EnvironmentMaterialFolder + "/MAT_Slagfall_IronSoil.mat",
                habitat,
                new Color(0.72f, 0.36f, 0.20f),
                0.04f,
                0.18f);
            Material runoff = CreateMaterial(
                EnvironmentMaterialFolder + "/MAT_Slagfall_Runoff.mat",
                habitat,
                new Color(0.35f, 0.48f, 0.50f),
                0.08f,
                0.42f);
            Material slagwhistleMaterial = CreateMaterial(
                SlagwhistleMaterialFolder + "/MAT_Slagwhistle_Opaque.mat",
                slagwhistle,
                new Color(0.74f, 0.58f, 0.44f),
                0.10f,
                0.22f);
            Material crowd = CreateSolidMaterial(
                EnvironmentMaterialFolder +
                "/MAT_Slagfall_SyntheticCrowd.mat",
                new Color(0.14f, 0.18f, 0.20f),
                0.05f,
                0.22f);
            Material ground = CreateMaterial(
                EnvironmentMaterialFolder + "/MAT_Slagfall_Ground.mat",
                habitat,
                new Color(0.62f, 0.66f, 0.70f),
                0.02f,
                0.16f);
            SetMaterialTiling(ground, 12f);
            Material cavity = CreateSolidMaterial(
                EnvironmentMaterialFolder + "/MAT_Slagfall_Cavity.mat",
                new Color(0.025f, 0.03f, 0.035f),
                0f,
                0.06f);
            return new MaterialSet(
                rock,
                soil,
                runoff,
                slagwhistleMaterial,
                crowd,
                ground,
                cavity);
        }

        private static void SetMaterialTiling(
            Material material,
            float tiling)
        {
            Vector2 scale = Vector2.one * tiling;
            material.SetTextureScale("_MainTex", scale);
            material.SetTextureScale("_BumpMap", scale);
            material.SetTextureScale("_MetallicGlossMap", scale);
            EditorUtility.SetDirty(material);
        }

        private static Material CreateMaterial(
            string path,
            TextureSet textures,
            Color color,
            float metallic,
            float smoothness)
        {
            Material material =
                CreateSolidMaterial(
                    path,
                    color,
                    metallic,
                    smoothness);
            material.mainTexture = textures.Color;
            material.SetTexture("_BumpMap", textures.Normal);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", textures.Packed);
            material.EnableKeyword("_METALLICGLOSSMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateSolidMaterial(
            string path,
            Color color,
            float metallic,
            float smoothness)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "The built-in Standard shader is unavailable.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.name = Path.GetFileNameWithoutExtension(path);
            material.color = color;
            material.enableInstancing = true;
            material.SetFloat("_Mode", 0f);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = -1;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static SlagfallHabitatFamilyEntry[] CreateHabitatFamilies(
            MaterialSet materials)
        {
            var entries =
                new SlagfallHabitatFamilyEntry[
                    SlagfallSourceAuthority.HabitatFamilyIds.Length];
            for (int familyIndex = 0;
                familyIndex < entries.Length;
                familyIndex++)
            {
                int variantCount = FamilyVariantCounts[familyIndex];
                var variants = new GameObject[variantCount];
                for (int variantIndex = 0;
                    variantIndex < variantCount;
                    variantIndex++)
                {
                    variants[variantIndex] =
                        CreateHabitatPrefab(
                            familyIndex,
                            variantIndex,
                            materials);
                }

                var entry = new SlagfallHabitatFamilyEntry();
                entry.Configure(
                    SlagfallSourceAuthority.HabitatFamilyIds[familyIndex],
                    variants);
                entries[familyIndex] = entry;
            }

            return entries;
        }

        private static GameObject CreateHabitatPrefab(
            int familyIndex,
            int variantIndex,
            MaterialSet materials)
        {
            string familySlug =
                SlagfallSourceAuthority.HabitatFamilyIds[familyIndex]
                    .Replace("slagfall.", string.Empty)
                    .Replace('_', '-');
            string prefabPath =
                $"{EnvironmentPrefabFolder}/" +
                $"{familySlug}-v{variantIndex + 1}.prefab";
            var root = new GameObject(
                $"Slagfall_{familySlug}_V{variantIndex + 1}");
            try
            {
                var renderers = new Renderer[4];
                for (int lodIndex = 0; lodIndex < 4; lodIndex++)
                {
                    var lod = new GameObject($"LOD{lodIndex}");
                    lod.transform.SetParent(root.transform, false);
                    Mesh mesh = CreateHabitatMesh(
                        familyIndex,
                        variantIndex,
                        lodIndex,
                        familySlug);
                    lod.AddComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer renderer =
                        lod.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial =
                        ResolveHabitatMaterial(familyIndex, materials);
                    ConfigureRenderer(renderer, lodIndex);
                    renderers[lodIndex] = renderer;
                }

                LODGroup lodGroup = root.AddComponent<LODGroup>();
                lodGroup.SetLODs(new[]
                {
                    new LOD(0.52f, new[] { renderers[0] }),
                    new LOD(0.24f, new[] { renderers[1] }),
                    new LOD(0.09f, new[] { renderers[2] }),
                    new LOD(0.018f, new[] { renderers[3] })
                });
                lodGroup.RecalculateBounds();
                root.AddComponent<SlagfallHabitatAsset>().Configure(
                    SlagfallSourceAuthority.HabitatFamilyIds[familyIndex],
                    variantIndex,
                    lodGroup);
                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Material ResolveHabitatMaterial(
            int familyIndex,
            MaterialSet materials)
        {
            if (familyIndex == 6)
            {
                return materials.Runoff;
            }

            if (familyIndex == 3 || familyIndex == 7)
            {
                return materials.Soil;
            }

            return materials.Rock;
        }

        private static Mesh CreateHabitatMesh(
            int familyIndex,
            int variantIndex,
            int lodIndex,
            string familySlug)
        {
            int[] sideCounts = { 18, 12, 7, 4 };
            int sides = sideCounts[lodIndex];
            float seed = familyIndex * 2.31f + variantIndex * 1.73f;
            Mesh mesh;
            switch (familyIndex)
            {
                case 3:
                    mesh = CreateRadialMoundMesh(
                        sides,
                        5.8f + variantIndex,
                        1.4f,
                        seed);
                    break;
                case 4:
                    mesh = CreateGalleryMouthMesh(
                        Math.Max(5, sides),
                        7.2f,
                        4.8f,
                        2.2f,
                        seed);
                    break;
                case 6:
                    mesh = CreateBraidedPoolMesh(
                        Math.Max(6, sides),
                        8.5f,
                        2.2f,
                        seed);
                    break;
                default:
                    float length =
                        familyIndex == 5 ? 10.5f : 8.0f;
                    float width =
                        familyIndex == 2 ? 4.4f : 6.2f;
                    float height =
                        familyIndex == 7 ? 0.9f : 1.7f;
                    mesh = CreateExtrudedPolygonMesh(
                        sides,
                        length,
                        width,
                        height,
                        seed,
                        familyIndex == 1 ? 0.42f : 0.18f);
                    break;
            }

            mesh.name =
                $"M_{familySlug}_V{variantIndex + 1}_LOD{lodIndex}";
            string path =
                $"{EnvironmentMeshFolder}/{mesh.name}.asset";
            return PersistMesh(mesh, path);
        }

        private static Mesh CreateExtrudedPolygonMesh(
            int sides,
            float length,
            float width,
            float height,
            float seed,
            float missingCorner)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var perimeter = new Vector3[sides];
            for (int index = 0; index < sides; index++)
            {
                float angle =
                    index / (float)sides * Mathf.PI * 2f;
                float noise =
                    0.78f +
                    0.20f * Mathf.Sin(index * 2.17f + seed) +
                    0.08f * Mathf.Cos(index * 3.73f - seed);
                if (index == (int)(seed * 3f) % sides)
                {
                    noise -= missingCorner;
                }

                perimeter[index] = new Vector3(
                    Mathf.Cos(angle) * length * 0.5f * noise,
                    0f,
                    Mathf.Sin(angle) * width * 0.5f * noise);
            }

            vertices.Add(new Vector3(0f, height, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int index = 0; index < sides; index++)
            {
                Vector3 point = perimeter[index];
                vertices.Add(new Vector3(point.x, height +
                    0.18f * Mathf.Sin(index * 1.91f + seed),
                    point.z));
                uvs.Add(new Vector2(
                    0.5f + point.x / length,
                    0.5f + point.z / width));
            }

            int bottomCenter = vertices.Count;
            vertices.Add(Vector3.zero);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int index = 0; index < sides; index++)
            {
                vertices.Add(perimeter[index]);
                uvs.Add(new Vector2(
                    0.5f + perimeter[index].x / length,
                    0.5f + perimeter[index].z / width));
            }

            for (int index = 0; index < sides; index++)
            {
                int next = (index + 1) % sides;
                int topA = 1 + index;
                int topB = 1 + next;
                int bottomA = bottomCenter + 1 + index;
                int bottomB = bottomCenter + 1 + next;
                triangles.AddRange(new[]
                {
                    0, topB, topA,
                    bottomCenter, bottomA, bottomB,
                    topA, topB, bottomB,
                    topA, bottomB, bottomA
                });
            }

            return FinalizeMesh(vertices, uvs, triangles);
        }

        private static Mesh CreateRadialMoundMesh(
            int sides,
            float radius,
            float height,
            float seed)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            const int rings = 3;
            for (int ring = 0; ring <= rings; ring++)
            {
                float t = ring / (float)rings;
                float ringRadius = radius * t;
                float y = height * (1f - t * t);
                for (int side = 0; side < sides; side++)
                {
                    float angle =
                        side / (float)sides * Mathf.PI * 2f;
                    float noise =
                        0.84f +
                        0.14f * Mathf.Sin(side * 2.7f + seed);
                    vertices.Add(new Vector3(
                        Mathf.Cos(angle) * ringRadius * noise,
                        y,
                        Mathf.Sin(angle) * ringRadius * noise));
                    uvs.Add(new Vector2(
                        0.5f + Mathf.Cos(angle) * t * 0.5f,
                        0.5f + Mathf.Sin(angle) * t * 0.5f));
                }
            }

            for (int ring = 0; ring < rings; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    int next = (side + 1) % sides;
                    int a = ring * sides + side;
                    int b = ring * sides + next;
                    int c = (ring + 1) * sides + side;
                    int d = (ring + 1) * sides + next;
                    triangles.AddRange(new[] { a, b, d, a, d, c });
                }
            }

            return FinalizeMesh(vertices, uvs, triangles);
        }

        private static Mesh CreateGalleryMouthMesh(
            int segments,
            float width,
            float depth,
            float height,
            float seed)
        {
            var data = new MeshData();
            AppendEllipsoid(
                data,
                segments,
                Math.Max(4, segments / 2),
                new Vector3(0f, height * 0.35f, 0f),
                new Vector3(width * 0.62f, height, depth * 0.58f),
                Quaternion.Euler(0f, seed * 7f, 0f),
                0);
            AppendEllipsoid(
                data,
                Math.Max(5, segments - 2),
                Math.Max(4, segments / 2),
                new Vector3(0f, height * 0.18f, -depth * 0.22f),
                new Vector3(width * 0.42f, height * 0.62f, depth * 0.66f),
                Quaternion.identity,
                0,
                true);
            return data.ToMesh();
        }

        private static Mesh CreateBraidedPoolMesh(
            int sides,
            float length,
            float width,
            float seed)
        {
            var data = new MeshData();
            for (int branch = 0; branch < 3; branch++)
            {
                AppendEllipsoid(
                    data,
                    sides,
                    Math.Max(3, sides / 2),
                    new Vector3(
                        (branch - 1) * length * 0.26f,
                        0.06f,
                        Mathf.Sin(seed + branch * 1.8f) *
                            width * 0.28f),
                    new Vector3(
                        length * 0.34f,
                        0.08f,
                        width * (0.42f - branch * 0.06f)),
                    Quaternion.Euler(
                        0f,
                        -18f + branch * 17f,
                        0f),
                    0);
            }
            return data.ToMesh();
        }

        private static AnimationClip[] CreateSlagwhistleAnimationClips()
        {
            var specs = new[]
            {
                new ClipSpec(
                    "Slagwhistle_RestVentRecovery",
                    "Rig/VentFoldLeft",
                    "localEulerAnglesRaw.z",
                    0f, -12f, 0f),
                new ClipSpec(
                    "Slagwhistle_LowScurry",
                    "Rig/ForelegLeft",
                    "localEulerAnglesRaw.x",
                    -18f, 24f, -18f),
                new ClipSpec(
                    "Slagwhistle_PlantStop",
                    "Rig/Head",
                    "localEulerAnglesRaw.x",
                    6f, -14f, 2f),
                new ClipSpec(
                    "Slagwhistle_EdgeCut",
                    "Rig/SpineMid",
                    "localEulerAnglesRaw.y",
                    -8f, 18f, -6f),
                new ClipSpec(
                    "Slagwhistle_SpoilPush",
                    "Rig/BraceTail",
                    "localEulerAnglesRaw.x",
                    -8f, 20f, -8f),
                new ClipSpec(
                    "Slagwhistle_TurnRecovery",
                    "Rig/Head",
                    "localEulerAnglesRaw.y",
                    -24f, 18f, 0f)
            };
            var clips = new AnimationClip[specs.Length];
            for (int index = 0; index < specs.Length; index++)
            {
                ClipSpec spec = specs[index];
                string path =
                    $"{SlagwhistleAnimationFolder}/{spec.Name}.anim";
                AnimationClip clip =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                {
                    clip = new AnimationClip();
                    AssetDatabase.CreateAsset(clip, path);
                }

                clip.name = spec.Name;
                clip.frameRate = 24f;
                clip.wrapMode = WrapMode.Loop;
                var curve = new AnimationCurve(
                    new Keyframe(0f, spec.Start),
                    new Keyframe(0.5f, spec.Middle),
                    new Keyframe(1f, spec.End));
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        spec.Path,
                        typeof(Transform),
                        spec.Property),
                    curve);
                AnimationUtility.SetAnimationClipSettings(
                    clip,
                    new AnimationClipSettings
                    {
                        loopTime = true,
                        loopBlend = true
                    });
                EditorUtility.SetDirty(clip);
                clips[index] = clip;
            }

            return clips;
        }

        private static RuntimeAnimatorController
            CreateSlagwhistleAnimatorController(AnimationClip[] clips)
        {
            string path =
                $"{SlagwhistleAnimationFolder}/" +
                "Slagwhistle_Presentation.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine stateMachine =
                controller.layers[0].stateMachine;
            for (int index = 0; index < clips.Length; index++)
            {
                AnimatorState state =
                    stateMachine.AddState(clips[index].name);
                state.motion = clips[index];
                if (index == 0)
                {
                    stateMachine.defaultState = state;
                }
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static GameObject CreateSlagwhistlePrefab(
            Material material,
            RuntimeAnimatorController controller)
        {
            var root =
                new GameObject("Slagwhistle_StandardAdult_Production");
            try
            {
                Transform[] bones = CreateSlagwhistleBones(root.transform);
                Transform rig = root.transform.Find("Rig");

                GameObject full = CreateSkinnedRepresentation(
                    root.transform,
                    "FullDetail",
                    0,
                    material,
                    bones,
                    rig,
                    false);
                GameObject medium = CreateSkinnedRepresentation(
                    root.transform,
                    "MediumDetail",
                    1,
                    material,
                    bones,
                    rig,
                    false);
                GameObject low = CreateStaticRepresentation(
                    root.transform,
                    "LowDetail",
                    2,
                    material);
                GameObject impostor = CreateStaticRepresentation(
                    root.transform,
                    "Impostor",
                    3,
                    material);

                Animator animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode =
                    AnimatorCullingMode.CullUpdateTransforms;

                full.SetActive(true);
                medium.SetActive(false);
                low.SetActive(false);
                impostor.SetActive(false);

                var identityRoot =
                    new GameObject("ProtectedIdentityMarkers");
                identityRoot.transform.SetParent(root.transform, false);
                foreach (string feature in
                    SlagfallSourceAuthority.ProtectedSlagwhistleFeatures)
                {
                    new GameObject(feature).transform.SetParent(
                        identityRoot.transform,
                        false);
                }

                Transform[] reducedMotion =
                {
                    root.transform.Find("Rig/VentFoldLeft"),
                    root.transform.Find("Rig/VentFoldRight"),
                    root.transform.Find("Rig/BraceTail")
                };
                root.AddComponent<SlagwhistlePresentation>().Configure(
                    full,
                    medium,
                    low,
                    impostor,
                    new[] { animator },
                    reducedMotion);

                return PrefabUtility.SaveAsPrefabAsset(
                    root,
                    SlagwhistlePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Transform[] CreateSlagwhistleBones(Transform parent)
        {
            Transform rig = CreateGroup(parent, "Rig");
            string[] names =
            {
                "RootBone", "SpineBase", "SpineMid", "SpineFront",
                "NeckBase", "Head", "Jaw", "NostrilLeft", "NostrilRight",
                "VentFoldLeft", "VentFoldLeftTip", "VentFoldRight",
                "VentFoldRightTip", "ForelegLeft", "ForepawLeft",
                "ShovelPalmLeft", "StabilizerLeftA", "StabilizerLeftB",
                "ForelegRight", "ForepawRight", "ShovelPalmRight",
                "StabilizerRightA", "StabilizerRightB", "HindlegLeft",
                "HindpawLeft", "HindlegRight", "HindpawRight",
                "BraceTail", "BraceTailTip", "ShoulderLeft",
                "ShoulderRight", "HipLeft", "HipRight", "Belly",
                "Chest", "RecoveryAnchor"
            };
            var bones = new Transform[names.Length];
            for (int index = 0; index < names.Length; index++)
            {
                bones[index] = CreateGroup(rig, names[index]);
            }

            bones[0].localPosition = new Vector3(0f, 0.65f, 0f);
            bones[1].localPosition = new Vector3(0f, 0.10f, -0.75f);
            bones[2].localPosition = new Vector3(0f, 0.16f, 0f);
            bones[3].localPosition = new Vector3(0f, 0.12f, 0.75f);
            bones[5].localPosition = new Vector3(0f, 0.05f, 1.45f);
            bones[9].localPosition = new Vector3(-0.52f, 0.55f, 0.48f);
            bones[11].localPosition = new Vector3(0.52f, 0.55f, 0.48f);
            bones[13].localPosition = new Vector3(-0.58f, -0.35f, 0.72f);
            bones[18].localPosition = new Vector3(0.58f, -0.35f, 0.72f);
            bones[23].localPosition = new Vector3(-0.56f, -0.34f, -0.75f);
            bones[25].localPosition = new Vector3(0.56f, -0.34f, -0.75f);
            bones[27].localPosition = new Vector3(0f, -0.05f, -1.35f);
            return bones;
        }

        private static GameObject CreateSkinnedRepresentation(
            Transform parent,
            string name,
            int lodIndex,
            Material material,
            Transform[] bones,
            Transform rootBone,
            bool castShadows)
        {
            var representation = new GameObject(name);
            representation.transform.SetParent(parent, false);
            SkinnedMeshRenderer renderer =
                representation.AddComponent<SkinnedMeshRenderer>();
            Mesh mesh = CreateSlagwhistleMesh(lodIndex, bones);
            mesh.bindposes = bones
                .Select(
                    bone =>
                        bone.worldToLocalMatrix *
                        parent.localToWorldMatrix)
                .ToArray();
            mesh = PersistMesh(
                mesh,
                $"{SlagwhistleMeshFolder}/" +
                $"M_Slagwhistle_LOD{lodIndex}.asset");
            renderer.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.bones = bones;
            renderer.rootBone = rootBone;
            renderer.updateWhenOffscreen = false;
            renderer.skinnedMotionVectors = false;
            renderer.shadowCastingMode =
                castShadows
                    ? ShadowCastingMode.On
                    : ShadowCastingMode.Off;
            renderer.receiveShadows = castShadows;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return representation;
        }

        private static GameObject CreateStaticRepresentation(
            Transform parent,
            string name,
            int lodIndex,
            Material material)
        {
            var representation = new GameObject(name);
            representation.transform.SetParent(parent, false);
            Mesh mesh = CreateSlagwhistleMesh(lodIndex, null);
            mesh = PersistMesh(
                mesh,
                $"{SlagwhistleMeshFolder}/" +
                $"M_Slagwhistle_LOD{lodIndex}.asset");
            representation.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer =
                representation.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            ConfigureRenderer(renderer, lodIndex);
            return representation;
        }

        private static Mesh CreateSlagwhistleMesh(
            int lodIndex,
            Transform[] bones)
        {
            DetailSpec spec = DetailSpec.ForLod(lodIndex);
            var data = new MeshData();

            AppendEllipsoid(
                data,
                spec.BodySides,
                spec.BodyRings,
                new Vector3(0f, 0.66f, -0.12f),
                new Vector3(0.95f, 0.52f, 1.48f),
                Quaternion.identity,
                2);
            AppendEllipsoid(
                data,
                spec.HeadSides,
                spec.HeadRings,
                new Vector3(0f, 0.58f, 1.48f),
                new Vector3(0.68f, 0.34f, 0.92f),
                Quaternion.Euler(-9f, 0f, 0f),
                5,
                false,
                0.28f);
            AppendEllipsoid(
                data,
                spec.TailSides,
                spec.TailRings,
                new Vector3(0f, 0.60f, -1.55f),
                new Vector3(0.70f, 0.17f, 0.72f),
                Quaternion.Euler(8f, 0f, 0f),
                27);

            AppendEllipsoid(
                data,
                spec.YokeSides,
                spec.YokeRings,
                new Vector3(-0.48f, 0.82f, 0.05f),
                new Vector3(0.55f, 0.10f, 0.34f),
                Quaternion.Euler(-4f, -8f, -10f),
                9);
            AppendEllipsoid(
                data,
                spec.YokeSides,
                spec.YokeRings,
                new Vector3(0.48f, 0.82f, 0.05f),
                new Vector3(0.55f, 0.10f, 0.34f),
                Quaternion.Euler(-4f, 8f, 10f),
                11);

            AppendEllipsoid(
                data,
                spec.ShovelSides,
                spec.ShovelRings,
                new Vector3(-0.55f, 0.22f, 1.12f),
                new Vector3(0.42f, 0.12f, 0.58f),
                Quaternion.Euler(6f, -18f, -8f),
                15);
            AppendEllipsoid(
                data,
                spec.ShovelSides,
                spec.ShovelRings,
                new Vector3(0.55f, 0.22f, 1.12f),
                new Vector3(0.42f, 0.12f, 0.58f),
                Quaternion.Euler(6f, 18f, 8f),
                20);

            AppendLeg(
                data,
                spec.LegSides,
                spec.LegRings,
                new Vector3(-0.57f, 0.35f, 0.60f),
                13);
            AppendLeg(
                data,
                spec.LegSides,
                spec.LegRings,
                new Vector3(0.57f, 0.35f, 0.60f),
                18);
            AppendLeg(
                data,
                spec.LegSides,
                spec.LegRings,
                new Vector3(-0.55f, 0.35f, -0.78f),
                23);
            AppendLeg(
                data,
                spec.LegSides,
                spec.LegRings,
                new Vector3(0.55f, 0.35f, -0.78f),
                25);

            if (spec.StabilizerSides > 0)
            {
                AppendStabilizer(
                    data,
                    spec.StabilizerSides,
                    spec.StabilizerRings,
                    new Vector3(-0.83f, 0.18f, 1.25f),
                    -18f,
                    16);
                AppendStabilizer(
                    data,
                    spec.StabilizerSides,
                    spec.StabilizerRings,
                    new Vector3(-0.47f, 0.16f, 1.48f),
                    8f,
                    17);
                AppendStabilizer(
                    data,
                    spec.StabilizerSides,
                    spec.StabilizerRings,
                    new Vector3(0.83f, 0.18f, 1.25f),
                    18f,
                    21);
                AppendStabilizer(
                    data,
                    spec.StabilizerSides,
                    spec.StabilizerRings,
                    new Vector3(0.47f, 0.16f, 1.48f),
                    -8f,
                    22);
            }

            if (spec.NostrilSides > 0)
            {
                AppendEllipsoid(
                    data,
                    spec.NostrilSides,
                    spec.NostrilRings,
                    new Vector3(-0.20f, 0.77f, 2.23f),
                    new Vector3(0.16f, 0.035f, 0.08f),
                    Quaternion.Euler(0f, -4f, 0f),
                    7);
                AppendEllipsoid(
                    data,
                    spec.NostrilSides,
                    spec.NostrilRings,
                    new Vector3(0.20f, 0.77f, 2.23f),
                    new Vector3(0.16f, 0.035f, 0.08f),
                    Quaternion.Euler(0f, 4f, 0f),
                    8);
            }

            if (spec.CheekSides > 0)
            {
                AppendEllipsoid(
                    data,
                    spec.CheekSides,
                    spec.CheekRings,
                    new Vector3(0f, 0.52f, 1.77f),
                    new Vector3(0.46f, 0.20f, 0.38f),
                    Quaternion.identity,
                    6);
            }

            Mesh mesh = data.ToMesh();
            mesh.name = $"M_Slagwhistle_LOD{lodIndex}";
            if (bones != null)
            {
                mesh.boneWeights = data.BoneWeights.ToArray();
            }
            return mesh;
        }

        private static void AppendLeg(
            MeshData data,
            int sides,
            int rings,
            Vector3 center,
            int boneIndex)
        {
            center.y = 0.28f;
            AppendEllipsoid(
                data,
                sides,
                rings,
                center,
                new Vector3(0.28f, 0.18f, 0.48f),
                Quaternion.Euler(-12f, 0f, 0f),
                boneIndex);
        }

        private static void AppendStabilizer(
            MeshData data,
            int sides,
            int rings,
            Vector3 center,
            float yaw,
            int boneIndex)
        {
            AppendEllipsoid(
                data,
                sides,
                rings,
                center,
                new Vector3(0.11f, 0.07f, 0.30f),
                Quaternion.Euler(0f, yaw, 0f),
                boneIndex);
        }

        private static void AppendEllipsoid(
            MeshData data,
            int sides,
            int rings,
            Vector3 center,
            Vector3 radius,
            Quaternion rotation,
            int boneIndex,
            bool invert = false,
            float wedge = 0f)
        {
            sides = Math.Max(3, sides);
            rings = Math.Max(3, rings);
            int start = data.Vertices.Count;

            data.AddVertex(
                center + rotation *
                new Vector3(0f, radius.y, 0f),
                new Vector2(0.5f, 1f),
                boneIndex);
            for (int ring = 1; ring < rings; ring++)
            {
                float v = ring / (float)rings;
                float phi = Mathf.PI * v;
                float y = Mathf.Cos(phi);
                float ringRadius = Mathf.Sin(phi);
                for (int side = 0; side < sides; side++)
                {
                    float u = side / (float)sides;
                    float theta = u * Mathf.PI * 2f;
                    float zScale = 1f -
                        wedge * Mathf.Clamp01(
                            (Mathf.Cos(phi) + 1f) * 0.5f);
                    Vector3 local = new Vector3(
                        Mathf.Cos(theta) * radius.x * ringRadius,
                        y * radius.y,
                        Mathf.Sin(theta) * radius.z *
                            ringRadius * zScale);
                    data.AddVertex(
                        center + rotation * local,
                        new Vector2(u, 1f - v),
                        boneIndex);
                }
            }
            int bottom = data.Vertices.Count;
            data.AddVertex(
                center + rotation *
                new Vector3(0f, -radius.y, 0f),
                new Vector2(0.5f, 0f),
                boneIndex);

            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                AddTriangle(
                    data,
                    start,
                    start + 1 + side,
                    start + 1 + next,
                    invert);
            }

            for (int ring = 0; ring < rings - 2; ring++)
            {
                int current = start + 1 + ring * sides;
                int nextRing = current + sides;
                for (int side = 0; side < sides; side++)
                {
                    int next = (side + 1) % sides;
                    AddTriangle(
                        data,
                        current + side,
                        nextRing + side,
                        nextRing + next,
                        invert);
                    AddTriangle(
                        data,
                        current + side,
                        nextRing + next,
                        current + next,
                        invert);
                }
            }

            int lastRing = bottom - sides;
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                AddTriangle(
                    data,
                    bottom,
                    lastRing + next,
                    lastRing + side,
                    invert);
            }
        }

        private static void AddTriangle(
            MeshData data,
            int a,
            int b,
            int c,
            bool invert)
        {
            if (invert)
            {
                data.Triangles.Add(a);
                data.Triangles.Add(c);
                data.Triangles.Add(b);
            }
            else
            {
                data.Triangles.Add(a);
                data.Triangles.Add(b);
                data.Triangles.Add(c);
            }
        }

        private static GameObject CreateSyntheticCrowdPrefab(
            Material material)
        {
            var root = new GameObject("Slagfall_SyntheticCrowd_User");
            try
            {
                Mesh fullMesh = PersistMesh(
                    CreateExtrudedPolygonMesh(
                        8, 0.8f, 0.55f, 1.8f, 0.7f, 0.05f),
                    EnvironmentMeshFolder +
                    "/M_Slagfall_SyntheticCrowd_Full.asset");
                Mesh mediumMesh = PersistMesh(
                    CreateExtrudedPolygonMesh(
                        6, 0.78f, 0.52f, 1.75f, 1.1f, 0.05f),
                    EnvironmentMeshFolder +
                    "/M_Slagfall_SyntheticCrowd_Medium.asset");
                Mesh lowMesh = PersistMesh(
                    CreateExtrudedPolygonMesh(
                        4, 0.74f, 0.48f, 1.68f, 1.7f, 0.05f),
                    EnvironmentMeshFolder +
                    "/M_Slagfall_SyntheticCrowd_Low.asset");
                Mesh impostorMesh = PersistMesh(
                    CreateQuadMesh(0.72f, 1.65f),
                    EnvironmentMeshFolder +
                    "/M_Slagfall_SyntheticCrowd_Impostor.asset");

                GameObject full =
                    CreateCrowdRepresentation(
                        root.transform,
                        "FullDetail",
                        fullMesh,
                        material,
                        true);
                GameObject medium =
                    CreateCrowdRepresentation(
                        root.transform,
                        "MediumDetail",
                        mediumMesh,
                        material,
                        true);
                GameObject low =
                    CreateCrowdRepresentation(
                        root.transform,
                        "LowDetail",
                        lowMesh,
                        material,
                        false);
                GameObject impostor =
                    CreateCrowdRepresentation(
                        root.transform,
                        "Impostor",
                        impostorMesh,
                        material,
                        false);
                full.SetActive(true);
                medium.SetActive(false);
                low.SetActive(false);
                impostor.SetActive(false);
                root.AddComponent<TerritoryCrowdParticipant>().Configure(
                    full,
                    medium,
                    low,
                    impostor);
                return PrefabUtility.SaveAsPrefabAsset(
                    root,
                    SyntheticCrowdPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateCrowdRepresentation(
            Transform parent,
            string name,
            Mesh mesh,
            Material material,
            bool animated)
        {
            var representation = new GameObject(name);
            representation.transform.SetParent(parent, false);
            representation.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer =
                representation.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (animated)
            {
                representation.AddComponent<Animator>();
            }
            return representation;
        }

        private static GameObject CreateRepresentativeSlicePrefab(
            SlagfallRepresentativeSliceProfile profile,
            SlagfallHabitatFamilyEntry[] habitatFamilies,
            GameObject slagwhistlePrefab,
            GameObject syntheticCrowdPrefab,
            MaterialSet materials)
        {
            var root =
                new GameObject("Slagfall_RepresentativeSlice_128m");
            try
            {
                Transform environment =
                    CreateGroup(root.transform, "Environment");
                Mesh bowlMesh = PersistMesh(
                    CreateQuarryBowlMesh(),
                    EnvironmentMeshFolder +
                    "/M_Slagfall_QuarryBowl_128m.asset");
                var bowl = new GameObject("CompressedQuarryBowl");
                bowl.transform.SetParent(environment, false);
                bowl.AddComponent<MeshFilter>().sharedMesh = bowlMesh;
                MeshRenderer bowlRenderer =
                    bowl.AddComponent<MeshRenderer>();
                bowlRenderer.sharedMaterial = materials.Ground;
                bowlRenderer.shadowCastingMode = ShadowCastingMode.Off;
                bowlRenderer.receiveShadows = true;

                var environmentLods = new List<LODGroup>();
                for (int familyIndex = 0;
                    familyIndex < habitatFamilies.Length;
                    familyIndex++)
                {
                    SlagfallHabitatFamilyEntry family =
                        habitatFamilies[familyIndex];
                    for (int variantIndex = 0;
                        variantIndex < family.Variants.Count;
                        variantIndex++)
                    {
                        GameObject instance =
                            (GameObject)PrefabUtility.InstantiatePrefab(
                                family.Variants[variantIndex]);
                        instance.transform.SetParent(environment, false);
                        instance.name =
                            $"{family.FamilyId}_V{variantIndex + 1}";
                        float offset = variantIndex -
                            (family.Variants.Count - 1) * 0.5f;
                        instance.transform.localPosition =
                            FamilyPositions[familyIndex] +
                            new Vector3(
                                offset * 8.5f,
                                variantIndex * 0.16f,
                                offset * -5.5f);
                        instance.transform.localRotation =
                            Quaternion.Euler(
                                -2f + familyIndex,
                                familyIndex * 31f +
                                    variantIndex * 47f,
                                familyIndex % 2 == 0 ? 3f : -4f);
                        instance.transform.localScale =
                            Vector3.Scale(
                                FamilyScales[familyIndex],
                                new Vector3(
                                    1f + variantIndex * 0.16f,
                                    1f - variantIndex * 0.08f,
                                    1f + (2 - variantIndex) * 0.10f));
                        environmentLods.Add(
                            instance.GetComponent<LODGroup>());
                    }
                }

                PopulateFracturedQuarry(
                    environment,
                    habitatFamilies,
                    environmentLods,
                    materials);

                GameObject slagwhistle =
                    (GameObject)PrefabUtility.InstantiatePrefab(
                        slagwhistlePrefab);
                slagwhistle.transform.SetParent(root.transform, false);
                slagwhistle.transform.localPosition =
                    new Vector3(4f, -0.4f, -4f);
                slagwhistle.transform.localRotation =
                    Quaternion.Euler(0f, 50f, 0f);
                slagwhistle.transform.localScale =
                    Vector3.one * 5.5f;

                Transform crowdRoot =
                    CreateGroup(root.transform, "SyntheticCrowd100");
                var crowd =
                    new TerritoryCrowdParticipant[
                        TerritoryLoadDegradationPlanner
                            .SafeRepresentedUserCapacity];
                for (int index = 0; index < crowd.Length; index++)
                {
                    GameObject user =
                        (GameObject)PrefabUtility.InstantiatePrefab(
                            syntheticCrowdPrefab);
                    user.transform.SetParent(crowdRoot, false);
                    user.name = $"SyntheticUser_{index:000}";
                    float angle =
                        index / (float)crowd.Length *
                        Mathf.PI * 2f;
                    float ring =
                        43f + (index % 5) * 2.4f;
                    user.transform.localPosition = new Vector3(
                        Mathf.Cos(angle) * ring,
                        -1.5f,
                        Mathf.Sin(angle) * ring);
                    user.transform.localRotation =
                        Quaternion.Euler(
                            0f,
                            -angle * Mathf.Rad2Deg + 90f,
                            0f);
                    crowd[index] =
                        user.GetComponent<TerritoryCrowdParticipant>();
                }

                Transform systems = CreateGroup(root.transform, "Systems");
                Transform observer = CreateGroup(systems, "Observer");
                observer.localPosition =
                    new Vector3(0f, 8f, -24f);
                TerritoryLoadVisualAdapter adapter =
                    systems.gameObject
                        .AddComponent<TerritoryLoadVisualAdapter>();
                adapter.Configure(
                    Array.Empty<ParticleSystem>(),
                    Array.Empty<ParticleSystem>(),
                    Array.Empty<Light>(),
                    environmentLods.ToArray());
                TerritoryLoadDegradationController controller =
                    systems.gameObject
                        .AddComponent<TerritoryLoadDegradationController>();
                controller.Configure(
                    observer,
                    adapter,
                    33.333f,
                    0.5f,
                    3f);
                SlagfallRepresentativeSlice slice =
                    root.AddComponent<SlagfallRepresentativeSlice>();
                slice.Configure(
                    profile,
                    controller,
                    adapter,
                    slagwhistle.GetComponent<
                        SlagwhistlePresentation>(),
                    crowd,
                    Array.Empty<GameObject>());

                return PrefabUtility.SaveAsPrefabAsset(
                    root,
                    SlicePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void PopulateFracturedQuarry(
            Transform environment,
            SlagfallHabitatFamilyEntry[] habitatFamilies,
            List<LODGroup> environmentLods,
            MaterialSet materials)
        {
            for (int index = 0;
                index < RimAngles.Length;
                index++)
            {
                int familyIndex = index % 3;
                SlagfallHabitatFamilyEntry family =
                    habitatFamilies[familyIndex];
                GameObject source =
                    family.Variants[index % family.Variants.Count];
                GameObject instance =
                    (GameObject)PrefabUtility.InstantiatePrefab(source);
                instance.transform.SetParent(environment, false);
                instance.name = $"QuarryRimFracture_{index:00}";
                float angle = RimAngles[index];
                float radius =
                    44f +
                    Mathf.Sin(index * 1.91f) * 5.2f +
                    Mathf.Cos(index * 0.73f) * 2.2f;
                instance.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0.2f + Mathf.Sin(index * 1.37f) * 1.2f,
                    Mathf.Sin(angle) * radius);
                instance.transform.localRotation =
                    Quaternion.Euler(
                        -5f + Mathf.Sin(index) * 4f,
                        -angle * Mathf.Rad2Deg + 90f,
                        Mathf.Cos(index * 0.7f) * 5f);
                instance.transform.localScale = new Vector3(
                    2.05f + (index % 5) * 0.31f,
                    1.45f + (index % 4) * 0.36f,
                    1.20f + (index % 3) * 0.25f);
                environmentLods.Add(instance.GetComponent<LODGroup>());
            }

            for (int index = 0; index < 10; index++)
            {
                int familyIndex = index % 3;
                SlagfallHabitatFamilyEntry family =
                    habitatFamilies[familyIndex];
                GameObject source =
                    family.Variants[
                        (index / 3) % family.Variants.Count];
                GameObject instance =
                    (GameObject)PrefabUtility.InstantiatePrefab(source);
                instance.transform.SetParent(environment, false);
                instance.name = $"QuarryBenchFracture_{index:00}";
                float angle =
                    (index * 0.6180339f % 1f) *
                    Mathf.PI * 2f;
                float radius = 18f + (index % 3) * 8f;
                instance.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    -1.35f + (index % 3) * 0.42f,
                    Mathf.Sin(angle) * radius);
                instance.transform.localRotation =
                    Quaternion.Euler(
                        -4f,
                        -angle * Mathf.Rad2Deg +
                            65f + (index % 4) * 13f,
                        index % 2 == 0 ? 3f : -3f);
                instance.transform.localScale = new Vector3(
                    2.05f + (index % 4) * 0.24f,
                    1.05f + (index % 3) * 0.22f,
                    1.55f + (index % 2) * 0.30f);
                environmentLods.Add(instance.GetComponent<LODGroup>());
            }

            SlagfallHabitatFamilyEntry runoffFamily =
                habitatFamilies[6];
            for (int index = 0; index < 7; index++)
            {
                GameObject instance =
                    (GameObject)PrefabUtility.InstantiatePrefab(
                        runoffFamily.Variants[0]);
                instance.transform.SetParent(environment, false);
                instance.name = $"BraidedRunoffChannel_{index:00}";
                float x = -29f + index * 9.5f;
                instance.transform.localPosition = new Vector3(
                    x,
                    -1.75f + (index % 2) * 0.08f,
                    -11f +
                        Mathf.Sin(index * 1.37f) * 8f);
                instance.transform.localRotation =
                    Quaternion.Euler(
                        0f,
                        -24f + index * 7f,
                        0f);
                instance.transform.localScale = new Vector3(
                    2.2f,
                    0.35f,
                    0.72f);
                environmentLods.Add(instance.GetComponent<LODGroup>());
            }

            Mesh cavityMesh = PersistMesh(
                CreateGalleryCavityMesh(9f, 3.2f, 12),
                EnvironmentMeshFolder +
                "/M_Slagfall_GalleryCavity.asset");
            float[] cavityAngles = { 1.05f, 1.72f, 2.48f };
            for (int index = 0;
                index < cavityAngles.Length;
                index++)
            {
                float angle = cavityAngles[index];
                var cavity =
                    new GameObject($"GalleryCavity_{index:00}");
                cavity.transform.SetParent(environment, false);
                cavity.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * 40.5f,
                    0.1f + index * 0.25f,
                    Mathf.Sin(angle) * 40.5f);
                cavity.transform.localRotation =
                    Quaternion.Euler(
                        -3f,
                        90f - angle * Mathf.Rad2Deg,
                        0f);
                cavity.AddComponent<MeshFilter>().sharedMesh =
                    cavityMesh;
                MeshRenderer renderer =
                    cavity.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = materials.Cavity;
                renderer.shadowCastingMode =
                    ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage =
                    ReflectionProbeUsage.Off;
            }
        }

        private static Mesh CreateQuarryBowlMesh()
        {
            const int resolution = 49;
            const float cellSize = 128f;
            float spacing = cellSize / (resolution - 1);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int z = 0; z < resolution; z++)
            {
                float worldZ = -cellSize * 0.5f + z * spacing;
                for (int x = 0; x < resolution; x++)
                {
                    float worldX =
                        -cellSize * 0.5f + x * spacing;
                    float radial =
                        Mathf.Clamp01(
                            new Vector2(worldX, worldZ)
                                .magnitude / 64f);
                    float bowl =
                        -3.4f +
                        5.8f * Mathf.Pow(radial, 1.65f);
                    float broadBreakup =
                        Mathf.Sin(worldX * 0.095f) * 0.24f +
                        Mathf.Cos(worldZ * 0.082f) * 0.20f +
                        Mathf.Sin(
                            (worldX + worldZ) * 0.061f) *
                        0.18f;
                    float granular =
                        (Mathf.PerlinNoise(
                            (worldX + 91f) * 0.075f,
                            (worldZ + 47f) * 0.075f) -
                            0.5f) *
                        0.72f;
                    float height =
                        bowl + broadBreakup + granular;
                    if (radial < 0.90f)
                    {
                        height =
                            Mathf.Round(height * 2f) * 0.5f;
                    }

                    vertices.Add(new Vector3(
                        worldX,
                        height,
                        worldZ));
                    uvs.Add(new Vector2(
                        x / (float)(resolution - 1),
                        z / (float)(resolution - 1)));
                }
            }

            for (int z = 0; z < resolution - 1; z++)
            {
                for (int x = 0; x < resolution - 1; x++)
                {
                    int a = z * resolution + x;
                    int b = a + 1;
                    int c = a + resolution;
                    int d = c + 1;
                    triangles.AddRange(
                        new[] { a, d, b, a, c, d });
                }
            }

            return FinalizeMesh(vertices, uvs, triangles);
        }

        private static Mesh CreateQuadMesh(float width, float height)
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-width * 0.5f, 0f, 0f),
                    new Vector3(width * 0.5f, 0f, 0f),
                    new Vector3(width * 0.5f, height, 0f),
                    new Vector3(-width * 0.5f, height, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 }
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateGalleryCavityMesh(
            float width,
            float height,
            int archSegments)
        {
            archSegments = Math.Max(4, archSegments);
            var vertices = new List<Vector3>
            {
                new Vector3(0f, height * 0.30f, 0f)
            };
            for (int index = 0;
                index <= archSegments;
                index++)
            {
                float angle =
                    Mathf.PI -
                    index / (float)archSegments *
                    Mathf.PI;
                vertices.Add(new Vector3(
                    Mathf.Cos(angle) * width * 0.5f,
                    Mathf.Sin(angle) * height,
                    0f));
            }

            var triangles = new List<int>();
            int perimeterCount = vertices.Count - 1;
            for (int index = 0;
                index < perimeterCount;
                index++)
            {
                int next = (index + 1) % perimeterCount;
                triangles.AddRange(
                    new[] { 0, next + 1, index + 1 });
            }

            var mesh = new Mesh
            {
                vertices = vertices.ToArray(),
                uv = vertices
                    .Select(
                        vertex =>
                            new Vector2(
                                0.5f + vertex.x / width,
                                vertex.y / height))
                    .ToArray(),
                triangles = triangles.ToArray()
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CreatePreviewScene(GameObject slicePrefab)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            GameObject slice =
                (GameObject)PrefabUtility.InstantiatePrefab(
                    slicePrefab);
            slice.name = "Slagfall_RepresentativeSlice_128m";
            Transform crowd =
                slice.transform.Find("SyntheticCrowd100");
            if (crowd != null)
            {
                crowd.gameObject.SetActive(false);
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor =
                new Color(0.24f, 0.25f, 0.26f);
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 260f;
            camera.transform.position =
                new Vector3(52f, 18f, -58f);
            camera.transform.LookAt(new Vector3(0f, -0.6f, 7f));

            var lightObject =
                new GameObject("ReviewLight_Key_Shadowless");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.95f, 0.96f, 1f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.52f;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.35f;
            lightObject.transform.rotation =
                Quaternion.Euler(52f, -34f, 0f);

            var fillObject =
                new GameObject("ReviewLight_Fill_Shadowless");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(1f, 0.82f, 0.67f);
            fill.intensity = 0.25f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation =
                Quaternion.Euler(28f, 142f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight =
                new Color(0.36f, 0.36f, 0.37f);

            var evidenceObject =
                new GameObject("Slagfall Device Evidence Runner");
            evidenceObject
                .AddComponent<SlagfallDeviceEvidenceRunner>()
                .Configure(
                    slice.GetComponent<
                        SlagfallRepresentativeSlice>(),
                    SlagfallEvidenceLane.MobileLow,
                    SlagfallEvidenceContract.MinimumRunSeconds,
                    false,
                    false);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void EnsureSceneExcludedFromBuild()
        {
            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Where(
                    scene =>
                        !string.Equals(
                            scene.path,
                            ScenePath,
                            StringComparison.Ordinal))
                .ToArray();
        }

        private static SlagfallRepresentativeSliceProfile
            LoadOrCreateProfile()
        {
            SlagfallRepresentativeSliceProfile profile =
                AssetDatabase.LoadAssetAtPath<
                    SlagfallRepresentativeSliceProfile>(ProfilePath);
            if (profile != null)
            {
                return profile;
            }

            profile =
                ScriptableObject.CreateInstance<
                    SlagfallRepresentativeSliceProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        private static SlagwhistleMetrics MeasureSlagwhistle(
            GameObject prefab)
        {
            Transform full = prefab.transform.Find("FullDetail");
            Transform medium = prefab.transform.Find("MediumDetail");
            Transform low = prefab.transform.Find("LowDetail");
            Transform impostor = prefab.transform.Find("Impostor");
            SkinnedMeshRenderer fullRenderer =
                full.GetComponent<SkinnedMeshRenderer>();
            SkinnedMeshRenderer mediumRenderer =
                medium.GetComponent<SkinnedMeshRenderer>();
            MeshRenderer[] renderers =
                prefab.GetComponentsInChildren<MeshRenderer>(true);
            return new SlagwhistleMetrics(
                TriangleCount(fullRenderer.sharedMesh),
                TriangleCount(mediumRenderer.sharedMesh),
                TriangleCount(
                    low.GetComponent<MeshFilter>().sharedMesh),
                TriangleCount(
                    impostor.GetComponent<MeshFilter>().sharedMesh),
                fullRenderer.bones.Length,
                prefab.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null)
                    .Distinct()
                    .Count());
        }

        private static int TriangleCount(Mesh mesh)
        {
            if (mesh == null)
            {
                return 0;
            }

            int count = 0;
            for (int subMesh = 0;
                subMesh < mesh.subMeshCount;
                subMesh++)
            {
                count += (int)mesh.GetIndexCount(subMesh) / 3;
            }
            return count;
        }

        private static long MeasureFolderBytes(string assetFolder)
        {
            string absolute = Path.GetFullPath(assetFolder);
            if (!Directory.Exists(absolute))
            {
                return 0L;
            }

            return Directory.EnumerateFiles(
                    absolute,
                    "*",
                    SearchOption.AllDirectories)
                .Where(
                    path =>
                        !path.EndsWith(
                            ".meta",
                            StringComparison.OrdinalIgnoreCase))
                .Sum(path => new FileInfo(path).Length);
        }

        private static Mesh PersistMesh(Mesh generated, string path)
        {
            generated.name = Path.GetFileNameWithoutExtension(path);
            generated.RecalculateNormals();
            generated.RecalculateTangents();
            generated.RecalculateBounds();
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }

            EditorUtility.CopySerialized(generated, existing);
            Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Mesh FinalizeMesh(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles)
        {
            var mesh = new Mesh
            {
                indexFormat =
                    vertices.Count > 65535
                        ? IndexFormat.UInt32
                        : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void ConfigureRenderer(
            MeshRenderer renderer,
            int lodIndex)
        {
            renderer.shadowCastingMode =
                lodIndex == 0
                    ? ShadowCastingMode.On
                    : ShadowCastingMode.Off;
            renderer.receiveShadows = lodIndex <= 1;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Transform CreateGroup(
            Transform parent,
            string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static void WriteFrame(
            Camera camera,
            int width,
            int height,
            string path)
        {
            var target = new RenderTexture(width, height, 24);
            var capture = new Texture2D(
                width,
                height,
                TextureFormat.RGB24,
                false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                capture.ReadPixels(
                    new Rect(0f, 0f, width, height),
                    0,
                    0);
                capture.Apply();
                string outputPath = Path.GetFullPath(path);
                Directory.CreateDirectory(
                    Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(
                    outputPath,
                    capture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                Object.DestroyImmediate(capture);
                Object.DestroyImmediate(target);
            }
        }

        private static void EnsureFolders()
        {
            string[] paths =
            {
                RootFolder,
                EnvironmentFolder,
                EnvironmentMeshFolder,
                EnvironmentMaterialFolder,
                EnvironmentTextureFolder,
                EnvironmentPrefabFolder,
                SlagwhistleFolder,
                SlagwhistleMeshFolder,
                SlagwhistleMaterialFolder,
                SlagwhistleTextureFolder,
                SlagwhistleAnimationFolder,
                SlagwhistlePrefabFolder,
                "Assets/AL/Scenes/Prototype",
                "Assets/AL/Scenes/Prototype/Terrestrials"
            };
            foreach (string path in paths)
            {
                EnsureFolder(path);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)
                ?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) ||
                string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException(
                    $"Cannot create Unity folder {path}.");
            }
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static byte ClampByte(int value)
        {
            return (byte)Mathf.Clamp(value, 0, 255);
        }

        private enum TextureRole
        {
            Color = 0,
            Normal = 1,
            Packed = 2
        }

        private sealed class TextureSet
        {
            public TextureSet(
                Texture2D color,
                Texture2D normal,
                Texture2D packed)
            {
                Color = color;
                Normal = normal;
                Packed = packed;
            }

            public Texture2D Color { get; }
            public Texture2D Normal { get; }
            public Texture2D Packed { get; }

            public Texture2D[] AsArray()
            {
                return new[] { Color, Normal, Packed };
            }
        }

        private sealed class MaterialSet
        {
            public MaterialSet(
                Material rock,
                Material soil,
                Material runoff,
                Material slagwhistle,
                Material crowd,
                Material ground,
                Material cavity)
            {
                Rock = rock;
                Soil = soil;
                Runoff = runoff;
                Slagwhistle = slagwhistle;
                Crowd = crowd;
                Ground = ground;
                Cavity = cavity;
            }

            public Material Rock { get; }
            public Material Soil { get; }
            public Material Runoff { get; }
            public Material Slagwhistle { get; }
            public Material Crowd { get; }
            public Material Ground { get; }
            public Material Cavity { get; }
        }

        private sealed class MeshData
        {
            public List<Vector3> Vertices { get; } =
                new List<Vector3>();
            public List<Vector2> Uvs { get; } =
                new List<Vector2>();
            public List<int> Triangles { get; } =
                new List<int>();
            public List<BoneWeight> BoneWeights { get; } =
                new List<BoneWeight>();

            public void AddVertex(
                Vector3 vertex,
                Vector2 uv,
                int boneIndex)
            {
                Vertices.Add(vertex);
                Uvs.Add(uv);
                BoneWeights.Add(new BoneWeight
                {
                    boneIndex0 = boneIndex,
                    weight0 = 1f
                });
            }

            public Mesh ToMesh()
            {
                var mesh = new Mesh
                {
                    indexFormat =
                        Vertices.Count > 65535
                            ? IndexFormat.UInt32
                            : IndexFormat.UInt16
                };
                mesh.SetVertices(Vertices);
                mesh.SetUVs(0, Uvs);
                mesh.SetTriangles(Triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        private readonly struct SlagwhistleMetrics
        {
            public SlagwhistleMetrics(
                int lod0Triangles,
                int lod1Triangles,
                int lod2Triangles,
                int impostorTriangles,
                int boneCount,
                int materialSlots)
            {
                Lod0Triangles = lod0Triangles;
                Lod1Triangles = lod1Triangles;
                Lod2Triangles = lod2Triangles;
                ImpostorTriangles = impostorTriangles;
                BoneCount = boneCount;
                MaterialSlots = materialSlots;
            }

            public int Lod0Triangles { get; }
            public int Lod1Triangles { get; }
            public int Lod2Triangles { get; }
            public int ImpostorTriangles { get; }
            public int BoneCount { get; }
            public int MaterialSlots { get; }
        }

        private readonly struct ClipSpec
        {
            public ClipSpec(
                string name,
                string path,
                string property,
                float start,
                float middle,
                float end)
            {
                Name = name;
                Path = path;
                Property = property;
                Start = start;
                Middle = middle;
                End = end;
            }

            public string Name { get; }
            public string Path { get; }
            public string Property { get; }
            public float Start { get; }
            public float Middle { get; }
            public float End { get; }
        }

        private readonly struct DetailSpec
        {
            private DetailSpec(
                int bodySides,
                int bodyRings,
                int headSides,
                int headRings,
                int tailSides,
                int tailRings,
                int yokeSides,
                int yokeRings,
                int shovelSides,
                int shovelRings,
                int legSides,
                int legRings,
                int stabilizerSides,
                int stabilizerRings,
                int nostrilSides,
                int nostrilRings,
                int cheekSides,
                int cheekRings)
            {
                BodySides = bodySides;
                BodyRings = bodyRings;
                HeadSides = headSides;
                HeadRings = headRings;
                TailSides = tailSides;
                TailRings = tailRings;
                YokeSides = yokeSides;
                YokeRings = yokeRings;
                ShovelSides = shovelSides;
                ShovelRings = shovelRings;
                LegSides = legSides;
                LegRings = legRings;
                StabilizerSides = stabilizerSides;
                StabilizerRings = stabilizerRings;
                NostrilSides = nostrilSides;
                NostrilRings = nostrilRings;
                CheekSides = cheekSides;
                CheekRings = cheekRings;
            }

            public int BodySides { get; }
            public int BodyRings { get; }
            public int HeadSides { get; }
            public int HeadRings { get; }
            public int TailSides { get; }
            public int TailRings { get; }
            public int YokeSides { get; }
            public int YokeRings { get; }
            public int ShovelSides { get; }
            public int ShovelRings { get; }
            public int LegSides { get; }
            public int LegRings { get; }
            public int StabilizerSides { get; }
            public int StabilizerRings { get; }
            public int NostrilSides { get; }
            public int NostrilRings { get; }
            public int CheekSides { get; }
            public int CheekRings { get; }

            public static DetailSpec ForLod(int lodIndex)
            {
                switch (lodIndex)
                {
                    case 0:
                        return new DetailSpec(
                            48, 24, 40, 20, 32, 14,
                            28, 14, 24, 12, 16, 8,
                            12, 6, 12, 6, 0, 0);
                    case 1:
                        return new DetailSpec(
                            38, 19, 30, 15, 24, 11,
                            20, 10, 18, 9, 12, 6,
                            8, 4, 8, 4, 10, 5);
                    case 2:
                        return new DetailSpec(
                            24, 12, 18, 9, 14, 7,
                            12, 6, 12, 6, 8, 4,
                            6, 3, 6, 3, 0, 0);
                    case 3:
                        return new DetailSpec(
                            14, 7, 12, 6, 10, 5,
                            8, 4, 8, 4, 6, 3,
                            0, 0, 0, 0, 0, 0);
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(lodIndex));
                }
            }
        }
    }
}
