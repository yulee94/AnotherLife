using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AL.Editor
{
    public static class FirstSessionAuthoredRealmPrefabBuilder
    {
        private const string OutputRoot =
            "Assets/AL/Art/Generated/World/FirstSession";
        private const string ArchitectureRoot =
            "Assets/AL/Art/Generated/Architecture";
        private const string ProductionEnvironmentRoot =
            "Assets/AL/Art/Production/FirstUserOnboarding/Environment";
        private const int TerrainColumns = 37;
        private const int TerrainRows = 41;
        private const float TerrainWidth = 180f;
        private const float TerrainLength = 200f;

        [MenuItem("Another Life/Build/First Session Authored Realms")]
        public static void GenerateForCli()
        {
            EnsureAssetFolder(OutputRoot);
            BuildRealm(RealmId.Stonehold, "Stonehold", "Workshop");
            BuildRealm(RealmId.Eldergrove, "Eldergrove", "Workshop");
            BuildRealm(RealmId.Crownlands, "Crownlands", "Stormwright");
            BuildRealm(RealmId.Umbral, "Umbral", "Veilwright");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            FirstSessionAuthoredAssetCatalogBuilder.GenerateForCli();
            Debug.Log("[AL-FIRST-SESSION-AUTHORED-REALMS] generated=" + OutputRoot);
        }

        private static void BuildRealm(RealmId realm, string realmName, string workshopName)
        {
            string realmFolder = OutputRoot + "/" + realmName;
            if (AssetDatabase.IsValidFolder(realmFolder))
            {
                AssetDatabase.DeleteAsset(realmFolder);
            }

            EnsureAssetFolder(realmFolder + "/Meshes");
            EnsureAssetFolder(realmFolder + "/Materials");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Material landscapeMaterial = CreateMaterialAsset(
                Load<Material>(GroundMaterialPath(realmName)),
                realmFolder + "/Materials/MAT_" + realmName + "_FirstSessionLandscape.mat",
                RealmGroundTint(realm));
            Material roadMaterial = CreateRoadMaterial(
                realm,
                realmFolder + "/Materials/MAT_" + realmName + "_FirstSessionRoad.mat");

            var root = new GameObject(realmName + "_FirstSessionAuthoredRealm");
            try
            {
                Mesh terrainMesh = CreateTerrainMesh(realm);
                string terrainMeshPath = realmFolder + "/Meshes/M_" + realmName +
                                         "_FirstSessionLandscape.asset";
                AssetDatabase.CreateAsset(terrainMesh, terrainMeshPath);
                CreateMeshObject(
                    root.transform,
                    FirstSessionAuthoredRealmRoute.LandscapeName,
                    terrainMesh,
                    landscapeMaterial);

                Mesh roadMesh = CreateRoadMesh();
                string roadMeshPath = realmFolder + "/Meshes/M_" + realmName +
                                      "_FirstSessionQuestRoad.asset";
                AssetDatabase.CreateAsset(roadMesh, roadMeshPath);
                CreateMeshObject(
                    root.transform,
                    FirstSessionAuthoredRealmRoute.QuestRoadName,
                    roadMesh,
                    roadMaterial);

                Mesh plazaMesh = CreateDiscMesh(10f, 48);
                string plazaMeshPath = realmFolder + "/Meshes/M_" + realmName +
                                       "_FirstSessionSpawnPlaza.asset";
                AssetDatabase.CreateAsset(plazaMesh, plazaMeshPath);
                GameObject plaza = CreateMeshObject(
                    root.transform,
                    FirstSessionAuthoredRealmRoute.SpawnPlazaName,
                    plazaMesh,
                    roadMaterial);
                plaza.transform.localPosition = new Vector3(0f, 0.10f, 0f);

                BuildArchitecture(root.transform, realmName, workshopName);
                BindRoute(root);
                MarkStatic(root);

                string prefabPath = realmFolder + "/" + realmName +
                                    "_FirstSessionAuthoredRealm.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildArchitecture(
            Transform root,
            string realmName,
            string workshopName)
        {
            string townHallPath = ArchitectureRoot + "/" + realmName +
                                  "/Production/TownHall/Runtime/" + realmName +
                                  "_TownHall_Production.prefab";
            string workshopPath = ArchitectureRoot + "/" + realmName +
                                  "/Production/Runtime/" + realmName + "_" +
                                  workshopName + "_Production.prefab";
            string capitalHallPath = ProductionEnvironmentRoot + "/" + realmName +
                                     "_CapitalHall_Meshy6_v001.fbx";

            PlaceArchitecture(
                Load<GameObject>(townHallPath),
                root,
                "AuthoredCapitalTownHall",
                new Vector3(0f, 0f, 86f),
                180f,
                26f);
            PlaceArchitecture(
                Load<GameObject>(workshopPath),
                root,
                "AuthoredRealmWorkshop",
                new Vector3(-29f, 0f, 49f),
                30f,
                20f);
            PlaceArchitecture(
                Load<GameObject>(capitalHallPath),
                root,
                "AuthoredPremiumCapitalHall",
                new Vector3(30f, 0f, 65f),
                210f,
                28f);
            PlaceArchitecture(
                Load<GameObject>(townHallPath),
                root,
                "AuthoredAvenueDistrictHall",
                new Vector3(31f, 0f, 22f),
                235f,
                18f);
        }

        private static void BindRoute(GameObject root)
        {
            Transform playerSpawn = CreateAnchor(
                root.transform,
                FirstSessionAuthoredRealmRoute.PlayerSpawnAnchorName,
                new Vector3(0f, 0f, -2f));
            Transform valerius = CreateAnchor(
                root.transform,
                FirstSessionAuthoredRealmRoute.CaptainValeriusAnchorName,
                new Vector3(0f, 0f, 10f));
            Transform guardian = CreateAnchor(
                root.transform,
                FirstSessionAuthoredRealmRoute.GuardianTrialAnchorName,
                new Vector3(0f, 0f, 38f));
            Transform covenant = CreateAnchor(
                root.transform,
                FirstSessionAuthoredRealmRoute.CovenantSiteAnchorName,
                new Vector3(5.5f, 0f, 56f));
            Transform destination = CreateAnchor(
                root.transform,
                FirstSessionAuthoredRealmRoute.LordshipDestinationAnchorName,
                new Vector3(0f, 0f, 75f));

            float[] waypointZ = { -2f, 10f, 22f, 38f, 48f, 56f, 66f, 75f };
            var waypoints = new Transform[waypointZ.Length];
            for (int index = 0; index < waypointZ.Length; index++)
            {
                waypoints[index] = CreateAnchor(
                    root.transform,
                    FirstSessionAuthoredRealmRoute.WaypointPrefix + index.ToString("00"),
                    new Vector3(index == 5 ? 5.5f : 0f, 0f, waypointZ[index]));
            }

            FirstSessionAuthoredRealmRoute route =
                root.AddComponent<FirstSessionAuthoredRealmRoute>();
            route.Bind(playerSpawn, valerius, guardian, covenant, destination, waypoints);
        }

        private static Transform CreateAnchor(Transform parent, string name, Vector3 position)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.localPosition = position;
            return anchor;
        }

        private static GameObject CreateMeshObject(
            Transform parent,
            string name,
            Mesh mesh,
            Material material)
        {
            var target = new GameObject(name);
            target.transform.SetParent(parent, false);
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return target;
        }

        private static Mesh CreateTerrainMesh(RealmId realm)
        {
            var vertices = new Vector3[TerrainColumns * TerrainRows];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(TerrainColumns - 1) * (TerrainRows - 1) * 6];
            for (int row = 0; row < TerrainRows; row++)
            {
                float z = Mathf.Lerp(-TerrainLength * 0.5f, TerrainLength * 0.5f,
                    row / (float)(TerrainRows - 1));
                for (int column = 0; column < TerrainColumns; column++)
                {
                    float x = Mathf.Lerp(-TerrainWidth * 0.5f, TerrainWidth * 0.5f,
                        column / (float)(TerrainColumns - 1));
                    int index = row * TerrainColumns + column;
                    vertices[index] = new Vector3(x, TerrainHeight(x, z, realm), z);
                    uv[index] = new Vector2(
                        column / (float)(TerrainColumns - 1) * 8f,
                        row / (float)(TerrainRows - 1) * 9f);
                }
            }

            int triangle = 0;
            for (int row = 0; row < TerrainRows - 1; row++)
            {
                for (int column = 0; column < TerrainColumns - 1; column++)
                {
                    int current = row * TerrainColumns + column;
                    triangles[triangle++] = current;
                    triangles[triangle++] = current + TerrainColumns;
                    triangles[triangle++] = current + 1;
                    triangles[triangle++] = current + 1;
                    triangles[triangle++] = current + TerrainColumns;
                    triangles[triangle++] = current + TerrainColumns + 1;
                }
            }

            var mesh = new Mesh { name = "AuthoredFirstSessionLandscape" };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float TerrainHeight(float x, float z, RealmId realm)
        {
            float routeBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 34f, Mathf.Abs(x)));
            float edgeRise = Mathf.Pow(
                Mathf.Clamp01((Mathf.Abs(x) - 38f) / 52f),
                1.45f) * 16f;
            float farRise = Mathf.Pow(
                Mathf.Clamp01((Mathf.Abs(z - 35f) - 70f) / 40f),
                1.5f) * 8f;
            float phase = (int)realm * 0.71f;
            float rolling = (Mathf.Sin(x * 0.085f + phase) * 1.7f +
                             Mathf.Cos(z * 0.065f - phase) * 1.2f) * routeBlend;
            return edgeRise + farRise + rolling;
        }

        private static Mesh CreateRoadMesh()
        {
            const int segments = 24;
            const float startZ = -12f;
            const float endZ = 84f;
            const float halfWidth = 4.5f;
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int index = 0; index <= segments; index++)
            {
                float t = index / (float)segments;
                float z = Mathf.Lerp(startZ, endZ, t);
                vertices[index * 2] = new Vector3(-halfWidth, 0.07f, z);
                vertices[index * 2 + 1] = new Vector3(halfWidth, 0.07f, z);
                uv[index * 2] = new Vector2(0f, t * 12f);
                uv[index * 2 + 1] = new Vector2(1f, t * 12f);
                if (index == segments)
                {
                    continue;
                }

                int triangle = index * 6;
                int vertex = index * 2;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }

            var mesh = new Mesh { name = "AuthoredFirstSessionQuestRoad" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateDiscMesh(float radius, int segments)
        {
            var vertices = new Vector3[segments + 1];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                vertices[index + 1] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius);
                uv[index + 1] = new Vector2(
                    Mathf.Cos(angle) * 0.5f + 0.5f,
                    Mathf.Sin(angle) * 0.5f + 0.5f);
                int next = (index + 1) % segments + 1;
                triangles[index * 3] = 0;
                triangles[index * 3 + 1] = index + 1;
                triangles[index * 3 + 2] = next;
            }

            var mesh = new Mesh { name = "AuthoredFirstSessionSpawnPlaza" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void PlaceArchitecture(
            GameObject prefab,
            Transform parent,
            string name,
            Vector3 position,
            float yaw,
            float targetExtent)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            RemoveCompetingColliders(instance);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            ScaleAndGround(instance, targetExtent);
        }

        private static void RemoveCompetingColliders(GameObject instance)
        {
            Collider[] colliders =
                instance.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                return;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(instance))
            {
                PrefabUtility.UnpackPrefabInstance(
                    instance,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }

            colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                UnityEngine.Object.DestroyImmediate(colliders[index]);
            }

            if (instance.GetComponentsInChildren<Collider>(true).Length != 0)
            {
                throw new InvalidOperationException(
                    "First-session architecture retained a competing Collider after stripping: " +
                    instance.name);
            }
        }

        private static void ScaleAndGround(GameObject target, float targetExtent)
        {
            Bounds bounds = CalculateBounds(target);
            float extent = Mathf.Max(bounds.size.x, bounds.size.z);
            if (extent > 0.01f)
            {
                target.transform.localScale *= Mathf.Clamp(targetExtent / extent, 0.2f, 4f);
                bounds = CalculateBounds(target);
            }

            target.transform.position += Vector3.up * -bounds.min.y;
        }

        private static Bounds CalculateBounds(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(target.transform.position, Vector3.zero);
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static Material CreateMaterialAsset(
            Material source,
            string path,
            Color tint)
        {
            Color readableColor = Color.Lerp(source.color, tint, 0.72f);
            const float minimumGrayscale = 0.20f;
            if (readableColor.grayscale < minimumGrayscale)
            {
                readableColor *= minimumGrayscale /
                                 Mathf.Max(0.001f, readableColor.grayscale);
                readableColor.a = 1f;
            }

            var material = new Material(source)
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path),
                color = readableColor
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material CreateRoadMaterial(RealmId realm, string path)
        {
            Material source = AssetDatabase.LoadAllAssetsAtPath(
                    ProductionEnvironmentRoot + "/Neutral_Covenant_Hall_Kit_v001.fbx")
                .OfType<Material>()
                .First(material => material.name == "M_CovenantHall_Floor");
            var material = new Material(source)
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path),
                color = Color.Lerp(Color.white, RealmRoadTint(realm), 0.16f)
            };
            material.mainTexture = Load<Texture2D>(
                ProductionEnvironmentRoot +
                "/Neutral_Covenant_Flagstone_Albedo_Meshy_v001.png");
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static string GroundMaterialPath(string realmName)
        {
            if (realmName == "Crownlands")
            {
                return ArchitectureRoot +
                       "/Crownlands/Materials/MAT_Crownlands_Stormwright_Ground.mat";
            }

            return ArchitectureRoot + "/" + realmName + "/Materials/MAT_" +
                   realmName + "_Ground.mat";
        }

        private static Color RealmGroundTint(RealmId realm)
        {
            switch (realm)
            {
                case RealmId.Stonehold: return new Color(0.27f, 0.18f, 0.13f);
                case RealmId.Eldergrove: return new Color(0.12f, 0.23f, 0.15f);
                case RealmId.Crownlands: return new Color(0.16f, 0.20f, 0.29f);
                case RealmId.Umbral: return new Color(0.16f, 0.11f, 0.21f);
                default: return Color.gray;
            }
        }

        private static Color RealmRoadTint(RealmId realm)
        {
            return realm == RealmId.Crownlands
                ? new Color(0.58f, 0.63f, 0.76f)
                : RealmGroundTint(realm);
        }

        private static void MarkStatic(GameObject root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].GetComponent<FirstSessionAuthoredRealmRoute>() == null)
                {
                    GameObjectUtility.SetStaticEditorFlags(
                        transforms[index].gameObject,
                        StaticEditorFlags.BatchingStatic |
                        StaticEditorFlags.OccluderStatic |
                        StaticEditorFlags.OccludeeStatic);
                }
            }
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

        private static void EnsureAssetFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }
    }
}
