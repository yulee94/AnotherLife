using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AL.Editor.Terrestrials
{
    public static class SlagfallEnvironmentKitImport
    {
        private const string EnvironmentRoot =
            "Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Environment";
        private const string ModelRoot = EnvironmentRoot + "/Models";
        private const string TextureRoot = EnvironmentRoot + "/Textures";
        private const string MaterialRoot = EnvironmentRoot + "/Materials";
        private const string PrefabRoot = EnvironmentRoot + "/Prefabs";
        private const string MaterialPath =
            MaterialRoot + "/tdf_mat_stonehold_slagfall_environment_atlas_v001.mat";
        private const string ReviewScenePath =
            "Assets/AL/Scenes/Review/Terrestrials/SlagfallEnvironmentKitReview.unity";
        private const string ReviewMaterialPath =
            "Assets/AL/Scenes/Review/Materials/SlagfallReviewSurface.mat";

        private static readonly string[] FamilyIds =
        {
            "irregular_fracture_raft",
            "broken_fracture_raft",
            "undercut_extraction_ledge",
            "talus_apron",
            "collapsed_gallery_mouth",
            "diagonal_fault_slab",
            "braided_runoff_pool",
            "iron_soil_wedge"
        };

        private static readonly float[] LodTransitions = { 0.55f, 0.25f, 0.08f, 0.02f };

        [MenuItem("AnotherLife/Terrestrials/Build Slagfall Environment Kit")]
        public static void Build()
        {
            EnsureFolder(MaterialRoot);
            EnsureFolder(PrefabRoot);
            ConfigureTextures();
            foreach (string familyId in FamilyIds)
            {
                ConfigureModelImporter(ModelPath(familyId));
            }

            Material material = BuildMaterial();
            foreach (string familyId in FamilyIds)
            {
                BuildPrefab(familyId, material);
            }

            BuildReviewScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[SlagfallEnvironmentKitImport] Built {FamilyIds.Length} profiling-scale prefabs " +
                "with one shared material, four render LODs, and LOD3 static mesh colliders.");
        }

        private static void BuildReviewScene()
        {
            EnsureFolder("Assets/AL/Scenes/Review/Terrestrials");
            EnsureFolder("Assets/AL/Scenes/Review/Materials");
            Scene previousActive = SceneManager.GetActiveScene();
            Scene reviewScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            Exception operationError = null;

            try
            {
                if (!SceneManager.SetActiveScene(reviewScene))
                {
                    throw new InvalidOperationException(
                        "Failed to activate the Slagfall review scene.");
                }

                GameObject root = new GameObject("SlagfallEnvironmentKit_ProfilingScaleOnly");
                Vector3[] positions =
                {
                    new Vector3(-6f, 0f, 3f),
                    new Vector3(-2f, 0f, 3f),
                    new Vector3(2f, 0f, 3f),
                    new Vector3(6f, 0f, 3f),
                    new Vector3(-6f, 0f, -3f),
                    new Vector3(-2f, 0f, -3f),
                    new Vector3(2f, 0f, -3f),
                    new Vector3(6f, 0f, -3f)
                };
                float[] rotations = { 15f, -10f, 20f, -20f, 15f, 30f, -15f, 10f };
                for (int index = 0; index < FamilyIds.Length; index++)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        PrefabRoot +
                        $"/tdf_prop_stonehold_slagfall_{FamilyIds[index]}_v001.prefab");
                    if (prefab == null)
                    {
                        throw new InvalidOperationException(
                            $"Missing Slagfall prefab for review: {FamilyIds[index]}");
                    }

                    GameObject instance = PrefabUtility.InstantiatePrefab(
                        prefab,
                        reviewScene) as GameObject;
                    if (instance == null)
                    {
                        throw new InvalidOperationException(
                            $"Failed to instantiate Slagfall prefab: {FamilyIds[index]}");
                    }

                    instance.name = $"{index + 1:00}_{FamilyIds[index]}";
                    instance.transform.SetParent(root.transform, false);
                    instance.transform.localPosition = positions[index];
                    instance.transform.localRotation = Quaternion.Euler(0f, rotations[index], 0f);
                }

                GameObject reviewFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                reviewFloor.name = "ReviewSurface_ProfilingOnly_DoNotShip";
                reviewFloor.transform.SetParent(root.transform, false);
                reviewFloor.transform.localScale = new Vector3(1.8f, 1f, 1.2f);
                reviewFloor.GetComponent<MeshRenderer>().sharedMaterial = BuildReviewSurfaceMaterial();
                MeshCollider floorCollider = reviewFloor.GetComponent<MeshCollider>();
                if (floorCollider != null)
                {
                    UnityEngine.Object.DestroyImmediate(floorCollider);
                }

                GameObject cameraObject = new GameObject("ReviewCamera");
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.position = new Vector3(0f, 7.8f, -13f);
                cameraObject.transform.LookAt(new Vector3(0f, 0.32f, 0f));
                camera.fieldOfView = 37f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.035f, 0.045f, 0.06f, 1f);
                camera.allowHDR = false;
                camera.allowMSAA = true;

                CreateReviewLight(
                    "ReviewKey",
                    new Color(1f, 0.82f, 0.66f, 1f),
                    1.25f,
                    new Vector3(48f, -32f, 0f),
                    LightShadows.Soft);
                CreateReviewLight(
                    "ReviewFill",
                    new Color(0.52f, 0.67f, 1f, 1f),
                    0.45f,
                    new Vector3(28f, 145f, 0f),
                    LightShadows.None);

                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.28f, 0.32f, 0.38f);
                RenderSettings.ambientEquatorColor = new Color(0.16f, 0.18f, 0.21f);
                RenderSettings.ambientGroundColor = new Color(0.055f, 0.05f, 0.045f);
                RenderSettings.fog = false;
                RenderSettings.skybox = null;

                if (!EditorSceneManager.SaveScene(reviewScene, ReviewScenePath))
                {
                    throw new InvalidOperationException(
                        $"Failed to save review scene: {ReviewScenePath}");
                }

                EditorBuildSettings.scenes = EditorBuildSettings.scenes
                    .Where(scene => scene.path != ReviewScenePath)
                    .ToArray();
            }
            catch (Exception exception)
            {
                operationError = exception;
                throw;
            }
            finally
            {
                bool restoredPreviousScene = true;
                if (previousActive.IsValid() && previousActive.isLoaded)
                {
                    restoredPreviousScene = SceneManager.SetActiveScene(previousActive);
                }

                bool closedReviewScene =
                    !reviewScene.IsValid() ||
                    !reviewScene.isLoaded ||
                    EditorSceneManager.CloseScene(reviewScene, true);
                if (!restoredPreviousScene || !closedReviewScene)
                {
                    string cleanupFailure =
                        "Failed to restore editor scene state after building the " +
                        "Slagfall review scene.";
                    if (operationError == null)
                    {
                        throw new InvalidOperationException(cleanupFailure);
                    }

                    Debug.LogError(cleanupFailure);
                }
            }
        }

        private static Material BuildReviewSurfaceMaterial()
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("Built-in Standard shader is unavailable.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(ReviewMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "SlagfallReviewSurface" };
                AssetDatabase.CreateAsset(material, ReviewMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_Color", new Color(0.10f, 0.105f, 0.115f, 1f));
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Glossiness", 0.12f);
            material.renderQueue = -1;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateReviewLight(
            string name,
            Color color,
            float intensity,
            Vector3 rotation,
            LightShadows shadows)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.rotation = Quaternion.Euler(rotation);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows;
        }

        private static void ConfigureTextures()
        {
            ConfigureTexture(
                TextureRoot + "/tdf_atlas_stonehold_slagfall_environment_basecolor_v001.png",
                TextureImporterType.Default,
                true,
                TextureImporterAlphaSource.None);
            ConfigureTexture(
                TextureRoot + "/tdf_atlas_stonehold_slagfall_environment_normal_v001.png",
                TextureImporterType.NormalMap,
                false,
                TextureImporterAlphaSource.None);
            ConfigureTexture(
                TextureRoot +
                "/tdf_atlas_stonehold_slagfall_environment_metallic_smoothness_v001.png",
                TextureImporterType.Default,
                false,
                TextureImporterAlphaSource.FromInput);
        }

        private static void ConfigureTexture(
            string path,
            TextureImporterType textureType,
            bool srgb,
            TextureImporterAlphaSource alphaSource)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Missing texture importer: {path}");
            }

            importer.textureType = textureType;
            importer.sRGBTexture = srgb;
            importer.alphaSource = alphaSource;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.streamingMipmapsPriority = 0;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void ConfigureModelImporter(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Missing model importer: {path}");
            }

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.useFileUnits = true;
            importer.bakeAxisConversion = true;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.isReadable = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.normalCalculationMode = ModelImporterNormalCalculationMode.AngleWeighted;
            importer.normalSmoothingAngle = 60f;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }

        private static Material BuildMaterial()
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("Built-in Standard shader is unavailable.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "tdf_mat_stonehold_slagfall_environment_atlas_v001"
                };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            Texture2D baseColor = RequireTexture(
                TextureRoot + "/tdf_atlas_stonehold_slagfall_environment_basecolor_v001.png");
            Texture2D normal = RequireTexture(
                TextureRoot + "/tdf_atlas_stonehold_slagfall_environment_normal_v001.png");
            Texture2D packed = RequireTexture(
                TextureRoot +
                "/tdf_atlas_stonehold_slagfall_environment_metallic_smoothness_v001.png");

            material.SetColor("_Color", Color.white);
            material.SetTexture("_MainTex", baseColor);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", packed);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_GlossMapScale", 1f);
            material.EnableKeyword("_METALLICGLOSSMAP");
            material.SetFloat("_OcclusionStrength", 0f);
            material.SetFloat("_Mode", 0f);
            material.renderQueue = -1;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D RequireTexture(string path)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                throw new InvalidOperationException($"Missing texture asset: {path}");
            }

            return texture;
        }

        private static void BuildPrefab(string familyId, Material material)
        {
            string baseName = $"tdf_prop_stonehold_slagfall_{familyId}_v001";
            string modelPath = ModelPath(familyId);
            Mesh[] lodMeshes = Enumerable.Range(0, LodTransitions.Length)
                .Select(index => RequireLodMesh(modelPath, index))
                .ToArray();

            GameObject root = new GameObject(baseName);
            ExecuteWithCleanup(
                () =>
                {
                    root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    root.transform.localScale = Vector3.one;
                    ApplyStaticFlags(root);

                    LODGroup lodGroup = root.AddComponent<LODGroup>();
                    List<LOD> lods = new List<LOD>(LodTransitions.Length);
                    for (int index = 0; index < LodTransitions.Length; index++)
                    {
                        GameObject child = new GameObject($"LOD{index}");
                        child.transform.SetParent(root.transform, false);
                        ApplyStaticFlags(child);

                        MeshFilter filter = child.AddComponent<MeshFilter>();
                        filter.sharedMesh = lodMeshes[index];
                        MeshRenderer renderer = child.AddComponent<MeshRenderer>();
                        renderer.sharedMaterial = material;
                        renderer.shadowCastingMode = index < 2
                            ? ShadowCastingMode.On
                            : ShadowCastingMode.Off;
                        renderer.receiveShadows = index < 3;
                        renderer.lightProbeUsage = LightProbeUsage.Off;
                        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                        renderer.motionVectorGenerationMode =
                            MotionVectorGenerationMode.ForceNoMotion;
                        renderer.allowOcclusionWhenDynamic = true;
                        lods.Add(new LOD(
                            LodTransitions[index],
                            new Renderer[] { renderer }));
                    }

                    lodGroup.fadeMode = LODFadeMode.None;
                    lodGroup.animateCrossFading = false;
                    lodGroup.SetLODs(lods.ToArray());
                    lodGroup.RecalculateBounds();

                    MeshCollider collider = root.AddComponent<MeshCollider>();
                    collider.sharedMesh = lodMeshes[3];
                    collider.convex = false;
                    collider.isTrigger = false;
                    collider.cookingOptions =
                        MeshColliderCookingOptions.CookForFasterSimulation |
                        MeshColliderCookingOptions.EnableMeshCleaning |
                        MeshColliderCookingOptions.WeldColocatedVertices |
                        MeshColliderCookingOptions.UseFastMidphase;

                    string prefabPath = PrefabRoot + "/" + baseName + ".prefab";
                    GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    if (saved == null)
                    {
                        throw new InvalidOperationException(
                            $"Failed to save prefab: {prefabPath}");
                    }
                },
                () => UnityEngine.Object.DestroyImmediate(root));
        }

        private static void ExecuteWithCleanup(Action operation, Action cleanup)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (cleanup == null)
            {
                throw new ArgumentNullException(nameof(cleanup));
            }

            try
            {
                operation();
            }
            finally
            {
                cleanup();
            }
        }

        private static Mesh RequireLodMesh(string modelPath, int lodIndex)
        {
            string token = $"LOD{lodIndex}";
            Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<Mesh>()
                .FirstOrDefault(candidate =>
                    candidate.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
            if (mesh == null)
            {
                throw new InvalidOperationException($"Missing {token} mesh in {modelPath}");
            }

            return mesh;
        }

        private static string ModelPath(string familyId)
        {
            return ModelRoot + $"/tdf_prop_stonehold_slagfall_{familyId}_v001.fbx";
        }

        private static void ApplyStaticFlags(GameObject target)
        {
            GameObjectUtility.SetStaticEditorFlags(
                target,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = path.Substring(0, path.LastIndexOf('/'));
            string leaf = path.Substring(path.LastIndexOf('/') + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
