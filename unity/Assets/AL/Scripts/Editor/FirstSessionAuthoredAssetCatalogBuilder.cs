using System;
using System.Linq;
using AL.Core;
using AL.World;
using UnityEditor;
using UnityEngine;

namespace AL.Editor
{
    public static class FirstSessionAuthoredAssetCatalogBuilder
    {
        private const string CatalogPath =
            "Assets/AL/Resources/FirstSessionAuthoredAssetCatalog.asset";
        private const string PacketRoot =
            "Assets/AL/Art/Production/FirstUserOnboarding";
        private const string HallPath = PacketRoot +
            "/Environment/Neutral_Covenant_Hall_Kit_v001.fbx";
        private const string ChampionBodyPath = PacketRoot +
            "/Characters/Champion_Vanguard_Body_v001.fbx";
        private const string ChampionArmorPath = PacketRoot +
            "/Characters/Champion_Vanguard_BasicArmor_v001.fbx";
        private const string ChampionWeaponPath = PacketRoot +
            "/Characters/Champion_Vanguard_BasicWeapon_v001.fbx";
        private const string MaleChampionBasePath = PacketRoot +
            "/Characters/Crownlands_Champion_Male_Base_Meshy6_Rigged_v001.fbx";
        private const string MaleChampionWalkPath = MaleChampionBasePath;
        private const string MaleChampionTextureRoot = PacketRoot +
            "/Characters/Crownlands_Champion_Male_Base_Meshy6_v001_textures/";
        private const string FemaleChampionBasePath = PacketRoot +
            "/Characters/Crownlands_Champion_Female_Base_Meshy6_Rigged_v001.fbx";
        private const string FemaleChampionWalkPath = FemaleChampionBasePath;
        private const string FemaleChampionTextureRoot = PacketRoot +
            "/Characters/Crownlands_Champion_Female_Base_Meshy6_v001_textures/";
        private const string GuardianPath = PacketRoot +
            "/Enemies/Covenant_Sentinel_Meshy6_Walking_v002.fbx";
        private const string GuardianTextureRoot = PacketRoot +
            "/Enemies/Covenant_Sentinel_Meshy6_v001_textures/";
        private const string PremiumEnvironmentRoot = PacketRoot + "/Environment/";
        private const string FirstSessionTerrainCatalogPath =
            "Assets/AL/StreamingAssets/GameData/al_first_session_terrain_catalog.json";

        [MenuItem("Another Life/Build/First Session Authored Asset Catalog")]
        public static void GenerateForCli()
        {
            EnsureFolder("Assets/AL", "Resources");
            FirstSessionAuthoredAssetCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FirstSessionAuthoredAssetCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<FirstSessionAuthoredAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            SetObject(serialized, "covenantHallPrefab", Load<GameObject>(HallPath));
            SetObject(serialized, "floorMaterial", LoadSubAsset<Material>(HallPath, "M_CovenantHall_Floor"));
            SetObject(serialized, "wallMaterial", LoadSubAsset<Material>(HallPath, "M_CovenantHall_Wall"));
            SetObject(serialized, "trimMaterial", LoadSubAsset<Material>(HallPath, "M_CovenantHall_Trim"));
            SetObject(serialized, "premiumFloorBaseColor", Load<Texture2D>(PremiumEnvironmentRoot + "Neutral_Covenant_Flagstone_Albedo_Meshy_v001.png"));
            SetObject(serialized, "premiumFloorNormal", Load<Texture2D>(PremiumEnvironmentRoot + "Neutral_Covenant_Flagstone_Normal_Derived_v001.png"));
            SetObject(serialized, "premiumFloorMetallic", Load<Texture2D>(PremiumEnvironmentRoot + "Neutral_Covenant_Flagstone_MetallicSmoothness_Derived_v001.png"));
            SetObject(serialized, "premiumFloorRoughness", Load<Texture2D>(PremiumEnvironmentRoot + "Neutral_Covenant_Flagstone_Roughness_Derived_v001.png"));
            SetObject(
                serialized,
                "firstSessionTerrainCatalog",
                Load<TextAsset>(FirstSessionTerrainCatalogPath));

            SerializedProperty championBases = serialized.FindProperty("championBases");
            championBases.arraySize = 2;
            SetChampionBase(
                championBases.GetArrayElementAtIndex(0),
                "male",
                MaleChampionBasePath,
                MaleChampionWalkPath,
                MaleChampionTextureRoot);
            SetChampionBase(
                championBases.GetArrayElementAtIndex(1),
                "female",
                FemaleChampionBasePath,
                FemaleChampionWalkPath,
                FemaleChampionTextureRoot);

            SetObject(serialized, "championBodyPrefab", Load<GameObject>(ChampionBodyPath));
            SetObject(serialized, "championArmorPrefab", Load<GameObject>(ChampionArmorPath));
            SetObject(serialized, "championWeaponPrefab", Load<GameObject>(ChampionWeaponPath));
            SetObject(serialized, "guardianPrefab", Load<GameObject>(GuardianPath));
            SetObject(serialized, "guardianBaseColor", Load<Texture2D>(GuardianTextureRoot + "base_color.png"));
            SetObject(serialized, "guardianNormal", Load<Texture2D>(GuardianTextureRoot + "normal.png"));
            SetObject(serialized, "guardianMetallic", Load<Texture2D>(GuardianTextureRoot + "metallic_smoothness.png"));
            SetObject(serialized, "guardianRoughness", Load<Texture2D>(GuardianTextureRoot + "roughness.png"));
            SetObject(serialized, "guardianEmission", Load<Texture2D>(GuardianTextureRoot + "emission.png"));
            SetObject(serialized, "guardianLocomotionClip", LoadFirstAnimationClip(GuardianPath));

            SerializedProperty realms = serialized.FindProperty("realmVisuals");
            realms.arraySize = 4;
            SetRealm(
                realms.GetArrayElementAtIndex(0),
                RealmId.Stonehold,
                "Stonehold");
            SetRealm(
                realms.GetArrayElementAtIndex(1),
                RealmId.Eldergrove,
                "Eldergrove");
            SetRealm(
                realms.GetArrayElementAtIndex(2),
                RealmId.Crownlands,
                "Crownlands");
            SetRealm(
                realms.GetArrayElementAtIndex(3),
                RealmId.Umbral,
                "Umbral");

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!catalog.HasRequiredAssets())
            {
                throw new InvalidOperationException(
                    "Generated first-session authored asset catalog is incomplete.");
            }

            Debug.Log("[AL-FIRST-SESSION-AUTHORED-CATALOG] generated=" + CatalogPath);
        }

        private static void SetRealm(
            SerializedProperty property,
            RealmId realm,
            string folder)
        {
            string production = "Assets/AL/Art/Generated/Architecture/" +
                                folder + "/Production/";
            property.FindPropertyRelative("realm").intValue = (int)realm;
            property.FindPropertyRelative("landmarkPrefab").objectReferenceValue =
                Load<GameObject>(production + "TownHall/Runtime/" +
                                 folder + "_TownHall_Production.prefab");
            string premiumAssetName = folder + "_CapitalHall_Meshy6_v001";
            string premiumTextureRoot = PremiumEnvironmentRoot +
                                        premiumAssetName + "_textures/";
            property.FindPropertyRelative("premiumLandmarkPrefab").objectReferenceValue =
                Load<GameObject>(PremiumEnvironmentRoot + premiumAssetName + ".fbx");
            property.FindPropertyRelative("premiumBaseColor").objectReferenceValue =
                Load<Texture2D>(premiumTextureRoot + "base_color.png");
            property.FindPropertyRelative("premiumNormal").objectReferenceValue =
                Load<Texture2D>(premiumTextureRoot + "normal.png");
            property.FindPropertyRelative("premiumMetallic").objectReferenceValue =
                Load<Texture2D>(premiumTextureRoot + "metallic_smoothness.png");
            property.FindPropertyRelative("premiumRoughness").objectReferenceValue =
                Load<Texture2D>(premiumTextureRoot + "roughness.png");
            property.FindPropertyRelative("premiumEmission").objectReferenceValue =
                Load<Texture2D>(premiumTextureRoot + "emission.png");
            property.FindPropertyRelative("panoramicSky").objectReferenceValue =
                Load<Texture2D>(PremiumEnvironmentRoot + folder +
                                "_PanoramicSky_Meshy_v001.png");
            property.FindPropertyRelative("firstSessionRealmPrefab").objectReferenceValue =
                Load<GameObject>(
                    "Assets/AL/Art/Generated/World/FirstSession/" + folder + "/" +
                    folder + "_FirstSessionAuthoredRealm.prefab");
        }

        private static void SetChampionBase(
            SerializedProperty property,
            string bodyBaseId,
            string prefabPath,
            string walkPath,
            string textureRoot)
        {
            property.FindPropertyRelative("bodyBaseId").stringValue = bodyBaseId;
            property.FindPropertyRelative("prefab").objectReferenceValue =
                Load<GameObject>(prefabPath);
            property.FindPropertyRelative("baseColor").objectReferenceValue =
                Load<Texture2D>(textureRoot + "base_color.png");
            property.FindPropertyRelative("normal").objectReferenceValue =
                Load<Texture2D>(textureRoot + "normal.png");
            property.FindPropertyRelative("metallic").objectReferenceValue =
                Load<Texture2D>(textureRoot + "metallic_smoothness.png");
            property.FindPropertyRelative("roughness").objectReferenceValue =
                Load<Texture2D>(textureRoot + "roughness.png");
            property.FindPropertyRelative("emission").objectReferenceValue =
                Load<Texture2D>(textureRoot + "emission.png");
            property.FindPropertyRelative("locomotionClip").objectReferenceValue =
                LoadFirstAnimationClip(walkPath);
        }

        private static void SetObject(
            SerializedObject serialized,
            string name,
            UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null)
            {
                throw new InvalidOperationException("Catalog field missing: " + name);
            }

            property.objectReferenceValue = value;
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException("Required authored asset missing: " + path);
            }

            return asset;
        }

        private static T LoadSubAsset<T>(string path, string name)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<T>()
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.name, name, StringComparison.Ordinal));
            if (asset == null)
            {
                throw new InvalidOperationException(
                    "Required authored sub-asset missing: " + path + "#" + name);
            }

            return asset;
        }

        private static AnimationClip LoadFirstAnimationClip(string path)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate =>
                    !candidate.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (clip == null)
            {
                throw new InvalidOperationException(
                    "Required authored animation missing: " + path);
            }

            return clip;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
