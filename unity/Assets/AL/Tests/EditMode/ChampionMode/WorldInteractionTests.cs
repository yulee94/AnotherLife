using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AL.ChampionMode;
using AL.ChampionMode.Interaction;
using AL.Core;
using AL.World;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class WorldInteractionTests
    {
        [Test]
        public void FirstSessionIdsAreAuthoredC1Objectives()
        {
            Assert.AreEqual("OBJ_C1_MEET_REALM_GUIDE", FirstSessionWorldInteractables.GuideCatalogId);
            Assert.AreEqual("OBJ_C1_RESTORE_COVENANT", FirstSessionWorldInteractables.CovenantSiteCatalogId);
            Assert.AreEqual("CaptainValerius", FirstSessionWorldInteractables.GuideObjectName);
            Assert.AreEqual("CovenantSite", FirstSessionWorldInteractables.CovenantSiteObjectName);
        }

        [Test]
        public void PromptCopyUsesTalkUseAndAuthoredSubjects()
        {
            Assert.AreEqual("Talk", WorldInteractionPromptCopy.Verb(WorldInteractionKind.Talk));
            Assert.AreEqual("Use", WorldInteractionPromptCopy.Verb(WorldInteractionKind.Use));
            Assert.AreEqual("Captain Valerius", WorldInteractionPromptCopy.Subject(FirstSessionWorldInteractables.GuideCatalogId));
            Assert.AreEqual(
                "Covenant Site",
                WorldInteractionPromptCopy.Subject(FirstSessionWorldInteractables.CovenantSiteCatalogId));
            Assert.AreEqual(
                "Speak with Captain Valerius about the Celestial Tear's response.",
                WorldInteractionPromptCopy.ObjectiveText(FirstSessionWorldInteractables.GuideCatalogId));
            Assert.AreEqual(
                "Restore the damaged covenant site without sacrificing its keepers.",
                WorldInteractionPromptCopy.ObjectiveText(FirstSessionWorldInteractables.CovenantSiteCatalogId));
            Assert.AreEqual(string.Empty, WorldInteractionPromptCopy.Subject("npc_invented_guide"));
            StringAssert.Contains("[F]", WorldInteractionPromptCopy.Compose(
                "F",
                WorldInteractionKind.Talk,
                FirstSessionWorldInteractables.GuideCatalogId));
            StringAssert.Contains("TALK", WorldInteractionPromptCopy.Compose(
                "F",
                WorldInteractionKind.Talk,
                FirstSessionWorldInteractables.GuideCatalogId));
            StringAssert.Contains("USE", WorldInteractionPromptCopy.Compose(
                "F",
                WorldInteractionKind.Use,
                FirstSessionWorldInteractables.CovenantSiteCatalogId));
        }

        [Test]
        public void FocusSelectsLookedAtTargetInsideRange()
        {
            var candidates = new List<WorldInteractionCandidate>
            {
                new WorldInteractionCandidate(
                    FirstSessionWorldInteractables.GuideCatalogId,
                    new Vector3(-3f, 1f, 2f),
                    WorldInteractionKind.Talk,
                    4.6f,
                    48f),
                new WorldInteractionCandidate(
                    FirstSessionWorldInteractables.CovenantSiteCatalogId,
                    new Vector3(3f, 1f, 2f),
                    WorldInteractionKind.Use,
                    4.6f,
                    48f)
            };

            Assert.IsTrue(WorldInteractionFocus.TrySelect(
                Vector3.up,
                new Vector3(-3f, 0f, 2f),
                candidates,
                out int guideIndex));
            Assert.AreEqual(0, guideIndex);

            Assert.IsTrue(WorldInteractionFocus.TrySelect(
                Vector3.up,
                new Vector3(3f, 0f, 2f),
                candidates,
                out int siteIndex));
            Assert.AreEqual(1, siteIndex);
        }

        [Test]
        public void FocusRejectsLookAwayAndOutOfRange()
        {
            var candidates = new List<WorldInteractionCandidate>
            {
                new WorldInteractionCandidate(
                    FirstSessionWorldInteractables.GuideCatalogId,
                    new Vector3(0f, 1f, 2f),
                    WorldInteractionKind.Talk,
                    4.6f,
                    48f)
            };

            Assert.IsFalse(WorldInteractionFocus.TrySelect(
                Vector3.up,
                Vector3.back,
                candidates,
                out _));
            Assert.IsFalse(WorldInteractionFocus.TrySelect(
                new Vector3(0f, 1f, -20f),
                Vector3.forward,
                candidates,
                out _));
        }

        [Test]
        public void FocusUsesHorizontalAimWhenGameplayCameraIsPitchedDown()
        {
            var candidates = new List<WorldInteractionCandidate>
            {
                new WorldInteractionCandidate(
                    FirstSessionWorldInteractables.GuideCatalogId,
                    new Vector3(0f, 1f, 3f),
                    WorldInteractionKind.Talk,
                    4.6f,
                    20f)
            };

            Vector3 pitchedCameraForward = Quaternion.Euler(30f, 0f, 0f) * Vector3.forward;
            Assert.IsTrue(WorldInteractionFocus.TrySelect(
                Vector3.up,
                pitchedCameraForward,
                candidates,
                out int selectedIndex));
            Assert.AreEqual(0, selectedIndex);
        }

        [Test]
        public void PolicyAllowsAvailableActorAndRejectsUnavailable()
        {
            Assert.IsTrue(WorldInteractionPolicy.CanConfirm(true));
            Assert.IsFalse(WorldInteractionPolicy.CanConfirm(false));
        }

        [Test]
        public void ConfirmTalkAndUseReturnAuthoredObjectiveText()
        {
            var host = new GameObject("InteractableHost");
            try
            {
                WorldInteractable talk = host.AddComponent<WorldInteractable>();
                talk.Configure(
                    FirstSessionWorldInteractables.GuideCatalogId,
                    WorldInteractionKind.Talk,
                    WorldInteractionPromptCopy.GuideSubject,
                    WorldInteractionPromptCopy.GuideObjectiveText);
                WorldInteractionResult talkResult = talk.Confirm(true);
                Assert.IsTrue(talkResult.Accepted);
                Assert.AreEqual(FirstSessionWorldInteractables.GuideCatalogId, talkResult.CatalogId);
                Assert.AreEqual(WorldInteractionKind.Talk, talkResult.Kind);
                Assert.AreEqual(WorldInteractionPromptCopy.GuideObjectiveText, talkResult.Feedback);
                Assert.AreEqual(1, talk.ConfirmCount);

                WorldInteractable use = host.AddComponent<WorldInteractable>();
                use.Configure(
                    FirstSessionWorldInteractables.CovenantSiteCatalogId,
                    WorldInteractionKind.Use,
                    WorldInteractionPromptCopy.CovenantSiteSubject,
                    WorldInteractionPromptCopy.CovenantObjectiveText);
                WorldInteractionResult useResult = use.Confirm(true);
                Assert.IsTrue(useResult.Accepted);
                Assert.AreEqual(FirstSessionWorldInteractables.CovenantSiteCatalogId, useResult.CatalogId);
                Assert.AreEqual(WorldInteractionKind.Use, useResult.Kind);
                Assert.AreEqual(WorldInteractionPromptCopy.CovenantObjectiveText, useResult.Feedback);

                Assert.IsFalse(talk.Confirm(false).Accepted);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PromptPresentsAuthoredConfirmationWithoutStaleInteractGlyph()
        {
            WorldInteractionPromptView view =
                WorldInteractionPromptView.Create(null, null);
            try
            {
                view.Show(WorldInteractionPromptCopy.Compose(
                    WorldInteractionPromptCopy.InteractGlyph,
                    WorldInteractionKind.Talk,
                    FirstSessionWorldInteractables.GuideCatalogId));
                Transform glyph = view.transform.Find(
                    WorldInteractionPromptView.PlateName + "/" +
                    WorldInteractionPromptView.GlyphName);
                Assert.NotNull(glyph);
                Assert.IsTrue(glyph.gameObject.activeSelf);

                view.ShowFeedback(WorldInteractionPromptCopy.GuideObjectiveText);
                Assert.IsTrue(view.IsVisible);
                Assert.AreEqual(
                    WorldInteractionPromptCopy.GuideObjectiveText,
                    view.CurrentCopy);
                Assert.IsFalse(glyph.gameObject.activeSelf,
                    "Accepted feedback must not retain an actionable F glyph.");
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void DirectorPresentsAuthoredFeedbackAfterFocusedConfirmation()
        {
            var player = new GameObject("Player_Champion");
            var cameraObject = new GameObject("Main Camera");
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            var targetObject = new GameObject("RealmGuide");
            var directorObject = new GameObject("WorldInteractionDirector");
            WorldInteractionPromptView prompt = WorldInteractionPromptView.Create(
                directorObject.transform,
                null);
            WorldInteractionDirector director = directorObject.AddComponent<WorldInteractionDirector>();
            try
            {
                player.transform.position = Vector3.zero;
                camera.transform.rotation = Quaternion.identity;
                targetObject.transform.position = new Vector3(0f, 0f, 3f);
                WorldInteractable target = targetObject.AddComponent<WorldInteractable>();
                target.Configure(
                    FirstSessionWorldInteractables.GuideCatalogId,
                    WorldInteractionKind.Talk,
                    WorldInteractionPromptCopy.GuideSubject,
                    WorldInteractionPromptCopy.GuideObjectiveText);
                director.Configure(player.transform, camera, prompt);
                director.Register(target);

                MethodInfo refreshFocus = typeof(WorldInteractionDirector).GetMethod(
                    "RefreshFocus",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(refreshFocus);
                refreshFocus.Invoke(director, null);
                Assert.AreSame(target, director.Focused);

                Assert.IsTrue(director.TryConfirmFocused());
                Assert.AreEqual(1, target.ConfirmCount);
                Assert.IsTrue(prompt.IsVisible);
                Assert.AreEqual(WorldInteractionPromptCopy.GuideObjectiveText, prompt.CurrentCopy);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(directorObject);
            }
        }

        [Test]
        public void InstallPlacesGuideAndCovenantSiteOnFirstSessionPath()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            FirstSessionAuthoredRealmRoute route = CreateRoute(out GameObject realmRoot);
            var player = new GameObject(FirstSessionChampionStart.PlayerObjectName);
            player.transform.position = route.PlayerSpawn.position + Vector3.up * 1.05f;
            var cameraObject = new GameObject("Main Camera");
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.transform.position = player.transform.position + new Vector3(0f, 6.1f, -6f);
            camera.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
            try
            {
                WorldInteractionDirector director = FirstSessionWorldInteractables.Install(
                    player.transform,
                    camera,
                    RealmId.Crownlands,
                    route);
                Assert.NotNull(director);
                WorldInteractable[] spawned = Object.FindObjectsOfType<WorldInteractable>();
                Assert.AreEqual(2, spawned.Length);

                WorldInteractable guide = Find(spawned, FirstSessionWorldInteractables.GuideCatalogId);
                WorldInteractable site = Find(spawned, FirstSessionWorldInteractables.CovenantSiteCatalogId);
                Assert.NotNull(guide);
                Assert.NotNull(site);
                Assert.AreEqual(WorldInteractionKind.Talk, guide.Kind);
                Assert.AreEqual(WorldInteractionKind.Use, site.Kind);
                Assert.AreEqual(FirstSessionWorldInteractables.GuideObjectName, guide.gameObject.name);
                Assert.AreEqual(FirstSessionWorldInteractables.CovenantSiteObjectName, site.gameObject.name);
                Assert.NotNull(GameObject.Find(WorldInteractionPromptView.CanvasName));
                Assert.That(
                    HorizontalDistance(guide.transform.position, route.CaptainValerius.position),
                    Is.LessThan(0.05f));
                Assert.That(
                    HorizontalDistance(site.transform.position, route.CovenantSite.position),
                    Is.LessThan(0.05f));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(realmRoot);
                GameObject root = GameObject.Find(FirstSessionWorldInteractables.RootName);
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void InstalledGuideUsesAuthoredHumanoidAndVisibleLabel()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            FirstSessionAuthoredRealmRoute route = CreateRoute(out GameObject realmRoot);
            var player = new GameObject(FirstSessionChampionStart.PlayerObjectName);
            player.transform.position = route.PlayerSpawn.position + Vector3.up * 1.05f;
            var cameraObject = new GameObject("Main Camera");
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.transform.position = player.transform.position + new Vector3(0f, 6f, -6f);
            try
            {
                FirstSessionWorldInteractables.Install(
                    player.transform,
                    camera,
                    RealmId.Crownlands,
                    route);
                WorldInteractable guide = Find(
                    Object.FindObjectsOfType<WorldInteractable>(),
                    FirstSessionWorldInteractables.GuideCatalogId);

                Assert.NotNull(guide);
                SkinnedMeshRenderer authoredRenderer = guide
                    .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(renderer => renderer.enabled);
                Assert.That(
                    authoredRenderer,
                    Is.Not.Null,
                    "Captain Valerius must use an admitted authored humanoid visual.");
                Assert.That(
                    authoredRenderer.sharedMaterials.All(material =>
                        material != null && Mathf.Max(
                            material.color.r,
                            material.color.g,
                            material.color.b) >= 0.08f),
                    Is.True,
                    "Captain Valerius runtime materials must not collapse to black defaults.");
                TextMesh label = guide.GetComponentInChildren<TextMesh>(true);
                Assert.NotNull(label);
                Assert.AreEqual("CAPTAIN VALERIUS", label.text);
                WorldSpaceTextBillboard billboard =
                    label.GetComponent<WorldSpaceTextBillboard>();
                Assert.That(billboard, Is.Not.Null);
                billboard.Face(camera.transform);
                Vector3 awayFromCamera =
                    (label.transform.position - camera.transform.position).normalized;
                Assert.That(
                    Vector3.Dot(label.transform.forward, awayFromCamera),
                    Is.GreaterThan(0.98f));

                Light visibilityLight = guide.GetComponentInChildren<Light>(true);
                Assert.That(visibilityLight, Is.Not.Null);
                Assert.That(visibilityLight.range, Is.GreaterThanOrEqualTo(5f));
                Assert.That(visibilityLight.intensity, Is.GreaterThanOrEqualTo(1f));
                Assert.That(
                    authoredRenderer.sharedMaterials.Any(material =>
                        material != null && material.IsKeywordEnabled("_EMISSION") &&
                        material.GetColor("_EmissionColor").maxColorComponent >= 0.05f),
                    Is.True,
                    "Captain Valerius needs a restrained emissive armor accent.");
                Assert.That(authoredRenderer.bounds.size.y, Is.GreaterThanOrEqualTo(1.9f));
                Assert.That(
                    Mathf.Abs(authoredRenderer.bounds.min.y - route.CaptainValerius.position.y),
                    Is.LessThanOrEqualTo(0.15f));
                Assert.That(guide.GetComponent<MeshRenderer>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(realmRoot);
                GameObject root = GameObject.Find(FirstSessionWorldInteractables.RootName);
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void CaptainValeriusAnchorIsVisibleAlongTheAuthoredAvenueFromSpawn()
        {
            FirstSessionAuthoredRealmRoute route = CreateRoute(out GameObject realmRoot);
            try
            {
                Vector3 delta = route.CaptainValerius.position - route.PlayerSpawn.position;
                Assert.That(Mathf.Abs(delta.x), Is.LessThanOrEqualTo(1f));
                Assert.That(delta.z, Is.InRange(8f, 16f));
            }
            finally
            {
                Object.DestroyImmediate(realmRoot);
            }
        }

        private static FirstSessionAuthoredRealmRoute CreateRoute(out GameObject realmRoot)
        {
            FirstSessionAuthoredAssetCatalog catalog =
                Resources.Load<FirstSessionAuthoredAssetCatalog>(
                    FirstSessionAuthoredAssetCatalog.ResourcesPath);
            Assert.That(
                catalog.TryResolveFirstSessionRealm(
                    RealmId.Crownlands,
                    out GameObject prefab),
                Is.True);
            realmRoot = Object.Instantiate(prefab);
            return realmRoot.GetComponent<FirstSessionAuthoredRealmRoute>();
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static WorldInteractable Find(WorldInteractable[] spawned, string catalogId)
        {
            for (int i = 0; i < spawned.Length; i++)
            {
                if (spawned[i] != null && spawned[i].CatalogId == catalogId)
                {
                    return spawned[i];
                }
            }

            return null;
        }
    }
}
