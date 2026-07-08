#if UNITY_EDITOR
using AL.ChampionMode.Customization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AL.Utilities
{
    public static class ALDesignAssetGenerator
    {
        private const string Root = "Assets/AL/Art/Generated";
        private const string MaterialFolder = Root + "/Materials";
        private const string CharacterPrefabFolder = Root + "/Prefabs/Characters";
        private const string VfxPrefabFolder = Root + "/Prefabs/VFX";
        private const string WeatherPrefabFolder = Root + "/Prefabs/Weather";

        [MenuItem("Another Life/Generate Design Assets")]
        public static void GenerateDesignAssets()
        {
            EnsureFolders();

            CreateMaterial("MAT_Stonehold_DarkIron", new Color(0.32f, 0.30f, 0.28f), 0.55f);
            CreateMaterial("MAT_Stonehold_ForgeGlow", new Color(1.0f, 0.42f, 0.12f), 0.15f);
            CreateMaterial("MAT_Crownlands_RoyalBlue", new Color(0.18f, 0.32f, 0.86f), 0.35f);
            CreateMaterial("MAT_Champion_Skin_Neutral", new Color(0.72f, 0.52f, 0.39f), 0.2f);
            CreateMaterial("MAT_Champion_Hair_Dark", new Color(0.08f, 0.06f, 0.04f), 0.15f);

            CreateMaterial("MAT_Eldergrove_LeafGold", new Color(0.28f, 0.72f, 0.34f), 0.25f);
            CreateMaterial("MAT_Umbral_Obsidian", new Color(0.08f, 0.05f, 0.10f), 0.65f);

            CreateChampionPrefab();
            CreateSkillVfxPrefab("VFX_Stonehold_ForgeBurst", new Color(1.0f, 0.44f, 0.08f), new Color(0.35f, 0.32f, 0.30f), 0.65f);
            CreateSkillVfxPrefab("VFX_Eldergrove_HealingBloom", new Color(0.35f, 1.0f, 0.45f), new Color(0.95f, 0.88f, 0.38f), 1.1f);
            CreateSkillVfxPrefab("VFX_Crownlands_RoyalStrike", new Color(0.2f, 0.42f, 1.0f), new Color(1.0f, 0.78f, 0.18f), 0.75f);
            CreateSkillVfxPrefab("VFX_Umbral_CurseMark", new Color(0.55f, 0.05f, 0.90f), new Color(0.95f, 0.05f, 0.12f), 0.9f);

            CreateWeatherPrefab("Weather_MountainSnowWind", new Color(0.82f, 0.92f, 1.0f), 250, 18f, 16f);
            CreateWeatherPrefab("Weather_EldergroveSunrain", new Color(0.45f, 0.95f, 0.68f), 180, 11f, 12f);
            CreateWeatherPrefab("Weather_CrownlandsClearStorm", new Color(0.55f, 0.62f, 0.82f), 140, 14f, 15f);
            CreateWeatherPrefab("Weather_UmbralAshfall", new Color(0.22f, 0.18f, 0.18f), 220, 7f, 10f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Another Life] Generated modular character, skill VFX, weather, and material starter assets.");
        }

        private static void EnsureFolders()
        {
            CreateFolderIfNeeded("Assets/AL", "Art");
            CreateFolderIfNeeded("Assets/AL/Art", "Generated");
            CreateFolderIfNeeded(Root, "Materials");
            CreateFolderIfNeeded(Root, "Prefabs");
            CreateFolderIfNeeded(Root + "/Prefabs", "Characters");
            CreateFolderIfNeeded(Root + "/Prefabs", "VFX");
            CreateFolderIfNeeded(Root + "/Prefabs", "Weather");
        }

        private static void CreateFolderIfNeeded(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static Material CreateMaterial(string name, Color color, float metallic)
        {
            var path = MaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader)
            {
                name = name,
                color = color
            };

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.35f);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void CreateChampionPrefab()
        {
            var path = CharacterPrefabFolder + "/AL_ModularChampion_Base.prefab";

            var root = new GameObject("AL_ModularChampion_Base");
            ProceduralChampionModelBuilder.EnsureModel(root);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void CreateSkillVfxPrefab(string name, Color startColor, Color endColor, float startSize)
        {
            var path = VfxPrefabFolder + "/" + name + ".prefab";
            if (File.Exists(path))
            {
                return;
            }

            var root = new GameObject(name);
            var particles = root.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.duration = 0.8f;
            main.loop = false;
            main.startLifetime = 0.55f;
            main.startSpeed = 3.4f;
            main.startSize = startSize;
            main.startColor = new ParticleSystem.MinMaxGradient(startColor, endColor);
            main.maxParticles = 80;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 42) });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.65f;

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void CreateWeatherPrefab(string name, Color color, int maxParticles, float fallSpeed, float radius)
        {
            var path = WeatherPrefabFolder + "/" + name + ".prefab";
            if (File.Exists(path))
            {
                return;
            }

            var root = new GameObject(name);
            var particles = root.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.duration = 8f;
            main.loop = true;
            main.startLifetime = 4f;
            main.startSpeed = fallSpeed;
            main.startSize = 0.06f;
            main.startColor = color;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = maxParticles / 4f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(radius, 4f, radius);

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = -fallSpeed;
            velocity.x = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

            var wind = root.AddComponent<WindZone>();
            wind.mode = WindZoneMode.Directional;
            wind.windMain = 0.35f;
            wind.windTurbulence = 0.2f;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
#endif
