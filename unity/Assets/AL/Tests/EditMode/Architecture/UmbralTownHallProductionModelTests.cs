using System;
using System.Linq;
using AL.Core;
using AL.Data.Runtime;
using AL.Kingdom;
using AL.Kingdom.Visuals.Architecture;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AL.Tests.EditMode.Architecture
{
    public sealed class UmbralTownHallProductionModelTests
    {
        private const string PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Umbral/Production/" +
            "TownHall/Runtime/Umbral_TownHall_Production.prefab";
        private const string CatalogPath =
            "Assets/AL/ScriptableObjects/Resources/" +
            "KingdomBuildingModelCatalog.asset";
        private const string AtlasPath =
            "Assets/AL/Art/Generated/Architecture/Umbral/Production/" +
            "TownHall/Runtime/T_Umbral_TownHall_Atlas_1024.png";
        private const string AtlasMaterialPath =
            "Assets/AL/Art/Generated/Architecture/Umbral/Production/" +
            "TownHall/Runtime/MAT_Umbral_TownHall_Atlas.mat";
        private const string AccentMaterialPath =
            "Assets/AL/Art/Generated/Architecture/Umbral/Production/" +
            "TownHall/Runtime/MAT_Umbral_TownHall_Accent.mat";
        private const string PreviewScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "UmbralTownHallProductionModel.unity";
        private const string ConceptSheetFolder =
            "Assets/AL/Art/Architecture/ConceptSheets";
        private const string ModelId =
            "building.umbral.townhall.production.v1";

        [SetUp]
        public void SetUp()
        {
            DestroyIfPresent("Kingdom_CityBoard");
        }

        [TearDown]
        public void TearDown()
        {
            DestroyIfPresent("Kingdom_CityBoard");
        }

        [Test]
        public void PackagedCatalogDeclaresExactLiveTownHallBinding()
        {
            KingdomBuildingModelCatalog catalog = LoadCatalog();
            Assert.That(
                KingdomBuildingModelCatalog.LoadDefault(),
                Is.EqualTo(catalog));
            Assert.That(
                catalog.Validate(out string diagnosticCode),
                Is.True,
                diagnosticCode);
            Assert.That(
                catalog.Entries,
                Has.Length.EqualTo(8),
                "The packaged catalog must contain four Workshops and " +
                "four Town Halls.");

            KingdomBuildingModelEntry entry = catalog.Entries.Single(
                candidate =>
                    candidate.RealmId == RealmId.Umbral &&
                    candidate.BuildingId == "TownHall");
            Assert.That(entry.ModelId, Is.EqualTo(ModelId));
            Assert.That(entry.Prefab, Is.EqualTo(LoadPrefab()));
            Assert.That(entry.StrategicBoardScale, Is.EqualTo(0.09f));
            Assert.That(entry.MinimumLevel, Is.EqualTo(1));
            Assert.That(entry.MaximumLevel, Is.EqualTo(10));
            Assert.That(entry.SupportsLevel(0), Is.False);
            Assert.That(entry.SupportsLevel(1), Is.True);
            Assert.That(entry.SupportsLevel(10), Is.True);
            Assert.That(
                catalog.Entries.Any(candidate =>
                    candidate.RealmId == RealmId.Umbral &&
                    candidate.BuildingId == "Workshop"),
                Is.True,
                "Adding Town Hall must preserve the Workshop binding.");
            Assert.That(entry.RealmMotionProfile, Is.Not.Null);
            Assert.That(entry.HasCompatibleRealmMotionProfile, Is.True);
            Assert.That(
                entry.RealmMotionProfile.ProfileId,
                Is.EqualTo("umbral.veilwright"));
        }

        [Test]
        public void ProductionPrefabOwnsStableIdentityEnvelopeAndTenDeltas()
        {
            KingdomBuildingLevelModel model =
                LoadPrefab().GetComponent<KingdomBuildingLevelModel>();
            Assert.That(model, Is.Not.Null);
            Assert.That(model.IsConfigured, Is.True);
            Assert.That(model.ModelId, Is.EqualTo(ModelId));
            Assert.That(model.BuildingId, Is.EqualTo("TownHall"));
            Assert.That(model.MaximumLevel, Is.EqualTo(10));
            Assert.That(
                model.SlotEnvelope,
                Is.EqualTo(new Vector3(16f, 13f, 16f)));
            Assert.That(
                model.MaximumArtBounds,
                Is.EqualTo(new Vector3(15.2f, 12.6f, 14.2f)));
            Assert.That(model.LevelDeltas, Has.Length.EqualTo(10));
            Assert.That(
                model.LevelDeltas.Select(delta => delta.MinimumLevel),
                Is.EqualTo(Enumerable.Range(1, 10)));
        }

        [TestCase(1, 9.50f, 4.99f, 9.01f)]
        [TestCase(6, 11.38f, 4.99f, 9.01f)]
        [TestCase(10, 11.58f, 6.92f, 10.21f)]
        public void RepresentativeLevelBoundsStayInsideFinalEnvelope(
            int level,
            float expectedWidth,
            float expectedHeight,
            float expectedDepth)
        {
            GameObject instance =
                UnityEngine.Object.Instantiate(LoadPrefab());
            try
            {
                KingdomBuildingLevelModel model =
                    instance.GetComponent<KingdomBuildingLevelModel>();
                Assert.That(model.ApplyConfirmedLevel(level), Is.True);
                Renderer[] renderers = instance.transform
                    .Find("LOD0")
                    .GetComponentsInChildren<Renderer>(false);
                Assert.That(renderers, Is.Not.Empty);

                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers.Skip(1))
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                Assert.That(
                    bounds.size.x,
                    Is.EqualTo(expectedWidth).Within(0.02f));
                Assert.That(
                    bounds.size.y,
                    Is.EqualTo(expectedHeight).Within(0.02f));
                Assert.That(
                    bounds.size.z,
                    Is.EqualTo(expectedDepth).Within(0.02f));
                Assert.That(bounds.size.x, Is.LessThanOrEqualTo(15.2f));
                Assert.That(bounds.size.y, Is.LessThanOrEqualTo(12.6f));
                Assert.That(bounds.size.z, Is.LessThanOrEqualTo(14.2f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [TestCase(1)]
        [TestCase(6)]
        [TestCase(10)]
        public void ConfirmedLevelActivatesOnlyCumulativeModules(int level)
        {
            GameObject instance =
                UnityEngine.Object.Instantiate(LoadPrefab());
            try
            {
                KingdomBuildingLevelModel model =
                    instance.GetComponent<KingdomBuildingLevelModel>();
                Assert.That(model.ApplyConfirmedLevel(level), Is.True);
                Assert.That(model.AppliedLevel, Is.EqualTo(level));

                foreach (KingdomBuildingLevelDelta delta in model.LevelDeltas)
                {
                    GameObject[] concreteObjects = delta.LodObjects
                        .Where(item => item != null)
                        .ToArray();
                    Assert.That(concreteObjects, Is.Not.Empty);
                    Assert.That(
                        concreteObjects.All(
                            item =>
                                item.activeSelf ==
                                (delta.MinimumLevel <= level)),
                        Is.True,
                        $"Level {delta.MinimumLevel} did not follow " +
                        $"confirmed Level {level}.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void LodTopologyMeetsFinalTriangleAndRendererBudgets()
        {
            GameObject prefab = LoadPrefab();
            LODGroup group = prefab.GetComponent<LODGroup>();
            Assert.That(group, Is.Not.Null);
            Assert.That(group.fadeMode, Is.EqualTo(LODFadeMode.None));
            Assert.That(group.animateCrossFading, Is.False);

            LOD[] lods = group.GetLODs();
            Assert.That(lods, Has.Length.EqualTo(4));
            float[] expectedTransitions = { 0.60f, 0.30f, 0.12f, 0.04f };
            int[] expectedRenderers = { 10, 10, 10, 3 };
            int[] expectedTriangles = { 1716, 1524, 1020, 780 };
            int[] triangleCeilings = { 12000, 6000, 2500, 800 };
            for (int index = 0; index < lods.Length; index++)
            {
                Assert.That(
                    lods[index].screenRelativeTransitionHeight,
                    Is.EqualTo(expectedTransitions[index]).Within(0.0001f));
                Assert.That(
                    lods[index].renderers.Length,
                    Is.EqualTo(expectedRenderers[index]));
                int triangles = lods[index].renderers.Sum(
                    renderer =>
                        TriangleCount(
                            renderer.GetComponent<MeshFilter>()
                                ?.sharedMesh));
                Assert.That(triangles, Is.EqualTo(expectedTriangles[index]));
                Assert.That(
                    triangles,
                    Is.LessThanOrEqualTo(triangleCeilings[index]));
                if (index == 3)
                {
                    Assert.That(
                        lods[index].renderers.All(
                            renderer =>
                                renderer.sharedMaterials.Length == 1),
                        Is.True);
                }
            }

            Assert.That(
                expectedTriangles[1],
                Is.LessThan(expectedTriangles[0]));
            Assert.That(
                expectedTriangles[2],
                Is.LessThan(expectedTriangles[1]));
            Assert.That(
                expectedTriangles[3],
                Is.LessThan(expectedTriangles[2]));
        }

        [Test]
        public void RendererPoliciesMatchEachLodBand()
        {
            GameObject prefab = LoadPrefab();
            for (int lodIndex = 0; lodIndex < 4; lodIndex++)
            {
                Transform lodRoot =
                    prefab.transform.Find($"LOD{lodIndex}");
                Assert.That(lodRoot, Is.Not.Null);
                foreach (Renderer renderer in
                    lodRoot.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.That(
                        renderer.shadowCastingMode,
                        Is.EqualTo(
                            lodIndex <= 1
                                ? ShadowCastingMode.On
                                : ShadowCastingMode.Off));
                    Assert.That(
                        renderer.receiveShadows,
                        Is.EqualTo(lodIndex <= 2));
                    Assert.That(
                        renderer.motionVectorGenerationMode,
                        Is.EqualTo(
                            MotionVectorGenerationMode.ForceNoMotion));
                    Assert.That(
                        renderer.lightProbeUsage,
                        Is.EqualTo(LightProbeUsage.Off));
                    Assert.That(
                        renderer.reflectionProbeUsage,
                        Is.EqualTo(ReflectionProbeUsage.Off));
                }
            }
        }

        [Test]
        public void ModelUsesOneRgbAtlasAndTwoOpaqueInstancedMaterials()
        {
            GameObject prefab = LoadPrefab();
            Material[] materials = prefab
                .GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Distinct()
                .ToArray();
            Assert.That(materials, Has.Length.EqualTo(2));
            Assert.That(
                materials.Select(AssetDatabase.GetAssetPath),
                Is.EquivalentTo(
                    new[]
                    {
                        AtlasMaterialPath,
                        AccentMaterialPath
                    }));
            Assert.That(
                materials.All(material => material.enableInstancing),
                Is.True);
            Assert.That(
                materials.All(material => material.GetFloat("_Mode") == 0f),
                Is.True);

            Texture2D atlas =
                AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            Assert.That(atlas, Is.Not.Null);
            Assert.That(atlas.width, Is.EqualTo(1024));
            Assert.That(atlas.height, Is.EqualTo(1024));
            var importer =
                AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(
                importer.alphaSource,
                Is.EqualTo(TextureImporterAlphaSource.None));
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.maxTextureSize, Is.EqualTo(1024));
            Assert.That(
                importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.CompressedHQ));

            Material atlasMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    AtlasMaterialPath);
            Material accentMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    AccentMaterialPath);
            Assert.That(atlasMaterial.mainTexture, Is.EqualTo(atlas));
            Assert.That(accentMaterial.mainTexture, Is.Null);
            Assert.That(
                atlasMaterial.IsKeywordEnabled("_EMISSION"),
                Is.False);
            Assert.That(
                accentMaterial.IsKeywordEnabled("_EMISSION"),
                Is.False);
        }

        [Test]
        public void PrefabHasOnlyTheTwoApprovedRootBoxColliders()
        {
            GameObject prefab = LoadPrefab();
            Collider[] colliders =
                prefab.GetComponentsInChildren<Collider>(true);
            Assert.That(colliders, Has.Length.EqualTo(2));
            Assert.That(
                colliders.All(collider =>
                    collider is BoxCollider &&
                    collider.transform == prefab.transform),
                Is.True);

            KingdomBuildingLevelModel model =
                prefab.GetComponent<KingdomBuildingLevelModel>();
            AssertBox(
                model.SelectionCollider,
                true,
                new Vector3(0.15f, 3.5f, -0.3f),
                new Vector3(13f, 7f, 12f));
            AssertBox(
                model.NavigationCollider,
                false,
                new Vector3(0.15f, 0.9f, 0.1f),
                new Vector3(12.4f, 1.8f, 10.8f));
        }

        [Test]
        public void LevelTenVeiledAccordYokeStaysCompactLoadedAndOpen()
        {
            GameObject prefab = LoadPrefab();
            MeshRenderer renderer = prefab.transform
                .Find("LOD0/L10_Delta")
                .GetComponent<MeshRenderer>();
            Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.subMeshCount, Is.EqualTo(1));
            Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(1));
            Assert.That(
                AssetDatabase.GetAssetPath(renderer.sharedMaterials[0]),
                Is.EqualTo(AtlasMaterialPath));
            Assert.That(
                TriangleCount(mesh),
                Is.EqualTo(480),
                "Four load rails, two four-part yoke frames, and four " +
                "connectors must remain the complete capstone.");
            Assert.That(mesh.bounds.max.y, Is.EqualTo(6.92f).Within(0.02f));
            Assert.That(mesh.bounds.size.x, Is.GreaterThan(9.3f));
            Assert.That(mesh.bounds.size.y, Is.LessThan(2.3f));
            Vector3[] loadVertices = mesh.vertices
                .Where(vertex => vertex.y <= 5.1f)
                .ToArray();
            Assert.That(
                loadVertices.Any(vertex => vertex.x <= -4.2f),
                Is.True);
            Assert.That(
                loadVertices.Any(vertex => vertex.x >= 4.5f),
                Is.True);
            Vector3[] frontSlitVertices = mesh.vertices
                .Where(vertex =>
                    vertex.x >= -0.1f &&
                    vertex.x <= 0.4f &&
                    vertex.y >= 6.18f &&
                    vertex.y <= 6.42f &&
                    Mathf.Abs(vertex.z - 0.1f) <= 0.2f)
                .ToArray();
            Assert.That(
                frontSlitVertices,
                Is.Empty,
                "The front council slit must remain truly empty.");
            Vector3[] rearSlitVertices = mesh.vertices
                .Where(vertex =>
                    vertex.x >= 0.15f &&
                    vertex.x <= 0.65f &&
                    vertex.y >= 6.3f &&
                    vertex.y <= 6.54f &&
                    Mathf.Abs(vertex.z - 1.35f) <= 0.2f)
                .ToArray();
            Assert.That(
                rearSlitVertices,
                Is.Empty,
                "The rear council slit must remain truly empty.");
            Assert.That(
                renderer.sharedMaterials.Any(material =>
                    AssetDatabase.GetAssetPath(material) ==
                    AccentMaterialPath),
                Is.False,
                "The fixed yoke stays structural and carries no glow accent.");
            Assert.That(
                mesh.bounds.center.z,
                Is.EqualTo(0.17f).Within(0.03f),
                "The compact yoke must stay close above the occupied roof.");
        }

        [Test]
        public void LevelFiveEstablishesExactlyFourGroundedBoundaryPiers()
        {
            Mesh mesh = LoadPrefab().transform
                .Find("LOD0/L05_Delta")
                .GetComponent<MeshFilter>()
                .sharedMesh;
            Vector3[] expectedPositions =
            {
                new Vector3(-4.35f, 0f, -2.75f),
                new Vector3(4.65f, 0f, -2.75f),
                new Vector3(-4.05f, 0f, 3.08f),
                new Vector3(4.95f, 0f, 3.08f)
            };
            foreach (Vector3 position in expectedPositions)
            {
                Vector3[] pierVertices = mesh.vertices
                    .Where(vertex =>
                        Mathf.Abs(vertex.x - position.x) <= 0.65f &&
                        Mathf.Abs(vertex.z - position.z) <= 0.65f &&
                        vertex.y >= 0.2f &&
                        vertex.y <= 4.85f)
                    .ToArray();
                Assert.That(
                    pierVertices,
                    Is.Not.Empty,
                    $"The grounded boundary pier at {position} is missing.");
            }
            Assert.That(mesh.bounds.max.y, Is.LessThan(4.9f));
        }

        [Test]
        public void ProductionPrefabOwnsStableCivicAnchors()
        {
            GameObject prefab = LoadPrefab();
            string[] anchors =
            {
                "Entrance",
                "CameraFocus",
                "Activity_00",
                "Output_00",
                "Occlusion_Roof",
                "Occlusion_Canopies",
                "Occlusion_Crown"
            };
            Assert.That(
                anchors.All(name => prefab.transform.Find(name) != null),
                Is.True);
            Assert.That(
                prefab.transform.Find("Entrance").localPosition,
                Is.EqualTo(new Vector3(1.05f, 0f, -4.65f)));
        }

        [Test]
        public void ProductionPrefabRemainsStaticAndSourceIndependent()
        {
            GameObject prefab = LoadPrefab();
            Assert.That(
                prefab.GetComponentsInChildren<Animator>(true),
                Is.Empty);
            Assert.That(
                prefab.GetComponentsInChildren<ParticleSystem>(true),
                Is.Empty);
            Assert.That(
                prefab.GetComponentsInChildren<AudioSource>(true),
                Is.Empty);
            Assert.That(
                prefab.GetComponentsInChildren<Light>(true),
                Is.Empty);
            Assert.That(
                AssetDatabase.GetDependencies(PrefabPath, true),
                Has.None.StartsWith(ConceptSheetFolder));
            Assert.That(
                EditorBuildSettings.scenes.Select(scene => scene.path),
                Does.Not.Contain(PreviewScenePath));
        }

        [Test]
        public void LiveKingdomBindsConfirmedTownHallLevelDirectly()
        {
            var engineObject =
                new GameObject("UmbralProductionBinding.Engine");
            try
            {
                var engine =
                    engineObject.AddComponent<CityLayoutEngine>();
                engine.ConfigureModelCatalog(LoadCatalog());
                engine.AutoPlaceBuildings(
                    RealmId.Umbral,
                    new[] { ResolveTownHall(State(6)) });

                Transform root = RequireTownHallRoot();
                Transform production = root.Find("ProductionModel");
                Assert.That(production, Is.Not.Null);
                Assert.That(root.Find("Base"), Is.Null);
                KingdomBuildingLevelModel model =
                    production.GetComponent<KingdomBuildingLevelModel>();
                Assert.That(model.AppliedLevel, Is.EqualTo(6));
                Assert.That(
                    production.GetComponent<
                        KingdomBuildingConfirmedLevelTransition>(),
                    Is.Null);
                Assert.That(
                    production.Find("LOD0/L06_Delta").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    production.Find("LOD0/L07_Delta").gameObject.activeSelf,
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(engineObject);
            }
        }

        [Test]
        public void AdjacentConfirmedTownHallLevelUsesUmbralMotionOnce()
        {
            var engineObject =
                new GameObject("UmbralTownHallTransition.Engine");
            try
            {
                var engine =
                    engineObject.AddComponent<CityLayoutEngine>();
                engine.ConfigureModelCatalog(LoadCatalog());
                engine.AutoPlaceBuildings(
                    RealmId.Umbral,
                    new[] { ResolveTownHall(State(5)) });

                engine.AutoPlaceBuildings(
                    RealmId.Umbral,
                    new[] { ResolveTownHall(State(6)) });
                Transform production =
                    RequireTownHallRoot().Find("ProductionModel");
                KingdomBuildingConfirmedLevelTransition transition =
                    production.GetComponent<
                        KingdomBuildingConfirmedLevelTransition>();
                Assert.That(transition, Is.Not.Null);
                Assert.That(transition.ConfirmedLevel, Is.EqualTo(6));
                Assert.That(transition.IsAnimating, Is.True);

                engine.AutoPlaceBuildings(
                    RealmId.Umbral,
                    new[] { ResolveTownHall(State(6)) });
                production =
                    RequireTownHallRoot().Find("ProductionModel");
                Assert.That(
                    production.GetComponent<
                        KingdomBuildingConfirmedLevelTransition>(),
                    Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(engineObject);
            }
        }

        [Test]
        public void UpgradeKeepsConfirmedModelAndDoesNotRevealTargetDelta()
        {
            var engineObject =
                new GameObject("UmbralUpgradeBinding.Engine");
            try
            {
                var engine =
                    engineObject.AddComponent<CityLayoutEngine>();
                engine.ConfigureModelCatalog(LoadCatalog());
                BuildingState state = State(6);
                state.IsUpgrading = true;
                state.UpgradeCompleteTimestamp =
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 120;
                engine.AutoPlaceBuildings(
                    RealmId.Umbral,
                    new[] { ResolveTownHall(state) });

                Transform root = RequireTownHallRoot();
                Transform production = root.Find("ProductionModel");
                KingdomBuildingLevelModel model =
                    production.GetComponent<KingdomBuildingLevelModel>();
                Assert.That(model.AppliedLevel, Is.EqualTo(6));
                Assert.That(
                    production.Find("LOD0/L07_Delta").gameObject.activeSelf,
                    Is.False);
                Assert.That(root.Find("UpgradeBaseRing"), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(engineObject);
            }
        }

        [Test]
        public void UnbuiltTownHallKeepsReservedPlotWithoutLoadingModel()
        {
            var engineObject =
                new GameObject("UmbralUnbuiltBinding.Engine");
            try
            {
                var engine =
                    engineObject.AddComponent<CityLayoutEngine>();
                engine.ConfigureModelCatalog(LoadCatalog());
                KingdomBuildingPresentation presentation =
                    KingdomBuildingPresentationResolver
                        .Resolve(
                            RealmId.Umbral,
                            Array.Empty<BuildingState>())
                        .Single(item => item.BuildingId == "TownHall");
                engine.AutoPlaceBuildings(
                    RealmId.Umbral,
                    new[] { presentation });

                Transform root = RequireTownHallRoot();
                Assert.That(root.Find("ReservedSiteMarker"), Is.Not.Null);
                Assert.That(root.Find("ProductionModel"), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(engineObject);
            }
        }

        [Test]
        public void DeclaredInvalidBindingFailsVisiblyWithoutFallback()
        {
            var invalidCatalog =
                ScriptableObject.CreateInstance<
                    KingdomBuildingModelCatalog>();
            var engineObject =
                new GameObject("UmbralInvalidBinding.Engine");
            try
            {
                invalidCatalog.Configure(
                    new[]
                    {
                        new KingdomBuildingModelEntry(
                            ModelId,
                            RealmId.Umbral,
                            "TownHall",
                            null,
                            0.09f,
                            1,
                            10)
                    });
                var engine =
                    engineObject.AddComponent<CityLayoutEngine>();
                engine.ConfigureModelCatalog(invalidCatalog);
                engine.AutoPlaceBuildings(
                    RealmId.Umbral,
                    new[] { ResolveTownHall(State(6)) });

                Transform root = RequireTownHallRoot();
                Assert.That(
                    root.Find("ProductionModelUnavailable"),
                    Is.Not.Null);
                Assert.That(root.Find("ProductionModel"), Is.Null);
                Assert.That(root.Find("Base"), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(engineObject);
                UnityEngine.Object.DestroyImmediate(invalidCatalog);
            }
        }

        [Test]
        public void MismatchedStableModelIdentityFailsVisibly()
        {
            var invalidCatalog =
                ScriptableObject.CreateInstance<
                    KingdomBuildingModelCatalog>();
            var engineObject =
                new GameObject("UmbralMismatchedBinding.Engine");
            try
            {
                invalidCatalog.Configure(
                    new[]
                    {
                        new KingdomBuildingModelEntry(
                            "building.umbral.townhall.wrong",
                            RealmId.Umbral,
                            "TownHall",
                            LoadPrefab(),
                            0.09f,
                            1,
                            10)
                    });
                Assert.That(
                    invalidCatalog.Validate(
                        out string diagnosticCode),
                    Is.False);
                Assert.That(
                    diagnosticCode,
                    Is.EqualTo(
                        KingdomBuildingModelCatalog
                            .InvalidBindingDiagnostic));

                var engine =
                    engineObject.AddComponent<CityLayoutEngine>();
                engine.ConfigureModelCatalog(invalidCatalog);
                engine.AutoPlaceBuildings(
                    RealmId.Umbral,
                    new[] { ResolveTownHall(State(6)) });

                Transform root = RequireTownHallRoot();
                Assert.That(
                    root.Find("ProductionModelUnavailable"),
                    Is.Not.Null);
                Assert.That(root.Find("ProductionModel"), Is.Null);
                Assert.That(root.Find("Base"), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(engineObject);
                UnityEngine.Object.DestroyImmediate(invalidCatalog);
            }
        }

        private static GameObject LoadPrefab()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);
            return prefab;
        }

        private static KingdomBuildingModelCatalog LoadCatalog()
        {
            KingdomBuildingModelCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    KingdomBuildingModelCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            return catalog;
        }

        private static KingdomBuildingPresentation ResolveTownHall(
            BuildingState state)
        {
            return KingdomBuildingPresentationResolver
                .Resolve(RealmId.Umbral, new[] { state })
                .Single(item => item.BuildingId == "TownHall");
        }

        private static BuildingState State(int level)
        {
            return new BuildingState
            {
                BuildingId = "TownHall",
                Level = level,
                IsUpgrading = false,
                UpgradeCompleteTimestamp = 0
            };
        }

        private static Transform RequireTownHallRoot()
        {
            GameObject board = GameObject.Find("Kingdom_CityBoard");
            Assert.That(board, Is.Not.Null);
            Transform root =
                board.transform.Find("Building_kingdom.slot.town-hall");
            Assert.That(root, Is.Not.Null);
            return root;
        }

        private static int TriangleCount(Mesh mesh)
        {
            if (mesh == null)
            {
                return 0;
            }

            int triangles = 0;
            for (int index = 0; index < mesh.subMeshCount; index++)
            {
                triangles += (int)mesh.GetIndexCount(index) / 3;
            }
            return triangles;
        }

        private static void AssertBox(
            BoxCollider collider,
            bool isTrigger,
            Vector3 center,
            Vector3 size)
        {
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.isTrigger, Is.EqualTo(isTrigger));
            Assert.That(collider.center, Is.EqualTo(center));
            Assert.That(collider.size, Is.EqualTo(size));
        }

        private static void DestroyIfPresent(string objectName)
        {
            GameObject existing = GameObject.Find(objectName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
        }
    }
}
