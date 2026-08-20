#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AL.Editor.Terrestrials
{
    /// <summary>
    /// Imports the Slagwhistle LOD0 FBX under the A1 art path, assigns the
    /// authored 1K color/normal/packed set to a Standard material, writes a
    /// prefab, and places a direct scene reference in the representative
    /// slice. Does not create a runtime catalog record and does not touch
    /// Player build settings.
    /// </summary>
    public static class SlagwhistlePrefabImport
    {
        public const string ArtRoot =
            "Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle";
        public const string MeshPath =
            ArtRoot + "/Meshes/tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.fbx";
        public const string ColorPath =
            ArtRoot + "/Textures/tdf_fauna_stonehold_slagwhistle_burrower_color_1k_v001.png";
        public const string NormalPath =
            ArtRoot + "/Textures/tdf_fauna_stonehold_slagwhistle_burrower_normal_1k_v001.png";
        public const string PackedPath =
            ArtRoot + "/Textures/tdf_fauna_stonehold_slagwhistle_burrower_packed_1k_v001.png";
        public const string MetallicGlossPath =
            ArtRoot + "/Materials/tdf_fauna_stonehold_slagwhistle_burrower_metallicgloss_derived_1k_v001.png";
        public const string MaterialPath =
            ArtRoot + "/Materials/M_Slagwhistle_LOD0.mat";
        public const string PrefabPath =
            ArtRoot + "/Prefabs/tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.prefab";
        public const string ScenePath =
            "Assets/AL/Scenes/Prototype/Terrestrials/SlagfallQuarryRepresentativeSlice.unity";
        public const string ReportAbsoluteRelative =
            "ArtSource/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/tdf_fauna_stonehold_slagwhistle_burrower_unity_import_report.json";

        public const string PrefabRootName = "Slagwhistle_Burrower_LOD0";
        public const string SceneInstanceName = "Slagwhistle_Burrower_LOD0";

        [MenuItem("Another Life/Terrestrials/Import Slagwhistle Prefab")]
        public static void Run()
        {
            EnsureFolders();
            ConfigureTextureImporters();
            ConfigureModelImporter();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            Texture2D color = LoadRequired<Texture2D>(ColorPath);
            Texture2D normal = LoadRequired<Texture2D>(NormalPath);
            Texture2D packed = LoadRequired<Texture2D>(PackedPath);
            Texture2D metallicGloss = CreateDerivedMetallicGloss(packed);
            Material material = CreateOrUpdateMaterial(color, normal, packed, metallicGloss);

            GameObject model = LoadRequired<GameObject>(MeshPath);
            GameObject prefab = CreatePrefab(model, material);
            CreateRepresentativeSlice(prefab);
            AssertNotInBuildSettings(ScenePath);

            ImportReport report = BuildReport(prefab, material);
            WriteReport(report);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[SlagwhistlePrefabImport] prefab=" + PrefabPath +
                " scene=" + ScenePath +
                " tris=" + report.SkinnedTriangles +
                " bones=" + report.Bones +
                " materials=" + report.RendererMaterials +
                " scale=" + report.LossyScale +
                " bounds=" + report.Bounds +
                " catalog=none buildSettings=excluded");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/AL/Art");
            EnsureFolder("Assets/AL/Art/Terrestrials");
            EnsureFolder("Assets/AL/Art/Terrestrials/Stonehold");
            EnsureFolder("Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry");
            EnsureFolder("Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna");
            EnsureFolder(ArtRoot);
            EnsureFolder(ArtRoot + "/Meshes");
            EnsureFolder(ArtRoot + "/Textures");
            EnsureFolder(ArtRoot + "/Materials");
            EnsureFolder(ArtRoot + "/Prefabs");
            EnsureFolder(ArtRoot + "/Animations");
            EnsureFolder("Assets/AL/Scenes");
            EnsureFolder("Assets/AL/Scenes/Prototype");
            EnsureFolder("Assets/AL/Scenes/Prototype/Terrestrials");
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(parent))
            {
                throw new InvalidOperationException("Cannot create folder " + assetPath);
            }

            parent = parent.Replace("\\", "/");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
        }

        private static void ConfigureTextureImporters()
        {
            ConfigureTexture(ColorPath, TextureImporterType.Default, true, false);
            ConfigureTexture(NormalPath, TextureImporterType.NormalMap, false, false);
            ConfigureTexture(PackedPath, TextureImporterType.Default, false, true);
        }

        private static void ConfigureTexture(
            string path,
            TextureImporterType type,
            bool sRGB,
            bool readable)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }

            if (importer == null)
            {
                throw new InvalidOperationException("Missing texture importer for " + path);
            }

            importer.textureType = type;
            importer.sRGBTexture = sRGB;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.isReadable = readable;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.npotScale = TextureImporterNPOTScale.ToNearest;
            importer.SaveAndReimport();
        }

        private static void ConfigureModelImporter()
        {
            var importer = AssetImporter.GetAtPath(MeshPath) as ModelImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(MeshPath, ImportAssetOptions.ForceUpdate);
                importer = AssetImporter.GetAtPath(MeshPath) as ModelImporter;
            }

            if (importer == null)
            {
                throw new InvalidOperationException("Missing model importer for " + MeshPath);
            }

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.useFileUnits = true;
            importer.bakeAxisConversion = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = true;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.preserveHierarchy = true;
            importer.addCollider = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.materialLocation = ModelImporterMaterialLocation.External;
            importer.SaveAndReimport();
        }

        private static Texture2D CreateDerivedMetallicGloss(Texture2D packed)
        {
            string absPacked = ToAbsoluteAssetPath(PackedPath);
            byte[] bytes = File.ReadAllBytes(absPacked);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!source.LoadImage(bytes, false))
            {
                throw new InvalidOperationException("Failed to decode packed texture.");
            }

            int width = source.width;
            int height = source.height;
            if (width != 1024 || height != 1024)
            {
                throw new InvalidOperationException(
                    "Packed texture is " + width + "x" + height + ", expected 1024x1024.");
            }

            Color32[] pixels = source.GetPixels32();
            var derived = new Color32[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte metallic = pixels[i].r;
                byte roughness = pixels[i].b;
                byte smoothness = (byte)(255 - roughness);
                derived[i] = new Color32(metallic, metallic, metallic, smoothness);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            texture.SetPixels32(derived);
            texture.Apply(false, false);

            string absDerived = ToAbsoluteAssetPath(MetallicGlossPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absDerived) ?? ".");
            File.WriteAllBytes(absDerived, texture.EncodeToPNG());
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(MetallicGlossPath, ImportAssetOptions.ForceUpdate);
            ConfigureTexture(MetallicGlossPath, TextureImporterType.Default, false, false);
            return LoadRequired<Texture2D>(MetallicGlossPath);
        }

        private static Material CreateOrUpdateMaterial(
            Texture2D color,
            Texture2D normal,
            Texture2D packed,
            Texture2D metallicGloss)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("Built-in Standard shader is unavailable.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.name = "M_Slagwhistle_LOD0";
            material.color = Color.white;
            material.mainTexture = color;
            material.SetTexture("_MainTex", color);
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", metallicGloss);
            material.EnableKeyword("_METALLICGLOSSMAP");
            material.SetTexture("_OcclusionMap", packed);
            material.SetFloat("_OcclusionStrength", 1f);
            material.SetFloat("_GlossMapScale", 1f);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Mode", 0f);
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.renderQueue = -1;
            material.enableInstancing = true;
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePrefab(GameObject model, Material material)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (instance == null)
            {
                instance = Object.Instantiate(model);
            }

            instance.name = PrefabRootName;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Object.DestroyImmediate(instance);
                throw new InvalidOperationException("Imported Slagwhistle FBX has no renderers.");
            }

            foreach (Renderer renderer in renderers)
            {
                var shared = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < shared.Length; i++)
                {
                    shared[i] = material;
                }

                renderer.sharedMaterials = shared;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
            if (prefab == null)
            {
                throw new InvalidOperationException("Unity did not save the Slagwhistle prefab.");
            }

            return prefab;
        }

        private static void CreateRepresentativeSlice(GameObject prefab)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = SceneInstanceName;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "ReviewGround";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            Object.DestroyImmediate(ground.GetComponent<Collider>());

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.055f, 0.045f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            camera.transform.position = new Vector3(2.4f, 1.6f, -2.8f);
            camera.transform.LookAt(new Vector3(0f, 0.28f, 0.2f));

            var keyObject = new GameObject("KeyLight");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.78f, 0.58f);
            key.intensity = 1.35f;
            key.shadows = LightShadows.Soft;
            keyObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.14f, 0.12f);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Failed to save " + ScenePath);
            }
        }

        private static void AssertNotInBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (string.Equals(scenes[i].path, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Representative slice was added to Player build settings. That is forbidden.");
                }
            }
        }

        private static ImportReport BuildReport(GameObject prefab, Material material)
        {
            var report = new ImportReport();
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                Bounds bounds = new Bounds(instance.transform.position, Vector3.zero);
                bool hasBounds = false;
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }

                    report.RendererMaterials += renderer.sharedMaterials.Length;
                    SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
                    if (skinned != null && skinned.sharedMesh != null)
                    {
                        report.SkinnedTriangles += skinned.sharedMesh.triangles.Length / 3;
                        report.Bones += skinned.bones != null ? skinned.bones.Length : 0;
                    }

                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null && skinned == null)
                    {
                        report.StaticTriangles += filter.sharedMesh.triangles.Length / 3;
                    }
                }

                report.Bounds = string.Format(
                    CultureInfo.InvariantCulture,
                    "center=({0:F4},{1:F4},{2:F4}) size=({3:F4},{4:F4},{5:F4}) minY={6:F4}",
                    bounds.center.x,
                    bounds.center.y,
                    bounds.center.z,
                    bounds.size.x,
                    bounds.size.y,
                    bounds.size.z,
                    bounds.min.y);
                report.LossyScale = instance.transform.lossyScale.ToString("F4");
                report.Forward = instance.transform.forward.ToString("F4");
                report.HasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
                report.HasScene = File.Exists(ToAbsoluteAssetPath(ScenePath));
                report.MaterialAlbedo = material.GetTexture("_MainTex") != null
                    ? AssetDatabase.GetAssetPath(material.GetTexture("_MainTex"))
                    : "missing";
                report.MaterialNormal = material.GetTexture("_BumpMap") != null
                    ? AssetDatabase.GetAssetPath(material.GetTexture("_BumpMap"))
                    : "missing";
                report.MaterialOcclusion = material.GetTexture("_OcclusionMap") != null
                    ? AssetDatabase.GetAssetPath(material.GetTexture("_OcclusionMap"))
                    : "missing";
                report.MaterialMetallicGloss = material.GetTexture("_MetallicGlossMap") != null
                    ? AssetDatabase.GetAssetPath(material.GetTexture("_MetallicGlossMap"))
                    : "missing";
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            return report;
        }

        private static void WriteReport(ImportReport report)
        {
            string abs = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", ReportAbsoluteRelative));
            Directory.CreateDirectory(Path.GetDirectoryName(abs) ?? ".");
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"task\": \"t_bb2a487f\",");
            sb.AppendLine("  \"prefab\": \"" + PrefabPath + "\",");
            sb.AppendLine("  \"scene\": \"" + ScenePath + "\",");
            sb.AppendLine("  \"hasPrefab\": " + (report.HasPrefab ? "true" : "false") + ",");
            sb.AppendLine("  \"hasScene\": " + (report.HasScene ? "true" : "false") + ",");
            sb.AppendLine("  \"skinnedTriangles\": " + report.SkinnedTriangles + ",");
            sb.AppendLine("  \"staticTriangles\": " + report.StaticTriangles + ",");
            sb.AppendLine("  \"bones\": " + report.Bones + ",");
            sb.AppendLine("  \"rendererMaterials\": " + report.RendererMaterials + ",");
            sb.AppendLine("  \"lossyScale\": \"" + Escape(report.LossyScale) + "\",");
            sb.AppendLine("  \"forward\": \"" + Escape(report.Forward) + "\",");
            sb.AppendLine("  \"bounds\": \"" + Escape(report.Bounds) + "\",");
            sb.AppendLine("  \"material\": {");
            sb.AppendLine("    \"path\": \"" + MaterialPath + "\",");
            sb.AppendLine("    \"albedo\": \"" + Escape(report.MaterialAlbedo) + "\",");
            sb.AppendLine("    \"normal\": \"" + Escape(report.MaterialNormal) + "\",");
            sb.AppendLine("    \"occlusionPacked\": \"" + Escape(report.MaterialOcclusion) + "\",");
            sb.AppendLine("    \"metallicGlossDerived\": \"" + Escape(report.MaterialMetallicGloss) + "\"");
            sb.AppendLine("  },");
            sb.AppendLine("  \"import\": {");
            sb.AppendLine("    \"unityForward\": \"+Z\",");
            sb.AppendLine("    \"scale\": \"1 unit/m\",");
            sb.AppendLine("    \"pivot\": \"ground-center (authored in FBX)\",");
            sb.AppendLine("    \"bakeAxisConversion\": true,");
            sb.AppendLine("    \"animationClips\": 0,");
            sb.AppendLine("    \"runtimeCatalogRecord\": false,");
            sb.AppendLine("    \"playerBuildSettings\": false");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            File.WriteAllText(abs, sb.ToString());
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) ||
                !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected an Assets-relative path, got " + assetPath);
            }

            return Path.GetFullPath(
                Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)));
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static T LoadRequired<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException("Missing required asset " + path);
            }

            return asset;
        }

        private sealed class ImportReport
        {
            public bool HasPrefab;
            public bool HasScene;
            public int SkinnedTriangles;
            public int StaticTriangles;
            public int Bones;
            public int RendererMaterials;
            public string LossyScale = string.Empty;
            public string Forward = string.Empty;
            public string Bounds = string.Empty;
            public string MaterialAlbedo = string.Empty;
            public string MaterialNormal = string.Empty;
            public string MaterialOcclusion = string.Empty;
            public string MaterialMetallicGloss = string.Empty;
        }
    }
}
#endif
