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
    public sealed class StoneholdTownHallProductionModelTests
    {
        private const string PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Stonehold/Production/" +
            "TownHall/Runtime/Stonehold_TownHall_Production.prefab";
        private const string CatalogPath =
            "Assets/AL/ScriptableObjects/Resources/" +
            "KingdomBuildingModelCatalog.asset";
        private const string AtlasPath =
            "Assets/AL/Art/Generated/Architecture/Stonehold/Production/" +
            "TownHall/Runtime/T_Stonehold_TownHall_Atlas_1024.png";
        private const string AtlasMaterialPath =
            "Assets/AL/Art/Generated/Architecture/Stonehold/Production/" +
            "TownHall/Runtime/MAT_Stonehold_TownHall_Atlas.mat";
        private const string AccentMaterialPath =
            "Assets/AL/Art/Generated/Architecture/Stonehold/Production/" +
            "TownHall/Runtime/MAT_Stonehold_TownHall_Accent.mat";
        private const string PreviewScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "StoneholdTownHallProductionModel.unity";
        private const string ConceptSheetFolder =
            "Assets/AL/Art/Architecture/ConceptSheets";
        private const string ModelId =
            "building.stonehold.townhall.production.v1";

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

            KingdomBuildingModelEntry entry = catalog.Entries.Single(
                candidate =>
                    candidate.RealmId == RealmId.Stonehold &&
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
                    candidate.RealmId == RealmId.Stonehold &&
                    candidate.BuildingId == "Workshop"),
                Is.True,
                "Adding Town Hall must preserve the Workshop binding.");
            Assert.That(entry.RealmMotionProfile, Is.Not.Null);
            Assert.That(entry.HasCompatibleRealmMotionProfile, Is.True);
            Assert.That(
                entry.RealmMotionProfile.ProfileId,
                Is.EqualTo("stonehold.workshop"));
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

        [TestCase(1, 9.35f, 6.33f, 8.84f)]
        [TestCase(6, 12.66f, 6.74f, 9.09f)]
        [TestCase(10, 12.66f, 10.45f, 10.66f)]
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
            int[] expectedTriangles = { 1368, 1052, 684, 564 };
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
                Is.True);
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
                new Vector3(0f, 6.1f, -0.2f),
                new Vector3(14.2f, 12.2f, 12.9f));
            AssertBox(
                model.NavigationCollider,
                false,
                new Vector3(0f, 1f, -0.1f),
                new Vector3(12.8f, 2f, 10.8f));
        }

        [Test]
        public void LevelTenOathstoneCrownOwnsContainedAmberAccent()
        {
            GameObject prefab = LoadPrefab();
            MeshRenderer renderer = prefab.transform
                .Find("LOD0/L10_Delta")
                .GetComponent<MeshRenderer>();
            Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.subMeshCount, Is.EqualTo(2));
            Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(2));
            Assert.That(
                AssetDatabase.GetAssetPath(renderer.sharedMaterials[1]),
                Is.EqualTo(AccentMaterialPath));
            Assert.That(mesh.bounds.max.y, Is.EqualTo(10.45f).Within(0.02f));
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
                Is.EqualTo(new Vector3(0f, 0f, -5.65f)));
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
                new GameObject("StoneholdProductionBinding.Engine");
            try
            {
                var engine =
                    engineObject.AddComponent<CityLayoutEngine>();
                engine.ConfigureModelCatalog(LoadCatalog());
                engine.AutoPlaceBuildings(
                    RealmId.Stonehold,
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
        public void AdjacentConfirmedTownHallLevelUsesStoneholdMotionOnce()
        {
            var engineObject =
                new GameObject("StoneholdTownHallTransition.Engine");
            try
            {
                var engine =
                    engineObject.AddComponent<CityLayoutEngine>();
                engine.ConfigureModelCatalog(LoadCatalog());
                engine.AutoPlaceBuildings(
                    RealmId.Stonehold,
                    new[] { ResolveTownHall(State(5)) });

                engine.AutoPlaceBuildings(
                    RealmId.Stonehold,
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
                    RealmId.Stonehold,
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
                new GameObject("StoneholdUpgradeBinding.Engine");
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
                    RealmId.Stonehold,
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
                new GameObject("StoneholdUnbuiltBinding.Engine");
            try
            {
                var engine =
                    engineObject.AddComponent<CityLayoutEngine>();
                engine.ConfigureModelCatalog(LoadCatalog());
                KingdomBuildingPresentation presentation =
                    KingdomBuildingPresentationResolver
                        .Resolve(
                            RealmId.Stonehold,
                            Array.Empty<BuildingState>())
                        .Single(item => item.BuildingId == "TownHall");
                engine.AutoPlaceBuildings(
                    RealmId.Stonehold,
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
                new GameObject("StoneholdInvalidBinding.Engine");
            try
            {
                invalidCatalog.Configure(
                    new[]
                    {
                        new KingdomBuildingModelEntry(
                            ModelId,
                            RealmId.Stonehold,
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
                    RealmId.Stonehold,
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
                new GameObject("StoneholdMismatchedBinding.Engine");
            try
            {
                invalidCatalog.Configure(
                    new[]
                    {
                        new KingdomBuildingModelEntry(
                            "building.stonehold.townhall.wrong",
                            RealmId.Stonehold,
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
                    RealmId.Stonehold,
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
                .Resolve(RealmId.Stonehold, new[] { state })
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
