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
    public sealed class StoneholdWorkshopProductionModelTests
    {
        private const string PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Stonehold/Production/" +
            "Runtime/Stonehold_Workshop_Production.prefab";
        private const string CatalogPath =
            "Assets/AL/ScriptableObjects/Resources/" +
            "KingdomBuildingModelCatalog.asset";
        private const string AtlasPath =
            "Assets/AL/Art/Generated/Architecture/Stonehold/Production/" +
            "Runtime/T_Stonehold_Workshop_Atlas_1024.png";
        private const string AtlasMaterialPath =
            "Assets/AL/Art/Generated/Architecture/Stonehold/Production/" +
            "Runtime/MAT_Stonehold_Workshop_Atlas.mat";
        private const string AccentMaterialPath =
            "Assets/AL/Art/Generated/Architecture/Stonehold/Production/" +
            "Runtime/MAT_Stonehold_Workshop_Accent.mat";
        private const string PreviewScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "StoneholdWorkshopProductionModel.unity";
        private const string ConceptSheetFolder =
            "Assets/AL/Art/Architecture/ConceptSheets";
        private const string ModelId =
            "building.stonehold.workshop.production.v1";

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
        public void PackagedCatalogDeclaresExactLiveWorkshopBinding()
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
                    candidate.BuildingId == "Workshop");
            Assert.That(entry.ModelId, Is.EqualTo(ModelId));
            Assert.That(entry.Prefab, Is.EqualTo(LoadPrefab()));
            Assert.That(entry.StrategicBoardScale, Is.EqualTo(0.12f));
            Assert.That(entry.MinimumLevel, Is.EqualTo(1));
            Assert.That(entry.MaximumLevel, Is.EqualTo(10));
            Assert.That(entry.SupportsLevel(0), Is.False);
            Assert.That(entry.SupportsLevel(1), Is.True);
            Assert.That(entry.SupportsLevel(10), Is.True);
            Assert.That(
                catalog.Entries.Any(candidate =>
                    candidate.RealmId == RealmId.Eldergrove &&
                    candidate.BuildingId == "Workshop"),
                Is.True,
                "Adding Stonehold must preserve the Eldergrove binding.");
        }

        [Test]
        public void ProductionPrefabOwnsStableIdentityEnvelopeAndTenDeltas()
        {
            KingdomBuildingLevelModel model =
                LoadPrefab().GetComponent<KingdomBuildingLevelModel>();
            Assert.That(model, Is.Not.Null);
            Assert.That(model.IsConfigured, Is.True);
            Assert.That(model.ModelId, Is.EqualTo(ModelId));
            Assert.That(model.BuildingId, Is.EqualTo("Workshop"));
            Assert.That(model.MaximumLevel, Is.EqualTo(10));
            Assert.That(
                model.SlotEnvelope,
                Is.EqualTo(new Vector3(10f, 6.8f, 8f)));
            Assert.That(
                model.MaximumArtBounds,
                Is.EqualTo(new Vector3(9.2f, 6.6f, 6.8f)));
            Assert.That(model.LevelDeltas, Has.Length.EqualTo(10));
            Assert.That(
                model.LevelDeltas.Select(delta => delta.MinimumLevel),
                Is.EqualTo(Enumerable.Range(1, 10)));
        }

        [TestCase(1, 6.40f, 5.20f, 5.40f, 6.60f, 5.30f, 5.60f)]
        [TestCase(6, 8.03f, 6.13f, 6.64f, 8.20f, 6.20f, 6.70f)]
        [TestCase(10, 9.18f, 6.48f, 6.64f, 9.20f, 6.60f, 6.80f)]
        public void RepresentativeLevelBoundsStayInsideFinalEnvelope(
            int level,
            float expectedWidth,
            float expectedHeight,
            float expectedDepth,
            float maximumWidth,
            float maximumHeight,
            float maximumDepth)
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
                Assert.That(bounds.size.x, Is.LessThanOrEqualTo(maximumWidth));
                Assert.That(bounds.size.y, Is.LessThanOrEqualTo(maximumHeight));
                Assert.That(bounds.size.z, Is.LessThanOrEqualTo(maximumDepth));
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
            int[] expectedTriangles = { 1872, 912, 504, 276 };
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
            }

            Assert.That(
                expectedTriangles[1],
                Is.LessThanOrEqualTo(expectedTriangles[0] * 0.65f));
            Assert.That(
                expectedTriangles[2],
                Is.LessThanOrEqualTo(expectedTriangles[0] * 0.35f));
            Assert.That(
                expectedTriangles[3],
                Is.LessThanOrEqualTo(expectedTriangles[0] * 0.15f));
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
                new Vector3(0f, 3.3f, 0f),
                new Vector3(9.4f, 6.6f, 7.2f));
            AssertBox(
                model.NavigationCollider,
                false,
                new Vector3(0f, 0.75f, 0f),
                new Vector3(9.0f, 1.5f, 6.4f));
        }

        [Test]
        public void LevelTenCapstoneOwnsStructureAndContainedEmberAccent()
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
            Assert.That(mesh.bounds.max.y, Is.EqualTo(6.48f).Within(0.02f));
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
                prefab.GetComponentsInChildren<
                    StoneholdWorkshopStableActivity>(true),
                Is.Empty);
            Assert.That(
                AssetDatabase.GetDependencies(PrefabPath, true),
                Has.None.StartsWith(ConceptSheetFolder));
            Assert.That(
                EditorBuildSettings.scenes.Select(scene => scene.path),
                Does.Not.Contain(PreviewScenePath));
        }

        [Test]
        public void LiveKingdomBindsConfirmedWorkshopLevelDirectly()
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
                    new[] { ResolveWorkshop(State(6)) });

                Transform root = RequireWorkshopRoot();
                Transform production = root.Find("ProductionModel");
                Assert.That(production, Is.Not.Null);
                Assert.That(root.Find("Base"), Is.Null);
                KingdomBuildingLevelModel model =
                    production.GetComponent<KingdomBuildingLevelModel>();
                Assert.That(model.AppliedLevel, Is.EqualTo(6));
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
                    new[] { ResolveWorkshop(state) });

                Transform root = RequireWorkshopRoot();
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
        public void UnbuiltWorkshopKeepsReservedPlotWithoutLoadingModel()
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
                        .Single(item => item.BuildingId == "Workshop");
                engine.AutoPlaceBuildings(
                    RealmId.Stonehold,
                    new[] { presentation });

                Transform root = RequireWorkshopRoot();
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
                            "Workshop",
                            null,
                            0.12f,
                            1,
                            10)
                    });
                var engine =
                    engineObject.AddComponent<CityLayoutEngine>();
                engine.ConfigureModelCatalog(invalidCatalog);
                engine.AutoPlaceBuildings(
                    RealmId.Stonehold,
                    new[] { ResolveWorkshop(State(6)) });

                Transform root = RequireWorkshopRoot();
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
                            "building.stonehold.workshop.wrong",
                            RealmId.Stonehold,
                            "Workshop",
                            LoadPrefab(),
                            0.12f,
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
                    new[] { ResolveWorkshop(State(6)) });

                Transform root = RequireWorkshopRoot();
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

        private static KingdomBuildingPresentation ResolveWorkshop(
            BuildingState state)
        {
            return KingdomBuildingPresentationResolver
                .Resolve(RealmId.Stonehold, new[] { state })
                .Single(item => item.BuildingId == "Workshop");
        }

        private static BuildingState State(int level)
        {
            return new BuildingState
            {
                BuildingId = "Workshop",
                Level = level,
                IsUpgrading = false,
                UpgradeCompleteTimestamp = 0
            };
        }

        private static Transform RequireWorkshopRoot()
        {
            GameObject board = GameObject.Find("Kingdom_CityBoard");
            Assert.That(board, Is.Not.Null);
            Transform root =
                board.transform.Find("Building_kingdom.slot.workshop");
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
