using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AL.Data.Catalogs.WorldStreaming;
using AL.World.Streaming;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AL.Editor.World
{
    public static class WorldBlockoutSceneGenerator
    {
        private const string MenuPath =
            "AnotherLife/World/Generate Full Modular World Blockout";
        private const string CatalogRelativePath =
            "AL/StreamingAssets/GameData/al_world_streaming_catalog.json";
        private const string GeneratedRoot = "Assets/AL/Worlds/Generated";
        private const string MaterialRoot = GeneratedRoot + "/Materials";
        private const string StampPath =
            GeneratedRoot + "/world_blockout_catalog.sha256.txt";
        private const string GeneratorVersion = "0.1.9";

        private sealed class Materials
        {
            internal Material Earth;
            internal Material Stone;
            internal Material Road;
            internal Material Warzone;
            internal Material Bridge;
            internal Material Kingdom;
            internal Material Crystal;
            internal Material Cave;
            internal Material Water;
            internal Material Vegetation;
        }

        [MenuItem(MenuPath)]
        public static void GenerateAllFromMenu()
        {
            GenerateAll();
        }

        public static void GenerateAll()
        {
            string catalogPath = Path.Combine(Application.dataPath, CatalogRelativePath);
            byte[] catalogBytes = File.ReadAllBytes(catalogPath);
            WorldStreamingLoadResult result =
                WorldStreamingCatalogLoader.Validate(catalogBytes);
            if (!result.IsAccepted)
            {
                throw new InvalidOperationException(
                    "World streaming catalog rejected:\n" +
                    string.Join("\n", result.Diagnostics.Select(value => value.Fingerprint)));
            }

            string hash = ComputeSha256(catalogBytes) + "-" + GeneratorVersion;
            if (CanReuseGeneratedScenes(result.Snapshot, hash))
            {
                Debug.Log(
                    $"World blockout is current: {result.Snapshot.Chunks.Count} chunk scenes.");
                return;
            }

            ThrowIfAnyLoadedSceneIsDirty();
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EnsureDirectory(GeneratedRoot);
                EnsureDirectory(MaterialRoot);
                AssetDatabase.Refresh();
                Materials materials = EnsureMaterials();

                foreach (WorldDimensionDefinition dimension in result.Snapshot.Dimensions)
                {
                    foreach (string worldId in dimension.WorldIds)
                    {
                        WorldInstanceDefinition world = result.Snapshot.GetWorld(worldId);
                        foreach (string chunkId in world.ChunkIds)
                        {
                            GenerateChunkScene(
                                result.Snapshot,
                                dimension,
                                world,
                                result.Snapshot.GetChunk(chunkId),
                                materials);
                        }
                    }
                }

                File.WriteAllText(StampPath, hash + Environment.NewLine);
                AssetDatabase.ImportAsset(StampPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.SaveAssets();
                Debug.Log(
                    $"Generated {result.Snapshot.Chunks.Count} modular world chunk scenes " +
                    $"across {result.Snapshot.Worlds.Count} world instances.");
            }
            finally
            {
                if (previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }
        }

        private static void ThrowIfAnyLoadedSceneIsDirty()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty)
                {
                    throw new InvalidOperationException(
                        "Save modified scenes before generating world blockout scenes: " +
                        scene.path);
                }
            }
        }

        private static bool CanReuseGeneratedScenes(
            WorldStreamingSnapshot snapshot,
            string expectedHash)
        {
            if (!File.Exists(StampPath) ||
                !string.Equals(
                    File.ReadAllText(StampPath).Trim(),
                    expectedHash,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return snapshot.Chunks.All(chunk => File.Exists(chunk.ScenePath));
        }

        private static void GenerateChunkScene(
            WorldStreamingSnapshot snapshot,
            WorldDimensionDefinition dimension,
            WorldInstanceDefinition world,
            WorldChunkDefinition chunk,
            Materials materials)
        {
            string directory = Path.GetDirectoryName(chunk.ScenePath);
            EnsureDirectory(directory);

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var rootObject = new GameObject("WorldChunkRoot");
            Vector3 origin = new Vector3(
                chunk.GridX * dimension.ChunkSpanMeters,
                0f,
                chunk.GridZ * dimension.ChunkSpanMeters);
            rootObject.transform.position = origin;
            rootObject.AddComponent<WorldChunkRoot>().Configure(
                dimension.Id,
                world.Id,
                chunk.Id,
                chunk.BlockoutArchetype,
                dimension.ChunkSpanMeters);

            BuildArchetype(
                snapshot,
                dimension,
                world,
                chunk,
                rootObject.transform,
                materials);
            CreateReplacementSockets(
                chunk,
                rootObject.transform,
                dimension.ChunkSpanMeters);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, chunk.ScenePath))
            {
                throw new IOException("Failed to save world chunk scene: " + chunk.ScenePath);
            }
        }

        private static void BuildArchetype(
            WorldStreamingSnapshot snapshot,
            WorldDimensionDefinition dimension,
            WorldInstanceDefinition world,
            WorldChunkDefinition chunk,
            Transform root,
            Materials materials)
        {
            float span = dimension.ChunkSpanMeters;
            switch (chunk.BlockoutArchetype)
            {
                case "realm_capital":
                    CreateGround(root, span, 20f, materials.Earth);
                    CreateCapital(root, span, materials);
                    break;
                case "realm_area":
                    CreateGround(root, span, 14f, materials.Earth);
                    CreateRealmArea(root, span, materials);
                    break;
                case "realm_gate":
                    CreateGround(root, span, 18f, materials.Earth);
                    CreateRealmGate(root, span, materials);
                    break;
                case "warzone_sector":
                    CreateGround(root, span, 30f, materials.Warzone);
                    CreateWarzoneSector(root, span, materials);
                    break;
                case "warzone_crossroads":
                    CreateGround(root, span, 24f, materials.Warzone);
                    CreateWarzoneCrossroads(root, span, materials);
                    break;
                case "warzone_gate_approach":
                    CreateGround(root, span, 20f, materials.Warzone);
                    CreateGateApproach(root, span, materials);
                    break;
                case "warzone_bridge":
                    CreateWarzoneBridge(snapshot, chunk, root, span, materials);
                    break;
                case "dragon_cave_entrance":
                    CreateCaveFloor(root, span, materials.Cave);
                    CreateCaveEntrance(root, span, materials);
                    break;
                case "dragon_cave_descent":
                    CreateCaveFloor(root, span, materials.Cave);
                    CreateCaveDescent(root, span, materials);
                    break;
                case "dragon_cave_lair":
                    CreateCaveFloor(root, span, materials.Cave);
                    CreateDragonLair(root, span, materials);
                    break;
                case "kingdom_castle":
                    CreateGround(root, span, 3f, materials.Kingdom);
                    CreateKingdomCastle(root, span, materials);
                    break;
                case "kingdom_area":
                    CreateGround(root, span, 2f, materials.Kingdom);
                    CreateKingdomArea(root, span, materials);
                    break;
                case "accordant_surface":
                    CreateAccordantSurface(root, span, materials);
                    break;
                case "accordant_sealed_bridge":
                    CreateAccordantSealedBridge(root, span, chunk, materials);
                    break;
                case "accordant_castle":
                    CreateGround(root, span, 28f, materials.Stone);
                    CreateAccordantCastle(root, span, materials);
                    break;
                case "accordant_entrance":
                    CreateGround(root, span, 20f, materials.Stone);
                    CreateAccordantEntrance(root, span, materials);
                    break;
                case "accordant_descent":
                    CreateCaveFloor(root, span, materials.Cave);
                    CreateAccordantDescent(root, span, materials);
                    break;
                case "accordant_wish_dragon_cavern":
                    CreateWishDragonCavern(root, span, materials);
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unknown world blockout archetype: " + chunk.BlockoutArchetype);
            }
        }

        private static void CreateGround(
            Transform root,
            float span,
            float height,
            Material material)
        {
            CreatePrimitive(
                root,
                "TerrainBlockout",
                PrimitiveType.Cube,
                new Vector3(0f, -height * 0.5f, 0f),
                new Vector3(span * 0.96f, height, span * 0.96f),
                material);
        }

        private static void CreateCapital(Transform root, float span, Materials materials)
        {
            CreateCrossRoads(root, span, materials.Road);
            CreatePrimitive(
                root,
                "CapitalKeepBlockout",
                PrimitiveType.Cube,
                new Vector3(0f, 75f, 0f),
                new Vector3(190f, 150f, 190f),
                materials.Stone);
            CreatePrimitive(
                root,
                "KeepCrown",
                PrimitiveType.Cylinder,
                new Vector3(0f, 115f, 0f),
                new Vector3(115f, 42f, 115f),
                materials.Bridge);
            for (int index = 0; index < 4; index++)
            {
                float x = index % 2 == 0 ? -145f : 145f;
                float z = index < 2 ? -145f : 145f;
                CreateTower(root, "CapitalTower_" + index, new Vector3(x, 68f, z), 52f, 136f, materials.Stone);
            }

            CreateWallRing(root, 330f, 22f, 58f, materials.Stone, "CapitalWall");
        }

        private static void CreateRealmArea(Transform root, float span, Materials materials)
        {
            CreateRoad(root, "AreaRoad", Vector3.zero, span * 0.82f, 36f, 0f, materials.Road);
            for (int index = 0; index < 7; index++)
            {
                float angle = index * Mathf.PI * 2f / 7f;
                float radius = 130f + (index % 2) * 65f;
                Vector3 position = new Vector3(
                    Mathf.Cos(angle) * radius,
                    24f + index % 3 * 8f,
                    Mathf.Sin(angle) * radius);
                CreatePrimitive(
                    root,
                    "AreaStructureBlockout_" + index,
                    PrimitiveType.Cube,
                    position,
                    new Vector3(72f, position.y * 2f, 58f),
                    materials.Stone,
                    new Vector3(0f, -angle * Mathf.Rad2Deg, 0f));
            }

            for (int index = 0; index < 12; index++)
            {
                float angle = index * Mathf.PI * 2f / 12f;
                CreateTree(
                    root,
                    "AreaTreeBlockout_" + index,
                    new Vector3(
                        Mathf.Cos(angle) * span * 0.34f,
                        0f,
                        Mathf.Sin(angle) * span * 0.34f),
                    materials);
            }
        }

        private static void CreateRealmGate(Transform root, float span, Materials materials)
        {
            CreateRoad(root, "GateRoad", Vector3.zero, span * 0.94f, 52f, 0f, materials.Road);
            float wallLength = span * 0.40f;
            CreatePrimitive(root, "InnerWallLeft", PrimitiveType.Cube,
                new Vector3(-wallLength * 0.58f, 58f, 0f),
                new Vector3(wallLength, 116f, 42f), materials.Stone);
            CreatePrimitive(root, "InnerWallRight", PrimitiveType.Cube,
                new Vector3(wallLength * 0.58f, 58f, 0f),
                new Vector3(wallLength, 116f, 42f), materials.Stone);
            CreateTower(root, "MainGateTowerLeft", new Vector3(-84f, 78f, 0f), 62f, 156f, materials.Stone);
            CreateTower(root, "MainGateTowerRight", new Vector3(84f, 78f, 0f), 62f, 156f, materials.Stone);
            CreatePrimitive(root, "MainGateLintel", PrimitiveType.Cube,
                new Vector3(0f, 132f, 0f), new Vector3(120f, 32f, 50f), materials.Bridge);
        }

        private static void CreateWarzoneSector(Transform root, float span, Materials materials)
        {
            CreateRoad(root, "WarzoneRoute", Vector3.zero, span * 0.90f, 46f, 0f, materials.Road);
            CreatePrimitive(
                root,
                "WarzoneFortressBlockout",
                PrimitiveType.Cube,
                new Vector3(0f, 62f, 0f),
                new Vector3(240f, 124f, 210f),
                materials.Stone);
            CreatePrimitive(root, "FortressCrown", PrimitiveType.Cube,
                new Vector3(0f, 92f, 0f), new Vector3(160f, 60f, 150f), materials.Warzone);
            CreateWallRing(root, 360f, 30f, 72f, materials.Stone, "WarzoneFortressWall");
            for (int index = 0; index < 10; index++)
            {
                float angle = index * Mathf.PI * 2f / 10f;
                CreatePrimitive(root, "WarzoneRock_" + index, PrimitiveType.Sphere,
                    new Vector3(Mathf.Cos(angle) * span * 0.37f, 24f, Mathf.Sin(angle) * span * 0.37f),
                    new Vector3(70f, 48f + index % 3 * 14f, 90f), materials.Warzone);
            }
        }

        private static void CreateWarzoneCrossroads(Transform root, float span, Materials materials)
        {
            CreateCrossRoads(root, span, materials.Road);
            CreatePrimitive(root, "CrossroadsMonumentBlockout", PrimitiveType.Cylinder,
                new Vector3(0f, 95f, 0f), new Vector3(72f, 190f, 72f), materials.Bridge);
            for (int index = 0; index < 4; index++)
            {
                float angle = index * 90f;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                CreateTower(root, "CrossroadsWatchtower_" + index,
                    direction * 250f + Vector3.up * 50f, 44f, 100f, materials.Stone);
            }
        }

        private static void CreateGateApproach(Transform root, float span, Materials materials)
        {
            CreateRoad(root, "GateApproachRoad", Vector3.zero, span * 0.94f, 54f, 0f, materials.Road);
            CreatePrimitive(root, "ControlledTransitionZoneBlockout", PrimitiveType.Cube,
                new Vector3(0f, 14f, -span * 0.24f),
                new Vector3(span * 0.48f, 10f, span * 0.20f), materials.Bridge);
            CreatePrimitive(root, "OuterWallLeftBlockout", PrimitiveType.Cube,
                new Vector3(-span * 0.31f, 62f, span * 0.08f),
                new Vector3(span * 0.38f, 124f, 42f), materials.Stone);
            CreatePrimitive(root, "OuterWallRightBlockout", PrimitiveType.Cube,
                new Vector3(span * 0.31f, 62f, span * 0.08f),
                new Vector3(span * 0.38f, 124f, 42f), materials.Stone);
            CreatePrimitive(root, "WarzoneEntryThresholdBlockout", PrimitiveType.Cube,
                new Vector3(0f, 18f, span * 0.27f),
                new Vector3(span * 0.42f, 16f, 64f), materials.Warzone);
            for (int side = -1; side <= 1; side += 2)
            {
                CreatePrimitive(root, "GateApproachRidge_" + side, PrimitiveType.Cube,
                    new Vector3(side * span * 0.32f, 70f, 0f),
                    new Vector3(span * 0.22f, 140f, span * 0.92f), materials.Warzone);
            }
        }

        private static void CreateWarzoneBridge(
            WorldStreamingSnapshot snapshot,
            WorldChunkDefinition chunk,
            Transform root,
            float span,
            Materials materials)
        {
            WorldChunkDefinition[] sectors = chunk.NeighborIds
                .Select(snapshot.GetChunk)
                .Where(value => value != null && value.BlockoutArchetype == "warzone_sector")
                .ToArray();
            Vector3 direction = sectors.Length == 2
                ? new Vector3(
                    sectors[1].GridX - sectors[0].GridX,
                    0f,
                    sectors[1].GridZ - sectors[0].GridZ).normalized
                : Vector3.forward;
            float length = sectors.Length == 2
                ? Vector2.Distance(
                    new Vector2(sectors[0].GridX, sectors[0].GridZ),
                    new Vector2(sectors[1].GridX, sectors[1].GridZ)) * span
                : span * 3.2f;
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            Vector3 lateral = rotation * Vector3.right;
            CreatePrimitive(root, "BridgeDeckBlockout", PrimitiveType.Cube,
                new Vector3(0f, 42f, 0f), new Vector3(110f, 28f, length * 0.92f),
                materials.Bridge, rotation.eulerAngles);
            CreatePrimitive(root, "BridgeRailLeft", PrimitiveType.Cube,
                lateral * -65f + Vector3.up * 72f, new Vector3(18f, 56f, length * 0.94f),
                materials.Stone, rotation.eulerAngles);
            CreatePrimitive(root, "BridgeRailRight", PrimitiveType.Cube,
                lateral * 65f + Vector3.up * 72f, new Vector3(18f, 56f, length * 0.94f),
                materials.Stone, rotation.eulerAngles);
            CreatePrimitive(root, "BridgeVoid", PrimitiveType.Cube,
                new Vector3(0f, -90f, 0f), new Vector3(span * 0.94f, 80f, length),
                materials.Water, rotation.eulerAngles);
        }

        private static void CreateCaveFloor(Transform root, float span, Material material)
        {
            CreatePrimitive(root, "CaveFloorBlockout", PrimitiveType.Cylinder,
                new Vector3(0f, -12f, 0f), new Vector3(span * 0.43f, 24f, span * 0.43f), material);
            for (int index = 0; index < 8; index++)
            {
                float angle = index * Mathf.PI * 2f / 8f;
                CreatePrimitive(root, "CaveWallBlockout_" + index, PrimitiveType.Sphere,
                    new Vector3(Mathf.Cos(angle) * span * 0.39f, 90f, Mathf.Sin(angle) * span * 0.39f),
                    new Vector3(span * 0.22f, 210f, span * 0.22f), material);
            }
        }

        private static void CreateCaveEntrance(Transform root, float span, Materials materials)
        {
            CreatePrimitive(root, "DragonCaveEntranceBlockout", PrimitiveType.Cube,
                new Vector3(0f, 88f, -span * 0.28f), new Vector3(220f, 176f, 100f), materials.Stone);
            CreateCrystalCluster(root, new Vector3(0f, 55f, 120f), 5, materials.Crystal);
        }

        private static void CreateCaveDescent(Transform root, float span, Materials materials)
        {
            for (int index = 0; index < 8; index++)
            {
                CreatePrimitive(root, "CaveDescentStep_" + index, PrimitiveType.Cube,
                    new Vector3(0f, 8f - index * 12f, -span * 0.28f + index * span * 0.08f),
                    new Vector3(180f, 18f, span * 0.10f), materials.Stone);
            }
            CreateCrystalCluster(root, new Vector3(180f, 34f, 0f), 4, materials.Crystal);
        }

        private static void CreateDragonLair(Transform root, float span, Materials materials)
        {
            CreatePrimitive(root, "DragonLairBlockout", PrimitiveType.Cylinder,
                new Vector3(0f, 6f, 0f), new Vector3(span * 0.30f, 12f, span * 0.30f), materials.Stone);
            for (int index = 0; index < 5; index++)
            {
                float angle = index * Mathf.PI * 2f / 5f;
                CreateCrystalCluster(root,
                    new Vector3(Mathf.Cos(angle) * span * 0.26f, 20f, Mathf.Sin(angle) * span * 0.26f),
                    4, materials.Crystal);
            }
        }

        private static void CreateKingdomCastle(Transform root, float span, Materials materials)
        {
            CreateCrossRoads(root, span, materials.Road);
            CreatePrimitive(root, "KingdomCastleBlockout", PrimitiveType.Cube,
                new Vector3(0f, 22f, 0f), new Vector3(42f, 44f, 42f), materials.Stone);
            CreateTower(root, "KingdomCastleTowerNorth", new Vector3(0f, 20f, 32f), 12f, 40f, materials.Stone);
            CreateTower(root, "KingdomCastleTowerSouth", new Vector3(0f, 20f, -32f), 12f, 40f, materials.Stone);
            CreateTower(root, "KingdomCastleTowerEast", new Vector3(32f, 20f, 0f), 12f, 40f, materials.Stone);
            CreateTower(root, "KingdomCastleTowerWest", new Vector3(-32f, 20f, 0f), 12f, 40f, materials.Stone);
            CreateWallRing(root, 54f, 4f, 12f, materials.Stone, "KingdomCastleWall");
        }

        private static void CreateKingdomArea(Transform root, float span, Materials materials)
        {
            CreateRoad(root, "KingdomAreaRoad", Vector3.zero, span * 0.92f, 7f, 0f, materials.Road);
            for (int index = 0; index < 5; index++)
            {
                float angle = index * Mathf.PI * 2f / 5f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 28f, 5f + index % 2 * 3f, Mathf.Sin(angle) * 28f);
                CreatePrimitive(root, "KingdomDistrictBlockout_" + index, PrimitiveType.Cube,
                    position, new Vector3(15f, position.y * 2f, 13f), materials.Stone,
                    new Vector3(0f, -angle * Mathf.Rad2Deg, 0f));
            }
        }

        private static void CreateAccordantSurface(Transform root, float span, Materials materials)
        {
            CreatePrimitive(root, "AccordantFloatingIslandBlockout", PrimitiveType.Sphere,
                new Vector3(0f, -span * 0.16f, 0f),
                new Vector3(span * 0.86f, span * 0.34f, span * 0.86f), materials.Stone);
            CreatePrimitive(root, "AccordantSurfacePlateau", PrimitiveType.Cylinder,
                new Vector3(0f, 12f, 0f), new Vector3(span * 0.38f, 24f, span * 0.38f), materials.Earth);
        }

        private static void CreateAccordantSealedBridge(
            Transform root,
            float span,
            WorldChunkDefinition chunk,
            Materials materials)
        {
            Vector3 towardCenter = new Vector3(-chunk.GridX, 0f, -chunk.GridZ).normalized;
            float yaw = Mathf.Atan2(towardCenter.x, towardCenter.z) * Mathf.Rad2Deg;
            CreatePrimitive(root, "SealedRegularPlayBridgeBlockout", PrimitiveType.Cube,
                towardCenter * span * 0.32f + Vector3.up * 22f,
                new Vector3(96f, 24f, span * 1.58f), materials.Bridge,
                new Vector3(0f, yaw, 0f));
            CreatePrimitive(root, "SealedCenterBridgeBarrierBlockout", PrimitiveType.Cube,
                towardCenter * span * 0.78f + Vector3.up * 68f,
                new Vector3(150f, 112f, 36f), materials.Crystal,
                new Vector3(0f, yaw, 0f));
            CreatePrimitive(root, "CenterBridgeVoid", PrimitiveType.Cube,
                new Vector3(0f, -42f, 0f),
                new Vector3(span * 0.92f, 30f, span * 0.92f), materials.Water);
        }

        private static void CreateAccordantCastle(Transform root, float span, Materials materials)
        {
            CreatePrimitive(root, "AccordantHugeCastleBlockout", PrimitiveType.Cube,
                new Vector3(0f, 140f, 0f), new Vector3(280f, 280f, 280f), materials.Stone);
            CreatePrimitive(root, "AccordantCastleCrown", PrimitiveType.Cylinder,
                new Vector3(0f, 210f, 0f), new Vector3(170f, 100f, 170f), materials.Crystal);
            CreateWallRing(root, 360f, 32f, 84f, materials.Stone, "AccordantCastleWall");
        }

        private static void CreateAccordantEntrance(Transform root, float span, Materials materials)
        {
            CreateRoad(root, "AccordantEntranceRoad", Vector3.zero, span * 0.90f, 58f, 0f, materials.Road);
            CreateTower(root, "AccordantEntranceTowerLeft", new Vector3(-90f, 88f, 0f), 62f, 176f, materials.Stone);
            CreateTower(root, "AccordantEntranceTowerRight", new Vector3(90f, 88f, 0f), 62f, 176f, materials.Stone);
            CreatePrimitive(root, "AccordantEntranceLintel", PrimitiveType.Cube,
                new Vector3(0f, 148f, 0f), new Vector3(140f, 36f, 54f), materials.Crystal);
        }

        private static void CreateAccordantDescent(Transform root, float span, Materials materials)
        {
            CreatePrimitive(root, "AccordantCrystalDescentBlockout", PrimitiveType.Cylinder,
                new Vector3(0f, -span * 0.18f, 0f),
                new Vector3(span * 0.28f, span * 0.42f, span * 0.28f), materials.Cave);
            for (int index = 0; index < 10; index++)
            {
                float angle = index * Mathf.PI * 0.44f;
                float radius = span * (0.20f - index * 0.008f);
                CreatePrimitive(root, "AccordantDescentStep_" + index, PrimitiveType.Cube,
                    new Vector3(Mathf.Cos(angle) * radius, -index * 18f, Mathf.Sin(angle) * radius),
                    new Vector3(90f, 16f, 52f), materials.Stone,
                    new Vector3(0f, -angle * Mathf.Rad2Deg, 0f));
            }
        }

        private static void CreateWishDragonCavern(Transform root, float span, Materials materials)
        {
            var cavern = new GameObject("WishDragonCavernBlockout");
            cavern.transform.SetParent(root, false);
            CreatePrimitive(root, "WishDragonCavernFloorBlockout", PrimitiveType.Cylinder,
                new Vector3(0f, -26f, 0f), new Vector3(span * 0.44f, 52f, span * 0.44f), materials.Cave);
            for (int index = 0; index < 10; index++)
            {
                float angle = index * Mathf.PI * 2f / 10f;
                float radius = span * 0.38f;
                CreatePrimitive(
                    cavern.transform,
                    "CavernWallSegmentBlockout_" + index,
                    PrimitiveType.Cube,
                    new Vector3(Mathf.Cos(angle) * radius, span * 0.13f, Mathf.Sin(angle) * radius),
                    new Vector3(span * 0.24f, span * 0.30f, span * 0.06f),
                    materials.Cave,
                    new Vector3(0f, -angle * Mathf.Rad2Deg + 90f, 0f));
            }

            GameObject flightVolume = CreatePrimitive(root, "WishDragonFlightVolumeBlockout", PrimitiveType.Sphere,
                new Vector3(0f, span * 0.24f, 0f),
                new Vector3(span * 0.72f, span * 0.52f, span * 0.72f), materials.Water);
            flightVolume.GetComponent<Renderer>().enabled = false;
            UnityEngine.Object.DestroyImmediate(flightVolume.GetComponent<Collider>());
            for (int index = 0; index < 16; index++)
            {
                float angle = index * Mathf.PI * 2f / 16f;
                float radius = span * 0.35f;
                CreateCrystalCluster(root,
                    new Vector3(Mathf.Cos(angle) * radius, 30f + index % 4 * 24f, Mathf.Sin(angle) * radius),
                    5, materials.Crystal);
            }
        }

        private static void CreateCrossRoads(Transform root, float span, Material road)
        {
            CreateRoad(root, "RoadNorthSouth", Vector3.zero, span * 0.94f, span * 0.055f, 0f, road);
            CreateRoad(root, "RoadEastWest", Vector3.zero, span * 0.94f, span * 0.055f, 90f, road);
        }

        private static void CreateRoad(
            Transform root,
            string name,
            Vector3 position,
            float length,
            float width,
            float yaw,
            Material material)
        {
            CreatePrimitive(root, name, PrimitiveType.Cube,
                position + Vector3.up * 1.5f,
                new Vector3(width, 3f, length), material,
                new Vector3(0f, yaw, 0f));
        }

        private static void CreateWallRing(
            Transform root,
            float radius,
            float thickness,
            float height,
            Material material,
            string prefix)
        {
            CreatePrimitive(root, prefix + "North", PrimitiveType.Cube,
                new Vector3(0f, height * 0.5f, radius),
                new Vector3(radius * 2f, height, thickness), material);
            CreatePrimitive(root, prefix + "South", PrimitiveType.Cube,
                new Vector3(0f, height * 0.5f, -radius),
                new Vector3(radius * 2f, height, thickness), material);
            CreatePrimitive(root, prefix + "East", PrimitiveType.Cube,
                new Vector3(radius, height * 0.5f, 0f),
                new Vector3(thickness, height, radius * 2f), material);
            CreatePrimitive(root, prefix + "West", PrimitiveType.Cube,
                new Vector3(-radius, height * 0.5f, 0f),
                new Vector3(thickness, height, radius * 2f), material);
        }

        private static void CreateTower(
            Transform root,
            string name,
            Vector3 position,
            float diameter,
            float height,
            Material material)
        {
            CreatePrimitive(root, name, PrimitiveType.Cylinder,
                position, new Vector3(diameter, height * 0.5f, diameter), material);
        }

        private static void CreateTree(
            Transform root,
            string name,
            Vector3 position,
            Materials materials)
        {
            CreatePrimitive(root, name, PrimitiveType.Cylinder,
                position + Vector3.up * 28f, new Vector3(10f, 28f, 10f), materials.Bridge);
            CreatePrimitive(root, name + "_Canopy", PrimitiveType.Sphere,
                position + Vector3.up * 70f, new Vector3(46f, 60f, 46f), materials.Vegetation);
        }

        private static void CreateCrystalCluster(
            Transform root,
            Vector3 center,
            int count,
            Material material)
        {
            for (int index = 0; index < count; index++)
            {
                float angle = index * Mathf.PI * 2f / count;
                float height = 42f + index * 12f;
                string name = string.Format(
                    CultureInfo.InvariantCulture,
                    "CrystalBlockout_{0}_{1}_{2}_{3:D2}",
                    Mathf.RoundToInt(center.x),
                    Mathf.RoundToInt(center.y),
                    Mathf.RoundToInt(center.z),
                    index);
                CreatePrimitive(root, name, PrimitiveType.Cube,
                    center + new Vector3(Mathf.Cos(angle) * 34f, height * 0.5f, Mathf.Sin(angle) * 34f),
                    new Vector3(18f, height, 18f), material,
                    new Vector3(index * 5f, -angle * Mathf.Rad2Deg + 45f, index * 4f));
            }
        }

        private static void CreateReplacementSockets(
            WorldChunkDefinition chunk,
            Transform root,
            float span)
        {
            int count = chunk.ReplacementSocketIds.Count;
            for (int index = 0; index < count; index++)
            {
                float angle = count == 1
                    ? 0f
                    : index * Mathf.PI * 2f / count;
                var socketObject = new GameObject(
                    "ReplacementSocket_" + chunk.ReplacementSocketIds[index]);
                socketObject.transform.SetParent(root, false);
                socketObject.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * span * 0.16f,
                    0f,
                    Mathf.Sin(angle) * span * 0.16f);
                socketObject.AddComponent<WorldReplacementSocket>().Configure(
                    chunk.ReplacementSocketIds[index],
                    chunk.BlockoutArchetype,
                    new Vector3(span * 0.18f, span * 0.14f, span * 0.18f));
            }
        }

        private static GameObject CreatePrimitive(
            Transform parent,
            string name,
            PrimitiveType primitive,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Vector3? localEulerAngles = null)
        {
            GameObject value = GameObject.CreatePrimitive(primitive);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.localPosition = localPosition;
            value.transform.localRotation = Quaternion.Euler(localEulerAngles ?? Vector3.zero);
            value.transform.localScale = localScale;
            Renderer renderer = value.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
            return value;
        }

        private static Materials EnsureMaterials()
        {
            return new Materials
            {
                Earth = EnsureMaterial("WorldBlockout_Earth", new Color(0.24f, 0.31f, 0.20f), 0f, 0.18f),
                Stone = EnsureMaterial("WorldBlockout_Stone", new Color(0.34f, 0.36f, 0.40f), 0.04f, 0.30f),
                Road = EnsureMaterial("WorldBlockout_Road", new Color(0.15f, 0.12f, 0.09f), 0f, 0.20f),
                Warzone = EnsureMaterial("WorldBlockout_Warzone", new Color(0.25f, 0.16f, 0.13f), 0.02f, 0.22f),
                Bridge = EnsureMaterial("WorldBlockout_Bridge", new Color(0.27f, 0.22f, 0.18f), 0.06f, 0.28f),
                Kingdom = EnsureMaterial("WorldBlockout_Kingdom", new Color(0.30f, 0.38f, 0.27f), 0f, 0.24f),
                Crystal = EnsureMaterial("WorldBlockout_Crystal", new Color(0.34f, 0.52f, 0.78f), 0.15f, 0.72f, new Color(0.08f, 0.20f, 0.46f)),
                Cave = EnsureMaterial("WorldBlockout_Cave", new Color(0.11f, 0.12f, 0.16f), 0.02f, 0.18f),
                Water = EnsureMaterial("WorldBlockout_VoidWater", new Color(0.05f, 0.11f, 0.18f), 0.12f, 0.62f),
                Vegetation = EnsureMaterial("WorldBlockout_Vegetation", new Color(0.12f, 0.28f, 0.15f), 0f, 0.18f)
            };
        }

        private static Material EnsureMaterial(
            string name,
            Color color,
            float metallic,
            float smoothness,
            Color? emission = null)
        {
            string path = MaterialRoot + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null)
                {
                    throw new InvalidOperationException("Standard shader is unavailable.");
                }
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }
            if (emission.HasValue && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureDirectory(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return string.Concat(
                    sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
            }
        }
    }
}
