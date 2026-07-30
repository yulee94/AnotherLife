using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AL.RealmWar.Territories.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AL.Tests.PlayMode
{
    public sealed class TerritoryLoadDegradationStressTests
    {
        private const string StressRootName = "TerritoryLoadStressRoot";
        private readonly List<GameObject> _roots = new List<GameObject>();
        private readonly List<Object> _ownedAssets = new List<Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject root in _roots)
            {
                if (root != null)
                {
                    Object.Destroy(root);
                }
            }

            _roots.Clear();
            foreach (Object ownedAsset in _ownedAssets)
            {
                if (ownedAsset != null)
                {
                    Object.Destroy(ownedAsset);
                }
            }

            _ownedAssets.Clear();
            yield return null;
            Assert.IsNull(GameObject.Find(StressRootName), "Stress fixtures must not leak into later tests.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator OneHundredRenderedUsersStayRepresentedAcrossCriticalHeavyCycles()
        {
            StressFixture fixture = CreateCrowdFixture(100);
            int[] participantIds = fixture.Participants.Select(participant => participant.GetInstanceID()).ToArray();

            Assert.AreEqual(TerritoryLoadLevel.Heavy, fixture.Controller.CurrentLevel);
            AssertTierCounts(fixture.Participants, 12, 20, 20, 48, 0);
            Assert.AreEqual(100, CountActiveRenderers(fixture.Root));
            Assert.AreEqual(32, CountActiveAnimators(fixture.Root));

            for (int cycle = 0; cycle < 12; cycle++)
            {
                Assert.IsTrue(fixture.Controller.ProcessSample(0, 60f, 0.5f));
                Assert.AreEqual(TerritoryLoadLevel.Critical, fixture.Controller.CurrentLevel);
                AssertTierCounts(fixture.Participants, 8, 12, 16, 64, 0);
                Assert.AreEqual(100, CountActiveRenderers(fixture.Root));
                Assert.AreEqual(20, CountActiveAnimators(fixture.Root));

                Assert.IsTrue(fixture.Controller.ProcessSample(0, 16f, 3f));
                Assert.AreEqual(TerritoryLoadLevel.Heavy, fixture.Controller.CurrentLevel);
                AssertTierCounts(fixture.Participants, 12, 20, 20, 48, 0);
                Assert.That(fixture.Participants.All(participant => participant.ActiveRepresentationCount == 1));
                Assert.AreEqual(100, CountActiveRenderers(fixture.Root));
                Assert.AreEqual(32, CountActiveAnimators(fixture.Root));
            }

            CollectionAssert.AreEqual(
                participantIds,
                fixture.Participants.Select(participant => participant.GetInstanceID()).ToArray(),
                "Degradation must reuse existing participant objects instead of spawning replacement waves.");
            Assert.AreEqual(100, fixture.Root.GetComponentsInChildren<TerritoryCrowdParticipant>(true).Length);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DistanceRankingKeepsNearestUsersAtHighestDetail()
        {
            StressFixture fixture = CreateCrowdFixture(100);

            Assert.AreEqual(TerritoryRenderTier.FullDetail, fixture.Participants[0].CurrentTier);
            Assert.AreEqual(TerritoryRenderTier.FullDetail, fixture.Participants[11].CurrentTier);
            Assert.AreEqual(TerritoryRenderTier.MediumDetail, fixture.Participants[12].CurrentTier);
            Assert.AreEqual(TerritoryRenderTier.MediumDetail, fixture.Participants[31].CurrentTier);
            Assert.AreEqual(TerritoryRenderTier.LowDetail, fixture.Participants[32].CurrentTier);
            Assert.AreEqual(TerritoryRenderTier.LowDetail, fixture.Participants[51].CurrentTier);
            Assert.AreEqual(TerritoryRenderTier.Impostor, fixture.Participants[52].CurrentTier);
            Assert.AreEqual(TerritoryRenderTier.Impostor, fixture.Participants[99].CurrentTier);
            yield return null;
        }

        [UnityTest]
        public IEnumerator IncompleteRepresentationSetIsRejectedBeforeRegistration()
        {
            GameObject root = TrackRoot(new GameObject(StressRootName));
            GameObject controllerObject = new GameObject("TerritoryLoadController");
            controllerObject.transform.SetParent(root.transform, false);
            TerritoryLoadDegradationController controller =
                controllerObject.AddComponent<TerritoryLoadDegradationController>();

            GameObject participantObject = new GameObject("IncompleteParticipant");
            participantObject.transform.SetParent(root.transform, false);
            TerritoryCrowdParticipant participant =
                participantObject.AddComponent<TerritoryCrowdParticipant>();

            Assert.Throws<System.InvalidOperationException>(() => controller.Register(participant));
            Assert.AreEqual(0, controller.CurrentPlan.VisibleUserCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CriticalLoadDropsDecorativeEffectsAndRecoversAuthoredState()
        {
            GameObject root = TrackRoot(new GameObject(StressRootName));
            GameObject particlesObject = new GameObject("DecorativeParticles");
            particlesObject.transform.SetParent(root.transform, false);
            ParticleSystem particles = particlesObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTimeMultiplier = 12f;
            emission.rateOverDistanceMultiplier = 3f;

            GameObject lightObject = new GameObject("DecorativeLight");
            lightObject.transform.SetParent(root.transform, false);
            Light decorativeLight = lightObject.AddComponent<Light>();
            decorativeLight.enabled = true;

            LODGroup lodGroup = root.AddComponent<LODGroup>();
            lodGroup.SetLODs(new[]
            {
                new LOD(0.60f, new Renderer[0]),
                new LOD(0.30f, new Renderer[0]),
                new LOD(0.10f, new Renderer[0])
            });

            TerritoryLoadVisualAdapter adapter = root.AddComponent<TerritoryLoadVisualAdapter>();
            adapter.Configure(
                new[] { particles },
                new ParticleSystem[0],
                new[] { decorativeLight },
                new[] { lodGroup });

            adapter.Apply(TerritoryLoadDegradationPlanner.CreateBudget(TerritoryLoadLevel.Critical));

            emission = particles.emission;
            Assert.AreEqual(0f, emission.rateOverTimeMultiplier);
            Assert.AreEqual(0f, emission.rateOverDistanceMultiplier);
            Assert.IsFalse(decorativeLight.enabled);
            Assert.AreEqual(TerritoryLoadLevel.Critical, adapter.LastAppliedLevel);
            Assert.AreEqual(0.96f, lodGroup.GetLODs()[0].screenRelativeTransitionHeight, 0.001f);
            Assert.AreEqual(0.16f, lodGroup.GetLODs()[2].screenRelativeTransitionHeight, 0.001f);

            adapter.Apply(TerritoryLoadDegradationPlanner.CreateBudget(TerritoryLoadLevel.Normal));

            emission = particles.emission;
            Assert.AreEqual(12f, emission.rateOverTimeMultiplier);
            Assert.AreEqual(3f, emission.rateOverDistanceMultiplier);
            Assert.IsTrue(decorativeLight.enabled);
            Assert.AreEqual(TerritoryLoadLevel.Normal, adapter.LastAppliedLevel);
            Assert.AreEqual(0.60f, lodGroup.GetLODs()[0].screenRelativeTransitionHeight, 0.001f);
            Assert.AreEqual(0.10f, lodGroup.GetLODs()[2].screenRelativeTransitionHeight, 0.001f);
            yield return null;
        }

        private StressFixture CreateCrowdFixture(int participantCount)
        {
            GameObject root = TrackRoot(new GameObject(StressRootName));
            GameObject observerObject = new GameObject("Observer");
            observerObject.transform.SetParent(root.transform, false);
            observerObject.transform.position = Vector3.zero;
            observerObject.AddComponent<Camera>();

            GameObject controllerObject = new GameObject("TerritoryLoadController");
            controllerObject.transform.SetParent(root.transform, false);
            TerritoryLoadDegradationController controller =
                controllerObject.AddComponent<TerritoryLoadDegradationController>();
            controller.Configure(observerObject.transform, null);

            var participants = new List<TerritoryCrowdParticipant>(participantCount);
            Mesh sharedMesh = CreateStressMesh();
            for (int index = 0; index < participantCount; index++)
            {
                GameObject participantObject = new GameObject($"SyntheticUser_{index:000}");
                participantObject.transform.SetParent(root.transform, false);
                participantObject.transform.position = new Vector3(4f + index * 0.25f, 0f, 0f);

                GameObject full = CreateRepresentation(participantObject.transform, "FullDetail", true, true, sharedMesh);
                GameObject medium = CreateRepresentation(participantObject.transform, "MediumDetail", false, true, sharedMesh);
                GameObject low = CreateRepresentation(participantObject.transform, "LowDetail", false, false, sharedMesh);
                GameObject impostor = CreateRepresentation(participantObject.transform, "Impostor", false, false, sharedMesh);

                TerritoryCrowdParticipant participant =
                    participantObject.AddComponent<TerritoryCrowdParticipant>();
                participant.Configure(full, medium, low, impostor);
                participants.Add(participant);
            }

            int applicationsBeforeBurst = controller.PlanApplicationCount;
            controller.RegisterRange(participants);
            Assert.AreEqual(
                applicationsBeforeBurst + 1,
                controller.PlanApplicationCount,
                "A 100-user registration burst must produce one crowd plan application.");

            return new StressFixture(root, controller, participants);
        }

        private GameObject TrackRoot(GameObject root)
        {
            _roots.Add(root);
            return root;
        }

        private Mesh CreateStressMesh()
        {
            var mesh = new Mesh
            {
                name = "TerritoryStressTriangle",
                vertices = new[]
                {
                    new Vector3(-0.5f, 0f, 0f),
                    new Vector3(0.5f, 0f, 0f),
                    new Vector3(0f, 1f, 0f)
                },
                triangles = new[] { 0, 1, 2 }
            };
            mesh.RecalculateBounds();
            _ownedAssets.Add(mesh);
            return mesh;
        }

        private static GameObject CreateRepresentation(
            Transform parent,
            string name,
            bool active,
            bool animated,
            Mesh sharedMesh)
        {
            var representation = new GameObject(name);
            representation.transform.SetParent(parent, false);
            representation.AddComponent<MeshRenderer>();
            MeshFilter meshFilter = representation.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = sharedMesh;
            if (animated)
            {
                representation.AddComponent<Animator>();
            }

            representation.SetActive(active);
            return representation;
        }

        private static int CountActiveRenderers(GameObject root)
        {
            return root.GetComponentsInChildren<Renderer>(false).Length;
        }

        private static int CountActiveAnimators(GameObject root)
        {
            return root.GetComponentsInChildren<Animator>(false).Count(animator => animator.enabled);
        }

        private static void AssertTierCounts(
            IReadOnlyCollection<TerritoryCrowdParticipant> participants,
            int full,
            int medium,
            int low,
            int impostor,
            int culled)
        {
            Assert.AreEqual(full, participants.Count(item => item.CurrentTier == TerritoryRenderTier.FullDetail));
            Assert.AreEqual(medium, participants.Count(item => item.CurrentTier == TerritoryRenderTier.MediumDetail));
            Assert.AreEqual(low, participants.Count(item => item.CurrentTier == TerritoryRenderTier.LowDetail));
            Assert.AreEqual(impostor, participants.Count(item => item.CurrentTier == TerritoryRenderTier.Impostor));
            Assert.AreEqual(culled, participants.Count(item => item.CurrentTier == TerritoryRenderTier.Culled));
        }

        private sealed class StressFixture
        {
            public StressFixture(
                GameObject root,
                TerritoryLoadDegradationController controller,
                List<TerritoryCrowdParticipant> participants)
            {
                Root = root;
                Controller = controller;
                Participants = participants;
            }

            public GameObject Root { get; }
            public TerritoryLoadDegradationController Controller { get; }
            public List<TerritoryCrowdParticipant> Participants { get; }
        }
    }
}
