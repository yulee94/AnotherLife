#if !UNITY_EDITOR
#error The admitted first-user authored environment is Editor-only.
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AL.ChampionMode.Control;
using AL.ChampionMode.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AL.Editor.Development.FirstUserGameTest
{
    [InitializeOnLoad]
    internal static class FirstUserOnboardingAuthoredEnvironmentProvider
    {
        private static readonly object Owner = new object();
        private static readonly FirstUserOnboardingAuthoredEnvironmentFactory Factory =
            new FirstUserOnboardingAuthoredEnvironmentFactory();

        static FirstUserOnboardingAuthoredEnvironmentProvider()
        {
            FirstUserOnboardingEnvironmentRegistry.TryRegister(Owner, Factory);
        }

        internal static bool Owns(
            object owner,
            IFirstUserOnboardingEnvironmentFactory factory)
        {
            return ReferenceEquals(owner, Owner) && ReferenceEquals(factory, Factory);
        }
    }

    internal sealed class FirstUserOnboardingFixedAssetInventoryVerifier :
        IFirstUserOnboardingAssetInventoryVerifier
    {
        private const string EnvironmentPath =
            "Assets/AL/Art/Production/FirstUserOnboarding/Environment/" +
            "Neutral_Covenant_Hall_Kit_v001.fbx";
        private const string ChampionPath =
            "Assets/AL/Art/Production/FirstUserOnboarding/Characters/" +
            "Champion_Vanguard_Body_v001.fbx";
        private const string ArmorPath =
            "Assets/AL/Art/Production/FirstUserOnboarding/Characters/" +
            "Champion_Vanguard_BasicArmor_v001.fbx";
        private const string WeaponPath =
            "Assets/AL/Art/Production/FirstUserOnboarding/Characters/" +
            "Champion_Vanguard_BasicWeapon_v001.fbx";
        private const string EnemyPath =
            "Assets/AL/Art/Production/FirstUserOnboarding/Enemies/" +
            "Covenant_Sentinel_Meshy6_v001.fbx";
        private const string EnemyTextureRoot =
            "Assets/AL/Art/Production/FirstUserOnboarding/Enemies/" +
            "Covenant_Sentinel_Meshy6_v001_textures/";
        private const string KingdomStructurePath =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Production/TownHall/Runtime/" +
            "Eldergrove_TownHall_Production.prefab";

        private static readonly ManifestRecord[] Manifest =
        {
            Role(
                FirstUserOnboardingAssetRole.EnvironmentModule,
                "neutral_covenant_hall_kit_v001",
                EnvironmentPath,
                "d5dfc319681fd774395f5a14c330583e",
                "6841d10ecb21cec3091b55b5a1657acf3b1e4e57c5e219d771805d8c11c915f7"),
            Role(
                FirstUserOnboardingAssetRole.ModularChampion,
                "champion_vanguard_body_v001",
                ChampionPath,
                "893b944277f8e6944af50e351c18d6bb",
                "a9a3decf76c090934d791188f06d6ab12a7c87d1df7ced861b2a12f8d3a8642a"),
            Role(
                FirstUserOnboardingAssetRole.SelectedBasicArmor,
                "champion_vanguard_basic_armor_v001",
                ArmorPath,
                "e203a6ad644712f44842f58c848034bb",
                "a7ee33907246ad58807e4937585774023da75a095ec83684671fe8e2b4121518"),
            Role(
                FirstUserOnboardingAssetRole.SelectedBasicWeapon,
                "champion_vanguard_basic_weapon_v001",
                WeaponPath,
                "ab7419a768b5d304f9d053c6d733b028",
                "6b7ffbb2739e10a5bef009ce7ecf0dd8a5424ab942b93e07e9bbd85d2a3a473c"),
            Role(
                FirstUserOnboardingAssetRole.CommonEnemy,
                "covenant_sentinel_meshy6_v001",
                EnemyPath,
                "3e37742af1236074b9badc6912efd01b",
                "b5bab61e821875ae5c6ce3d12fc03c14dba78d4081df398b4b03e6791bccedd3"),
            Role(
                FirstUserOnboardingAssetRole.KingdomBaseStructure,
                "eldergrove_town_hall_production",
                KingdomStructurePath,
                "64552283a5bd040f08d3f41d6a94ca5a",
                "2d91fc0a7cf825facabe995ef9d04204b0cc1471ad19fded3093f9602699b16e"),
            Role(
                FirstUserOnboardingAssetRole.FloorMaterial,
                "covenant_hall_floor_v001",
                EnvironmentPath,
                "d5dfc319681fd774395f5a14c330583e",
                "6841d10ecb21cec3091b55b5a1657acf3b1e4e57c5e219d771805d8c11c915f7",
                "M_CovenantHall_Floor"),
            Role(
                FirstUserOnboardingAssetRole.WallMaterial,
                "covenant_hall_wall_v001",
                EnvironmentPath,
                "d5dfc319681fd774395f5a14c330583e",
                "6841d10ecb21cec3091b55b5a1657acf3b1e4e57c5e219d771805d8c11c915f7",
                "M_CovenantHall_Wall"),
            Role(
                FirstUserOnboardingAssetRole.TrimMaterial,
                "covenant_hall_trim_v001",
                EnvironmentPath,
                "d5dfc319681fd774395f5a14c330583e",
                "6841d10ecb21cec3091b55b5a1657acf3b1e4e57c5e219d771805d8c11c915f7",
                "M_CovenantHall_Trim"),
            Dependency(
                "sentinel_base_color_1024",
                EnemyTextureRoot + "base_color.png",
                "56a36ff708765c24abb40dbb2f4ce759",
                "0ef23ea82fdd2678982b97e3d2c00acbac168c4091fe6eb615e85c1c5fcce40d"),
            Dependency(
                "sentinel_metallic_1024",
                EnemyTextureRoot + "metallic.png",
                "ac69d09caa9750548a23abe1c6795487",
                "24d2cabfe0da9591cde1db194a9024d55d17484c1c34186a766b7c98e9042493"),
            Dependency(
                "sentinel_roughness_1024",
                EnemyTextureRoot + "roughness.png",
                "d633ef1a94d15f544b7a9e3f96b6c75c",
                "cdd570b1518c40223c67ab4f3cb488e7f50fef30339cd3c3ba03dc90f94c4b9d"),
            Dependency(
                "sentinel_normal_1024",
                EnemyTextureRoot + "normal.png",
                "2f562600cf131a44b8d8a0a870b61b73",
                "fa7927d382e302aca78b82dbd90ad77e8743e93b02341432e78ed49c1f15d642"),
            Dependency(
                "sentinel_emission_1024",
                EnemyTextureRoot + "emission.png",
                "95482f2597decd44fb4917e59465ff2c",
                "f43da6c6ca8afc3d2b37a07a2410a7011972df01c189de43c563ed5360565eec")
        };

        internal static readonly FirstUserOnboardingFixedAssetInventoryVerifier Instance =
            new FirstUserOnboardingFixedAssetInventoryVerifier();

        private readonly Dictionary<FirstUserOnboardingAssetRole, ManifestRecord> _roles;

        private FirstUserOnboardingFixedAssetInventoryVerifier()
        {
            _roles = Manifest
                .Where(record => record.Role != FirstUserOnboardingAssetRole.Invalid)
                .ToDictionary(record => record.Role);
            InventoryFingerprint = ComputeInventoryFingerprint();
        }

        public string InventoryFingerprint { get; }

        internal bool TryVerifyManifest(out string diagnostic)
        {
            for (int index = 0; index < Manifest.Length; index++)
            {
                ManifestRecord record = Manifest[index];
                string currentGuid = AssetDatabase.AssetPathToGUID(record.Path);
                if (!string.Equals(currentGuid, record.Guid, StringComparison.Ordinal))
                {
                    diagnostic = "asset_guid_drift:" + record.Id;
                    return false;
                }

                string absolutePath = ToAbsolutePath(record.Path);
                if (!File.Exists(absolutePath) ||
                    !string.Equals(
                        ComputeFileSha256(absolutePath),
                        record.Sha256,
                        StringComparison.Ordinal))
                {
                    diagnostic = "asset_sha_drift:" + record.Id;
                    return false;
                }

                Object loaded = string.IsNullOrEmpty(record.SubAssetName)
                    ? AssetDatabase.LoadMainAssetAtPath(record.Path)
                    : LoadSubAsset<Object>(record.Path, record.SubAssetName);
                if (loaded == null)
                {
                    diagnostic = "asset_missing:" + record.Id;
                    return false;
                }
            }

            diagnostic = string.Empty;
            return true;
        }

        public bool TryVerifyExactAsset(
            FirstUserOnboardingAssetRole role,
            string assetId,
            Object sourceAsset,
            Object runtimeInstance,
            out string diagnostic)
        {
            if (!TryVerifyManifest(out diagnostic) ||
                !_roles.TryGetValue(role, out ManifestRecord record) ||
                !string.Equals(assetId, record.Id, StringComparison.Ordinal) ||
                sourceAsset == null || runtimeInstance == null ||
                !string.Equals(
                    AssetDatabase.GetAssetPath(sourceAsset),
                    record.Path,
                    StringComparison.Ordinal) ||
                (!string.IsNullOrEmpty(record.SubAssetName) &&
                 !string.Equals(sourceAsset.name, record.SubAssetName, StringComparison.Ordinal)))
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "exact_asset_rejected:" + role
                    : diagnostic;
                return false;
            }

            if (sourceAsset is Material)
            {
                bool materialMatches = ReferenceEquals(sourceAsset, runtimeInstance);
                diagnostic = materialMatches ? string.Empty : "material_instance_drift:" + role;
                return materialMatches;
            }

            if (!(sourceAsset is GameObject) || !(runtimeInstance is GameObject runtimeRoot) ||
                !runtimeRoot.scene.IsValid())
            {
                diagnostic = "runtime_asset_invalid:" + role;
                return false;
            }

            Object corresponding =
                PrefabUtility.GetCorrespondingObjectFromOriginalSource(runtimeRoot) ??
                PrefabUtility.GetCorrespondingObjectFromSource(runtimeRoot);
            bool matches = corresponding != null &&
                           string.Equals(
                               AssetDatabase.GetAssetPath(corresponding),
                               record.Path,
                               StringComparison.Ordinal);
            diagnostic = matches ? string.Empty : "runtime_source_drift:" + role;
            return matches;
        }

        public bool TryVerifyModularKit(
            IFirstUserOnboardingEnvironmentLease lease,
            out string diagnostic)
        {
            GameObject[] modules =
            {
                lease.FloorModuleRoot,
                lease.WallModuleRoot,
                lease.InnerCornerModuleRoot,
                lease.OuterCornerModuleRoot,
                lease.DoorwayModuleRoot,
                lease.CeilingBeamModuleRoot,
                lease.TrimModuleRoot,
                lease.BrazierPropRoot,
                lease.BannerStandPropRoot,
                lease.CrateBarrelPropRoot
            };
            bool valid = modules.All(module =>
                module != null && module.activeInHierarchy &&
                module.GetComponentsInChildren<MeshFilter>(true)
                    .Any(filter => filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0));
            diagnostic = valid ? string.Empty : "modular_kit_incomplete";
            return valid;
        }

        public bool TryVerifyChampionRigAndLoadout(
            IFirstUserOnboardingEnvironmentLease lease,
            out string diagnostic)
        {
            Animator animator = lease.ModularChampionRoot == null
                ? null
                : lease.ModularChampionRoot.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                diagnostic = "champion_animator_missing";
                return false;
            }

            Transform[] championTransforms = lease.ModularChampionRoot
                .GetComponentsInChildren<Transform>(true);
            SkinnedMeshRenderer[] skinned = lease.ModularChampionRoot
                .GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (!championTransforms.Any(transform => string.Equals(
                    transform.name,
                    "Champion_Vanguard_Rig",
                    StringComparison.Ordinal)) ||
                skinned.Length == 0 ||
                skinned.Any(renderer =>
                    renderer.sharedMesh == null || renderer.bones == null ||
                    renderer.bones.Length == 0))
            {
                diagnostic = "champion_skinned_rig_invalid";
                return false;
            }

            bool valid = lease.SelectedArmorRoot != null && lease.SelectedWeaponRoot != null &&
                         lease.SelectedArmorRoot.transform.IsChildOf(
                             lease.PlayerChampion.transform) &&
                         lease.SelectedWeaponRoot.transform.IsChildOf(
                             lease.PlayerChampion.transform) &&
                         lease.SelectedArmorRoot.GetComponentsInChildren<Renderer>(true).Length > 0 &&
                         lease.SelectedWeaponRoot.GetComponentsInChildren<Renderer>(true).Length > 0;
            diagnostic = valid ? string.Empty : "champion_loadout_invalid";
            return valid;
        }

        public bool TryVerifyMechanicsEncounterSlot(
            IFirstUserOnboardingEnvironmentLease lease,
            out string diagnostic)
        {
            bool valid = lease.EnemyRoot != null && lease.EnemyEncounter != null &&
                         lease.EnemyCandidateKind ==
                         FirstUserOnboardingEnemyCandidateKind.Normal &&
                         lease.EncounterMode ==
                         FirstUserOnboardingEncounterMode.BoundedMechanicsEncounter &&
                         lease.EnemyRoot.GetComponentsInChildren<MonoBehaviour>(true).Length == 0 &&
                         lease.EnemyRoot.GetComponentsInChildren<Renderer>(true).Length > 0;
            diagnostic = valid ? string.Empty : "mechanics_encounter_invalid";
            return valid;
        }

        public bool TryVerifyLockedKingdomStructureSlot(
            IFirstUserOnboardingEnvironmentLease lease,
            out string diagnostic)
        {
            bool valid = lease.KingdomStructureMode ==
                         FirstUserOnboardingKingdomStructureMode.LockedPreviewOnly &&
                         lease.KingdomStructureRoot != null &&
                         lease.KingdomStructureRoot.activeInHierarchy &&
                         lease.KingdomStructureRoot
                             .GetComponentsInChildren<Renderer>(true).Length > 0;
            diagnostic = valid ? string.Empty : "locked_kingdom_structure_invalid";
            return valid;
        }

        public bool TryVerifyCharacterControllerSafeTraversal(
            IFirstUserOnboardingEnvironmentLease lease,
            out string diagnostic)
        {
            CharacterController controller = lease.PlayerController;
            Collider floor = lease.FloorModuleRoot == null
                ? null
                : lease.FloorModuleRoot.GetComponentInChildren<Collider>(true);
            Vector3 start = lease.MovementProofStart;
            Vector3 end = lease.MovementProofEnd;
            if (controller == null || !controller.enabled)
            {
                diagnostic = "character_controller_missing_or_disabled";
                return false;
            }

            if (floor == null || !floor.enabled || floor.isTrigger)
            {
                diagnostic = "walkable_floor_collider_invalid";
                return false;
            }

            bool valid = lease.WalkableBounds.Contains(start) &&
                         lease.WalkableBounds.Contains(end) &&
                         floor.bounds.min.x <= Math.Min(start.x, end.x) - controller.radius &&
                         floor.bounds.max.x >= Math.Max(start.x, end.x) + controller.radius &&
                         floor.bounds.min.z <= Math.Min(start.z, end.z) - controller.radius &&
                         floor.bounds.max.z >= Math.Max(start.z, end.z) + controller.radius;
            diagnostic = valid
                ? string.Empty
                : "character_controller_route_outside_floor:" + floor.bounds.ToString("F3");
            return valid;
        }

        public bool TryVerifyRuntimeComponentInventory(
            IFirstUserOnboardingEnvironmentLease lease,
            out string diagnostic)
        {
            if (lease.OwnedRoot == null)
            {
                diagnostic = "owned_root_missing";
                return false;
            }

            Component[] components = lease.OwnedRoot.GetComponentsInChildren<Component>(true);
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                Type type = component == null ? null : component.GetType();
                if (type == null || IsAllowedComponent(type))
                {
                    continue;
                }

                diagnostic = "runtime_component_not_admitted:" + type.FullName;
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        public bool TryVerifyBuiltInPbrMaterial(
            FirstUserOnboardingAssetRole role,
            Material material,
            out string diagnostic)
        {
            bool valid = (role == FirstUserOnboardingAssetRole.FloorMaterial ||
                          role == FirstUserOnboardingAssetRole.WallMaterial ||
                          role == FirstUserOnboardingAssetRole.TrimMaterial) &&
                         material != null && material.shader != null &&
                         string.Equals(material.shader.name, "Standard", StringComparison.Ordinal) &&
                         material.HasProperty("_Color") &&
                         material.HasProperty("_Metallic") &&
                         material.HasProperty("_Glossiness");
            diagnostic = valid ? string.Empty : "built_in_pbr_material_invalid:" + role;
            return valid;
        }

        internal static T LoadSubAsset<T>(string path, string name) where T : Object
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<T>()
                .SingleOrDefault(asset => string.Equals(
                    asset.name,
                    name,
                    StringComparison.Ordinal));
        }

        internal static string AssetId(FirstUserOnboardingAssetRole role)
        {
            return Manifest.Single(record => record.Role == role).Id;
        }

        internal static string AssetPath(FirstUserOnboardingAssetRole role)
        {
            return Manifest.Single(record => record.Role == role).Path;
        }

        private static bool IsAllowedComponent(Type type)
        {
            if (type == typeof(Transform) || type == typeof(MeshFilter) ||
                type == typeof(MeshRenderer) || type == typeof(SkinnedMeshRenderer) ||
                type == typeof(Animator) || type == typeof(CharacterController) ||
                type == typeof(Camera) || type == typeof(AudioListener) ||
                type == typeof(BoxCollider) || type == typeof(CapsuleCollider) ||
                type == typeof(MeshCollider) || type == typeof(Light) ||
                type == typeof(LODGroup) || type == typeof(ChampionController) ||
                type == typeof(FirstUserGameTestDestinationMarker) ||
                type == typeof(FirstUserGameTestTutorialPresenter) ||
                type == typeof(ChampionMoveButton) || type == typeof(RectTransform) ||
                type == typeof(Canvas) || type == typeof(CanvasRenderer) ||
                type == typeof(CanvasScaler) || type == typeof(GraphicRaycaster) ||
                type == typeof(Text) || type == typeof(Image) || type == typeof(Button))
            {
                return true;
            }

            string fullName = type.FullName ?? string.Empty;
            return string.Equals(
                       fullName,
                       "AL.Kingdom.Visuals.Architecture.KingdomBuildingLevelModel",
                       StringComparison.Ordinal) ||
                   string.Equals(
                       fullName,
                       "AL.ChampionMode.Skills.SkillCaster",
                       StringComparison.Ordinal) ||
                   string.Equals(
                       fullName,
                       "AL.ChampionMode.Control.ChampionCombat",
                       StringComparison.Ordinal);
        }

        private static ManifestRecord Role(
            FirstUserOnboardingAssetRole role,
            string id,
            string path,
            string guid,
            string sha256,
            string subAssetName = "")
        {
            return new ManifestRecord(role, id, path, guid, sha256, subAssetName);
        }

        private static ManifestRecord Dependency(
            string id,
            string path,
            string guid,
            string sha256)
        {
            return new ManifestRecord(
                FirstUserOnboardingAssetRole.Invalid,
                id,
                path,
                guid,
                sha256,
                string.Empty);
        }

        private static string ComputeInventoryFingerprint()
        {
            var builder = new StringBuilder();
            for (int index = 0; index < Manifest.Length; index++)
            {
                ManifestRecord record = Manifest[index];
                builder.Append((int)record.Role).Append('|')
                    .Append(record.Id).Append('|')
                    .Append(record.Path).Append('|')
                    .Append(record.Guid).Append('|')
                    .Append(record.Sha256).Append('|')
                    .Append(record.SubAssetName).Append('\n');
            }

            using (SHA256 sha = SHA256.Create())
            {
                return ToLowerHex(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
            }
        }

        private static string ComputeFileSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                return ToLowerHex(sha.ComputeHash(stream));
            }
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++)
            {
                builder.Append(bytes[index].ToString("x2"));
            }

            return builder.ToString();
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private sealed class ManifestRecord
        {
            internal ManifestRecord(
                FirstUserOnboardingAssetRole role,
                string id,
                string path,
                string guid,
                string sha256,
                string subAssetName)
            {
                Role = role;
                Id = id;
                Path = path;
                Guid = guid;
                Sha256 = sha256;
                SubAssetName = subAssetName;
            }

            internal FirstUserOnboardingAssetRole Role { get; }
            internal string Id { get; }
            internal string Path { get; }
            internal string Guid { get; }
            internal string Sha256 { get; }
            internal string SubAssetName { get; }
        }
    }

    internal sealed class FirstUserOnboardingAuthoredEnvironmentFactory :
        IFirstUserOnboardingEnvironmentFactory
    {
        private const string ModuleId = "first_user_neutral_covenant_hall_v001";

        public bool TryCreate(
            FirstUserOnboardingEnvironmentRequest request,
            out IFirstUserOnboardingEnvironmentLease lease,
            out string diagnostic)
        {
            lease = null;
            diagnostic = string.Empty;
            FirstUserOnboardingFixedAssetInventoryVerifier verifier =
                FirstUserOnboardingFixedAssetInventoryVerifier.Instance;
            if (!request.Scene.IsValid() || !request.Scene.isLoaded ||
                request.AllowUnitTestDouble ||
                !ReferenceEquals(request.AssetInventoryVerifier, verifier) ||
                !verifier.TryVerifyManifest(out diagnostic))
            {
                if (string.IsNullOrEmpty(diagnostic))
                {
                    diagnostic = "authored_environment_request_rejected";
                }

                return false;
            }

            FirstUserOnboardingAuthoredEnvironmentLease created = null;
            try
            {
                created = Build(request, verifier);
                lease = created;
                return true;
            }
            catch (Exception exception)
            {
                created?.Dispose();
                diagnostic = "authored_environment_build_failed:" + exception.GetType().Name;
                return false;
            }
        }

        private static FirstUserOnboardingAuthoredEnvironmentLease Build(
            FirstUserOnboardingEnvironmentRequest request,
            FirstUserOnboardingFixedAssetInventoryVerifier verifier)
        {
            var root = new GameObject("FirstUserAuthoredEnvironmentRoot");
            SceneManager.MoveGameObjectToScene(root, request.Scene);

            GameObject environmentSource = LoadGameObject(
                FirstUserOnboardingAssetRole.EnvironmentModule);
            GameObject championSource = LoadGameObject(
                FirstUserOnboardingAssetRole.ModularChampion);
            GameObject armorSource = LoadGameObject(
                FirstUserOnboardingAssetRole.SelectedBasicArmor);
            GameObject weaponSource = LoadGameObject(
                FirstUserOnboardingAssetRole.SelectedBasicWeapon);
            GameObject enemySource = LoadGameObject(
                FirstUserOnboardingAssetRole.CommonEnemy);
            GameObject structureSource = LoadGameObject(
                FirstUserOnboardingAssetRole.KingdomBaseStructure);

            Material floorMaterial = LoadMaterial(
                FirstUserOnboardingAssetRole.FloorMaterial,
                "M_CovenantHall_Floor");
            Material wallMaterial = LoadMaterial(
                FirstUserOnboardingAssetRole.WallMaterial,
                "M_CovenantHall_Wall");
            Material trimMaterial = LoadMaterial(
                FirstUserOnboardingAssetRole.TrimMaterial,
                "M_CovenantHall_Trim");

            GameObject environment = Instantiate(environmentSource, root.transform, "NeutralEnvironment");
            environment.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject floor = FindRequired(environment, "FloorModule");
            GameObject wall = FindRequired(environment, "WallModule");
            GameObject innerCorner = FindRequired(environment, "InnerCornerModule");
            GameObject outerCorner = FindRequired(environment, "OuterCornerModule");
            GameObject doorway = FindRequired(environment, "DoorwayModule");
            GameObject beam = FindRequired(environment, "CeilingBeamModule");
            GameObject trim = FindRequired(environment, "TrimModule");
            GameObject brazier = FindRequired(environment, "BrazierProp");
            GameObject banner = FindRequired(environment, "BannerStandProp");
            GameObject crates = FindRequired(environment, "CrateBarrelProp");

            AssignMaterial(floor, floorMaterial);
            AssignMaterial(wall, wallMaterial);
            AssignMaterial(innerCorner, wallMaterial);
            AssignMaterial(outerCorner, wallMaterial);
            AssignMaterial(doorway, wallMaterial);
            AssignMaterial(beam, trimMaterial);
            AssignMaterial(trim, trimMaterial);
            AssignMaterial(brazier, trimMaterial);
            AssignMaterial(banner, floorMaterial);
            AssignMaterial(crates, wallMaterial);

            AddSolidBoxCollider(floor);
            AddSolidBoxCollider(wall);
            AddSolidBoxCollider(innerCorner);
            AddSolidBoxCollider(outerCorner);
            AddSolidBoxCollider(doorway);

            Transform props = environment.transform;

            Transform sceneAnchor = Child(root.transform, "SceneAnchor", Vector3.zero);
            Transform spawnAnchor = Child(root.transform, "SpawnAnchor", new Vector3(0f, 0f, -3f));
            Transform cameraAnchor = Child(root.transform, "CameraAnchor", new Vector3(0f, 2.8f, -6.2f));
            Transform cameraTarget = Child(root.transform, "CameraTarget", new Vector3(0f, 1.15f, -0.5f));
            Transform omenAnchor = Child(root.transform, "OmenAnchor", new Vector3(-2.4f, 0f, 2.2f));
            Transform lightingHook = Child(environment.transform, "LightingHook", new Vector3(0f, 3f, 0f));
            Transform presentationHook = Child(root.transform, "PresentationHook", new Vector3(0f, 1.5f, 4.5f));
            Transform enemySpawn = Child(root.transform, "EnemySpawn", new Vector3(0f, 0f, 2.25f));

            var lightObject = new GameObject("CovenantHallKeyLight");
            lightObject.transform.SetParent(lightingHook, false);
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light keyLight = lightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(0.82f, 0.89f, 1f);
            keyLight.intensity = 1.1f;
            keyLight.shadows = LightShadows.Soft;

            var player = new GameObject("PlayerChampion");
            player.transform.SetParent(root.transform, false);
            player.transform.position = spawnAnchor.position;
            CharacterController characterController = player.AddComponent<CharacterController>();
            characterController.center = new Vector3(0f, 1f, 0f);
            characterController.height = 2f;
            characterController.radius = 0.34f;
            characterController.stepOffset = 0.30f;
            ChampionController championController = player.AddComponent<ChampionController>();
            GameObject champion = Instantiate(championSource, player.transform, "ModularChampion");
            champion.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            if (champion.GetComponentInChildren<Animator>(true) == null)
            {
                champion.AddComponent<Animator>();
            }
            GameObject armor = Instantiate(armorSource, player.transform, "SelectedBasicArmor");
            GameObject weapon = Instantiate(weaponSource, player.transform, "SelectedBasicWeapon");
            armor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            weapon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var cameraObject = new GameObject("PrimaryCamera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = cameraAnchor.position;
            cameraObject.transform.LookAt(cameraTarget.position);
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 120f;
            cameraObject.AddComponent<AudioListener>();

            GameObject enemy = Instantiate(enemySource, root.transform, "CovenantSentinel");
            enemy.transform.position = enemySpawn.position;
            enemy.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var enemyCollider = enemy.AddComponent<CapsuleCollider>();
            enemyCollider.direction = 2;
            enemyCollider.center = new Vector3(0f, 0f, -0.95f);
            enemyCollider.height = 1.7f;
            enemyCollider.radius = 0.34f;
            var runtimeMaterials = new List<Material>();
            ApplySentinelPbr(enemy, runtimeMaterials);
            var encounter = new FirstUserOnboardingAuthoredEnemyEncounter(
                request.SessionId,
                request.Generation,
                FirstUserOnboardingFixedAssetInventoryVerifier.AssetId(
                    FirstUserOnboardingAssetRole.CommonEnemy),
                enemy,
                initialHitPoints: 3);

            GameObject structure = Instantiate(
                structureSource,
                root.transform,
                "LockedKingdomStructurePreview");
            structure.transform.localPosition = new Vector3(0f, 0f, 9f);
            structure.transform.localScale = Vector3.one * 0.22f;

            Physics.SyncTransforms();

            return new FirstUserOnboardingAuthoredEnvironmentLease(
                request,
                verifier.InventoryFingerprint,
                root,
                environmentSource,
                environment,
                sceneAnchor,
                spawnAnchor,
                new Bounds(new Vector3(0f, 1.5f, 0f), new Vector3(8f, 3f, 12f)),
                spawnAnchor.position,
                new Vector3(0f, 0f, -0.5f),
                new Bounds(new Vector3(0f, 1f, 1.5f), new Vector3(4f, 2f, 5f)),
                characterController,
                championController,
                camera,
                cameraAnchor,
                cameraTarget,
                omenAnchor,
                lightingHook,
                presentationHook,
                champion,
                championSource,
                armor,
                armorSource,
                weapon,
                weaponSource,
                enemy,
                enemySource,
                encounter,
                enemySpawn,
                structure,
                structureSource,
                floorMaterial,
                wallMaterial,
                trimMaterial,
                props,
                floor,
                wall,
                innerCorner,
                outerCorner,
                doorway,
                beam,
                trim,
                brazier,
                banner,
                crates,
                runtimeMaterials);
        }

        private static GameObject LoadGameObject(FirstUserOnboardingAssetRole role)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                FirstUserOnboardingFixedAssetInventoryVerifier.AssetPath(role));
            if (asset == null)
            {
                throw new InvalidOperationException("Missing admitted GameObject: " + role);
            }

            return asset;
        }

        private static Material LoadMaterial(
            FirstUserOnboardingAssetRole role,
            string name)
        {
            Material asset = FirstUserOnboardingFixedAssetInventoryVerifier
                .LoadSubAsset<Material>(
                    FirstUserOnboardingFixedAssetInventoryVerifier.AssetPath(role),
                    name);
            if (asset == null)
            {
                throw new InvalidOperationException("Missing admitted material: " + role);
            }

            return asset;
        }

        private static GameObject Instantiate(
            GameObject source,
            Transform parent,
            string name)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(
                source,
                parent.gameObject.scene) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    "Could not instantiate admitted source asset: " + source.name);
            }

            instance.transform.SetParent(parent, false);
            instance.name = name;
            instance.SetActive(true);
            return instance;
        }

        private static GameObject FindRequired(GameObject root, string name)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            Transform match = transforms.SingleOrDefault(candidate =>
                string.Equals(candidate.name, name, StringComparison.Ordinal));
            if (match == null)
            {
                throw new InvalidOperationException("Missing authored module: " + name);
            }

            return match.gameObject;
        }

        private static void AssignMaterial(GameObject root, Material material)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    materials[index] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static void AddSolidBoxCollider(GameObject root)
        {
            if (root.GetComponentsInChildren<Collider>(true).Length != 0)
            {
                return;
            }

            BoxCollider collider = root.AddComponent<BoxCollider>();
            Bounds bounds = CalculateLocalRendererBounds(root);
            collider.center = bounds.center;
            collider.size = bounds.size;
            collider.isTrigger = false;
        }

        private static Bounds CalculateLocalRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException("No renderer for collider: " + root.name);
            }

            Bounds world = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                world.Encapsulate(renderers[index].bounds);
            }

            return new Bounds(
                root.transform.InverseTransformPoint(world.center),
                root.transform.InverseTransformVector(world.size));
        }

        private static Transform Child(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.position = position;
            return child.transform;
        }

        private static void ApplySentinelPbr(
            GameObject enemy,
            ICollection<Material> runtimeMaterials)
        {
            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(
                FirstUserOnboardingFixedAssetInventoryVerifier.AssetPath(
                    FirstUserOnboardingAssetRole.CommonEnemy)
                    .Replace(
                        "Covenant_Sentinel_Meshy6_v001.fbx",
                        "Covenant_Sentinel_Meshy6_v001_textures/base_color.png"));
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(
                FirstUserOnboardingFixedAssetInventoryVerifier.AssetPath(
                    FirstUserOnboardingAssetRole.CommonEnemy)
                    .Replace(
                        "Covenant_Sentinel_Meshy6_v001.fbx",
                        "Covenant_Sentinel_Meshy6_v001_textures/normal.png"));
            Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(
                FirstUserOnboardingFixedAssetInventoryVerifier.AssetPath(
                    FirstUserOnboardingAssetRole.CommonEnemy)
                    .Replace(
                        "Covenant_Sentinel_Meshy6_v001.fbx",
                        "Covenant_Sentinel_Meshy6_v001_textures/metallic.png"));
            Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(
                FirstUserOnboardingFixedAssetInventoryVerifier.AssetPath(
                    FirstUserOnboardingAssetRole.CommonEnemy)
                    .Replace(
                        "Covenant_Sentinel_Meshy6_v001.fbx",
                        "Covenant_Sentinel_Meshy6_v001_textures/emission.png"));

            Shader shader = Shader.Find("Standard");
            if (shader == null || baseColor == null || normal == null ||
                metallic == null || emission == null)
            {
                throw new InvalidOperationException("Sentinel PBR dependencies unavailable.");
            }

            foreach (Renderer renderer in enemy.GetComponentsInChildren<Renderer>(true))
            {
                var material = new Material(shader)
                {
                    name = "CovenantSentinelPbr_Runtime"
                };
                material.SetTexture("_MainTex", baseColor);
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap", metallic);
                material.EnableKeyword("_METALLICGLOSSMAP");
                material.SetFloat("_Metallic", 0.65f);
                material.SetFloat("_Glossiness", 0.52f);
                material.SetTexture("_EmissionMap", emission);
                material.SetColor("_EmissionColor", Color.white * 0.55f);
                material.EnableKeyword("_EMISSION");
                renderer.sharedMaterial = material;
                runtimeMaterials.Add(material);
            }
        }
    }

    internal sealed class FirstUserOnboardingAuthoredEnvironmentLease :
        IFirstUserOnboardingEnvironmentLease
    {
        private readonly GameObject _ownedRoot;
        private readonly IReadOnlyList<Material> _runtimeMaterials;

        internal FirstUserOnboardingAuthoredEnvironmentLease(
            FirstUserOnboardingEnvironmentRequest request,
            string inventoryFingerprint,
            GameObject root,
            Object environmentSource,
            GameObject environment,
            Transform sceneAnchor,
            Transform spawnAnchor,
            Bounds walkableBounds,
            Vector3 movementStart,
            Vector3 movementEnd,
            Bounds attackSafeBounds,
            CharacterController playerController,
            ChampionController playerChampion,
            Camera primaryCamera,
            Transform cameraAnchor,
            Transform cameraTarget,
            Transform omenAnchor,
            Transform lightingHook,
            Transform presentationHook,
            GameObject champion,
            Object championSource,
            GameObject armor,
            Object armorSource,
            GameObject weapon,
            Object weaponSource,
            GameObject enemy,
            Object enemySource,
            IFirstUserOnboardingEnemyEncounter encounter,
            Transform enemySpawn,
            GameObject structure,
            Object structureSource,
            Material floorMaterial,
            Material wallMaterial,
            Material trimMaterial,
            Transform props,
            GameObject floor,
            GameObject wall,
            GameObject innerCorner,
            GameObject outerCorner,
            GameObject doorway,
            GameObject beam,
            GameObject trim,
            GameObject brazier,
            GameObject banner,
            GameObject crates,
            IReadOnlyList<Material> runtimeMaterials)
        {
            SessionId = request.SessionId;
            Generation = request.Generation;
            AssetInventoryFingerprint = inventoryFingerprint;
            _ownedRoot = root;
            EnvironmentModuleSourceAsset = environmentSource;
            NeutralEnvironmentRoot = environment;
            SceneAnchor = sceneAnchor;
            SpawnAnchor = spawnAnchor;
            WalkableBounds = walkableBounds;
            MovementProofStart = movementStart;
            MovementProofEnd = movementEnd;
            AttackSafeBounds = attackSafeBounds;
            PlayerController = playerController;
            PlayerChampion = playerChampion;
            PrimaryCamera = primaryCamera;
            PrimaryCameraAnchor = cameraAnchor;
            PrimaryCameraTarget = cameraTarget;
            OmenAnchor = omenAnchor;
            LightingHook = lightingHook;
            PresentationHook = presentationHook;
            ModularChampionRoot = champion;
            ChampionSourceAsset = championSource;
            SelectedArmorRoot = armor;
            ArmorSourceAsset = armorSource;
            SelectedWeaponRoot = weapon;
            WeaponSourceAsset = weaponSource;
            EnemyRoot = enemy;
            EnemySourceAsset = enemySource;
            EnemyEncounter = encounter;
            EnemySpawnAnchor = enemySpawn;
            KingdomStructureRoot = structure;
            KingdomStructureSourceAsset = structureSource;
            FloorMaterial = floorMaterial;
            WallMaterial = wallMaterial;
            TrimMaterial = trimMaterial;
            PropsRoot = props;
            FloorModuleRoot = floor;
            WallModuleRoot = wall;
            InnerCornerModuleRoot = innerCorner;
            OuterCornerModuleRoot = outerCorner;
            DoorwayModuleRoot = doorway;
            CeilingBeamModuleRoot = beam;
            TrimModuleRoot = trim;
            BrazierPropRoot = brazier;
            BannerStandPropRoot = banner;
            CrateBarrelPropRoot = crates;
            _runtimeMaterials = runtimeMaterials;
        }

        public string SessionId { get; }
        public int Generation { get; }
        public string ModuleId => "first_user_neutral_covenant_hall_v001";
        public string ContentFingerprint =>
            "6841d10ecb21cec3091b55b5a1657acf3b1e4e57c5e219d771805d8c11c915f7";
        public string AssetInventoryFingerprint { get; }
        public FirstUserOnboardingEnvironmentSourceKind SourceKind =>
            FirstUserOnboardingEnvironmentSourceKind.AuthoredModule;
        public GameObject OwnedRoot => _ownedRoot;
        public Object EnvironmentModuleSourceAsset { get; }
        public string EnvironmentModuleAssetId =>
            FirstUserOnboardingFixedAssetInventoryVerifier.AssetId(
                FirstUserOnboardingAssetRole.EnvironmentModule);
        public GameObject NeutralEnvironmentRoot { get; }
        public Transform SceneAnchor { get; }
        public Transform SpawnAnchor { get; }
        public Bounds WalkableBounds { get; }
        public Vector3 MovementProofStart { get; }
        public Vector3 MovementProofEnd { get; }
        public Bounds AttackSafeBounds { get; }
        public CharacterController PlayerController { get; }
        public ChampionController PlayerChampion { get; }
        public Camera PrimaryCamera { get; }
        public Transform PrimaryCameraAnchor { get; }
        public Transform PrimaryCameraTarget { get; }
        public Transform OmenAnchor { get; }
        public Transform LightingHook { get; }
        public Transform PresentationHook { get; }
        public GameObject ModularChampionRoot { get; }
        public string ChampionAssetId =>
            FirstUserOnboardingFixedAssetInventoryVerifier.AssetId(
                FirstUserOnboardingAssetRole.ModularChampion);
        public Object ChampionSourceAsset { get; }
        public GameObject SelectedArmorRoot { get; }
        public string ArmorAssetId =>
            FirstUserOnboardingFixedAssetInventoryVerifier.AssetId(
                FirstUserOnboardingAssetRole.SelectedBasicArmor);
        public Object ArmorSourceAsset { get; }
        public GameObject SelectedWeaponRoot { get; }
        public string WeaponAssetId =>
            FirstUserOnboardingFixedAssetInventoryVerifier.AssetId(
                FirstUserOnboardingAssetRole.SelectedBasicWeapon);
        public Object WeaponSourceAsset { get; }
        public GameObject EnemyRoot { get; }
        public string EnemyAssetId =>
            FirstUserOnboardingFixedAssetInventoryVerifier.AssetId(
                FirstUserOnboardingAssetRole.CommonEnemy);
        public Object EnemySourceAsset { get; }
        public FirstUserOnboardingEnemyCandidateKind EnemyCandidateKind =>
            FirstUserOnboardingEnemyCandidateKind.Normal;
        public FirstUserOnboardingEncounterMode EncounterMode =>
            FirstUserOnboardingEncounterMode.BoundedMechanicsEncounter;
        public IFirstUserOnboardingEnemyEncounter EnemyEncounter { get; }
        public Transform EnemySpawnAnchor { get; }
        public GameObject KingdomStructureRoot { get; }
        public string KingdomStructureAssetId =>
            FirstUserOnboardingFixedAssetInventoryVerifier.AssetId(
                FirstUserOnboardingAssetRole.KingdomBaseStructure);
        public Object KingdomStructureSourceAsset { get; }
        public FirstUserOnboardingKingdomStructureMode KingdomStructureMode =>
            FirstUserOnboardingKingdomStructureMode.LockedPreviewOnly;
        public Material FloorMaterial { get; }
        public string FloorMaterialAssetId =>
            FirstUserOnboardingFixedAssetInventoryVerifier.AssetId(
                FirstUserOnboardingAssetRole.FloorMaterial);
        public Material WallMaterial { get; }
        public string WallMaterialAssetId =>
            FirstUserOnboardingFixedAssetInventoryVerifier.AssetId(
                FirstUserOnboardingAssetRole.WallMaterial);
        public Material TrimMaterial { get; }
        public string TrimMaterialAssetId =>
            FirstUserOnboardingFixedAssetInventoryVerifier.AssetId(
                FirstUserOnboardingAssetRole.TrimMaterial);
        public Transform PropsRoot { get; }
        public GameObject FloorModuleRoot { get; }
        public GameObject WallModuleRoot { get; }
        public GameObject InnerCornerModuleRoot { get; }
        public GameObject OuterCornerModuleRoot { get; }
        public GameObject DoorwayModuleRoot { get; }
        public GameObject CeilingBeamModuleRoot { get; }
        public GameObject TrimModuleRoot { get; }
        public GameObject BrazierPropRoot { get; }
        public GameObject BannerStandPropRoot { get; }
        public GameObject CrateBarrelPropRoot { get; }
        public int EffectiveTexelsPerMeter =>
            FirstUserOnboardingEnvironmentBudget.LowTierEffectiveTexelsPerMeter;
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            if (_ownedRoot != null)
            {
                Object.DestroyImmediate(_ownedRoot);
            }

            if (_runtimeMaterials == null)
            {
                return;
            }

            for (int index = 0; index < _runtimeMaterials.Count; index++)
            {
                Material material = _runtimeMaterials[index];
                if (material == null)
                {
                    continue;
                }

                Object.DestroyImmediate(material);
            }
        }
    }

    internal sealed class FirstUserOnboardingAuthoredEnemyEncounter :
        IFirstUserOnboardingEnemyEncounter
    {
        internal FirstUserOnboardingAuthoredEnemyEncounter(
            string sessionId,
            int generation,
            string assetId,
            GameObject enemyRoot,
            int initialHitPoints)
        {
            SessionId = sessionId;
            Generation = generation;
            EnemyAssetId = assetId;
            EnemyRoot = enemyRoot;
            InitialHitPoints = initialHitPoints;
            CurrentHitPoints = initialHitPoints;
            IsReady = true;
            PresentationState = FirstUserOnboardingEncounterPresentationState.Idle;
        }

        public string SessionId { get; }
        public int Generation { get; }
        public string EnemyAssetId { get; }
        public GameObject EnemyRoot { get; }
        public int InitialHitPoints { get; }
        public int CurrentHitPoints { get; private set; }
        public int ResetSequence { get; private set; }
        public bool IsReady { get; private set; }
        public FirstUserOnboardingEncounterPresentationState PresentationState { get; private set; }

        public bool TryApplyBasicAttack(
            FirstUserOnboardingAttackRequest request,
            out FirstUserOnboardingAttackReceipt receipt,
            out string diagnostic)
        {
            bool valid = IsReady && CurrentHitPoints > 0 &&
                         FirstUserOnboardingEncounterContract.IsValidRequest(request) &&
                         string.Equals(request.SessionId, SessionId, StringComparison.Ordinal) &&
                         request.Generation == Generation &&
                         string.Equals(request.EnemyAssetId, EnemyAssetId, StringComparison.Ordinal);
            if (!valid)
            {
                receipt = default;
                diagnostic = "authored_enemy_attack_rejected";
                return false;
            }

            int before = CurrentHitPoints;
            CurrentHitPoints = Math.Max(0, CurrentHitPoints - 1);
            bool defeated = CurrentHitPoints == 0;
            IsReady = !defeated;
            PresentationState = defeated
                ? FirstUserOnboardingEncounterPresentationState.Defeated
                : FirstUserOnboardingEncounterPresentationState.HitReaction;
            receipt = new FirstUserOnboardingAttackReceipt(
                SessionId,
                Generation,
                request.AttackSequence,
                EnemyAssetId,
                defeated
                    ? FirstUserOnboardingEncounterResult.Defeated
                    : FirstUserOnboardingEncounterResult.HitConfirmed,
                before,
                CurrentHitPoints,
                ResetSequence);
            diagnostic = string.Empty;
            return true;
        }

        public bool TryReset(
            string sessionId,
            int generation,
            int expectedNextResetSequence,
            out int appliedResetSequence,
            out string diagnostic)
        {
            bool valid = string.Equals(sessionId, SessionId, StringComparison.Ordinal) &&
                         generation == Generation &&
                         expectedNextResetSequence == ResetSequence + 1 &&
                         expectedNextResetSequence <=
                         FirstUserOnboardingEnvironmentBudget.MaximumEncounterResetSequence;
            if (valid)
            {
                ResetSequence = expectedNextResetSequence;
                CurrentHitPoints = InitialHitPoints;
                IsReady = true;
                PresentationState = FirstUserOnboardingEncounterPresentationState.Idle;
            }

            appliedResetSequence = ResetSequence;
            diagnostic = valid ? string.Empty : "authored_enemy_reset_rejected";
            return valid;
        }
    }

    public static class FirstUserOnboardingEvidenceCapture
    {
        [MenuItem("Another Life/Dev/Capture First User Authored Environment")]
        public static void CaptureForCli()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            if (!FirstUserOnboardingEnvironmentRegistry.TryResolve(
                    out IFirstUserOnboardingEnvironmentFactory factory,
                    out IFirstUserOnboardingAssetInventoryVerifier verifier))
            {
                throw new InvalidOperationException("The authored environment is not registered.");
            }

            var request = new FirstUserOnboardingEnvironmentRequest(
                "1234567890abcdef1234567890abcdef",
                generation: 1,
                scene,
                allowUnitTestDouble: false,
                assetInventoryVerifier: verifier);
            if (!factory.TryCreate(
                    request,
                    out IFirstUserOnboardingEnvironmentLease lease,
                    out string diagnostic))
            {
                throw new InvalidOperationException(diagnostic);
            }

            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                FirstUserOnboardingEnvironmentValidation validation =
                    FirstUserOnboardingEnvironmentValidator.Validate(request, lease);
                if (!validation.IsValid)
                {
                    throw new InvalidOperationException(validation.Failure.ToString());
                }

                Debug.Log(
                    "[AL-FIRST-USER-ASSET-BUDGET] triangles=" +
                    validation.VisibleTriangles +
                    " renderers=" + validation.RendererCount +
                    " sharedMaterials=" + validation.SharedMaterialCount +
                    " shadowedDirectional=" + validation.ShadowedDirectionalLightCount +
                    " localLights=" + validation.NonShadowedLocalLightCount +
                    " particles=" + validation.AmbientParticleCount);

                target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
                lease.PrimaryCamera.targetTexture = target;
                lease.PrimaryCamera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                pixels = new Texture2D(1280, 720, TextureFormat.RGB24, mipChain: false);
                pixels.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
                pixels.Apply();
                RenderTexture.active = previous;

                string output = Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName,
                    "Logs",
                    "first-user-authored-environment.png");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllBytes(output, pixels.EncodeToPNG());
                Debug.Log("[AL-FIRST-USER-ASSET-EVIDENCE] " + output);
            }
            finally
            {
                if (lease.PrimaryCamera != null)
                {
                    lease.PrimaryCamera.targetTexture = null;
                }

                lease.Dispose();
                if (target != null)
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }

                if (pixels != null)
                {
                    Object.DestroyImmediate(pixels);
                }
            }
        }
    }
}
